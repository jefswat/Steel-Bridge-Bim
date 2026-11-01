import pymupdf as fitz,sys,collections
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p=int(sys.argv[1]); R=fitz.Rect(*map(float,sys.argv[2:6])); cx,cy,sc=map(float,sys.argv[6:9])
pg=doc[p-1]
# collect closed 4-line rectangles from drawings
for d in pg.get_drawings():
    its=d['items']; 
    if not its: continue
    xs=[];ys=[]
    for it in its:
        if it[0]=='l': xs+= [it[1].x,it[2].x]; ys+=[it[1].y,it[2].y]
        elif it[0]=='re': xs+=[it[1].x0,it[1].x1]; ys+=[it[1].y0,it[1].y1]
        elif it[0]=='c': xs+=[it[1].x,it[4].x]; ys+=[it[1].y,it[4].y]
    if not xs: continue
    r=fitz.Rect(min(xs),min(ys),max(xs),max(ys))
    if not R.contains(r): continue
    w,h=r.width,r.height
    if 5<w<200 and 5<h<200:
        print('%-3s n%2d w%.2f %s  size %.2f x %.2f in  ctr S %.3f N %.3f ft'%(''.join(sorted(set(i[0] for i in its))),len(its),d.get('width') or 0,'dash' if d.get('dashes') not in (None,'[] 0') else '', w/sc*12/1, h/sc*12, ((r.x0+r.x1)/2-cx)/sc, (cy-(r.y0+r.y1)/2)/sc))
