#Requires -Version 7.0

<#
.SYNOPSIS
    Snapshots everything that could change when a Teams channel is added to the hosted agent, so a
    portal action can be reverse-engineered into IaC.

.DESCRIPTION
    The Teams/M365 channel is added and published in the Foundry portal today - there is no agent-
    data-plane route for it (surveyed across api-versions). But "portal-only" has been wrong in
    this repo before: the Work IQ connection's decisive field was found by creating one in the
    portal and diffing it against an API-made one. This script is that method as a tool.

    Run it with -Label before, do the portal step (add the Teams channel, publish), run it again
    with -Label after, and diff the two JSON files. What the diff shows - most likely a new
    Microsoft.BotService/botServices resource with a Teams channel, plus changed fields on the
    agent endpoint (publish_approval_status, protocol_configuration.activity) - is exactly what to
    codify: Bicep for the ARM resources, a data-plane PATCH for the agent fields.

    Read-only. It captures:
      - every ARM resource in the resource group (a new Azure Bot would appear here);
      - the full agent record from the Foundry data plane;
      - any Azure Bot Service resources and their channels, expanded.

.PARAMETER Label
    A label for this snapshot, e.g. 'before' or 'after'. Becomes part of the filename.

.PARAMETER Environment
    Environment to snapshot.

.PARAMETER ProjectEndpoint
    Foundry project endpoint. Defaults to the environment's FOUNDRY_PROJECT_ENDPOINT variable.

.PARAMETER AgentName
    The hosted agent to snapshot.

.PARAMETER ResourceGroupName
    Resource group holding the platform. Defaults to rg-caesarea-<environment>.

.PARAMETER OutputDirectory
    Where to write the snapshot. Defaults to a gitignored capture folder under the repo.

.PARAMETER Repository
    owner/name of the GitHub repository, used only to read the endpoint default.

.EXAMPLE
    ./scripts/Capture-AgentChannelState.ps1 -Label before

.EXAMPLE
    ./scripts/Capture-AgentChannelState.ps1 -Label after
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z0-9-]{1,40}$')]
    [string] $Label,
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $ProjectEndpoint,
    [string] $AgentName = 'caesarea-operations',
    [string] $ResourceGroupName,
    [string] $OutputDirectory,
    [string] $Repository
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

function Invoke-Checked {
    param([Parameter(Mandatory)] [scriptblock] $Command, [Parameter(Mandatory)] [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'az is not on PATH.' }
if (-not $ResourceGroupName) { $ResourceGroupName = "rg-caesarea-$Environment" }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot '..' 'artifacts' 'channel-capture' }

if (-not $ProjectEndpoint) {
    if (-not $Repository -and (Get-Command gh -ErrorAction SilentlyContinue)) {
        $originUrl = Invoke-Checked { git -C "$PSScriptRoot/.." remote get-url origin } 'Reading the origin remote'
        $Repository = ("$originUrl" -replace '^.*github\.com[:/]', '' -replace '\.git$', '').Trim()
    }
    if ($Repository) {
        $value = gh api "repos/$Repository/environments/$Environment/variables/FOUNDRY_PROJECT_ENDPOINT" --jq '.value' 2>&1
        if ($LASTEXITCODE -eq 0) { $ProjectEndpoint = "$value".Trim() }
        $global:LASTEXITCODE = 0
    }
    if (-not $ProjectEndpoint) { throw 'Could not resolve FOUNDRY_PROJECT_ENDPOINT; pass -ProjectEndpoint.' }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

Write-Step "Capturing '$Label' state"
Write-Note "Resource group: $ResourceGroupName"
Write-Note "Project:        $ProjectEndpoint"

$snapshot = [ordered]@{
    label          = $Label
    capturedAt     = (Get-Date).ToUniversalTime().ToString('o')
    resourceGroup  = $ResourceGroupName
    projectEndpoint = $ProjectEndpoint
    agentName      = $AgentName
}

# 1. Every ARM resource in the group. A Teams channel backed by an Azure Bot shows up here.
$snapshot.armResources = (Invoke-Checked {
    az resource list --resource-group $ResourceGroupName --query "sort_by([].{name:name, type:type, id:id}, &type)" --output json
} 'Listing ARM resources') | ConvertFrom-Json

# 2. Any Azure Bot Service resources, with their channels expanded - the most likely home of a
#    Teams binding.
$bots = (Invoke-Checked {
    az resource list --resource-group $ResourceGroupName --resource-type 'Microsoft.BotService/botServices' --output json
} 'Listing bot services') | ConvertFrom-Json
$snapshot.botServices = @()
foreach ($bot in @($bots)) {
    $channels = az resource list --resource-group $ResourceGroupName --resource-type 'Microsoft.BotService/botServices/channels' --query "[?contains(id, '$($bot.name)')]" --output json 2>$null | ConvertFrom-Json
    $global:LASTEXITCODE = 0
    $snapshot.botServices += [ordered]@{ bot = $bot; channels = @($channels) }
}

# 3. The full agent record from the data plane - publish_approval_status, protocol_configuration,
#    and anything a publish flips.
$token = (Invoke-Checked { az account get-access-token --resource https://ai.azure.com --query accessToken --output tsv } 'Getting a Foundry token').Trim()
$agentResponse = Invoke-WebRequest -Uri "$ProjectEndpoint/agents/$AgentName`?api-version=v1" -Headers @{ Authorization = "Bearer $token" } -SkipHttpErrorCheck
$agentText = if ($agentResponse.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($agentResponse.Content) } else { "$($agentResponse.Content)" }
$snapshot.agentRecord = if ($agentResponse.StatusCode -eq 200) { $agentText | ConvertFrom-Json } else { "HTTP $($agentResponse.StatusCode): $agentText" }

$path = Join-Path $OutputDirectory "$stamp-$Label.json"
$snapshot | ConvertTo-Json -Depth 20 | Set-Content -Path $path -Encoding utf8

Write-Step 'Done'
Write-Note "Wrote $path"
Write-Note "ARM resources: $(@($snapshot.armResources).Count), bot services: $(@($snapshot.botServices).Count)"
Write-Host @"
  Capture the 'before' now, add the Teams channel and publish in the Foundry portal, then capture
  'after'. Compare the two files - the new ARM resource(s) and the changed agent fields are the IaC
  to write. On Windows: Compare-Object (Get-Content <before>) (Get-Content <after>), or just open
  both. Send the diff and it becomes Bicep plus a data-plane PATCH.
"@ -ForegroundColor Green
