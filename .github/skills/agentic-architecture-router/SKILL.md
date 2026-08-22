---
name: agentic-architecture-router
description: >
  Architecture decision skill for software systems that may combine deterministic code,
  bounded AI capabilities, agents, workflows, tools, MCP, knowledge, memory, and policy controls.
  Use BEFORE implementation when a requirement may benefit from AI or agentic behavior, or when
  deciding whether AI is needed at all. Decomposes responsibilities, chooses the least autonomous
  mechanism that satisfies each responsibility, preserves authoritative state and deterministic
  control, and produces a reviewable architecture decision.
license: MIT
metadata:
  author: A. Fliess
  version: "0.2.0"
  status: experimental
---

# Agentic Architecture Router

## Purpose

Decide **what kind of computation and control each responsibility needs before choosing products or writing code**.

> Use the least autonomy that satisfies the requirement. Add explicit orchestration where control matters.

This is a router, not a framework tutorial. Choose architectural mechanisms first. Product-specific skills may be used **after** those decisions are made.

## Core distinctions

- **Deterministic code** — code owns the next step; rules/algorithms define behavior.
- **Bounded AI capability** — code owns the flow; a model performs one probabilistic task such as classification, extraction, summarization, translation, semantic interpretation, or grounded retrieval.
- **Single agent** — AI owns part of the control decision: what evidence/capability is needed next, in what order, and when enough evidence exists.
- **Multi-agent** — multiple autonomous reasoning contexts introduced for a concrete architectural reason.
- **Workflow** — explicit execution graph owns sequencing, durable process state, checkpoints, retry/recovery, approvals, SLA, escalation, or HIL. Workflow is orthogonal to agenticity.
- **Tool/function** — executable capability selected by an agent or invoked by code/workflow.
- **MCP** — interoperability/protocol boundary for exposing/discovering capabilities or context. MCP is not a reason by itself to use an agent.
- **Knowledge** — evidence used to ground decisions.
- **Memory** — contextual information retained or recalled for an agent; not authoritative operational state.
- **Policy/authorization/HIL** — deterministic controls deciding whether consequential actions are allowed.

Key rule:

> Using an LLM does not make software agentic. It becomes agentic when AI receives authority to decide part of what happens next.

## When to activate

Use this skill when:

- a new requirement mentions AI, agents, copilots, semantic search, MCP, or workflows;
- deciding between deterministic code, bounded AI, an agent, or multiple agents;
- a coding agent is about to introduce an agent into an existing system;
- a requirement crosses existing system boundaries or may require evidence not modeled in authoritative operational systems;
- deterministic and probabilistic components must be combined;
- AI may suggest or initiate a consequential action.

Do not activate merely because the repository contains AI packages.

## Required inputs

Before deciding, inspect available repository and architecture material for:

1. Existing system responsibilities and boundaries.
2. Authoritative systems of record and state owners.
3. Existing deterministic workflows/processes.
4. Existing APIs, tools, events, hubs/services, integration boundaries, and protocols.
5. Security, authorization, audit, SLA, and HIL requirements.
6. The new requirement and success criteria.
7. Which facts are explicitly known versus unknown.

If a required fact is missing, mark it as an **open question**. Do not invent a domain fact to complete the design.

Read:
- `references/decision-framework.md`
- `references/evidence-and-authority.md`
- `references/quality-gates.md`

Use:
- `assets/architecture-analysis-template.md`

## Mandatory workflow

### Step 1 — Establish facts before architecture

Create a compact fact table with:

- **Known from repository / architecture docs**
- **Known from the new requirement**
- **Unknown / must not be assumed**

Never fabricate incidents, telemetry values, work orders, people, faults, firmware versions, policies, or system capabilities to make the scenario coherent.

If an example is useful, label it explicitly as a **hypothesis** or **illustrative example**, never as current system state.

### Step 2 — Decompose the requirement into responsibilities

Do not choose one architecture for the whole feature. Split it into responsibilities such as:

- obtain operational state;
- detect an anomaly;
- interpret natural language;
- retrieve evidence;
- correlate evidence;
- decide what evidence to seek next;
- explain a likely cause;
- propose an action;
- authorize an action;
- execute an action;
- audit/observe the outcome.

For each responsibility, identify inputs, output, authoritative-state owner, whether the next step is known in advance, whether probabilistic judgment is required, and whether side effects occur.

### Step 3 — Route each responsibility to the least autonomous mechanism

Apply this order. Do not skip directly to an agent.

#### 3.1 Deterministic code

Choose deterministic code when required inputs, rules/algorithms, evidence sources, and next steps are known. Prefer existing deterministic services/workflows over adding AI.

#### 3.2 Bounded AI capability

Choose a bounded AI operation when one probabilistic transformation is needed but application code still owns the control flow.

Examples: classify, summarize, extract, translate, interpret, fixed grounded semantic query.

#### 3.3 Single agent

Choose one agent only when delegated control decision is required, such as:

- deciding what evidence to seek next;
- dynamically choosing among information sources or tools;
- adapting the investigation sequence based on intermediate results;
- iterating until enough evidence is available;
- planning an open-ended investigation.

Before choosing an agent, perform the **bounded-AI challenge**:

> Could deterministic code/workflow collect all relevant candidate evidence and then use one bounded AI operation to produce the answer?

If yes, prefer deterministic orchestration + bounded AI. Choose an agent only when intermediate findings materially determine **what evidence/capability to use next, in what order, or whether another step is required**.

Start with **one agent plus capabilities**.

Do not derive agent boundaries from existing service, Hub, bounded-context, team, or domain boundaries. A deterministic domain boundary does not imply a corresponding agent. Create a separate agent only when the **reasoning responsibility itself** needs a distinct security, context, deployment, ownership, model, or specialization boundary.

#### 3.4 Multi-agent

Introduce multiple agents only when a concrete separation benefit is documented, such as:

- different permission/security boundaries;
- independent deployment/service ownership;
- materially different context that should remain isolated;
- specialized models or tool sets;
- context/tool overload that cannot reasonably be managed in one agent;
- organizational ownership requiring an autonomous boundary.

A2A is a protocol option after multi-agent is justified, not a justification by itself.

#### 3.5 Workflow

Evaluate workflow independently of intelligence level.

Add or reuse explicit workflow when the process requires known sequencing, durable process state, checkpoints, retries/recovery, SLA, escalation, approval/HIL, auditable control, or deterministic handoff from probabilistic recommendation to consequential execution.

A workflow may execute deterministic activities and agent activities.

Do not describe workflow as orchestrating an agent's internal tools, skills, knowledge, or memory.

### Step 4 — Classify evidence and context

For every information need, classify the source as:

- authoritative operational state;
- structured application data;
- organizational knowledge;
- documents/files;
- historical evidence;
- agent memory;
- external capability.

Important:

- Agent memory is not a system of record.
- Knowledge is evidence, not authoritative process state.
- Organizational knowledge can matter precisely because it was not modeled into the operational system.

### Step 5 — Classify capability boundaries

For each capability decide among:

- local code/function;
- existing API;
- event;
- MCP boundary;
- workflow request;
- another agent.

Choose MCP only when its protocol properties add concrete value, e.g. interoperability, discovery, independent lifecycle/deployment, or reusable exposure across multiple compatible agent clients.

For **every proposed MCP boundary**, state:

> Why MCP instead of the existing API/service/tool boundary?

If there is no concrete architectural benefit, preserve the existing API/service boundary and expose it as a normal tool/capability if needed.

Do not choose MCP simply because an agent exists.

### Step 6 — Preserve authority boundaries

Explicitly identify:

- Who owns authoritative state?
- Who may read it?
- Who may recommend a change?
- Who may request a change?
- Who authorizes it?
- Who executes it?
- Where is it audited?

For consequential operations require an explicit deterministic control boundary:

`Agent reasoning/recommendation -> authorization/policy/process control -> authoritative service -> execution`

Do **not** assume that human approval is required. HIL is one possible control outcome, not the default. If the architecture/policy does not specify the exact authorization rule, mark it **unknown / governance decision required** rather than inventing an approver or approval policy.

Direct agent execution is acceptable only when the authoritative architecture and policy explicitly permit it.

### Step 7 — Select technology only after mechanism selection

First state the mechanism generically.

Example:

`open-ended evidence investigation -> single agent`

Only then map to available platform technology, using installed platform/product skills where helpful.

Do not let the presence of a platform skill pre-decide the architecture.

### Step 8 — Test rejected alternatives

Explicitly evaluate:

- all deterministic;
- bounded AI only;
- agent everywhere;
- multi-agent;
- workflow-only.

State briefly why each is sufficient or insufficient.

### Step 9 — Stop before implementation

The first response from this skill MUST be an architecture analysis.

Do not scaffold projects, add packages, write production classes, provision cloud resources, create agents, expose MCP servers, or modify infrastructure.

End with:

**Architecture review required before implementation.**

Continue only after explicit approval.

## Output modes

### Full architecture analysis — default

Use `assets/architecture-analysis-template.md`. This is the normal mode for design work.

### Decision Card — concise/demo mode

When the user explicitly asks for **concise**, **demo**, **conference**, or **decision card** output, do the same architecture analysis internally but return only the reviewable conclusions below. Do not omit the quality gates.

Maximum about 12–15 lines:

1. **Keep deterministic:** what existing responsibilities remain deterministic.
2. **Gap:** what the existing system cannot reliably decide/explain.
3. **AI level:** none / bounded AI / single agent / multi-agent, with one-sentence justification.
4. **Evidence:** authoritative state vs supporting organizational/knowledge sources.
5. **Process control:** where explicit workflow/policy/authorization is needed, if any.
6. **MCP:** needed or not, and the concrete reason.
7. **Not needed:** explicitly reject unnecessary agent/multi-agent/workflow/MCP choices.
8. **Open questions:** only material unresolved decisions.
9. **Decision:** one sentence describing the least-autonomous viable architecture.

End with **Architecture review required before implementation.**

## Required output

Use `assets/architecture-analysis-template.md`.

Keep rationale concise and reviewable. Provide decisions, evidence, trade-offs, assumptions, and open questions; do not expose hidden chain-of-thought.

## Success criteria

- Every responsibility has an explicit mechanism/owner.
- Deterministic responsibilities stay deterministic unless there is a concrete reason to change them.
- Agentic behavior appears only where AI needs delegated control decision.
- Authoritative state stays in the proper system of record.
- Consequential execution crosses explicit deterministic control.
- Technology choices follow architectural choices.
- Invented domain facts are prevented.
- An agent is selected only after the deterministic-orchestration + bounded-AI alternative is tested and rejected.
- Agent boundaries follow reasoning responsibilities, not deterministic domain/service boundaries.
- Every MCP boundary states the value MCP adds over the existing integration surface.
- HIL/approval policy is never invented; unknown governance remains explicit.
- Multi-agent and MCP require explicit justification.
- The output is reviewable before code is generated.
