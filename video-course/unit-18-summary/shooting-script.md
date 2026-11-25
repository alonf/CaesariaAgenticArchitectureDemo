# Closing chapter — the journey and your next step

New synthetic review narration over explicitly retrospective course excerpts. No new live run or cloud deployment. Exact final timestamps are generated in output/timed-shooting-script.md.

## 01 — Course summary: look how far you have come

**Picture:** Open on architecture slide 12 (previously shown around 00:42) and introduce the purpose of the course summary. Hold the complete picture throughout the introduction. At “We began”, return to the earlier Lab 01 daylight anomaly, then the branded journey map.

**Narration:** Welcome to the course summary. Before we finish, let us step back and look at the complete picture. Throughout Agentic C sharp, we have explored how to architect, design and implement agentic systems using Microsoft Agent Framework and Microsoft Foundry. In this final chapter, we will connect the lessons, revisit the decisions behind the code, and turn what you have learned into a practical next step for your own system. We began with a light that was on during daylight. By the end, you could explain far more than whether it was on. You could identify the service that owns the facts, inspect the evidence behind an explanation, locate the decision that permits an action, and prove what actually happened. Let us retrace that journey using excerpts from our earlier labs. These are separate recorded experiments, with their own starting fixtures, not one uninterrupted run. The smart city gave us a concrete example. The skills you have developed apply to the C sharp systems you already own, and to the systems you will build next.

## 02 — Keep the useful system at the center

**Picture:** Slide 12 establishes the Hubs. Earlier Mac restore action, then genuine Hub JSON. End on the three-role card.

**Narration:** Our first architectural decision was to establish a useful deterministic system. The Hubs connect independent city systems and expose a controlled view of their state. The Energy Hub owns the lighting operation. In the first lab, the operator requested Restore Scheduled Mode. The service checked the operation, called the controller boundary, and recorded its outcome. The independent Hub read then showed the reported state and the command receipt. The lamp in our lab is simulated; the software request and verification are real. Keep that separation in your own design: the agent can help choose a useful next step, while domain code owns the rules and the facts that establish success.

## 03 — Understand the loop behind an answer

**Picture:** Slide 14, followed by the two debugger exchanges from the introduction. Match each excerpt to its spoken cue.

**Narration:** Then we added an investigation agent with Microsoft Agent Framework. Recall the context engineering diagram. Our code and the framework assemble the request, instructions, available tools and relevant context for the model. The model can request a tool; ordinary C sharp executes that capability, and the result becomes input to another model exchange. In the debugger, we inspected the selected function and its arguments. We then inspected the returned Hub facts and the answer built from them. Those are observable messages and results, not access to private model reasoning. One call from the application can contain several model and tool interactions. You can now debug that boundary instead of treating an answer as an unexplained string.

## 04 — Give context a meaning and a source

**Picture:** Earlier work-order evidence, then empty retrieval. Use the context card to distinguish the four roles.

**Narration:** The investigation became more useful when it could explain why the light was on. A retrieved work order supplied evidence with a source. When we removed that evidence, a good answer admitted what it could not establish. We also learned that different kinds of context have different jobs. A session carries the conversation. A remembered case supplies a lead to investigate. A skill supplies a procedure. None of those replaces a fresh authoritative read. When you design context for an agent, ask where each fact came from, whether it is current, and what it permits the agent to conclude. More context is useful only when its relevance and trust are understood.

## 05 — Put authority at an explicit boundary

**Picture:** Three control owners on a branded card, then the recorded protected-tool review and denial. The excerpt is a separate Controller Fault experiment.

**Narration:** As soon as an operation has consequences, choosing an action and authorizing it become separate responsibilities. We demonstrated three control points. An MCP tool requested input while it was running. A workflow paused at a gate we deliberately placed in its graph. A protected function requested approval after the model selected that tool. The visible protected-call review named the capability and proposed arguments. Denial meant the invocation did not execute. For the physical repair workflow, approval also had to match the state being changed. A stale decision was refused. The lesson is to place enforcement at the boundary that can actually prevent the side effect, and verify both the permitted and denied paths.

## 06 — Collaborate only when a boundary earns its place

**Picture:** Earlier specialist verdict and workforce peer card. End on the decision questions, not a feature list.

**Narration:** The next advance was knowing when another capability or agent adds value. MCP moved a focused capability across a service boundary. The security specialist contributed a bounded judgment from its own domain. The workforce peer exposed a shareable view of work, while private fields stayed outside the accessible tool result. These examples teach a design question: what knowledge, responsibility or execution boundary justifies this separation? Another agent introduces more communication, latency and failure paths. Use one when the responsibility deserves it. Keep discovery, identity, permission and the data projection explicit. A protocol connection does not by itself grant access to everything behind the endpoint.

## 07 — Make progress and failure observable

**Picture:** Earlier durable pending review, worker controls, then approval/Hub proof. Each cut follows a narration anchor; do not suggest scheduler failure was tested.

**Narration:** Operating an agentic system means knowing which work has really completed. The durable lesson made that distinction visible. We first inspected an actual pending approval. We interrupted only the local Functions worker, while the scheduler and city services remained available. After continuation, the original review still governed the decision. Approval was followed by a terminal result and an independent Hub check. This demonstrated that particular recovery boundary, not universal exactly-once execution. Remembering a conversation and resuming a business operation are separate requirements. Choose the hosting and persistence model from those requirements, and make pending, failed, completed and unknown outcomes distinguishable to the operator.

## 08 — Measure before you expand

**Picture:** Recorded ASSERT report, then explicit optional-preparation boundary and the engineering feedback loop.

**Narration:** Evaluation gave us a way to compare intended behavior with evidence. We read the requirement, checked the actual tools and outcomes, and kept a behavioral failure separate from an evaluation error. A small passing sample is a useful checkpoint, not a production guarantee. The optional Routines and Insights lessons prepared activation and an engineering feedback loop. They did not demonstrate successful routine dispatch or a retrieved generated Insight. Carry that honesty into your own work. Define representative cases, include missing evidence and dependency failures, inspect traces, and turn a discovered problem into a regression check. Then make a bounded change and measure again.

## 09 — Choose the smallest sufficient design

**Picture:** Return to the Agentic Decision Map slide. Display the architecture-skills repository on the branded resource card.

**Narration:** You now have a decision process, not just a collection of APIs. Use deterministic code when the steps and rules are known. Consider a bounded model operation when interpretation or generation is the missing capability. Use an agent when the next useful step depends on what it discovers. Introduce an explicit workflow when the path needs planned control, and additional agents when separate responsibilities justify them. Microsoft Agent Framework supplies development building blocks; Microsoft Foundry supplies platform capabilities you select for the solution. The Agentic Architecture Skills repository can help structure that discussion with a coding assistant. Review the reasoning and the resulting design yourself. The requirement should explain every added mechanism.

## 10 — Your first path: extend a system you own

**Picture:** New action card for an existing application, then earlier C# tool implementation as the narrow-boundary example.

**Narration:** If you already own a working application, start with one difficult question its users ask. Keep the existing domain services as the authority. Expose one narrow read capability through its supported API and let an agent investigate without changing state. Compare the answer with a direct service read. Add selected knowledge only when the question requires it. When you later introduce an action, give it a focused contract, validation, authorization, a receipt and a defined failure path. The C sharp function we inspected is a useful pattern: a small capability over a controlled service boundary. Your first milestone is one demonstrably useful investigation, with evidence, inside the application people already use.

## 11 — Your second path: design a new system

**Picture:** New architecture card with a five-step vertical sequence and a slide-12 thumbnail. No new cloud execution.

**Narration:** For a new agentic system, begin with the users, the decisions they need to make, and the outcomes they must be able to trust. Identify the systems of record and draw the domain boundaries before deciding how many agents to create. Implement the deterministic operations and their contracts. Then add the context and capabilities that support one meaningful end-to-end task in C sharp. Decide which steps need planned orchestration, human authority, durable progress or separate reasoning. Choose identity, deployment and observation deliberately. The first useful version should be small enough to inspect completely. Expand it when a new requirement shows why the existing design is insufficient, and bring the evaluation cases forward with it.

## 12 — Pause and write your first design brief

**Picture:** Capstone card. The companion brief supplies copyable fields, success criteria and investigation questions.

**Narration:** Pause here and choose one of those two paths. Write a one-page brief for a real use case. Name the user outcome, the authoritative source, the agent responsibility, the permission boundary and the evidence of completion. Sketch one successful request and one failure. For an existing system, identify the first read-only capability you can expose. For a new system, identify the first deterministic operation you must build. Then challenge your proposal: why does it need an agent, what happens when evidence is missing, and how will you know a side effect happened? Use the companion worksheet, inspect the lab code, consult the documentation, or discuss your design with an AI assistant. Keep ownership of the final engineering decision.

## 13 — Build, explain, and share the journey

**Picture:** Brief earlier repaired-city view, then the branded closing card with author, course name and sharing invitation.

**Narration:** The progress is visible in how you can now explain this simple light. You can trace observation to evidence, distinguish an agent request from an authorized operation, and prove the outcome through the owning service. Take that discipline into your next C sharp project. Add an agentic layer where it makes an existing system more useful, or architect, design and implement a new system around clear responsibilities. If this course helped you, share Agentic C sharp with a colleague or your team. Invite them to run a lab with you and compare design decisions, not just answers. Thank you for joining me on Building C sharp Agentic Systems with Agent Framework and Foundry. I am Alon Fliess. Enjoy the journey, and build something you can explain.
