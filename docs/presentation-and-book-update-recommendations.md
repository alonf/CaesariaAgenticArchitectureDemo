# Presentation updates, then book updates

Based on the November 17 implementation batch. The September PDFs remain historical releases.
PDF locators below are animation-export pages, not verified PowerPoint slide numbers. Use the
editable sources and stable code anchors when applying these recommendations.

**Agreed scope — November 17:** demonstrate Durable Functions locally; explain Azure hosting in
the presentations and then the book. Add a local/Azure comparison covering the deployed Function
App portal, managed scheduler, identity/RBAC, networking, scaling and monitoring. Label the cloud
architecture as proposed and unverified. Full Azure Functions deployment is deferred. Record the
v2 local restart before claiming live verification; do not promise an agent-specific dashboard
view until this composition and emulator version have been checked.

**Ignite review — November 18:** the [dated announcement and gap inventory](product-status/ignite-2026-gap-review.md)
maps the new capabilities to the actual H08/W20 PDF pages. Add Foundry-integrated customer AKS
(announced preview; rollout unverified), the expanded IQ/context layer and external-agent
observability. Update the improvement loop with Insights service GA, expert annotation and
reviewed optimizer candidates. Our local Functions scope remains unchanged.

## 1. Presentations

### H08: implementation and runtime

1. Refresh the setup/version slide and code screenshots to .NET 11.0.100, MAF 1.28, MCP 2.4 and the
   tested dependency set in `docs/product-status/net11-upgrade.md`. Keep beta/preview labels on
   selected Foundry packages and experimental SDK surfaces. Insights service GA was announced
   November 17; service maturity and SDK maturity are separate facts.
2. Re-export marked code for agent creation, function tools, workflow and A2A from the compiled
   source. Mention that A2A's blocking HTTP call can use the streaming model path internally; the
   existing domain contract and time budget still govern it.
3. At Hosting (PDF pp. 45-49), add a concise Routines beat: timer -> read-only investigation ->
   recommendation. Show the manifest, disabled creation, real dispatch/run evidence and unchanged
   lamp. Rehearse the unattended identity before using this as a live service demonstration.
4. At assurance/Agent DevOps (pp. 52-55), follow ASSERT with Insights: generated finding -> trace ->
   reviewed hypothesis -> regression -> measured change. The current project has no monitor, so
   prepare real evidence before producing this slide's screenshots or promising a live finding.
5. Add the verified **local** Durable Functions recovery comparison: persistent agent session plus
   a separate durable process. Ask “What happened to the repair you started?” after killing its
   worker, without including the operation ID. Wait for an actual `AwaitingApproval` review before
   restart; then approve and poll to a terminal business result. Show the matching Hub receipt. Contrast the
   original Stage 8 MAF graph with the new Durable Task orchestrator; they share policy/gateways,
   not an execution engine. Show the Security Operation variant refusing stale approval. Name the
   local supervisor credential honestly; Entra/Command Center integration remains a cloud gate.
   Add a short recovery-outcome table: confirmed receipt -> success; pending receipt -> read-only
   reconciliation; unconfirmed effect -> `OutcomeUnknown`, never "not executed." Mention bounded
   read retries and fresh approval when explicitly retrying a pre-dispatch terminal failure.
   Use [the runbook](runbooks/durable-recovery.md) and its evidence bundle. The retained captures
   document v1 outputs; rehearse and record the isolated v2 build before claiming a live v2 restart.
6. Refresh verification claims from the dated [upgrade record](product-status/net11-upgrade.md).
   The review fixes passed **447 deterministic tests on Windows and Linux** (four existing IDE
   skips each), plus six polling-helper checks. These are local implementer results.
   The pre-review slice passed **428 deterministic tests on Windows and Linux**, with four live
   IDE tests skipped on each; keep this distinct from the review-fix results. The first batch passed 41 Windows
   helper, 16 Python adapter and seven Node tests. Preserve these as
   dated results, not a guarantee about every environment or future model response.

### W20: architecture and engineering practice

- In the 2 AM/deterministic spine discussion (pp. 23-34), explain that a trigger starts investigation
  without increasing authority. Keep the actual demonstration on L-417; the water story remains
  conceptual/deferred.
- In the two loops discussion (pp. 36-38), use real Insights evidence to connect runtime observation
  to reviewed ASSERT cases. The model's diagnosis remains a hypothesis.
- In the runtime/identity discussion (pp. 40-46), separate activation (Routines), execution recovery
  (the local Durable Functions recovery beat) and engineering feedback (Insights). None replaces Hub authorization.
- Keep the 30-minute demo budget: replace generic hosting/evaluation explanation with short prepared
  beats. Put cold setup, monitor generation and the full restart/failure matrix in the lab.

Reconcile speaker-note anchors with the actual editable decks, visually verify exports, and publish
new named assets without overwriting the conference release or changing its QR destination.

## 2. Companion book

After the revised demonstrations and presentations are rehearsed, prepare a new book edition:

| Current edition location | Revision |
| --- | --- |
| Chapter 7, starts p. 78 | Update framework baseline and compile all affected agent-construction listings. |
| Chapters 8-11; Chapter 10 starts p. 116 | Refresh tools, sessions, MCP and A2A APIs. Explain graph execution, checkpoint storage and durable runtime execution separately. Add signed reviews, bounded read retries, fresh attempts after pre-dispatch failure, fixed-revision execution and pending-receipt reconciliation. Explain nullable unknown outcomes and versioned histories; do not claim exactly-once effects. |
| Chapter 12, pp. 146-162 | Add unattended Routines activation, its identity boundary and real run evidence. Update hosting dependency history; retain the 1.19 port defect as a dated observation. Add the verified local Functions comparison, entity-owned versus service-owned history, new Functions SDK and separate host/worker dependency graphs. |
| Chapter 13, pp. 163-183 | Preserve ASSERT and add the Insights evidence-to-regression loop. Cover empty results, missing monitors, incomplete retrieval, version attribution and sensitive telemetry. |
| Chapter 14, pp. 184-198 | Update .NET 11 setup, per-user SDK selection, reliable Testing Platform commands, Linux image distro and CI. Add Routines/Insights exercises and the local durable restart lab: wait for approval before restart, poll terminal after a decision, capture evidence, inject a transient read failure and interrupt an in-flight command. Include refusal, stale revision, timeout and unknown outcome. Keep cloud integration separately scoped. |
| Appendix A, pp. 199-201 | Replace the version inventory with the tested matrix. Distinguish GA service announcements from preview packages and individually date maturity claims. |

Update edition metadata, supported source commit, links, screenshots and glossary. Preserve the
original book's release/hash. Editable deck and manuscript sources are still needed for actual
layout changes; this file is the recommendation, not a rewritten presentation or book.

## Slide after Hosting: durable recovery (November 18)

Proposed title: **The worker stops. The approval survives.**

Place immediately after the Hosting slide(s). Show the short sequence:
**Request → Awaiting approval → Worker crash → Same session resumes → Supervisor decision → Hub result**.

Use Director beat **15. Durable recovery**, after Evaluation. Prepare foundation in the Director, run the conversation and approval in Command Center, and switch back only to crash/restart the worker. Continue the same conversation explicitly in Command Center. Its panel supplies the narration, timeline, review and result. UI beat numbering does not dictate slide placement.

Speaker notes: both Foundry and Functions can persist conversation/session state. Foundry's opt-in
resilient execution re-enters a handler; framework/application checkpoints preserve progress.
Durable Functions explicitly models process replay, timers and approval events. Our local rehearsal
interrupts only the Functions worker; the scheduler and city remain available. The agent cannot
approve. The workflow revalidates the bound Hub revision before issuing the command. Neither host
alone guarantees exactly-once device effects. A Foundry agent calling a durable repair workflow is
a possible combined architecture, not the current implementation.

After the slide/demo is rehearsed, add the same comparison and screenshot sequence to the book's
hosting/workflow discussion. Include decline, expiry and OutcomeUnknown, with the local boundary
and links to the runbook and evidence. This is a recommendation; the published slides and book
have not been edited here.
