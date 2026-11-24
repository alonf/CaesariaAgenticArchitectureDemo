# Agentic C# — Unit 00 shooting script

Timing follows the rendered edit. Real motion is at captured speed; cuts omit waits, and freeze holds are labeled. Synthetic narration is a review voice, not Alon’s voice. Debug timeouts and the successful detached retry are separate requests.

## 00:00:00,000–00:00:38,867 | Agentic C#

**Screen:** Open on the new course title and Alon signature background. No city imagery yet.

**Source:** `card:title`; motion edits: `None`. Holds are labeled.

**Narration:** Welcome to Building C sharp Agentic Systems with MAF and Foundry, by Alon Fliess. Agentic C sharp for short. Agentic development is moving fast. It is easy to assemble a convincing demonstration before understanding the foundations that make its architecture dependable. This course gives you the background and the practical toolset to make informed decisions. You will learn to ask what belongs in deterministic code, where a language model helps, when an agent is justified, and when explicit orchestration is needed. You will run the code, inspect its behavior, and explain the tradeoffs. Our goal is engineering judgment you can use in your own applications.

## 00:00:38,867–00:01:26,400 | The framework and the platform

**Screen:** Introduce MAF and Microsoft Foundry as complementary responsibilities, before the example domain.

**Source:** `card:platform`; motion edits: `None`. Holds are labeled.

**Narration:** We will go deep into Microsoft Agent Framework as our development framework and Microsoft Foundry as our platform. The framework gives our C sharp application a way to compose agents, tools, context, sessions, and workflows. The platform provides the model and agent services and the operational capabilities we choose to use. We will ask when to use each capability, how to integrate it, and why it belongs in the design. You will find many agent examples in Python. Here we focus on C sharp and dot net, connecting agentic development to the software engineering practices you already know. Running an agent locally can still involve a real remote model call. Hosting, conversational state, operational memory, and durable execution are separate decisions that we will examine carefully.

## 00:01:26,400–00:02:10,067 | Build a system around the agent

**Screen:** Trace deterministic code, model decisions, tools and governance in the composition slide.

**Source:** `deck:7`; motion edits: `None`. Holds are labeled.

**Narration:** Building an agentic system involves much more than constructing an agent object. The system needs reliable deterministic code underneath it. The agent needs knowledge to interpret a situation and tools that give it controlled ways to act. We decide which responsibilities are ordinary code, bounded model calls, a single agent, a planned workflow, or collaboration among agents. We also decide what policy admits, who can authorize consequential actions, and what evidence proves an outcome. These decisions affect testing, security, latency, cost, and recovery. Throughout the course, each new capability must earn its place by solving a concrete requirement. You will see both the mechanics and the reasoning behind them.

## 00:02:10,067–00:02:48,600 | Meet Alon Fliess

**Screen:** Use the supplied About Me slide, with its own logo and book imagery.

**Source:** `deck:2`; motion edits: `None`. Holds are labeled.

**Narration:** This course was developed by Alon Fliess, Microsoft Foundry MVP, Microsoft Regional Director, and chief technology officer at ZioNet. Alon brings more than forty years of hands-on experience, together with his work as an architect, author, and speaker. The companion book develops the architectural rationale; the code and labs let you test those ideas for yourself. Treat the lessons as an invitation to investigate. Pause, change a condition, inspect the result, and explain what you discovered. You do not need to accept a model answer or an instructor statement without evidence. Enjoy the journey, and make the lab your own.

## 00:02:48,600–00:03:38,000 | The questions behind the labs

**Screen:** Reveal the four learning questions. Explain the repeated lesson pattern.

**Source:** `card:outcomes`; motion edits: `None`. Holds are labeled.

**Narration:** Every lab starts with an observable outcome, not a package name. We prepare a known starting fixture and stage. We walk through the behavior, then connect it to a small piece of code. You pause and reproduce it, check the expected result, and investigate one failure path. Keep four questions in mind. What did the system observe? What did the agent infer? Who can authorize a change? And what evidence proves that change happened? A plausible answer is not an execution receipt. A remembered incident is a lead to investigate, not proof of today's cause. This distinction will connect all the lessons, from the first read-only tool to protected actions, collaboration, and recovery. The goal is not to memorize the Director's buttons. It is to know when a capability solves a real requirement and when simpler deterministic code is sufficient.

## 00:03:38,000–00:04:31,600 | Runtime agents and coding assistants

**Screen:** Use the two-agentic-systems slide; point to the runtime side.

**Source:** `deck:3`; motion edits: `None`. Holds are labeled.

**Narration:** There are two different uses of agents around this repository. A coding assistant can help us write, review, or change the software. The Operations Agent inside the running city application is a runtime component serving an operator. Our course is about that second system. Its inputs are an operator question, instructions, allowed tools, and relevant context. Its output may be an answer, a request for more information, or a proposed action. It operates inside an application with service boundaries, permissions, timeouts, and failure modes. Using a coding assistant to produce a tool does not make that tool's runtime behavior correct. We still inspect its contracts, verify what it reads and writes, and test the outcome. During debugging, we will follow the runtime agent's actual model calls and tool results, rather than treating an impressive chat answer as proof that the system works.

## 00:04:31,600–00:05:26,467 | What Microsoft Agent Framework provides

**Screen:** Show the agent model/instructions/capabilities diagram. Reserve exact signatures for current source.

**Source:** `deck:15`; motion edits: `None`. Holds are labeled.

**Narration:** Microsoft Agent Framework gives us application building blocks for agents and workflows. In this C sharp application, an agent combines a model connection, instructions, and capabilities such as callable tools. Sessions carry conversational context between related interactions. Context providers can add the information a request needs. Workflow code can coordinate steps and explicit gates. These are useful building blocks, but they do not decide our domain architecture for us. We choose which tools are available at each stage, how those tools cross service boundaries, which actions require approval, and how outcomes are verified. The first agent in this course has one narrow read tool. That small starting point is intentional: it lets us see the complete model and tool loop without hiding the responsibility of each component behind a large toolbox. Later lessons add capabilities only when the incident gives us a reason to add them.

## 00:05:26,467–00:06:15,267 | Choose the smallest sufficient mechanism

**Screen:** Explain the decision map in order: code, model call, agent, workflow and multiple agents.

**Source:** `deck:6`; motion edits: `None`. Holds are labeled.

**Narration:** Start with deterministic code when the rules and sequence are known. Comparing a reported lamp state with its schedule does not require a language model. A direct model call is useful when a bounded interpretation or generation task is enough. An agent becomes useful when the next information-gathering step depends on what it discovers and it must choose among permitted tools. A workflow is useful when required steps and gates must be explicit even though individual steps may use an agent. Multiple agents become useful when there are real responsibility, permission, or context boundaries. They are not automatically an improvement over one well-designed agent. Ask what the added boundary buys you and what new failure modes it introduces. We will revisit this decision map throughout the course. Learning when not to add an agent is part of learning to build an agentic system well.

## 00:06:15,267–00:06:59,800 | Use the architecture skills

**Screen:** Show Alon's architecture skills slide and github.com/alonf/agentic-architecture-skills.

**Source:** `deck:10`; motion edits: `None`. Holds are labeled.

**Narration:** Alon's agentic architecture skills repository turns this decision map into a practical design aid. The architecture router helps decompose responsibilities and choose the least autonomous mechanism that meets the requirement. The MAF mapping skill then translates an agreed architecture into framework, hosting, governance, and assurance decisions. Use these skills with a coding assistant to challenge your design, not simply to generate more agents. Their advice still needs review, tests, and enforcement in your application. In the lab exercise, bring one responsibility from your own system, ask for a recommendation, and verify the reasoning against the code and current documentation. The repository link is on screen and in the companion lab.

## 00:06:59,800–00:08:10,400 | Caesarea is a system of systems

**Screen:** Use the city-domain diagram. Distinguish implemented services from conceptual breadth.

**Source:** `deck:11`; motion edits: `None`. Holds are labeled.

**Narration:** Our example is Caesarea, a smart city. The city is our means of learning agentic architecture; the course goal is the engineering you can transfer to another domain. A city is not one application with one database. Energy, security, mobility, maintenance, and other operational domains already have systems, owners, and policies. They may use different vendors and different representations of the same physical place. Our architecture connects these systems without pretending that one model owns them all. Domain Hubs provide focused interfaces and normalize the operational information that their domain is responsible for. A city operator can ask a cross-domain question while each domain still controls what it discloses and what commands it accepts. The diagram shows the architectural breadth. The executable lab deliberately implements a smaller slice: a lamp, a simulator, the Energy Hub, Command Center, and agent services, with additional domain capabilities introduced as the stages grow. For example, the main UI explicitly labels Mobility as not connected. We will always distinguish the architectural idea from behavior this checkout actually demonstrates.

## 00:08:10,400–00:09:05,467 | The deterministic core

**Screen:** Trace simulator → Energy Hub → Command Center; then locate the agent service beside that path.

**Source:** `deck:13`; motion edits: `None`. Holds are labeled.

**Narration:** At the bottom of our running slice, the Smart Pole simulator stands in for a device controller. The Energy Hub exposes the operational twin and the permitted energy commands. Command Center assembles the operator's view and manages its own incident responsibilities. These services form the deterministic core. They continue to own operational facts when we enable an agent. The agent layer uses focused endpoints and tools instead of reaching around the Hub into its internals. That separation has practical value. We can reason about a command's validation independently of the model's wording. We can correlate a request across HTTP boundaries. We can test stale-state rejection and device failure without hoping that a prompt will enforce them. In a system of systems, integration and authority are software contracts. The model can help decide what to investigate, but those contracts still determine what the application is allowed to do.

## 00:09:05,467–00:10:01,200 | What the operational twin means

**Screen:** Show reported/desired/schedule/receipt diagram. Explain the simulator boundary.

**Source:** `card:twin`; motion edits: `None`. Holds are labeled.

**Narration:** An operational digital twin is a representation of an asset with explicit state and confirmation semantics. Reported state tells us what the controller has reported. Desired state expresses a requested target. The schedule expresses a rule for expected operation, and an override can explain why operation differs from that rule. Health tells us something about the controller's condition. Revisions help detect that state changed between observation and action. A command receipt records the progress and outcome of a particular request. These values answer different questions. Setting a desired target does not prove that a device acknowledged it. A successful user-interface message alone does not establish the authoritative state. In this lab, the device is simulated, so the strongest demonstrated claim is a confirmed transition through the simulator and Hub. That is valuable evidence for the software path, while remaining distinct from delivery to real equipment.

## 00:10:01,200–00:10:53,933 | Adding the agentic layer

**Screen:** Overlay observe → infer → request action → verify on the deterministic architecture.

**Source:** `deck:7`; motion edits: `None`. Holds are labeled.

**Narration:** Now we can add the agentic layer without moving the source of truth. The Operations Agent observes current state through a tool. It may retrieve evidence, infer a likely explanation, recommend a next step, or request a bounded action. The model chooses among the capabilities the application admits. The runtime invokes the selected tool, and the domain service still applies its rules. When an action completes, the system must verify the receipt and resulting state. This is why observation, inference, authority, and verification remain separate in our lesson structure. The model's ability to keep a conversation does not make it a digital twin. A session can remember what the operator said; a Hub tells us what the operational system currently reports. Likewise, a remembered case can suggest where to look, but current evidence and policy must decide whether the same explanation applies today.

## 00:10:53,933–00:11:42,467 | The model and tool loop

**Screen:** Show function-tool slide; connect descriptors and arguments to the later EnergyTools capture.

**Source:** `deck:17`; motion edits: `None`. Holds are labeled.

**Narration:** A tool in this first stage is an ordinary C sharp method exposed with a name, description, and arguments. The application supplies the question and available tools to the model. The model can return a request to call a tool. The runtime executes that request through our code and supplies the result to a subsequent model interaction. The model can then answer using the returned information. In our recorded read-only request, one tool invocation required two model round trips. That is an observation about this request, not a promise that every agent always uses two calls. We will inspect both round trips in the debugger: what the model requested and what information came back from the Hub. This makes the loop concrete. We are inspecting observable messages and results, not claiming access to the model's private internal reasoning.

## 00:11:42,467–00:12:42,333 | How the course grows

**Screen:** Reveal four groups of learning needs, not seventeen feature boxes.

**Source:** `card:journey`; motion edits: `None`. Holds are labeled.

**Narration:** The labs grow along four practical needs. First, investigate with the right context. We begin with current state, then study sessions, retrieved knowledge, remembered cases, skills, and remote tool boundaries. Second, control consequential action. We separate a tool asking for missing input, a workflow stopping at a planned gate, and the runtime intercepting a protected tool selected by the model. Those are different mechanisms with different guarantees. Third, collaborate across responsibility boundaries, including specialist agents and delegation. Fourth, operate and improve the system: hosting, durable recovery, evaluation, and optional cloud capabilities. Each step asks why this capability belongs here, when it is appropriate, how the code implements it, and how a learner proves it worked. We will not imply that ordinary in-memory approvals survive a restart. Cloud-only and preview paths will be labeled and kept optional when the local lesson does not require them.

## 00:12:42,333–00:13:34,600 | Prepare a reproducible checkout

**Screen:** Show copyable clone/build commands from lab.md, then the exact baseline hash without date comparisons.

**Source:** `card:setup`; motion edits: `None`. Holds are labeled.

**Narration:** We use main, with the dot net eleven code merged into it. Clone the repository, select main, and record your commit before starting. The companion lab records the commit used for this video. Read global dot json, select the SDK, restore, build, and run the test project. Open this same folder in both editors so the source matches the running binaries. Install the repository's VS Code attach extension, and on Windows build the Visual Studio helper. For the AI exercises, use an existing Foundry project and model deployment that your account can invoke. Set the local environment variables shown in the lab, then start the AppHost from that shell. Keep the shell open. We run the services locally and use remote model inference. This lesson creates no cloud resources. Complete sign-in before recording and keep private tokens out of the video.

## 00:13:34,600–00:14:24,000 | Aspire is our starting point

**Screen:** Show actual running resources; use this run's token off camera. Point to the two web resources and Operations Agent.

**Source:** `capture:aspire-resources`; motion edits: `[('aspire-live', 0, 17, None)]`. Holds are labeled.

**Narration:** Start the AppHost from the configured shell using the direct command in the lab, and keep that shell running. Open the private dashboard link printed by this run. Aspire is the first live environment screen because it shows the processes that make up our system. Find the simulator, Energy Hub, Command Center API, Operations Agent, and the two web applications. Check their status, then use their endpoint links rather than memorizing ports. A running process is a useful first checkpoint, but it does not prove that the model endpoint or permissions work. When a request fails, select the relevant resource and inspect its logs and traces. The request correlation helps connect activity across boundaries. Starting here also avoids the common mistake of opening an old browser tab against an application instance that is no longer the one you are debugging.

## 00:14:24,000–00:15:14,000 | Read the resource graph

**Screen:** Show real graph, then identify the operational path and the agent's dependency on the Hub.

**Source:** `capture:aspire-graph`; motion edits: `[('aspire-live', 17, 18, None)]`. Holds are labeled.

**Narration:** The Aspire graph makes the architecture visible as running services. Follow the application's dependencies and compare them with the slide diagram. The operator uses Command Center. The Operations Agent reaches domain information through its service clients and tools. The Energy Hub connects to the simulated device. These process boundaries matter when you choose a debugger target. Attaching to Command Center Web will not automatically stop inside the Operations Agent service. Likewise, an agent-side breakpoint does not pause every service in the city. We will attach to the process that owns the code we want to inspect and leave the rest of the environment running. Before moving on, locate the two web application links. One is the Director used to prepare the lesson, and the other is the main operator application where we will ask the question and inspect the result.

## 00:15:14,000–00:16:08,733 | Director prepares the starting fixture

**Screen:** Show actual Investigation Agent preparation preview, then ready state. Explain scenario versus stage.

**Source:** `capture:director-preview`; motion edits: `[('director-live', 49, 20, (310, 230, 1300, 620))]`. Holds are labeled.

**Narration:** The Demo Director is our laboratory control panel. A scenario sets the operational starting conditions. A stage controls which implemented capabilities participate. For this onboarding exercise, select Investigation Agent and review the preparation preview. It applies Lights On Reported by a Client and selects the read-only investigation stage. Confirm, then check the ready state. The scenario and the stage are related, but they are not the same thing. Reusing an already-repaired lamp without resetting the fixture would change the meaning of the next demonstration. The Director also provides teaching breakpoints and debugger attachment controls under Advanced controls. Those prepare the investigation; the Director is not where we perform the operator's work. Keep the local function and local Aspire habitat for this exercise. We do not need a cloud-hosted agent to learn how this local request crosses the service boundaries.

## 00:16:08,733–00:17:15,000 | Command Center is the operator's application

**Screen:** Show main UI; point to L-417, reported/schedule/override, agent question, and deterministic action without clicking repair.

**Source:** `capture:command-center`; motion edits: `[('director-live', 76, 8, None), ('retry-live', 39, 13, (1100, 0, 820, 700))]`. Holds are labeled.

**Narration:** Now switch to the main Command Center. Open Walkthrough at the upper right. Read its starting conditions, numbered actions, code references, and the point of the demonstration. This is your in-app guide throughout the labs. Close the panel when you need the agent controls. The map identifies L four seventeen at North Promenade. The operational panel reports that the lamp is on, while the daylight schedule expects it to be off. The override is set and the controller is healthy. These are observations from the deterministic system. They do not yet prove why someone left the override in place. The agent panel asks a deliberately small question: is streetlight L four seventeen on? We will use that question to learn the debugging workflow. Leave Restore Scheduled Mode alone for now; repair is the first lab after onboarding. Notice that the operator workflow and the agent panel coexist. Enabling the agent did not replace the core. Before the debugger exercise, read the Energy Hub endpoint using the companion lab's command and save the process ID. Those checkpoints let us verify both operational state and detach behavior later.

## 00:17:15,000–00:18:06,667 | Attach VS Code

**Screen:** Show arming and attached captures. Explain attach versus launch and one-shot teaching stops.

**Source:** `capture:vscode-attached`; motion edits: `[('vscode-live', 0, 25, (310, 300, 1320, 700))]`. Holds are labeled.

**Narration:** Open the same checkout in Visual Studio Code. In the Director, expand Advanced controls and Demo Breakpoints. Verify that the attach extension is ready. Attach the Operations Agent row, then arm Agent Creation and Function Tool. The attached status matters: arming a checkbox by itself does not connect a debugger. These teaching stops are consumed on the next matching execution. We will also set an ordinary breakpoint in Foundry Operations Agent, immediately after the awaited Run Async call, so we can inspect its returned result. This is an attach session to an existing process started by Aspire. We are not launching a second copy of the application. Prepare the expressions you want to inspect before asking the question. Keep the live pauses short; we can freeze the edited video to explain the source without holding a real HTTP request open for several minutes.

## 00:18:06,667–00:18:58,400 | Stop and single-step in C sharp

**Screen:** Show actual breakpoint and F10 transition from Pause to AsAIAgent. Highlight question, stage and tools.

**Source:** `capture:vscode-question`; motion edits: `[('vscode-take2', 51, 16, None)]`. Holds are labeled.

**Narration:** Click Ask agent in Command Center. Visual Studio Code stops at the Agent Creation teaching point. The yellow execution marker shows where this request is paused. Press F ten to step over to the next statement. In the captured request, the question variable is exactly: is streetlight L four seventeen on? The stage has already determined which capabilities were composed. Read the agent construction code: it supplies the model, instructions, tools, and any applicable context providers. This is the connection between the framework diagram and ordinary application code. We can inspect the request and admitted tools before allowing the model call to proceed. Press F five to continue. Because Function Tool is armed, the next relevant stop will show the C sharp method that the model selected, rather than asking us to infer tool execution from the final answer alone.

## 00:18:58,400–00:19:47,333 | Inspect the tool boundary

**Screen:** Show actual EnergyTools stop and assetId; point to gateway call.

**Source:** `capture:vscode-tool`; motion edits: `[('vscode-take2', 68, 11, None)]`. Holds are labeled.

**Narration:** We are now in Energy Tools, inside Get Streetlight State Async. The asset ID is L four seventeen. The model selected the narrow state-reading tool, and the runtime arrived at this method with its arguments. Follow the code: it checks the input, gets the correlation ID, logs the invocation, and calls the Energy read gateway. The gateway obtains current state through the Hub boundary. There is no direct database shortcut and no model-owned lamp state. This is a useful place to distinguish selection from execution. A requested tool is a model output; reaching this method shows invocation, and the returned Hub data provides the evidence that can support the answer. Continue with F five to the breakpoint after Run Async. That next stop lets us compare the model's final response with the intermediate tool exchange that produced it.

## 00:19:47,333–00:20:43,133 | Inspect AI results and model exchanges

**Screen:** Show response.Text and recorded Exchanges. Crop to readable code/console; do not expose unrelated IDE notifications.

**Source:** `capture:vscode-exchanges`; motion edits: `[('vscode-take2', 105, 42, None), ('vscode-take2', 151, 13, None)]`. Holds are labeled.

**Narration:** After Run Async returns, inspect response dot Text. The captured model answered that streetlight L four seventeen is currently on. Now expand model Flight Recorder dot Exchanges. There are two recorded model round trips for this request. In the first received message, the model requested get streetlight state with the L four seventeen argument. The next sent message contains the tool result, including reported Is On set to true. That is the bridge from the Hub's observation to the model's answer. The recorder preserves readable message and tool-result information for this demonstration. It does not reveal private model reasoning. An answer can vary in wording across runs, so the learner checkpoint is the supported claim and the observed tool path, not exact prose. Continue the request, remove the ordinary breakpoint, and detach from the service. We will verify that the same process remains running.

## 00:20:43,133–00:21:29,267 | A real timeout and recovery

**Screen:** Explain the recorded 100-second web timeout and distinguish the debugger take from the fresh detached retry.

**Source:** `card:recovery`; motion edits: `None`. Holds are labeled.

**Narration:** There is a useful failure case here. A long debugger pause can outlast the web client's one hundred second attempt timeout, even though the agent service relaxes its own budget when a debugger is attached. Our recording encountered this. The debugger returned a model answer, while the browser had already stopped waiting. That does not prove a deployment failure. Continue the paused request, remove ordinary breakpoints, detach, and verify the Hub. Then retry this read-only question as a fresh request. The later browser answer belongs to that fresh retry. For write tools, do not generalize this advice into blindly resending commands. First reconcile the receipt and current state. Pause the video to study the code; keep pauses in your live HTTP request short.

## 00:21:29,267–00:22:46,067 | Switch debuggers without restarting the service

**Screen:** Show the actual Visual Studio attach, breakpoint, F10, Watch values, result and detach. Windows only.

**Source:** `capture:vs-attached`; motion edits: `[('vs-take2', 12, 12, None), ('vs-take2', 108, 17, (250, 0, 1670, 1030)), ('vs-take2', 162, 9, (250, 0, 1670, 1030)), ('vs-take2', 201, 17, (250, 0, 1670, 1030)), ('vs-take2', 230, 21, (250, 0, 1670, 1030))]`. Holds are labeled.

**Narration:** Now repeat the workflow in Visual Studio twenty twenty-six. Visual Studio twenty twenty-six is available only on Windows. VS Code is our alternative across Windows, macOS, and Linux. Open the same solution, and detach VS Code before attaching Visual Studio to the Operations Agent. Here we use the native Attach to Process dialog and filter by the verified process identifier. Arm the teaching stops and set the ordinary breakpoint after Run Async. Ask the same read-only question in the main application. The yellow marker shows the breakpoint. F ten steps over a statement; F five continues. Use Watch to inspect the operator question and the returned response. At the tool boundary, inspect the asset identifier. A Watch expression from another method may be out of scope; inspect the current method arguments instead. After the model returns, inspect response dot Text and the exchange count. These are real C sharp values from the executing request, not access to the model's private reasoning. Continue and choose Detach All, leaving Aspire and the service running. The companion lab includes the exact source locations and verification commands.

## 00:22:46,067–00:23:35,200 | Verify the outcome at the Hub

**Screen:** Show real detached retry answer and RAN trace. Pair with saved Hub and PID evidence in lab.

**Source:** `capture:read-answer`; motion edits: `[('retry-live', 10, 25, None), ('card:hub-checkpoint', 0, 23, None)]`. Holds are labeled.

**Narration:** The main application now shows the answer from the fresh read-only retry. The trace marks get streetlight state as ran. The Hub still reports that the lamp is on, and the operator has not issued a repair command. The Operations Agent process is still alive after detaching the debugger. These observations answer our four questions. The Hub supplied operational state. The model selected a read tool and formed an answer from its result. No authority to change the lamp was exercised in this onboarding request. And the fresh Hub read confirms that we did not silently turn the investigation into a repair. This is how the course will treat results: a UI message, a tool trace, and authoritative state each contribute evidence, but they answer different questions. Keep that habit when later labs introduce actions that can actually change the city.

## 00:23:35,200–00:24:32,000 | Pause, practice, and prepare Lab 01

**Screen:** Hold the exercise card; learner repeats an IDE cycle, checks Hub/PID, resets Deterministic fixture. Stop after Unit00 review.

**Source:** `card:practice`; motion edits: `[('reset-live', 15, 20, None), ('reset-live', 52, 10, None), ('card:practice', 0, 26, None)]`. Holds are labeled.

**Narration:** Pause here and repeat one attach, stop, inspect, continue, and detach cycle without following the video. Use the companion lab's copyable commands to check the Hub and the process ID. Explain aloud where the operational truth lives, what the model selected, and why an answer did not repair the lamp. Then repeat with the other IDE after the first is detached. If the user interface times out during a long pause, use the read-only recovery procedure we just demonstrated. When you are ready for Lab one, finish outstanding requests, clear the teaching stops and ordinary breakpoints, and choose Deterministic in the Director. Review and confirm the preparation preview, then verify the daylight anomaly again. A reset can change the revision, so discard old snapshots before the next exercise. You now have the architectural foundation and an inspectable environment. The next lab begins with the deterministic system and proves a repair through the Energy Hub.

## 00:24:32,000–00:25:23,467 | Investigate before the next lab

**Screen:** End on code, documentation and AI investigation questions. Stop after Unit 00 for review.

**Source:** `card:questions`; motion edits: `None`. Holds are labeled.

**Narration:** Before moving on, investigate three questions. First, trace the state read from Energy Tools to the Energy Hub endpoint. Which service owns the answer, and how is the correlation identifier propagated? Second, find the agent service and web client timeout policies. Why can one request produce a debugger result but no browser answer? Third, give an AI assistant a proposed responsibility and the agentic architecture skills. Ask whether it belongs in code, a model call, an agent, or a workflow. Challenge its recommendation with one failure case, then verify its claims in source and official documentation. Save your answer with a file location or source link and a reproducible observation. Every later lab ends with this kind of investigation. You now have the foundation, the tools, and a way to test what you learn. Enjoy building Agentic C sharp.
