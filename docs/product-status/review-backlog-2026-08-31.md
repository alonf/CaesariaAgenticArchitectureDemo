# Review backlog — 2026-08-31

All findings from the full working-tree review are now resolved.

## Fixed in the first pass

- Unsafe-method retries disabled globally; the agent HTTP client got budget-aligned timeouts.
- Web apps no longer WaitFor the Operations Agent.
- Energy Hub rejects success-without-confirmed-state.
- Demo breakpoint endpoints are loopback-only.
- `/ask` enforces a 1,000-character question limit and rejects blank model answers.

## Fixed in the second pass

1. **Stage enforcement** — DemoStage now propagates to OperationsAgent.Api
   (`POST /api/operations-agent/demo-stage`, via a new `IOperationsAgentStageClient` in the
   StageCoordinator); a `DemoStageGate` rejects `/ask` with 409 while the stage is Deterministic, so
   Stage 0 can never reach Foundry. Agent-propagation failure degrades to a visible warning instead
   of failing the stage change. Verified live: 409 → propagate → 200.
2. **Command/reset race** — SmartPole and Energy Hub now carry a monotonic state revision; scenario
   apply and reset bump it, and an in-flight command commits only if its captured revision is still
   current, otherwise it returns a "superseded" failure without touching the new state. Covered by
   concurrency tests in `SupersededCommandTests`.
3. **Problem Details consistency** — all five APIs run `UseExceptionHandler()` +
   `UseStatusCodePages()`; web clients fall back to the HTTP status line when an error body is empty
   or not JSON.
4. **Presenter failure controls** — DemoControl gained a Simulator Behavior panel (command delay,
   simulated timeout, simulated failure) routed through a narrow
   `GET/POST /api/simulator-behavior` surface on DemoScenario.Api; settings reset with every
   scenario apply/reset (inherent: scenarios carry their own configuration).
5. **Tool-invocation proof** — `OperationsAgentResponse` now carries `ToolCalls` (tool name +
   validated arguments) and `ModelRoundTrips`, sourced from the ModelExchangeRecorder;
   CommandCenter renders a model → tool → model capability trace. Verified live.
6. **Line endings** — normalized via `dotnet format`; `dotnet format --verify-no-changes` passes.
7. **Package refresh** — Microsoft.Extensions.* 10.9.0 and OpenTelemetry 1.18.0 across
   ServiceDefaults.

## Fixed in the Memory-stage review pass

1. **Prompt-injection hardening (high)** — recalled case text (operator input / earlier model
   output) no longer reaches `AIContext.Instructions`. Behavioral rules are static trusted
   instructions; case content travels as a JSON data message the rules mark as reference-only.
   The close-case API validates the asset format and caps symptom/resolution lengths, the store
   truncates as defense in depth, and recall returns at most three cases. An adversarial-content
   test proves stored text stays out of the instruction channel.
2. **Agent-propagation degradation** — the stage push to the Operations Agent now also tolerates
   `TimeoutRejectedException` (the resilience attempt timeout) and internal cancellation while the
   caller's token is not cancelled; real caller cancellation still propagates. Tests cover each
   path separately.
3. **Presenter reset restores CASE-1** — `Clear()` resets the case counter inside the lock; the
   clear test asserts the next case is CASE-1.
4. **Command generation ordering** — accepting a command claims a fresh revision in both the
   SmartPole simulator and the Energy Hub restore workflow, so an older concurrent command can
   never overwrite a newer one; deterministic tests cover both boundaries.
5. **Recall precision** — word-boundary tokenization with stop words, at least two shared
   meaningful terms required, top-3 results; negative tests cover controller/consumption
   questions.
6. **Presenter flow polish** — close-case has busy/completed states (no duplicate cases);
   DemoControl reloads case memory during polling and manual refresh.
7. **Test infrastructure** — migrated to xUnit v3 (4.0.0) on Microsoft.Testing.Platform:
   `global.json` opts `dotnet test` into MTP mode, the test project is an executable, the legacy
   Test SDK and the VSTest-only coverlet collector were removed (MTP coverage would use
   `Microsoft.Testing.Extensions.CodeCoverage` when needed).

## Fixed in the markdown-rendering review pass

1. **Autolink scheme bypass (high)** — `<javascript:...>` autolinks parse as `AutolinkInline`,
   which the first sanitizer pass missed. AnswerHtml now handles regular links and autolinks:
   destinations must be absolute http/https/mailto; anything else - javascript:, data:,
   vbscript:, file:, and relative paths - is reduced to plain text (unwrapped, not left as an
   empty-href anchor). Markdown images are reduced to their alt text so model output can never
   trigger outbound requests. Adversarial tests cover all four schemes in both link forms,
   relative links, and images.
2. **Restore result race** — the success `RestoreScheduledModeResult` now reports the
   `confirmedIsOn` captured for this command instead of re-reading the live twin outside the lock.
3. **Test-runner migration completed (MTP-only)** — the VSTest adapter package is removed, VS Code
   Test Explorer uses the Testing Platform protocol via `.vscode/settings.json`, and the
   documented quality command is `dotnet test --project Tests/...` (a bare solution-wide
   `dotnet test` can fail on machines where the Aspire AppHost SDK does not resolve during test
   discovery).
4. **Message honesty** — superseded summaries name the newer-operation cause; the stage-push
   warning says propagation could not be confirmed (reconciliation retries) instead of claiming
   the agent did not receive it.
5. **Close-case boundary validation** extracted to `CloseCaseValidation` and tested through the
   validator (invalid/blank/over-limit/at-limit).
6. **Stale wording** — comments and 04-memory.md now describe trusted static rules plus untrusted
   JSON reference data, and call the separation a risk reduction, not a guarantee.
7. **Table containment** — `.agent-answer` scrolls wide content locally and wraps long tokens.

## Fixed in the Skills-stage review pass

1. **Session stage floor** — sessions record the highest stage they ran at; continuation at a
   lower stage is rejected (410) so a downgrade fully restores the earlier composition, and the
   Command Center resets its session id when it observes a stage moving backward.
2. **Loaded-badge accuracy** — a skill counts as loaded only when a `load_skill` call named it
   with an exact JSON `skillName` match (the SDK does exact lookup) and the pipeline recorded a
   result for that call id; the recorder now correlates `FunctionResultContent` back to calls.
3. **Catalog snapshot and robustness** — the skills catalog is snapshotted before the model run
   (a mid-run presenter edit cannot fail a successful answer), parses only the `---` frontmatter
   block, applies the SDK's `AgentSkillFrontmatter` validation, matches the SDK's discovery
   depth, and skips unreadable/invalid files with a warning.
4. **Single stage snapshot per request** — one `DemoStage` read drives every capability
   decision, so a mid-composition stage change cannot produce a mixed set.
5. **Provider disposal** — the per-request `AgentSkillsProvider` is disposed in a `finally`.
6. **Real SDK discovery test** — the actual `AgentSkillsProvider` over the repository skill,
   driven through a deterministic capturing chat client, proves advertisement and `load_skill`
   exposure; the shipped skill's procedure sections, brief format, and footer are pinned, and the
   skills directory is scanned for credential-like content.
7. **Trust boundary documented** — skill content is injected as instructions without
   sanitization; only reviewed, trusted skill sources may be configured.

## Fixed in the InteractiveInput-stage review pass (September 2026)

1. **Own-write invalidation (high)** — an approved agent restore no longer invalidates the very
   answer that reported it. The ask's correlation travels the whole chain (ask → MCP client
   header → EnergyHub restore tool via `IHttpContextAccessor` → SmartPole → Command Center
   `LastCommand`), and `AgentEvidenceGuard` accepts a changed snapshot when the last command
   carries the answer's correlation while all ambient evidence (stage, scenario, report,
   incident) is untouched — adopting the post-write version so later polling doesn't trip
   either. Verified live end-to-end.
2. **Stage-downgrade withdrawal** — `StageTransitionEffects` (wired into the stage endpoint and
   the synchronizer) cancels every pending interactive-input request on any downgrade and resets
   the tool source to Local below McpTools; the approvals decision endpoint is 409-gated below
   InteractiveInput, and the elicitation handler re-checks the stage after its await. DemoControl
   polls the tool source so the toggle stays truthful.
3. **MRTR confirmation binding** — `MrtrRequestStateStore` issues a one-time, five-minute,
   asset-bound request state with each pause; a continuation must echo it or the confirmation is
   rejected (fabrication/replay impossible). `IsMrtrSupported` is checked before anything else.
   Docs now call the mechanism operator confirmation — interactive input, not authorization.
4. **PendingApprovalStore race** — the entry is stored before the cancellation callback is
   registered, and an already-cancelled token cleans up deterministically; `CancelAll` supports
   the downgrade path. Tests cover pre-cancelled tokens, decide-vs-cancel, and cancel-all.
5. **MCP lifecycle and budget** — client/transport creation and tool discovery run inside the
   request's wall-clock budget; the transport (owning its HTTP client) is disposed in `finally`
   alongside the client; `McpException` and missing tools surface as
   `OperationsAgentToolUnavailableException` → 502 instead of an unexplained 500.
6. **Honest tool annotations** — `get_streetlight_state` declares ReadOnly/Idempotent/closed
   world; `restore_scheduled_mode` declares non-destructive/Idempotent/closed world.
7. **Approval polling resilience** — one failed poll no longer kills the Command Center's
   approval loop; a failed decision delivery keeps the prompt (the tool is still paused) and the
   buttons disable while a decision is in flight.
8. **Atomic report resolution** — `CustomerReportModule.TryRemove` removes the matching report
   under the lock (concurrency test pins exactly-one-winner); the web client treats only 404 as
   "no longer open" and throws on other failures.
9. **In-proc MCP integration tests** — the real Energy Hub MCP server boots under
   `WebApplicationFactory` with only the SmartPole gateway faked, driven by the real MCP client:
   discovery + annotations, read tool side-effect-free, restore without a handler never executes,
   denial leaves state untouched, approval executes with the caller's correlation.
10. **Restore-intent reliability** — the agent instructions now direct it to invoke an available
    state-changing tool immediately (the tool obtains the operator's confirmation itself), fixing
    occasional runs that narrated instead of pausing.

## Fixed in the Workflow-stage review pass (September 2026)

1. **Effective target, not raw schedule (high)** — policy and verification judged anomalies
   against `ExpectedScheduledState`, so the Security Operation scenario (lighting required while
   the daylight schedule says off) was classified as an anomaly and **auto-corrected without
   approval**. Both boundaries now share one canonical rule, `LightingTarget.Resolve`, which the
   Energy Hub's `ComputeScheduledTarget` also delegates to; the twin exposes `EffectiveTargetIsOn`
   and `IsAnomalous`. A workflow test proves required lighting is never remediated, and the live
   walk applies the scenario end to end.
2. **State validated before approval is now protected at execution (high)** — the Energy Hub
   exposes an authoritative `StateRevision` on the twin (bumped by reset, scenario application,
   and every accepted command); the workflow captures it at validate, the restore carries it, and
   the Hub checks it under the same lock that claims the command, returning 409 with a
   `preconditionFailed` marker. The execute step re-validates once, refuses to clear an override
   that appeared after a no-approval decision, retries only against the fresh revision, rechecks
   the demo stage immediately before commanding, and a downgrade cancels in-flight runs.
3. **Own-write evidence exemption is now one-time and state-checked (high)** — the fingerprint
   uses the authoritative state revision, and adoption requires the last command to be this run's,
   to have succeeded, and the observed state to match what it commanded. A later telemetry change
   while the same command record stands makes the answer stale, as a regression test pins.
4. **Executed is not resolved** — the run report separates `CommandExecuted`, `Resolved`, and a
   terminal `Status`; a run that fails after a command landed still reports that it landed, and
   the UI colors by verified resolution.
5. **Requirements alignment** — the agent no longer holds the direct write at Workflow+; it calls
   `start_restore_lighting_operation`, and the workflow owns the operation. The failure branch
   raises a maintenance work item in a work-management emulator (`IWorkItemGateway`), and the
   completion is audited. Checkpoint/resume is deferred with a trigger in the stage document.
6. **Honest MCP write annotations** — `restore_scheduled_mode` is now `Destructive = true`,
   `Idempotent = false`: it discards operator intent and each call issues another physical command.
7. **Workflow diagram survives stage round-trips** — the render flag resets when leaving the
   stage, rendering requires the Workflow stage, and the flag is set only after the interop call
   succeeds.
8. **Bounded registries** — completed workflow runs are pruned to a recent tail (never evicting a
   run still executing), work items are capped, and expired MRTR tokens are swept on issue.
9. **YAML/graph equivalence is enforced** — a test compares the displayed declarative topology
   (start, nodes, edge endpoints, which edges are conditional) against the executing graph via
   `ReflectExecutors`/`ReflectEdges`, rather than checking for a couple of substrings.
10. **Vendored asset hygiene** — `.gitattributes` stores `*.min.js` byte-for-byte with no
    whitespace checks, and the Mermaid folder records source, version, SHA-256, and license.

## Fixed in the Workflow-stage second review pass (September 2026)

1. **The precondition marker was never sent (high)** — the Energy Hub detected the stale revision
   and answered 409, but never set the `preconditionFailed` extension the gateway looks for, so
   over real HTTP a refusal was read as a plain command failure and the re-validation path never
   ran. The workflow tests missed it because the fake gateway returned the flag directly. Fixed
   with a typed discriminator (`RestoreScheduledModeResult.PreconditionFailed`) driving the
   shared `EnergyCommandProblem.PreconditionFailedExtension`, replacing the summary-prefix match,
   and pinned by an integration test that drives the real endpoint through the real
   `HttpEnergyCommandGateway`. That test was confirmed to fail without the marker.
2. **An approval could authorize a newer override (high)** — after a precondition failure the
   earlier approval was reused whenever the fresh state still required approval, and a revision
   cannot distinguish harmless movement from a replaced operator override. The step now fails
   closed: a re-validated state that still requires approval needs a *fresh* one, and only a
   state that no longer requires approval is retried. The stage is rechecked before the retry.
3. **Transport and verification failures bypassed the work-item branch** — gateway exceptions
   escaped to the run's outer handler, which marked the run failed without raising the
   maintenance work item the requirement calls for. Expected downstream failures (unreachable
   hub, non-caller timeouts) now become outcome data that routes to `workitem`; caller and stage
   cancellation still propagate as cancellation.
4. **Agent-started runs are visible and approvable** — the run report carries its asset and
   correlation, a correlation-filtered lookup exposes the run the agent started, and the Command
   Center adopts it after an ask so its steps appear and its approval gate is answerable. Terminal
   run logs carry asset and correlation.
5. **The Command Center judged anomalies by the raw schedule** — it now uses the same
   `IsAnomalous` rule as the workflow and the Energy Hub, and shows Schedule beside Effective
   target so the security-operation distinction is visible on the projector ("Lit for operation",
   attention count zero).
6. **Hard bounds on the registries** — MRTR tokens have an issuance ceiling as well as an expiry,
   run pruning skips active entries instead of abandoning the sweep, evicted runs are disposed,
   and a finished run triggers another sweep.
7. **Claims match the implementation** — the YAML/graph test is described as topological (the SDK
   exposes no predicate text), the README describes the write capability's stage *window* rather
   than "from that stage on", and the vendored Mermaid folder carries the full upstream MIT text.

## Fixed in the ToolApproval / MultiAgent review pass (September 2026)

1. **The consulted agent could disclose an unexamined snapshot (high)** — the Security Agent read
   the hub once for sanitization and its internal tool read it again with a model-supplied area.
   Now one snapshot is captured and served by a tool bound to the area under assessment; an
   out-of-scope area is refused.
2. **The denylist was not a confidentiality guarantee (high)** — it forwarded model prose unless a
   whole restricted value appeared verbatim, so "Bar-On authorized it", a paraphrase, or an
   abbreviation all crossed. No model-authored text crosses now: the agent selects a reason code
   from a closed set, the verdict and deadline are computed from the records, and the public
   sentence is rendered from a template. Tests cover fragments, paraphrases and encodings.
3. **The permission boundary is enforced, not assumed** — the Security Hub requires a caller
   identity on every route: reads admit the Security Agent only, admin routes admit the scenario
   service only, verified over real HTTP. It is a demo-grade header rather than an authenticated
   principal, and the stage document now says so plainly.
4. **The multi-agent claim is stated precisely** — the verdict is deterministic by design (a model
   must not be able to switch off security lighting), so the stage is presented as a permission
   and isolation boundary rather than proof that a model was required to reach the answer. The
   assessment carries the consulted agent's name so the nested consult is visible in a trace.
5. **Approved maintenance cannot execute after a downgrade** — the stage is rechecked inside the
   tool, immediately before the side effect, not only before the resumed run.
6. **Approval-loop exhaustion fails explicitly** — unresolved approval requests after the bounded
   rounds raise `OperationsAgentApprovalLoopException`, answered as 409, instead of returning an
   unfinished turn and persisting it as successful.
7. **The control point is in the contract** — each pending approval carries a typed
   `OperationsAgentControlPoint` plus the tool name and arguments, and the Command Center renders
   each row from its own type instead of inferring it from busy flags.
8. **The A2A note was factually wrong and is corrected** — `Microsoft.Agents.AI.A2A` and
   `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` are published in the same preview family this
   solution uses; the earlier claim came from a package search that omitted `--prerelease`.
9. **The caps are hard** — MRTR issuance prunes, evicts and inserts under one lock, and the
   workflow separates concurrent-run admission from completed-run retention.
10. **Smaller fixes** — a non-string `reasonCode` can no longer throw; scenario synchronization is
    described as consistent after successful application rather than impossible to disagree; and
    model-supplied maintenance arguments are validated against the canonical asset form and capped.
11. **Presented-code order** — the `TOOL_APPROVAL` region now precedes the MultiAgent composition,
    so stepping through cumulative stages follows the lecture order.

## Deferred (with trigger)

- **Snippet region scan scope** (low, from the demo-anchor review): the region synchronization
  test scans `Services/` only, while the requirements allow snippets in dedicated sample files
  (e.g. a future `Samples/` root). Trigger: the first snippet region outside
  `Services/` — then either widen the scan to every source root (generated directories excluded)
  or introduce an explicit snippet catalog shared by registration and validation. The companion
  gap (a `DemoSnippets` constant missing from runtime `MapDemoBreakpoints` registration) is
  closed by `RegisteredBreakpointsCoverEveryDemoSnippet`, which since the McpTools stage
  validates the union of every service's registration (MCP_SERVER registers in EnergyHub.Api).
- **Multi-service breakpoint arming in DemoControl** (from the McpTools stage): the DemoControl
  breakpoints panel arms and attaches only the Operations Agent process; the Energy Hub's
  MCP_SERVER snippet pauses when a debugger is attached to EnergyHub.Api (compound launch) and
  can be armed via its loopback breakpoints endpoint. Trigger: if a lecture beat needs one-click
  arming of an Energy Hub snippet, extend the panel to enumerate breakpoint sources per service
  and route attach requests by process name.
