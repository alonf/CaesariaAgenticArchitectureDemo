<#
.SYNOPSIS
Creates a disabled one-shot Foundry investigation routine and inspects or operates it explicitly.
.DESCRIPTION
Uses the installed azure.ai.routines extension. The target must be the read-only hosted
composition with WORKIQ_TOOLBOX unset when no delegated user identity is available.
Dispatch acceptance is not completion; inspect Runs for the actual service result.
.EXAMPLE
./scripts/Manage-InvestigationRoutine.ps1 -Operation Create -ProjectEndpoint $endpoint -AgentName $agent -AtUtc ([DateTimeOffset]::UtcNow.AddMinutes(10)) -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Create', 'Show', 'Enable', 'Disable', 'Dispatch', 'Runs')]
    [string] $Operation,
    [Parameter(Mandatory)]
    [uri] $ProjectEndpoint,
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}$')]
    [string] $Name = 'caesarea-lighting-investigation',
    [string] $AgentName,
    [DateTimeOffset] $AtUtc
)

$ErrorActionPreference = 'Stop'
if ($ProjectEndpoint.Scheme -ne 'https' -or
    -not $ProjectEndpoint.Host.EndsWith('.services.ai.azure.com', [StringComparison]::OrdinalIgnoreCase) -or
    -not $ProjectEndpoint.IsDefaultPort -or $ProjectEndpoint.UserInfo -or
    $ProjectEndpoint.Query -or $ProjectEndpoint.Fragment -or
    $ProjectEndpoint.AbsolutePath -notmatch '^/api/projects/[^/]+/?$') {
    throw 'Use an HTTPS Foundry project endpoint ending in /api/projects/<project>.'
}
if ($Operation -eq 'Create' -and
    ([string]::IsNullOrWhiteSpace($AgentName) -or $AtUtc -le [DateTimeOffset]::UtcNow)) {
    throw 'Create requires AgentName and a future AtUtc. The routine is created disabled.'
}

$arguments = @('ai', 'routine')
$manifestPath = $null
try {
    switch ($Operation) {
        'Create' {
            if (-not $PSCmdlet.ShouldProcess("$Name at $ProjectEndpoint", 'Create disabled routine')) { return }
            $template = Join-Path $PSScriptRoot '../deployment/routines/lighting-investigation.json'
            $manifest = Get-Content -LiteralPath $template -Raw | ConvertFrom-Json
            $manifest.action.agent_name = $AgentName
            $manifest.triggers.default.at = $AtUtc.UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ssZ')
            $manifestPath = [IO.Path]::GetTempFileName()
            $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding utf8
            $arguments += @('create', $Name, '--file', $manifestPath, '--enabled=false')
        }
        'Runs' { $arguments += @('run', 'list', $Name) }
        'Show' { $arguments += @('show', $Name) }
        default {
            if (-not $PSCmdlet.ShouldProcess("$Name at $ProjectEndpoint", $Operation)) { return }
            $arguments += @($Operation.ToLowerInvariant(), $Name)
        }
    }
    $arguments += @('--project-endpoint', $ProjectEndpoint.AbsoluteUri.TrimEnd('/'), '--output', 'json', '--no-prompt')
    & azd @arguments
    if ($LASTEXITCODE -ne 0) { throw "azd routine $Operation failed (exit $LASTEXITCODE). Inspect Show before retrying a write." }
}
finally {
    if ($manifestPath -and (Test-Path -LiteralPath $manifestPath)) {
        Remove-Item -LiteralPath $manifestPath
    }
}
