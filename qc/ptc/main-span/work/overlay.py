import csv, fitz, sys
from PIL import Image, ImageDraw
D=r"C:\SBM\docs\plan-data\ptc-delaware-river\connections"
pdf=fitz.open(r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf")
O=[x for x in csv.DictReader(open(D+r"\gusset-outlines.csv")) if x['structure'].startswith('main')]
RP=[x for x in csv.DictReader(open(D+r"\rivet-positions.csv")) if x['structure'].startswith('main')]
only=sys.argv[1:]
def P(s): return [tuple(map(float,p.split(','))) for p in s.replace(';',' ').split()] if ';' not in s else [tuple(map(float,p.strip().split(','))) for p in s.split(';')]
for o in O:
    if only and o['joint'] not in only: continue
    pg=pdf[int(o['page'])-1]; S=2  # render at 300 dpi = 2x the 150 dpi px
    wx,wy=map(float,o['wp_px'].split(',')); ppi=float(o['px_per_in'])
    poly=[tuple(map(float,p.strip().split(','))) for p in o['outline_px'].split(';')]
    xs=[p[0] for p in poly]; ys=[p[1] for p in poly]
    x0,x1=min(xs)-120,max(xs)+120; y0,y1=min(ys)-120,max(ys)+120
    clip=fitz.Rect(x0*72/150,y0*72/150,x1*72/150,y1*72/150)
    pix=pg.get_pixmap(dpi=300,clip=clip); im=Image.frombytes('RGB',(pix.width,pix.height),pix.samples)
    d=ImageDraw.Draw(im)
    T=lambda x,y:((x-x0)*S,(y-y0)*S)
    d.line([T(*p) for p in poly+[poly[0]]],fill=(255,0,0),width=3)
    c=[tuple(map(float,p.strip().split(','))) for p in o['chord_px'].split(';')]
    d.line([T(*p) for p in c],fill=(255,0,255),width=2)
    d.ellipse([T(wx,wy)[0]-8,T(wx,wy)[1]-8,T(wx,wy)[0]+8,T(wx,wy)[1]+8],outline=(255,0,255),width=3)
    n=0
    for r in RP:
        if r['joint']!=o['joint'] or r['page']!=o['page']: continue
        x=wx+float(r['u_in'])*ppi; y=wy-float(r['v_in'])*ppi; X,Y=T(x,y)
        col=(0,170,0) if r['region']=='inside' else (0,0,255)
        d.ellipse([X-5,Y-5,X+5,Y+5],outline=col,width=2); n+=1
    im.save(f"ovl/{o['joint']}_p{o['page']}.png"); print(o['joint'],o['page'],n,im.size)
