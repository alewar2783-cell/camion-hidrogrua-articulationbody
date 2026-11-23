# Rasteriza cada pagina del PDF a PNG para revisar. Uso: python render_pages.py <dir_salida>
import sys, os, pymupdf
d = pymupdf.open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Documento_Entrega.pdf"))
print("paginas:", len(d))
for i, pg in enumerate(d): pg.get_pixmap(dpi=100).save(f"{sys.argv[1]}/p{i+1}.png")
