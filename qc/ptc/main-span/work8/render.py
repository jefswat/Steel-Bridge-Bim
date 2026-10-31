import sys, fitz
PDF = r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf"
# render.py page zoom out.png [x0 y0 x1 y1 as fractions of page]
pg=int(sys.argv[1]); z=float(sys.argv[2]); out=sys.argv[3]
d=fitz.open(PDF); p=d[pg-1]; r=p.rect
if len(sys.argv)>4:
    f=[float(a) for a in sys.argv[4:8]]
    clip=fitz.Rect(r.x0+f[0]*r.width, r.y0+f[1]*r.height, r.x0+f[2]*r.width, r.y0+f[3]*r.height)
else: clip=r
pix=p.get_pixmap(matrix=fitz.Matrix(z,z), clip=clip, colorspace=fitz.csGRAY)
pix.save(out); print(r, pix.width, pix.height)
