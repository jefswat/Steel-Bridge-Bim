import sys,csv,pymupdf
from PIL import Image,ImageDraw
# govl.py structure JOINT dpi out : sheet crop with the csv outline polygon overlaid (red, thin) so a gusset edge that is off shows as a gap
st,jt,dpi,out=sys.argv[1],sys.argv[2],float(sys.argv[3]),sys.argv[4]
rows=[r for r in csv.DictReader(open(r'C:\SBM\docs\plan-data\ptc-delaware-river\connections\gusset-outlines.csv',encoding='utf-8')) if r['structure'].startswith(st) and r['joint']==jt]
r=rows[0]; pg=int(r['page'])
pts=[tuple(map(float,p.split(','))) for p in r['outline_px'].split(';')]
x0=min(p[0] for p in pts)-40; x1=max(p[0] for p in pts)+40; y0=min(p[1] for p in pts)-40; y1=max(p[1] for p in pts)+40
k=72/150; s=dpi/150
d=pymupdf.open(r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf')
pm=d[pg-1].get_pixmap(matrix=pymupdf.Matrix(dpi/72,dpi/72),clip=pymupdf.Rect(x0*k,y0*k,x1*k,y1*k)); pm.save(out)
im=Image.open(out).convert('RGB'); dr=ImageDraw.Draw(im)
q=[((x-x0)*s,(y-y0)*s) for x,y in pts]
dr.line(q+[q[0]],fill=(255,0,0),width=1)
im.save(out); print(pg,im.width,im.height)
