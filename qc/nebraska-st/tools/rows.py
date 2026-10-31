import sys, pymupdf
d=pymupdf.open(r"C:\SBMData\B00167 - Nebraska Ramp B - Design 116.pdf")
def rows(pg,x0,y0,x1,y1,tol=2.5):
    p=d[pg-1]; ws=[w for w in p.get_text("words") if w[0]>=x0 and w[2]<=x1 and w[1]>=y0 and w[3]<=y1]
    ws.sort(key=lambda w:(round((w[1]+w[3])/2),w[0]))
    out=[];cur=[];cy=None
    for w in sorted(ws,key=lambda w:((w[1]+w[3])/2,w[0])):
        yc=(w[1]+w[3])/2
        if cy is None or abs(yc-cy)<=tol: cur.append(w); cy=yc if cy is None else (cy+yc)/2
        else: out.append((cy,cur)); cur=[w]; cy=yc
    if cur: out.append((cy,cur))
    return [(round(c,1),[(round(w[0]),w[4]) for w in sorted(r,key=lambda w:w[0])]) for c,r in out]
if __name__=="__main__":
    pg=int(sys.argv[1]); a=list(map(float,sys.argv[2:6]))
    for c,r in rows(pg,*a): print(c," | ".join(f"{t}" for x,t in r))
