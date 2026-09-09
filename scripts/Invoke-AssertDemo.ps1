#Requires -Version 7.0
<#
.SYNOPSIS
    Runs the ASSERT L-417 behavior suite against the local demo.
.DESCRIPTION
    Resets the local scenario per case and scripts supervisor decisions through the existing
    approval API. Run against a dedicated rehearsal instance; do not operate its UI concurrently.
    Results and logs go to evaluation/assert_demo/artifacts. No deployment is performed.
.EXAMPLE
    ./scripts/Invoke-AssertDemo.ps1 -Arm both
.EXAMPLE
    ./scripts/Invoke-AssertDemo.ps1 -PrepareOnly
.EXAMPLE
    ./scripts/Invoke-AssertDemo.ps1 -Arm both -Captured
    Stamps the report CAPTURED / NOT LIVE, for a rehearsal result shown from a stage later.
#>
[CmdletBinding()]
param(
    [ValidateSet('baseline', 'governed', 'both')][string] $Arm = 'governed',
    [string[]] $Case,
    [string] $Model = 'azure/gpt-5.5',
    [switch] $PrepareOnly,
    [switch] $Generate,
    [switch] $Captured,
    [string] $ReviewedDirectory
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path "$PSScriptRoot/..").Path
$python = Join-Path $repositoryRoot '.venv-assert/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $python)) {
    throw 'Install the ASSERT environment first; see evaluation/assert_demo/README.md.'
}
$arguments = @('-m', 'evaluation.assert_demo.run', '--arm', $Arm, '--model', $Model)
foreach ($caseId in $Case) { $arguments += @('--case', $caseId) }
if ($PrepareOnly) { $arguments += '--prepare-only' }
if ($Generate) { $arguments += '--generate' }
if ($Captured) { $arguments += '--captured' }
if ($ReviewedDirectory) { $arguments += @('--reviewed-dir', (Resolve-Path $ReviewedDirectory).Path) }
Push-Location $repositoryRoot
try {
    & $python @arguments
    if ($LASTEXITCODE -ne 0) { throw "ASSERT finished with exit code $LASTEXITCODE; inspect the generated report and logs." }
} finally {
    Pop-Location
}
