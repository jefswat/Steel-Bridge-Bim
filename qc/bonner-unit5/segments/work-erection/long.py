import fitz, sys, math
doc = fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p=int(sys.argv[1]); x0,y0,x1,y1=map(float,sys.argv[2:6]); minlen=float(sys.argv[6])
pg=doc[p-1]; out=[]
for d in pg.get_drawings():
    w=d.get("width") or 0; dash=d.get("dashes")
    for it in d["items"]:
        if it[0]=="l":
            a,b=it[1],it[2]
            if math.dist(a,b)<minlen: continue
            if x0<=min(a.x,b.x) and max(a.x,b.x)<=x1 and y0<=min(a.y,b.y) and max(a.y,b.y)<=y1:
                out.append((round(w,2),dash,round(a.x,2),round(a.y,2),round(b.x,2),round(b.y,2)))
        elif it[0]=="re":
            r=it[1]
            if x0<=r.x0 and r.x1<=x1 and y0<=r.y0 and r.y1<=y1: out.append((round(w,2),dash,"RECT",round(r.x0,2),round(r.y0,2),round(r.x1,2),round(r.y1,2)))
for o in sorted(set(out),key=str): print(o)
