# Lab 10 — policy follows the protected function

Real local Windows execution; baseline `843e0d0`. Synthetic review narration. Holds are labelled.

## 01 — Three owners of a human decision
**Picture:** Context loop, then main application Walkthrough.

**Narration:** We have seen a remote tool ask for input and a workflow pause at a planned gate. This third control starts somewhere else: the model selects a consequential function, and the runtime intercepts it before execution. We will investigate a controller fault, deny the proposed maintenance write, then approve a new request and verify the record. Finally we will show why giving the agent a way to discover existing work can prevent an unnecessary approval altogether. The model chooses its path; policy still controls the protected capability.

## 02 — Inspect and deny the selected call
**Picture:** Controller Fault reset; separate successful denial take, exact arguments.

**Narration:** Prepare Tool Approval in Director with Controller Fault. Read the main application Walkthrough and choose Investigate the reported controller fault. The request does not name a function. The agent reads the live state, consults its procedure and evidence, and selects create maintenance work item. The review names the capability, L-417, and the exact proposed summary. Deny it. In this recorded run the model tries the same capability again with a shorter summary. The resolver applies the standing refusal without asking the operator a second time. Both write attempts are marked denied, did not run.

## 03 — Check execution, not just trace presence
**Picture:** Denied trace and approved request.

**Narration:** The trace records five requested calls, but only three ran: the skill, live read and knowledge search. Both maintenance calls were blocked. The service work-item list stayed empty. This is why a tool-result message is not proof of execution; a rejected invocation also produces a result. Now start a fresh investigation and approve its new proposed call. Approval is specific to the call and its arguments. It is not permission for any later summary or any future investigation.

## 04 — A timeout is not proof that nothing happened
**Picture:** Actual UI timeout, independently fetched WI-1 JSON, unchanged faulted Hub.

**Narration:** Here the recording takes a useful unexpected turn. The write completes, but the final response exceeds the main application request timeout. Do not press the write button again. First reconcile the outcome. This independently fetched service record shows work item W I one, its approved summary, creation time and matching correlation. Exactly one item exists. The Hub still reports on and faulted. We created an administrative commitment; no technician has repaired the simulated controller. A timeout after a side effect is an ambiguous caller outcome, not evidence that the side effect failed.

## 05 — Follow the policy in code
**Picture:** Approval wrapper, bounded resolver and execution validation.

**Narration:** Open ToolApprovalCapability. ApprovalRequiredAIFunction wraps the ordinary maintenance function; the incident lookup remains read-only and unwrapped. In ToolApprovalResolver, requests are answered by call identifier, resumed on the same session, and bounded by a round limit. The declined capability set makes a refusal stand for this request. In MaintenanceTools, the implementation validates the asset and summary, then checks the stage again in the same critical section as the write. The wrapper controls whether a call may begin; deterministic code still owns validation and the actual effect.

## 06 — The best write may be no write
**Picture:** Apply Existing Incident; answer and trace showing get_incident and no protected call.

**Narration:** Apply Existing Incident in Director and start the same investigation again. Now the asset names incident I N C L four seventeen zero zero one, with dispatch already requested. The agent calls get incident and discovers that work is under way. Its answer says it did not create a duplicate work item. Inspect the trace: four read or procedure calls ran, including the incident lookup, and there is no protected-write request. This is not relaxed policy. The agent has enough knowledge to avoid an unnecessary commitment. Looking before writing saves both resources and operator attention.

## 07 — Pause, reconcile and investigate
**Picture:** Exercise card.

**Narration:** Pause and run both decisions. Save the work-item list before and after. Denial should add nothing; approval should add one item while the lamp remains faulted. If the UI times out, inspect the records and correlation before considering another write. Then apply Existing Incident and verify that the lookup avoids duplicate work. Investigate where the approval binds to a call, how repeated requests are refused, and how the write rechecks its stage. These waits are in memory; do not infer restart recovery. We now have three distinct human-control mechanisms, each placed at a different architectural boundary.
