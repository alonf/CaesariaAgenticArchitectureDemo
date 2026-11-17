# Routines and Insights: activation and engineering feedback

These are optional presenter beats inside Hosting and Evaluation. Selecting a stage does not
provision a routine, run a model, create a monitor, or grant a capability. The existing director
continues preparing local prerequisites; cloud readiness is verified separately here.

## Routines: investigate without an operator click

Use the deployed read-only Operations Agent, with `WORKIQ_TOOLBOX` unset for an unattended run
unless the selected routine identity and delegated Work IQ path have been explicitly verified.
The first demo ends at a recommendation with an unchanged lamp. It does not start remediation.

Prerequisites: current azd with `azure.ai.routines`, `azd auth login`, an accessible Foundry project,
and a ready deployed agent. Record the exact agent version and identity used by the run.

```powershell
# Set these to the existing project and its read-only agent; no tenant values are committed.
$endpoint = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$agent = '<deployed-read-only-agent>'
$at = [DateTimeOffset]::UtcNow.AddMinutes(10)

# Review the action, then create the routine disabled.
./scripts/Manage-InvestigationRoutine.ps1 -Operation Create -ProjectEndpoint $endpoint -AgentName $agent -AtUtc $at -WhatIf
./scripts/Manage-InvestigationRoutine.ps1 -Operation Create -ProjectEndpoint $endpoint -AgentName $agent -AtUtc $at
./scripts/Manage-InvestigationRoutine.ps1 -Operation Show -ProjectEndpoint $endpoint

# Rehearsal: explicit dispatch. Acceptance is not completion.
./scripts/Manage-InvestigationRoutine.ps1 -Operation Dispatch -ProjectEndpoint $endpoint
./scripts/Manage-InvestigationRoutine.ps1 -Operation Runs -ProjectEndpoint $endpoint

# Live timer beat: enable before the future UTC timestamp. Inspect Runs after it fires.
./scripts/Manage-InvestigationRoutine.ps1 -Operation Enable -ProjectEndpoint $endpoint
# Cleanup after the beat, including when a run fails.
./scripts/Manage-InvestigationRoutine.ps1 -Operation Disable -ProjectEndpoint $endpoint
```

The source-controlled manifest is `deployment/routines/lighting-investigation.json`. Creation
substitutes the agent and timestamp, always passes `--enabled=false`, and refuses to overwrite
an existing routine. Inspect an existing resource before deciding whether to change it.

Show the actual trigger time, dispatch/run identifiers, status, caller identity and output in the
Foundry run history. Read the cloud Hub again to verify that the lamp stayed unchanged. A failed
or unavailable run is an error to explain; do not substitute a local answer labeled hosted.

This increment is a one-shot timer. It implements neither arbitrary Hub-event delivery nor a local
Routines emulator. Durable duplicate suppression is required before adding consequential follow-on
work. Do not describe the timer as the deferred Service Bus event-driven stage.

## Insights: evidence becomes a reviewed regression

Availability update, November 18: [Ignite announced Insights service GA](https://azure.microsoft.com/en-us/blog/continuous-improvement-comes-to-microsoft-foundry/).
The selected .NET Projects SDK and its Insights surface remain beta/experimental. Keep those
labels separate; the announcement does not configure a monitor or create evidence for this demo.
See the [Ignite gap review](../product-status/ignite-2026-gap-review.md) for related previews and
the proposed external-agent observability exercise.

The presenter utility uses Azure.AI.Projects **3.0.0-beta.6** and deliberately opts into the preview
Agent Insights API. It reads existing monitors and their findings using the Azure CLI identity;
it does not create monitors, start analysis, change insight status, or execute proposed fixes.

```powershell
. ./scripts/Use-CaesareaSdk.ps1
dotnet run --project tools/FoundryInsights -- $endpoint --list-monitors $agent
dotnet run --project tools/FoundryInsights -- $endpoint '<monitor-id>'
# Restrict the collection to active findings when that is the question being asked:
dotnet run --project tools/FoundryInsights -- $endpoint '<monitor-id>' --active
```

The SDK collection follows continuation pages; the utility requests expanded details, deduplicates
IDs, preserves the wire models, and ends with a completion record and unique count. A failed page
leaves earlier output available but reports `complete:false` and exits nonzero. No matching findings
does not establish health. An absent monitor must be configured and supplied with representative
traces before this beat can demonstrate generated insights.

Keep raw output in the ignored `docs/runbooks/artifacts/` directory only when needed for rehearsal.
Review and scrub it before projecting. Label a prepared capture **CAPTURED / NOT LIVE**. Never
commit actual trace content, tenant identifiers, or service-generated proposed patches as fixtures.

Walkthrough: finding -> linked representative trace -> human hypothesis -> reviewed ASSERT case ->
code or instruction change -> rerun on frozen and held-out cases. Treat a generated diagnosis as a
hypothesis. Keep operational state and authorization in the Hubs and workflow.

The local Operations Agent emits MAF spans under `OperationsAgent.Api`, already subscribed by its
service telemetry configuration, with sensitive-content export disabled. The Foundry hosting SDK
owns its platform telemetry; this update does not add a second wrapper around that host.

## Observed validation, 2026-11-17

- Routine create dry run and command syntax verified. Live `Show` failed to acquire an azd token;
  no routine creation, dispatch, or enablement was performed.
- The configured project lists `caesarea-operations`. The Insights monitor list for this agent
  completed successfully with zero monitors. No finding or analysis run is claimed.
- The durable Functions remediation scenario remains the next implementation slice described in
  `upgrade plan.md`, section F. These utilities do not provide durable workflow recovery.
