# Stage 14 — ASSERT behavioral evaluation

Deck anchor: `EVALUATION`. Enable `DemoStage.Evaluation` for the cumulative local composition.
Agent 365 remains segment 13, not a runtime stage.

## Requirement and boundary

Measure whether the agent investigates, requests appropriate controlled actions, and respects
denials across repeatable L-417 situations. ASSERT operates outside the production flow; scenario
control, authorization, workflow execution, and operational state stay with their existing owners.

The implementation is [evaluation/assert_demo](../../evaluation/assert_demo/README.md): a reviewed
behavior specification, frozen lecture prompts, a Python callable over the existing .NET APIs,
OTel observation evidence, deterministic outcome checks, and a standalone HTML report. The
existing hosted-agent `eval.yaml` remains a separate evaluation.

## Lecture beat

1. Show one behavior in `cases.json`: a denied restore must not change the light.
2. Run `./scripts/Invoke-AssertDemo.ps1 -Arm both` during rehearsal. For a short live run, select
   `-Case forgotten-override` or `-Case denied-restore`.
3. Open the generated `report.html`. Baseline is the actual Session-stage agent with fewer
   capabilities; governed is the full local agent. The inputs and behavior requirements match.
4. Expand a failed baseline case. Inspect the answer and tool outcomes, then the governed result.
   Do not promise a pass: a live model or judge can expose a real failure.
5. Show that a denied action stays blocked **and** approved legitimate restoration succeeds.
6. Optionally show `-Generate`: ASSERT expands the requirement into candidate categories and test
   cases. Review these artifacts before a comparison; generation does not run the target.

## Honest interpretation

- Baseline compares an earlier composition, not a deliberately insecure deployed agent.
- A passing suite is sampled evidence, not a proof that every conversation is safe.
- ERROR identifies missing evaluation evidence or infrastructure failure, never agent compliance.
- The observation trace is the public API projection, not original per-tool timings or hidden reasoning.
- A recorded report must be labeled **CAPTURED / NOT LIVE** when used as a fallback.

## Rehearsal verification — 2026-09-09

The live local agent and an Azure `gpt-5.5` ASSERT judge were exercised on all nine fixtures.
The seven investigation/maintenance fixtures passed in the governed composition. The restoration
fixtures initially exposed two evaluator errors: demanding a redundant agent state read when the
workflow owns validation, and scoring a pending reply against an outcome not delivered to the
agent. After correcting the specification and adding the explicit post-denial bypass follow-up,
both restoration fixtures passed in a targeted rerun. The Session baseline passed current-state
and failed the requirements needing later-stage capabilities. Generated candidates were also
produced successfully; they remain review inputs, not certified regressions.

This records what was verified, not a promised future score. The original and corrected run
artifacts remain under the Git-ignored local artifacts directory.

> Unit tests prove the operation works. ASSERT measures whether the agent asked for the right
> operation, with the right evidence, under the right authority.
