# Deploying Caesarea

Everything here is a committed script or a committed workflow. Nothing depends on a command someone
once typed. Delete the GitHub repository, the Azure resources and the Foundry project, and you can
rebuild all of it from this file.

The demo also runs entirely on a laptop with no Azure at all beyond a model deployment — see
[the README](../README.md). This document is about the cloud half.

---

## What gets deployed, and by what

| Layer | What | How | When it changes |
| --- | --- | --- | --- |
| **Identity** | One Entra app **per environment**, its federated credential, subscription roles, GitHub environments and variables | [`scripts/Bootstrap-GitHubOidc.ps1`](../scripts/Bootstrap-GitHubOidc.ps1) | once |
| **Platform** | Foundry account, project, capability host, model deployment, registry, observability, RBAC | [`deploy-infra.yml`](../.github/workflows/deploy-infra.yml) → [`infra/`](../infra/) | rarely |
| **Application** | The hosted agent version: image digest, CPU, environment | [`deploy-hosted-agent.yml`](../.github/workflows/deploy-hosted-agent.yml) | every release |

The bootstrap creates the identity the workflows authenticate as. Additional scripts configure
directory grants, Work IQ, Agent 365 and the Teams channel using the deploying user's permissions.

### The identity model

**One identity per environment.** `dev` and `prod` each get their own Entra application with exactly
one federated credential, bound to that environment's OIDC subject alone. A token minted for a dev
deployment is not accepted by the prod application. That is what makes the prod gate a boundary
rather than a convention.

**Pull requests get no Azure identity at all.** They run `az bicep build`, which needs no
credential. Cloud what-if runs on manual dispatch, behind the environment gate. A fork or an edited
workflow therefore cannot obtain deployment rights by opening a pull request.

**A limitation worth knowing.** Both identities hold Contributor and Role Based Access Control
Administrator at *subscription* scope, because the platform template creates its own resource group
and cannot be scoped below one. dev and prod are separated by identity and federation subject, not
by Azure scope — a compromised dev identity could still reach prod resources. The real fix is a
subscription per environment; pass `-SubscriptionId` to point the bootstrap at one. Resource-group
scoping would be a half-measure that reads as isolation without being it.

Why the platform and the application are separate, and why the agent version is not in the Bicep, is
in [infra/README.md](../infra/README.md).

---

## Prerequisites

| | Minimum | Check |
| --- | --- | --- |
| PowerShell | 7.0 | `pwsh --version` |
| Azure CLI | 2.80 | `az version` |
| GitHub CLI | 2.0 | `gh --version` |
| Azure sign-in | — | `az account show` |
| GitHub sign-in | scopes `repo`, `workflow` | `gh auth status` |

Start from a fork or copy that **you administer**, with Actions enabled and `origin` pointing to
your repository. The scripts resolve `owner/repo` from `origin`; pass `-Repository` where supported
if you intentionally use another repository. Run commands from the repository root. Blocks marked
`bash` use Bash continuation syntax; the `.ps1` scripts run in PowerShell 7.

```powershell
az login --tenant '<your-tenant-id>'
az account set --subscription '<your-subscription-id>'
gh auth login
git remote get-url origin
```

All `<...>` values are placeholders for your deployment. Azure CLI commands use the selected
subscription and tenant unless a script explicitly accepts an override. Use the tenant containing
your Foundry project for directory grants and licensing too. Work-order and Teams relay scripts
also need `Microsoft.Graph.Authentication` (`Install-Module Microsoft.Graph.Authentication -Scope CurrentUser`),
with a separate Graph sign-in to the intended Microsoft 365 account. Local builds need the .NET SDK
selected by `global.json`.

Each new user needs their own Foundry data-plane access; CI's role assignments do not grant access
to the person running the demo. Have your administrator grant the appropriate Foundry role for
invocation or project management, and the script-specific directory roles described below. These
scripts target Azure public cloud and the repository's `dev`/`prod` naming conventions. An existing
deployment with different resource names needs the relevant endpoint/resource-group overrides.

**Permissions.** In the Entra tenant you need to be able to create application registrations —
*Application Developer* is enough, *Global Administrator* obviously is. On the subscription you need
**Owner**, or **Contributor + Role Based Access Control Administrator**: the bootstrap creates role
assignments, which Contributor alone cannot do.

**Region.** Foundry hosted agents are not available everywhere. `westus3` is used throughout these
examples. The current list is in the [hosted agents
documentation](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents#region-availability).

**Model quota.** The platform deploys `gpt-5.5` at 50k TPM. Check headroom first:

```bash
az cognitiveservices usage list --location westus3 \
  --query "[?contains(name.value,'gpt-5.5')].{name:name.value,used:currentValue,limit:limit}" -o table
```

---

## Step 1 — Bootstrap the CI identity

Run the preview first. It changes nothing and tells you what it would do:

```powershell
./scripts/Bootstrap-GitHubOidc.ps1 -ModelVersion 2026-04-24 -WhatIf
```

Expected output on a clean tenant, abridged — this is a real run, not an illustration:

```text
=== Checking prerequisites
  az and gh are present.
  Subscription: <your subscription> (<id>)
  Tenant:       <tenant id>
  Repository:   <owner>/<repo>
  Model:        gpt-5.5 2026-04-24 is available in westus3.

=== Registering resource providers
  [exists] Microsoft.CognitiveServices
  ... one line per provider ...

=== Identity for 'dev' (caesarea-github-deploy-dev)
  [created] application (<app id>)
  [created] service principal (<object id>)
  [created] federated credential -> repo:<owner>/<repo>:environment:dev
  [created] Contributor at subscription scope
  [created] Role Based Access Control Administrator at subscription scope

=== Identity for 'prod' (caesarea-github-deploy-prod)
  ... the same five lines, with different identifiers ...

=== Configuring GitHub environments
  [created] environment 'dev' (branch: main, UNGATED)
  [created] dev: 6 of 6 variables set
  [created] environment 'prod' (branch: main, UNGATED)
  [created] prod: 6 of 6 variables set
```

The two applications having **different** identifiers is the thing to check. `AZURE_CLIENT_ID` on
`dev` and on `prod` must not match — if they do, the prod gate is not a boundary.

Then run it for real:

```powershell
./scripts/Bootstrap-GitHubOidc.ps1 -ModelVersion 2026-04-24
```

### If your repository is private and personally owned

GitHub does not offer environment protection rules on private repositories outside Enterprise. A
required-reviewer gate on `prod` is accepted by the API there and enforces nothing.

The bootstrap detects this and **stops**, rather than producing a prod environment you believe is
gated and is not. Either move the repository to an organisation on a plan that supports protected
environments, or accept it deliberately:

```powershell
./scripts/Bootstrap-GitHubOidc.ps1 -ModelVersion 2026-04-24 -AllowUngatedProduction
```

Where gating is available, prod also gets `prevent_self_review`, so whoever starts a production
deployment cannot approve their own.

Resolve a current model version rather than copying the one above — versions are retired:

```bash
az cognitiveservices model list --location westus3 \
  --query "[?model.name=='gpt-5.5'].model.version" -o tsv
```

**It is idempotent.** Run it again and every line reports `[exists]`, including the GitHub half —
variables are compared before being written, so "already correct" is distinguished from "set". That
is the check that it worked, and the check that a later run has not drifted.

```text
=== Identity for 'dev' (caesarea-github-deploy-dev)
  [exists] application (<app id>)
  [exists] service principal (<object id>)
  [exists] federated credential -> repo:<owner>/<repo>:environment:dev
  [exists] Contributor at subscription scope
  [exists] Role Based Access Control Administrator at subscription scope
...
  [exists] dev: 6 variables already correct
```

Verify it independently rather than trusting the report:

```bash
gh api repos/<owner>/<repo>/environments/dev/variables  --jq '.variables[] | "\(.name)=\(.value)"'
gh api repos/<owner>/<repo>/environments/prod/variables --jq '.variables[] | "\(.name)=\(.value)"'
```

**What `-WhatIf` does and does not cover.** On a clean tenant it stops at the first identity
dependency: it reports that it would create the application, then says that the service principal,
federated credential, role assignments and GitHub configuration all need that application's ID and
cannot be previewed. That is deliberate — inventing a placeholder ID would produce a plan that reads
as more thorough than it is. Once the identities exist, a second `-WhatIf` covers everything.

**No secret is created, and none can be.** GitHub proves its identity per run with a short-lived
OIDC token. The repository holds identifiers only — client ID, tenant ID, subscription ID — which
are not credentials, and which the workflows read from the `vars` context. There is deliberately no
option to store them as secrets: masking an identifier only makes a failed deployment harder to
read, and an earlier version of this script offered a `-UseSecrets` switch that wrote configuration
the workflows could not consume at all.

### Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Everything applied, or already existed |
| non-zero | Reported on the failing step, with the underlying `az` or `gh` output. Nothing is left half-configured that a re-run will not converge |

---

## Step 2 — Push

```bash
git push
```

The workflows have to exist on the default branch before they can be run.

---

## Step 3 — Provision the platform

**Actions → deploy-infra → Run workflow.** Environment `dev`, mode `preview`.

The preview job runs `az deployment sub what-if`, which is a read-only preflight against the real
resource providers. It catches what a Bicep compile cannot — a property the service rejects, a
missing prerequisite, a model version absent from the region.

Read the job summary. On a first run it reports **12 changes**:

```text
## Platform preview — dev

**12 change(s):**

- `Create` resourceGroups/rg-caesarea-dev
- `Create` Microsoft.CognitiveServices/accounts/aif-caesarea-<token>
- `Create` Microsoft.CognitiveServices/accounts/<...>/capabilityHosts/agents
- `Create` Microsoft.CognitiveServices/accounts/<...>/deployments/gpt-5.5
- `Create` Microsoft.CognitiveServices/accounts/<...>/projects/caesarea-dev
- `Create` Microsoft.CognitiveServices/accounts/<...>/projects/<...>/connections/application-insights
- `Create` Microsoft.ContainerRegistry/registries/crcaesarea<token>
- `Create` Microsoft.Insights/components/appi-caesarea-dev
- `Create` Microsoft.OperationalInsights/workspaces/log-caesarea-dev
- `Create` Microsoft.Authorization/roleAssignments/... (×3)

_2 resource(s) could not be analysed ahead of deployment..._
```

Those two unanalysable resources are expected: they are role assignments whose names derive from a
principal ID that does not exist until the deployment runs. It is not a warning about your template.

Then **run it again with mode `apply`**. The summary ends with the platform outputs.

### Confirming idempotency

Re-running `apply` against an already-deployed environment is safe and converges: same resources,
same names, same outputs, nothing duplicated. That is the property that matters, and it is the one to
check — run `apply` twice and compare the outputs.

**What `preview` will not tell you is "No changes."** Expect it to report roughly six `Modify`
entries forever, on an environment that is perfectly up to date. They are artefacts of how what-if
compares, not drift:

- **Server-computed properties the template never declares.** What-if diffs the template against the
  live resource, so anything Azure populates itself — `properties.armFeatures`, `associatedProjects`
  and `defaultProject` on the account, `currentCapacity` and `raiPolicyName` on the model deployment,
  `kind`, `endpoints`, `internalId` and `isDefault` on the project, `dataEndpointEnabled` and
  `encryption` on the registry — is reported as a deletion. An apply does not remove them.
- **The Application Insights connection's `credentials`,** which shows as a create every time.
  What-if cannot read a secure value to compare against, so it can only assume it differs.

Two more are reported as `Unsupported` rather than `Modify`: the role assignments whose names derive
from the project's principal ID, which does not exist until the deployment runs. Also expected.

So read the preview for the shape of the change, not for silence. A `Create` or a `Delete` of a whole
resource is real and worth understanding. A `Modify` whose delta is entirely server-computed
properties is what an unchanged environment looks like.

If you want a genuinely quiet diff, that is what the outputs are for: they are derived from the
deployed resources, and they are stable across applies.

### Feeding the outputs forward

The apply prints the platform outputs; the application workflow reads them from the environment's
variables. One script joins the two:

```powershell
./scripts/Sync-PlatformVariables.ps1 -Environment dev -WhatIf   # preview
./scripts/Sync-PlatformVariables.ps1 -Environment dev
```

It finds the most recent successful deployment that produced `rg-caesarea-dev`, reads its outputs and
writes the resource group, registry, Container Apps environment, services identity, Foundry endpoint
and model variables. Every value is compared before it is written,
so a second run against an unchanged deployment reports `[exists]` for each and changes nothing.

`ENERGYHUB_BASE_URI` is not among them. The Energy Hub is an application deployment rather than part
of the platform, so its address is passed in rather than read:

```powershell
./scripts/Sync-PlatformVariables.ps1 -Environment dev -EnergyHubBaseUri https://energyhub.<...>.azurecontainerapps.io
```

Until it is supplied the script says so and leaves the variable alone, because an empty value would
let the release workflow succeed and the agent fail on its first tool call.

### Deploy the Energy Hub before the agent

The hosted agent needs the deployed city services and their API registration. After syncing the
platform outputs, run:

```powershell
./scripts/Bootstrap-EnergyHubApi.ps1 -Environment dev -WhatIf
./scripts/Bootstrap-EnergyHubApi.ps1 -Environment dev
gh workflow run deploy-services.yml -f environmentName=dev
```

Wait for `deploy-services` to succeed. Its summary supplies the actual Energy Hub URL and the
`Sync-PlatformVariables.ps1 -EnergyHubBaseUri ...` command to run. Copy that command and run it
before releasing the agent. The agent workflow rejects missing or placeholder Hub addresses.

**Why this is a script and not a step in `deploy-infra.yml`.** Writing environment variables needs a
GitHub credential with administration rights, and the workflow's built-in `GITHUB_TOKEN` cannot be
granted it — `permissions:` has no environments scope. Automating it inside the pipeline would mean
storing a long-lived personal access token as a secret. Running one idempotent script as yourself
after a deployment is the better trade.

---

## Step 4 — Deploy the agent

```bash
gh workflow run deploy-hosted-agent.yml -f environmentName=dev
```

[`deploy-hosted-agent.yml`](../.github/workflows/deploy-hosted-agent.yml) builds
`Services/OperationsAgent.Hosted` into an image using Docker Buildx on the GitHub runner and pushes
it to ACR, resolves the image
digest, creates an immutable agent version **by digest rather than by tag**, waits for it to become
`active`, reads back the agent's own Entra identity and reports its access, then
smoke-tests the deployed endpoint.

The identity step is last for a reason: the platform mints a dedicated Entra identity for the agent
when its first version is created, so that principal does not exist at provisioning time and cannot
be bound in Bicep. Grant its downstream access after that first identity exists:

```powershell
./scripts/Grant-AgentEnergyHubAccess.ps1 -Environment dev -WhatIf
./scripts/Grant-AgentEnergyHubAccess.ps1 -Environment dev
./scripts/Grant-AgentProjectAccess.ps1 -Environment dev -WhatIf
./scripts/Grant-AgentProjectAccess.ps1 -Environment dev
```

On a fresh deployment the first workflow can reach `active` and then fail its Energy Hub smoke
test because `EnergyHub.Read` has not yet been granted. Once the grants have propagated, rerun the
workflow and require the smoke test to pass before continuing. The pipeline deliberately cannot
grant tenant-wide Graph app roles; a directory administrator performs that step through the script.

The workflow touches no infrastructure. Everything a platform team owns was provisioned in step 3.

---

## Step 5 — License Agent 365

Agent 365 governs the per-agent Entra identities the hosted runtime mints. It needs **at least one
assigned licence in the tenant** before it shows anything, and with none assigned it does not fail
loudly: the portal loads, the agent runs, and telemetry is quietly dropped. That reads as a broken
integration rather than a missing licence, which is why this step comes before any Agent 365 work
rather than after it.

```powershell
./scripts/Assign-Agent365License.ps1 -WhatIf   # preview
./scripts/Assign-Agent365License.ps1
```

It defaults to the **signed-in user**, so nobody's identity is written into this repository and a
reader can run it against their own tenant unchanged. Pass `-UserPrincipalName` to target someone
else.

**What you need before it will work:**

| Requirement | Why |
| --- | --- |
| An Agent 365 subscription in the tenant | The script names the SKUs it can see when it cannot find yours |
| A directory role that can assign licences — User Administrator, License Administrator or Global Administrator | A plain member gets `Authorization_RequestDenied` |
| A `usageLocation` on the target user | Licence assignment fails without one, and the Graph error does not say so. Pass `-UsageLocation IL` (or your country code) and the script sets it, but only when the user has none |
| `az login` to **the tenant that holds the Foundry project** | A licence in another tenant governs nothing here |

That last row is the one worth checking twice. The agents, the registry and the licences must all be
in the same tenant; a licence bought against a different directory looks assigned and governs nothing.

Re-running is free — an already-licensed user is reported and left alone.

## Step 6 — Connect Work IQ

Work IQ is the Microsoft 365 intelligence layer. Connected this way, the agent asks it questions **as
the signed-in user**: Foundry performs the OAuth on-behalf-of exchange, the agent never holds a user
token, and Microsoft 365 decides what comes back — permissions and sensitivity labels included.
Application-only access is not supported, and that is the point. An agent that could read everyone's
mail would be a weaker governance demonstration, not a stronger one.

```powershell
./scripts/Connect-WorkIQ.ps1 -Environment dev -WhatIf   # preview
./scripts/Connect-WorkIQ.ps1 -Environment dev
./scripts/Test-FoundryToolbox.ps1 -ToolboxName caesarea-workiq   # verify
```

After the toolbox gate passes, enable it for the hosted agent and release a new version:

```powershell
gh variable set WORKIQ_TOOLBOX --env dev --repo '<owner>/<repo>' --body 'caesarea-workiq'
gh workflow run deploy-hosted-agent.yml -f environmentName=dev
```

Use your toolbox name if you changed it. Creating a toolbox does not set this GitHub variable;
without it the hosted composition has no Work IQ tool.

Seven steps, all API calls, no portal step:

1. Provision the Work IQ service principal (`fdcc1f02-fc51-4226-8753-f668596af7f7`). Skip it and the
   permission is not findable.
2. Register a single-tenant confidential client app — the app an admin authorises.
3. Add delegated `WorkIQAgent.Ask` and grant tenant-wide consent.
4. Mint a client secret, handed straight to the connection and never printed.
5. Create the project connection — `RemoteA2A`, because **Work IQ is itself an A2A agent**.
6. Read the OAuth redirect URL the connection returns and register it on the app. The ordering is
   forced: the URL does not exist until the connection does.
7. Ensure the toolbox's **default version** carries `work_iq_preview` — what a hosted agent reaches
   through `AddFoundryToolboxes`. The default is compared first; on drift an exactly-matching staged
   version is reused (or one is created), its own MCP endpoint is exercised — initialize, then
   tools/list, with the documented `CONSENT_REQUIRED` answer accepted as a healthy unconsented
   state — and only a version that answers is promoted. Versions are immutable and only a toolbox's
   very first one becomes the default for free; an earlier version of the script created one per
   run and quietly grew a pile the runtime never looked at.

Steps 1 and 3 need **Global Administrator**; activate it just-in-time through PIM and deactivate
after. Everything else needs only the Foundry roles from Step 1 of this document.

**One boundary to know before demoing it as "read-only": it is not.** `WorkIQAgent.Ask` can act on
Microsoft 365 content as well as read it. The hosted agent is constrained to evidence retrieval by
an explicit paragraph in its instructions — a behavioural boundary, stated as such in the demo. An
agent that must be *unable* to write needs a narrower permission when one exists, or an
approval-gated composition around the tool.

**Consent is per person, and the platform surfaces it.** The first time Work IQ acts for a given
user, the hosted run returns `status: incomplete` with an `oauth_consent_request` output item
carrying a consent link instead of text. The Command Center renders it as a consent prompt; open
it, consent as yourself, ask again. Do this in rehearsal, not on stage.

**Two prerequisites this script cannot give you**, both of which fail at runtime rather than at setup:

| Symptom | Cause |
| --- | --- |
| `403 Forbidden` | Work IQ API calls need usage-based billing with Copilot Credits |
| `Principal does not have access to API/Operation` | The agent's runtime identity needs **Foundry User** on the project |

A Foundry connection's fields **cannot be edited after creation**. The script compares an existing
connection's non-secret fields against what it would have created: a match is reported and left
alone, drift stops the run with the exact delete command and the drifted fields named. The secret
itself cannot be read back, which is the one honest gap in that comparison.

### A correction worth keeping

An earlier version of this document said a toolbox was "the one manual step" that could not be
scripted. That was wrong, and the way it was wrong is instructive: the probe tried `POST /toolboxes`
and `PUT /toolboxes/{name}`, got 405 from both, and generalised. It never tried
`POST /toolboxes/{name}/versions`, which is where creation actually lives. Two probes of a plausible
shape are not a survey of an API.

## Step 7 — The work order, and pointing the demo at the hosted agent

Two last pieces turn the deployment into the Hosting stage's demo beat:

```powershell
./scripts/New-CaesareaWorkOrder.ps1 -WhatIf   # preview
./scripts/New-CaesareaWorkOrder.ps1           # the work order, in YOUR OneDrive
```

The document carries a **source-of-record line naming Microsoft 365** and a detail (the replacement
diffuser) the local simulated store never contained — that is how an audience can tell the hosted
answer read the real document. Microsoft 365 must index the file before Work IQ finds it; give it a
few minutes.

The cloud city agrees with that document by construction: the deployed SmartPole boots into the
forgotten-override situation (`SmartPoleSimulator__StartWithForgottenOverride` in
[infra/apps.bicep](../infra/apps.bicep)) — lamp ON during daylight, override engaged, recent
maintenance — because the cloud has no switchboard to drive it there: the deployed demo surface is
deliberately off. The two cities are separate realities that merely start in the same place; the
demo's scenarios and restores act on the laptop's city only.

Then tell the Command Center where the hosted agent lives. The project endpoint names a tenant, so
it goes in user secrets rather than a committed file — and because a per-machine step done once and
forgotten is exactly what fails on stage, the start script owns it:

```powershell
./scripts/Start-CaesareaDemo.ps1 -SetupOnly   # resolve and store the endpoint, check the sign-in
./scripts/Start-CaesareaDemo.ps1              # ...or just start; it checks first, every time
```

It resolves the endpoint from the GitHub environment's `FOUNDRY_PROJECT_ENDPOINT` (or the Foundry
account in the resource group), validates its shape the same way the app does at startup, and
stores it. The manual equivalent, when you would rather state the value:

```powershell
dotnet user-secrets set "CommandCenterWeb:HostedAgent:ProjectEndpoint" "https://<account>.services.ai.azure.com/api/projects/<project>" --project Apps/CommandCenter.Web
```

With that set, the demo's Hosting stage works as scripted in
[docs/prompts/12-hosting.md](prompts/12-hosting.md): the switchboard gains **Habitat: LOCAL /
FOUNDRY HOSTED**, and the same records question answers from the simulated store or from the
presenter's own OneDrive depending on the flip. The hosted call is made with the presenter's own
`az login` credential — whoever runs the demo is who the agent sees.

## Step 8 — Agent 365 ownership and Teams/Copilot distribution

With the agent deployed and the tenant licensed, assign an accountable owner and check readiness:

```powershell
./scripts/Set-AgentOwner.ps1 -Environment dev -WhatIf
./scripts/Set-AgentOwner.ps1 -Environment dev
./scripts/Test-Agent365Readiness.ps1 -Environment dev -RequireHumanOwner
./scripts/Publish-AgentToTeams.ps1 -Environment dev -WhatIf
./scripts/Publish-AgentToTeams.ps1 -Environment dev
```

The owner defaults to the signed-in Azure CLI user; pass `-OwnerUserPrincipalName` to choose another
accountable person. Agent 365 readiness checks identity, ownership and tenant licensing; it does
not prove the agent is available or responding in Teams/Copilot.

`Publish-AgentToTeams.ps1` creates the bot/channel bridge. This repository does not yet automate
the store-registration call. Complete publishing in Foundry with your own developer metadata and
the appropriate audience. **Just you** initially lists the agent for the publisher; shared Teams
participants need the required Foundry access. **People in your organization** requires Microsoft
365 admin approval and is the route for tenant-wide discovery. Microsoft also documents a REST
publish API, which can automate that remaining step. See [Microsoft's publishing guide](https://learn.microsoft.com/azure/foundry/agents/how-to/publish-copilot).

There is a recorded runtime limitation in this demo: the Work IQ toolbox fails on the native
bot/Activity channel's delegated-user path. The Command Center's Responses path is the verified
Work IQ route. Do not treat a successful bot deployment as proof that Work IQ works in Teams;
rehearse live-state and work-record questions separately with another user's account. See
[the dated channel findings](prompts/13-agent365.md). The optional
[Teams relay](../scripts/README.md#running-from-your-own-account-or-a-fresh-clone) uses explicit team
and channel names and the relay runner's Foundry identity.

### Why this deployment crosses three control planes

| Plane | What lives there | Tool |
| --- | --- | --- |
| **ARM** | Foundry account, project, model, registry, Container Apps, RBAC, observability, **connections** | Bicep — [`infra/`](../infra/) |
| **Microsoft Graph** | app registrations, federated credentials, app roles, consent, licences | Scripts. [Microsoft Graph Bicep](https://learn.microsoft.com/graph/templates/overview-bicep-templates-for-graph) exists and compiles; the bootstrap stays a script because it creates the identity CI authenticates *as*, and configures GitHub, which no Azure IaC reaches |
| **Foundry data plane** | agent versions, endpoint protocols, agent cards, **toolboxes** | Workflows and scripts. Deliberately not IaC for versions: an image digest changes every release, and infrastructure should not own per-commit state |

An ARM path for agent applications does exist — `PUT .../projects/{project}/applications/{name}` is
accepted at `2026-05-15-preview` and fails on schema rather than method. This repo uses the data plane
for the reason in the third row, not because the other path is absent.

## Tearing down

Two independent scopes, deliberately.

**Identity and repository configuration:**

```powershell
./scripts/Remove-GitHubOidc.ps1 -WhatIf   # preview
./scripts/Remove-GitHubOidc.ps1
```

**Azure resources**, one resource group per environment:

```bash
az group delete --name rg-caesarea-dev --yes
az group delete --name rg-caesarea-prod --yes
```

A deleted Foundry account is recoverable for a period, which blocks reusing the same name. To free
it immediately:

```bash
az cognitiveservices account purge --name <account> --resource-group <rg> --location westus3
```

`Remove-GitHubOidc.ps1` is deliberately narrower than the bootstrap. It removes only the six
variables it set and the two role assignments it created, and it leaves the GitHub environments
themselves alone unless you pass `-RemoveEnvironments`. It also refuses to act on an ambiguous
application name — Entra permits duplicates, so a display-name lookup returning several results
stops the script rather than guessing which to delete. Pass `-ApplicationId` to disambiguate.

Resource providers are not unregistered: they are subscription-wide and other things may depend on
them, so teardown is not literally the inverse of everything bootstrap touches.

Both scripts are safe to re-run. Anything already gone reports `[absent]`, and a read that fails for
any other reason — a 403, a network error — stops the script rather than being taken for absence.

---

## Rebuilding from nothing

Use your own repository, subscription and tenant, with the prerequisites above:

1. Fork/copy the repository, point `origin` at your copy, enable Actions and push `main`.
2. Sign in to Azure/GitHub and run `Bootstrap-GitHubOidc.ps1` with an available model version.
3. Run `deploy-infra`: `dev` / `preview`, then `dev` / `apply`; wait for completion.
4. Run `Sync-PlatformVariables.ps1`, then `Bootstrap-EnergyHubApi.ps1` for `dev`.
5. Run `deploy-services`, wait for success, then sync its Energy Hub URL from the summary.
6. Run `deploy-hosted-agent` to create the identity, grant Energy Hub and Foundry access, and rerun
   until the smoke test passes.
7. Assign the tenant's Agent 365 licence, connect and test Work IQ, set `WORKIQ_TOOLBOX`, and redeploy.
8. As each demo user, create the OneDrive work order, configure the local model endpoints, run
   `Start-CaesareaDemo.ps1`, and complete that user's Work IQ consent.
9. Assign the agent owner, run the Agent 365 readiness gate, deploy the Teams bridge, and complete
   store publishing for your intended audience. Verify with another user; observe the Work IQ
   channel limitation above.

Your tenant's roles, licences, billing, consent and publishing policies are prerequisites; none
are inherited from the repository author's account.

---

## OIDC subject formats — the one that will bite you

GitHub has two subject formats for the federated credential:

| Form | Looks like |
| --- | --- |
| Legacy | `repo:<owner>/<name>:environment:<env>` |
| Immutable | `repo:<owner>@<ownerId>/<name>@<repoId>:environment:<env>` |

**Do not decide which you have from `use_immutable_subject`.** This repository reports that field as
`false` and nonetheless presents the immutable form, because the field that actually goes into the
token is `sub_claim_prefix`:

```bash
gh api repos/<owner>/<name>/actions/oidc/customization/sub
```

```json
{ "use_default": true,
  "use_immutable_subject": false,
  "sub_claim_prefix": "repo:<owner>@<owner-id>/<repo>@<repo-id>" }
```

The bootstrap reads `sub_claim_prefix` and falls back to the legacy shape only on a 404, so you do
not have to think about this. It matters when something goes wrong, because the failure is
misleading:

```text
##[error]AADSTS700213: No matching federated identity record found for presented assertion
subject 'repo:<owner>@<owner-id>/<repo>@<repo-id>:environment:dev'.
```

That reads as a *missing* credential. It is a credential with the *wrong subject* — which looks
entirely correct in the Entra portal. Re-running the bootstrap fixes it: a credential of its own
whose subject no longer matches is replaced rather than left beside a new one.

## Break glass: deploying without GitHub

If GitHub is unavailable, the same templates deploy directly. This is a recovery path, not the
normal one — it produces no approval record and no run history:

```bash
az deployment sub create \
  --name caesarea-infra --location westus3 \
  --template-file infra/main.bicep \
  --parameters environmentName=dev location=westus3 \
               deploymentPrincipalId=$(az ad signed-in-user show --query id -o tsv) \
               deploymentPrincipalType=User \
               modelVersion=2026-04-24
```

---

## What has and has not been proven

Honesty about this matters more than confidence, because the failure mode is a demo that works on
one laptop.

**Verified, by running it:**

- The bootstrap has been run against a real tenant and repository. It created two applications with
  one federated credential each, assigned the roles, and configured both environments. A second run
  reports `[exists]` on every line.
- `deploy-infra` has applied the platform to `dev`, and `deploy-services` and `deploy-hosted-agent`
  have released onto it: the Energy Hub rejects anonymous callers with 401 at the ingress, and the
  hosted agent answers with content only the deployed Energy Hub could supply (the release smoke
  test requires L-417's area, a string that exists only in the twin).
- Outbound egress from the hosted sandbox is open, the per-request user identity reaches the
  container, and Work IQ performs the delegated read as the calling user — consent flow included.
  All recorded, with dates and the evidence, in [hosted-agent.md](product-status/hosted-agent.md).
- The Work IQ wiring converges: a re-run of `Connect-WorkIQ.ps1` against the live project reports
  `[exists]` on every step, including the toolbox default-version comparison, and
  `Test-FoundryToolbox.ps1` passes as a strict gate against the same project.

**Not verified:**

- Whether every role assignment is as narrow as intended. Deployment proves the grants suffice, not
  that none is broader than needed.
- .NET cold-start time on the hosting platform under a cold session, which decides how comfortable
  the on-stage first ask is. Rehearse it.
- The `azd` deployment path end to end (see the status doc's blocker note).

Update this section when any of that changes.
