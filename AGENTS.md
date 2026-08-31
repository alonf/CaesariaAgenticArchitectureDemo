# Repository guidance for coding agents

Caesarea is a cumulative Smart City demo. `main` contains every implemented lecture stage behind a runtime
`DemoStage` switch (`Deterministic`, `InvestigationAgent`, ...); do not fork stages into separate branches.

Read `docs/caesarea-demo-build-prompts.md` and the stage files under `docs/prompts/` before making changes, and
follow the architecture boundaries and exclusions documented there.

This project was built with the microsoft-foundry skill. Before working on or answering questions about foundry
agents, read the microsoft-foundry skill first.

## Conventions

- Keep authoritative operational state in the deterministic services (`EnergyHub.Api`, `SmartPole.Simulator.Api`,
  `CommandCenter.Api`). Agents only read through narrow, focused endpoints; they never bypass a Hub.
- Give every API boundary its own focused `*.Contracts` project. Never introduce a generic `Common` or
  `Shared.Contracts` project.
- Keep `Caesarea.ServiceDefaults` independent of every domain/contract project.
- Use file-scoped namespaces, required accessibility modifiers, and `[LoggerMessage]` source-generated logging,
  matching the existing services.
- Every public contract, interface, and static method needs an XML doc comment; implementations use
  `<inheritdoc />` where applicable.
- Propagate correlation IDs (`X-Correlation-ID`) across every HTTP boundary and log them.
- Keep the code projector-friendly: obvious names, small types, minimal ceremony, no speculative abstraction.

## Quality commands

```powershell
dotnet restore
dotnet format
dotnet build
dotnet test --project Tests/Caesarea.Deterministic.Tests/Caesarea.Deterministic.Tests.csproj
git diff --check
```

Run these before considering any change complete. All existing tests must keep passing.

`dotnet test` runs in Microsoft.Testing.Platform mode (opted in via `global.json`; the test project
is an xunit.v3 executable). The `--project` form is the reliable invocation: a bare `dotnet test`
walks every project in the solution and can fail on machines where the Aspire AppHost SDK does not
resolve during test discovery. VS Code's Test Explorer uses the Testing Platform protocol
(`.vscode/settings.json`).
