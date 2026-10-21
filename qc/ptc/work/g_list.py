import sqlite3,sys
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
print([r[1] for r in c.execute('pragma table_info(elements)')])
q=sys.argv[1] if len(sys.argv)>1 else "category='gusset plate'"
rows=c.execute(f"select global_id,category,name,mark,bridge_group,volume_m3 from elements where {q}").fetchall()
print(len(rows))
for r in rows[:int(sys.argv[2]) if len(sys.argv)>2 else 15]: print(r)
