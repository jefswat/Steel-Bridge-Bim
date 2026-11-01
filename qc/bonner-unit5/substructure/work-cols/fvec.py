import fitz,sys,math,collections
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p=int(sys.argv[1]); x0,y0,x1,y1=map(float,sys.argv[2:6]); minlen=float(sys.argv[6]) if len(sys.argv)>6 else 40
pg=doc[p-1]
segs=[]
for d in pg.get_drawings():
    for it in d["items"]:
        if it[0]=="l":
            a,b=it[1],it[2]
            if all(x0<=q.x<=x1 and y0<=q.y<=y1 for q in (a,b)):
                L=math.hypot(b.x-a.x,b.y-a.y)
                if L>=minlen: segs.append((round(a.x,2),round(a.y,2),round(b.x,2),round(b.y,2),round(L,1),d.get("width"),bool(d.get("dashes") and d["dashes"]!="[] 0")))
        elif it[0]=="re":
            r=it[1]
            if x0<=r.x0 and r.x1<=x1 and y0<=r.y0 and r.y1<=y1 and max(r.width,r.height)>=minlen: segs.append(("RE",round(r.x0,2),round(r.y0,2),round(r.x1,2),round(r.y1,2),d.get("width")))
for s in sorted(segs,key=lambda s:-s[4] if s[0]!="RE" else 0): print(s)
