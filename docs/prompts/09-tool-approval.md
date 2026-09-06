# Stage 9 — ToolApproval

Deck anchor: `TOOL_APPROVAL` (MAF Human Control, slides 34–35).

## Goal

Add `DemoStage.ToolApproval`: the **third human-control point**, and the only reactive one. The
model decides by itself that a technician is needed and selects a protected capability; the
framework intercepts the call, and a supervisor approves before it runs.

Slide 34's notes give the taxonomy this stage completes:

| Control point | Who raises the pause | Stage |
| --- | --- | --- |
| MCP MRTR | the *capability* needs more input | 7 — InteractiveInput |
| Workflow HITL | the *orchestration* contains a human gate we drew | 8 — Workflow |
| **Tool approval** | the *model* chose a sensitive capability and policy requires a supervisor | **9 — this one** |

## Scope

- **`TOOL_APPROVAL`** (`Capabilities/ToolApprovalCapability.cs`) — the slide-verbatim wrapping:

  ```csharp
  AIFunction fileWorkItem = new ApprovalRequiredAIFunction(
      AIFunctionFactory.Create(maintenanceTools.CreateMaintenanceWorkItemAsync, ...));
  ```

  Nothing about the tool changes; the wrapper is the whole policy.
- **The protected capability is administrative, not physical.** Stage 8 deliberately took the
  direct write away from the agent, and this stage does not give it back. What the agent may
  decide by itself is to **file a maintenance work item** — committing city resources, through the
  same work-management emulator the workflow's failure branch uses.
- **The resume flow, as the slide draws it**: the run returns `ToolApprovalRequestContent`
  instead of an answer; the request is carried to the operator; `request.CreateResponse(approved)`
  goes back in a `User` message on the **same session**; the tool then runs. The operator prompt
  and polling are the ones stages 7 and 8 already use, so the control point differs while the
  operator's experience stays the same — which is the point.
- **A refusal stands for the request.** If the model asks again for a capability the operator just
  declined, it is answered from that standing decision instead of asking again until the execution
  budget expires. Bounded at three rounds.
- **Stage-gated and downgrade-safe**: the capability exists only at ToolApproval+, an approval
  answered after a downgrade is refused, and the stage is rechecked *inside the tool*, immediately
  before the work item is filed — the operator answers seconds before the model calls, and the
  capability can be withdrawn in between.
- **Arguments are validated** even though the model supplies them: the asset identifier must be
  canonical and the summary is capped, so a hallucinated asset never enters the work-item store.
- **Existing work is found before new work is filed.** The Existing Incident scenario is the same
  faulted controller, already tracked by `INC-L417-001` with a technician dispatch pending. The
  asset's state names the incident, and a read-only `get_incident` lookup - deliberately not
  wrapped, because looking is not committing - tells the model what that incident already covers.
  The same question that filed a work item a minute earlier now files nothing and raises no
  prompt: duplicate work is avoided by the model's own judgment, with the instructions naming the
  check.
- **Exhaustion fails loudly.** If approval requests remain after the bounded rounds, the turn
  fails with a 409 rather than returning a half-finished answer and persisting it as a completed
  turn.

## A note on what this approval is bound to

The requirements bind the *physical* corrective command to a validated state revision, because the
world can move between deciding and acting. This approval is bound to **the specific tool call**
instead, which is the framework's own binding, and that is the right choice here: filing a work
item is an administrative record, not a state transition, so there is no prior state whose change
would invalidate the decision. What can change is the *capability* — hence the stage recheck
immediately before the write. Stage 8's physical command keeps the revision binding.

## Lecture beat

1. At the ToolApproval stage, click **"Ask the agent to handle L-417's faulty controller"**. The
   request never names a tool — the model decides what to do.
2. The run stops and the operator prompt names the exact capability and arguments the model chose:
   `create_maintenance_work_item (assetId: L-417, summary: …)`.
3. **Deny first**: the agent completes its answer and reports that nothing was filed. Check the
   work-item list — empty. The model wanted to act; policy said no.
4. Ask again and **approve**: the same call now executes and the work item appears.
5. Apply **Existing Incident** on the switchboard and click the same button again. The trace shows
   `get_streetlight_state` naming `INC-L417-001`, then `get_incident` - and no work-item call. The
   answer reports the dispatch already pending; nothing is filed and no prompt appears. The model
   looked before it asked.
6. Name the taxonomy out loud: MRTR was the tool asking, the workflow gate was a node we drew,
   this is the model choosing and policy intercepting. Three control points, one operator
   experience. Then contrast reactive with proactive: *"I did not draw a graph that says 'ask the
   human here'. I attached a policy to one capability, and the framework enforces it wherever the
   model happens to select it."*

## Verification

- Deterministic tests pin that the maintenance capability reaches the model **only** through the
  approval wrapper, that it is stage-gated, and that the stage catalog and snippet registration
  include it.
- The live walk drives both paths against the real model: the framework intercepts the call, the
  prompt names the capability, a denial files nothing and the answer still completes, and an
  approval files exactly one work item.
- The scenario catalog test pins Existing Incident as the Controller Fault situation with the
  incident that tracks it, and the incident-tool tests pin what the lookup tells the model, for a
  tracked incident and for one the Command Center does not know.

## Deck note

Slide 34's **speaker notes name `FunctionApprovalRequestContent`, which does not exist** in the
current framework. The slide face is correct: `ApprovalRequiredAIFunction` and
`ToolApprovalRequestContent` (with `CreateResponse(bool, string?)`) are the real types, verified
against the packages this repository restores. Fix the notes before presenting from them.
