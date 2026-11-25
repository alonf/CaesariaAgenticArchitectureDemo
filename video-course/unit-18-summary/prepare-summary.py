"""Author the closing chapter from approved artwork and reviewed course excerpts.

The source videos remain local. Narration is new; excerpt audio is never reused.
Run from any directory, then use build_course_lab.py with COURSE_UNIT=this folder.
"""
from pathlib import Path
import hashlib
import json
import subprocess
from PIL import Image, ImageDraw, ImageFont

UNIT = Path(__file__).resolve().parent
ROOT = UNIT.parent
SCENES = []
SOURCES = {}


def excerpt(number, scene_id, offset, seconds, anchor=None, focus=None):
    unit = ROOT / 'unit-00-agentic-csharp' if number == 0 else next(ROOT.glob(f'unit-{number:02}-*'))
    scene = next(s for s in json.loads((unit / 'output/timeline.json').read_text(encoding='utf-8')) if s['id'] == scene_id)
    assert offset + seconds <= scene['end'] - scene['start'], scene_id
    movie = next((unit / 'output').glob('*rough-cut.mp4'))
    key = movie.relative_to(ROOT).as_posix()
    SOURCES[key] = dict(sha256=hashlib.sha256(movie.read_bytes()).hexdigest(),
                        review='Previously rendered, privacy-redacted course export; reused as retrospective evidence.')
    result = dict(source='../' + key, start=round(scene['start'] + offset, 4), seconds=seconds)
    if anchor:
        result['narration_anchor'] = anchor
    if focus:
        result['focus'] = focus
    return result


def still(name, seconds=1, anchor=None):
    result = dict(source=f'assets/{name}.png', seconds=seconds)
    if anchor:
        result['narration_anchor'] = anchor
    return result


def deck(number, seconds=1, anchor=None):
    result = dict(source=f'../unit-00-agentic-csharp/assets/deck-{number}.png', seconds=seconds)
    if anchor:
        result['narration_anchor'] = anchor
    return result


def add(title, picture, narration, shots):
    SCENES.append(dict(title=title, picture=picture, narration=narration, shots=shots))


add('Course summary: look how far you have come',
    'Open on architecture slide 12 (previously shown around 00:42) and introduce the purpose of the course summary. Hold the complete picture throughout the introduction. At “We began”, return to the earlier Lab 01 daylight anomaly, then the branded journey map.',
    'Welcome to the course summary. Before we finish, let us step back and look at the complete picture. Throughout Agentic C sharp, we have explored how to architect, design and implement agentic systems using Microsoft Agent Framework and Microsoft Foundry. In this final chapter, we will connect the lessons, revisit the decisions behind the code, and turn what you have learned into a practical next step for your own system. We began with a light that was on during daylight. By the end, you could explain far more than whether it was on. You could identify the service that owns the facts, inspect the evidence behind an explanation, locate the decision that permits an action, and prove what actually happened. Let us retrace that journey using excerpts from our earlier labs. These are separate recorded experiments, with their own starting fixtures, not one uninterrupted run. The smart city gave us a concrete example. The skills you have developed apply to the C sharp systems you already own, and to the systems you will build next.',
    [deck(12), excerpt(1,'lab01-01',0,4,anchor='We began'), still('journey',anchor='Let us retrace')])

add('Keep the useful system at the center',
    'Slide 12 establishes the Hubs. Earlier Mac restore action, then genuine Hub JSON. End on the three-role card.',
    'Our first architectural decision was to establish a useful deterministic system. The Hubs connect independent city systems and expose a controlled view of their state. The Energy Hub owns the lighting operation. In the first lab, the operator requested Restore Scheduled Mode. The service checked the operation, called the controller boundary, and recorded its outcome. The independent Hub read then showed the reported state and the command receipt. The lamp in our lab is simulated; the software request and verification are real. Keep that separation in your own design: the agent can help choose a useful next step, while domain code owns the rules and the facts that establish success.',
    [deck(12), excerpt(1,'lab01-06',28,8,anchor='In the first lab'),
     excerpt(1,'lab01-07',0,8,anchor='The independent Hub read'), still('authority',anchor='Keep that separation')])

add('Understand the loop behind an answer',
    'Slide 14, followed by the two debugger exchanges from the introduction. Match each excerpt to its spoken cue.',
    'Then we added an investigation agent with Microsoft Agent Framework. Recall the context engineering diagram. Our code and the framework assemble the request, instructions, available tools and relevant context for the model. The model can request a tool; ordinary C sharp executes that capability, and the result becomes input to another model exchange. In the debugger, we inspected the selected function and its arguments. We then inspected the returned Hub facts and the answer built from them. Those are observable messages and results, not access to private model reasoning. One call from the application can contain several model and tool interactions. You can now debug that boundary instead of treating an answer as an unexplained string.',
    [deck(14), excerpt(0,'agentic-29',18,3.5,anchor='In the debugger'),
     excerpt(0,'agentic-30',3,12,anchor='We then inspected')])

add('Give context a meaning and a source',
    'Earlier work-order evidence, then empty retrieval. Use the context card to distinguish the four roles.',
    'The investigation became more useful when it could explain why the light was on. A retrieved work order supplied evidence with a source. When we removed that evidence, a good answer admitted what it could not establish. We also learned that different kinds of context have different jobs. A session carries the conversation. A remembered case supplies a lead to investigate. A skill supplies a procedure. None of those replaces a fresh authoritative read. When you design context for an agent, ask where each fact came from, whether it is current, and what it permits the agent to conclude. More context is useful only when its relevance and trust are understood.',
    [excerpt(4,'lab04-03',12,7), excerpt(4,'lab04-06',15,3.5,anchor='When we removed'),
     still('context',anchor='We also learned')])

add('Put authority at an explicit boundary',
    'Three control owners on a branded card, then the recorded protected-tool review and denial. The excerpt is a separate Controller Fault experiment.',
    'As soon as an operation has consequences, choosing an action and authorizing it become separate responsibilities. We demonstrated three control points. An MCP tool requested input while it was running. A workflow paused at a gate we deliberately placed in its graph. A protected function requested approval after the model selected that tool. The visible protected-call review named the capability and proposed arguments. Denial meant the invocation did not execute. For the physical repair workflow, approval also had to match the state being changed. A stale decision was refused. The lesson is to place enforcement at the boundary that can actually prevent the side effect, and verify both the permitted and denied paths.',
    [still('approval'), excerpt(10,'lab10-02',20,4,anchor='The visible protected-call review',focus=[940,500,960,540]),
     excerpt(10,'lab10-02',35.1,2,anchor='Denial meant',focus=[940,330,960,540]),
     still('authority',anchor='For the physical repair workflow')])

add('Collaborate only when a boundary earns its place',
    'Earlier specialist verdict and workforce peer card. End on the decision questions, not a feature list.',
    'The next advance was knowing when another capability or agent adds value. MCP moved a focused capability across a service boundary. The security specialist contributed a bounded judgment from its own domain. The workforce peer exposed a shareable view of work, while private fields stayed outside the accessible tool result. These examples teach a design question: what knowledge, responsibility or execution boundary justifies this separation? Another agent introduces more communication, latency and failure paths. Use one when the responsibility deserves it. Keep discovery, identity, permission and the data projection explicit. A protocol connection does not by itself grant access to everything behind the endpoint.',
    [excerpt(11,'lab11-03',12,8), excerpt(12,'lab12-03',12,6,anchor='The workforce peer'),
     still('boundaries',anchor='These examples teach')])

add('Make progress and failure observable',
    'Earlier durable pending review, worker controls, then approval/Hub proof. Each cut follows a narration anchor; do not suggest scheduler failure was tested.',
    'Operating an agentic system means knowing which work has really completed. The durable lesson made that distinction visible. We first inspected an actual pending approval. We interrupted only the local Functions worker, while the scheduler and city services remained available. After continuation, the original review still governed the decision. Approval was followed by a terminal result and an independent Hub check. This demonstrated that particular recovery boundary, not universal exactly-once execution. Remembering a conversation and resuming a business operation are separate requirements. Choose the hosting and persistence model from those requirements, and make pending, failed, completed and unknown outcomes distinguishable to the operator.',
    [excerpt(15,'lab15-02',16,6), excerpt(15,'lab15-03',2,5,anchor='We interrupted only'),
     excerpt(15,'lab15-03',25,3,anchor='After continuation'),
     excerpt(15,'lab15-04',24,8,anchor='Approval was followed'), still('operations',anchor='Remembering a conversation')])

add('Measure before you expand',
    'Recorded ASSERT report, then explicit optional-preparation boundary and the engineering feedback loop.',
    'Evaluation gave us a way to compare intended behavior with evidence. We read the requirement, checked the actual tools and outcomes, and kept a behavioral failure separate from an evaluation error. A small passing sample is a useful checkpoint, not a production guarantee. The optional Routines and Insights lessons prepared activation and an engineering feedback loop. They did not demonstrate successful routine dispatch or a retrieved generated Insight. Carry that honesty into your own work. Define representative cases, include missing evidence and dependency failures, inspect traces, and turn a discovered problem into a regression check. Then make a bounded change and measure again.',
    [excerpt(14,'lab14-04',0,12), still('measure',anchor='The optional Routines')])

add('Choose the smallest sufficient design',
    'Return to the Agentic Decision Map slide. Display the architecture-skills repository on the branded resource card.',
    'You now have a decision process, not just a collection of APIs. Use deterministic code when the steps and rules are known. Consider a bounded model operation when interpretation or generation is the missing capability. Use an agent when the next useful step depends on what it discovers. Introduce an explicit workflow when the path needs planned control, and additional agents when separate responsibilities justify them. Microsoft Agent Framework supplies development building blocks; Microsoft Foundry supplies platform capabilities you select for the solution. The Agentic Architecture Skills repository can help structure that discussion with a coding assistant. Review the reasoning and the resulting design yourself. The requirement should explain every added mechanism.',
    [deck(6), still('resources',anchor='The Agentic Architecture Skills repository')])

add('Your first path: extend a system you own',
    'New action card for an existing application, then earlier C# tool implementation as the narrow-boundary example.',
    'If you already own a working application, start with one difficult question its users ask. Keep the existing domain services as the authority. Expose one narrow read capability through its supported API and let an agent investigate without changing state. Compare the answer with a direct service read. Add selected knowledge only when the question requires it. When you later introduce an action, give it a focused contract, validation, authorization, a receipt and a defined failure path. The C sharp function we inspected is a useful pattern: a small capability over a controlled service boundary. Your first milestone is one demonstrably useful investigation, with evidence, inside the application people already use.',
    [still('existing'), excerpt(2,'lab02-05',4,12,anchor='The C sharp function')])

add('Your second path: design a new system',
    'New architecture card with a five-step vertical sequence and a slide-12 thumbnail. No new cloud execution.',
    'For a new agentic system, begin with the users, the decisions they need to make, and the outcomes they must be able to trust. Identify the systems of record and draw the domain boundaries before deciding how many agents to create. Implement the deterministic operations and their contracts. Then add the context and capabilities that support one meaningful end-to-end task in C sharp. Decide which steps need planned orchestration, human authority, durable progress or separate reasoning. Choose identity, deployment and observation deliberately. The first useful version should be small enough to inspect completely. Expand it when a new requirement shows why the existing design is insufficient, and bring the evaluation cases forward with it.',
    [still('new-system')])

add('Pause and write your first design brief',
    'Capstone card. The companion brief supplies copyable fields, success criteria and investigation questions.',
    'Pause here and choose one of those two paths. Write a one-page brief for a real use case. Name the user outcome, the authoritative source, the agent responsibility, the permission boundary and the evidence of completion. Sketch one successful request and one failure. For an existing system, identify the first read-only capability you can expose. For a new system, identify the first deterministic operation you must build. Then challenge your proposal: why does it need an agent, what happens when evidence is missing, and how will you know a side effect happened? Use the companion worksheet, inspect the lab code, consult the documentation, or discuss your design with an AI assistant. Keep ownership of the final engineering decision.',
    [still('capstone')])

add('Build, explain, and share the journey',
    'Brief earlier repaired-city view, then the branded closing card with author, course name and sharing invitation.',
    'The progress is visible in how you can now explain this simple light. You can trace observation to evidence, distinguish an agent request from an authorized operation, and prove the outcome through the owning service. Take that discipline into your next C sharp project. Add an agentic layer where it makes an existing system more useful, or architect, design and implement a new system around clear responsibilities. If this course helped you, share Agentic C sharp with a colleague or your team. Invite them to run a lab with you and compare design decisions, not just answers. Thank you for joining me on Building C sharp Agentic Systems with Agent Framework and Foundry. I am Alon Fliess. Enjoy the journey, and build something you can explain.',
    [excerpt(1,'lab01-07',24,6), still('closing',anchor='Take that discipline')])

CARDS = {
    'journey': ('From a question to a trusted outcome', ['Observe the real system', 'Investigate with evidence', 'Authorize consequential actions', 'Verify, recover and improve']),
    'authority': ('Three responsibilities', ['Agent: investigate and propose', 'Policy / human: authorize', 'Domain service: enforce and record', 'Independent read: prove the outcome']),
    'context': ('Context has different jobs', ['Session: conversation continuity', 'Knowledge: evidence with provenance', 'Memory: a lead to investigate', 'Skill: a procedure to follow', 'Hub: current operational truth']),
    'approval': ('Who owns the pause?', ['MCP input: the executing tool', 'Workflow gate: the planned graph', 'Tool approval: the protected invocation', 'Check denial and stale state too']),
    'boundaries': ('Give each boundary a reason', ['What knowledge does it own?', 'What is it allowed to disclose?', 'Who may request which capability?', 'How will failures be observed?']),
    'operations': ('Conversation and operation', ['Context continuity and durable progress', 'Pending / failed / completed / unknown', 'Identity, hosting and persistence', 'A receipt plus a fresh state read']),
    'measure': ('Keep improving from evidence', ['Requirement → observed behavior', 'Failure → hypothesis → regression', 'Bounded change → measured result', 'Routines / Insights: preparation only']),
    'resources': ('Continue the design conversation', ['Microsoft Learn: Agent Framework', 'Companion book and lab source', 'github.com/alonf/', 'agentic-architecture-skills', 'Review the decision; own the result.']),
    'existing': ('Add a useful agentic layer', ['1. Choose one difficult user question', '2. Keep existing services authoritative', '3. Expose one narrow read capability', '4. Verify before adding write authority', '5. Measure usefulness in the real UI']),
    'new-system': ('Architect, design, implement', ['1. Define outcomes and domain owners', '2. Build deterministic operations', '3. Add one useful C# agent capability', '4. Design authority and persistence', '5. Evaluate, observe and evolve']),
    'capstone': ('Your one-page design brief', ['Outcome and user', 'Authoritative source and agent role', 'Permission and failure boundaries', 'Success evidence and counterexample', 'Next step: one bounded experiment']),
    'closing': ('Build something you can explain', ['Extend your existing system', 'Or design a new C# agentic system', 'Run a lab with a colleague', 'Share Agentic C# with your team', 'Enjoy the journey.']),
}

THUMBNAILS = {
    'journey': ('lab', 1, 'lab01-01', 2),
    'authority': ('deck', 12), 'context': ('deck', 14),
    'existing': ('lab', 2, 'lab02-05', 8),
    'new-system': ('deck', 12), 'closing': ('lab', 1, 'lab01-07', 26),
}


def prepare():
    (UNIT / 'assets').mkdir(exist_ok=True)
    for name, (title, lines) in CARDS.items():
        canvas = Image.open(ROOT / 'unit-00-agentic-csharp/assets/signature-background.png').convert('RGB').resize((1920,1080))
        draw = ImageDraw.Draw(canvas)
        def text(x, y, value, size, color='white'):
            font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', size)
            bounds = draw.textbbox((x,y),value,font=font)
            assert bounds[2] < 1830 and bounds[3] < 990, (name,value,bounds)
            draw.text((x,y),value,font=font,fill=color)
        text(95,65,'Agentic C#  |  The journey and your next step',36,'#8de6ed')
        text(95,155,title,62)
        thumb = THUMBNAILS.get(name)
        for i,line in enumerate(lines):
            text(100,300+i*105,line,40 if thumb else 48)
        if thumb:
            if thumb[0]=='deck':
                im=Image.open(ROOT / f'unit-00-agentic-csharp/assets/deck-{thumb[1]}.png')
            else:
                q=excerpt(thumb[1],thumb[2],thumb[3],.1)
                capture=UNIT/'assets'/f'{name}-reference.png'
                subprocess.run(['ffmpeg','-y','-v','error','-ss',str(q['start']),'-i',str((UNIT/q['source']).resolve()),'-frames:v','1',str(capture)],check=True)
                im=Image.open(capture)
            im=im.convert('RGB').resize((800,450))
            canvas.paste(im,(1010,325))
            draw.rectangle((1008,323,1812,777),outline='#8de6ed',width=2)
            text(1015,800,'Earlier course example',28,'#8de6ed')
        text(100,900,'Alon Fliess  ·  Building C# Agentic Systems',32,'#8de6ed')
        canvas.save(UNIT/'assets'/f'{name}.png')

    script=['# Closing chapter — the journey and your next step','','New synthetic review narration over explicitly retrospective course excerpts. No new live run or cloud deployment. Exact final timestamps are generated in output/timed-shooting-script.md.','']
    shots={}
    for i,s in enumerate(SCENES,1):
        script += [f'## {i:02} — {s["title"]}','',f'**Picture:** {s["picture"]}','',f'**Narration:** {s["narration"]}','']
        shots[f'lab18-{i:02}']=s['shots']
    (UNIT/'shooting-script.md').write_text('\n'.join(script),encoding='utf-8')
    (UNIT/'shots.json').write_text(json.dumps(shots,indent=2)+'\n',encoding='utf-8')
    (UNIT/'sources.json').write_text(json.dumps(SOURCES,indent=2)+'\n',encoding='utf-8')
    privacy=ROOT/'production-batch/privacy-redactions.json'
    data=json.loads(privacy.read_text(encoding='utf-8'))
    for key in SOURCES:
        data['sources'][key]=[]  # Already-redacted exports; hashes and rationale in sources.json.
    privacy.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
    print('Prepared',len(SCENES),'scenes and',len(CARDS),'branded cards')


if __name__=='__main__':
    prepare()
