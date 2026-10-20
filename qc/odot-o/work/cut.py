import numpy as np, math
from qc import *
from PIL import Image, ImageDraw, ImageFont
def plane_at_sta(sta, skew_deg=0.0):
    p,t=align_pt(sta); n2=np.array([t[0],t[1]])
    if skew_deg:
        a=math.radians(skew_deg); c,s=math.cos(a),math.sin(a); n2=np.array([c*t[0]-s*t[1], s*t[0]+c*t[1]])
    return np.array([p[0],p[1],0.0]), np.array([n2[0],n2[1],0.0])
def cut(E, P, n, filt=lambda r: True, pad=0.0):
    n=n/np.linalg.norm(n); u=np.array([-n[1],n[0],0.0])  # u = left of normal direction
    out={}
    for r in E:
        if not r.get('xmin') or not filt(r): continue
        corners=np.array([[x,y] for x in (r['xmin'],r['xmax']) for y in (r['ymin'],r['ymax'])])
        d=(corners-P[:2])@n[:2]
        if d.min()>0 or d.max()<0: continue
        v,f=geom(r['guid']); dv=(v-P)@n
        segs=[]
        tri=dv[f]; m=(tri.min(1)<0)&(tri.max(1)>0)
        for fi in np.where(m)[0]:
            ids=f[fi]; pts=[]
            for a,b in ((0,1),(1,2),(2,0)):
                da,db=dv[ids[a]],dv[ids[b]]
                if (da<0)!=(db<0):
                    t=da/(da-db); q=v[ids[a]]+t*(v[ids[b]]-v[ids[a]]); pts.append(((q-P)@u, q[2]))
            if len(pts)==2: segs.append(pts)
        if segs: out[r['guid']]=(r,segs)
    return out
def draw(out, fn, title='', scale=None, labels=True, extra=None):
    allp=np.array([p for r,s in out.values() for seg in s for p in seg])
    umin,vmin=allp.min(0); umax,vmax=allp.max(0)
    W=1800; m=60
    sc=scale or (W-2*m)/max(umax-umin,1e-6)
    H=int((vmax-vmin)*sc+2*m+40)
    im=Image.new('RGB',(W,H),'white'); d=ImageDraw.Draw(im)
    X=lambda uu: m+(umax-uu)*sc   # looking along station: left offset drawn on the left -> flip
    Y=lambda vv: H-m-(vv-vmin)*sc
    cols=[(200,0,0),(0,0,200),(0,130,0),(150,0,150),(0,120,120),(160,90,0)]
    for i,(g,(r,segs)) in enumerate(out.items()):
        c=cols[i%len(cols)]
        for (a,b) in segs: d.line([X(a[0]),Y(a[1]),X(b[0]),Y(b[1])],fill=c,width=2)
    d.text((10,10),title,fill='black')
    if extra: extra(d,X,Y)
    im.save(fn); return sc
