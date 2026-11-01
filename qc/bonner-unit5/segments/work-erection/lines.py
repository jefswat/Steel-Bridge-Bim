import fitz, sys
doc = fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p=int(sys.argv[1]); x0,y0,x1,y1=map(float,sys.argv[2:6]); minw=float(sys.argv[6]) if len(sys.argv)>6 else 0
pg=doc[p-1]
for d in pg.get_drawings():
    w=d.get("width") or 0
    if w<minw: continue
    for it in d["items"]:
        if it[0]=="l":
            a,b=it[1],it[2]
            if x0<=min(a.x,b.x) and max(a.x,b.x)<=x1 and y0<=min(a.y,b.y) and max(a.y,b.y)<=y1:
                print(f"w={w:.2f} ({a.x:.2f},{a.y:.2f})-({b.x:.2f},{b.y:.2f})")
        elif it[0]=="re":
            r=it[1]
            if x0<=r.x0 and r.x1<=x1 and y0<=r.y0 and r.y1<=y1: print(f"w={w:.2f} RECT {r}")
