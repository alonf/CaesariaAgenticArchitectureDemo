# Stage 10 — MultiAgent

Deck anchor: `MULTI_AGENT` (MAF Multi-Agent, slides 36–40).

## Goal

Add `DemoStage.MultiAgent`: a second agent introduced for a **real boundary**. Slide 36 is blunt —
*"If you only need another capability, add a tool, not an agent"* — so the demo has to show a
boundary a tool cannot cross. Security is that boundary: it owns records the Operations Agent may
not read, and the hub enforces that rather than assuming it.

## Why a second agent here, and not anywhere else

Of slide 36's five reasons, two carry this stage:

- **Authority and permissions** — the Security Hub is reachable from exactly one service. The hub
  *enforces* this rather than assuming it: every route requires a caller identity, reads admit the
  Security Agent only, and the admin routes admit the scenario service only. An architecture test
  additionally asserts that `OperationsAgent.Api` contains no Security Hub address, no Security
  contract reference, and no `/api/security` path. If it could read the hub, the honest answer
  would be a tool.
- **Context isolation** — restricted operational detail never enters the Operations Agent's
  context window. Only a typed, closed-set verdict crosses.

Expertise and ownership/lifecycle support the case. "Different model" does not apply here, and
claiming it would be dishonest.

**What the second agent actually decides, stated precisely.** Draw the line between the two halves
of its answer and say it out loud:

| Field | Decided by | Why |
| --- | --- | --- |
| `RequiresLighting`, `UntilUtc`, `ReasonCode` | the **records**, deterministically | a model must never be able to switch a city's security lighting off |
| `Recommendation` | the **agent** | advice is judgment, and judgment is what a model is for |

So the model is behaviourally relevant — its recommendation reaches the asking domain and shapes
what the Operations Agent tells the operator — while having no authority over the safety-critical
decision. That split *is* the lesson, and it generalizes: give the model the part where being
wrong costs an awkward sentence, not the part where being wrong costs a dark street.

Its larger contribution is **containment**: it is the thing that can hold the restricted records
at all. A deterministic Security service could compute the same verdict; what it could not do is
let a *reasoning* caller ask open questions of the Security domain without handing that caller the
records. Present this stage as a permission-and-isolation boundary, not as proof that a model was
required to reach the verdict.

The demo-grade caller identity is a header, not an authenticated principal. It is enforced, and it
is honest about what it is: the Governance stage replaces it with real identity.

## Composition: relationship first, boundary second

Slide 39's note is the sharpest line in the deck: *"A handoff does not imply A2A. A remote agent
does not imply group chat. Those are independent axes."* This stage picks one point on each axis:

- **Relationship — Delegate.** The Operations Agent consults and keeps ownership of the answer.
  Handoff would be wrong (responsibility never moves) and group chat would be theatre for a
  one-question consult.
- **Boundary — remote capability over MCP**, slide 40's top-right block: the Security Agent runs
  in its own service and is published as a capability another domain can discover.

## Scope

- **`SecurityHub.Api`** — a deterministic hub owning active operations per area, with the
  restricted fields (classification, authorizing officer, unit call sign, notes) that make the
  boundary real. Synchronized from the same scenario recipe as every other hub, so after a
  successful application the Security and Energy domains agree; the synchronization is sequential
  rather than transactional, so a partial failure can leave them briefly divergent until the
  presenter re-applies.
- **`SecurityAgent.Api`** — the Caesarea Security Operations Agent: its own instructions, its own
  restricted tool, its own model call, and the only Security Hub client in the system. Published
  over MCP as `assess_lighting_requirement`.
- **`MULTI_AGENT`** region — composing the second agent, in the service that owns it.
- **Structural sanitization** — no model-authored text crosses the boundary at all. The agent
  selects a **reason code from a closed set**; the decision and deadline are computed from the
  records, and the public sentence is rendered from a deterministic template. A denylist would not
  have been enough: "Bar-On authorized it" contains no whole restricted value, and a paraphrase or
  an abbreviation contains none either. The agent's snapshot is also the sanitizer's snapshot -
  one read, served to the model by a tool bound to the area under assessment - so nothing can be
  disclosed that was not also examined.
- **The lighting domain learns the requirement, not its origin.** `OperationalContext` carries
  `RequiresLighting` and a non-attributed summary — deliberately no "security operation active"
  flag. Deterministic automation stays safe (the workflow still refuses to "correct" a lamp that
  must stay lit), while *why* it must stay lit is the Security domain's to disclose.
- **Presenter toggle** — Security consult ON / OFF in DemoControl, off by default and reset by a
  stage downgrade.

## Lecture beat

1. Apply **Security Operation** and go to the MultiAgent stage with the consult **off**. Click
   **"Why is L-417 on during daylight? (new session)"** — the stage's own button, so the beat does
   not depend on the model volunteering a consult for a question that never needed one. The agent
   answers correctly but thinly: *"an external
   operational directive requires lighting in this area; the requesting domain is not disclosed."*
   It knows the lamp is intentional and cannot say more. Note what it does **not** do: it does not
   invent a reason, and it does not recommend restoring.
2. Turn the consult **on** and ask again. The capability trace now shows `assess_lighting_requirement`
   marked **ran**, and a **Consulted specialist** block appears carrying the other agent's own typed
   answer: who assessed it, the verdict, its classification, its recommendation, the deadline, and
   the line the audience should notice — *"Operational details are withheld."* The Operations
   Agent's answer then repeats that recommendation rather than reaching one of its own.
3. That contrast is the whole argument: the second agent did not add a capability, it added
   **authority to know something**. Point at the architecture test — the Operations Agent has no
   route to the Security Hub at all.
4. Close on slide 39's independent axes: this is *delegate over a remote boundary*. Handoff and
   group chat are different relationships, and A2A is a different boundary for the same
   relationship.

## Verification

- Deterministic tests pin the sanitizer against a model that leaks whole restricted values,
  fragments, paraphrases and abbreviations; that the verdict and deadline come from the records
  rather than from the model; that the restricted tool refuses an out-of-scope area and serves the
  same snapshot the result is computed from; that the hub rejects the wrong caller on each route
  over real HTTP; and that the Operations Agent has no path to the Security Hub.
- The live walk applies the scenario, checks that the Security Hub agrees with it, asks with the
  consult off and on, asserts the consult only happens when enabled, and scans the answer for
  every restricted token.

## Deck note

**Slides 41–42 (A2A) are demoable and simply not implemented yet.** `Microsoft.Agents.AI.A2A` and
`Microsoft.Agents.AI.Hosting.A2A.AspNetCore` are published on nuget.org in the same preview family
this solution already uses (`1.20.0-preview.*`). An earlier version of this note claimed they were
unavailable; that was wrong — the package search behind it omitted `--prerelease`. Adding A2A as a
*second boundary for the same delegation* — flip the Security consult between MCP and A2A while
the relationship stays "delegate" — is the natural next increment, and is the sharpest possible
demonstration of slide 39's independent axes.
