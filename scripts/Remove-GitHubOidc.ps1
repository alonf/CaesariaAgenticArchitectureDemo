#Requires -Version 7.0

<#
.SYNOPSIS
    Removes the CI identities and repository configuration created by Bootstrap-GitHubOidc.ps1.

.DESCRIPTION
    The inverse of the bootstrap, and deliberately narrower than it. This script deletes things, so
    it removes only what the bootstrap is known to own:

      - the six variables it set, on each environment - not the whole environment, and not anything
        else someone added to it. Pass -RemoveEnvironments to delete the environments themselves;
      - exactly the two role assignments it created, at exactly the scope it created them - not
        every assignment the principal happens to hold;
      - the applications it created, identified unambiguously. A display-name lookup returning zero
        or several results is an error, never a licence to guess: Entra permits duplicate display
        names and the application ID is the only unique identifier.

    Every read distinguishes "not found" from "could not read". A 403 or a network failure is a
    failure, not evidence of absence.

    It does NOT delete Azure resources, and it does not unregister resource providers - those are
    subscription-wide and other things may depend on them. Resources are a separate blast radius:

        az group delete --name rg-caesarea-dev --yes
        az group delete --name rg-caesarea-prod --yes

    A deleted Foundry account stays recoverable for a period, which blocks reusing its name. Purge
    it to free the name immediately:

        az cognitiveservices account purge --name <account> --resource-group <rg> --location <region>

.PARAMETER SubscriptionId
    Subscription the role assignments live on. Defaults to the current az account.

.PARAMETER Repository
    owner/name of the GitHub repository. Defaults to the origin remote of this working tree.

.PARAMETER ApplicationNamePrefix
    Applications are named <prefix>-<environment>, matching the bootstrap.

.PARAMETER ApplicationId
    Delete this exact application instead of resolving by name. Use it when a display-name lookup is
    ambiguous. Only valid with a single environment.

.PARAMETER Environments
    Environments to clean up.

.PARAMETER RemoveEnvironments
    Delete the GitHub environments outright rather than only the variables this bootstrap set.
    Destroys anything else configured on them, including protection rules and unrelated secrets.

.EXAMPLE
    ./scripts/Remove-GitHubOidc.ps1 -WhatIf

.EXAMPLE
    ./scripts/Remove-GitHubOidc.ps1 -RemoveEnvironments
#>

[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [string] $SubscriptionId,
    [string] $Repository,
    [string] $ApplicationNamePrefix = 'caesarea-github-deploy',
    [string] $ApplicationId,
    [string[]] $Environments = @('dev', 'prod'),
    [switch] $RemoveEnvironments
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Exactly the roles the bootstrap assigns. Anything else the principal holds was granted by someone
# else, for some other reason, and is not this script's to remove.
$RoleIds = [ordered]@{
    'Contributor'                             = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
    'Role Based Access Control Administrator' = 'f58310d9-a9f6-439a-9e8d-f62e7b41a168'
}

# Exactly the variables the bootstrap sets.
$OwnedVariables = @(
    'AZURE_CLIENT_ID'
    'AZURE_TENANT_ID'
    'AZURE_SUBSCRIPTION_ID'
    'AZURE_LOCATION'
    'AZURE_DEPLOYMENT_PRINCIPAL_ID'
    'AZURE_MODEL_VERSION'
)

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Gone { param([string] $Message) Write-Host "  [absent] $Message" -ForegroundColor DarkGray }
function Write-Removed { param([string] $Message) Write-Host "  [removed] $Message" -ForegroundColor Yellow }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

function Invoke-Checked {
    param([Parameter(Mandatory)] [scriptblock] $Command, [Parameter(Mandatory)] [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

function Test-GitHubResource {
    <#  True when it exists, false on a genuine 404, throws otherwise. Reporting a 403 as [absent]
        would let this script claim it had cleaned up something it never saw. #>
    param([Parameter(Mandatory)] [string] $Path)

    $output = gh api $Path --silent 2>&1
    if ($LASTEXITCODE -eq 0) { return $true }
    if ("$output" -match '(?i)HTTP 404|Not Found') { return $false }
    throw "Reading GitHub resource '$Path' failed: $($output -join [Environment]::NewLine)"
}

Write-Step 'Checking prerequisites'

foreach ($tool in @('az', 'gh')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool is not on PATH." }
}

if ($ApplicationId -and $Environments.Count -ne 1) {
    throw '-ApplicationId identifies one application, so it must be used with exactly one -Environments value.'
}

if (-not $SubscriptionId) {
    $SubscriptionId = (Invoke-Checked { az account show --query id --output tsv } 'Reading the current Azure account').Trim()
}

$account = (Invoke-Checked {
    az account show --subscription $SubscriptionId --output json
} "Reading subscription '$SubscriptionId'") | ConvertFrom-Json

if (-not $Repository) {
    $originUrl = Invoke-Checked { git -C "$PSScriptRoot/.." remote get-url origin } 'Reading the origin remote'
    $Repository = ("$originUrl" -replace '^.*github\.com[:/]', '' -replace '\.git$', '').Trim()
}

Write-Note "Subscription: $($account.name) ($($account.id))"
Write-Note "Repository:   $Repository"
$scope = "/subscriptions/$($account.id)"

# ---------------------------------------------------------------------------------------------
# GitHub first. Removing the identity before the configuration that names it would leave variables
# pointing at a principal that no longer resolves.
# ---------------------------------------------------------------------------------------------

Write-Step 'Removing GitHub configuration'

foreach ($environment in $Environments) {
    if (-not (Test-GitHubResource "repos/$Repository/environments/$environment")) {
        Write-Gone "environment '$environment'"
        continue
    }

    if ($RemoveEnvironments) {
        if ($PSCmdlet.ShouldProcess("$Repository/$environment", 'Delete GitHub environment and everything on it')) {
            Invoke-Checked {
                gh api --method DELETE "repos/$Repository/environments/$environment" --silent
            } "Deleting environment '$environment'" | Out-Null
            Write-Removed "environment '$environment' and all of its configuration"
        }
        continue
    }

    # Only the variables this bootstrap set. Anything else on the environment belongs to someone.
    $removed = 0
    foreach ($name in $OwnedVariables) {
        if (-not (Test-GitHubResource "repos/$Repository/environments/$environment/variables/$name")) { continue }

        if ($PSCmdlet.ShouldProcess("$environment/$name", 'Delete repository variable')) {
            Invoke-Checked {
                gh variable delete $name --env $environment --repo $Repository
            } "Deleting variable '$name'" | Out-Null
            $removed++
        }
    }

    if ($removed -gt 0) { Write-Removed "${environment}: $removed variable(s)" }
    else { Write-Gone "${environment}: no owned variables" }
    Write-Note "environment '$environment' kept (pass -RemoveEnvironments to delete it)"
}

# ---------------------------------------------------------------------------------------------
# Role assignments, then the applications that hold them. Deleting an application first leaves
# assignments pointing at a principal that no longer resolves - they show as "Identity not found"
# and are then awkward to find and remove.
# ---------------------------------------------------------------------------------------------

foreach ($environment in $Environments) {
    $applicationName = "$ApplicationNamePrefix-$environment"
    Write-Step "Identity for '$environment' ($applicationName)"

    $appId = $ApplicationId
    if (-not $appId) {
        $found = @(@(Invoke-Checked {
            az ad app list --display-name $applicationName --query "[].appId" --output tsv
    } "Listing applications named '$applicationName'") | ForEach-Object { "$_".Trim() } | Where-Object { $_ })

        if ($found.Count -eq 0) {
            Write-Gone "application '$applicationName'"
            continue
        }
        if ($found.Count -gt 1) {
            throw "Found $($found.Count) applications named '$applicationName'. Refusing to guess which to delete - re-run with -ApplicationId <appId> and a single -Environments value."
        }
        $appId = $found[0]
    }

    foreach ($roleName in $RoleIds.Keys) {
        $assignments = @(@(Invoke-Checked {
            az role assignment list --subscription $SubscriptionId --assignee $appId --scope $scope `
                --role $RoleIds[$roleName] --query "[].id" --output tsv
    } "Listing '$roleName' assignments") | ForEach-Object { "$_".Trim() } | Where-Object { $_ })

        if ($assignments.Count -eq 0) {
            Write-Gone "$roleName at subscription scope"
            continue
        }

        foreach ($assignmentId in $assignments) {
            if ($PSCmdlet.ShouldProcess("$roleName for $applicationName", 'Delete role assignment')) {
                Invoke-Checked {
                    az role assignment delete --subscription $SubscriptionId --ids $assignmentId --yes
                } "Deleting role assignment '$assignmentId'" | Out-Null
                Write-Removed "$roleName at subscription scope"
            }
        }
    }

    # The application takes its service principal and federated credentials with it.
    if ($PSCmdlet.ShouldProcess("$applicationName ($appId)", 'Delete Entra application')) {
        Invoke-Checked { az ad app delete --id $appId } "Deleting application '$applicationName'" | Out-Null
        Write-Removed "application '$applicationName' with its service principal and federated credential"
    }
}

Write-Step 'Done'
Write-Host @"
  The CI identities and the variables this bootstrap set are gone.

  Left alone deliberately:
    - the GitHub environments themselves, unless -RemoveEnvironments was passed;
    - resource providers, which are subscription-wide and shared;
    - all Azure resources:

        az group delete --name rg-caesarea-dev --yes
        az group delete --name rg-caesarea-prod --yes

  Re-create everything with ./scripts/Bootstrap-GitHubOidc.ps1.
"@ -ForegroundColor Green
