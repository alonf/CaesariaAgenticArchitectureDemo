# Stage 12 — Hosting

Deck anchor: MAF Hosting, slides 43–44.

**Status: deployed and verified.** The spike is finished and the deployment half has run
([hosted-agent.md](../product-status/hosted-agent.md) records what was verified, and what it cost).
The agent runs on the Foundry hosted runtime with its own Entra identity, reads from an Energy Hub
container app behind Entra-authenticated ingress, and is released by
[deploy-hosted-agent.yml](../../.github/workflows/deploy-hosted-agent.yml). See
[deployment.md](../deployment.md) for the reproducible path.

This file holds the slide corrections and the presenter notes.

## Decisions on record

- **One hosted agent**, the Operations Agent. Slide 43's claim is that the agent code does not
  determine where it must run, and moving one agent proves it. A second multiplies the ACR, version
  and identity surface while proving the same thing.
- **`WorkforceAgent.Hosted` is a protocol probe, not a deployment.** It exists in the solution to
  prove the A2A-on-hosted findings in [hosted-agent.md](../product-status/hosted-agent.md) compile
  and run; it has no image in the release pipeline, no protected Workforce Hub ingress, no identity
  grants and no smoke test, and none of that is planned — the stage hosts one agent, above. Anyone
  tempted to deploy it starts by building that path, not by assuming it exists.
- **Reduced hosted composition, no tunnel.** Same code, instructions and skills; a toolset that does
  not need the city hubs on the presenter's laptop. This is a recorded deviation from §22's
  presenter step 6 ("compare same scenario behavior") — see the status doc for why, and for the
  dev-tunnel variant that remains documented but is not the stage path.
- **Deployment is Bicep + GitHub Actions**, not `azd up`. See [infra/README.md](../../infra/README.md).
  `azd` stays the inner-loop tool.

## The stage in the demo: the Habitat switch

The Hosting stage is in the scenario itself (`DemoStage.Hosting`), and its beat is a presenter
switch, exactly like Tools LOCAL/MCP:

1. **Prerequisites, once per machine and person.** The hosted agent deployed
   ([deploy-hosted-agent.yml](../../.github/workflows/deploy-hosted-agent.yml)); the work order in
   *your* OneDrive (`./scripts/New-CaesareaWorkOrder.ps1`) and indexed; and the Command Center told
   where the hosted agent lives. That last one is the start script's job — run the demo with
   `./scripts/Start-CaesareaDemo.ps1` and it resolves and stores
   `CommandCenterWeb:HostedAgent:ProjectEndpoint` in user secrets when missing, and checks the
   Azure sign-in the hosted call runs as. The endpoint names a tenant, which is why it is
   deliberately not in a committed appsettings file.
2. **The beat.** At the Hosting stage, the Command Center's agent rail gains *"Ask about L-417's
   work records"*. Press it with Habitat: LOCAL — the familiar answer from the simulated store.
   Flip **Habitat: LOCAL → FOUNDRY HOSTED** on the switchboard and press the same button: the
   answer now comes from Foundry's hosted runtime, cites work order WO-8732 from the presenter's
   OneDrive, and names **Microsoft 365 as the source of record** — a sentence only the real
   document contains, alongside the replacement-diffuser detail the simulated store never had.
3. **First use per person: consent, on screen.** Work IQ will not act for a person who has not
   consented. The first hosted ask returns an OAuth consent link instead of an answer, and the
   Command Center renders it as what it is — delegated access made visible, not an error. Open it,
   consent, ask again. Do this in rehearsal; on stage it is either a ten-second beat or already done.
4. **What the hosted agent's answers describe.** Its Energy Hub is the **deployed** one, and the
   cloud city is not driven by the switchboard — its demo surface is deliberately off. Instead, the
   cloud pole **boots into the forgotten-override situation**
   (`SmartPoleSimulator__StartWithForgottenOverride` in [apps.bicep](../../infra/apps.bicep)): lamp
   ON during daylight, override engaged, recent maintenance — the exact state WO-8732 in the
   presenter's OneDrive explains. So a live-state question hosted agrees with the local
   ForgottenOverride scenario on screen, and both of the hosted agent's sources tell one story. Say
   the boundary out loud anyway: the two cities are separate, they merely start in the same place —
   restoring the local lamp changes nothing in the cloud, and the panel footnote states it.
5. **Identity, without configuring any.** The Command Center mints the hosted call's token with the
   presenter's own credential (`az login` via `DefaultAzureCredential`), the platform reads the
   caller from it and injects `x-agent-user-id`, and Work IQ answers with what *that person* can
   see. A different presenter gets their own OneDrive's answer, with nothing edited anywhere.

The switch state lives on the Operations Agent service (`/api/operations-agent/habitat`), is
flipped from the switchboard, requires the Hosting stage, and resets to LOCAL on any stage
downgrade — same lifecycle as the tool-source toggle, for the same reason: the flip is the beat,
so re-entering the stage must start from LOCAL.

### The closing beat: ask it to turn the light off

Do this live — it lands harder than any slide. Ask the hosted agent, in any phrasing, to restore
L-417 / turn it off. Verified against the deployed agent: it runs the full investigation, *looks
for* a governed control tool, reports honestly that "no such control tool is available to me
here", and recommends escalating to the duty supervisor. Then say the ladder out loud:

1. **Instruction-only** — the Work IQ evidence-only rule: the permission could write; the prompt
   says don't. The weakest tier, and this demo names it as such.
2. **Approval-gated** — MRTR, the workflow gate, the intercepted tool: the capability exists and a
   human stands in front of it. The local stages' whole arc.
3. **Not composed** — the hosted agent: no restore tool exists to approve, no instruction to talk
   it out of, no confused-deputy path to it. Its Entra identity only ever held `EnergyHub.Read`.

*The most secure capability is the one the composition never granted.* This is not a limitation
being excused — it is the recorded design ("a hosted agent that carried the presenter-driven
control surfaces would be a worse example of hosting"), promoted to the stage's closing line.

## Slide 43/44 corrections

The code block is **correct** — verified by compiling it verbatim, 0 errors and 0 warnings, on the
pinned `1.19.0-preview` family. `AgentHost.CreateBuilder` and `RegisterProtocol` come from
`Azure.AI.AgentServer.Core`; `AddFoundryResponses` and `MapFoundryResponses` from
`Microsoft.Agents.AI.Foundry.Hosting`. The slide does not say they are two packages; a copy-paster
will need to know.

**The Bicep is stale and must change.** `minReplicas` / `maxReplicas` still exist in the ARM schema
at `2026-05-15-preview`, but the runtime does not work that way: hosted agents scale per session in
VM-isolated sandboxes, with no replica count and no warm pool. The two docs contradict each other,
which is itself worth one sentence on stage. Replace the excerpt with the shape that describes
reality:

```bicep
// The deployment ROUTES to agent versions. Note what it does not have: no image,
// no CPU, no memory, no environment. Those belong to a version, created through
// the data plane - which is why the pipeline has two halves.
resource deployment 'Microsoft.CognitiveServices/accounts/projects/applications/agentDeployments@2026-05-15-preview' = {
  parent: application
  name: 'caesarea-operations'
  properties: {
    deploymentType: 'Hosted'
    protocols: [ { protocol: 'Responses', version: '1.0.0' } ]
  }
}
```

**The `protocols` list is right in spirit and wrong in two details**, both verified against the
deployed agent.

*The version number is stale.* A container built on `Microsoft.Agents.AI.Foundry.Hosting` 1.20 serves
Responses **2.0.0**, not 1.0.0. Declaring 1.0.0 produces an agent that goes `active` and then answers
every call with `unsupported_container_protocol_version` - a failure that looks like a deployment
problem and is a version-negotiation one.

*Protocols alone do not enable A2A.* If the slide is used to answer "what do I set to support any
kind of communication, including A2A", it is incomplete: setting `protocols` and nothing else gets
you `"An agent card is required for A2A protocol support."` Three things are needed, and only the
first resembles what is on the slide:

| # | What | Where |
| --- | --- | --- |
| 1 | `{protocol: "a2a", version: "1.0.0"}` | the agent **version**'s `protocol_versions` |
| 2 | `a2a` **and** `responses` together | the agent **endpoint**'s `protocols` |
| 3 | An agent card | `agent_endpoint.protocol_configuration.a2a.agent_card` |

Step 2 refuses A2A on its own - *"Both 'a2a' and 'responses' protocols must be enabled on the
endpoint"* - because the platform bridges A2A onto the Responses agent rather than offering it as an
alternative. Step 3 is the one nothing hints at.

The payoff is worth a sentence on stage: **the container never serves A2A.** The platform publishes
the card, fills its `supportedInterfaces` with absolute URLs to its own endpoint, and translates
inbound A2A into a Responses invocation. `MapA2AHttpJson` - which the Aspire-hosted Workforce agent
does use - has no place in a hosted agent.

**A caveat on the excerpt itself, now partly resolved.** The Bicep above is the ARM control-plane
path, `applications/agentDeployments`, while this repo's pipeline provisions through the **data
plane** (`POST /agents/{name}/versions`, `PATCH /agents/{name}`).

Probing ARM directly settles two things and leaves one open:

- **The ARM path is real.** `PUT .../projects/{project}/applications/{name}` is accepted at
  `2026-05-15-preview` and `2025-10-01-preview` - it fails on *schema* ("Agents cannot be null or
  empty"), not on method, so the resource type exists and is creatable.
- **The slide's API version is wrong.** `2026-03-01` returns `NoRegisteredProviderFound` for this type
  in `westus3`.
- **The exact body is undetermined.** `properties.agents` must be non-empty; a bare string is
  rejected as malformed and an object is answered with an unhelpful `SystemError`. Working it out was
  stopped as schema archaeology on a path this repo does not use.

So the accurate line is not "agent deployment cannot be IaC". It is: an ARM path exists, this repo
deploys through the data plane because a version carries an image digest that changes every release,
and the Bicep on the slide has never been run here. Present it as an alternative, with a corrected
API version, or replace it with the data-plane call that was actually used.

Add to the bullets, because it is the fact that changes how people size things:

> **Scale-to-zero is per session, not per replica.** A sandbox per session, idle timeout 5–60 minutes
> (default 15), `$HOME` persisted and restored on resume. `cpu` and `memory` describe *one session* —
> oversizing multiplies cost by concurrency.

## One forward reference worth adding

The deck introduces **declarative agents** on slide 53 ("There Is More"), ten slides *after* this
one. So at the moment the audience first meets the word "hosted", the contrast is not available to
them yet, and "hosted agent" sounds like it should cover both.

A parenthetical on 43 fixes it without stealing slide 53's content:

> *"Foundry runs two kinds. The declarative one — Foundry calls it a **prompt agent** — is
> configuration: instructions, model, tools, no code. We come back to those at the end. This slide
> is the other kind: my container, my framework, my code, and Microsoft runs it."*

Give the audience the noun **prompt agent** once, out loud, even though the deck's word is
*declarative*. Both are correct — Microsoft's own docs describe a prompt agent as "a declaratively
defined agent" — but *declarative agent* is also an Agent Framework concept and a Microsoft 365
Copilot product, so it is the term that returns the wrong search results. *Prompt agent* is the
Foundry noun that finds the right page.

## Related work, and where it stops

Microsoft's [AI Agents for IT/Ops workshop](https://github.com/microsoft/AIAgentsforITOps) is the
mirror image of this material: its stated scope is *"infrastructure management, not agent
development"*. It is worth naming on stage, because it is where to send the deep networking,
secrets and cost questions.

Its Path 2 is complete and actively maintained — but it is a **prompt agent**, not a hosted one. The
web app's project file references `Azure.Identity` and nothing else; the container it deploys to AKS
is a chat UI, and the agent is declarative configuration in Foundry. Its own source says so.

That matters for one reason. Its identity lab enumerates the AKS control-plane identity, the kubelet
identity, the Search identity, the Foundry account and project identities, and the signed-in user —
**every one a service-to-service identity.** There is no agent principal, because prompt agents do
not get one. The per-agent Entra identity is a hosted-agent feature, and it is the thing Agent 365
governs. That gap is this stage's differentiator and the W20 segment's foundation.

Two details from their working code cross-check what this repo's pipeline already does: the endpoint
shape `POST {projectEndpoint}/agents/{name}/endpoint/protocols/openai/responses`, and the token
scope `https://ai.azure.com/.default`. One detail differs and is worth confirming against a live
call before the lecture: they grant the calling workload `Cognitive Services User` where
[modules/rbac.bicep](../../infra/modules/rbac.bicep) grants the narrower `Foundry User`.

## Speaker notes — slides 43/44

**Open on the claim, not the API.** *"The agent code does not determine where it must run. Same
Operations Agent you have been watching for an hour. I have not changed its instructions, its skills
or its tools. I have changed who operates its runtime."*

**Three habitats, one sentence each.** *"Self-hosted: I own the runtime, and everything that comes
with owning a runtime. Functions with the durable extension: Azure owns the compute, I own the state
model. Foundry hosted agents: Microsoft owns the runtime, the scaling, the identity and the platform
integration — and I still own the code."*

**The code.** *"Four lines. Build a host, register the agent, declare the protocol, run. Two
packages, not one, which the slide does not tell you. And note the readiness endpoint you cannot
see: the protocol library maps it for you, because the platform is going to health-check you."*

**The part that surprised me.** *"This whole protocol runs on a laptop. Port 8088, `/responses`,
buffered or as server-sent events, with no Azure, no credential and no deployment. I found that out
by doing it. So the inner loop for a hosted agent is not 'push and pray' — you can hold the whole
thing locally and only then decide where it lives."*

**Identity — and slow down here, because this is the W20 hinge.** *"When the version is created, the
platform mints a dedicated Microsoft Entra identity for this agent. Not for the pod. Not for the
app. For the agent. It has an object ID, it appears in your directory, it holds role assignments,
and it shows up in your audit log as the thing that called your database.*

*That is new, and it is why I could not put the whole deployment in Bicep. The identity does not
exist until the agent does. So platform RBAC is declarative and the agent's own grants happen after
the version goes active. Nothing about that ordering is a workaround — it is what it means for an
agent to be a principal."*

**Scaling, corrected.** *"The Bicep on this slide came from documentation that says minReplicas and
maxReplicas. There is no replica model. Sandboxes are per session, they idle out after fifteen
minutes by default, and your CPU and memory numbers describe one session — so oversizing multiplies
by concurrency instead of adding. Which is a better scale-to-zero story than the one I was going to
tell you, and I only know it because I went and checked."*

**Close on ownership, per the deck's own note.** *"Same conceptual agent. Different operational
responsibility. Choose on control, durability, scale, integration and who carries the pager — not on
which one is newest."*
