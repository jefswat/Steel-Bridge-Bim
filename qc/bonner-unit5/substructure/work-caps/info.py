import fitz,sys
doc=fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
for p in range(int(sys.argv[1]),int(sys.argv[2])+1):
    pg=doc[p-1]; t=pg.get_text(); d=pg.get_drawings()
    print(p, pg.rect, 'chars',len(t),'draw',len(d),'imgs',len(pg.get_images()), t[:150].replace('\n','|'))
