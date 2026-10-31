import sys, fitz
pdf = r"C:\SBMData\0047-07-228 NHT 100% Plans_09-04-2020_comments_Reduced.pdf"
doc = fitz.open(pdf)
# usage: render.py page zoom [x0 y0 x1 y1 as fractions] [name]
p = int(sys.argv[1]); z = float(sys.argv[2])
page = doc[p - 1]
r = page.rect
clip = None
name = f"p{p}"
if len(sys.argv) >= 7:
    f = [float(v) for v in sys.argv[3:7]]
    clip = fitz.Rect(r.x0 + f[0] * r.width, r.y0 + f[1] * r.height, r.x0 + f[2] * r.width, r.y0 + f[3] * r.height)
    name += "_" + (sys.argv[7] if len(sys.argv) > 7 else "crop")
pix = page.get_pixmap(matrix=fitz.Matrix(z, z), clip=clip)
out = rf"C:\SBM\qc\nht\curbs\work\{name}.png"
pix.save(out)
print(out, pix.width, pix.height, r)
