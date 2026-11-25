# Lab 11 — a specialist with a narrow disclosure boundary

Outcome: explain intentional daylight lighting without exposing the Security domain's operational detail. Allow 20 minutes. Book chapter 11. Local Windows services and simulated city, real model calls.

## Baseline and actual takes

OFF footage: `843e0d0`, correlation `018d185af432`. ON footage: `59017d7`, correlation `37dcf6567f6840ac933f3283b8ae1148`. The app was restarted and the fixture reapplied between takes. These are separate requests, not one continuous request. The new baseline corrects agent transport budgets; 471 automated tests passed, four opt-in live debugger tests skipped.

## Walkthrough

1. Director → **Multi-Agent** under **Security Operation** → confirm. Keep local habitat and **Consult OFF**.
2. Main application → **Walkthrough** → **Why is L-417 on during daylight? (new session)**. Expect intentional lighting from an external directive, without private operational attribution. Save the Hub snapshot.
3. Director → **Consult ON**. Ask the same question again in a new session.
4. Inspect **Capability trace**: `assess_lighting_requirement` must actually run. Inspect **Consulted specialist**, its identity, area, verdict, deadline, recommendation and details-withheld explanation.
5. Independently fetch the Hub again. Compare revision and last-command correlation; both must be unchanged by consultation. Expected: `reportedIsOn=true`, `effectiveTargetIsOn=true`, `operationContext.requiresLighting=true`, `isAnomalous=false`.

```bash
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > hub-state.json
```

Successful ON evidence is under `../production-batch/evidence/11-fixed-*`: before/after revision 3 and scenario receipt `7dd7691e4c6f4be9b1708773ed35d535`. Four outer tool calls/four outer model round trips. Specialist logs separately show restricted read and sanitized completion.

## Source investigation

- `Services/OperationsAgent.Api/Services/Capabilities/SecurityConsultCapability.cs`: stage/switch gate, MCP discovery, named-tool adoption and correlation.
- `Services/SecurityAgent.Api/Services/SecurityAssessmentAgent.cs`: one snapshot, bound area, separate instructions/tool/model run; `SecurityAssessmentSanitizer` produces the disclosed result.
- `Services/SecurityAgent.Api/Services/SecurityMcpTools.cs`: the advertised tool returns the sanitized assessment.
- `Services/SecurityHub.Api/Services/CallerIdentity.cs`: local demonstration caller header; do not present this as authenticated production identity.

## Failure and recovery

The first ON take failed because the default 10-second HTTP attempt timeout could cancel a specialist with a 45-second execution budget. Commit `59017d7` replaces that client pipeline with bounded 50-second attempt/55-second total limits and disables unsafe POST retries. The new completed take is a separate run. The configured specialist default is 45 seconds; if changing that configuration, review caller budgets too. This is a local client-policy mismatch, not evidence of a cloud deployment failure. A paused debugger can still exceed transport budgets.

If no specialist result arrives, preserve the limited authoritative explanation, inspect correlation across both services, detach any debugger, and retry the read-only consultation. Do not infer permission to restore lighting. Reset through Director → Multi-Agent; this reapplies the fixture and turns consult OFF.

## Pause and investigate

- Which lighting facts are available before consultation?
- Where does model judgment stop and deterministic policy begin?
- Can a model request a different area from the snapshot-bound tool?
- Why is a second agent justified here, and when would a deterministic projection be sufficient?
- Why does the outer trace not measure all model calls inside the specialist?
- What authentication and service authorization would replace the local header in a deployed system?
