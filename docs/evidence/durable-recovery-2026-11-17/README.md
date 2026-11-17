# Local recovery evidence, November 17, 2026

These are selected outputs retained from the implementer's earlier **v1** local rehearsal,
saved alongside the review fixes. They are historical captured results, not a live v2 run,
an independent verification, a video, or a full worker-termination transcript.

Files 01–03 preserve the start response, later conversation response and completed status for
one operation/session. Files 04–05 preserve separate stale-review and timeout outcomes.
Opaque operation and session identifiers were replaced with consistent descriptive aliases;
all other response fields and message text are preserved. Durable Task's `outcome` remains a
serialized JSON string, matching the original HTTP status response. No keys or project endpoints
are included. [Original hashes](original-hashes.md) identify the retained temporary source files
before redaction; they do not authenticate an independently witnessed event.

The resumed conversation explicitly reports `AwaitingApproval`; the completed status confirms
the business result. These snapshots do **not** prove the process was killed between requests,
show the original user prompts, or establish that only one physical command was sent. Those
limits matter when presenting the worker-restart claim. The in-flight/no-resend regression is
covered separately by `EnergyCommandGatewayIntegrationTests` against the real Hub HTTP endpoint.
The [review-fix validation record](review-validation.md) reports the new Windows/Linux regression
results separately from these historical live outputs.

For a fresh demonstration, follow the [v2 runbook](../../runbooks/durable-recovery.md): wait for
the valid pending review, save before/after status, capture the worker restart, then poll and
save the terminal result. Keep raw captures in ignored runbook artifacts; review/redact them
before adding public evidence. The earlier live observations remain implementer-reported.
