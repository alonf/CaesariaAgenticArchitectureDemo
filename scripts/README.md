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
| [`Start-CaesareaDemo.ps1`](Start-CaesareaDemo.ps1) | The presenter's one command: checks the per-machine setup (hosted-agent endpoint in user secrets, Azure sign-in, and that the OneDrive work order is current — refreshing a stale one), repairs what it can, warns about the rest, starts the AppHost and opens the dashboard | Yes — a configured machine reports `[exists]` and just starts |
| [`Test-Agent365Readiness.ps1`](Test-Agent365Readiness.ps1) | Gate for the Agent 365 segment: the agent's Entra **agent identity**, its owners (flagging a pipeline-only owner), the tenant licence, and the agent-identity inventory | Read-only |
| [`Set-AgentOwner.ps1`](Set-AgentOwner.ps1) | Adds an accountable human owner (default: the signed-in user) to the hosted agent's identity, alongside the pipeline that created it | Yes — an existing owner reports `[exists]` |
| [`Start-TeamsOperatorRelay.ps1`](Start-TeamsOperatorRelay.ps1) | Presenter-run relay that answers a Teams channel's new messages with the hosted agent, honestly labeled ("relayed as \<presenter\>"). First run prompts once for Microsoft Graph consent (admin-restricted read scope); silent afterwards | The relay answers each message once per run |
| [`New-CaesareaOperator.ps1`](New-CaesareaOperator.ps1) | Mints the "Caesarea Operator" identity the relay can post as: Entra user, an Agent 365 seat from the pool the tenant already owns (Teams + mailbox + OneDrive + agent-governance plans), team membership, and the relay's Graph consent for that user alone | Yes — everything reports `[exists]` on a re-run |
| [`Set-AgentActivityProtocol.ps1`](Set-AgentActivityProtocol.ps1) | Declares the Activity (Teams/M365) protocol on the hosted agent's endpoint — the scriptable half of the native Teams-channel path; verifies Responses still serves | Yes — `[exists]` when already declared |
| [`Capture-AgentChannelState.ps1`](Capture-AgentChannelState.ps1) | Snapshots the resource group's ARM resources, any Azure Bot services/channels, and the agent record — run before/after a portal channel-add to reverse-engineer it into Bicep | Read-only |
| [`Publish-AgentToTeams.ps1`](Publish-AgentToTeams.ps1) | Deploys the messaging half of the Teams publish as code: declares the Activity protocol, then deploys [`infra/modules/teams-channel.bicep`](../infra/modules/teams-channel.bicep) — the Azure Bot + Teams channel that front the agent, authenticating as its own identity | Yes — the Bicep converges |

Setup and deployment scripts support `-WhatIf`. **Run that first** to preview the changes their
available dependency IDs allow. The Teams relay posts messages while running and has no `-WhatIf`.

```powershell
./scripts/Bootstrap-GitHubOidc.ps1 -ModelVersion 2026-04-24 -WhatIf
```

## Running from your own account or a fresh clone

Use PowerShell 7, Git, Azure CLI and GitHub CLI. Local builds also need the .NET SDK from
[`global.json`](../global.json). Run examples from the repository root. Fork or copy the repository
to a GitHub repository you administer, enable Actions, and make sure `origin` points to that copy;
scripts derive the repository from `origin` unless you pass `-Repository owner/repo`.

```powershell
az login --tenant '<your-tenant-id>'
az account set --subscription '<your-subscription-id>'
gh auth login
git remote get-url origin
Install-Module Microsoft.Graph.Authentication -Scope CurrentUser
```

The Graph module is needed for the OneDrive work-order and Teams relay scripts. Azure CLI and
Microsoft Graph PowerShell have separate sign-ins: use the intended tenant and user in each.
The licence and owner scripts default to the Azure CLI user; the work-order script writes to the
Graph user's OneDrive. Each presenter needs their own Foundry access, Microsoft 365 entitlements
and Work IQ consent. See [the deployment guide](../docs/deployment.md) for roles and deployment order.

The optional relay requires an existing team and channel; supply their actual display names:

```powershell
./scripts/Start-TeamsOperatorRelay.ps1 -TeamName 'Your demo team' -ChannelName 'Your channel'
# Optional separate Graph identity (requires user creation/licensing/consent permissions):
./scripts/New-CaesareaOperator.ps1 -TeamName 'Your demo team' -UsageLocation '<country-code>' -WhatIf
./scripts/New-CaesareaOperator.ps1 -TeamName 'Your demo team' -UsageLocation '<country-code>'
./scripts/Start-TeamsOperatorRelay.ps1 -TeamName 'Your demo team' -ChannelName 'Your channel' -Account 'caesarea-operator@contoso.com'
```

Replace the example names, country code and UPN. The admin and relay account must have joined the
team. A guest admin must pass an operator `-UserPrincipalName` in a verified domain of the target
tenant. Check the tenant has the requested licence SKU and the Teams/OneDrive entitlements needed
by the operator. Initialise the Microsoft Graph Command Line Tools service principal with an
admin-approved `Connect-MgGraph` session before provisioning the operator's relay consent.

`-Account` verifies the Graph identity selected during sign-in; the Foundry call still uses the
Azure CLI identity. Every channel participant's question therefore uses the relay runner's Foundry
and Work IQ access. The relay is a separate demo path from native Teams/Copilot publishing.

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
