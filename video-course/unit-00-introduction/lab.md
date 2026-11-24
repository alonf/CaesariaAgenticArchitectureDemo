# Unit 00 lab — repair L-417 and prove it happened

Allow 20–30 minutes for a first setup; the edited video is 8–12 minutes. Restore a simulated
streetlight, verify the Energy Hub's receipt/state, and reset for the next lesson. No Azure
subscription, model credential, Docker or Functions runtime is required. No real equipment changes.

## 1. Use the course baseline

Read [baseline.md](baseline.md); GitHub main is not the .NET 11 recording baseline. This local
pilot uses a clean worktree at the committed upgrade baseline below:

```powershell
git clone https://github.com/alonf/CaesariaAgenticArchitectureDemo.git
cd CaesariaAgenticArchitectureDemo
git fetch origin upgrade/net11-foundry-insights-routines
git switch --detach 89d5a513be97301857dd4f156ce0773103712d51
git rev-parse HEAD
```

If that revision is unavailable remotely, obtain the local course Git bundle from the instructor;
do not substitute main. No bundle has been published by this task. Pending durable controls in
the instructor's other worktree are excluded from this pilot.

Prerequisites: Git, PowerShell 7, the SDK selected by global.json (11.0.100 or compatible later
.NET 11 feature band), and a browser. VS Code or Visual Studio 2026 is useful for source reading;
no debugger is required here.

```powershell
$PSVersionTable.PSVersion
Get-Content ./global.json
. ./scripts/Use-CaesareaSdk.ps1
dotnet --version
dotnet restore
dotnet build
dotnet test --project ./Tests/Caesarea.Deterministic.Tests/Caesarea.Deterministic.Tests.csproj
```

Checkpoint: build and tests succeed. Do not expect a historical test count from the book. If SDK
resolution fails, install the required SDK and restart the shell; do not loosen global.json.

## 2. Start only the local lab

```powershell
dotnet run --project ./Caesarea.AppHost/Caesarea.AppHost.csproj --no-build --launch-profile https
```

Keep the terminal open. Use this run's printed Aspire login link; its private token changes. If
needed, complete normal .NET development-certificate trust setup; do not disable HTTPS validation.
Open `democontrol-web` and `commandcenter-web` from Aspire. Typical HTTPS URLs are ports 7076
and 7028, but the dashboard is authoritative. Wait for deterministic resources to run.

Do not use `Start-CaesareaDemo.ps1` for this local-only exercise: that presenter script also checks
hosted configuration and may refresh a OneDrive work order. Agent credentials are irrelevant here.

## 3. Prepare L-417

In Director choose **1 Deterministic**, inspect and confirm the preparation preview. Start with
**Lights On Reported by a Client** (code name `ForgottenOverride`). Verify readiness, then switch
to Command Center. If already repaired, explicitly reapply the scenario; selecting an active stage
alone is not proof of reset.

Checkpoint: L-417 / North Promenade; daylight; reported On; schedule Off; override on; healthy
controller. There is no agent investigation. An override is a condition, not the technician's reason.

## 4. Read the Hub before acting

In a second PowerShell terminal use the HTTPS `energyhub-api` base URL shown by Aspire:

```powershell
$hubBase = 'https://localhost:7089' # Replace if Aspire reports another port.
$checkHeaders = @{ 'X-Correlation-ID' = "unit00-read-$([guid]::NewGuid())" }
$before = Invoke-RestMethod "$hubBase/api/energy/assets/L-417" -Headers $checkHeaders
$before | Select-Object assetId, isDaylight, reportedIsOn, expectedScheduledState,
    effectiveTargetIsOn, manualOverride, stateRevision | Format-List
```

Expected: daylight/reported/override True; schedule/effective target False. Save the revision;
its numeric value changes across resets.

## 5. Restore through the main app and verify

In **Command Center**, click **Restore Scheduled Mode** once. Wait for completion. This is the
operator's deterministic command path. Read the Hub again:

```powershell
$after = Invoke-RestMethod "$hubBase/api/energy/assets/L-417" -Headers $checkHeaders
$after | Select-Object assetId, reportedIsOn, desiredIsOn, manualOverride, stateRevision | Format-List
$after.lastCommand | Format-List
if ($after.reportedIsOn -or $after.desiredIsOn -or $after.manualOverride) {
    throw 'Repair not verified: lamp or override is still on.'
}
if ($after.lastCommand.status -ne 'Succeeded') {
    throw 'Repair not verified: no succeeded Hub command receipt.'
}
if ($after.stateRevision -le $before.stateRevision) {
    throw 'Repair not verified: the Hub revision did not advance.'
}
$commandCorrelation = $after.lastCommand.correlationId
$activity = Invoke-RestMethod "$hubBase/api/energy/assets/L-417/activity?limit=20" -Headers $checkHeaders
$activity | Where-Object correlationId -EQ $commandCorrelation | Format-List
```

Checkpoint: reported Off, desired Off, override cleared, succeeded receipt, nonempty command
correlation, newer revision. The read correlation differs from the command correlation: join events
using `lastCommand.correlationId`. Inspect its completion time and simulator-confirmation summary.
This proves local simulated execution, not physical equipment delivery or production identity.

## 6. Inspect the boundary

Open `Services/EnergyHub.Api/Services/EnergyHubService.cs`, method `RestoreScheduledModeAsync`.
Find Pending, `SetLampStateAsync`, and the success branch setting `ReportedIsOn = confirmedIsOn`
and clearing `ManualOverride`. Desired state may change before confirmation; reported success
must wait for the device response. Find the optional expected-revision check above it.

Book: Chapter 6 pp66–77 and Chapter 14 pp184–190. Use its rationale, not its old SDK commands.
H08 p13 locates services; p15 introduces the later agent. Breakpoint setup is deferred to that lesson.

## 7. Pause and do

In Director reapply **Lights On Reported by a Client**, keeping **Deterministic**. Verify the
starting anomaly at the Hub and obtain a new `$before`. Repeat steps 4–5 without the video.

1. Which service owns reported state?
2. What distinguishes command acceptance from successful execution?
3. What do you still not know about the override's cause?

Expected: Energy Hub owns the twin; a succeeded correlated receipt plus confirmed reported
state proves this simulated restore; the technician's reason still needs evidence.

## Recovery and reset

Check the fixture first if the outcome differs. **Controller Fault** intentionally cannot produce
the healthy confirmation expected here; **Normal Operation** is already off. Reapply **Lights On
Reported by a Client**, verify healthy controller and Deterministic, reread the Hub, and retry.
Do not call a failed receipt successful because the desired target changed.

If a browser asks for an unrelated username/password, use the resource link from Aspire.
Port 5187 on the recording machine belonged to another app; Director used HTTPS 7076.
Do not enter credentials into the wrong service.

At the end, reapply Lights On and verify On/override on: the first agent lesson starts there.
Stop with Ctrl+C in the launch terminal or repository-scoped cleanup:

```powershell
./scripts/Stop-CaesareaDemo.ps1
```
