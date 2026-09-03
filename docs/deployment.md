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

Expected output on a clean tenant:

```text
=== Checking prerequisites
  az and gh are present.
  Subscription: <your subscription> (<id>)
  Repository: <owner>/<repo>
  Model version 2026-04-24 is available in westus3.

=== Registering resource providers
  [exists] Microsoft.CognitiveServices
  ... one line per provider, [exists] or a registration ...

=== Identity for 'dev' (caesarea-github-deploy-dev)
What if: Performing the operation "Create Entra application" on target "caesarea-github-deploy-dev".
  Would create the application. The steps below depend on its ID and cannot be previewed.

=== Identity for 'prod' (caesarea-github-deploy-prod)
What if: Performing the operation "Create Entra application" on target "caesarea-github-deploy-prod".
  Would create the application. The steps below depend on its ID and cannot be previewed.
```

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

**It is idempotent.** Run it again and every line reports `[exists]`; nothing is created twice. That
is the check that it worked, and the check that a later run has not drifted.

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

Run `preview` again after applying. It should report:

```text
**No changes.** The deployed platform already matches this template.
```

That is the proof, not the promise. If it reports changes after an unchanged apply, something in the
template is non-deterministic and worth finding before you trust the pipeline.

### Feeding the outputs forward

Copy these from the apply summary into the environment's variables, for the application workflow:

- `AZURE_CONTAINER_REGISTRY_ENDPOINT`
- `AZURE_CONTAINER_REGISTRY_NAME`
- `FOUNDRY_PROJECT_ENDPOINT`
- `MODEL_DEPLOYMENT_NAME`

---

## Step 4 — Deploy the agent

**Not yet possible.** `Services/OperationsAgent.Hosted` does not exist — it is Stage 12's code, and
[`deploy-hosted-agent.yml`](../.github/workflows/deploy-hosted-agent.yml) is parked behind `if:
false` until it does. A committed pipeline that fails on its first step is worse than one that is
visibly disabled.

When the project exists, the workflow: builds the image in ACR for `linux/amd64`, resolves its
digest, creates an immutable agent version, waits for `active`, reads back the agent's own Entra
identity and binds any downstream access it needs, then smoke-tests the deployed endpoint.

---

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

## A note on OIDC subject formats

The federated credentials use GitHub's legacy subject form,
`repo:<owner>/<name>:environment:<environment>`. GitHub also supports immutable subjects keyed on
the repository *ID*, which survive a rename. This repository currently reports
`use_immutable_subject: false`, so the legacy form is correct for it.

If you enable immutable subjects, or GitHub defaults new repositories to them, the subjects the
bootstrap writes will no longer match and `azure/login` will fail with an audience or subject error.
Check with:

```bash
gh api repos/<owner>/<name>/actions/oidc/customization/sub
```

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

**Verified:**

- Both scripts parse, run, and detect real tenant and repository state under `-WhatIf` without
  changing anything. On a clean tenant that preview stops at the first identity dependency, as
  described above — it is not a full plan.
- `infra/main.bicep` passes `az deployment sub what-if` against a real subscription: 12 creates, no
  errors.
- The hosted-agent protocol serves `/responses` and `/readiness` on a laptop with no Azure at all
  (see [hosted-agent.md](product-status/hosted-agent.md)).

**Not verified — nothing here has actually been deployed:**

- Whether the role assignments grant what was intended. `what-if` proves a template is valid, not
  that its RBAC is correct.
- Outbound egress from a deployed hosted-agent sandbox.
- .NET cold-start time on the hosting platform.

Treat everything past `what-if` as reviewed, not proven, until the first real deployment says
otherwise — and update this section when it does.
