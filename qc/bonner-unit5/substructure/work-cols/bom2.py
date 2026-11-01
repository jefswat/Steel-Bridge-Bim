import fitz,re,sys,json
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
FR={0x192:'3/4',0x201a:'3/8',0x201e:'5/8',0x2020:'1/8',0x2021:'7/8',0x2022:'1/2',0x2026:'1/4',0x2030:'?'}
def fix(s): return "".join((" "+FR.get(ord(c),'?')) if ord(c)>127 else c for c in s)
out={}
for p in map(int,sys.argv[1:]):
    pg=doc[p-1]; W=pg.get_text("words")
    hdr=[w for w in W if w[4]=="WEIGHT"]
    for h in hdr:
        row=[w for w in W if abs(w[1]-h[1])<4 and h[0]-480<w[0]<=h[0]]
        cols={w[4]:(w[0]+w[2])/2 for w in row}
        if "BAR" not in cols: continue
        xb=cols["BAR"]
        marks=[w for w in W if re.match(r"^[A-Z]\d+$",w[4]) and abs((w[0]+w[2])/2-xb)<15 and w[1]>h[1] and w[1]<h[1]+700]
        marks.sort(key=lambda w:w[1])
        names=["NO.","SIZE","TYPE","MAT'L","LENGTH","WEIGHT"]
        res=[]
        for m in marks:
            ym=(m[1]+m[3])/2
            r={"bar":m[4]}
            for n in names:
                if n not in cols: continue
                cand=[w for w in W if abs((w[0]+w[2])/2-cols[n])<(40 if n in("LENGTH","WEIGHT") else 18) and abs((w[1]+w[3])/2-ym)<9 and w is not m]
                r[n]=" ".join(fix(w[4]) for w in cand)
            res.append(r)
        # totals
        tot=[w for w in W if w[4] in("LBS.",) and w[1]>h[1]]
        print("== p",p,"hdr at",round(h[0]),round(h[1]))
        for r in res: print(",".join(r.get(k,"") if k!="bar" else r[k] for k in ["bar"]+names))
