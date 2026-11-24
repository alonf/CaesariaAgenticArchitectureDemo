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

## Actual UI repair and capture

The user correctly reported that Chrome was not running: the earlier dialog report came from
stale computer-control state. Resetting the capture connection and launching a fresh Chrome
window resolved that first blocker. It did not require the user to close a nonexistent dialog.

The following actions were performed through the native browser UI and captured as still images:

1. Open Command Center at its actual HTTPS resource URL.
2. In Director reset Normal Operation, select Deterministic, inspect the preparation preview,
   confirm it, and observe readiness for Lights On Reported by a Client.
3. Read the Hub endpoint in the browser. [Before evidence](evidence/ui-before.json): revision19,
   reported On, desired On, override active, healthy controller, schedule Off.
4. Click **Restore Scheduled Mode in Command Center**. The captured resulting screen reports Off
   and includes the events for the operator request and SmartPole confirmation. No API restore
   was substituted for this click. Its brief pending transition was not captured.

After this click, the computer-control transport closed. Reconnecting/resetting returned
`Transport closed`. The completed repair screenshot had already been saved. A direct read of
the Hub verified the same command; no second repair command was sent:

- [After state](evidence/ui-after.json): revision20, reported/desired Off, override cleared,
  `Restore scheduled mode`, Succeeded.
- Command correlation: `e4980ed381f84ef192c787e7af460674`.
- [Matching activity](evidence/ui-activity.json): pending desired-state event followed by
  `SmartPole confirmed the reported state as Off.` with Succeeded status.
- [Actual API reset](evidence/api-reset-after-ui.json): ForgottenOverride reapplied locally,
  revision22, reported On, override active and controller Healthy. This is explicitly labeled as
  an API reset in the narration and picture, not a recorded Director reset click.

The rough cut uses the real before/after app stills, preparation captures, original H08 visuals,
readable baseline source, command instructions and clearly labeled saved-response cards.
It is a narrated **edited-still rough cut**, not a continuous screen recording. The Hub after/reset
cards are generated from the saved JSON; they are not fabricated outputs or imitation terminals.

## Pickups still required for a finished motion edit

1. Actual launch terminal and Aspire resource view, with token-bearing addresses excluded.
   The rough cut currently shows instructions and a labeled verification summary.
2. Continuous Director preparation/confirmation and main-app click/completion footage.
   Actual stills and the successful UI command are available; cursor motion/pending footage is not.
3. Browser/terminal Hub-after and activity read. The rough cut shows saved actual response cards.
4. Director reapply scenario, main-app refresh and fresh Hub read for reset.
   The rough cut instead shows the verified API fallback, with that limitation spoken aloud.

The script, lab and captions accompany the local MP4. Review Unit00 before producing later units.

## Rough-cut export validation

The local MP4 is 1920×1080 H.264 with AAC audio, embedded English captions, a separate SRT,
five chapter markers, and a duration of 645.02 seconds (10:45). It contains 125 caption cues.
Full FFmpeg decoding completed without errors. Caption times are ordered, non-overlapping and
within the video; every narration word boundary fits its scene. Representative cold-open,
preparation, restore, receipt, source-code and reset frames were visually inspected. Audio was
measured at -24.3 dB mean / -5.7 dB peak; a full listening review remains part of pilot review.

[Media validation and SHA-256 hashes](evidence/media-validation.json) identify the exact MP4,
captions and source captures. Raw captures and the MP4 remain local and ignored by Git.
Python compilation and `git diff --check` passed for the production edits. Application code
was unchanged by this production work; the clean-baseline application checks are recorded above.

## Local startup finding

An attempted detached Aspire launch reported success but did not retain usable child services.
A stale resource-service port also caused one restart to fail. Repository-scoped cleanup followed
by an interactive `dotnet run` terminal kept the app and deterministic endpoints available. The lab
therefore explicitly keeps that launch terminal open. Port 5187 belongs to an unrelated local app;
use the Director's HTTPS resource link (7076 on this run). Do not stop the unrelated service.

These are recording-machine observations, not general claims about Aspire reliability.
