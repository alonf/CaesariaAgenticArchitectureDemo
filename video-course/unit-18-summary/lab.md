# Closing exercise — apply the architecture to your system

Choose **one** path: extend an existing application, or design a new C# agentic system. The deliverable is a one-page design brief plus one bounded experiment. You do not need every mechanism from the course.

## Your design brief

Copy this template into a local document:

```text
User and decision they need to make:
Observable outcome and independent verification:
Existing system or new system:
Authoritative services / systems of record:
Deterministic operations and invariants:
Why a bounded model call or an agent is needed:
First narrow capability and its contract:
Context sources, freshness and provenance:
Who can authorize a consequential action:
Where authorization and validation are enforced:
Session needs and business-operation persistence needs:
One representative success case:
One counterexample, denial or dependency failure:
What pending, failed, completed and unknown mean:
Smallest experiment, owner and next step:
```

## Path A — add an agentic layer

1. Select one question users struggle to answer in your existing application.
2. Identify its authoritative API and implement a focused read-only adapter in C#.
3. Expose only the capability required for that question. Keep existing business rules in their services.
4. Trace the request, tool selection, service result and final answer. Compare the answer with an independent service read.
5. Try missing evidence and a failed dependency. The answer should identify uncertainty or failure without inventing evidence or claiming a write.

**Checkpoint:** one useful investigation, with visible provenance and no unintended state change. Introduce a write only after defining its authorization, validation, receipt and recovery behavior.

Code to investigate: `Services/OperationsAgent.Api/Services/EnergyTools.cs`, the capability composition under `Services/OperationsAgent.Api/Services/Capabilities`, and the owning Hub's service contract. Locate the exact enforcement point; a description or model instruction alone is not enforcement.

## Path B — build a new system

1. Define the user outcome and name each domain's authoritative owner.
2. Draw a request path from the UI to the service that performs the operation. Mark trust boundaries and systems of record.
3. Build and test the deterministic operation before adding model-selected steps.
4. Add one C# agent capability for a task whose next step depends on evidence. Choose orchestration, specialists and hosting only when the requirements justify them.
5. Define success, denied, stale-state and unavailable-dependency cases. Trace each case to a service result and an observable operator outcome.

**Checkpoint:** one end-to-end slice whose success and failure you can explain. Additional agents are a design choice, not a completion requirement.

## Challenge your design

- Could deterministic code or one bounded model call satisfy the requirement more simply?
- Which statement is a live fact, which is retrieved evidence, and which is inference?
- What can a remembered conversation get wrong after the underlying system changes?
- If approval arrives after a revision changes, which component refuses stale work?
- After a timeout or restart, how do you discover the outcome before retrying a write?
- What specific failure would convince you that the design needs to change?

Use the code, documentation or an AI assistant to investigate these questions. Record the supporting evidence and review the proposed answer yourself.

## Recovery and reset

For the course demo, reopen the named lab's companion instructions and reapply its fixture in Director before repeating a repaired scenario. Clear only the context store that experiment calls for; a new conversation and a cleared case-memory store are different resets. Before resetting durable work, inspect any pending or unknown operation and reach an understood outcome. Detach the debugger when done. Do not blindly retry a consequential request after a timeout.

For your own system, use a local/sandbox fixture, save initial state and record a correlation identifier. Stop the experiment if completion is unknown; reconcile through the authoritative service before another attempt.

## Continue and share

- [Course repository and setup](https://github.com/alonf/CaesariaAgenticArchitectureDemo): use the README and the checkout's declared SDK.
- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/): agents, workflows and integration building blocks.
- [Agentic Architecture Skills](https://github.com/alonf/agentic-architecture-skills): structure the deterministic/model/agent/orchestration decision with a coding assistant.
- [Companion-book alignment](../book-alignment.md): architecture rationale and chapter references.

Share **Agentic C#** with a colleague or your team. Run one lab together, compare design briefs, and explain the evidence behind your choices. Course videos remain local review cuts until Alon chooses to publish them; this invitation does not publish or send anything automatically.

Production note: this chapter uses new synthetic review narration over excerpts from earlier recorded labs. It does not claim a new live run, production readiness, completed Routines dispatch, a retrieved generated Insight, or cloud Functions deployment.
