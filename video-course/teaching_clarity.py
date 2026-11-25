"""Editorial inserts with stable scene IDs and emphasis on genuine pixels."""
import copy
import json
import re


def edits(unit):
    path = unit / 'teaching-edits.json'
    return json.loads(path.read_text(encoding='utf-8')) if path.exists() else {}


def scenes(unit, original):
    config = edits(unit)
    result = []
    for scene in original:
        for insert in config.get('inserts', []):
            if insert['before'] == scene['id']:
                result.append({k: v for k, v in insert.items() if k not in ('before', 'shots')})
        result.append({**scene, **config.get('revisions', {}).get(scene['id'], {})})
    ids = [s['id'] for s in result]
    if len(ids) != len(set(ids)):
        raise ValueError('Duplicate editorial scene ID')
    return result


def manifest(unit, original):
    result = copy.deepcopy(original)
    config = edits(unit)
    result.update(config.get('shots', {}))
    for insert in config.get('inserts', []):
        result[insert['id']] = insert['shots']
    for scene, markers in config.get('highlights', {}).items():
        result.setdefault('highlights', {}).setdefault(scene, []).extend(markers)
    return result


def focus_filter(shot):
    if 'focus' not in shot:
        return ''
    x, y, w, h = shot['focus']
    if min(x, y) < 0 or min(w, h) <= 0 or x + w > 1920 or y + h > 1080:
        raise ValueError(f'Invalid normalized focus rectangle: {shot}')
    return f'scale=1920:1080,crop={w}:{h}:{x}:{y},'


def guide_filter(shot):
    rect = shot.get('point')
    if not rect:
        return ''
    x, y, w, h = rect
    fx, fy, fw, fh = shot.get('focus', [0, 0, 1920, 1080])
    scale = min(1920 / fw, 1080 / fh)
    x = round((x-fx)*scale + (1920-fw*scale)/2)
    y = round((y-fy)*scale + (1080-fh*scale)/2)
    w, h = round(w*scale), round(h*scale)
    enabled = f"between(t,0.6,{shot['point_end']})" if 'point_end' in shot else 'gte(t,0.6)'
    return f",drawbox=x={x}:y={y}:w={w}:h={h}:color=0xffd54f:thickness=4:enable='{enabled}'"


def timed_shots(unit, scene, shots):
    """Align selected cuts to spoken phrases, extending only labeled frame holds."""
    result = copy.deepcopy(shots)
    words = json.loads((unit / 'assets' / (scene['id']+'-words.json')).read_text())
    tokens = [(t, w['start']) for w in words for t in re.findall('[a-z0-9]+', w['text'].lower())]
    elapsed = 0
    for i, shot in enumerate(result):
        if 'narration_anchor' in shot:
            wanted = re.findall('[a-z0-9]+', shot['narration_anchor'].lower())
            matches = [j for j in range(len(tokens)-len(wanted)+1)
                       if [t[0] for t in tokens[j:j+len(wanted)]] == wanted]
            if not matches or i == 0:
                raise ValueError(f'Invalid shot anchor: {shot}')
            target = tokens[matches[0]][1] + .3
            if target < elapsed:
                raise ValueError(f'Source motion exceeds narration anchor: {scene["id"]} {shot}')
            result[i-1]['hold'] = result[i-1].get('hold', 0) + target-elapsed
            elapsed = target
        elapsed += shot.get('seconds', 0) + shot.get('hold', 0)
    return result


def phrase_markers(unit, scene):
    config = edits(unit).get('phrase_markers', {}).get(scene['id'], [])
    if not config:
        return []
    words = json.loads((unit / 'assets' / (scene['id']+'-words.json')).read_text())
    tokens = [(t, w['start']) for w in words for t in re.findall('[a-z0-9]+', w['text'].lower())]
    result = []
    for marker in config:
        wanted = re.findall('[a-z0-9]+', marker['phrase'].lower())
        matches = [i for i in range(len(tokens)-len(wanted)+1)
                   if [t[0] for t in tokens[i:i+len(wanted)]] == wanted]
        if not matches:
            raise ValueError(f'Missing narration anchor: {marker}')
        start = tokens[matches[0]][1] + .3
        result.append(dict(start=start, end=start+marker.get('seconds', 5), rect=marker['rect']))
    return result
