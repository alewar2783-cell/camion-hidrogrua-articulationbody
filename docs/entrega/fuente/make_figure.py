# Compone vistas.png (tira de 3 vistas enmarcadas) a partir de las capturas de docs/img
import os
from PIL import Image, ImageDraw, ImageFont
here = os.path.dirname(os.path.abspath(__file__)); img = os.path.join(here, "..", "..", "img")
INK, ORANGE, PAPER = (18, 51, 47), (232, 89, 12), (247, 243, 234)
a = Image.open(os.path.join(img, "estilo_04_volcado.png")).convert("RGB")
b = Image.open(os.path.join(img, "estilo_03_agarre.png")).convert("RGB")
H = 580
panel = a.crop((0, 0, 530, H))
grip = b.crop((700, 90, 1400, 670)); grip = grip.resize((int(grip.width * H / grip.height), H))
dump = a.crop((900, 200, 1480, 780))
parts = [panel, grip, dump]; tags = ["V1", "V2", "V3"]
pad, gap, fr = 14, 14, 3
W = sum(p.width for p in parts) + gap * 2 + 2 * pad + 6 * fr
out = Image.new("RGB", (W, H + 2 * pad + 2 * fr), PAPER); d = ImageDraw.Draw(out)
f = ImageFont.truetype("C:/Windows/Fonts/bahnschrift.ttf", 30)
x = pad
for p, t in zip(parts, tags):
    d.rectangle([x, pad, x + p.width + 2 * fr - 1, pad + H + 2 * fr - 1], outline=INK, width=fr)
    out.paste(p, (x + fr, pad + fr))
    d.rectangle([x + fr, pad + fr, x + fr + 62, pad + fr + 40], fill=ORANGE)
    d.text((x + fr + 31, pad + fr + 20), t, font=f, fill="white", anchor="mm")
    x += p.width + 2 * fr + gap
out.save(os.path.join(here, "vistas.png")); print(out.size)
