# Stage 1 — Investigation Agent

## Previous system state

Stage 0 delivered the deterministic Caesarea Smart City foundation: `CommandCenter.Web`, `DemoControl.Web`,
`EnergyHub.Api`, `SmartPole.Simulator.Api`, `CommandCenter.Api`, and `DemoScenario.Api`, coordinated through a single
`DemoStage=Deterministic` composition. No LLM, Microsoft Agent Framework, MCP, AI agent, or AI credential existed
anywhere in the solution.

## Purpose

Give the Command Center operator a **read-only investigator** that can look at the same authoritative evidence a
human operator would look at — the current customer report, the authoritative Energy Hub state, recent Energy Hub
activity, and any existing Command Center incident — and produce a disciplined, evidence-grounded explanation of
what is currently happening with L-417. The agent must never be able to change city state itself.

## New business requirement

A citizen reports that streetlight L-417 is on during the day. The operator now wants more than the raw deterministic
snapshot: they want an assistant that actively investigates, tells them what is *verified*, what is only a
*hypothesis*, and what evidence is *missing*, before anyone decides to act.

> **Operator:** "Investigate L-417 for me."
>
> **Operations Agent:** Reads the current customer report, the Energy Hub state, recent Energy Hub activity, and
> any open incident — in whatever order it decides — and returns verified facts, ranked hypotheses with confidence
> and reasoning, missing evidence, and the exact tool trace it used to get there.

## Architectural reason

Introducing agentic reasoning must not weaken anything Stage 0 already guarantees. The deterministic platform still
owns operational truth; the Operations Agent only *reads* projections of that truth through narrow, focused
endpoints, and it never bypasses `EnergyHub.Api` or `CommandCenter.Api`.

```text
CommandCenter.Web -> OperationsAgent.Api -> EnergyHub.Api   (asset state, recent activity)
                                          -> CommandCenter.Api (customer report, incident context)

OperationsAgent.Api -> Microsoft Foundry project (model reasoning + function-tool orchestration)
```

Boundaries preserved and added in this stage:

- `OperationsAgent.Api` is a new, independently hosted, read-only service. It has its own focused
  `OperationsAgent.Contracts` project, exactly like every other Stage 0 boundary.
- `OperationsAgent.Api` calls the **existing** `EnergyHub.Api` and `CommandCenter.Api` REST endpoints. It does not
  duplicate their business logic, and it has no reference to `SmartPole.Contracts` or `SmartPole.Simulator.Api`.
- Two narrow read endpoints were added only where evidence did not already have a standalone read surface:
  `GET /api/command-center/customer-report/{assetId}` and `GET /api/command-center/incidents/current/{assetId}`.
  `EnergyHub.Api` already exposed asset-state and activity reads from Stage 0, so nothing changed there.
- A new `DemoStage` runtime model (`Deterministic`, `InvestigationAgent`) is coordinated by `DemoScenario.Api`
  (mirroring how it already coordinates `ScenarioId`) and propagated to `CommandCenter.Api`, which now carries the
  current stage inside `CommandCenterSnapshot`. No service needs to restart to change stage.
- `CommandCenter.Web` reads the propagated stage from its snapshot and shows the Investigation panel only while
  `InvestigationAgent` is active; the deterministic map, report, incident, workflow, and event stream stay exactly
  as they were.
- `DemoControl.Web` becomes a presenter switchboard: current stage, its enabled capabilities, and buttons to switch
  between `Deterministic` and `Investigation Agent`.

## Architecture boundaries (explicit)

- The Operations Agent is **strictly read-only**. Its toolset (`OperationsToolset`) exposes exactly four tools —
  `get_customer_report`, `get_energy_asset_state`, `get_energy_recent_activity`, `get_incident_context` — and none
  of them can write, restore, command, apply a scenario, or reach the vendor/device simulator layer.
- `OperationsAgent.Api` never calls `SmartPole.Simulator.Api` and never references `SmartPole.Contracts`.
- `Caesarea.ServiceDefaults` remains independent of every domain/contract project, including the new ones.
- The agent's model and instructions are ephemeral (Microsoft Agent Framework's ephemeral-agent pattern): nothing is
  persisted as a Foundry agent resource. The agent definition ships with the application code.

## Coding-agent prompt

```text
Read the architecture documents under /docs and the repository guidance before making changes.

Use the installed agentic-architecture-router and microsoft-foundry skills when they apply.

Extend the deterministic Caesarea application with Stage 1: a read-only Investigation Agent, without
weakening any Stage 0 behavior.

New requirement:
An operator can press "Investigate" for L-417 and receive a structured, evidence-grounded investigation:
verified facts, ranked hypotheses with confidence and reason, missing evidence, an ordered tool/evidence
trace, a summary, and a correlation identifier.

Implement:

1. A DemoStage runtime model with at least Deterministic and InvestigationAgent, coordinated centrally
   (DemoScenario.Api) and propagated to CommandCenter.Api without restarting any service.
   - DemoControl.Web becomes a presenter switchboard: show the current stage, its capabilities, and let the
     presenter switch between Deterministic and Investigation Agent.
   - Resetting the deterministic scenario must not silently change the selected stage.

2. A focused OperationsAgent.Contracts project and a new, independently hosted OperationsAgent.Api service,
   wired into Caesarea.slnx and the Aspire AppHost.
   - Expose one read-only POST /api/operations-agent/assets/{assetId}/investigate endpoint.
   - Return verified facts, hypotheses (confidence + reason), missing evidence, an ordered tool/evidence
     trace, a summary, a correlation identifier, and timestamps/status.

3. Use the Microsoft Agent Framework in .NET (Microsoft.Agents.AI.Foundry, Azure.AI.Projects, Azure.Identity,
   Microsoft.Extensions.AI) against the existing Microsoft Foundry project endpoint and the
   gpt-5.2-chat model deployment, through typed IConfiguration options and DefaultAzureCredential.
   - The application must still start in Deterministic mode if Azure authentication or model invocation is
     unavailable. An investigation request must surface a clear failure rather than fabricate an answer.

4. Give the agent four narrow, read-only function tools: current customer report, authoritative Energy Hub
   asset state, Energy Hub recent activity, and existing Command Center incident context.
   - Add narrow read endpoints only where one does not already exist.
   - Every tool must validate its inputs, propagate the correlation identifier, log structured events, and
     record an evidence-trace entry.
   - The agent must be genuinely agentic: it decides which tool to call next, and in what order, based on
     intermediate evidence. Do not prefetch all evidence deterministically before the model runs.

5. Force evidence discipline in the agent's instructions: separate verified facts from hypotheses, never
   fabricate, disclose missing evidence, never claim root cause without support, never request or execute a
   state change, and respond only with strict JSON matching the response schema.
   - Parse and validate the raw model response tightly. Do not use a broad catch or a success-shaped
     fallback; surface an honest failure instead.

6. Add a projector-friendly Investigation panel to CommandCenter.Web, visible only in the InvestigationAgent
   stage: a prominent Investigate button, agent identity/status, verified facts, hypotheses, missing
   evidence, and the ordered tool/evidence trace. Keep the deterministic map, report, incident, workflow, and
   event stream visible and functional. Use the existing Clawpilot (--cp-*) theme variables only.

Do NOT add:
- Common/Shared.Contracts, Dapr, a message broker, MCP;
- organizational/work knowledge sources;
- a corrective workflow or any state-changing agent action;
- Entra Agent Identity or authorization policy mechanics;
- multi-agent collaboration;
- future-stage mechanisms beyond declaring the DemoStage enum member if useful.

Add tests for: stage switching/propagation; contract/architecture boundaries (no SmartPole reference, no
write tools); agent result parsing/validation without a live model; read-only tool trace behavior; and
API-adjacent HTTP client behavior. All Stage 0 tests must remain passing.

Before coding:
1. Summarize the architectural impact in no more than 10 lines.
2. List the files/components you plan to add or change.
3. Then implement.

After coding:
1. Build the solution.
2. Run relevant tests.
3. Summarize exactly what changed.
4. State that DemoStage=InvestigationAgent enables the capability.
```

## Demo flow

1. Open `DemoControl.Web` and confirm the switchboard shows **Deterministic** as the current stage.
2. Select the **Forgotten Override** scenario as in Stage 0 — the deterministic story is unchanged.
3. In the switchboard, press **Investigation Agent**. Observe the stage change propagate to `CommandCenter.Web`
   without restarting anything.
4. Open `CommandCenter.Web`. The deterministic map, customer-report pin, incident panel, workflow, and event
   stream are still there. A new **Operations Agent** panel now appears.
5. Press **Investigate**. The agent reads the current customer report, the Energy Hub asset state, recent Energy
   Hub activity, and the incident context (in whatever order it decides), then returns verified facts, a ranked
   hypothesis (for example, that the manual override left over from maintenance explains the anomaly), any missing
   evidence, and the ordered evidence trace.
6. Switch back to **Deterministic** in `DemoControl.Web` and confirm the Investigation panel disappears while
   everything else keeps working.
7. Press **Reset to Normal Operation** in `DemoControl.Web` and confirm the selected stage is preserved — reset
   only restores the deterministic scenario data, not the presenter's stage choice.

## Code tour

- `Contracts/DemoScenario.Contracts/StageModels.cs` — `DemoStage`, `DemoStageDescriptor`, `DemoStageStatus`,
  `DemoStageCatalogResponse`, `DemoStageChangeResult`.
- `Services/DemoScenario.Api/Services/StageCatalog.cs`, `StageCoordinator.cs`, `StageClients.cs` — the stage
  catalog, the coordinator that applies and propagates a stage change, and the HTTP client that pushes it to
  `CommandCenter.Api` (mirrors `ScenarioCatalog`/`ScenarioCoordinator`/`ScenarioClients.cs`).
  New endpoints: `GET /api/demo-stage`, `GET /api/demo-stage/current`, `POST /api/demo-stage/apply/{stage}`.
- `Services/CommandCenter.Api/Services/CommandCenterModules.cs` — new `StageContextModule` holding the current
  stage; `CommandCenterService.GetCurrentStage`/`ApplyStage` and the stage field on `CommandCenterSnapshot`.
  New endpoints: `GET /api/command-center/stage`, `POST /api/command-center/admin/stage`,
  `GET /api/command-center/customer-report/{assetId}`, `GET /api/command-center/incidents/current/{assetId}`.
- `Apps/DemoControl.Web` — `Services/DemoStageApiClient.cs` and the switchboard section in `Home.razor`.
- `Contracts/OperationsAgent.Contracts/OperationsAgentModels.cs` — the public `InvestigationResult` contract:
  `VerifiedFact`, `Hypothesis`, `EvidenceTraceEntry`, `InvestigationStatus`.
- `Services/OperationsAgent.Api` — the new service:
  - `Configuration/OperationsAgentApiOptions.cs` — Energy Hub/Command Center base URIs, the Foundry project
    endpoint, model deployment name, and agent name, all validated at startup.
  - `Services/EnergyReadGateway.cs`, `CommandCenterReadGateway.cs` — narrow, read-only, correlated HTTP gateways
    to the existing Energy Hub and Command Center endpoints.
  - `Services/OperationsToolset.cs` — the four read-only function tools, each validating its bound context,
    propagating the correlation identifier, logging, and recording an `EvidenceTraceEntry`.
  - `Services/InvestigationAgentInstructions.cs` — the fixed evidence-discipline system prompt.
  - `Services/ModelInvestigationResponse.cs`, `InvestigationResponseParser.cs`,
    `InvestigationResponseFormatException.cs` — the strict JSON schema requested from the model and its
    dependency-free, fully unit-testable parser/validator.
  - `Services/IInvestigationAgentRunner.cs`, `FoundryInvestigationAgentRunner.cs` — the swappable reasoning step;
    the Foundry implementation wraps `AIProjectClient.AsAIAgent(...)` as an ephemeral `AIAgent` with the four tools
    and `AgentRunOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema<ModelInvestigationResponse>(...)`.
  - `Services/InvestigationService.cs` — orchestrates one investigation and assembles the public
    `InvestigationResult` from the model response and the recorded evidence trace.
  - `Program.cs` — `POST /api/operations-agent/assets/{assetId}/investigate`, mapping
    `InvestigationResponseFormatException`/`HttpRequestException`/`RequestFailedException` to clear
    `ProblemDetails` failures instead of a fabricated result.
- `Apps/CommandCenter.Web` — `Services/OperationsAgentApiClient.cs` and the Investigation panel added to
  `Home.razor`/`app.css`, visible only when `CurrentStage.Id == DemoStage.InvestigationAgent`.
- `Caesarea.AppHost/AppHost.cs` — `operationsagent-api` wired to `energyhub-api` and `commandcenter-api`;
  `commandcenter-web` now also references `operationsagent-api`.

## Tests

- `StageCoordinatorTests` — default stage, successful propagation, and that a failed propagation does not corrupt
  the coordinator's current stage.
- `CommandCenterServiceTests` (additions) — stage propagation into the snapshot and that `Reset` preserves the
  selected stage.
- `InvestigationResponseParserTests` — accepts well-formed and empty-evidence responses; rejects empty/malformed
  JSON, a missing summary, out-of-range confidence, and blank evidence entries — all without a live model call.
- `OperationsToolsetTests` — each tool validates its bound context, propagates the correlation identifier, and
  records a trace entry on both success and downstream failure, without fabricating evidence.
- `InvestigationServiceTests` — a fake `IInvestigationAgentRunner` proves the orchestration assembles
  `InvestigationResult` correctly, including a trace built purely from the tools the fake chose to call.
- `OperationsAgentReadGatewayTests` — HTTP-level tests for the new read gateways using a recording message handler,
  matching the Stage 0 `HttpGatewayTests` pattern.
- `ArchitectureBoundaryTests` (additions) — `OperationsAgent.Api` never references `SmartPole.Simulator.Api`/
  `SmartPole.Contracts`; its toolset exposes only `get_*` tools and contains no write/restore/command tokens; its
  `Program.cs` maps no `PUT`/`DELETE`/admin endpoints.
- `ValidationAndOptionsTests` (additions) — `OperationsAgentApiOptions` rejects invalid service URIs and a
  non-`https` Foundry endpoint.

All 32 Stage 0 tests keep passing unmodified in behavior; only two Stage 0 test call sites needed a new
constructor parameter (`StageContextModule`) to match the extended `CommandCenterService` constructor.

## Explicit exclusions

- Common/Shared.Contracts or any miscellaneous shared-contracts project
- Dapr and any message broker (no Azure Service Bus)
- MCP
- Organizational/work knowledge sources (`IWorkKnowledgeSource`, Work IQ)
- A corrective workflow, workflow engine, or any agent-initiated state change
- Direct agent writes to the Energy Hub, Command Center, or SmartPole
- Microsoft Entra Agent Identity and authorization-policy mechanics
- Multi-agent collaboration (Security Agent, Agent-as-Tool)
- Event-driven agent activation
- ASSERT behavioral evaluation
- Session/conversation continuity across investigations (each Investigate press is a fresh, independent run)

## Teaching point

> **A read-only agent still has to earn its authority. Evidence discipline — verified facts, honest hypotheses,
> disclosed gaps — matters more than a confident-sounding answer.**

> **The stage switch is a presenter concern, not a business capability. Nothing about the deterministic platform
> changed; we only added a new lens for looking at the same authoritative truth.**

## Demo stage

```text
DemoStage=InvestigationAgent
```
