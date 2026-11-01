# Footing concrete volume from the plan outline + hip top (level over the 16'x11' column base, planes down to the edges), bottom EL -0.50
import numpy as np
from matplotlib.path import Path
def vol(poly, ztop_cl, ztop_edge, bot=-0.5, pile_n=0, emb=4.0, step=0.05):
    P=Path(poly); xs=np.arange(min(p[0] for p in poly),max(p[0] for p in poly),step)+step/2
    ys=np.arange(min(p[1] for p in poly),max(p[1] for p in poly),step)+step/2
    X,Y=np.meshgrid(xs,ys); inside=P.contains_points(np.c_[X.ravel(),Y.ravel()]).reshape(X.shape)
    # top: per outer edge plane facet; take the elevation as min over the edge planes (hip = lower envelope)
    z=np.full(X.shape,ztop_cl)
    n=len(poly)
    for i in range(n):
        a=np.array(poly[i]);b=np.array(poly[(i+1)%n]); t=b-a; nrm=np.array([t[1],-t[0]])/np.hypot(*t)
        c=np.array([0,0]); 
        if np.dot(c-a,nrm)>0: nrm=-nrm   # outward
        d_edge=np.dot(-a,nrm)*-1  # distance of edge from origin along outward normal
        dist_edge=np.dot(a,nrm)
        # inner line: base rectangle support in this direction
        h=8*abs(nrm[0])+5.5*abs(nrm[1])
        s=(X*nrm[0]+Y*nrm[1]-h)/(dist_edge-h)
        zz=ztop_cl-(ztop_cl-ztop_edge)*np.clip(s,0,None)
        z=np.minimum(z,zz)
    v=((z-bot)*inside).sum()*step*step
    return v/27, v/27 - pile_n*9*emb/27
rect=lambda a,b:[(-a/2,-b/2),(a/2,-b/2),(a/2,b/2),(-a/2,b/2)]
octo=[(-10.5,-29.25),(10.5,-29.25),(25.5,-8),(25.5,8),(10.5,29.25),(-10.5,29.25),(-25.5,8),(-25.5,-8)]
for name,poly,zc,ze,bom,np_ in [("18/29",rect(36,36),6.0,5.75,282.4,18),("19",rect(43.5,36),6.0,5.73,338.1,20),("26-28",rect(43.5,36),6.0,5.73,338.1,24),("20-22",rect(51,36),7.0,6.65,467.2,24),("25",rect(51,36),7.0,6.65,467.2,24),("23/24",octo,7.5,7.03,631.6,30)]:
    g,net=vol(poly,zc,ze,pile_n=np_)
    print(name,"gross %.1f CY, minus %d piles x 4ft embedded %.1f CY, BOM %.1f"%(g,np_,net,bom))
