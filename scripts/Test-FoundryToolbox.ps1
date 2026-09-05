#Requires -Version 7.0

<#
.SYNOPSIS
    Verifies that a Foundry Toolbox exists and that its DEFAULT version carries exactly the expected
    tool - failing loudly on anything less.

.DESCRIPTION
    A gate, not a report. The hosted agent resolves the toolbox's default version through
    AddFoundryToolboxes, so this script verifies that chain: the toolbox exists, it has a default
    version, that version is readable, and it carries the expected tool exactly once, bound to the
    expected connection. Every other outcome - missing toolbox, unreadable versions, no default,
    absent tool, duplicated tool, wrong connection, or any read failing for a reason other than a
    true 404 - exits non-zero with an explanation, so a pipeline or a rehearsal fails on the broken
    prerequisite rather than on a confusing symptom an hour later.

    An earlier version printed notes where this one fails, and exited 0 regardless - which let a
    project with five versions, a stale default and a duplicated tool read as healthy.

    The default version's own MCP endpoint is then exercised: a JSON-RPC initialize, then
    tools/list. For a caller who has not consented to the Work IQ connection, tools/list answers
    with a structured error embedding CONSENT_REQUIRED - which this gate treats as HEALTHY, because
    it proves the transport, the version and the tool source while a person's consent is simply
    pending. An earlier draft assumed any probe from a bare client must fail and skipped it; a live
    probe disproved that.

    A correction is embedded in this file's history and worth stating, because the wrong version of it
    was committed. Toolboxes ARE creatable through the data plane:

      POST {projectEndpoint}/toolboxes/{name}/versions?api-version=v1

    An earlier probe tried POST on the collection and PUT on the named resource, got 405 from both,
    and concluded "portal only". It never tried the versions sub-resource, which is where creation
    lives. ./scripts/Connect-WorkIQ.ps1 uses it, and there is no portal step.

.PARAMETER ToolboxName
    The toolbox to look for.

.PARAMETER ExpectedTool
    The tool type the default version must carry, exactly once.

.PARAMETER ExpectedConnectionName
    The project connection the tool must be bound to. Compared by name against the tail of the
    tool's project_connection_id.

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
    [string] $ExpectedTool = 'work_iq_preview',
    [string] $ExpectedConnectionName = 'caesarea-workiq',
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

if ($response.StatusCode -eq 404) {
    Write-Missing "'$ToolboxName' does not exist"

    Write-Step 'Create it'

    Write-Host @"
  Toolboxes are created through the data plane, so this is scripted:

      ./scripts/Connect-WorkIQ.ps1 -Environment $Environment

  That provisions the Work IQ service principal, registers the client app, grants admin consent,
  creates the connection, registers its OAuth redirect URI and ensures the toolbox's default
  version carries the tool. Then run this script again; it exits 0 when the chain is healthy.
"@ -ForegroundColor Yellow

    exit 1
}

if ($response.StatusCode -ne 200) {
    throw "Reading toolbox '$ToolboxName' failed with HTTP $($response.StatusCode): $($response.Content)"
}

$toolbox = $response.Content | ConvertFrom-Json
Write-Found "'$ToolboxName' exists"

# Everything below is a hard requirement. The default version is the ONLY one the hosted runtime
# resolves, so listing other versions is context; verifying the default is the gate.
$failures = [System.Collections.Generic.List[string]]::new()

$defaultVersion = if ($toolbox.PSObject.Properties.Name -contains 'default_version' -and $toolbox.default_version) {
    "$($toolbox.default_version)"
}
else {
    $null
}

if (-not $defaultVersion) {
    $failures.Add("The toolbox has no default_version. A toolbox without one serves no tools; run ./scripts/Connect-WorkIQ.ps1 -Environment $Environment.")
}
else {
    Write-Note "default_version: $defaultVersion"
}

# Tools live on a VERSION, not on the toolbox record, so reading the record alone always looks
# empty - which an earlier version of this script reported as "advertises no tools".
$versions = Invoke-WebRequest -Uri "$ProjectEndpoint/toolboxes/$ToolboxName/versions`?api-version=v1" `
    -Headers $headers -SkipHttpErrorCheck

if ($versions.StatusCode -ne 200) {
    $failures.Add("Reading the versions failed with HTTP $($versions.StatusCode): $($versions.Content)")
}
elseif ($defaultVersion) {
    $all = @(($versions.Content | ConvertFrom-Json).data)

    if ($all.Count -eq 0) {
        $failures.Add("The toolbox has no versions at all; run ./scripts/Connect-WorkIQ.ps1 -Environment $Environment.")
    }

    foreach ($version in $all) {
        $tools = @($version.tools | ForEach-Object { $_.type })
        $marker = if ("$($version.version)" -eq $defaultVersion) { ' (default)' } else { '' }
        Write-Note "version $($version.version)$($marker): $(if ($tools.Count) { $tools -join ', ' } else { '(no tools)' })"
    }

    $current = @($all | Where-Object { "$($_.version)" -eq $defaultVersion }) | Select-Object -First 1

    if (-not $current) {
        $failures.Add("default_version is $defaultVersion but no such version exists.")
    }
    else {
        $expectedTools = @($current.tools | Where-Object { "$($_.type)" -eq $ExpectedTool })
        $allTools = @($current.tools)

        if ($expectedTools.Count -eq 0) {
            $failures.Add("The default version carries no '$ExpectedTool' tool. The hosted agent would start and answer, just without Work IQ.")
        }
        elseif ($expectedTools.Count -gt 1) {
            # Seen live: a version with the same tool twice, from a double-posted setup. Which of
            # the two the proxy serves is not defined anywhere, so it fails rather than being
            # shrugged past.
            $failures.Add("The default version carries '$ExpectedTool' $($expectedTools.Count) times; a duplicated tool is drift, not redundancy.")
        }
        elseif ($allTools.Count -ne 1) {
            $extras = @($allTools | Where-Object { "$($_.type)" -ne $ExpectedTool } | ForEach-Object { "$($_.type)" })
            $failures.Add("The default version carries unexpected extra tool(s): $($extras -join ', ').")
        }
        else {
            $connectionId = "$($expectedTools[0].project_connection_id)"
            if ($connectionId -notmatch "/connections/$([regex]::Escape($ExpectedConnectionName))$") {
                $failures.Add("The '$ExpectedTool' tool is bound to '$connectionId', not to connection '$ExpectedConnectionName'.")
            }
        }
    }
}

# ---------------------------------------------------------------------------------------------
# The endpoint itself, not only its record. Skipped when the configuration already failed - the
# probe would only restate the problem with a worse error.
# ---------------------------------------------------------------------------------------------

if ($failures.Count -eq 0) {
    Write-Step 'MCP endpoint probe'

    $mcpUrl = "$ProjectEndpoint/toolboxes/$ToolboxName/versions/$defaultVersion/mcp?api-version=v1"
    # Streamable-HTTP servers may insist the client accepts SSE even when they answer JSON.
    $probeHeaders = $headers + @{ Accept = 'application/json, text/event-stream' }

    $initBody = @{
        jsonrpc = '2.0'; id = 1; method = 'initialize'
        params  = @{ protocolVersion = '2025-06-18'; capabilities = @{}; clientInfo = @{ name = 'test-foundry-toolbox'; version = '1.0' } }
    } | ConvertTo-Json -Depth 6

    $initResponse = Invoke-WebRequest -Uri $mcpUrl -Method Post -Headers $probeHeaders -ContentType 'application/json' -Body $initBody -SkipHttpErrorCheck

    if ($initResponse.StatusCode -ne 200) {
        $failures.Add("MCP initialize failed with HTTP $($initResponse.StatusCode): $($initResponse.Content)")
    }
    else {
        $init = $initResponse.Content | ConvertFrom-Json
        if (-not ($init.PSObject.Properties.Name -contains 'result')) {
            $failures.Add("MCP initialize returned no result: $($initResponse.Content)")
        }
        else {
            Write-Found "initialize answered: $($init.result.serverInfo.name) $($init.result.serverInfo.version)"

            $listBody = @{ jsonrpc = '2.0'; id = 2; method = 'tools/list'; params = @{} } | ConvertTo-Json -Depth 4
            $listResponse = Invoke-WebRequest -Uri $mcpUrl -Method Post -Headers $probeHeaders -ContentType 'application/json' -Body $listBody -SkipHttpErrorCheck

            if ($listResponse.StatusCode -ne 200) {
                $failures.Add("MCP tools/list failed with HTTP $($listResponse.StatusCode): $($listResponse.Content)")
            }
            else {
                $list = $listResponse.Content | ConvertFrom-Json
                if ($list.PSObject.Properties.Name -contains 'result') {
                    Write-Found 'tools/list succeeded - the caller has consented and the tool resolves'
                }
                elseif ($list.PSObject.Properties.Name -contains 'error' -and "$($list.error.message)" -match 'CONSENT_REQUIRED') {
                    # Healthy: the transport and the tool source work; a person just has not said
                    # yes yet. The hosted agent surfaces the same state as a consent link.
                    Write-Found 'tools/list reports CONSENT_REQUIRED - healthy; this caller has not consented to Work IQ yet'
                }
                else {
                    $failures.Add("MCP tools/list failed in an unexpected way: $($listResponse.Content)")
                }
            }
        }
    }
}

Write-Step 'Verdict'

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Missing $failure }
    Write-Host ''
    Write-Host "  The toolbox is not usable as the hosted agent would resolve it. Fix with:" -ForegroundColor Yellow
    Write-Host "      ./scripts/Connect-WorkIQ.ps1 -Environment $Environment" -ForegroundColor Yellow
    exit 1
}

Write-Found "default version $defaultVersion carries exactly one '$ExpectedTool' bound to '$ExpectedConnectionName', and its MCP endpoint answers"
Write-Host "  AddFoundryToolboxes('$ToolboxName') in the hosted agent will resolve this configuration." -ForegroundColor Green
exit 0
