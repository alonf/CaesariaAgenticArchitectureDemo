# Agentic C# — course storyboard

**Building C# Agentic Systems with MAF and Foundry by Alon Fliess**.
The smart city is the teaching example; transferable engineering judgment is the goal.

Production review: [Labs 02–17 local videos, captions and companion labs](review-index.md).
The review index records actual execution, optional preparation-only lessons and remaining pickups.

Audience: C#/.NET developers who need to architect, design and implement new agentic systems,
or add agentic capabilities to existing applications. No prior agent-development knowledge is
assumed. Developers who already use the APIs should gain the architectural judgment to decide
when to use each mechanism and why. Primary language: English. First production:
Unit 00, a chaptered foundation and environment/debugging lesson, approximately 33 minutes.
Later units are separate videos, normally 6–12 minutes.

## Teaching contract

One cumulative application, one city, one recurring question: why is streetlight L-417 on in
daylight? Each unit introduces a business need before its framework feature. The Director sets
the conditions. The main Command Center is where the operator performs the demonstration.
The Energy Hub and deterministic services own operational truth and physical commands.

This is a self-paced lab course, not a conference recording, product tour, or feature checklist.
Unit 00 first lays the foundation: audience, course purpose, Microsoft Agent Framework and
Foundry, and architecture decisions. Orient through GitHub/README/project structure and the
existing Foundry project and hosted agent. Introduce the city with slide **12**, tracing the
city systems, domain Hubs/digital twins and deterministic core; only then use slide **13** for
the agentic layer. Teach **slide 14, Context Engineering**, before the first code/tool example.
Then visit Microsoft Learn and use the short code on slides **16 and 18** before the real code. The first live **local environment** view is Aspire, followed by the two UIs.
Record actual attach/step/inspect/continue/detach in VS Code followed by Visual Studio 2026,
using Director controls for both attachments and detachments. Only then begin individual labs.
Each topic follows concept → concrete walkthrough → short recap. Preserve and improve the
existing introduction rather than replacing it with a product tour.

Use full-frame application/code footage without permanent title or credit strips. Custom cards
use larger type, short statements and relevant deck/app visuals. Show actual Watch-tree expansion,
tool name/arguments, the C# invocation, the Hub result sent to the next model interaction, and the
final response. Truncated values should be expanded or evaluated in Debug Console. Explain that
Mac learners use VS Code; Windows learners may choose either IDE. Synthetic narration and local
rough-cut status belong in the companion notes and file labeling, not persistent video borders.
Later labs open on an observable incident and a question the learner can answer by the end. Use a branded-deck
keyframe only when it explains that decision; the book supplies rationale, the local .NET 11
checkout supplies commands, and actual execution settles discrepancies. See
[book alignment](book-alignment.md) and [the revised Unit 00 plan](unit-00-agentic-csharp/production-plan.md).
Teach in present tense without historical date comparisons. Keep exact version provenance in
the setup/source notes. Spoken version guidance: the lab code is .NET 11; the same Agent
Framework concepts and patterns apply on .NET 10 LTS. This checkout still requires its declared SDK.
Always say **Microsoft Agent Framework** or **Agent Framework**, never the acronym.
Unit 00 explains the IDE choice without a Mac take. Lab 01 introduces the actual Mac checkout,
C# Dev Kit and Foundry Toolkit before the deterministic exercise; see its
[shooting plan](unit-01-deterministic-foundation/storyboard.md).

Context Engineering is the recurring model for later lessons: application/framework assemble
instructions, user request, history, knowledge and admitted tool definitions; the model may request
a tool; deterministic code executes it under policy; its result enters the next context/model call.
Connected agents can be capabilities too. One app invocation can contain multiple exchanges.
Completion, requests for input, approval gates, cancellation, errors and limits are distinct outcomes.
A Description attribute supplies metadata; AIFunctionFactory and registration expose the local
function. Do not invent a tool-class attribute.

In debugger footage, announce entry only after the visible breakpoint. Use synchronized markers
on actual values. Explicitly distinguish exchange 1 selecting the tool, deterministic execution,
and exchange 2 using its result to answer. Explain observable protocol data, not private reasoning.

Keep L-417 as the continuing case. After each repair, explicitly show the Director reapplying
**Lights On Reported by a Client** before another investigation. L-528 is only a controlled
transfer exercise in the memory lesson: the prior L-417 case is a lead, never evidence about
another asset. Scenario variations change the cause of this same daylight anomaly, not the course's
subject. Each lesson records what was observed, what was inferred, who may authorize the action,
and the independent service evidence of its outcome. Never end with a pending approval or paused debugger.

## Repeatable unit structure

This table applies to the labs after Unit 00. The foundation/onboarding unit follows its required
architecture → environment → both debuggers sequence instead.

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
Combine or split later lessons after reviewing the pilot. Unit 00 is approved. Production is now
authorized through the remaining lessons for a batch review when Alon returns. Hosting, Routines, Insights and other cloud or preview experiences are optional labs
with explicit prerequisites and availability labels. No cloud deployment, shared-setting change,
or publishing is authorized as part of this local course-production task.

| Unit | Title / purpose | Published slide pages | Scenario / stage | Code anchor and completion evidence |
|---|---|---|---|---|
| 00 | Understand, run and debug the system | Alon-branded deck; book rationale; GitHub; Microsoft Learn; real app/Foundry/IDE recordings | Foundations, then Lights On / InvestigationAgent for a read-only debugger exercise | Audience and purpose; MAF/Foundry; repo/README/code map; slide 12 Hubs/twins → 13 agentic layer → 14 context loop → 16/18 simple C#; Aspire → Director → Command Center → Director attach/inspect/detach in either IDE; actual model/tool/result evidence |
| 01 | Operate and verify before adding an agent | Deck 12; 14 for recap; 6 for decisions | Lights On / Deterministic, then Controller Fault | Mac VS Code and Foundry Toolkit orientation; `CommandCenterService.cs`, `HttpEnergyHubGateway.cs`, `EnergyHubService.cs`; restore schedule, verify Hub receipt and reported state, demonstrate failure, reset |
| 02 | The first agent and its function tool | Deck 15–18 | Lights On / InvestigationAgent | `AGENT_CREATION`, `FUNCTION_TOOL`; real `get_streetlight_state` invocation and unchanged authoritative state |
| 03 | A conversation that remembers | Deck 20–21 | Lights On / Session | `AGENT_SESSION`, `AgentSessionStore.cs`; ask then Why in the same session, compare a new session |
| 04 | Knowledge with evidence | Deck 22–23 | Lights On / Knowledge, evidence present | `KNOWLEDGE_RETRIEVAL`, `WorkKnowledge.cs`; cite WO-8732, then withhold evidence and show uncertainty |
| 05 | Memory across investigations | Deck 24–25 | Knowledge case closed, then Memory / L-528 fixture | `CASE_MEMORY`, `CaseMemory.cs`; recall L-417 as a hypothesis, verify live facts independently |
| 06 | Skills as procedures | Deck 26–27 | Lights On / Skills | `AGENT_SKILLS`, actual `SKILL.md`; observe skill load and structured incident brief |
| 07 | Move a tool across an MCP boundary | Deck 28–31 | Lights On / McpTools, LOCAL then MCP | `MCP_CLIENT`, `MCP_SERVER`; same focused state query, different transport, correlated Hub read |
| 08 | The tool asks for human input | Deck 32–33 | Lights On / InteractiveInput, MCP required | `MULTI_ROUND_TRIP_REQUEST`; deny with no command, rerun/approve, verify final state |
| 09 | An explicit remediation workflow | Deck 34–35 | Lights On / Workflow | `WORKFLOW`, `RemediationWorkflowService.cs`; validate, policy, approval, execute, verify; finish denied and approved runs |
| 10 | Policy intercepts a model-selected tool | Deck 36–37 | Controller Fault, then Existing Incident / ToolApproval | `TOOL_APPROVAL`, `MaintenanceTools.cs`; deny then approve work item, existing incident avoids duplication |
| 11 | A second reasoning and authority boundary | Deck 38–42 | Security Operation / MultiAgent, consult OFF then ON | `SecurityAssessmentAgent.cs`, `SecurityMcpTools.cs`; sanitized judgment explains required lighting, lamp remains on |
| 12 | Delegate to a peer with A2A | Deck 43–44 | Lights On / A2ADelegation | `A2A_DELEGATION`, workforce agent card and `WorkOrderTools.cs`; shareable work-order projection, cost never enters accessible tool data |
| 13 | Hosting, identity and deployment | Deck 45–51; W20 40–46 | Hosting, LOCAL then FOUNDRY HOSTED | Hosted `Program.cs`, cloud gateway and deployment workflows; real hosted answer with Work IQ source and consent, cloud Hub distinguished from local |
| 14 | Prove behavior with ASSERT | Deck 52–55 | Evaluation harness owns fixture changes | `evaluation/assert_demo/cases.json`, `Invoke-AssertDemo.ps1`; baseline/governed report with actual tools, approvals, outcomes and failures |
| 15 | A durable agent and a surviving repair | Deck 45–46, refer back to 34–37; new explanatory insert needed | Director 15 / Workflow or later | `RecoveryHttpFunctions.cs`, `RecoveryOrchestration.cs`, `RecoveryActivities.cs`; same session, unchanged review, crash/restart, decision, terminal receipt |
| 16 | Routines activate the agent | Deck 45–46 plus clearly labeled course insert | Prepared deployed agent and routine identity | `docs/runbooks/routines-and-insights.md`, actual routine invocation code; real run ID/status and read-only outcome |
| 17 | Insights feed engineering improvement | Deck 52–55 plus clearly labeled course insert; W20 36–38 | Existing monitor with findings | Insights retrieval and reviewed ASSERT case; show evidence/version attribution and a bounded regression, not automatic production repair |

## Detailed shooting beats

### 00 — Foundations and practical onboarding

Follow [the revised production plan](unit-00-agentic-csharp/production-plan.md). Open with the course promise before naming the city: rapid change makes foundations and sound architectural decisions essential. Introduce MAF as the development framework, Foundry as the platform, C# as the course language, and knowledge, tools, deterministic code, policy and governance as the system around the agent. Use Alon’s About Me slide and invite learners to enjoy the journey. Then explain what
the learner will build and why the architecture is divided this way. Define MAF and its model/tool
loop; explain domain Hubs and operational twins before adding agentic investigation. At the decision map, introduce `github.com/alonf/agentic-architecture-skills`: architecture router, least sufficient autonomy, MAF mapping, and verification of assistant advice. Introduce the
learning journey through requirements, tradeoffs and verification, then teach environment use.

The first live environment view is Aspire, followed by Director and Command Center. Use a simple
read-only InvestigationAgent request to teach the mechanics of debugging: attach VS Code, arm a
stop, ask in the main app, inspect tool arguments and real model results, resume and detach. Repeat
in Visual Studio 2026 with single stepping, Watch values and AI result inspection. State that Visual Studio 2026 is Windows-only. Open Walkthrough at the upper right of the main app and read the starting conditions and actions before each walkthrough. Confirm the service remains
alive and no lamp command ran. This orientation uses existing model inference; it does not deploy.

The earlier repair-first rough cut under `unit-00-introduction/` is archived review material.
Its deterministic repair content is useful for Lab 01, but does not satisfy the revised introduction.
Do not present old captures as new debugger footage. Finish only revised Unit00, then stop for review.

### 01 — Deterministic city

Continue from the Unit 00 environment orientation: prepare L-417 explicitly, then explain the distinction between
detecting an anomaly and investigating its cause. Use Deck 11 if helpful.
In Director select **1 Deterministic**, accept the listed preparation, and open Command Center.
Show reported On, schedule Off, override Yes. Click **Restore Scheduled Mode**. Follow the
Command Center gateway into the Energy Hub and simulator. Return to the UI: reported Off,
override cleared, correlated successful command, no incident magically opened. Exercise: reapply
Lights On and identify the owner of each displayed fact. This baseline needs no model call.

### 02 — Agent and tool

Start with the unresolved L-417 anomaly: can an agent establish the current state? Use Deck 15
and 17 to explain the loop. Prepare **2 Investigation Agent**. Ask whether L-417 is on. Show the actual
tool event and answer. Attach VS Code to OperationsAgent.Api and arm **Agent creation** or
**Function tool**. Repeat the question, step through the named region and inspect the model,
instructions and focused tool registration. Resume and show the completed answer. Nothing here
authorizes repair. Exercise: ask for a cause not present in the returned operational state.

### 03 — Session

Start with the ambiguous follow-up “Why?” about L-417. Use Deck 20 when explaining context.
Prepare **3 Session**. Ask, then use the same-session Why action. Inspect the session
identity and serialize/deserialize path. Demonstrate that context continuity can resolve a reference
without obtaining new evidence. Start a new conversation and compare. Complete both turns and
detach. Exercise: distinguish remembered state from a fresh Hub read.

### 04 — Knowledge

Start with the missing reason for L-417's override. Use Deck 22 to distinguish evidence from context.
Prepare **4 Knowledge** with evidence enabled. Ask and follow up. Show the retrieved
work order and technician note and their answer citations. Step through `TextSearchProvider`
registration and the narrow search result. Disable seeded evidence, start a fresh investigation,
and show that an unsupported explanation must be withheld. Restore the evidence toggle afterward.

### 05 — Case memory

Start with the question of what a closed L-417 case can justify in another investigation. Use Deck 24.
Complete and close the evidence-backed case before selecting **5 Memory**. Investigate
the documented L-528 fixture. Inspect the memory provider, its retrieved case and hypothesis framing.
Verify current facts separately. Finish the turn and explain why memory cannot become a command
authorization or a substitute for evidence. Exercise: remove the prior case and compare.

### 06 — Skills

Start with the need for a repeatable L-417 investigation procedure. Use Deck 27 where useful.
Prepare **6 Skills**. Show the skill catalog and the actual triage SKILL.md. Run the
investigation, observe the load call and mandated brief format. Inspect provider construction and
the loaded procedure. An optional markdown edit belongs in a disposable copy and must be restored.
Finish with a completed brief and clear source attribution, not a claim of another agent.

### 07 — MCP

Start with the same L-417 question across a service boundary. Use Deck 28 and 30 to locate it.
Prepare **7 MCP Tools**, starting LOCAL. Run the question, flip to MCP and
repeat. Trace discovery on the client and the server's focused Energy Hub tool. Show correlation
across HTTP and explain that remote reach does not grant extra authority. Finish with equivalent
authoritative state. Exercise: make the MCP endpoint unavailable and read the actual failure.

### 08 — Interactive input

Start with the need to obtain operator input before clearing the override. Use Deck 32.
Prepare **8 Interactive Input** with MCP selected. Request restoration and stop at
input-required. In code inspect the question, continuation and reviewed state. Deny, verify the
lamp did not change, then run again and explicitly approve. Finish on the confirmed receipt.
Explain that the tool is asking for input; this differs from a workflow gate and a policy wrapper.
The pending input is in memory in this lab; restarting a worker is not a supported continuation.
Exercise: deny and verify no succeeded restore receipt; start a new request to recover.

### 09 — Workflow

Start with the need to enforce a complete repair sequence. Use Deck 34 to explain the planned gate.
Prepare **9 Workflow**, show its rendered graph and equivalent displayed YAML.
Run, deny the review, and verify no repair. Reapply the fixture, run and approve. Use Visual Studio
2026 for this unit's code walk through validation, revision-bound decision, execute and verify.
Resume every breakpoint before checking the outcome. Optional negative case: Security Operation
requires lighting and policy prevents turning it off. Do not call this in-process graph durable.
Exercise: change the fixture while a review is pending and inspect the stale-state refusal. Reset,
re-read and start a new workflow; never reuse approval against different state. Do not imply that
these in-memory approvals survive process restart.

### 10 — Tool approval

Start with a repair that failed because L-417's controller is faulted. Use Deck 36 to locate policy.
Prepare **10 Tool Approval** with Controller Fault. Let the model select the protected
maintenance tool and show the framework's interception. Deny with no work item, then approve a
new attempt and show the actual work item. Apply Existing Incident and show reuse without another
item. Inspect `ApprovalRequiredAIFunction` and the domain enforcement that remains beneath it.

### 11 — Multi-agent boundary

Start with evidence that another domain may need L-417 on. Use Deck 38 and 42 to explain the boundary.
Prepare **11 Multi-Agent**, Security Operation, consult OFF. Ask why the lamp
is on. Enable consult and repeat. Show Operations asking Security through a remote capability,
then inspect the sanitized response and the domain API boundary. Finish with lighting still on
and an explanation. Do not demonstrate handoff or group chat as implemented features.

### 12 — A2A

Start with the missing workforce explanation for L-417. Use Deck 44 to explain delegation.
Prepare **12 A2A Delegation**. Discover the workforce card and delegate the work-order
question. Inspect the shareable projection at its source. Ask for labor cost and show that this
field was never included in accessible records. Complete the peer task and the caller's answer.
Contrast task delegation with Security agent-as-tool without claiming every A2A pattern is used.

### 13 — Hosting and cloud deployment

Start with the requirement to operate the same investigation in a managed habitat. Use Deck 45–49.
Explain three separate setup scopes: local services, access to model
inference, and deployed hosted agents. Use `docs/deployment.md`: prerequisites, CI identity,
reviewed infrastructure plan, platform apply, Energy Hub deployment, then hosted-agent deployment.
Show account/tenant values only as placeholders. Use a prepared environment for the live unit;
do not spend viewer time waiting for provisioning or silently provision billable resources.
Demonstrate LOCAL vs FOUNDRY HOSTED, source-of-record Work IQ evidence and required user consent.
Show the actual cloud Hub boundary. Close with Agent 365/identity as a separately configured
tenant experience. Portal actions, unavailable features and historical screenshots must be labeled.

### 14 — Evaluation

Start with a concrete L-417 behavior that could regress. Read the requirement, then use Deck 52–55.
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
- Use the compiled course checkout for executable API details; teach the deck's architecture in
  present tense without historical date comparisons.
- Keep tenant IDs, tokens, private work orders, notifications and unrelated desktop content out.
- Crop to the relevant app. Include readable code at 1080p, with a tighter crop when needed.
- Every chapter has narration and subtitles. Explain the outcome before changing screens.
- Record real application and debugger motion: attach, hit breakpoint, single-step, inspect Watch and AI results, continue and detach. Cut idle waits transparently; label freeze holds. Slides explain rationale; screenshots cannot substitute for the requested motion demonstrations.
- Capture both debugger frontends. Attach one debugger per process; F10 steps over, F11 steps
  into, Shift+F11 steps out, F5 resumes. Debug local C# execution, not a model's internal reasoning.
- Mark future units as planned until their own execution, code inspection and outcome are recorded.
- Distinguish real local execution, simulated physical devices, prerecorded captures, and optional
  cloud results on screen. Do not replace a failed run with invented successful output.
- Unit 00 teaches actual attachment, stepping, Watch and detach in both IDEs before later labs. Keep installation notes brief; later lessons revisit breakpoints to explain the architecture.
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


## End-of-lab investigation questions

Every lab ends with a code investigation, an official-documentation check, or a challenged AI-chat hypothesis. Require a file location/source link and a reproducible observation. These are learner exercises, not claims of completed future recordings.

| Unit | Investigation question | Evidence to return |
|---|---|---|
| 00 | Trace EnergyTools to the Hub; explain the two timeout policies; challenge an architecture-skill recommendation. | Source paths, a debugger observation and a verified source link. |
| 01 | Which assignment changes reported state, and what confirms it? | Successful command branch and correlated receipt. |
| 02 | Where is the tool admitted, and why can this stage not repair the lamp? | Capability registration and exposed method contracts. |
| 03 | What is serialized, where is it stored, and what survives a process restart? | Store implementation; distinguish supported behavior from assumptions. |
| 04 | Which retrieved fields justify the cause, and which do not? | Work-order citation and a no-evidence comparison. |
| 05 | How does memory frame a prior case as a hypothesis? | Provider instructions and independent current evidence. |
| 06 | Can a skill instruction enforce permission by itself? | Procedure plus actual enforcement code. |
| 07 | How does the correlation cross the MCP boundary? | Client/server paths and matching trace. |
| 08 | Who requests human input and owns its continuation? | MCP request/response and restart limitation. |
| 09 | Where does a stale revision invalidate approval? | Gate/execute source and denied outcome. |
| 10 | Which layer intercepts the selected tool; which domain check still applies? | Framework wrapper and deterministic service guard. |
| 11 | What information stays inside Security? | Allowed projection and negative query result. |
| 12 | Why can inaccessible cost not be recovered by a clever prompt? | Source projection and peer contract. |
| 13 | Compare session state, memory and durable execution across hosting choices. | Current official docs and configured runtime evidence. |
| 14 | What does ASSERT measure, and what would an infrastructure failure look like? | Case, evaluator scope and actual report. |
| 15 | What survives worker restart, and what if the scheduler or Hub disappears? | Orchestration/activity boundaries, retry policy and runbook limits. |
| 16 | Does a routine grant the agent any new command authority? | Invocation identity, tools and domain authorization path. |
| 17 | When is an Insight a hypothesis, and how does it become a regression test? | Linked finding evidence and a reviewed bounded case. |

## Current production baseline

Lab 01 application baseline is `843e0d0`: GitHub main, Windows and Mac are synchronized on
.NET 11 and Aspire 17. Course edits use `video-course/lab01-cross-platform`. Alternate Mac and
Windows across teaching beats without repeating the same demo; VS Code is the primary editor
on both, with occasional Visual Studio 2026 on Windows. Explicitly establish the fixture after
switching machines because their local operational state is independent. The paragraph below
records Unit 00's historical production baseline, not the current state of GitHub main.

Unit 00 uses the local `main` application merge `9d2f766cc19050495c52a601075df15870c4c0af`; expanded captures were made at `90f14e6` after course-only changes. The .NET 11 upgrade and durable UI are merged. All media remains local; remote main has not been updated by this production task. The companion lab includes a release gate before distributing clone instructions. Use the new Alon-branded deck, not conference-branded frames. Older pilots and the preceding branded cut remain archived review material.
