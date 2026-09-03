#Requires -Version 7.0

<#
.SYNOPSIS
    Creates the CI identity that lets GitHub Actions deploy this solution to Azure, and configures
    the repository to use it.

.DESCRIPTION
    This is the one step that cannot come from a workflow: something has to create the identity the
    workflows authenticate as, before any workflow can run. Everything after this is GitHub Actions.

    What it creates, all of it idempotent - re-running against a configured tenant reports what
    already exists and changes nothing:

      Azure
        1. An Entra application registration and its service principal.
        2. Federated credentials binding that application to this repository, so GitHub can exchange
           a short-lived OIDC token for an Azure token. No client secret is ever created.
        3. Contributor and Role Based Access Control Administrator at subscription scope. Not Owner:
           the platform template creates role assignments, so CI needs roleAssignments/write, and
           this pair is the least-privilege way to have it.
        4. Registration of the resource providers the platform needs.

      GitHub
        5. The dev and prod environments, with required reviewers on prod.
        6. The identifiers the workflows read.

    Nothing secret is stored anywhere. With workload identity federation there is nothing to store:
    GitHub proves who it is per run, and the repository holds identifiers only. See -UseSecrets if
    your policy requires them masked anyway.

.PARAMETER SubscriptionId
    Target subscription. Defaults to the current az account.

.PARAMETER Repository
    owner/name of the GitHub repository. Defaults to the origin remote of this working tree.

.PARAMETER Location
    Azure region. Must be one where Foundry hosted agents are available.

.PARAMETER ModelVersion
    Model version to pin. Resolve a current one with:
      az cognitiveservices model list --location <region> --query "[?model.name=='gpt-5.5'].model.version" -o tsv

.PARAMETER ApplicationName
    Display name of the Entra application. Change it only if you are running more than one
    independent copy of this demo in the same tenant.

.PARAMETER Environments
    GitHub environments to configure.

.PARAMETER ProductionReviewer
    Set to any non-empty value to require review before a prod deployment; the signed-in GitHub user
    becomes the reviewer. Pass an empty string to leave prod ungated. Defaults to gated.

.PARAMETER UseSecrets
    Store the identifiers as GitHub secrets rather than variables. They are not credentials, so the
    only effect is that they are masked in workflow logs - which makes a failed deployment harder to
    diagnose. Provided because some organisations require it.

.EXAMPLE
    ./scripts/Bootstrap-GitHubOidc.ps1 -WhatIf
    Reports what would change and touches nothing. Run this first.

.EXAMPLE
    ./scripts/Bootstrap-GitHubOidc.ps1 -Location westus3 -ModelVersion 2026-04-24

.NOTES
    Undo with ./scripts/Remove-GitHubOidc.ps1. Full walkthrough in docs/deployment.md.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $SubscriptionId,
    [string] $Repository,
    [string] $Location = 'westus3',
    [Parameter(Mandatory)] [string] $ModelVersion,
    [string] $ApplicationName = 'caesarea-github-deploy',
    [string[]] $Environments = @('dev', 'prod'),
    [string] $ProductionReviewer,
    [switch] $UseSecrets
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Built-in role IDs. Resolve them yourself rather than trusting these constants - several Azure AI
# roles were renamed in 2026 while keeping their IDs:
#   az role definition list --name "Contributor" --query "[0].name" -o tsv
$ContributorRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
$RbacAdministratorRoleId = 'f58310d9-a9f6-439a-9e8d-f62e7b41a168'

$RequiredProviders = @(
    'Microsoft.CognitiveServices'
    'Microsoft.ContainerRegistry'
    'Microsoft.OperationalInsights'
    'Microsoft.Insights'
    'Microsoft.App'
    'Microsoft.ContainerService'
    'Microsoft.KeyVault'
)

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Exists { param([string] $Message) Write-Host "  [exists] $Message" -ForegroundColor DarkGray }
function Write-Created { param([string] $Message) Write-Host "  [created] $Message" -ForegroundColor Green }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

function Invoke-Native {
    <#  Runs a native command and throws on a non-zero exit code. Without this, a failed az call
        prints an error, returns nothing, and the script carries on building on the absence. #>
    param([scriptblock] $Command, [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

# ---------------------------------------------------------------------------------------------
# Preflight. Every prerequisite is checked before anything is created, so a missing tool fails
# immediately rather than half way through a tenant change.
# ---------------------------------------------------------------------------------------------

Write-Step 'Checking prerequisites'

foreach ($tool in @('az', 'gh')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool is not on PATH. See docs/deployment.md for the prerequisites."
    }
}
Write-Note 'az and gh are present.'

$accountJson = az account show --output json 2>$null
if (-not $accountJson) { throw 'Not signed in to Azure. Run: az login' }
$account = $accountJson | ConvertFrom-Json

if (-not $SubscriptionId) { $SubscriptionId = $account.id }
Write-Note "Subscription: $($account.name) ($SubscriptionId)"

$ghStatus = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) { throw 'Not signed in to GitHub. Run: gh auth login' }

if (-not $Repository) {
    $originUrl = git -C $PSScriptRoot/.. remote get-url origin 2>$null
    if (-not $originUrl) { throw 'No origin remote found. Pass -Repository owner/name.' }
    $Repository = ($originUrl -replace '^.*github\.com[:/]', '' -replace '\.git$', '')
}
Write-Note "Repository: $Repository"

if (-not $PSBoundParameters.ContainsKey('ProductionReviewer')) { $ProductionReviewer = 'signed-in-user' }

# The model version is pinned deliberately - a floating version changes agent behaviour with no
# deployment to point at - so it is verified here rather than discovered at deployment time.
$availableVersions = az cognitiveservices model list --location $Location `
    --query "[?model.name=='gpt-5.5'].model.version" --output tsv 2>$null
if ($availableVersions -and $ModelVersion -notin @($availableVersions)) {
    throw "Model version '$ModelVersion' is not available in $Location. Available: $($availableVersions -join ', ')"
}
Write-Note "Model version $ModelVersion is available in $Location."

# ---------------------------------------------------------------------------------------------
# 1. Resource providers.
# ---------------------------------------------------------------------------------------------

Write-Step 'Registering resource providers'

foreach ($provider in $RequiredProviders) {
    $state = az provider show --namespace $provider --query registrationState --output tsv 2>$null
    if ($state -eq 'Registered') {
        Write-Exists $provider
        continue
    }
    if ($PSCmdlet.ShouldProcess($provider, 'Register resource provider')) {
        Invoke-Native { az provider register --namespace $provider } "Registering $provider" | Out-Null
        Write-Created "$provider (registration is asynchronous and may take a few minutes)"
    }
}

# ---------------------------------------------------------------------------------------------
# 2. The application registration and its service principal.
# ---------------------------------------------------------------------------------------------

Write-Step 'Creating the CI identity'

$appId = az ad app list --display-name $ApplicationName --query "[0].appId" --output tsv 2>$null

if ($appId) {
    Write-Exists "application '$ApplicationName' ($appId)"
}
elseif ($PSCmdlet.ShouldProcess($ApplicationName, 'Create Entra application')) {
    $appId = Invoke-Native {
        az ad app create --display-name $ApplicationName --query appId --output tsv
    } 'Creating the application registration'
    Write-Created "application '$ApplicationName' ($appId)"
}
else {
    # -WhatIf: everything downstream needs an appId, so report and stop rather than guess.
    Write-Note 'Would create the application; skipping the remaining steps under -WhatIf.'
    return
}

$servicePrincipalId = az ad sp show --id $appId --query id --output tsv 2>$null

if ($servicePrincipalId) {
    Write-Exists "service principal ($servicePrincipalId)"
}
elseif ($PSCmdlet.ShouldProcess($appId, 'Create service principal')) {
    $servicePrincipalId = Invoke-Native {
        az ad sp create --id $appId --query id --output tsv
    } 'Creating the service principal'
    Write-Created "service principal ($servicePrincipalId)"
}

# ---------------------------------------------------------------------------------------------
# 3. Federated credentials. One per trust relationship, and no client secret anywhere.
# ---------------------------------------------------------------------------------------------

Write-Step 'Configuring federated credentials'

$subjects = @{}
foreach ($environment in $Environments) {
    $subjects["caesarea-$environment"] = "repo:${Repository}:environment:$environment"
}
# Pull requests run the read-only preview. Without this the what-if job cannot authenticate, and
# reviewers see a template nobody checked against a real subscription.
$subjects['caesarea-pull-request'] = "repo:${Repository}:pull_request"

$existingCredentials = az ad app federated-credential list --id $appId --output json 2>$null | ConvertFrom-Json

foreach ($name in $subjects.Keys | Sort-Object) {
    $subject = $subjects[$name]
    $match = @($existingCredentials) | Where-Object { $_ -and $_.subject -eq $subject }

    if ($match) {
        Write-Exists "$name -> $subject"
        continue
    }

    if ($PSCmdlet.ShouldProcess($subject, 'Create federated credential')) {
        $parameters = @{
            name        = $name
            issuer      = 'https://token.actions.githubusercontent.com'
            subject     = $subject
            description = 'Caesarea deployment from GitHub Actions'
            audiences   = @('api://AzureADTokenExchange')
        } | ConvertTo-Json -Compress

        $parameters | az ad app federated-credential create --id $appId --parameters '@-' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Creating federated credential '$name' failed." }
        Write-Created "$name -> $subject"
    }
}

# ---------------------------------------------------------------------------------------------
# 4. Role assignments.
# ---------------------------------------------------------------------------------------------

Write-Step 'Assigning subscription roles'

$scope = "/subscriptions/$SubscriptionId"
$roles = [ordered]@{
    'Contributor'                            = $ContributorRoleId
    'Role Based Access Control Administrator' = $RbacAdministratorRoleId
}

foreach ($roleName in $roles.Keys) {
    $existing = az role assignment list --assignee $appId --scope $scope `
        --role $roles[$roleName] --query "[0].id" --output tsv 2>$null

    if ($existing) {
        Write-Exists "$roleName at subscription scope"
        continue
    }

    if ($PSCmdlet.ShouldProcess("$roleName at $scope", 'Create role assignment')) {
        # Entra replication means a freshly created principal is not immediately assignable.
        $assigned = $false
        foreach ($attempt in 1..12) {
            az role assignment create --assignee-object-id $servicePrincipalId `
                --assignee-principal-type ServicePrincipal `
                --role $roles[$roleName] --scope $scope --output none 2>$null
            if ($LASTEXITCODE -eq 0) { $assigned = $true; break }
            Start-Sleep -Seconds 5
        }
        if (-not $assigned) { throw "Assigning '$roleName' failed after retrying for a minute." }
        Write-Created "$roleName at subscription scope"
    }
}

# ---------------------------------------------------------------------------------------------
# 5 and 6. GitHub environments and the identifiers the workflows read.
# ---------------------------------------------------------------------------------------------

Write-Step 'Configuring GitHub environments'

foreach ($environment in $Environments) {
    if (-not $PSCmdlet.ShouldProcess("$Repository/$environment", 'Create GitHub environment')) { continue }

    # Only prod is gated. A review gate on dev turns every experiment into a ceremony, gets
    # disabled within a week, and takes the prod gate with it when it goes.
    #
    # Reviewers are identified by GitHub user ID, not Entra object ID - two different directories
    # for the same person, and passing the wrong one fails with an unhelpful 422.
    $body = @{ deployment_branch_policy = $null }
    $gated = $false

    if ($environment -eq 'prod' -and $ProductionReviewer) {
        $reviewerId = gh api user --jq '.id' 2>$null
        if ($reviewerId) {
            $body['reviewers'] = @(@{ type = 'User'; id = [int64] $reviewerId })
            $gated = $true
        }
        else {
            Write-Note 'Could not resolve a GitHub reviewer; prod will be created ungated.'
        }
    }

    $body | ConvertTo-Json -Depth 5 -Compress |
        gh api --method PUT "repos/$Repository/environments/$environment" --input - | Out-Null

    if ($LASTEXITCODE -ne 0) { throw "Creating GitHub environment '$environment' failed." }
    Write-Created "environment '$environment'$(if ($gated) { ' (review required)' })"
}

Write-Step 'Setting repository configuration'

$configuration = [ordered]@{
    AZURE_CLIENT_ID              = $appId
    AZURE_TENANT_ID              = $account.tenantId
    AZURE_SUBSCRIPTION_ID        = $SubscriptionId
    AZURE_LOCATION               = $Location
    AZURE_DEPLOYMENT_PRINCIPAL_ID = $servicePrincipalId
    AZURE_MODEL_VERSION          = $ModelVersion
}

foreach ($environment in $Environments) {
    foreach ($key in $configuration.Keys) {
        if (-not $PSCmdlet.ShouldProcess("$environment/$key", 'Set repository configuration')) { continue }

        if ($UseSecrets) {
            gh secret set $key --env $environment --repo $Repository --body $configuration[$key] | Out-Null
        }
        else {
            gh variable set $key --env $environment --repo $Repository --body $configuration[$key] | Out-Null
        }
        if ($LASTEXITCODE -ne 0) { throw "Setting '$key' on '$environment' failed." }
    }
    Write-Created "${environment}: $($configuration.Count) $(if ($UseSecrets) { 'secrets' } else { 'variables' })"
}

# ---------------------------------------------------------------------------------------------

Write-Step 'Done'
Write-Host @"
  The CI identity exists and this repository is configured to use it.
  No credential was created: GitHub proves its identity per run with a short-lived OIDC token.

  Next:
    1. git push
    2. Actions -> deploy-infra -> Run workflow -> environment: dev, mode: preview
       Read the what-if summary. It should propose 12 resources on a first run.
    3. Re-run with mode: apply.
    4. Copy the platform outputs from the job summary into the environment's configuration
       (AZURE_CONTAINER_REGISTRY_ENDPOINT, FOUNDRY_PROJECT_ENDPOINT, MODEL_DEPLOYMENT_NAME).

  Undo with ./scripts/Remove-GitHubOidc.ps1. Details in docs/deployment.md.
"@ -ForegroundColor Green
