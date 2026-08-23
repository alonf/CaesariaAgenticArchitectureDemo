# Caesarea Agentic Architecture Demo

The demo runs as **one cumulative application** with a runtime `DemoStage` that presenters switch through
`DemoControl.Web` without restarting any service:

- `DemoStage=Deterministic` — the conference-ready Stage 0 smart-city demo. No LLM, agent, MCP, or AI credential.
- `DemoStage=InvestigationAgent` — adds the read-only Operations Agent investigation capability on top of Stage 0.

The selected stage is coordinated by `DemoScenario.Api` and propagated live to `CommandCenter.Api`/`CommandCenter.Web`;
switching stages never requires restarting `dotnet run --project .\Caesarea.AppHost`.

## Architecture boundaries

- `Services\EnergyHub.Api` is the authoritative lighting boundary.
- `Services\SmartPole.Simulator.Api` simulates the vendor/device system behind Energy Hub.
- `Services\CommandCenter.Api` aggregates snapshots, incidents, and activity without calling SmartPole directly.
- `Services\DemoScenario.Api` coordinates repeatable presenter scenarios and the current `DemoStage` across the deterministic services.
- `Services\OperationsAgent.Api` is the read-only Operations Agent boundary. It reads the current customer report and incident context from `CommandCenter.Api` and the authoritative asset state/activity from `EnergyHub.Api`; it never calls SmartPole and has no write/command tool.
- `Shared\CanonicalModel` contains only canonical operational concepts.
- API DTO ownership stays focused in `Contracts\SmartPole.Contracts`, `Contracts\Energy.Contracts`, `Contracts\CommandCenter.Contracts`, `Contracts\DemoScenario.Contracts`, and `Contracts\OperationsAgent.Contracts`.
- `Caesarea.ServiceDefaults` stays independent of domain and transport contract projects.

## Stage 1 — Investigation Agent configuration

`Services\OperationsAgent.Api` reads its Microsoft Foundry configuration from `OperationsAgentApi` in
`appsettings.json` (or environment/user-secrets overrides):

```json
{
  "OperationsAgentApi": {
    "EnergyHubBaseUri": "https+http://energyhub-api",
    "CommandCenterBaseUri": "https+http://commandcenter-api",
    "FoundryProjectEndpoint": "https://alonlecturedemo-resource.services.ai.azure.com/api/projects/alonlecturedemo",
    "ModelDeploymentName": "gpt-5.2-chat",
    "AgentName": "Operations Agent"
  }
}
```

`FoundryProjectEndpoint` and `ModelDeploymentName` are non-secret development defaults; authentication uses
`DefaultAzureCredential` (for example, an `az login` session), so no key or secret is stored anywhere. Both
`AIProjectClient` construction and `DefaultAzureCredential` are lazy: the API still starts cleanly in
`Deterministic` mode even when no Azure credential is available. An `Investigate` request only fails, with a
clear `ProblemDetails` error, if authentication or model invocation cannot succeed at request time.

See [docs/prompts/01-investigation-agent.md](docs/prompts/01-investigation-agent.md) for the full lecture-ready
walkthrough of this stage.

## Quality commands

```powershell
dotnet restore
dotnet format
dotnet build
dotnet test
dotnet test --settings .\coverage.runsettings --collect:"XPlat Code Coverage" --results-directory .\TestResults
git diff --check
```

## Run locally

```powershell
dotnet run --project .\Caesarea.AppHost
```

Open `DemoControl.Web` to select deterministic scenarios and switch the presenter switchboard between
`Deterministic` and `Investigation Agent`. Open `CommandCenter.Web` to see the operational view; the Investigation
panel only appears while `Investigation Agent` is the active stage.

## Coverage artifact

After the coverage command finishes, inspect the generated Cobertura file under:

```text
.\TestResults\<run-id>\coverage.cobertura.xml
```

The coverage profile measures executable deterministic API code. It excludes DTO-only contract assemblies, generated OpenAPI code, and composition-root `Program.cs` files; those surfaces are protected by clean builds, analyzers, and architecture tests.
