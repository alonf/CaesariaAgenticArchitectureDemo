# Lab 02 — first function tool

Outcome: prove that a model selected a narrow function, code read the Energy Hub, and the result became context for a second model exchange. Allow 25–35 minutes. Application baseline `843e0d0`; .NET 11.0.100, Aspire 17.0.0. The patterns also apply with .NET 10 LTS. Use Lab 01 and the root README for platform setup and trusted HTTPS.

## Prepare

```bash
dotnet run --project Caesarea.AppHost/Caesarea.AppHost.csproj
```

Use Aspire's service links (ports below are the recorded defaults). In Director select **Investigation Agent**, confirm, then **Lights On Reported by a Client**. Open the main Command Center and top-right **Walkthrough**. Model calls require an existing configured Foundry project/model deployment and an authenticated identity with access. Do not deploy a new resource for this lesson.

Before asking, save the Hub state:

```bash
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > before.json
```

Expect `reportedIsOn=true`, `manualOverride=true`, `isDaylight=true`, `expectedScheduledState=false`, healthy controller. Reset if not.

## Execute in Command Center

Ask **Is streetlight L-417 on?** Read the answer and capability trace. In the recording: `get_streetlight_state({"assetId":"L-417"})`, marked **RAN**, one tool call over two model round trips. Counts can vary; inspect actual behavior rather than requiring exact wording.

Read the Hub again:

```bash
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > after.json
```

Expected: no repair; reported/desired state, override, revision and last-command identity unchanged. The physical pole is simulated. The model response alone does not establish operational truth.

## Debug the boundary

Director → Advanced controls → Operations Agent → arm **FUNCTION_TOOL** → **Attach VS Code**. Ask again in the main app. VS Code works on Windows/macOS; Visual Studio 2026 is Windows only.

Inspect `Services/OperationsAgent.Api/Services/EnergyTools.cs`, `GetStreetlightStateAsync` (baseline line44 pause; line50 Hub delegate). `Description` on method/parameter provides metadata. Registration actually happens in `Services/OperationsAgent.Api/Services/Capabilities/StreetlightToolsCapability.cs` through `AIFunctionFactory.Create`, not by the attribute alone.

Step into `EnergyReadGateway.GetStreetlightStateAsync`. Find the focused GET request and correlation header. Set a temporary breakpoint in `FoundryOperationsAgent.cs` immediately after `RunAsync` and approval resolution (baseline292). Continue. In Watch inspect:

```csharp
question
response.Text
modelFlightRecorder.Exchanges
```

Expand the two exchanges. For full readable text, evaluate these **read-only expressions in Debug Console**, not a shell:

```csharp
modelFlightRecorder.Exchanges[0].Received[0]
modelFlightRecorder.Exchanges[1].Sent[0]
modelFlightRecorder.Exchanges[1].Received[0]
```

Expected for this recorded run: tool request → Hub JSON tool result → natural-language answer. These are observable messages, not private model reasoning. Remove the temporary breakpoint, continue, then **Detach VS Code** through Director.

## Failure and recovery

A long breakpoint pause can exceed the web caller's 100-second timeout. This happened during recording. The debugger request and later successful uninterrupted UI request are separate executions. Continue first; inspect correlated logs and Hub state before retrying any command. This lesson's tool is read only. Do not generalize this retry advice to mutations.

If the agent fails before a tool runs, inspect **Aspire → operationsagent-api → logs**. During preparation a previous validation process used `foundry.example.invalid`. Restarting the local AppHost with the existing configured `OperationsAgent__FoundryProjectEndpoint` and `OperationsAgent__ModelDeploymentName` resolved it. Use your own existing endpoint/deployment; never put access tokens in scripts or recordings. An authentication/endpoint problem is not proof that the service needs redeployment.

## Reset and investigate

Reapply **Lights On Reported by a Client** in Director; verify the initial fields above. Detach debuggers before the next lesson. No lamp repair is required here.

1. Where does the stage determine which tools exist? Could a model invoke an unregistered repair method?
2. What does the method description tell the model, and where is the parameter schema produced?
3. Trace correlation across the function, gateway and Hub. What would you check if the UI timed out?
4. In the Agent Framework documentation, find the function-invocation loop. Compare it with these recorded exchanges. Use an AI chat to propose an explanation, then verify each claim against code and traces.

Book alignment: chapter 7, agents/tools/sessions. Course slide14: context engineering. Continue with Lab03: conversation history is context, not a fresh read.
