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

### Resolved: tool-calling turns fail with invalid_payload in the hosted runtime

A hosted turn in which the model calls a tool fails with:

```text
status: failed
HTTP 400 (ServiceError: invalid_payload)
The provided data does not match the expected schema
```

The error names no field, and for a while this file recorded it as intermittent - "the second
distinct tool fails about half the time". Both characterisations were wrong, and the path from each
wrong story to the true one is preserved below because it is the most instructive thing in this file.

**Root cause, proven on 2026-09-05 by reading the rejected request.** `ModelTrafficDumpPolicy`
(enabled with `DUMP_MODEL_TRAFFIC`, a directory path) captures every outbound model request body.
The rejected one shows the chain:

1. The hosted runtime drives the model with `store: false` and
   `include: ["reasoning.encrypted_content"]` - it keeps history in Foundry storage itself, so it
   does not chain responses server-side, and a reasoning model returns its chain-of-thought as an
   opaque `encrypted_content` blob.
2. When the model calls a tool, the function-invocation loop sends a follow-up request replaying the
   turn's items - user message, **reasoning item with the blob**, function_call, function_call_output.
3. Azure Foundry's `/openai/v1/responses` rejects `encrypted_content` on an *input* reasoning item
   with `invalid_payload`. The service refuses the very field the SDK asked it to emit.

Replaying the captured body verbatim with `curl` reproduces the 400 with no SDK in the loop. Deleting
the reasoning item from the same body returns 200 - this endpoint, unlike openai.com, does not
require a function_call to be preceded by its reasoning item. Patching `summary: []` into the item
instead does not help; `encrypted_content` itself is what the validator refuses.

**Why it masqueraded as intermittent.** The failure is deterministic *given a reasoning item
alongside a function call in an earlier leg of the same turn*. Whether the model emits one is the
model's choice per leg - so measured rates ("one tool 10/10, two tools ~5/10") were measuring
gpt-5.5's propensity to reason before its second tool call, not a runtime coin-flip. Single samples
produced a clean wrong story ("skills break it"); repeated samples produced a subtler wrong story
("it is random"). Only the request body told the truth.

**Fix.** `ReasoningReplaySanitizingChatClient` - a `DelegatingChatClient` that strips
`TextReasoningContent` from outgoing messages - wired through `AsAIAgent`'s `clientFactory` so it
sits *beneath* the function-invocation loop and sees the replayed legs. Applied in
`OperationsAgent.Hosted` and in `WorkforceAgentFactory` (whose two-tool procedure would otherwise
fail nearly every time hosted; under Aspire the session chains by `previous_response_id`, nothing is
replayed, and the filter is a no-op). Cost: the model re-reasons after each tool result instead of
resuming its chain-of-thought. Verified locally: a three-tool turn (load_skill +
get_streetlight_state twice) completes, with the dumps showing every leg free of reasoning items.

The local reproduction recipe, for when a future preview claims to fix it: run
`OperationsAgent.Hosted` with `DUMP_MODEL_TRAFFIC` set and POST to `/responses` with headers
`x-agent-foundry-call-id: <any>` and `x-agent-user-id: <caller oid>` - the first is how the platform
signals responses protocol v2, without it the container answers 501; the second satisfies the
per-user isolation key. Do not set `FOUNDRY_HOSTING_ENVIRONMENT` for this: that switches task
storage to the hosted store, which refuses to write without the platform-minted agent credential.

`SKILLS_MODE` remains in `OperationsAgent.Hosted` as a result of the first wrong diagnosis:
`provider` (the default, and the real `AgentSkillsProvider`) or `tool`, which advertises the same
skills and serves their bodies through an ordinary function. The `tool` path is kept because it is
independently useful - no files in the image, no `SKILLS_DIRECTORY` - not because skills were ever
implicated.

### A2A on a hosted agent: the platform fronts it, the container does not

Verified by enabling it on a deployed agent and reading what came back. This changes the design of
any hosted A2A work, so it is worth stating plainly: **a hosted agent does not serve A2A itself.**

The pieces, and the order they have to happen in:

1. **Declare the protocol on the version** — `protocol_versions: [{protocol: "a2a", version: "1.0.0"}]`
   alongside `responses`. The platform validates the version: `0.3.0` is rejected with "please use
   version '1.0.0'". Note that an unknown protocol name is accepted silently (a definition naming
   `bogus` succeeds), so acceptance alone proves nothing.
2. **Enable it on the agent endpoint**, which is separate configuration and the step that is easy to
   miss. `PATCH /agents/{name}` with
   `agent_endpoint.protocols: ["responses", "a2a"]`. Until this is set, every A2A call returns
   `endpoint-protocol-not-enabled`: *"Both 'a2a' and 'responses' protocols must be enabled on the
   endpoint"* — A2A is layered over the Responses agent, not an alternative to it.
3. **Declare an agent card** at `agent_endpoint.protocol_configuration.a2a.agent_card`. Without one:
   `agent-card-not-defined` — *"An agent card is required for A2A protocol support."* The card is
   configuration on the agent, not something the container publishes.

The card is then served from **versioned** URLs, not the well-known path. Asking for
`/.well-known/agent-card.json` returns a helpful 404 that names them:

```text
.../agents/{name}/endpoint/protocols/a2a/agentCard/v1.0
.../agents/{name}/endpoint/protocols/a2a/agentCard/v0.3
```

**What this settles.** The obvious worry — that A2A discovery is specified at the origin root while
the platform serves protocols under a prefix, so a container-published card would advertise an
unreachable base address — does not arise. The platform builds the card itself and fills in
`supportedInterfaces` with absolute URLs pointing at its own endpoint. `MapA2AHttpJson` in the
container is unnecessary.

**One constraint for callers.** The published interfaces are:

| Binding | Protocol versions |
| --- | --- |
| `JSONRPC` | 1.0, 0.3 |
| `HTTP+JSON` | 0.3 only |

`WorkforceDelegation` currently selects `ProtocolBindingNames.HttpJson`, which was the right choice
against the Aspire-hosted peer. Against a Foundry-hosted peer it constrains you to A2A 0.3, or means
moving to JSON-RPC. Decide that before porting, not after — the last time a binding assumption went
unchecked here it cost a live 404 that only a real client against a real host revealed.

### A2A limitations, and the two ways to make the outgoing call

Reviewer-supplied and consistent with what was measured here. Two of these were confirmed directly:
`Foundry Agent Consumer` is a real role (`eed3b665-ab3a-47b6-8f48-c9382fb1dad6`), and the A2A client
library exposes `JsonRpc` and `Grpc` bindings alongside `HttpJson`.

- Public preview, no SLA.
- **Incoming A2A requires the Responses protocol** - which is why the endpoint refuses `a2a` alone.
- Only A2A **1.0 and 0.3**. **1.0 is JSON-RPC only**; **HTTP+JSON exists only at 0.3**; gRPC is not
  supported.
- **Text only** - no files or other modalities - and **no streaming/SSE**.
- Entra authentication is required for the A2A endpoint *and for the agent card itself*.
- Not configurable end to end through the portal; REST/API automation is still needed.
- The calling agent's identity needs **`Foundry Agent Consumer`** on the target agent or project.

**Two ways for a hosted agent to make the call, and they are not equivalent.**

*Foundry's managed A2A tool, via a Toolbox.* The platform owns discovery, auth, routing and protocol
negotiation, so no binding decision has to be made. But `FoundryToolboxService` connects to the
**Foundry Toolboxes MCP proxy**, discovers tools through `tools/list`, and injects them as
`McpClientTool` instances. The peer therefore arrives as *a tool in a toolbox* - which is exactly the
distinction Stage 11 is built to teach against. `WorkforceAgentCard` puts it plainly: a caller
"discovers a named agent with a provider, a version and declared skills, and decides whether to
consult it - it does not receive a function signature to invoke."

*The Stage 11 client, ported.* `A2ACardResolver` (which takes an `agentCardPath`, so it can be
pointed at the platform's `/agentCard/v1.0`) then `A2AClientFactory` and `A2AAgent` - the same code
path the Aspire-hosted agent uses, with `ProtocolBindingNames.HttpJson` changed to `JsonRpc` for
A2A 1.0. The peer stays an agent.

For a lecture whose whole argument is that an agent is not a function, the second is the demo path
and the first is worth a slide: the managed option is genuinely easier, and what it costs is the
boundary you spent an hour establishing.

### The calling user's identity reaches the container

Verified by logging inbound headers on the deployed agent and calling it as two different callers.

```text
Inbound /responses: user identity present (ba1e5c6e…, 36 chars).
  Platform headers: x-agent-foundry-call-id, x-agent-response-id, x-agent-user-id, x-ms-client-request-id
Inbound /responses: user identity present (a5e76e44…, 36 chars).
Inbound /readiness: user identity absent. Platform headers: (none).
```

The two values are different callers and both match exactly: `ba1e5c6e…` is the signed-in user's
object id, `a5e76e44…` is the CI deployment principal that ran the release smoke test. So
**`x-agent-user-id` carries whoever is asking**, per request, injected by the platform. Health probes
carry no user, which is right.

**Why this matters.** It means a demo can read *the presenter's own* data without any identity in the
code, the configuration or the repository: whoever runs it is who the agent sees. A reader cloning
this repo gets their own identity, not someone else's, with nothing to edit.

**What it does not prove.** `x-agent-user-id` is an identifier, not a token. Knowing who called is not
the same as being able to act as them. Delegated access to that user's Microsoft 365 data needs a
token minted for them, which is what the Foundry Toolbox machinery describes: a tool source needing
"a per-user delegated identity, which is only available on a user request's egress", resolved through
"the platform-injected per-user isolation key", with an explicit OAuth consent state
(`CONSENT_REQUIRED`) when the user has not yet agreed.

So the open question is narrower than it was: not *does identity flow* - it does - but *can a Toolbox
with a Microsoft Graph connection turn that identity into a delegated token*. That is the next thing
to settle before building on it.

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
