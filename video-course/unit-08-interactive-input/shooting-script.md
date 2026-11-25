# Lab 08 — a tool requests input

Real local Windows execution; baseline `843e0d0`. Synthetic review narration. Holds are labelled.

## 01 — A tool can need more information
**Picture:** Context loop then input-stage Walkthrough.

**Narration:** A remote capability sometimes needs information that the model cannot supply on its own. MCP elicitation lets the tool ask its client for input. In this lesson that input is an operator decision, but the same interaction shape can request a missing field or choice. Our outcome is observable: decline a restore and prove no command happened, then approve a fresh request and verify the actual repair. This is the tool asking for input during its invocation. Later we will compare a planned workflow gate and a runtime interception before a protected tool executes.

## 02 — Prepare and deny first
**Picture:** Director stage, Walkthrough, restore request and actual pending panel, Deny.

**Narration:** Prepare Interactive Input in Director and keep MCP remote selected. L-417 must start on in daylight with a manual override. Read the main application Walkthrough and choose Restore L-417 to scheduled mode, agent. Wait until Operator input required actually appears. The agent has selected restore scheduled mode; the remote tool is waiting for input. Deny first. A refusal is a successful exercise of control, not a broken demo. We are looking for a cancellation answer and an unchanged Hub command receipt, not simply a button that disappears.

## 03 — Verify denial, then approve a new request
**Picture:** Denied answer, repeated request, pending review, approve, resulting green map.

**Narration:** The answer reports that the operator declined and the lamp remains unchanged. The Hub snapshot confirms that its command receipt and revision are unchanged. Now request the restore again. This is a new confirmation, not reuse of the old decision. Review the asset and the effect: clearing the manual override. Approve this request. Watch the deterministic path finish. The SmartPole simulator confirms off, the Hub records success, and the map becomes green. The model summarizes that outcome after the tool returns; its sentence is not what changed the lamp.

## 04 — Prove the result at the Hub
**Picture:** Actual saved GET response, highlight state and receipt.

**Narration:** Here is the saved response from a fresh Energy Hub GET, taken after approval. Reported on is false, manual override is false, and the last command is Restore scheduled mode with status Succeeded. The correlation matches the application trace. This is our verification checkpoint. The physical device here is a simulator, but the agent request, protocol exchange, Hub execution, and receipt are real application behavior. If the UI said success while these fields disagreed, we would investigate the discrepancy rather than accept the prose.

## 05 — Read the server control flow
**Picture:** EnergyRestoreMcpTool continuation checks then InputRequiredException/schema.

**Narration:** Open EnergyMcpTools. A client without the supported interaction gets a refusal. A continuation must consume the server-issued request state for this asset. Then the code checks the decision. Decline returns without calling restore. Approval reaches the deterministic Hub method. With no answer yet, the method throws InputRequiredException containing a Boolean schema and fresh request state. The MCP client handles that interaction and retries with the response. From the agent perspective this remains one tool invocation, although protocol round trips and a human decision happen inside it.

## 06 — Understand what this does not guarantee
**Picture:** Client handler and request-state store.

**Narration:** The client handler parks the question in a pending store, awaits the decision, and checks that the stage still permits this capability. The server consumes request state once, with an asset binding and expiry. These are useful controls, but they do not authenticate the operator. The demo trusts its client to ask the human, and the restore in this stage is not bound to the state revision the operator saw. Both the pending question and request-state store are in memory. A restart does not preserve this approval wait. The workflow and durable lessons address different parts of those requirements.

## 07 — Pause and prove both decisions
**Picture:** Exercise card.

**Narration:** Pause and reproduce both branches. Reset the daylight anomaly, deny, and compare the Hub receipt before and after. Then start a fresh request, approve, and verify off with the override cleared. If the wait expires, start again with a fresh request; do not reuse an old confirmation. Investigate where request state is issued and consumed, whether the server knows who approved, and what happens if operational state changes while a question is open. We have demonstrated tool-driven interaction, not durable or fully state-bound authorization. Next we put the corrective operation on an explicit workflow path.
