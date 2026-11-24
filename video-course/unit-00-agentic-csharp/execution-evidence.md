# Unit 00 — execution and recording evidence

Recorded from local main at `9d2f766cc19050495c52a601075df15870c4c0af`. Source validation completed before the merge: restore, format, build (0 errors, 0 warnings), deterministic tests (469 passed, 4 skipped), and `git diff --check`. The source was not changed during recording. Course-only edits follow the baseline.

## Observed execution

- Aspire ran the main checkout and exposed the resource list and graph. Director prepared InvestigationAgent with Lights On Reported by a Client. Command Center displayed L-417 On against its Off schedule, override set, healthy controller.
- VS Code attached to OperationsAgent.Api PID 27972. Actual motion shows Agent Creation at `FoundryOperationsAgent.cs:223`, F10 to construction, Function Tool at `EnergyTools.cs:44`, and the ordinary post-RunAsync breakpoint at line 288. Watch showed the question, returned response and two model exchanges. The collection was expanded. Continue and disconnect left the service alive.
- Visual Studio 2026 opened the same main solution and attached to PID 27972 through its native Attach to Process dialog. Actual motion shows creation and tool stops, stepping, question and response in Watch, exchange count 2, clearing the ordinary breakpoint and Debug → Detach All.
- Both long debugger inspections exceeded the web caller's 100-second attempt timeout. A returned C# response was not represented as a successful browser response. A separate detached retry returned “Yes. Streetlight L-417 is currently reported on.” with one `get_streetlight_state` call and two model round trips. The displayed correlation prefix was `276e2fef4061`.
- `after-visual-studio.json` independently records debugger false, original PID 27972, Hub reported On, override true and revision 3. No repair was requested. `before-debugging.json` and `after-vscode.json` provide intermediate checkpoints.
- Director visibly selected Deterministic and reapplied Lights On. `after-reset.json` confirms the starting anomaly at revision 5. Revision values are run-specific. No debugger or approval remains pending.

## Motion sources and editing

Raw recordings are local/ignored under `video-course/.build/`:

| Source | Evidence used |
|---|---|
| `aspire-live.mp4` | Running resources and graph |
| `director-live.mp4` | Stage preparation and main-app orientation |
| `vscode-live.mp4` | Director attach controls only; this first take did not capture the IDE monitor |
| `vscode-take2.mp4` | Actual VS Code stepping, Watch and result collection |
| `vs-take2.mp4` | Actual native Visual Studio attach, stops, Watch and detach |
| `retry-live.mp4` | Timeout, separate successful retry, top-right Walkthrough |
| `reset-live.mp4` | Deterministic stage selection and fixture reset |

The renderer's `SHOTS` map and output `timeline.json` record exact source trims. Motion is not simulated or sped up. Freeze holds are labeled. Evidence cards summarize saved API responses and say that they are cards, not terminal captures. Failed black HWND-capture attempts and the broad desktop test are excluded. No prior pilot debugger stills substitute for current motion.

## Scope and limits

This is a local review rough cut with synthetic narration, not a published finished course. UI footage uses the running local app and real remote model calls; the lamp hardware is simulated. No cloud deployment, cloud resilience or production scale claim is established. The current main merge and course files were not pushed. Clone instructions require the separate release gate in `lab.md` before distribution.

The automated render validates source existence and card bounds. Final media checks, duration, streams and hashes are recorded in `evidence/media-validation.json`. Review creative pacing and pronunciation before replacing the synthetic review voice or producing later units.
