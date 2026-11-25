# Lab 03 — conversation continuity and current truth

Outcome: explain which facts came from session context and which came from a fresh Hub read. Allow 20 minutes. Baseline `843e0d0`, .NET 11.0.100 / Aspire 17.0.0. Use Lab 01 and README for setup; the Agent Framework patterns also apply to .NET 10 LTS.

## Prepare and execute

```bash
dotnet run --project Caesarea.AppHost/Caesarea.AppHost.csproj
```

1. Use Aspire service links. Director → **Session** → confirm preparation. Verify **Lights On Reported by a Client**: L-417 on, daylight, override true, healthy controller.
2. Main Command Center → top-right **Walkthrough**. Click **Ask agent**, then **Ask "Why?" (same session)**.
3. Save the session/correlation identifiers and tool counts. In the recording both the initial request and Why ran one Hub tool over two model round trips. The walkthrough's zero-tool prediction did not match this run; commit `4e5a6d3` later corrects that wording. Do not require exact counts or prose as a pass criterion.
4. Click deterministic **Restore Scheduled Mode**. Verify Off and cleared override at the Hub. Ask Why again in the same session. The recording made a fresh read and reported the changed state; another run may choose differently.

```bash
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > current.json
```

Expected after repair: `reportedIsOn=false`, `manualOverride=false`, a successful command receipt. Use the Aspire endpoint if your assigned port differs. The SmartPole is simulated; the response is the real local Hub state.

## Inspect code

- `Services/OperationsAgent.Api/Services/AgentSessionStore.cs`: in-memory serialized state, 30-minute sliding expiration, 50-session cap, stage guard.
- `Services/OperationsAgent.Api/Services/FoundryOperationsAgent.cs`: `AGENT_SESSION`, create/deserialize, RunAsync, serialize/save. Inspect this after Lab 02's tool loop.

## Failure and recovery

Deliberately send an unknown identifier (use `curl.exe` in Windows PowerShell if `curl` is an alias):

```bash
curl -i https://localhost:7311/api/operations-agent/ask \
  -H 'Content-Type: application/json' \
  -d '{"question":"Why?","sessionId":"lab03-unknown-session"}'
```

Expected HTTP 410 with a session-expired problem response. The saved recording evidence contains an actual response. Start a new conversation through **Ask agent**; do not reuse the unknown identifier. In-memory history does not survive a service restart. Do not restart the whole lab merely to demonstrate an unknown ID.

## Pause exercise and reset

Repeat the before/after sequence. Explain whether the second answer used fresh data, using the trace rather than an assumption about sessions. Reapply **Lights On Reported by a Client** in Director and check the Hub is On with override true before Lab 04.

Investigate in code/documentation or with an AI chat, then verify its claims:

1. Which operation refreshes the session expiration? What happens to the oldest sessions at capacity?
2. How does the stage guard prevent reuse of history from a more capable stage?
3. What happens if two requests update the same session concurrently?
4. What would require persistence, and why would persistence alone still not guarantee fresh operational facts?

Book: chapter 7, session continuity; chapter 8 introduces evidence and memory.
