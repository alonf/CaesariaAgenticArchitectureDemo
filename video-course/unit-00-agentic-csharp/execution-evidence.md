# Unit 00 — execution and recording evidence

Application baseline: local main merge `9d2f766cc19050495c52a601075df15870c4c0af`.
Expanded recordings: `90f14e6` (the intervening commit changed course files). No application source
was changed for the recordings. The earlier source checks passed restore, format, build (0 errors,
0 warnings), 469 tests with 4 skips, and diff checking. The improved cut's checks and media metadata
are recorded in `evidence/expanded-production.json` and `evidence/media-validation.json`.

## Observed execution

- Aspire ran the main checkout and exposed the resource list and graph. Director prepared InvestigationAgent with Lights On Reported by a Client. Command Center displayed L-417 On against its Off schedule, override set, healthy controller.
- The expanded VS Code take attached to OperationsAgent.Api PID 27972 through Director. Actual motion shows Agent Creation at `FoundryOperationsAgent.cs:223`, F10, Function Tool at `EnergyTools.cs:44`, and the ordinary post-RunAsync breakpoint at 288. Watch showed the question, answer and two exchanges. Expansion exposed `get_streetlight_state({"assetId":"L-417"})`; Debug Console displayed the actual next-request tool payload. Director detach left the service alive. Correlation: `1811ee62159544b0b8031f491bd8904b`.
- The expanded Visual Studio 2026 take used **Director Attach Visual Studio 2026**, not the native process dialog. Actual motion shows creation and tool stops, stepping, question/answer in Watch, exchange count 2 and expansion through `[0].Received` and `[1].Sent`. Director **Detach Visual Studio 2026** returned the row to not attached. A text visualizer opened outside the recorded monitor, so its view is not represented as captured footage. The Watch tree is used; the full payload is demonstrated in VS Code.
- Both long debugger inspections exceeded the web caller's 100-second attempt timeout. A returned C# response was not represented as a successful browser response. A separate detached retry returned “Yes. Streetlight L-417 is currently reported on.” with one `get_streetlight_state` call and two model round trips. The displayed correlation prefix was `276e2fef4061`.
- `after-visual-studio.json` independently records debugger false, original PID 27972, Hub reported On, override true and revision 3. No repair was requested. `before-debugging.json` and `after-vscode.json` provide intermediate checkpoints.
- Director visibly selected Deterministic and reapplied Lights On. `after-reset.json` confirms the starting anomaly at revision 5. Revision values are run-specific. No debugger or approval remains pending.
- `expanded-after-vscode.json` and `expanded-after-visual-studio.json` confirm PID 27972, debugger false, Hub On/override true/revision 5 after the new takes. The final Director stage returned to Deterministic. Since the fixture was already present, this stage selection did not reapply it. The earlier explicit reset footage remains in the edit.
- The new orientation records actual GitHub root/cumulative-stage README, the local code tree/request handler, and Microsoft Learn's C# first-agent/tools pages. The minimal Learn example is a code-reading segment, not a separately executed console app.
- The read-only Foundry visit showed project `caesarea-dev`, hosted agent `caesarea-operations`, Running, active version 13, identity and Responses endpoint. No invocation, deployment, shared configuration or permission change was performed. The local lab and hosted agent use separate city-service boundaries.

## Motion sources and editing

Raw recordings are local/ignored under `video-course/.build/`:

| Source | Evidence used |
|---|---|
| `aspire-live.mp4` | Running resources and graph |
| `director-live.mp4` | Stage preparation and main-app orientation |
| `orientation-repo.mp4` | GitHub root and README cumulative stages |
| `orientation-code.mp4` | Local project tree and typed request/response handler |
| `orientation-learn.mp4` | Actual Learn C# first-agent and tools examples |
| `orientation-foundry.mp4` | Existing project and real hosted agent, read-only |
| `vscode-values.mp4` | Director attach/detach, stepping, Watch expansion and actual tool payload |
| `vs-values.mp4` | Director attach/detach, stepping and Watch-tree drill-down |
| `retry-live.mp4` | Timeout, separate successful retry, top-right Walkthrough |
| `reset-live.mp4` | Deterministic stage selection and fixture reset |

The renderer's title-keyed `SHOTS` map and output `timeline.json` record exact source trims. Motion
is not simulated or sped up. Freeze holds are labeled. Evidence cards summarize saved API responses
and identify them as recorded API evidence. Failed hex-editor/copy attempts, unrelated Quick Open
history, black capture attempts and broad desktop tests are excluded. The native-attach take is
superseded. No prior pilot debugger stills substitute for current motion. The preceding branded
output is retained locally in `output/prior-branded-cut/`.

## Scope and limits

This is a local review rough cut with synthetic narration, not a published finished course. UI footage uses the running local app and real remote model calls; the lamp hardware is simulated. No cloud deployment, cloud resilience or production scale claim is established. The current main merge and course files were not pushed. Clone instructions require the separate release gate in `lab.md` before distribution.

The automated render validates source existence and card bounds. Final media checks, duration, streams and hashes are recorded in `evidence/media-validation.json`. Review creative pacing and pronunciation before replacing the synthetic review voice or producing later units.
