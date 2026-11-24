# Unit 00 production evidence and remaining work

## Sources actually consulted

- Supplied lecture recording: its Stream transcript was downloaded as VTT and reviewed. H08 begins
  around 01:35:52; see production-plan.md for interval mapping. This is a transcript review, not a
  claim that the entire recording was watched. The SharePoint connector could not retrieve the
  1.46 GB video because its download limit is 100 MB; Stream's transcript download succeeded.
- H08 release PDF: 59 pages, SHA-256
  `1adb7953e203f2005cc8a3cde45bc32bc35990242f74cfd66f696f509fe1989b`.
- Companion book: rationale and chapter mapping in `../book-alignment.md`.
- Executable baseline: `89d5a513be97301857dd4f156ce0773103712d51`, separate clean worktree.
  Existing unrelated durable-demo working changes are excluded from the recording baseline.

## Observed on this rehearsal

1. AppHost started directly with existing Foundry project/model configuration. No cloud resource
   was created or reconfigured. Aspire showed all 11 resources running; its resource list and
   graph were captured. The private Aspire token is excluded from course files and video.
2. Director prepared Lights On Reported by a Client / Investigation Agent. Its preview and ready
   state were captured. The operator view showed L-417 On during daylight, schedule Off,
   override set, healthy controller, no incident.
3. VS Code helper was updated from installed 0.1.0 to repository 0.3.0. The Director reported
   ready and attached. OperationsAgent.Api PID **117032** was visible in debugger output.
4. The real main-UI request stopped at Agent Creation and Function Tool. F10 stepping, the
   question variable, `assetId = L-417`, `response.Text`, and recorded exchanges were captured.
   One `get_streetlight_state` invocation and two model round trips were observed. The first
   result requested that tool; the next input contained Hub state with `reportedIsOn = true`.
5. The long debugger inspection exceeded the web client's **100-second attempt timeout**.
   The model response was inspected, but the original UI request timed out. This failure is
   shown explicitly; it is not edited into the subsequent successful request.
6. After F5 and detaching VS Code, a **separate read-only retry** succeeded in the main UI.
   Its trace showed one tool marked RAN across two model round trips. Answer wording differed
   from the first request, which is expected. The video must not imply they are one request.
7. Independent API evidence in `evidence/debugger-and-hub.json`: PID **117032** remains alive,
   debugger detached, all teaching snippets disarmed; Hub revision **3**, reported On, override
   set, and last command still the fixture's Apply scenario receipt. No repair was issued.

8. Visual Studio 2026's helper built successfully. A transient native-UI/window-launch failure
   initially blocked capture; UI control subsequently recovered and opened the clean-worktree
   solution. Director reported Visual Studio 18.0 ready and attached Operations Agent PID 117032.
9. Actual Visual Studio captures show Agent Creation at line 223, F10 to construction at line
   225, and the real `question` value in Immediate Window. This request exceeded its time budget
   and stopped with AzureCliCredential authentication canceled. It was continued to completion.
10. A **fresh read-only request** stopped at EnergyTools line 44 (`assetId = L-417`), then at
    FoundryOperationsAgent line 288 after RunAsync. Autos and Immediate Window showed
    `Yes. Streetlight L-417 is currently reported on.` The recorder contained two round trips,
    a `get_streetlight_state` request, and a subsequent Hub result with `reportedIsOn = true`.
    These captures are explicitly distinguished from the first expired request in narration.
11. Continued execution, removed the regular breakpoint, and clicked Detach Visual Studio 2026.
    Director showed **0/4 services attached** and **Debugger not attached**. Independent evidence
    in `evidence/after-visual-studio.json` confirms PID 117032, detached, all snippets disarmed,
    Hub revision 3, reported On and override set. No repair command was issued.

## Editorial status

The local video is an **edited-still rough cut with synthetic narration**, not a continuous
screencast and not an imitation of Alon's voice. Source slides and actual captures are used.
No screenshots, model responses, terminal outputs or debugger values were fabricated. Further
motion pickups and final editorial review remain before treating it as the finished intro.

## Validation

The exact executable baseline passed `dotnet restore`, `dotnet format`, `dotnet build --no-restore`,
`dotnet test --project Tests/Caesarea.Deterministic.Tests/Caesarea.Deterministic.Tests.csproj`,
and `git diff --check` in a separate clean validation worktree: **447 passed, 4 skipped, 0 failed**.
An initial build against the running recording worktree hit an AppHost executable file lock;
the clean validation worktree avoided disturbing the live demo. No application source was changed.
Media validation is recorded in `evidence/media-qa.json` after final rendering.

Stop after Unit 00 for author review. Keep files local; do not deploy, publish or push.
