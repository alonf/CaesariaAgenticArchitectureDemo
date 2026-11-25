# Agentic C# — Lab 01 storyboard

**Module:** Operate and verify before adding an agent.
**Status:** Recorded rough cut ready for review; actual timeline below.
**Recorded duration:** 11:07. English narration; say Microsoft Agent Framework or Agent Framework.

## Cross-platform edit

Use one teaching progression, not duplicate demonstrations. Mac + VS Code carries repository
orientation, Foundry Toolkit, the successful main-app repair and authoritative verification.
Windows + VS Code carries source navigation and the controller-failure investigation; include a
brief Visual Studio 2026 inspection where it explains the failed controller result. Later labs
alternate platforms and use VS Code most often. Name each platform change in narration and
establish its fixture: these are separate local systems, not a session migrated between machines.
Use the Director's attach controls whenever attaching an IDE. Never substitute a static code
view for a claimed live debugger observation.

## Observable outcome

The learner restores L-417 to scheduled operation through Command Center and proves the result
at the Energy Hub: a succeeded command receipt, reported Off, desired Off, and override cleared.
A second take demonstrates that a failed controller command does not become a reported success.
End by resetting the original daylight anomaly for Lab 02.

## Opening order requested by the author

1. Review Unit 00: deterministic authority, the agent/context/tool loop, framework versus platform,
   and the ability to inspect execution in a debugger.
2. Show the cloned solution in VS Code on the Mac: README, project tree, global.json and C# Dev Kit.
3. Recall the Foundry web portal. Show the same existing project and hosted agent in **Foundry
   Toolkit** inside VS Code. Explain that this is another client for supported Foundry resources,
   not a promise that every portal feature has an identical editor view. Do not deploy or invoke.
4. State this module's outcome and why deterministic execution is the prerequisite for agents.
5. Run the lab in the main Command Center, with Director used only for fixtures and failure setup.

## Timestamped shooting plan

| Recorded range | Screen action | Narration / teaching purpose | Required evidence |
|---|---|---|---|
| 00:00–00:40 | Unit 00 slide 12, then 14; brief L-417 preview | Recall the operational core and the context/tool loop. Last unit inspected an answer; this unit proves a state transition. | Clearly identify recap visuals as recap; no new model-call claim. |
| 00:40–01:46 | Actual Mac VS Code: clone/open repository, README, global.json, Apps/Services/Contracts/AppHost/Tests | Same C# solution and boundaries on macOS. C# Dev Kit supplies the C# project experience. Exact SDK/commit are lab prerequisites. | Record actual clone path, commit and SDK. Never imply remote main contains an unpublished local merge. |
| 01:46–02:45 | Foundry Toolkit: caesarea-dev → Agents → Hosted Agent → caesarea-operations; Models if useful | Recall the browser portal in Unit 00. The extension brings supported project resources into the editor. The listed cloud agent is separate from the local deterministic exercise. | Existing version/status visible; read-only. No cloud setting change, deployment or model invocation. |
| 02:45–03:18 | Large outcome card + slide 12 Hub/core region | Learn to trace a command, distinguish desired from reported state, verify a receipt, and recover from failure. A fixed operation belongs in ordinary code. | No AI credentials required for this stage; optional Foundry orientation is separable. |
| 03:18–04:05 | Aspire → Director: Deterministic; Lights On Reported by a Client; main UI top-right Walkthrough | Establish L-417 on in daylight, expected Off, override true, healthy controller. Director sets conditions; main UI performs operations. | Initial Hub GET saved. Current UI labels take precedence over the older Forgotten Override label. |
| 04:05–04:54 | Command Center Restore Scheduled Mode; show pending/desired versus reported if a controlled delay makes it visible | Operator requests a bounded operation. Hub rereads state, computes the permitted target, requests the simulator command and waits for confirmation. | Real click, correlated command receipt. Do not claim a pending frame unless observed. |
| 04:54–05:42 | Energy Hub GET and activity; main UI outcome | Desired Off is intention; reported Off plus succeeded receipt is the confirmed software outcome. Device is simulated. | reportedIsOn=false, desiredIsOn=false, manualOverride=false, lastCommand.status=Succeeded; matching correlation ID. |
| 05:42–07:06 | Windows VS Code source: CommandCenterService → HttpEnergyHubGateway → EnergyHubService.RestoreScheduledModeAsync | Read the short relevant blocks. Point to Pending, gateway call, confirmation guard and reported-state assignment. Explain why UI optimism/model prose cannot substitute for this boundary. | Exact source files and measured highlights; use real debugger stepping only after verifying the attached Windows process. |
| 07:06–08:36 | Director Controller Fault; main UI restore; inspect Hub receipt/state | Failure remains failure. A request was made but the controller did not confirm a repair. Keep reported state truthful. | Failed receipt, lamp still On; no false success. |
| 08:36–09:16 | Director reapplies Lights On; repeat successful restore or verify fixture as appropriate | Recover through a known fixture, not by hiding failure or blindly retrying an unknown write. | Fresh authoritative GET; final reset restores initial anomaly. |
| 09:16–10:12 | Pause-and-do card, exact steps and expected values | Learner repeats happy and failure paths, collects correlation and state evidence, resets for next lab. | Copyable lab, expected results and recovery tip. |
| 10:12–11:07 | Questions + slide 6 decision map | Investigate ownership, confirmation, stale state and when an agent would add value. Next lesson adds investigation; the operational core remains authoritative. | Code/documentation/AI questions require supporting file locations or observed results. |

## Source and execution anchors

- Book: *The Agentic Revolution in .NET*, chapters 4 and 6 (PDF pp39–52,66–77),
  chapter14 setup/exercise pp184–197. Book p189's restore is this lab's operation.
- Branded slides: 12 for Hubs/core; 14 only for recap; 6 for architectural decision recap.
- `Services/CommandCenter.Api/Services/CommandCenterService.cs` — operational request.
- `Services/CommandCenter.Api/Services/HttpEnergyHubGateway.cs` — Hub boundary.
- `Services/EnergyHub.Api/Services/EnergyHubService.cs` — restore, pending, confirmation, failure.
- `GET /api/energy/assets/L-417` and `/api/energy/assets/L-417/activity` — independent proof.
- `docs/prompts/00-deterministic-foundation.md` — architecture intent; current source/UI resolve drift.
- [Foundry Toolkit overview](https://code.visualstudio.com/docs/intelligentapps/overview).

## Mac preparation and provenance

The existing TigerVNC connection is the authorized Mac access. Both machines and GitHub main
were synchronized at `843e0d0` before this recording: .NET 11.0.100 and Aspire 17.0.0.
The course edits live on `video-course/lab01-cross-platform`; application code matches main.
Both machines passed 469 tests (four live IDE tests skipped), all eleven HTTPS health checks,
and a deterministic repair. Mac certificate trust is verified. The recorded Toolkit showed hosted agent version 13 with Running status; narration does not
hardcode that version.
See [execution evidence](execution-evidence.md).

## Production rules

Full-frame motion, readable code, restrained timed highlights, and labeled freeze holds.
Narration is a synthetic review voice unless replaced by Alon. Never fabricate UI or outputs.
Capture only the project/toolkit, debuggers and relevant application surfaces; exclude unrelated apps.
Capture tokens/sign-in off-camera. Local files only; no cloud deployment, shared-setting changes,
media publication. Repository commits/pushes are authorized; video remains local. Unit 00 is approved; this authorization advances production through Lab 01
only, then stop for review.

## Learner investigation questions

1. Where is the scheduled target computed? Can Command Center bypass the Hub to set the lamp?
2. Which exact condition allows ReportedIsOn to change? What happens if success lacks physical confirmation?
3. How does expectedStateRevision reject a stale decision? Reproduce safely in the disposable lab.
4. Ask an AI assistant whether an agent is needed for this fixed operation. Challenge its answer
   with the observed controller-failure path and verify its code claims.
5. What new requirement would justify adding an investigation agent in the next module?
