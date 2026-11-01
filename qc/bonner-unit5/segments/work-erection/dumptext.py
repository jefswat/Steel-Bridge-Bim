import fitz, sys
doc = fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
for a in sys.argv[1:]:
    p=int(a); pg=doc[p-1]
    print("=====PAGE",p,pg.rect)
    for b in pg.get_text("blocks"):
        t=b[4].strip().replace("\n"," | ")
        if t: print(f"[{b[0]:.0f},{b[1]:.0f},{b[2]:.0f},{b[3]:.0f}] {t}")
