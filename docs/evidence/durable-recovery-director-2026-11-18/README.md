# Guided recovery rehearsal — November 18, 2026

## Final two-screen flow

The final layout was verified live in Chrome on Windows at 02:39–02:41 Asia/Jerusalem.
Preparation and the forced worker crash/restart were clicked in **Demo Director**. The initial
agent request, continuation and explicit supervisor approval were clicked in **Command Center**.
Continue was disabled before restart, enabled after the worker returned, and disabled with an
explanation after the same conversation resumed. The exact original review was confirmed unchanged.
Approval completed the repair; the matching Hub receipt succeeded and the light and override
were both OFF. See [two-screen transcript](two-screen-transcript.json).

The user-reported disabled-button incident was separately traced to a scheduler operation marked
`Terminated`, with plain-text output `Terminated by user.`. The old controller tried to deserialize
that output as a business result and retained stale AwaitingApproval state. Terminal scheduler
status is now preserved, continuation explains its disabled state, and a fresh status check
prevents sending another agent turn to an ended repair. Unknown command outcomes remain unknown.
The inspected incident snapshot and worker log remain in the ignored local evidence directory.

A second live attempt exposed a transient snapshot replacement failure on Windows, before the
initial model turn was sent. Bounded file-write retries now handle brief reader locks without
retrying agent turns or decisions. Final validation: solution build has zero warnings/errors;
469 tests passed, 4 existing tests skipped; restore, format and diff checks completed.

## Earlier all-Director flow

Executed through the actual Demo Director UI in Chrome on Windows, with the real Foundry model,
local Functions worker, v2 scheduler emulator and city services. All three actions were clicked
in the dashboard, including the explicit supervisor approval. No terminal command performed the
worker interruption or the approval.

Observed sequence (Asia/Jerusalem; the JSON uses UTC, on November 17):

- 01:48:29 — owned Functions launcher 36472 ready.
- 01:48:54 — authoritative AwaitingApproval, Hub revision 3, deadline 01:53:53.
- 01:49:17 — forced worker process-tree termination confirmed; scheduler and city remained up.
- 01:49:31 — new owned launcher 78736 ready.
- 01:49:46 — same session resumed without an operation ID in the prompt; exact review compared unchanged.
- 01:50:19 — explicit supervisor approval queued.
- 01:50:21 — terminal Completed; command executed and resolved both true; matching Hub receipt
  Succeeded, light OFF, manual override OFF, revision 4.

[Transcript](transcript.json) retains the actual agent messages, observed timeline, operation
identity and Hub result. The review challenge is omitted. The private snapshot hash is included;
raw worker logs and the full snapshot remain in ignored
`docs/runbooks/artifacts/director-recovery/`. These logs are not published with this evidence.

This proves recovery of the local Functions worker while the scheduler and city remain available.
It does not prove scheduler/city restart recovery, deployed Azure operations, separate Entra
human users, or general exactly-once effects. The initial live run exposed a first-click anchor
scroll issue; the recovery panel was subsequently made present before anchor navigation.
