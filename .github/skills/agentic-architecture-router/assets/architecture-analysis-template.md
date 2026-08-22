# Architecture Analysis

## 1. Requirement in one sentence

<What new capability is actually required?>

## 2. Facts and unknowns

### Known from the existing architecture
- ...

### Known from the requirement
- ...

### Unknown / must not be assumed
- ...

## 3. Responsibility map

| Responsibility | Inputs / evidence | Authority owner | Mechanism | Why |
|---|---|---|---|---|
| ... | ... | ... | Deterministic / Bounded AI / Agent / Workflow / Tool / MCP / Knowledge | ... |

## 4. AI control-decision boundary

### Keep deterministic
- ...

### Bounded AI, if any
- ...

### Agentic responsibility, if any
- ...

State exactly what decision authority is delegated to AI.

## 5. Evidence model

| Evidence needed | Category | Source known in advance? | Access mechanism | Trust/authority |
|---|---|---:|---|---|
| ... | Operational state / structured data / organizational knowledge / documents / memory | Yes/No | API / Tool / MCP / knowledge query / other | authoritative / supporting |

## 6. Process-control boundary

Describe whether explicit workflow is required.

If yes:

`Trigger -> deterministic checks -> agent activity (if needed) -> policy/approval -> authoritative execution -> audit`

## 7. Consequential actions

| Action | Agent may recommend? | Agent may request? | Authorizer | Executor | Audit |
|---|---:|---:|---|---|---|
| ... | ... | ... | ... | ... | ... |

## 8. Technology mapping

Only now map mechanisms to technologies.

| Generic mechanism | Candidate technology | Why / required property |
|---|---|---|
| ... | ... | ... |

Use installed platform/product skills to validate this section.

## 9. Rejected alternatives

### All deterministic
...

### Bounded AI only
...

### Agent everywhere
...

### Multi-agent
...

### Workflow-only
...

## 10. Risks and open questions
- ...

## 11. Proposed architecture

Provide a concise component/relationship description and, when useful, a Mermaid diagram.

## 12. Decision summary

- **Least-autonomous viable design:** ...
- **AI decision authority:** ...
- **Authoritative state remains in:** ...
- **Consequential execution controlled by:** ...
- **Why this is agentic (if it is):** ...

**Architecture review required before implementation.**
