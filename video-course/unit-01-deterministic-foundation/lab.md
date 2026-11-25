# Agentic C# — Lab 01: operate and verify

Allow 30–45 minutes. Your outcome is a verified state transition, followed by a controlled
failure and a repeatable reset. The smart pole is simulated; its command response and the Hub's
receipt are real software results. No model call is needed for this deterministic stage.

## Before starting

Recall Unit 00: the Agent Framework can assemble context, call a model, execute a selected
tool and feed the result into another model exchange. That loop does not replace operational
ownership. In this lab, identify the ordinary C# operation that a later tool or workflow may request.

Use the SDK declared by `global.json`. The course code uses .NET 11; the Agent Framework
concepts and patterns also apply on .NET 10 LTS. Do not simply change this checkout's target
framework to run it with a different SDK.

**Application baseline:** `843e0d0`, published on GitHub main and synchronized on Windows and
macOS before recording. It uses .NET 11.0.100 and Aspire 17.0.0. See the root README for verified
Mac SDK selection, HTTPS certificate trust and port-conflict recovery. Both machines pass 469
tests; four live IDE tests are opt-in. Course-only edits use `video-course/lab01-cross-platform`.
The video changes machines between teaching beats, not to repeat an exercise. Each machine has
its own local state. You can complete every exercise on either platform; only Visual Studio 2026
requires Windows. See [execution evidence](execution-evidence.md).

## 1. Orient in VS Code on macOS

Use **Clone Git Repository** with:

```text
https://github.com/alonf/CaesariaAgenticArchitectureDemo.git
```

Open the repository. Review the source before trusting the workspace. Check the C# Dev Kit
extension, README and `global.json`. In the integrated terminal record:

```bash
git rev-parse HEAD
git status --short
dotnet --info
```

Find these folders and explain their responsibility:

| Folder | Responsibility |
|---|---|
| `Apps` | Operator UI and presenter Director |
| `Services` | Domain Hubs, simulator, Command Center and agent services |
| `Contracts` | Focused API boundary types |
| `Caesarea.AppHost` | Starts and connects the local services |
| `Tests` | Executable checks of boundaries and outcomes |

Open **Foundry Toolkit → My Resources → your existing project → Agents**. In the recorded
environment the project is `caesarea-dev` and the hosted agent is `caesarea-operations`.
Compare it with the Foundry portal shown in Unit 00. This is another client for supported
Foundry resources, not a second local deployment or complete portal replacement. Do not deploy
anything for this lab. Learners without Azure access can skip this orientation.

[Foundry Toolkit documentation](https://code.visualstudio.com/docs/intelligentapps/overview)
describes the editor experience. The cloud hosted agent is separate from this local repair.

## 2. Start the local system and establish the fixture

From the verified repository root:

```bash
dotnet restore
dotnet build
dotnet run --project Caesarea.AppHost/Caesarea.AppHost.csproj
```

Use the Aspire dashboard URL printed by AppHost, including its login token. Do not put that
token in recordings. Open `democontrol-web` and `commandcenter-web` through Aspire.
Keep AppHost running. If development HTTPS certificates are not trusted, follow the platform's
normal .NET development-certificate setup rather than disabling certificate validation.

In Director select **Deterministic**, then prepare **Lights On Reported by a Client**.
In Command Center select L-417 and open the top-right **Walkthrough**. Read its explanation
before acting. The Director creates the starting conditions; perform the repair in Command Center.

If the default stage shows no Walkthrough, select Investigation Agent in Director and confirm,
then select Deterministic and confirm before applying the fixture. No agent question is needed.
This reapplies the stage's walkthrough metadata; selecting an already-active beat can report
"Already in place" without refreshing it. This workaround was observed during recording.

Find `energyhub-api` in Aspire and copy its HTTPS base URL. In a second terminal set it below;
replace the example if Aspire shows a different endpoint:

```bash
HUB=https://localhost:7089
curl --fail --silent --show-error "$HUB/api/energy/assets/L-417" | python3 -m json.tool
```

Checkpoint: L-417 is reported On, daylight is true, the scheduled target is Off, manual override
is true and the controller is healthy. Stop and reset if those facts do not match. This is a
known lab fixture, not evidence that every daylight anomaly has the same cause.

## 3. Request and verify the repair

In the main Command Center click **Restore Scheduled Mode**. Inspect the actual result.
Read the Hub again:

```bash
curl --fail --silent --show-error "$HUB/api/energy/assets/L-417" | python3 -m json.tool
curl --fail --silent --show-error "$HUB/api/energy/assets/L-417/activity?limit=20" | python3 -m json.tool
```

Expected result:

| Field | Expected value |
|---|---|
| `desiredIsOn` | `false` |
| `reportedIsOn` | `false` |
| `manualOverride` | `false` |
| `lastCommand.status` | `Succeeded` |
| `lastCommand.correlationId` | Same operation in the Hub activity |

Save the correlation ID. Desired state describes the requested target. Reported state changes
only after the command result supplies confirmation. A UI message alone is insufficient proof.
The receipt proves this simulator operation's outcome, not a physical streetlight deployment.

## 4. Follow the C# boundary

Use VS Code's file search and method search; line numbers can change.

1. `Services/CommandCenter.Api/Services/CommandCenterService.cs`: find `RestoreScheduledModeAsync`.
2. `Services/CommandCenter.Api/Services/HttpEnergyHubGateway.cs`: follow the HTTP request to the Hub.
3. `Services/EnergyHub.Api/Services/EnergyHubService.cs`: find `RestoreScheduledModeAsync`.
   Locate the physical-state read, scheduled-target computation, Pending receipt, command call,
   confirmation guard and assignment of reported state.
4. `Services/EnergyHub.Api/Services/HttpSmartPoleGateway.cs`: identify the simulator boundary.

Explain why `Succeeded` without `ActualIsOn` is insufficient, and why a stale revision cannot
overwrite a newer decision. Ordinary code enforces these rules whether an operator or a future
agent initiates the request. This lab does not need a debugger to manufacture a pending state.

Optional debugger investigation: attach **Energy Hub** using the Director's Advanced controls.
Choose VS Code on either platform, or Visual Studio 2026 on Windows. Set an ordinary breakpoint
on the confirmation guard (baseline `EnergyHubService.cs:370`), then run the Controller Fault
attempt below. In Locals expand `commandResult`: expect `Status=Failed`, `ActualIsOn=null`,
and the controller-rejection summary. Step over to see the success branch skipped. Continue,
then detach through Director; do not stop the service. A long pause can time out the caller.
After continuing, refresh and read the Hub receipt before making another request. The recording
shows this timeout and a separate, uninterrupted failed attempt with a different correlation ID.

## 5. Observe a real failure and recover

In Director apply **Controller Fault**. Return to Command Center and attempt the same restore.
Read the Hub state and activity again. Expected: the command fails and reported state remains
On. The operation did not repair the controller. Capture the failed receipt, its detail and its
correlation ID; do not substitute an earlier successful receipt.

Recover by reapplying **Lights On Reported by a Client** in Director. Verify the healthy
controller and original On/override fixture. Repeat the restore once if you want to confirm
recovery, then reapply the fixture again for the next lesson.

The UI label is **Lights On Reported by a Client**; its API fixture ID remains `ForgottenOverride`.
For a terminal reset, copy `demoscenario-api`'s base URL from Aspire:

```bash
SCENARIO=https://localhost:7152
curl --fail --silent --show-error -X POST \
  -H 'X-Correlation-ID: lab01-reset' \
  "$SCENARIO/api/demo-scenarios/apply/ForgottenOverride"
curl --fail --silent --show-error "$HUB/api/energy/assets/L-417" | python3 -m json.tool
```

Recovery tip: if a request hangs, first check Aspire resource health and whether a debugger is
paused. Continue/detach through the supported Director controls before retrying. Read the latest
receipt before repeating a write whose outcome you have not established.

## Pause and do

Without following the instructor's clicks, prepare the initial fixture, repair it, verify the
Hub result, reproduce Controller Fault and reset. Keep a short evidence note with starting facts,
success and failure correlation IDs, final facts and the source location enforcing confirmation.

Investigate these questions in code, documentation or with an AI assistant. Verify any AI answer
against the source and your observed results:

1. Who computes the scheduled target? Can Command Center bypass the Hub?
2. Which condition allows reported state to change? What if a success response omits confirmation?
3. Where is `expectedStateRevision` checked, and what response represents a stale decision?
4. Why is a fixed restore operation appropriate for deterministic code? What additional requirement
   would justify an investigation agent?
5. Which facts from this lab would become tool-result context in the next lesson? Which would
   still require authorization before changing state?

Finish with L-417 On in daylight, override enabled and a healthy controller. The next lesson
adds investigation to this same operational foundation.
