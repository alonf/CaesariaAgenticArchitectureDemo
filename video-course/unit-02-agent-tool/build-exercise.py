"""Compose Lab 02's exercise card on the approved course background."""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

unit = Path(__file__).resolve().parent
background = unit.parent / 'unit-00-agentic-csharp/assets/signature-background.png'
canvas = Image.open(background).convert('RGB').resize((1920, 1080))
draw = ImageDraw.Draw(canvas)
font_path = Path('C:/Windows/Fonts/segoeui.ttf')


def text(position, value, size, color):
    draw.text(position, value, font=ImageFont.truetype(str(font_path), size), fill=color)


text((100, 90), 'Agentic C# · Lab 02', 38, '#8de6ed')
text((100, 185), 'Pause and prove the boundary', 64, 'white')
for index, line in enumerate([
    '1. Reset L-417 and verify the Hub fixture.',
    '2. Ask, then inspect tool arguments and result.',
    '3. Compare the two observable model exchanges.',
    '4. Confirm the Hub was read, not repaired.',
]):
    text((110, 335 + index * 115), line, 44, 'white')
text((110, 845), 'Explain: selection → execution → context → answer', 38, '#ffd54f')
canvas.save(unit / 'assets/exercise.png')
