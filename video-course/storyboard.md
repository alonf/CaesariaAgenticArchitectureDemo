# Course storyboard

Prepared November 24, 2026. Audience: C#/.NET developers who can build a web API and want to
understand, run and debug an agentic application. Primary language: English. First production:
Unit 00, approximately 8–12 minutes. Later units are separate videos, normally 6–12 minutes.

## Teaching contract

One cumulative application, one city, one recurring question: why is streetlight L-417 on in
daylight? Each unit introduces a business need before its framework feature. The Director sets
the conditions. The main Command Center is where the operator performs the demonstration.
The Energy Hub and deterministic services own operational truth and physical commands.

This is a self-paced lab course, not a conference recording, product tour, or feature checklist.
Open on an observable incident and a question the learner can answer by the end. Use an H08
keyframe only when it explains that decision; the book supplies rationale, the pinned .NET 11
checkout supplies commands, and actual execution settles discrepancies. See
[book alignment](book-alignment.md) and [the Unit 00 shooting script](unit-00-introduction/shooting-script.md).

Keep L-417 as the continuing case. After each repair, explicitly show the Director reapplying
**Lights On Reported by a Client** before another investigation. L-528 is only a controlled
transfer exercise in the memory lesson: the prior L-417 case is a lead, never evidence about
another asset. Scenario variations change the cause of this same daylight anomaly, not the course's
subject. Each lesson records what was observed, what was inferred, who may authorize the action,
and the independent service evidence of its outcome. Never end with a pending approval or paused debugger.

## Repeatable unit structure

| Segment | Approximate time | Picture and narration | Evidence |
|---|---:|---|---|
| Observable outcome | first 30 sec | L-417 and the question this lesson will resolve | Define success before introducing the API |
| Starting fixture and stage | 0:30–1:30 | Director preparation, starting facts, explicit reset if already repaired | Stage, fixture and prerequisites verified |
| Instructor walkthrough | 1:30–4:00 | Main Command Center actions, actual decisions and waits | Read the trace, separate observation from inference |
| Exact implementation | 4:00–6:00 | File, command or useful breakpoint; an optional slide explains why | Identify decision owner and enforcement boundary |
| Pause and do | 6:00–7:00 | Learner repeats a bounded task using the companion lab | State the question; give viewers time to pause |
| Expected result and verification | 7:00–8:30 | Show service state, receipt, evidence or no-side-effect proof | UI/model prose alone is insufficient |
| Failure and recovery | final minute | One relevant negative path and exact reset/retry procedure | Learner can recover without the instructor |

Short units can compress the timings. Workflow, hosting, evaluation and recovery can take 12–15
minutes. Do not speed through code or time-critical approvals to hit a fixed duration.

## Unit map

The numbers are navigation anchors into the Director, not a requirement for seventeen equally
weighted videos. The learning path is **operate and verify → investigate with evidence → control
consequential actions → collaborate across boundaries → prove and operate the system**.
Combine or split later lessons after reviewing the pilot. Produce **only Unit 00 now**, then stop
for review. Hosting, Routines, Insights and other cloud or preview experiences are optional labs
with explicit prerequisites and availability labels. No cloud deployment, shared-setting change,
or publishing is authorized as part of this local course-production task.

| Unit | Title / purpose | Published slide pages | Scenario / stage | Code anchor and completion evidence |
|---|---|---|---|---|
| 00 | Repair L-417 and prove it happened | H08 7, 13, 15 only where useful | Lights On / Deterministic | Start pinned lab, prepare fixture, restore in main UI, GET Hub state/receipt, reset and repeat; debugger setup deferred |
| 01 | The deterministic foundation | H08 7, 11–13 | Lights On / Deterministic | `CommandCenterService.cs`, `HttpEnergyHubGateway.cs`; restore schedule, receipt succeeds, lamp off, no invented investigation |
| 02 | The first agent and its function tool | H08 15–18 | Lights On / InvestigationAgent | `AGENT_CREATION`, `FUNCTION_TOOL`; real `get_streetlight_state` invocation and unchanged authoritative state |
| 03 | A conversation that remembers | H08 20–21 | Lights On / Session | `AGENT_SESSION`, `AgentSessionStore.cs`; ask then Why in the same session, compare a new session |
| 04 | Knowledge with evidence | H08 22–23 | Lights On / Knowledge, evidence present | `KNOWLEDGE_RETRIEVAL`, `WorkKnowledge.cs`; cite WO-8732, then withhold evidence and show uncertainty |
| 05 | Memory across investigations | H08 24–25 | Knowledge case closed, then Memory / L-528 fixture | `CASE_MEMORY`, `CaseMemory.cs`; recall L-417 as a hypothesis, verify live facts independently |
| 06 | Skills as procedures | H08 26–27 | Lights On / Skills | `AGENT_SKILLS`, actual `SKILL.md`; observe skill load and structured incident brief |
| 07 | Move a tool across an MCP boundary | H08 28–31 | Lights On / McpTools, LOCAL then MCP | `MCP_CLIENT`, `MCP_SERVER`; same focused state query, different transport, correlated Hub read |
| 08 | The tool asks for human input | H08 32–33 | Lights On / InteractiveInput, MCP required | `MULTI_ROUND_TRIP_REQUEST`; deny with no command, rerun/approve, verify final state |
| 09 | An explicit remediation workflow | H08 34–35 | Lights On / Workflow | `WORKFLOW`, `RemediationWorkflowService.cs`; validate, policy, approval, execute, verify; finish denied and approved runs |
| 10 | Policy intercepts a model-selected tool | H08 36–37 | Controller Fault, then Existing Incident / ToolApproval | `TOOL_APPROVAL`, `MaintenanceTools.cs`; deny then approve work item, existing incident avoids duplication |
| 11 | A second reasoning and authority boundary | H08 38–42 | Security Operation / MultiAgent, consult OFF then ON | `SecurityAssessmentAgent.cs`, `SecurityMcpTools.cs`; sanitized judgment explains required lighting, lamp remains on |
| 12 | Delegate to a peer with A2A | H08 43–44 | Lights On / A2ADelegation | `A2A_DELEGATION`, workforce agent card and `WorkOrderTools.cs`; shareable work-order projection, cost never enters accessible tool data |
| 13 | Hosting, identity and deployment | H08 45–51; W20 40–46 | Hosting, LOCAL then FOUNDRY HOSTED | Hosted `Program.cs`, cloud gateway and deployment workflows; real hosted answer with Work IQ source and consent, cloud Hub distinguished from local |
| 14 | Prove behavior with ASSERT | H08 52–55 | Evaluation harness owns fixture changes | `evaluation/assert_demo/cases.json`, `Invoke-AssertDemo.ps1`; baseline/governed report with actual tools, approvals, outcomes and failures |
| 15 | A durable agent and a surviving repair | H08 45–46, refer back to 34–37; new explanatory insert needed | Director 15 / Workflow or later | `RecoveryHttpFunctions.cs`, `RecoveryOrchestration.cs`, `RecoveryActivities.cs`; same session, unchanged review, crash/restart, decision, terminal receipt |
| 16 | Routines activate the agent | H08 45–46 plus clearly labeled course insert | Prepared deployed agent and routine identity | `docs/runbooks/routines-and-insights.md`, actual routine invocation code; real run ID/status and read-only outcome |
| 17 | Insights feed engineering improvement | H08 52–55 plus clearly labeled course insert; W20 36–38 | Existing monitor with findings | Insights retrieval and reviewed ASSERT case; show evidence/version attribution and a bounded regression, not automatic production repair |

## Detailed shooting beats

### 00 — Repair L-417 and prove it happened (produce first)

Open within the first 30 seconds on L-417 reported On in daylight. Promise one outcome: the learner
will restore scheduled mode and prove the simulator-confirmed result at the Energy Hub. Follow
the [timed shooting script](unit-00-introduction/shooting-script.md) and
[copyable lab](unit-00-introduction/lab.md). These replace the earlier slide-led introduction.
Use no model call or cloud deployment. Show the exact local baseline and prerequisites, Director
preparation, Command Center action, actual Hub JSON before/after, a short readable code excerpt,
and an explicit reset. Close with pause-and-do, expected receipt/state, and recovery from the wrong
fixture. Mark SmartPole as simulated and the pilot as a rough cut if it uses edited screen captures.

Pilot production status: actual Director preparation and the main-app repair were executed and
captured as stills. The rough cut uses actual Hub responses for after-state/activity and an explicitly
labeled API reset after the capture connection closed. Startup/Aspire and Director-reset motion
shots remain pickups before a finished release. See Unit00 execution-evidence.md; do not present
the rough cut as fulfillment of those missing shots.

### 01 — Deterministic city

Continue from the Unit 00 repair: reset L-417 explicitly, then explain the distinction between
detecting an anomaly and investigating its cause. Use H08 11 if helpful.
In Director select **1 Deterministic**, accept the listed preparation, and open Command Center.
Show reported On, schedule Off, override Yes. Click **Restore Scheduled Mode**. Follow the
Command Center gateway into the Energy Hub and simulator. Return to the UI: reported Off,
override cleared, correlated successful command, no incident magically opened. Exercise: reapply
Lights On and identify the owner of each displayed fact. This baseline needs no model call.

### 02 — Agent and tool

Start with the unresolved L-417 anomaly: can an agent establish the current state? Use H08 15
and 17 to explain the loop. Prepare **2 Investigation Agent**. Ask whether L-417 is on. Show the actual
tool event and answer. Attach VS Code to OperationsAgent.Api and arm **Agent creation** or
**Function tool**. Repeat the question, step through the named region and inspect the model,
instructions and focused tool registration. Resume and show the completed answer. Nothing here
authorizes repair. Exercise: ask for a cause not present in the returned operational state.

### 03 — Session

Start with the ambiguous follow-up “Why?” about L-417. Use H08 20 when explaining context.
Prepare **3 Session**. Ask, then use the same-session Why action. Inspect the session
identity and serialize/deserialize path. Demonstrate that context continuity can resolve a reference
without obtaining new evidence. Start a new conversation and compare. Complete both turns and
detach. Exercise: distinguish remembered state from a fresh Hub read.

### 04 — Knowledge

Start with the missing reason for L-417's override. Use H08 22 to distinguish evidence from context.
Prepare **4 Knowledge** with evidence enabled. Ask and follow up. Show the retrieved
work order and technician note and their answer citations. Step through `TextSearchProvider`
registration and the narrow search result. Disable seeded evidence, start a fresh investigation,
and show that an unsupported explanation must be withheld. Restore the evidence toggle afterward.

### 05 — Case memory

Start with the question of what a closed L-417 case can justify in another investigation. Use H08 24.
Complete and close the evidence-backed case before selecting **5 Memory**. Investigate
the documented L-528 fixture. Inspect the memory provider, its retrieved case and hypothesis framing.
Verify current facts separately. Finish the turn and explain why memory cannot become a command
authorization or a substitute for evidence. Exercise: remove the prior case and compare.

### 06 — Skills

Start with the need for a repeatable L-417 investigation procedure. Use H08 27 where useful.
Prepare **6 Skills**. Show the skill catalog and the actual triage SKILL.md. Run the
investigation, observe the load call and mandated brief format. Inspect provider construction and
the loaded procedure. An optional markdown edit belongs in a disposable copy and must be restored.
Finish with a completed brief and clear source attribution, not a claim of another agent.

### 07 — MCP

Start with the same L-417 question across a service boundary. Use H08 28 and 30 to locate it.
Prepare **7 MCP Tools**, starting LOCAL. Run the question, flip to MCP and
repeat. Trace discovery on the client and the server's focused Energy Hub tool. Show correlation
across HTTP and explain that remote reach does not grant extra authority. Finish with equivalent
authoritative state. Exercise: make the MCP endpoint unavailable and read the actual failure.

### 08 — Interactive input

Start with the need to obtain operator input before clearing the override. Use H08 32.
Prepare **8 Interactive Input** with MCP selected. Request restoration and stop at
input-required. In code inspect the question, continuation and reviewed state. Deny, verify the
lamp did not change, then run again and explicitly approve. Finish on the confirmed receipt.
Explain that the tool is asking for input; this differs from a workflow gate and a policy wrapper.
The pending input is in memory in this lab; restarting a worker is not a supported continuation.
Exercise: deny and verify no succeeded restore receipt; start a new request to recover.

### 09 — Workflow

Start with the need to enforce a complete repair sequence. Use H08 34 to explain the planned gate.
Prepare **9 Workflow**, show its rendered graph and equivalent displayed YAML.
Run, deny the review, and verify no repair. Reapply the fixture, run and approve. Use Visual Studio
2026 for this unit's code walk through validation, revision-bound decision, execute and verify.
Resume every breakpoint before checking the outcome. Optional negative case: Security Operation
requires lighting and policy prevents turning it off. Do not call this in-process graph durable.
Exercise: change the fixture while a review is pending and inspect the stale-state refusal. Reset,
re-read and start a new workflow; never reuse approval against different state. Do not imply that
these in-memory approvals survive process restart.

### 10 — Tool approval

Start with a repair that failed because L-417's controller is faulted. Use H08 36 to locate policy.
Prepare **10 Tool Approval** with Controller Fault. Let the model select the protected
maintenance tool and show the framework's interception. Deny with no work item, then approve a
new attempt and show the actual work item. Apply Existing Incident and show reuse without another
item. Inspect `ApprovalRequiredAIFunction` and the domain enforcement that remains beneath it.

### 11 — Multi-agent boundary

Start with evidence that another domain may need L-417 on. Use H08 38 and 42 to explain the boundary.
Prepare **11 Multi-Agent**, Security Operation, consult OFF. Ask why the lamp
is on. Enable consult and repeat. Show Operations asking Security through a remote capability,
then inspect the sanitized response and the domain API boundary. Finish with lighting still on
and an explanation. Do not demonstrate handoff or group chat as implemented features.

### 12 — A2A

Start with the missing workforce explanation for L-417. Use H08 44 to explain delegation.
Prepare **12 A2A Delegation**. Discover the workforce card and delegate the work-order
question. Inspect the shareable projection at its source. Ask for labor cost and show that this
field was never included in accessible records. Complete the peer task and the caller's answer.
Contrast task delegation with Security agent-as-tool without claiming every A2A pattern is used.

### 13 — Hosting and cloud deployment

Start with the requirement to operate the same investigation in a managed habitat. Use H08 45–49.
Explain three separate setup scopes: local services, access to model
inference, and deployed hosted agents. Use `docs/deployment.md`: prerequisites, CI identity,
reviewed infrastructure plan, platform apply, Energy Hub deployment, then hosted-agent deployment.
Show account/tenant values only as placeholders. Use a prepared environment for the live unit;
do not spend viewer time waiting for provisioning or silently provision billable resources.
Demonstrate LOCAL vs FOUNDRY HOSTED, source-of-record Work IQ evidence and required user consent.
Show the actual cloud Hub boundary. Close with Agent 365/identity as a separately configured
tenant experience. Portal actions, unavailable features and historical screenshots must be labeled.

### 14 — Evaluation

Start with a concrete L-417 behavior that could regress. Read the requirement, then use H08 52–55.
Prepare ASSERT and a judge
deployment, execute one forgotten-override case in both arms, and open the generated report.
Inspect ordered tools, approval decisions, outcome and judging rationale. A model response or
infrastructure error is not a pass. Keep the full suite as an exercise and preserve real artifacts.

### 15 — Durable recovery

Start with a repair waiting for a decision when its worker disappears. Use hosting/workflow
slides and a course insert explaining the separate persisted
conversation and repair orchestration. In Director choose **15 Durable recovery**, then **Prepare
foundation**. In Command Center start recovery and explicitly wait for **AwaitingApproval**.
Return to Director and click **Crash worker and restart**. Never use scheduler **Terminate**.
Back in Command Center click **Continue the same conversation after restart**. Inspect the
unchanged original review, approve, and poll to a terminal business result plus Hub receipt.
Show entity-owned conversation, orchestration timer/event, activities, bounded read retries and
pending-receipt reconciliation. Finish with the documented local-worker-only recovery boundary.

### 16–17 — Extensions with explicit evidence gates

Routines: prepare authentication and a deployed runnable agent before recording. Dispatch a
read-only run and retain real run evidence. A schedule grants no additional command authority.
Insights: retrieve generated findings from an existing monitor and inspect linked evidence.
Separate diagnosis hypotheses from verified causes and convert one reviewed finding into a
behavioral regression. If authentication, monitor data or a meaningful finding is unavailable,
record a clearly labeled explanation of setup and limitations; never fabricate a successful run.

## Capture and editing rules

- Preserve Alon Fliess's deck attribution. Show original slides, not rewritten approximations.
- Use the current code for executable API details and label September deck examples as historical.
- Keep tenant IDs, tokens, private work orders, notifications and unrelated desktop content out.
- Crop to the relevant app. Include readable code at 1080p, with a tighter crop when needed.
- Every chapter has narration and subtitles. Explain the outcome before changing screens.
- Record real scenario changes and waits. Edited still sequences are identified as walkthrough
  captures; never claim they are uninterrupted screen recordings or a fresh cloud deployment.
- Capture both debugger frontends. Attach one debugger per process; F10 steps over, F11 steps
  into, Shift+F11 steps out, F5 resumes. Debug local C# execution, not a model's internal reasoning.
- Mark future units as planned until their own execution, code inspection and outcome are recorded.
- Distinguish real local execution, simulated physical devices, prerecorded captures, and optional
  cloud results on screen. Do not replace a failed run with invented successful output.
- Unit 00 keeps IDE setup to one short note; actual attachment and stepping belong in the lesson
  where the breakpoint explains a decision. Later lessons demonstrate both supported IDEs.
- If capture is blocked, deliver the script and lab, list the missing shots precisely, and do not
  call a narration track or slide montage a completed hands-on pilot.

## Learner tasks to complete before later units can be recorded

These are planning cards, not completed demonstrations. Each later shooting script must use the
same seven-part lesson structure above and turn its file/region anchor into an exact baseline path.

| Units | Pause-and-do outcome | Expected verification | Failure and recovery |
|---|---|---|---|
| 01–02 | Reset L-417; distinguish deterministic restore from read-only agent observation | Hub receipt for restore; actual tool trace and unchanged Hub state for agent read | Missing model credentials: fix local model access, start a fresh turn; city still works |
| 03 | Ask a follow-up in the same session, then compare a new session | Conversation identity/context; live state still obtained from Hub | Downgrade invalidates session: start a new conversation |
| 04 | Withhold work evidence and ask for the reason | No supported work-order explanation without retrieval | Restore evidence toggle and start a fresh investigation |
| 05 | Explain what a remembered L-417 case permits you to hypothesize | Recall labeled hypothesis; separate fresh Hub/tool evidence | Clear memory and compare; rerun Knowledge to rebuild the prerequisite case |
| 06 | Find the loaded procedure and one instruction reflected in the brief | Actual load call plus reviewed skill source | Restore any edited skill; repeat with a new investigation |
| 07 | Compare local and MCP read of the same reset asset | Same state, new boundary, correlated tool call | Unreachable MCP: restore endpoint/process and retry a fresh read |
| 08 | Deny an input request before trying approval | Denial has no successful restore receipt; approval changes Hub state | Restart loses pending in-memory request: reset and request again |
| 09 | Change fixture during review, then try the old decision | Stale-state refusal without applying that old decision | Reapply correct fixture, re-read, start a new workflow/review |
| 10 | Deny a model-selected maintenance tool, then test existing incident | No work item on denial; existing work reused | Finish/cancel pending requests; reset fixture before next attempt |
| 11 | Compare consult off/on for security-required lighting | Shareable verdict, lamp stays on, no restricted records leaked | Restore consult toggle and re-investigate; do not repair required lighting |
| 12 | Ask the peer for work context and then inaccessible cost | Shareable result; cost absent from accessible projection | Restore peer endpoint and begin a new task |
| 13 (optional) | Identify local/cloud Hub and habitat of one real response | Actual cloud execution identity/version and reachable Hub | Missing permissions/setup: use local lesson, label cloud section unavailable |
| 14 | Run a single behavioral case and explain its outcome | Actual tools, approvals, Hub outcome and judge scope | Infrastructure failure is not a pass; correct it and rerun the case |
| 15 | Restart only the worker while AwaitingApproval | Same pending operation, original review, terminal Hub receipt | Expired/terminated operation: explain status and begin a new attempt |
| 16–17 (optional) | Link a real run/finding to evidence and a regression | Real invocation/monitor identifiers, bounded reviewed test | Missing monitor data/auth: stop the cloud exercise; never invent findings |
