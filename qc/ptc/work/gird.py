import ifcopenshell, ifcopenshell.geom as G, sqlite3, numpy as np, csv
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
rows=c.execute("select global_id,name,bridge_group from elements where category='plate girder' and name like 'plate girder N-%'").fetchall()
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
FT=1/0.3048; O=np.array([70.4888176,-113.8711093])*0.3048; ang=-0.3169866987
u=np.array([np.cos(ang),np.sin(ang)]);n=np.array([-u[1],u[0]])
s=G.settings();s.set('use-world-coords',True)
out=open('girder_prof.csv','w',newline='');w=csv.writer(out);w.writerow(['name','s','zmin','zmax','omin','omax'])
for gid,name,br in rows:
    sh=G.create_shape(s,f.by_guid(gid)); v=np.array(sh.geometry.verts).reshape(-1,3)
    S=(v[:,:2]-O)@u*FT+63800; Of=(v[:,:2]-O)@n*FT; Z=v[:,2]*FT
    ss=np.unique(np.round(S,2))
    # distinct stations where vertices exist
    for q in ss:
        m=np.abs(S-q)<0.005
        w.writerow([name,q,round(Z[m].min(),4),round(Z[m].max(),4),round(Of[m].min(),4),round(Of[m].max(),4)])
out.close()
