# Unit 00 — understand, run, and debug Caesarea

Complete this onboarding before Lab 01. The outcome is a running, inspectable agent request;
the lamp remains on. Allow 45–60 minutes including first-time setup. Use the companion book for
the architecture rationale and the H08 deck for diagrams. This checkout supplies the executable
commands and API signatures.

## Explain the boundaries first

- A city is a system of independently owned systems. Domain Hubs normalize their interfaces.
- The Energy Hub owns the operational twin: reported state, desired state, schedule, override,
  health, revisions and command receipts. A simulator stands in for the physical controller.
- Command Center owns the operator interaction and incident workflow. An agent reads through
  focused tools and requests permitted actions; it never becomes the operational source of truth.
- Microsoft Agent Framework composes model interaction, instructions, tools, context, sessions
  and workflows. You still design the permissions, evidence boundaries and verification.

Checkpoint: explain why an agent saying “done” cannot prove that a lamp changed.

## 1. Clone the exact course baseline

```powershell
git clone https://github.com/alonf/CaesariaAgenticArchitectureDemo.git
cd CaesariaAgenticArchitectureDemo
git fetch origin upgrade/net11-foundry-insights-routines
git switch --detach 89d5a513be97301857dd4f156ce0773103712d51
git rev-parse HEAD
```

Use a separate clone if you already have work in progress. Do not substitute `main` or reset an
existing working tree. If this commit is unavailable, obtain the instructor's baseline; this task
does not publish a bundle. The course production branch itself is local.

Prerequisites: Git, PowerShell 7, .NET SDK selected by `global.json` (11.0.100 with compatible
feature-band roll-forward), a browser, VS Code with the Microsoft C# debugger, and Visual Studio
2026 with .NET development tools. The AI exercise additionally needs an **existing** Foundry
project/model deployment and permission to invoke it. Model inference can incur usage charges.
The local worker is not an offline model. This unit creates no Azure resources.

```powershell
. ./scripts/Use-CaesareaSdk.ps1
dotnet --version
dotnet restore
dotnet build
dotnet test --project ./Tests/Caesarea.Deterministic.Tests/Caesarea.Deterministic.Tests.csproj
code --install-extension ./tools/vscode-demo-attach/demo-attach-0.3.0.vsix
dotnet build ./tools/visualstudio-demo-attach/src --configuration Release
```

Reload VS Code after updating the helper. Open this same folder there, and this folder's
`Caesarea.slnx` in Visual Studio 2026. Source and binaries must come from the same checkout.

## 2. Configure existing model access and start

Sign in to your authorized tenant/subscription if necessary. Substitute your own existing
project endpoint and model deployment name; never put secrets or Aspire tokens in recordings.

```powershell
az login --tenant '<your-tenant-id>'
az account set --subscription '<your-subscription-id>'
$courseProjectEndpoint = '<your Foundry project endpoint>'
$courseModelDeployment = '<your deployed model name>'
$env:OperationsAgentApi__FoundryProjectEndpoint = $courseProjectEndpoint
$env:SecurityAgentApi__FoundryProjectEndpoint = $courseProjectEndpoint
$env:WorkforceAgentApi__FoundryProjectEndpoint = $courseProjectEndpoint
$env:OperationsAgentApi__ModelDeploymentName = $courseModelDeployment
$env:SecurityAgentApi__ModelDeploymentName = $courseModelDeployment
$env:WorkforceAgentApi__ModelDeploymentName = $courseModelDeployment
dotnet run --project ./Caesarea.AppHost/Caesarea.AppHost.csproj --no-build --launch-profile https
```

Keep that shell open. Use the private Aspire login link printed by **this** run. Complete normal
development-certificate setup if needed; do not bypass browser certificate warnings. The direct
AppHost command avoids the presenter's startup script, which can refresh a shared OneDrive work
order. Docker/Functions are not exercised by this unit's read-only path.

## 3. Tour Aspire, then the two UIs

In Aspire verify the resources run. Open the graph; follow Command Center → Operations Agent →
Energy Hub → simulator. Inspect logs/traces when something fails. Running is a process status,
not proof of working model credentials.

Open `democontrol-web` and `commandcenter-web` from their Aspire links. Typical HTTPS ports are
7076 and 7028; use the actual links if different.

In Director select **2 Investigation Agent**. Review the preview and confirm. Expected readiness:
**Lights On Reported by a Client**, Investigation Agent stage, local function, local Aspire habitat.
The Director prepares the fixture. The operator asks the agent in the **main Command Center**.
Do not click Restore Scheduled Mode in this onboarding exercise.

Read the Energy Hub in another PowerShell window:

```powershell
$courseHubBase = 'https://localhost:7089' # Use Aspire's energyhub-api URL.
$courseAgentBase = 'https://localhost:7311' # Use Aspire's operationsagent-api URL.
$courseHeaders = @{ 'X-Correlation-ID' = "unit00-$([guid]::NewGuid())" }
$courseBefore = Invoke-RestMethod "$courseHubBase/api/energy/assets/L-417" -Headers $courseHeaders
$courseBefore | Select-Object assetId, reportedIsOn, isDaylight,
    expectedScheduledState, manualOverride, stateRevision | Format-List
$coursePid = (Invoke-RestMethod "$courseAgentBase/api/demo-breakpoints").processId
```

Checkpoint: L-417 reported On, daylight, scheduled Off, override set. The UI's Mobility Hub is
explicitly not connected in this demo; the architecture drawing is broader than this lab.

## 4. Inspect a request in VS Code

1. In Director expand **Advanced controls → Demo Breakpoints**. Verify VS Code extension 0.3.0
   is ready. Choose VS Code if the IDE picker is shown. Attach the **Operations Agent** row.
2. Arm **AGENT_CREATION** and **FUNCTION_TOOL**. These are one-shot teaching stops.
3. Open `Services/OperationsAgent.Api/Services/FoundryOperationsAgent.cs`. Set a regular F9
   breakpoint at the `ResolveToolApprovalsAsync` call immediately after `await agent.RunAsync`
   (line 288 at this commit). Prepare the expressions below before starting the request.
4. In Command Center click **Ask agent** for **Is streetlight L-417 on?**
5. At Agent Creation, press F10 until the next statement is highlighted. Inspect `question`,
   `currentStage`, and `composition.Tools`. The configured model receives only admitted tools.
6. F5 reaches `EnergyTools.cs`, `GetStreetlightStateAsync`, FUNCTION_TOOL (line 44).
   Inspect `assetId` (`L-417`). The method calls `_energyReadGateway.GetStateAsync`; it does not
   read a model-owned copy of city state.
7. F5 reaches the breakpoint after `RunAsync`. In Debug Console inspect:

```csharp
response.Text
modelFlightRecorder.Exchanges.Count
modelFlightRecorder.Exchanges[0].Received
modelFlightRecorder.Exchanges[1].Sent
modelFlightRecorder.ToolCalls
```

For the captured request there were two model round trips and one `get_streetlight_state` call.
Expand the collections. The first model response requests the tool; the next request contains its
result. Count and wording are observations, not a universal framework guarantee. The recorder
shows messages, arguments and results; it is not access to private model reasoning.

8. F5 finishes. Remove the ordinary breakpoint. **Detach VS Code** in Director, or use the
   attached session's Disconnect/Shift+F5. This is an attach session, not an IDE-launched server.

**Timing:** keep the entire request under 100 seconds. The web client still has a 100-second
attempt timeout while the agent is paused. Pause the *video* to study the code; do not hold a live
request through a long explanation. See recovery below if it expires.

## 5. Repeat in Visual Studio 2026

Open the same `Caesarea.slnx`. Verify VS Code is detached before switching IDEs. In Director
select Visual Studio 2026 and attach Operations Agent. The helper selects this solution and
process, not an unrelated instance. If it reports “solution not open,” open the exact solution.

Arm Agent Creation and Function Tool again. Set the same post-`RunAsync` breakpoint using F9.
Ask the same read-only question in Command Center. F10 steps over; F5 continues. Use Locals or
Watch to inspect `question`, `assetId`, `response.Text` and `modelFlightRecorder.Exchanges`.
Expand the first response and next request; inspect the actual Hub result. Continue promptly,
remove the ordinary breakpoint, then use **Detach Visual Studio** in Director. Do not terminate
the service. Production capture status for this IDE is tracked separately in execution-evidence.md.

## 6. Prove what happened

```powershell
$courseAfter = Invoke-RestMethod "$courseHubBase/api/energy/assets/L-417" -Headers $courseHeaders
$courseDebug = Invoke-RestMethod "$courseAgentBase/api/demo-breakpoints"
$courseAfter | Select-Object assetId, reportedIsOn, manualOverride, stateRevision | Format-List
$courseDebug | Select-Object processId, debuggerAttached | Format-List
if (-not $courseAfter.reportedIsOn) { throw 'Unexpected change: the read-only exercise did not request a repair.' }
if ($courseDebug.debuggerAttached) { throw 'Detach the debugger before finishing.' }
if ($courseDebug.processId -ne $coursePid) { throw 'The service restarted; detach continuity was not demonstrated.' }
```

Expected: main UI shows an answer supported by the tool trace; the Hub still reports On; the same
service PID remains alive with no debugger. Do not expect identical prose from the model.

## Pause and do

Repeat one attach → stop → inspect → continue → detach cycle without following the video.
Then explain: who observed state, what the model selected, where authority resides, and why the
answer did not repair the lamp. For the second IDE, repeat after the first has detached.

## Failure and recovery

- **UI timeout during a long pause:** finish/continue the paused request, remove ordinary
  breakpoints, detach, and check the Hub. Retry this **read-only** question. Do not generalize
  this recovery to write tools: reconcile their receipt/state before resending a command.
- **No breakpoint:** check attachment to Operations Agent, stage, one-shot arm state and matching
  source/binaries. A checkbox alone does not attach a debugger.
- **Model access error:** use Aspire logs and the request correlation. Verify the local endpoint,
  deployment name, account and invocation permission. Do not change shared cloud settings as a
  troubleshooting shortcut.

## Reset before Lab 01

Finish any pending request and detach. Clear remaining armed stops in Director and ordinary IDE
breakpoints. Select **1 Deterministic**, inspect/confirm its fixture preview, and verify **Lights On
Reported by a Client** was reapplied. If necessary click Apply again. Read the Hub again: daylight,
On against the Off schedule, healthy controller, override set. Reset may advance revisions; never
reuse a saved revision across fixtures. For shutdown, Ctrl+C in the AppHost launch terminal.
