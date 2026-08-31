# Caesarea Agentic Architecture Demo

The demo runs as one cumulative application with a presenter-controlled `DemoStage`:

- `DemoStage=Deterministic` — the Stage 0 smart-city system with no model or agent.
- `DemoStage=InvestigationAgent` — adds the first, intentionally minimal Caesarea Operations Agent.
- `DemoStage=Session` — adds conversational context (`AgentSession`), so a follow-up like
  **"Why?"** refers to the previous question. Session state is never the authoritative city state.
- `DemoStage=Knowledge` — adds on-demand organizational knowledge retrieval
  (`search_work_knowledge`), so **"Why?"** gets an evidence-grounded explanation citing the
  seeded work order WO-8732 and its technician note.
- `DemoStage=Memory` — adds case memory: closed investigations are recalled in later sessions as
  explicitly framed **hypotheses** (asking about the L-528 fixture recalls the closed L-417 case).
  Memory is never evidence: live state is still verified and real evidence still searched.
- `DemoStage=Skills` — adds documented procedures: the agent discovers `skills/*/SKILL.md`,
  loads one on demand (`load_skill`), and the investigation follows the expert-authored triage
  guide and the mandated Caesarea Incident Brief format. Edit the markdown, re-run, and the
  behavior changes — skills are auditable configuration, not code.
- `DemoStage=McpTools` — the Energy Hub serves `get_streetlight_state` over the Model Context
  Protocol at its own boundary, and a presenter toggle (`Tools: LOCAL / MCP`) switches the agent
  between the compiled-in function and runtime discovery. Same capability, same behavior, new
  boundary.

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
