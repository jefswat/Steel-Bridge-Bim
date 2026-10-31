import sys,json; sys.path.insert(0,r"C:\SBM\qc\nebraska-st\tools")
from qclib import *
import numpy as np
T=json.load(open(BASE+"/extract/top_of_deck.json"))
deck=els("category='deck slab'")[0]['global_id']
v=verts_ft(deck); f=geom()[deck][3]
tri=v[f]  # (n,3,3) grid ft
print("deck tris",len(tri))
# precompute 2D grid for speed: bin triangles
a=tri[:,0,:2];b=tri[:,1,:2];c=tri[:,2,:2]
def top_z(E,N):
    # point-in-triangle in xy, barycentric, return max z
    p=np.array([E,N])
    # prefilter by bbox
    mn=np.minimum(np.minimum(a,b),c); mx=np.maximum(np.maximum(a,b),c)
    m=(p[0]>=mn[:,0]-1e-9)&(p[0]<=mx[:,0]+1e-9)&(p[1]>=mn[:,1]-1e-9)&(p[1]<=mx[:,1]+1e-9)
    idx=np.where(m)[0]; zs=[]
    for i in idx:
        A,B,C=tri[i]
        d=(B[1]-C[1])*(A[0]-C[0])+(C[0]-B[0])*(A[1]-C[1])
        if abs(d)<1e-12: continue
        l1=((B[1]-C[1])*(E-C[0])+(C[0]-B[0])*(N-C[1]))/d
        l2=((C[1]-A[1])*(E-C[0])+(A[0]-C[0])*(N-C[1]))/d
        l3=1-l1-l2
        if l1>=-1e-9 and l2>=-1e-9 and l3>=-1e-9: zs.append(l1*A[2]+l2*B[2]+l3*C[2])
    return max(zs) if zs else None
bear={}
for g in OFFS:
    r=els("category='bearing disc' and support='S. Abut.' and girder=?",g)[0]
    lo,hi=ubox([r['global_id']]); c0=(lo+hi)/2; bear[g]=garc(g,c0[0],c0[1])[0]
gpts={}
for gi,g in zip("ABCD",OFFS):
    m=json.loads(els("category='girder' and name=?",g)[0]['properties'])['camber_table'].split(";")
    sts,P,cum=gpath(g); L=[]
    for row in m:
        r=row.split(","); s=bear[g]+float(r[1])-1.0
        i=int(np.abs(cum-s).argmin()); L.append(P[i]+np.array([E0,N0]))
    gpts[gi]=np.array(L)
offs=[OFFS[g] for g in OFFS]
lines={"LEFT EDGE OF DECK":-21.5833,"LEFT GUTTER LINE":-20.0,"GIRDER LINE A":-18.5,"GIRDER LINE B":-10.8333333,"GIRDER LINE C":-3.1666667,"CL NEBRASKA ST RAMP B":0.0,"GIRDER LINE D":4.5,"RIGHT GUTTER LINE":6.0,"RIGHT EDGE OF DECK":7.58}
def pt(o,k):
    P=[gpts[g][k] for g in "ABCD"]
    if o<=offs[1]: i=0
    elif o>=offs[2]: i=2
    else: i=1 if o<offs[2] else 2
    if o<=offs[1]: i=0
    elif o<=offs[2]: i=1
    else: i=2
    t=(o-offs[i])/(offs[i+1]-offs[i]); return P[i]+t*(P[i+1]-P[i])
res=[]
for name,o in lines.items():
    for k in range(67):
        E,N=pt(o,k)
        z=top_z(E,N); plan=T[name][k]
        res.append((name,k+1,plan,z,None if z is None else z-plan))
json.dump(res,open(BASE+"/extract/deck_top_compare.json","w"))
import collections
for name in lines:
    d=[r[4] for r in res if r[0]==name and r[4] is not None]
    print(name,len(d),"mean %+.4f max %+.4f min %+.4f"%(np.mean(d),max(d),min(d)), "none",sum(1 for r in res if r[0]==name and r[4] is None))
