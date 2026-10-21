import sys,pymupdf
from PIL import Image,ImageDraw
# gcrop.py page cx cy halfw halfh dpi out  ; cx.. in the 150-dpi px frame of the page; draws a 50-px ruler grid labelled in px of the output
pg=int(sys.argv[1]); cx,cy,hw,hh=map(float,sys.argv[2:6]); dpi=float(sys.argv[6]); out=sys.argv[7]
k=72/150
d=pymupdf.open(r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf')
r=pymupdf.Rect((cx-hw)*k,(cy-hh)*k,(cx+hw)*k,(cy+hh)*k)
pm=d[pg-1].get_pixmap(matrix=pymupdf.Matrix(dpi/72,dpi/72),clip=r); pm.save(out)
im=Image.open(out).convert('RGB'); dr=ImageDraw.Draw(im)
for x in range(0,im.width,50):
    dr.line([(x,0),(x,8)],fill=(255,0,0)); dr.text((x+2,8),str(x),fill=(255,0,0))
for y in range(0,im.height,50):
    dr.line([(0,y),(8,y)],fill=(255,0,0)); dr.text((10,y-4),str(y),fill=(255,0,0))
im.save(out); print(im.width,im.height)
