#Requires -Version 7.0

<#
.SYNOPSIS
    Creates the CI identities that let GitHub Actions deploy this solution to Azure, and configures
    the repository to use them.

.DESCRIPTION
    This is the one step that cannot come from a workflow: something has to create the identity the
    workflows authenticate as, before any workflow can run. Everything after this is GitHub Actions.

    ONE IDENTITY PER ENVIRONMENT. dev and prod each get their own Entra application with exactly one
    federated credential, bound to that environment alone. A token minted for dev cannot deploy to
    prod, which is what makes the prod approval gate a boundary rather than a convention.

    Pull requests get NO Azure identity at all. They compile the templates locally, which needs no
    credential. Cloud what-if runs on manual dispatch, behind the environment gate, where it belongs.

    What it creates, all idempotent - a second run reports what exists and changes nothing:

      Azure, per environment
        1. An Entra application and its service principal.
        2. One federated credential: repo:<owner>/<repo>:environment:<name>. No client secret, ever.
        3. Contributor and Role Based Access Control Administrator.
        4. Resource providers registered, and waited for.

      GitHub, per environment
        5. The environment, with a deployment branch policy, and review required on prod.
        6. The identifiers the workflows read, as variables. They are not credentials.

    KNOWN LIMITATION. Both identities hold their roles at SUBSCRIPTION scope, because the platform
    template creates its own resource group and so cannot be scoped below it. dev and prod are
    separated by identity and federation subject, not by Azure scope: a compromised dev identity
    could still reach prod resources. Separate subscriptions per environment is the real fix, and is
    cleaner than resource-group scoping. Pass -SubscriptionId to run this against one of them.

.PARAMETER SubscriptionId
    Target subscription. Defaults to the current az account. Every Azure call is explicitly scoped to
    it, so your global CLI context is never changed.

.PARAMETER Repository
    owner/name of the GitHub repository. Defaults to the origin remote of this working tree.

.PARAMETER Location
    Azure region. Must be one where Foundry hosted agents are available.

.PARAMETER ModelVersion
    Model version to pin. Verified against the region before anything is created.

.PARAMETER ApplicationNamePrefix
    Entra applications are named <prefix>-<environment>. Change only if you are running more than one
    independent copy of this demo in the same tenant.

.PARAMETER Environments
    Environments to configure. Each gets its own identity.

.PARAMETER DeploymentBranch
    Branch permitted to deploy. Other branches and all tags are refused by the branch policy.

.PARAMETER AllowUngatedProduction
    Proceed even when required reviewers cannot be configured on prod. Needed for a private
    repository on a personal account, where GitHub does not offer environment protection rules.
    Without this switch that situation stops the bootstrap rather than silently producing an ungated
    prod, because a gate you believe in and do not have is worse than no gate at all.

.EXAMPLE
    ./scripts/Bootstrap-GitHubOidc.ps1 -ModelVersion 2026-04-24 -WhatIf

.EXAMPLE
    ./scripts/Bootstrap-GitHubOidc.ps1 -ModelVersion 2026-04-24 -AllowUngatedProduction

.NOTES
    Undo with ./scripts/Remove-GitHubOidc.ps1. Full walkthrough in docs/deployment.md.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $SubscriptionId,
    [string] $Repository,
    [string] $Location = 'westus3',
    [Parameter(Mandatory)] [string] $ModelVersion,
    [string] $ApplicationNamePrefix = 'caesarea-github-deploy',
    [string[]] $Environments = @('dev', 'prod'),
    [string] $DeploymentBranch = 'main',
    [switch] $AllowUngatedProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Built-in role IDs. Resolve them yourself rather than trusting these constants - several Azure AI
# roles were renamed in 2026 while keeping their IDs:
#   az role definition list --name "Contributor" --query "[0].name" -o tsv
$RoleIds = [ordered]@{
    'Contributor'                             = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
    'Role Based Access Control Administrator' = 'f58310d9-a9f6-439a-9e8d-f62e7b41a168'
}

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

function Invoke-Checked {
    <#  Runs a native command and throws on a non-zero exit code.

        Every read goes through this. The alternative - letting a failed call return nothing and
        reading that as absence - turns an expired token or a network blip into "the application
        does not exist", after which the script cheerfully creates a second one. #>
    param([Parameter(Mandatory)] [scriptblock] $Command, [Parameter(Mandatory)] [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

function Test-GitHubResource {
    <#  True when the resource exists, false on a genuine 404, and throws on anything else. A 403 or
        a DNS failure must never read as "absent". #>
    param([Parameter(Mandatory)] [string] $Path)

    $output = gh api $Path --silent 2>&1
    if ($LASTEXITCODE -eq 0) { return $true }
    if ("$output" -match '(?i)HTTP 404|Not Found') { return $false }
    throw "Reading GitHub resource '$Path' failed: $($output -join [Environment]::NewLine)"
}

# ---------------------------------------------------------------------------------------------
# Preflight. Everything is checked before anything is created, so a missing prerequisite fails
# immediately rather than half way through a tenant change.
# ---------------------------------------------------------------------------------------------

Write-Step 'Checking prerequisites'

foreach ($tool in @('az', 'gh')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool is not on PATH. See docs/deployment.md for the prerequisites."
    }
}
Write-Note 'az and gh are present.'

if (-not $SubscriptionId) {
    $SubscriptionId = (Invoke-Checked { az account show --query id --output tsv } 'Reading the current Azure account').Trim()
}

# Resolved with --subscription rather than by switching the CLI's default, so this never changes the
# operator's global context. Every Azure call below is scoped the same way.
$account = (Invoke-Checked {
    az account show --subscription $SubscriptionId --output json
} "Reading subscription '$SubscriptionId'") | ConvertFrom-Json

Write-Note "Subscription: $($account.name) ($($account.id))"
Write-Note "Tenant:       $($account.tenantId)"

Invoke-Checked { gh auth status } 'Checking GitHub authentication' | Out-Null

if (-not $Repository) {
    $originUrl = Invoke-Checked {
        git -C "$PSScriptRoot/.." remote get-url origin
    } 'Reading the origin remote'
    $Repository = ("$originUrl" -replace '^.*github\.com[:/]', '' -replace '\.git$', '').Trim()
}
Write-Note "Repository:   $Repository"

# The model version is pinned deliberately - a floating version changes agent behaviour with no
# deployment to point at - so it is verified now rather than discovered mid-deployment. An empty
# list is a failure, not a pass: announcing "available" off no data is how this goes wrong quietly.
$availableVersions = @(@(Invoke-Checked {
    az cognitiveservices model list --subscription $SubscriptionId --location $Location `
        --query "[?model.name=='gpt-5.5'].model.version" --output tsv
    } "Listing models in $Location") | ForEach-Object { "$_".Trim() } | Where-Object { $_ })

if ($availableVersions.Count -eq 0) {
    throw "No gpt-5.5 versions were returned for $Location. Confirm the region offers the model before continuing."
}
if ($ModelVersion -notin $availableVersions) {
    throw "Model version '$ModelVersion' is not available in $Location. Available: $($availableVersions -join ', ')"
}
Write-Note "Model:        gpt-5.5 $ModelVersion is available in $Location."

# ---------------------------------------------------------------------------------------------
# Can prod actually be gated? GitHub does not offer environment protection rules on private
# repositories outside Enterprise, so on a personal private repository a reviewer configuration is
# accepted by the API and enforces nothing. That is decided here rather than discovered later.
# ---------------------------------------------------------------------------------------------

$repositoryInfo = (Invoke-Checked {
    gh api "repos/$Repository" --jq '{private: .private, ownerType: .owner.type}'
} "Reading repository '$Repository'") | ConvertFrom-Json

$protectionAvailable = -not ($repositoryInfo.private -and $repositoryInfo.ownerType -eq 'User')

if (('prod' -in $Environments) -and -not $protectionAvailable) {
    if (-not $AllowUngatedProduction) {
        throw @"
This repository is private and owned by a personal account, where GitHub does not offer environment
protection rules. A required-reviewer gate on prod would be accepted by the API and enforce nothing.

Re-run with -AllowUngatedProduction to proceed with prod deliberately ungated, or move the
repository to an organisation on a plan that supports protected environments.
"@
    }
    Write-Note 'Production will be UNGATED: protection rules are unavailable here (-AllowUngatedProduction).'
}

# ---------------------------------------------------------------------------------------------
# Resource providers. Registration is asynchronous, so it is waited for: finishing the bootstrap
# while a provider is still registering leaves the very next workflow run to fail on it.
# ---------------------------------------------------------------------------------------------

Write-Step 'Registering resource providers'

foreach ($provider in $RequiredProviders) {
    $state = (Invoke-Checked {
        az provider show --subscription $SubscriptionId --namespace $provider --query registrationState --output tsv
    } "Reading provider $provider").Trim()

    if ($state -eq 'Registered') {
        Write-Exists $provider
        continue
    }

    if ($PSCmdlet.ShouldProcess($provider, 'Register resource provider')) {
        Invoke-Checked {
            az provider register --subscription $SubscriptionId --namespace $provider --wait
        } "Registering $provider" | Out-Null
        Write-Created $provider
    }
}

# ---------------------------------------------------------------------------------------------
# One identity per environment. This is the security boundary.
# ---------------------------------------------------------------------------------------------

$scope = "/subscriptions/$($account.id)"
$identities = @{}

foreach ($environment in $Environments) {
    $applicationName = "$ApplicationNamePrefix-$environment"
    Write-Step "Identity for '$environment' ($applicationName)"

    # Display names are not unique in Entra - the same name may be registered many times, and the
    # application ID is the only real identifier. Taking [0] of an ambiguous match is how the wrong
    # application gets modified, so ambiguity is fatal here and in the teardown script.
    $found = @(@(Invoke-Checked {
        az ad app list --display-name $applicationName --query "[].appId" --output tsv
    } "Listing applications named '$applicationName'") | ForEach-Object { "$_".Trim() } | Where-Object { $_ })

    if ($found.Count -gt 1) {
        throw "Found $($found.Count) applications named '$applicationName'. Resolve the ambiguity first; the application ID is the unique identifier."
    }

    $appId = if ($found.Count -eq 1) { $found[0] } else { $null }

    if ($appId) {
        Write-Exists "application ($appId)"
    }
    elseif ($PSCmdlet.ShouldProcess($applicationName, 'Create Entra application')) {
        $appId = (Invoke-Checked {
            az ad app create --display-name $applicationName --query appId --output tsv
        } "Creating application '$applicationName'").Trim()
        Write-Created "application ($appId)"
    }
    else {
        # -WhatIf on a clean tenant: everything below needs a real application ID, so it is reported
        # as unpreviewable rather than faked. docs/deployment.md says so too.
        Write-Note 'Would create the application. The steps below depend on its ID and cannot be previewed.'
        continue
    }

    $servicePrincipalId = @(@(Invoke-Checked {
        az ad sp list --filter "appId eq '$appId'" --query "[].id" --output tsv
    } 'Listing service principals') | ForEach-Object { "$_".Trim() } | Where-Object { $_ }) | Select-Object -First 1

    if ($servicePrincipalId) {
        Write-Exists "service principal ($servicePrincipalId)"
    }
    elseif ($PSCmdlet.ShouldProcess($appId, 'Create service principal')) {
        $servicePrincipalId = (Invoke-Checked {
            az ad sp create --id $appId --query id --output tsv
        } 'Creating the service principal').Trim()
        Write-Created "service principal ($servicePrincipalId)"
    }

    # Exactly one federated credential, for this environment only. This is the boundary: a token
    # minted for dev carries subject environment:dev, and no other application will accept it.
    $subject = "repo:${Repository}:environment:$environment"
    $existingSubjects = @(@(Invoke-Checked {
        az ad app federated-credential list --id $appId --query "[].subject" --output tsv
    } 'Listing federated credentials') | ForEach-Object { "$_".Trim() } | Where-Object { $_ })

    if ($subject -in $existingSubjects) {
        Write-Exists "federated credential -> $subject"
    }
    elseif ($PSCmdlet.ShouldProcess($subject, 'Create federated credential')) {
        $parameters = @{
            name        = "caesarea-$environment"
            issuer      = 'https://token.actions.githubusercontent.com'
            subject     = $subject
            description = "Caesarea $environment deployment from GitHub Actions"
            audiences   = @('api://AzureADTokenExchange')
        } | ConvertTo-Json -Compress

        $parameters | az ad app federated-credential create --id $appId --parameters '@-' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Creating the federated credential for '$environment' failed." }
        Write-Created "federated credential -> $subject"
    }

    foreach ($roleName in $RoleIds.Keys) {
        $assigned = @(@(Invoke-Checked {
            az role assignment list --subscription $SubscriptionId --assignee $appId --scope $scope `
                --role $RoleIds[$roleName] --query "[].id" --output tsv
    } "Listing '$roleName' assignments") | ForEach-Object { "$_".Trim() } | Where-Object { $_ })

        if ($assigned.Count -gt 0) {
            Write-Exists "$roleName at subscription scope"
            continue
        }

        if ($PSCmdlet.ShouldProcess("$roleName for $applicationName", 'Create role assignment')) {
            # Entra replication means a principal created seconds ago is not yet assignable.
            $ok = $false
            foreach ($attempt in 1..12) {
                az role assignment create --subscription $SubscriptionId `
                    --assignee-object-id $servicePrincipalId --assignee-principal-type ServicePrincipal `
                    --role $RoleIds[$roleName] --scope $scope --output none 2>$null
                if ($LASTEXITCODE -eq 0) { $ok = $true; break }
                Start-Sleep -Seconds 5
            }
            if (-not $ok) { throw "Assigning '$roleName' to '$applicationName' failed after retrying for a minute." }
            Write-Created "$roleName at subscription scope"
        }
    }

    $identities[$environment] = @{ AppId = $appId; PrincipalId = $servicePrincipalId }
}

# ---------------------------------------------------------------------------------------------
# GitHub environments and the identifiers the workflows read.
# ---------------------------------------------------------------------------------------------

Write-Step 'Configuring GitHub environments'

foreach ($environment in $Environments) {
    if (-not $PSCmdlet.ShouldProcess("$Repository/$environment", 'Configure GitHub environment')) { continue }

    # The PUT below converges either way, but saying [created] about something that already existed
    # makes the idempotency claim in docs/deployment.md untrue on a re-run - and a report you cannot
    # trust is worse than no report.
    $environmentExisted = Test-GitHubResource "repos/$Repository/environments/$environment"

    # A named branch policy, never null. Null permits every branch and every tag to deploy, which
    # makes the environment a label rather than a control.
    $body = @{
        deployment_branch_policy = @{ protected_branches = $false; custom_branch_policies = $true }
    }
    $gated = $false

    if ($environment -eq 'prod' -and $protectionAvailable) {
        $reviewerId = (Invoke-Checked { gh api user --jq '.id' } 'Reading the signed-in GitHub user').Trim()
        $body['reviewers'] = @(@{ type = 'User'; id = [int64] $reviewerId })
        # Without this the person who starts a production deployment can approve their own.
        $body['prevent_self_review'] = $true
        $gated = $true
    }

    $body | ConvertTo-Json -Depth 5 -Compress |
        gh api --method PUT "repos/$Repository/environments/$environment" --input - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Configuring environment '$environment' failed." }

    # Branch policies are a sub-resource; the PUT above only enables custom policies.
    $currentPolicies = @()
    if (Test-GitHubResource "repos/$Repository/environments/$environment/deployment-branch-policies") {
        $currentPolicies = @((Invoke-Checked {
            gh api "repos/$Repository/environments/$environment/deployment-branch-policies" --jq '.branch_policies[].name'
        } 'Listing branch policies') | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
    }

    if ($DeploymentBranch -notin $currentPolicies) {
        @{ name = $DeploymentBranch } | ConvertTo-Json -Compress |
            gh api --method POST "repos/$Repository/environments/$environment/deployment-branch-policies" --input - | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Adding the branch policy for '$environment' failed." }
    }

    $summary = "environment '$environment' (branch: $DeploymentBranch$(if ($gated) { ', review required' } else { ', UNGATED' }))"
    if ($environmentExisted) { Write-Exists $summary } else { Write-Created $summary }

    if (-not $identities.ContainsKey($environment)) {
        Write-Note 'Skipping variables: this environment has no identity yet.'
        continue
    }

    # Variables, not secrets. These are identifiers, not credentials - there is no credential in
    # this system to store. Masking them would only make a failed deployment harder to read, and the
    # workflows read the vars context.
    $configuration = [ordered]@{
        AZURE_CLIENT_ID               = $identities[$environment].AppId
        AZURE_TENANT_ID               = $account.tenantId
        AZURE_SUBSCRIPTION_ID         = $account.id
        AZURE_LOCATION                = $Location
        AZURE_DEPLOYMENT_PRINCIPAL_ID = $identities[$environment].PrincipalId
        AZURE_MODEL_VERSION           = $ModelVersion
    }

    $changed = 0
    foreach ($key in $configuration.Keys) {
        # Compare before writing, so the report distinguishes "already correct" from "set". Both
        # converge; only one of them is a change.
        $current = $null
        if (Test-GitHubResource "repos/$Repository/environments/$environment/variables/$key") {
            $current = (Invoke-Checked {
                gh api "repos/$Repository/environments/$environment/variables/$key" --jq '.value'
            } "Reading variable '$key'").Trim()
        }

        if ($current -eq $configuration[$key]) { continue }

        Invoke-Checked {
            gh variable set $key --env $environment --repo $Repository --body $configuration[$key]
        } "Setting variable '$key' on '$environment'" | Out-Null
        $changed++
    }

    if ($changed -gt 0) { Write-Created "${environment}: $changed of $($configuration.Count) variables set" }
    else { Write-Exists "${environment}: $($configuration.Count) variables already correct" }
}

# ---------------------------------------------------------------------------------------------

Write-Step 'Done'
Write-Host @"
  Each environment has its own identity, federated to that environment alone. No credential was
  created: GitHub proves who it is per run with a short-lived OIDC token, and pull requests get no
  Azure identity at all.

  Next:
    1. git push
    2. Actions -> deploy-infra -> Run workflow -> environment: dev, mode: preview
       Read the what-if summary. It should propose 12 resources on a first run.
    3. Re-run with mode: apply.
    4. Copy the platform outputs from the job summary into that environment's variables.

  Undo with ./scripts/Remove-GitHubOidc.ps1. Details in docs/deployment.md.
"@ -ForegroundColor Green
