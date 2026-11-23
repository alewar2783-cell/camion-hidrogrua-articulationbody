# Arbol de joints simple (SVG -> PNG con Chrome headless). Salida: arbol_joints.svg / arbol_joints.png
import os, subprocess
HERE = os.path.dirname(os.path.abspath(__file__))
CHROME = "C:/Program Files/Google/Chrome/Application/chrome.exe"
BLUE = "#1F3A5F"
# (nivel, nombre, detalle, padre_idx)
N = [(0, "Chasis_Root", "raíz fija, 1000 kg", None),
     (1, "Torre_Base", "Revolute, eje Y, −170° a 170°", 0),
     (2, "Brazo_Principal", "Revolute, eje X, −15° a 75°", 1),
     (3, "Brazo_Secundario", "Revolute, eje X, −125° a 15°", 2),
     (4, "Tramo_Telescopico", "Prismatic, eje Z, 0 a 2.5 m", 3),
     (5, "Cabezal_Pinza", "Spherical, pasivo (sin drive)", 4),
     (6, "Dedo_Izquierdo", "Prismatic, 0 a 0.35 m", 5),
     (6, "Dedo_Derecho", "Prismatic, 0 a 0.35 m", 5),
     (1, "Caja_Volcadora", "Revolute, 0° a 55°", 0),
     (2, "Compuerta_Trasera", "Revolute, pasivo (sin drive)", 8)]
RH, X0, DX, W = 34, 20, 34, 1000
H = RH * len(N) + 20
o = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}"><rect width="{W}" height="{H}" fill="white"/>']
ys = [14 + RH * i + RH // 2 for i in range(len(N))]
for i, (lv, nm, dt, par) in enumerate(N):
    x = X0 + lv * DX
    if par is not None:
        px = X0 + N[par][0] * DX + 10
        o.append(f'<path d="M{px} {ys[par]+10} V{ys[i]} H{x-4}" fill="none" stroke="{BLUE}" stroke-width="1.5"/>')
    o.append(f'<text x="{x}" y="{ys[i]+5}" font-family="Arial" font-size="17" fill="#000"><tspan font-weight="bold" fill="{BLUE}">{i+1}. {nm}</tspan>  —  {dt}</text>')
o.append("</svg>")
sp = os.path.join(HERE, "arbol_joints.svg"); open(sp, "w", encoding="utf-8").write("".join(o))
subprocess.run([CHROME, "--headless", "--disable-gpu", "--hide-scrollbars", f"--window-size={W},{H}", "--force-device-scale-factor=2",
                "--screenshot=" + os.path.join(HERE, "arbol_joints.png"), "file:///" + sp.replace("\\", "/")], check=True, capture_output=True)
print("ok")
