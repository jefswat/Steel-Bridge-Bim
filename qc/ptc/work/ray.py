import numpy as np
def vray(P,T,x,y):
    """vertical ray at (x,y): return sorted z hits (P: n x 3, T: m x 3)"""
    A=P[T[:,0]];B=P[T[:,1]];C=P[T[:,2]]
    # 2D barycentric in xy
    d=(B[:,1]-C[:,1])*(A[:,0]-C[:,0])+(C[:,0]-B[:,0])*(A[:,1]-C[:,1])
    ok=np.abs(d)>1e-12
    l1=((B[:,1]-C[:,1])*(x-C[:,0])+(C[:,0]-B[:,0])*(y-C[:,1]))/np.where(ok,d,1)
    l2=((C[:,1]-A[:,1])*(x-C[:,0])+(A[:,0]-C[:,0])*(y-C[:,1]))/np.where(ok,d,1)
    l3=1-l1-l2
    m=ok&(l1>=-1e-9)&(l2>=-1e-9)&(l3>=-1e-9)
    z=l1[m]*A[m,2]+l2[m]*B[m,2]+l3[m]*C[m,2]
    return np.unique(np.round(z,4))
