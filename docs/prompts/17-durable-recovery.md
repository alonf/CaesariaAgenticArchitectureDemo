# Optional beat 17 — Durable agent and repair recovery

Requirement: the duty supervisor changes shifts or the worker restarts while a repair awaits approval.
The conversation and the pending operation must still exist afterward.

Use the existing Forgotten Override story at Workflow or later. Keep the Stage 8 MAF graph as the
earlier explanation of explicit orchestration. This beat uses a separate Functions composition,
an actual MAF agent backed by Durable Entities, and a Durable Task orchestration with activities.
It introduces no new `DemoStage` grant.

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
It does not claim Entra user isolation, a deployed Functions app, durable Hub storage, or an
integrated Command Center approval panel. See the [runbook](../runbooks/durable-recovery.md) for
setup, verified behavior and the remaining cloud gates.
