import sys,json,math,collections,time; sys.path.insert(0,r"C:\SBM\qc\nebraska-st\tools")
from qclib import *
t0=time.time()
G=geom()
cat={r['global_id']:(r['category'],r['name'],r['mark'],r['support'],r['girder']) for r in q("select global_id,category,name,mark,support,girder from elements")}
skip={'alignment','site','project','unit'}
gids=[g for g in G if cat.get(g,('',))[0] not in skip and cat[g][0] not in ('rebar','pile','footing','column','pedestal','pier cap','maskwall','backwall','deck slab','barrier','thin brick','form liner','bearing pad','pier cap haunch','wire mesh screen','expansion joint device')]
print(len(gids),"solids (steel + bearings)")
V={g:G[g][2]/FT for g in gids}; F={g:G[g][3] for g in gids}
lo={g:V[g].min(0) for g in gids}; hi={g:V[g].max(0) for g in gids}
TOL=1/16/12
# candidate pairs by sorting on x
order=sorted(gids,key=lambda g:lo[g][0])
pairs=[]
for i,a in enumerate(order):
    for b in order[i+1:]:
        if lo[b][0]>hi[a][0]-TOL: break
        if (lo[b][1]<hi[a][1]-TOL and lo[a][1]<hi[b][1]-TOL and lo[b][2]<hi[a][2]-TOL and lo[a][2]<hi[b][2]-TOL): pairs.append((a,b))
print(len(pairs),"candidate box pairs")
def inside(P,tri):
    """ray parity along a skewed direction, tri (n,3,3); returns bool array"""
    d=np.array([0.5773,0.3141,0.7523]); d/=np.linalg.norm(d)
    A=tri[:,0];B=tri[:,1];C=tri[:,2]; e1=B-A; e2=C-A
    h=np.cross(d,e2); det=(e1*h).sum(1); ok=np.abs(det)>1e-12
    inv=np.where(ok,1/np.where(ok,det,1),0)
    res=np.zeros(len(P),bool)
    for i,p in enumerate(P):
        s=p-A; u=(s*h).sum(1)*inv; qv=np.cross(s,e1); v=(qv*d).sum(1)*inv; t=(qv*e2).sum(1)*inv
        hit=ok&(u>=0)&(v>=0)&(u+v<=1)&(t>1e-9)
        res[i]=hit.sum()%2==1
    return res
def ptri_dist(p,tri):
    A=tri[:,0];B=tri[:,1];C=tri[:,2]
    n=np.cross(B-A,C-A); nn=np.linalg.norm(n,axis=1); ok=nn>1e-14
    n=n[ok]/nn[ok][:,None]; d=np.abs(((p-A[ok])*n).sum(1))
    return d
found=[]
for a,b in pairs:
    for X,Y in ((a,b),(b,a)):
        P=V[X]; m=(P>lo[Y]-1e-9).all(1)&(P<hi[Y]+1e-9).all(1)
        if not m.any(): continue
        Pc=P[m]; ins=inside(Pc,V[Y][F[Y]])
        if ins.any():
            depth=max(ptri_dist(p,V[Y][F[Y]]).min() for p in Pc[ins])
            if depth>TOL: found.append((X,Y,depth))
res={}
for X,Y,d in found:
    k=tuple(sorted((X,Y))); res[k]=max(res.get(k,0),d)
print("second engine pairs with a vertex inside the other solid deeper than 1/16in:",len(res),"time %.1f s"%(time.time()-t0))
rows=[]
for (a,b),d in res.items(): rows.append((cat[a][0]+" x "+cat[b][0],cat[a][2] or cat[a][1],cat[b][2] or cat[b][1],round(d*12,3)))
rows.sort()
import csv
with open(BASE+"/clash/clash_engine2_ifc.csv","w",newline="",encoding="utf8") as f:
    w=csv.writer(f); w.writerow(["pair","mark_a","mark_b","depth_in(vertex-in-solid, lower bound)"]); w.writerows(rows)
print(collections.Counter(r[0] for r in rows).most_common())
