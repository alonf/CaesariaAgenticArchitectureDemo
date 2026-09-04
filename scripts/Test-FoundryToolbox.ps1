#Requires -Version 7.0

<#
.SYNOPSIS
    Confirms that a Foundry Toolbox exists and is readable, and explains how to create it when it is
    not.

.DESCRIPTION
    Reports whether a toolbox exists and what its current version carries, and exits non-zero when it
    does not - so a pipeline or a rehearsal fails on the missing prerequisite with an explanation
    rather than on a confusing symptom an hour later.

    A correction is embedded in this file's history and worth stating, because the wrong version of it
    was committed. Toolboxes ARE creatable through the data plane:

      POST {projectEndpoint}/toolboxes/{name}/versions?api-version=v1

    An earlier probe tried POST on the collection and PUT on the named resource, got 405 from both,
    and concluded "portal only". It never tried the versions sub-resource, which is where creation
    lives. ./scripts/Connect-WorkIQ.ps1 uses it, and there is no portal step.

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

    # Tools live on a VERSION, not on the toolbox record, so reading the record alone always looks
    # empty - which an earlier version of this script reported as "advertises no tools".
    $versions = Invoke-WebRequest -Uri "$ProjectEndpoint/toolboxes/$ToolboxName/versions`?api-version=v1" `
        -Headers $headers -SkipHttpErrorCheck

    if ($versions.StatusCode -eq 200) {
        $all = @(($versions.Content | ConvertFrom-Json).data)
        if ($all.Count -eq 0) {
            Write-Note 'No versions yet. A toolbox with no version carries no tools; run ./scripts/Connect-WorkIQ.ps1.'
        }
        foreach ($version in $all) {
            $tools = @($version.tools | ForEach-Object { $_.type })
            Write-Note "version $($version.version): $(if ($tools.Count) { $tools -join ', ' } else { '(no tools)' })"
        }
    }
    else {
        Write-Note "Could not read versions (HTTP $($versions.StatusCode))."
    }

    Write-Step 'Done'
    Write-Host "  The toolbox is present. AddFoundryToolboxes('$ToolboxName') in the hosted agent will find it." -ForegroundColor Green
    exit 0
}

if ($response.StatusCode -ne 404) {
    throw "Reading toolbox '$ToolboxName' failed with HTTP $($response.StatusCode): $($response.Content)"
}

Write-Missing "'$ToolboxName' does not exist"

Write-Step 'Create it'

Write-Host @"
  Toolboxes are created through the data plane, so this is scripted:

      ./scripts/Connect-WorkIQ.ps1 -Environment $Environment

  That provisions the Work IQ service principal, registers the client app, grants admin consent,
  creates the connection, registers its OAuth redirect URI and creates the toolbox version. Then run
  this script again; it exits 0 when the toolbox is there.
"@ -ForegroundColor Yellow

exit 1
