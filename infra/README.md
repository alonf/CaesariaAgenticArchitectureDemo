# Caesarea agent platform — infrastructure

Two layers, two workflows, two sets of approvers. The split is the point.

| | Platform | Application |
| --- | --- | --- |
| What | Foundry account, project, model deployment, registry, observability, RBAC | The hosted agent version: image digest, CPU, environment |
| How | `infra/main.bicep` via `az deployment sub create` | data-plane `POST /agents/{name}/versions` |
| Workflow | [`deploy-infra.yml`](../.github/workflows/deploy-infra.yml) | [`deploy-hosted-agent.yml`](../.github/workflows/deploy-hosted-agent.yml) |
| Changes | rarely, and changes the shape of the estate | every release |
| Owner | platform team | application team |

## Why the agent version is not in the Bicep

Not an omission, and not a shortcut. The ARM resource
`Microsoft.CognitiveServices/accounts/projects/applications/agentDeployments` exists — but read its
schema. It has `agents[]` (name + version references), `protocols[]`, `deploymentType` and `state`.
It has **no container image, no CPU, no memory and no environment variables**.

It cannot create a hosted agent. It *routes to* agent versions that already exist, and those are
created through the project's data plane.

That boundary is correct. An image digest is an application artifact, exactly like a container tag
in any other deployment — you would not put it in the template that owns your network. The seam ARM
draws here is the seam an enterprise release process already has.

## Why some RBAC is in the pipeline and not here

[`modules/rbac.bicep`](modules/rbac.bicep) binds every principal that exists at provisioning time:
the Foundry account identity that pulls the image, and the CI identity that pushes it and creates
versions.

The **agent's own Microsoft Entra identity is not one of them**, because the platform creates it
when the first agent version is created. There is no principal ID to bind until that call returns.
So anything the agent must reach beyond models and its own session storage — your storage account,
your database — is granted in `deploy-hosted-agent.yml`, after the version is active and the
principal can be read back.

If you take one thing from this directory, take that ordering. It is the detail that turns a
working demo into something that survives a security review.

## What is deliberately strict

- **`disableLocalAuth: true`** on the Foundry account. No API keys. Every caller authenticates as
  itself, so every call carries an identity into the audit log instead of a shared secret.
- **`adminUserEnabled: false`** on the registry. Same argument.
- **`versionUpgradeOption: 'NoAutoUpgrade'`** and a pinned `modelVersion`. A floating model version
  changes agent behaviour with no code change and no deployment — the least debuggable outage there
  is.
- **Deploy by digest, not tag.** A tag can be moved after it is approved. A digest cannot.
- **Workload identity federation.** No `AZURE_CREDENTIALS`, no client secret. The runner proves who
  it is with a short-lived OIDC token.
- **`Foundry User` for workloads, `Foundry Project Manager` only for CI.** A running service that
  can redefine the agent it is running is a service that can rewrite its own instructions.

## What is missing, and named rather than hidden

- **Private networking.** `publicNetworkAccess: false` is wired through every module, but the
  private endpoints, DNS zones and delegated subnet are not written. Hosted-agent VNet injection
  must be configured when the Foundry account is **first created** — it cannot be added afterwards.
  Decide before you provision.
- **Key Vault.** Not needed while nothing holds a secret. Add it when something does.
- **A second environment.** The parameters file is written for one; promotion between environments
  is a matter of a second parameter file and a second GitHub environment, not a template change.

## Running it

```bash
az deployment sub create \
  --name caesarea-infra \
  --location westus3 \
  --template-file infra/main.bicep \
  --parameters environmentName=dev location=westus3 \
               deploymentPrincipalId=<CI service principal object ID> \
               modelVersion=<pinned version>
```

Role definition IDs in `modules/rbac.bicep` are the built-in GUIDs. Several of these roles were
renamed in 2026 — *Azure AI User* became *Foundry User* — while keeping their IDs, so resolve them
yourself rather than trusting a copied constant:

```bash
az role definition list --name "Foundry User" --query "[0].name" -o tsv
```

## On azd

`azd` is the right inner-loop tool: `azd ai agent run` hosts the agent locally on port 8088,
`invoke` calls it, `doctor` diagnoses RBAC and DNS. Nothing here replaces that, and you should use
it while developing.

What it is not is a deployment architecture. `azd up` provisions and deploys in one gesture, keeps
environment state in `.azure/` on a developer's machine, and creates the account and registry
implicitly. Those are three different objections to the same thing: it assumes a developer with a
laptop, where this assumes a repository with environments and approvers.

The templates here are the ones `azd provision` would run. That is not a coincidence — it is the
whole reason the split costs so little.
