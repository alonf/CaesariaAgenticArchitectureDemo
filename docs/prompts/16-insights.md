# Presenter beat 16 — Insights

Requirement: repeated observed agent behavior should contribute evidence to the engineering loop,
beyond hand-authored lecture cases.

Use the optional Evaluation walkthrough step and
[Routines and Insights runbook](../runbooks/routines-and-insights.md). The implemented utility
lists existing monitors and retrieves their findings with expanded evidence and full SDK pagination.
It has no mutation commands and never feeds proposed fixes into the operational agent.

Show one real finding and its linked trace. Review the hypothesis, add an appropriate ASSERT case,
make a reviewed change and measure the result. Preserve absent evidence and version metadata as
absent. An empty collection, missing monitor or failed request is not a passing evaluation.

This beat needs a configured monitor and representative synthetic runs collected during rehearsal.
The November 17 read-only check found no monitor for the configured Operations Agent. Configure
monitoring and produce real evidence before advertising this as a live generated-insights demo.
