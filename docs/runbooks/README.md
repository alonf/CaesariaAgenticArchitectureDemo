# Runbooks

One page per lecture, separating what runs live from what is shown on a slide, and naming what
this repository does not demonstrate at all. The stage catalog and the switchboard's director are
the source for every live beat; the runbooks only put them in the order each talk tells them.

- [H08 - the code lecture](H08-code-lecture.md): slides 13 to 49, one runnable stage per API.
- [W20 - the architecture talk](W20-architecture-talk.md): four acts in thirty minutes, plus the
  short prepared walkthroughs.

## Before either lecture

1. `./scripts/Start-CaesareaDemo.ps1` - checks the SDK, the Azure sign-in, the OneDrive work order
   for the hosted beat, then starts the Aspire host.
2. Open the switchboard (DemoControl.Web). Choose a beat; the director shows what it will apply and
   asks for confirmation. Tick **Rehearsal** to skip the confirmations during practice.
3. Open the Command Center (CommandCenter.Web) on the projector. Its **Walkthrough** button shows
   the current beat's steps and marks each prerequisite met or unmet.

A beat that reads **Ready to present** on the switchboard is ready; **Not verified** means a check
could not run, and the readiness list says which.

## Not in this demo

Said here once so neither lecture promises it from the stage.

| Advertised | Status | What to do on stage |
|---|---|---|
| Group chat and handoff (H08 slide 40) | Not implemented. The only multi-agent modes that run are agent-as-tool (Multi-Agent beat) and A2A delegation. | Compare the four modes on the slide; run the two that exist. |
| ASSERT finale - a failing baseline against a passing governed run (W20 act 4) | Not a runnable comparison. `eval.yaml` and the 15-question dataset exist and are meant as an honest, imperfect measurement of the hosted agent, not as proof of the L-417 beats. | Show the dataset and a recorded result; say plainly that it measures uncertainty handling on unseeded assets. |
| Runtime policy middleware and the agency budget (H08 slide 47, W20 "governance stack") | Not implemented as middleware. The governance that exists is the three approval mechanisms, stage gating, and Hub-side authorization. | Slide only. |
| Harness agent comparison (H08 slide 15) | Not implemented. | Slide only. |
| Protocol decision docs (H08 slide 45) | Documents, not code: [the hosting guide](../product-status/hosted-agent.md) records the Responses, Invocations and A2A decisions. | Slide only. |
| Event-driven activation (W20 walkthrough) | Not implemented; every run starts from an operator's click. | Slide only. |
| The 2 AM water leak (W20 part II) | Deferred; nothing in the repository. | Slide only. |
| Agent 365 lifecycle, sponsor and access reviews | A terminal-and-portal segment, not a stage: [segment 13](../prompts/13-agent365.md). Depends on the tenant. | Prepared walkthrough with screenshots as the fallback. |
