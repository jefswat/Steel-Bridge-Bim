import sys,csv,pymupdf,numpy as np
from PIL import Image,ImageDraw
from scipy import ndimage as ndi
# rdet.py structure JOINT out.png : detect rivet symbols (filled dots by opening, open circles / crossed circles by small enclosed holes) inside the csv gusset polygon
st,jt,out=sys.argv[1],sys.argv[2],sys.argv[3]
DPI=300
def run(st,jt,out=None,quiet=False):
    rows=[r for r in csv.DictReader(open(r'C:\SBM\docs\plan-data\ptc-delaware-river\connections\gusset-outlines.csv',encoding='utf-8')) if r['structure'].startswith(st) and r['joint']==jt]
    r=rows[0]; pg=int(r['page'])
    pts=[tuple(map(float,p.split(','))) for p in r['outline_px'].split(';')]
    x0=min(p[0] for p in pts)-20; x1=max(p[0] for p in pts)+20; y0=min(p[1] for p in pts)-20; y1=max(p[1] for p in pts)+20
    k=72/150; s=DPI/150
    d=pymupdf.open(r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf')
    pm=d[pg-1].get_pixmap(matrix=pymupdf.Matrix(DPI/72,DPI/72),clip=pymupdf.Rect(x0*k,y0*k,x1*k,y1*k),colorspace=pymupdf.csGRAY)
    a=np.frombuffer(pm.samples,dtype=np.uint8).reshape(pm.height,pm.width)
    dark=a<140
    poly=[((x-x0)*s,(y-y0)*s) for x,y in pts]
    mask=Image.new('L',(pm.width,pm.height),0); ImageDraw.Draw(mask).polygon(poly,fill=1); mask=np.array(mask,bool)
    R0=3
    yy,xx=np.mgrid[-R0:R0+1,-R0:R0+1]; disk=(xx*xx+yy*yy)<=R0*R0
    O=ndi.binary_opening(dark,structure=disk)
    lab,n=ndi.label(O); dots=[]
    for i,sl in enumerate(ndi.find_objects(lab),1):
        h=sl[0].stop-sl[0].start; w=sl[1].stop-sl[1].start; area=(lab[sl]==i).sum()
        cy=(sl[0].start+sl[0].stop)/2; cx=(sl[1].start+sl[1].stop)/2
        if mask[int(cy),int(cx)] and 6<=h<=22 and 6<=w<=22 and area>=30: dots.append((cx,cy))
    H=ndi.binary_fill_holes(dark)&~dark
    lab2,n2=ndi.label(H); pieces=[]
    for i,sl in enumerate(ndi.find_objects(lab2),1):
        h=sl[0].stop-sl[0].start; w=sl[1].stop-sl[1].start; area=(lab2[sl]==i).sum()
        cy=(sl[0].start+sl[0].stop)/2; cx=(sl[1].start+sl[1].stop)/2
        if mask[int(cy),int(cx)] and h<=22 and w<=22 and area>=12: pieces.append((cx,cy,area))
    # group quadrant pieces of crossed circles (centres within 9 px) and drop pieces inside a detected dot
    rings=[]
    for cx,cy,ar in pieces:
        if any((cx-dx)**2+(cy-dy)**2<40 for dx,dy in dots): continue
        for g in rings:
            if (g[0]-cx)**2+(g[1]-cy)**2<110: g[2]+=1; g[0]=(g[0]*g[2]+cx)/(g[2]+1); break
        else: rings.append([cx,cy,0])
    if not quiet: print(jt,'dots',len(dots),'rings',len(rings),'total',len(dots)+len(rings))
    if out:
        im=Image.fromarray(np.where(dark,0,255).astype(np.uint8)).convert('RGB'); dr=ImageDraw.Draw(im)
        dr.polygon(poly,outline=(0,160,0))
        for cx,cy in dots: dr.ellipse([cx-10,cy-10,cx+10,cy+10],outline=(255,0,0),width=2)
        for cx,cy,_ in rings: dr.ellipse([cx-10,cy-10,cx+10,cy+10],outline=(0,0,255),width=2)
        im.save(out)
    return len(dots),len(rings)
if __name__=='__main__': run(st,jt,out)
