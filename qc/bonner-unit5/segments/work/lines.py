import fitz,sys
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p=int(sys.argv[1]); pg=doc[p-1]
R=fitz.Rect(*map(float,sys.argv[2:6])); minw=float(sys.argv[6]) if len(sys.argv)>6 else 0
for d in pg.get_drawings():
    if not (d['rect']+(-1,-1,1,1)).intersects(R): continue
    w=d.get('width') or 0
    if w<minw: continue
    dash=d.get('dashes')
    for it in d['items']:
        if it[0]=='l':
            a,b=it[1],it[2]
            if fitz.Rect(min(a.x,b.x)-.1,min(a.y,b.y)-.1,max(a.x,b.x)+.1,max(a.y,b.y)+.1).intersects(R):
                print(f"L w{w:.2f} {dash if dash and dash!='[] 0' else ''} {a.x:8.2f} {a.y:8.2f} {b.x:8.2f} {b.y:8.2f}")
        elif it[0]=='c':
            a,b=it[1],it[4]
            if fitz.Rect(min(a.x,b.x)-.1,min(a.y,b.y)-.1,max(a.x,b.x)+.1,max(a.y,b.y)+.1).intersects(R):
                print(f"C w{w:.2f} {a.x:8.2f} {a.y:8.2f} {b.x:8.2f} {b.y:8.2f}  ctrl {it[2].x:.2f},{it[2].y:.2f} {it[3].x:.2f},{it[3].y:.2f}")
        elif it[0]=='re':
            r=it[1]; print(f"R w{w:.2f} {r}")
