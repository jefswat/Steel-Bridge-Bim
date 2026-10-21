"""crop.py page x0 y0 x1 y1 zoom out.png  (fractions of the page, 0..1). Read-only render of the plan PDF."""
import sys, fitz
PDF = r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf'
p, x0, y0, x1, y1, z, out = int(sys.argv[1]), *map(float, sys.argv[2:7]), sys.argv[7]
doc = fitz.open(PDF)
pg = doc[p - 1]
r = pg.rect
clip = fitz.Rect(r.x0 + x0 * r.width, r.y0 + y0 * r.height, r.x0 + x1 * r.width, r.y0 + y1 * r.height)
pix = pg.get_pixmap(matrix=fitz.Matrix(z, z), clip=clip, colorspace=fitz.csGRAY)
pix.save(out)
print(out, pix.width, pix.height)
