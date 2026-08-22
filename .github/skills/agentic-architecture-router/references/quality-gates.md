# Architecture Quality Gates

## 1 — No technology-first architecture

Bad: `Use Foundry Agent Service + MCP + Work IQ + Workflow`

Good: `Open-ended evidence investigation requires a single agent; organizational knowledge is an evidence source; consequential execution remains deterministic. Product mapping follows.`

## 2 — No invented scenario facts

Examples/hypotheses must be labeled. If a deterministic fact already explains the situation, question whether an agent is needed.

## 3 — Preserve deterministic responsibilities

Do not reimplement known deterministic scenarios as agentic logic.

## 4 — Agent threshold satisfied

An agent needs a reason to own a control decision: source selection, adaptive evidence gathering, dynamic sequence, iterative investigation, or open-ended planning.

`Uses an LLM` is not sufficient.

## 5 — Multi-agent threshold satisfied

Require a concrete boundary. Different business domains alone are not enough.

## 6 — Workflow semantics correct

Workflow owns explicit process topology. It may run agent or deterministic activities. It does not orchestrate the agent's internal tools, skills, knowledge, or memory.

## 7 — Authority explicit

For each consequential operation identify recommender, requester, authorizer, executor, and auditor.

## 8 — Product mapping second

Only after generic design is stable should product/platform skills map mechanisms to services.

## 9 — Open questions remain open

Do not hide uncertainty behind a polished diagram.

## 10 — Stop before code

Return architecture and request review.
