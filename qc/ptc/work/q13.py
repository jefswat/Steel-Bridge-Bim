import ifcopenshell, ifcopenshell.geom as G, sqlite3, numpy as np, collections
FT=1/0.3048
O=np.array([70.4888176,-113.8711093])*0.3048
ang=-0.3169866987
u=np.array([np.cos(ang),np.sin(ang)]); n=np.array([-u[1],u[0]]); S0=63800.0
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
s=G.settings(); s.set('use-world-coords',True)
for br,nm in (('PTC 3-span truss 11-13','walkway CL posts L b'),('PTC 3-span truss 11-13','walkway CL posts L a'),('PTC Arch truss 18-20','walkway CL W15-W15\' posts L b'),('PTC 4-span truss 14-17','walkway CL posts L b')):
    r=c.execute("select global_id from elements where name like ? and bridge_group=?",('%'+nm,br)).fetchone()
    if not r: print('none',nm); continue
    sh=G.create_shape(s,f.by_guid(r[0])); v=np.array(sh.geometry.verts).reshape(-1,3)
    tr=np.array(sh.geometry.faces).reshape(-1,3)
    P=np.column_stack([(v[:,:2]-O)@u*FT+S0,(v[:,:2]-O)@n*FT,v[:,2]*FT])
    # connected components of triangles = posts
    parent=list(range(len(P)))
    def fd(a):
        while parent[a]!=a: parent[a]=parent[parent[a]]; a=parent[a]
        return a
    for t in tr:
        a,b,cc=map(fd,t); parent[b]=a; parent[fd(cc)]=a
    comp=collections.defaultdict(list)
    for i in range(len(P)): comp[fd(i)].append(i)
    cen=sorted(P[idx,0].mean() for idx in comp.values())
    d=np.diff(cen)
    print(br,nm,'components',len(cen),'s %.2f..%.2f'%(cen[0],cen[-1]),'spacing',collections.Counter(np.round(d,2)).most_common(5))
