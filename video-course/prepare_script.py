"""Export the Unit00 shooting script and draft captions from the editable scene manifest.

The timestamps are a planned edit, not evidence that footage was captured. Audio word timings,
when available, replace evenly distributed draft timings. Never creates application screenshots.
"""

import json
import textwrap
from pathlib import Path

UNIT = Path(__file__).resolve().parent / "unit-00-introduction"


def stamp(seconds, srt=False):
    total_ms = round(seconds * 1000)
    hours, rest = divmod(total_ms, 3600000)
    minutes, rest = divmod(rest, 60000)
    secs, ms = divmod(rest, 1000)
    return f"{hours:02}:{minutes:02}:{secs:02},{ms:03}" if srt else f"{minutes + hours * 60:02}:{secs:02}"


def main():
    scenes = json.loads((UNIT / "scenes.json").read_text(encoding="utf-8"))
    script = ["# Unit 00 — timestamped shooting script", "",
              "**Status: Unit 00 still-capture rough cut.** Timings describe the planned 10:45 edit;",
              "see execution-evidence.md for captured shots and missing pickups. English synthetic narration. Stop after this",
              "pilot for review. Use the clean application baseline in baseline.md and the copyable",
              "steps in lab.md. All app shots must come from actual execution. SmartPole is simulated.", "",
              "H08 slide pages include animation keyframes; retain Alon Fliess attribution. Book",
              "Ch6/7 supplies the autonomy rationale, Ch14 the reproducible lab. Current code and",
              "actual Hub responses settle discrepancies. No cloud deployment or model call here.", "",
              "Record at 1080p or better. Enlarge code/JSON to readable text; hide tokens, notifications",
              "and unrelated windows. Record the actual clicks and waits. A rough cut using edited",
              "still captures must say so on screen; do not call it continuous footage.", ""]
    cues = []
    position = 0
    for scene in scenes:
        length = scene["planned_seconds"]
        script += [f"## {scene['id']} · {stamp(position)}–{stamp(position + length)} · {scene['title']}", "",
                   f"**Screen action:** {scene['action']}", "",
                   f"**Shot source:** `{scene['visual']}`. **Check:** {scene['verify']}", "",
                   "**Edit:** " + "; ".join(f"{s['seconds']}s {s['visual']}" + (" (crop)" if "crop" in s else "") for s in scene.get("shots", [{"visual": scene['visual'], "seconds": length}])), "",
                   "**Narration:**", "", scene["narration"], ""]
        words_path = UNIT / "assets" / f"{scene['id']}-words.json"
        if words_path.exists():
            words = json.loads(words_path.read_text(encoding="utf-8"))
        else:
            tokens = scene["narration"].split()
            step = (length - 1) / len(tokens)
            words = [{"text": t, "start": i * step, "end": (i + 1) * step} for i, t in enumerate(tokens)]
        group = []
        for word in words:
            group.append(word)
            if len(" ".join(w["text"] for w in group)) >= 72 or word is words[-1]:
                cues.append((position + .3 + group[0]["start"], position + .3 + group[-1]["end"],
                             "\n".join(textwrap.wrap(" ".join(w["text"] for w in group), 42))))
                group = []
        position += length
    script += ["## Recording completion checklist", "",
               "- [x] Actual anomaly, Director preparation and post-click main-app result captured as stills.",
               "- [x] Hub before captured in browser; after/receipt/activity saved and rendered as labeled evidence cards.",
               "- [x] Clean-baseline source excerpt and verified API reset prepared.",
               "- [x] Exercise, expected outcome and failure recovery included.",
               "- [ ] Pickup: startup/Aspire resource view and Director reset with main-app refresh.",
               "- [ ] Pickup: continuous click/pending/completion footage for a finished motion edit.",
               "- [ ] Pilot review before any later unit is produced.", ""]
    (UNIT / "shooting-script.md").write_text("\n".join(script), encoding="utf-8")
    (UNIT / "unit-00-draft.en.srt").write_text("\n\n".join(
        f"{i}\n{stamp(a, True)} --> {stamp(b, True)}\n{t}" for i, (a, b, t) in enumerate(cues, 1)) + "\n", encoding="utf-8")
    (UNIT / "narration.md").write_text("# Unit 00 narration — revised hands-on pilot\n\nSynthetic voice for the edited-still rough cut; see execution-evidence.md for limitations.\n\n" +
        "\n\n".join(f"## {s['id']}. {s['title']}\n\n{s['narration']}" for s in scenes), encoding="utf-8")
    print(f"Exported script and draft captions: {stamp(position)}, {len(scenes)} shots")


if __name__ == "__main__":
    main()
