"""Build revised Unit00 locally: real captured frames, source slides, synthetic voice.

This is explicitly an edited-still rough cut, not a continuous screen recording.
Both IDE sequences use observed execution; this script never fabricates UI evidence.
"""

import argparse
import asyncio
import hashlib
import importlib.util
import json
import math
import sys
from pathlib import Path

import build_intro as renderer

ROOT = Path(__file__).resolve().parent
UNIT = ROOT / "unit-00-foundations"
spec = importlib.util.spec_from_file_location("foundation_lesson", UNIT / "lesson.py")
lesson = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lesson)
renderer.UNIT = UNIT
renderer.ASSETS = UNIT / "assets"
renderer.OUT = UNIT / "output"
renderer.SPEECH_RATE = "+0%"
renderer.SCENES = []
for index, (title, visual, chapter, action, narration) in enumerate(lesson.LESSON):
    scene = {"id": f"foundation-{index + 1:02}", "title": title, "visual": visual,
             "actions": action, "narration": narration}
    if chapter:
        scene["chapter"] = chapter
    renderer.SCENES.append(scene)


def prepare_assets():
    import fitz
    from PIL import Image, ImageDraw, ImageFont
    pdf_path = renderer.BUILD / "H08.pdf"
    expected = "1adb7953e203f2005cc8a3cde45bc32bc35990242f74cfd66f696f509fe1989b"
    if hashlib.sha256(pdf_path.read_bytes()).hexdigest() != expected:
        raise RuntimeError("H08 source does not match recorded provenance")
    with fitz.open(pdf_path) as pdf:
        for scene in renderer.SCENES:
            if scene["visual"].startswith("H08:"):
                page = int(scene["visual"].split(":")[1])
                pdf[page - 1].get_pixmap(matrix=fitz.Matrix(2.5, 2.5)).save(renderer.ASSETS / f"H08-{page}.png")
    evidence = json.loads((UNIT / "evidence/after-visual-studio.json").read_text(encoding="utf-8-sig"))
    hub, debugger = evidence["hub"], evidence["debugger"]
    if debugger["debuggerAttached"] or not hub["reportedIsOn"]:
        raise RuntimeError("Captured final evidence does not meet the read-only/detach checkpoint")
    lesson.CARDS["hub-checkpoint"] = (
        "Actual Hub and debugger checkpoint",
        "Selected fields from independent API reads after both IDEs\n"
        "GET /api/energy/assets/L-417\n\n" +
        json.dumps({k: hub[k] for k in ["assetId", "reportedIsOn", "manualOverride", "stateRevision"]}, indent=2) +
        "\n\nGET /api/demo-breakpoints\n" +
        json.dumps({k: debugger[k] for k in ["processId", "debuggerAttached"]}, indent=2) +
        "\n\nRecorded evidence, not a terminal screenshot. No repair issued.")
    for key, (title, body) in lesson.CARDS.items():
        canvas = Image.new("RGB", (1920, 1080), "#081321")
        draw = ImageDraw.Draw(canvas)
        heading = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 50)
        font = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 29)
        draw.text((90, 90), title, font=heading, fill="#67dec3")
        bounds = draw.multiline_textbbox((90, 245), body, font=font, spacing=24)
        if bounds[2] > 1840 or bounds[3] > 1020:
            raise RuntimeError(f"Card overflow: {key}: {bounds}")
        draw.multiline_text((90, 245), body, font=font, fill="white", spacing=24)
        canvas.save(renderer.ASSETS / f"card-{key}.png")


def shooting_script():
    position = 0
    sections = []
    for scene in renderer.SCENES:
        audio = renderer.ASSETS / f"{scene['id']}.mp3"
        seconds = math.ceil((renderer.duration(audio) + .8) * 15) / 15 if audio.exists() else len(scene["narration"].split()) / 2.6
        start, end = renderer.srt_time(position), renderer.srt_time(position + seconds)
        sections.append(f"## {start}–{end} | {scene['title']}\n\n"
                        f"**Screen:** `{scene['visual']}`. {scene['actions']}\n\n"
                        f"**Narration:** {scene['narration']}\n")
        position += seconds
    (UNIT / "shooting-script.md").write_text(
        "# Unit 00 — foundations, environment, and debugging\n\n"
        "Timestamped production script. Timing follows generated narration when available. "
        "The rough cut uses edited actual still captures with synthetic narration; it is not a continuous screencast. "
        "Both IDE sequences use actual captures. Visual Studio's first expired request and fresh retry are distinguished. "
        "The long-pause timeout and successful detached retry are separate requests.\n\n" + "\n".join(sections), encoding="utf-8")


def sequence_captures():
    # Crops improve legibility; every image is an actual captured UI state.
    sequences = {
        15: [("capture:director-preview", .5, None), ("capture:director-ready", .5, None)],
        17: [("capture:vscode-arm", .45, [370, 300, 1440, 670]),
             ("capture:vscode-attached", .55, [370, 300, 1440, 670])],
        18: [("capture:vscode-creation-step", .45, [380, 180, 1700, 940]),
             ("capture:vscode-question", .55, [380, 185, 1500, 1210])],
        19: [("capture:vscode-tool", 1, [380, 330, 1500, 1060])],
        20: [("capture:vscode-response", .35, [380, 180, 1500, 1210]),
             ("capture:vscode-exchanges", .65, [380, 875, 2160, 520])],
        21: [("capture:debug-timeout", .7, [1788, 135, 395, 500]),
             ("capture:read-answer", .3, [1790, 135, 390, 735])],
        22: [("capture:vs-attached", .16, [375, 550, 1430, 630]),
             ("capture:vs-question", .17, [0, 140, 1495, 970]),
             ("capture:vs-step", .16, [0, 140, 1495, 970]),
             ("capture:vs-tool", .18, [0, 140, 1495, 970]),
             ("capture:vs-result", .23, [745, 675, 750, 410]),
             ("capture:vs-detached", .10, [375, 550, 1430, 630])],
        23: [("capture:read-answer", .3, [1790, 135, 390, 735]),
             ("card:hub-checkpoint", .7, None)],
    }
    for number, shots in sequences.items():
        scene = renderer.SCENES[number - 1]
        length = math.ceil((renderer.duration(renderer.ASSETS / f"{scene['id']}.mp3") + .8) * 15) / 15
        scene["shots"] = []
        for visual, share, crop in shots:
            item = {"visual": visual, "seconds": length * share}
            if crop:
                item["crop"] = crop
            scene["shots"].append(item)


async def speech():
    sys.path.insert(0, str(renderer.BUILD / "vendor"))
    import edge_tts
    gate = asyncio.Semaphore(4)

    async def speak(scene):
        async with gate:
            target = renderer.ASSETS / f"{scene['id']}.mp3"
            boundary = renderer.ASSETS / f"{scene['id']}-words.json"
            stamp = renderer.ASSETS / f"{scene['id']}-voice.sha256"
            digest = hashlib.sha256((renderer.VOICE + renderer.SPEECH_RATE + scene["narration"]).encode()).hexdigest()
            if target.exists() and boundary.exists() and stamp.exists() and stamp.read_text() == digest:
                print(f"Speech {scene['id']}: cached", flush=True)
                return
            for attempt in range(3):
                try:
                    words = []
                    temporary = target.with_suffix(".part")
                    with temporary.open("wb") as stream:
                        async for event in edge_tts.Communicate(scene["narration"], renderer.VOICE,
                                                               rate=renderer.SPEECH_RATE, boundary="WordBoundary").stream():
                            if event["type"] == "audio":
                                stream.write(event["data"])
                            elif event["type"] == "WordBoundary":
                                words.append({"start": event["offset"] / 1e7,
                                              "end": (event["offset"] + event["duration"]) / 1e7, "text": event["text"]})
                    if temporary.stat().st_size < 1000 or not words:
                        raise RuntimeError("Speech response has no usable audio/timing")
                    temporary.replace(target)
                    boundary.write_text(json.dumps(words, indent=2), encoding="utf-8")
                    stamp.write_text(digest)
                    print(f"Speech {scene['id']}: {renderer.duration(target):.1f}s", flush=True)
                    return
                except Exception:
                    if attempt == 2:
                        raise
                    await asyncio.sleep(2 + attempt * 2)

    await asyncio.gather(*(speak(scene) for scene in renderer.SCENES))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--phase", choices=["script", "assets", "audio", "video", "all"], default="all")
    phase = parser.parse_args().phase
    for folder in [renderer.ASSETS, renderer.OUT]:
        folder.mkdir(parents=True, exist_ok=True)
    (UNIT / "scenes.json").write_text(json.dumps(renderer.SCENES, indent=2), encoding="utf-8")
    if phase in ["assets", "all"]:
        prepare_assets()
    if phase in ["audio", "all"]:
        asyncio.run(speech())
    shooting_script()
    if phase in ["video", "all"]:
        sequence_captures()
        renderer.render_video()


if __name__ == "__main__":
    main()
