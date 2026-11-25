# Optional Lab 17 — an observation becomes a reviewed regression

Outcome: follow the engineering feedback path in code and turn a real course failure into a proposed regression. This is a preparation lab; no generated Foundry Insight is claimed.

1. Recall ASSERT's measured behavior. Distinguish operational health, observed agent decisions, and hypotheses about improvement.
2. Use a real course failure: Security consultation hit a 10-second caller timeout while a specialist needed a larger budget. Inspect correlated evidence and the reviewed timeout regression test. Do not label this a Foundry-generated finding.
3. Inspect tools/FoundryInsights: existing monitors/findings only, pagination, unique IDs, expanded details, complete:false on partial failure. No monitoring creation or proposed patch execution.
4. Explain required representative telemetry and sensitive-content export decision. Missing monitors or empty findings are not health evidence. Current connected toolset has no agent_insights_get; historical zero monitors are historical, not this session's verified inventory.
5. Exercise: propose a concrete requirement and held-out case, review intervention, rerun and compare. No automatic promotion, policy change, deployment or publication.
