import fitz, sys
doc = fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p, x0,y0,x1,y1, name = int(sys.argv[1]), *map(float, sys.argv[2:6]), sys.argv[6]
dpi = int(sys.argv[7]) if len(sys.argv)>7 else 200
pg = doc[p-1]
# coords given in the 2000px display of 110dpi render: convert
s = 72/110*1.87
r = fitz.Rect(x0*s, y0*s, x1*s, y1*s)
pg.get_pixmap(dpi=dpi, clip=r).save(f"C:/SBM/qc/bonner-unit5/pt-profiles/img/{name}.png")
