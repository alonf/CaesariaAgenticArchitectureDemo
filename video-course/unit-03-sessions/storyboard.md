# Lab 03 — Conversation continuity is not a fresh read

Application baseline 843e0d0. Real local app/model execution; SmartPole and local work-knowledge store simulated. Course book chapters7–8. Say Agent Framework, not the acronym. No deployments.

1. Recall the prior boundary; define the new concept before showing implementation.
2. State one outcome: Conversation continuity is not a fresh read.
3. Director prepares Session; show top-right main-app Walkthrough and starting fixture.
4. Instructor walkthrough: Ask Is L-417 on; follow up Why in the same session; inspect the actual fresh Hub read; repair through deterministic UI; ask again and compare with Hub; reset. The recorded model chose a fresh read on both follow-ups, contrary to the walkthrough's zero-tool prediction.
5. Exact code: Services/OperationsAgent.Api/Services/AgentSessionStore.cs and FoundryOperationsAgent.cs AGENT_SESSION (skill at repository root).
6. Pause exercise: reproduce, save trace/correlation, compare to authoritative Hub.
7. Failure/recovery: Show an actual unknown-session HTTP 410; start a fresh session. Explain that continuity alone cannot guarantee freshness, even though this recording did re-read the Hub.
8. Recap what was observed versus inferred; investigation questions in companion lab.

Record first; final narration must match the actual tool counts/results. No promise that every model run chooses the identical sequence. Platform capture may use Windows execution and Mac source inspection, explicitly labeled. Mac local inference currently awaits Azure CLI sign-in; never substitute Windows output as Mac execution.
