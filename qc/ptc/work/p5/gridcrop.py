"""gridcrop.py page x0 y0 x1 y1 zoom step out.png - crop of the plan PDF with a labelled pixel grid every `step` px (for scaling outlines). Read-only."""
import sys, fitz
from PIL import Image, ImageDraw
PDF = r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf'
p, x0, y0, x1, y1, z, step, out = int(sys.argv[1]), *map(float, sys.argv[2:7]), int(sys.argv[7]), sys.argv[8]
pg = fitz.open(PDF)[p - 1]; r = pg.rect
clip = fitz.Rect(r.x0 + x0 * r.width, r.y0 + y0 * r.height, r.x0 + x1 * r.width, r.y0 + y1 * r.height)
pix = pg.get_pixmap(matrix=fitz.Matrix(z, z), clip=clip, colorspace=fitz.csGRAY)
im = Image.frombytes('L', (pix.width, pix.height), pix.samples).convert('RGB')
d = ImageDraw.Draw(im)
for x in range(0, im.width, step):
    d.line([(x, 0), (x, im.height)], fill=(255, 150, 150) if x % (step * 2) else (255, 0, 0), width=1)
    d.text((x + 2, 2), str(x), fill=(255, 0, 0))
for y in range(0, im.height, step):
    d.line([(0, y), (im.width, y)], fill=(150, 150, 255) if y % (step * 2) else (0, 0, 255), width=1)
    d.text((2, y + 2), str(y), fill=(0, 0, 255))
im.save(out)
print(out, im.width, im.height, 'px/in at the 0.47 reduced 3/4"=1ft scale ~', round(0.75 / 12 * 72 * z * 0.4706 * 12 / 12, 3))
