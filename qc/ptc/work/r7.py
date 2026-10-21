import ifcopenshell, ifcopenshell.geom as G, sqlite3, numpy as np, csv, collections, re
from scipy.spatial import Delaunay
FT=1/0.3048
O=np.array([70.4888176,-113.8711093])*0.3048
ang=-0.3169866987
u=np.array([np.cos(ang),np.sin(ang)]); n=np.array([-u[1],u[0]]); S0=63800.0
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
s=G.settings(); s.set('use-world-coords',True)
R=collections.defaultdict(list)
for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\rivets2.csv')): R[r['bridge']].append((float(r['s']),float(r['o']),float(r['z'])))
for k in R: R[k]=np.array(R[k])
out=csv.writer(open(r'C:\SBM\qc\ptc\work\r7.csv','w',newline='')); out.writerow(['bridge','mark','n_rivets_hull','area_sf'])
for br in ('PTC 3-span truss 11-13','PTC 4-span truss 14-17','PTC Arch truss 18-20'):
    A=R[br]
    for gid,mk,vol in c.execute("select global_id,mark,volume_m3 from elements where category='gusset plate' and bridge_group=? and mark like 'N-G%-L'",(br,)).fetchall():
        sh=G.create_shape(s,f.by_guid(gid)); v=np.array(sh.geometry.verts).reshape(-1,3)
        P=np.column_stack([(v[:,:2]-O)@u*FT+S0,(v[:,:2]-O)@n*FT,v[:,2]*FT])
        sel=(A[:,1]>=P[:,1].min()-0.3)&(A[:,1]<=P[:,1].max()+0.3)&(A[:,0]>=P[:,0].min())&(A[:,0]<=P[:,0].max())&(A[:,2]>=P[:,2].min())&(A[:,2]<=P[:,2].max())
        cand=A[sel]
        if len(cand):
            H=Delaunay(P[:,[0,2]]); cand=cand[H.find_simplex(cand[:,[0,2]])>=0]
        out.writerow([br,mk,len(cand),round(vol/0.3048**3,2)])
print('done')
