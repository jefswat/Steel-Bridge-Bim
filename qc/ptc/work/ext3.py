import ifcopenshell, ifcopenshell.geom as G, ifcopenshell.util.placement as P, numpy as np, csv, time, sqlite3
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
FT=1/0.3048
O=np.array([70.4888176,-113.8711093])*0.3048
ang=-0.3169866987
u=np.array([np.cos(ang),np.sin(ang)]); n=np.array([-u[1],u[0]]); S0=63800.0
db=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
info={r[0]:r[1:] for r in db.execute('select global_id,category,name,mark,bridge_group,material,volume_m3,length_m,parent_global_id from elements')}
def tosof(P3): return np.column_stack([(P3[:,:2]-O)@u*FT+S0,(P3[:,:2]-O)@n*FT,P3[:,2]*FT])
# rivets by placement
t=time.time()
out=open('rivets.csv','w',newline=''); w=csv.writer(out); w.writerow(['guid','bridge','parent','s','o','z','type'])
nr=0
for e in f.by_type('IfcMechanicalFastener'):
    if e.PredefinedType!='RIVET' and e.Name!='Rivet': continue
    M=P.get_local_placement(e.ObjectPlacement)
    p=np.array([[M[0,3],M[1,3],M[2,3]]]); q=tosof(p)[0]
    i=info.get(e.GlobalId,('?',)*8)
    w.writerow([e.GlobalId,i[3],i[7],round(q[0],3),round(q[1],3),round(q[2],3),e.ObjectType or ''])
    nr+=1
out.close(); print('rivets',nr,time.time()-t,flush=True)
inc=[e for e in f.by_type('IfcProduct') if e.is_a() in ('IfcMember','IfcPlate','IfcBeam','IfcBearing','IfcColumn','IfcFooting','IfcSlab','IfcWall','IfcRailing') ]
inc+= [e for e in f.by_type('IfcMechanicalFastener') if e.PredefinedType!='RIVET']
print(len(inc),flush=True)
s=G.settings(); s.set('use-world-coords',True)
it=G.iterator(s,f,4,include=inc)
out=open('geom3.csv','w',newline=''); w=csv.writer(out)
w.writerow(['guid','bridge','cat','name','mark','ifcname','desc','smin','smax','omin','omax','zmin','zmax','e1','e2','e3','vol','cs','co','cz','nv'])
k=0
if it.initialize():
    while True:
        sh=it.get(); gid=sh.guid
        v=np.array(sh.geometry.verts).reshape(-1,3)
        if len(v):
            P3=tosof(v); mn=P3.min(0); mx=P3.max(0); c=P3.mean(0)
            X=P3-c; ev=np.linalg.eigh(X.T@X)[1]
            ex=sorted([float(np.ptp(X@ev[:,j])) for j in range(3)])
            i=info.get(gid,('?',)*8); ent=f.by_guid(gid)
            w.writerow([gid,i[3],i[0],i[1],i[2],ent.Name,ent.Description or '']+[round(float(x),4) for x in (mn[0],mx[0],mn[1],mx[1],mn[2],mx[2])]+[round(x,4) for x in ex]+[i[5],round(c[0],3),round(c[1],3),round(c[2],3),len(v)])
        k+=1
        if k%2000==0: print(k,time.time()-t,flush=True); out.flush()
        if not it.next(): break
out.close(); print('done',k,time.time()-t)
