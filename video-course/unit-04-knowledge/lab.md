# Lab 04 — retrieved evidence and justified explanations

Outcome: distinguish Hub observation, retrieved record, and inference. Allow 20–25 minutes. Baseline `843e0d0`; same .NET 11/Aspire prerequisites as Lab 01. Existing Foundry model configuration and identity required. Local work records are simulated; no cloud deployment is needed.

## Start and prepare

```bash
dotnet run --project Caesarea.AppHost/Caesarea.AppHost.csproj
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > before.json
```

Use Aspire service links. Director → **Knowledge** → confirm. Prerequisites: **Lights On Reported by a Client**, **Work Knowledge: evidence present**. Main app → top-right **Walkthrough**. Check Hub On, override true, daylight true, scheduled Off.

## Walkthrough and checkpoint

1. Click **Ask agent**, then **Ask "Why?" (same session)**.
2. Inspect `get_streetlight_state` and `search_work_knowledge` in the actual trace. Recording: two tools/two model round trips for Why, correlation prefix `07f98ba789b4`.
3. Scroll to retrieved evidence. Expand `WO-8732/NOTE-1`. The local fixture says the light was left on for post-maintenance verification. Read provenance and timestamp, not just the answer.
4. Compare the answer's claims with the source. A **Cited in answer** badge detects the identifier's appearance; it is not a semantic entailment check.
5. Read the Hub again. No repair should have occurred.

```bash
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > after.json
```

Compare reported state, override, revision and last-command identity, excluding read timestamps. Use your actual Aspire port if different.

## Code

`Services/OperationsAgent.Api/Services/Capabilities/KnowledgeCapability.cs`, region `KNOWLEDGE_RETRIEVAL`: `TextSearchProvider`, `OnDemandFunctionCalling`, normalized source metadata, `ContextProviders.Add`.

`Services/OperationsAgent.Api/Services/WorkKnowledge.cs`: `IWorkKnowledgeSearch`, deterministic seeded provider, evidence availability, asset-target filtering. This is not an embedding index or a live Work IQ call.

## Failure case and recovery

Director → **Work Knowledge** → uncheck seeded evidence. Refresh the main app to start without the previous session, then Ask and Why again. Recording: two searches plus one Hub read, three tools/four model round trips, correlation prefix `82b23058eb55`. The answer explicitly could not establish who authorized the override or why, and the retrieval card showed no evidence. Counts/prose may vary; verify actual trace and factual limits.

If old evidence still appears, check that you started a new conversation. Withholding retrieval does not erase earlier session history. Restore the evidence checkbox after the comparison. Reapply **Lights On Reported by a Client** if the operational state was repaired in another exercise.

## Pause and investigate

Reproduce both paths and explain one supported claim and one unverified claim. Then investigate:

1. Where are source identifiers, labels and timestamps preserved?
2. How does `QueryTargetsEvidenceAsset` handle L-528? What happens with a broad query that names no asset?
3. How is **Cited in answer** computed? What additional check would assess whether the source supports a claim?
4. What changes would a real search provider need for authorization, freshness, failure handling and provenance?

Book alignment: chapter 8, knowledge versus memory. Use documentation or an AI chat to develop your answer, then check it against code and observed traces.
