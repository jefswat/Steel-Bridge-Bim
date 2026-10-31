import fitz, sys
doc = fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
for p in sys.argv[1:]:
    pg = doc[int(p)-1]
    print(p, pg.rect, pg.rotation)
    pg.get_pixmap(dpi=110).save(f"C:/SBM/qc/bonner-unit5/pt-profiles/img/p{p}.png")
