# Lab 14 — measure behavior, then investigate the evidence

Outcome: run a small live ASSERT comparison and distinguish deterministic checks, model judgments and missing evidence. Book chapter 13, pages 172–179; repository evaluation/assert_demo is the executable authority.

1. Recall the tool loop and deterministic ownership. Introduce three layers: model-free enforcement tests, deterministic checks over live agent behavior, and semantic model grading.
2. Director Evaluation and its Walkthrough explain the harness. A dedicated local instance is required because fixtures reset the city and supply scripted decisions.
3. Read current-state and forgotten-override requirements in cases.json before running them. Show the exact command and real output. Start with current-state, both compositions; then compare forgotten-override. Do not promise either score.
4. Open the actual generated report, expand evidence and inspect an actual failure/error if present. Explain Session baseline versus cumulative governed composition; this is not a controlled causal study of one policy.
5. Trace target.py scenario reset, public execution evidence and Hub verification. This projection is not raw per-tool timing or hidden reasoning.
6. Exercise: predict a result, run one case, inspect tool status and authoritative state, then propose a reviewed regression. Failure recovery separates environment errors from agent behavior; reset via Director afterward.

No deployment or shared cloud settings. Agent and judge inference use existing authorized model resources. Generated artifacts remain local.
