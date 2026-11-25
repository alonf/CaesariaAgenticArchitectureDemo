# Lab 13 — compare habitats without inventing provenance

Outcome: trace execution, context and authorization across local and existing Foundry hosting. Allow 20 minutes. Baseline `59017d7`; companion book chapter 12. No deployment or cloud configuration change is required or performed.

## Steps and checkpoints

1. Director → Hosting; main app → Walkthrough. Start with LOCAL habitat and ask about L-417 work records. Inspect the **Simulated work knowledge** label. Recorded correlation `854c38d75f8c`.
2. With an existing authorized deployment, switch habitat to FOUNDRY HOSTED and ask the same question. Save the actual response identifier and inspect the answer. Do not infer tool execution from the habitat badge.
3. In this recording, response `caresp_0ab31` completed but said Work IQ was unavailable and no work-order evidence was used. Its description of cloud state was not independently verified at the cloud Hub. **This is not a successful Work IQ demonstration.**
4. The recorded static footnote claims Microsoft 365 was read and the cloud fixture is permanently faulted. Those claims are unsupported by this execution; the take exposes a presentation defect. Commit `4e5a6d3`, after this recording, corrects those static claims to conditional guidance. Prefer actual response/trace evidence. The local map belongs to the laptop city.
5. Read `Services/OperationsAgent.Hosted/Program.cs`: `AgentHost.CreateBuilder`, conditional `AddFoundryToolboxes`, shared `EnergyTools`, reduced tool list, `AddFoundryResponses`, `RegisterProtocol`.
6. Read `Services/OperationsAgent.Hosted/EnergyHubAuthorizationHandler.cs`: the host composes authentication around the shared read gateway. Compare agent identity for the Hub with user-delegated Work IQ. Instructions describing read-only behavior are weaker than removing write capabilities or enforcing permissions.

## Pause and investigate

- Which code is shared and which capabilities were deliberately omitted?
- What would prove that a particular document was read? Why is a static footnote insufficient?
- Can a cloud answer and the laptop map legitimately differ?
- What changes when a user-delegated capability is invoked unattended?
- Compare session persistence with execution recovery. Current [hosted-agent documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents) describes managed sessions and optional resilient execution; [Durable extension documentation](https://learn.microsoft.com/en-us/agent-framework/hosting/durable-extension) describes durable agent/workflow hosting. Do not repeat a blanket claim that only Functions can recover execution.

Recovery: inspect response status and correlated hosting logs, then verify deployment composition and existing permissions with the owner. Do not grant permissions or redeploy merely to complete this exercise. Reset habitat to LOCAL; reapply Lights On Reported by a Client before later local lessons if repaired.

Missing from this cut: verified Work IQ retrieval, independently captured cloud Hub state and a fresh portal walkthrough. These remain explicit pickup items, not simulated successes.
