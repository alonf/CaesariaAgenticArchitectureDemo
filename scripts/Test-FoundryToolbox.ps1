#Requires -Version 7.0

<#
.SYNOPSIS
    Confirms that a Foundry Toolbox exists and is readable, and explains how to create it when it is
    not.

.DESCRIPTION
    This is the one part of the Caesarea deployment that cannot be automated, and this script exists
    to make that honest rather than invisible.

    Foundry Toolboxes are readable through the project data plane and creatable nowhere reachable:

      GET  {projectEndpoint}/toolboxes?api-version=v1        200, lists toolboxes
      GET  {projectEndpoint}/toolboxes/{name}?api-version=v1 200, or 404 with a clear message
      POST {projectEndpoint}/toolboxes                       405 Method Not Allowed
      PUT  {projectEndpoint}/toolboxes/{name}                405 Method Not Allowed

    Both `v1` and `2025-05-15-preview` are supported api-versions - they answer 405, not "API version
    not supported" - so this is a deliberate read-only surface rather than a wrong guess at the URL.
    There is no `Microsoft.CognitiveServices/.../toolboxes` ARM type either, and Azure.AI.Projects
    2.1.0-beta.4 exposes no create. So a Toolbox is a one-time portal step per tenant.

    What this script does instead is verify. It reports whether the toolbox exists, what tools it
    advertises, and exits non-zero when it does not - so a pipeline or a rehearsal fails on the
    missing prerequisite with an explanation, rather than on a confusing symptom an hour later.

.PARAMETER ToolboxName
    The toolbox to look for.

.PARAMETER Environment
    Environment whose FOUNDRY_PROJECT_ENDPOINT to read, when one is not supplied directly.

.PARAMETER ProjectEndpoint
    Foundry project endpoint. Defaults to the environment's FOUNDRY_PROJECT_ENDPOINT variable.

.PARAMETER Repository
    owner/name of the GitHub repository, used only to read that default.

.EXAMPLE
    ./scripts/Test-FoundryToolbox.ps1

.EXAMPLE
    ./scripts/Test-FoundryToolbox.ps1 -ToolboxName caesarea-workiq -Environment dev
#>

[CmdletBinding()]
param(
    [string] $ToolboxName = 'caesarea-workiq',
    [ValidateSet('dev', 'prod')]
    [string] $Environment = 'dev',
    [string] $ProjectEndpoint,
    [string] $Repository
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Found { param([string] $Message) Write-Host "  [found] $Message" -ForegroundColor Green }
function Write-Missing { param([string] $Message) Write-Host "  [missing] $Message" -ForegroundColor Yellow }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

function Invoke-Checked {
    param([Parameter(Mandatory)] [scriptblock] $Command, [Parameter(Mandatory)] [string] $What)

    $output = & $Command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed (exit $LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }
    return $output
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

Write-Note "Project:  $ProjectEndpoint"
Write-Note "Toolbox:  $ToolboxName"

$token = (Invoke-Checked {
    az account get-access-token --resource https://ai.azure.com --query accessToken --output tsv
} 'Getting a Foundry data-plane token').Trim()

$headers = @{
    Authorization      = "Bearer $token"
    'Foundry-Features' = 'Toolboxes=V1Preview'
}

# ---------------------------------------------------------------------------------------------
# What exists.
# ---------------------------------------------------------------------------------------------

Write-Step 'Toolboxes in this project'

$listResponse = Invoke-WebRequest -Uri "$ProjectEndpoint/toolboxes?api-version=v1" -Headers $headers -SkipHttpErrorCheck

if ($listResponse.StatusCode -ne 200) {
    throw "Listing toolboxes failed with HTTP $($listResponse.StatusCode): $($listResponse.Content)"
}

$all = @(($listResponse.Content | ConvertFrom-Json).data)

if ($all.Count -eq 0) {
    Write-Note 'none'
}
else {
    foreach ($item in $all) { Write-Note "- $($item.name)" }
}

# ---------------------------------------------------------------------------------------------
# The one we need.
# ---------------------------------------------------------------------------------------------

Write-Step "Toolbox '$ToolboxName'"

$response = Invoke-WebRequest -Uri "$ProjectEndpoint/toolboxes/$ToolboxName`?api-version=v1" -Headers $headers -SkipHttpErrorCheck

if ($response.StatusCode -eq 200) {
    $toolbox = $response.Content | ConvertFrom-Json
    Write-Found "'$ToolboxName' exists"

    if ($toolbox.PSObject.Properties.Name -contains 'tools' -and $toolbox.tools) {
        foreach ($tool in $toolbox.tools) {
            $label = if ($tool.PSObject.Properties.Name -contains 'name') { $tool.name } else { $tool }
            Write-Note "tool: $label"
        }
    }
    else {
        Write-Note 'It advertises no tools yet. A toolbox with no tool source connected is reachable and useless;'
        Write-Note 'add the connection in the portal before expecting the agent to discover anything.'
    }

    Write-Step 'Done'
    Write-Host "  The toolbox is present. AddFoundryToolboxes('$ToolboxName') in the hosted agent will find it." -ForegroundColor Green
    exit 0
}

if ($response.StatusCode -ne 404) {
    throw "Reading toolbox '$ToolboxName' failed with HTTP $($response.StatusCode): $($response.Content)"
}

Write-Missing "'$ToolboxName' does not exist"

Write-Step 'Create it in the Foundry portal'
Write-Host @"
  This is the one step in this repository that cannot be scripted. It is not an oversight:

    GET  {projectEndpoint}/toolboxes        200   - listing works
    POST {projectEndpoint}/toolboxes        405   - creating does not
    PUT  {projectEndpoint}/toolboxes/{name} 405

  Both supported api-versions answer 405 rather than "API version not supported", there is no ARM
  resource type for toolboxes, and Azure.AI.Projects exposes no create. So it is a portal step, once
  per tenant, until the API catches up.

  In https://ai.azure.com, open this project and:

    1. Go to Toolboxes and create one named exactly:  $ToolboxName
    2. Add the tool source the agent needs - for the Work IQ scenario, a Microsoft Graph
       connection with delegated (per-user) access, not application access. The distinction is the
       whole point: the agent should read the caller's own files, not everyone's.
    3. Consent to the connection as yourself when prompted. Until someone consents, the toolbox
       enumerates nothing and the container reports it as consent-pending rather than failing.

  Then run this script again. It exits 0 when the toolbox is there.
"@ -ForegroundColor Yellow

exit 1
