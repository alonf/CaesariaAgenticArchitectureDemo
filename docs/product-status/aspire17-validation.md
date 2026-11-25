# Aspire 17 validation

Date: 2026-11-25. Code revision: `c5e346a` on `main`. SDK: .NET 11.0.100.

## Change

- Replaced `Aspire.AppHost.Sdk/13.6.5` with `Microsoft.NET.Sdk` and explicit
  `Aspire.Hosting.AppHost` / `Aspire.Hosting.Dotnet` 17.0.0 packages.
- Migrated all eleven resources to `AddDotnetProject`, preserving service names, references,
  waits and launch profiles. Services remain in the solution; solution builds still build them.
- Retained the matching CLI bundle and `dotnet run --project Caesarea.AppHost` entry point.
- No cloud deployment or shared Azure configuration changed.

Sources: [Aspire 17 migration notes](https://aspire.dev/whats-new/aspire-17/),
[C# AppHost configuration](https://aspire.dev/get-started/aspire-sdk/),
[.NET resource migration](https://github.com/microsoft/aspire/blob/main/docs/migrate-from-addproject.md).

## Observed checks

| Check | Windows x64 | macOS Arm64 |
| --- | --- | --- |
| Restore, format, solution build | Passed; build has zero warnings/errors | Passed; build succeeded in 9.7 s |
| Deterministic test project | 469 passed, 4 skipped, 0 failed | 469 passed, 4 skipped, 0 failed |
| HTTPS `/health`, all eleven AppHost resources | All HTTP 200 | All `Healthy`, using curl with certificate validation |
| Operations Agent startup | Healthy | Healthy; former port collision did not recur |
| Deterministic repair / fault / reset via API | Passed; successful repair advanced Hub revision 3 to 4; fault rejected with HTTP 409 | See browser verification below |

The skipped tests require live IDE attachment. This pass does not revalidate live VS Code or
Visual Studio debugger attachment, cloud model calls, optional Functions hosting, or cloud deployment.

### Mac browser verification

Safari rendered both Demo Director and Command Center over trusted HTTPS. Applied **Lights On
Reported by a Client** in Director, observed L-417 On / schedule mismatch in Command Center,
then clicked **Restore Scheduled Mode** in the main app. Its event stream showed the request,
desired-state update, SmartPole confirmation and completed operation. A separate HTTPS GET to
`https://localhost:7089/api/energy/assets/L-417` confirmed `lastCommand.status = Succeeded`,
reported/desired state Off, `manualOverride = false`, `isAnomalous = false`, and revision 4.
Reapplying the scenario in Director restored its **Ready to present** fixture for the next lab.
This was actual UI execution against the local SmartPole simulator, not video footage or a
physical-device result.

## Port investigation

Before migration, the Mac Operations Agent failed to bind `https://127.0.0.1:22119`.
`lsof -nP -iTCP:22119 -sTCP:LISTEN` identified the AppHost as the existing listener, matching
`ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL` in its launch settings. Restarting only the agent did
not resolve it. The service's own launch profile uses 7311/5311, not 22119.

After stopping the entire AppHost, upgrading and starting the matching Aspire 17 bundle, the
Operations Agent and all other resources were healthy. No fixed port change or TLS bypass was
needed. The observation confirms recovery; it does not isolate an upstream allocator defect
or prove that a particular Aspire patch fixed it. Preserve that distinction in course narration.

The README documents identifying the listener, stopping the correct old instance, changing a
legitimately occupied fixed port, and retaining Aspire service discovery. This follows Aspire's
[networking](https://aspire.dev/fundamentals/networking-overview/) and
[troubleshooting](https://aspire.dev/get-started/troubleshooting/) guidance.
