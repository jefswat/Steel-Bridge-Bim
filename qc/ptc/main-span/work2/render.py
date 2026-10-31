import sys, fitz
doc = fitz.open(r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf")
pno = int(sys.argv[1]); dpi = int(sys.argv[2]); out = sys.argv[3]
clip = None
if len(sys.argv) > 4:
    x0,y0,x1,y1 = map(float, sys.argv[4].split(','))
    r = doc[pno-1].rect
    clip = fitz.Rect(r.x0 + x0*r.width, r.y0 + y0*r.height, r.x0 + x1*r.width, r.y0 + y1*r.height)
pix = doc[pno-1].get_pixmap(dpi=dpi, clip=clip)
pix.save(out); print(pix.width, pix.height, doc[pno-1].rect)
