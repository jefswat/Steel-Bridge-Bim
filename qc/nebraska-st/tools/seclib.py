import sys; sys.path.insert(0,r"C:\SBM\qc\nebraska-st\tools")
from qclib import *
import numpy as np, math
from shapely.geometry import LineString, Polygon, MultiPolygon, box
from shapely.ops import unary_union, polygonize
from PIL import Image, ImageDraw, ImageFont

def plane_frame(sta, skew_deg=0.0, off=0.0):
    """origin at alignment point (sta, offset off); u axis = cross-section direction pointing RIGHT of alignment (rotated by skew)"""
    x,y,th=align_pt(sta)
    a=th-math.pi/2+math.radians(skew_deg)   # direction pointing right
    # point at offset: right normal
    px=x+off*math.sin(th); py=y-off*math.cos(th)
    return np.array([px+E0,py+N0]),np.array([math.cos(a),math.sin(a)])
def cut_segments(gids,p0,u):
    """intersect triangle meshes with the vertical plane through p0 containing direction u. returns list of segments ((s1,z1),(s2,z2)) where s = signed dist along u"""
    n=np.array([-u[1],u[0]]); segs=[]
    for g in gids:
        if g not in geom(): continue
        v=verts_ft(g); f=geom()[g][3]
        d=(v[:,:2]-p0)@n
        tri=f; dd=d[tri]
        mask=(dd.min(1)<0)&(dd.max(1)>0)
        for t in tri[mask]:
            pts=[]
            for i in range(3):
                a,b=t[i],t[(i+1)%3]
                da,db=d[a],d[b]
                if (da<0)!=(db<0):
                    k=da/(da-db); p=v[a]+k*(v[b]-v[a]); pts.append(((p[:2]-p0)@u,p[2]))
            if len(pts)==2: segs.append((pts[0],pts[1]))
    return segs
def polys(segs):
    lines=[LineString([a,b]) for a,b in segs if a!=b]
    if not lines: return []
    u=unary_union(lines); return list(polygonize(u))
def draw(polys_by_color,fn,scale=24,pad=1.0,annotations=None,title="",bounds=None):
    allp=[p for ps in polys_by_color.values() for p in ps[0]]
    if bounds is None:
        U=unary_union(allp); minx,miny,maxx,maxy=U.bounds
    else: minx,miny,maxx,maxy=bounds
    W=int((maxx-minx+2*pad)*scale); H=int((maxy-miny+2*pad)*scale)+30
    im=Image.new("RGB",(W,H),"white"); dr=ImageDraw.Draw(im)
    def P(s,z): return ((s-minx+pad)*scale, H-30-(z-miny+pad)*scale)
    for name,(ps,col) in polys_by_color.items():
        for p in ps:
            dr.polygon([P(*c) for c in p.exterior.coords],fill=col,outline="black")
            for h in p.interiors: dr.polygon([P(*c) for c in h.coords],fill="white",outline="black")
    if annotations:
        for a in annotations:
            if a[0]=='dim':
                (s1,z1),(s2,z2),txt=a[1],a[2],a[3]; dr.line([P(s1,z1),P(s2,z2)],fill=(200,0,0),width=1); m=P((s1+s2)/2,(z1+z2)/2); dr.text((m[0]+3,m[1]-12),txt,fill=(200,0,0))
    dr.text((5,H-22),title,fill="black")
    im.save(fn); return fn
