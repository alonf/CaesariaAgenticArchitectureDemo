#Requires -Version 7.0

<#
.SYNOPSIS
    Grants the hosted agent's identity the EnergyHub.Read application role.

.DESCRIPTION
    The last link in the chain, and the only one that cannot be automated in the release pipeline.

    The Energy Hub's ingress rejects any token that does not carry its audience, and the agent can
    only obtain such a token if its identity holds an application role on the Energy Hub's service
    principal. That identity is created by the Foundry platform when the agent's first version is
    deployed, so it cannot be granted anything in advance.

    Assigning an application role is a Microsoft Graph write, not an Azure RBAC one. Doing it from
    CI would mean granting the pipeline AppRoleAssignment.ReadWrite.All - permission to grant any
    application any role in the tenant, permanently, to get one assignment made once. That is a bad
    trade, so this runs as a person who already holds the authority, and the pipeline reports what it
    would need instead of holding the rights to do it.

    Idempotent: an existing assignment is left alone.

.PARAMETER Environment
    Environment whose agent and API registration to wire together.

.PARAMETER AgentName
    Name of the hosted agent. Must match deploy-hosted-agent.yml.

.PARAMETER ProjectEndpoint
    Foundry project endpoint. Defaults to the FOUNDRY_PROJECT_ENDPOINT variable on the GitHub
    environment, which Sync-PlatformVariables.ps1 wrote.

.PARAMETER EnergyHubApiClientId
    Application ID of the Energy Hub API. Defaults to the ENERGYHUB_API_CLIENT_ID variable on the
    GitHub environment, which Bootstrap-EnergyHubApi.ps1 wrote.

.PARAMETER Repository
    owner/name of the GitHub repository, used only to read those defaults.

.EXAMPLE
    ./scripts/Grant-AgentEnergyHubAccess.ps1 -Environment dev -WhatIf

.EXAMPLE
    ./scripts/Grant-AgentEnergyHubAccess.ps1 -Environment dev
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $AgentName = 'caesarea-operations',
    [string] $ProjectEndpoint,
    [string] $EnergyHubApiClientId,
    [string] $Repository
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$AppRoleValue = 'EnergyHub.Read'
$GraphBase = 'https://graph.microsoft.com/v1.0'

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Exists { param([string] $Message) Write-Host "  [exists] $Message" -ForegroundColor DarkGray }
function Write-Created { param([string] $Message) Write-Host "  [granted] $Message" -ForegroundColor Green }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

function Invoke-Checked {
    param([Parameter(Mandatory)] [scriptblock] $Command, [Parameter(Mandatory)] [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

function Get-GitHubVariable {
    param([Parameter(Mandatory)] [string] $Name)

    $output = gh api "repos/$Repository/environments/$Environment/variables/$Name" --jq '.value' 2>&1
    if ($LASTEXITCODE -ne 0) {
        $global:LASTEXITCODE = 0
        return $null
    }
    return "$output".Trim()
}

Write-Step 'Checking prerequisites'

foreach ($tool in @('az')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool is not on PATH." }
}

if ((-not $ProjectEndpoint -or -not $EnergyHubApiClientId)) {
    if (-not (Get-Command 'gh' -ErrorAction SilentlyContinue)) {
        throw 'gh is not on PATH, so the defaults cannot be read. Pass -ProjectEndpoint and -EnergyHubApiClientId explicitly.'
    }
    if (-not $Repository) {
        $originUrl = Invoke-Checked { git -C "$PSScriptRoot/.." remote get-url origin } 'Reading the origin remote'
        $Repository = ("$originUrl" -replace '^.*github\.com[:/]', '' -replace '\.git$', '').Trim()
    }
    if (-not $ProjectEndpoint) { $ProjectEndpoint = Get-GitHubVariable 'FOUNDRY_PROJECT_ENDPOINT' }
    if (-not $EnergyHubApiClientId) { $EnergyHubApiClientId = Get-GitHubVariable 'ENERGYHUB_API_CLIENT_ID' }
}

if (-not $ProjectEndpoint) { throw 'FOUNDRY_PROJECT_ENDPOINT is not set. Run ./scripts/Sync-PlatformVariables.ps1 first.' }
if (-not $EnergyHubApiClientId) { throw 'ENERGYHUB_API_CLIENT_ID is not set. Run ./scripts/Bootstrap-EnergyHubApi.ps1 first.' }

Write-Note "Agent:       $AgentName"
Write-Note "Project:     $ProjectEndpoint"
Write-Note "Energy Hub:  $EnergyHubApiClientId"

# ---------------------------------------------------------------------------------------------
# The agent's identity. It exists only after a version has been deployed.
# ---------------------------------------------------------------------------------------------

Write-Step 'Agent identity'

$token = (Invoke-Checked {
    az account get-access-token --resource https://ai.azure.com --query accessToken --output tsv
} 'Getting a Foundry data-plane token').Trim()

$agentResponse = Invoke-WebRequest -Uri "$ProjectEndpoint/agents/$AgentName`?api-version=v1" `
    -Headers @{ Authorization = "Bearer $token" } -SkipHttpErrorCheck

if ($agentResponse.StatusCode -eq 404) {
    throw "Agent '$AgentName' does not exist yet. Deploy it first: gh workflow run deploy-hosted-agent.yml -f environmentName=$Environment"
}
if ($agentResponse.StatusCode -ne 200) {
    throw "Reading agent '$AgentName' failed with HTTP $($agentResponse.StatusCode): $($agentResponse.Content)"
}

$agent = $agentResponse.Content | ConvertFrom-Json
$agentPrincipalId = $agent.instance_identity.principal_id

if (-not $agentPrincipalId) {
    throw "Agent '$AgentName' has no instance identity. It may still be provisioning; wait for its version to become active and retry."
}

Write-Note "Principal:   $agentPrincipalId"

# The principal ID identifies a service principal in this tenant. If Graph cannot see it yet, the
# assignment below would fail with a message about a malformed request rather than a missing object.
$agentSp = (Invoke-Checked {
    az rest --method get --url "$GraphBase/servicePrincipals/$agentPrincipalId" --output json
} "Reading the agent's service principal") | ConvertFrom-Json

Write-Note "Display:     $($agentSp.displayName)"

# ---------------------------------------------------------------------------------------------
# The Energy Hub's service principal and the role to grant.
# ---------------------------------------------------------------------------------------------

Write-Step "Application role '$AppRoleValue'"

$energyHubSps = (Invoke-Checked {
    az rest --method get --url "$GraphBase/servicePrincipals?`$filter=appId eq '$EnergyHubApiClientId'" --output json
} 'Reading the Energy Hub service principal') | ConvertFrom-Json

if ($energyHubSps.value.Count -eq 0) {
    throw "No service principal for application $EnergyHubApiClientId. Run ./scripts/Bootstrap-EnergyHubApi.ps1 -Environment $Environment."
}

$energyHubSp = $energyHubSps.value[0]
$appRole = $energyHubSp.appRoles | Where-Object { $_.value -eq $AppRoleValue } | Select-Object -First 1

if (-not $appRole) {
    throw "The Energy Hub service principal has no '$AppRoleValue' role. Re-run ./scripts/Bootstrap-EnergyHubApi.ps1 -Environment $Environment."
}

Write-Note "Resource:    $($energyHubSp.id)"
Write-Note "Role:        $($appRole.id)"

$existing = (Invoke-Checked {
    az rest --method get --url "$GraphBase/servicePrincipals/$agentPrincipalId/appRoleAssignments" --output json
} 'Listing the agent existing role assignments') | ConvertFrom-Json

$alreadyGranted = @($existing.value | Where-Object {
    $_.resourceId -eq $energyHubSp.id -and $_.appRoleId -eq $appRole.id
})

if ($alreadyGranted.Count -gt 0) {
    Write-Exists "$AppRoleValue on the Energy Hub"
}
elseif ($PSCmdlet.ShouldProcess("$($agentSp.displayName) -> $AppRoleValue", 'Grant application role')) {
    $file = New-TemporaryFile
    try {
        @{
            principalId = $agentPrincipalId
            resourceId  = $energyHubSp.id
            appRoleId   = $appRole.id
        } | ConvertTo-Json | Set-Content -Path $file -Encoding utf8

        Invoke-Checked {
            az rest --method post `
                --url "$GraphBase/servicePrincipals/$agentPrincipalId/appRoleAssignments" `
                --headers 'Content-Type=application/json' --body "@$file" --output none
        } "Granting '$AppRoleValue'" | Out-Null
    }
    finally {
        Remove-Item $file -ErrorAction SilentlyContinue
    }

    Write-Created "$AppRoleValue on the Energy Hub"
}

Write-Step 'Done'
Write-Host @"
  The agent may now obtain a token the Energy Hub will accept.

  Note that an application role assignment is not instantaneous - a token minted in the next minute
  or so may still be missing the role. If the agent reports a 403, wait and ask again before
  changing anything.

  Verify by asking the deployed agent about a real asset (L-417 or L-528):

    gh workflow run deploy-hosted-agent.yml -f environmentName=$Environment
"@ -ForegroundColor Green
