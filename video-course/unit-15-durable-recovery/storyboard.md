# Lab 15 — preserve a pending process across worker loss

Outcome: demonstrate the same durable conversation and original approval after a Functions worker restart, then verify the repair at Energy Hub. Recorded baseline 4e5a6d3 and docs/runbooks/durable-recovery.md. The actual restart/receipt evidence is retained in the companion files.

1. Explain conversation state versus execution history. Both Foundry hosting and Durable Functions can support state; this implementation uses durable agent entities plus a separate repair orchestration, not the unchanged in-process graph.
2. Director beat 15 prepares foundation only. Show main application Walkthrough and durable panel. Ask there, and explicitly wait for AwaitingApproval.
3. Show original review/revision. Director Crash worker and restart, keeping scheduler and city services alive. Return to main UI and continue the same conversation without inserting an operation ID.
4. Show same session, same original review and the enabled supervisor decision. Approve and poll to terminal business result, then verify actual Hub receipt and lamp state. Scheduling acceptance and completed orchestration alone do not prove repair.
5. Read ConfigureDurableAgents, the external-event/timer wait, bounded read retries, signed decision/revision revalidation and pending-receipt reconciliation. Explain outcome unknown and why commands are not blindly repeated.
6. Exercise: repeat with denial or an expired review; verify no unauthorized effect. Use Director controls rather than scheduler Terminate. Reset only once the prior outcome is known. No claim of cloud deployment, general exactly-once execution or scheduler-failure tolerance.
