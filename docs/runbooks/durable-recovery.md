# Durable Functions agent recovery rehearsal

This is the local L-417 recovery beat, implemented November 17, 2026. It adds an actual MAF agent
hosted by Azure Functions with Durable Entities for conversation state, plus a separate Durable
Task repair orchestration. It preserves the original Stage 8 in-process MAF graph.

## What runs where

- `OperationsAgent.Functions`: MAF 1.28 agent, entity-owned chat history, read/start/status tools,
  deterministic orchestration, authenticated review endpoint and deterministic execution activities.
- Durable Task Scheduler emulator: conversation state, process history, events and durable timers.
- Existing Command Center/Energy Hub/SmartPole: authoritative stage, revision, policy and physical
  effects. Restart **only the Functions worker** for this demo.
- Foundry: the real model via Chat Completions. This composition does not use Foundry hosted-agent
  infrastructure or inherit Work IQ delegation, Teams integration or Routines activation.

The selected packages also expose `ConfigureDurableWorkflows` and `RequestPort`. They do not turn
the current `PendingApprovalStore`/`TaskCompletionSource` executor into a persisted approval wait.
This first implementation uses an explicit Durable Task orchestrator, sharing `RemediationPolicy`
and the narrow Hub gateways. Do not describe it as the unchanged Stage 8 graph running durably.

## Prerequisites and start

Use PowerShell 7, SDK **11.0.100**, Functions Core Tools **4.16.1**, Docker Desktop and a valid
`az login` for the model. No Azure resources are provisioned by this runbook.

1. Start the normal city demo, select Workflow or later, and apply Forgotten Override. Check the
   actual Energy Hub and Command Center URLs in Aspire. Their standard HTTP profiles use 5016
   and 5235; pass explicit URLs if your composition differs.
2. Start these dedicated local emulators. If Azurite already owns ports 10000-10002, reuse it.

   ```powershell
   docker run -d --name caesarea-recovery-dts-v2 -p 127.0.0.1:18080:8080 -p 127.0.0.1:18082:8082 -e DTS_TASK_HUB_NAMES=caesarea-recovery-v2 mcr.microsoft.com/dts/dts-emulator@sha256:82ceb7df2fdfc2e22b0013c7b87b7a8e443bcd220d4fb37c32ced8ee23438719
   docker run -d --name caesarea-recovery-azurite -p 127.0.0.1:10000:10000 -p 127.0.0.1:10001:10001 -p 127.0.0.1:10002:10002 mcr.microsoft.com/azure-storage/azurite@sha256:830430c1da1a2d537e08f3e6764dd1f5ae00cf0346bcaf625b968ec3f0971fd5
   ```

   If these v2 containers already exist, use `docker start caesarea-recovery-dts-v2 caesarea-recovery-azurite`.
   The earlier `caesarea-recovery-dts` container belongs to v1; keep it stopped and preserve it.
   Do not replay v1 histories with this build: the retry/revalidation/reconciliation activity
   schedule changed. For an existing private settings file, change only `TaskHub=caesarea-recovery-v1`
   to `TaskHub=caesarea-recovery-v2` in `DURABLE_TASK_SCHEDULER_CONNECTION_STRING`, preserving keys.
   Start a fresh session and operation on v2; this is isolation, not migration of pending v1 work.
   Restarting the emulator is outside the worker-only recovery claim.

3. Initialize once. Use your existing Foundry project endpoint; the initializer creates separate
   random operator and supervisor keys in an ignored `local.settings.json`. It refuses to overwrite
   an existing file, because rotating its signing key invalidates pending reviews.

   ```powershell
   ./scripts/Initialize-DurableRecovery.ps1 -ProjectEndpoint 'https://<account>.services.ai.azure.com/api/projects/<project>' -SupervisorName 'presenter'
   ./scripts/Start-DurableRecovery.ps1
   ```

   Keep the host terminal open. The launcher builds, selects this machine's SDK, and starts Core
   Tools from the compiled output. Its local `worker.config.json` patch is necessary on Windows:
   Core Tools otherwise resolves bare `dotnet` to Program Files despite a per-user SDK on PATH.
   It changes generated local output only. No credentials enter source control or Docker context.

## Demonstrate the conversation and approval

In a second terminal at the repository root:

```powershell
$local = Get-Content Services/OperationsAgent.Functions/local.settings.json -Raw | ConvertFrom-Json
$operatorHeaders = @{ 'X-Caesarea-Key' = $local.Values.CAESAREA_OPERATOR_KEY }
$supervisorHeaders = @{ 'X-Caesarea-Key' = $local.Values.CAESAREA_SUPERVISOR_KEY }
$base = 'http://localhost:17071/api'
$turn = Invoke-RestMethod "$base/durable-agent/run" -Method Post -Headers $operatorHeaders -ContentType 'application/json' -Body '{"message":"Investigate L-417 and start supervised recovery. Do not approve it."}' -TimeoutSec 120
$turn.Message
$session = $turn.SessionId
```

Copy the returned operation ID into `$operation`. **Wait for an actual, unexpired approval review
before stopping the worker.** An ID proves only that scheduling was accepted:

```powershell
$evidence = 'docs/runbooks/artifacts/durable-recovery-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $evidence | Out-Null
$pending = ./scripts/Wait-DurableRecovery.ps1 -OperationId $operation -State AwaitingApproval -Headers $operatorHeaders -EvidencePath "$evidence/before-restart.json"
$reviewBeforeRestart = $pending.progress.Review | ConvertTo-Json -Compress
$pending.progress
```

Open <http://localhost:18082> and select `caesarea-recovery-v2`.
Only now stop/restart the Functions host with Ctrl+C and `Start-DurableRecovery.ps1
-NoBuild`, or terminate only its `OperationsAgent.Functions.dll` worker to demonstrate an unexpected
failure and Core Tools recovery. Do not stop Docker, the scheduler, or the city services.

```powershell
$followUp = @{ message = 'What happened to the repair you started? Check its status.'; sessionId = $session } | ConvertTo-Json
(Invoke-RestMethod "$base/durable-agent/run" -Method Post -Headers $operatorHeaders -ContentType 'application/json' -Body $followUp -TimeoutSec 120).Message
$pending = ./scripts/Wait-DurableRecovery.ps1 -OperationId $operation -State AwaitingApproval -Headers $operatorHeaders -EvidencePath "$evidence/after-restart.json"
if (($pending.progress.Review | ConvertTo-Json -Compress) -ne $reviewBeforeRestart) { throw 'The original approval review did not survive unchanged.' }
$pending.progress
```

The follow-up prompt contains no operation ID. Review the asset, revision and expiry displayed in
`$pending.progress.Review`, then submit the exact review through the supervisor boundary:

```powershell
$decision = @{ review = $pending.progress.Review; approved = $true } | ConvertTo-Json -Depth 8
Invoke-RestMethod "$base/durable-operations/$operation/decision" -Method Post -Headers $supervisorHeaders -ContentType 'application/json' -Body $decision
$finished = ./scripts/Wait-DurableRecovery.ps1 -OperationId $operation -State Terminal -Headers $operatorHeaders -EvidencePath "$evidence/terminal.json"
$finished
if ($finished.runtimeStatus -ne 'Completed') { throw 'The orchestration did not complete successfully; inspect its failure.' }
if ($finished.outcome.CommandExecuted -ne $true -or $finished.outcome.Resolved -ne $true) {
    throw "Repair not confirmed: $($finished.outcome.State). Inspect the Hub; do not infer that no command ran."
}
```

Use `approved = $false` for denial. An operator key cannot approve (401). Expired, changed or
completed reviews return 409. A 202 only acknowledges a queued decision. Two racing submissions
may both get 202; the single durable wait consumes one, so no second execution is scheduled.
Ask the same agent session for the final result, then verify the Energy Hub's state and receipt.
The helper tolerates transient status-read failures but stops on authorization errors, an early
terminal result, or its timeout. `Completed` is an orchestration state, not proof of a repaired
lamp: inspect the business outcome. For denial/timeout demonstrations, poll `Terminal` and assert
the intended outcome instead of using the successful-repair assertion above.

Alternate runs:

- Apply Security Operation while a review is pending. Approval of the old revision must finish
  `StateChanged` without switching off required lighting.
- Downgrade the stage while waiting. The next durable poll (15 seconds) finishes `StageWithdrawn`.
  Re-entering a capable stage does not revive the old review: the stage epoch is also bound.
- Leave a review unanswered. The durable timer ends it as `TimedOut` after five minutes.
- Repeat the start tool within the same session/revision/stage epoch. It returns the existing
  operation. A new session is a separate request, but the fixed Hub revision still prevents two
  accepted commands against the same validated state.
- A transient Hub read or approval-stage poll gets three activity attempts with a two-second
  retry interval. If all attempts fail **before execution is scheduled**, the operation remains
  `Failed`. Explicitly ask the agent to retry the failed operation; its start tool must pass
  `retryFailedOperation=true`. It preserves the failed history, allocates `-attempt-2` (then 3),
  and requires a fresh review. The three-attempt cap applies to the same session/revision/stage.
  Normal starts do not silently retry a failure. Failed execution/unknown-phase histories cannot
  use this retry path; inspect the Hub first. Existing running or completed operations are reused.

## Boundaries and recovery guarantees

The agent exposes no approval or command tool. Reviews bind instance, asset, revision, challenge,
reviewer and expiry with an HMAC signature created by deterministic code. The execution activity
verifies the signature and current stage/Hub revision before sending a command. This beat requires
supervision for **every** write, including anomalies the earlier Stage 8 policy would fix automatically.

The existing Hub atomically claims a fresh revision before a device command. Activity retries keep
the original revision. If the Hub accepted a command but the activity lost the response, the retry
recovers the matching successful Hub receipt. A matching pending receipt, or an advanced revision
without that receipt, enters `Reconciling`: read the receipt without resending. Reconciliation has
a 30-second scheduling budget with two-second durable polls; an in-flight read's bounded retries
can extend wall time. Hub/stage HTTP reads time out after five seconds; command calls after 30.
If success cannot be confirmed, report `OutcomeUnknown` with `CommandExecuted=null`; unresolved
confirmation also leaves `Resolved=null`. A failure receipt may supply a current resolution
observation, but never proves the physical command did not execute. The original command may
still complete after reconciliation ends. Do not treat null as false or automatically start
another repair. Before any execution is scheduled, a stale revision still yields definite
`StateChanged` / `CommandExecuted=false`. This is a bounded recovery demonstration,
**not** general exactly-once delivery or proof of recovery after a Hub/storage restart.

The Functions process contains both the read-only agent composition and deterministic executor.
It does not yet prove separate managed identities. The local key identifies one configured
presenter/supervisor; it is not multi-user Entra authorization. Session IDs are identifiers, never
credentials. Generated agent HTTP/MCP triggers are disabled; the custom endpoints enforce keys.
The host refuses App Service deployment and non-loopback city URLs for this rehearsal.

Command Center polls remain durable activities. Timer cancellation is synchronous; awaiting
`CancellationTokenSource.CancelAsync()` inside the orchestrator introduces a non-durable task and
can strand replay. Never use arbitrary `Task.Delay`, external HTTP calls or wall-clock reads in
the orchestrator. This is a completed-turn JSON demo, not reliable streaming.

Conversation history stays in the entity via Chat Completions. The Responses adapter uses remote
history and needs an explicit stable durable history `ProviderKey`; changing providers for an
existing session is not a transparent migration. MAF content logs are filtered to Warning;
operation logs keep correlation IDs. The process ID is persisted as its command correlation ID.

The configuration requests a 24-hour agent TTL and one-hour request-result retention. These are
different from process history retention; schema-2 completion receipts also have their own lifetime
rules. Do not promise automatic full deletion. Keep this task hub isolated and retire its local
data after rehearsal. Persisted history contains prompts/tool output; no supervisor key is stored
in a prompt, tool schema or durable event. Do not enable sensitive-data telemetry for a slide capture.

## Package and tooling findings

| Component | Tested version |
| --- | --- |
| Azure.Functions.Sdk | 1.1.0, pinned in global.json |
| Functions Worker / HTTP ASP.NET | 2.52.0 / 2.1.1 |
| MAF Functions hosting / DurableTask | 1.16.0 |
| Worker DurableTask / AzureManaged | 1.22.0 / 1.12.0 |
| Worker MCP host dependency | 1.6.0 |
| Core Tools | 4.16.1 |

The new Functions SDK builds a **second dependency graph** for host extensions. Hosting 1.16 embeds
MCP 1.0 metadata; the project normalizes that stale declaration to the selected worker's MCP 1.6
declaration. The generated host graph also needs WebUtilities **2.3.14**, which fixes the advisory
reported for 2.3.0. Both graphs are audited; no warning or vulnerability is suppressed. Remove these
targeted workarounds only when a newer hosting/Functions SDK combination proves they are unnecessary.

References: [Durable extension](https://learn.microsoft.com/en-us/agent-framework/hosting/durable-extension),
[upstream samples](https://github.com/microsoft/agent-framework-durable-extension),
[.NET 11 Functions](https://techcommunity.microsoft.com/blog/AppsonAzureBlog/-net-11-is-now-generally-available-in-azure-functions/4571176),
[Core Tools SDK-path issue](https://github.com/Azure/azure-functions-core-tools/issues/4725).

## Verification recorded November 17 (initial v1 implementation)

- Real model-backed conversation resumed after host restart and forced worker termination,
  remembered the operation ID without it in the follow-up prompt, and queried actual status.
- Supervisor approval completed the operation through the Hub; the operator credential returned
  401 and a duplicate completed approval returned 409. A clean run changed the Hub revision once.
- Security Operation during the wait ended as `StateChanged`, without a command; lighting stayed on.
- An unanswered review expired after worker downtime as `TimedOut`, without a command or revision change.
- Windows and Linux each passed **428 deterministic tests**, with four existing live-IDE skips.
  The real-Hub integration test deliberately loses a successful command response and proves the
  activity recovers the receipt without another HTTP command. Stage withdrawal, signed-review
  tampering, expiry and denial are covered by deterministic tests.
- Restore, format and build passed with zero build warnings/errors. Both Functions dependency
  graphs were audited with no known vulnerable packages; direct Functions packages had no stable
  updates in the configured feeds. This is not a cloud deployment or remote CI result.

These are implementer observations, not an independent rerun. Selected original status and
conversation outputs are saved in [the sanitized evidence bundle](../evidence/durable-recovery-2026-11-17/README.md).
They are not a video or complete process-termination transcript. They predate the review fixes
and do not establish a live v2 restart. The polling commands above save fresh checkpoints for
the next rehearsal; capture the worker stop/restart alongside them before presenting that run.

## Review fixes and regression evidence

The review added bounded read retries, explicit admission for failed pre-dispatch attempts,
and receipt-only reconciliation. The orchestration unit tests use a deterministic scheduler
double to check supplied retry policies and branches; they are not a DTS emulator integration.
The real Energy Hub HTTP test holds a device command pending, interrupts the activity while the
original request is still in flight, redelivers the activity, then completes the original command.
It verifies pending/unknown fields, confirmed recovery, one device command and no resend.
The earlier test for a lost response after success remains. See the current verification record
in [the upgrade plan](../../upgrade%20plan.md#12-recovery-review-fixes--november-17).

## Before cloud publication

Integrate the Command Center supervisor UI and authenticated Entra boundary; isolate executor
identity from the read-only agent; use a supported Functions plan and protected DTS endpoint;
define enforced history/receipt retention; persist idempotency receipts at the Hub for a wider
restart/failure scope; and rehearse production cancellation, ownership, transport loss and recovery.
No Functions resources or deployment pipeline are created by this local slice.
