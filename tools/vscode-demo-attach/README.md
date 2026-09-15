# Caesarea Demo Attach

A tiny companion extension for the Caesarea lecture demo. The DemoControl switchboard uses it to
attach the VS Code .NET debugger to a running demo service, and to detach from it again, without
leaving the demo UI, as part of the demo-breakpoint feature (requirements Section 39.6). It works
wherever VS Code and the C# extension do: Windows, macOS and Linux.

## How it works

The demo control app launches one of:

```text
code --open-url "vscode://caesarea-demo.demo-attach/attach?processName=OperationsAgent.Api.exe&processId=4242"
code --open-url "vscode://caesarea-demo.demo-attach/detach?processName=OperationsAgent.Api.exe&processId=4242"
```

`processId` is the id the service itself reports from `/api/demo-breakpoints`, so the extension
attaches to exactly that process. `processName` names the debug session and is the fallback lookup
key when no id came along (an older service, or a hand-typed URI): Windows lists processes with
`tasklist`, macOS and Linux with `ps`, matching either the apphost named after the service or
`dotnet <Service>.dll`. The name is platform-shaped - `OperationsAgent.Api.exe` on Windows,
`OperationsAgent.Api` elsewhere - and DemoControl sends the right one.

For `attach`, the extension starts a `coreclr` attach debug session named after the process and
reports the result as a VS Code notification. For `detach`, it finds that session - or a
launch.json attach session naming the same process - and stops it, which for an attach session
disconnects the debugger and leaves the service running. The extension tracks every debug session
it sees after activation, because VS Code exposes only the active one and the presenter attaches
to several services.

The DemoControl "Demo Breakpoints" panel shows each service's "Debugger attached" stamp through
the service's `/api/demo-breakpoints` endpoint, and turns the button into Attach or Detach
accordingly. On Windows the same panel can put Visual Studio 2026 on a service instead, through
`../visualstudio-demo-attach`; one service never carries both.

## Packaging and installing

```powershell
npx --yes @vscode/vsce package --allow-missing-repository
code --install-extension .\demo-attach-0.3.0.vsix --force
```

DemoControl's **Install** button runs the same `code --install-extension --force` command against
the `.vsix` committed in this folder; when VS Code holds an older version than the committed
package, the button reads **Update** instead. Bump `version` in `package.json` and repackage
whenever the extension changes, so the switchboard can tell an old install from a current one.

## Testing

```powershell
node --test tools/vscode-demo-attach/extension.test.js
```

covers the one decision the extension makes on its own: which debug session a request is about.
A session started by this extension carries the process id it attached to, and every `coreclr`
session - a launch.json attach by name included - has its real process id recorded from the debug
adapter's own `process` event. A request that names an id matches only the session on that id, so
two checkouts running a service of the same name never affect each other; a name-only session of
unknown id is matched by name only while it is the sole candidate. The live round trip, VS Code
actually attaching to a process and letting it go while a same-named process stays attached, is
`DebuggerLiveTests` in the main test project, run by `scripts/Test-DemoDebuggers.ps1`.

The extension keeps a **Caesarea Demo Attach** channel in the Output panel: each request, the
session it resolved, the process id the adapter reported, and what the debugger said while
declining. VS Code also writes it under the window's extension host logs, so a failed button on
stage has a reason on record.

## Notes

- A VS Code window that already activated an older version keeps running it until the window is
  reloaded (Developer: Reload Window), and an older version silently ignores routes it does not
  know. Reload after every install or update, before the first attach.
- Requires the `ms-dotnettools.csharp` extension (declared as an extension dependency) for the
  `coreclr` debug type.
- The `/detach` route exists from 0.2.0 and `processId` from 0.3.0; the switchboard disables the
  buttons until the installed extension matches the committed package.
