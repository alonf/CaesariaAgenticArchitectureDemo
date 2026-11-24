# Unit 00 revision — foundations, environment, and debugging

This replaces the repair-first introduction. The earlier rough cut remains an archived pilot;
its deterministic repair belongs in Lab 01. Complete this onboarding unit before teaching labs.
The latest author direction supersedes the earlier 8–12 minute limit and deferred-debugger plan.
Allow roughly 20–30 minutes with chapters; do not accelerate the IDE actions to fit the old limit.

## Learning contract

After this unit the learner can explain why Caesarea combines a deterministic operational core
with an agentic layer; locate the owner of operational state; choose between code, a model call,
an agent and a workflow; start the lab; prepare its stage and fixture; and inspect a real agent
request in both VS Code and Visual Studio 2026 without terminating its service.

## Required order

1. **Purpose and course promise.** Build, inspect, justify and test an agentic .NET system.
   Each later capability answers a requirement: why it is needed, when it is appropriate,
   how it is implemented, and what evidence proves it worked.
2. **What Microsoft Agent Framework does.** Model, instructions, capabilities, sessions,
   context providers, tool invocation and workflows. Explain the model/tool loop and distinguish
   a runtime agent from the coding assistant used to build the repository.
3. **System of systems.** Independently owned domains and vendor systems; domain Hubs normalize
   access and own operational twins, policies, commands and events. Distinguish architecture
   breadth from the services actually implemented in this lab.
4. **Deterministic core.** Desired versus reported state, device acknowledgement, revision,
   correlation, incident ownership, and the limits of a simulated device. A twin is an operational
   representation with freshness/confirmation semantics, not a model's memory.
5. **Agentic layer.** Observe, infer, retrieve evidence, recommend and request bounded action.
   Sessions are conversational context; knowledge can support a claim; recalled incidents are
   leads; skills describe procedures. None replaces authoritative current Hub state.
6. **Course journey.** Investigate with evidence; control consequential action; collaborate across
   boundaries; host, recover, evaluate and improve. Introduce how/why/when, not an API checklist.
7. **Start the environment.** Pinned checkout and SDK, model prerequisite for the debugging exercise,
   direct AppHost launch. First live environment screen is **Aspire**: resources, links, logs/traces.
8. **Two UIs.** Open Director and Command Center from Aspire. Director prepares stage/fixture and
   arms debugger stops. The main app performs the operator interaction and shows results.
9. **VS Code.** Open the same checkout; verify C# and demo attach extension; select VS Code in
   Director, attach OperationsAgent.Api, arm Agent Creation and Function Tool, ask the main app,
   stop on source, step, inspect arguments and Hub result, continue through the model call,
   inspect `response.Text` and `modelFlightRecorder.Exchanges`, finish the request, detach.
10. **Visual Studio 2026.** Open that same solution; attach only after VS Code is detached;
    repeat a real read-only request, single-step, inspect local variables and model exchanges,
    continue to the result, detach, and prove the same process remains alive.
11. **Ready for labs.** Learner repeats one attach/inspect/continue/detach cycle, verifies the
    read-only question did not repair L-417, clears armed stops and resets the first lab fixture.

## Recording requirements

- Use present-tense teaching. No narration comparing material by historical dates. Exact source
  hashes/commit versions remain in production provenance and the setup lab.
- Show actual UI state and actual debugger values; never fabricate model output or a breakpoint.
- Explain that local agent execution still calls an existing Foundry model for inference. No new
  cloud deployment or shared configuration changes are part of this task.
- Inspect externally visible model messages/tool calls/results, not hidden model reasoning.
- Capture an attach, stop, step, result, resume and detach in **each** IDE. Static source cards
  cannot satisfy this requirement. Record API process IDs before/after to prove detach kept it alive.
- Do not leave a paused request or attached debugger at the end. Do not begin later labs yet.

## Source grounding

The supplied recording's downloaded VTT identifies H08 beginning at approximately **01:35:52**.
Transcript recognition errors such as “MVP” for MCP and “math” for MAF must be corrected against
the slides and code, never copied into the lesson.

| Recording interval | Teaching material |
|---|---|
| 00:40:18–00:47:26 | City as a system of systems; judgment versus authority; domain Hubs |
| 01:35:52–01:38:56 | H08 introduction, runtime agents versus coding agents, learning goals |
| 01:40:24–01:42:49 | Code/model/agent/multi-agent decision; responsibility and permission boundaries |
| 01:42:54–01:45:19 | Deterministic services and agentic layer; domain Hubs and governance |
| 01:46:25–01:50:46 | L-417; state versus cause; context and the model/tool loop |
| 01:52:50–01:56:23 | Aspire, two UIs, stage switch, attach, breakpoint, model exchanges, detach |
| 01:59:27–02:14:24 | Session, knowledge, memory, skills and MCP rationale |

Use the companion book's architecture/MAF/context/lab chapters for rationale and actual compiled
code for API signatures. The recording is a teaching source, not evidence that the current local
debugger rehearsal succeeded. Source VTT remains local under the ignored `.build` folder.
