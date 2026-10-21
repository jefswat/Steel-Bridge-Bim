import sqlite3,collections
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
tot=collections.Counter();
for br,mat,m,cat in c.execute("select bridge_group,material,mass_kg,category from elements where mass_kg is not null and in_ifc=1"):
    if not mat: continue
    ml=m*2.20462
    if 'oncrete' in mat: tot[('concrete',br)]+=ml; continue
    key='steel'
    tot[(key,br)]+=ml
    tot[('mat',mat[:30])]+=ml
s=sum(v for k,v in tot.items() if k[0]=='steel')
print('steel total lb',round(s));
for k,v in sorted(tot.items()):
    if k[0]=='steel' or k[0]=='mat': print(k,round(v))
