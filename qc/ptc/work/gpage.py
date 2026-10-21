import sys,pymupdf
pg=int(sys.argv[1]); dpi=float(sys.argv[2]); out=sys.argv[3]
d=pymupdf.open(r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf')
p=d[pg-1]; print(p.rect,p.rotation)
pm=p.get_pixmap(matrix=pymupdf.Matrix(dpi/72,dpi/72)); pm.save(out); print(pm.width,pm.height)
