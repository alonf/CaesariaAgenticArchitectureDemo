# scripts

One-time setup that cannot come from a workflow, and its inverse. Everything else this solution
deploys is a GitHub Actions workflow driving committed Bicep — see [docs/deployment.md](../docs/deployment.md).

| Script | Does | Idempotent |
| --- | --- | --- |
| [`Bootstrap-GitHubOidc.ps1`](Bootstrap-GitHubOidc.ps1) | Creates one Entra application **per environment** with a single federated credential each, their role assignments, and the GitHub environments and variables the workflows read | Yes — a second run reports `[exists]` and changes nothing |
| [`Remove-GitHubOidc.ps1`](Remove-GitHubOidc.ps1) | Removes only what the bootstrap owns. Does not touch Azure resources or resource providers | Yes — anything already gone reports `[absent]` |

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
- **Reads distinguish absent from unreadable.** A 404 means absent. A 403 or a network failure is a
  failure, and stops the script — otherwise a bad token reads as "nothing exists" and the bootstrap
  creates a duplicate identity, or the teardown reports it cleaned up something it never saw.
- **Ambiguity is fatal.** Entra permits several applications with the same display name, and the
  application ID is the only unique identifier. Both scripts refuse to act on a name that resolves
  to more than one, rather than taking the first.
- **No secrets.** Workload identity federation means there is nothing to store. The workflows read
  identifiers from the `vars` context, and there is no option to write them anywhere else.
- **Least surprise on teardown.** `Remove-GitHubOidc.ps1` deletes the six variables it set and the
  two role assignments it created — not whole environments, not every role a principal holds.
  Deleting the environments needs `-RemoveEnvironments`, and the command carries
  `ConfirmImpact = 'High'`.
