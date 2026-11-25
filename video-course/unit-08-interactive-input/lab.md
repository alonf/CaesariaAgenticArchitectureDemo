# Lab 08 — the remote tool requests operator input

Outcome: denial issues no restore; explicit approval completes a new request and produces a Hub receipt. Allow 20 minutes. Baseline `843e0d0`; book chapter 9; same setup as Lab 07. Local execution with a simulated SmartPole.

## Prepare and run

1. Director → **Interactive Input** → confirm. Ensure **MCP remote**, **LOCAL (Aspire)**, **Lights On Reported by a Client**. If L-417 is already Off, reapply that scenario.
2. Save a Hub snapshot. Main app → **Walkthrough** → **Restore L-417 to scheduled mode (agent)**.
3. Wait for **Operator input required**, then **Deny**. Wait for the answer. Save another snapshot: `lastCommand.correlationId`, `stateRevision`, On and manual override must be unchanged.
4. Request again, wait for the new input panel, inspect the asset/effect, then **Approve**. Wait for the terminal answer and independently GET the Hub.

```bash
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > hub-state.json
```

Expected after approval: `reportedIsOn=false`, `manualOverride=false`, `lastCommand.operation="Restore scheduled mode"`, `status="Succeeded"`; correlate the receipt with the application trace. Recorded denial `4e1679ca8531`, approval `54bd7f23c3614adcbea8cffe9a257317`, revision 6→7 on approval only.

## Exact code

- `Services/EnergyHub.Api/Services/EnergyMcpTools.cs`, `MULTI_ROUND_TRIP_REQUEST`: `IsMrtrSupported`, `TryConsume`, `IsApproved`, deterministic restore, then `InputRequiredException`/schema on the initial call.
- `Services/EnergyHub.Api/Services/MrtrRequestStateStore.cs`: five-minute lifetime, hard cap, removal before asset/expiry validation. State is in memory.
- `Services/OperationsAgent.Api/Services/Capabilities/StreetlightToolsCapability.cs`: `CreateOperatorApprovalHandler`, pending decision and stage-window recheck.

This example trusts the client to ask the human. It does not prove caller identity, principal binding, state-revision binding, or restart survival. A continuation is one-use; the underlying restore command is not generally idempotent.

## Recovery and reset

If the question expires or the service restarts, make a fresh request. If the result is ambiguous, read the Hub before retrying. Reapply **Lights On Reported by a Client** before repeating the approval branch or beginning the next lesson. Don't silently reuse the repaired lamp.

## Investigate

1. Why does accepting a form with `approved=false` still decline the operation?
2. Why remove a token before checking its asset and expiry?
3. What would authenticate an approver, beyond a Boolean supplied by a client?
4. Which state changes could make this confirmation stale? Find the revision-bound solution in the next stage.
5. Why is this interaction different from `ApprovalRequiredAIFunction`?

Editorial note: the current Walkthrough's last sentence says the next stage gates a tool before execution. Course order first teaches Workflow, then runtime Tool Approval; narration makes that distinction explicitly.
