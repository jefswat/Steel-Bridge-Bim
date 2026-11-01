import pymupdf, sys
doc = pymupdf.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p=int(sys.argv[1]); x0,y0,x1,y1=map(float,sys.argv[2:6]); minlen=float(sys.argv[6]) if len(sys.argv)>6 else 0
pg=doc[p-1]
for d in pg.get_drawings():
    r=d['rect']
    if r.x1<x0 or r.x0>x1 or r.y1<y0 or r.y0>y1: continue
    w=d.get('width') or 0; ds=d.get('dashes') or ''
    for it in d['items']:
        if it[0]=='l':
            a,b=it[1],it[2]
            L=((a.x-b.x)**2+(a.y-b.y)**2)**.5
            if L<minlen: continue
            if max(a.x,b.x)<x0 or min(a.x,b.x)>x1 or max(a.y,b.y)<y0 or min(a.y,b.y)>y1: continue
            print(f"L w{w:.2f} {ds[:12]:12s} {a.x:8.2f} {a.y:8.2f} {b.x:8.2f} {b.y:8.2f} len{L:7.2f}")
        elif it[0]=='c':
            a,b=it[1],it[4]
            print(f"C w{w:.2f} {ds[:12]:12s} {a.x:8.2f} {a.y:8.2f} {b.x:8.2f} {b.y:8.2f}")
        elif it[0]=='re':
            print("R",w,it[1])
