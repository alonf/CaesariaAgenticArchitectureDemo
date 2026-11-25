# Lab 10 — the runtime intercepts a consequential tool

Baseline `843e0d0`; companion book chapter 10. Windows recording, local services and simulated controller. Outcome: deny a model-selected maintenance request without filing work, approve a new request and inspect the work-item record, then discover existing work instead of creating a duplicate.

1. Recall the previous two controls: the MCP tool requested input; the workflow paused at an authored gate. Explain runtime interception before a selected protected function executes.
2. Director → Tool Approval / Controller Fault. Main app Walkthrough, investigate; wait for the actual capability and arguments. Deny first, verify the work-item list unchanged.
3. Repeat investigation, approve a new call, verify a work-item identifier at the service. The lamp remains faulted: an administrative commitment is not a physical repair.
4. Show `ToolApprovalCapability` wrapping only the write in `ApprovalRequiredAIFunction`; `ToolApprovalResolver` decisions by call ID, bounded resumption and standing refusal; `MaintenanceTools` validation and stage check at execution.
5. Apply Existing Incident and repeat the same investigation. Show the incident lookup and no duplicate work item. If the model differs from the expected path, keep the actual result and explain it.
6. Exercise and recovery: deny/approve separately, verify record and Hub, reset fixture. Investigate approval ownership, arguments, stage downgrade, session resumption and in-memory limitations. Do not claim crash recovery or cloud identity.
