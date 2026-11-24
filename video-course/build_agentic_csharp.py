"""Build the local Agentic C# pilot from branded slides and genuine motion captures.

No application outputs are synthesized. Freeze holds are explicitly labeled.
Narration uses the existing synthetic voice pipeline; no presenter voice cloning.
"""
import argparse
import asyncio
import hashlib
import json
import math
import runpy
import textwrap
from pathlib import Path

import build_foundations as speech_pipeline
import build_intro as r

UNIT = Path(__file__).resolve().parent / 'unit-00-agentic-csharp'
lesson = runpy.run_path(str(UNIT / 'lesson.py'))
r.UNIT, r.ASSETS, r.OUT = UNIT, UNIT / 'assets', UNIT / 'output'
r.SPEECH_RATE = '+0%'
r.SCENES = [dict(id=f'agentic-{i:02}', title=t, visual=v, chapter=c, actions=a, narration=n)
            for i, (t, v, c, a, n) in enumerate(lesson['LESSON'], 1)]

# Seconds refer to actual, local, uninterrupted source recordings. Unneeded waits
# and setup are cut. Motion remains at its captured speed. A hold is labeled.
SHOTS = {
 17: [('aspire-live', 0, 17, None)],
 18: [('aspire-live', 17, 18, None)],
 19: [('director-live', 49, 20, (310, 230, 1300, 620))],
 20: [('director-live', 76, 8, None), ('retry-live', 39, 13, (1100, 0, 820, 700))],
 21: [('vscode-live', 0, 25, (310, 300, 1320, 700))],
 22: [('vscode-take2', 51, 16, None)],
 23: [('vscode-take2', 68, 11, None)],
 24: [('vscode-take2', 105, 42, None), ('vscode-take2', 151, 13, None)],
 26: [('vs-take2', 12, 12, None), ('vs-take2', 108, 17, (250, 0, 1670, 1030)),
      ('vs-take2', 162, 9, (250, 0, 1670, 1030)),
      ('vs-take2', 201, 17, (250, 0, 1670, 1030)),
      ('vs-take2', 230, 21, (250, 0, 1670, 1030))],
 27: [('retry-live', 10, 25, None), ('card:hub-checkpoint', 0, 23, None)],
 28: [('reset-live', 15, 20, None), ('reset-live', 52, 10, None), ('card:practice', 0, 26, None)],
}

def cards():
    from PIL import Image, ImageDraw, ImageFont
    checkpoint=json.loads((UNIT/'evidence/after-visual-studio.json').read_text(encoding='utf-8-sig'))
    hub,debug=checkpoint['hub'],checkpoint['debugger']
    if debug['debuggerAttached'] or not hub['reportedIsOn']: raise RuntimeError('Checkpoint failed')
    lesson['CARDS']['hub-checkpoint']=('Independent service checkpoint',
        'Actual API responses after debugging; before fixture reset\n\nGET /api/energy/assets/L-417\n'+
        json.dumps({k:hub[k] for k in ['assetId','reportedIsOn','manualOverride','stateRevision']},indent=2)+
        '\n\nGET /api/demo-breakpoints\n'+json.dumps({k:debug[k] for k in ['processId','debuggerAttached']},indent=2)+
        '\n\nEvidence card from saved responses; not a terminal capture.')
    for key, (title, body) in lesson['CARDS'].items():
        im = Image.open(r.ASSETS / 'signature-background.png').convert('RGB').resize((1920,1080))
        draw = ImageDraw.Draw(im)
        heading = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 54)
        size = 32
        while True:
            font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', size)
            box = draw.multiline_textbbox((95,235),body,font=font,spacing=22)
            if box[2] < 1820 and box[3] < 930: break
            size -= 1
            if size < 24: raise RuntimeError(f'Card overflow: {key}')
        draw.text((95,100),title,font=heading,fill='#8de6ed')
        draw.multiline_text((95,235),body,font=font,spacing=22,fill='white')
        im.save(r.ASSETS / f'card-{key}.png')

def render():
    timeline, cues, clips, script = [], [], [], []
    position = 0
    for num, s in enumerate(r.SCENES,1):
        audio = r.ASSETS / (s['id']+'.mp3')
        shots = SHOTS.get(num)
        motion_total = sum(x[2] for x in shots) if shots else 0
        length = math.ceil(max(r.duration(audio)+.8, motion_total+.8)*15)/15
        inputs, filters = [], []
        if shots:
            for i,(name,start,seconds,crop) in enumerate(shots):
                if name.startswith('card:'):
                    inputs += ['-loop','1','-framerate','15','-t',str(seconds),'-i',r.ASSETS/(name.replace(':','-')+'.png')]
                else:
                    inputs += ['-ss',str(start),'-t',str(seconds),'-i',r.BUILD/(name+'.mp4')]
                cut = f'crop={crop[2]}:{crop[3]}:{crop[0]}:{crop[1]},' if crop else ''
                filters.append(f'[{i}:v]{cut}scale=1880:960:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:65:color=0x141122,setsar=1,fps=15,setpts=PTS-STARTPTS[v{i}]')
            labels=''.join(f'[v{i}]' for i in range(len(shots)))
            filters.append(f'{labels}concat=n={len(shots)}:v=1:a=0,tpad=stop_mode=clone:stop_duration={length-motion_total}[base]')
            ninputs=len(shots)
        else:
            kind,key=s['visual'].split(':')
            source=r.ASSETS/f'{kind}-{key}.png'
            if not source.exists(): raise RuntimeError(f'Missing source: {source}')
            inputs=['-loop','1','-framerate','15','-i',source]
            filters.append('[0:v]scale=1880:960:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:65:color=0x141122,setsar=1[base]')
            ninputs=1
        title=r.BUILD/(s['id']+'-title.txt'); title.write_text('AGENTIC C#  /  UNIT 00     '+s['title'],encoding='utf-8')
        credit=r.BUILD/(s['id']+'-credit.txt')
        credit.write_text('LOCAL ROUGH CUT | Alon Fliess course | Synthetic narration | '+('Actual screen recording; waits edited' if shots else 'Branded source slide / teaching card'),encoding='utf-8')
        vf=f'[base]drawtext=fontfile=sans.ttf:textfile={title.name}:expansion=none:fontcolor=white:fontsize=27:x=45:y=20,drawtext=fontfile=sans.ttf:textfile={credit.name}:expansion=none:fontcolor=0xb8b7c8:fontsize=19:x=45:y=1044'
        if shots and length>motion_total:
            vf+=f",drawtext=fontfile=sans.ttf:text='Paused for explanation':fontcolor=white:fontsize=22:box=1:boxcolor=0x222222:x=1480:y=1040:enable='gte(t,{motion_total})'"
        filters.append(vf+'[out]')
        clip=r.BUILD/(s['id']+'.mp4')
        digest=hashlib.sha256(json.dumps([s,SHOTS.get(num),length]).encode()).hexdigest()
        stamp=clip.with_suffix('.sha256')
        if not clip.exists() or not stamp.exists() or stamp.read_text()!=digest:
            r.run('ffmpeg','-y','-v','error',*inputs,'-i',audio,'-filter_complex',';'.join(filters),'-map','[out]','-map',f'{ninputs}:a','-af','adelay=300:all=1,apad','-t',length,'-c:v','libx264','-preset','veryfast','-crf','21','-pix_fmt','yuv420p','-c:a','aac','-b:a','128k','-ar','48000','-ac','2',clip)
            stamp.write_text(digest)
        clips.append(clip)
        timeline.append({**s,'start':position,'end':position+length,'motionShots':shots})
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
    (UNIT/'shooting-script.md').write_text('# Agentic C# — Unit 00 shooting script\n\nTiming follows the rendered edit. Real motion is at captured speed; cuts omit waits, and freeze holds are labeled. Synthetic narration is a review voice, not Alon’s voice. Debug timeouts and the successful detached retry are separate requests.\n\n'+'\n'.join(script),encoding='utf-8')
    print(f'Complete: {position/60:.2f} minutes',flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--phase',choices=['audio','video','all'],default='all');args=parser.parse_args()
    r.ASSETS.mkdir(exist_ok=True);r.OUT.mkdir(exist_ok=True)
    if args.phase in ['audio','all']: asyncio.run(speech_pipeline.speech())
    if args.phase in ['video','all']: cards();render()
