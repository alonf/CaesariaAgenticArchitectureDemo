# Agentic C# — Unit 00 shooting script

Timing follows the rendered edit. Real motion is at captured speed; cuts omit waits, and freeze holds are labeled. Synthetic narration is a review voice, not Alon's voice. Debug timeouts and the successful detached retry are separate requests.

## 00:00:00,000–00:01:01,733 | Agentic C#

**Screen:** Open on the new course title and Alon signature background. No city imagery yet.

**Source:** `card:title`; motion edits: `None`. Holds are labeled.

**Narration:** Welcome to Building C sharp Agentic Systems with Microsoft Agent Framework and Foundry, by Alon Fliess. Agentic C sharp for short. Agentic development is moving fast. It is easy to assemble a convincing demonstration before understanding the foundations that make its architecture dependable. This course gives you the background and the practical toolset to make informed decisions. You will learn to ask what belongs in deterministic code, where a language model helps, when an agent is justified, and when explicit orchestration is needed. You will run the code, inspect its behavior, and explain the tradeoffs. Our goal is engineering judgment you can use in your own applications. This course is for C sharp developers who need to architect, design, and implement new agentic systems, or add agentic capabilities to existing software. We assume familiarity with C sharp development, but no previous experience building agents. If you already know the APIs, the architectural decisions will help you understand when to use what, and why.

## 00:01:01,733–00:01:49,467 | The framework and the platform

**Screen:** Introduce MAF and Microsoft Foundry as complementary responsibilities, before the example domain.

**Source:** `card:platform`; motion edits: `None`. Holds are labeled.

**Narration:** We will go deep into Microsoft Agent Framework as our development framework and Microsoft Foundry as our platform. The framework gives our C sharp application a way to compose agents, tools, context, sessions, and workflows. The platform provides the model and agent services and the operational capabilities we choose to use. We will ask when to use each capability, how to integrate it, and why it belongs in the design. You will find many agent examples in Python. Here we focus on C sharp and dot net, connecting agentic development to the software engineering practices you already know. Running an agent locally can still involve a real remote model call. Hosting, conversational state, operational memory, and durable execution are separate decisions that we will examine carefully.

## 00:01:49,467–00:02:33,333 | Build a system around the agent

**Screen:** Trace deterministic code, model decisions, tools and governance in the composition slide.

**Source:** `deck:7`; motion edits: `None`. Holds are labeled.

**Narration:** Building an agentic system involves much more than constructing an agent object. The system needs reliable deterministic code underneath it. The agent needs knowledge to interpret a situation and tools that give it controlled ways to act. We decide which responsibilities are ordinary code, bounded model calls, a single agent, a planned workflow, or collaboration among agents. We also decide what policy admits, who can authorize consequential actions, and what evidence proves an outcome. These decisions affect testing, security, latency, cost, and recovery. Throughout the course, each new capability must earn its place by solving a concrete requirement. You will see both the mechanics and the reasoning behind them.

## 00:02:33,333–00:03:12,067 | Meet Alon Fliess

**Screen:** Use the supplied About Me slide, with its own logo and book imagery.

**Source:** `deck:2`; motion edits: `None`. Holds are labeled.

**Narration:** This course was developed by Alon Fliess, Microsoft Foundry MVP, Microsoft Regional Director, and chief technology officer at ZioNet. Alon brings more than forty years of hands-on experience, together with his work as an architect, author, and speaker. The companion book develops the architectural rationale; the code and labs let you test those ideas for yourself. Treat the lessons as an invitation to investigate. Pause, change a condition, inspect the result, and explain what you discovered. You do not need to accept a model answer or an instructor statement without evidence. Enjoy the journey, and make the lab your own.

## 00:03:12,067–00:04:01,667 | The questions behind the labs

**Screen:** Reveal the four learning questions. Explain the repeated lesson pattern.

**Source:** `card:outcomes`; motion edits: `None`. Holds are labeled.

**Narration:** Every lab starts with an observable outcome, not a package name. We prepare a known starting fixture and stage. We walk through the behavior, then connect it to a small piece of code. You pause and reproduce it, check the expected result, and investigate one failure path. Keep four questions in mind. What did the system observe? What did the agent infer? Who can authorize a change? And what evidence proves that change happened? A plausible answer is not an execution receipt. A remembered incident is a lead to investigate, not proof of today's cause. This distinction will connect all the lessons, from the first read-only tool to protected actions, collaboration, and recovery. The goal is not to memorize the Director's buttons. It is to know when a capability solves a real requirement and when simpler deterministic code is sufficient.

## 00:04:01,667–00:04:55,467 | Runtime agents and coding assistants

**Screen:** Use the two-agentic-systems slide; point to the runtime side.

**Source:** `deck:3`; motion edits: `None`. Holds are labeled.

**Narration:** There are two different uses of agents around this repository. A coding assistant can help us write, review, or change the software. The Operations Agent inside the running city application is a runtime component serving an operator. Our course is about that second system. Its inputs are an operator question, instructions, allowed tools, and relevant context. Its output may be an answer, a request for more information, or a proposed action. It operates inside an application with service boundaries, permissions, timeouts, and failure modes. Using a coding assistant to produce a tool does not make that tool's runtime behavior correct. We still inspect its contracts, verify what it reads and writes, and test the outcome. During debugging, we will follow the runtime agent's actual model calls and tool results, rather than treating an impressive chat answer as proof that the system works.

## 00:04:55,467–00:05:50,533 | What Microsoft Agent Framework provides

**Screen:** Show the agent model/instructions/capabilities diagram. Reserve exact signatures for current source.

**Source:** `deck:15`; motion edits: `None`. Holds are labeled.

**Narration:** Microsoft Agent Framework gives us application building blocks for agents and workflows. In this C sharp application, an agent combines a model connection, instructions, and capabilities such as callable tools. Sessions carry conversational context between related interactions. Context providers can add the information a request needs. Workflow code can coordinate steps and explicit gates. These are useful building blocks, but they do not decide our domain architecture for us. We choose which tools are available at each stage, how those tools cross service boundaries, which actions require approval, and how outcomes are verified. The first agent in this course has one narrow read tool. That small starting point is intentional: it lets us see the complete model and tool loop without hiding the responsibility of each component behind a large toolbox. Later lessons add capabilities only when the incident gives us a reason to add them.

## 00:05:50,533–00:06:39,533 | Choose the smallest sufficient mechanism

**Screen:** Explain the decision map in order: code, model call, agent, workflow and multiple agents.

**Source:** `deck:6`; motion edits: `None`. Holds are labeled.

**Narration:** Start with deterministic code when the rules and sequence are known. Comparing a reported lamp state with its schedule does not require a language model. A direct model call is useful when a bounded interpretation or generation task is enough. An agent becomes useful when the next information-gathering step depends on what it discovers and it must choose among permitted tools. A workflow is useful when required steps and gates must be explicit even though individual steps may use an agent. Multiple agents become useful when there are real responsibility, permission, or context boundaries. They are not automatically an improvement over one well-designed agent. Ask what the added boundary buys you and what new failure modes it introduces. We will revisit this decision map throughout the course. Learning when not to add an agent is part of learning to build an agentic system well.

## 00:06:39,533–00:07:25,533 | Use the architecture skills

**Screen:** Show Alon's architecture skills slide and github.com/alonf/agentic-architecture-skills.

**Source:** `deck:10`; motion edits: `None`. Holds are labeled.

**Narration:** Alon's agentic architecture skills repository turns this decision map into a practical design aid. The architecture router helps decompose responsibilities and choose the least autonomous mechanism that meets the requirement. The Microsoft Agent Framework mapping skill then translates an agreed architecture into framework, hosting, governance, and assurance decisions. Use these skills with a coding assistant to challenge your design, not simply to generate more agents. Their advice still needs review, tests, and enforcement in your application. In the lab exercise, bring one responsibility from your own system, ask for a recommendation, and verify the reasoning against the code and current documentation. The repository link is on screen and in the companion lab.

## 00:07:25,533–00:08:21,733 | Find your way around the repository

**Screen:** GitHub orientation, then a readable close-up of the actual local project tree.

**Source:** `card:repository`; motion edits: `[['orientation-repo', 2, 18, None], ['orientation-repo', 48, 8, None], ['orientation-code', 24, 22, [0, 70, 1280, 720]]]`. Holds are labeled.

**Narration:** Before exploring the city, let us connect the moving parts. A repository is more than the agent class: it contains the executable system, its contracts, tests, and instructions for running it. Open the Caesarea Agentic Architecture Demo on GitHub. The README introduces the system and maps its cumulative stages. Main contains the implemented capabilities, selected at runtime. We do not maintain a separate application for each lesson. In the local solution, Apps contains the two user interfaces. Services contains the deterministic services and agent services. Each API boundary has its own Contracts project. The AppHost starts the local system, Tests verifies behavior, and docs explains the stage decisions. Keep the README and the companion lab nearby. The companion lab records the exact source commit and prerequisites. You now have a map from a visible component to the place where its implementation lives.

## 00:08:21,733–00:09:22,000 | Locate the project and its hosted agent

**Screen:** Actual existing Foundry project, agent list and loaded details. Omit the loading-only interval.

**Source:** `card:platform`; motion edits: `[['orientation-foundry', 2, 10, None], ['orientation-foundry', 28, 8, [250, 65, 1600, 900]], ['orientation-foundry', 60, 12, [250, 65, 1600, 900]]]`. Holds are labeled.

**Narration:** The platform is the next moving part. A Foundry project supplies the environment in which model and agent resources are organized. Here is this solution's existing project, caesarea dev. Open Agents. The list contains caesarea operations, with kind Hosted and status Running. Its details identify an active version, identity, and Responses endpoint. This is a real deployed agent, not a slide illustration. This visit is read-only: we are locating an existing deployment, not deploying one. Keep two execution paths distinct. Our upcoming debugger exercise runs the Operations Agent locally and calls remote model inference. The hosted agent runs in Foundry and uses the cloud Energy Hub; the local Director does not reset that cloud city. Later hosting lessons will explain its composition and identity boundaries. For now, recognize where the platform lives and how to locate an agent. You do not need to create a hosted agent to complete the local introductory exercise.

## 00:09:22,000–00:10:32,800 | Caesarea is a system of systems

**Screen:** Show slide 12 first. Point to city systems, domain Hubs and the deterministic operational core before discussing the agentic layer.

**Source:** `deck:12`; motion edits: `None`. Holds are labeled.

**Narration:** Our example is Caesarea, a smart city. The city is our means of learning agentic architecture; the course goal is the engineering you can transfer to another domain. A city is not one application with one database. Energy, security, mobility, maintenance, and other operational domains already have systems, owners, and policies. They may use different vendors and different representations of the same physical place. Our architecture connects these systems without pretending that one model owns them all. Domain Hubs provide focused interfaces and normalize the operational information that their domain is responsible for. A city operator can ask a cross-domain question while each domain still controls what it discloses and what commands it accepts. The diagram shows the architectural breadth. The executable lab deliberately implements a smaller slice: a lamp, a simulator, the Energy Hub, Command Center, and agent services, with additional domain capabilities introduced as the stages grow. For example, the main UI explicitly labels Mobility as not connected. We will always distinguish the architectural idea from behavior this checkout actually demonstrates.

## 00:10:32,800–00:11:28,067 | The deterministic core

**Screen:** Trace simulator → Energy Hub → Command Center; then locate the agent service beside that path.

**Source:** `deck:12`; motion edits: `None`. Holds are labeled.

**Narration:** At the bottom of our running slice, the Smart Pole simulator stands in for a device controller. The Energy Hub exposes the operational twin and the permitted energy commands. Command Center assembles the operator's view and manages its own incident responsibilities. These services form the deterministic core. They continue to own operational facts when we enable an agent. The agent layer uses focused endpoints and tools instead of reaching around the Hub into its internals. That separation has practical value. We can reason about a command's validation independently of the model's wording. We can correlate a request across HTTP boundaries. We can test stale-state rejection and device failure without hoping that a prompt will enforce them. In a system of systems, integration and authority are software contracts. The model can help decide what to investigate, but those contracts still determine what the application is allowed to do.

## 00:11:28,067–00:12:24,000 | What the operational twin means

**Screen:** Show reported/desired/schedule/receipt diagram. Explain the simulator boundary.

**Source:** `card:twin`; motion edits: `None`. Holds are labeled.

**Narration:** An operational digital twin is a representation of an asset with explicit state and confirmation semantics. Reported state tells us what the controller has reported. Desired state expresses a requested target. The schedule expresses a rule for expected operation, and an override can explain why operation differs from that rule. Health tells us something about the controller's condition. Revisions help detect that state changed between observation and action. A command receipt records the progress and outcome of a particular request. These values answer different questions. Setting a desired target does not prove that a device acknowledged it. A successful user-interface message alone does not establish the authoritative state. In this lab, the device is simulated, so the strongest demonstrated claim is a confirmed transition through the simulator and Hub. That is valuable evidence for the software path, while remaining distinct from delivery to real equipment.

## 00:12:24,000–00:13:16,933 | Adding the agentic layer

**Screen:** Overlay observe → infer → request action → verify on the deterministic architecture.

**Source:** `deck:13`; motion edits: `None`. Holds are labeled.

**Narration:** Now we can add the agentic layer without moving the source of truth. The Operations Agent observes current state through a tool. It may retrieve evidence, infer a likely explanation, recommend a next step, or request a bounded action. The model chooses among the capabilities the application admits. The runtime invokes the selected tool, and the domain service still applies its rules. When an action completes, the system must verify the receipt and resulting state. This is why observation, inference, authority, and verification remain separate in our lesson structure. The model's ability to keep a conversation does not make it a digital twin. A session can remember what the operator said; a Hub tells us what the operational system currently reports. Likewise, a remembered case can suggest where to look, but current evidence and policy must decide whether the same explanation applies today.

## 00:13:16,933–00:14:58,867 | Context Engineering: one request, several steps

**Screen:** Trace slide 14 from context inputs to model, deterministic execution and the return arrow. Highlight each region with its explanation.

**Source:** `deck:14`; motion edits: `None`. Holds are labeled.

**Narration:** Before reading the first tool example, we need to understand the execution loop. This is context engineering. Our code and Microsoft Agent Framework build the context for each model call. On the left are instructions, the user request, conversation history, relevant knowledge, available tool definitions, previous tool results, and any memory we choose to supply. We select what is relevant and permitted; we do not simply send everything. The model receives that context and decides what to produce next. It may answer, ask the caller for more information, or request a tool. A tool request names a capability and supplies arguments. It is not the execution itself. On the right, the framework and our application invoke admitted deterministic code. That code can read an API, retrieve knowledge, perform an authorized action, or call another agent when we expose that capability. Each resource still enforces its own access and business rules. Follow the bottom arrow. The tool result becomes part of the updated context for another model call. The model can now use the new evidence, request another step, or produce its answer. One call from our application to the agent can therefore involve several model calls, tools, and possibly connected agents. Normally the answer returns when the agent has enough information or needs input from the caller. Errors, cancellation, configured limits, and approval gates can also interrupt the run; success is not guaranteed. The engineering work is both the context and this controlled execution loop. In the debugger, we will observe one concrete case: the first model exchange requests a state read, and the second uses the returned facts to answer.

## 00:14:58,867–00:15:34,267 | Use the Microsoft Learn starting point

**Screen:** Show Microsoft Learn Your First Agent with C# selected, then Add Tools. Explain documentation navigation before signatures.

**Source:** `deck:15`; motion edits: `[('orientation-learn', 2, 17, None), ('orientation-learn', 38, 15, None)]`. Holds are labeled.

**Narration:** For framework concepts and API examples, use Microsoft Learn. Start in the Agent Framework get-started sequence and select C sharp. Your First Agent introduces construction and invocation; Add Tools adds a callable function. Use this sequence to isolate a framework idea before looking at our larger application. Documentation and packages evolve, so compare a sample with the versions in your checkout. The companion lab links these pages. We now have two complementary references: the repository explains this system, while Learn explains the framework building blocks.

## 00:15:34,267–00:16:24,400 | The smallest agent call in C sharp

**Screen:** Read the short construction example on slide 16. Explain invocation verbally; inspect the real RunAsync later.

**Source:** `deck:16`; motion edits: `None`. Holds are labeled.

**Narration:** Now start with the small construction example in the slides. An AI Agent combines a model, instructions, and capabilities. The project client is supplied by our application. As AI Agent selects the model and gives this agent a name and instructions. Notice the instruction to use evidence and never invent operational facts. That expresses the intended behavior; validation and permissions still belong in code. The calling pattern is ordinary asynchronous C sharp: pass the question to Run Async, await the response, and return the result to the caller. This slide isolates construction, while the debugger will show the actual invocation. Remember the context engineering diagram: awaiting one agent call does not mean there was only one model call. The framework can run the tool loop inside it. We will add that capability next.

## 00:16:24,400–00:17:35,667 | The model and tool loop

**Screen:** Use slide 18 short function and registration code before the real implementation. Point to Description, AIFunctionFactory.Create and tools registration.

**Source:** `deck:18`; motion edits: `None`. Holds are labeled.

**Narration:** A language model cannot inspect a streetlight by itself. We give it a focused function that obtains the state from the Energy Hub. On this slide, Get Streetlight State Async is an ordinary C sharp method accepting an asset identifier. The Description attribute explains the function to the model. In the real implementation, the asset parameter also has a Description explaining its meaning and giving L four seventeen as an example. These descriptions help form the tool schema. They do not register the tool by themselves. AI Function Factory dot Create wraps the method as a callable function, and adding it to the agent's tools makes it available. The class is an ordinary C sharp class; this local function path needs no special tool-class attribute. The model can request the function by name with arguments, and the framework invokes the registered method subject to our controls. That method calls the Hub, and its returned data feeds the next model request, exactly as in Context Engineering. The tool name and metadata guide selection; deterministic code performs the work. We will keep this teaching pattern in later lessons: a short slide example first, then the real implementation and its behavior.

## 00:17:35,667–00:18:35,733 | How the course grows

**Screen:** Reveal four groups of learning needs, not seventeen feature boxes.

**Source:** `card:journey`; motion edits: `None`. Holds are labeled.

**Narration:** The labs grow along four practical needs. First, investigate with the right context. We begin with current state, then study sessions, retrieved knowledge, remembered cases, skills, and remote tool boundaries. Second, control consequential action. We separate a tool asking for missing input, a workflow stopping at a planned gate, and the runtime intercepting a protected tool selected by the model. Those are different mechanisms with different guarantees. Third, collaborate across responsibility boundaries, including specialist agents and delegation. Fourth, operate and improve the system: hosting, durable recovery, evaluation, and optional cloud capabilities. Each step asks why this capability belongs here, when it is appropriate, how the code implements it, and how a learner proves it worked. We will not imply that ordinary in-memory approvals survive a restart. Cloud-only and preview paths will be labeled and kept optional when the local lesson does not require them.

## 00:18:35,733–00:19:39,600 | Prepare a reproducible checkout

**Screen:** Show copyable clone/build commands from lab.md, then the exact baseline hash without date comparisons.

**Source:** `card:setup`; motion edits: `None`. Holds are labeled.

**Narration:** The lab code is dot net eleven code. Using dot net ten L T S with Microsoft Agent Framework follows the same concepts and development patterns. This particular checkout targets eleven, so use its required SDK to reproduce the lab. The companion lab records the exact commit and prerequisites. Clone the repository and open its root folder. Read global dot json, select the SDK, restore, build, and run the test project. Open this same folder in your preferred editor so the source matches the running binaries. Install the repository's VS Code attach extension, or on Windows build the Visual Studio helper. For the AI exercises, use an existing Foundry project and model deployment that your account can invoke. Set the local environment variables shown in the lab, then start the AppHost from that shell. Keep the shell open. The services run locally, with remote model inference. Complete sign-in before starting and keep private tokens out of recordings. The lab provides copyable steps and checks for each prerequisite.

## 00:19:39,600–00:20:29,200 | Aspire is our starting point

**Screen:** Show actual running resources; use this run's token off camera. Point to the two web resources and Operations Agent.

**Source:** `capture:aspire-resources`; motion edits: `[('aspire-live', 0, 17, None)]`. Holds are labeled.

**Narration:** Start the AppHost from the configured shell using the direct command in the lab, and keep that shell running. Open the private dashboard link printed by this run. Aspire is the first live environment screen because it shows the processes that make up our system. Find the simulator, Energy Hub, Command Center API, Operations Agent, and the two web applications. Check their status, then use their endpoint links rather than memorizing ports. A running process is a useful first checkpoint, but it does not prove that the model endpoint or permissions work. When a request fails, select the relevant resource and inspect its logs and traces. The request correlation helps connect activity across boundaries. Starting here also avoids the common mistake of opening an old browser tab against an application instance that is no longer the one you are debugging.

## 00:20:29,200–00:21:19,400 | Read the resource graph

**Screen:** Show real graph, then identify the operational path and the agent's dependency on the Hub.

**Source:** `capture:aspire-graph`; motion edits: `[('aspire-live', 17, 18, None)]`. Holds are labeled.

**Narration:** The Aspire graph makes the architecture visible as running services. Follow the application's dependencies and compare them with the slide diagram. The operator uses Command Center. The Operations Agent reaches domain information through its service clients and tools. The Energy Hub connects to the simulated device. These process boundaries matter when you choose a debugger target. Attaching to Command Center Web will not automatically stop inside the Operations Agent service. Likewise, an agent-side breakpoint does not pause every service in the city. We will attach to the process that owns the code we want to inspect and leave the rest of the environment running. Before moving on, locate the two web application links. One is the Director used to prepare the lesson, and the other is the main operator application where we will ask the question and inspect the result.

## 00:21:19,400–00:22:04,333 | Director prepares the starting fixture

**Screen:** Show actual Investigation Agent preparation preview, then ready state. Explain scenario versus stage.

**Source:** `capture:director-preview`; motion edits: `[['director-live', 10, 1, None, 15.475000000000001], ['director-live', 55, 2, [310, 180, 1300, 732], 0.0], ['director-live', 60, 0.2, [1120, 630, 800, 450], 7.175000000000001], ['director-live', 65, 6, None, 0]]`. Holds are labeled.

**Narration:** The Demo Director prepares the teaching environment. Read the first scenario card: Lights On Reported by a Client. A scenario creates operational conditions; a capability stage controls what the application can do. The daylight fixture is already applied in this recording. We select Investigation Agent and inspect the preparation review. This review changes the stage only. It is not a repair and it is not a new investigation. Confirm the selection, then check Now playing and the readiness message. If you repaired the lamp in an earlier run, use Apply again to reset this fixture. Command Center is where we will ask the question and observe the result. Keep those two responsibilities separate: Director prepares; the main application demonstrates.

## 00:22:04,333–00:23:10,800 | Command Center is the operator's application

**Screen:** Show main UI; point to L-417, reported/schedule/override, agent question, and deterministic action without clicking repair.

**Source:** `capture:command-center`; motion edits: `[('director-live', 76, 8, None), ('retry-live', 39, 13, (640, 0, 1280, 720))]`. Holds are labeled.

**Narration:** Now switch to the main Command Center. Open Walkthrough at the upper right. Read its starting conditions, numbered actions, code references, and the point of the demonstration. This is your in-app guide throughout the labs. Close the panel when you need the agent controls. The map identifies L four seventeen at North Promenade. The operational panel reports that the lamp is on, while the daylight schedule expects it to be off. The override is set and the controller is healthy. These are observations from the deterministic system. They do not yet prove why someone left the override in place. The agent panel asks a deliberately small question: is streetlight L four seventeen on? We will use that question to learn the debugging workflow. Leave Restore Scheduled Mode alone for now; repair is the first lab after onboarding. Notice that the operator workflow and the agent panel coexist. Enabling the agent did not replace the core. Before the debugger exercise, read the Energy Hub endpoint using the companion lab's command and save the process ID. Those checkpoints let us verify both operational state and detach behavior later.

## 00:23:10,800–00:24:02,667 | Attach VS Code

**Screen:** Show arming and attached captures. Explain attach versus launch and one-shot teaching stops.

**Source:** `capture:vscode-attached`; motion edits: `[('vscode-values', 4, 30, (320, 330, 1300, 732))]`. Holds are labeled.

**Narration:** Open the same checkout in Visual Studio Code. In the Director, expand Advanced controls and Demo Breakpoints. Verify that the attach extension is ready. Attach the Operations Agent row, then arm Agent Creation and Function Tool. The attached status matters: arming a checkbox by itself does not connect a debugger. These teaching stops are consumed on the next matching execution. We will also set an ordinary breakpoint in Foundry Operations Agent, immediately after the awaited Run Async call, so we can inspect its returned result. This is an attach session to an existing process started by Aspire. We are not launching a second copy of the application. Prepare the expressions you want to inspect before asking the question. Keep the live pauses short; we can freeze the edited video to explain the source without holding a real HTTP request open for several minutes.

## 00:24:02,667–00:24:54,600 | Stop and single-step in C sharp

**Screen:** Show actual breakpoint and F10 transition from Pause to AsAIAgent. Highlight question, stage and tools.

**Source:** `capture:vscode-question`; motion edits: `[('vscode-values', 81, 8, None), ('vscode-values', 90, 18, None)]`. Holds are labeled.

**Narration:** Click Ask agent in Command Center. Visual Studio Code stops at the Agent Creation teaching point. The yellow execution marker shows where this request is paused. Press F ten to step over to the next statement. In the captured request, the question variable is exactly: is streetlight L four seventeen on? The stage has already determined which capabilities were composed. Read the agent construction code: it supplies the model, instructions, tools, and any applicable context providers. This is the connection between the framework diagram and ordinary application code. We can inspect the request and admitted tools before allowing the model call to proceed. Press F five to continue. Because Function Tool is armed, the next relevant stop will show the C sharp method that the model selected, rather than asking us to infer tool execution from the final answer alone.

## 00:24:54,600–00:25:54,000 | Inspect the tool boundary

**Screen:** Start at the visible EnergyTools breakpoint, source 120s. Highlight Locals assetId, method and parameter Description, then gateway call. Freeze holds labeled.

**Source:** `capture:vscode-tool`; motion edits: `[('vscode-values', 120, 6, None)]`. Holds are labeled.

**Narration:** The yellow execution marker shows that the breakpoint has been reached. We are now in Energy Tools, inside Get Streetlight State Async. In Locals, the asset identifier is L four seventeen. Watch expressions from the caller are out of scope here; use this method's local values. Look at the Description attribute above the method, then the Description on the asset parameter. They provide the purpose and argument meaning we discussed on the short code slide. The enclosing Energy Tools class is ordinary C sharp. Registration through AI Function Factory happens in the capability composition code. Here we see execution: validate the input, get the correlation identifier, log the invocation, and call the Energy read gateway. The gateway reads through the Hub boundary. This is the deterministic part of the loop. The model selected a request; the framework brought us to this method, with these arguments. Continue with F five to the breakpoint after Run Async, where we can inspect the completed exchanges.

## 00:25:54,000–00:26:34,733 | Inspect AI results and model exchanges

**Screen:** Show post-RunAsync Watch then drill into Exchanges[0].Received. Hold the actual expanded tool request and highlight its name and arguments.

**Source:** `capture:vscode-exchanges`; motion edits: `[('vscode-values', 145, 2, None), ('vscode-values', 169, 5, None), ('vscode-values', 187, 7.5, (0, 80, 1450, 816))]`. Holds are labeled.

**Narration:** We have reached the breakpoint after Run Async. The recorded exchanges let us reconstruct what happened inside that one awaited call. There are two model round trips in this request. Expand the first exchange, item zero, then Received and its result. Step one: the model requested the get streetlight state tool, with asset ID L four seventeen. The highlighted row is the actual request and arguments. The framework dispatched the function we just inspected. This is the selection side of the Context Engineering loop. We can observe a tool request and its execution without claiming to see the model's private reasoning. The content-type placeholder is not a reasoning transcript.

## 00:26:34,733–00:27:15,467 | The Hub result becomes the next context

**Screen:** Show actual Debug Console evaluation of Exchanges[1].Sent[0], source 322–329.8. Highlight actual booleans with narration.

**Source:** `capture:vscode-exchanges`; motion edits: `[('vscode-values', 322, 7.8, None)]`. Holds are labeled.

**Narration:** Now inspect what went into the second model call. In the Debug Console, evaluate the first Sent string of exchange one. The visible payload is the tool result sent back as context. Reported Is On is true: the device reports that the lamp is on. Is Daylight is true. The expected scheduled state is false: the schedule expects the lamp to be off. Manual Override is true. Those are separate observations, and together they explain why this incident deserves investigation. They came through the Energy Hub, not from the model's memory. This is the return arrow on our Context Engineering slide: deterministic execution contributes evidence to the next model interaction.

## 00:27:15,467–00:27:57,267 | The second exchange produces the answer

**Screen:** Show expanded second exchange Sent and Received plus response.Text. Highlight received answer. Then detach in a separate scene.

**Source:** `capture:vscode-exchanges`; motion edits: `[('vscode-values', 210, 12, (0, 80, 1450, 816))]`. Holds are labeled.

**Narration:** Step two: the model receives that evidence and produces the answer. In the second exchange, compare Sent, containing the tool result, with Received, summarized on the exchange row as the statement that L four seventeen is currently reported on. The response text agrees. We can now explain both parts of the behavior: an agentic choice to request a useful capability, followed by deterministic execution, followed by a model answer grounded in the returned data. That is what the debugger adds beyond seeing a sentence in the browser. This recording has two model calls; another question could require a different number. An answer about the lamp does not mean we repaired it. We asked a read-only question.

## 00:27:57,267–00:28:14,867 | Detach VS Code through the Director

**Screen:** Show source 400–414 Director Detach VS Code and resulting status.

**Source:** `capture:vscode-attached`; motion edits: `[('vscode-values', 400, 14, (320, 330, 1300, 732))]`. Holds are labeled.

**Narration:** Finish the request and remove the ordinary breakpoint. Return to the Director and choose Detach VS Code. Check that the debugger is no longer attached. The service remains available. Use the Director for this lifecycle so the demo controls and the debugger status stay in agreement.

## 00:28:14,867–00:29:01,200 | A real timeout and recovery

**Screen:** Explain the recorded 100-second web timeout and distinguish the debugger take from the fresh detached retry.

**Source:** `card:recovery`; motion edits: `None`. Holds are labeled.

**Narration:** There is a useful failure case here. A long debugger pause can outlast the web client's one hundred second attempt timeout, even though the agent service relaxes its own budget when a debugger is attached. Our recording encountered this. The debugger returned a model answer, while the browser had already stopped waiting. That does not prove a deployment failure. Continue the paused request, remove ordinary breakpoints, detach, and verify the Hub. Then retry this read-only question as a fresh request. The later browser answer belongs to that fresh retry. For write tools, do not generalize this advice into blindly resending commands. First reconcile the receipt and current state. Pause the video to study the code; keep pauses in your live HTTP request short.

## 00:29:01,200–00:30:36,067 | Switch debuggers without restarting the service

**Screen:** Show the actual Visual Studio attach, breakpoint, F10, Watch values, result and detach. Windows only.

**Source:** `capture:vs-attached`; motion edits: `[('card:editors', 0, 16.5, None), ('vs-values', 134, 5.5, (320, 330, 1300, 732)), ('vs-values', 180, 10, (320, 330, 1300, 732)), ('vs-values', 236, 9.5, (250, 45, 1660, 934)), ('vs-values', 261, 4.5, (250, 45, 1660, 934)), ('vs-values', 293, 5, (250, 45, 1660, 934)), ('vs-values', 317, 6, (255, 625, 650, 366)), ('vs-values', 392, 14, (255, 625, 650, 366), 7), ('vs-values', 524, 14, (320, 340, 1300, 732))]`. Holds are labeled.

**Narration:** Choose the debugger that suits you. On a Mac, use VS Code. On Windows, either VS Code or Visual Studio twenty twenty-six is suitable. Visual Studio twenty twenty-six is Windows only. Both editors inspect the same C sharp request. First detach VS Code. In the Director, choose Attach Visual Studio twenty twenty-six on the Operations Agent row. Watch the status become attached. Arm Agent Creation and Function Tool, and set the ordinary breakpoint after Run Async. Ask the same read-only question in Command Center. The yellow marker shows the creation stop; Watch contains the operator question. F ten steps over a statement, and F five continues to the selected function. At Energy Tools, the inline asset ID is L four seventeen. Continue to the returned response. Watch now contains the answer and two exchanges. Expand Exchanges, item zero, then Received. This is the model response containing the tool request. Collapse that item and open item one, then Sent. This is the next model request containing the tool result. The tree gives you the path; the string visualizer can show longer values. In the VS Code take we inspected the full payload. These are observable messages, arguments, and data from the running request. Continue, remove the ordinary breakpoint, and click Detach Visual Studio twenty twenty-six in the Director. The row returns to debugger not attached. We have followed the same request through creation, tool invocation, and model response with the editor of our choice. The service remains running.

## 00:30:36,067–00:31:25,400 | Verify the outcome at the Hub

**Screen:** Show real detached retry answer and RAN trace. Pair with saved Hub and PID evidence in lab.

**Source:** `capture:read-answer`; motion edits: `[('retry-live', 10, 25, None), ('card:hub-checkpoint', 0, 23, None)]`. Holds are labeled.

**Narration:** The main application now shows the answer from the fresh read-only retry. The trace marks get streetlight state as ran. The Hub still reports that the lamp is on, and the operator has not issued a repair command. The Operations Agent process is still alive after detaching the debugger. These observations answer our four questions. The Hub supplied operational state. The model selected a read tool and formed an answer from its result. No authority to change the lamp was exercised in this onboarding request. And the fresh Hub read confirms that we did not silently turn the investigation into a repair. This is how the course will treat results: a UI message, a tool trace, and authoritative state each contribute evidence, but they answer different questions. Keep that habit when later labs introduce actions that can actually change the city.

## 00:31:25,400–00:32:26,200 | Pause, practice, and prepare Lab 01

**Screen:** Hold the exercise card; learner repeats an IDE cycle, checks Hub/PID, resets Deterministic fixture. Stop after Unit00 review.

**Source:** `card:practice`; motion edits: `[['card:practice', 0, 1, None, 31.424999999999997], ['reset-live', 15, 5, None, 3.125], ['reset-live', 52, 10, None, 0]]`. Holds are labeled.

**Narration:** Pause here and repeat one attach, stop, inspect, continue, and detach cycle without following the video. Use the companion lab's copyable commands to check the Hub and the process ID. Explain aloud where the operational truth lives, what the model selected, and why an answer did not repair the lamp. On Windows, you may repeat with the other IDE after the first is detached. On a Mac, complete the exercise in VS Code. If the user interface times out during a long pause, use the read-only recovery procedure we just demonstrated. When you are ready for Lab one, finish outstanding requests, clear the teaching stops and ordinary breakpoints, and choose Deterministic in the Director. Review and confirm the preparation preview, then verify the daylight anomaly again. A reset can change the revision, so discard old snapshots before the next exercise. You now have the architectural foundation and an inspectable environment. The next lab begins with the deterministic system and proves a repair through the Energy Hub.

## 00:32:26,200–00:33:17,867 | Investigate before the next lab

**Screen:** End on code, documentation and AI investigation questions. Stop after Unit 00 for review.

**Source:** `card:questions`; motion edits: `None`. Holds are labeled.

**Narration:** Before moving on, investigate three questions. First, trace the state read from Energy Tools to the Energy Hub endpoint. Which service owns the answer, and how is the correlation identifier propagated? Second, find the agent service and web client timeout policies. Why can one request produce a debugger result but no browser answer? Third, give an AI assistant a proposed responsibility and the agentic architecture skills. Ask whether it belongs in code, a model call, an agent, or a workflow. Challenge its recommendation with one failure case, then verify its claims in source and official documentation. Save your answer with a file location or source link and a reproducible observation. Every later lab ends with this kind of investigation. You now have the foundation, the tools, and a way to test what you learn. Enjoy building Agentic C sharp.
