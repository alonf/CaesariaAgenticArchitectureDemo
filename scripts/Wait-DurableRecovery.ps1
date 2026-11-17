# Poll the local status boundary; an accepted schedule/decision is not business completion.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$OperationId,
    [Parameter(Mandatory)][ValidateSet('AwaitingApproval', 'Terminal')][string]$State,
    [Parameter(Mandatory)][hashtable]$Headers,
    [uri]$BaseUri = 'http://localhost:17071/api',
    [ValidateRange(1, 900)][int]$TimeoutSeconds = 120,
    [string]$EvidencePath
)

$ErrorActionPreference = 'Stop'
if (!$BaseUri.IsLoopback -or $BaseUri.Scheme -notin @('http', 'https')) {
    throw 'This rehearsal helper sends credentials only to a loopback HTTP(S) endpoint.'
}
$deadline = [Diagnostics.Stopwatch]::StartNew()
$lastObservation = 'No status received.'
while ($deadline.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
    $status = $null
    try {
        $status = Invoke-RestMethod "$($BaseUri.AbsoluteUri.TrimEnd('/'))/durable-operations/$OperationId" `
            -Headers $Headers -TimeoutSec 5 -MaximumRedirection 0
    }
    catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        if ($code -ne 0 -and $code -notin @(404, 408, 429) -and $code -lt 500) { throw }
        $lastObservation = "Transient status read failure (HTTP $code); no outcome inferred."
    }
    if ($null -ne $status) {
        $terminal = $status.runtimeStatus -in @('Completed', 'Failed', 'Terminated', 'Canceled')
        $lastObservation = "Runtime=$($status.runtimeStatus); phase=$($status.progress.State)."
        if ($State -eq 'AwaitingApproval' -and $terminal) {
            throw "Operation reached a terminal state before the requested approval wait. $lastObservation"
        }
        $ready = if ($State -eq 'Terminal') { $terminal } else {
            $status.runtimeStatus -eq 'Running' -and $status.progress.State -eq 'AwaitingApproval' -and
                $null -ne $status.progress.Review -and
                [DateTimeOffset]$status.progress.Review.ExpiresAt -gt [DateTimeOffset]::UtcNow
        }
        if ($ready) {
            # The endpoint exposes Durable Task's serialized output as a JSON string.
            if ($status.outcome -is [string] -and ![string]::IsNullOrWhiteSpace($status.outcome)) {
                $status.outcome = $status.outcome | ConvertFrom-Json
            }
            if ($EvidencePath) {
                # Status only: never serialize request headers or local settings.
                $status | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
            }
            return $status
        }
    }
    Start-Sleep -Milliseconds 500
}
throw "Timed out waiting for $State on $OperationId. $lastObservation"
