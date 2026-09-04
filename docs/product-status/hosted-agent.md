# Foundry Hosted Agent — verified platform status

Required by requirements §22. This file records what was **verified by running it**, and what is
still unverified, so the Hosting stage is designed against facts rather than against the deck.

Spike date: 2026-09-03; deployed and verified end to end 2026-09-04.

The hosted project runs on **`Microsoft.Agents.AI.Foundry.Hosting 1.20.0-preview.260831.1`**, ahead of
the `1.19.0-preview` family the rest of the solution pins. That is not drift for its own sake: 1.19 is
unusable in the hosted runtime (see the port collision below).

## Verified by compiling

`Microsoft.Agents.AI.Foundry.Hosting` publishes **`1.19.0-preview.260822.1`**, matching the rest of
the solution — so the *compile* needs no family upgrade. The **runtime does**: 1.19 cannot start in a
hosted sandbox, and `OperationsAgent.Hosted` therefore references 1.20.

Its transitive dependencies at 1.19:

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

- listens on **port 8088** — `FoundryEnvironment.Port`, sourced from `PORT`, which the hosted
  platform injects and **reserves**: the version API rejects `PORT` (and anything named `FOUNDRY_*`
  or `AGENT_*`) in `environment_variables` with "reserved for platform use". Take the port it gives
  you; there is no supported way to change it, and `AgentHostOptions` exposes no port despite what
  its `Configure` summary says.
- serves **`GET /readiness` → 200**, mapped by the protocol library without being asked
- serves **`POST /responses`** non-streaming, returning a well-formed Responses payload:
  `object: "response"`, `status: "completed"`, `output[].content[].output_text`, and an
  `agent_session_id`
- serves **`POST /responses`** with `stream: true` as SSE: `response.created`,
  `response.in_progress`, … with `sequence_number` maintained by the library

### The 1.19 port collision, and how to reproduce the sandbox on a laptop

On `Microsoft.Agents.AI.Foundry.Hosting` **1.19.0-preview.260822.1**, a hosted container binds `PORT`
twice and dies at startup:

```text
Failed to bind to address http://[::]:8088: address already in use.
```

In an empty container, with nothing else running — the process competes with itself. The session then
fails with `session_not_ready` and a message recommending you check that `/readiness` returns 200,
which points at the one component that was working. **Fixed in 1.20.0-preview.260831.1**; the upgrade
is the whole fix, and no change of port, composition or infrastructure substitutes for it.

The reason this cost so much to find: it does not happen outside the hosted runtime. Locally the two
listeners land on different ports and both come up, so every local test passes. The switch is
`FoundryEnvironment.IsHosted`, which is true whenever **`FOUNDRY_HOSTING_ENVIRONMENT`** is set to a
non-empty value — so the sandbox reproduces on a laptop in seconds:

```bash
docker run -e FOUNDRY_HOSTING_ENVIRONMENT=Production -e PORT=8088 \
  -e FOUNDRY_PROJECT_ENDPOINT=... -e ENERGYHUB_BASE_URI=... <image>
```

Allow a minute before concluding anything: `TaskManager` retries its storage calls before Kestrel
binds, so a container that looks hung at fifteen seconds is usually just waiting. Judging it too early
produced two confident, wrong diagnoses here.

**The protocol version must match the image.** A container built on 1.20 serves Responses `2.0.0`;
declare `1.0.0` in the version definition and the agent goes `active` and then answers every call with
`unsupported_container_protocol_version`.

**This matters for the lecture.** The hosted-agent *protocol* is demonstrable on a laptop with no
cloud, no credential and no deployment. The local/hosted contrast does not depend on the network in
the room for its first half.

## Verified in a deployed sandbox

**Outbound public egress: open.** This was the open question underneath every decision about what a
hosted agent can reach, and the docs never state it for the default, non-isolated sandbox. Settled by
asking the deployed agent for a streetlight's state with `ENERGYHUB_BASE_URI` pointed at a placeholder
host, and reading the trace it produced:

```text
Loaded skill: streetlight-investigation
Operations Agent invoked tool get_streetlight_state for asset SL-1042. CorrelationId: hosted.
Sending HTTP request GET https://example.com/api/energy/assets/SL-1042
Energy Hub state read for asset SL-1042 failed with status 404. Detail: <!doctype html>...Example Domain...
```

A 404 carrying the origin's own HTML is proof the request left the sandbox and was answered by the
public internet. A blocked sandbox fails earlier and differently, with no status code at all - which
the gateway distinguishes, logging `StateRequestHttpError` when `HttpRequestException.StatusCode` is
null. So an Energy Hub behind public HTTPS ingress is reachable from a hosted agent.

The same trace settles two other things at once: **skills work in the hosted habitat** (the agent
pulled `streetlight-investigation` through progressive disclosure, unprompted), and **the tool path
is intact end to end**, from model to `AIFunctionFactory` tool to outbound HTTP.

### Open: AgentSkillsProvider breaks the Responses reply in the hosted runtime

A hosted response that involves `load_skill` fails. The skill loads - the container logs
`Loaded skill: streetlight-investigation` - and the reply then comes back as:

```text
status: failed
HTTP 400 (ServiceError: invalid_payload)
The provided data does not match the expected schema
```

The error names no parameter, and reproduces on every attempt.

Isolated by asking the deployed agent four things:

| Probe | Composition | Result |
| --- | --- | --- |
| A | `get_streetlight_state` only | **completed** |
| B | `search_work_knowledge` only | **completed** |
| C | `load_skill` + `get_streetlight_state` | **failed** |
| D | `get_streetlight_state` + `search_work_knowledge`, no skill | **completed** |

So tools work, two `AIContextProvider`s work, the authenticated Energy Hub path works, and the model
works. `AgentSkillsProvider` is the one component that turns a good response into an invalid payload,
and D rules out the text-search provider added alongside it.

This is not new: the very first hosted probe in this environment also logged `Loaded skill` and
returned `status: failed`, which was attributed at the time to the placeholder Energy Hub returning
404. It was this.

**What it costs.** Progressive disclosure is demonstrable in the hosted runtime only as far as the
log line: the skill is discovered and loaded, and the answer built on it cannot be returned. The
Aspire-hosted agent is unaffected, so the Skills stage is intact - it is the hosted habitat that
cannot currently complete a skill-driven investigation.

Unresolved. Worth re-testing on the next `Microsoft.Agents.AI.Foundry.Hosting` preview before
investing in a workaround, given that 1.19 to 1.20 fixed a comparable hosted-only defect.

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
