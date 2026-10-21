import ifcopenshell, ifcopenshell.util.placement as P, numpy as np, csv, sqlite3, time
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
FT=1/0.3048; UF=0.3048
O=np.array([70.4888176,-113.8711093])*0.3048
ang=-0.3169866987
u=np.array([np.cos(ang),np.sin(ang)]); n=np.array([-u[1],u[0]]); S0=63800.0
db=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
info={r[0]:r[1:] for r in db.execute('select global_id,bridge_group,parent_global_id,name from elements')}
out=csv.writer(open(r'C:\SBM\qc\ptc\work\rivets2.csv','w',newline='')); out.writerow(['guid','bridge','parent','s','o','z'])
t=time.time(); k=0
for e in f.by_type('IfcMechanicalFastener'):
    if e.PredefinedType!='RIVET' and e.Name!='Rivet': continue
    M=P.get_local_placement(e.ObjectPlacement)
    x,y,z=M[0,3]*UF,M[1,3]*UF,M[2,3]*UF
    s=((x-O[0])*u[0]+(y-O[1])*u[1])*FT+S0; o=((x-O[0])*n[0]+(y-O[1])*n[1])*FT
    i=info.get(e.GlobalId,('?','?','?'))
    out.writerow([e.GlobalId,i[0],i[1],round(s,3),round(o,3),round(z*FT,3)]); k+=1
print(k,time.time()-t)
