"""Apply reviewed redactions to source pixels before any crop, zoom or freeze.

The manifest contains coordinates and reasons, never the sensitive text itself.
Rectangles use a normalized 1920 x 1080 source canvas. Opaque fills deliberately
replace pixels; a weak blur is not adequate for account identifiers.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def source_filters(source, start=0, seconds=0):
    """Return FFmpeg filters for the requested source interval, including PTS reset."""
    source = Path(source).resolve()
    if source.suffix.lower() not in ('.mp4', '.mov'):
        return ''
    manifest = json.loads((ROOT / 'production-batch/privacy-redactions.json').read_text())
    key = source.relative_to(ROOT).as_posix()
    if key not in manifest['sources']:
        raise ValueError(f'Footage has no privacy review entry: {key}')
    filters = ['setpts=PTS-STARTPTS']
    for mask in manifest['sources'][key]:
        a, b = mask.get('start', 0), mask.get('end', 100000)
        if b < start or a > start + seconds:
            continue
        x, y, width, height = mask['rect']
        enabled = f'between(t,{max(0,a-start)},{b-start})'
        for excluded_start, excluded_end in mask.get('reviewedFullscreenIntervals', []):
            enabled += f'*not(between(t,{excluded_start-start},{excluded_end-start}))'
        filters.append(f"drawbox=x=iw*{x}/1920:y=ih*{y}/1080:"
                       f"w=iw*{width}/1920:h=ih*{height}/1080:color=0x252525:t=fill:"
                       f"enable='{enabled}'")
    return ','.join(filters) + ','
