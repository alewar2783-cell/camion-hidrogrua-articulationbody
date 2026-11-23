# Esquema cinematico (SVG -> PNG con Chrome headless). Valores de Assets/Scripts/Runtime/TruckSpec.cs
import os, math, subprocess
INK, TEAL, ORANGE, OTX, SAND, PAPER, GREY = "#12332f", "#0f6b5f", "#e8590c", "#b4440a", "#d9c9a0", "#f7f3ea", "#55615c"
W, H = 1200, 680
o = []
def line(x1, y1, x2, y2, c=TEAL, w=6, dash=None, cap="round"):
    d = f' stroke-dasharray="{dash}"' if dash else ""
    o.append(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{c}" stroke-width="{w}" stroke-linecap="{cap}"{d}/>')
def text(x, y, t, size=17, w="normal", fam="Consolas", fill=INK, anchor="middle"):
    o.append(f'<text x="{x}" y="{y}" text-anchor="{anchor}" font-family="{fam}" font-size="{size}" font-weight="{w}" fill="{fill}">{t}</text>')
def col(p): return ORANGE if p else TEAL
def dsh(p): return ' stroke-dasharray="7 5"' if p else ""

def revolute(x, y, p=False):
    o.append(f'<circle cx="{x}" cy="{y}" r="19" fill="{PAPER}" stroke="{col(p)}" stroke-width="3.5"{dsh(p)}/>')
    for a in (45, 135):
        dx, dy = 10 * math.cos(math.radians(a)), 10 * math.sin(math.radians(a))
        line(x - dx, y - dy, x + dx, y + dy, col(p), 3, cap="butt")
def spherical(x, y, p=True):
    o.append(f'<circle cx="{x}" cy="{y}" r="19" fill="{PAPER}" stroke="{col(p)}" stroke-width="3.5"{dsh(p)}/>')
    o.append(f'<circle cx="{x}" cy="{y}" r="5" fill="{col(p)}"/>')
    for s in (-1, 1):
        r = 30; a0, a1 = math.radians(-42), math.radians(42)
        x0, y0 = x + s * r * math.cos(a0), y + r * math.sin(a0); x1, y1 = x + s * r * math.cos(a1), y + r * math.sin(a1)
        sweep = 1 if s == 1 else 0
        o.append(f'<path d="M{x0:.1f} {y0:.1f} A{r} {r} 0 0 {sweep} {x1:.1f} {y1:.1f}" fill="none" stroke="{col(p)}" stroke-width="3"/>')
def prismatic(x, y, arrow_y=-28):
    o.append(f'<rect x="{x-38}" y="{y-15}" width="76" height="30" rx="3" fill="{PAPER}" stroke="{TEAL}" stroke-width="3.5"/>')
    o.append(f'<rect x="{x-8}" y="{y-8}" width="40" height="16" fill="{TEAL}"/>')
    ay = y + arrow_y
    line(x - 32, ay, x + 32, ay, TEAL, 2.5, cap="butt")
    o.append(f'<path d="M{x-40} {ay} l9 -5 v10 z M{x+40} {ay} l-9 -5 v10 z" fill="{TEAL}"/>')
def ground(x, y, k=1.0):
    line(x, y - 38 * k, x, y + 38 * k, INK, 6, cap="butt")
    for i in range(-3, 3):
        line(x - 3, y + (i * 11 + 7) * k, x - 22 * k, y + (i * 11 + 20) * k, INK, 2.5, cap="butt")
def label(x, y, name, l2, l3, above, passive=False):
    # bloque de 3 lineas con guia hacia la union
    c = OTX if passive else INK
    if above:
        ys = (y - 98, y - 74, y - 50); line(x, y - 40, x, y - 24, GREY, 1.5, "3 3", "butt")
    else:
        ys = (y + 66, y + 90, y + 114); line(x, y + 24, x, y + 42, GREY, 1.5, "3 3", "butt")
    text(x, ys[0], name, 21, "bold", "Bahnschrift", c)
    text(x, ys[1], l2, 17, "normal", "Consolas", c); text(x, ys[2], l3, 17, "normal", "Consolas", c)

YC, YB, YR = 190, 520, 355
# raices y cadenas (enlaces)
line(200, YC, 910, YC); line(200, YR, 200, YB); line(200, YC, 200, YR)
line(200, YB, 640, YB); line(100, YR, 200, YR, TEAL)
line(910, 120, 910, 260); line(910, 120, 930, 120); line(910, 260, 930, 260)
ground(100, YR)
text(20, YR + 84, "1 Chasis_Root", 19, "bold", "Bahnschrift", INK, "start")
text(20, YR + 106, "inmóvil", 17, "normal", "Consolas", INK, "start")
text(20, YR + 126, "1000 kg", 17, "normal", "Consolas", INK, "start")
text(215, 52, "GRÚA", 20, "bold", "Bahnschrift", OTX, "start"); line(285, 46, 840, 46, SAND, 2, cap="butt")
text(215, 386, "VOLQUETE", 20, "bold", "Bahnschrift", OTX, "start"); line(320, 380, 640, 380, SAND, 2, cap="butt")
xs = [310, 450, 590, 730, 790]
xs = [300, 440, 580, 700, 790]
J = [("2 Torre_Base", "Revolute · eje Y", "−170° a 170°", False, True),
     ("3 Brazo_Principal", "Revolute · eje X", "−15° a 75°", True, False),
     ("4 Brazo_Secundario", "Revolute · eje X", "−125° a 15°", False, True),
     ("5 Tramo_Telescopico", "Prismatic · eje Z", "0 a 2.5 m", True, False)]
for x, (n, a, b, below, _) in zip(xs, J):
    pass
# posiciones/lado: J2 abajo, J3 arriba, J4 abajo, J5 arriba (prismatica con flecha dentro) , J6 abajo
revolute(260, YC); label(260, YC, "2 Torre_Base", "Revolute · eje Y", "−170° a 170°", above=True)
revolute(390, YC); label(390, YC, "3 Brazo_Principal", "Revolute · eje X", "−15° a 75°", above=False)
revolute(520, YC); label(520, YC, "4 Brazo_Secundario", "Revolute · eje X", "−125° a 15°", above=True)
prismatic(650, YC, arrow_y=-30)
label(650, YC, "5 Tramo_Telescopico", "Prismatic · eje Z", "0 a 2.5 m", above=False)
spherical(790, YC)
text(785, YC - 98, "6 Cabezal_Pinza", 21, "bold", "Bahnschrift", OTX)
text(785, YC - 74, "PASIVO · Spherical", 17, "normal", "Consolas", OTX)
text(785, YC - 50, "swing Y/Z ±40°", 17, "normal", "Consolas", OTX)
line(790, YC - 40, 790, YC - 26, GREY, 1.5, "3 3", "butt")
# dedos
for yy, n, b in ((120, "7 Dedo_Izquierdo", "0 a 0.35 m"), (260, "8 Dedo_Derecho", "−0.35 a 0 m")):
    prismatic(968, yy, arrow_y=-26)
    text(1022, yy - 20, n, 21, "bold", "Bahnschrift", INK, "start")
    text(1022, yy + 4, "Prismatic · eje X", 17, "normal", "Consolas", INK, "start")
    text(1022, yy + 28, b, 17, "normal", "Consolas", INK, "start")
# volquete
revolute(360, YB); label(360, YB, "9 Caja_Volcadora", "Revolute · eje X", "0° a 55°", above=True)
revolute(540, YB, True); label(540, YB, "10 Compuerta_Trasera", "PASIVA · Revolute · eje X", "0° a 95°", above=False, passive=True)
# leyenda
lx, ly = 700, 385
o.append(f'<rect x="{lx}" y="{ly}" width="480" height="285" rx="4" fill="#efe7d3" stroke="{INK}" stroke-width="2"/>')
text(lx + 18, ly + 30, "LEYENDA", 18, "bold", "Bahnschrift", OTX, "start")
entries = [("rev", "Revolute: giro en torno al eje"), ("pri", "Prismatic: desliza a lo largo del eje"),
           ("sph", "Spherical: swing limitado, twist fijo"), ("pas", "Punteado naranja: pasivo, sin drive"),
           ("gnd", "Raíz fija (immovable)")]
yy = ly + 62
for k, t in entries:
    cx = lx + 40
    if k == "rev": revolute(cx, yy)
    elif k == "pri":
        o.append(f'<rect x="{cx-24}" y="{yy-10}" width="48" height="20" rx="2" fill="{PAPER}" stroke="{TEAL}" stroke-width="3"/><rect x="{cx-5}" y="{yy-5}" width="26" height="10" fill="{TEAL}"/>')
    elif k == "sph": spherical(cx, yy)
    elif k == "pas": line(cx - 24, yy, cx + 24, yy, ORANGE, 4, "7 5", "butt")
    else: ground(cx + 10, yy, 0.4)
    text(lx + 78, yy + 6, t, 16.5, "normal", "Consolas", INK, "start")
    yy += 36
text(lx + 18, ly + 252, "Ejes: X derecha, Y arriba, Z adelante.", 15.5, "normal", "Consolas", GREY, "start")
text(lx + 18, ly + 274, "Grados o metros, relativos al padre.", 15.5, "normal", "Consolas", GREY, "start")

svg = (f'<?xml version="1.0" encoding="UTF-8"?>\n<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}">'
       f'<rect width="{W}" height="{H}" fill="{PAPER}"/>' + "".join(o) + "</svg>")
here = os.path.dirname(os.path.abspath(__file__))
sp = os.path.join(here, "esquema_cinematico.svg")
open(sp, "w", encoding="utf-8").write(svg)
subprocess.run([r"C:/Program Files/Google/Chrome/Application/chrome.exe", "--headless", "--disable-gpu", "--hide-scrollbars",
                "--force-device-scale-factor=2.5", f"--window-size={W},{H}", "--screenshot=" + os.path.join(here, "esquema_cinematico.png"),
                "file:///" + sp.replace("\\", "/")], capture_output=True)
print("ok")
