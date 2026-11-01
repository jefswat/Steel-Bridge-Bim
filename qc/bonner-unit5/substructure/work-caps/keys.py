import pymupdf as fitz,sys
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p=int(sys.argv[1]); R=fitz.Rect(*map(float,sys.argv[2:6])); cx,cy,sc=map(float,sys.argv[6:9]); W=float(sys.argv[9])
pg=doc[p-1]
pts=[]
for d in pg.get_drawings():
    if abs((d.get('width') or 0)-W)>0.01: continue
    for it in d['items']:
        if it[0]=='l' and R.contains(it[1]) and R.contains(it[2]):
            pts.append(((it[1].x+it[2].x)/2,(it[1].y+it[2].y)/2,abs(it[1]-it[2])))
# cluster by proximity 
cl=[]
for x,y,l in pts:
    for c in cl:
        if abs(c['x']-x)<25 and abs(c['y']-y)<25: c['p'].append((x,y)); break
    else: cl.append({'x':x,'y':y,'p':[(x,y)]})
    # update
for c in cl:
    xs=[a for a,b in c['p']]; ys=[b for a,b in c['p']]
    if len(c["p"])>=int(sys.argv[10]): print('n%2d S %.3f N %.3f  size %.1f x %.1f in'%(len(c['p']),((min(xs)+max(xs))/2-cx)/sc,(cy-(min(ys)+max(ys))/2)/sc,(max(xs)-min(xs))/sc*12,(max(ys)-min(ys))/sc*12))
