<#
.SYNOPSIS
Selects the installed SDK required by global.json for the current PowerShell process.
.DESCRIPTION
Dot-source before CLI work when the SDK was installed per user. No installation or persistent
PATH changes occur. The normal system installation is preferred when it satisfies the repo pin.
.EXAMPLE
. ./scripts/Use-CaesareaSdk.ps1
dotnet --version
#>
$sdkRepositoryRoot = Split-Path $PSScriptRoot -Parent
$sdkPin = (Get-Content (Join-Path $sdkRepositoryRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
$sdkCandidates = @(Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
if ($IsWindows) {
    $sdkCandidates += @(
        (Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet11/dotnet.exe'),
        (Join-Path $env:USERPROFILE '.dotnet/dotnet.exe')
    )
}
else {
    $sdkCandidates += (Join-Path $env:HOME '.dotnet/dotnet')
}
$sdkSelected = $false
Push-Location $sdkRepositoryRoot
try {
    foreach ($sdkCandidate in ($sdkCandidates | Where-Object { $_ } | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $sdkCandidate)) { continue }
        $sdkResolvedVersion = & $sdkCandidate --version 2>$null
        if ($LASTEXITCODE -ne 0 -or $sdkResolvedVersion -notmatch '^11\.0\.\d+$') { continue }
        $sdkLinkTarget = [IO.FileInfo]::new($sdkCandidate).ResolveLinkTarget($true)
        $sdkDirectory = Split-Path $(if ($sdkLinkTarget) { $sdkLinkTarget.FullName } else { $sdkCandidate }) -Parent
        $env:PATH = "$sdkDirectory$([IO.Path]::PathSeparator)$env:PATH"
        $env:DOTNET_ROOT = $sdkDirectory
        $sdkSelected = $true
        break
    }
}
finally { Pop-Location }
if (-not $sdkSelected) { throw "Install .NET SDK $sdkPin (or a compatible feature band) before running this checkout." }
