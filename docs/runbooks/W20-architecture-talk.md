# W20 runbook - The Agentic Revolution

Seventy-five minutes, with about thirty of demo. One continuous L-417 story in four acts; the
implementation stages are not shown one by one. Each act names the beats it borrows from the
switchboard, and what in the act is a slide rather than a click.

## Act 1 - Deterministic city operation (0-4 min)

Beat: **Deterministic**, fixture Lights On.

- Show L-417 on the map: reported on, schedule off, the customer report, no incident.
- Click Restore Scheduled Mode: SmartPole confirms, the lamp goes off, the timeline records it.
- The city operates without AI. Everything that follows joins this architecture.

Prepare the next beat afterwards: the director notices the lamp is now off and re-applies the fixture.

## Act 2 - Identity-aware investigation (4-15 min)

Beats: **Investigation Agent**, **Session**, **Knowledge**, all on Lights On.

- Ask agent: one Energy Hub tool call. Ask "Why?" in the same session: answered from context.
- At Knowledge, ask again: the trace adds the work-knowledge search and the evidence cards.
- Identity, said honestly: the local agent calls the Energy Hub as a neighbour on a private network,
  under the presenter's own sign-in. The agent's *own* Entra identity is the hosted agent's story
  (act 3's walkthrough, the hosting guide's identity section). Show the identity and its
  permissions from the prepared screenshots, not from the local run.

## Act 3 - Governed corrective execution (15-24 min)

Beats: **MCP Tools**, **Interactive Input**, **Workflow**.

- MCP Tools: flip Tools to MCP; same capability, discovered at runtime.
- Interactive Input: the restore tool pauses for the operator; deny, then approve.
- Workflow: from this stage the direct write is withdrawn from the agent and the governed
  operation replaces it. Deny at the gate, then approve; the outcome is verified, not assumed.
- "A direct write denied for the Operations Agent" is shown as the capability *withdrawn* at the
  Workflow stage (the tool is no longer in its toolbox), not as a runtime authorization refusal.
  The Hub-side app-role check exists only on the hosted path.

## Act 4 - Behavioral proof (24-30 min): slide, not click

There is no runnable baseline-versus-governed comparison in this repository, and no recorded
result either. What the repository ships is the specification: `eval.yaml`, the 15-question
dataset under `datasets/caesarea-operations-behavior/`, and the rubric under
`evaluators/caesarea-operations-behavior/`. The dataset measures the hosted agent's uncertainty
handling on unseeded assets and is kept as an honest, imperfect measurement rather than tuned to
pass. Show the dataset and the rubric from the repository; if you have run the evaluation against
your own hosted agent, show your numbers and one trace captured during rehearsal (see "Fallback
artifacts" in the [runbook index](README.md)). Close on the distinction the act is for: unit tests
prove the restore works, identity decides who may ask, and behavioral evaluation asks whether the
agent should have requested it.

## Short prepared walkthroughs

| Topic | Live? | How |
|---|---|---|
| Multi-agent consultation | Yes - **Multi-Agent** beat, Security Operation, consult OFF then ON | The second agent adds authority to know, not a capability. |
| A2A delegation | Yes - **A2A Delegation** beat | The peer answers freely and cannot disclose what it never held. |
| Hosting in Foundry | Yes when deployed - **Hosting** beat | Same code, the platform's identity, Work IQ as the presenter. Screenshots as the fallback. |
| Event-driven activation | No | Slide only; every run here starts from a click. |
| Entra lifecycle, sponsor, access reviews | Terminal and portal - segment 13 | Depends on the tenant; screenshots ready. |
| Runtime policy and the agency budget | No | Slide only; not implemented as middleware. |
| The 2 AM water leak | No | Deferred. |
