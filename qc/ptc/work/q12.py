import sqlite3,re,collections,sys
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
for br in ('PTC 3-span truss 11-13','PTC 4-span truss 14-17','PTC Arch truss 18-20'):
    print(br)
    for cat in ('walkway rail','walkway post','walkway stringer','grating'):
        g=collections.defaultdict(list)
        for m,n,v,l in c.execute("select mark,name,volume_m3,length_m from elements where bridge_group=? and category=?",(br,cat)):
            k=re.sub(r'[\d]+\'?','#',n)
            g[k].append((v,l,n))
        for k,v in sorted(g.items()):
            ar=[x[0]/0.3048**3*144/(x[1]/0.3048) for x in v if x[0] and x[1]]
            ln=[x[1]/0.3048 for x in v if x[1]]
            print(' ',cat,k,len(v),'area %.2f..%.2f'%(min(ar),max(ar)) if ar else '', 'len %.2f..%.2f'%(min(ln),max(ln)) if ln else '', v[0][2])
