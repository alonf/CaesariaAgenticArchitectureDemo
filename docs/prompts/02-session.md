# Stage 2 — Session (H08 slides 18/19)

## Goal

Add `DemoStage.Session`: the Operations Agent keeps conversational context across runs, so a
follow-up like **"Why?"** refers to the previous question. Session state is conversational context
only — it is never the authoritative operational state, which stays in the deterministic Hubs.

## Scope

- Add `Session` to the `DemoStage` enum and the presenter stage catalog.
- The ask endpoint accepts an optional `SessionId` and honors it only at the Session stage or
  later; every answer returns the session identifier a follow-up should send.
- Command Center shows an **Ask "Why?" (same session)** button once a session exists, plus a
  footnote with the session identifier and the "context, not authority" caveat. The button
  survives a transient failure so the presenter can retry.
- New `H08_S18_SESSION` snippet region, registered with the demo breakpoints.

## Implementation decisions

- **Sessions are stored serialized, never as live objects.** `AgentSessionStore` keeps the
  `JsonElement` produced by `AIAgent.SerializeSessionAsync`; each request restores its own private
  `AgentSession` through the current agent's `DeserializeSessionAsync`. No session instance is ever
  shared across requests or agent instances, so the per-request agent composition (the
  `H08_S13_AGENT` snippet) stays untouched.
- Concurrent saves for the same identifier are last-writer-wins: the worst case is one lost turn of
  conversational context, never corrupted state. The UI serializes its own requests anyway.
- Sessions expire after 30 idle minutes and the store is capped (oldest evicted). A follow-up that
  references an unknown or expired session fails explicitly with **410 Gone** instead of silently
  starting an empty conversation.
- The demo stage is continuously reconciled with the authoritative Command Center (10-second poll
  with an applied-at ordering guard), so neither a restarted Operations Agent nor a failed stage
  push can leave the local gate diverged - in particular, a change back to Deterministic takes
  effect here within one poll interval even if the push was lost.
- The Foundry credential warmup runs once, in the background, and only when an agent-enabled stage
  becomes active — the Deterministic stage keeps its promise that no AI credential is used.

## Lecture beat

1. Switch to the Session stage; ask **"Is streetlight L-417 on?"** — tool invoked, 2 round trips.
2. Click **Ask "Why?" (same session)** — the model resolves "Why?" to L-417 purely from session
   context: zero tool calls, one round trip, a few seconds.
3. Land the slide-19 line: *conversation continuity is useful context, not evidence about reality* —
   the agent did not re-check the city. Knowledge retrieval (Stage 3) is what closes that gap.

## Verification

- Deterministic tests cover the session store (round trip, unknown/expired identifiers, pruning,
  capacity) and the stage catalog.
- The two-turn continuation and evidence grounding are model-dependent and are verified live; they
  join the evaluation suite when it lands (requirements Section 25).
