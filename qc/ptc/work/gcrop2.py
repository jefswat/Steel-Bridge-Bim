import sys,csv,pymupdf
from PIL import Image,ImageDraw
# gcrop2.py "<structure>" JOINT dpi out   -> crop around the csv outline bbox (locating only), ruler in output px
st,jt,dpi,out=sys.argv[1],sys.argv[2],float(sys.argv[3]),sys.argv[4]
rows=[r for r in csv.DictReader(open(r'C:\SBM\docs\plan-data\ptc-delaware-river\connections\gusset-outlines.csv',encoding='utf-8')) if r['structure'].startswith(st) and r['joint']==jt]
r=rows[0]; pg=int(r['page'])
pts=[tuple(map(float,p.split(','))) for p in r['outline_px'].split(';')]
x0=min(p[0] for p in pts)-60; x1=max(p[0] for p in pts)+60; y0=min(p[1] for p in pts)-60; y1=max(p[1] for p in pts)+60
k=72/150
d=pymupdf.open(r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf')
pm=d[pg-1].get_pixmap(matrix=pymupdf.Matrix(dpi/72,dpi/72),clip=pymupdf.Rect(x0*k,y0*k,x1*k,y1*k)); pm.save(out)
im=Image.open(out).convert('RGB'); dr=ImageDraw.Draw(im)
for x in range(0,im.width,50):
    dr.line([(x,0),(x,8)],fill=(255,0,0)); dr.text((x+2,8),str(x),fill=(255,0,0))
for y in range(0,im.height,50):
    dr.line([(0,y),(8,y)],fill=(255,0,0)); dr.text((10,y-4),str(y),fill=(255,0,0))
im.save(out); print(pg,im.width,im.height,'origin(150dpi px)',x0,y0,'scale out/150dpi',dpi/150)
