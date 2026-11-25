# Lab 14 — measure behavior against a requirement

Real local Windows execution; baseline `59017d7`. Synthetic review narration. Holds are labelled.

## 01 — Three different kinds of evidence
**Picture:** Main app Evaluation Walkthrough, reviewed cases.

**Narration:** We have watched the agent choose tools, retrieve evidence and respect control boundaries. How do we check that behavior after a change? Separate three kinds of evidence. Model-free tests verify our enforcement code. Deterministic checks over live runs verify observed behavior, such as a completed state read. A model judge measures narrower semantic requirements. This lesson runs two reviewed cases through two real compositions, then opens the generated report. It is a small measured sample, not a certification and not a full-suite result.

## 02 — Read the requirement before running
**Picture:** Readable cases and launcher.

**Narration:** Open evaluation, assert demo, cases dot json. Current state requires a completed authoritative read and forbids a change. Forgotten override also requires work knowledge before explaining the supported cause. The baseline is the real Session-stage composition. The governed arm is the cumulative local composition. We compare them against the same requirements. The PowerShell launcher selects cases and arms; on a Mac, use the Python module command in the lab. Run on a dedicated instance because the harness resets fixtures and supplies scripted decisions. Our recording runs current state and forgotten override, once per arm.

## 03 — An evaluation error is not an agent failure
**Picture:** First report ERROR, expanded passing execution checks.

**Narration:** The first attempt did not produce a usable score. Expand the error row: the agent did complete get streetlight state, and the deterministic checks passed. The judge failed because conflicting authentication inputs reached its client. Therefore the report says error, not pass and not a behavioral fail. We isolated the judge process to Azure sign-in credentials and ran again without changing the agent or requirements. This is why an evaluation harness needs its own diagnostics. Missing measurement must never silently become success.

## 04 — Compare the actual results
**Picture:** Final report and baseline failure details.

**Narration:** The clean run passed current state in both arms. For forgotten override, the baseline failed and the governed arm passed. Inspect the failed baseline. It read live state but explicitly lacked the work-record search tool. That honest limitation is safer than inventing a work order, yet it does not satisfy the requested capability. The deterministic check names the missing completed search, and the judge explains its reasoning from the evidence. This comparison adds capabilities and controls together. It does not isolate the causal effect of one policy.

## 05 — Explain the pass and the adapter
**Picture:** Governed evidence, tool list, target.py collection and checks.

**Narration:** The governed answer cites the seeded work order and technician note. Its completed tools include skill loading, the state read and work-knowledge search. It recommends a controlled next step without claiming that the repair happened. In target dot py, the adapter captures state before and after, sends the actual question, retains the session, and collects correlated approvals, workflows and work items. Checks inspect those observations separately from the judge. The trace supplied here is a public API projection. It does not expose hidden reasoning or original per-tool timings.

## 06 — Pause, reproduce and challenge the measurement
**Picture:** Exercise card.

**Narration:** Pause and run current state first. Predict what evidence should be present, then compare the actual tool status, answer and Hub state. Next run forgotten override in both arms and explain any difference without promising these exact scores. Inspect one judge rationale against the underlying observation. Propose a new case for missing evidence or denial, review it, then rerun on frozen and held-out inputs. If the judge is unavailable, report an error and repair the evaluation environment. Reset the city after the harness finishes. Evaluation turns architectural intent into inspectable evidence, with uncertainty that we must still explain.
