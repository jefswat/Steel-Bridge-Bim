import sys; sys.path.insert(0,'.'); from qc import *
E=load_el(); M={r['mark']:r for r in E if r['mark']}
def outline(mark):
    v,f=geom(M[mark]['guid']); c=v.mean(0); w,U=np.linalg.eigh(np.cov((v-c).T)); n=U[:,0]
    p=(v-c)@U[:,1:]  # in-plane coords
    t=(v-c)@n
    # unique 2D pts on one face
    top=p[t>t.mean()]
    pts=np.unique(np.round(top*12,3),axis=0)
    # convex hull (monotone chain)
    pts=sorted(map(tuple,pts))
    def cross(o,a,b): return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    lo=[];hi=[]
    for q in pts:
        while len(lo)>=2 and cross(lo[-2],lo[-1],q)<=1e-6: lo.pop()
        lo.append(q)
    for q in reversed(pts):
        while len(hi)>=2 and cross(hi[-2],hi[-1],q)<=1e-6: hi.pop()
        hi.append(q)
    h=lo[:-1]+hi[:-1]
    edges=[(h[i],h[(i+1)%len(h)]) for i in range(len(h))]
    return h,[round(math.dist(a,b),3) for a,b in edges], round((t.max()-t.min())*12,3)
for m in sys.argv[1:]:
    h,e,th=outline(m); print(m,'thk',th,'edges(in)',e)
