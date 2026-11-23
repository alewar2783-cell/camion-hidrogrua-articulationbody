# Genera Documento_Entrega.docx (python-docx) y Documento_Entrega.pdf (HTML -> Chrome headless), estilo simple.
# Orden: python make_arbol.py ; python build_documento.py ; python render_pages.py <dir>
import os, re, html, subprocess
from docx import Document
from docx.shared import Pt, Cm, RGBColor
from docx.oxml.ns import qn

HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.abspath(os.path.join(HERE, ".."))
CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe"
BLUE = "1F3A5F"; FONT = "Arial"; SIZE = 10.5
TITLE = "Simulador de camión volcador con hidrogrúa (Articulation Body)"
DATA = [("Nombre y apellido", "COMPLETAR"), ("Repositorio", "https://github.com/alewar2783-cell/camion-hidrogrua-articulationbody"), ("Build en itch.io", "COMPLETAR"), ("Video en YouTube", "COMPLETAR")]

# bloques: ("h", titulo) ("p", texto) ("ul", [items]) ("fig", png, caption) ("tb", head, rows)
DOC = [
 ("h", "1. La máquina"),
 ("p", "Modelamos un **camión volcador con hidrogrúa**: una grúa articulada con pinza de dos dedos montada detrás de la cabina. "
       "Primero la grúa levanta cubos de 8 kg del suelo y los deja en la caja. Después se pliega la grúa, se inclina la caja hasta 55° y las cargas empujan la compuerta trasera hasta abrirla y caen. Al bajar la caja, la compuerta se cierra sola."),
 ("h", "2. Árbol de joints"),
 ("p", "Son 10 `ArticulationBody` en un único árbol, con `Chasis_Root` como raíz fija. De ahí cuelgan dos ramas: la grúa y el volquete. Usamos los tres tipos de joint que pide la consigna: Revolute, Prismatic y Spherical."),
 ("fig", "arbol_joints.png", "Figura 1. Árbol de joints con tipo, eje y límites de cada unión."),
 ("hb", "3. Drives"),
 ("p", "Hay 7 uniones con drive y 6 comandos, porque los dos dedos comparten el comando de pinza. Los valores salen de `TruckSpec.cs`."),
 ("tb", ["Unión", "Stiffness", "Damping", "Force limit"],
        [["Torre_Base", "60 000", "6 000", "120 000"], ["Brazo_Principal", "90 000", "9 000", "180 000"],
         ["Brazo_Secundario", "65 000", "6 500", "110 000"], ["Tramo_Telescopico", "45 000", "4 500", "60 000"],
         ["Dedos (cada uno)", "25 000", "2 500", "300"], ["Caja_Volcadora", "120 000", "12 000", "250 000"]]),
 ("p", "Las masas bajan de la raíz a la punta (1000, 150, 100, 60, 35, 15 y 5 kg) para que el solver sea estable. El torque estático en el peor caso ocupa entre 1 % y 4 % del force limit, así que ningún drive satura."),
 ("h", "4. Target vs Real"),
 ("p", "Cada eje guarda un **Target**. La tecla lo mueve a velocidad constante y el slider lo escribe directamente. En `FixedUpdate` el Target pasa a `drive.target`; nunca movemos un cuerpo por `transform`. "
       "El panel muestra también el valor **Real** medido en la articulación. La diferencia es el hundimiento por peso, por ejemplo `Pluma principal | Target: 70.0° | Real: 67.9°`."),
 ("h", "5. Agarre por fricción y compuerta pasiva"),
 ("ul", ["**Agarre.** El cubo no está atado al cabezal: lo sostiene la fricción de la goma de los dedos (0.9 / 0.95). El force limit de los dedos es 300 N; el cubo pesa 78 N y la fricción ofrece unos 486 N.",
         "**Caja y suelo.** El acero de la caja (0.25 / 0.3) deja que los cubos resbalen a unos 27.5°. El suelo tiene poca fricción (0.1) para que las cargas se deslicen y la compuerta pueda cerrar.",
         "**Compuerta.** Es una unión pasiva, sin drive (stiffness 0, damping 10). La abren el peso y el empuje de las cargas (unos 72° al volcar) y vuelve a menos de 5° al bajar la caja.",
         "**Cabezal.** También es pasivo: cuelga por gravedad dentro de un cono de ±40°."]),
 ("h", "6. Controles"),
 ("ul", ["Torre: `A` / `D`. Pluma principal: `S` / `W`. Brazo secundario: `F` / `R`. Telescópico: `G` / `T`.",
         "Pinza: `Q` abre y `E` cierra. Caja volcadora: `Z` / `X` o las flechas.",
         "Reinicio: `Retroceso`, `P` o el botón Reiniciar Escena. Cámara: botón derecho para orbitar y rueda para acercar."]),
 ("h", "7. Cómo ejecutar"),
 ("ul", ["**Build web:** abrir el link de itch.io y hacer click sobre el canvas para que el teclado llegue al juego.",
         "**Editor:** abrir el proyecto con Unity 6000.3.14f1, cargar la escena principal y dar Play."]),
]

PAT = re.compile(r"(\*\*.+?\*\*|`.+?`)")
def runs(t):
    out = []
    for part in PAT.split(t):
        if not part: continue
        if part.startswith("**"): out.append((part[2:-2], "b"))
        elif part.startswith("`"): out.append((part[1:-1], "c"))
        else: out.append((part, ""))
    return out
def hrun(t): return "".join({"b": "<b>%s</b>", "c": "<code>%s</code>", "": "%s"}[k] % html.escape(x) for x, k in runs(t))

CSS = """@page{size:A4;margin:22mm 22mm 20mm}
body{font-family:Arial,sans-serif;font-size:10.5pt;line-height:1.42;color:#000;margin:0}
h1{font-size:20pt;color:#1F3A5F;margin:0 0 5mm;line-height:1.2}
h2{font-size:13pt;color:#1F3A5F;margin:5mm 0 1.5mm;break-after:avoid}
p{margin:0 0 3mm} .d{margin:0 0 1.5mm} .d b{display:inline-block;width:42mm}
code{font-family:Consolas,monospace;font-size:9.5pt}
ul{margin:0 0 3mm;padding-left:6mm} li{margin:0 0 1.5mm}
table{border-collapse:collapse;width:100%;margin:2mm 0 4mm;break-inside:avoid} th,td{border:.5pt solid #888;padding:.6mm 2.5mm;text-align:left;font-size:10pt} th{color:#1F3A5F}
figure{margin:2mm 0 3mm} img{width:86%;display:block} figcaption{font-size:9pt;color:#444;margin-top:1mm}"""

def build_html(path):
    o = ["<!doctype html><html lang='es'><head><meta charset='utf-8'><title>%s</title><style>%s</style></head><body>" % (html.escape(TITLE), CSS),
         "<h1>%s</h1>" % html.escape(TITLE)]
    o += ["<div class='d'><b>%s:</b> %s</div>" % (html.escape(k), html.escape(v)) for k, v in DATA]
    for b in DOC:
        k = b[0]
        if k in ("h", "hb"): o.append("<h2%s>%s</h2>" % (" style='break-before:page;margin-top:0'" if k == "hb" else "", html.escape(b[1])))
        elif k == "p": o.append("<p>%s</p>" % hrun(b[1]))
        elif k == "ul": o.append("<ul>" + "".join("<li>%s</li>" % hrun(i) for i in b[1]) + "</ul>")
        elif k == "fig": o.append("<figure><img src='%s'><figcaption>%s</figcaption></figure>" % (b[1], html.escape(b[2])))
        elif k == "tb":
            o.append("<table><tr>" + "".join("<th>%s</th>" % html.escape(h) for h in b[1]) + "</tr>" +
                     "".join("<tr>" + "".join("<td>%s</td>" % html.escape(c) for c in r) + "</tr>" for r in b[2]) + "</table>")
    o.append("</body></html>")
    open(path, "w", encoding="utf-8").write("".join(o))

def setfont(style, size, color=None, bold=None):
    style.font.name = FONT; style.font.size = Pt(size)
    rf = style.element.get_or_add_rPr().find(qn("w:rFonts"))
    for a in ("ascii", "hAnsi", "cs", "eastAsia"): rf.set(qn("w:" + a), FONT)
    for a in ("asciiTheme", "hAnsiTheme", "eastAsiaTheme", "cstheme"):
        if rf.get(qn("w:" + a)) is not None: del rf.attrib[qn("w:" + a)]
    if color: style.font.color.rgb = RGBColor.from_string(color)
    if bold is not None: style.font.bold = bold

def add_runs(par, t):
    for x, k in runs(t):
        r = par.add_run(x)
        if k == "b": r.bold = True
        if k == "c":
            r.font.name = "Consolas"; r.font.size = Pt(SIZE - 1)

def build_docx(path):
    d = Document(); s = d.sections[0]
    s.page_width, s.page_height = Cm(21), Cm(29.7)
    s.left_margin = s.right_margin = Cm(2.2); s.top_margin = Cm(2.2); s.bottom_margin = Cm(2.0)
    nm = d.styles["Normal"]; setfont(nm, SIZE); nm.paragraph_format.space_after = Pt(8); nm.paragraph_format.line_spacing = 1.3
    setfont(d.styles["Title"], 20, BLUE, True)
    tp = d.styles["Title"].element.get_or_add_pPr()
    for b in tp.findall(qn("w:pBdr")): tp.remove(b)
    d.styles["Title"].paragraph_format.space_after = Pt(10)
    h1 = d.styles["Heading 1"]; setfont(h1, 13, BLUE, True)
    h1.paragraph_format.space_before = Pt(16); h1.paragraph_format.space_after = Pt(5)
    lb = d.styles["List Bullet"]; setfont(lb, SIZE); lb.paragraph_format.space_after = Pt(4)
    d.add_paragraph(TITLE, style="Title")
    for k, v in DATA:
        p = d.add_paragraph(); p.paragraph_format.space_after = Pt(4); r = p.add_run(k + ": "); r.bold = True; p.add_run(v)
    for b in DOC:
        k = b[0]
        if k in ("h", "hb"): d.add_paragraph(b[1], style="Heading 1")
        elif k == "p": add_runs(d.add_paragraph(), b[1])
        elif k == "ul":
            for i in b[1]: add_runs(d.add_paragraph(style="List Bullet"), i)
        elif k == "fig":
            p = d.add_paragraph(); p.paragraph_format.space_after = Pt(2); p.add_run().add_picture(os.path.join(HERE, b[1]), width=Cm(16.4))
            c = d.add_paragraph(); r = c.add_run(b[2]); r.font.size = Pt(9)
        elif k == "tb":
            t = d.add_table(rows=1 + len(b[2]), cols=len(b[1])); t.style = "Table Grid"
            for ri, row in enumerate([b[1]] + b[2]):
                for ci, v in enumerate(row):
                    c = t.cell(ri, ci); c.width = Cm(16.6 / len(row)); par = c.paragraphs[0]; par.paragraph_format.space_after = Pt(1)
                    r = par.add_run(v); r.font.size = Pt(10)
                    if ri == 0: r.bold = True; r.font.color.rgb = RGBColor.from_string(BLUE)
            d.add_paragraph().paragraph_format.space_after = Pt(2)
    d.core_properties.title = TITLE; d.core_properties.author = ""
    d.save(path)

build_docx(os.path.join(OUT, "Documento_Entrega.docx"))
hp = os.path.join(HERE, "documento.html"); build_html(hp)
subprocess.run([CHROME, "--headless", "--disable-gpu", "--no-pdf-header-footer", "--print-to-pdf=" + os.path.join(OUT, "Documento_Entrega.pdf"),
                "file:///" + hp.replace("\\", "/")], check=True, capture_output=True)
print("ok")
