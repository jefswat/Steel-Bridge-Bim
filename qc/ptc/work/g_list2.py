import sqlite3,collections
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
d=collections.defaultdict(list)
for m,b in c.execute("select mark,bridge_group from elements where category='gusset plate'"): d[b].append(m)
for b,v in d.items(): print(b,len(v),v[:6],v[-6:])
