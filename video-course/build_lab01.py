"""Render Lab 01 from its narration and an explicit manifest of real recordings.

Run --phase audio while preparing footage, then --phase video after reviewing shots.json.
Missing footage is an error. This renderer never substitutes fabricated app output.
"""
import argparse
import asyncio
import hashlib
import json
import math
import re
import textwrap
from pathlib import Path

import build_foundations as speech
import build_intro as r

UNIT = Path(__file__).resolve().parent / 'unit-01-deterministic-foundation'
r.UNIT, r.ASSETS, r.OUT = UNIT, UNIT / 'assets', UNIT / 'output'
r.SPEECH_RATE = '+0%'
script = (UNIT / 'shooting-script.md').read_text(encoding='utf-8')
r.SCENES = []
for index, section in enumerate(re.split(r'^## ', script, flags=re.M)[1:], 1):
    heading, body = section.split('\n', 1)
    title = heading.split(' — ', 1)[1]
    picture, narration = body.split('**Narration:**', 1)
    r.SCENES.append(dict(id=f'lab01-{index:02}', title=title,
                         actions=picture.replace('**Picture:**', '').strip(),
                         narration=' '.join(narration.split())))


def render():
    manifest = json.loads((UNIT / 'shots.json').read_text(encoding='utf-8'))
    clips, timeline, cues = [], [], []
    position = 0.0
    for scene in r.SCENES:
        audio = r.ASSETS / (scene['id'] + '.mp3')
        voice_hash = hashlib.sha256((r.VOICE + r.SPEECH_RATE + scene['narration']).encode()).hexdigest()
        if (r.ASSETS / (scene['id'] + '-voice.sha256')).read_text() != voice_hash:
            raise RuntimeError('Narration changed; regenerate audio first')
        shots = manifest[scene['id']]
        inputs, filters, identities = [], [], []
        motion_seconds = 0
        for i, shot in enumerate(shots):
            source = (UNIT / shot['source']).resolve()
            if not source.is_file():
                raise FileNotFoundError(source)
            duration = shot['seconds']
            hold = shot.get('hold', 0)
            motion_seconds += duration + hold
            identities.append([str(source), source.stat().st_size, source.stat().st_mtime_ns, shot])
            if source.suffix.lower() in ['.png', '.jpg']:
                inputs += ['-loop', '1', '-framerate', '15', '-t', duration, '-i', source]
            else:
                start = shot.get('start', 0)
                if r.duration(source) + .1 < start + duration:
                    raise ValueError(f'Shot exceeds recording: {source}')
                inputs += ['-ss', start, '-t', duration, '-i', source]
            crop = shot.get('crop')
            prefix = f'crop={crop[2]}:{crop[3]}:{crop[0]}:{crop[1]},' if crop else ''
            shot_filter = (f'[{i}:v]{prefix}scale=1920:1080:force_original_aspect_ratio=decrease,'
                           f'pad=1920:1080:(ow-iw)/2:(oh-ih)/2,setsar=1,fps=15,setpts=PTS-STARTPTS')
            if hold:
                shot_filter += (f',tpad=stop_mode=clone:stop_duration={hold},'
                                "drawtext=fontfile=sans.ttf:text='Paused for explanation':fontcolor=white:fontsize=24:"
                                f"box=1:boxcolor=0x222222:x=w-tw-24:y=h-th-22:enable='gte(t,{duration})'")
            filters.append(shot_filter + f'[v{i}]')
        length = math.ceil(max(r.duration(audio) + 1, motion_seconds) * 15) / 15
        chain = ''.join(f'[v{i}]' for i in range(len(shots)))
        filters.append(f'{chain}concat=n={len(shots)}:v=1:a=0,'
                       f'tpad=stop_mode=clone:stop_duration={length-motion_seconds}[base]')
        marker = '[base]null'
        if length > motion_seconds and Path(shots[-1]['source']).suffix.lower() not in ['.png', '.jpg']:
            marker += ",drawtext=fontfile=sans.ttf:text='Paused for explanation':fontcolor=white:fontsize=24:" \
                      f"box=1:boxcolor=0x222222:x=w-tw-24:y=h-th-22:enable='gte(t,{motion_seconds})'"
        for highlight in manifest.get('highlights', {}).get(scene['id'], []):
            x, y, width, height = highlight['rect']
            marker += f",drawbox=x={x}:y={y}:w={width}:h={height}:color=0xffd54f:thickness=4:" \
                      f"enable='between(t,{highlight['start']},{highlight['end']})'"
        filters.append(marker + '[out]')
        clip = r.BUILD / (scene['id'] + '.mp4')
        stamp = clip.with_suffix('.sha256')
        digest = hashlib.sha256(json.dumps([scene, identities, filters, length]).encode()).hexdigest()
        if not clip.exists() or not stamp.exists() or stamp.read_text() != digest:
            r.run('ffmpeg', '-y', '-v', 'error', *inputs, '-i', audio,
                  '-filter_complex', ';'.join(filters), '-map', '[out]', '-map', f'{len(shots)}:a',
                  '-af', 'adelay=300:all=1,apad', '-t', length, '-c:v', 'libx264', '-preset', 'veryfast',
                  '-crf', '20', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '128k', '-ar', '48000', '-ac', '2', clip)
            stamp.write_text(digest)
        clips.append(clip)
        timeline.append({**scene, 'start': position, 'end': position + length, 'shots': shots})
        words = json.loads((r.ASSETS / (scene['id'] + '-words.json')).read_text())
        group = []
        for word in words:
            group.append(word)
            if len(' '.join(w['text'] for w in group)) >= 76 or len(group) >= 13 or word is words[-1]:
                cues.append((position + .3 + group[0]['start'], position + .3 + group[-1]['end'],
                             '\n'.join(textwrap.wrap(' '.join(w['text'] for w in group), 44))))
                group = []
        position += length
        print(f"Rendered {scene['id']}: {length:.1f}s", flush=True)
    base = 'agentic-csharp-lab-01'
    srt = r.OUT / (base + '.en.srt')
    srt.write_text('\n\n'.join(f'{i}\n{r.srt_time(a)} --> {r.srt_time(b)}\n{t}'
                              for i, (a, b, t) in enumerate(cues, 1)) + '\n', encoding='utf-8')
    (r.OUT / (base + '.en.vtt')).write_text('WEBVTT\n\n' + '\n\n'.join(
        f'{r.srt_time(a).replace(",", ".")} --> {r.srt_time(b).replace(",", ".")}\n{t}'
        for a, b, t in cues), encoding='utf-8')
    meta = [';FFMETADATA1', 'title=Agentic C# - Lab 01', 'artist=Alon Fliess - synthetic review narration']
    for scene in timeline:
        meta += ['[CHAPTER]', 'TIMEBASE=1/1000', f'START={round(scene["start"]*1000)}',
                 f'END={round(scene["end"]*1000)}', 'title=' + scene['title']]
    (r.BUILD / 'lab01-chapters.txt').write_text('\n'.join(meta), encoding='utf-8')
    (r.BUILD / 'lab01-concat.txt').write_text('\n'.join(f"file '{p.name}'" for p in clips))
    r.run('ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', 'lab01-concat.txt',
          '-c', 'copy', 'lab01-joined.mp4')
    r.run('ffmpeg', '-y', '-v', 'error', '-i', 'lab01-joined.mp4', '-i', srt, '-i', 'lab01-chapters.txt',
          '-map', '0:v', '-map', '0:a', '-map', '1:0', '-map_metadata', '2', '-map_chapters', '2',
          '-c:v', 'copy', '-c:a', 'copy', '-c:s', 'mov_text', '-metadata:s:s:0', 'language=eng',
          '-movflags', '+faststart', r.OUT / (base + '-rough-cut.mp4'))
    (r.OUT / 'timeline.json').write_text(json.dumps(timeline, indent=2), encoding='utf-8')
    (r.OUT / 'timed-shooting-script.md').write_text('# Lab 01 — recorded edit\n\nSynthetic review narration; real footage.\n\n' + '\n\n'.join(
        f"## {r.srt_time(s['start'])}–{r.srt_time(s['end'])} — {s['title']}\n\n{s['narration']}"
        for s in timeline), encoding='utf-8')
    print(f'Complete: {position/60:.2f} minutes', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--phase', choices=['audio', 'video', 'all'], default='all')
    args = parser.parse_args()
    r.ASSETS.mkdir(exist_ok=True)
    r.OUT.mkdir(exist_ok=True)
    if args.phase in ['audio', 'all']:
        asyncio.run(speech.speech())
    if args.phase in ['video', 'all']:
        render()
