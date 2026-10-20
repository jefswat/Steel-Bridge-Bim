import ifcopenshell, ifcopenshell.geom as G, sqlite3, numpy as np, pickle, sys, time
cats=sys.argv[2].split(',')
out=sys.argv[1]
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
rows={}
q=','.join('?'*len(cats))
for gid,cat,name,mark,br in c.execute(f'select global_id,category,name,mark,bridge_group from elements where category in ({q})',cats): rows[gid]=(cat,name,mark,br)
print(len(rows),flush=True)
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
FT=1/0.3048
O=np.array([70.4888176,-113.8711093])*0.3048
ang=-0.3169866987
u=np.array([np.cos(ang),np.sin(ang)]); n=np.array([-u[1],u[0]])
S0=63800.0
inc=[]
for gid in rows:
    try: inc.append(f.by_guid(gid))
    except Exception: pass
s=G.settings(); s.set('use-world-coords',True)
it=G.iterator(s,f,4,include=inc)
res={}
if it.initialize():
    while True:
        sh=it.get()
        v=np.array(sh.geometry.verts).reshape(-1,3)
        t=np.array(sh.geometry.faces).reshape(-1,3)
        P=np.column_stack([ (v[:,:2]-O)@u*FT+S0, (v[:,:2]-O)@n*FT, v[:,2]*FT])
        res[sh.guid]=(rows.get(sh.guid),P.astype(np.float32),t.astype(np.int32))
        if not it.next(): break
pickle.dump(res,open(out,'wb')); print('done',len(res))
