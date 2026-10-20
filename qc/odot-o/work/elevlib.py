import sys,json; sys.path.insert(0,'.'); from qc import *
E=load_el(); M={r['mark']:r for r in E if r['mark']}
F=json.load(open('girder_feat.json'))
dv,df=geom(M['DECK']['guid'])
def ztop(x,y,mesh=(dv,df),which=max):
    v,f=mesh; a,b,c=v[f[:,0]],v[f[:,1]],v[f[:,2]]
    # barycentric in xy
    d=(b[:,1]-c[:,1])*(a[:,0]-c[:,0])+(c[:,0]-b[:,0])*(a[:,1]-c[:,1])
    ok=np.abs(d)>1e-12
    l1=((b[:,1]-c[:,1])*(x-c[:,0])+(c[:,0]-b[:,0])*(y-c[:,1]))/np.where(ok,d,1)
    l2=((c[:,1]-a[:,1])*(x-c[:,0])+(a[:,0]-c[:,0])*(y-c[:,1]))/np.where(ok,d,1)
    l3=1-l1-l2; m=ok&(l1>=-1e-9)&(l2>=-1e-9)&(l3>=-1e-9)
    z=l1*a[:,2]+l2*b[:,2]+l3*c[:,2]
    return which(z[m]) if m.any() else None
