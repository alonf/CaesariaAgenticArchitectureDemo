# Lab 15 — preserve a pending process across worker loss

Real local Windows execution; baseline `4e5a6d3`. Synthetic review narration. Holds are labelled.

## 01 — Conversation and process are different state
**Picture:** Main app walkthrough, then registration.

**Narration:** Earlier, our planned workflow gate lived in the Operations Agent process. Killing that process would not preserve its in-memory approval. This lesson adds an actual Agent Framework agent hosted in Durable Functions. Conversation history lives in a durable entity, while a separate repair orchestration persists its execution history, approval event and deadline. Our outcome is to restart the worker, continue the same conversation, approve the original review, and prove the repair at the Hub. This does not convert the earlier workflow graph into a durable graph.

## 02 — Prepare, ask and wait for a real review
**Picture:** Director foundation and main application request.

**Narration:** In Director, select beat fifteen and Prepare foundation. This starts the owned worker and existing local emulators, restores the daylight anomaly and makes Workflow capability available. It does not call the model. In Command Center, expand the durable Walkthrough and ask the durable agent to recover L-417. The model reads the Hub and starts a supervised operation. Acceptance is not repair. Wait for Awaiting Approval and the real review: revision three, with its existing expiry. The lamp is still on. An operation identifier alone would not prove that the approval was pending.

## 03 — Interrupt only the worker
**Picture:** Director crash and restart, then continue in main UI.

**Narration:** Switch briefly to Director and select Crash worker and restart. The control confirms forced termination, then starts a new owned process. The scheduler and city services remain available. Return to Command Center. Continue the same conversation after restart sends the visible question without an operation identifier. The agent uses its remembered operation to query actual status. The reply says it is still awaiting supervisor approval. The controller confirms that the original review and deadline are unchanged. Remembering an identifier and preserving a pending process are related, but distinct, responsibilities.

## 04 — Approve and verify the effect
**Picture:** Approval click, terminal result, direct Hub JSON.

**Narration:** Now approve the original review. Watch the map change, but keep checking. The timeline first records queued approval and revalidation, then the terminal business result. Command executed is yes, and resolved is yes. The timestamped Hub evidence reports the light off, override off and revision four. Open the Energy Hub endpoint independently: reported on is false, manual override is false, and the succeeded receipt carries this operation correlation. These are the deterministic service observations that support the outcome. A completed orchestration by itself would not establish that the physical effect succeeded.

## 05 — Read the two durable components
**Picture:** Agent registration, orchestration event and revalidation.

**Narration:** In the Functions Program, Configure Durable Agents registers the agent and its retention settings. Its tool surface contains a state read, start recovery, and get recovery status. There is no approval or device-command tool. In Recovery Orchestration, authoritative reads run as activities with bounded retries. The review uses orchestration time and a deterministic challenge, then waits for an external supervisor event or durable timer. On approval, it rereads state and rejects a changed revision before dispatch. The activity checks the signed review, current stage, policy and expiry. Replayable orchestration code performs no direct external I O.

## 06 — A lost response is not proof of no effect
**Picture:** Receipt reconciliation and explicit retry policy.

**Narration:** The difficult case is losing the response after dispatch. Recovery Activities first looks for the matching Hub receipt. A pending receipt means reconcile without resending. If the revision changed but no matching receipt is available, the outcome is also uncertain; it does not become command executed false. The orchestrator polls with a bounded deadline and can report outcome unknown. Recovery Tools allows an explicit fresh attempt only for a failed operation with a trustworthy pre-dispatch phase, with fresh approval and a three-attempt limit. The recording demonstrates worker loss during approval, not these additional fault cases; their model-free tests provide separate evidence.

## 07 — Pause, reproduce and define the boundary
**Picture:** Exercise card.

**Narration:** Pause and repeat the worker restart. Check Awaiting Approval before the crash, then compare the original revision and expiry after continuing. Try declining a fresh request and verify no new command receipt. If an approval expires, let the operation reach a terminal result before preparing again. Never use the scheduler Terminate action as a worker restart. Keep the scheduler and city services running, and inspect an unknown outcome before any new command. This local rehearsal proves the demonstrated recovery boundary. Azure identity, cloud operations, scale and end-to-end availability need their own deployment and verification.
