import ifcopenshell, ifcopenshell.geom as G, sqlite3, numpy as np, csv, sys
FT=1/0.3048
O=np.array([70.4888176,-113.8711093])*0.3048
ang=-0.3169866987
u=np.array([np.cos(ang),np.sin(ang)]); n=np.array([-u[1],u[0]]); S0=63800.0
want={ # bridge_group -> marks (base names, N side)
 'PTC 3-span truss 11-13':['GU1','GU7','GL8'],
 'PTC 4-span truss 14-17':['GL10','GL20','GU11'],
 'PTC Arch truss 18-20':['GU9','GL10','GU11','GL12','GU12','GL15','GL17','GL18','GL20','GL16','GL19','GL21','GU5'],
}
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
s=G.settings(); s.set('use-world-coords',True)
out=csv.writer(open(r'C:\SBM\qc\ptc\work\g_model.csv','w',newline=''))
out.writerow(['bridge','mark','smin','smax','zmin','zmax','w_in','h_in','thk_in','area_sf','vol_m3','cs','cz'])
for br,ms in want.items():
    for m in ms:
        for suf in ('-L','-L2','-L3','-R'):
            mark='N-'+m+suf
            r=c.execute('select global_id,volume_m3 from elements where category=? and mark=? and bridge_group=?',('gusset plate',mark,br)).fetchall()
            for gid,vol in r:
                sh=G.create_shape(s,f.by_guid(gid))
                v=np.array(sh.geometry.verts).reshape(-1,3)
                P=np.column_stack([(v[:,:2]-O)@u*FT+S0,(v[:,:2]-O)@n*FT,v[:,2]*FT])
                X=P-P.mean(0); w_,V=np.linalg.eigh(X.T@X)
                ext=[np.ptp(X@V[:,j]) for j in range(3)]
                thk=ext[0]*12
                # in-plane bbox in (s,z)
                smin,smax=P[:,0].min(),P[:,0].max(); zmin,zmax=P[:,2].min(),P[:,2].max()
                area=vol/0.3048**3/(thk/12)
                out.writerow([br,mark,round(smin,3),round(smax,3),round(zmin,3),round(zmax,3),round((smax-smin)*12,2),round((zmax-zmin)*12,2),round(thk,3),round(area,2),vol,round(P[:,0].mean(),2),round(P[:,2].mean(),2)])
print('ok')
