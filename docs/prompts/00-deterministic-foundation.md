# Stage 0 — Deterministic Foundation

## Previous system state

No application exists yet. This stage establishes the useful Smart City platform that exists before any agentic capability is introduced.

## New business requirement

The Caesarea Command & Control system must operate city infrastructure deterministically.

The first demo domain is street lighting in the Energy Hub:

```text
Streetlight: L-417
Area: North Promenade
```

An operator must be able to observe repeatable scenarios, inspect authoritative operational state, and request a deterministic restoration to scheduled operation.

## Architectural reason

The deterministic platform must own operational truth and execution before AI participates in the architecture.

This stage establishes the boundaries that later agents, MCP capabilities, workflows, identity controls, and evaluations must respect:

```text
Demo Control -> Demo Scenario API -> SmartPole Simulator
                                     -> Energy Hub

Command Center -> Command Center API -> Energy Hub -> SmartPole Simulator
```

The Energy Hub is independently hosted from the beginning because it is the authoritative operational and authorization boundary. Incident management, spatial context, and activity history begin as modules within one Command Center host.

## Coding-agent prompt

```text
Read the architecture documents under /docs and the repository guidance before making changes.

Use the installed agentic-architecture-router skill and relevant Microsoft/.NET skills when they apply.

Create Stage 0, the deterministic foundation for the Caesarea Smart City conference demo.

This stage must contain:
- NO LLM;
- NO Microsoft Agent Framework;
- NO MCP;
- NO AI agent;
- NO AI credentials;
- NO Dapr;
- NO production message broker;
- NO cloud dependency.

Use .NET Aspire for local orchestration, service discovery, health checks, configuration, and OpenTelemetry. Business code must not depend on Aspire.

Use ASP.NET Core Minimal APIs for deterministic services and Blazor Web App with Interactive Server rendering for browser applications.

Create this solution structure:

Caesarea.AppHost
Caesarea.ServiceDefaults
Apps/CommandCenter.Web
Apps/DemoControl.Web
Services/CommandCenter.Api
Services/EnergyHub.Api
Services/SmartPole.Simulator.Api
Services/DemoScenario.Api
Shared/CanonicalModel
Contracts/SmartPole.Contracts
Contracts/Energy.Contracts
Contracts/CommandCenter.Contracts
Contracts/DemoScenario.Contracts
Tests

Implement:

1. CommandCenter.Web
   - A polished operations-center interface that remains stable throughout the lecture.
   - Show a simple North Promenade map with L-417.
   - Show current reported state, daylight, expected schedule, manual override, controller health, desired state, open incident, current scenario, and recent activity.
   - Use blue/green for deterministic state and execution.
   - Receive live updates through SignalR where useful, but reload authoritative snapshots after reconnection.
   - Do not expose simulator controls in the normal operator experience.

2. DemoControl.Web
   - A clearly labelled presenter-only simulation console.
   - Select and reset deterministic scenarios.
   - Show simulator controls and failure/telemetry controls.
   - Do not treat this application as part of normal city operations.

3. EnergyHub.Api
   - Independently hosted authoritative boundary for streetlight state and operations.
   - Expose a normal REST API using Minimal APIs and OpenAPI.
   - Own the operational twin, desired/reported state, command validation, and Energy events.
   - Never expose vendor-specific SmartPole behavior to consumers.
   - Never update reported state until confirmation is received from SmartPole.
   - Use typed contracts and ProblemDetails.

4. SmartPole.Simulator.Api
   - Simulate the physical/vendor streetlight system behind the Energy Hub.
   - Own actual lamp state, command acknowledgement, telemetry delay, controller faults, and timeouts.
   - Keep state resettable and deterministic for conference use.

5. CommandCenter.Api
   - Keep incident management, minimal spatial/asset context, and activity timeline as explicit modules in one host.
   - Aggregate a projection-friendly operational snapshot for CommandCenter.Web.
   - Do not communicate directly with SmartPole.

6. DemoScenario.Api
   - Coordinate repeatable synthetic scenarios across SmartPole and controlled demo data.
   - Keep scenario manipulation separate from normal operational APIs.

7. Contracts and cross-cutting behavior
   - Give each API boundary its own focused contracts project.
   - Use Caesarea.CanonicalModel only for the deliberately shared operational language.
   - Do not create a generic Common or Shared.Contracts project.
   - Keep ServiceDefaults independent of domain and transport contracts.
   - Use correlation IDs across scenario selection, Hub commands, simulator calls, events, and activity records.
   - Add health checks and OpenTelemetry through ServiceDefaults.
   - Keep service-owned state. SQLite is sufficient for persistent local state; SmartPole may use in-memory state.
   - Add a DemoStage setting beginning with Deterministic.

Required L-417 operational model:
- AssetId
- Area
- IsOn / ReportedIsOn
- DesiredIsOn
- IsDaylight
- ExpectedScheduledState
- ManualOverride
- ControllerHealth
- LastCommand
- LastMaintenanceTime
- OpenIncidentId (optional)
- LastReportedAt

Implement these scenario presets:

1. Forgotten Override
   - daylight = true;
   - light = on;
   - schedule = off;
   - manual override = on;
   - controller = healthy;
   - recent maintenance exists;
   - no open incident.

2. Security Operation
   - daylight = true;
   - light = on;
   - schedule = off;
   - active Security operation in the area;
   - RequiresLighting = true.

3. Controller Fault
   - daylight = true;
   - light = on;
   - schedule = off;
   - manual override = off;
   - controller = faulted.

4. Existing Incident
   - an anomaly exists;
   - an open incident already exists.

5. Normal Operation

6. Night Operation

Implement a narrow deterministic operation:

RestoreScheduledMode(assetId)

Required behavior:
- Energy Hub rereads authoritative current state.
- Energy Hub sets desired state to the configured schedule.
- Energy Hub calls SmartPole through its vendor adapter.
- Reported state changes only after SmartPole confirmation.
- The operation publishes correlated activity/events.
- Failure and timeout remain visible and do not produce a false success state.

Add automated tests for:
- every scenario reset;
- Energy Hub state reads;
- successful RestoreScheduledMode;
- desired/reported state behavior;
- controller failure and timeout;
- event/activity publication;
- incident lookup;
- correlation ID propagation;
- Command Center never calling SmartPole directly.

Keep the code projector-friendly:
- obvious names;
- small types;
- minimal ceremony;
- no generic repository;
- no heavy CQRS framework;
- no speculative Hub framework;
- no abstraction without a current substitution or boundary.

Before coding:
1. Summarize the architectural impact in no more than 10 lines.
2. List the files/components to add or change.
3. Then implement.

After coding:
1. Build the solution.
2. Run relevant tests.
3. Summarize exactly what changed.
4. State that DemoStage=Deterministic enables the capability.
```

## Explicit exclusions

- Agent chat or model calls
- Organizational knowledge retrieval
- MCP
- Workflow engine
- Entra Agent Identity
- Agent authorization policies
- Azure Service Bus or Dapr
- Security Agent or multi-agent communication
- ASSERT behavioral evaluation

## Expected interaction

1. Open `DemoControl.Web`.
2. Select **Forgotten Override**.
3. Open `CommandCenter.Web`.
4. Observe L-417 on during daylight while its schedule expects off.
5. Invoke **Restore Scheduled Mode** through the operational UI.
6. Observe desired state, SmartPole confirmation, reported state, and the correlated activity timeline.

## Tests

- Scenario reset and repeatability
- Hub reads and commands
- Desired/reported state synchronization
- Failure and timeout behavior
- Incident lookup
- Event and activity generation
- Correlation propagation
- Architectural boundary test preventing Command Center-to-SmartPole access

## Teaching point

> **The deterministic platform operates the city. Everything agentic we add later must participate in this architecture, not bypass it.**

## Demo stage

```text
DemoStage=Deterministic
```
