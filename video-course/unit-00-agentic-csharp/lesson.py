"""Agentic C# Unit 00: branded foundation and genuine motion walkthroughs."""
import importlib.util
from pathlib import Path

_spec = importlib.util.spec_from_file_location('previous_foundation', Path(__file__).parents[1] / 'unit-00-foundations/lesson.py')
_old = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_old)

LESSON = [
    ('Agentic C#', 'card:title', 'Welcome to Agentic C#', 'Open on the new course title and Alon signature background. No city imagery yet.',
     'Welcome to Building C sharp Agentic Systems with MAF and Foundry, by Alon Fliess. Agentic C sharp for short. Agentic development is moving fast. It is easy to assemble a convincing demonstration before understanding the foundations that make its architecture dependable. This course gives you the background and the practical toolset to make informed decisions. You will learn to ask what belongs in deterministic code, where a language model helps, when an agent is justified, and when explicit orchestration is needed. You will run the code, inspect its behavior, and explain the tradeoffs. Our goal is engineering judgment you can use in your own applications.'),
    ('The framework and the platform', 'card:platform', None, 'Introduce MAF and Microsoft Foundry as complementary responsibilities, before the example domain.',
     'We will go deep into Microsoft Agent Framework as our development framework and Microsoft Foundry as our platform. The framework gives our C sharp application a way to compose agents, tools, context, sessions, and workflows. The platform provides the model and agent services and the operational capabilities we choose to use. We will ask when to use each capability, how to integrate it, and why it belongs in the design. You will find many agent examples in Python. Here we focus on C sharp and dot net, connecting agentic development to the software engineering practices you already know. Running an agent locally can still involve a real remote model call. Hosting, conversational state, operational memory, and durable execution are separate decisions that we will examine carefully.'),
    ('Build a system around the agent', 'deck:7', None, 'Trace deterministic code, model decisions, tools and governance in the composition slide.',
     'Building an agentic system involves much more than constructing an agent object. The system needs reliable deterministic code underneath it. The agent needs knowledge to interpret a situation and tools that give it controlled ways to act. We decide which responsibilities are ordinary code, bounded model calls, a single agent, a planned workflow, or collaboration among agents. We also decide what policy admits, who can authorize consequential actions, and what evidence proves an outcome. These decisions affect testing, security, latency, cost, and recovery. Throughout the course, each new capability must earn its place by solving a concrete requirement. You will see both the mechanics and the reasoning behind them.'),
    ('Meet Alon Fliess', 'deck:2', None, 'Use the supplied About Me slide, with its own logo and book imagery.',
     'This course was developed by Alon Fliess, Microsoft Foundry MVP, Microsoft Regional Director, and chief technology officer at ZioNet. Alon brings more than forty years of hands-on experience, together with his work as an architect, author, and speaker. The companion book develops the architectural rationale; the code and labs let you test those ideas for yourself. Treat the lessons as an invitation to investigate. Pause, change a condition, inspect the result, and explain what you discovered. You do not need to accept a model answer or an instructor statement without evidence. Enjoy the journey, and make the lab your own.'),
]

for i, item in enumerate(_old.LESSON[1:]):
    title, visual, chapter, action, narration = item
    visual = visual.replace('H08:', 'deck:')
    action = action.replace('H08', 'the supplied branded deck')
    if title == 'Caesarea is a system of systems':
        narration = 'Our example is Caesarea, a smart city. The city is our means of learning agentic architecture; the course goal is the engineering you can transfer to another domain. ' + narration
    if title == 'Prepare a reproducible checkout':
        narration = ('We use main, with the dot net eleven code merged into it. Clone the repository, select main, and record your commit before starting. The companion lab records the commit used for this video. Read global dot json, select the SDK, restore, build, and run the test project. Open this same folder in both editors so the source matches the running binaries. Install the repository\'s VS Code attach extension, and on Windows build the Visual Studio helper. For the AI exercises, use an existing Foundry project and model deployment that your account can invoke. Set the local environment variables shown in the lab, then start the AppHost from that shell. Keep the shell open. We run the services locally and use remote model inference. This lesson creates no cloud resources. Complete sign-in before recording and keep private tokens out of the video.')
    if title == "Command Center is the operator's application":
        narration = narration.replace('Now switch to the main Command Center.', 'Now switch to the main Command Center. Open Walkthrough at the upper right. Read its starting conditions, numbered actions, code references, and the point of the demonstration. This is your in-app guide throughout the labs. Close the panel when you need the agent controls.')
    if title == 'A real timeout and recovery':
        visual = 'card:recovery'
        action = 'Explain the recorded 100-second web timeout and distinguish the debugger take from the fresh detached retry.'
        narration = ('There is a useful failure case here. A long debugger pause can outlast the web client\'s one hundred second attempt timeout, even though the agent service relaxes its own budget when a debugger is attached. Our recording encountered this. The debugger returned a model answer, while the browser had already stopped waiting. That does not prove a deployment failure. Continue the paused request, remove ordinary breakpoints, detach, and verify the Hub. Then retry this read-only question as a fresh request. The later browser answer belongs to that fresh retry. For write tools, do not generalize this advice into blindly resending commands. First reconcile the receipt and current state. Pause the video to study the code; keep pauses in your live HTTP request short.')
    if title == 'Switch debuggers without restarting the service':
        action = 'Show the actual Visual Studio attach, breakpoint, F10, Watch values, result and detach. Windows only.'
        narration = ('Choose the debugger that suits you. On a Mac, use VS Code. On Windows, either VS Code or Visual Studio twenty twenty-six is suitable. Visual Studio twenty twenty-six is Windows only. Both editors inspect the same C sharp request. First detach VS Code. In the Director, choose Attach Visual Studio twenty twenty-six on the Operations Agent row. Watch the status become attached. Arm Agent Creation and Function Tool, and set the ordinary breakpoint after Run Async. Ask the same read-only question in Command Center. The yellow marker shows the creation stop; Watch contains the operator question. F ten steps over a statement, and F five continues to the selected function. At Energy Tools, the inline asset ID is L four seventeen. Continue to the returned response. Watch now contains the answer and two exchanges. Expand Exchanges, item zero, then Received. This is the model response containing the tool request. Collapse that item and open item one, then Sent. This is the next model request containing the tool result. The tree gives you the path; the string visualizer can show longer values. In the VS Code take we inspected the full payload. These are observable messages, arguments, and data from the running request. Continue, remove the ordinary breakpoint, and click Detach Visual Studio twenty twenty-six in the Director. The row returns to debugger not attached. We have followed the same request through creation, tool invocation, and model response with the editor of our choice. The service remains running.')
    if title == 'Verify the outcome at the Hub':
        narration = narration.replace('separate read-only retry', 'fresh read-only retry')
    LESSON.append((title, visual, chapter, action, narration))
    if title == 'Choose the smallest sufficient mechanism':
        LESSON.append(('Use the architecture skills', 'deck:10', None, 'Show Alon\'s architecture skills slide and github.com/alonf/agentic-architecture-skills.',
          'Alon\'s agentic architecture skills repository turns this decision map into a practical design aid. The architecture router helps decompose responsibilities and choose the least autonomous mechanism that meets the requirement. The MAF mapping skill then translates an agreed architecture into framework, hosting, governance, and assurance decisions. Use these skills with a coding assistant to challenge your design, not simply to generate more agents. Their advice still needs review, tests, and enforcement in your application. In the lab exercise, bring one responsibility from your own system, ask for a recommendation, and verify the reasoning against the code and current documentation. The repository link is on screen and in the companion lab.'))

LESSON.append(('Investigate before the next lab', 'card:questions', None, 'End on code, documentation and AI investigation questions. Stop after Unit 00 for review.',
    'Before moving on, investigate three questions. First, trace the state read from Energy Tools to the Energy Hub endpoint. Which service owns the answer, and how is the correlation identifier propagated? Second, find the agent service and web client timeout policies. Why can one request produce a debugger result but no browser answer? Third, give an AI assistant a proposed responsibility and the agentic architecture skills. Ask whether it belongs in code, a model call, an agent, or a workflow. Challenge its recommendation with one failure case, then verify its claims in source and official documentation. Save your answer with a file location or source link and a reproducible observation. Every later lab ends with this kind of investigation. You now have the foundation, the tools, and a way to test what you learn. Enjoy building Agentic C sharp.'))

CARDS = dict(_old.CARDS)
CARDS.update({
 'title': ('Agentic C#', 'Building C# Agentic Systems\nwith MAF and Foundry\n\nby Alon Fliess\n\nUnit 00 · Foundations, environment and debugging'),
 'platform': ('Understand when, how and why', 'Microsoft Agent Framework · development framework\nMicrosoft Foundry · platform\n\nC# and .NET throughout\nKnowledge + tools + policy + governance\nInspect the behavior. Prove the outcome.'),
 'setup': ('Build and run from main', 'git clone https://github.com/alonf/CaesariaAgenticArchitectureDemo.git\ncd CaesariaAgenticArchitectureDemo\ngit switch main\n. ./scripts/Use-CaesareaSdk.ps1\ndotnet restore\ndotnet build\n\nConfigure your existing Foundry project and model.\nStart Caesarea.AppHost from the configured shell.\nCopyable commands and baseline: lab.md'),
 'recovery': ('A paused process still has callers', 'Observed: web attempt timeout = 100 seconds\n\nContinue → clear breakpoints → detach → verify Hub\nThen retry this read-only request.\n\nFor writes: reconcile receipt and state before retrying.\nPause the video for study; keep live debugger waits short.'),
 'questions': ('Investigate, explain, verify', 'CODE     Trace EnergyTools → gateway → Energy Hub.\nDOCS     Explain the two timeout policies.\nAI CHAT  Challenge an architecture recommendation.\n\nProvide a file location, source link or reproducible test.\n\ngithub.com/alonf/agentic-architecture-skills\n\nEnjoy the journey.'),
})

# Extend the existing pilot: preserve its foundation and replace only the takes
# and explanations that need stronger orientation or observable debugger values.
def revise(title, *, narration=None, action=None, visual=None):
    for index, item in enumerate(LESSON):
        if item[0] == title:
            t, v, c, a, n = item
            LESSON[index] = (t, visual or v, c, action or a, narration or n)
            return
    raise ValueError(title)

revise('Agentic C#', narration=LESSON[0][4] +
    ' This course is for C sharp developers who need to architect, design, and implement new agentic systems, or add agentic capabilities to existing software. We assume familiarity with C sharp development, but no previous experience building agents. If you already know the APIs, the architectural decisions will help you understand when to use what, and why.')

orientation = [
    ('Find your way around the repository', 'card:repository', 'The code, framework and platform',
     'Show actual GitHub root, README introduction cropped above conference details, then cumulative stages. Transition to the local Explorer tree.',
     'Before exploring the city, let us connect the moving parts. A repository is more than the agent class: it contains the executable system, its contracts, tests, and instructions for running it. Open the Caesarea Agentic Architecture Demo on GitHub. The README introduces the system and maps its cumulative stages. Main contains the implemented capabilities, selected at runtime. We do not maintain a separate application for each lesson. In the local solution, Apps contains the two user interfaces. Services contains the deterministic services and agent services. Each API boundary has its own Contracts project. The AppHost starts the local system, Tests verifies behavior, and docs explains the stage decisions. Keep the README and the companion lab nearby. This recording uses the merged local main checkout identified in the lab; the remote source must be synchronized before these instructions are released. You now have a map from a visible component to the place where its implementation lives.'),
    ('Use the Microsoft Learn starting point', 'deck:15', None,
     'Show Microsoft Learn Your First Agent with C# selected, then Add Tools. Explain documentation navigation before signatures.',
     'For framework concepts and API examples, use Microsoft Learn. Start in the Agent Framework get-started sequence and select C sharp. Your First Agent introduces construction and invocation; Add Tools adds a callable function. Use this sequence to isolate a framework idea before looking at our larger application. Documentation and packages evolve, so compare a sample with the versions in your checkout. The companion lab links these pages. We now have two complementary references: the repository explains this system, while Learn explains the framework building blocks.'),
    ('Locate the project and its hosted agent', 'card:platform', None,
     'Read-only Foundry portal recording: project home, Agents list, caesarea-operations, Details. No invocation, deployment or setting changes.',
     'The platform is the next moving part. A Foundry project supplies the environment in which model and agent resources are organized. Here is this solution\'s existing project, caesarea dev. Open Agents. The list contains caesarea operations, with kind Hosted and status Running. Its details identify an active version, identity, and Responses endpoint. This is a real deployed agent, not a slide illustration. This visit is read-only: we are locating an existing deployment, not deploying one. Keep two execution paths distinct. Our upcoming debugger exercise runs the Operations Agent locally and calls remote model inference. The hosted agent runs in Foundry and uses the cloud Energy Hub; the local Director does not reset that cloud city. Later hosting lessons will explain its composition and identity boundaries. For now, recognize where the platform lives and how to locate an agent. You do not need to create a hosted agent to complete the local introductory exercise.'),
    ('The smallest agent call in C sharp', 'deck:17', None,
     'Return to Learn creation and RunAsync example; show actual local Program.cs AskAsync handler and typed response. The documentation sample is read, not claimed as independently executed.',
     'Start with the smallest useful shape: construct an agent, pass a question, await a response, and use the result. The Learn example makes that shape visible without our city application around it. Its project client is configured with an endpoint and credentials. As AI Agent supplies the model and instructions. Run Async accepts the question and returns the response. That sample is a code-reading example here. Now connect the shape to our real application. In Operations Agent API, Program dot C sharp receives a typed question request. Its handler calls Ask Async with the question, session, and correlation identifier, then builds a typed response for the caller. Inside Foundry Operations Agent, the same construction and Run Async pattern is surrounded by this system\'s policies and capabilities. A tool adds another step: the model can request a named function, the framework invokes admitted code, and the result becomes input to the next model interaction. We will see that exact sequence in the debugger. The basic unit remains familiar asynchronous C sharp; the new design work is deciding what the model may know and do.'),
]
insert_at = next(i for i, s in enumerate(LESSON) if s[0] == 'Caesarea is a system of systems')
LESSON[insert_at:insert_at] = orientation

revise('Inspect the tool boundary', narration=next(s[4] for s in LESSON if s[0]=='Inspect the tool boundary').replace(
    'The asset ID is L four seventeen.',
    'The asset ID is L four seventeen. The Watch expressions from Ask Async are out of scope here. Use Locals for this method\'s arguments.'))

revise('Inspect AI results and model exchanges',
    action='Record Watch expansion through Exchanges[0].Received and the tool call; use Debug Console for Exchanges[1].Sent[0]. Cut failed hex-editor/copy attempts. End on Director detach.',
    narration='A final answer alone hides how the agent obtained its facts. We will inspect the message exchange as well as the returned text. After Run Async, Watch shows that the lamp is currently reported on. Expand model Flight Recorder dot Exchanges. There are two round trips in this captured request. Open item zero, Received, and its result. The visible tool request is get streetlight state, with asset ID L four seventeen. The model selected that request; the framework dispatched the C sharp function we just stepped through. Now inspect item one, Sent. Use the Debug Console to evaluate its first string when the tree truncates a long value. The payload is the actual tool result: reported Is On is true, daylight is true, the expected scheduled state is false, and the manual override is true. These are Hub observations. The content-type placeholder in the recorded message does not expose private model reasoning. The final answer is grounded in this returned data. This completes the loop: question, selected tool and arguments, invocation, result, and answer. Finish the request, remove the ordinary breakpoint, and use Detach VS Code in the Director. We can now explain the answer from observable execution evidence.')

revise('Prepare a reproducible checkout', narration=next(s[4] for s in LESSON if s[0]=='Prepare a reproducible checkout').replace(
    'Clone the repository, select main, and record your commit before starting.',
    'The companion lab identifies the local review baseline and the clean-clone release check. When using the published course, clone the repository, select main, and record your commit before starting.'))
revise('Pause, practice, and prepare Lab 01', narration=next(s[4] for s in LESSON if s[0]=='Pause, practice, and prepare Lab 01').replace(
    'Then repeat with the other IDE after the first is detached.',
    'On Windows, you may repeat with the other IDE after the first is detached. On a Mac, complete the exercise in VS Code.'))

# Short, large type; copyable setup and long payloads belong in the companion lab.
CARDS.update({
 'title': ('Agentic C#', 'Building C# Agentic Systems\nwith MAF and Foundry\n\nby Alon Fliess\n\nArchitect. Design. Implement.\nNew systems and existing applications.'),
 'repository': ('A map of the solution', 'Apps          Operator and lab UIs\nServices      Hubs and agents\nContracts     Focused API boundaries\nAppHost       Local orchestration\nTests + docs  Evidence and rationale'),
 'platform': ('Framework and platform', 'MAF: compose the application\nFoundry: connect and operate\n\nC# and .NET\nWhen, how, and why'),
 'outcomes': ('Four questions in every lab', 'Observe: what does the system report?\nInfer: what does the agent conclude?\nAuthorize: who may permit a change?\nVerify: what proves the outcome?'),
 'twin': ('The Hub owns operational state', 'Reported ≠ desired\nSchedule and override\nHealth and revision\nCommand receipt\n\nA session is not a digital twin.'),
 'journey': ('One incident, growing needs', 'Investigate with evidence\nControl consequential action\nCollaborate across boundaries\nOperate and improve\n\nWhy → when → how → proof'),
 'setup': ('Build and run from main', 'Record the course commit\nSelect the .NET SDK\nRestore → build → test\nConfigure existing model access\nRun the AppHost\n\nCopyable commands: companion lab'),
 'recovery': ('A paused process still has callers', 'Observed web timeout: 100 seconds\n\nContinue → detach → verify\nRetry this read-only question\n\nFor writes, reconcile before retrying.'),
 'practice': ('Pause and do', 'Attach → ask → inspect → continue\nDetach in the Director\nVerify Hub state and process ID\n\nReset before the next lab\nUse your preferred IDE'),
 'questions': ('Investigate, explain, verify', 'CODE: trace the Hub boundary\nDOCS: explain the timeout\nAI CHAT: challenge a design choice\n\nBring evidence for your answer.\nEnjoy the journey.'),
})
