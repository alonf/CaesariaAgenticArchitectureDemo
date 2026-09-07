#Requires -Version 7.0

<#
.SYNOPSIS
    Stops the running demo: the Aspire AppHost and everything it started.

.DESCRIPTION
    Ctrl+C in the console that ran Start-CaesareaDemo.ps1 is the normal way down. This script is
    for every other case: that console is gone, the host was started from the IDE, a build fails
    because a running service still holds its DLL, or a previous stop left something behind.

    It asks the Aspire CLI first (`aspire stop`, scoped to this repository's AppHost), which brings
    the host down the way Ctrl+C does and cleans up the CLI's own bookkeeping. Then it looks for
    what survived - the AppHost, the dcp orchestrator processes under it, the `dotnet run` wrappers,
    the CLI that launched the host, and the service executables built from this repository - and
    kills those. Last, it deletes the CLI's backchannel socket file for every AppHost pid that is
    gone, because a stale one makes the next start fail with "Access is denied" before it builds.

    The CLI is not always on PATH: the IDE's Aspire extension runs it straight from the NuGet
    cache, so that is where this script looks when `aspire` does not resolve. With no CLI at all,
    the kill pass is the whole stop.

.PARAMETER Force
    Skip the graceful stop and kill straight away.

.EXAMPLE
    ./scripts/Stop-CaesareaDemo.ps1

.EXAMPLE
    ./scripts/Stop-CaesareaDemo.ps1 -WhatIf
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepositoryRoot = (Resolve-Path "$PSScriptRoot/..").Path
# With the separator, so a sibling folder that merely starts with this name is not matched.
$RepositoryPrefix = $RepositoryRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
# The same with the other separator, for a command line that named the project that way.
$RepositoryPrefixAlt = $RepositoryPrefix.Replace([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
$TestsPrefix = Join-Path $RepositoryRoot 'Tests' ''
$AppHostProject = Join-Path $RepositoryRoot 'Caesarea.AppHost' 'Caesarea.AppHost.csproj'
$SocketDirectory = Join-Path $HOME '.aspire' 'cli' 'bch'

# A dotnet process that is running something rather than building it: `dotnet run` (the AppHost
# from the start script, the per-service wrappers the AppHost starts), `dotnet watch`, or an
# assembly from this repository named directly. MSBuild worker nodes, the compiler server and the
# IDE's language services are dotnet processes too, and none of them is the demo.
$RunPattern = '^"?[^"]*?dotnet(.exe)?"?[ ]+(run|watch)[ ]'
$AssemblyPattern = '^"?[^"]*?dotnet(.exe)?"?[ ]+"?' + [regex]::Escape($RepositoryPrefix) + '[^"]*.dll'
# `dotnet run` on an AppHost hands over to the Aspire CLI through dnx, so the chain is
# dotnet run -> dotnet exec dnx aspire.cli run -> aspire run -> the AppHost.
$DnxPattern = '[ ]dnx[ ].*aspire.cli'
# dcp and the dashboard are not always children of the AppHost - their parent may already be
# gone - but each names the pid it watches, and exits when that pid does.
$MonitorPattern = '--monitor[ ]+([0-9]+)'

function Write-Step { param([string] $Message) Write-Host "`n=== $Message" -ForegroundColor Cyan }
function Write-Note { param([string] $Message) Write-Host "  $Message" -ForegroundColor DarkGray }
function Write-Done { param([string] $Label, [string] $Message) Write-Host "  [$Label] $Message" -ForegroundColor Green }
function Write-Warn { param([string] $Message) Write-Host "  $Message" -ForegroundColor Yellow }

# One snapshot of every process with what the matching needs: who started it, where its executable
# lives, and how it was invoked. A single CIM query on Windows, because reading CommandLine through
# Get-Process there costs one WMI round trip per process.
function Get-ProcessTable {
    if ($IsWindows) {
        foreach ($process in Get-CimInstance Win32_Process) {
            [pscustomobject]@{
                Id          = [int] $process.ProcessId
                ParentId    = [int] $process.ParentProcessId
                Name        = [IO.Path]::GetFileNameWithoutExtension([string] $process.Name)
                Path        = [string] $process.ExecutablePath
                CommandLine = [string] $process.CommandLine
            }
        }
    }
    else {
        foreach ($process in Get-Process) {
            [pscustomobject]@{
                Id          = $process.Id
                ParentId    = if ($process.Parent) { $process.Parent.Id } else { 0 }
                Name        = $process.ProcessName
                Path        = [string] $process.Path
                CommandLine = [string] $process.CommandLine
            }
        }
    }
}

# Built from this repository - the AppHost and the services. Not the test runner: it is built here
# too, hosts the services in-process, and is not the demo.
function Test-BuiltHere {
    param($Process)
    $Process.Path.StartsWith($RepositoryPrefix, [StringComparison]::OrdinalIgnoreCase) -and
        -not $Process.Path.StartsWith($TestsPrefix, [StringComparison]::OrdinalIgnoreCase)
}

function Test-DotnetRunning {
    param($Process)
    $Process.Name -eq 'dotnet' -and (
        $Process.CommandLine -match $RunPattern -or
        $Process.CommandLine -match $AssemblyPattern -or
        $Process.CommandLine -match $DnxPattern)
}

# A dotnet process running something of this repository. Named by path in the command line, with
# either separator, so a `dotnet run` of some other project on the same machine is left alone; the
# AppHost started with a relative --project from the repository root is still found, through its
# child.
function Test-RunsHere {
    param($Process)
    (Test-DotnetRunning $Process) -and (
        $Process.CommandLine.Contains($RepositoryPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        $Process.CommandLine.Contains($RepositoryPrefixAlt, [StringComparison]::OrdinalIgnoreCase))
}

# The pids a dcp or dashboard process watches, from its --monitor arguments.
function Get-MonitoredIds {
    param($Process)
    foreach ($match in [regex]::Matches($Process.CommandLine, $MonitorPattern)) { [int] $match.Groups[1].Value }
}

# The kinds of process the demo is made of, by name: the Aspire CLI and its managed host, the dcp
# orchestrator, and dotnet when it is running rather than building. Walking the process tree is
# restricted to these so that something the demo merely launched - VS Code, when the switchboard
# attaches a debugger and no window was open yet - is never treated as part of it.
function Test-DemoFamily {
    param($Process)
    $Process.Name -in @('aspire', 'aspire-managed', 'dcp') -or (Test-DotnetRunning $Process)
}

# Everything that belongs to the demo, from one snapshot: what was built here, what runs it, then
# above each AppHost the CLI chain that launched it, then whatever hangs off any of those - by
# parent, or by the pid it monitors (dcp, the service wrappers, the dashboard).
function Find-DemoProcesses {
    param([object[]] $Table)

    $byId = @{}
    foreach ($process in $Table) { $byId[$process.Id] = $process }

    $found = @{}
    foreach ($process in $Table) {
        if ((Test-BuiltHere $process) -or (Test-RunsHere $process)) { $found[$process.Id] = $process }
    }

    foreach ($appHost in @($found.Values | Where-Object Name -eq 'Caesarea.AppHost')) {
        $parent = $byId[$appHost.ParentId]
        while ($parent -and -not $found.ContainsKey($parent.Id) -and (Test-DemoFamily $parent)) {
            $found[$parent.Id] = $parent
            $parent = $byId[$parent.ParentId]
        }
    }

    do {
        $added = $false
        foreach ($process in $Table) {
            if ($found.ContainsKey($process.Id) -or -not (Test-DemoFamily $process)) { continue }

            $attached = $found.ContainsKey($process.ParentId)
            if (-not $attached) {
                foreach ($id in Get-MonitoredIds $process) { if ($found.ContainsKey($id)) { $attached = $true } }
            }

            if ($attached) {
                $found[$process.Id] = $process
                $added = $true
            }
        }
    } while ($added)

    return @($found.Values | Sort-Object Id)
}

function Find-AspireCli {
    $command = Get-Command aspire -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget' 'packages' }
    $executable = if ($IsWindows) { 'aspire.exe' } else { 'aspire' }
    $candidates = @(Get-ChildItem -Path (Join-Path $packages 'aspire.cli.*' '*' 'tools' '*' '*' $executable) -File -ErrorAction SilentlyContinue)

    return ($candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1)?.FullName
}

function Wait-ProcessExit {
    param([int[]] $Ids, [int] $Seconds)

    if ($Ids.Count -eq 0) { return @() }
    $deadline = (Get-Date).AddSeconds($Seconds)

    do {
        $alive = @(Get-Process -Id $Ids -ErrorAction SilentlyContinue)
        if ($alive.Count -eq 0) { return @() }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    return $alive
}

function Format-Process { param($Process) "$($Process.Name) (pid $($Process.Id))" }

# ---------------------------------------------------------------------------------------------
# 1. The graceful way: the CLI tells the AppHost to stop, and the AppHost takes its services down.
# ---------------------------------------------------------------------------------------------

Write-Step 'Looking for the demo'
Write-Note "Repository: $RepositoryRoot"

$running = @(Find-DemoProcesses (Get-ProcessTable))
$stopped = @{}

if ($running.Count -eq 0) {
    Write-Note 'Nothing of the demo is running.'
}
else {
    foreach ($process in $running) { Write-Note (Format-Process $process) }

    if (-not $Force) {
        Write-Step 'Asking the Aspire CLI to stop the AppHost'
        $cli = Find-AspireCli

        if (-not $cli) {
            Write-Warn 'No Aspire CLI found, on PATH or in the NuGet cache; going straight to the kill pass.'
        }
        elseif ($PSCmdlet.ShouldProcess($AppHostProject, 'aspire stop')) {
            Write-Note "$cli stop --apphost $AppHostProject"
            & $cli stop --apphost $AppHostProject --non-interactive --nologo 2>&1 | ForEach-Object { Write-Note "$_" }

            if ($LASTEXITCODE -ne 0) {
                Write-Warn "aspire stop exited with $LASTEXITCODE; whatever it left is killed next."
            }
            $global:LASTEXITCODE = 0

            $left = @(Wait-ProcessExit -Ids @($running | ForEach-Object { $_.Id }) -Seconds 15)
            $leftIds = @($left | ForEach-Object { $_.Id })
            foreach ($process in $running) {
                if ($process.Id -notin $leftIds) {
                    $stopped[$process.Id] = $process
                    Write-Done 'stopped' (Format-Process $process)
                }
            }
        }
    }
}

# ---------------------------------------------------------------------------------------------
# 2. The rest: whatever the graceful stop did not reach, or everything when -Force said so.
# ---------------------------------------------------------------------------------------------

$survivors = @()
if ($running.Count -gt 0) {
    $survivors = @(Find-DemoProcesses (Get-ProcessTable))
}

if ($survivors.Count -gt 0) {
    Write-Step $(if ($Force) { 'Killing the demo' } else { 'Killing what survived' })

    foreach ($process in $survivors) {
        if ($PSCmdlet.ShouldProcess((Format-Process $process), 'Kill')) {
            try {
                Stop-Process -Id $process.Id -Force -ErrorAction Stop
            }
            catch [Microsoft.PowerShell.Commands.ProcessCommandException] {
                # Already gone: it went down with a parent killed a moment earlier.
            }
            $stopped[$process.Id] = $process
            Write-Done 'killed' (Format-Process $process)
        }
    }

    $survivors = @(Wait-ProcessExit -Ids @($survivors | ForEach-Object { $_.Id }) -Seconds 10)
}

# ---------------------------------------------------------------------------------------------
# 3. The CLI's bookkeeping. It keeps one socket file per AppHost, named <hash>.<pid>, and probes
#    that pid on the next start; a file whose pid is gone - or, worse, reused by a process the
#    probe cannot open - fails that start before anything builds.
# ---------------------------------------------------------------------------------------------

Write-Step 'Backchannel socket files'

if (-not (Test-Path $SocketDirectory)) {
    Write-Note "None: $SocketDirectory does not exist."
}
else {
    $live = @{}
    foreach ($process in Get-Process) { $live[$process.Id] = $process.ProcessName }
    $removed = 0

    foreach ($file in Get-ChildItem -Path $SocketDirectory -File) {
        $suffix = $file.Name.Split('.')[-1]
        $socketPid = 0
        if ($file.Name -notlike '*.*' -or -not [int]::TryParse($suffix, [ref] $socketPid)) { continue }

        # Live and still an AppHost (its own executable, or dotnet hosting the assembly): in use.
        $owner = $live[$socketPid]
        $inUse = $owner -and -not $stopped.ContainsKey($socketPid) -and ($owner -like '*AppHost*' -or $owner -eq 'dotnet')
        if ($inUse) { continue }

        if ($PSCmdlet.ShouldProcess($file.FullName, 'Delete stale socket file')) {
            Remove-Item -LiteralPath $file.FullName -Force
            $removed++
            Write-Done 'removed' "$($file.Name) (pid $socketPid is $(if ($owner) { "now $owner" } else { 'gone' }))"
        }
    }

    if ($removed -eq 0) { Write-Note 'No stale socket files.' }
}

# ---------------------------------------------------------------------------------------------
# 4. The verdict.
# ---------------------------------------------------------------------------------------------

if ($survivors.Count -gt 0) {
    Write-Step 'Still running'
    foreach ($process in $survivors) { Write-Warn "$($process.ProcessName) (pid $($process.Id))" }
    Write-Warn 'These did not exit; a process started elevated needs an elevated console to stop.'
    exit 1
}

Write-Step $(if ($running.Count -eq 0) { 'Done (nothing was running)' } else { 'Done' })
