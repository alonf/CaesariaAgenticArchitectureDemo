# Stage 4 — Memory

Deck anchor: `CASE_MEMORY` (case-memory demo).

## Goal

Add `DemoStage.Memory`: the agent recalls its own closed cases across sessions as **hypotheses**.
Session gave the agent this conversation; Knowledge gave it the organization's records; Memory
gives it its own accumulated experience - and teaches that memory is a lead, never evidence.

## Scope

- `ClosedCase` + `ICaseMemoryStore` with a deterministic token-matched in-memory implementation
  (word-boundary tokens, stop words excluded, at least two shared meaningful terms, top three
  matches). The seam exists so an embedding-backed store (IEmbeddingGenerator + a vector store)
  can slot in without changing the provider - deliberately not demoed: at one or two cases
  semantic search is indistinguishable on stage, and Memory's differentiator is provenance, not
  retrieval machinery.
- `CaseMemoryProvider` - the demo's first **hand-written `AIContextProvider`** (Knowledge used the
  SDK's `TextSearchProvider`). It overrides `ProvideAIContextAsync` and recalls cases matching the
  latest user message. Trusted static rules travel in `AIContext.Instructions` (verify live state,
  search real evidence, cite a recalled case id only as an unconfirmed analogy); the recalled case
  content - operator input and earlier model output - travels separately as a JSON reference-data
  message the rules mark as data, never instructions. The separation reduces prompt-injection
  risk from stored text; it cannot make probabilistic model behavior certain.
- New `CASE_MEMORY` snippet region composing the provider at the Memory stage or later.
- **L-528 fixture**: the Energy Hub exposes a second streetlight as a deterministic read-only twin
  in the same on-during-daylight-with-override anomaly, but with no maintenance history - and the
  work-knowledge search is now asset-scoped, so L-417's WO-8732 is never returned for a question
  naming another asset.
- **Close case flow**: Command Center gains "Close case (record to memory)" (summary assembled
  deterministically from the answer - no extra model call) and "Ask about L-528 (new session)".
  The response carries `RecalledCases`; the UI renders them as amber **Hypothesis** cards, visually
  distinct from the green evidence citations.
- DemoControl gains a Case Memory panel (closed-case list + presenter clear) via
  `GET/POST /api/operations-agent/cases[/clear]`; closing a case requires the Memory stage (409).

## Lecture beat

1. At the Knowledge stage flow's end (L-417 explained via WO-8732), switch to **Memory** and click
   **Close case** - the conclusion is recorded as CASE-1.
2. Click **Ask about L-528 (new session)** - a different streetlight, same symptom, no evidence.
3. The agent recalls CASE-1 as a hypothesis ("a similar case was a post-maintenance override"),
   still calls `get_streetlight_state` for L-528, still searches work knowledge, finds nothing,
   and answers honestly: plausible cause by analogy, unconfirmed.
4. Point at the panels: green cards = evidence (organization's records), amber card = memory (the
   agent's own past conclusion). **Memory ≠ evidence** mirrors Session's "context ≠ authority".

## Verification

- Deterministic tests cover the case store (ids, cross-asset recall, no-match, clear), the
  asset-scoped work-knowledge search, the L-528 fixture (state, no activity, commands rejected),
  and the stage catalog/snippet registration. One test records the symptom the Command Center
  actually files for the L-417 anomaly and asserts the L-528 question recalls it: recall is by
  shared terms, so the symptom must be phrased in the words the question uses, or the beat shows
  nothing.
- The recall-plus-honest-gap answer is model-dependent and verified live.
