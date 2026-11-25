# Optional Lab 16 — activation is not authorization

Outcome: inspect and dry-run the repository's one-shot routine manifest without changing cloud state. This is a preparation lab, not a recorded successful dispatch.

1. Recall the same read-only incident investigation; replace an operator click with a scheduled activation.
2. Read deployment/routines/lighting-investigation.json and scripts/Manage-InvestigationRoutine.ps1. Show prompt, target, UTC schedule, disabled creation, explicit dispatch/run observation and cleanup.
3. Run WhatIf locally with placeholders and label it planned configuration. Show actual read-only inventory attempt failed at azd authentication, never claim scheduling or completion.
4. Explain unattended identity versus delegated Work IQ; no consequential workflow implied, no general event-trigger integration implemented.
5. Exercise: review manifest and identity, predict unchanged lamp, specify the evidence a future authorized run must retain. Do not create, dispatch or enable in this recording. Recovery requires interactive azd authentication and an authorized existing read-only deployment.
