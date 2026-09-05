# scripts

Setup that cannot come from a workflow, and its inverse. Everything else this solution deploys is a
GitHub Actions workflow driving committed Bicep — see [docs/deployment.md](../docs/deployment.md).

| Script | Does | Idempotent |
| --- | --- | --- |
| [`Bootstrap-GitHubOidc.ps1`](Bootstrap-GitHubOidc.ps1) | Creates one Entra application **per environment** with a single federated credential each, their role assignments, and the GitHub environments and variables the workflows read | Yes — a second run reports `[exists]` and changes nothing |
| [`Remove-GitHubOidc.ps1`](Remove-GitHubOidc.ps1) | Removes only what the bootstrap owns. Does not touch Azure resources or resource providers | Yes — anything already gone reports `[absent]` |
| [`Sync-PlatformVariables.ps1`](Sync-PlatformVariables.ps1) | Reads the latest platform deployment's outputs and writes them to the GitHub environment's variables, so the release workflows see what infra produced | Yes — every value is compared before it is written |
| [`Bootstrap-EnergyHubApi.ps1`](Bootstrap-EnergyHubApi.ps1) | Registers the Energy Hub as an Entra-protected API: identifier URI, `EnergyHub.Read` app role, service principal, and the GitHub variables naming them | Yes — the app role keeps its ID across runs, so grants survive |
| [`Grant-AgentEnergyHubAccess.ps1`](Grant-AgentEnergyHubAccess.ps1) | Grants the hosted agent's platform-minted identity the `EnergyHub.Read` role. Runs after the first agent version exists, because so does the identity | Yes |
| [`Grant-AgentProjectAccess.ps1`](Grant-AgentProjectAccess.ps1) | Grants the hosted agent's identity the Foundry User role on the project, which Work IQ calls require | Yes |
| [`Assign-Agent365License.ps1`](Assign-Agent365License.ps1) | Assigns an Agent 365 licence to a user (default: the signed-in one), which Agent 365 needs before it governs anything | Yes — an already-licensed user is reported before any free-seat check |
| [`Connect-WorkIQ.ps1`](Connect-WorkIQ.ps1) | Wires Work IQ end to end: service principal, client app, admin consent, Foundry connection, redirect URI, and a toolbox whose **default version** carries the tool | Yes — the default version is compared before any new one is created; connection drift stops the run |
| [`Test-FoundryToolbox.ps1`](Test-FoundryToolbox.ps1) | Gate: verifies the toolbox's default version carries exactly the expected tool on the expected connection, and fails non-zero on anything less | Read-only |
| [`New-CaesareaWorkOrder.ps1`](New-CaesareaWorkOrder.ps1) | Creates the demo work order in the **signed-in user's own OneDrive** — the document the hosted agent finds through Work IQ, as that person | Yes — an existing file is left alone without `-Force`, and every write is conditional (ETag) |
| [`Start-CaesareaDemo.ps1`](Start-CaesareaDemo.ps1) | The presenter's one command: checks the per-machine setup (hosted-agent endpoint in user secrets, Azure sign-in), repairs what it can, warns about the rest, and starts the AppHost | Yes — a configured machine reports `[exists]` and just starts |

All mutating scripts support `-WhatIf`. **Run that first**; it makes no changes and prints exactly
what would happen.

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
