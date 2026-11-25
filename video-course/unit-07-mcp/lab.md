# Lab 07 — same capability, a new boundary

Outcome: compare local and remote tool execution without changing operational state. Allow 20 minutes. Baseline `843e0d0`; prerequisites from README/Lab 01; book chapter 9. Real model and local services, simulated SmartPole.

## Walkthrough

1. Director → **MCP Tools** → confirm. Use **Lights On Reported by a Client**, **LOCAL function**, local habitat.
2. Main app → **Walkthrough** → **Ask agent**. Save the trace and correlation.
3. Director → **MCP remote**. Ask again. Expect the same live state, the `MCP REMOTE` badge and `get_streetlight_state`.
4. Read the authoritative Hub before and after using the command below. Reads must not change `lastCommand.correlationId` or `stateRevision`.
5. Open `Services/OperationsAgent.Api/Services/Capabilities/StreetlightToolsCapability.cs`, region `MCP_CLIENT`: HTTP transport, correlation, `ListToolsAsync`, then named adoption.
6. Optional debugger: Director → Advanced controls → Energy Hub → `MCP_SERVER` → **Attach VS Code**. Ask, observe `assetId`, F10 then F11 into `GetState`, expand the actual twin, F5 and **Detach VS Code** in Director.

```bash
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > hub-state.json
```

## Checkpoints and recovery

Recorded uninterrupted remote read: `ff626204dabd`; recovered read: `921ddc2962d9`. Both one tool/two model round trips. The debugged request failed while the remote service was paused. Standard HTTP resilience logged a 10-second attempt timeout; the separate retry succeeded after detaching. Never present those as one successful request. Don't extend production timeouts merely to make a recording easier.

The debugger inspected private `_twin`; its raw `StateRevision` is not the published revision assigned by `CurrentTwin()`. Use the public API for revision comparisons. The authoritative snapshot before this lesson had revision 6 and the scenario receipt, unchanged by the reads.

Reset: reapply **Lights On Reported by a Client** if already repaired; select MCP Tools again and **LOCAL function** to reproduce the comparison.

## Investigate

- Why does discovery not automatically expose every advertised tool?
- Which code actually prevents writes, versus annotations describing them?
- Where are the MCP client and transport disposed?
- Why might pausing a remote process time out even though a local breakpoint did not?
- Which changes would a production endpoint need for caller identity and authorization?
