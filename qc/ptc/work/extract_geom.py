import ifcopenshell, ifcopenshell.geom as G, sqlite3, numpy as np, csv, sys, time
cats=('truss chord','truss diagonal','truss vertical','truss hanger','floor beam','plate girder','stringer','fixed shoe','expansion shoe','bearing plate','sole plate','masonry plate','pin','pier cap','column','footing','pedestal','backwall','wingwall','deck slab','barrier','gusset plate','truss bracing member','channel','intermediate stiffener','splice plate','filler plate','joint armor plate','suspender rope','suspender socket','grating')
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
rows={}
q=','.join('?'*len(cats))
for gid,cat,name,mark,br in c.execute(f'select global_id,category,name,mark,bridge_group from elements where category in ({q})',cats): rows[gid]=(cat,name,mark,br)
print(len(rows),flush=True)
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
FT=1/0.3048
O=np.array([70.4888176,-113.8711093])*0.3048  # alignment start (local, m) assuming IFC geom is metres
ang=-0.3169866987
u=np.array([np.cos(ang),np.sin(ang)]); n=np.array([-u[1],u[0]])  # left normal
S0=63800.0
inc=[]
miss=0
for gid in rows:
    try: inc.append(f.by_guid(gid))
    except Exception: miss+=1
print('entities',len(inc),'miss',miss,flush=True)
s=G.settings(); s.set('use-world-coords',True)
it=G.iterator(s,f,4,include=inc)
out=open(r'C:\SBM\qc\ptc\work\geom.csv','w',newline='')
w=csv.writer(out)
w.writerow(['guid','bridge','cat','name','mark','smin','smax','omin','omax','zmin','zmax','a0s','a0o','a0z','a1s','a1o','a1z','nv'])
t=time.time();k=0
if it.initialize():
    while True:
        sh=it.get()
        gid=sh.guid if hasattr(sh,'guid') else sh.id
        v=np.array(sh.geometry.verts).reshape(-1,3)
        if len(v):
            P=np.column_stack([ (v[:,:2]-O)@u*FT+S0, (v[:,:2]-O)@n*FT, v[:,2]*FT])
            cat,name,mark,br=rows.get(gid,('?','?','?','?'))
            mn=P.min(0);mx=P.max(0)
            # axis fit
            c0=P.mean(0); X=P-c0
            w_,V=np.linalg.eigh(X.T@X); d=V[:,-1]
            tt=X@d; t0,t1=tt.min(),tt.max()
            bins=np.linspace(t0,t1,9); cs=[];ts=[]
            for i in range(8):
                m=(tt>=bins[i])&(tt<=bins[i+1])
                if m.sum()>2:
                    Q=P[m]; cs.append((Q.min(0)+Q.max(0))/2); ts.append(tt[m].mean())
            if len(cs)>=2:
                cs=np.array(cs);ts=np.array(ts)
                A=np.column_stack([ts,np.ones(len(ts))])
                coef,_,_,_=np.linalg.lstsq(A,cs,rcond=None)
                p0=coef[0]*t0+coef[1]; p1=coef[0]*t1+coef[1]
            else: p0=p1=c0
            w.writerow([gid,br,cat,name,mark]+[round(float(x),4) for x in (mn[0],mx[0],mn[1],mx[1],mn[2],mx[2])]+[round(float(x),4) for x in (*p0,*p1)]+[len(v)])
        k+=1
        if k%500==0: print(k,time.time()-t,flush=True); out.flush()
        if not it.next(): break
out.close(); print('done',k)
