# Lab 02 — first function tool

Synthetic narration for review. All execution footage is actual local execution; no fabricated results. Final timestamps are generated in output/timed-shooting-script.md.

## Beat 01 — From an operation to a tool

**Picture:** Deck12 then14, then actual app before question.

**Narration:** Welcome back to Agentic C sharp. In the previous lab we operated the deterministic system and verified a command at the Energy Hub. Today we add one narrow capability to an agent: read the state of a streetlight. By the end, you should be able to prove that a model selected a C sharp function, that our code called the Hub, and that the result returned to the model before the answer reached the operator. We are deliberately starting with a read. Choosing a useful function and authorizing an operational change are different design decisions. The lamp in this lab is simulated, but the HTTP calls, tool execution and model responses you will inspect are real.

## Beat 02 — Recall the context loop

**Picture:** Course slide14. Then first successful UI answer and capability trace.

**Narration:** Recall the context engineering picture from the introduction. Our application supplies instructions and a tool description to the Agent Framework. The model can respond with a request to call that tool instead of a final answer. The framework invokes the registered function and sends its result into a subsequent model exchange. Only then does the answer return to the caller. In this recorded request, that means one tool invocation over two model round trips. Those are observations, not a universal promise about every prompt or model. We will inspect the messages rather than guessing from the final sentence. This is when an agent adds value: the caller can express an intention in language while a focused deterministic boundary supplies the facts.

## Beat 03 — Prepare and ask

**Picture:** Actual Director setup and main app successful Ask. Display the real stage walkthrough.

**Narration:** Start the lab using the prerequisites and commands from Lab one. Keep Aspire running and use its links for Director and Command Center. In Director select Investigation Agent, and prepare Lights On Reported by a Client. The API calls this fixture Forgotten Override. Return to the main application and open Walkthrough. Confirm the starting facts: L four seventeen is reported on, it is daylight, the schedule expects off, and the controller is healthy. Then ask: Is streetlight L four seventeen on? The answer says it is currently reported on. Expand the capability trace. We can see get streetlight state, the L four seventeen argument, and the outcome marked ran. The deterministic panels are unchanged. The agent read the city; it did not repair it.

## Beat 04 — Attach at the tool boundary

**Picture:** Actual Director attach, main Ask, stopped EnergyTools. Only identify stopped code after breakpoint.

**Narration:** For the next request, open the Director advanced controls, arm Function Tool, and attach the Operations Agent using VS Code. These controls attach to the correct local service; they do not replace the service with a second instance. You can use VS Code on Windows or macOS. Visual Studio twenty twenty six is another choice on Windows. Return to Command Center and ask again. Now the breakpoint has stopped in Energy Tools. We can inspect the asset identifier and follow the narrow Hub gateway. This is ordinary C sharp code inside the agent loop. Keep pauses short when you want the browser request to remain active. We will also show what happened when this recording stayed paused too long.

## Beat 05 — Metadata and deterministic execution

**Picture:** Recorded EnergyTools44→50 and F11 EnergyReadGateway37. Read class/method descriptions and boundary.

**Narration:** Look at the Description attributes on the method and its asset identifier parameter. They supply meaningful metadata about the capability and the input. An attribute alone does not register a tool. Streetlight Tools Capability creates an AI function from the delegate and includes it in the tools available at this stage. The class is an ordinary service. In this local function example, it is not an MCP server registration. That distinction matters when we change the transport in a later lesson. Step into the Energy Read Gateway. It constructs a focused GET request, propagates the correlation identifier, and handles the HTTP result. The language model is not reading a database or modifying the digital twin directly. The Hub remains the authority for operational facts.

## Beat 06 — Read the two exchanges

**Picture:** Actual Watch drilldown, then Debug Console values. Highlight tool request, reportedIsOn and final answer.

**Narration:** We have now stopped after Run Async. First inspect question and response dot Text. Then expand the recorded model exchanges. There are two. The first received message is a tool call: get streetlight state with asset identifier L four seventeen. We read that exact value in the debug console to make it visible. Next inspect the second exchange's sent message. It is the tool result, including reported is on true, daylight true, scheduled state false, and manual override true. Finally inspect the second received message. This is the natural language answer. This recording exposes observable messages and tool results, not private model reasoning. The value of the debugger is that we can distinguish the agentic selection from deterministic execution, then see how execution becomes context for the next model call.

## Beat 07 — A pause can outlive its caller

**Picture:** Actual timeout UI, followed by separate successful fresh request. Clearly label separate request.

**Narration:** Here is a real failure from this session. We stayed in the debugger longer than the web caller's one hundred second timeout. The browser therefore reports a timeout even though we inspected a completed agent response. Do not edit those two facts into a fictional successful browser round trip. We continued the paused process, disabled the temporary breakpoint, and made this separate uninterrupted request. It answered successfully with one tool call over two model round trips. Then we detached through Director, leaving the service running. When a caller times out, inspect correlated logs and authoritative state before retrying a command. In this lesson the selected capability was read only, so neither request changed the lamp.

## Beat 08 — Pause and prove the boundary

**Picture:** Exercise card, Hub evidence file in companion, recap context slide14.

**Narration:** Pause and try it. Reapply the daylight fixture, ask the same question, and save the capability trace and correlation identifier. Read L four seventeen directly through the Energy Hub before and after. Expect reported on and manual override to remain true, with no new repair command. Attach using Director and inspect the first tool request and the second model exchange. Explain which values came from the Hub and which sentence came from the model. If the request fails before a tool runs, inspect Aspire logs. During preparation we found an existing placeholder model endpoint; restoring the configured project endpoint and deployment for the local process fixed it. Do not diagnose every agent error as a deployment failure. Your investigation questions are in the companion lab: where is the function registered, what information describes its parameter, and where is correlation propagated? We have added language driven selection around an existing boundary. Next, we will add conversational continuity and examine why a remembered answer is not a fresh observation.
