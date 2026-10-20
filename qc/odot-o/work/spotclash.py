import sys,csv; sys.path.insert(0,'.'); from qc import *
E=load_el(); M={}
for r in E:
    if r['mark']: M[r['mark']]=r
    if r['name']: M.setdefault(r['name'],r)
def inside(pts,v,f):
    a,b,c=v[f[:,0]],v[f[:,1]],v[f[:,2]]; e1=b-a; e2=c-a
    d=np.array([0.5773,0.5774,0.5772]); h=np.cross(d,e2); det=(e1*h).sum(1); ok=np.abs(det)>1e-12
    res=[]
    for p in pts:
        s=p-a; u=(s*h).sum(1)/np.where(ok,det,1); q=np.cross(s,e1); vv=(q*d).sum(1)/np.where(ok,det,1); t=(e2*q).sum(1)/np.where(ok,det,1)
        hit=ok&(u>=0)&(vv>=0)&(u+vv<=1)&(t>1e-9); res.append(hit.sum()%2==1)
    return np.array(res)
def pair(A,B,n=4000,seed=1):
    va,fa=geom(M[A]['guid']); vb,fb=geom(M[B]['guid'])
    lo=np.maximum(va.min(0),vb.min(0)); hi=np.minimum(va.max(0),vb.max(0))
    if (hi<=lo).any(): return 0.0,0.0
    rng=np.random.default_rng(seed); P=lo+rng.random((n,3))*(hi-lo)
    ia=inside(P,va,fa); P2=P[ia]; ib=inside(P2,vb,fb) if len(P2) else np.array([],bool)
    vol=np.prod(hi-lo)*ib.sum()/n*1728
    # depth estimate: max over overlapping sample of distance to nearest B vertex-plane approx -> use min extent of overlap box of points
    Q=P2[ib]
    depth=np.min(np.ptp(Q,0))*12 if len(Q)>1 else 0.0
    return vol,depth
ab=[r for r in E if r['ptype']=='ANCHORBOLT']; pad=M['B2-G1-PAD']
ab.sort(key=lambda r:(r['cx']-pad['cx'])**2+(r['cy']-pad['cy'])**2); M['AB_near_B2-G1']=ab[0]
pairs=[('G1-1-TFL1','G1-SD1-TF'),('G1-LAT1-L1','G1-1-TFR1'),('AB_near_B2-G1','G1-2-BF2'),('XD1-W','G1-1-WL1'),('G1-K1.2-TS','G1-1-TFL1'),('XSD1-1-CL1','G1-1-WR1'),('G1-SD1-BS1','G1-1-BF1')]
out=[]
for A,B in pairs:
    if A not in M or B not in M: print('missing',A,B); continue
    vol,dep=pair(A,B); print(A,B,'overlap vol %.3f in3, overlap-box min extent %.3f in'%(vol,dep)); out.append((A,B,round(vol,3),round(dep,3)))
with open('../results/clash_spotcheck_ifcopenshell.csv','w',newline='') as f:
    w=csv.writer(f); w.writerow(['element_a','element_b','overlap_volume_in3_montecarlo','overlap_min_extent_in']); w.writerows(out)
