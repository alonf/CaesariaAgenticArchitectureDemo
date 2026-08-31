# Stage 3 — Knowledge (H08 slides 20/21)

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
- New `H08_S20_KNOWLEDGE` snippet region, registered with the demo breakpoints.

## API drift note

The string-parameter `AsAIAgent(...)` overload cannot attach `AIContextProviders`, and the SDK's
provider chat client is internal, so agent creation moved to the `ChatClientAgentOptions` form (the
shape H08 slide 24 shows) with the model supplied through `ChatOptions.ModelId`. The slide 13 and
slide 20 concepts are otherwise implemented with the current installed APIs.

## Lecture beat

1. Apply the **Forgotten Override** scenario; switch to the Knowledge stage.
2. Ask **"Is L-417 on?"** — on, with the schedule anomaly and override noted.
3. Click **"Why?"** — the capability trace now shows both `get_streetlight_state` *and*
   `search_work_knowledge`: the agent re-verifies live state, retrieves WO-8732 and the technician
   note, and explains the override - citing the evidence identifiers.
4. Optional forbidden-behavior beat: withhold the evidence in DemoControl, ask again in a new
   session - the agent reports that the search found nothing and sticks to operational facts; it
   never invents a ticket.

## Verification

- Deterministic tests cover the simulated search (match, no match, withheld evidence) and the stage
  catalog.
- Verified live: the two-turn beat produced an answer citing WO-8732 with both tools in the trace;
  the withheld-evidence ask searched, found nothing, and said so.
