import sys,pymupdf
from PIL import Image,ImageDraw
# gcrop3.py page x0 y0 x1 y1 dpi out   (150-dpi page px frame)
pg=int(sys.argv[1]); x0,y0,x1,y1=map(float,sys.argv[2:6]); dpi=float(sys.argv[6]); out=sys.argv[7]
k=72/150
d=pymupdf.open(r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf')
pm=d[pg-1].get_pixmap(matrix=pymupdf.Matrix(dpi/72,dpi/72),clip=pymupdf.Rect(x0*k,y0*k,x1*k,y1*k)); pm.save(out)
im=Image.open(out).convert('RGB'); dr=ImageDraw.Draw(im)
s=dpi/150
for x in range(0,im.width,50):
    dr.line([(x,0),(x,8)],fill=(255,0,0)); dr.text((x+2,8),str(int(x0+x/s)),fill=(255,0,0))
for y in range(0,im.height,50):
    dr.line([(0,y),(8,y)],fill=(255,0,0)); dr.text((10,y-4),str(int(y0+y/s)),fill=(255,0,0))
im.save(out); print(im.width,im.height)
