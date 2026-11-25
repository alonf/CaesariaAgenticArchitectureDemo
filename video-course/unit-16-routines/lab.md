# Optional Lab 16 — prepare a one-shot investigation

This is a **local preparation lab**, not a successful cloud dispatch recording. Outcome: inspect the activation boundary, reproduce WhatIf and local validation failure, and specify the evidence needed for a future run. Allow 15 minutes. Code `4e5a6d3`; no cloud settings are changed.

## Copyable local check

Run PowerShell 7 from the repository root on Windows or macOS. Keep `-WhatIf` and the placeholder endpoint. No network request or deployment occurs on this path.

```powershell
./scripts/Manage-InvestigationRoutine.ps1 -Operation Create -ProjectEndpoint 'https://example.services.ai.azure.com/api/projects/example' -AgentName 'read-only-investigator' -AtUtc ([DateTimeOffset]::UtcNow.AddMinutes(10)) -WhatIf
```

Expected: `What if: Performing the operation "Create disabled routine"...`. This proves argument validation and the ShouldProcess boundary, not service readiness. Change AtUtc to `'2000-01-01T00:00:00Z'`; expected local exception: Create requires AgentName and a future AtUtc. Recover by restoring a future time and retaining WhatIf. There is no cloud fixture to reset.

## Read the code

- `deployment/routines/lighting-investigation.json`: one-shot timer, placeholder target, read-only investigation prompt.
- `scripts/Manage-InvestigationRoutine.ps1:25`: endpoint validation; `:42` returns on WhatIf; `:49` disabled creation; `:51` run listing. WhatIf does not build or validate the remote manifest.
- `Services/OperationsAgent.Hosted/Program.cs:200`: actual hosted tool composition; conditional Work IQ registration elsewhere in the file. A prompt is not authorization.

The real read-only inventory attempt returned `AzureDeveloperCLICredential: please run "azd auth login"...`. It did not return a routine inventory. No Create, Enable or Dispatch was performed. Fresh cloud execution remains a pickup requiring working sign-in, a verified existing read-only target and authorization for that run.

Microsoft documents agent identity as the default and an explicit creator-identity option for delegated tools. Our wrapper does not implement that identity-selection option; review the target's assumptions rather than inferring delegated access. Supported activation and run-inspection details are in [the official Routines guide](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/use-routines). This repository example uses a one-shot timer; it does not implement a city Service Bus event integration.

## Pause and investigate

Write a proposed acceptance record containing exact target/version, effective invocation identity, trigger time, terminal run status, response/tool evidence and Hub before/after state. Do not fill missing fields with guesses.

1. Why does a timer not authorize a repair?
2. Why is acceptance different from completion?
3. What would establish that the intended Hub was read?
4. Which identity accesses tools when no operator is present?
5. Does disabling future activation cancel an already-running invocation? Verify the current service contract before relying on it.

For a later authorized cloud rehearsal, inspect Show/Runs and explicitly disable/remove disposable routines using the current supported commands. Removing a local manifest is not proof that a remote resource was removed. This lesson creates none.
