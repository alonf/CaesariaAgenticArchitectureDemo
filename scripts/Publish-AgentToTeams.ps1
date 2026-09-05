#Requires -Version 7.0

<#
.SYNOPSIS
    Reproduces the messaging half of "Publish to Teams and Microsoft 365" as code: the Azure Bot and
    its Teams channel that front the hosted agent.

.DESCRIPTION
    The Foundry portal's publish button creates an Azure Bot whose messaging endpoint is the agent's
    Activity endpoint and whose app id is the agent's own identity, with the MsTeams channel enabled.
    That footprint was captured (scripts/Capture-AgentChannelState.ps1) and codified as
    infra/modules/teams-channel.bicep. This script deploys it repeatably:

      1. declare the Activity protocol on the agent endpoint (Set-AgentActivityProtocol.ps1);
      2. read the agent's identity (msaAppId) and build its Activity messaging endpoint;
      3. deploy the bot + Teams channel with those values.

    It runs after the agent's first version exists, because the identity it references is minted
    then - the same ordering as the agent's RBAC grants.

    What it does NOT do: the M365 Copilot agent-store registration the portal also performs (the
    publish step that sets the audience and publish_approval_status). Microsoft documents a REST
    publish API, but this script does not yet implement it. Complete that step in the portal with
    your intended audience; see docs/deployment.md. The bot + Teams channel is the messaging bridge.

    Idempotent: the Bicep converges, and re-running reports the same bot.

.PARAMETER Environment
    Environment to publish in.

.PARAMETER ProjectEndpoint
    Foundry project endpoint. Defaults to the environment's FOUNDRY_PROJECT_ENDPOINT variable.

.PARAMETER AgentName
    The hosted agent to front.

.PARAMETER BotName
    Stable name for the Azure Bot. Defaults to <agent>-teams.

.PARAMETER ResourceGroupName
    Resource group to create the bot in. Defaults to rg-caesarea-<environment>.

.PARAMETER Repository
    owner/name of the GitHub repository, used only to read the endpoint default.

.EXAMPLE
    ./scripts/Publish-AgentToTeams.ps1 -Environment dev -WhatIf

.EXAMPLE
    ./scripts/Publish-AgentToTeams.ps1 -Environment dev
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $ProjectEndpoint,
    [string] $AgentName = 'caesarea-operations',
    [string] $BotName,
    [string] $ResourceGroupName,
    [string] $Repository
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The api-version the portal-made bot's endpoint carries. Kept as a constant so a platform change is
# a one-line edit, not a hunt.
$ActivityApiVersion = '2025-11-15-preview'

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
if (-not $BotName) { $BotName = "$AgentName-teams" }

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

Write-Step 'Checking prerequisites'
Write-Note "Project:        $ProjectEndpoint"
Write-Note "Agent:          $AgentName"
Write-Note "Resource group: $ResourceGroupName"
Write-Note "Bot:            $BotName"

# ---------------------------------------------------------------------------------------------
# 1. The Activity protocol, delegated to the script that owns it.
# ---------------------------------------------------------------------------------------------

& "$PSScriptRoot/Set-AgentActivityProtocol.ps1" -Environment $Environment -ProjectEndpoint $ProjectEndpoint -AgentName $AgentName -WhatIf:$WhatIfPreference
if ($LASTEXITCODE -ne 0) { throw 'Declaring the activity protocol failed; see above.' }

# ---------------------------------------------------------------------------------------------
# 2. The agent identity and its Activity endpoint.
# ---------------------------------------------------------------------------------------------

Write-Step 'Reading the agent identity'

$token = (Invoke-Checked { az account get-access-token --resource https://ai.azure.com --query accessToken --output tsv } 'Getting a Foundry token').Trim()
$agent = Invoke-RestMethod -Uri "$ProjectEndpoint/agents/$AgentName`?api-version=v1" -Headers @{ Authorization = "Bearer $token" }
$agentAppId = "$($agent.instance_identity.principal_id)"

if (-not $agentAppId) {
    throw "Agent '$AgentName' has no instance identity yet. Deploy a version first (deploy-hosted-agent.yml)."
}

$messagingEndpoint = "$ProjectEndpoint/agents/$AgentName/endpoint/protocols/activityprotocol?api-version=$ActivityApiVersion"
Write-Note "Agent identity (msaAppId): $agentAppId"
Write-Note "Messaging endpoint:        $messagingEndpoint"

# ---------------------------------------------------------------------------------------------
# 3. The bot + Teams channel.
# ---------------------------------------------------------------------------------------------

Write-Step 'Deploying the Azure Bot and Teams channel'

$templateFile = Join-Path $PSScriptRoot '..' 'infra' 'modules' 'teams-channel.bicep'

if ($PSCmdlet.ShouldProcess($BotName, 'Deploy the Azure Bot and Teams channel')) {
    $deploymentName = "caesarea-teams-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
    Invoke-Checked {
        az deployment group create `
            --resource-group $ResourceGroupName `
            --name $deploymentName `
            --template-file $templateFile `
            --parameters `
                botName=$BotName `
                displayName="$AgentName Bot Service" `
                agentAppId=$agentAppId `
                messagingEndpoint=$messagingEndpoint `
            --output none
    } 'Deploying the Teams channel' | Out-Null

    Write-Note "Deployed as $deploymentName."
}
else {
    Write-Note 'What-if: az deployment group what-if would show the bot + Teams channel.'
    az deployment group what-if `
        --resource-group $ResourceGroupName `
        --name "caesarea-teams-whatif" `
        --template-file $templateFile `
        --parameters botName=$BotName displayName="$AgentName Bot Service" agentAppId=$agentAppId messagingEndpoint=$messagingEndpoint `
        2>&1 | Select-Object -Last 20
}

Write-Step 'Done'
Write-Host @"
  The Azure Bot and its Teams channel are deployed - the messaging bridge that puts $AgentName in
  Teams, authenticating as the agent's own identity, forwarding to its Activity endpoint. This half
  is now IaC and rebuilds with the platform.

  Store registration remains: Foundry portal -> the agent -> Publish -> Direct publish. Choose
  Just you for a personal pilot, or People in your organization for admin-approved distribution.
  Microsoft's REST publish API can automate this step, but this script does not yet call it.
  See docs/deployment.md for audience permissions and the recorded Work IQ channel limitation.
"@ -ForegroundColor Green
