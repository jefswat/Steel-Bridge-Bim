import fitz,sys
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
for p in map(int,sys.argv[1:]):
    pg=doc[p-1]; W=pg.get_text("words")
    bs=[w for w in W if w[4]=="BILL"]
    for b in bs:
        x0,y0=b[0]-250,b[1]-5
        reg=[w for w in W if w[0]>x0 and w[0]<x0+700 and w[1]>y0 and w[1]<y0+800]
        rows={}
        for w in reg:
            k=round((w[1]+w[3])/2/3)
            rows.setdefault(k,[]).append(w)
        print("== p",p,"BILL at",b[:2])
        for k in sorted(rows):
            ws=sorted(rows[k],key=lambda w:w[0])
            print(" | ".join(f"{w[4]}@{w[0]:.0f}" for w in ws))
