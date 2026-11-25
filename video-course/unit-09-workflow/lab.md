# Lab 09 — explicit workflow, approval and changing state

Outcome: deny, reject a stale approval, then complete a fresh correction. Allow 25 minutes. Baseline `843e0d0`; companion book chapter 10. Real local services and model configuration; SmartPole is simulated.

## Run the three branches

1. Director → **Workflow** → confirm. This prepares **Lights On Reported by a Client**. Main app → **Walkthrough**, then inspect **Graph (generated)** and **Declarative YAML**. YAML is displayed, not executed.
2. **Run remediation workflow for L-417**. Wait for **Operator input required** and the `approval` node. Note `stateRevision`. **Deny**. Expect gate declined, execute skipped, verify unresolved, no work item.
3. Run again. Leave the gate open. Director → scenario 1 → **Apply again**. Return and approve the old review. The Hub revision has changed; expect no stale restore, unresolved result, and a request to review fresh state.
4. Run again, inspect the new revision, approve. Wait for terminal success, then independently read the Hub.

```bash
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > hub-state.json
curl --fail --silent --show-error https://localhost:7311/api/operations-agent/approvals/ > pending-review.json
```

Expected success: `reportedIsOn=false`, `manualOverride=false`, `lastCommand.status="Succeeded"`, matching correlation, `isAnomalous=false`. In the recording: denied `5825f2e3a9cc41dd81ea8a948a286ce8`; stale `33fdd21fe62a4400abc6e2f40892941b` (review9/current11); fresh `23db1ef19b0c42809047bea0b653711b` (review11/result12). These are separate runs.

## Code and debugger

- `Services/OperationsAgent.Api/Services/RemediationWorkflowService.cs`: `WORKFLOW`, `CreateRemediationWorkflow`, `InProcessExecution`.
- `Services/OperationsAgent.Api/Services/RemediationWorkflow.cs`: `OperatorApprovalExecutor`, `ExecuteRestoreExecutor.HandleAsync`, `VerifyStateExecutor`.
- Director → Advanced controls → Operations Agent → `WORKFLOW` → Attach VS Code or Attach Visual Studio 2026 (Windows only). Trigger a fresh run. Step graph creation, continue promptly. Optional breakpoint in `ExecuteRestoreExecutor.HandleAsync` at its first `if`; approve, expand `input` and `Request`, inspect approval and revision, then step over the Hub command and expand `result`. Continue; detach in Director.
- Remove optional editor breakpoints when finished. Never approve against an unrelated run just to advance a debugger.

## Recovery and reset

Reapply the daylight fixture after success. If a previous workflow card remains after changing stage, reload the main page; historical results must not be read as the new scenario's current state. A pending decision is held in `PendingApprovalStore`, backed by an in-memory task. This run is not checkpointed or restart durable. Restarting that service is not a way to resume its old approval; prepare a fresh run. Lab 15 teaches a separate durable implementation.

## Investigate

- Why does reapplying the same-looking scenario invalidate the original approval?
- Where is the expected revision enforced at the authoritative boundary?
- What is the difference between command execution and verified resolution?
- Why does denial not produce a maintenance work item?
- Apply Security Operation, then run the workflow: why should it take a no-action path?
- Which additional persistence and authorization properties would production require?

## Framework reference in the revised video

Read [Workflow human input](https://learn.microsoft.com/en-us/agent-framework/workflows/human-in-the-loop), then return to the repository. Identify the executor that requests input and the application response path. Contrast this recorded in-memory gate with Lab 15 durable recovery.
