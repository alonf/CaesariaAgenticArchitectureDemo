# Optional beat 17 — Durable agent and repair recovery

Requirement: the duty supervisor changes shifts or the worker restarts while a repair awaits approval.
The conversation and the pending operation must still exist afterward.

Use the existing Forgotten Override story at Workflow or later. Keep the Stage 8 MAF graph as the
earlier explanation of explicit orchestration. This beat uses a separate Functions composition,
an actual MAF agent backed by Durable Entities, and a Durable Task orchestration with activities.
It introduces no new `DemoStage` grant.

## Guided two-screen flow

Add **15. Durable recovery** at the end of Demo Director's list, after Evaluation, and link it
from Lights On Reported by a Client. This is a presenter beat number, not a new DemoStage grant.

The Director prepares foundation only: check/start the worker and existing emulators, apply the
scenario and enable Workflow if needed. It must not call the model or start a repair. The live
conversation, approval, timeline and result belong in **Command Center**, using its existing
agent-answer and approval styling. Return to the Director only to crash/restart the owned worker;
then explicitly continue the same conversation in Command Center before approving there.

Retain the session before model execution and discover operations through the typed session query.
Require an unexpired AwaitingApproval review before crash. Confirm the original session, review
and deadline after the main-app follow-up. Keep operator and supervisor credentials server-side,
with distinct authorization on the local adapter and Functions boundaries. No arbitrary process,
command or endpoint input. Page refresh preserves the rehearsal. Prevent accidental scenario/reset
while a repair is pending; unknown outcomes require inspection before a new run.

Recommend a slide after Hosting, regardless of the UI's final beat number. Both Foundry and
Functions persist sessions; Foundry also offers opt-in resilient handler reentry. Explain explicit
workflow replay, timers, approval events and revalidation rather than claiming only Functions
remembers. A Foundry agent calling a Functions workflow remains an extension, not this demo.

## Manual fallback flow

1. Ask the Functions agent to recover L-417. The agent returns a real process ID and has no approval
   or command tool. Poll until `AwaitingApproval` with a valid review before restarting; an ID only
   proves scheduling. Show the separate session/process in the scheduler dashboard.
2. Stop only the Functions worker. Leave the scheduler and the deterministic city services running.
3. Continue the same session: “What happened to the repair you started?” Supply no process ID in
   the prompt. The answer must come from the status tool, using the remembered ID.
4. Review and approve through the separate supervisor endpoint. Show the revalidation, confirmed
   Hub receipt and final state by polling to a terminal result. A queued decision or completed chat
   turn is not business completion. `OutcomeUnknown` means stop and inspect the Hub, never "not executed."
5. Alternate: apply Security Operation before approval. The changed Hub revision invalidates the
   old approval; required lighting stays on. With no decision, the process expires after five minutes.

Explain the regression evidence: one transient read is retried; a pending command is reconciled
without resending. Explicit retries of terminal pre-dispatch failures create a new approval.
Use the isolated v2 task hub for this build; do not replay the earlier v1 activity history.

The current composition is a **local rehearsal** with separate operator/supervisor credentials.
The Command Center approval panel uses local server-side credentials. It does not claim Entra
user isolation, a deployed Functions app, or durable Hub storage. See the [runbook](../runbooks/durable-recovery.md) for
setup, verified behavior and the remaining cloud gates.
