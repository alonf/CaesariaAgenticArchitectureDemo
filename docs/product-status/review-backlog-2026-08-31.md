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

## Deferred (with trigger)

- **Snippet region scan scope** (low, from the demo-anchor review): the region synchronization
  test scans `Services/` only, while the requirements allow snippets in dedicated sample files
  (e.g. a future `Samples/` root). Trigger: the first snippet region outside
  `Services/` — then either widen the scan to every source root (generated directories excluded)
  or introduce an explicit snippet catalog shared by registration and validation. The companion
  gap (a `DemoSnippets` constant missing from runtime `MapDemoBreakpoints` registration) is
  closed by `RegisteredBreakpointsCoverEveryDemoSnippet`.
