import sqlite3,collections,sys
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
br=sys.argv[1]; cat=sys.argv[2]
rows=c.execute("select mark,name,material,volume_m3,length_m,parent_global_id,support from elements where bridge_group=? and category=?",(br,cat)).fetchall()
print(len(rows))
import re
g=collections.defaultdict(list)
for m,n,mat,v,l,p,s in rows:
    key=re.sub(r'\d+','#',m or n)
    g[key].append((m,n,mat,v,l))
for k,v in sorted(g.items()):
    n=len(v); A=[ (x[3]/0.3048**3*144/ (x[4]/0.3048) ) for x in v if x[3] and x[4]]  # sq in = vol(ft3)*144/len(ft)
    print(k,n,'e.g.',v[0][0],v[0][1],v[0][2],'len ft %.2f..%.2f'%(min(x[4]/0.3048 for x in v if x[4]),max(x[4]/0.3048 for x in v if x[4])),'area %.1f..%.1f'%(min(A),max(A)))
