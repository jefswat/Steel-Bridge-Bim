import numpy as np, ifcopenshell, ifcopenshell.geom as G
FT=1/0.3048
_f=None
def ifc():
    global _f
    if _f is None: _f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
    return _f
_s=G.settings(); _s.set('use-world-coords',True)
def mesh(guid):
    sh=G.create_shape(_s,ifc().by_guid(guid))
    v=np.array(sh.geometry.verts).reshape(-1,3); t=np.array(sh.geometry.faces).reshape(-1,3)
    return v,t
def axis_of(v):
    c=v.mean(0); X=v-c; w,V=np.linalg.eigh(X.T@X); return c,V[:,-1],V
def area_at(v,t,c,ax,s):
    """cross-section area (m2) of closed mesh at plane ax.(x-c)=s"""
    d=(v-c)@ax-s
    a=d[t]  # (n,3)
    cross=(a.min(1)<0)&(a.max(1)>0)
    tt=t[cross]
    P=v[tt]; dd=d[tt]
    # in-plane basis
    e1=np.cross(ax,[0,0,1.0]); 
    if np.linalg.norm(e1)<1e-6: e1=np.cross(ax,[1.0,0,0])
    e1/=np.linalg.norm(e1); e2=np.cross(ax,e1)
    tot=0.0
    for p,dv in zip(P,dd):
        pts=[]
        for i,j in ((0,1),(1,2),(2,0)):
            if (dv[i]<0)!=(dv[j]<0):
                k=dv[i]/(dv[i]-dv[j]); pts.append(p[i]+k*(p[j]-p[i]))
        if len(pts)!=2: continue
        nt=np.cross(p[1]-p[0],p[2]-p[0])
        A,B=pts
        if np.dot(np.cross(ax,nt),B-A)<0: A,B=B,A
        a1=(A@e1,A@e2); b1=(B@e1,B@e2)
        tot+=a1[0]*b1[1]-a1[1]*b1[0]
    return abs(tot)/2
def profile(guid,fracs=(0.5,)):
    v,t=mesh(guid); c,ax,V=axis_of(v)
    tt=(v-c)@ax; t0,t1=tt.min(),tt.max()
    return [(f,area_at(v,t,c,ax,t0+f*(t1-t0))*FT*FT*144) for f in fracs], (t1-t0)*FT
if __name__=='__main__':
    import sys
    for g in sys.argv[1:]: print(g,profile(g,(0.1,0.25,0.5,0.75,0.9)))
