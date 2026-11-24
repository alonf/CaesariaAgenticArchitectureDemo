#Requires -Version 7.0
<#
.SYNOPSIS
    Rehearses the local Unit00 API path and saves actual evidence; does not record UI footage.
.DESCRIPTION
    Resets only local demo fixtures, restores via CommandCenter.Api, verifies EnergyHub.Api,
    checks the ControllerFault failure, then leaves the healthy Lights On fixture ready.
    Run only against the pinned local lab. No model or cloud service is invoked.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$scenarioBase = 'https://localhost:7152'
$hubBase = 'https://localhost:7089'
$commandBase = 'https://localhost:7234'
$capture = [ordered]@{
    kind = 'API rehearsal, not UI recording'
    baseline = '89d5a513be97301857dd4f156ce0773103712d51'
    capturedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    physicalDevice = 'SmartPole simulator'
}
function Read-Hub {
    Invoke-RestMethod "$hubBase/api/energy/assets/L-417" -Headers @{ 'X-Correlation-ID' = "unit00-read-$([guid]::NewGuid())" }
}
$capture.preparation = Invoke-RestMethod "$scenarioBase/api/demo-stage/prepare/Deterministic" -Method Post -Headers @{ 'X-Correlation-ID' = 'unit00-prepare' }
$capture.before = Read-Hub
if (-not $capture.before.reportedIsOn -or -not $capture.before.manualOverride -or $capture.before.expectedScheduledState) {
    throw 'Starting fixture does not match Unit00.'
}
$commandCorrelation = "unit00-restore-$([guid]::NewGuid())"
$capture.command = Invoke-RestMethod "$commandBase/api/command-center/assets/L-417/restore-scheduled-mode" -Method Post -Headers @{ 'X-Correlation-ID' = $commandCorrelation }
$capture.after = Read-Hub
if ($capture.after.reportedIsOn -or $capture.after.desiredIsOn -or $capture.after.manualOverride -or
    $capture.after.lastCommand.status -ne 'Succeeded' -or
    $capture.after.stateRevision -le $capture.before.stateRevision -or
    $capture.after.lastCommand.correlationId -ne $commandCorrelation) {
    throw 'Hub did not verify the repair.'
}
$allActivity = Invoke-RestMethod "$hubBase/api/energy/assets/L-417/activity?limit=20"
$capture.activity = @($allActivity | Where-Object correlationId -EQ $commandCorrelation)
if (-not ($capture.activity | Where-Object commandStatus -EQ 'Succeeded')) {
    throw 'No matching successful Hub activity was found.'
}
# Exercise the recovery tip against a genuinely faulted local fixture.
$null = Invoke-RestMethod "$scenarioBase/api/demo-scenarios/apply/ControllerFault" -Method Post -Headers @{ 'X-Correlation-ID' = 'unit00-fault-fixture' }
$capture.faultBefore = Read-Hub
$faultReply = Invoke-WebRequest "$commandBase/api/command-center/assets/L-417/restore-scheduled-mode" -Method Post -Headers @{ 'X-Correlation-ID' = 'unit00-fault-command' } -SkipHttpErrorCheck
$capture.faultHttpStatus = [int]$faultReply.StatusCode
$capture.faultProblem = $faultReply.Content | ConvertFrom-Json
$capture.faultAfter = Read-Hub
if ($capture.faultHttpStatus -ne 409 -or -not $capture.faultAfter.reportedIsOn -or $capture.faultAfter.lastCommand.status -ne 'Failed') {
    throw 'Fault fixture did not produce the expected failure.'
}
$null = Invoke-RestMethod "$scenarioBase/api/demo-scenarios/apply/ForgottenOverride" -Method Post -Headers @{ 'X-Correlation-ID' = 'unit00-reset' }
$capture.reset = Read-Hub
if (-not $capture.reset.reportedIsOn -or -not $capture.reset.manualOverride -or $capture.reset.controllerHealth.status -ne 'Healthy') {
    throw 'Reset did not recreate the next lesson starting fixture.'
}
$evidenceFolder = Join-Path $PSScriptRoot 'unit-00-introduction/evidence'
$null = New-Item -ItemType Directory -Force $evidenceFolder
$capture | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $evidenceFolder 'api-rehearsal.json') -Encoding utf8
Write-Output "Verified healthy restore, fault rejection and reset at baseline $($capture.baseline)."
Write-Output "Before revision $($capture.before.stateRevision); after $($capture.after.stateRevision); status $($capture.after.lastCommand.status)."
Write-Output 'This evidence does not replace the required main-app click recording.'
