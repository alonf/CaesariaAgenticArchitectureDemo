#Requires -Version 7.0

<#
.SYNOPSIS
    Proves the demo's debugger attach on this machine: VS Code, and on Windows Visual Studio 2026,
    each attach their .NET debugger to a test process and let it go again.

.DESCRIPTION
    Runs DebuggerLiveTests from the deterministic test project with CAESAREA_LIVE_DEBUGGER_TESTS
    set, which is the opt-in those tests wait for. The tests attach to their own process and watch
    Debugger.IsAttached exactly the way a demo service does, so a pass here means the switchboard's
    Attach and Detach buttons will work on stage.

    What has to be true before running:

      - VS Code: the demo-attach extension from tools/vscode-demo-attach is installed at the
        committed version (DemoControl's Install/Update button, or
        code --install-extension tools/vscode-demo-attach/demo-attach-<version>.vsix --force),
        with a VS Code window open, reloaded after the install.
      - Visual Studio 2026 (Windows only): the Caesarea solution is open in it. The helper under
        tools/visualstudio-demo-attach is built here if it is missing.

    A precondition that does not hold makes that test skip with the reason, not fail. Run without a
    debugger on this script's own process.

.PARAMETER Debugger
    Which IDE to exercise: All (default), VsCode or VisualStudio.

.PARAMETER NoBuild
    Skip building the test project and the helper; use what is already built.

.EXAMPLE
    ./scripts/Test-DemoDebuggers.ps1

.EXAMPLE
    ./scripts/Test-DemoDebuggers.ps1 -Debugger VisualStudio
#>

[CmdletBinding()]
param(
    [ValidateSet('All', 'VsCode', 'VisualStudio')]
    [string] $Debugger = 'All',
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepositoryRoot = Resolve-Path "$PSScriptRoot/.."
$TestProject = Join-Path $RepositoryRoot 'Tests/Caesarea.Deterministic.Tests/Caesarea.Deterministic.Tests.csproj'
$TestExecutable = Join-Path $RepositoryRoot 'Tests/Caesarea.Deterministic.Tests/bin/Debug/net10.0/Caesarea.Deterministic.Tests'
$HelperProject = Join-Path $RepositoryRoot 'tools/visualstudio-demo-attach/src/VisualStudioDemoAttach.csproj'
$HelperExecutable = Join-Path $RepositoryRoot 'tools/visualstudio-demo-attach/dist/VisualStudioDemoAttach.exe'
$Solution = Join-Path $RepositoryRoot 'Caesarea.slnx'

if ($IsWindows) { $TestExecutable += '.exe' }

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

if (-not $NoBuild) {
    Write-Step 'Building the test project'
    dotnet build $TestProject --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'The test project did not build.' }
}

if ($IsWindows -and $Debugger -ne 'VsCode') {
    if (-not $NoBuild -or -not (Test-Path $HelperExecutable)) {
        Write-Step 'Building the Visual Studio helper'
        dotnet build $HelperProject --configuration Release --nologo -v q
        if ($LASTEXITCODE -ne 0) { throw 'The Visual Studio helper did not build.' }
    }

    Write-Step 'Visual Studio instances'
    # Informational: the test itself skips with the same message when no instance qualifies.
    & $HelperExecutable status --solution $Solution | Out-Host
    $global:LASTEXITCODE = 0
}
elseif ($Debugger -eq 'VisualStudio') {
    Write-Note 'Visual Studio runs on Windows only; the Visual Studio test will skip here.'
}

Write-Step "Live debugger round trips ($Debugger)"
$env:CAESAREA_LIVE_DEBUGGER_TESTS = '1'

# The test executable's own runner is used rather than dotnet test, so the filter is a plain class
# or method name and the output reads the same on every platform.
$filter = switch ($Debugger) {
    'VsCode' { @('-method', 'Caesarea.Deterministic.Tests.DebuggerLiveTests.VsCodeAttachesToThisProcessAndLetsItGo') }
    'VisualStudio' { @('-method', 'Caesarea.Deterministic.Tests.DebuggerLiveTests.VisualStudio2026AttachesToThisProcessAndLetsItGo') }
    default { @('-class', 'Caesarea.Deterministic.Tests.DebuggerLiveTests') }
}

try {
    & $TestExecutable @filter
    exit $LASTEXITCODE
}
finally {
    Remove-Item Env:CAESAREA_LIVE_DEBUGGER_TESTS -ErrorAction SilentlyContinue
}
