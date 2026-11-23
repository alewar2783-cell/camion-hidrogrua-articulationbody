# Genera Documento_Entrega.docx (python-docx) y Documento_Entrega.pdf (HTML -> Chrome headless) desde el mismo contenido.
# Orden: python make_diagram.py ; python make_figure.py ; python build_documento.py ; python render_pages.py <dir>
import os, re, html, subprocess
from docx import Document
from docx.shared import Pt, Cm, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_TAB_ALIGNMENT
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.abspath(os.path.join(HERE, ".."))
CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe"
INK, TEAL, ORANGE, OTX = "12332F", "0F6B5F", "E8590C", "B4440A"
SAND, SANDL, PAPER, GREY = "D9C9A0", "EFE7D3", "F7F3EA", "55615C"
HEAD, BODYF, MONO = "Bahnschrift", "Segoe UI", "Consolas"
BODY = 9
TW = 18.4  # ancho util en cm (A4 menos 2 x 1.3 cm)
FECHA, REV, UNITY = "2026-11-23", "A", "Unity 6000.3.14f1"
TITLE = "Simulador de camión volcador con hidrogrúa (Articulation Body)"
SUB = "Simuladores · Licenciatura en Desarrollo de Videojuegos y Simulaciones · 2.º año"
SHEET_TITLE = "Camión volcador con hidrogrúa · Ficha de ingeniería"
NSHEETS = 3
CAJETIN = [[("Nombre y apellido", "COMPLETAR"), ("Hoja", "1/%d" % NSHEETS)],
           [("Repositorio", "COMPLETAR"), ("Fecha", FECHA)],
           [("Build (itch.io)", "COMPLETAR"), ("Motor", UNITY)],
           [("Video (YouTube)", "COMPLETAR"), ("Rev. / Escala", REV + " · s/e")]]
FICHA = [("10", "cuerpos"), ("3", "tipos de joint"), ("7", "drives"), ("6", "comandos"), ("2", "pasivos"),
         ("1640 kg", "masa total"), ("55°", "volteo máx. de la caja"), ("5 × 8 kg", "cubos de carga")]

# ------------------------------------------------------------------ contenido
A_TEXT = ("Modelamos un **camión volcador con hidrogrúa**: una grúa articulada con pinza de dos dedos montada detrás de la cabina. "
          "La operación tiene dos tiempos. En la **carga**, la grúa levanta del suelo cubos de 8 kg y los deja en la caja; el cubo se sostiene solo por fricción, no está atado al cabezal. "
          "En la **descarga** plegamos la grúa, inclinamos la caja hasta 55° y las cargas, al deslizar, empujan la compuerta trasera hasta abrirla y caen al corralón; al bajar la caja la compuerta se cierra sola. "
          "Elegimos esta máquina porque reúne en un único árbol los tres tipos de joint, una cadena larga que soporta peso real y dos uniones pasivas gobernadas por contacto.")
B_TEXT = ("Diez cuerpos en un único árbol. `Chasis_Root` es la raíz fija y de él cuelgan dos ramas: la **grúa** (torre, pluma, brazo secundario, telescópico, cabezal y dos dedos) y el **volquete** (caja y compuerta). "
          "Los ejes y límites están en el sistema local de cada unión; los ángulos van en grados y los desplazamientos en metros, relativos al cuerpo padre.")
C_TABLE = dict(head=["N.º", "Cuerpo", "Joint", "m (kg)", "Stiffness", "Damping", "Force limit"],
               rows=[["01", "Chasis_Root", "raíz fija", "1000", "—", "—", "—"],
                     ["02", "Torre_Base", "Revolute", "150", "60 000", "6 000", "120 000"],
                     ["03", "Brazo_Principal", "Revolute", "100", "90 000", "9 000", "180 000"],
                     ["04", "Brazo_Secundario", "Revolute", "60", "65 000", "6 500", "110 000"],
                     ["05", "Tramo_Telescopico", "Prismatic", "35", "45 000", "4 500", "60 000"],
                     ["06", "Cabezal_Pinza · pasivo", "Spherical", "15", "0", "25", "ilimitado"],
                     ["07", "Dedo_Izquierdo", "Prismatic", "5", "25 000", "2 500", "**300**"],
                     ["08", "Dedo_Derecho", "Prismatic", "5", "25 000", "2 500", "**300**"],
                     ["09", "Caja_Volcadora", "Revolute", "250", "120 000", "12 000", "250 000"],
                     ["10", "Compuerta_Trasera · pasiva", "Revolute", "20", "0", "10", "ilimitado"]],
               widths=[1.0, 5.4, 2.6, 1.8, 2.6, 2.4, 2.6], aligns="lllrrrr", mono="mtmmmmm", hl={5, 9})
C_NOTE = ("Stiffness y damping son por radián en las uniones angulares y por metro en las lineales. Hay 7 uniones accionadas pero solo **6 comandos**: los dos dedos comparten el de pinza, con target +t en el izquierdo y −t en el derecho. Cabezal y compuerta no tienen drive.")
D_TEXT = ("Teclado y sliders gobiernan los mismos seis ejes. Cada eje guarda un **Target**: la tecla lo desplaza a velocidad constante (`Target = Clamp(Target + eje · velocidad · Δt)`), el slider lo escribe al arrastrarlo y cada uno refleja al otro; dos teclas opuestas a la vez se anulan. "
          "En `FixedUpdate` el Target pasa a `drive.target`: nunca movemos un `ArticulationBody` por `transform`. Junto al Target el panel lee el valor **Real** medido en la articulación, y la diferencia es el hundimiento por peso (por ejemplo `Pluma principal | Target: 70.0° | Real: 67.9°`).")
D_KEYS = dict(head=["Eje", "Tecla −", "Tecla +", "Rango", "Velocidad"],
              rows=[["Torre", "A", "D", "−170° a 170°", "45 °/s"],
                    ["Pluma principal", "S", "W", "−15° a 75°", "30 °/s"],
                    ["Brazo secundario", "F", "R", "−125° a 15°", "35 °/s"],
                    ["Telescópico", "G", "T", "0 a 2.5 m", "0.8 m/s"],
                    ["Pinza (por dedo)", "Q  abrir", "E  cerrar", "0 a 0.35 m", "0.4 m/s"],
                    ["Caja volcadora", "↓ · Z", "↑ · X", "0° a 55°", "20 °/s"]],
              widths=[4.4, 3.0, 3.0, 4.2, 3.8], aligns="lccrr", mono="tmmmm", hl=set())
D_NOTE = ("Reinicio con `Retroceso`, `P` o el botón *Reiniciar Escena*. Cámara: botón derecho y arrastre para orbitar, rueda para acercar. En el navegador hay que hacer click sobre el canvas para que el teclado llegue al juego.")
E_TEXT = ("Partimos de masas que **bajan de la raíz a la punta** (1000, 150, 100, 60, 35, 15 y 5 kg): una cadena con masas parecidas, o con un eslabón liviano colgado de uno pesado, oscila y le cuesta al solver, sobre todo en WebGL. "
          "Después revisamos cada unión de la raíz a la punta en el peor caso (pluma y secundario horizontales, telescópico en 2.5 m) con tres cuentas: el **torque estático** τ = Σ m·g·brazo, que debe caber en el force limit; el **hundimiento** τ / stiffness, con tolerancia de 6° o 0.08 m en los tests; y la **razón de amortiguamiento** ζ = c / (2·√(k·I)). "
          "El solver corre con 16 iteraciones de posición, 4 de velocidad y `Fixed Timestep` de 0.02 s.")
E_TABLE = dict(head=["Peor caso", "Torre", "Pluma", "Secundario", "Telescópico", "Caja", "Dedos"],
               rows=[["Torque estático", "sin carga", "8000 N·m", "3000 N·m", "590 N", "4900 N·m", "sin carga"],
                     ["Hundimiento τ / k", "n/a", "5.1°", "2.7°", "0.013 m", "2.4°", "n/a"],
                     ["Razón ζ", "0.19", "0.23", "0.37", "1.4", "0.51", "3.5"]],
               widths=[3.8, 2.0, 2.2, 2.6, 2.8, 2.6, 2.4], aligns="lrrrrrr", mono="tmmmmmm", hl=set())
E_BUL = ["**Margen.** El torque ocupa entre 1 % y 4 % del force limit, así que nunca satura. Medido al 50 % del rango: la pluma queda en 28.7° para 30° pedidos y el telescópico en 1.26 m para 1.25 m.",
         "**Dedos.** Heredaban un force limit de 20 000 N: con target 0.35 m contra un cubo de 0.5 m, los dedos lo penetraban 82 mm. Con el cubo agarrado y elevado, la interferencia desaparece desde 500 N; fijamos **300 N**, porque el cubo pesa 78 N y la fricción de las dos caras ofrece unos 486 N.",
         "**Fricción.** Goma de la pinza 0.9 / 0.95 con combine *Multiply* (con *Maximum* la goma ganaba en todo par y los cubos no resbalaban en la caja antes de unos 40°); acero de la caja 0.25 / 0.3, donde los cubos resbalan a unos 27.5°; suelo 0.1 / 0.1 (`Mat_Suelo`), para que las cargas caídas se deslicen, despejen la zona y la compuerta pueda volver a cerrar.",
         "**Pasivos.** Con stiffness 0 no hay drive. El cabezal cuelga por gravedad dentro de un cono de ±40° y su damping de 25 frena el péndulo. La compuerta (damping 10) la abren el peso y el empuje de las cargas, hasta 95° como máximo; observamos unos 72° al volcar, y al bajar la caja vuelve a menos de 5°."]
F_TEXT = ("Con los seis ejes se recorre la maniobra completa: tomar un cubo con la pinza solo por fricción, levantarlo, depositarlo en la caja y volcarla para que la carga abra la compuerta y caiga al corralón.")
REF_TEXT = ("Valores: `Assets/Scripts/Runtime/TruckSpec.cs` (fuente única de masas, ejes, límites y drives). Método y mediciones: `docs/CALIBRACION.md` (secciones 5, 7 y 8).")

SHEETS = [
    [("band",), ("top",), ("sec", "B", "Cadena cinemática"), ("p", B_TEXT),
     ("fig", "esquema_cinematico.png", "Lámina 1", "Esquema cinemático: unión, tipo de joint, eje y límites (valores de `TruckSpec`). Trazo punteado naranja: unión pasiva.")],
    [("sec", "C", "Cuerpos y accionamientos"), ("tb", C_TABLE), ("p", C_NOTE),
     ("sec", "D", "Mando y lectura de estado"), ("p", D_TEXT), ("tb", D_KEYS), ("p", D_NOTE),
     ("fig", "vistas.png", "Vistas", "V1 panel de mando con Target y Real por eje · V2 carga depositada sobre la caja · V3 volcado a 55° con la compuerta abierta.")],
    [("sec", "E", "Elección de los drives"), ("p", E_TEXT), ("tb", E_TABLE), ("ul", E_BUL),
     ("sec", "F", "Prueba física"), ("p", F_TEXT), ("ref", REF_TEXT)],
]

# ------------------------------------------------------------------ utilidades de markup
PAT = re.compile(r"(\*\*.+?\*\*|`.+?`|\*.+?\*)")
def runs(t):
    out = []
    for part in PAT.split(t):
        if not part: continue
        if part.startswith("**"): out.append((part[2:-2], "b"))
        elif part.startswith("`"): out.append((part[1:-1], "c"))
        elif part.startswith("*"): out.append((part[1:-1], "i"))
        else: out.append((part, ""))
    return out
def hrun(t):
    o = ""
    for txt, k in runs(t):
        e = html.escape(txt)
        o += {"b": "<b>%s</b>", "c": "<code>%s</code>", "i": "<i>%s</i>", "": "%s"}[k] % e
    return o

# ------------------------------------------------------------------ HTML / PDF
CSS = f"""@page{{size:A4;margin:0}}
*{{box-sizing:border-box;-webkit-print-color-adjust:exact;print-color-adjust:exact}}
html,body{{margin:0;padding:0;background:#{PAPER}}}
body{{font-family:'{BODYF}',sans-serif;font-size:{BODY}pt;line-height:1.38;color:#{INK}}}
.sheet{{width:210mm;height:296.6mm;position:relative;background:#{PAPER};overflow:hidden;page-break-after:always;break-after:page}}
.sheet:last-child{{page-break-after:auto;break-after:auto}}
.frame{{position:absolute;inset:7mm;border:.8pt solid #{INK}}}
.frame2{{position:absolute;inset:8.2mm;border:.3pt solid #{INK}}}
.reg{{position:absolute;width:5mm;height:5mm}} .reg:before,.reg:after{{content:'';position:absolute;background:#{INK}}}
.reg:before{{left:2.5mm;top:0;width:.3pt;height:5mm}} .reg:after{{top:2.5mm;left:0;height:.3pt;width:5mm}}
.content{{position:absolute;left:13mm;right:13mm;top:12mm;bottom:17mm}}
.foot{{position:absolute;left:13mm;right:13mm;bottom:9.5mm;display:flex;justify-content:space-between;font-family:'{HEAD}';font-size:7pt;letter-spacing:.06em;text-transform:uppercase;color:#{GREY};border-top:.4pt solid #{SAND};padding-top:1.2mm}}
.runh{{display:flex;justify-content:space-between;font-family:'{HEAD}';font-size:7pt;letter-spacing:.06em;text-transform:uppercase;color:#{GREY};border-bottom:.4pt solid #{SAND};padding-bottom:1mm;margin-bottom:3mm}}
.band{{background-color:#{PAPER};background-image:linear-gradient(rgba(18,51,47,.16) .5px,transparent .5px),linear-gradient(90deg,rgba(18,51,47,.16) .5px,transparent .5px),linear-gradient(rgba(18,51,47,.30) .6px,transparent .6px),linear-gradient(90deg,rgba(18,51,47,.30) .6px,transparent .6px);
 background-size:5mm 5mm,5mm 5mm,25mm 25mm,25mm 25mm;border:.7pt solid #{INK};border-bottom:none;padding:4.5mm 6mm 4mm}}
.kick{{font-family:'{HEAD}';font-size:7.5pt;letter-spacing:.22em;text-transform:uppercase;color:#{OTX}}}
h1{{font-family:'{HEAD}';font-weight:600;font-size:19pt;line-height:1.12;margin:1mm 0 1.5mm;color:#{INK};letter-spacing:.005em}}
h1 span{{font-weight:300;color:#{TEAL}}}
.sub{{font-size:8.5pt;color:#{GREY}}}
table.cj{{width:100%;border-collapse:collapse;border:.7pt solid #{INK};margin:0 0 3.5mm;background:#{PAPER}}}
.cj td{{border:.5pt solid #{INK};padding:1mm 2.2mm;vertical-align:middle}}
.cj td.l{{font-family:'{HEAD}';font-size:6.8pt;letter-spacing:.09em;text-transform:uppercase;color:#{GREY};background:#{SANDL};width:27mm}}
.cj td.v{{font-family:'{MONO}';font-size:9pt;font-weight:bold;width:63mm}} .cj td.todo{{color:#{OTX}}}
.top{{display:flex;gap:4mm;margin-bottom:1mm}}
.ficha{{width:50mm;flex:none;border-right:.7pt solid #{INK};padding-right:3mm}}
.ficha .k{{font-family:'{HEAD}';font-size:7.5pt;letter-spacing:.14em;text-transform:uppercase;color:#{OTX};margin-bottom:1.2mm}}
.fg{{display:grid;grid-template-columns:1fr 1fr;column-gap:2mm}}
.ficha .n{{font-family:'{HEAD}';font-weight:600;font-size:15pt;line-height:1.05;color:#{TEAL}}}
.ficha .u{{font-size:7.2pt;color:#{GREY};margin-bottom:1.8mm;line-height:1.15}}
.sec{{display:flex;align-items:baseline;gap:2.4mm;margin:3.2mm 0 1.6mm;border-bottom:.6pt solid #{INK};padding-bottom:.7mm;break-after:avoid}}
.sec .L{{font-family:'{HEAD}';font-weight:700;font-size:13pt;color:#{OTX};line-height:1}}
.sec .T{{font-family:'{HEAD}';font-weight:600;font-size:9.5pt;letter-spacing:.13em;text-transform:uppercase;color:#{INK}}}
p{{margin:0 0 2mm}} code{{font-family:'{MONO}';font-size:8.4pt;color:#{TEAL}}}
ul{{list-style:none;margin:0 0 1mm;padding:0}} li{{position:relative;padding-left:4mm;margin:0 0 1.6mm}} li:before{{content:'';position:absolute;left:.4mm;top:1.5mm;width:1.6mm;height:1.6mm;background:#{ORANGE}}}
table.t{{width:100%;border-collapse:collapse;margin:1mm 0 2mm;border-top:1.1pt solid #{INK};border-bottom:1.1pt solid #{INK}}}
.t th{{font-family:'{HEAD}';font-weight:600;font-size:7.2pt;letter-spacing:.1em;text-transform:uppercase;color:#{GREY};border-bottom:.8pt solid #{INK};padding:1.2mm 2mm;text-align:inherit}}
.t td{{padding:.9mm 2mm;border-bottom:.4pt solid #{SAND};font-size:8.6pt}} .t tr:last-child td{{border-bottom:none}}
.t td.m{{font-family:'{MONO}';font-size:8.6pt}} .t tr.hl td{{color:#{OTX}}}
figure{{margin:2mm 0 2mm;padding:0}} figure .img{{border:.8pt solid #{INK};padding:1.6mm;background:#{PAPER}}} figure img{{display:block;width:100%}}
figcaption{{font-size:7.8pt;color:#{GREY};margin-top:1.2mm;line-height:1.3}} figcaption b{{font-family:'{HEAD}';font-weight:600;letter-spacing:.12em;text-transform:uppercase;color:#{OTX};font-size:7.2pt;margin-right:1.5mm}}
.ref{{margin-top:3mm;border-top:.6pt solid #{INK};padding-top:1.5mm;font-size:8pt;color:#{GREY}}} .ref b{{font-family:'{HEAD}';letter-spacing:.14em;text-transform:uppercase;color:#{OTX};font-size:7.5pt;margin-right:2mm}}"""

def h_table(sp):
    al = {"l": "left", "c": "center", "r": "right"}; w = sum(sp["widths"])
    o = ["<table class='t'><tr>"]
    for i, x in enumerate(sp["head"]):
        o.append("<th style='width:%.1f%%;text-align:%s'>%s</th>" % (100 * sp["widths"][i] / w, al[sp["aligns"][i]], html.escape(x)))
    o.append("</tr>")
    for ri, row in enumerate(sp["rows"]):
        o.append("<tr class='%s'>" % ("hl" if ri in sp["hl"] else ""))
        for ci, v in enumerate(row):
            o.append("<td class='%s' style='text-align:%s'>%s</td>" % ("m" if sp["mono"][ci] == "m" else "", al[sp["aligns"][ci]], hrun(v)))
        o.append("</tr>")
    return "".join(o) + "</table>"

def h_cajetin(n):
    o = ["<table class='cj'>"]
    for row in CAJETIN:
        o.append("<tr>")
        for k, v in row:
            if k == "Hoja": v = "%d/%d" % (n, NSHEETS)
            o.append("<td class='l'>%s</td><td class='v%s'>%s</td>" % (html.escape(k), " todo" if v == "COMPLETAR" else "", html.escape(v)))
        o.append("</tr>")
    return "".join(o) + "</table>"

def h_block(b, n):
    k = b[0]
    if k == "band":
        return ("<div class='band'><div class='kick'>Ficha de ingeniería · mecanismo articulado</div><h1>Simulador de camión volcador con hidrogrúa <span>(Articulation Body)</span></h1>"
                "<div class='sub'>%s</div></div>" % html.escape(SUB)) + h_cajetin(n)
    if k == "top":
        f = "<div class='fg'>" + "".join("<div><div class='n'>%s</div><div class='u'>%s</div></div>" % (html.escape(a), html.escape(c)) for a, c in FICHA) + "</div>"
        return ("<div class='top'><div class='ficha'><div class='k'>Ficha técnica</div>%s</div><div style='flex:1'>"
                "<div class='sec' style='margin-top:0'><span class='L'>A</span><span class='T'>Máquina y operación</span></div><p>%s</p></div></div>" % (f, hrun(A_TEXT)))
    if k == "sec": return "<div class='sec'><span class='L'>%s</span><span class='T'>%s</span></div>" % (b[1], html.escape(b[2]))
    if k == "p": return "<p>%s</p>" % hrun(b[1])
    if k == "ul": return "<ul>" + "".join("<li>%s</li>" % hrun(i) for i in b[1]) + "</ul>"
    if k == "tb": return h_table(b[1])
    if k == "fig": return "<figure><div class='img'><img src='%s'></div><figcaption><b>%s</b>%s</figcaption></figure>" % (b[1], html.escape(b[2]), hrun(b[3]))
    if k == "ref": return "<div class='ref'><b>Referencias</b>%s</div>" % hrun(b[1])

def build_html(path):
    o = ["<!doctype html><html lang='es'><head><meta charset='utf-8'><title>%s</title><style>%s</style></head><body>" % (html.escape(TITLE), CSS)]
    for n, blocks in enumerate(SHEETS, 1):
        o.append("<div class='sheet'><div class='frame'></div><div class='frame2'></div>")
        for pos in ("left:2mm;top:2mm", "right:2mm;top:2mm", "left:2mm;bottom:2mm", "right:2mm;bottom:2mm"):
            o.append("<div class='reg' style='%s'></div>" % pos)
        o.append("<div class='content'>")
        if n > 1: o.append("<div class='runh'><span>%s</span><span>Hoja %d/%d</span></div>" % (html.escape(SHEET_TITLE), n, NSHEETS))
        o += [h_block(b, n) for b in blocks]
        o.append("</div><div class='foot'><span>%s · Rev. %s · %s</span><span>Hoja %d/%d</span></div></div>" % (UNITY, REV, FECHA, n, NSHEETS))
    o.append("</body></html>")
    open(path, "w", encoding="utf-8").write("".join(o))

# ------------------------------------------------------------------ DOCX
ORD = {
 "pPr": "pStyle keepNext keepLines pageBreakBefore framePr widowControl numPr suppressLineNumbers pBdr shd tabs suppressAutoHyphens kinsoku wordWrap overflowPunct topLinePunct autoSpaceDE autoSpaceDN bidi adjustRightInd snapToGrid spacing ind contextualSpacing mirrorIndents suppressOverlap jc textDirection textAlignment textboxTightWrap outlineLvl divId cnfStyle rPr sectPr pPrChange".split(),
 "tcPr": "cnfStyle tcW gridSpan hMerge vMerge tcBorders shd noWrap tcMar textDirection tcFitText vAlign hideMark".split(),
 "tblPr": "tblStyle tblpPr tblOverlap bidiVisual tblStyleRowBandSize tblStyleColBandSize tblW jc tblCellSpacing tblInd tblBorders shd tblLayout tblCellMar tblLook".split(),
 "rPr": "rStyle rFonts b bCs i iCs caps smallCaps strike dstrike outline shadow emboss imprint noProof snapToGrid vanish webHidden color spacing w kern position sz szCs highlight u effect bdr shd fitText vertAlign rtl cs em lang eastAsianLayout specVanish oMath".split(),
}
def local(el): return el.tag.split("}")[1]
def put(parent, child, kind):
    order = ORD[kind]; idx = order.index(local(child))
    for old in parent.findall(child.tag): parent.remove(old)
    for i, sib in enumerate(list(parent)):
        if local(sib) in order and order.index(local(sib)) > idx:
            parent.insert(i, child); return child
    parent.append(child); return child
def el(tag, **attrs):
    e = OxmlElement("w:" + tag)
    for k, v in attrs.items(): e.set(qn("w:" + k), str(v))
    return e
def borders(tag, spec):  # spec: {side: (sz, color)}
    b = el(tag)
    for side in ("top", "left", "bottom", "right", "insideH", "insideV"):
        if side in spec:
            sz, c = spec[side]; b.append(el(side, val="single", sz=sz, space=0, color=c) if sz else el(side, val="nil"))
        elif tag == "tblBorders":
            b.append(el(side, val="nil"))
    return b
def shade(cell, fill):
    put(cell._tc.get_or_add_tcPr(), el("shd", val="clear", color="auto", fill=fill), "tcPr")
def cell_borders(cell, spec): put(cell._tc.get_or_add_tcPr(), borders("tcBorders", spec), "tcPr")
def tbl_borders(tbl, spec): put(tbl._tbl.tblPr, borders("tblBorders", spec), "tblPr")
def tbl_margins(tbl, top=30, bottom=30, left=70, right=70):
    m = el("tblCellMar")
    for k, v in (("top", top), ("left", left), ("bottom", bottom), ("right", right)): m.append(el(k, w=v, type="dxa"))
    put(tbl._tbl.tblPr, m, "tblPr")
def fixed(tbl): put(tbl._tbl.tblPr, el("tblLayout", type="fixed"), "tblPr")
def pborder(par, spec):
    put(par._p.get_or_add_pPr(), borders("pBdr", spec), "pPr")
def rfmt(r, font=BODYF, size=BODY, color=INK, bold=False, caps=False, spacing=0, italic=False):
    r.font.name = font; r.font.size = Pt(size); r.font.color.rgb = RGBColor.from_string(color); r.bold = bold or None; r.italic = italic or None
    rf = r._element.get_or_add_rPr().find(qn("w:rFonts"))
    for a in ("ascii", "hAnsi", "cs", "eastAsia"): rf.set(qn("w:" + a), font)
    if caps: r.font.all_caps = True
    if spacing: put(r._element.get_or_add_rPr(), el("spacing", val=spacing), "rPr")
def add_runs(par, t, size=BODY, color=INK, bold=False, mono_color=TEAL):
    for txt, k in runs(t):
        r = par.add_run(txt)
        if k == "c": rfmt(r, MONO, size - 0.6, mono_color)
        else: rfmt(r, BODYF, size, color, bold=bold or k == "b", italic=k == "i")
def spacing(par, before=0, after=3, line=1.18):
    f = par.paragraph_format; f.space_before = Pt(before); f.space_after = Pt(after); f.line_spacing = line
def label(par, text, size=7.5, color=GREY, spacing_=20, bold=False):
    r = par.add_run(text); rfmt(r, HEAD, size, color, bold=bold, caps=True, spacing=spacing_); return r
def field(par, instr, size=7, color=GREY):
    for typ, txt in (("begin", None), (None, instr), ("separate", None), (None, "1"), ("end", None)):
        r = par.add_run(); rfmt(r, HEAD, size, color, caps=True)
        if typ: r._r.append(el("fldChar", fldCharType=typ))
        elif txt == instr:
            it = el("instrText"); it.set(qn("xml:space"), "preserve"); it.text = " %s " % instr; r._r.append(it)
        else: r.text = txt

def d_section(d, letter, title, container=None):
    par = (container or d).add_paragraph(); spacing(par, 7, 3); par.paragraph_format.keep_with_next = True
    r = par.add_run(letter + "   "); rfmt(r, HEAD, 13, OTX, bold=True)
    label(par, title, 9.5, INK, 26, bold=True)
    pborder(par, {"bottom": (6, INK)})
    return par

def d_table(d, sp):
    w = sp["widths"]; k = TW / sum(w); w = [x * k for x in w]
    tbl = d.add_table(rows=1 + len(sp["rows"]), cols=len(w)); tbl.alignment = WD_TABLE_ALIGNMENT.CENTER; tbl.autofit = False
    fixed(tbl); tbl_margins(tbl, 25, 25, 60, 60)
    tbl_borders(tbl, {"top": (9, INK), "bottom": (9, INK)})
    for ri, row in enumerate([sp["head"]] + sp["rows"]):
        trPr = tbl.rows[ri]._tr.get_or_add_trPr(); trPr.append(el("cantSplit"))
        for ci, v in enumerate(row):
            c = tbl.cell(ri, ci); c.width = Cm(w[ci]); par = c.paragraphs[0]; spacing(par, 0, 0, 1.0)
            par.alignment = {"l": WD_ALIGN_PARAGRAPH.LEFT, "c": WD_ALIGN_PARAGRAPH.CENTER, "r": WD_ALIGN_PARAGRAPH.RIGHT}[sp["aligns"][ci]]
            if ri == 0:
                label(par, v, 7, GREY, 14, bold=True); cell_borders(c, {"bottom": (6, INK)})
            else:
                hl = (ri - 1) in sp["hl"]; col = OTX if hl else INK
                if sp["mono"][ci] == "m":
                    for txt, kk in runs(v): rfmt(par.add_run(txt), MONO, 8.4, col, bold=kk == "b")
                else:
                    for txt, kk in runs(v): rfmt(par.add_run(txt), BODYF, 8.6, col, bold=kk == "b")
                if ri < len(sp["rows"]): cell_borders(c, {"bottom": (3, SAND)})
    sp_ = d.add_paragraph(); spacing(sp_, 0, 0, 0.5)

def d_picture(d, path, caption_label, caption):
    par = d.add_paragraph(); par.alignment = WD_ALIGN_PARAGRAPH.CENTER; spacing(par, 3, 1, 1.0)
    pborder(par, {"top": (6, INK), "left": (6, INK), "bottom": (6, INK), "right": (6, INK)})
    par.paragraph_format.keep_with_next = True
    wcm = TW - 0.5
    par.add_run().add_picture(os.path.join(HERE, path), width=Cm(wcm))
    cp = d.add_paragraph(); spacing(cp, 1, 5, 1.1)
    label(cp, caption_label + "  ", 7.2, OTX, 24, bold=True); add_runs(cp, caption, 7.8, GREY)

def build_docx(path):
    d = Document(); s = d.sections[0]
    s.page_width, s.page_height = Cm(21), Cm(29.7)
    s.left_margin = s.right_margin = Cm(1.3); s.top_margin = Cm(1.9); s.bottom_margin = Cm(1.8)
    s.header_distance = Cm(1.0); s.footer_distance = Cm(0.95)
    pg = el("pgBorders", offsetFrom="page")
    for side in ("top", "left", "bottom", "right"): pg.append(el(side, val="single", sz=6, space=20, color=INK))
    pm = s._sectPr.find(qn("w:pgMar")); pm.addnext(pg)
    # fondo de papel
    d.element.insert(0, el("background", color=PAPER))
    st = d.settings.element; dbs = el("displayBackgroundShape")
    z = st.find(qn("w:zoom")); (z.addnext(dbs) if z is not None else st.insert(0, dbs))
    nm = d.styles["Normal"]; nm.font.name = BODYF; nm.font.size = Pt(BODY); nm.element.rPr.rFonts.set(qn("w:eastAsia"), BODYF)
    # cabecera y pie con numeracion de hoja
    hp = s.header.paragraphs[0]; hp.paragraph_format.tab_stops.add_tab_stop(Cm(TW), WD_TAB_ALIGNMENT.RIGHT)
    label(hp, SHEET_TITLE + "\t" + "Hoja ", 7, GREY, 14); field(hp, "PAGE"); label(hp, "/", 7, GREY, 14); field(hp, "NUMPAGES")
    pborder(hp, {"bottom": (3, SAND)})
    fp = s.footer.paragraphs[0]; fp.paragraph_format.tab_stops.add_tab_stop(Cm(TW), WD_TAB_ALIGNMENT.RIGHT)
    label(fp, "%s · Rev. %s · %s\tFuente: TruckSpec.cs" % (UNITY, REV, FECHA), 7, GREY, 14); pborder(fp, {"top": (3, SAND)})
    for n, blocks in enumerate(SHEETS, 1):
        for bi, b in enumerate(blocks):
            k = b[0]
            if k == "band":
                bt = d.add_table(rows=1, cols=1); fixed(bt); tbl_margins(bt, 90, 90, 160, 160); bt.autofit = False
                tbl_borders(bt, {"top": (6, INK), "left": (6, INK), "right": (6, INK), "bottom": (0, INK)})
                c = bt.cell(0, 0); c.width = Cm(TW); shade(c, SANDL)
                p0 = c.paragraphs[0]; spacing(p0, 0, 1); label(p0, "Ficha de ingeniería · mecanismo articulado", 7.5, OTX, 40)
                p1 = c.add_paragraph(); spacing(p1, 0, 1, 1.0)
                r = p1.add_run("Simulador de camión volcador con hidrogrúa "); rfmt(r, HEAD, 18, INK, bold=True)
                r = p1.add_run("(Articulation Body)"); rfmt(r, HEAD, 18, TEAL)
                p2 = c.add_paragraph(); spacing(p2, 0, 0); add_runs(p2, SUB, 8.5, GREY)
                ct = d.add_table(rows=4, cols=4); fixed(ct); ct.autofit = False; tbl_margins(ct, 40, 40, 80, 80)
                tbl_borders(ct, {k2: (6, INK) for k2 in ("top", "left", "bottom", "right", "insideH", "insideV")})
                cw = [3.0, 6.2, 3.0, 6.2]; cw = [x * TW / sum(cw) for x in cw]
                for ri, row in enumerate(CAJETIN):
                    for pi, (kk, v) in enumerate(row):
                        lc, vc = ct.cell(ri, pi * 2), ct.cell(ri, pi * 2 + 1)
                        lc.width, vc.width = Cm(cw[pi * 2]), Cm(cw[pi * 2 + 1]); shade(lc, SANDL)
                        pl = lc.paragraphs[0]; spacing(pl, 0, 0, 1.0); label(pl, kk, 6.8, GREY, 14)
                        pv = vc.paragraphs[0]; spacing(pv, 0, 0, 1.0)
                        rfmt(pv.add_run(v), MONO, 9, OTX if v == "COMPLETAR" else INK, bold=True)
                sp_ = d.add_paragraph(); spacing(sp_, 0, 0, 0.6)
            elif k == "top":
                lt = d.add_table(rows=1, cols=2); fixed(lt); lt.autofit = False; tbl_margins(lt, 0, 0, 0, 90)
                tbl_borders(lt, {})
                l, rgt = lt.cell(0, 0), lt.cell(0, 1); l.width, rgt.width = Cm(5.0), Cm(TW - 5.0)
                cell_borders(l, {"right": (6, INK)})
                mm = el("tcMar"); mm.append(el("left", w=200, type="dxa")); put(rgt._tc.get_or_add_tcPr(), mm, "tcPr")
                p0 = l.paragraphs[0]; spacing(p0, 0, 4); label(p0, "Ficha técnica", 7.5, OTX, 30)
                nt = l.add_table(rows=4, cols=2); fixed(nt); nt.autofit = False; tbl_borders(nt, {}); tbl_margins(nt, 0, 0, 0, 40)
                for i, (a, cc) in enumerate(FICHA):
                    c2 = nt.cell(i // 2, i % 2); c2.width = Cm(2.3)
                    pn = c2.paragraphs[0]; spacing(pn, 0, 0, 1.0); rfmt(pn.add_run(a), HEAD, 14, TEAL, bold=True)
                    pu = c2.add_paragraph(); spacing(pu, 0, 5, 1.0); rfmt(pu.add_run(cc), BODYF, 7.2, GREY)
                hd = rgt.paragraphs[0]; spacing(hd, 0, 3); hd.paragraph_format.keep_with_next = True
                r = hd.add_run("A   "); rfmt(r, HEAD, 13, OTX, bold=True); label(hd, "Máquina y operación", 9.5, INK, 26, bold=True)
                pborder(hd, {"bottom": (6, INK)})
                pa = rgt.add_paragraph(); spacing(pa, 0, 0); add_runs(pa, A_TEXT)
            elif k == "sec": d_section(d, b[1], b[2])
            elif k == "p":
                par = d.add_paragraph(); add_runs(par, b[1]); spacing(par, 0, 4)
            elif k == "ul":
                for it in b[1]:
                    par = d.add_paragraph(); spacing(par, 0, 3); par.paragraph_format.left_indent = Cm(0.45); par.paragraph_format.first_line_indent = Cm(-0.45)
                    rfmt(par.add_run("■  "), BODYF, 6, ORANGE); add_runs(par, it)
            elif k == "tb": d_table(d, b[1])
            elif k == "fig": d_picture(d, b[1], b[2], b[3])
            elif k == "ref":
                par = d.add_paragraph(); spacing(par, 8, 0); pborder(par, {"top": (6, INK)})
                label(par, "Referencias   ", 7.5, OTX, 30, bold=True); add_runs(par, b[1], 8, GREY)
        if n < len(SHEETS):
            pb = d.add_paragraph(); pb.add_run().add_break(__import__("docx").enum.text.WD_BREAK.PAGE); spacing(pb, 0, 0, 0.1)
    # primer parrafo vacio inicial
    d.core_properties.title = TITLE; d.core_properties.author = ""
    d.save(path)

build_docx(os.path.join(OUT, "Documento_Entrega.docx"))
hp_ = os.path.join(HERE, "documento.html"); build_html(hp_)
subprocess.run([CHROME, "--headless", "--disable-gpu", "--no-pdf-header-footer", "--print-to-pdf=" + os.path.join(OUT, "Documento_Entrega.pdf"),
                "file:///" + hp_.replace("\\", "/")], check=True, capture_output=True)
print("ok")
