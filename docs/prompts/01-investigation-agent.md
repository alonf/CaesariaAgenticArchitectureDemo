# Stage 1 — First Agent

## Purpose

Introduce the smallest useful Microsoft Agent Framework example without changing Stage 0 ownership boundaries.

The audience should see three ideas:

1. The agent has a stable, general identity: **Caesarea Operations Agent**.
2. A tool is an ordinary C# function.
3. The model decides to invoke that tool when the operator asks for current operational state.

## Requirement

The operator asks:

> **Is streetlight L-417 on?**

The agent uses one read-only tool:

```text
get_streetlight_state
```

That tool calls the existing Energy Hub REST API and returns its authoritative operational state. The agent then
answers in natural language.

## Architecture

```text
CommandCenter.Web
        |
        v
OperationsAgent.Api
        |
        +-- Caesarea Operations Agent
                |
                +-- get_streetlight_state
                            |
                            v
                       EnergyHub.Api
```

- Energy Hub remains the system of record.
- The agent can read but cannot write, restore scheduled mode, apply scenarios, or call SmartPole.
- The agent is general; L-417 is only the first example.
- No capability registry, investigation framework, evidence schema, domain router, knowledge source, Skill, MCP,
  workflow, or multi-agent design is introduced in Stage 1.

## Projector-visible code

```csharp
var energyTools = new EnergyTools(energyReadGateway, correlationId, logger);

AIAgent agent = projectClient.AsAIAgent(
    model: modelName,
    name: "Caesarea Operations Agent",
    instructions: """
        You are the Caesarea Operations Agent for the city Command & Control center.
        Answer operator questions using the tools available to you.
        Use authoritative operational systems for current city state.
        Do not invent operational facts.
        """,
    tools:
    [
        AIFunctionFactory.Create(
            energyTools.GetStreetlightStateAsync,
            EnergyTools.StreetlightStateToolName,
            "Gets the current authoritative operational state of a streetlight.")
    ]);
```

```csharp
[Description("Gets the current authoritative operational state of a streetlight.")]
public Task<EnergyOperationalTwin> GetStreetlightStateAsync(
    [Description("The streetlight asset identifier, for example L-417.")]
    string assetId,
    CancellationToken cancellationToken)
{
    return energyHub.GetStateAsync(assetId, correlationId, cancellationToken);
}
```

Operational loop and timeout limits remain hosting safeguards rather than a teaching abstraction.

## Demo flow

1. Show Stage 0 and the deterministic L-417 state.
2. In Demo Control, switch to **First Agent**.
3. Show that the deterministic Command Center remains unchanged.
4. Ask the agent: **“Is streetlight L-417 on?”**
5. Show `FoundryOperationsAgent.cs` - one general agent - and `Capabilities/StreetlightToolsCapability.cs`,
   the one tool registration. Each later stage adds a capability file beside it, never a branch in
   the agent.
6. Show `EnergyTools.cs`: one ordinary C# method reading Energy Hub.
7. Explain that the model selected the tool; Energy Hub still owns the answer.

## Later stages

- **Stage 2:** give the same agent more evidence sources so it can answer “Why?”
- **Stage 3:** add a runtime lighting-investigation Skill that teaches a reusable procedure.
- **Stage 4:** introduce MCP when Energy Hub needs a reusable interoperable capability surface.
- **Later:** add governed consequential action and durable workflow.

> **Tools tell the general agent what it can do. Skills teach it how to approach a particular kind of problem.**
