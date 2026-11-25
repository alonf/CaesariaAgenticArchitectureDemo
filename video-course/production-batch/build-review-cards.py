"""Rebuild Labs 03–06 exercise cards using the approved Agentic C# artwork."""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
CARDS = {
    3: ('Pause and prove it', [
        '1. Ask, then follow up in the same session.',
        '2. Inspect the actual tool trace.',
        '3. Repair through the deterministic UI.',
        '4. Ask again; verify directly at the Hub.',
        '5. Reset before the next lesson.',
    ], 'Investigate: expiration, restart, concurrent updates.',
        'Conversation continuity is not a freshness guarantee.'),
    4: ('Observation, evidence, inference', [
        '1. Ask Why with evidence present.',
        '2. Open the source and check the claim.',
        '3. Withhold evidence; start a new session.',
        '4. Inspect empty retrieval and uncertainty.',
        '5. Verify unchanged Hub state; restore evidence.',
    ], 'A citation badge is not a correctness proof.',
        'Investigate provenance and asset filtering.'),
    5: ('Memory is a lead, not proof', [
        '1. Save an explained L-417 case.',
        '2. Ask about L-528 in a new session.',
        '3. Separate live facts, evidence and analogy.',
        '4. Clear demo memory; repeat in a fresh session.',
        '5. Check the Hub and compare the trace.',
    ], 'Investigate matching, truncation and trust.',
        'Copyable steps and reset: lab.md'),
    6: ('Pause: verify the procedure', [
        '1. Investigate L-417 in a new session.',
        '2. Check load_skill and the Loaded badge.',
        '3. Compare the brief with SKILL.md and evidence.',
        '4. Withhold evidence and investigate again.',
        '5. Restore evidence; verify no repair occurred.',
    ], 'A procedure supplies context, not authority.',
        'Investigate discovery, governance and evaluation.'),
}

for number, (title, steps, takeaway, question) in CARDS.items():
    canvas = Image.open(ROOT / 'unit-00-agentic-csharp/assets/signature-background.png').convert('RGB').resize((1920, 1080))
    draw = ImageDraw.Draw(canvas)

    def write(x, y, text, size, color='white'):
        font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', size)
        assert draw.textbbox((x, y), text, font=font)[2] < 1820, text
        draw.text((x, y), text, font=font, fill=color)

    write(100, 70, f'Agentic C# · Lab {number:02}', 38, '#8de6ed')
    write(100, 160, title, 64)
    for index, step in enumerate(steps):
        write(110, 300 + index * 100, step, 44)
    write(110, 840, takeaway, 38, '#ffd54f')
    write(110, 905, question, 36)
    canvas.save(next(ROOT.glob(f'unit-{number:02}-*')) / 'assets/exercise.png')
