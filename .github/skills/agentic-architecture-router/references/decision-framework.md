# Decision Framework

## Two independent architecture axes

### Axis A — How much decision authority does AI receive?

1. **None** — deterministic code.
2. **Bounded probabilistic operation** — model performs one task; code owns flow.
3. **Agentic control decision** — model decides part of what to do next.
4. **Multiple autonomous reasoning contexts** — multi-agent, only if justified.

### Axis B — How much execution control must remain explicit?

Workflow is a separate concern. It may be required at any point on Axis A.

## Routing table

| Condition | Preferred mechanism | Why |
|---|---|---|
| Known inputs + known rule/algorithm + known next step | Deterministic code | Predictable, testable, governable |
| One semantic/probabilistic operation, fixed continuation | Bounded AI capability | AI adds judgment without owning flow |
| Next information/tool/order depends on intermediate evidence | Single agent | Delegated control decision is useful |
| Autonomous contexts required by security/context/deployment boundaries | Multi-agent | Separation has concrete value |
| Process needs durable state, sequence, checkpoints, SLA, recovery, approval/HIL | Workflow | Execution topology must remain explicit |
| Reusable/discoverable capability across compatible clients/services | Consider MCP | Standardized boundary may add value |
| Action changes authoritative/physical/business state | Deterministic control boundary | Recommendation does not imply authority |

## Diagnostic questions

1. Could a normal function solve this if all inputs were available?
2. Are the required inputs known in advance?
3. Is the sequence of evidence gathering known in advance?
4. Does the model only transform information, or must it decide what to do next?
5. Can relevant evidence exist outside the modeled system?
6. Is there one known source, or must the system decide which source to consult?
7. Is this a recommendation or an operation?
8. Who owns the state being changed?
9. Does the action require approval, authorization, policy, SLA, retry, or audit?
10. What fails if the model is wrong?

## Useful patterns

### Deterministic system + bounded AI

`Code -> AI capability -> Code`

### Deterministic system + investigation agent

Use when authoritative operational facts exist, but explanation may require adaptive investigation across additional evidence.

`Authoritative systems <-> Agent <-> additional knowledge`

### Agent requests deterministic process

`Agent -> request/start -> Workflow -> deterministic services`

### Workflow invokes bounded agent activity

`Workflow -> Agent activity -> Workflow`

### Multi-agent

Use only after documenting why one agent is insufficient.
