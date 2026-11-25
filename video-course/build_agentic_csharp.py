"""Build the local Agentic C# pilot from branded slides and genuine motion captures.

No application outputs are synthesized. Freeze holds are explicitly labeled.
Narration uses the existing synthetic voice pipeline; no presenter voice cloning.
"""
import argparse
import asyncio
import hashlib
import json
import math
import re
import runpy
import textwrap
from pathlib import Path

import build_foundations as speech_pipeline
import build_intro as r
import render_privacy
import teaching_clarity

UNIT = Path(__file__).resolve().parent / 'unit-00-agentic-csharp'
lesson = runpy.run_path(str(UNIT / 'lesson.py'))
r.UNIT, r.ASSETS, r.OUT = UNIT, UNIT / 'assets', UNIT / 'output'
r.SPEECH_RATE = '+0%'
r.SCENES = [dict(id=f'agentic-{i:02}', title=t, visual=v, chapter=c, actions=a, narration=n)
            for i, (t, v, c, a, n) in enumerate(lesson['LESSON'], 1)]
r.SCENES = teaching_clarity.scenes(UNIT, r.SCENES)

# Seconds refer to actual, local, uninterrupted source recordings. Unneeded waits
# and setup are cut. Motion remains at its captured speed. A hold is labeled.
SHOTS = {
 'Find your way around the repository': [('orientation-repo', 2, 18, None), ('orientation-repo', 48, 8, None), ('orientation-code', 24, 22, None)],
 'Use the Microsoft Learn starting point': [('orientation-learn', 2, 17, None), ('orientation-learn', 38, 15, None)],
 'Locate the project and its hosted agent': [('orientation-foundry', 2, 12, None), ('orientation-foundry', 28, 17, None), ('orientation-foundry', 47, 8, None), ('orientation-foundry', 57, 15, None)],

 'Aspire is our starting point': [('aspire-live', 0, 17, None)],
 'Read the resource graph': [('aspire-live', 17, 18, None)],
 'Director prepares the starting fixture': [('director-live', 49, 20, (310, 230, 1300, 732))],
 "Command Center is the operator's application": [('director-live', 76, 8, None), ('retry-live', 39, 13, (640, 0, 1280, 720))],
 'Attach VS Code': [('vscode-values', 4, 30, (320, 330, 1300, 732))],
 'Stop and single-step in C sharp': [('vscode-values', 81, 8, None), ('vscode-values', 90, 18, None)],
 'Inspect the tool boundary': [('vscode-values', 120, 6, None)],
 'Inspect AI results and model exchanges': [('vscode-values', 145, 2, None), ('vscode-values', 169, 5, None), ('vscode-values', 187, 7.5, (0, 80, 1450, 816))],
 'The Hub result becomes the next context': [('vscode-values', 322, 7.8, None)],
 'The second exchange produces the answer': [('vscode-values', 210, 12, (0, 80, 1450, 816))],
 'Detach VS Code through the Director': [('vscode-values', 400, 14, (320, 330, 1300, 732))],
 'Switch debuggers without restarting the service': [('card:editors', 0, 16.5, None), ('vs-values', 134, 5.5, (320, 330, 1300, 732)), ('vs-values', 180, 10, (320, 330, 1300, 732)), ('vs-values', 236, 9.5, (250, 45, 1660, 934)), ('vs-values', 261, 4.5, (250, 45, 1660, 934)), ('vs-values', 293, 5, (250, 45, 1660, 934)), ('vs-values', 317, 6, (255, 625, 650, 366)), ('vs-values', 392, 14, (255, 625, 650, 366), 7), ('vs-values', 524, 14, (320, 340, 1300, 732))],
 'Verify the outcome at the Hub': [('retry-live', 10, 25, None), ('card:hub-checkpoint', 0, 23, None)],
 'Pause, practice, and prepare Lab 01': [('reset-live', 15, 20, None), ('reset-live', 52, 10, None), ('card:practice', 0, 26, None)],
}
# Rendering changes participate in the cache: old bordered frames must not survive.
RENDER_VERSION = 'privacy-and-pacing-v4'
SHOTS.update(teaching_clarity.edits(UNIT).get('motion', {}))

# Editorial markers point to genuine pixels; no UI values are changed.
# Phrase anchors use the synthesized word timings, keeping highlights in sync.
# Rectangles are measured on the final 1920x1080 frame after the shot crop.
MARKERS = {
 'Context Engineering: one request, several steps': [
  ('On the left', (30,155,400,695), 13),
  ('The model receives', (950,390,240,225), 8),
  ('On the right', (1605,280,285,415), 14),
  ('Follow the bottom arrow', (685,730,1090,85), 12),
 ],
 'Inspect the tool boundary': [
  ('The yellow execution marker', (775,457,1015,34), 4),
  ('In Locals', (100,174,260,31), 7),
  ('above the method', (890,295,825,31), 3),
  ('on the asset parameter', (927,364,675,32), 5),
  ('call the Energy read gateway', (927,601,875,33), 9),
 ],
 'Inspect AI results and model exchanges': [
  ('Step one', (177,688,1370,40), 12),
 ],
 'The Hub result becomes the next context': [
  ('Reported Is On is true', (995,833,244,31), 6),
  ('Is Daylight is true', (1555,833,215,31), 3),
  ('The expected scheduled state is false', (104,859,260,31), 7),
  ('Manual Override is true', (420,859,252,31), 5),
 ],
 'The second exchange produces the answer': [
  ('compare Sent', (150,578,1518,70), 4),
  ('with Received', (152,688,1275,39), 6),
  ('The response text agrees', (130,322,805,34), 6),
 ],
}

def resolve_highlights(scene):
    words=json.loads((r.ASSETS/(scene['id']+'-words.json')).read_text())
    tokens=[]
    for w in words:
        for token in re.findall(r"[a-z0-9]+",w['text'].lower()):
            tokens.append((token,w['start']))
    result=[]
    for phrase,rect,seconds in MARKERS.get(scene['title'],[]):
        wanted=re.findall(r"[a-z0-9]+",phrase.lower())
        matches=[i for i in range(len(tokens)-len(wanted)+1)
                 if [v[0] for v in tokens[i:i+len(wanted)]]==wanted]
        if not matches: raise ValueError(f'Missing marker phrase: {phrase}')
        start=round(tokens[matches[0]][1]+.3,3)
        result.append(dict(phrase=phrase,start=start,end=round(start+seconds,3),rect=rect))
    return result

def cards():
    from PIL import Image, ImageDraw, ImageFont
    checkpoint=json.loads((UNIT/'evidence/after-visual-studio.json').read_text(encoding='utf-8-sig'))
    hub,debug=checkpoint['hub'],checkpoint['debugger']
    if debug['debuggerAttached'] or not hub['reportedIsOn']: raise RuntimeError('Checkpoint failed')
    lesson['CARDS']['hub-checkpoint']=('Independent service checkpoint',
        f"Recorded API evidence; earlier detached retry\n\nL-417 reported On: {hub['reportedIsOn']}\nOverride: {hub['manualOverride']}   Revision: {hub['stateRevision']}\nProcess: {debug['processId']}\nDebugger attached: {debug['debuggerAttached']}\n\nRead-only investigation; no repair requested.")
    for key, (title, body) in lesson['CARDS'].items():
        im = Image.open(r.ASSETS / 'signature-background.png').convert('RGB').resize((1920,1080))
        draw = ImageDraw.Draw(im)
        heading = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 62)
        size = 46
        while True:
            font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', size)
            box = draw.multiline_textbbox((95,235),body,font=font,spacing=22)
            if box[2] < 1820 and box[3] < 930: break
            size -= 1
            if size < 40: raise RuntimeError(f'Card overflow: {key}')
        draw.text((95,100),title,font=heading,fill='#8de6ed')
        draw.multiline_text((95,235),body,font=font,spacing=22,fill='white')
        inset = {'platform':'deck-15.png', 'twin':'deck-12.png', 'journey':'deck-7.png'}.get(key)
        if inset:
            source=Image.open(r.ASSETS/inset).convert('RGB')
            source.thumbnail((880,495))
            im.paste(source,(980,480 if key=='platform' else 330))
        im.save(r.ASSETS / f'card-{key}.png')

def render():
    timeline, cues, clips, script = [], [], [], []
    position = 0
    for num, s in enumerate(r.SCENES,1):
        audio = r.ASSETS / (s['id']+'.mp3')
        expected_voice=hashlib.sha256((r.VOICE+r.SPEECH_RATE+s['narration']).encode()).hexdigest()
        if (r.ASSETS/(s['id']+'-voice.sha256')).read_text()!=expected_voice:
            raise RuntimeError(f"Stale narration for {s['title']}; run --phase audio first")
        shots = SHOTS.get(s['title'])
        anchors = teaching_clarity.edits(UNIT).get('motion_anchors', {}).get(s['id'])
        if shots and anchors:
            timing = []
            for shot, anchor in zip(shots, anchors, strict=True):
                item = dict(seconds=shot[2], hold=shot[4] if len(shot)>4 else 0)
                if anchor:
                    item['narration_anchor'] = anchor
                timing.append(item)
            timing = teaching_clarity.timed_shots(UNIT, s, timing)
            shots = [list(shot[:4])+[t.get('hold',0)] for shot,t in zip(shots,timing)]
        # Optional fifth field holds the final genuine frame for explanation.
        # Every such hold is labeled; motion is never slowed or synthesized.
        motion_total = sum(x[2] + (x[4] if len(x) > 4 else 0) for x in shots) if shots else 0
        length = math.ceil((r.duration(audio)+1)*15)/15
        inputs, filters = [], []
        if shots:
            for i,shot in enumerate(shots):
                name,start,seconds,crop,*extra = shot
                hold = extra[0] if extra else 0
                if name.startswith('card:'):
                    inputs += ['-loop','1','-framerate','15','-t',str(seconds),'-i',r.ASSETS/(name.replace(':','-')+'.png')]
                else:
                    inputs += ['-ss',str(start),'-t',str(seconds),'-i',r.BUILD/(name+'.mp4')]
                cut = '' if name.startswith('card:') else render_privacy.source_filters(r.BUILD/(name+'.mp4'), start, seconds)
                cut += f'crop={crop[2]}:{crop[3]}:{crop[0]}:{crop[1]},' if crop else ''
                hold_filter = (f",tpad=stop_mode=clone:stop_duration={hold},drawtext=fontfile=sans.ttf:text='Paused for explanation':fontcolor=white:fontsize=24:box=1:boxcolor=0x222222:x=w-tw-24:y=h-th-22:enable='gte(t,{seconds})'" if hold else '')
                filters.append(f'[{i}:v]{cut}scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:color=0x141122,setsar=1,fps=15,setpts=PTS-STARTPTS{hold_filter}[v{i}]')
            labels=''.join(f'[v{i}]' for i in range(len(shots)))
            filters.append(f'{labels}concat=n={len(shots)}:v=1:a=0,tpad=stop_mode=clone:stop_duration={max(0, length-motion_total)}[base]')
            ninputs=len(shots)
        else:
            kind,key=s['visual'].split(':')
            source=r.ASSETS/f'{kind}-{key}.png'
            if not source.exists(): raise RuntimeError(f'Missing source: {source}')
            inputs=['-loop','1','-framerate','15','-i',source]
            filters.append('[0:v]scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:color=0x141122,setsar=1[base]')
            ninputs=1
        highlights = resolve_highlights(s) + teaching_clarity.phrase_markers(UNIT, s)
        vf='[base]null'
        for h in highlights:
            x,y,w,hgt=h['rect']
            vf+=f",drawbox=x={x}:y={y}:w={w}:h={hgt}:color=0xffd54f:thickness=4:enable='between(t,{h['start']},{h['end']})'"
        if shots and length>motion_total:
            vf+=f",drawtext=fontfile=sans.ttf:text='Paused for explanation':fontcolor=white:fontsize=24:box=1:boxcolor=0x222222:x=w-tw-24:y=h-th-22:enable='gte(t,{motion_total})'"
        filters.append(vf+'[out]')
        clip=r.BUILD/(s['id']+'.mp4')
        digest=hashlib.sha256(json.dumps([RENDER_VERSION,s,SHOTS.get(s['title']),length,filters,highlights, hashlib.sha256(source.read_bytes()).hexdigest() if not shots else None]).encode()).hexdigest()
        stamp=clip.with_suffix('.sha256')
        if not clip.exists() or not stamp.exists() or stamp.read_text()!=digest:
            r.run('ffmpeg','-y','-v','error',*inputs,'-i',audio,'-filter_complex',';'.join(filters),'-map','[out]','-map',f'{ninputs}:a','-af','adelay=300:all=1,apad','-t',length,'-c:v','libx264','-preset','veryfast','-crf','21','-pix_fmt','yuv420p','-c:a','aac','-b:a','128k','-ar','48000','-ac','2',clip)
            stamp.write_text(digest)
        clips.append(clip)
        timeline.append({**s,'start':position,'end':position+length,'motionShots':shots,'highlights':highlights})
        script.append(f"## {r.srt_time(position)}–{r.srt_time(position+length)} | {s['title']}\n\n**Screen:** {s['actions']}\n\n**Source:** `{s['visual']}`; motion edits: `{shots}`. Holds are labeled.\n\n**Narration:** {s['narration']}\n")
        words=json.loads((r.ASSETS/(s['id']+'-words.json')).read_text())
        group=[]
        for w in words:
            group.append(w)
            if len(' '.join(v['text'] for v in group))>=78 or len(group)>=14 or w is words[-1]:
                cues.append((position+.3+group[0]['start'],position+.3+group[-1]['end'],'\n'.join(textwrap.wrap(' '.join(v['text'] for v in group),44))))
                group=[]
        position+=length
        print(f'Rendered {num}/{len(r.SCENES)}: {length:.1f}s',flush=True)
    srt=r.OUT/'agentic-csharp-unit-00.en.srt'
    srt.write_text('\n\n'.join(f'{i}\n{r.srt_time(a)} --> {r.srt_time(b)}\n{t}' for i,(a,b,t) in enumerate(cues,1))+'\n',encoding='utf-8')
    (r.OUT/'agentic-csharp-unit-00.en.vtt').write_text('WEBVTT\n\n'+'\n\n'.join(f'{r.srt_time(a).replace(",",".")} --> {r.srt_time(b).replace(",",".")}\n{t}' for a,b,t in cues),encoding='utf-8')
    chapters=[s for s in timeline if s['chapter']]
    meta=[';FFMETADATA1','title=Agentic C# - Unit 00','artist=Alon Fliess - course materials; synthetic narration']
    for i,s in enumerate(chapters):
        end=chapters[i+1]['start'] if i+1<len(chapters) else position
        meta+=['[CHAPTER]','TIMEBASE=1/1000',f'START={round(s["start"]*1000)}',f'END={round(end*1000)}','title='+s['chapter']]
    (r.BUILD/'agentic-chapters.txt').write_text('\n'.join(meta),encoding='utf-8')
    (r.BUILD/'agentic-concat.txt').write_text('\n'.join(f"file '{p.name}'" for p in clips))
    r.run('ffmpeg','-y','-v','error','-f','concat','-safe','0','-i','agentic-concat.txt','-c','copy','agentic-joined.mp4')
    r.run('ffmpeg','-y','-v','error','-i','agentic-joined.mp4','-i',srt,'-i','agentic-chapters.txt','-map','0:v','-map','0:a','-map','1:0','-map_metadata','2','-map_chapters','2','-c:v','copy','-c:a','copy','-c:s','mov_text','-metadata:s:s:0','language=eng','-movflags','+faststart',r.OUT/'agentic-csharp-unit-00-rough-cut.mp4')
    (r.OUT/'timeline.json').write_text(json.dumps(timeline,indent=2),encoding='utf-8')
    (r.OUT/'chapters.txt').write_text('\n'.join(r.srt_time(s['start']).split(',')[0]+' '+s['chapter'] for s in chapters))
    (UNIT/'shooting-script.md').write_text('# Agentic C# — Unit 00 shooting script\n\nTiming follows the rendered edit. Real motion is at captured speed; cuts omit waits, and freeze holds are labeled. Synthetic narration is a review voice, not Alon\'s voice. Debug timeouts and the successful detached retry are separate requests.\n\n'+'\n'.join(script),encoding='utf-8')
    print(f'Complete: {position/60:.2f} minutes',flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--phase',choices=['audio','video','all'],default='all');args=parser.parse_args()
    r.ASSETS.mkdir(exist_ok=True);r.OUT.mkdir(exist_ok=True)
    if args.phase in ['audio','all']: asyncio.run(speech_pipeline.speech())
    if args.phase in ['video','all']: cards();render()
