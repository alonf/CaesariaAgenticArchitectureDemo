[CmdletBinding()]
param([ValidateRange(1024, 65535)][int]$Port = 17071, [switch]$NoBuild)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-CaesareaSdk.ps1')
$project = Join-Path $PSScriptRoot '../Services/OperationsAgent.Functions'
if (!(Test-Path -LiteralPath (Join-Path $project 'local.settings.json'))) {
    throw 'Run Initialize-DurableRecovery.ps1 first.'
}
$settings = Get-Content -LiteralPath (Join-Path $project 'local.settings.json') -Raw | ConvertFrom-Json
if ($settings.Values.DURABLE_TASK_SCHEDULER_CONNECTION_STRING -notmatch '(?i)(?:^|;)\s*TaskHub=caesarea-recovery-v2(?:;|$)') {
    throw 'This orchestration requires task hub caesarea-recovery-v2. Follow the runbook migration note; do not replay v1 histories with this build.'
}
if (!$NoBuild) {
    dotnet build (Join-Path $project 'OperationsAgent.Functions.csproj') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Functions build failed.' }
}
$output = Join-Path $project 'bin/Debug/net11.0'
$workerPath = Join-Path $output 'worker.config.json'
if (!(Test-Path -LiteralPath $workerPath)) { throw 'Build the Functions project before using -NoBuild.' }
# On Windows Core Tools resolves the bare "dotnet" worker name to Program Files,
# ignoring a per-user SDK on PATH. Patch only generated local output, never a package
# or committed runtime file. See Azure/azure-functions-core-tools issues 4725 and 5138.
$worker = Get-Content -LiteralPath $workerPath -Raw | ConvertFrom-Json
$worker.description.defaultExecutablePath = (Get-Command dotnet).Source
$worker | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $workerPath -Encoding utf8
Copy-Item -LiteralPath (Join-Path $project 'local.settings.json') -Destination (Join-Path $output 'local.settings.json') -Force
Push-Location $output
try {
    func start --port $Port --no-build
    if ($LASTEXITCODE -ne 0) { throw "Functions host exited with code $LASTEXITCODE." }
}
finally { Pop-Location }
