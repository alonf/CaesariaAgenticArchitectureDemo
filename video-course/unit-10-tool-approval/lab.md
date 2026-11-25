# Lab 10 — runtime interception before a protected write

Outcome: distinguish requested calls from executed writes and reconcile a timeout after a side effect. Allow 25 minutes. Baseline `843e0d0`; book chapter 10. Local services, real model calls, simulated controller fault.

## Walkthrough

1. Director → **Tool Approval** under **Controller Fault** → confirm. Main app → **Walkthrough**. Save the work-item list below.
2. **Investigate the reported controller fault on L-417**. Wait for the selected capability and actual argument summary. **Deny**. Respond promptly: this request is held open and the UI has a finite timeout.
3. Wait for the answer and inspect **Capability trace**. A rejected tool result is not execution. In this take the model requested the write twice; both were denied and the second used the standing refusal. Three of five calls ran across six model round trips. The list stayed empty.
4. Start a new investigation and **Approve** its fresh review. Verify the work-item list independently. The lamp should remain On/Faulted: a maintenance work item is not a physical repair.
5. Director → **Existing Incident** → **Apply Scenario**. Repeat the same investigation. Expect `get_incident`, incident `INC-L417-001`, dispatch already requested, and no additional work item. Read the records rather than assuming a particular sentence.

```bash
curl --fail --silent --show-error https://localhost:7311/api/operations-agent/remediation/work-items > work-items.json
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > hub-state.json
```

## Actual captured outcomes

- Denied run `aa03f1f37711478b9c177c59af3f99f0`: no item, two blocked attempts, standing refusal logged.
- Approved run `5a7860c10e4c4cc3a48b4bccf38f645a`: **WI-1 committed**, then the UI timed out during final response. This is not presented as a successful end-to-end answer. The evidence JSON and correlated logs establish the write. No retry was made.
- Existing-incident run `320504a6c243`: four tools/five model round trips, incident lookup, no duplicate write.
- An earlier denial take `93710df2d9b541b3833aca311a6e66df` also timed out after a long human wait; no item was created. It is a separate take, not silently joined to the completed denial.

## Code and limits

- `Services/OperationsAgent.Api/Services/Capabilities/ToolApprovalCapability.cs`: `ApprovalRequiredAIFunction`, unwrapped incident lookup.
- `Services/OperationsAgent.Api/Services/ToolApprovalResolver.cs`: bounded rounds, decision dictionary keyed by CallId, standing refusal, `CreateResponse`, same-session resume.
- `Services/OperationsAgent.Api/Services/MaintenanceTools.cs`: canonical asset, maximum summary length, stage recheck at the write.
- `Apps/CommandCenter.Web/Program.cs`: the captured baseline had an Operations Agent HTTP attempt timeout of 100s and total timeout of 110s. The subsequent course fix raises these to 190s/200s, beyond the server’s maximum 180s budget; unsafe POST retries disabled. Model latency and operator wait consume the request budget. This is a recorded limitation, not an Azure deployment diagnosis.

The pending operator store is in memory. This lesson does not demonstrate restart persistence, authenticated supervisor identity, or general exactly-once execution. The book discusses additional framework versions; this lab teaches the checked-out behavior and actual traces.

## Recovery and reset

After a timeout, read the work-item records first. A missing final answer does not justify repeating a committed write. Reapply Controller Fault for a fresh fixture and capture the new list baseline rather than assuming it is empty. Reload the main page if a prior workflow card remains after a stage change. Do not treat an old card as current execution.

## Investigate

- Who asks for the decision in MCP input, workflow approval and protected-tool interception?
- Can a denied call still have a tool-result message?
- What prevents repeated model requests from repeatedly prompting the human?
- Why validate arguments after approval, and why not silently rewrite an approved summary?
- How would you design a persisted operation/status resource so a caller timeout can be reconciled without duplicate writes?
- Why can discovering an existing incident improve approval quality without weakening policy?
