# Lab 04 — evidence that explains the anomaly

Actual Windows app/model execution, baseline843e0d0. Work knowledge is the deterministic local simulated provider. Synthetic narration for review.

## 01 — From current state to an explanation
**Picture:** Context-engineering slide, then main Knowledge walkthrough.

**Narration:** A current-state tool can tell us that a lamp is on and a manual override is active. It cannot tell us the maintenance history that led to that state. In the previous lessons we added a tool and a conversation. Now we add organizational knowledge. Our outcome is to distinguish a live observation, a retrieved work record, and the conclusion the agent draws from them. This is still the same context-engineering loop: our code supplies a focused search capability, and the model can request evidence before answering.

## 02 — Ask the question that needs knowledge
**Picture:** Knowledge preparation, Walkthrough, initial Ask, then Why.

**Narration:** Prepare Knowledge in Director. Check that the daylight anomaly and work evidence are both present. Return to the Command Center and read the top-right Walkthrough. First ask whether L-417 is on. That narrow question needs only a Hub read in this run. Then ask Why in the same session. Now the trace adds search work knowledge alongside the fresh streetlight read. Two tools ran over two model round trips in this recording. The exact count is not the lesson; the new evidence boundary is.

## 03 — Read the source, not just the answer
**Picture:** Scroll the answer, trace, retrieved cards; expand technician note.

**Narration:** The answer identifies work order W O eighty-seven thirty-two and its technician note. Open the note. It says the light was left on for post-maintenance verification, with the override to be cleared after an inspection confirms stability. Notice the provenance label: simulated work knowledge. This is a reproducible local fixture, not a live maintenance system. The cards show what retrieval returned. The cited-in-answer badge shows that the answer mentioned the identifier. That badge does not prove that every claim faithfully follows the source. Read the record and compare it with the conclusion.

## 04 — The provider joins the context
**Picture:** VS Code KnowledgeCapability, callback, options and ContextProviders.Add.

**Narration:** Open KnowledgeCapability in the Operations Agent service. TextSearchProvider receives a callback that searches our IWorkKnowledgeSearch abstraction. The callback preserves a source name, identifier, provenance label, text, and time. The options select on-demand function calling and name the search tool. Adding the provider to the composition makes it part of the agent's available context machinery. Retrieval results are also collected for the UI trace. The local implementation is deterministic and in memory. Swapping the search implementation is a separate integration task; this lesson does not claim to demonstrate a production search index.

## 05 — Withhold evidence and start fresh
**Picture:** Director checkbox off; fresh main-app Ask and Why.

**Narration:** Now test the limit. In Director, uncheck the seeded work evidence. Return to the main app and start a fresh conversation before asking Why. A fresh session matters: otherwise the previous record could still be in conversational history even when the search source is empty. The operational fixture remains on with an override. We changed the availability of explanatory evidence, not the lamp. This comparison lets us see whether the answer respects the difference between a known state and an unknown maintenance explanation.

## 06 — A useful answer can include uncertainty
**Picture:** Missing-evidence answer, three-tool trace, empty retrieval card.

**Narration:** This run searched twice and read the Hub, over four model round trips. The retrieval card explicitly says no evidence was returned. The answer says it cannot identify who authorized the override or why from the available work records. It still reports the actual operational facts. That is a useful boundary: missing retrieval is not permission to invent a ticket, and an empty search is not proof that no maintenance ever happened. We can explain what was observed while being precise about what remains unverified.

## 07 — Pause and compare the evidence
**Picture:** Exercise card and restored evidence switch.

**Narration:** Pause and reproduce both paths. With evidence present, open the technician note and identify which sentence supports the explanation. With evidence absent, start a new conversation and inspect the empty retrieval result. Save the correlation identifiers and verify that the Hub state did not change. Restore the evidence switch before continuing. In the code, investigate how a query naming another asset avoids receiving L-417's work order. Find how the UI awards a citation badge, and explain why that is weaker than validating the answer. We now have current facts and organizational evidence. Next we will reuse a past conclusion as a lead, without confusing memory with proof.
