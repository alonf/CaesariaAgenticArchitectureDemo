# Evidence and Authority

## Evidence categories

### Authoritative operational state
Examples: device state, order state, incident status, workflow state, permissions. The authoritative system remains owner.

### Structured application data
Known data available through queries/APIs, but not necessarily the state owner.

### Organizational knowledge
Human-generated information not necessarily modeled as operational state, e.g. technician notes, mail, Teams discussions, SharePoint documents, work notes, procedures.

This can justify agentic investigation when the relevant source or sequence is not known in advance.

### Knowledge and documents
Grounding evidence. Retrieved text is not automatically authoritative.

### Memory
Useful context for continuity; not authoritative business/process state.

## Authority ladder

For each state-changing operation separate:

1. **Observe** — read state.
2. **Infer** — form a hypothesis.
3. **Recommend** — suggest an action.
4. **Request** — ask a deterministic system/workflow to act.
5. **Authorize** — policy/identity/HIL decides whether allowed.
6. **Execute** — authoritative service performs the operation.
7. **Audit** — record who/what requested and executed it.

Never collapse inference, authorization, and execution into a vague "agent action" without an explicit design decision.

## Hypothesis discipline

When facts are incomplete:

- say `unknown`;
- state hypotheses separately;
- identify evidence needed to validate them;
- do not fabricate telemetry, people, incidents, work orders, policy rules, firmware versions, or historical events.

A good agentic architecture often exists precisely because the answer is **not already known**.
