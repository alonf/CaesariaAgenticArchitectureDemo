# Lab 03 — a conversation remembers; the Hub knows

Application baseline 843e0d0. Synthetic review narration over actual Windows execution. The Mac model credential is pending; do not label this as Mac execution.

## 01 — Context across requests
**Picture:** Recall the context-engineering slide, then prepare Session in Director.

**Narration:** In the last lesson, we watched a model request a function, receive the Hub's response, and turn that response into an answer. Now we add continuity across user requests. A session gives the next request conversational context. It does not turn an old observation into a current fact. Our outcome is to explain the difference using the same streetlight before and after a repair. We will also inspect the actual tool trace, because the model need not choose the same sequence every time.

## 02 — Start a conversation
**Picture:** Main-app walkthrough, close it, Ask agent, then completed answer and session identifier.

**Narration:** Prepare Session in the Director, and return to the main Command Center. The lamp is on in daylight with a manual override. Open Walkthrough before starting. Ask whether L-417 is on. The answer is yes, and the trace records a fresh Energy Hub read. Below it is the session identifier. The follow-up button will send that identifier with the next question. This is how our application connects two HTTP requests to one conversation. It is not a new permission to change the city.

## 03 — Follow the observed behavior
**Picture:** Why click and completed answer; highlight actual tool count.

**Narration:** Ask Why in the same session. The question does not repeat the asset identifier, but the agent understands what it refers to. Notice the actual trace: one tool ran over two model round trips. The walkthrough predicted a response from history alone. That prediction is not a guarantee. In this recording the model read the Hub again. It identifies the manual override, but cannot verify the maintenance explanation because this stage has no work-knowledge search. We have continuity and a fresh observation, with a clear limit on what that observation can explain.

## 04 — Change reality, then ask again
**Picture:** Restore Scheduled Mode, show Off, then same-session Why and updated answer.

**Narration:** Now use the deterministic Restore Scheduled Mode action. This is the operator's action, not an agent repair. The Hub reports the lamp off and the override cleared. Ask Why again in the same conversation. This run performs another fresh read and says the current state has changed. That is the important evidence: the answer follows the new Hub observation. A different run might rely on history, so never use the existence of a session as your freshness check. Inspect the tool trace and compare the answer with the authoritative state.

## 05 — Where this session lives
**Picture:** AgentSessionStore fields and TryGetState, then FoundryOperationsAgent session restoration.

**Narration:** In AgentSessionStore, the demo keeps serialized session state in an in-memory dictionary. The store has a thirty-minute sliding lifetime and a maximum of fifty sessions. In FoundryOperationsAgent, a new request creates a session, while a known identifier restores one before RunAsync. After the run, the updated session is serialized and saved. These choices make the demo easy to inspect. They do not promise survival across a worker restart, and they are not the durable approval mechanism we will study later. Persistence, expiration, and concurrent updates are application design decisions.

## 06 — An explicit missing-session failure
**Picture:** Open saved actual HTTP 410 evidence in VS Code, readable status and detail.

**Narration:** Here is a separate API check, saved as evidence. We deliberately supplied an unknown session identifier. The service returned HTTP four ten, with a conversation-session-expired problem response. This was an actual request, not a simulated error. Recovery is to start a new conversation and obtain current evidence again. Do not silently pretend that the old conversation was restored. Also remember that changing stages can change which capabilities exist. Session history must not become a way to carry access from a more capable stage into a less capable one.

## 07 — Pause, prove, and reset
**Picture:** Exercise card; then Director preparation of the next fixture.

**Narration:** Pause and try it. Start a session, ask whether L-417 is on, and follow up with Why. Save the session identifier, correlation identifier, and actual tool count. Restore scheduled mode through the deterministic UI, then ask again in the same session. Verify the current state directly at the Energy Hub. Your answer may differ from mine; your explanation must account for the evidence you observed. Finally, reapply Lights On Reported by a Client before the next lesson. Investigate where expiration is enforced, what happens after a process restart, and how you would prevent conflicting simultaneous updates. We have separated conversation continuity from operational truth. Next we add evidence that can explain the maintenance history.
