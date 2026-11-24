# Caesarea upgrade plan: .NET 11, Foundry, demo, slides, and book

Prepared: 2026-11-17. Status: authorized implementation in progress. Sections 10–12 record completed
work and review fixes; unchecked items below are remaining work, not a request to reapprove local updates.

**Agreed scope — November 17:** keep the Durable Functions live demonstration local for this
update. The v2 dashboard restart rehearsal is now recorded (November 18; see section 14). Explain Azure Function
App operations, managed Durable Task Scheduler, identity, networking, scaling and monitoring in
the presentations, then the book. Label the cloud architecture as proposed, not deployed or
verified. Full Azure Functions deployment is deferred to a separate future task; the cloud gates
below are requirements for that future scope, not blockers for completing the local demonstration.
This decision concerns the Functions recovery beat; Routines/Insights still require their own
real service evidence before being presented as live features.

**Ignite follow-up — November 18:** see [the announcement/gap inventory](docs/product-status/ignite-2026-gap-review.md)
and section 13. It updates service maturity and the material backlog without expanding the agreed
live demo to AKS or cloud Functions.

## 1. Verified baseline and corrections to the initial review

This section records the pre-upgrade assessment. Sections 10–12 supersede its package counts,
correlation finding, telemetry gap and local recovery status; the current solution has 28 projects
(30 including the two Windows helpers).

- Repository: https://github.com/alonf/CaesariaAgenticArchitectureDemo
- Fast-forwarded local `main` from `dfe9ddc` to `7ad5369cbfb066d3fb30729b2a6e9c5460e6d54c` with `git pull --ff-only` (24 commits). The working tree was clean before the pull.
- Created local branch `upgrade/net11-foundry-insights-routines` from that synchronized revision. This is a temporary integration branch; all lecture stages remain cumulative on the maintained main branch.
- GitHub reported no open PRs. The latest CI run on this revision succeeded on September 16: [run 35128222033](https://github.com/alonf/CaesariaAgenticArchitectureDemo/actions/runs/35128222033). This historical success does not override today's dependency audit failures.
- CI **already exists** in `.github/workflows/ci.yml`: Linux/Windows/macOS builds, tests, formatting, Node tests, Windows helper checks, and Bicep compilation. Extend it rather than create a duplicate.
- Behavioral evaluation **already exists**: `evaluation/assert_demo`, nine fixtures, baseline/governed comparison, adapter tests, HTML reports, and `docs/prompts/14-assert-evaluation.md`. The root `eval.yaml`, `datasets/`, and `evaluators/` define a separate hosted-agent suite. Extend these assets rather than replace ASSERT.
- At assessment there were **25 projects in the main solution**, plus **two Windows helper projects** outside it: 27 project files to review for .NET 11, including `DebuggerBystander` and the Visual Studio attach helper.
- The refreshed Operations Agent composes capabilities through `Services/OperationsAgent.Api/Services/Capabilities/`. Integrate at these existing seams rather than recreate the earlier monolithic composition.
- Hosted deployment already declares Responses container protocol **2.0.0**. Do not describe the upgrade as a required switch from v1.
- The hosted composition deliberately omits local remediation, approvals, Security consultation, and A2A delegation. A routine invoking it does not magically gain those capabilities.
- Local sessions and remediation runs remain in memory. Hosted resilience is not equivalent to durable business workflow state.
- The hosted Operations Agent still passes the constant `"hosted"` as the Energy tool correlation ID. This needs request-specific propagation.
- Repository-wide search found service OTel configuration but no explicit `UseOpenTelemetry` on the local agent. Verify hosted SDK instrumentation before adding it, to avoid duplicate spans.

### Reviewed published material

Downloaded all three release PDFs, extracted their text, inspected selected rendered pages, and verified SHA-256 against the repository. This is a content/upgrade review, not a full visual audit of every page.

| Artifact | Release / length | SHA-256 |
| --- | --- | --- |
| H08 deck | `vslive-san-diego-2026`, 59 PDF pages | `1adb7953e203f2005cc8a3cde45bc32bc35990242f74cfd66f696f509fe1989b` |
| W20 deck | Same release, 51 PDF pages | `9927edfc42688018b5defeeb8dc95eb70ff03c8fcdf3300e3d502242d31c12a7` |
| Companion book | `companion-book-v1.0`, 205 pages | `f00b0eeb9dfc4d77aa69dcf289827e65d82083493d3fb5720f3bbf903a0af94e` |

Release links: [slides](https://github.com/alonf/CaesariaAgenticArchitectureDemo/releases/tag/vslive-san-diego-2026), [book](https://github.com/alonf/CaesariaAgenticArchitectureDemo/releases/tag/companion-book-v1.0).

The releases contain PDFs, not editable decks/manuscript. Their source locations are an open input requested from the author. PDF page numbers below identify animation-state exports; they are **not verified PowerPoint slide numbers**. Reconcile speaker-note anchors against the editable sources before renumbering runbooks.

## 2. Architecture decisions

New requirements: investigate a known lighting discrepancy without waiting for an operator; use recurring observed agent failures to improve the implementation and its regression suite.

| Responsibility | Mechanism / owner | Authority |
| --- | --- | --- |
| Detect known state discrepancy | Deterministic Energy Hub logic | Owns current operational state; model does not duplicate anomaly rules |
| Start an investigation | Foundry Routine or local deterministic trigger adapter | May invoke the approved agent; grants no extra tools or write rights |
| Choose evidence and explain discrepancy | Existing Operations Agent | Read, reason, recommend; uncertainty remains visible |
| Approve and execute correction | Existing controlled workflow and authoritative Hub | Existing validation, approval, state-revision checks, and execution identity |
| Find recurring agent behavior | Foundry Insights over representative traces | Advisory evidence for engineering; no operational authority |
| Decide and validate a change | Human review, code changes, ASSERT and hosted evaluations | No automatic promotion of generated recommendations |

Keep focused contracts, independent ServiceDefaults, correlation propagation, LoggerMessage logging, and stable runtime stages. No generic shared project, extra specialist agent, replacement workflow engine, or water-domain implementation is needed for these requirements.

Operational evidence comes from Hubs. Work IQ supplies supporting organizational evidence under the selected identity. Memories remain hypotheses. Insights describe agent behavior; they do not establish why a real lamp is on.

Rejected approaches: a new polling agent to duplicate a deterministic state check; one new agent for each feature; giving a scheduled agent direct device-write access; treating Insights recommendations as executable policy; replacing the existing C# workflow with a portal workflow.

## 3. Work packages and sequencing

### A. Repair the baseline and update dependency inventory

- [x] Update `Aspire.AppHost.Sdk` 13.5.3 to 13.6.5. The advisory's documented scenario is macOS-specific, which is also one of this repository's CI platforms.
- [x] Verify the resulting MessagePack dependency is patched: resolved 2.5.307 through the parent SDK; no override required.
- [x] Keep audit warnings enabled; audit the resolved solution, helpers and generated Functions host graph. The implementation records distinguish vulnerability, deprecation and available-update findings.
- [x] Resolve the post-pull Razor compilation failure: renaming escaped `@switch` to `selectedSwitch` restored compilation. The repaired .NET 10 baseline passed 415 tests before migration.
- [x] Create the isolated `.venv-assert` environment with pinned requirements; all 16 adapter tests passed. The ASSERT upstream commit is unchanged.
- [x] Add weekly Dependabot proposals for NuGet, GitHub Actions and the Python integration; add the offline ASSERT adapter tests to CI.
- [ ] Refresh Python ASSERT, Node extension, vendored Mermaid, GitHub Actions, Bicep API versions, and CLI inventory alongside NuGet. ASSERT is deliberately commit-pinned: compare upstream changes and run adapter tests before changing it.

Candidate package versions observed on 2026-11-17; recheck before implementation. Availability does not establish that these versions form a compatible package set.

| Package group | Current | Candidate |
| --- | --- | --- |
| Aspire AppHost SDK | 13.5.3 | 13.6.5 |
| ASP.NET Core OpenAPI / MVC Testing | 10.0.11 | 11.0.0 for .NET 11; OpenAPI 10.x patch observed: 10.0.15 |
| Extensions AI / HTTP resilience / service discovery | 10.9.0 | 11.0.0 |
| Agent Framework Workflows | 1.19.0 | 1.28.0 |
| Agent Framework Foundry / A2A / A2A hosting | 1.19.0-preview.260822.1 | 1.28.0-preview.261106.1 |
| Agent Framework Foundry Hosting | 1.20.0-preview.260831.1 | 1.28.0-preview.261106.1 |
| Azure.AI.Projects | 2.1.0-beta.4 | 3.0.0-beta.6, subject to coherent transitive dependencies |
| Azure.Identity | 1.21.0 | Already current |
| MCP client / ASP.NET Core server | 2.2.0 | 2.4.0 |
| OpenTelemetry exporters / hosting / instrumentation | 1.18.0 | 1.21.0 |
| xunit.v3 | 4.0.0 | 4.0.3 |
| SonarAnalyzer.CSharp | 10.33.0.1635 | 10.36.0.15337 |
| Markdig | 1.3.2 | 1.6.0 |
| Vendored Mermaid | 11.12.0 | 12.3.0; separate major-version rendering check |
| Microsoft.VisualStudio.Interop | 17.14.40260 | Current NuGet page still lists 17.14.40260; do not invent an 18.x requirement |

### B. Upgrade .NET and developer tooling

- [x] Install and verify SDK 11.0.100 side by side with .NET 10; retain .NET 10 for rollback and the existing baseline. Installed per-user; see execution record.
- [x] Pin SDK 11.0.100, retaining Microsoft.Testing.Platform and `latestFeature` roll-forward.
- [x] Update the original 25 solution TFMs to `net11.0` and both Windows helper TFMs to `net11.0-windows`. Insights, Durable contracts and Functions bring the current total to 30.
- [x] Update CI SDK setup and test invocations to `dotnet test --project ...`, including the Windows helper and Linux validation script.
- [x] Update all five Dockerfiles to published `11.0.100-resolute` / `11.0-resolute` tags; fix stale SDK comments. All five images built successfully.
- [x] Update hardcoded TFM paths in scripts, launch/debugger configuration and documentation.
- [ ] Exercise interactive attach, hold, release and detach with VS Code and Visual Studio on presenter machines.
- [x] Resolve analyzer findings with warnings-as-errors retained; builds have zero warnings/errors.
- [x] Build all five Linux amd64 images and verify readiness/startup.
- [ ] Verify .NET 11 CPU/OS requirements on every intended presenter machine and hosting target.
- [ ] Rehearse Blazor/SignalR reconnect, markdown sanitization, Mermaid workflow diagrams, and stage switching.

### C. Upgrade Foundry and agent dependencies coherently

- [x] Upgrade Projects, Foundry, Hosting, A2A, Workflows, Extensions.AI and related dependencies coherently. The solution compiles and deterministic endpoint tests pass; cloud compatibility remains a separate gate below.
- [ ] Check `AsAIAgent`, context providers, Skills provider, session serialization, workflow approvals, MCP elicitation, and A2A run-mode/task-state changes against compiled examples.
- [ ] Verify host registration, readiness, streaming, cancellation, and Responses protocol 2.0 compatibility. Keep container protocol declarations distinct from endpoint Activity support and REST API versions.
- [ ] Reproduce multi-step tool calls before changing either domain's `ReasoningReplaySanitizingChatClient`; remove the workaround only with evidence that encrypted reasoning replay now works on the selected deployment. Retain historical diagnosis with tested version/date.
- [ ] Retest Work IQ consent, two-user isolation, call-id forwarding, session continuation, and Teams/Copilot behavior. A recent change on upstream `main` is not evidence that the selected NuGet version contains it.
- [x] Replace constant hosted Energy correlation with invocation-time incoming correlation/trace/request identity, propagate it to the Hub, and tag the current activity. A concurrent two-request test passes. The SDK continues to own first-party Foundry call-id forwarding.
- [ ] Update azd and compatible Foundry extensions as a deliberate toolchain task. Existing azd 1.31.2 forced the skill prerequisite check to select an older evaluations extension. Preserve the current Bicep/REST deployment pipeline unless a specific CLI capability warrants an addition.
- [ ] Evaluate current model candidates against the same frozen cases, latency and cost budgets. Do not change the default model merely because a newer model exists.

### D. Routines: an unattended investigation beat

**Teaching requirement:** an operator should not have to click Ask Agent to obtain a bounded investigation of an already known lighting discrepancy.

**First increment:** a disabled-by-default timer/schedule routine targeting the existing hosted Operations Agent, with stored input identifying L-417 and requiring a fresh Hub read. Use a manual dispatch for rehearsal and a one-shot timer for the actual unattended demonstration. Read the real run record and output; show identity and timestamps. End at a recommendation and an unchanged lamp.

The existing hosted agent has read-only Energy access and optional delegated Work IQ. For the first unattended beat, use agent identity and a supported composition that does not require user delegation. Reuse the existing configuration switch for optional Work IQ. An evidence-limited answer is acceptable and must state its limits. Add creator-identity/Work IQ only after a specific integration test; creator identity is not an arbitrary event sender's identity.

- [x] Add the source-controlled routine manifest and parameterized disabled-create/dispatch/list/disable tooling. Live provisioning/rehearsal remains open below.
- [x] Add `docs/prompts/15-routines.md` and an optional Hosting walkthrough beat in the existing catalog. No new stage enum or grant is needed.
- [ ] Keep the Foundry run history authoritative for routine execution. The Command Center may project its run ID, correlation, status, trigger, principal and recommendation through a narrow existing API boundary.
- [ ] Provide a local trigger adapter invoking the same investigation application path, explicitly labeled local simulation. Never fabricate a Foundry run ID or mark a local result as hosted.
- [ ] Gate admission deterministically: current asset state, existing investigation/workflow, bounded concurrency, duplicate dispatch and stale event handling. Persist an idempotency key before consequential follow-on work.
- [ ] Test duplicate/replayed triggers, overlap, identity failure, disabled routine, cancellation, missing evidence and no device-write path.
- [x] Document UTC schedule and identity/service prerequisites in the Routines runbook.
- [ ] Confirm region, endpoint authentication, minimum schedule interval and service restrictions during a real routine rehearsal; azd authentication remains unresolved.

**Later increment:** consume the Hub's actual anomaly event and reuse the same investigation. Current documented routine integrations include GitHub/Teams; do not assume a generic Energy Hub or Service Bus trigger exists. Validate a supported bridge, or use a deterministic event consumer to call the agent directly. If this introduces the deferred event-driven stage, update the original stage requirement (including its Service Bus expectation) explicitly. Do not call scheduled polling an event-driven implementation.

The hosted agent does not currently expose corrective workflows. Adding an unattended recommendation-to-workflow transition requires a separate authenticated request boundary and tests; retain the existing workflow owner, approval, and execution identity.

### E. Insights: evidence becomes an engineering change

**Teaching requirement:** repeated real agent behavior should reveal useful regression cases and proposed improvements beyond hand-written scenarios.

Keep Insights outside the operational control path. Use a prepared set of representative synthetic demo runs and an actual monitor; do not expect a useful finding to appear during a short live talk.

- [ ] Complete agent/tool/model tracing with matched ActivitySource subscriptions and agent/version metadata. Avoid double instrumentation; keep sensitive content off by default and use synthetic evidence for demonstrations.
- [ ] Configure a Foundry Insights monitor in the demo project after verifying model support, App Insights connection and required reader/model permissions. Record monitor configuration, lookback window, agent version and collection time without committing tenant data.
- [x] Add `docs/prompts/16-insights.md`, the read-only utility and Evaluation walkthrough: finding -> trace -> reviewed hypothesis -> regression -> measured change.
- [x] Implement SDK pagination, expanded details and explicit absent/incomplete/error outcomes. A real finding capture remains open.
- [ ] Use the existing frozen ASSERT cases and hosted evaluation assets. Add a case only after reviewing the finding; keep held-out cases and comparable versions for improvement claims.
- [x] Keep Insights retrieval read-only: no automatic code/policy changes, incidents or deployment.
- [ ] Test pagination, empty/partial/error states, evidence links, version attribution and sensitive-data handling in any new adapter.
- [ ] Prepare a genuine scrubbed capture labeled `CAPTURED / NOT LIVE`; keep private rehearsal captures in the already ignored runbook artifacts directory. If no finding exists, show that honestly.

An Insights monitor's own scheduled analysis is distinct from a Routine that invokes the Operations Agent. Neither replaces deterministic operational alerting or ASSERT.

### F. Durable Functions hosted MAF agent: continue the conversation and the work

**Research first:** reviewed the Durable Extension documentation, the Functions single-agent registration sample, the long-running-tools sample (`Program.cs`, `Tools.cs`, `FunctionTriggers.cs`), the graph workflow HITL sample, and `DurableAIAgent.cs`. Microsoft announced GA on November 9, 2026. Source on `main` may lead published packages; the implementation spike must verify these APIs against the selected release.

The specific hosting model has two separately identifiable lifetimes:

- **Agent session:** register an actual MAF `AIAgent` with `ConfigureDurableAgents` / `AddAIAgent` or `AddAIAgentFactory`. Functions generates an agent HTTP endpoint. The client retains the opaque `x-ms-session-id` and supplies `sessionId` on subsequent turns. Durable entity infrastructure persists agent session state across worker restarts.
- **Background work:** an agent tool can use `DurableAgentContext.Current.ScheduleNewOrchestration`, return an instance ID, and finish the conversational turn. Later tools query `GetOrchestrationStatusAsync` or deliver an event with `RaiseOrchestrationEventAsync`. A durable orchestration can invoke another registered agent through `context.GetAgent`, maintain its session, and wait for external input with a timeout.

This is an additional Functions hosting composition. It is separate from the current Foundry hosted container and does not automatically inherit its Work IQ delegation, Teams protocol, or Foundry Routines integration.

**Recommended demo: L-417 remediation across a shift interruption.** Extend the existing Forgotten Override story with a named Durable Remediation beat after the workflow/hosting explanation:

1. Ask the Functions-hosted Operations Agent to investigate L-417 and request remediation. Its narrow tool starts the controlled process and returns a real instance ID; the chat turn ends promptly.
2. Poll until `AwaitingApproval` with a valid review before restarting. Show the pending process and agent session in the Durable Task Scheduler dashboard; scheduling alone is insufficient.
3. Stop and restart only the Functions worker. Keep the durable backend and deterministic city services alive.
4. Continue the same conversation: "What happened to the repair you started?" Do not paste the workflow ID into the prompt. The agent uses its persisted context to query actual workflow status. This demonstrates agent hosting and session durability as well as process recovery.
5. In the implemented local beat, approve through the separate signed-key supervisor endpoint; Command Center/Entra integration remains open. Resume, revalidate and execute through the Hub, then poll to a terminal business outcome. Query it through the same agent session. Unknown execution must remain unknown.
6. Alternate rehearsal: introduce Security Operation while approval is pending. The resumed process must refuse stale authorization and preserve the effective lighting target.

**Adaptation work and gates:**

- [x] Add a focused Functions host with read/investigation and start/status tools; reuse domain logic without bypassing Hubs or introducing generic shared contracts. Preserve the local Stage 8 graph for the earlier teaching beat. The local implementation is in `Services/OperationsAgent.Functions`; see execution record 11 below.
- [x] Inspect the released graph/RequestPort API and select the documented explicit Durable Task orchestrator sharing deterministic policy/gateways. The in-memory Stage 8 graph remains separate; adapting it is not claimed complete.
- [x] Keep local approval authorization in deterministic code, binding reviewer, operation/action, asset, revision and expiry. Test mismatch, expiry and duplicate handling; no agent approval tool.
- [x] Add durable process identity and fixed-revision retries. Test interrupted activity both after success and while the original Hub command is pending. Reconcile receipts without resending; unknown is nullable, not false.
- [ ] Persist Hub receipts and test broader Hub/storage failure recovery. No general exactly-once claim.
- [x] Replace process-local cancellation/waits with durable events/timers and explicit stage-downgrade behavior. The inspected `DurableAIAgent` implementation rejects cancellable tokens. Keep the concrete durable proxy type inside orchestrators to preserve their synchronization context; external I/O belongs in activities or supported agent calls. The implemented orchestration calls activities; its HTTP boundary uses the agent proxy outside orchestration replay.
- [x] Configure local session/result lifetimes, separate operator/supervisor keys and correlation. Document history-retention limits and opaque session IDs.
- [ ] Enforce cloud retention, session ownership and separate executor identity; integrate Command Center/Entra authorization.
- [x] Validate the .NET 11 Functions package graph and local DTS emulator composition.
- [ ] Select/provision a compatible cloud Functions plan and protected DTS endpoint.
- [x] Keep streaming separate: this slice uses completed-turn JSON. It makes no reliable-streaming claim and does not reuse the current chat streaming adapter.
- [x] Record v1 local conversation/review restart and timeout observations; test admission/approval, stale revision, stage withdrawal and interrupted command execution. Preserve the evidence provenance and distinguish scheduler-double tests from live DTS.
- [x] Record a fresh v2 worker-restart rehearsal with explicit pending/terminal checkpoints and a process-restart transcript. See docs/evidence/durable-recovery-director-2026-11-18. Existing v1 history remains isolated.

The assessment originally estimated a 0.5–1 day spike and 2–4 further implementation days.
The local host and review fixes are now implemented (sections 11–12); cloud resources remain uncreated.

**Material placement:** H08 workflow/human-control pages 34-37 introduce the missing requirement; hosting pages 45-49 show the Functions composition and restart demonstration. W20 can replace part of the hosting explanation with a short prepared recovery beat; keep the full failure exercise in the lab. Book Chapter 10 explains durable execution versus local graph checkpoint storage, Chapter 12 covers the hosted agent/session and background-work lifetimes, Chapter 14 adds the recovery lab, and Appendix A updates the extension's dated GA/package status.

## 4. Presentation update map

Use stable concept anchors in speaker notes, with PDF pages below only as edition-specific locators. Preserve each deck's visual language and animation intent; export and visually verify every changed slide.

| Material / current PDF locator | Update |
| --- | --- |
| H08 pp. 15-27: agent, harness, session, knowledge, memory, skills | Re-export code from compiled regions after SDK changes. Distinguish the MAF harness from the newer managed Copilot harness; label optional/unimplemented features. |
| H08 pp. 28-44: MCP, workflow, human control, multi-agent, A2A | Verify current API names/signatures and approval semantics. Preserve domain/data boundaries. |
| H08 pp. 45-49: Hosting, protocols, isolation | Update actual host registration and runtime behavior. Insert one Routines concept/code slide and a short unattended read-only run immediately after Hosting. Use proposed anchor `ROUTINE_INVOCATION`. |
| H08 pp. 52-55: assurance, ASSERT, Agent DevOps | Preserve ASSERT; add an Insights evidence walkthrough at Agent DevOps, proposed anchor `AGENT_INSIGHTS`. Audit illustrative evaluation APIs against actual packages; label illustrative code clearly. |
| H08 p. 57: wider catalogue | Add Routines and Insights with separate service/SDK maturity labels; keep preview caveats dated. |
| W20 pp. 23-34: 2 AM, deterministic spine, workflow | Add the trigger/identity boundary: time or event starts an investigation, not permission to act. Demonstrate L-417; keep the water scenario explicitly conceptual/deferred. |
| W20 pp. 36-38: evidence and the two loops | Use one real Insight to explain how runtime evidence becomes a reviewed ASSERT case. Keep the model-generated diagnosis distinct from verified cause. |
| W20 pp. 40-46: runtime map, identity, governance | Place Routines in activation/hosting and Insights in observation/improvement, outside authorization and physical execution. Update lifecycle/runtime maturity statements. |
| Both decks and runbooks | Reconcile PowerPoint slides, PDF animation pages and code anchors; refresh links, screenshots, dates, supported toolchain and live-versus-captured labels. |

Keep the core 30-minute W20 story. Use approximately 2 minutes for a prepared routine run and 2 minutes for an Insight during the existing hosted/assurance walkthroughs, replacing generic explanation rather than simply extending the talk. Keep cold setup, schedule waiting, monitor generation and full evaluations in rehearsal.

Concrete drift: the H08 runbook currently refers to Hosting as slides 43-44 and Evaluation as 49, while the published animation PDF locates Hosting at pages 45-46 and assurance/ASSERT at 52-53. This requires source-deck reconciliation, not a blind global +2 adjustment.

## 5. Book update map

Prepare a new edition, provisionally 1.1. Preserve edition 1.0 and its download/hash. Exact new pagination follows layout, not these planning locators.

| Existing chapter / edition 1.0 pages | Planned revision |
| --- | --- |
| Chapter 5, especially p. 59 | Extend production-feedback discussion with Insights as advisory analysis feeding reviewed evaluations. Update dated preview/GA statements individually. |
| Chapter 7, starts p. 78 | Update framework baseline, construction examples and harness distinction. |
| Chapters 8-11, including Chapter 10 starting p. 116 | Compile every affected listing against the chosen packages; refresh Skills/MCP/A2A APIs, session serialization and approvals. Explain durable recovery/idempotency separately from framework workflow graphs. |
| Chapter 12, pp. 146-162 | Add a section on unattended activation with Routines: timer versus event, identity, run history, retries and evidence limits. Recheck hosting/Work IQ/Teams findings and reasoning workaround; retain historical lessons as dated observations. |
| Chapter 13, pp. 163-183, especially pp. 178-179 | Add Insights to Agent DevOps after ASSERT. Trace -> generated finding -> reviewed hypothesis -> frozen regression -> measured change. Keep evaluation, observability and authorization distinct. Update tracing examples and prevent automatic sensitive-data capture. |
| Chapter 14, pp. 184-198 | Update .NET 11 setup, 27-project/tool inventory, CI commands, debugger support and test results. Add Routines and Insights labs, expected errors, identity checks and captured fallbacks. Fold Exercise 8's instrumentation task into the baseline if implemented. |
| Appendix A, pp. 199-201 | Replace version/status inventory with the resolved, tested dependency matrix. Remove the timeless claim that hosting "must stay on 1.20"; preserve the 1.19 port defect as history. Routines service is GA. Insights service GA was announced November 17; selected CLI/SDK beta/preview status is separate. Reverify all other maturity/model/protocol/pricing assertions before republishing. |
| Glossary/checklist and front matter | Add Routine, dispatch identity, Insight, monitor, idempotency and recovery terms. Update edition date, supported commit and provenance. |

No source manuscript or editable PPTX was found in the repository or its releases. Obtain originals before editing; do not reconstruct the whole book or animated decks from PDFs by default.

## 6. Verification and release gates

Run repository-required commands for every implementation slice:

```powershell
dotnet restore
dotnet format
dotnet build
dotnet test --project Tests/Caesarea.Deterministic.Tests/Caesarea.Deterministic.Tests.csproj
git diff --check
```

Also run CI-equivalent Windows helper build/tests, Node extension tests, Python ASSERT adapter tests, all Bicep compilation and dependency audits. For review-only work, use `dotnet format --verify-no-changes` to avoid unrelated edits. Exercise full CI across all three OSes before merging the framework upgrade.

Live gates: local stage regression; Linux container readiness; hosted Energy read; two-user Work IQ isolation; tool-call continuation; Teams/Copilot error recovery; timer dispatch with identity/run evidence; unchanged lamp after routine; duplicate trigger suppression; real Insights evidence; ASSERT baseline/governed and hosted suite results. Restart/interruption tests must prove no duplicate consequential operation before claiming durable recovery.

Record results as pass/fail/error/skipped with reasons, SDK/package/model versions and commit. Historical 340-test results belong to the pre-pull checkout, not the new baseline. Do not claim cloud or interactive debugger tests were run during this planning task.

Publication: update README, `docs/presentations.md`, `docs/companion-book.md`, stage prompts, runbooks, `docs/product-status/api-drift.md`, hosting findings and deployment docs together. Publish revised decks under new asset names/releases and the book under a new edition release. Preserve conference assets and QR destination. Record hashes/page counts and use a code release tag matching the new material. Verify downloads and links before announcing them.

Rollback: keep .NET 10 installed, retain prior image digests/agent versions and evaluation artifacts, and use small independently reviewable commits. Do not remove working compatibility shims until replacement evidence exists.

## 7. Estimates and open inputs

Planning estimates for one engineer, excluding external access delays:

| Slice | Estimate |
| --- | --- |
| Security/dependency baseline | 0.5-1 day |
| .NET 11, Docker, CI and debugger regression | 1-2 days |
| Coordinated Foundry upgrade and cloud regression | 2-4 days |
| Bounded Routines demo | 1-2 days; arbitrary Hub-event bridge is additional scope |
| Insights instrumentation, monitor and evidence demo | 1-2 days plus trace collection time |
| Deck/book revision, rendering and rehearsal | 2-4 days after editable sources arrive |

Open inputs: editable deck/manuscript locations; target demo project/region and permissions; representative scrubbed traces; chosen model/judge deployment; whether the first Routines release needs true Hub-event activation or only unattended timer activation. Default this plan to the timer increment and keep the later bridge explicit.

Optional later work: managed Copilot harness comparison, egress restrictions and broader production persistence. The focused Durable Functions demonstration is now assessed separately in work package F. Browser automation is unnecessary for the current focused Hub APIs.

## 8. Sources and status discipline

Checked 2026-11-17. Use dated service announcements for maturity and actual package manifests/changelogs for shipped API compatibility; documentation on upstream main can lead published packages.

- [.NET 11 release](https://github.com/dotnet/core/blob/main/release-notes/11.0/11.0.0/11.0.0.md) and [ASP.NET migration](https://learn.microsoft.com/en-us/aspnet/core/migration/100-to-110?view=aspnetcore-11.0).
- [.NET 11 compatibility](https://learn.microsoft.com/en-us/dotnet/core/compatibility/11).
- [Aspire advisory](https://github.com/advisories/GHSA-f7vh-h5g3-7jrh) and [MessagePack advisory](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h).
- [Foundry Hosting package](https://www.nuget.org/packages/Microsoft.Agents.AI.Foundry.Hosting) and [hosted context ADR](https://github.com/microsoft/agent-framework/blob/main/docs/decisions/0034-hosted-platform-context-agentserver-2.0.md).
- [Routines GA announcement, September 24](https://devblogs.microsoft.com/foundry/from-chatbots-to-automated-assistants-routines-in-microsoft-foundry-are-now-generally-available/) and [current usage/identity restrictions](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/use-routines). Service GA does not imply stable .NET or azd integration APIs.
- [Insights preview documentation](https://learn.microsoft.com/en-us/azure/foundry/observability/how-to/agent-insights) and [evaluation overview](https://learn.microsoft.com/en-us/azure/foundry/concepts/evaluation-overview).
- [October Foundry update](https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-october-2026/): managed Copilot harness preview and hosted resilience GA are separate capabilities.
- [Durable Extension documentation](https://learn.microsoft.com/en-us/agent-framework/hosting/durable-extension) and [GA announcement, November 9](https://techcommunity.microsoft.com/blog/AppsonAzureBlog/durable-task-extension-for-microsoft-agent-framework-is-now-generally-available/4571100).
- [Functions long-running agent tools sample](https://github.com/microsoft/agent-framework-durable-extension/tree/main/dotnet/samples/DurableAgents/AzureFunctions/06_LongRunningTools), [durable graph HITL sample](https://github.com/microsoft/agent-framework-durable-extension/tree/main/dotnet/samples/DurableWorkflows/AzureFunctions/03_WorkflowHITL), and [durable agent proxy implementation](https://github.com/microsoft/agent-framework-durable-extension/blob/main/dotnet/src/Microsoft.Agents.AI.DurableTask/DurableAIAgent.cs). These links track source main; pin a release/commit when implementing.

## 9. Preparation record (before implementation)

- [x] Sync GitHub main and inspect latest CI/release metadata.
- [x] Create update branch.
- [x] Review and hash-verify published decks/book; identify concrete update locations.
- [x] Create this plan, correcting pre-pull findings.
- [x] Complete .NET 11 installation and record verification.
- [x] Record post-pull local quality-check outcomes (failures are baseline findings, not successful gates).
- [x] Begin the authorized implementation; completed local batches are recorded in sections 10–12.
- [ ] Complete the remaining cloud/live gates above, revise editable presentation/book sources and publish new versions.

Historical post-pull checks on SDK 10.0.115, before the repairs (superseded by sections 10–12):

| Check | Result |
| --- | --- |
| `dotnet restore` | Failed: Aspire NU1903 and MessagePack NU1902 audit findings |
| `dotnet build` | Failed at restore on the same advisories |
| `dotnet test --project Tests/Caesarea.Deterministic.Tests/Caesarea.Deterministic.Tests.csproj` | Could not run tests: CommandCenter.Web Razor compilation errors |
| `dotnet format --verify-no-changes --no-restore` | Exit 0; workspace-loading warning observed, so this does not establish successful compilation |
| Node extension tests | 7 passed |
| `python -m unittest evaluation.assert_demo.test_adapter` | Could not run: missing `opentelemetry`; isolated requirements not installed |
| `git diff --check` | Passed |

No cloud evaluation, Insights scan, routine provisioning, release publishing, or interactive debugger operation was performed.

### SDK installation verification

The machine-wide WinGet installer waited for Windows UAC approval. That pending attempt was stopped and replaced with the official Windows x64 SDK ZIP from Microsoft's .NET 11 release metadata. Its SHA-512 matched the published value before extraction.

- Installed SDK: **11.0.100**.
- Location: `C:\Users\alon.HOME\AppData\Local\Microsoft\dotnet11`.
- Installed runtimes: Microsoft.NETCore.App, Microsoft.AspNetCore.App, Microsoft.WindowsDesktop.App **11.0.0**.
- Verified outside the repository: `dotnet.exe --version`, creation of a `net11.0` console project, restore/build/run, and `Hello, World!` output.
- Existing machine-wide SDKs and PATH were retained. At installation the repository still pinned 10.0.115; work package B subsequently changed `global.json` to **11.0.100**. Dot-source `scripts/Use-CaesareaSdk.ps1` for current repository work.

Invoke the installed SDK explicitly:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet11\dotnet.exe" --version
```

The SDK-selection script prepends that directory for the current shell. Machine-wide installation/IDE discovery can be completed later through the Microsoft installer when elevation is available; the verified per-user installation is already usable for CLI builds.

## 10. First implementation batch — November 17

Implemented on the existing update branch. Detailed changes and remaining gates are in
[`docs/product-status/net11-upgrade.md`](docs/product-status/net11-upgrade.md).

- [x] Repair the .NET 10 baseline, then migrate the project graph, Dockerfiles, CI and debugger paths to .NET 11.
- [x] Upgrade the coordinated package set and fix migration/analyzer issues with warnings-as-errors retained.
- [x] Add invocation-specific hosted Energy correlation, concurrent isolation coverage, and local MAF telemetry.
- [x] Add disabled routine creation/operation tooling and its stored L-417 prompt.
- [x] Add the preview Insights reader with monitor discovery, full SDK pagination, expanded evidence, active filtering and explicit incomplete/error outcomes.
- [x] Integrate optional presenter steps in existing Hosting/Evaluation walkthroughs and add prompts/runbooks. No new runtime stage grants authority implicitly.
- [x] Add weekly dependency proposals and CI coverage for the pinned ASSERT adapter.
- [x] Document [presentation recommendations first, then book recommendations](docs/presentation-and-book-update-recommendations.md), based on implemented behavior and explicit remaining prerequisites.

Verified: Windows and Linux each passed 416 deterministic tests with four live IDE tests skipped;
Windows helper 41, Python adapter 16, and Node extension seven passed. All five Linux images built
and passed their health/readiness startup checks. All nine Bicep templates compiled. The NuGet
audit found no vulnerable direct or transitive dependencies in the solution or Windows helper;
no deprecated direct solution packages were found. The Release build completed with zero warnings
and errors. Final restore, solution/helper format verification, Windows Release tests (416 passed,
four skipped), and tracked/new-file whitespace checks passed. Updated GitHub Actions jobs have
not yet been executed remotely.

The configured Foundry project and `caesarea-operations` agent were discovered from existing
Command Center configuration. The Insights monitor query succeeded with zero monitors. A read-only
routine query failed because the azd credential could not acquire a token; `azd auth login` is
required. No routine, monitor, analysis, deployment or published asset was created remotely.

**Open at the end of the first batch (see section 11 for subsequent F progress):** live hosted/Work IQ/Teams checks, monitor setup and evidence collection, actual routine
rehearsal, a local activation adapter with persistent admission, the Durable Functions implementation
and recovery tests (work package F), interactive debugger/UI rehearsal, the vendored Mermaid major
upgrade, and edits to the original presentation/book sources. This first batch does not complete
all of A-F. Use `. ./scripts/Use-CaesareaSdk.ps1` for the installed per-user SDK; startup now does
that automatically.

## 11. Durable Functions implementation slice — November 17

Implemented the [local recovery rehearsal](docs/runbooks/durable-recovery.md) on the same update
branch. The separate `OperationsAgent.Functions` host targets .NET 11 with `Azure.Functions.Sdk`
1.1.0, MAF Functions/DurableTask 1.16.0, Worker DurableTask 1.22.0 and AzureManaged 1.12.0.
Its new API boundary has `DurableOperations.Contracts`; the solution now contains 28 projects
(30 including the two Windows helper projects).

- [x] Host a real MAF agent with durable conversation state and read/start/status tools.
- [x] Persist the repair process, signed supervisor review, five-minute timer and stage epoch.
- [x] Keep approval and commands outside the agent toolset; require supervision for every write
  in this additional beat. Preserve the earlier Stage 8 graph and its original policy branches.
- [x] Revalidate stage and Hub revision; retain the same revision across activity retries and
  recover a successful Hub receipt after a lost response. This guarantee assumes the Hub remains
  alive; durable Hub receipts for wider failure scope are still needed.
- [x] Verify a real model-backed conversation survives Functions host restart and forced worker
  termination, recalls the operation ID without it in the follow-up prompt, and reads actual status.
- [x] Verify supervisor approval completes the repair, duplicate completed approval returns 409,
  an operator key cannot approve, and Security Operation invalidates stale authorization without
  switching off required lighting.
- [x] Add deterministic tests for signature tampering/expiry, stage replacement/withdrawal,
  stale revision, denial, duplicate activity and recovery after a successful Hub command loses
  its HTTP response. The latter also exercises the real Energy Hub HTTP endpoint.
- [x] Add the local setup/launch scripts, optional Hosting walkthrough and presenter prompt.
- [x] Update presentation recommendations first, then the book recommendations, with the local
  implementation's actual boundary and verified failure modes.

Compatibility findings: the new SDK has separate worker and host-extension dependency graphs;
both must be audited. Normalize the hosting package's stale MCP 1.0 declaration to the selected
MCP 1.6 host extension and patch host-side WebUtilities to 2.3.14. Core Tools was updated from 4.6.0
to 4.16.1. The launcher selects the per-user .NET 11 executable in generated local worker config.
The Foundry Responses adapter owns remote history and requires a durable history-provider key;
this demo instead uses Chat Completions with entity-owned history. Awaiting `CancelAsync()` in an
orchestrator stranded a durable timer cancellation; synchronous cancellation is the verified fix.

The released graph-hosting API and RequestPort sample were inspected. The old approval executor
depends on process-local waits, so the implemented recovery path uses an explicit Durable Task
orchestrator sharing policy/gateways. A durable MAF graph adaptation remains separate work; do not
claim the original graph was moved unchanged.

Still open for F: authenticated Command Center approval UI, Entra authorization and session ownership,
separate executor identity, cloud Functions/DTS infrastructure and enforced retention, durable Hub
receipts for a broader crash scope, and reliable-streaming work if required. The local signed-key
review is explicitly a single-presenter rehearsal, not a production identity implementation.

GitHub was fetched again; the branch base still matches `origin/main` at `7ad5369` (0/0 divergence).
All implementation changes remain local and uncommitted. Routines still fails read-only token
acquisition and needs the user's `azd auth login`; no remote routine/monitor/deployment was created.

Final verification for this slice: `dotnet restore`, `dotnet format`, `dotnet build` (zero warnings
and errors), and `dotnet test --project ...` passed. **428 tests passed on Windows and 428 on Linux**,
with four existing live-IDE skips per environment. Solution and generated host-extension audits
reported no known vulnerable packages. Functions direct dependencies had no available stable
updates in the configured feeds. The unanswered review expired after worker downtime as
`TimedOut`, with no command and no additional Hub revision. Whitespace and script parsing checks
passed. Remote CI and cloud Functions deployment remain unexecuted.

## 12. Recovery review fixes — November 17

Addressed the two recovery findings before extending presentation claims:

- Initial Hub reads, approval-stage polls and pre-dispatch revalidation now have three activity
  attempts with a two-second retry interval. Read HTTP calls have five-second timeouts.
- A failed pre-dispatch operation can be explicitly retried with `retryFailedOperation=true`.
  Preserve its history; use a distinct attempt ID and fresh supervisor review. Cap attempts at
  three per session/revision/stage. Failures after possible dispatch or without a trustworthy
  phase cannot use this retry path. This is not a global lock across new sessions/state revisions.
- Matching pending receipts and changed revisions without a matching receipt enter read-only
  reconciliation. No command is resent from reconciliation. Confirmed success is recovered;
  unresolved effects become `OutcomeUnknown` with nullable execution/resolution, never a false
  claim that the command did not run. A separate pre-dispatch read preserves definite refusal
  for already stale approval. The 30-second reconciliation scheduling budget can be extended
  by an in-flight activity's bounded read attempts.
- Added 19 deterministic cases, including transient read failure/recovery, retry exhaustion and
  admission, pending-receipt timeout, and a real Hub HTTP test interrupting the activity while
  the original device command is still in flight. The orchestration retry tests use a scheduler
  double; they do not independently verify the DTS engine's implementation.
- The runbook now waits for an unexpired `AwaitingApproval` review before worker restart, verifies
  that review survives, and polls a terminal result after approval. The helper parses the status
  endpoint's serialized business outcome and can save status-only evidence. Six local HTTP helper
  tests cover transient reads, actual readiness, terminal polling, nulls, authorization and timeout.
- New histories use `caesarea-recovery-v2`: activity scheduling changed, so v1 histories are not
  compatible. The initializer, host, launcher and this machine's ignored settings select v2;
  credentials were retained and v1 emulator/history was left untouched. A fresh v2 emulator/session
  is required. At this November 17 checkpoint no live v2 restart was claimed; the November 18 dashboard rehearsal below closes that gap. No cloud deployment is claimed.
- Reconciled the earlier checklists and historical SDK/baseline wording. Updated presentation
  recommendations first, then the book map, with unknown outcomes and evidence requirements.

Fresh local verification: `dotnet restore`, `dotnet format`, `dotnet build`, the required
`dotnet test --project ...` and `git diff --check` passed. Windows build: zero warnings/errors.
**447 deterministic tests passed on Windows and Linux**, with four existing live-IDE skips each.
Linux clean build succeeded with one SDK `AZFW0108` warning: the generated Functions extensions
project used restore-during-build. No warning was suppressed. All six PowerShell polling checks
and PowerShell syntax checks passed. These are implementer results, not independent review or CI.

The [evidence bundle](docs/evidence/durable-recovery-2026-11-17/README.md) preserves selected
sanitized v1 outputs and original hashes, plus the current regression verification record. It is
not a complete restart transcript. The November 18 dashboard transcript now records a fresh v2 worker crash/restart and successful repair.
All changes remain on the existing update branch, uncommitted; no publication or remote write occurred.

## 13. Ignite announcement review — November 18

Reviewed the Microsoft Book of News, Foundry launch/model articles, Azure observability news,
IQ and governance announcements, and current AKS account schema against the released slide text
and current source. The [full inventory](docs/product-status/ignite-2026-gap-review.md) includes
feature-level availability, source discrepancies, repo gaps, specific PDF pages and book placement.

- [x] Record the announcement snapshot and correct Insights service status to GA, retaining
  experimental .NET SDK labels. This is documentation review, not new runtime verification.
- [ ] After Ignite concludes November 20, recheck late announcements and rollout wording before
  freezing presentation availability claims.
- [ ] Update H08 hosting pp. 45–46 and W20 p. 41 with the Foundry-integrated customer AKS option.
  Label it announced preview; do not imply the current account can switch hosting in place.
- [ ] Update H08 p. 19 with the separate MAF, Copilot SDK 2.0 and managed Copilot harness choices.
- [ ] Extend H08 pp. 52–55 / W20 p. 38 with trace annotations, Insights, evaluations and reviewed
  optimizer candidates. Keep new preview capabilities distinct from GA services and retain ASSERT.
- [ ] Update the knowledge/catalogue and governance material for IQ graphs/Retriever, business
  plugins, Agent Grid, Agent 365/Entra and model/training choices with feature-specific labels.
- [ ] Optional next integration: assess compatible local-agent telemetry into Application Insights
  and Foundry external-agent registration. Show a real trace before claiming cloud visibility;
  registration alone is insufficient. No registration, monitor or telemetry deployment was done.
- [ ] Apply book updates after the presentation/demo rehearsal, using the inventory's chapter map.

The v2 recovery recording is now complete; Routines authentication/rehearsal and genuine Insights evidence
remain the immediate demo work. Full AKS, Agent Grid, cloud Functions, training and model-default
changes remain deferred. The local system was left running for the user's testing.

## 14. Guided durable recovery in Demo Director (November 18)

Checkpoint before implementation: `89d5a51`, pushed on `upgrade/net11-foundry-insights-routines`.

- [x] Add 15. Durable recovery after Evaluation and link it from Lights On Reported by a Client.
- [x] Reuse existing panels, numbered instructions, expected results and teaching-point styles.
- [x] Implement server-side local worker lifecycle and separate operator/supervisor HTTP calls.
- [x] Director prepares foundation only; Command Center starts/resumes the conversation and approves. Director alone crashes/restarts the worker.
- [x] Preserve the exact review, require explicit decision, poll terminal status and fresh Hub evidence.
- [x] Retain rehearsal across page refresh; block accidental scenario/reset and ambiguous retries.
- [x] Add deterministic regression tests and complete repository quality checks and live rehearsal.
- [x] Update the runbook; recommend a slide immediately after Hosting, then a book section.
- [x] Handle scheduler termination/failure output without retaining stale AwaitingApproval UI;
  explain disabled continuation and recheck authoritative status before another agent turn.
- [x] Tolerate temporary Windows snapshot replacement locks without repeating external actions.
  Final two-screen live flow passed; evidence saved alongside the initial all-Director transcript.
  Latest Windows build: zero warnings/errors; 469 tests passed, 4 existing skips.

The comparison must acknowledge Foundry session storage and opt-in resilient handler reentry.
Durable Functions adds explicit workflow replay, durable timers/events and process coordination.
Neither hosting option supplies exactly-once external device effects by itself.
