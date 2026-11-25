# Lab 12 — discover and consult a peer agent

Outcome: explain the daylight override through a named Workforce peer, then prove its data boundary. Allow 25 minutes. Baseline `59017d7`; book chapter 11 pages 140–145. Local Windows services, real model calls, synthetic work records. C# source and commands also apply to the Mac setup in README.

## Walkthrough and checkpoints

1. Director → **A2A Delegation** → confirm. This resets **Lights On Reported by a Client**. Use local habitat.
2. Main application → **Walkthrough** → **Ask the workforce domain about L-417**.
3. Expect a completed **Consulted peer (A2A)** block naming Caesarea Workforce Agent, its provider and advertised skill `asset-maintenance-situation`. In this run transport is `A2A (HTTP+JSON)`. Read both the peer answer and the attributed Operations answer.
4. Verify open `WO-8732` explains the override; the older closed `WO-8610` does not explain the current incident. These records are evidence, not a successful repair.
5. Scroll below the answer and choose **Ask the workforce domain for the technician and labour cost**. Expect those fields to be unavailable to the peer; wording may vary.
6. Director → **Advanced controls** → **Workforce Records** → **Show work orders in full**. Compare the synthetic private fields with the shareable response below. This is a presenter view, not the agent tool result.
7. Fetch Energy Hub state. The lamp remains on with manual override and anomaly; no restore command occurred. Compare revision and last-command correlation before and after your requests.

```bash
curl --fail --silent --show-error https://localhost:7293/.well-known/agent-card.json > agent-card.json
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > hub-state.json
curl --fail --silent --show-error -H "X-Caesarea-Caller: workforce-agent" -H "X-Correlation-ID: course12-projection-check" https://localhost:7292/api/workforce/work-orders/WO-8732/shareable > shareable.json
```

The explicit caller header above is a **local demonstration stand-in, not authenticated identity**. Do not treat copying that header as a production authorization design.

## Actual evidence

- Maintenance question: `5573b6db91fa437ab1d912ab7d4a7741`.
- Private-field question: `a64566311f0f40aab657fc06a0502eeb`.
- Both completed; logs show peer search and shareable-details reads, followed by Operations composition.
- Hub snapshots after the first and second questions both have revision 5, manual override true, anomaly true, and last-command correlation `40a95ed379414f578d9c9327f3969bb2` (scenario application).
- Outer UI reports zero tool invocations and one local composer model round trip. This does **not** mean the remote agent used no tools. Inspect peer logs for its own execution.
- Actual discovered card advertises HTTP+JSON protocol version 1.0. Teach the captured card rather than assuming every hosting environment advertises the same bindings.

## Exact source

- `Services/WorkforceAgent.Api/Services/WorkforceAgentCard.cs`: published identity, provider, skills, protocol capabilities and interface.
- `Services/OperationsAgent.Api/Services/WorkforceDelegation.cs`: card resolver, binding preference, A2AAgent, remote session, bounded 60-second task and failure result.
- `Services/WorkforceAgent.Api/Services/WorkOrderTools.cs`: search and shareable-details tools.
- `Services/WorkforceHub.Api/Services/WorkforceHubService.cs`, `GetShareableDetails`: selected fields; no model-based redaction.
- `Services/OperationsAgent.Api/Services/FoundryOperationsAgent.cs`, `ConsultWorkforceAsync` / `ComposeFromConsultAsync`: explicit delegation and tool-less attributed composition.

## Failure and reset

If the peer is unavailable or exceeds its task budget, the response must state that maintenance context is missing. Check Aspire resource health, agent-card reachability and correlation across Operations, Workforce Agent and Workforce Hub. Retry only after recovering the read-only peer path. Long debugger pauses can exceed its bounded transport budget. Reprepare A2A Delegation for the next learner; reset keeps later cumulative capabilities available.

## Pause and investigate

- Why is this delegation selected by application code rather than by a model tool call?
- How do advertised skills differ from protocol capabilities?
- Which binding is actually advertised and which client chooses it?
- Would deleting the card description expose the private fields? Show the enforcing code.
- Why does a tool-less composer still need treatment of peer prose as untrusted data?
- How would authenticated service identity replace the local caller header?
- Why is a work-order explanation insufficient proof that the light was repaired?
