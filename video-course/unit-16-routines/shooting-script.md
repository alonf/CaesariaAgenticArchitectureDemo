# Optional Lab 16 — activation is not authorization

Local preparation recording on Windows; code baseline `4e5a6d3`. No successful cloud execution or generated Insight is claimed. Synthetic review narration. Holds are labelled.

## 01 — What starts the investigation?
**Picture:** Manifest and local read trace from Lab 02.

**Narration:** Until now an operator has started the investigation. A routine changes when an agent is invoked: a schedule, a one-shot timer or a supported event can activate it. That does not decide what it may change. Our optional preparation lab follows the same L-417 investigation into a one-shot manifest, runs a local What If check and identifies the evidence required for a real run. We do not create, enable or dispatch a cloud routine in this recording. This is a preparation exercise, not a demonstration of successful scheduled execution.

## 02 — Inspect trigger, target and authority separately
**Picture:** Readable manifest and hosted tool surface.

**Narration:** Open deployment, routines, lighting investigation dot json. The timer has a placeholder future time, and the agent name is a placeholder too. The input asks for current Hub evidence and a bounded recommendation. It explicitly excludes remediation and work items. Those instructions communicate intent; the deployed capability surface must enforce the boundary. Inspect the hosted composition: the local C sharp tool is a state read. Work IQ is conditional on the toolbox and identity. Our wrapper assumes the unattended read-only composition, without a delegated user capability. Review that assumption before choosing any existing deployment.

## 03 — Run the dry check and reject a stale schedule
**Picture:** Actual WhatIf output and actual past-time rejection.

**Narration:** The management script validates the endpoint shape and requires an agent name and future time. With What If, it reports the intended disabled creation and returns before invoking the cloud CLI. Copy the command from the lab; its example endpoint is deliberately a placeholder. The output says What If, not created. Now pass a time in the past. The script rejects it locally. That failure is useful: configuration can be checked before a network call. The check does not validate a real deployment, permissions, service availability or execution. Replace assumptions with evidence in a later authorized cloud rehearsal.

## 04 — What a real run still needs
**Picture:** Recorded inventory authentication error and management code.

**Narration:** Our read-only inventory attempt failed at Azure Developer CLI authentication. The recorded error is evidence of that blocker, not evidence that the project has no routines. A real rehearsal must first restore sign-in and verify the exact target. Creation, enabling, dispatch and completion are separate states. This wrapper creates disabled, and provides Show and Runs for inspection. Dispatch acceptance alone would not prove the agent finished or read the intended Hub. Review the chosen invocation identity and permissions, retain the terminal run and response evidence, and compare authoritative state before and after. No write capability is justified merely because a timer fired.

## 05 — Pause and design the evidence
**Picture:** Exercise card.

**Narration:** Pause and run only the local dry check. Identify the trigger, target, prompt and capability assumptions. Change the time to the past and explain why it fails before any network access. Then write down what you would collect from an authorized cloud run: its terminal status, agent response, actual tool evidence and unchanged Hub state. Investigate how the routine identity differs from the human who created it, and how you would stop future activation without confusing that with cancelling an in-flight run. We have separated activation from authority and preparation from observed execution.
