import sys,pymupdf
# usage: crop.py page x0 y0 x1 y1 zoom out   (coords in PDF pts)
pg,x0,y0,x1,y1,z,out=int(sys.argv[1]),*map(float,sys.argv[2:7]),sys.argv[7]
d=pymupdf.open(r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf')
pm=d[pg-1].get_pixmap(matrix=pymupdf.Matrix(z,z),clip=pymupdf.Rect(x0,y0,x1,y1)); pm.save(out); print(pm.width,pm.height)
