# Stage 10 — MultiAgent

Deck anchor: `MULTI_AGENT` (MAF Multi-Agent, slides 36–40).

## Goal

Add `DemoStage.MultiAgent`: a **second agent that earns its cost**. Slide 36 is blunt — *"If you
only need another capability, add a tool, not an agent"* — so the demo has to show a boundary a
tool cannot cross. Security is that boundary: it owns records the Operations Agent may not read.

## Why a second agent here, and not anywhere else

Of slide 36's five reasons, two carry this stage:

- **Authority and permissions** — the Security Hub is reachable from exactly one service. An
  architecture test asserts that `OperationsAgent.Api` contains no Security Hub address, no
  Security contract reference, and no `/api/security` path. If it could read the hub, the honest
  answer would be a tool.
- **Context isolation** — restricted operational detail never enters the Operations Agent's
  context window. Only a typed, sanitized verdict crosses.

Expertise and ownership/lifecycle support the case. "Different model" does not apply here, and
claiming it would be dishonest.

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
  boundary real. Synchronized from the same scenario recipe as every other hub, so the Security
  and Energy domains can never contradict each other on stage.
- **`SecurityAgent.Api`** — the Caesarea Security Operations Agent: its own instructions, its own
  restricted tool, its own model call, and the only Security Hub client in the system. Published
  over MCP as `assess_lighting_requirement`.
- **`MULTI_AGENT`** region — composing the second agent, in the service that owns it.
- **Structural sanitization** — the agent is *instructed* to withhold restricted detail, and
  `SecurityAssessmentSanitizer` *guarantees* it: the decision and deadline are computed from the
  records rather than taken from the model, and any reason containing a restricted value is
  replaced wholesale. Instructions are a request; a boundary should be a guarantee.
- **The lighting domain learns the requirement, not its origin.** `OperationalContext` carries
  `RequiresLighting` and a non-attributed summary — deliberately no "security operation active"
  flag. Deterministic automation stays safe (the workflow still refuses to "correct" a lamp that
  must stay lit), while *why* it must stay lit is the Security domain's to disclose.
- **Presenter toggle** — Security consult ON / OFF in DemoControl, off by default and reset by a
  stage downgrade.

## Lecture beat

1. Apply **Security Operation** and go to the MultiAgent stage with the consult **off**. Ask
   *"Why is L-417 on during daylight?"*. The agent answers correctly but thinly: *"an external
   operational directive requires lighting in this area; the requesting domain is not disclosed."*
   It knows the lamp is intentional and cannot say more. Note what it does **not** do: it does not
   invent a reason, and it does not recommend restoring.
2. Turn the consult **on** and ask again. Now the trace shows `assess_lighting_requirement`, and
   the answer names an **active security operation**, states the time it lapses, and adds a
   recommended action anchored to that deadline — plus one line the audience should notice in the
   evidence list: *"Operational details are withheld."*
3. That contrast is the whole argument: the second agent did not add a capability, it added
   **authority to know something**. Point at the architecture test — the Operations Agent has no
   route to the Security Hub at all.
4. Close on slide 39's independent axes: this is *delegate over a remote boundary*. Handoff and
   group chat are different relationships, and A2A is a different boundary for the same
   relationship.

## Verification

- Deterministic tests pin the sanitizer against a model that leaks the call sign, the officer, the
  classification and the notes; that the verdict and deadline come from the records rather than
  from the model; and that the Operations Agent has no path to the Security Hub.
- The live walk applies the scenario, checks that the Security Hub agrees with it, asks with the
  consult off and on, asserts the consult only happens when enabled, and scans the answer for
  every restricted token.

## Deck note

**Slides 41–42 (A2A) cannot be demoed from this repository's feeds.** There is no
`Microsoft.Agents.AI.A2A` package on nuget.org, and no `AddA2AServer` / `A2ACardResolver` /
`AgentCard` type in any package the solution restores. MCP wrapping covers the same axis and is
slide 40's own top-right block. If the A2A package exists on a private feed, adding it as a second
boundary for the same delegation is a small increment on top of this stage.
