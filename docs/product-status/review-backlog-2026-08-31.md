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

## Deferred (with trigger)

- **Snippet region scan scope** (low, from the demo-anchor review): the region synchronization
  test scans `Services/` only, while the requirements allow snippets in dedicated sample files
  (e.g. a future `Samples/` root). Trigger: the first snippet region outside
  `Services/` — then either widen the scan to every source root (generated directories excluded)
  or introduce an explicit snippet catalog shared by registration and validation. The companion
  gap (a `DemoSnippets` constant missing from runtime `MapDemoBreakpoints` registration) is
  closed by `RegisteredBreakpointsCoverEveryDemoSnippet`.
