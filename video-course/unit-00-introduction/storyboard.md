# Unit 00 — repair L-417 and prove it happened

Outcome: start the pinned local lab, restore scheduled mode, prove the simulator-confirmed
outcome at the Energy Hub, and reset the starting fixture. Target 8–12 minutes, English synthetic
narration, captions and real execution captures. The anomaly appears within the first 30 seconds.

Use the [timed shooting script](shooting-script.md), [copyable lab](lab.md),
[baseline](baseline.md), and [book alignment](../book-alignment.md).
`scenes.json` is the editable narration/action manifest; `prepare_script.py` exports the script.
These supersede the earlier slide-led introduction and its longer debugger tour.

The Director prepares conditions; the main Command Center performs the restore. Capture the Hub
before and after. Explain the actual confirmation branch in `EnergyHubService.cs`, not an agent
tool that this unit never invokes. MAF receives a short rationale: later it chooses investigation
steps while the deterministic boundary remains authoritative.

VS Code / Visual Studio 2026 setup is a short note. Actual attachment and stepping belong in the
first agent lesson. No model access, cloud deployment, Docker or Functions is required here.

## Acceptance

- Real main-app anomaly in the first 30 seconds; simulated-device label.
- Exact committed baseline and prerequisites; never call GitHub main the .NET 11 baseline.
- Director preparation, main-app action, actual Hub receipt/state, readable source, explicit reset.
- Pause-and-do exercise, expected result and recovery from the wrong fixture.
- Local MP4 with inspected frames, audible narration and retimed captions. Label edited screen
  captures as a rough cut; do not represent them as continuous recording.
- If capture is blocked, deliver script/lab and name missing shots; narration alone is not a pilot.

Stop after Unit00 for review. Later units remain plans.
