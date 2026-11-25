# Lab 09 — the corrective operation has a planned path

Real local Windows execution; baseline `843e0d0`. Synthetic review narration. Holds are labelled.

## 01 — An explicit path for a consequential action
**Picture:** Generated graph and actual Walkthrough.

**Narration:** The previous lesson let a remote tool request input. Now we put correction on an explicit path. A workflow is a graph of typed executors and edges: validate the current state, apply policy, obtain approval when required, execute, and verify. The model cannot simply skip those steps. Our outcome is to distinguish a denied operation, an approval that became stale, and a separately approved successful correction. This is an in-process Agent Framework workflow. Its pending human decision is not durable across a worker restart.

## 02 — Prepare, inspect and deny
**Picture:** Director reset, graph/YAML, run, pending revision9, deny.

**Narration:** Prepare Workflow in Director. This reapplies the daylight anomaly after our previous repair. In the main application read the Walkthrough and inspect the workflow definition. The diagram is generated from the code-built graph. The equivalent YAML is displayed for comparison; it is not the definition executing this run. Start remediation and wait for the approval node. Notice the asset and state revision in the request. Deny first. The timeline marks the gate declined and execution skipped. Verification remains unresolved, and refusal itself does not create a maintenance work item.

## 03 — An approval can become stale
**Picture:** Second pending gate; reapply fixture; approve old revision; actual rejected outcome.

**Narration:** Run again, but leave the approval open. In Director reapply the daylight fixture. It looks like the same lamp situation, yet the Hub revision has advanced from nine to eleven. Return to the original review and approve it. That decision still covers revision nine. The command carries the expected revision, so the Hub refuses the stale write. The executor reads current state again and finds that fresh approval would be required. It does not carry the old decision forward. The outcome is unresolved with no command executed. That is the correct result for this changed picture.

## 04 — The graph is real C sharp code
**Picture:** Director attach Visual Studio; actual graph builder stepping.

**Narration:** For a fresh run, arm the Workflow snippet and attach Visual Studio 2026 through Director. Visual Studio is a Windows choice; VS Code can attach on either supported operating system. Trigger remediation from the main application. The breakpoint stops before graph construction. Step into CreateRemediationWorkflow and watch the executor objects and conditional edges being connected. This graph, rather than an instruction asking the model to behave, owns the correction sequence. After building it, continue and review the new approval. This time it carries the current revision, eleven.

## 05 — Inspect the approved plan and result
**Picture:** Fresh approval; actual ExecuteRestoreExecutor breakpoint and expanded input/result.

**Narration:** Approve the fresh review. Now execution has stopped inside ExecuteRestoreExecutor. Expand input in Locals: action required is true, approval required is true, operator approved is true, and validated state revision is eleven. Expand Request to see L-417 and the correlation identifier. Step through the policy guards to the command gateway. The call passes that revision to the deterministic Hub. Step over it and expand result: precondition failed is false and status is Succeeded. We have observed both the decision entering deterministic code and the command result coming back. Verification still has its own job.

## 06 — Verify independently
**Picture:** Terminal timeline and saved authoritative Hub response.

**Narration:** Continue and return to the application. Execute has completed, then Verify reads state and confirms the lamp is off and matches its effective target. The map is green. Here is the independently saved Energy Hub response: reported on is false, manual override is false, revision is twelve, and the successful restore receipt carries this run correlation. The simulator supplied device confirmation. A command accepted or executed is not automatically a resolved incident; that is why we inspect the fresh state as well as the receipt.

## 07 — Pause and test the policy
**Picture:** Exercise card.

**Narration:** Pause and reproduce denial, stale approval, and fresh approval as three separate runs. Reset the daylight fixture between experiments where needed. Save the pending revision and compare it with the current Hub revision. Expect no restore for the stale review, then off after a fresh approved run. As an extension, apply Security Operation and observe the no-action path: intentional lighting is not an anomaly. Investigate where the graph branches, why a refusal does not create a work item, and why this in-memory gate cannot survive a restart. We have made control explicit and state-bound; next policy will follow a tool the model selects.
