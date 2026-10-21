import sqlite3,sys
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
pat=sys.argv[1]; br=sys.argv[2] if len(sys.argv)>2 else 'PTC Arch truss 18-20'
for m,n,cat,mat,v,l,s0,s1 in c.execute("select mark,name,category,material,volume_m3,length_m,station_begin,station_end from elements where bridge_group=? and mark like ? order by mark",(br,pat)):
    print(m,cat,mat[:22] if mat else '', 'len ft %.2f'%(l/0.3048) if l else '', 'A %.2f'%(v/0.3048**3*144/(l/0.3048)) if v and l else '', s0,s1)
