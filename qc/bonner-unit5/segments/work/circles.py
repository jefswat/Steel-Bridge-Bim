import pymupdf, sys, math
doc = pymupdf.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p=int(sys.argv[1]); x0,y0,x1,y1=map(float,sys.argv[2:6])
pg=doc[p-1]
for d in pg.get_drawings():
    its=[it for it in d['items'] if it[0]=='c']
    if len(its)<4: continue
    pts=[(it[1].x,it[1].y) for it in its]
    xs=[q[0] for q in pts]; ys=[q[1] for q in pts]
    cx=(max(xs)+min(xs))/2; cy=(max(ys)+min(ys))/2
    if not (x0<cx<x1 and y0<cy<y1): continue
    print(f"circle w{d.get('width') or 0:.2f} c=({cx:.2f},{cy:.2f}) dx={max(xs)-min(xs):.2f} dy={max(ys)-min(ys):.2f} n={len(its)}")
