# Caesarea Demo Attach

A tiny companion extension for the Caesarea lecture demo. The DemoControl switchboard uses it to
attach the VS Code .NET debugger to a running demo service, and to detach from it again, without
leaving the demo UI, as part of the demo-breakpoint feature (requirements Section 39.6).

## How it works

The demo control app launches one of:

```text
code --open-url "vscode://caesarea-demo.demo-attach/attach?processName=OperationsAgent.Api.exe"
code --open-url "vscode://caesarea-demo.demo-attach/detach?processName=OperationsAgent.Api.exe"
```

For `attach`, the extension resolves the process id (Windows `tasklist`), starts a `coreclr` attach
debug session named after the process, and reports the result as a VS Code notification. For
`detach`, it finds that session - or a launch.json attach session naming the same process - and
stops it, which for an attach session disconnects the debugger and leaves the service running.
The extension tracks every debug session it sees after activation, because VS Code exposes only
the active one and the presenter attaches to several services.

The DemoControl "Demo Breakpoints" panel shows each service's "Debugger attached" stamp through
the service's `/api/demo-breakpoints` endpoint, and turns the button into Attach or Detach
accordingly.

## Packaging and installing

```powershell
npx --yes @vscode/vsce package --allow-missing-repository
code --install-extension .\demo-attach-0.2.0.vsix --force
```

DemoControl's **Install** button runs the same `code --install-extension --force` command against
the `.vsix` committed in this folder; when VS Code holds an older version than the committed
package, the button reads **Update** instead. Bump `version` in `package.json` and repackage
whenever the extension changes, so the switchboard can tell an old install from a current one.

## Notes

- A VS Code window that already activated an older version keeps running it until the window is
  reloaded (Developer: Reload Window), and an older version silently ignores routes it does not
  know. Reload after every install or update, before the first attach.
- Windows-only process lookup (the presenter machine).
- Requires the `ms-dotnettools.csharp` extension (declared as an extension dependency) for the
  `coreclr` debug type.
- The `/detach` route exists from 0.2.0; the switchboard disables Detach until the installed
  extension is at least that version.
