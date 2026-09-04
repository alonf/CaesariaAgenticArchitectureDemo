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

Only the first is a script, and only because it cannot be anything else: something has to create the
identity the workflows authenticate as, before any workflow can run. Everything after it is CI.

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
writes `AZURE_RESOURCE_GROUP`, `AZURE_CONTAINER_REGISTRY_ENDPOINT`, `AZURE_CONTAINER_REGISTRY_NAME`,
`FOUNDRY_PROJECT_ENDPOINT` and `MODEL_DEPLOYMENT_NAME`. Every value is compared before it is written,
so a second run against an unchanged deployment reports `[exists]` for each and changes nothing.

`ENERGYHUB_BASE_URI` is not among them. The Energy Hub is an application deployment rather than part
of the platform, so its address is passed in rather than read:

```powershell
./scripts/Sync-PlatformVariables.ps1 -Environment dev -EnergyHubBaseUri https://energyhub.<...>.azurecontainerapps.io
```

Until it is supplied the script says so and leaves the variable alone, because an empty value would
let the release workflow succeed and the agent fail on its first tool call.

> **`dev` currently holds a placeholder.** `ENERGYHUB_BASE_URI` is set to `https://example.com` — it
> was used to prove that a hosted agent can make outbound calls at all, before the Energy Hub existed
> in Azure. It looks like a real value in the GitHub UI and it is not. Until the Energy Hub is
> deployed and the variable re-synced, the agent will answer questions about a streetlight
> confidently, load its investigation skill, call its tool, receive a 404 and have nothing to report.
> That is the worst failure to discover in front of an audience, which is why it is written down here
> rather than left to be remembered.

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
`Services/OperationsAgent.Hosted` into an image **in ACR** rather than on the runner — so the runner
needs no Docker and the image never transits a machine outside the boundary — resolves the image
digest, creates an immutable agent version **by digest rather than by tag**, waits for it to become
`active`, reads back the agent's own Entra identity and binds any downstream access it needs, then
smoke-tests the deployed endpoint.

The identity step is last for a reason: the platform mints a dedicated Entra identity for the agent
when its first version is created, so that principal does not exist at provisioning time and cannot
be bound in Bicep. This is the one piece of access control that has to happen after deployment.

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

Seven steps, all API calls, no portal step:

1. Provision the Work IQ service principal (`fdcc1f02-fc51-4226-8753-f668596af7f7`). Skip it and the
   permission is not findable.
2. Register a single-tenant confidential client app — the app an admin authorises.
3. Add delegated `WorkIQAgent.Ask` and grant tenant-wide consent.
4. Mint a client secret, handed straight to the connection and never printed.
5. Create the project connection — `RemoteA2A`, because **Work IQ is itself an A2A agent**.
6. Read the OAuth redirect URL the connection returns and register it on the app. The ordering is
   forced: the URL does not exist until the connection does.
7. Create a toolbox version carrying `work_iq_preview` — what a hosted agent reaches through
   `AddFoundryToolboxes`.

Steps 1 and 3 need **Global Administrator**; activate it just-in-time through PIM and deactivate
after. Everything else needs only the Foundry roles from Step 1 of this document.

**Two prerequisites this script cannot give you**, both of which fail at runtime rather than at setup:

| Symptom | Cause |
| --- | --- |
| `403 Forbidden` | Work IQ API calls need usage-based billing with Copilot Credits |
| `Principal does not have access to API/Operation` | The agent's runtime identity needs **Foundry User** on the project |

A Foundry connection's fields **cannot be edited after creation**. The script reports an existing one
and leaves it alone rather than pretending to update it; to change it, delete and re-run.

### A correction worth keeping

An earlier version of this document said a toolbox was "the one manual step" that could not be
scripted. That was wrong, and the way it was wrong is instructive: the probe tried `POST /toolboxes`
and `PUT /toolboxes/{name}`, got 405 from both, and generalised. It never tried
`POST /toolboxes/{name}/versions`, which is where creation actually lives. Two probes of a plausible
shape are not a survey of an API.

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

The point of all this. Given only a clone of this repository and an Azure subscription:

1. `gh repo create <owner>/<name> --private --source . --push`
2. `./scripts/Bootstrap-GitHubOidc.ps1 -ModelVersion <version>`
3. Actions → deploy-infra → `dev` / `preview`, then `dev` / `apply`
4. Copy the outputs into the environment variables
5. Actions → deploy-hosted-agent *(once Stage 12 exists)*

No step depends on state that only exists on one machine.

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
  "sub_claim_prefix": "repo:alonf@554150/CaesariaAgenticArchitectureDemo@1352569832" }
```

The bootstrap reads `sub_claim_prefix` and falls back to the legacy shape only on a 404, so you do
not have to think about this. It matters when something goes wrong, because the failure is
misleading:

```text
##[error]AADSTS700213: No matching federated identity record found for presented assertion
subject 'repo:alonf@554150/CaesariaAgenticArchitectureDemo@1352569832:environment:dev'.
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
- `deploy-infra` has run on GitHub Actions, authenticated by workload identity federation as the
  dev identity, and produced a what-if of **12 creates, 2 unanalysable, no errors** — matching what
  this document says to expect.
- `infra/main.bicep` also passes `what-if` locally.
- The hosted-agent protocol serves `/responses` and `/readiness` on a laptop with no Azure at all
  (see [hosted-agent.md](product-status/hosted-agent.md)).

**Not verified — nothing here has actually been deployed:**

- Whether the role assignments grant what was intended. `what-if` proves a template is valid, not
  that its RBAC is correct.
- Outbound egress from a deployed hosted-agent sandbox.
- .NET cold-start time on the hosting platform.

Treat everything past `what-if` as reviewed, not proven, until the first real deployment says
otherwise — and update this section when it does.
