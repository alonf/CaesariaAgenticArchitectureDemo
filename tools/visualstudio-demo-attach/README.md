# Caesarea Visual Studio Demo Attach

A small Windows-only helper that lets the DemoControl switchboard attach the Visual Studio 2026
debugger to one running demo service, and detach it again, without leaving the demo UI. It is
the Visual Studio counterpart of the VS Code extension in `../vscode-demo-attach`, for the
demo-breakpoint feature (requirements Section 39.6).

Visual Studio is an optional debugger frontend. Nothing in the demo runtime depends on this
helper, on Visual Studio, or on Windows: the services only ever check `Debugger.IsAttached`, and
on macOS and Linux the switchboard simply reports Visual Studio as unavailable and offers VS Code.

## How it works

DemoControl runs the built executable and reads one JSON report from standard output:

```text
VisualStudioDemoAttach.exe status --solution C:\Dev\Caesarea\Caesarea.slnx
VisualStudioDemoAttach.exe attach --solution C:\Dev\Caesarea\Caesarea.slnx --process-name OperationsAgent.Api.exe --process-id 4242
VisualStudioDemoAttach.exe detach --solution C:\Dev\Caesarea\Caesarea.slnx --process-name OperationsAgent.Api.exe --process-id 4242
VisualStudioDemoAttach.exe instances
```

- The instance is found through the COM running-object table, where every `devenv` registers its
  automation object as `!VisualStudio.DTE.<version>:<pid>`. The helper picks the Visual Studio 2026
  instance that has the given solution open - never "the first one running", so a Visual Studio
  2022 window on another repository, or a second 2026 window on a scratch project, is never the
  one that gets the debugger. Visual Studio 2022 is reported but not used: the demo targets .NET 10
  and Visual Studio 2026 only.
- The exact solution wins; another solution file in the same folder counts only when nothing has
  the exact one open; two windows on the same solution are reported as ambiguous rather than
  guessed at. Once an instance has attached, DemoControl passes its `--instance-pid` on detach so
  the request goes to that window, whatever else has opened since.
- The process is resolved by id when DemoControl has one (each service reports its own id from
  `/api/demo-breakpoints`), by exact executable name otherwise. Attach uses the
  `Managed (.NET Core, .NET 5+)` engine, which is the one that lands `Debugger.Break()` on the
  demo line, and reports success only once Visual Studio lists the process among the ones it
  debugs; detach lets go of that one process and leaves it running. No `DetachAll`, ever.
- A busy Visual Studio (mid-build, mid-paint) rejects automation calls with "call was rejected by
  callee"; a registered COM message filter and a retry loop turn that into a short wait.

The report is one JSON object, exit code 0 on success, 1 on failure, 2 for a bad command line:

```json
{"succeeded":true,"operation":"attach","message":"Visual Studio 2026 Enterprise attached to OperationsAgent.Api.exe (PID 4242) with Managed (.NET Core, .NET 5+).","attached":true,"instance":{"displayName":"Visual Studio 2026 Enterprise","version":"18.9.12105.275","processId":31240,"solution":"C:\\Dev\\Caesarea\\Caesarea.slnx","supported":true},"target":{"processName":"OperationsAgent.Api.exe","processId":4242}}
```

Failures carry a presenter-facing `message` and, for `status`, the `instances` seen.

## Building

The helper is not part of `Caesarea.slnx`, so the portable `dotnet build` never touches it. Build
it on the Windows presenter machine:

```powershell
dotnet build tools/visualstudio-demo-attach/src --configuration Release
```

Both configurations land in `tools/visualstudio-demo-attach/dist/`, the one path DemoControl looks
at. The switchboard's debugger picker offers **Build Visual Studio helper** when the executable is
missing, and `scripts/Start-CaesareaDemo.ps1` builds it during setup when Visual Studio 2026 is
installed. The output folder is git-ignored; never commit the executable.

## Testing

```powershell
dotnet test --project tools/visualstudio-demo-attach/tests/VisualStudioDemoAttach.Tests.csproj
```

covers the pure parts: command line, moniker parsing, instance selection, engine choice and the
JSON contract. The live round trip - Visual Studio 2026 actually attaching to a process and letting
it go - is `DebuggerLiveTests` in the main test project, run by `scripts/Test-DemoDebuggers.ps1`
with Visual Studio 2026 open on the solution.

## Notes

- Requires Visual Studio 2026 (major version 18) with the Caesarea solution open, and a Debug build
  of the services so `Debugger.Break()` lands on source.
- The `Microsoft.VisualStudio.Interop` package provides the DTE interfaces. They are COM contracts,
  unchanged across versions; the 17.x assemblies drive Visual Studio 2026 through the same IIDs.
- The helper never terminates a service. A failed attach or detach is reported and the service
  keeps running.
