#Requires -Version 7.0

<#
.SYNOPSIS
    Builds the demo and runs the deterministic suite inside the official .NET 10 SDK Linux
    container, from this checkout, without touching the local build output.

.DESCRIPTION
    The demo is cross-platform and the debugger integration must stay that way: the portable build
    may never grow a Windows dependency, and the switchboard must answer "Visual Studio is
    unavailable here" on Linux instead of failing. CI proves that on a Linux runner; this script
    proves it on the presenter's machine before pushing, using Docker Desktop.

    The tracked and untracked-but-not-ignored files are archived with git and tar and unpacked
    inside the container, so no Windows bin/obj folder or project.assets.json ever enters it and
    the container restores and builds from clean. Nothing is written back to the checkout.

    Inside the container: dotnet restore, dotnet build, dotnet test on the deterministic suite -
    the same steps as .github/workflows/ci.yml.

.PARAMETER Image
    The SDK image to use. Defaults to mcr.microsoft.com/dotnet/sdk:10.0.

.PARAMETER Filter
    An optional xunit class or method filter, passed to the suite as --filter-query
    (for example "/*/*/DebuggerBoundaryTests/*"). Default: the whole suite.

.EXAMPLE
    ./scripts/Test-LinuxContainer.ps1

.EXAMPLE
    ./scripts/Test-LinuxContainer.ps1 -Filter "/*/*/VisualStudioAttachServiceTests/*"
#>

[CmdletBinding()]
param(
    [string] $Image = 'mcr.microsoft.com/dotnet/sdk:10.0',
    [string] $Filter
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepositoryRoot = Resolve-Path "$PSScriptRoot/.."

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }

Write-Step 'Docker'
$engine = & docker version --format '{{.Server.Os}}' 2>$null
if ($LASTEXITCODE -ne 0 -or $engine -ne 'linux') {
    $global:LASTEXITCODE = 0
    throw 'Docker Desktop is not running with a Linux engine. Start it (Linux containers) and try again.'
}
Write-Note "Linux engine, image $Image"

Write-Step 'Archiving the checkout'
$stamp = [guid]::NewGuid().ToString('n')
$archive = Join-Path ([IO.Path]::GetTempPath()) "caesarea-linux-check-$stamp.tar"
$fileList = "$archive.list"

try {
    # git decides what belongs to the source tree; tar packs exactly that list.
    & git -C $RepositoryRoot ls-files --cached --others --exclude-standard | Set-Content -Path $fileList -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed.' }
    & tar -cf $archive -C $RepositoryRoot -T $fileList
    if ($LASTEXITCODE -ne 0) { throw 'tar failed.' }
    Write-Note "$((Get-Content $fileList).Count) files, $([math]::Round((Get-Item $archive).Length / 1MB, 1)) MB"

    Write-Step 'Restore, build and test on Linux'
    $testArguments = if ($Filter) { "--no-build -- --filter-query '$Filter'" } else { '--no-build' }
    $script = @(
        'set -euo pipefail',
        'mkdir -p /src && tar -xf /src.tar -C /src && cd /src',
        'echo "== $(uname -srm), $(dotnet --version)"',
        'dotnet restore',
        'dotnet build --no-restore',
        "dotnet test Tests/Caesarea.Deterministic.Tests $testArguments"
    ) -join ' && '

    & docker run --rm -v "${archive}:/src.tar:ro" $Image bash -c $script
    exit $LASTEXITCODE
}
finally {
    Remove-Item $archive, $fileList -ErrorAction SilentlyContinue
}
