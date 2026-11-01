import fitz,sys
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
for p in list(range(334,354))+list(range(376,385)):
    pg=doc[p-1]
    w=pg.get_text("words")
    d=pg.get_drawings()
    imgs=pg.get_images()
    print(p,pg.rect,len(w),"words",len(d),"draws",len(imgs),"imgs")
    open(f"txt/p{p}.txt","w",encoding="utf8").write(pg.get_text())
