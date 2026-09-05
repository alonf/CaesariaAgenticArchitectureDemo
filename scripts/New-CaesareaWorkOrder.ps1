#Requires -Version 7.0
#Requires -Modules Microsoft.Graph.Authentication

<#
.SYNOPSIS
    Creates the Caesarea maintenance work order in the signed-in user's own OneDrive.

.DESCRIPTION
    The evidence the Operations Agent looks for when it investigates a streetlight, placed where a
    real one would live: a document in the person's own Microsoft 365, not a fixture compiled into the
    demo.

    That difference is the whole point of the Work IQ segment. The agent reaches this file through
    Work IQ, asking **as the signed-in user** - so it finds this document because *you* can see it,
    not because the agent was given tenant-wide read access. Run this as yourself and the agent finds
    your copy; a colleague runs it and the agent finds theirs. Nobody's identity is written into this
    repository.

    Idempotent. An existing file with the same name in the same folder is left alone unless -Force is
    passed, because overwriting someone's document is not something a demo script should do quietly.

    Uses Connect-MgGraph rather than `az rest`, and the reason is worth knowing before reaching for
    the CLI: the Azure CLI's Microsoft Graph token carries no Files or Sites scopes at all, so every
    OneDrive call comes back `itemNotFound` - which reads as "you have no OneDrive" rather than "this
    token may not look". Connect-MgGraph asks for Files.ReadWrite explicitly and signs you in for it.

.PARAMETER FolderPath
    Folder to create under the OneDrive root.

.PARAMETER FileName
    Name of the work order document.

.PARAMETER AssetId
    The asset the work order concerns. Must match the seeded scenario for the demo to line up.

.PARAMETER WorkOrderId
    The work order identifier that appears in the document and in the agent's answer.

.PARAMETER Force
    Overwrite an existing file.

.EXAMPLE
    ./scripts/New-CaesareaWorkOrder.ps1 -WhatIf

.EXAMPLE
    ./scripts/New-CaesareaWorkOrder.ps1
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $FolderPath = 'Caesarea Smart City',
    [string] $FileName = 'WO-8732 Streetlight L-417 maintenance.md',
    [string] $AssetId = 'L-417',
    [string] $WorkOrderId = 'WO-8732',
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$GraphBase = 'https://graph.microsoft.com/v1.0'

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Exists { param([string] $Message) Write-Host "  [exists] $Message" -ForegroundColor DarkGray }
function Write-Created { param([string] $Message) Write-Host "  [created] $Message" -ForegroundColor Green }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

function Get-DriveItemOrNull {
    <#  Only a true 404 is absence. A 403, an expired token, throttling or a network failure would
        otherwise fall through to the write path and replace a document this run never actually
        read - the one way this script could destroy someone's file without being asked to. #>
    param([Parameter(Mandatory)] [string] $Uri)

    try {
        return Invoke-MgGraphRequest -Method GET -Uri $Uri
    }
    catch {
        $isNotFound = $false

        # Invoke-MgGraphRequest surfaces the Graph error body through ErrorDetails; the HTTP status,
        # when present, rides on the exception. Either signal alone is enough to call it absent.
        if ($_.ErrorDetails -and $_.ErrorDetails.Message -match '"code"\s*:\s*"itemNotFound"') {
            $isNotFound = $true
        }
        elseif ($_.Exception.PSObject.Properties['Response'] -and
                $_.Exception.Response -and
                [int]$_.Exception.Response.StatusCode -eq 404) {
            $isNotFound = $true
        }

        if ($isNotFound) { return $null }
        throw
    }
}

Write-Step 'Signing in'

Import-Module Microsoft.Graph.Authentication -ErrorAction Stop

# Files.ReadWrite, delegated. The sign-in prompt is the demo's own point made concrete: this runs as
# a person, and what it can reach is what that person can reach.
$context = Get-MgContext
if (-not $context -or $context.Scopes -notcontains 'Files.ReadWrite') {
    Connect-MgGraph -Scopes 'Files.ReadWrite' -NoWelcome -ErrorAction Stop
    $context = Get-MgContext
}

$me = Invoke-MgGraphRequest -Method GET -Uri "$GraphBase/me?`$select=userPrincipalName,displayName"
Write-Note "User:   $($me.userPrincipalName)"

$drive = Invoke-MgGraphRequest -Method GET -Uri "$GraphBase/me/drive?`$select=id,name,driveType"
Write-Note "Drive:  $($drive.name) ($($drive.driveType))"
Write-Note "Folder: /$FolderPath"
Write-Note "File:   $FileName"

# ---------------------------------------------------------------------------------------------
# The folder.
# ---------------------------------------------------------------------------------------------

Write-Step "Folder '/$FolderPath'"

$encodedFolder = [uri]::EscapeDataString($FolderPath)
$folder = Get-DriveItemOrNull -Uri "$GraphBase/me/drive/root:/$encodedFolder"

if ($folder) {
    Write-Exists "folder '/$FolderPath'"
}
elseif ($PSCmdlet.ShouldProcess("/$FolderPath", 'Create the OneDrive folder')) {
    Invoke-MgGraphRequest -Method POST -Uri "$GraphBase/me/drive/root/children" -Body @{
        name                                = $FolderPath
        folder                              = @{}
        '@microsoft.graph.conflictBehavior' = 'fail'
    } | Out-Null
    Write-Created "folder '/$FolderPath'"
}
else {
    Write-Note 'Skipped; the file cannot be previewed without it.'
    return
}

# ---------------------------------------------------------------------------------------------
# The work order.
# ---------------------------------------------------------------------------------------------

Write-Step "Work order '$FileName'"

$encodedPath = "$encodedFolder/$([uri]::EscapeDataString($FileName))"
$existingItem = Get-DriveItemOrNull -Uri "$GraphBase/me/drive/root:/$encodedPath"
$fileExists = $null -ne $existingItem

if ($fileExists -and -not $Force) {
    Write-Exists "'$FileName' ($($existingItem.webUrl))"
    Write-Note 'Pass -Force to overwrite it.'
}
elseif ($PSCmdlet.ShouldProcess("/$FolderPath/$FileName", $(if ($fileExists) { 'Overwrite the work order' } else { 'Create the work order' }))) {
    # Deliberately consistent with the seeded scenario: the agent's triage table turns "maintenance
    # evidence found" into "forgotten override" rather than "unexplained override", so the wording
    # here is what decides the classification the audience sees.
    $raised = (Get-Date).AddDays(-3).ToString('yyyy-MM-dd')
    $expected = (Get-Date).AddDays(1).ToString('yyyy-MM-dd')

    # The source-of-record line is the beat: the local demo's simulated store tells a similar story
    # about the same asset, so the audience needs one sentence only this document contains. An agent
    # that names Microsoft 365 as where the record lives - and mentions the diffuser - read this
    # file, not a fixture.
    $content = @"
# Work order $WorkOrderId - streetlight $AssetId luminaire maintenance

**Asset:** $AssetId (North Promenade)
**Raised:** $raised
**Status:** In progress
**Expected clearance:** $expected
**Source of record:** Microsoft 365 - this document lives in the owner's OneDrive and is retrieved through Work IQ

## Summary

Scheduled luminaire maintenance on streetlight $AssetId. The pole was switched to **manual override**
so the crew could work safely on the fitting during daylight hours.

## Technician note

Override left in place at end of shift pending a replacement diffuser, which is on order. The lamp
will therefore run against its normal schedule until the override is cleared. No fault is present in
the controller.

## Follow-up

Clear the manual override and return $AssetId to scheduled mode once the diffuser is fitted.

---

Provenance note: this is the Microsoft 365 copy of $WorkOrderId. An assistant that cites this
document is reading the document owner's own OneDrive, with that person's permissions.
"@

    # UTF-8 without a BOM: a BOM survives into the indexed text and shows up as stray characters in
    # whatever the agent quotes back.
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($content)

    # The write is conditional either way. Creating: If-None-Match * fails with 412 if the file
    # appeared between the read above and this PUT. Overwriting: If-Match pins the exact version
    # -Force was approved against, so a copy that changed since the read is not silently replaced.
    $conditionalHeaders = if ($fileExists -and $existingItem.PSObject.Properties['eTag'] -and $existingItem.eTag) {
        @{ 'If-Match' = $existingItem.eTag }
    }
    else {
        @{ 'If-None-Match' = '*' }
    }

    try {
        $item = Invoke-MgGraphRequest -Method PUT `
            -Uri "$GraphBase/me/drive/root:/$encodedPath`:/content" `
            -Headers $conditionalHeaders `
            -Body $bytes `
            -ContentType 'text/markdown'
    }
    catch {
        if ($_.Exception.PSObject.Properties['Response'] -and
            $_.Exception.Response -and
            [int]$_.Exception.Response.StatusCode -eq 412) {
            throw "The file changed (or appeared) after this script read it. Re-run to read the current state; nothing was overwritten."
        }
        throw
    }

    Write-Created "'$FileName'"
    Write-Note "URL: $($item.webUrl)"
}

Write-Step 'Done'
Write-Host @"
  The work order is in your own OneDrive, which is the point: the agent finds it through Work IQ
  because it asks as YOU, not because it was granted tenant-wide read access. Run this as a different
  person and the agent finds that person's copy instead.

  Indexing is not instant. Microsoft 365 has to index the file before Work IQ can retrieve it - if
  the agent reports no evidence in the next few minutes, wait rather than changing anything.

  In the demo, this is the Hosting stage's beat: flip Habitat to FOUNDRY HOSTED on the switchboard
  and click "Ask about $AssetId's work records" in the Command Center. The answer should name
  Microsoft 365 as the source of record and mention the replacement diffuser - two things the
  local simulated store never contained. First use per person shows a Work IQ consent link in the
  Command Center; open it, consent as yourself, and ask again.
"@ -ForegroundColor Green
