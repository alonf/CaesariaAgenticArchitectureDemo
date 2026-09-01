# Caesarea Agentic Architecture Demo

A complete, stage-by-stage **agentic system in .NET**, built with C# and the **Microsoft Agent
Framework (MAF)** on Microsoft Foundry. The story: the fictional smart city of Caesarea runs a
deterministic Command & Control center — and capability by capability, an Operations Agent grows
inside it: reasoning, tools, sessions, knowledge retrieval, memory, skills, and MCP boundaries,
each added live at the flip of a presenter switch.

This repository is the companion code for two **Visual Studio Live! San Diego 2026** sessions —
but it is written to stand on its own: **if you want to learn how to build agentic systems with
MAF in C#, this codebase and its stage-by-stage documentation are for you.** Every capability is
a small, reviewable increment with the exact SDK code in a marked region, deterministic tests,
and a documented lecture beat explaining *why* it is built that way.

## The lectures

### H08 — Developing Agentic Systems in .NET: From Concept to Code

*Thursday, September 17, 2026 · [session page](https://vslive.com/events/san-diego-2026/sessions/thursday/h08-agentic-systems.aspx)*

> Chatbots just talk; Agents act. We are leaving the era of passive AI assistants and entering the
> age of Agentic Systems — intelligent applications that perceive, plan, and execute complex
> goals. For the .NET developer, this is not just a new library; it is a fundamental architectural
> shift.
>
> In this practical, code-focused session, we will use C# and the Microsoft Agent Framework (MAF)
> to build robust, autonomous agents. We will move rapidly from theory to implementation, telling
> the story of a production-ready agent through its four essential components. We start by
> constructing the **Brain**, implementing reasoning loops, memory, and state management using
> standard .NET abstractions. Once the agent can think, we give it **Hands** to interact with the
> real world, using the MAF function call capability and the Model Context Protocol (MCP) to
> connect to APIs, databases, and local systems securely. As needs grow, we scale from a single
> agent to a **Team**, demonstrating how to orchestrate complex multi-agent workforces using
> patterns like Group Chat and Handoffs. Finally, we establish the **Habitat**, exploring diverse
> strategies for deploying stateful agents — whether self-hosted in ASP.NET Core and Azure
> Functions, or running on the fully managed Microsoft Agent Service (Foundry). We will conclude
> by securing this digital fleet with Agent 365, demonstrating the critical role of auditing and
> governance in the enterprise.

### W20 — The Agentic Revolution: From Code Builders to System Rulers

*Wednesday, September 16, 2026 · [session page](https://vslive.com/events/san-diego-2026/sessions/wednesday/w20-agentic-revolution.aspx)*

> We are standing at the event horizon of a vertical shift in software engineering. The transition
> from Generative AI to Agentic Systems is not just a platform upgrade; it is a fundamental
> rewrite of our profession, moving us from deterministic code builders to probabilistic "System
> Rulers." In this session, we will navigate the 2026 landscape where AI is no longer just a
> tool, but a universal employee that requires a new kind of governance. We will explore how to
> tame the chaos of "vibe coding" through Spec-Driven Engineering — a rigorous methodology in
> which natural-language specifications serve as the single source of truth for autonomous
> agents. Beyond theory, we will architect the complete solution using the Microsoft Agents
> Universe: designing in Foundry, deploying stateful resources via the Agent Service, and
> connecting tools with the Model Context Protocol (MCP). Finally, we will address the critical
> "Anti-Shadow AI" challenge, demonstrating how to secure your digital fleet with Entra ID and
> Agent 365.

## About the presenter

**Alon Fliess** is a **Microsoft Regional Director** (since 2010) and **Microsoft Foundry MVP**
(MVP since 2005) with over 30 years of experience in software architecture. As the **CTO and
Founder of ZioNet**, he specializes in Azure, AI integration, and scalable microservices systems.
A frequent speaker at major global conferences — including a featured lecture at **Microsoft
Build 2025** and serving as a Subject Matter Expert at the Microsoft Foundry booth at **Ignite
2025** — Alon is the author of *Architecting Scalable Solutions*, a recognized community leader,
and leads the Azure Israel developer community.

## Who this repository is for

Anyone learning to build agentic systems with the **Microsoft Agent Framework in C#**. The demo
grows one capability at a time, and each stage maps to a concrete MAF concept:

| Stage | MAF concept demonstrated |
| --- | --- |
| InvestigationAgent | `AIProjectClient.AsAIAgent(...)` + one `AIFunctionFactory` tool |
| Session | `AgentSession` serialize/deserialize — conversational context, safely stored |
| Knowledge | `TextSearchProvider` with on-demand function calling (RAG as a context provider) |
| Memory | A hand-written `AIContextProvider` — recalled cases as explicitly framed hypotheses |
| Skills | `AgentSkillsProvider` — progressive disclosure of documented, auditable procedures |
| McpTools | MCP server (`ModelContextProtocol.AspNetCore`) + runtime tool discovery (`McpClient`) |
| InteractiveInput | MCP Multi Round-Trip Requests — `InputRequiredException` + elicitation handler |
| Workflow | `Microsoft.Agents.AI.Workflows` — code-built graph (`WorkflowBuilder`), approval-gate node, self-rendered diagram |
| ToolApproval | `ApprovalRequiredAIFunction` — the model selects a protected capability and the framework intercepts it |
| MultiAgent | A second agent with its own permission boundary, consulted as a remote capability (`AsAIFunction` over MCP) |

Each stage has a build-and-design document under [docs/prompts/](docs/prompts/), the exact
lecture-slide code lives in named `#region` blocks (see the deck anchors in the docs), and
[docs/product-status/](docs/product-status/) records API-drift notes and review history.

## The cumulative demo stages

The demo runs as one application with a presenter-controlled `DemoStage`:

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
- `DemoStage=InteractiveInput` — the first write-capable tool: `restore_scheduled_mode` over MCP
  with **Multi Round-Trip Requests (MRTR)**. The tool pauses input-required for explicit operator
  approval and produces no side effect before the input arrives; deny and the state provably
  does not change.
- `DemoStage=MultiAgent` — a **second agent for a real boundary**. The Security Hub holds records the
  Operations Agent may not read (an architecture test pins that it has no route to them at all),
  so it consults a Security Operations Agent and receives a sanitized judgment while keeping
  ownership of the answer. Turn the consult off and the same question yields a thinner answer:
  the lamp is intentional, but the reason belongs to a domain that will not disclose it.
- `DemoStage=ToolApproval` — the third human-control point, and the reactive one: the agent may
  decide by itself to file a maintenance work item, and `ApprovalRequiredAIFunction` makes the
  framework intercept that call so a supervisor approves before it runs. MRTR was the tool asking,
  the workflow gate was a node we drew; here the model chooses and policy intercepts.
- `DemoStage=Workflow` — remediation becomes an **explicit code-built workflow**: validate,
  policy, an operator-approval gate when a manual override would be cleared, execute, verify, and
  a maintenance work item when the correction does not hold. The agent stops writing and starts
  *requesting* the operation; the command carries the state revision it was decided on, so a
  picture that moved is refused rather than acted on. The graph renders its own Mermaid diagram
  (`WorkflowVisualizer`), live steps stream to the Command Center, and the equivalent declarative
  YAML is displayed beside it.

`CommandCenter.Api` owns the selected stage. `DemoScenario.Api` reads and changes it through that authoritative
boundary, so switching stages does not restart the application.

## Architecture boundaries

- `EnergyHub.Api` owns authoritative lighting state — and serves its streetlight tool over MCP at `/mcp`.
- `SmartPole.Simulator.Api` simulates the physical/vendor system behind Energy Hub.
- `CommandCenter.Api` aggregates the deterministic operational view without calling SmartPole directly.
- `DemoScenario.Api` applies synthetic presenter scenarios.
- `OperationsAgent.Api` hosts one general, read-only **Caesarea Operations Agent**.
- `SecurityHub.Api` owns active security operations, including restricted detail.
- `SecurityAgent.Api` hosts the **Security Operations Agent** — the only service that may read the
  Security Hub, published to other domains as a consult capability.

The agent has no direct SmartPole access and no write capability below the InteractiveInput stage.
In that stage's window the single write tool (`restore_scheduled_mode`) exists only over MCP and
only behind an interactive operator approval — the tool pauses before any side effect. At the
Workflow stage even that is withdrawn: the agent asks the governed operation to start, and the
workflow owns validation, policy, approval, execution, and verification.
Deterministic Hubs keep operational authority at every stage: sessions are context, memory is
hypothesis, knowledge is evidence, and skills are procedure — the agent's answers cite which is
which.

## Configuration

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
`AIProjectClient.AsAIAgent(...)`. The API can start without an Azure credential; authentication is
required only when the operator asks the agent a question. To run against your own Foundry
project, change `FoundryProjectEndpoint` and `ModelDeploymentName`.

## Run locally

```powershell
dotnet run --project .\Caesarea.AppHost
```

Use `DemoControl.Web` (the presenter switchboard) to pick a scenario and stage, then open
`CommandCenter.Web` and ask: **"Is streetlight L-417 on?"** — and follow the stage documents in
[docs/prompts/](docs/prompts/) for each lecture beat.

## Quality commands

```powershell
dotnet restore
dotnet format
dotnet build
dotnet test --project Tests/Caesarea.Deterministic.Tests/Caesarea.Deterministic.Tests.csproj
git diff --check
```

## License

Licensed under the [MIT License](LICENSE).
