#Requires -Version 7.0

<#
.SYNOPSIS
    Gives the hosted agent's Entra identity an accountable human owner.

.DESCRIPTION
    The platform mints the agent's identity when its first version is created - so its owner is
    whoever ran that creation, and in this repository that is the CI deployment principal. An
    unattended pipeline as the only party answering for an agent is precisely the anti-pattern the
    Agent 365 segment teaches against, and this script is the fix the presenter performs live.

    Additive on purpose: the pipeline owner is left in place. It still deploys versions, and the
    lesson is "a human is accountable", not "the pipeline is untrusted". The human owner defaults
    to the signed-in user, so nobody's identity is written into this repository.

    Idempotent: an owner who is already an owner is reported and left alone.

    Directory rights: adding an owner to a service principal needs an application-administration
    role (Application Administrator, Cloud Application Administrator, or Global Administrator) or
    existing ownership. A plain member gets Authorization_RequestDenied.

.PARAMETER Environment
    Environment whose FOUNDRY_PROJECT_ENDPOINT to read, when one is not supplied directly.

.PARAMETER ProjectEndpoint
    Foundry project endpoint. Defaults to the environment's FOUNDRY_PROJECT_ENDPOINT variable.

.PARAMETER AgentName
    The hosted agent whose identity to own.

.PARAMETER OwnerUserPrincipalName
    The human to add. Defaults to the signed-in user.

.PARAMETER Repository
    owner/name of the GitHub repository, used only to read the endpoint default.

.EXAMPLE
    ./scripts/Set-AgentOwner.ps1 -WhatIf

.EXAMPLE
    ./scripts/Set-AgentOwner.ps1
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $ProjectEndpoint,
    [string] $AgentName = 'caesarea-operations',
    [string] $OwnerUserPrincipalName,
    [string] $Repository
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$GraphBase = 'https://graph.microsoft.com/v1.0'

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Exists { param([string] $Message) Write-Host "  [exists] $Message" -ForegroundColor DarkGray }
function Write-Created { param([string] $Message) Write-Host "  [added] $Message" -ForegroundColor Green }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

function Invoke-Checked {
    param([Parameter(Mandatory)] [scriptblock] $Command, [Parameter(Mandatory)] [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

$script:BearerTokens = @{}

function Get-BearerToken {
    param([Parameter(Mandatory)] [string] $Resource)

    if (-not $script:BearerTokens.ContainsKey($Resource)) {
        $script:BearerTokens[$Resource] = (Invoke-Checked {
            az account get-access-token --resource $Resource --query accessToken --output tsv
        } "Getting a token for $Resource").Trim()
    }
    return $script:BearerTokens[$Resource]
}

function Invoke-RestJson {
    <#  Native Invoke-WebRequest rather than `az rest`: the az launcher is a batch file on Windows
        and breaks on ampersands inside Graph URLs; a POST body avoids its quoting entirely. #>
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Url,
        [object] $Body,
        [Parameter(Mandatory)] [string] $Resource,
        [Parameter(Mandatory)] [string] $What
    )

    $arguments = @{
        Method             = $Method
        Uri                = $Url
        Headers            = @{ Authorization = "Bearer $(Get-BearerToken -Resource $Resource)" }
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $Body) {
        $arguments.Body = ($Body | ConvertTo-Json -Depth 6)
        $arguments.ContentType = 'application/json'
    }

    $response = Invoke-WebRequest @arguments

    if ($response.StatusCode -ge 400) {
        throw "$What failed with HTTP $($response.StatusCode): $($response.Content)"
    }
    if ([string]::IsNullOrWhiteSpace($response.Content)) { return $null }
    return $response.Content | ConvertFrom-Json
}

Write-Step 'Checking prerequisites'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'az is not on PATH.' }

if (-not $ProjectEndpoint) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw 'gh is not on PATH, so FOUNDRY_PROJECT_ENDPOINT cannot be read. Pass -ProjectEndpoint explicitly.'
    }
    if (-not $Repository) {
        $originUrl = Invoke-Checked { git -C "$PSScriptRoot/.." remote get-url origin } 'Reading the origin remote'
        $Repository = ("$originUrl" -replace '^.*github\.com[:/]', '' -replace '\.git$', '').Trim()
    }

    $value = gh api "repos/$Repository/environments/$Environment/variables/FOUNDRY_PROJECT_ENDPOINT" --jq '.value' 2>&1
    if ($LASTEXITCODE -ne 0) {
        $global:LASTEXITCODE = 0
        throw "FOUNDRY_PROJECT_ENDPOINT is not set on the '$Environment' environment. Run ./scripts/Sync-PlatformVariables.ps1 first, or pass -ProjectEndpoint."
    }
    $ProjectEndpoint = "$value".Trim()
}

if (-not $OwnerUserPrincipalName) {
    $me = Invoke-RestJson -Method get -Url "$GraphBase/me?`$select=id,userPrincipalName,displayName" -Resource 'https://graph.microsoft.com' -What 'Reading the signed-in user'
    $OwnerUserPrincipalName = $me.userPrincipalName
    $ownerUser = $me
    Write-Note "Owner:   $OwnerUserPrincipalName (signed in; pass -OwnerUserPrincipalName to target another)"
}
else {
    $ownerUser = Invoke-RestJson -Method get -Url "$GraphBase/users/$OwnerUserPrincipalName`?`$select=id,userPrincipalName,displayName" -Resource 'https://graph.microsoft.com' -What "Reading user '$OwnerUserPrincipalName'"
    Write-Note "Owner:   $OwnerUserPrincipalName"
}

Write-Note "Project: $ProjectEndpoint"
Write-Note "Agent:   $AgentName"

# ---------------------------------------------------------------------------------------------
# The agent identity, and who currently answers for it.
# ---------------------------------------------------------------------------------------------

Write-Step "Agent identity of '$AgentName'"

$agent = Invoke-RestJson -Method get -Url "$ProjectEndpoint/agents/$AgentName`?api-version=v1" -Resource 'https://ai.azure.com' -What "Reading agent '$AgentName'"
$principalId = "$($agent.instance_identity.principal_id)"

if (-not $principalId) {
    throw "Agent '$AgentName' has no instance identity. Deploy a version first (deploy-hosted-agent.yml)."
}

Write-Note "Principal: $principalId"

$owners = @((Invoke-RestJson -Method get -Url "$GraphBase/servicePrincipals/$principalId/owners" -Resource 'https://graph.microsoft.com' -What 'Reading the identity owners').value)
foreach ($owner in $owners) {
    $kind = if ("$($owner.'@odata.type')" -eq '#microsoft.graph.user') { 'user' } else { 'service principal' }
    Write-Note "- current owner: $($owner.displayName) ($kind)"
}

# ---------------------------------------------------------------------------------------------
# The grant. Additive: the pipeline that deploys versions keeps its ownership.
# ---------------------------------------------------------------------------------------------

Write-Step 'Ownership'

if (@($owners | Where-Object { "$($_.id)" -eq "$($ownerUser.id)" }).Count -gt 0) {
    Write-Exists "$($ownerUser.displayName) already owns the agent identity"
}
elseif ($PSCmdlet.ShouldProcess("$AgentName ($principalId)", "Add $OwnerUserPrincipalName as owner")) {
    Invoke-RestJson -Method post -Url "$GraphBase/servicePrincipals/$principalId/owners/`$ref" -Resource 'https://graph.microsoft.com' -What 'Adding the owner' -Body @{
        '@odata.id' = "$GraphBase/directoryObjects/$($ownerUser.id)"
    } | Out-Null

    Write-Created "$($ownerUser.displayName) now answers for agent '$AgentName'"
}

Write-Step 'Done'
Write-Host @"
  The agent identity has a human owner alongside the pipeline that deploys it. That is the Agent
  365 lesson in one line: the pipeline creates agents; a person answers for them.
"@ -ForegroundColor Green
