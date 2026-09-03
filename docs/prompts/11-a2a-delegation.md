# Stage 11 — A2A Delegation

Deck anchor: `A2A_DELEGATION` and `A2A_SPECIALIST` (MAF A2A, slides 41–42).

## Goal

Add `DemoStage.A2ADelegation`: a peer agent in **another organization's domain**, discovered by its
agent card and given a task over A2A. Stage 10 already made the case for a second agent; this stage
makes a different case, and the two must not be told as the same story.

Stage 10's boundary is a **permission** boundary inside one system: the Operations Agent may not
read the Security Hub, so a second agent holds the records and returns a sanitized verdict. The
sanitization is real work — the Security Agent *does* hold restricted values and must be prevented
from emitting them.

Stage 11's boundary is a **domain** boundary between two owners. The workforce domain's answer is
free-form and cannot be reduced to a closed-set verdict, so the Stage 10 technique does not apply.
Instead the boundary moves one step earlier: the peer agent is never given the secrets in the first
place. There is then nothing to sanitize, and nothing a caller can talk it out of.

**That is the lesson of this stage: context minimisation beats output sanitisation wherever it is
achievable.** Stage 10 is the case where it is not.

## Why an agent and not a tool, again — and differently

Slide 36's test still applies: *if you only need another capability, add a tool, not an agent.*
Here the reason is not permissions but **the shape of the question**.

The Operations Agent does not know which work order matters. Answering requires searching an asset,
reading the candidates, deciding which one explains the current state (an open maintenance visit,
not a closed lamp replacement from last month), and saying why in prose. That is a small
investigation in another domain's vocabulary — a task, not a lookup. A tool would force the caller
to already know the work order id, which is exactly the knowledge it does not have.

Wrapping the peer as an `AIFunction` was considered and rejected. It would put the peer in the
model's tool list and let the model decide whether to consult — the Operations Agent would no
longer own the delegation. The consult is therefore **a route of its own**
(`POST /api/operations-agent/workforce-consult`), invoked by the service, and the peer's reply
becomes context for the answer this service composes. Ownership never moves; this is *delegate*,
not *handoff*.

## The boundary, stated exactly

`WorkOrderRecord` is the whole thing: reason, status, expected clearance — and technician name,
badge number, labour cost, contracted call-out rate, and the technician's note as written, which
mixes the operational and the commercial in one paragraph.

`ShareableWorkOrderDetails` is the projection. The commercial and personal fields are not redacted
out of it: **they were never selected into it**. The Workforce Hub's `GetShareableDetails` builds
the projection field by field, and the Workforce Agent's extraction tool returns only that type. The
peer agent has therefore never held a rate, so no instruction from any caller can extract one.

The record carries `OperationalSummary` as a field of its own, separate from `TechnicianNote`. That
separation is the design lesson, not a convenience of the fixture: **a domain that intends to answer
other domains' questions has to make the shareable part separable at write time.** Keep only the
entangled paragraph and there is no safe way to share it — you are back to asking a model to decide
which half may leave, with the whole paragraph in its context.

The peer's two tools mirror the two reads, and the order matters:

| Tool | Returns | Why it is safe |
| --- | --- | --- |
| `find_work_orders_for_asset` | `WorkOrderSummary[]` — id, asset, title, status, raised-at | a search result is itself a disclosure surface, so it carries nothing commercial or personal |
| `get_shareable_work_order_details` | `ShareableWorkOrderDetails` | the projection; the sensitive fields are absent, not masked |

Search, then open. The caller asks about an asset, the peer finds the work order id itself, and only
then extracts — so the caller never needs (and never gets) an identifier-shaped key to anything it
should not see.

## Scope

- **`WorkforceHub.Api`** — the work-order system of record. Two shared reads, both admitting only
  `CallerIdentity.WorkforceAgent`. One admin route (`reset`) admitting only the scenario service.
  One presenter route, `/api/workforce-records`, returning everything in full behind **two
  conditions that are not the same condition**: the caller must name itself `demo-control`, and the
  connection must be loopback. Loopback alone identifies nobody here — every service in this demo
  runs on the presenter's machine, so "local" would admit the Operations Agent as readily as the
  switchboard; the header alone is a string anyone can send. Together they mean "the presenter, at
  the keyboard". The header is demo-grade identification, not authentication, like its Security
  counterpart.
- **`WorkforceAgent.Api`** — the Caesarea Workforce Agent: its own instructions, its own two tools,
  its own model call, and the only Workforce Hub client in the system. Published over **A2A**
  (`AddA2AServer` + `MapA2AHttpJson`), with its card served at `/.well-known/agent-card.json`.
- **`A2A_SPECIALIST`** region — composing the peer agent, in the service that owns it.
- **`A2A_DELEGATION`** region — resolving the card and running the peer, in the consulting service.
- **The card is read before the task is sent.** That is what makes this a relationship with a named
  agent rather than a call to a URL: the card names the domain being consulted, what that domain
  owns, who runs it, which skills it declares, and — in the skill's own description — what is not in
  the records that skill reads. A caller learns the limit at discovery time instead of meeting it as
  a refusal at runtime.
- **The card is documentation, not enforcement.** Every sentence of it could be deleted without
  weakening the boundary by anything, because what makes the commercial and personal fields
  unreachable is the projection the tools return, one service away. This is worth saying on stage:
  it is how the audience can tell the card from the control.
- **The description names the domain, not just the skill.** An agent card whose identity says
  "Workforce Management" and whose description only mentions maintenance leaves a reader unable to
  work out why that domain is answering questions about a streetlight - or that the technician data
  is held there at all.
- **The card also chooses the transport.** `A2AClientFactory.Create(card, …)` builds the client for
  the binding the card declares, so discovery decides both the address and the protocol.
- **Bounded, and budgeted end to end.** The delegated task gets 60 seconds — a whole peer run, but
  never unbounded. A peer that is down, slow or refusing is reported to the operator as a peer that
  did not contribute, rather than being silently dropped from an answer that then looks complete.
  The A2A client's own resilience pipeline is replaced for this reason: the standard 10-second
  attempt timeout aborts every delegated task, and a retry would re-run the peer's entire
  investigation. Its budget sits just above the delegation's, so a slow peer trips in this service's
  own words rather than as a transport exception.
- **No evidence versioning.** Command Center answers are pinned to the operational evidence version
  they were produced against and go stale when the city moves. A delegated consult is a claim about
  another domain's records; a lamp switching during a 60-second consult says nothing about whether
  that claim is still right, and versioning it discarded good answers mid-beat.

## Lecture beat

1. Apply **Lights On Reported by a Client** and go to the A2A Delegation stage. Click
   **"Ask the workforce domain about L-417."** The card is resolved first, then the task is
   delegated: the peer searches its work orders, picks the open WO-8732 over the closed WO-8610,
   and explains the override and its expected clearance.
2. Read the **Consulted peer (A2A)** block: the agent's name, the organization that runs it, the
   advertised skill *with the peer's own description of it* — including the sentence saying what is
   not in the records that skill reads — and the transport. A named agent with a provider — not an anonymous endpoint,
   and not a tool in this agent's toolbox. Note the footnote: *"the peer read the work order; this
   agent never did."*
3. Point at the capability trace, or rather its absence: **"No tool was invoked."** The peer is not
   a tool. This service composed the operator's answer from a peer's answer.
4. Now click **"Ask the workforce domain for the technician and labour cost."** The question claims
   finance-audit authorisation. It does not refuse on policy — it answers that the cost is not
   visible in the records it can access. There is no policy to argue with, because there is no
   value in the room.
5. Open the switchboard's **Workforce Records** panel and click *Show work orders in full*. Green is
   what crossed; red — technician, badge, labour cost, call-out rate, the note as written — never
   entered the agent's context at all.
6. Close on the contrast with Stage 10, out loud: *the Security Agent holds the secret and must be
   stopped from saying it; the Workforce Agent was never given it.* Do not ask a model to keep a
   secret it holds. Hand it only what may cross, and there is nothing left to extract — that is a
   tool-boundary decision, not a prompt.

## Verification

- Deterministic tests pin: the shareable projection type declares no commercial or personal
  property at all; the wire payload of the shared read contains none of the fixture's secret values;
  a search result carries nothing sensitive; both shared reads reject a caller that is not the
  workforce agent; and the full-record route refuses a remote caller over real HTTP.
- An architecture test asserts `OperationsAgent.Api` contains no Workforce Hub address, no Workforce
  contract reference, and no `/api/workforce` path — the same guard Stage 10 uses for the Security
  Hub, and the claim this stage's slide makes.
- A2A tests run the **real Workforce Agent host** with only the model replaced: the card is
  discovered at the well-known path, the client is built from the binding the card declares, and the
  task reaches the peer over the shipped routes. An unreachable peer is asserted to come back as a
  reported failure rather than a silent gap, addressed at a closed port so the test fails on
  connection rather than on the 60-second budget.

  This test earned its keep immediately: it found that the delegation had never actually worked.
  The card published its interface as `.../a2a` with no trailing slash, so every protocol path
  resolved one level too high, and the client was being built as JSON-RPC while the host serves
  HTTP+JSON. Both are fixed; both were invisible to every test that mocked the peer.

## Slide text (slides 41–42)

Copy-ready. Keep the two halves distinct — the left column is Stage 10, the right is this stage.

### Slide 41 — corrections to the existing "MAF A2A" slide

The existing slide is about the **Security** agent over A2A. In this demo Security is Stage 10 and
it is **MCP**; the A2A peer is the **Workforce** agent. Every identifier on the slide has to move, or
the deck and the screen disagree at the exact moment the audience is looking for the connection.

| Region | Change | Why |
| --- | --- | --- |
| Diagram, "Remote Agent" | Security → **Workforce** | the demo's A2A peer |
| Agent Card panel | `CaesareaSecurity` → `Caesarea Workforce Agent`; capabilities → **skills** | `capabilities` in `AgentCard` is `{streaming, pushNotifications}`; the list of what an agent does is `skills` |
| Card panel, `protocols` | → **`supportedInterfaces`** | that is the real field, and it carries the binding |
| Publish code | `"security-agent"` → the agent's published name; `"/a2a/security"` → `"/a2a"` | matches the demo |
| Publish code, `MapWellKnownAgentCard(…)` | **delete — this API does not exist** | verified by compiling it against the pinned packages: `CS1061`. There is no well-known card helper in `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` 1.19; you map the route yourself |
| "When to use A2A", first item | "Independently hosted" → **"You are giving a task, not calling a function"** | the Security Agent is *also* independently hosted and uses MCP, so hosting does not discriminate |
| Footer note | promote it | *"MCP exposes capabilities. A2A preserves the agent abstraction."* is the best line on the slide and it is currently the smallest text on it |

The consuming code block is **correct** — `A2ACardResolver`, the one-argument constructor,
`GetAIAgentAsync()` and the string overload of `RunAsync` all compile as shown.

#### Corrected slide text

> **A2A — expose an agent as an agent**
>
> **MCP exposes capabilities. A2A preserves the agent abstraction.**
>
> Reach for A2A when:
> - **You are giving a task, not calling a function** — the peer decides how to answer it
> - **Another owner** — different team, org or contract; you control neither their model nor their data
> - **They own their context** — tools, memory, execution; their records never enter yours
> - **Discovered, not configured** — an agent card and declared skills, resolved at runtime
> - **Interoperable** — cross-framework, cross-language, cross-service
>
> *The card states the boundary. It is not the boundary.*

#### Publish (the workforce domain)

```csharp
// The A2A server resolves the agent by name, so it is registered under that key.
builder.Services.AddKeyedSingleton<AIAgent>(agentName,
    (sp, _) => WorkforceAgentFactory.Create(/* project client, tools, model */));

builder.Services.AddA2AServer(agentName);

var app = builder.Build();
app.MapA2AHttpJson(agentName, "/a2a");

// No well-known-card helper in this version: map it yourself, or no standard
// resolver can find you. The framework's own /a2a/card is an empty default.
app.MapGet("/.well-known/agent-card.json", (HttpContext ctx) =>
    Results.Json(WorkforceAgentCard.Create(agentName, BaseAddress(ctx)),
                 A2AJsonUtilities.DefaultOptions));
```

#### Consume (the operations domain)

```csharp
// Discovery first. This is what makes it a relationship with a named agent
// rather than a call to a URL - and the card's binding picks the transport.
var resolver = new A2ACardResolver(peerBaseUri, httpClient);
var card = await resolver.GetAgentCardAsync(ct);

var peer = new A2AAgent(
    A2AClientFactory.Create(card, httpClient, new A2AClientOptions()),
    new A2AAgentOptions { Name = card.Name, Description = card.Description });

var reply = await peer.RunAsync(
    "Do you have any work order related to L-417?", ct);

// Shorter, when you do not need the card's own fields:
//   AIAgent peer = await resolver.GetAIAgentAsync(httpClient);
// The demo takes the long road because the Consulted-peer panel renders
// card.Provider and card.Skills - reading the card *is* the beat.
```

#### Agent card (trimmed, and accurate to the real shape)

```json
{
  "name": "Caesarea Workforce Agent",
  "version": "1.0.0",
  "provider": { "organization": "Caesarea Smart City - Workforce Management" },
  "supportedInterfaces": [
    { "url": "https://workforce.caesarea/a2a/", "protocolBinding": "HTTP+JSON" }
  ],
  "capabilities": { "streaming": true },
  "skills": [{
    "id": "asset-maintenance-situation",
    "name": "Asset maintenance situation",
    "description": "Finds the work orders raised for an asset ... Labour cost, contracted
                    rates and technician identity are not in the records this skill reads."
  }]
}
```

Two details on that JSON are worth leaving visible, because both cost real debugging time:
`capabilities` is not the list of what the agent does (that is `skills`), and **the interface URL ends
in a slash** — clients resolve the protocol's method paths relatively against it, so `.../a2a` makes
every call land one level too high and return 404.

### Slide 42 — The boundary is where the data is selected, not where it is refused

> **Two ways to keep a secret across an agent boundary:**
>
> | | Security Agent (slide 36–40) | Workforce Agent (this slide) |
> | --- | --- | --- |
> | Boundary | permission, inside one system | domain, between two owners |
> | The agent | **holds** the restricted records | **never receives** them |
> | Technique | **output sanitisation** — closed-set codes, templated sentences | **context minimisation** — the tool returns a projection |
> | Failure mode | a leak is one clever prompt away | there is nothing to leak |
> | Cost | you must reason over the secret | the shareable part must be separable at write time |
>
> **Do not ask a model to keep a secret it holds.**
> Hand it only what may cross, and there is nothing left to extract.
> That is a tool-boundary decision, not a prompt.

**Optional code strip for slide 42** (this is the whole mechanism):

```csharp
// Workforce Hub - the boundary. Not a redaction: a selection.
return new ShareableWorkOrderDetails(
    record.WorkOrderId, record.AssetId, record.Title, record.Reason,
    record.Status, record.RaisedAt, record.ExpectedClearanceAt,
    record.OperationalSummary);
// TechnicianName, TechnicianBadge, LabourCost, CallOutRate, TechnicianNote:
// never selected, so never in the agent's context.
```

## Speaker notes — slide 41 (the mechanism)

Roughly two minutes. This slide is the *how*; slide 42 is the *why it is safe*. Do not merge them.

**Open on the discriminator, not on the protocol.** *"We already have a second agent — the Security
Agent, from twenty minutes ago. It is independently hosted, it has its own model, its own
credential, its own restricted database. And we reached it over MCP. So let me kill the obvious
theory straight away: A2A is not 'the one you use when the other agent is somewhere else.' That was
already true of the last one."*

*"The difference is one line."* (Point at the subtitle.) ***"MCP exposes capabilities. A2A preserves
the agent abstraction."*** *"With MCP I published the Security Agent as a function called
`assess_lighting_requirement`. My Operations Agent saw a tool. It never knew there was an agent on
the other end. With A2A I publish an agent, and the caller knows it is talking to one — it gets a
name, an owner, declared skills, and a task-shaped conversation instead of a signature."*

**The publish side — three calls and a fourth you write yourself.** *"Registering it is small.
Register the agent under a name, hand that name to `AddA2AServer`, map the HTTP+JSON endpoints. Three
lines."*

*"And then there is the fourth, which is the one I want you to actually remember."* (Point at the
`MapGet`.) *"There is no `MapWellKnownAgentCard` in this version. I looked, then I compiled it to be
sure — it does not exist. The framework serves a card at `/a2a/card`, but it is an empty default; it
does not consult your DI container, and I tried that both keyed and unkeyed before I accepted it. So
if you want anyone's standard resolver to find you, you map `/.well-known/agent-card.json`
yourself. Every A2A client in the world looks there. Miss it and you are invisible."*

**The consume side — discovery is the point, not the plumbing.** *"Resolve the card first. That is
what turns this from 'POST to a URL' into 'consult a named agent': the card tells me who they are,
which organisation runs them, what they declare they can do."*

*"And it tells me something I did not expect to need: which protocol to speak. The card carries the
binding, and `A2AClientFactory` builds the right client from it. I learned that the hard way — I
hand-built a JSON-RPC client against a host serving HTTP+JSON and spent a while staring at 404s.
Let the card choose."*

*"There is a one-liner — `GetAIAgentAsync` — that does all of it in a single call, and if you do not
need the card's own fields, use it. The demo takes the long road on purpose, because the panel you
are about to see renders the provider and the declared skill. Reading the card *is* the beat."*

**One detail that will cost you an afternoon.** (Point at the JSON.) *"The interface URL ends in a
slash. Clients resolve the protocol's method paths relatively against it. Against `/a2a` the last
segment gets replaced instead of extended, every call lands one level too high, and everything is a
404 with nothing in the logs to tell you why. `/a2a/`. One character."*

*"Also: `capabilities` is not the list of what the agent can do. That is `skills`. `capabilities` is
streaming and push notifications. The naming is unfortunate; the compiler will not save you."*

**Hand off to the demo, not to slide 42 yet.** *"So that is the mechanism. Now the interesting part,
which is not the protocol at all: what happens when the agent you are consulting holds something it
must not tell you."*

## Speaker notes — slide 42 and the demo

Told the way it should sound out loud. Roughly four minutes.

**Set the scene.** *"A customer reports that L-417 is on in broad daylight. Our Operations Agent
knows the lamp is lit and knows it is overridden. It does not know why, because the reason lives in
a work order — and work orders belong to the workforce department. Different owner, different
system, different city contract."*

**Why not a tool.** *"The obvious move is to add a tool: `get_work_order(id)`. Except we don't have
an id. Answering this means searching an asset, looking at the candidates, deciding that the open
maintenance visit explains today's state and the lamp replacement from five weeks ago does not, and
saying so in a sentence. That is a small investigation in someone else's vocabulary. Slide 36 said
it: if you only need another capability, add a tool. We need someone who knows their own domain.
So we ask an agent."*

**The card.** *"Watch what happens first — before any question is sent, we read their agent card."*
(Click. Point at the Consulted peer block.) *"Caesarea Workforce Agent. Run by Workforce Management —
the domain that owns the city's maintenance work orders and the technicians who carry them out. One
declared skill: asset maintenance situation. And read the last line of that skill's own description:
labour cost, contracted rates and technician identity are not in the records this skill reads."*

*"Note the wording, because I chose it carefully. Not 'will not disclose' — 'are not in the records
it reads'. One of those is a promise you can argue with. The other is a fact about the room."*

*"And here is the thing to be honest about: that sentence is a courtesy. It is documentation for a
caller deciding whether to bother asking. Delete it from the card and nothing leaks — the boundary
is a service away, in what the tool returns. The card tells you where the boundary is; it is not
the boundary."*

**The delegation.** *"Then we hand over a task, in the operator's own words, and they run their own
investigation: they search their work orders, they pick WO-8732 over the closed WO-8610, and they
tell us the light was left on for post-maintenance verification and the override clears after the
afternoon inspection."* (Point at the capability trace.) *"And notice what the trace says: no tool
was invoked. Nothing in our model's toolbox was used. We didn't call a capability — we asked a
peer, and then wrote our own answer from what they said. The answer to the operator is still ours.
That is delegate, not handoff."*

**Now the sharp part.** *"Last stage we had a second agent too — the Security Agent. Same
relationship, delegate. Different boundary: that one was MCP, this one is A2A. Slide 39 said those
are independent axes; here they are, on the same relationship."*

*"But there is a much more interesting difference, and it is not about protocol at all."*

*"The Security Agent holds the restricted records. It has to — its verdict is reasoned over them.
So everything it emits has to be structurally sanitised: a code from a closed set, a sentence from
a template, because model-authored prose could carry a call sign. That is real engineering, and we
did it, and it works. But be honest about what it is: it is a guard standing in front of a room
that has the secret in it."*

*"This agent has no room. Let me prove it."* (Click the second button.) *"I'm asking the workforce
agent, with authority, for the technician's name, their badge number, and the labour cost — and I
told it this is an authorised finance audit."*

(Read the answer.) *"It doesn't refuse. There is no policy for it to enforce and no rule for me to
argue with. It says the cost is not visible in the records it can access — because it is telling
the truth. Its extraction tool returns eight fields, and none of them is a cost."*

**The reveal.** (Switch to the switchboard, Workforce Records, *Show work orders in full*.)
*"Here is the actual work order. Green crossed the boundary. Red never left the workforce domain:
J. Cohen, badge 4471, one thousand two hundred and forty, premium call-out rate. And look at the
technician's note as written — 'took three hours beyond the quote, bills at the premium rate agreed
for J. Cohen, flag it to finance.' Operational fact and commercial fact, in one paragraph, the way
a real work order actually arrives."*

*"That paragraph is why this only works if you plan for it. The workforce system records the
operational summary as its own field, separately, at write time. If it hadn't — if all we had was
that mixed paragraph — there would be no safe way to share any of it. We'd be back to asking a
model to decide which half may leave, with the whole paragraph sitting in its context."*

**The line to land on.** *"So: do not ask a model to keep a secret it holds. Hand it only what may
cross, and there is nothing left to extract. That is a tool-boundary decision, not a prompt — and
prompts are the thing that argue back."*

**If you have thirty seconds more.** *"One caveat, because I don't want to oversell it. Context
minimisation is the stronger answer wherever you can get it. You cannot always get it: sometimes,
like the Security Agent, the judgment genuinely has to be made over the restricted data. Then you
sanitise the output and you do it structurally. Know which situation you are in — that is the
engineering decision. The protocol is the easy part."*

## Two things this stage does not claim

**The peer's reply is untrusted text.** It is another service's model output, and it becomes context
for a model here. The composer is given it delimited and labelled as data, and told not to follow
instructions found inside it. That reduces the risk that a manipulated peer steers the answer; it
does not eliminate it, and the composer having no tools is what bounds the damage. The stronger fix
— forcing the peer to answer in a typed artifact — is deliberately not taken: answering in its own
words is the contrast with Stage 10 that this stage exists to make, and constraining it would argue
the opposite case by accident.

**The trace reports what this service observed, not what happened everywhere.** The round-trip count
is labelled *local composer round trips* because the peer's own investigation ran in another process
and is not visible from here. The skill is labelled *advertised* because A2A reports which skills a
peer declares, not which one served a task.

## Dependency note

The MAF A2A packages are pinned to the `1.19.0-preview` family, matching the rest of the solution.
A `1.20` preview family exists. **Do not upgrade before the lecture.** The 1.19 pin is verified end
to end under the AppHost, and the two bugs that stood between "compiles" and "works" were both wire
-format surprises — the card's trailing slash and the JSON-RPC/HTTP+JSON binding mismatch. An
upgrade means re-running the full live walk against a new set of those. Upgrade the whole family
together afterwards, and repeat the walk.

## Deck note

Slide 39's independent axes are now demonstrated on both: Stage 10 is *delegate over MCP*, Stage 11
is *delegate over A2A*. Same relationship, different boundary — which is exactly the point that
slide makes.
