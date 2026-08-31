# Stage 3 — Knowledge

Deck anchor: `KNOWLEDGE_RETRIEVAL` (knowledge-retrieval demo).

## Goal

Add `DemoStage.Knowledge`: the Operations Agent retrieves organizational work knowledge on demand,
so **"Why?"** gets an evidence-grounded explanation. Operational systems tell us *what* is
happening; knowledge may explain *why*.

## Scope

- `IWorkKnowledgeSearch` with a normalized `WorkEvidence` shape shared by the simulated provider and
  the future optional Work IQ provider, so the agent's grounding is identical for either source.
- `SimulatedWorkKnowledgeSearch` seeds the story's punchline: work order **WO-8732** (luminaire
  maintenance on L-417, override engaged) and its technician note (*"Left the light ON for
  post-maintenance verification."*).
- The provider joins the agent as the SDK's `TextSearchProvider` in `OnDemandFunctionCalling` mode:
  the model decides when to search; nothing is eagerly stuffed into the prompt.
- The retrieval capability is composed only at the Knowledge stage or later; earlier stages keep
  their exact prior behavior.
- A presenter toggle (DemoControl "Work Knowledge" panel) withholds the seeded evidence to show the
  agent reporting missing evidence instead of inventing a work order.
- New `KNOWLEDGE_RETRIEVAL` snippet region, registered with the demo breakpoints.
- **Retrieved-evidence trace in Command Center**: the search lambda records the `WorkEvidence` the
  provider returned, the API carries it as `OperationsAgentResponse.Evidence`, and the UI renders
  compact cards under the answer. A card is marked **"Cited in answer"** when the answer text
  contains its identifier (an exact check against IDs we own, never prose parsing); clicking a card
  expands the full summary and, when a `SourceUri` is present, a link to the original item. If the
  search ran and returned nothing, the panel says so. Retrieval and citation are different claims:
  the cards show what the search returned, the badge shows what the agent grounded its answer in.

## API drift note

The string-parameter `AsAIAgent(...)` overload cannot attach `AIContextProviders`, and the SDK's
provider chat client is internal, so agent creation moved to the `ChatClientAgentOptions` form (a
shape the deck also shows for later demos) with the model supplied through `ChatOptions.ModelId`.
The agent-creation and knowledge-retrieval concepts are otherwise implemented with the current
installed APIs. Details in `docs/product-status/api-drift.md`.

## Lecture beat

1. Apply the **Forgotten Override** scenario; switch to the Knowledge stage.
2. Ask **"Is L-417 on?"** — on, with the schedule anomaly and override noted.
3. Click **"Why?"** — the capability trace now shows both `get_streetlight_state` *and*
   `search_work_knowledge`: the agent re-verifies live state, retrieves WO-8732 and the technician
   note, and explains the override - citing the evidence identifiers. Evidence cards appear under
   the answer with "Cited in answer" badges; click WO-8732/NOTE-1 to reveal the punchline note
   *"Left the light ON for post-maintenance verification."*
4. Optional teaching point at the evidence panel: retrieval ≠ citation - the cards show what the
   search returned; the badge shows what the agent actually relied on. Honest agent UIs keep those
   claims separate.
5. Optional forbidden-behavior beat: withhold the evidence in DemoControl, ask again in a new
   session - the agent reports that the search found nothing and sticks to operational facts; it
   never invents a ticket. The evidence panel shows "search ran and returned no evidence".

## Future Work IQ connector notes

The evidence cards and citation badges sit on the `IWorkKnowledgeSearch` seam, so a live Work IQ
provider slots in with a DI registration change - but its normalization layer carries three rules:

- **Manufacture citable IDs.** The badge matches evidence IDs inside the answer text, and the
  agent's instructions require citing identifiers. A raw Outlook message ID is a 70-character
  opaque string the model will never reproduce - the connector must synthesize short,
  human-citable IDs (for example `EMAIL-0831-1`, `TASK-142`) when the source lacks one.
- **Tame content.** Truncate long bodies into `Summary`; demo against a curated account - card
  content lands on a projector.
- **Fill `SourceUri`** with a link to the original email/task/document so the expanded card offers
  "Open source item". The simulator leaves it null.

The DemoControl withhold toggle is a simulator concept; with a live provider, evidence presence is
whatever the mailbox actually contains.

## Verification

- Deterministic tests cover the simulated search (match, no match, withheld evidence) and the stage
  catalog.
- Verified live: the two-turn beat produced an answer citing WO-8732 with both tools in the trace;
  the withheld-evidence ask searched, found nothing, and said so.
