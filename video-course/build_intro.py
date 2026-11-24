"""Build the narrated introductory video from the supplied slides and real app captures.

Usage: python -X utf8 video-course/build_intro.py --phase audio|assets|video|all
Requires ffmpeg/ffprobe, PyMuPDF and edge-tts. No credentials are read by this script.
The speech service receives only the reviewed narration in scenes.json.
"""

import argparse
import asyncio
import hashlib
import json
import math
import shutil
import subprocess
import textwrap
from pathlib import Path

ROOT = Path(__file__).resolve().parent
UNIT = ROOT / "unit-00-introduction"
BUILD = ROOT / ".build"
ASSETS = UNIT / "assets"
OUT = UNIT / "output"
SCENES = json.loads((UNIT / "scenes.json").read_text(encoding="utf-8"))
VOICE = "en-US-AndrewNeural"
SPEECH_RATE = "+8%"


def run(*args, cwd=BUILD):
    result = subprocess.run([str(a) for a in args], cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError(result.stderr[-6000:])
    return result.stdout


def duration(path):
    return float(run("ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", path))


async def speech():
    import sys
    sys.path.insert(0, str(BUILD / "vendor"))
    import edge_tts
    for scene in SCENES:
        target = ASSETS / f"{scene['id']}.mp3"
        boundary = ASSETS / f"{scene['id']}-words.json"
        digest = hashlib.sha256((VOICE + SPEECH_RATE + scene["narration"]).encode()).hexdigest()
        stamp = ASSETS / f"{scene['id']}-voice.sha256"
        if target.exists() and boundary.exists() and stamp.exists() and stamp.read_text() == digest:
            print(f"Speech {scene['id']}: cached", flush=True)
            continue
        for attempt in range(3):
            try:
                words = []
                temporary = target.with_suffix(".part")
                with temporary.open("wb") as stream:
                    async for event in edge_tts.Communicate(scene["narration"], VOICE, rate=SPEECH_RATE, boundary="WordBoundary").stream():
                        if event["type"] == "audio":
                            stream.write(event["data"])
                        elif event["type"] == "WordBoundary":
                            words.append({"start": event["offset"] / 1e7, "end": (event["offset"] + event["duration"]) / 1e7, "text": event["text"]})
                if temporary.stat().st_size < 1000 or not words:
                    raise RuntimeError("Speech response has no usable audio or timing")
                temporary.replace(target)
                boundary.write_text(json.dumps(words, indent=2), encoding="utf-8")
                stamp.write_text(digest)
                print(f"Speech {scene['id']}: {duration(target):.1f}s", flush=True)
                break
            except Exception:
                if attempt == 2:
                    raise
                await asyncio.sleep(2 + attempt * 2)


CARDS = {
    "clone": ("Use the pinned .NET 11 baseline", "git clone `\n  https://github.com/alonf/CaesariaAgenticArchitectureDemo.git\ncd CaesariaAgenticArchitectureDemo\ngit fetch origin upgrade/net11-foundry-insights-routines\ngit switch --detach `\n  89d5a513be97301857dd4f156ce0773103712d51\ngit rev-parse HEAD\n\nExact copyable commands: lab.md\nBook rationale: September edition\nExecutable baseline: this .NET 11 commit"),
    "build": ("Build before running", "# PowerShell 7; SDK selected by global.json\n. ./scripts/Use-CaesareaSdk.ps1\ndotnet --version\ndotnet restore\ndotnet build\n\ndotnet test --project `\n  ./Tests/Caesarea.Deterministic.Tests/Caesarea.Deterministic.Tests.csproj\n\nUnit 00 requires no model, Docker or Functions setup.\nCheck actual build/test output before recording."),
    "model": ("Use your own model deployment", "az login --tenant '<your-tenant-id>'\naz account set --subscription '<subscription-id>'\n\n$projectEndpoint = '<your Foundry project endpoint>'\n$modelDeployment = '<your deployment name>'\n\nConfigure these three local agents:\n  OperationsAgentApi\n  SecurityAgentApi\n  WorkforceAgentApi\n\nSee setup-guide.md for all six environment variables.\nStart Aspire from the same configured shell."),
    "start": ("Start only the local lab", "dotnet run --project `\n  ./Caesarea.AppHost/Caesarea.AppHost.csproj `\n  --no-build --launch-profile https\n\nKeep the interactive launch terminal open.\nUse this run's private Aspire login link.\n\nDemoControl.Web     prepares conditions\nCommandCenter.Web   performs the operator action\n\nStop: Ctrl+C, or ./scripts/Stop-CaesareaDemo.ps1"),
    "exercise": ("Pause and try it", "1. Reset Lights On / Deterministic.\n2. Save a fresh Hub state snapshot.\n3. Restore through Command Center.\n4. Check Hub state, receipt and revision.\n\nExplain:\nWho owns reported state?\nWhat proves completion?\nWhat do we still not know about the cause?\n\nCopyable steps and checkpoints: lab.md"),
    "recovery": ("Expected result and recovery", "EXPECTED, not a fabricated execution result:\n\nreportedIsOn = false\nmanualOverride = false\nlastCommand.status = Succeeded\nnewer revision + correlated confirmation\n\nWrong fixture? Reapply Lights On Reported by a Client.\nCheck healthy controller and Deterministic stage.\nRead fresh state before retrying.\n\nA desired target alone is not success."),
    "next": ("Ready for the first investigation", "EnergyHubService.cs / RestoreScheduledModeAsync\n\nBook: Chapter 6 and Chapter 14\nSlides: H08 architecture and agent keyframes\n\nVS Code or Visual Studio 2026 for source reading.\nFirst useful breakpoint: the first agent lesson.\n\nReset L-417 for the next lesson, or stop the lab:\n./scripts/Stop-CaesareaDemo.ps1\n\nUnit 00 pilot — stop here for review."),
}


def prepare_assets():
    import fitz
    from PIL import Image, ImageDraw, ImageFont
    for name, expected in [("H08", "1adb7953e203f2005cc8a3cde45bc32bc35990242f74cfd66f696f509fe1989b"), ("W20", "9927edfc42688018b5defeeb8dc95eb70ff03c8fcdf3300e3d502242d31c12a7")]:
        pdf = BUILD / f"{name}.pdf"
        if hashlib.sha256(pdf.read_bytes()).hexdigest() != expected:
            raise RuntimeError(f"Unexpected source PDF: {name}")
    for scene in SCENES:
        if scene["visual"].startswith("H08:"):
            page = int(scene["visual"].split(":")[1])
            with fitz.open(BUILD / "H08.pdf") as pdf:
                pdf[page - 1].get_pixmap(matrix=fitz.Matrix(1920 / 720, 1920 / 720)).save(ASSETS / f"H08-{page}.png")
    source_path = BUILD / "unit00-lab/Services/EnergyHub.Api/Services/EnergyHubService.cs"
    source = source_path.read_text(encoding="utf-8")
    start = source.index("            var commandResult = await _smartpoleGateway.SetLampStateAsync")
    end = source.index("                        LastCommand = new CommandRecord(", start)
    assignment_start = source.index("                    _twin = _twin with", start)
    assignment = textwrap.dedent(source[assignment_start:end])
    code = ("// Excerpt inside the confirmed-success branch.\n"
            "// SetLampStateAsync, lock and revision guard precede this.\n\n" + assignment +
            "    // ... LastCommand receipt continues here ...\n};\n\n"
            "// Reported state comes from confirmedIsOn, not a model answer.")
    line = source[:assignment_start].count("\n") + 1
    CARDS["hub-code"] = (f"EnergyHubService.cs — line {line} (excerpt)", code)
    before = json.loads((UNIT / "evidence/ui-before.json").read_text(encoding="utf-8-sig"))
    after = json.loads((UNIT / "evidence/ui-after.json").read_text(encoding="utf-8-sig"))
    receipt = after["lastCommand"]
    activity = json.loads((UNIT / "evidence/ui-activity.json").read_text(encoding="utf-8-sig"))
    reset = json.loads((UNIT / "evidence/api-reset-after-ui.json").read_text(encoding="utf-8-sig"))["state"]
    if after["reportedIsOn"] or after["manualOverride"] or receipt["status"] != "Succeeded":
        raise RuntimeError("Recorded evidence does not demonstrate a successful repair")
    CARDS["hub-after"] = ("Actual Hub response after the UI click", json.dumps({
        "assetId": after["assetId"], "reportedIsOn": after["reportedIsOn"],
        "desiredIsOn": after["desiredIsOn"], "manualOverride": after["manualOverride"],
        "stateRevision": after["stateRevision"], "lastCommand": receipt}, indent=2) +
        f"\n\nSelected fields from evidence/ui-after.json\nBefore revision: {before['stateRevision']} — this run only")
    CARDS["hub-activity"] = ("Actual correlated Hub events", "GET /api/energy/assets/L-417/activity?limit=20\n\n" +
        "Command correlation:\n" + receipt["correlationId"] + "\n\n" +
        "\n\n".join(f"{e['commandStatus']}\n{e['message']}" for e in reversed(activity)) +
        "\n\nSource: evidence/ui-activity.json\nSelected actual response fields; not a terminal capture.")
    CARDS["reset"] = ("Verified reset — API fallback in this rough cut", "Director reset click still needs a screen pickup.\nThe local API reset was executed and verified:\n\n" +
        "POST /api/demo-scenarios/apply/ForgottenOverride\nGET  /api/energy/assets/L-417\n\n" +
        json.dumps({k: reset[k] for k in ["reportedIsOn", "manualOverride", "stateRevision", "controllerHealth"]}, indent=2) +
        "\n\nActual response: evidence/api-reset-after-ui.json\nLearner: reapply Scenario 1 in Director (lab step 7).")
    CARDS["baseline-check"] = ("Clean baseline verification", "89d5a513be97301857dd4f156ce0773103712d51\n\nRestore and build succeeded.\nBuild: 1 AZFW0108 warning (Functions extensions restore).\nTests: 447 passed, 4 skipped.\nFormatting verification passed.\n\nRecorded check summary — not a console screenshot.\nDetails: baseline.md and execution-evidence.md\n\nStartup / Aspire screen pickup remains outstanding.\nThe app captures that follow are actual local execution.")
    shutil.copyfile("C:/Windows/Fonts/consola.ttf", BUILD / "mono.ttf")
    shutil.copyfile("C:/Windows/Fonts/segoeui.ttf", BUILD / "sans.ttf")
    for key, (heading, body) in CARDS.items():
        (BUILD / f"card-{key}-heading.txt").write_text(heading, encoding="utf-8", newline="\n")
        (BUILD / f"card-{key}.txt").write_text(body, encoding="utf-8", newline="\n")
        canvas = Image.new("RGB", (1920, 1080), "#101a29")
        draw = ImageDraw.Draw(canvas)
        heading_font = ImageFont.truetype(str(BUILD / "sans.ttf"), 54)
        body_font = ImageFont.truetype(str(BUILD / "mono.ttf"), 27 if key == "hub-after" else 30)
        spacing = 9 if key == "hub-after" else 18
        draw.text((90, 90), heading, font=heading_font, fill="#72d5e7")
        bounds = draw.multiline_textbbox((90, 230), body, font=body_font, spacing=spacing)
        if bounds[2] > 1830 or bounds[3] > 1020:
            raise RuntimeError(f"Card text overflows the safe area: {key}, {bounds}")
        draw.multiline_text((90, 230), body, font=body_font, fill="white", spacing=spacing)
        canvas.save(ASSETS / f"card-{key}.png")
    print("Original slide frames and readable setup/code cards prepared", flush=True)


def srt_time(seconds):
    milliseconds = round(seconds * 1000)
    hours, remainder = divmod(milliseconds, 3600000)
    minutes, remainder = divmod(remainder, 60000)
    secs, ms = divmod(remainder, 1000)
    return f"{hours:02}:{minutes:02}:{secs:02},{ms:03}"


def visual_path(scene):
    kind, key = scene["visual"].split(":", 1)
    if kind == "H08":
        return ASSETS / f"H08-{key}.png"
    return ASSETS / f"{kind}-{key}.png"


def render_video():
    missing = [str(visual_path(v)) for s in SCENES for v in s.get("shots", [s]) if not visual_path(v).exists()]
    if missing:
        raise RuntimeError("Required real captures missing; no substitutes were generated:\n" + "\n".join(missing))
    clips = []
    timeline = []
    cues = []
    position = 0.0
    for scene in SCENES:
        audio = ASSETS / f"{scene['id']}.mp3"
        length = math.ceil(max(duration(audio) + 0.8, scene.get("planned_seconds", 0)) * 15) / 15
        title = BUILD / f"title-{scene['id']}.txt"
        title.write_text(f"CAESAREA  /  UNIT 00     {scene['title']}", encoding="utf-8", newline="\n")
        source = scene.get("credit") or ("Alon Fliess | H08 | PDF page " + scene["visual"].split(":")[1] if scene["visual"].startswith("H08:") else "ROUGH CUT | Edited still captures and evidence cards | Simulated device | 89d5a51")
        credit = BUILD / f"credit-{scene['id']}.txt"
        credit.write_text(source, encoding="utf-8", newline="\n")
        clip = BUILD / f"clip-{scene['id']}.mp4"
        inputs = []
        filters = []
        shots = scene.get("shots", [{"visual": scene["visual"], "seconds": length}])
        if abs(sum(s["seconds"] for s in shots) - length) > .01:
            raise RuntimeError(f"Shot durations do not match scene {scene['id']}")
        for i, shot in enumerate(shots):
            inputs.extend(["-loop", "1", "-framerate", "15", "-i", visual_path(shot)])
            crop = ""
            if "crop" in shot:
                x, y, w, h = shot["crop"]
                crop = f"crop={w}:{h}:{x}:{y},"
            filters.append(f"[{i}:v]{crop}scale=1800:900:force_original_aspect_ratio=decrease,"
                           f"pad=1920:1080:(ow-iw)/2:80:color=0x081321,setsar=1,"
                           f"trim=duration={shot['seconds']},setpts=PTS-STARTPTS[v{i}]")
        labels = "".join(f"[v{i}]" for i in range(len(shots)))
        vf = (f"{labels}concat=n={len(shots)}:v=1:a=0,"
              f"drawtext=fontfile=sans.ttf:textfile={title.name}:expansion=none:fontcolor=white:fontsize=29:x=60:y=22,"
              f"drawtext=fontfile=sans.ttf:textfile={credit.name}:expansion=none:fontcolor=0xa5bbc8:fontsize=20:x=60:y=1035,"
              f"fade=t=in:st=0:d=0.25,fade=t=out:st={length - .25:.3f}:d=0.25[outv]")
        filters.append(vf)
        run("ffmpeg", "-y", "-v", "error", *inputs, "-i", audio,
            "-filter_complex", ";".join(filters), "-map", "[outv]", "-map", f"{len(shots)}:a",
            "-af", "adelay=300:all=1,apad", "-t", f"{length:.6f}", "-c:v", "libx264", "-preset", "veryfast",
            "-tune", "stillimage", "-crf", "23", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k", "-ar", "48000", "-ac", "2", clip)
        clips.append(clip)
        timeline.append({**scene, "start": round(position, 3), "end": round(position + length, 3)})
        words = json.loads((ASSETS / f"{scene['id']}-words.json").read_text(encoding="utf-8"))
        group = []
        for word in words:
            group.append(word)
            if len(" ".join(w["text"] for w in group)) >= 78 or len(group) >= 14 or word is words[-1]:
                text = " ".join(w["text"] for w in group)
                cues.append((position + .3 + group[0]["start"], position + .3 + group[-1]["end"], "\n".join(textwrap.wrap(text, 44))))
                group = []
        position += length
        print(f"Rendered scene {scene['id']}: {length:.1f}s", flush=True)
    srt = OUT / "unit-00-introduction.en.srt"
    srt.write_text("\n\n".join(f"{i}\n{srt_time(a)} --> {srt_time(b)}\n{text}" for i, (a, b, text) in enumerate(cues, 1)) + "\n", encoding="utf-8")
    chapters = [s for s in timeline if "chapter" in s]
    metadata = [";FFMETADATA1", "title=Caesarea - Unit 00 - MAF and the demo", "artist=Alon Fliess - course materials; synthetic narration", "comment=Original slides with edited walkthrough captures. No cloud provisioning was performed."]
    for i, chapter in enumerate(chapters):
        end = chapters[i + 1]["start"] if i + 1 < len(chapters) else position
        metadata.extend(["[CHAPTER]", "TIMEBASE=1/1000", f"START={round(chapter['start'] * 1000)}", f"END={round(end * 1000)}", f"title={chapter['chapter']}"])
    (BUILD / "chapters.ffmeta").write_text("\n".join(metadata), encoding="utf-8")
    (BUILD / "concat.txt").write_text("\n".join(f"file '{clip.name}'" for clip in clips), encoding="utf-8")
    run("ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", "concat.txt", "-c", "copy", "joined.mp4")
    final = OUT / "caesarea-unit-00-rough-cut.mp4"
    run("ffmpeg", "-y", "-v", "error", "-i", "joined.mp4", "-i", srt, "-i", "chapters.ffmeta", "-map", "0:v", "-map", "0:a", "-map", "1:0", "-map_metadata", "2", "-map_chapters", "2", "-c:v", "copy", "-c:a", "copy", "-c:s", "mov_text", "-metadata:s:s:0", "language=eng", "-disposition:s:0", "default", "-movflags", "+faststart", final)
    (OUT / "timeline.json").write_text(json.dumps(timeline, indent=2), encoding="utf-8")
    (OUT / "chapters.txt").write_text("\n".join(f"{srt_time(s['start']).split(',')[0]} {s['chapter']}" for s in chapters), encoding="utf-8")
    (UNIT / "narration.md").write_text("# Unit 00 narration\n\nVoice: Microsoft Andrew Neural, synthetic English narration.\n\n" + "\n\n".join(f"## {s['id']}. {s['title']}\n\n{s['narration']}" for s in SCENES), encoding="utf-8")
    print(f"VIDEO COMPLETE: {final} ({position / 60:.2f} minutes)", flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--phase", choices=["audio", "assets", "video", "all"], default="all")
    args = parser.parse_args()
    for folder in [BUILD, ASSETS, OUT]:
        folder.mkdir(parents=True, exist_ok=True)
    if args.phase in ["audio", "all"]:
        asyncio.run(speech())
    if args.phase in ["assets", "all"]:
        prepare_assets()
    if args.phase in ["video", "all"]:
        render_video()


if __name__ == "__main__":
    main()
