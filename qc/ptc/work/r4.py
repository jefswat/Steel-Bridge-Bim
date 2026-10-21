import ifcopenshell, ifcopenshell.geom as G, sqlite3, numpy as np, csv, collections, sys, json
from scipy.spatial import ConvexHull, Delaunay
FT=1/0.3048
O=np.array([70.4888176,-113.8711093])*0.3048
ang=-0.3169866987
u=np.array([np.cos(ang),np.sin(ang)]); n=np.array([-u[1],u[0]]); S0=63800.0
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
s=G.settings(); s.set('use-world-coords',True)
R=[(r['bridge'],float(r['s']),float(r['o']),float(r['z'])) for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\rivets2.csv'))]
BR={'approach3':'PTC 3-span truss 11-13','approach4':'PTC 4-span truss 14-17','main':'PTC Arch truss 18-20'}
jobs=[a.split(':') for a in sys.argv[1:]]   # struct:joint
res={}
for st,j in jobs:
    br=BR[st]; mk='N-G'+j+'-L'
    gid=c.execute("select global_id from elements where category='gusset plate' and mark=? and bridge_group=?",(mk,br)).fetchone()[0]
    sh=G.create_shape(s,f.by_guid(gid)); v=np.array(sh.geometry.verts).reshape(-1,3)
    P=np.column_stack([(v[:,:2]-O)@u*FT+S0,(v[:,:2]-O)@n*FT,v[:,2]*FT])
    H=Delaunay(P[:,[0,2]])
    omin,omax=P[:,1].min(),P[:,1].max()
    sel=[(r[1],r[2],r[3]) for r in R if r[0]==br and omin-1.0<=r[2]<=omax+1.0 and P[:,0].min()-0.5<=r[1]<=P[:,0].max()+0.5 and P[:,2].min()-0.5<=r[3]<=P[:,2].max()+0.5]
    sel=[x for x in sel if H.find_simplex([x[0],x[2]])>=0]
    cl=collections.Counter(round(x[1],2) for x in sel)
    res[st+':'+j]=dict(clusters=dict(cl),total=len(sel))
    print(st,j,mk,'plate o %.2f..%.2f'%(omin,omax),'rivets in hull',len(sel),dict(cl))
json.dump(res,open(r'C:\SBM\qc\ptc\work\r4.json','w'))
