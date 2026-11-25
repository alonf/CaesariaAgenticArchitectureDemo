# Lab 05 — a remembered case is a lead

Real Windows recording, baseline843e0d0. Synthetic review narration. No physical repair in this lesson.

## 01 — Three different kinds of context
**Picture:** Context-engineering slide, Memory walkthrough.

**Narration:** We have used conversation history to understand a follow-up, and organizational records to explain a maintenance event. Case memory adds something different: a past conclusion that may help investigate a similar symptom. It can be wrong, incomplete, or about a different asset. Our outcome is to reuse the L-417 investigation without treating it as proof about L-528. The model will still need current observations and relevant work evidence. Memory helps choose what to investigate; it does not replace that investigation.

## 02 — Save an explained case
**Picture:** Earlier stages expansion, Why, grounded answer, Close case and CASE-1 confirmation.

**Narration:** Prepare Memory with the daylight fixture and work evidence present. Read the main-app Walkthrough. If you refreshed the page, use Ask agent, expand Earlier stages, and ask Why to obtain the explanation again. Check that the answer actually refers to the work order and technician note before saving it. Click Close case, record to memory. The UI confirms CASE one for L-417. This button stores the investigation conclusion. It does not repair the lamp, create a maintenance record, or prove that the conclusion will apply to another asset.

## 03 — A new asset and a new session
**Picture:** Ask L-528, current-state answer, trace.

**Narration:** Now click Ask about L-528, new session. The map still displays L-417; read the question and tool arguments carefully. The request is about L-528. The trace shows a fresh Hub read for that asset and two work-knowledge searches. No relevant work evidence was found. The answer reports a manual override but does not establish why it was set. A new session has no previous conversational exchange to rely on, yet the case-memory provider can still supply a related past case from the application store.

## 04 — Inspect the analogy's provenance
**Picture:** Unconfirmed analogy paragraph, empty evidence card, expand amber CASE-1 card.

**Narration:** The answer explicitly calls CASE one an unconfirmed analogy involving a different asset, L-417. Below it, the evidence card is empty and the recalled-case card is labeled Hypothesis. Expand it and compare the stored conclusion with the original answer. These are two separate provenance paths: organizational records versus a previous investigation conclusion. The model did not find an L-528 work order simply because a similar case existed. This separation is what makes memory useful without presenting familiarity as certainty.

## 05 — How recalled data enters the call
**Picture:** CaseMemoryProvider instructions, then CreateRecallContext.

**Narration:** Open CaseMemoryProvider in CaseMemory dot C S. It finds the latest user question and asks the store for related cases. The demo uses bounded, deterministic token matching, not a vector database. The provider puts static behavioral rules in Instructions and serializes recalled case content into a separate reference-data message. The rules say to verify current state and treat the case as a hypothesis. This separation reduces the risk of stored text acting like new instructions, but a prompt is not a security guarantee. The actual tools and deterministic services still enforce their own boundaries.

## 06 — Remove the lead and compare
**Picture:** Director Clear case memory; new L-528 request; answer and absence of recalled card.

**Narration:** For a controlled comparison, clear this disposable demo memory in Director and ask about L-528 in a new session. The model still reads the Hub and searches work knowledge. It reports that the override's cause is unverified, and there is no recalled-case card. Clearing memory did not clear the Hub or repair the lamp. If an old analogy survives in an existing conversation, start fresh: clearing the case store does not erase messages already placed into a session. To recover the original demonstration, investigate L-417 and record its explained case again.

## 07 — Pause and investigate the store
**Picture:** Exercise card.

**Narration:** Pause and reproduce the comparison. Save an explained L-417 case, ask about L-528, and identify which statements are current facts, missing evidence, and analogy. Verify L-528 directly through the Hub. Clear the demo memory and repeat in a new session. Then inspect the store's capacity and matching threshold, the UI's truncation of the closing summary, and the boundary between trusted rules and stored text. Ask an AI chat how it would make this durable, then check whether its proposal also addresses provenance and stale conclusions. We have added experience across sessions without turning experience into operational truth.
