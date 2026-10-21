import ifcopenshell, ifcopenshell.geom as G, sqlite3, numpy as np
FT=1/0.3048
O=np.array([70.4888176,-113.8711093])*0.3048
ang=-0.3169866987
u=np.array([np.cos(ang),np.sin(ang)]); n=np.array([-u[1],u[0]]); S0=63800.0
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
s=G.settings(); s.set('use-world-coords',True)
for nm in ('dam Pier 20 teeth back','dam Pier 20 teeth ahead','dam W22 teeth back','dam W22 teeth ahead','dam Pier 20 plate back'):
    gid=c.execute("select global_id from elements where mark=? and bridge_group='PTC Arch truss 18-20'",(nm,)).fetchone()[0]
    sh=G.create_shape(s,f.by_guid(gid)); v=np.array(sh.geometry.verts).reshape(-1,3)
    P=np.column_stack([(v[:,:2]-O)@u*FT+S0,(v[:,:2]-O)@n*FT,v[:,2]*FT])
    # vertices on the far tooth tip line: those with max s (for 'back' teeth tips toward the gap)
    for side,sel in (('smax',P[:,0]>P[:,0].max()-0.01),('smin',P[:,0]<P[:,0].min()+0.01)):
        o=np.unique(np.round(P[sel][:,1],3))
        print(nm,side,'n vertices',sel.sum(),'unique o',len(o))
    o=np.sort(np.unique(np.round(P[:,1],3)))
    d=np.diff(o);
    import collections
    print(' o spacings (in):',collections.Counter(np.round(d*12,2)).most_common(6))
    print(' s extent ft',P[:,0].min(),P[:,0].max(),'thk z in',(P[:,2].max()-P[:,2].min())*12)
