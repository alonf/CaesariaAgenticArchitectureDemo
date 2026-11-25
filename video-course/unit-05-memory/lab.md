# Lab 05 — case memory as a lead

Outcome: distinguish a recalled conclusion from evidence about a new asset. Allow 25 minutes. Baseline `843e0d0`; use Lab 01/README setup. Book chapter 8. Local SmartPole/work records/case store are deterministic demo fixtures; model inference uses the configured Foundry deployment.

## Fixture and main-app flow

1. Director → **Memory**, with **Lights On Reported by a Client** and work evidence present. If starting clean, run the AppHost as in previous labs. Open main-app **Walkthrough**.
2. Obtain an explained L-417 answer. If the prior answer is gone, **Ask agent** → **Earlier stages — still available** → **Ask "Why?"**. Confirm the answer names `WO-8732` and its technician note.
3. **Close case (record to memory)**. Expect `CASE-1` in a clean store. This saves a conclusion; it does not repair L-417 or close an operational incident.
4. **Ask about L-528 (new session)**. The map remains selected on L-417; inspect the question and tool arguments for L-528.
5. Expand the recalled case. Expected: fresh L-528 Hub read, search of L-528 work knowledge, empty evidence, CASE-1 labeled a hypothesis from L-417. Counts may vary. Recording: three tools/three model round trips, correlation `7f8ffbda274649b0a72968cc6e8dd466`.

```bash
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-528 > l528.json
curl --fail --silent --show-error https://localhost:7311/api/operations-agent/cases > cases.json
```

Check asset IDs, On/override state and the saved case's provenance. No repair command is expected. Use the endpoints shown by your Aspire instance if ports differ.

## Code walkthrough

- `Services/OperationsAgent.Api/Services/CaseMemory.cs`: bounded in-memory store, two meaningful shared terms minimum, three recalled cases maximum; `CaseMemoryProvider.CreateRecallContext` separates static rules from serialized data.
- `Services/OperationsAgent.Api/Services/Capabilities/CaseMemoryCapability.cs`: provider composition and trace of recalled cases.
- `Apps/CommandCenter.Web/Components/Pages/Home.razor`, `CloseCaseAsync`: operator action and summary preparation. The saved summary is truncated; it is not a complete transcript.

## Failure comparison, recovery and reset

Director → Advanced controls → **Case Memory → Clear case memory**. This removes disposable local demo cases; save `cases.json` first if you want to compare. Start a new main-app session and ask L-528 again. The recording returned current facts and missing work evidence with no recalled case (correlation prefix `31ec41414a9e`). Do not infer persistent storage from cross-session recall: this store is in memory.

If a prior case remains in an answer, check for old conversation history. To recreate the case, ask/Why about L-417 with evidence present and close that explanation again. For the next lesson, keep **Lights On Reported by a Client**, restore work evidence, and leave case memory empty for a simple comparison.

## Pause exercise and questions

Explain three statements from the answer: a Hub observation, a missing-evidence statement, and an analogy. Then investigate:

1. Why can a case from another asset match? What words are excluded?
2. What is lost when the UI truncates the closing summary?
3. Why is recalled text placed outside static Instructions? What does this mitigation fail to guarantee?
4. How would a persistent store handle expiry, correction, user scope and provenance without making old conclusions authoritative?
