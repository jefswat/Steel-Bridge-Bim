import sys, pymupdf as fitz
p=int(sys.argv[1]); x0,y0,x1,y1=map(float,sys.argv[2:6]); dpi=int(sys.argv[6]); out=sys.argv[7]
d=fitz.open(r'C:\SBMData\Vol 2_Phase 2B Bridge Combined.pdf')
d[p-1].get_pixmap(dpi=dpi, clip=fitz.Rect(x0,y0,x1,y1)).save(out)
