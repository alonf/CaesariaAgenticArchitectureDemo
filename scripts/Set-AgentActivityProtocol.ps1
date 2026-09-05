#Requires -Version 7.0

<#
.SYNOPSIS
    Declares the Activity (Teams/M365) protocol on the hosted agent's endpoint - the one part of
    the Teams-channel path that IS on the scriptable data plane.

.DESCRIPTION
    A Foundry-hosted MAF agent reaches Teams without any M365 Agents SDK code: the platform fronts
    the Activity protocol the same way it fronts A2A, translating a channel's Activity traffic into
    the Responses invocations the container already serves. The first step of that is declaring
    'activity' alongside 'responses' on the agent endpoint, which this script does with a PATCH -
    confirmed to work, and Responses keeps serving.

    What it does NOT do, because the agent data plane does not expose it (surveyed across
    api-versions v1 / 2025-05-15-preview / 2025-11-15-preview - every channels/publish/publications
    route 404s): add the Teams channel and publish. Those are a Foundry-portal action today, very
    likely backed by an Azure Bot ARM resource. Use ./scripts/Capture-AgentChannelState.ps1 to
    snapshot before and after that portal step so the ARM footprint can be codified as Bicep.

    Idempotent: an endpoint that already declares 'activity' is reported and left alone.

.PARAMETER Environment
    Environment whose FOUNDRY_PROJECT_ENDPOINT to read, when one is not supplied directly.

.PARAMETER ProjectEndpoint
    Foundry project endpoint. Defaults to the environment's FOUNDRY_PROJECT_ENDPOINT variable.

.PARAMETER AgentName
    The hosted agent to configure.

.PARAMETER Repository
    owner/name of the GitHub repository, used only to read the endpoint default.

.EXAMPLE
    ./scripts/Set-AgentActivityProtocol.ps1 -WhatIf

.EXAMPLE
    ./scripts/Set-AgentActivityProtocol.ps1 -Environment dev
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $ProjectEndpoint,
    [string] $AgentName = 'caesarea-operations',
    [string] $Repository
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ApiVersion = 'v1'

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Exists { param([string] $Message) Write-Host "  [exists] $Message" -ForegroundColor DarkGray }
function Write-Created { param([string] $Message) Write-Host "  [set] $Message" -ForegroundColor Green }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

function Invoke-Checked {
    param([Parameter(Mandatory)] [scriptblock] $Command, [Parameter(Mandatory)] [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

$script:Token = $null
function Get-Token {
    if (-not $script:Token) {
        $script:Token = (Invoke-Checked { az account get-access-token --resource https://ai.azure.com --query accessToken --output tsv } 'Getting a Foundry token').Trim()
    }
    return $script:Token
}

function Invoke-AgentJson {
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Url,
        [object] $Body,
        [Parameter(Mandatory)] [string] $What
    )

    $arguments = @{
        Method             = $Method
        Uri                = $Url
        Headers            = @{ Authorization = "Bearer $(Get-Token)" }
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $Body) {
        $arguments.Body = ($Body | ConvertTo-Json -Depth 10)
        $arguments.ContentType = 'application/json'
    }

    $response = Invoke-WebRequest @arguments
    $text = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { "$($response.Content)" }
    if ($response.StatusCode -ge 400) {
        throw "$What failed with HTTP $($response.StatusCode): $text"
    }
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json
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

Write-Note "Project: $ProjectEndpoint"
Write-Note "Agent:   $AgentName"

Write-Step "Activity protocol on '$AgentName'"

$agentUrl = "$ProjectEndpoint/agents/$AgentName`?api-version=$ApiVersion"
$agent = Invoke-AgentJson -Method get -Url $agentUrl -What "Reading agent '$AgentName'"

$protocols = @($agent.agent_endpoint.protocols)
Write-Note "current protocols: $($protocols -join ', ')"

if ($protocols -contains 'activity') {
    Write-Exists "endpoint already declares 'activity'"
}
elseif ($PSCmdlet.ShouldProcess($AgentName, "Declare the 'activity' protocol on the endpoint")) {
    # Additive: responses must stay - the container serves it, and A2A/Activity are layered over it,
    # never instead of it. The endpoint keeps every protocol it already had.
    $desired = @($protocols + 'activity' | Select-Object -Unique)

    Invoke-AgentJson -Method patch -Url $agentUrl -What 'Declaring the activity protocol' -Body @{
        agent_endpoint = @{ protocols = $desired }
    } | Out-Null

    # Verified, not assumed: read it back, and confirm Responses is still there.
    $after = Invoke-AgentJson -Method get -Url $agentUrl -What 'Re-reading the agent'
    $afterProtocols = @($after.agent_endpoint.protocols)
    if ($afterProtocols -notcontains 'activity') {
        throw "The PATCH was accepted but 'activity' is not on the endpoint. Inspect before relying on it."
    }
    if ($afterProtocols -notcontains 'responses') {
        throw "'responses' disappeared from the endpoint - the Command Center and the smoke test both depend on it. Restore it before continuing."
    }

    Write-Created "protocols: $($afterProtocols -join ', ')"
}

Write-Step 'Done'
Write-Host @"
  The endpoint declares the Activity protocol. That is the scriptable half of putting the agent in
  Teams; the other half - add the Teams/M365 channel and publish - is a Foundry-portal action
  today (no agent-data-plane route for it, surveyed across api-versions). To make THAT repeatable:

    1. ./scripts/Capture-AgentChannelState.ps1 -Label before
    2. In the Foundry portal, add the Teams/M365 channel to '$AgentName' and publish.
    3. ./scripts/Capture-AgentChannelState.ps1 -Label after
    4. Diff the two snapshots - the ARM resources that appeared (likely an Azure Bot + Teams
       channel) are what to codify as Bicep. See docs/prompts/13-agent365.md.

  Per the A2A lesson, a declared protocol is not a working one until the channel proves it. Rehearse
  the Teams path end to end before the lecture.
"@ -ForegroundColor Green
