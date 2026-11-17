# .NET 11 upgrade execution record

Date: 2026-11-17. Branch: `upgrade/net11-foundry-insights-routines`, based on `7ad5369`.
This records the implementation slices, not completion of every work package in the plan.

November 18 research follow-up: [Ignite announcement and gap review](ignite-2026-gap-review.md).
Insights service is now announced GA; the selected SDK remains beta. New AKS, observability,
IQ and optimization announcements update the material backlog, not the implemented feature list.

## Implemented

- SDK 11.0.100, `net11.0` in the original 25 portable projects, and `net11.0-windows` in both
  Visual Studio helper projects. Insights, the Functions host and its focused contracts are also
  `net11.0` (30 projects total, 28 in the solution).
- Aspire AppHost SDK 13.6.5. Its graph resolves MessagePack 2.5.307; no explicit transitive override
  or audit suppression was needed.
- ASP.NET Core, Extensions.AI, resilience and service discovery 11.0.0; MAF Workflows 1.28.0 and
  Foundry/Hosting/A2A 1.28.0-preview.261106.1; Projects 3.0.0-beta.6; MCP 2.4.0; OpenTelemetry
  1.21.0; xunit.v3 4.0.3; Markdig 1.6.0; SonarAnalyzer 10.36.0.15337. Azure.Identity remains 1.21.0.
- The Insights utility pins Extensions.OpenAI 3.0.0-beta.5 to match the compiled MAF bridge. Projects
  beta.6 otherwise asks for an unpublished alpha minimum and produces NU1603. Extensions.OpenAI
  3.0.0 is available; promotion of that bridge requires a separate compatibility check.
- Five Dockerfiles now use verified `11.0.100-resolute` build and `11.0-resolute` runtime tags.
  .NET 11 `noble` tags do not exist. CI uses .NET 11 and explicit Testing Platform project arguments.
- GitHub Actions updated to current release tags, weekly Dependabot configured, and the offline
  ASSERT adapter suite added to CI. Hosted GitHub runner execution remains a merge gate.
- Fixed the pre-existing Razor parser failure by replacing the escaped `@switch` identifier.
  Adopted asynchronous DataAnnotations validation, `Lock`, and generic JSON metadata lookup.
  Protocol-only hosted projects disable JS modules to avoid a .NET 11 Debug hot-reload asset collision;
  the Blazor applications retain browser hot reload.
- The A2A test model now implements streaming because MAF 1.28 consumes that path internally.
  Existing peer time-budget and authority tests continue to exercise the actual endpoints.
- Hosted Energy tools resolve correlation per invocation rather than capturing the constant
  `hosted`; a concurrent two-request test covers separation. Local agent telemetry is explicitly
  instrumented with sensitive-content export disabled. Reasoning-replay compatibility shims remain.
- Added disabled timer routine tooling, a read-only Insights utility, and optional presenter steps
  in Hosting/Evaluation. See the runbook for their cloud prerequisites and current validation limits.

## Validation

The .NET 10 baseline passed 415 tests after the Aspire/Razor repairs. The upgraded .NET 11 suite
passed **416** on Windows (Debug and Release) and Linux, with **4 live IDE tests skipped**. The added test covers concurrent invocation
correlation. The Windows helper passed **41**, ASSERT adapter **16**, and VS Code extension **7**.
All five Linux images built and passed their startup health/readiness checks. All nine Bicep
templates compiled. The solution and helper NuGet audits reported no vulnerable direct or transitive
dependencies, and no deprecated direct solution packages were found. Restore, Release build,
format verification (solution and helper) and whitespace checks passed; the build had zero warnings
and errors. These are local checks; the updated GitHub Actions jobs have not run remotely.

No cloud deployment, publication, model evaluation, Insights analysis, or routine invocation is
implied by these local checks. The existing project's agent was found; its monitor list was empty.
The Routines read failed due to azd credential acquisition. A fresh `azd auth login` is needed.

## Durable Functions slice

The [local recovery host and runbook](../runbooks/durable-recovery.md) now implement the two durable
lifetimes: a MAF conversation backed by Durable Entities and a supervised repair orchestration.
Real Foundry model calls proved session recovery after host restart and forced worker termination,
then successful supervised completion through the Hub. Operator approval was rejected; duplicate
completed approval was rejected; Security Operation caused `StateChanged` and left lighting on.
These are local-host/model-inference checks, not a cloud Functions deployment or behavioral eval suite.

The new package/toolchain matrix, host-graph security patch, Windows worker-path workaround and
entity-owned versus service-owned history finding are documented in that runbook. Core Tools on
this machine is now 4.16.1. The original Stage 8 MAF graph remains; the recovery beat uses a separate
Durable Task orchestrator sharing deterministic policy and narrow gateways.

The final slice passed **428 deterministic tests on Windows and 428 in the Linux SDK container**,
with four existing live-IDE skips on each. Restore, format, build and whitespace checks passed;
the Windows build had zero warnings/errors. The solution and generated Functions host-extension
graph have no known vulnerable packages in the configured sources. The Functions project's direct
packages have no available stable updates in those sources. An unanswered review also expired
as `TimedOut` after worker downtime, without changing the Hub revision or lighting.

## Remaining work

### Recovery review follow-up

The two review findings are fixed in code: bounded initial/polling read retries and explicit new
attempts for terminal pre-dispatch failures; pending-command receipt reconciliation without a
resend. Unconfirmed effects return `OutcomeUnknown` with nullable fields. Stale approval before
dispatch still has a definite no-command result. New activity histories use the isolated v2 task hub.

Fresh verification passed **447 deterministic tests on Windows and Linux**, with four existing
live-IDE skips each, and six local HTTP tests for the PowerShell rehearsal poller. Restore, format,
build and whitespace checks passed. Windows build had zero warnings/errors; the clean Linux build
emitted one `AZFW0108` warning while restoring the generated Functions extensions during build.
The orchestration tests use a scheduler double; the in-flight command test uses the real Energy
Hub HTTP endpoint. A fresh v2 DTS worker-restart rehearsal remains open.

The revised runbook waits for `AwaitingApproval` before restart and a terminal business result
after approval. [Historical sanitized captures](../evidence/durable-recovery-2026-11-17/README.md)
preserve v1 outputs, with explicit provenance limits. The earlier 428-test/live-restart record
is an implementer observation, not an independent rerun or evidence of v2 live behavior.

### Open gates

Agreed scope: the current recovery demonstration stays local. Explain Azure hosting in the slides
and book as a proposed architecture. Full Azure Functions deployment and its associated cloud
identity/infrastructure work below are deferred to a separate task, not required for this update.
Recording the v2 local worker restart remains required before presenting that behavior as verified.

- Complete the live host/Work IQ/Teams regression and three-OS GitHub CI before merging.
- Configure an Insights monitor and collect genuine evidence; rehearse timer activation with the
  intended unattended identity. Add persistent admission before any consequential follow-on work.
- Future cloud scope: extend the local Durable Functions recovery slice to authenticated Command
  Center approval, separate executor identity, cloud hosting and durable Hub receipts for wider failures.
- Verify interactive debugger attach/detach and Blazor/Mermaid visuals. The vendored Mermaid major
  upgrade and ASSERT upstream commit change are deliberately not bundled into this SDK migration.
- Revise editable presentation and book sources after the implemented demonstrations are verified.

On this machine the SDK is per-user. `Start-CaesareaDemo.ps1` selects it automatically; dot-source
`scripts/Use-CaesareaSdk.ps1` before other CLI work. The helper changes only the current process's
PATH and DOTNET_ROOT, preserving the machine's .NET 10 installation.
