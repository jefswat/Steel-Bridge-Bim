# grid.py page x0 y0 x1 y1 out.png [scale=2] [step=20]  (absolute page px at 300 dpi)
import sys, fitz
from PIL import Image, ImageDraw
PDF = r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf"
pg=int(sys.argv[1]); x0,y0,x1,y1=[float(a) for a in sys.argv[2:6]]; out=sys.argv[6]
sc=float(sys.argv[7]) if len(sys.argv)>7 else 2; st=int(sys.argv[8]) if len(sys.argv)>8 else 20
p=fitz.open(PDF)[pg-1]; k=72/300
z=300/72*sc
pix=p.get_pixmap(matrix=fitz.Matrix(z,z),clip=fitz.Rect(x0*k,y0*k,x1*k,y1*k),colorspace=fitz.csGRAY)
im=Image.frombytes("L",(pix.width,pix.height),pix.samples).convert("RGB"); d=ImageDraw.Draw(im)
import math
for gx in range(int(math.ceil(x0/st)*st), int(x1)+1, st):
    X=(gx-x0)*sc; c=(255,0,0) if gx%100==0 else (255,180,180); d.line([(X,0),(X,im.height)],fill=c)
    if gx%(st*2)==0: d.text((X+2,2),str(gx),fill=(255,0,0))
for gy in range(int(math.ceil(y0/st)*st), int(y1)+1, st):
    Y=(gy-y0)*sc; c=(0,0,255) if gy%100==0 else (180,180,255); d.line([(0,Y),(im.width,Y)],fill=c)
    if gy%(st*2)==0: d.text((2,Y+2),str(gy),fill=(0,0,255))
im.save(out); print(im.size)
