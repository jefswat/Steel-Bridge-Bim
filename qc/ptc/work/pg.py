import sys,pymupdf
d=pymupdf.open(r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf')
pg=int(sys.argv[1]); z=float(sys.argv[2]) if len(sys.argv)>2 else 1.3
if len(sys.argv)>3:
    x0,y0,x1,y1=map(float,sys.argv[3:7]); clip=pymupdf.Rect(x0,y0,x1,y1)
else: clip=None
pm=d[pg-1].get_pixmap(matrix=pymupdf.Matrix(z,z),clip=clip)
out=sys.argv[7] if len(sys.argv)>7 else f'r3/p/pg{pg}.png'
pm.save(out); print(out,pm.width,pm.height)
