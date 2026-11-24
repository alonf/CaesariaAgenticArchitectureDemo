# Agentic C# — Unit 00 production plan

Course: **Building C# Agentic Systems with MAF and Foundry by Alon Fliess**.
Produce this foundation unit only, then stop for author review.

## Opening and teaching order

1. Open on the course promise, before naming Caesarea: a rapidly changing field needs foundations, sound decisions and a practical toolset. Explicitly address C# developers building new systems or adding agentic layers to existing systems; assume no agent experience and explain the value to developers who already know the APIs.
2. Introduce Microsoft Agent Framework as the development framework and Microsoft Foundry as the platform. Teach when, how and why, with C# and .NET throughout.
3. Explain the system around the agent: deterministic code, knowledge, controlled tools, architecture choices, policy and governance.
4. Use Alon's About Me slide: Foundry MVP, Regional Director, ZioNet CTO, architect, author and speaker. Invite the learner to enjoy the journey.
5. Preserve the previous foundation: learning questions, runtime agent versus coding assistant, MAF, decision map, system of systems, Hubs, operational twins, deterministic core and agentic layer. The smart city is an example, not the course goal.
6. At the decision map show `github.com/alonf/agentic-architecture-skills`. Explain the architecture router and MAF mapping; advice still requires review and enforcement.
7. Insert a moving-parts orientation: real GitHub root and cumulative-stage README, local project tree, Microsoft Learn C# first-agent/tools pages, this Foundry project and its actual hosted agent. After the architecture/context foundation, use slides 16 and 18 for the smallest C# construction/tool examples. The slide and Learn snippets are code reading; the app/debugger sequence supplies execution evidence.
8. State that the lab targets .NET 11 and the same Agent Framework patterns apply on .NET 10 LTS. Keep source/merge provenance in the lab. First local environment view: Aspire resources and graph, then Director preparation, then the operator's main app. Open its top-right Walkthrough. Record actual VS Code attachment, breakpoint hit, F10, Watch, tool call, model result, exchanges, continuation and detach. Repeat in Visual Studio 2026, identified as Windows-only. Both IDEs attach/detach through Director. Windows learners choose either; Mac learners use VS Code.
9. Explain the observed timeout honestly. Show a separate detached read-only retry and independent Hub/process checks.
10. Show the fixture reset. End with pause-and-do, expected evidence, recovery and code/documentation/AI investigation questions.

## Sources and visual treatment

Use the author's supplied `Developing Agentic Systems in .NET From Concept to Code.pptx`, exported locally to PDF. Original file is unchanged. Deck pages used: 2, 3, 6, 7, 10, 12, 13, 14, 15, 16 and 18. Slide 6 is freshly exported with the corrected title color. Introduce the city on slide 12, then its agentic layer on 13. Teach slide 14 before code/tool examples. Intermediate cards use the supplied signature background. Source hashes and the main commit are in `evidence/production-baseline.json`.

Use the companion book for rationale, current source for exact API/commands, and actual execution to resolve differences. Prior book/lecture analysis remains in `../book-alignment.md` and the archived foundation production plan. Conference-branded frames are not used in this edit. Do not narrate historical dates.

The rough cut combines original slides, real motion recordings and explicit evidence cards. Keep captured motion at normal speed, cut idle time, and label freeze holds. Remove permanent top/bottom strips. Use the full frame, with readable crops for Watch trees and code, and large custom-card type. Reuse relevant deck diagrams beside short card text. Narration is Microsoft Andrew Neural, a synthetic review voice; it does not imitate Alon. A simulated device and local services are distinguished from real remote model inference. The Foundry segment is a read-only visit to an existing hosted agent, not a deployment or cloud execution demonstration.

For every topic introduce the concept, show the concrete instance, and recap what it establishes.
The debugger evidence chain is question → selected tool/arguments → executing C# method →
returned Hub data → subsequent model request → answer. The exchange recorder exposes protocol
data, not private reasoning. Exclude the failed hex-editor/copy attempts from the VS Code edit.
Visual Studio's string visualizer opened outside the recorded monitor; its footage is not used.
Use the recorded Watch drill-down there and the actual full payload in VS Code. No fabricated UI.

## Local production and release boundary

The executable baseline is merged local main, `9d2f766cc19050495c52a601075df15870c4c0af`. All media stays local. No push, course publication, deployment or shared cloud settings change is part of this work. Before releasing clone instructions, publish the approved source separately and validate a clean clone of remote main. The lab makes this release gate explicit.

Review the opening, pacing, source legibility, Watch crops, pronunciation and exercise before producing Unit 01. The repair-first and edited-still pilots are archived, not current deliverables.

## Context and debugger revision

Spoken names are Microsoft Agent Framework or Agent Framework. Preserve the author's source
slide titles without pronouncing their acronym. The refreshed PDF provenance is in
`evidence/context-revision.json`. No edits were made to the supplied PowerPoint.

Separate the debugger evidence into tool breakpoint, first exchange request, Hub payload,
second exchange answer, and Director detach. The yellow marker must be visible before saying
we are inside the tool. Editorial yellow boxes use speech word timings and measured coordinates
on genuine frames. They point out assetId, Description metadata, the gateway, actual tool
request/arguments, reported/daylight/schedule/override booleans and the returned answer.
Freeze holds stay labeled. The model exchange recorder is protocol evidence, not reasoning.

The Mac screen-recording segment is deferred to a later lesson. Windows recordings demonstrate
the two editor choices; no remote Mac interaction is part of this revision.
