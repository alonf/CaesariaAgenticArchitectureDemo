# Optional Lab 17 — reviewed improvement from evidence

This is a **preparation and engineering-review lab**, not a successful Foundry Insights retrieval. Outcome: distinguish an observation from a generated finding, inspect the read-only collector, and propose a regression with disconfirming evidence. Allow 20 minutes. Code `4e5a6d3`; no monitor creation, analysis run, status change, deployment or telemetry publication.

## Local commands

```bash
dotnet build tools/FoundryInsights/FoundryInsights.csproj
dotnet run --no-build --project tools/FoundryInsights -- --help
dotnet run --no-build --project tools/FoundryInsights -- http://localhost --list-monitors
```

Expected: help prints monitor/insight read modes; the invalid endpoint exits with an error before network access. Use the valid HTTPS endpoint shape only in a separately authorized retrieval. This lab has no external resources to reset.

## Real course incident, not a generated Insight

The Security specialist call hit the old default ten-second HTTP attempt budget. Commit `59017d7` adjusts the specific client's budgets and disables unsafe automatic retries. A subsequent actual consultation completed with correlation `37dcf6567f6840ac933f3283b8ae1148`; Hub state was unchanged. The failed and successful invocations are distinct evidence. See Lab 11 and local production evidence.

Read `Tests/Caesarea.Deterministic.Tests/OperationsAgentApiTests.cs:26` for the eleven-second success test and `:41` for one-call behavior on 503. Those tests replace the response handler; they do not call a real model. A passing boundary test does not certify agent semantics. The current Windows suite passed 473 tests with four opt-in IDE integration tests skipped.

## Collector and retrieval limits

`tools/FoundryInsights/Program.cs` uses an existing monitor, follows SDK continuation pages, requests expanded details, deduplicates IDs and emits an explicit completion marker. Keep a partial result marked incomplete. This utility does not create monitors, apply fixes or change insight status.

The current connected toolset has no `agent_insights_get`. The [Foundry Insights workflow](C:/Users/alon.HOME/.agents/skills/microsoft-foundry/foundry-agent/insights/insights.md) says: "If the tool is unavailable, stop and report that blocker." Accordingly, no fresh generated findings are claimed. A historical zero-monitor inventory is not a current health result.

Microsoft's [Insights guidance](https://learn.microsoft.com/en-us/azure/foundry/observability/how-to/agent-insights) describes trace-based recurring patterns and representative evidence. Before a future retrieval, verify telemetry readiness and access for the selected agent/monitor. The repository isolates preview SDK operations in this presenter utility. Keep any real telemetry and proposed patches in private artifacts.

## Pause and investigate

Create a local review note with: observed failure; exact evidence/correlation; two competing explanations; minimal proposed intervention; deterministic regression; held-out behavioral case; expected result; a result that would falsify the hypothesis; and rollback criteria.

1. Which evidence supports cause, and which only establishes timing?
2. What does an empty or incomplete finding collection actually mean?
3. Could a proposed prompt fix conceal a deterministic transport bug?
4. Which checks should be model-free, and which require representative model runs?
5. Who reviews a recommendation before policy or code changes?

Recovery: resolve invalid arguments or access separately; never reinterpret an error as zero findings or health. No automatic promotion or publication is part of this lab.
