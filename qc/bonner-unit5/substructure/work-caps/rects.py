import pymupdf as fitz,sys
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p=int(sys.argv[1]); R=fitz.Rect(*map(float,sys.argv[2:6])); cx,cy,sc=map(float,sys.argv[6:9])
pg=doc[p-1]
H=[];V=[]
for d in pg.get_drawings():
    if abs((d.get('width') or 0)-1.8)>0.01: continue
    for it in d['items']:
        if it[0]=='l' and R.contains(it[1]) and R.contains(it[2]):
            a,b=it[1],it[2]
            if abs(a.y-b.y)<0.05 and abs(a.x-b.x)>20: H.append((min(a.x,b.x),max(a.x,b.x),a.y))
            if abs(a.x-b.x)<0.05 and abs(a.y-b.y)>20: V.append((min(a.y,b.y),max(a.y,b.y),a.x))
f=lambda x:(x-cx)/sc; g=lambda y:(cy-y)/sc
for x0,x1,y in H: print('H S %.3f..%.3f N %.3f'%(f(x0),f(x1),g(y)))
for y0,y1,x in V: print('V N %.3f..%.3f S %.3f'%(g(y1),g(y0),f(x)))
