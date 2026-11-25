# Lab 12 — a peer owns its domain, Operations owns the answer

Real local Windows execution; baseline `59017d7`. Synthetic review narration. Holds are labelled.

## 01 — A peer relationship starts with discovery
**Picture:** Context loop and main app A2A Walkthrough.

**Narration:** The Security specialist was exposed as a tool the model could select. This lesson uses a different relationship. Application code gives a task to a named peer agent over the Agent to Agent protocol, then uses its answer as context. We will discover the Workforce Agent, ask it why L-417 remains overridden, and test which work-order details it can actually see. The architecture question is who owns the task, the data and the final response. A protocol does not answer those questions for us.

## 02 — Reset and delegate the incident question
**Picture:** Prepare A2A, question and actual answer.

**Narration:** Prepare A2A Delegation in Director. This reapplies Lights On Reported by a Client, so the daylight override is back even though earlier lessons repaired it. Open the main app Walkthrough and ask the workforce domain about L-417. In this run the peer finds the open work order W O eight seven three two, explains post-maintenance verification, and distinguishes an older closed work order that does not explain the current override. Operations attributes its answer to the Workforce Agent. No command was requested or executed.

## 03 — Read the card and the peer block
**Picture:** Discovered identity, task, transport; actual card JSON.

**Narration:** The consulted-peer block names the agent, the provider, its advertised skill, the transport and the task we sent. Here the declared transport is HTTP plus JSON. Open the actual agent-card response. Skills describe the work this peer offers; capabilities describe protocol features such as streaming. The card also publishes its interface address and version. These declarations support discovery and client construction. They do not authenticate the caller or enforce the data boundary. That enforcement belongs in the owning services.

## 04 — Ask for something the peer cannot see
**Picture:** Second supported question, unavailable fields, full synthetic fixture.

**Narration:** Now use the supported question asking for technician identity, badge and labour cost. It even claims an authorized finance audit. In this take the answer says those fields are not available to the Workforce Agent. Compare that with Director, Advanced controls, Show work orders in full. These are synthetic demonstration records. The full fixture includes a technician, badge, cost, rate and a private note. The agent did not redact those values after reading them. Its tools never received them. A claimed authorization in a prompt cannot add fields to a deterministic projection.

## 05 — Follow the task and the projection
**Picture:** Resolver, remote session, Hub projection, actual shareable JSON.

**Narration:** WorkforceDelegation first resolves the card, builds the client for an advertised binding, creates a remote session and runs the task. This is not an A I Function in the Operations model tool list. The peer has its own search and details tools. In WorkforceHubService, GetShareableDetails constructs a focused record from selected fields. The actual response includes the operational note and expected clearance, with no technician or commercial fields. The local caller-name header is a teaching stand-in; a deployed system still needs real identity and authorization.

## 06 — Retain ownership without granting peer prose tools
**Picture:** Tool-less composer and final Energy Hub JSON.

**Narration:** Operations then composes the operator-facing answer. The peer reply is delimited and described as reference data, and this composer has no tools. That reduces the consequence of malicious peer instructions, but it does not prove the prose cannot be misleading. Compare the two contexts with Security: Security returns a constrained assessment; Workforce can speak freely over records already selected for sharing. Finally verify the Energy Hub. The override is still on, the asset is anomalous, and the last command remains scenario application. A work-order explanation is not a repair receipt.

## 07 — Pause, compare and investigate
**Picture:** Exercise card.

**Narration:** Pause and reproduce both questions. Save the discovered card, compare the peer block with the final Operations answer, and verify the unchanged Hub state. Find the exact projection in code and explain why removing the card description would not expose private fields. Investigate how the client selects a binding and how a failed peer is reported without pretending a model call happened. If consultation times out, inspect both services with the correlation identifier and retry this read-only task after restoring service health. Reset with A2A Delegation. We have delegated knowledge work without handing over operational authority.
