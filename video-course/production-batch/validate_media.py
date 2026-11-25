from pathlib import Path
import concurrent.futures,json,re,subprocess
root=Path('video-course')
def check(unit):
    movie=next((unit/'output').glob('*rough-cut.mp4'))
    info=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_format','-show_streams','-show_chapters','-of','json',str(movie)]))
    streams=info['streams']; duration=float(info['format']['duration'])
    assert any(s['codec_type']=='video' and s['width']==1920 and s['height']==1080 for s in streams)
    assert any(s['codec_type']=='audio' for s in streams)
    assert any(s['codec_type']=='subtitle' for s in streams)
    timeline=json.loads((unit/'output/timeline.json').read_text(encoding='utf-8'))
    expected_chapters = sum(bool(s.get('chapter')) for s in timeline) if unit.name.startswith('unit-00-') else len(timeline)
    assert len(info['chapters']) == expected_chapters
    assert abs(duration-timeline[-1]['end'])<.3
    srt=next((unit/'output').glob('*.srt')).read_text(encoding='utf-8')
    previous=0; count=0
    def seconds(s):
        h,m,v=s.replace(',','.').split(':');return int(h)*3600+int(m)*60+float(v)
    for a,b in re.findall(r'(\d\d:\d\d:\d\d,\d\d\d) --> (\d\d:\d\d:\d\d,\d\d\d)',srt):
        a,b=seconds(a),seconds(b)
        assert previous<=a+.002 and a<b and b<=duration
        previous=b;count+=1
    assert count>20
    tails = []
    for scene in timeline:
        words = json.loads((unit/'assets'/(scene['id']+'-words.json')).read_text())
        tail = scene['end'] - scene['start'] - .3 - words[-1]['end']
        assert 0 <= tail < 2.5, (unit.name, scene['id'], 'narration tail', tail)
        tails.append(tail)
    silence = subprocess.run(['ffmpeg','-hide_banner','-i',str(movie),
        '-af','silencedetect=noise=-40dB:d=2.5','-vn','-f','null','-'],capture_output=True,text=True)
    assert silence.returncode == 0, silence.stderr
    long_silences = re.findall(r'silence_start: ([0-9.]+)', silence.stderr)
    assert not long_silences, (unit.name, 'silences longer than 2.5 seconds', long_silences)
    result=subprocess.run(['ffmpeg','-v','error','-xerror','-i',str(movie),'-map','0:v:0','-map','0:a:0','-f','null','-'],capture_output=True,text=True)
    assert result.returncode==0,result.stderr
    return dict(unit=unit.name,duration=round(duration,2),chapters=expected_chapters,captions=count,
        maxNarrationTailSeconds=round(max(tails),3),silencesOver2_5Seconds=0,
        decode='passed',streams='1080p video + audio + English subtitles')
units=[p for p in sorted(root.glob('unit-*')) if p.name=='unit-00-agentic-csharp' or 1<=int(p.name.split('-')[1])<=17]
results=[]
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    for r in pool.map(check,units):
        results.append(r);print(r['unit'],'passed',flush=True)
(root/'production-batch/media-validation.json').write_text(json.dumps(results,indent=2)+'\n',encoding='utf-8')
