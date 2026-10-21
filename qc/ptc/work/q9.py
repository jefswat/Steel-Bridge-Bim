import sqlite3,sys,collections
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
pat=sys.argv[1]; lim=int(sys.argv[2]) if len(sys.argv)>2 else 40
rows=c.execute("select bridge_group,category,name,mark,material,volume_m3,length_m from elements where name like ? or mark like ?",(pat,pat)).fetchall()
print(len(rows))
cat=collections.Counter((r[0],r[1]) for r in rows)
for k,v in cat.items(): print(k,v)
for r in rows[:lim]: print(r)
