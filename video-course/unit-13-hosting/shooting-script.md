# Lab 13 — the habitat changes; evidence still matters

Real local Windows execution; baseline `59017d7`. Synthetic review narration. Holds are labelled.

## 01 — Separate execution, context and authority
**Picture:** Architecture slide, then hosting Walkthrough.

**Narration:** The previous lessons changed what the agent could do. Hosting changes where its code runs and who operates that runtime. Keep three questions separate: where does execution happen, where is conversation context kept, and who authorizes access to real systems? Microsoft Agent Framework provides our development model. Foundry can provide a managed habitat. In this lesson we compare the existing local and hosted paths, inspect their actual answers, and trace the C sharp composition. We will not deploy or alter shared cloud configuration.

## 02 — Establish the local source first
**Picture:** Director, work-records question, simulated provenance.

**Narration:** Prepare Hosting in Director and open the main application Walkthrough. Begin with Local habitat and ask about L-417 work records. The answer refers to the seeded work order and technician note. Inspect the provenance label: simulated work knowledge. This is a real model invocation over synthetic training evidence. The Energy Hub remains the operational authority. A useful explanation of a work order is not a command receipt, and it does not clear the override.

## 03 — Measure the hosted path, including its limits
**Picture:** Habitat switch, submitted question, actual hosted response.

**Narration:** Now select Foundry Hosted and ask the same supported question. This take completed with a hosted response identifier, but it did not demonstrate the expected Work IQ retrieval. The answer explicitly says that a maintenance-record search tool was unavailable and that no work-order evidence was used. It also reports a different cloud Hub situation. That is the agent answer, not an independently captured cloud Hub receipt. The laptop map still shows our local anomaly. These are separate systems; switching habitat does not reset the deployed city.

## 04 — Do not turn explanatory text into evidence
**Picture:** Actual response and the incorrect static footnote.

**Narration:** Notice the footnote below the answer. It says Work IQ read Microsoft 365 and describes a permanently faulted cloud fixture. That text is static, and this execution does not support it. Treat it as a presentation defect, not proof that a tool ran. The correct learning checkpoint is to inspect the actual response, trace and source record. Missing evidence should remain missing. We are preserving this limitation in the review cut, with no new permission grant or cloud configuration change hidden behind the recording.

## 05 — Read the hosting boundary in C sharp
**Picture:** AgentHost, reduced tools, Responses protocol, identity handler and conditional toolbox.

**Narration:** Open OperationsAgent.Hosted, Program. AgentHost constructs the runtime. The agent reuses EnergyTools and the shared instruction core, but this is a deliberately reduced composition: the local approvals, workflow and peer delegation are not registered here. AddFoundryResponses and RegisterProtocol expose the Responses contract. EnergyHubAuthorizationHandler adds an Entra bearer token around the shared gateway. Work IQ is registered separately and only when its toolbox configuration is present. An agent identity reading the Hub and user-delegated document access are distinct boundaries. Neither is granted merely by adding a prompt.

## 06 — Pause and compare responsibilities
**Picture:** Exercise card.

**Narration:** Pause and trace the two compositions. Identify the source of state, the registered tools, the host endpoint and each identity boundary. If an existing authorized hosted deployment is available, compare its real response with the local answer and retain the response identifier. If Work IQ or consent is missing, report the blocker instead of treating the static footnote as evidence. Return habitat to Local before the next lesson. Both hosting models can support session state; optional Foundry resilient execution and Durable orchestration have explicit contracts. Our later recovery lesson demonstrates one concrete worker-restart boundary, not a universal hosting winner.
