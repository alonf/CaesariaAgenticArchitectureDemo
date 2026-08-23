# Caesarea Stage 0 Deterministic Demo

`DemoStage=Deterministic` enables the conference-ready Stage 0 smart-city demo.

## Architecture boundaries

- `Services\EnergyHub.Api` is the authoritative lighting boundary.
- `Services\SmartPole.Simulator.Api` simulates the vendor/device system behind Energy Hub.
- `Services\CommandCenter.Api` aggregates snapshots, incidents, and activity without calling SmartPole directly.
- `Services\DemoScenario.Api` coordinates repeatable presenter scenarios across the deterministic services.
- `Shared\CanonicalModel` contains only canonical operational concepts.
- API DTO ownership stays focused in `Contracts\SmartPole.Contracts`, `Contracts\Energy.Contracts`, `Contracts\CommandCenter.Contracts`, and `Contracts\DemoScenario.Contracts`.
- `Caesarea.ServiceDefaults` stays independent of domain and transport contract projects.

## Quality commands

```powershell
dotnet restore
dotnet format
dotnet build
dotnet test
dotnet test --settings .\coverage.runsettings --collect:"XPlat Code Coverage" --results-directory .\TestResults
git diff --check
```

## Run locally

```powershell
dotnet run --project .\Caesarea.AppHost
```

## Coverage artifact

After the coverage command finishes, inspect the generated Cobertura file under:

```text
.\TestResults\<run-id>\coverage.cobertura.xml
```

The coverage profile measures executable deterministic API code. It excludes DTO-only contract assemblies, generated OpenAPI code, and composition-root `Program.cs` files; those surfaces are protected by clean builds, analyzers, and architecture tests.
