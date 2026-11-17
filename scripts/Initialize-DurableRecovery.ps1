[CmdletBinding()]
param(
    [Parameter(Mandatory)][uri]$ProjectEndpoint,
    [uri]$EnergyHub = 'http://localhost:5016',
    [uri]$CommandCenter = 'http://localhost:5235',
    [string]$SupervisorName = 'local-presenter'
)

$ErrorActionPreference = 'Stop'
if ($ProjectEndpoint.Scheme -ne 'https' -or !$ProjectEndpoint.Host.EndsWith('.services.ai.azure.com')) {
    throw 'Supply an HTTPS Foundry project endpoint.'
}
foreach ($address in @($EnergyHub, $CommandCenter)) {
    if (!$address.IsLoopback -or $address.Scheme -notin @('http', 'https')) { throw 'City boundaries must be local for this rehearsal.' }
}
$path = Join-Path $PSScriptRoot '../Services/OperationsAgent.Functions/local.settings.json'
if (Test-Path -LiteralPath $path) { throw 'local.settings.json exists; preserve its keys to resume existing sessions and approvals.' }
$settings = @{
    IsEncrypted = $false
    Values = @{
        AzureWebJobsStorage = 'UseDevelopmentStorage=true'
        FUNCTIONS_WORKER_RUNTIME = 'dotnet-isolated'
        DURABLE_TASK_SCHEDULER_CONNECTION_STRING = 'Endpoint=http://localhost:18080;TaskHub=caesarea-recovery-v2;Authentication=None'
        CAESAREA_LOCAL_REHEARSAL = 'true'
        CAESAREA_OPERATOR_KEY = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        CAESAREA_SUPERVISOR_KEY = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        CAESAREA_SUPERVISOR_NAME = $SupervisorName
        ENERGYHUB_BASE_URI = $EnergyHub.AbsoluteUri
        COMMANDCENTER_BASE_URI = $CommandCenter.AbsoluteUri
        FOUNDRY_PROJECT_ENDPOINT = $ProjectEndpoint.AbsoluteUri
    }
}
$settings | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $path -Encoding utf8
Write-Host 'Created ignored local.settings.json with separate operator and supervisor keys. Keep this file private.'
