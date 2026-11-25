# Optional Lab 17 — turn observations into reviewed improvements

Local preparation recording on Windows; code baseline `4e5a6d3`. No successful cloud execution or generated Insight is claimed. Synthetic review narration. Holds are labelled.

## 01 — A finding starts an investigation
**Picture:** Real failed specialist call, then fixed run evidence.

**Narration:** Evaluation checks requirements we already know to ask about. Operational observation can reveal a problem we did not anticipate. Foundry Insights can identify recurring patterns in representative traces and suggest investigation or improvement. This optional lesson prepares that feedback loop. We have no retrieved generated finding in this session, so we use a real course incident as our own engineering example: a specialist call exceeded its caller timeout. We will inspect the regression check and the repository collector, then define what evidence would justify a change. This is not a generated Insights result.

## 02 — Separate observation, hypothesis and intervention
**Picture:** Actual timeout and no-retry tests in VS Code.

**Narration:** The observed failure was a caller giving up while another agent was still working. The hypothesis was a mismatched HTTP budget. We adjusted the specific client budget, kept automatic retries off for the POST, and verified a new live consultation separately. Read Operations Agent API Tests. One test delays a response for eleven seconds, beyond the old attempt timeout, and requires one successful call. The adjacent test returns service unavailable and requires exactly one attempt. Those are model-free boundary checks. They do not prove every model response is correct or that an unlimited timeout would be safe.

## 03 — Read findings without giving them authority
**Picture:** Insights collector pagination and completion marker.

**Narration:** The presenter-side Foundry Insights utility reads existing monitors and their findings. It does not become a tool of the Operations Agent. Inspect the asynchronous iteration, expanded details and unique identifier set. Then look at the final marker: complete true after successful retrieval, complete false after an exception. Preserve that distinction when a later page fails. An empty collection is not proof of health, and a missing monitor is an error. A generated recommendation is an input to engineering review. It cannot authorize a policy change, execute a proposed patch or deploy itself.

## 04 — Run local checks and state what is missing
**Picture:** Actual help output and invalid-endpoint rejection.

**Narration:** Run the utility with help. It prints the supported read modes without contacting a project. Now pass an invalid HTTP endpoint. It rejects that input before network access. The code is inspectable, but we still have not retrieved a generated finding. This session lacks the Insights retrieval tool, and we do not create a monitor or start an analysis to fill the gap. A future rehearsal needs representative traces, a selected existing monitor, appropriate access and a deliberate decision about sensitive content. Keep raw telemetry private and distinguish a historical empty result from a fresh observation.

## 05 — Pause and close the engineering loop
**Picture:** Exercise card and course recap.

**Narration:** Pause and choose one real failure from these labs. Write the observation, supporting correlation, alternative explanations and a proposed regression requirement. Identify the smallest intervention, then define a held-out case that could disprove your explanation. Review any generated recommendation before changing code and compare the new measured behavior with the original evidence. Across this course, the agent receives context and capabilities, deterministic services retain authority, and observable evidence tells us whether the intended outcome occurred. Your next task is to apply those boundaries to a system you own, with the same care about why and when.
