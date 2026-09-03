# Foundry Hosted Agent — verified platform status

Required by requirements §22. This file records what was **verified by running it**, and what is
still unverified, so the Hosting stage is designed against facts rather than against the deck.

Spike date: 2026-09-03. Verified on the pinned `1.19.0-preview` MAF family.

## Verified by compiling

`Microsoft.Agents.AI.Foundry.Hosting` publishes **`1.19.0-preview.260822.1`** — the same version as
the rest of the solution. **The hosting path needs no family upgrade.**

Its transitive dependencies at that version:

| Package | Version |
| --- | --- |
| `Azure.AI.AgentServer.Core` | 1.0.0-beta.28 |
| `Azure.AI.AgentServer.Responses` | 1.0.0-beta.8 |
| `Azure.AI.Projects` | 2.1.0-beta.4 |
| `Microsoft.Agents.AI.Foundry` | 1.19.0-preview.260822.1 |
| `ModelContextProtocol` | 2.1.0 |

The deck's slide-43 snippet **compiles verbatim, 0 errors and 0 warnings**. The APIs are split
across two packages, which the slide does not say:

| API | Assembly |
| --- | --- |
| `AgentHost.CreateBuilder(args)` | `Azure.AI.AgentServer.Core` |
| `AgentHostBuilder.RegisterProtocol(string, Action<IEndpointRouteBuilder>)` | `Azure.AI.AgentServer.Core` |
| `IServiceCollection.AddFoundryResponses(...)` | `Microsoft.Agents.AI.Foundry.Hosting` |
| `IEndpointRouteBuilder.MapFoundryResponses(string)` | `Microsoft.Agents.AI.Foundry.Hosting` |

Also present and relevant later: `AddFoundryToolboxes(TokenCredential, params string[])` — the .NET
entry point for Foundry-managed tools; `InMemoryAgentSessionStore` / `FileSystemAgentSessionStore` /
`FoundryAgentSessionStore`; `FoundryJsonCheckpointStore` and `ApplyWorkflowCheckpointing`;
`ConsentAwareMcpClientAIFunction` and the MCP consent types.

One API-shape note: `ChatClientAgentOptions` has no `Instructions` property. Instructions live on
`ChatClientAgentOptions.ChatOptions.Instructions`, as the rest of this solution already does it.

## Verified by running it locally, with no Azure at all

A minimal host built from the snippet above, over a scripted `IChatClient`:

- listens on **port 8088**
- serves **`GET /readiness` → 200**, mapped by the protocol library without being asked
- serves **`POST /responses`** non-streaming, returning a well-formed Responses payload:
  `object: "response"`, `status: "completed"`, `output[].content[].output_text`, and an
  `agent_session_id`
- serves **`POST /responses`** with `stream: true` as SSE: `response.created`,
  `response.in_progress`, … with `sequence_number` maintained by the library

**This matters for the lecture.** The hosted-agent *protocol* is demonstrable on a laptop with no
cloud, no credential and no deployment. The local/hosted contrast does not depend on the network in
the room for its first half.

## Verified stale — the deck's Bicep

Slide 43 shows `minReplicas: 0` / `maxReplicas: 5`. **There is no replica model.** Hosted agents
scale per *session*, in VM-isolated sandboxes:

- no replica count, no warm pool — the docs say so explicitly
- sandbox sizes are **0.5 vCPU/1 GiB, 1/2 GiB, 2/4 GiB**, and describe *one session*, so oversizing
  multiplies cost by concurrency
- idle timeout is configurable **5–60 minutes, default 15**; compute is then deprovisioned and
  `$HOME` + `/files` persist, restored when the session resumes
- sessions are deleted after 30 days idle

That is a better scale-to-zero story than the slide's, and it needs different words.

Requirements §13.15 predicted this: the excerpt is "a lecture artifact requiring validation against
the current platform, not deployment truth." Confirmed.

## Platform facts gathered from current docs

| Topic | Fact |
| --- | --- |
| Languages | Python and **C#** |
| Protocols | Responses, Invocations, Invocations-WS, **A2A (preview)**, **Activity** (Teams/M365). One container may expose several |
| Image | **linux/amd64 only** |
| Docker required | **No** — `azd deploy` builds remotely in ACR; a zip source-code path also exists |
| Identity | The platform creates a **dedicated Entra agent identity per agent** at deploy time. §22's "no embedded Azure credential" is satisfied by the platform, not by our code |
| Tracing | App Insights connection string **auto-injected**; OTel traces on by default |
| Injected env | `FOUNDRY_PROJECT_ENDPOINT`, `FOUNDRY_PROJECT_ARM_ID`, `FOUNDRY_AGENT_NAME`, `FOUNDRY_AGENT_VERSION`, `FOUNDRY_AGENT_SESSION_ID`, `APPLICATIONINSIGHTS_CONNECTION_STRING` |
| Secrets | `${{connections.<name>.credentials.<field>}}`, resolved from project connections at sandbox start |
| Versions | Immutable; one version serves 100% of traffic; no traffic splitting |
| Role to deploy | **Foundry Project Manager** at project scope |
| Tools | Via a project-level **Toolbox MCP endpoint**, not on the agent definition |

## Corrected: the capability host needs no BYO datastores

An earlier draft of this file warned that the Agents capability host might require Bring-Your-Own
Storage, Cosmos DB and AI Search connections, and that this was the most likely first-deploy
failure. **That was wrong**, and the error is worth recording because it is easy to repeat.

The BYO requirement is real but belongs to the **network-secured standard setup** - VNet injection,
no public egress. It does not apply to a public hosted-agent environment. The two live on the same
documentation page, and the distinction was collapsed.

`Azure-Samples/azd-ai-starter-basic`, the infrastructure `azd ai agent init` scaffolds for hosted
agents, settles it. There is no Cosmos DB in it anywhere. Storage and AI Search appear only as
optional tool connections, gated on an opt-in list, for file search and vector grounding. Its
capability host declares no connections at all:

```bicep
resource aiFoundryAccountCapabilityHost 'capabilityHosts@2025-10-01-preview' = if (enableHostedAgents && enableCapabilityHost) {
  name: 'agents'
  properties: {
    capabilityHostKind: 'Agents'
    // IMPORTANT: this is required to enable hosted agents deployment
    // if no BYO Net is provided
    enablePublicHostingEnvironment: true
  }
}
```

The same file independently confirms three things this repo had already concluded:
`allowProjectManagement: true` on the account; the image pull granted to the **project** identity
rather than the account identity; and `53ca6127-db72-4b80-b1b0-d745d6d5456d` as Foundry User.

It also supplied the shape for the Application Insights project connection, which is now in
`infra/modules/foundry.bicep` - and a trap with it. That connection authenticates with the
component's key, so `DisableLocalAuth: true` on Application Insights leaves the connection looking
valid and produces no traces at all. This repo had set exactly that. It is removed, and the
connection string is now treated as the credential it is: a `@secure()` parameter, and no longer a
deployment output.

## Not yet verified

- **Outbound egress from a deployed sandbox.** The docs state that Standard Setup *with private
  networking* has "no public egress", and that default templates create public resources; in BYO-VNet
  mode the Micro VM has "a dedicated network interface and uses its own IP for outbound
  communication". Nowhere is egress from the **default, non-isolated** sandbox stated directly. It
  is very likely open, but it is an inference, and it is the fact underneath any decision about
  whether a hosted agent could reach a tunnel. **Settle it with one outbound request from inside a
  deployed container before relying on it.**
- **`azd` deployment end to end.** Not attempted yet. Blocker found: the `azure.ai.agents` azd
  extension reports **Incompatible** (installed 1.0.0-beta.11, latest 1.0.0-beta.13) against the
  installed `azd` 1.31.2, which itself has 1.33.0 available. Upgrading both is a prerequisite.
- **Cold-start time** for a .NET image, which determines whether the on-stage invoke is comfortable
  or awkward.

## Related work: microsoft/AIAgentsforITOps

Checked 2026-09-03, against the git tree API rather than rendered pages - a first pass read from
GitHub's tree view reported an empty `src/path2` and an "under development" banner, and was wrong.
Path 2 landed 2026-06-23 and had commits the day this was written.

The workshop's stated scope is *"infrastructure management, not agent development"* - the mirror
image of H08. Its six labs are Deploy Infrastructure, Managed Identity, Networking, Secrets
Management, Monitoring, Cost Management. Useful as a place to send deep ops questions.

What it is **not** is a hosted-agent reference. Path 2 deploys a **prompt agent**: the AKS container
is a chat UI whose project file references `Azure.Identity` and nothing else, and the agent itself is
declarative configuration in Foundry. Its own source comments say so. No container image, no agent
SDK, no Agent Framework - and .NET 8 throughout.

The consequence worth recording: its identity lab enumerates the AKS control-plane and kubelet
identities, the Search identity, the Foundry account and project identities and the signed-in user.
**Every one is a service-to-service identity.** There is no agent principal, because prompt agents do
not get one. The per-agent Entra identity is specific to hosted agents, and it is absent from
Microsoft's own IT/Ops identity material.

Two cross-checks from their working code, which agree with this repo's pipeline:

- endpoint `POST {projectEndpoint}/agents/{name}/endpoint/protocols/openai/responses`
- token scope `https://ai.azure.com/.default`

One difference to confirm against a live call: they grant the calling workload `Cognitive Services
User`; `infra/modules/rbac.bicep` grants the narrower `Foundry User`. Both are plausible; only one
has been exercised.

Also worth borrowing: `previous_response_id` is how the Responses protocol chains multi-turn context
without the caller holding history. Relevant if the hosted composition needs a Session-stage
equivalent.

## Cost model

Billing is CPU and memory consumed across **active sessions**, not per agent and not per request.

- `cpu` and `memory` describe **one session**. Sandboxes are per session, so oversizing multiplies
  cost by concurrency. Available pairs: 0.5 vCPU/1 GiB, 1/2 GiB, 2/4 GiB.
- Idle compute is deprovisioned after the configured timeout (5-60 minutes, default 15) and session
  state is restored on resume, so a short timeout costs a cold start rather than the work.
- Right-size from App Insights Performance under representative load: sustained peaks above ~70% of
  allocation mean raise it next version, well below means lower it. Versions are immutable, so the
  comparison is honest.

## Stage design decision on record

The Hosting stage will host **exactly one** agent — the Operations Agent. Slide 43's claim is that
the agent code does not determine where it must run, and moving one agent proves it; a second
multiplies the ACR/version/identity surface while proving the same thing.

It will run a **reduced hosted composition**: same code, instructions and skills, with a toolset
that does not require the city hubs on the presenter's laptop. This is a deliberate, recorded
deviation from §22's presenter step 6 ("compare same scenario behavior"), taken because the
alternative is an inbound tunnel from Azure to the presenter's machine over conference wi-fi. The
substitute beat is stronger material anyway: *the hosting decision is about where the agent has to
live to reach what it needs.* A dev-tunnel variant stays documented as an optional full-fidelity
path, not the stage path.
