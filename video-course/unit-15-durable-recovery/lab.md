# Lab 15 — durable agent and pending repair

Outcome: recover the same conversation and original approval across local Functions worker loss, then verify a terminal repair at Energy Hub. Allow 30 minutes. Recorded code `4e5a6d3`. Actual model calls, local scheduler/storage emulators, simulated SmartPole. No Azure Function App deployment is claimed.

## Prerequisites

Complete [one-time Functions setup](../../docs/runbooks/durable-recovery.md), including Docker, Core Tools, the separately built Functions project, existing model sign-in and private local settings. Do not commit credentials. Start the city using the root README. VS Code can display these files without adding the optional Functions project to the main solution; use its project explicitly for build/debug setup.

```powershell
dotnet build Services/OperationsAgent.Functions/OperationsAgent.Functions.csproj
```

## Walkthrough

1. Director → **15. Durable recovery** → **Prepare foundation**. Check foundation ready. This applies **Lights On Reported by a Client** and enables Workflow if necessary. A later stage is retained.
2. Command Center → durable panel → **Walkthrough** → **Ask the durable agent to recover L-417**.
3. Wait for **AwaitingApproval**, not just an operation ID. Save the original review/revision/expiry. The lamp must still be On with manual override.
4. Director → **Crash worker and restart**. Only its owned Functions process tree is interrupted; scheduler, Docker and city stay running.
5. Command Center → **Continue the same conversation after restart**. Its displayed prompt contains no operation ID. Verify the same session and **Original review confirmed unchanged**.
6. **Approve repair** before expiry. Wait for the terminal business outcome, then inspect Hub evidence and GET the Hub independently.

```bash
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > hub-state.json
```

Expected: `reportedIsOn=false`, `manualOverride=false`, `isAnomalous=false`, a matching `lastCommand.correlationId` and `status="Succeeded"`. Recorded revision 3→4, operation `repair-d58d3e7834fdfe7ba0ff87fc-7ee7121128e8beb80502f689`, approval expiry unchanged at `2026-11-25T05:15:02.9342621Z`; worker stopped 05:10:51Z, restarted 05:11:05Z, conversation recovered 05:11:36Z, approved 05:11:55Z, terminal observed 05:11:57Z. Application timestamps display local time.

## Code and limits

- `Services/OperationsAgent.Functions/Program.cs:66`: durable agent entity and tool surface.
- `RecoveryOrchestration.cs:36`: original review, external event, durable timer; `:69` revalidation and bounded activity retries.
- `RecoveryActivities.cs:52`: matching pending/succeeded receipt reconciliation; uncertain outcome is nullable, never false certainty.
- `RecoveryTools.cs:70`: only explicit pre-dispatch retry, fresh approval and bounded attempt count.
- `RecoveryAuthorization.cs`: signed decision binds the exact review. The agent cannot approve.

The live recording tests worker restart while approval is pending. Additional transient-read and in-flight-command cases are covered separately in `RecoveryOrchestrationTests.cs` and the recovery activity tests; they are not simulated into this footage. Scheduler loss, general exactly-once execution and completed cloud integration are not demonstrated.

## Failure and reset

Repeat with **Decline** and verify no new command. If expired, wait for terminal timeout and prepare a fresh foundation. If the continue button is disabled, read its reason: an unfinished action, missing worker restart, unavailable review or already-resumed conversation require different responses. Reconnect/refresh status before assuming failure. Do not terminate the orchestration to demonstrate restart.

If a port is occupied, use current code: Command Center discovers Demo Control through Aspire instead of hardcoding port 5187. Never stop an unrelated application to free a guessed port. This recording exposed that routing defect; `4e5a6d3` fixes it and adds two port-routing tests.

Reset only after the outcome is known: Director → **Prepare foundation** for another durable run, or choose the next beat and its fixture. Preserve evidence before reset. If outcome is unknown, reconcile at the Hub first.

## Investigate

1. Which state belongs to the agent entity versus the repair orchestration?
2. Why must an orchestration use its own clock and activities for I/O?
3. Why can a matching pending receipt not become `CommandExecuted=false`?
4. What does a fresh attempt authorize, and why can an old decision not authorize it?
5. Compare current Functions and Foundry hosting documentation: which durable execution, identity and observability features would your architecture actually configure and verify?
