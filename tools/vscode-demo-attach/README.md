# Caesarea Demo Attach

A tiny companion extension for the Caesarea lecture demo. The DemoControl switchboard uses it to
attach the VS Code .NET debugger to a running demo service without leaving the demo UI, as part of
the demo-breakpoint feature (requirements Section 39.6).

## How it works

The demo control app launches:

```text
code --open-url "vscode://caesarea-demo.demo-attach/attach?processName=OperationsAgent.Api.exe"
```

The extension resolves the process id (Windows `tasklist`), starts a `coreclr` attach debug session,
and reports the result as a VS Code notification. The DemoControl "Demo Breakpoints" panel then
shows "Debugger attached" through the service's `/api/demo-breakpoints` endpoint.

## Packaging and installing

```powershell
npx --yes @vscode/vsce package --allow-missing-repository
code --install-extension .\demo-attach-0.1.0.vsix
```

DemoControl's **Install** button runs the same `code --install-extension` command against the
`.vsix` committed in this folder.

## Notes

- Windows-only process lookup (the presenter machine).
- Requires the `ms-dotnettools.csharp` extension (declared as an extension dependency) for the
  `coreclr` debug type.
