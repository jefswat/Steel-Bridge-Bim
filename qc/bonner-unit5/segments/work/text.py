import fitz, sys
doc = fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p=int(sys.argv[1]); pg=doc[p-1]
print(pg.rect, pg.rotation)
for b in pg.get_text("words"):
    print(f"{b[0]:7.1f} {b[1]:7.1f} {b[2]:7.1f} {b[3]:7.1f} {b[4]}")
