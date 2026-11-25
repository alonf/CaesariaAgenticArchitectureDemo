"""Reproducible editorial decisions; all times address the original recordings."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
configs = {n: {'inserts': [], 'revisions': {}, 'shots': {}, 'phrase_markers': {}} for n in range(18)}
units = {n: next(ROOT.glob(f'unit-{n:02}-*')) for n in range(1,18)}
units[0] = ROOT / 'unit-00-agentic-csharp'


def shot(source, start, seconds, focus=None, point=None, hold=0):
    s = dict(source='../production-batch/assets/'+source+'.mp4', start=start, seconds=seconds)
    if focus: s['focus'] = focus
    if point: s['point'] = point
    if hold: s['hold'] = hold
    return s


def add(n, before, key, title, narration, shots):
    configs[n]['inserts'].append(dict(before=f'lab{n:02}-{before:02}', id=f'lab{n:02}-clarity-{key}',
        title=title, actions='Actual source footage; deliberate close-up and editorial emphasis. Holds are labeled.',
        narration=narration, shots=shots))


def setup(n, before, stage, source, start, seconds, explanation):
    add(n,before,'prepare','Locate, select and confirm '+stage,
        f'Let us make the preparation visible. In Demo Director, locate {stage}. Select that named capability. '
        f'Read the preparation review before confirming. {explanation} '
        'The selected chip is a pending choice until confirmation. After applying it, check the stage and the readiness message, then return to Command Center.',
        [shot(source,start,5,[285,40,1344,756]),
         dict(**shot(source,start+seconds-4,.2,[1120,630,800,450]),narration_anchor='Read the preparation review'),
         dict(**shot(source,start+seconds-3,3),narration_anchor='After applying it'),
         shot(source,start+seconds+4,.2)])


def doc(n,before,key,title,narration,filename):
    add(n,before,'learn-'+key,title,narration,[dict(source='../production-batch/assets/clarity/'+filename+'.png',seconds=45)])


setup(2,3,'Investigation Agent','windows-02-03',12,15,
      'The review changes the capability stage. The daylight fixture is already applied in this take. If your lamp was repaired in Lab one, apply that fixture again first.')
setup(3,2,'Session','windows-03-06',12,9,
      'This review changes the capability stage. It does not by itself prove that the daylight fixture was reset. Check L-417 separately before asking.')
setup(4,2,'Knowledge','windows-03-06',378,16,
      'Here the review lists both the daylight scenario and the Knowledge stage. Confirming prepares those prerequisites; it does not ask the agent a question.')
setup(5,2,'Memory','windows-05-06',24,7,
      'This transition adds memory to the already explained incident. Keep the work evidence available and finish the grounded explanation before recording a case.')
setup(6,2,'Skills','windows-05-06',361,12,
      'This review changes the stage. The lesson still needs the daylight anomaly and work evidence. A stage selection is not an investigation result.')
setup(7,2,'MCP Tools','windows-07-08',19,12,
      'This adds a service boundary. Start with Local Function selected in Tool Source so that the first request remains a useful comparison.')
setup(8,2,'Interactive Input','windows-07-08',431,12,
      'The remote restore tool is available in this stage. Keep MCP remote selected and check that the lamp is on under an override before starting.')
setup(9,2,'Workflow','windows-09-10',22,14,
      'This review reapplies the daylight anomaly as well as changing the stage. That reset is necessary because the previous lesson repaired the lamp.')
setup(10,2,'Tool Approval under Controller Fault','windows-09-10',765,9,
      'Use the Controller Fault card, not the daylight override card. The model will investigate a fault, and the protected action is creating maintenance work.')
setup(11,2,'Multi-Agent under Security Operation','windows-11-12',14,12,
      'The review selects Security Operation. Intentional lighting is a different fixture from a forgotten override. Start with Security Consult off.')
setup(12,2,'A2A Delegation','windows-11-12',240,13,
      'This review restores the reported daylight anomaly and selects peer delegation. An open work order can explain the override; it does not repair it.')
setup(13,2,'Hosting','windows-13-hosting',29,8,
      'This stage makes the hosting comparison available. It does not deploy a cloud agent or change the Agent Habitat selector. We change that selector separately.')

doc(3,5,'session','Connect Microsoft Learn sessions to our store',
    'Before reading our store, here is the Session page on Microsoft Learn. A session carries conversational state between runs. How that state is kept and restored depends on the agent and the storage we provide. In this lab, the application serializes a session and saves it in an in-memory store. That is continuity within the running service, not durable recovery after a restart. Next, follow that choice into AgentSessionStore and the restoration code.', 'learn-session')
doc(4,4,'rag','Connect retrieval modes to KnowledgeCapability',
    'Microsoft Learn describes two retrieval patterns here. Context can be supplied before a model invocation, or a search tool can let the model request evidence when needed. Our KnowledgeCapability uses the second pattern with TextSearchProvider. The callback searches the seeded work records and returns evidence with identifiers. This local demonstration does not require Azure AI Search. Now return to the C sharp composition and identify the on-demand setting, the callback, and the evidence that reaches the trace.', 'learn-rag-mechanism')
doc(6,4,'skills','Connect progressive disclosure to SKILL.md',
    'The Agent Skills page on Microsoft Learn explains progressive disclosure. The agent can discover a skill before loading its fuller instructions and resources. That reduces the need to put every procedure into the initial context. Our example supplies the investigation procedure from SKILL dot M D. Loading it is observable in the trace, but loading a procedure does not prove that its factual conclusions are true. Next, locate the provider and the separately verified evidence in our code.', 'learn-skills')
doc(9,4,'workflow','Locate the owner of the workflow wait',
    'Microsoft Learn places this human interaction in workflow request and response handling. An executor requests input and the application supplies the response. The graph decides where that wait belongs. In our recorded workflow, the pending operator decision lives in the running process; we have not configured restart recovery for that gate. Now return to the actual graph and its approval executor. Later, the durable lesson will demonstrate a different persistence boundary.', 'learn-workflow')
doc(10,5,'approval','Locate the protected function boundary',
    'Here is the function-tool approval mechanism on Microsoft Learn. ApprovalRequiredAIFunction wraps a callable function. If the model selects that protected capability, the caller receives an approval request and must provide a decision before execution can proceed. This differs from placing a fixed approval node in a workflow. In our code, find the wrapper around the maintenance function and then the resolver that binds the response to the requested call. The deterministic function still validates the actual write.', 'learn-approval-code')

add(3,4,'restore','Locate the deterministic restore before the follow-up',
    'Look at the operator workflow column. Restore Scheduled Mode is the deterministic action, separate from Ask Why in the agent column. The recorded click clears the override and the map changes. Keep the same conversation for the next Why question. The comparison is between remembered context and a new authoritative read; the conversation itself did not operate the light.',
    [shot('windows-03-06',117,9,[1056,230,864,486],[1190,600,370,65])])
add(4,5,'withhold','Show the evidence switch before the empty search',
    'Return to Director and find Work Knowledge, beside Agent Habitat. Watch the Seeded evidence checkbox change from checked to unchecked. The panel reports no evidence. This switch changes the synthetic retrieval source; it does not clear a previous conversation. Back in Command Center, start a new Ask, then use Why in that new session. Otherwise previously retrieved notes could still be part of the context and spoil the comparison.',
    [shot('windows-03-06',526,1,[900,420,960,540],[960,605,650,100]),
     dict(**shot('windows-03-06',533,1.5,[900,400,960,540]),narration_anchor='Watch the Seeded evidence checkbox')])
add(5,3,'close-case','Record a case only after the grounded answer',
    'After the grounded explanation, locate Close case, record to memory, in the agent panel. Watch the button change to Case recorded. This stores the incident explanation as a remembered case. It is not the Restore Scheduled Mode command and does not turn the lamp off. The next supported question names L-528 in a new session, so we can inspect how a previous case influences a different investigation.',
    [shot('windows-05-06',176,5,[1056,200,864,486],[1560,445,335,50])])
add(5,5,'clear','Show exactly which store is cleared',
    'In Director, expand Advanced controls and find Case Memory. The list shows one case before the action. Watch Clear case memory, then read Empty. This clears the case store, not the Hub and not every conversation. Return to the application and ask about L-528 in a fresh session. Now the comparison tests whether the remembered lead is absent, while current operational facts still come from the Hub.',
    [shot('windows-05-06',290,12,[285,490,1344,756-166],[325,580,1270,145])])
add(7,3,'source','Show the Local to MCP switch',
    'Here is the Tool Source panel in Director. Local Function is selected first. Watch the selection move to MCP remote. This changes where the tool is invoked, not who owns the streetlight state. Return to the same question in Command Center and inspect the MCP remote badge beside the trace. The protocol boundary changed; neither read is a repair.',
    [shot('windows-07-08',104,12,[285,650,1344,430],[325,830,620,160])])
add(9,3,'stale','Change the fixture while the old review remains open',
    'For the stale-state experiment, leave the second approval unanswered in Command Center. In Director, watch Apply again on the daylight scenario. Reapplying the fixture advances the authoritative revision even when the visible situation looks similar. Return to the original review instead of requesting a new one. The following approval still refers to the old state, so the command must be refused.',
    [shot('windows-09-10',125,9.5,[285,40,1344,756],[325,460,235,100])])
add(10,6,'existing','Select the Existing Incident fixture deliberately',
    'Find the Existing Incident card in Director. Watch Apply Scenario on that card. This prepares a controller fault that already has an incident and dispatch, so the next investigation should look before creating more work. After returning to Command Center, check the Active Incident panel before asking. We are changing the fixture for a new run, not replaying the previous approval.',
    [shot('windows-10b-11',150,10,[285,40,1344,756],[1090,120,250,420])])
add(11,3,'consult','Locate Security Consult before changing it',
    'The comparison has one explicit switch. In Director, find Security Consult beside Tool Source. Consult Off leaves Operations with its own evidence. Select Consult On before a new daylight question to make the specialist available. The following trace must name the consulted specialist and its bounded result. A changed button alone is not proof that the model used the capability.',
    [shot('windows-11-12',56,1,[900,460,960,540],[970,630,300,65]),
     dict(**shot('windows-11-12',60,.8,[900,460,960,540],[970,630,300,65]),narration_anchor='Select Consult On')])
add(12,4,'projection','Locate the full fixture for comparison',
    'When comparing the refusal with the underlying synthetic fixture, use Director, Advanced controls, Workforce Records. Show work orders in full exposes the teaching data to us as instructors. It does not add fields to the agent tool. Compare that full record with the shareable result in code. This is how we demonstrate a data boundary rather than relying only on a model promise.',
    [dict(**shot('windows-11-12',430,12,[285,400,1344,680],[325,860,1270,135]),point_end=4)])

add(1,5,'prepare','Prepare the deterministic fixture on the Mac',
    'Before the operation, let us locate its starting fixture on the Mac. This is the recorded reset procedure. In Director, the first card is Lights On Reported by a Client. Deterministic is the selected capability. Apply again resets the scenario even if that capability is already selected. Watch the application timestamp and the Ready to Present check change. Then return to Command Center. We expect a healthy controller, reported on, and an active manual override. A prepared fixture is our starting condition, not the result of a repair.',
    [dict(source='assets/mac-reset.mp4',start=15,seconds=1,focus=[285,35,1344,756],hold=12),
     dict(source='assets/mac-reset.mp4',start=22,seconds=11,focus=[285,35,1344,756]),
     dict(source='assets/mac-reset.mp4',start=40,seconds=6)])
add(2,8,'detach','Show the service remaining after detach',
    'Here is the detach action at a readable size. In the Operations Agent section, the button initially says Detach VS Code. Watch it return to Attach VS Code and the status become debugger not attached. Detaching leaves the existing service available. This is different from stopping the AppHost or launching a second service under a debugger. Clear any temporary snippet selections before the next exercise.',
    [shot('windows-02-03',865,1,[310,440,1320,600],hold=7),
     shot('windows-02-03',866,13,[310,440,1320,600])])
add(1,9,'failure-controls','Locate the failure fixture and the owning debugger row',
    'Before the failure experiment, locate two controls in Director. Controller Fault is the third scenario card. Apply Scenario prepares it; the button reads Apply again when that fixture is already selected, as it is in this recorded view. This changes the operational fixture while our stage remains Deterministic. For debugging, expand Advanced controls and find the Energy Hub row. The attach buttons belong to that service. Choose Visual Studio 2026 on Windows, or VS Code on either platform. The next sequence shows the attachment and the confirmation guard. Preparing a fault and attaching a debugger are separate actions; neither requests the repair.',
    [dict(source='assets/windows-screen.mp4',start=217,seconds=1,focus=[310,40,1344,756],point=[830,120,260,420]),
     dict(source='assets/windows-orientation.mp4',start=32,seconds=4,focus=[310,70,1344,756],point=[335,150,1250,130],narration_anchor='For debugging')])
add(13,3,'habitat','Choose the habitat before the next question',
    'Now locate Agent Habitat in Director. Local, Aspire is selected first. Foundry Hosted is the other choice. Watch the selection move before returning to Command Center. This routes the next question to the configured hosted agent; it does not move the local Hub or automatically transfer every local tool. The following recorded answer and error are the evidence for what this particular hosted runtime could actually do.',
    [shot('windows-13-hosting',280,1,[285,450,1344,600]),
     dict(**shot('windows-13-hosting',285,.7,[285,450,1344,600]),narration_anchor='Watch the selection move'),
     shot('windows-13-hosting',286,.067,[285,450,1344,600])])
add(15,2,'foundation','Locate beat fifteen and its preparation control',
    'Find section fifteen in Director: Durable recovery, foundation and worker controls. Read Before the first step. Prepare foundation starts the local prerequisites and recreates the daylight anomaly. It does not ask the agent to act. Watch the preparation status change after the click. Once ready, Open Command Center demo takes us to the operator interface. That is where we start the conversation and inspect the approval. We return to Director only for the worker interruption.',
    [shot('windows-15b-durable',258,1,[300,450,1320,630],hold=15),
     shot('windows-15b-durable',265,12,[300,450,1320,630])])

# Original scenes continue from the new orientation rather than repeating unseen setup.
configs[0]['revisions']['agentic-24'] = dict(narration=
    'The Demo Director prepares the teaching environment. Read the first scenario card: Lights On Reported by a Client. '
    'A scenario creates operational conditions; a capability stage controls what the application can do. '
    'The daylight fixture is already applied in this recording. We select Investigation Agent and inspect the preparation review. '
    'This review changes the stage only. It is not a repair and it is not a new investigation. '
    'Confirm the selection, then check Now playing and the readiness message. If you repaired the lamp in an earlier run, use Apply again to reset this fixture. '
    'Command Center is where we will ask the question and observe the result. Keep those two responsibilities separate: Director prepares; the main application demonstrates.')
configs[0]['motion'] = {'Director prepares the starting fixture':[
    ['director-live',10,1,None],['director-live',55,2,[310,180,1300,732]],
    ['director-live',60,.2,[1120,630,800,450]],['director-live',65,6,None]],
    'Pause, practice, and prepare Lab 01':[
    ['card:practice',0,1,None],['reset-live',15,5,None],['reset-live',52,10,None]]}
configs[0]['motion_anchors']={'agentic-24':[None,'We select Investigation Agent','inspect the preparation review','Confirm the selection'],
    'agentic-36':[None,'When you are ready for Lab one','Review and confirm']}
configs[0]['phrase_markers']['agentic-24']=[dict(phrase='Read the first scenario card',rect=[315,125,270,525],seconds=6)]
configs[0]['phrase_markers']['agentic-22']=[
    dict(phrase='Find the simulator',rect=[65,235,570,320],seconds=6),
    dict(phrase='use their endpoint links',rect=[1310,235,340,320],seconds=5)]
configs[0]['phrase_markers']['agentic-26']=[
    dict(phrase='Attach the Operations Agent row',rect=[30,970,620,80],seconds=3),
    dict(phrase='then arm Agent Creation and Function Tool',rect=[30,620,680,220],seconds=6),
    dict(phrase='The attached status matters',rect=[1620,560,250,65],seconds=5)]
configs[0]['phrase_markers']['agentic-32']=[
    dict(phrase='choose Detach VS Code',rect=[35,975,230,70],seconds=3)]
configs[0]['motion']['Find your way around the repository']=[
    ['orientation-repo',2,18,None],['orientation-repo',48,8,None],
    ['orientation-code',24,22,[0,70,1280,720]]]
configs[0]['motion']['Locate the project and its hosted agent']=[
    ['orientation-foundry',2,10,None],['orientation-foundry',28,8,[250,65,1600,900]],
    ['orientation-foundry',60,12,[250,65,1600,900]]]
configs[0]['revisions']['agentic-10']=dict(actions='GitHub orientation, then a readable close-up of the actual local project tree.')
configs[0]['revisions']['agentic-11']=dict(actions='Actual existing Foundry project, agent list and loaded details. Omit the loading-only interval.')

for n in range(3,14):
    # Preserve all factual content; convert the already demonstrated imperative to a recap.
    import re
    script=(units[n]/'shooting-script.md').read_text(encoding='utf-8')
    section=re.split(r'^## ',script,flags=re.M)[2]
    narration=' '.join(section.split('**Narration:**',1)[1].split())
    first,sep,rest=narration.partition('. ')
    configs[n]['revisions'][f'lab{n:02}-02']=dict(narration='With the starting conditions prepared, let us follow the recorded run. '+rest if sep else narration)

configs[15]['revisions']['lab15-02']=dict(narration=
    'With the foundation prepared, move to Command Center and expand the durable Walkthrough. Ask the durable agent to recover L-417. '
    'The model reads the Hub and starts a supervised operation. Acceptance is not repair. Now inspect the actual pending review. '
    'The operation is awaiting approval. The review records revision three and its existing expiry. The lamp is still on. '
    'An operation identifier alone would not prove that approval was pending. Keep this review open for the restart experiment.')
configs[15]['shots']['lab15-02']=[shot('windows-15b-durable',305,6),shot('windows-15b-durable',318,5),
    dict(**shot('windows-15b-durable',365,1,[1056,594,864,486],[1570,595,320,350]),narration_anchor='Now inspect the actual pending review')]
configs[15]['shots']['lab15-03']=[shot('windows-15b-durable',384,1,[300,330,1320,750],hold=1),
    shot('windows-15b-durable',391,9,[300,330,1320,750]),
    dict(**shot('windows-15b-durable',419,8,[1056,300,864,486]),narration_anchor='Return to Command Center'),
    dict(**shot('windows-15b-durable',436,8,[1056,486,864,486]),narration_anchor='The reply says')]

configs[2]['inserts'][0]['shots'][1]['start']=20
configs[2]['inserts'][0]['shots'][2].update(start=21,seconds=3)
configs[9]['inserts'][0]['shots'][1]['start']=30
configs[13]['inserts'][0]['shots'][0].pop('focus')

# Focus selected existing shots while preserving their original narration and timing.
def focus(n, scene, indexes, rect, point=None):
    key=f'lab{n:02}-{scene:02}'
    original=configs[n]['shots'].get(key) or json.loads((units[n]/'shots.json').read_text())[key]
    original=json.loads(json.dumps(original))
    for i in indexes:
        original[i].pop('crop',None)
        original[i]['focus']=rect
        if point: original[i]['point']=point
    configs[n]['shots'][key]=original

focus(2,4,[0],[285,170,1344,756])
focus(2,5,[0,1],[30,55,1568,882])
focus(3,5,[0],[30,55,1568,882])
focus(4,3,[1],[1056,486,864,486],[1550,740,350,250])
focus(4,4,[0,1],[30,55,1568,882])
focus(4,6,[1],[1056,486,864,486])
focus(5,3,[0],[1056,280,864,486])
focus(6,3,[0],[1056,486,864,486])
focus(7,3,[0,1],[30,55,1568,882])
focus(7,4,[0],[310,90,1320,742],[330,120,1250,120])
configs[7]['shots']['lab07-04'][0].update(start=278,seconds=4,hold=3)
focus(8,4,[0],[30,55,1568,882],[90,120,1300,460])
focus(8,2,[3,4],[1056,486,864,486])
focus(8,3,[2,3,4],[1056,486,864,486])
focus(9,6,[1],[30,55,1568,882])
focus(10,4,[1],[30,55,1568,882])
focus(11,5,[1],[30,55,1568,882])
focus(12,3,[1],[30,55,1568,882])
focus(13,3,[0],[285,350,1344,730])
focus(13,4,[0],[1056,486,864,486],[1570,800,330,155])
focus(14,2,[0,1],[30,55,1568,882],[100,120,1400,380])
focus(14,3,[0],[0,35,1504,846],[15,170,1450,380])
focus(14,4,[0],[0,35,1504,846])
focus(16,2,[0,1],[30,55,1568,882],[90,130,1400,350])
focus(16,3,[1,2],[0,480,1536,600])
focus(16,4,[0],[20,20,1568,882],[30,130,1480,80])
focus(17,2,[0],[30,55,1568,882])
focus(17,3,[1],[30,55,1568,882],[100,570,1400,280])
focus(17,4,[0,1],[0,450,1536,630])

for n,config in configs.items():
    (units[n]/'teaching-edits.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
print('Wrote editorial manifests; source and output media remain local.')
