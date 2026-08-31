# Caesarea Agentic Architecture Demo

The demo runs as one cumulative application with a presenter-controlled `DemoStage`:

- `DemoStage=Deterministic` — the Stage 0 smart-city system with no model or agent.
- `DemoStage=InvestigationAgent` — adds the first, intentionally minimal Caesarea Operations Agent.

`CommandCenter.Api` owns the selected stage. `DemoScenario.Api` reads and changes it through that authoritative
boundary, so switching stages does not restart the application.

## Architecture boundaries

- `EnergyHub.Api` owns authoritative lighting state.
- `SmartPole.Simulator.Api` simulates the physical/vendor system behind Energy Hub.
- `CommandCenter.Api` aggregates the deterministic operational view without calling SmartPole directly.
- `DemoScenario.Api` applies synthetic presenter scenarios.
- `OperationsAgent.Api` hosts one general, read-only **Caesarea Operations Agent**.
- Stage 1 gives that agent exactly one ordinary C# function tool: `get_streetlight_state`.

The agent reads Energy Hub through its existing REST API. It has no write tool, no direct SmartPole access, no
capability registry, no domain-specific investigation logic, no strict evidence schema, and no runtime Skill yet.

## Stage 1 configuration

```json
{
  "OperationsAgentApi": {
    "EnergyHubBaseUri": "https+http://energyhub-api",
    "FoundryProjectEndpoint": "https://alonlecturedemo-resource.services.ai.azure.com/api/projects/alonlecturedemo",
    "ModelDeploymentName": "gpt-5.5",
    "AgentName": "Caesarea Operations Agent"
  }
}
```

Authentication uses `DefaultAzureCredential`; no key is stored. The agent is created in code with
`AIProjectClient.AsAIAgent(...)` and an `AIFunctionFactory` tool. The API can start without an Azure credential;
authentication is required only when the operator asks the agent a question.

See [docs/prompts/01-investigation-agent.md](docs/prompts/01-investigation-agent.md) for the lecture flow.

## Run locally

```powershell
dotnet run --project .\Caesarea.AppHost
```

Use `DemoControl.Web` to select **First Agent**, then open `CommandCenter.Web` and ask:
**“Is streetlight L-417 on?”**

## Quality commands

```powershell
dotnet restore
dotnet format
dotnet build
dotnet test
git diff --check
```
