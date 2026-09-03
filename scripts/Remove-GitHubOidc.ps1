#Requires -Version 7.0

<#
.SYNOPSIS
    Removes the CI identity and repository configuration created by Bootstrap-GitHubOidc.ps1.

.DESCRIPTION
    The inverse of the bootstrap, and idempotent in the same way: anything already gone is reported
    and skipped. Run it when you are finished with the demo, or before rebuilding from scratch.

    It removes, in this order:
      1. The repository variables and secrets the workflows read.
      2. The GitHub environments.
      3. The subscription role assignments held by the CI identity.
      4. The Entra application registration, which takes its service principal and federated
         credentials with it.

    It does NOT delete Azure resources. Those are a separate concern with a separate blast radius:

        az group delete --name rg-caesarea-dev --yes

    Deleting the resource group leaves the Foundry account recoverable for a period. To reuse the
    same account name immediately, purge it:

        az cognitiveservices account purge --name <account> --resource-group <rg> --location <region>

.PARAMETER Repository
    owner/name of the GitHub repository. Defaults to the origin remote of this working tree.

.PARAMETER SubscriptionId
    Subscription the role assignments live on. Defaults to the current az account.

.PARAMETER ApplicationName
    Display name of the Entra application to remove.

.PARAMETER Environments
    GitHub environments to remove.

.EXAMPLE
    ./scripts/Remove-GitHubOidc.ps1 -WhatIf

.EXAMPLE
    ./scripts/Remove-GitHubOidc.ps1
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $Repository,
    [string] $SubscriptionId,
    [string] $ApplicationName = 'caesarea-github-deploy',
    [string[]] $Environments = @('dev', 'prod')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Gone { param([string] $Message) Write-Host "  [absent] $Message" -ForegroundColor DarkGray }
function Write-Removed { param([string] $Message) Write-Host "  [removed] $Message" -ForegroundColor Yellow }

Write-Step 'Checking prerequisites'

foreach ($tool in @('az', 'gh')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool is not on PATH."
    }
}

$accountJson = az account show --output json 2>$null
if (-not $accountJson) { throw 'Not signed in to Azure. Run: az login' }
$account = $accountJson | ConvertFrom-Json
if (-not $SubscriptionId) { $SubscriptionId = $account.id }

if (-not $Repository) {
    $originUrl = git -C $PSScriptRoot/.. remote get-url origin 2>$null
    if (-not $originUrl) { throw 'No origin remote found. Pass -Repository owner/name.' }
    $Repository = ($originUrl -replace '^.*github\.com[:/]', '' -replace '\.git$', '')
}

Write-Host "  Subscription: $($account.name)" -ForegroundColor DarkGray
Write-Host "  Repository:   $Repository" -ForegroundColor DarkGray

# ---------------------------------------------------------------------------------------------
# GitHub environments. Deleting an environment removes the variables and secrets scoped to it, so
# this is done first: the reverse order would leave orphans behind if the run were interrupted.
# ---------------------------------------------------------------------------------------------

Write-Step 'Removing GitHub environments'

foreach ($environment in $Environments) {
    $exists = gh api "repos/$Repository/environments/$environment" --silent 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Gone "environment '$environment'"
        continue
    }

    if ($PSCmdlet.ShouldProcess("$Repository/$environment", 'Delete GitHub environment')) {
        gh api --method DELETE "repos/$Repository/environments/$environment" --silent 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Deleting environment '$environment' failed." }
        Write-Removed "environment '$environment' and its configuration"
    }
}

# ---------------------------------------------------------------------------------------------
# Role assignments, before the application they are attached to. Deleting the application first
# leaves assignments pointing at a principal that no longer resolves, which show in the portal as
# "Identity not found" and are then awkward to clean up.
# ---------------------------------------------------------------------------------------------

Write-Step 'Removing role assignments'

$appId = az ad app list --display-name $ApplicationName --query "[0].appId" --output tsv 2>$null

if (-not $appId) {
    Write-Gone "application '$ApplicationName' (nothing to unassign)"
}
else {
    $scope = "/subscriptions/$SubscriptionId"
    $assignments = az role assignment list --assignee $appId --scope $scope --output json 2>$null | ConvertFrom-Json

    if (-not $assignments -or @($assignments).Count -eq 0) {
        Write-Gone 'subscription role assignments'
    }
    else {
        foreach ($assignment in @($assignments)) {
            if ($PSCmdlet.ShouldProcess($assignment.roleDefinitionName, 'Delete role assignment')) {
                az role assignment delete --ids $assignment.id --output none 2>$null
                if ($LASTEXITCODE -ne 0) { throw "Deleting role assignment '$($assignment.id)' failed." }
                Write-Removed "$($assignment.roleDefinitionName) at subscription scope"
            }
        }
    }

    # ---------------------------------------------------------------------------------------------
    # The application. Its service principal and federated credentials are children and go with it.
    # ---------------------------------------------------------------------------------------------

    Write-Step 'Removing the CI identity'

    if ($PSCmdlet.ShouldProcess($ApplicationName, 'Delete Entra application')) {
        az ad app delete --id $appId --output none 2>$null
        if ($LASTEXITCODE -ne 0) { throw "Deleting application '$ApplicationName' failed." }
        Write-Removed "application '$ApplicationName' with its service principal and federated credentials"
    }
}

Write-Step 'Done'
Write-Host @"
  The CI identity and repository configuration are gone. Azure resources are untouched:

    az group delete --name rg-caesarea-dev --yes

  Re-create everything with ./scripts/Bootstrap-GitHubOidc.ps1.
"@ -ForegroundColor Green
