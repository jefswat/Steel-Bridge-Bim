import sqlite3,sys
c=sqlite3.connect('file:samples/plans/PTC Delaware River Bridge.sqlite?mode=ro',uri=True)
cols=[r[1] for r in c.execute('pragma table_info(v_element_sheets)')]
print(cols)
marks=sys.argv[1:]
for m in marks:
    rows=c.execute("select e.global_id,e.global_id,e.level,e.category,e.name,e.mark,e.bridge_group,e.parent_global_id from elements e where e.mark=? and e.bridge_group like 'PTC Arch%'",(m,)).fetchall()
    for r in rows[:2]:
        par=c.execute("select level,category,name from elements where global_id=?",(r[7],)).fetchone()
        sh=c.execute("select s.pdf_page from v_element_sheets v join sheets s on s.sheet_id=v.sheet_id where v.global_id=? order by s.pdf_page",(r[0],)).fetchall()
        print(m,'|',r[2],r[3],'| parent',par,'| pages',sorted(set(x[0] for x in sh)))
    if not rows: print(m,'NOT FOUND')
