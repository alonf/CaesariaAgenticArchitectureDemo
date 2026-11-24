"""Agentic C# Unit 00: context engineering and genuine debugger evidence.

Narration always expands the framework name. Source slides retain author branding.
"""

LESSON = [('Agentic C#',
  'card:title',
  'Welcome to Agentic C#',
  'Open on the new course title and Alon signature background. No city imagery yet.',
  'Welcome to Building C sharp Agentic Systems with Microsoft Agent Framework and Foundry, by Alon Fliess. '
  'Agentic C sharp for short. Agentic development is moving fast. It is easy to assemble a convincing '
  'demonstration before understanding the foundations that make its architecture dependable. This course '
  'gives you the background and the practical toolset to make informed decisions. You will learn to ask what '
  'belongs in deterministic code, where a language model helps, when an agent is justified, and when '
  'explicit orchestration is needed. You will run the code, inspect its behavior, and explain the tradeoffs. '
  'Our goal is engineering judgment you can use in your own applications. This course is for C sharp '
  'developers who need to architect, design, and implement new agentic systems, or add agentic capabilities '
  'to existing software. We assume familiarity with C sharp development, but no previous experience building '
  'agents. If you already know the APIs, the architectural decisions will help you understand when to use '
  'what, and why.'),
 ('The framework and the platform',
  'card:platform',
  None,
  'Introduce MAF and Microsoft Foundry as complementary responsibilities, before the example domain.',
  'We will go deep into Microsoft Agent Framework as our development framework and Microsoft Foundry as our '
  'platform. The framework gives our C sharp application a way to compose agents, tools, context, sessions, '
  'and workflows. The platform provides the model and agent services and the operational capabilities we '
  'choose to use. We will ask when to use each capability, how to integrate it, and why it belongs in the '
  'design. You will find many agent examples in Python. Here we focus on C sharp and dot net, connecting '
  'agentic development to the software engineering practices you already know. Running an agent locally can '
  'still involve a real remote model call. Hosting, conversational state, operational memory, and durable '
  'execution are separate decisions that we will examine carefully.'),
 ('Build a system around the agent',
  'deck:7',
  None,
  'Trace deterministic code, model decisions, tools and governance in the composition slide.',
  'Building an agentic system involves much more than constructing an agent object. The system needs '
  'reliable deterministic code underneath it. The agent needs knowledge to interpret a situation and tools '
  'that give it controlled ways to act. We decide which responsibilities are ordinary code, bounded model '
  'calls, a single agent, a planned workflow, or collaboration among agents. We also decide what policy '
  'admits, who can authorize consequential actions, and what evidence proves an outcome. These decisions '
  'affect testing, security, latency, cost, and recovery. Throughout the course, each new capability must '
  'earn its place by solving a concrete requirement. You will see both the mechanics and the reasoning '
  'behind them.'),
 ('Meet Alon Fliess',
  'deck:2',
  None,
  'Use the supplied About Me slide, with its own logo and book imagery.',
  'This course was developed by Alon Fliess, Microsoft Foundry MVP, Microsoft Regional Director, and chief '
  'technology officer at ZioNet. Alon brings more than forty years of hands-on experience, together with his '
  'work as an architect, author, and speaker. The companion book develops the architectural rationale; the '
  'code and labs let you test those ideas for yourself. Treat the lessons as an invitation to investigate. '
  'Pause, change a condition, inspect the result, and explain what you discovered. You do not need to accept '
  'a model answer or an instructor statement without evidence. Enjoy the journey, and make the lab your '
  'own.'),
 ('The questions behind the labs',
  'card:outcomes',
  None,
  'Reveal the four learning questions. Explain the repeated lesson pattern.',
  'Every lab starts with an observable outcome, not a package name. We prepare a known starting fixture and '
  'stage. We walk through the behavior, then connect it to a small piece of code. You pause and reproduce '
  'it, check the expected result, and investigate one failure path. Keep four questions in mind. What did '
  'the system observe? What did the agent infer? Who can authorize a change? And what evidence proves that '
  'change happened? A plausible answer is not an execution receipt. A remembered incident is a lead to '
  "investigate, not proof of today's cause. This distinction will connect all the lessons, from the first "
  'read-only tool to protected actions, collaboration, and recovery. The goal is not to memorize the '
  "Director's buttons. It is to know when a capability solves a real requirement and when simpler "
  'deterministic code is sufficient.'),
 ('Runtime agents and coding assistants',
  'deck:3',
  None,
  'Use the two-agentic-systems slide; point to the runtime side.',
  'There are two different uses of agents around this repository. A coding assistant can help us write, '
  'review, or change the software. The Operations Agent inside the running city application is a runtime '
  'component serving an operator. Our course is about that second system. Its inputs are an operator '
  'question, instructions, allowed tools, and relevant context. Its output may be an answer, a request for '
  'more information, or a proposed action. It operates inside an application with service boundaries, '
  'permissions, timeouts, and failure modes. Using a coding assistant to produce a tool does not make that '
  "tool's runtime behavior correct. We still inspect its contracts, verify what it reads and writes, and "
  "test the outcome. During debugging, we will follow the runtime agent's actual model calls and tool "
  'results, rather than treating an impressive chat answer as proof that the system works.'),
 ('What Microsoft Agent Framework provides',
  'deck:15',
  None,
  'Show the agent model/instructions/capabilities diagram. Reserve exact signatures for current source.',
  'Microsoft Agent Framework gives us application building blocks for agents and workflows. In this C sharp '
  'application, an agent combines a model connection, instructions, and capabilities such as callable tools. '
  'Sessions carry conversational context between related interactions. Context providers can add the '
  'information a request needs. Workflow code can coordinate steps and explicit gates. These are useful '
  'building blocks, but they do not decide our domain architecture for us. We choose which tools are '
  'available at each stage, how those tools cross service boundaries, which actions require approval, and '
  'how outcomes are verified. The first agent in this course has one narrow read tool. That small starting '
  'point is intentional: it lets us see the complete model and tool loop without hiding the responsibility '
  'of each component behind a large toolbox. Later lessons add capabilities only when the incident gives us '
  'a reason to add them.'),
 ('Choose the smallest sufficient mechanism',
  'deck:6',
  None,
  'Explain the decision map in order: code, model call, agent, workflow and multiple agents.',
  'Start with deterministic code when the rules and sequence are known. Comparing a reported lamp state with '
  'its schedule does not require a language model. A direct model call is useful when a bounded '
  'interpretation or generation task is enough. An agent becomes useful when the next information-gathering '
  'step depends on what it discovers and it must choose among permitted tools. A workflow is useful when '
  'required steps and gates must be explicit even though individual steps may use an agent. Multiple agents '
  'become useful when there are real responsibility, permission, or context boundaries. They are not '
  'automatically an improvement over one well-designed agent. Ask what the added boundary buys you and what '
  'new failure modes it introduces. We will revisit this decision map throughout the course. Learning when '
  'not to add an agent is part of learning to build an agentic system well.'),
 ('Use the architecture skills',
  'deck:10',
  None,
  "Show Alon's architecture skills slide and github.com/alonf/agentic-architecture-skills.",
  "Alon's agentic architecture skills repository turns this decision map into a practical design aid. The "
  'architecture router helps decompose responsibilities and choose the least autonomous mechanism that meets '
  'the requirement. The Microsoft Agent Framework mapping skill then translates an agreed architecture into '
  'framework, hosting, governance, and assurance decisions. Use these skills with a coding assistant to '
  'challenge your design, not simply to generate more agents. Their advice still needs review, tests, and '
  'enforcement in your application. In the lab exercise, bring one responsibility from your own system, ask '
  'for a recommendation, and verify the reasoning against the code and current documentation. The repository '
  'link is on screen and in the companion lab.'),
 ('Find your way around the repository',
  'card:repository',
  'The code, framework and platform',
  'Show actual GitHub root, README introduction cropped above conference details, then cumulative stages. '
  'Transition to the local Explorer tree.',
  'Before exploring the city, let us connect the moving parts. A repository is more than the agent class: it '
  'contains the executable system, its contracts, tests, and instructions for running it. Open the Caesarea '
  'Agentic Architecture Demo on GitHub. The README introduces the system and maps its cumulative stages. '
  'Main contains the implemented capabilities, selected at runtime. We do not maintain a separate '
  'application for each lesson. In the local solution, Apps contains the two user interfaces. Services '
  'contains the deterministic services and agent services. Each API boundary has its own Contracts project. '
  'The AppHost starts the local system, Tests verifies behavior, and docs explains the stage decisions. Keep '
  'the README and the companion lab nearby. The companion lab records the exact source commit and '
  'prerequisites. You now have a map from a visible component to the place where its implementation lives.'),
 ('Locate the project and its hosted agent',
  'card:platform',
  None,
  'Read-only Foundry portal recording: project home, Agents list, caesarea-operations, Details. No '
  'invocation, deployment or setting changes.',
  'The platform is the next moving part. A Foundry project supplies the environment in which model and agent '
  "resources are organized. Here is this solution's existing project, caesarea dev. Open Agents. The list "
  'contains caesarea operations, with kind Hosted and status Running. Its details identify an active '
  'version, identity, and Responses endpoint. This is a real deployed agent, not a slide illustration. This '
  'visit is read-only: we are locating an existing deployment, not deploying one. Keep two execution paths '
  'distinct. Our upcoming debugger exercise runs the Operations Agent locally and calls remote model '
  'inference. The hosted agent runs in Foundry and uses the cloud Energy Hub; the local Director does not '
  'reset that cloud city. Later hosting lessons will explain its composition and identity boundaries. For '
  'now, recognize where the platform lives and how to locate an agent. You do not need to create a hosted '
  'agent to complete the local introductory exercise.'),
 ('Caesarea is a system of systems',
  'deck:12',
  'Architecture and operational authority',
  'Show slide 12 first. Point to city systems, domain Hubs and the deterministic operational core before '
  'discussing the agentic layer.',
  'Our example is Caesarea, a smart city. The city is our means of learning agentic architecture; the course '
  'goal is the engineering you can transfer to another domain. A city is not one application with one '
  'database. Energy, security, mobility, maintenance, and other operational domains already have systems, '
  'owners, and policies. They may use different vendors and different representations of the same physical '
  'place. Our architecture connects these systems without pretending that one model owns them all. Domain '
  'Hubs provide focused interfaces and normalize the operational information that their domain is '
  'responsible for. A city operator can ask a cross-domain question while each domain still controls what it '
  'discloses and what commands it accepts. The diagram shows the architectural breadth. The executable lab '
  'deliberately implements a smaller slice: a lamp, a simulator, the Energy Hub, Command Center, and agent '
  'services, with additional domain capabilities introduced as the stages grow. For example, the main UI '
  'explicitly labels Mobility as not connected. We will always distinguish the architectural idea from '
  'behavior this checkout actually demonstrates.'),
 ('The deterministic core',
  'deck:12',
  None,
  'Trace simulator → Energy Hub → Command Center; then locate the agent service beside that path.',
  'At the bottom of our running slice, the Smart Pole simulator stands in for a device controller. The '
  'Energy Hub exposes the operational twin and the permitted energy commands. Command Center assembles the '
  "operator's view and manages its own incident responsibilities. These services form the deterministic "
  'core. They continue to own operational facts when we enable an agent. The agent layer uses focused '
  'endpoints and tools instead of reaching around the Hub into its internals. That separation has practical '
  "value. We can reason about a command's validation independently of the model's wording. We can correlate "
  'a request across HTTP boundaries. We can test stale-state rejection and device failure without hoping '
  'that a prompt will enforce them. In a system of systems, integration and authority are software '
  'contracts. The model can help decide what to investigate, but those contracts still determine what the '
  'application is allowed to do.'),
 ('What the operational twin means',
  'card:twin',
  None,
  'Show reported/desired/schedule/receipt diagram. Explain the simulator boundary.',
  'An operational digital twin is a representation of an asset with explicit state and confirmation '
  'semantics. Reported state tells us what the controller has reported. Desired state expresses a requested '
  'target. The schedule expresses a rule for expected operation, and an override can explain why operation '
  "differs from that rule. Health tells us something about the controller's condition. Revisions help detect "
  'that state changed between observation and action. A command receipt records the progress and outcome of '
  'a particular request. These values answer different questions. Setting a desired target does not prove '
  'that a device acknowledged it. A successful user-interface message alone does not establish the '
  'authoritative state. In this lab, the device is simulated, so the strongest demonstrated claim is a '
  'confirmed transition through the simulator and Hub. That is valuable evidence for the software path, '
  'while remaining distinct from delivery to real equipment.'),
 ('Adding the agentic layer',
  'deck:13',
  None,
  'Overlay observe → infer → request action → verify on the deterministic architecture.',
  'Now we can add the agentic layer without moving the source of truth. The Operations Agent observes '
  'current state through a tool. It may retrieve evidence, infer a likely explanation, recommend a next '
  'step, or request a bounded action. The model chooses among the capabilities the application admits. The '
  'runtime invokes the selected tool, and the domain service still applies its rules. When an action '
  'completes, the system must verify the receipt and resulting state. This is why observation, inference, '
  "authority, and verification remain separate in our lesson structure. The model's ability to keep a "
  'conversation does not make it a digital twin. A session can remember what the operator said; a Hub tells '
  'us what the operational system currently reports. Likewise, a remembered case can suggest where to look, '
  'but current evidence and policy must decide whether the same explanation applies today.'),
 ('Context Engineering: one request, several steps',
  'deck:14',
  None,
  'Trace slide 14 from context inputs to model, deterministic execution and the return arrow. Highlight each '
  'region with its explanation.',
  'Before reading the first tool example, we need to understand the execution loop. This is context '
  'engineering. Our code and Microsoft Agent Framework build the context for each model call. On the left '
  'are instructions, the user request, conversation history, relevant knowledge, available tool definitions, '
  'previous tool results, and any memory we choose to supply. We select what is relevant and permitted; we '
  'do not simply send everything. The model receives that context and decides what to produce next. It may '
  'answer, ask the caller for more information, or request a tool. A tool request names a capability and '
  'supplies arguments. It is not the execution itself. On the right, the framework and our application '
  'invoke admitted deterministic code. That code can read an API, retrieve knowledge, perform an authorized '
  'action, or call another agent when we expose that capability. Each resource still enforces its own access '
  'and business rules. Follow the bottom arrow. The tool result becomes part of the updated context for '
  'another model call. The model can now use the new evidence, request another step, or produce its answer. '
  'One call from our application to the agent can therefore involve several model calls, tools, and possibly '
  'connected agents. Normally the answer returns when the agent has enough information or needs input from '
  'the caller. Errors, cancellation, configured limits, and approval gates can also interrupt the run; '
  'success is not guaranteed. The engineering work is both the context and this controlled execution loop. '
  'In the debugger, we will observe one concrete case: the first model exchange requests a state read, and '
  'the second uses the returned facts to answer.'),
 ('Use the Microsoft Learn starting point',
  'deck:15',
  None,
  'Show Microsoft Learn Your First Agent with C# selected, then Add Tools. Explain documentation navigation '
  'before signatures.',
  'For framework concepts and API examples, use Microsoft Learn. Start in the Agent Framework get-started '
  'sequence and select C sharp. Your First Agent introduces construction and invocation; Add Tools adds a '
  'callable function. Use this sequence to isolate a framework idea before looking at our larger '
  'application. Documentation and packages evolve, so compare a sample with the versions in your checkout. '
  'The companion lab links these pages. We now have two complementary references: the repository explains '
  'this system, while Learn explains the framework building blocks.'),
 ('The smallest agent call in C sharp',
  'deck:16',
  None,
  'Read the short construction example on slide 16. Explain invocation verbally; inspect the real RunAsync '
  'later.',
  'Now start with the small construction example in the slides. An AI Agent combines a model, instructions, '
  'and capabilities. The project client is supplied by our application. As AI Agent selects the model and '
  'gives this agent a name and instructions. Notice the instruction to use evidence and never invent '
  'operational facts. That expresses the intended behavior; validation and permissions still belong in code. '
  'The calling pattern is ordinary asynchronous C sharp: pass the question to Run Async, await the response, '
  'and return the result to the caller. This slide isolates construction, while the debugger will show the '
  'actual invocation. Remember the context engineering diagram: awaiting one agent call does not mean there '
  'was only one model call. The framework can run the tool loop inside it. We will add that capability '
  'next.'),
 ('The model and tool loop',
  'deck:18',
  None,
  'Use slide 18 short function and registration code before the real implementation. Point to Description, '
  'AIFunctionFactory.Create and tools registration.',
  'A language model cannot inspect a streetlight by itself. We give it a focused function that obtains the '
  'state from the Energy Hub. On this slide, Get Streetlight State Async is an ordinary C sharp method '
  'accepting an asset identifier. The Description attribute explains the function to the model. In the real '
  'implementation, the asset parameter also has a Description explaining its meaning and giving L four '
  'seventeen as an example. These descriptions help form the tool schema. They do not register the tool by '
  'themselves. AI Function Factory dot Create wraps the method as a callable function, and adding it to the '
  "agent's tools makes it available. The class is an ordinary C sharp class; this local function path needs "
  'no special tool-class attribute. The model can request the function by name with arguments, and the '
  'framework invokes the registered method subject to our controls. That method calls the Hub, and its '
  'returned data feeds the next model request, exactly as in Context Engineering. The tool name and metadata '
  'guide selection; deterministic code performs the work. We will keep this teaching pattern in later '
  'lessons: a short slide example first, then the real implementation and its behavior.'),
 ('How the course grows',
  'card:journey',
  None,
  'Reveal four groups of learning needs, not seventeen feature boxes.',
  'The labs grow along four practical needs. First, investigate with the right context. We begin with '
  'current state, then study sessions, retrieved knowledge, remembered cases, skills, and remote tool '
  'boundaries. Second, control consequential action. We separate a tool asking for missing input, a workflow '
  'stopping at a planned gate, and the runtime intercepting a protected tool selected by the model. Those '
  'are different mechanisms with different guarantees. Third, collaborate across responsibility boundaries, '
  'including specialist agents and delegation. Fourth, operate and improve the system: hosting, durable '
  'recovery, evaluation, and optional cloud capabilities. Each step asks why this capability belongs here, '
  'when it is appropriate, how the code implements it, and how a learner proves it worked. We will not imply '
  'that ordinary in-memory approvals survive a restart. Cloud-only and preview paths will be labeled and '
  'kept optional when the local lesson does not require them.'),
 ('Prepare a reproducible checkout',
  'card:setup',
  'Start and navigate the lab',
  'Show copyable clone/build commands from lab.md, then the exact baseline hash without date comparisons.',
  'The lab code is dot net eleven code. Using dot net ten L T S with Microsoft Agent Framework follows the '
  'same concepts and development patterns. This particular checkout targets eleven, so use its required SDK '
  'to reproduce the lab. The companion lab records the exact commit and prerequisites. Clone the repository '
  'and open its root folder. Read global dot json, select the SDK, restore, build, and run the test project. '
  'Open this same folder in your preferred editor so the source matches the running binaries. Install the '
  "repository's VS Code attach extension, or on Windows build the Visual Studio helper. For the AI "
  'exercises, use an existing Foundry project and model deployment that your account can invoke. Set the '
  'local environment variables shown in the lab, then start the AppHost from that shell. Keep the shell '
  'open. The services run locally, with remote model inference. Complete sign-in before starting and keep '
  'private tokens out of recordings. The lab provides copyable steps and checks for each prerequisite.'),
 ('Aspire is our starting point',
  'capture:aspire-resources',
  None,
  "Show actual running resources; use this run's token off camera. Point to the two web resources and "
  'Operations Agent.',
  'Start the AppHost from the configured shell using the direct command in the lab, and keep that shell '
  'running. Open the private dashboard link printed by this run. Aspire is the first live environment screen '
  'because it shows the processes that make up our system. Find the simulator, Energy Hub, Command Center '
  'API, Operations Agent, and the two web applications. Check their status, then use their endpoint links '
  'rather than memorizing ports. A running process is a useful first checkpoint, but it does not prove that '
  'the model endpoint or permissions work. When a request fails, select the relevant resource and inspect '
  'its logs and traces. The request correlation helps connect activity across boundaries. Starting here also '
  'avoids the common mistake of opening an old browser tab against an application instance that is no longer '
  'the one you are debugging.'),
 ('Read the resource graph',
  'capture:aspire-graph',
  None,
  "Show real graph, then identify the operational path and the agent's dependency on the Hub.",
  "The Aspire graph makes the architecture visible as running services. Follow the application's "
  'dependencies and compare them with the slide diagram. The operator uses Command Center. The Operations '
  'Agent reaches domain information through its service clients and tools. The Energy Hub connects to the '
  'simulated device. These process boundaries matter when you choose a debugger target. Attaching to Command '
  'Center Web will not automatically stop inside the Operations Agent service. Likewise, an agent-side '
  'breakpoint does not pause every service in the city. We will attach to the process that owns the code we '
  'want to inspect and leave the rest of the environment running. Before moving on, locate the two web '
  'application links. One is the Director used to prepare the lesson, and the other is the main operator '
  'application where we will ask the question and inspect the result.'),
 ('Director prepares the starting fixture',
  'capture:director-preview',
  None,
  'Show actual Investigation Agent preparation preview, then ready state. Explain scenario versus stage.',
  'The Demo Director is our laboratory control panel. A scenario sets the operational starting conditions. A '
  'stage controls which implemented capabilities participate. For this onboarding exercise, select '
  'Investigation Agent and review the preparation preview. It applies Lights On Reported by a Client and '
  'selects the read-only investigation stage. Confirm, then check the ready state. The scenario and the '
  'stage are related, but they are not the same thing. Reusing an already-repaired lamp without resetting '
  'the fixture would change the meaning of the next demonstration. The Director also provides teaching '
  'breakpoints and debugger attachment controls under Advanced controls. Those prepare the investigation; '
  "the Director is not where we perform the operator's work. Keep the local function and local Aspire "
  'habitat for this exercise. We do not need a cloud-hosted agent to learn how this local request crosses '
  'the service boundaries.'),
 ("Command Center is the operator's application",
  'capture:command-center',
  None,
  'Show main UI; point to L-417, reported/schedule/override, agent question, and deterministic action '
  'without clicking repair.',
  'Now switch to the main Command Center. Open Walkthrough at the upper right. Read its starting conditions, '
  'numbered actions, code references, and the point of the demonstration. This is your in-app guide '
  'throughout the labs. Close the panel when you need the agent controls. The map identifies L four '
  'seventeen at North Promenade. The operational panel reports that the lamp is on, while the daylight '
  'schedule expects it to be off. The override is set and the controller is healthy. These are observations '
  'from the deterministic system. They do not yet prove why someone left the override in place. The agent '
  'panel asks a deliberately small question: is streetlight L four seventeen on? We will use that question '
  'to learn the debugging workflow. Leave Restore Scheduled Mode alone for now; repair is the first lab '
  'after onboarding. Notice that the operator workflow and the agent panel coexist. Enabling the agent did '
  'not replace the core. Before the debugger exercise, read the Energy Hub endpoint using the companion '
  "lab's command and save the process ID. Those checkpoints let us verify both operational state and detach "
  'behavior later.'),
 ('Attach VS Code',
  'capture:vscode-attached',
  'Debug in Visual Studio Code',
  'Show arming and attached captures. Explain attach versus launch and one-shot teaching stops.',
  'Open the same checkout in Visual Studio Code. In the Director, expand Advanced controls and Demo '
  'Breakpoints. Verify that the attach extension is ready. Attach the Operations Agent row, then arm Agent '
  'Creation and Function Tool. The attached status matters: arming a checkbox by itself does not connect a '
  'debugger. These teaching stops are consumed on the next matching execution. We will also set an ordinary '
  'breakpoint in Foundry Operations Agent, immediately after the awaited Run Async call, so we can inspect '
  'its returned result. This is an attach session to an existing process started by Aspire. We are not '
  'launching a second copy of the application. Prepare the expressions you want to inspect before asking the '
  'question. Keep the live pauses short; we can freeze the edited video to explain the source without '
  'holding a real HTTP request open for several minutes.'),
 ('Stop and single-step in C sharp',
  'capture:vscode-question',
  None,
  'Show actual breakpoint and F10 transition from Pause to AsAIAgent. Highlight question, stage and tools.',
  'Click Ask agent in Command Center. Visual Studio Code stops at the Agent Creation teaching point. The '
  'yellow execution marker shows where this request is paused. Press F ten to step over to the next '
  'statement. In the captured request, the question variable is exactly: is streetlight L four seventeen on? '
  'The stage has already determined which capabilities were composed. Read the agent construction code: it '
  'supplies the model, instructions, tools, and any applicable context providers. This is the connection '
  'between the framework diagram and ordinary application code. We can inspect the request and admitted '
  'tools before allowing the model call to proceed. Press F five to continue. Because Function Tool is '
  'armed, the next relevant stop will show the C sharp method that the model selected, rather than asking us '
  'to infer tool execution from the final answer alone.'),
 ('Inspect the tool boundary',
  'capture:vscode-tool',
  None,
  'Start at the visible EnergyTools breakpoint, source 120s. Highlight Locals assetId, method and parameter '
  'Description, then gateway call. Freeze holds labeled.',
  'The yellow execution marker shows that the breakpoint has been reached. We are now in Energy Tools, '
  'inside Get Streetlight State Async. In Locals, the asset identifier is L four seventeen. Watch '
  "expressions from the caller are out of scope here; use this method's local values. Look at the "
  'Description attribute above the method, then the Description on the asset parameter. They provide the '
  'purpose and argument meaning we discussed on the short code slide. The enclosing Energy Tools class is '
  'ordinary C sharp. Registration through AI Function Factory happens in the capability composition code. '
  'Here we see execution: validate the input, get the correlation identifier, log the invocation, and call '
  'the Energy read gateway. The gateway reads through the Hub boundary. This is the deterministic part of '
  'the loop. The model selected a request; the framework brought us to this method, with these arguments. '
  'Continue with F five to the breakpoint after Run Async, where we can inspect the completed exchanges.'),
 ('Inspect AI results and model exchanges',
  'capture:vscode-exchanges',
  None,
  'Show post-RunAsync Watch then drill into Exchanges[0].Received. Hold the actual expanded tool request and '
  'highlight its name and arguments.',
  'We have reached the breakpoint after Run Async. The recorded exchanges let us reconstruct what happened '
  'inside that one awaited call. There are two model round trips in this request. Expand the first exchange, '
  'item zero, then Received and its result. Step one: the model requested the get streetlight state tool, '
  'with asset ID L four seventeen. The highlighted row is the actual request and arguments. The framework '
  'dispatched the function we just inspected. This is the selection side of the Context Engineering loop. We '
  "can observe a tool request and its execution without claiming to see the model's private reasoning. The "
  'content-type placeholder is not a reasoning transcript.'),
 ('The Hub result becomes the next context',
  'capture:vscode-exchanges',
  None,
  'Show actual Debug Console evaluation of Exchanges[1].Sent[0], source 322–329.8. Highlight actual booleans '
  'with narration.',
  'Now inspect what went into the second model call. In the Debug Console, evaluate the first Sent string of '
  'exchange one. The visible payload is the tool result sent back as context. Reported Is On is true: the '
  'device reports that the lamp is on. Is Daylight is true. The expected scheduled state is false: the '
  'schedule expects the lamp to be off. Manual Override is true. Those are separate observations, and '
  'together they explain why this incident deserves investigation. They came through the Energy Hub, not '
  "from the model's memory. This is the return arrow on our Context Engineering slide: deterministic "
  'execution contributes evidence to the next model interaction.'),
 ('The second exchange produces the answer',
  'capture:vscode-exchanges',
  None,
  'Show expanded second exchange Sent and Received plus response.Text. Highlight received answer. Then '
  'detach in a separate scene.',
  'Step two: the model receives that evidence and produces the answer. In the second exchange, compare Sent, '
  'containing the tool result, with Received, summarized on the exchange row as the statement that L four seventeen is currently '
  'reported on. The response text agrees. We can now explain both parts of the behavior: an agentic choice '
  'to request a useful capability, followed by deterministic execution, followed by a model answer grounded '
  'in the returned data. That is what the debugger adds beyond seeing a sentence in the browser. This '
  'recording has two model calls; another question could require a different number. An answer about the '
  'lamp does not mean we repaired it. We asked a read-only question.'),
 ('Detach VS Code through the Director',
  'capture:vscode-attached',
  None,
  'Show source 400–414 Director Detach VS Code and resulting status.',
  'Finish the request and remove the ordinary breakpoint. Return to the Director and choose Detach VS Code. '
  'Check that the debugger is no longer attached. The service remains available. Use the Director for this '
  'lifecycle so the demo controls and the debugger status stay in agreement.'),
 ('A real timeout and recovery',
  'card:recovery',
  None,
  'Explain the recorded 100-second web timeout and distinguish the debugger take from the fresh detached '
  'retry.',
  "There is a useful failure case here. A long debugger pause can outlast the web client's one hundred "
  'second attempt timeout, even though the agent service relaxes its own budget when a debugger is attached. '
  'Our recording encountered this. The debugger returned a model answer, while the browser had already '
  'stopped waiting. That does not prove a deployment failure. Continue the paused request, remove ordinary '
  'breakpoints, detach, and verify the Hub. Then retry this read-only question as a fresh request. The later '
  'browser answer belongs to that fresh retry. For write tools, do not generalize this advice into blindly '
  'resending commands. First reconcile the receipt and current state. Pause the video to study the code; '
  'keep pauses in your live HTTP request short.'),
 ('Switch debuggers without restarting the service',
  'capture:vs-attached',
  'Debug in Visual Studio 2026',
  'Show the actual Visual Studio attach, breakpoint, F10, Watch values, result and detach. Windows only.',
  'Choose the debugger that suits you. On a Mac, use VS Code. On Windows, either VS Code or Visual Studio '
  'twenty twenty-six is suitable. Visual Studio twenty twenty-six is Windows only. Both editors inspect the '
  'same C sharp request. First detach VS Code. In the Director, choose Attach Visual Studio twenty '
  'twenty-six on the Operations Agent row. Watch the status become attached. Arm Agent Creation and Function '
  'Tool, and set the ordinary breakpoint after Run Async. Ask the same read-only question in Command Center. '
  'The yellow marker shows the creation stop; Watch contains the operator question. F ten steps over a '
  'statement, and F five continues to the selected function. At Energy Tools, the inline asset ID is L four '
  'seventeen. Continue to the returned response. Watch now contains the answer and two exchanges. Expand '
  'Exchanges, item zero, then Received. This is the model response containing the tool request. Collapse '
  'that item and open item one, then Sent. This is the next model request containing the tool result. The '
  'tree gives you the path; the string visualizer can show longer values. In the VS Code take we inspected '
  'the full payload. These are observable messages, arguments, and data from the running request. Continue, '
  'remove the ordinary breakpoint, and click Detach Visual Studio twenty twenty-six in the Director. The row '
  'returns to debugger not attached. We have followed the same request through creation, tool invocation, '
  'and model response with the editor of our choice. The service remains running.'),
 ('Verify the outcome at the Hub',
  'capture:read-answer',
  'Verify, practice, and reset',
  'Show real detached retry answer and RAN trace. Pair with saved Hub and PID evidence in lab.',
  'The main application now shows the answer from the fresh read-only retry. The trace marks get streetlight '
  'state as ran. The Hub still reports that the lamp is on, and the operator has not issued a repair '
  'command. The Operations Agent process is still alive after detaching the debugger. These observations '
  'answer our four questions. The Hub supplied operational state. The model selected a read tool and formed '
  'an answer from its result. No authority to change the lamp was exercised in this onboarding request. And '
  'the fresh Hub read confirms that we did not silently turn the investigation into a repair. This is how '
  'the course will treat results: a UI message, a tool trace, and authoritative state each contribute '
  'evidence, but they answer different questions. Keep that habit when later labs introduce actions that can '
  'actually change the city.'),
 ('Pause, practice, and prepare Lab 01',
  'card:practice',
  None,
  'Hold the exercise card; learner repeats an IDE cycle, checks Hub/PID, resets Deterministic fixture. Stop '
  'after Unit00 review.',
  'Pause here and repeat one attach, stop, inspect, continue, and detach cycle without following the video. '
  "Use the companion lab's copyable commands to check the Hub and the process ID. Explain aloud where the "
  'operational truth lives, what the model selected, and why an answer did not repair the lamp. On Windows, '
  'you may repeat with the other IDE after the first is detached. On a Mac, complete the exercise in VS '
  'Code. If the user interface times out during a long pause, use the read-only recovery procedure we just '
  'demonstrated. When you are ready for Lab one, finish outstanding requests, clear the teaching stops and '
  'ordinary breakpoints, and choose Deterministic in the Director. Review and confirm the preparation '
  'preview, then verify the daylight anomaly again. A reset can change the revision, so discard old '
  'snapshots before the next exercise. You now have the architectural foundation and an inspectable '
  'environment. The next lab begins with the deterministic system and proves a repair through the Energy '
  'Hub.'),
 ('Investigate before the next lab',
  'card:questions',
  None,
  'End on code, documentation and AI investigation questions. Stop after Unit 00 for review.',
  'Before moving on, investigate three questions. First, trace the state read from Energy Tools to the '
  'Energy Hub endpoint. Which service owns the answer, and how is the correlation identifier propagated? '
  'Second, find the agent service and web client timeout policies. Why can one request produce a debugger '
  'result but no browser answer? Third, give an AI assistant a proposed responsibility and the agentic '
  'architecture skills. Ask whether it belongs in code, a model call, an agent, or a workflow. Challenge its '
  'recommendation with one failure case, then verify its claims in source and official documentation. Save '
  'your answer with a file location or source link and a reproducible observation. Every later lab ends with '
  'this kind of investigation. You now have the foundation, the tools, and a way to test what you learn. '
  'Enjoy building Agentic C sharp.')]

CARDS = {'outcomes': ('Four questions in every lab',
              'Observe: what does the system report?\n'
              'Infer: what does the agent conclude?\n'
              'Authorize: who may permit a change?\n'
              'Verify: what proves the outcome?'),
 'twin': ('The Hub owns operational state',
          'Reported ≠ desired\n'
          'Schedule and override\n'
          'Health and revision\n'
          'Command receipt\n'
          '\n'
          'A session is not a digital twin.'),
 'journey': ('One incident, growing needs',
             'Investigate with evidence\n'
             'Control consequential action\n'
             'Collaborate across boundaries\n'
             'Operate and improve\n'
             '\n'
             'Why → when → how → proof'),
 'setup': ('Prepare the lab',
           'Lab checkout: .NET 11\n'
           'Same Agent Framework patterns on .NET 10 LTS\n'
           'Select SDK → restore → build → test\n'
           'Configure existing model access\n'
           'Run the AppHost\n'
           '\n'
           'Copyable commands: companion lab'),
 'practice': ('Pause and do',
              'Attach → ask → inspect → continue\n'
              'Detach in the Director\n'
              'Verify Hub state and process ID\n'
              '\n'
              'Reset before the next lab\n'
              'Use your preferred IDE'),
 'title': ('Agentic C#',
           'Building C# Agentic Systems\n'
           'with Microsoft Agent Framework and Foundry\n'
           '\n'
           'by Alon Fliess\n'
           '\n'
           'Architect. Design. Implement.\n'
           'New systems and existing applications.'),
 'platform': ('Framework and platform',
              'Agent Framework: compose the application\n'
              'Foundry: connect and operate\n'
              '\n'
              'C# and .NET\n'
              'When, how, and why'),
 'recovery': ('A paused process still has callers',
              'Observed web timeout: 100 seconds\n'
              '\n'
              'Continue → detach → verify\n'
              'Retry this read-only question\n'
              '\n'
              'For writes, reconcile before retrying.'),
 'questions': ('Investigate, explain, verify',
               'CODE: trace the Hub boundary\n'
               'DOCS: explain the timeout\n'
               'AI CHAT: challenge a design choice\n'
               '\n'
               'Bring evidence for your answer.\n'
               'Enjoy the journey.'),
 'repository': ('A map of the solution',
                'Apps          Operator and lab UIs\n'
                'Services      Hubs and agents\n'
                'Contracts     Focused API boundaries\n'
                'AppHost       Local orchestration\n'
                'Tests + docs  Evidence and rationale')}

CARDS['editors'] = ('Choose your debugger', 'Windows: VS Code or Visual Studio 2026\nmacOS: VS Code\n\nAttach and detach through the Director\nInspect the same running C# request\n\nMac recording: a later lesson')
