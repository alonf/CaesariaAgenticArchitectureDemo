# Pilot readiness and execution evidence — November 24, 2026

## What is verified

The application ran from the clean detached worktree at
`89d5a513be97301857dd4f156ce0773103712d51`, rather than the instructor's dirty durable-UI
worktree. The remote upgrade branch resolved to that same commit during a read-only check.
No merge, push, cloud deployment, shared-setting change or publication was performed.

Fresh restore/build/test results: build succeeded with one AZFW0108 warning (Functions extensions
restored during build); 447 tests passed, 4 skipped. The prior 469-pass result belongs to the
different working tree and is not used as this pilot's test result.
Formatting verification passed on the clean worktree as well.

[Actual API rehearsal evidence](evidence/api-rehearsal.json) records:

- Deterministic preparation of the healthy Lights On fixture.
- Hub before snapshot, revision 9, reported On and override on.
- Restore through **CommandCenter.Api**, successful response and Hub receipt; revision 10,
  reported/desired Off, override cleared, matching command correlation and Hub activity.
- ControllerFault fixture: HTTP 409, Failed receipt, lamp remained On.
- Reset to ForgottenOverride: healthy controller, On, override on, revision 15.

The numbers above describe this saved run, not constants a learner should expect. The real device
is simulated by SmartPole. This is an **API rehearsal**, not evidence that the browser button was
clicked or screen footage recorded. `rehearse_unit00.ps1` deliberately makes that distinction.

## Capture blocker and missing work

The native capture tool could not control Chrome's Open file dialog. It first rejected its Cancel
button as belonging to a different target window; later it returned **“foreground window did not
report a process id.”** Browser automation access was unavailable in this session. A request to
close the dialog manually was sent; no confirmation of a usable recording surface was received.

The current deliverable is the revised shooting script, lab, narration, draft captions and verified
API evidence. **There is no completed pilot MP4 or recorded rough cut yet.** Old narration/capture
plans are superseded by the current scenes.json and shooting-script.md.

Missing shots before the pilot can be edited:

1. Command Center cold open on L-417 (shots 01–02, 09).
2. Actual startup/resource view, excluding the login token (shot 06).
3. Director preparation preview, confirmation and readiness (shot 08).
4. Readable terminal Hub-before capture (shot 10).
5. Main-app Restore Scheduled Mode click, wait and completion (shot 11).
6. Hub-after receipt/correlated activity and completed main UI (shots 12, 14).
7. Director reset and fresh Hub/main-app anomaly (shot 15).

A source excerpt from the clean baseline is prepared for shot13. Code/source cards and narration
are production assets, not substitutes for the missing app execution footage. After capture,
retime captions, assemble the edit, inspect readable frames/audio/privacy, and deliver only Unit00
for review. Later units must not start until that review.

## Local startup finding

An attempted detached Aspire launch reported success but did not retain usable child services.
A stale resource-service port also caused one restart to fail. Repository-scoped cleanup followed
by an interactive `dotnet run` terminal kept the app and deterministic endpoints available. The lab
therefore explicitly keeps that launch terminal open. Port 5187 belongs to an unrelated local app;
use the Director's HTTPS resource link (7076 on this run). Do not stop the unrelated service.

These are recording-machine observations, not general claims about Aspire reliability.
