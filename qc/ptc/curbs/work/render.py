import sys, fitz
pdf=r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf"
d=fitz.open(pdf)
p=int(sys.argv[1]); z=float(sys.argv[2]) if len(sys.argv)>2 else 1
pg=d[p-1]; r=pg.rect
if len(sys.argv)>3:
    x0,y0,x1,y1=[float(v) for v in sys.argv[3].split(',')]
    clip=fitz.Rect(r.x0+x0*r.width,r.y0+y0*r.height,r.x0+x1*r.width,r.y0+y1*r.height)
else: clip=r
out=sys.argv[4] if len(sys.argv)>4 else f"p{p}.png"
pg.get_pixmap(matrix=fitz.Matrix(z,z),clip=clip).save(out); print(r, out)
