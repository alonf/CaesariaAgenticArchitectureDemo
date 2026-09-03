# scripts

One-time setup that cannot come from a workflow, and its inverse. Everything else this solution
deploys is a GitHub Actions workflow driving committed Bicep — see [docs/deployment.md](../docs/deployment.md).

| Script | Does | Idempotent |
| --- | --- | --- |
| [`Bootstrap-GitHubOidc.ps1`](Bootstrap-GitHubOidc.ps1) | Creates the Entra application, its federated credentials, subscription role assignments, and the GitHub environments and variables the workflows read | Yes — a second run reports `[exists]` and changes nothing |
| [`Remove-GitHubOidc.ps1`](Remove-GitHubOidc.ps1) | Removes all of the above. Does not touch Azure resources | Yes — anything already gone reports `[absent]` |

Both support `-WhatIf`. **Run that first**; it makes no changes and prints exactly what would happen.

```powershell
./scripts/Bootstrap-GitHubOidc.ps1 -ModelVersion 2026-04-24 -WhatIf
```

## Why this exists as a script rather than instructions

A workflow cannot create the identity it authenticates as. That is the only genuinely manual step in
this system, so it is automated, committed and reversible rather than written down as a list of
portal clicks that will be stale in a month.

## Conventions these follow

- **Preflight before mutation.** Every prerequisite is checked before anything is created, so a
  missing tool fails immediately instead of half way through a tenant change.
- **Non-zero exit codes are fatal.** A failed `az` call that prints an error and returns nothing
  would otherwise let the script carry on building on the absence.
- **No secrets.** Workload identity federation means there is nothing to store. `-UseSecrets` exists
  for organisations whose policy requires the identifiers masked anyway.
