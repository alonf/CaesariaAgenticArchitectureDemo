# Caesarea agent platform — infrastructure

Two layers, two workflows, two sets of approvers. The split is the point.

| | Platform | Application |
| --- | --- | --- |
| What | Foundry account, project, model deployment, registry, observability, RBAC | The hosted agent version: image digest, CPU, environment |
| How | `infra/main.bicep` via `az deployment sub create` | data-plane `POST /agents/{name}/versions` |
| Workflow | [`deploy-infra.yml`](../.github/workflows/deploy-infra.yml) | [`deploy-hosted-agent.yml`](../.github/workflows/deploy-hosted-agent.yml) — **parked** until `Services/OperationsAgent.Hosted` exists |
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
- **Application Insights keeps local auth on**, unlike the Foundry account. The project's
  `AppInsights` connection authenticates with that component's key, so disabling it would leave the
  connection valid-looking and the traces missing - a failure with no error to find. The connection
  string is handled as the credential it therefore is, and is not a deployment output.
- **Workload identity federation.** No `AZURE_CREDENTIALS`, no client secret. The runner proves who
  it is with a short-lived OIDC token.
- **`Foundry User` for workloads, `Foundry Project Manager` only for CI.** A running service that
  can redefine the agent it is running is a service that can rewrite its own instructions.

## Cost, because it is a design input and not an afterthought

Hosted agents bill on **CPU and memory consumed across active sessions**. Three consequences worth
knowing before you size anything:

- **`cpu` and `memory` describe one session, not the agent.** A sandbox is created per session, so
  oversizing multiplies your bill by concurrency rather than adding to it. The available pairs are
  0.5 vCPU/1 GiB, 1/2 GiB and 2/4 GiB - there is no smaller step to retreat to.
- **Idle compute is deprovisioned, and that is the scale-to-zero story.** The timeout is
  configurable from 5 to 60 minutes and defaults to 15. Session state (`$HOME` and `/files`)
  survives and is restored on resume, so a short timeout costs a cold start rather than the work.
  Sessions are deleted outright after 30 days idle.
- **Right-size from evidence, not from a guess.** App Insights is wired up by the platform; look at
  Performance for CPU, available memory and request duration under a representative load. Sustained
  peaks above roughly 70% of allocation mean raise it on the next version; well below means lower
  it. Versions are immutable, so every change is a new one and you can compare them honestly.

## What is missing, and named rather than hidden

- **Workload identities.** `workloadPrincipalIds` is plumbed end to end but empty: the services run
  on the presenter's machine, as the presenter. Populating it is what the Governance stage is for.
- **Private networking.** `publicNetworkAccess: false` is wired through every module, but the
  private endpoints, DNS zones and delegated subnet are not written. Hosted-agent VNet injection
  must be configured when the Foundry account is **first created** — it cannot be added afterwards.
  Decide before you provision.
- **Key Vault.** Not needed while nothing holds a secret. Add it when something does.
- **A second environment.** The parameters file is written for one; promotion between environments
  is a matter of a second parameter file and a second GitHub environment, not a template change.

## Validation status

The templates compile **and pass `az deployment sub what-if`** against a real subscription. That
second gate matters more than it sounds: two service-level errors survived a clean compile and were
only caught by preflight - a missing `allowProjectManagement` on the account, without which project
creation is rejected, and a missing `capabilityHosts` resource, without which there is no hosted
agent runtime at all.

Nothing here has been **deployed**. what-if validates the template against the resource providers;
it does not prove a role assignment grants what you meant, or that a policy applies on the SKU you
chose. Treat anything below that line as reviewed, not proven:

```bash
az deployment sub what-if   --name caesarea-whatif --location westus3   --template-file infra/main.bicep   --parameters environmentName=dev location=westus3                deploymentPrincipalId=<principal object ID>                modelVersion=<a version from `az cognitiveservices model list`>
```

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
