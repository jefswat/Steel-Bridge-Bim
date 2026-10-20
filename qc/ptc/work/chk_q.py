import sqlite3,collections
from rec import add
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
LB=2.2046226
ex=('walkway post','walkway rail','walkway stringer','railing post','rail member','grating','suspender rope')
d=collections.defaultdict(float)
for b,cat,m,mass,vol in c.execute('select bridge_group,category,material,mass_kg,volume_m3 from elements where mass_kg is not null'):
    if cat in ex: d[(b,'walk/rail/grating')]+= mass*LB if cat!='suspender rope' else 0; 
    if cat in ex: continue
    if m and 'Concrete' in m: d[(b,'conc')]+=vol*1.30795/1 if cat in('deck slab','barrier','wingwall','backwall','pedestal') else 0; d[(b,'conc_sub')]+=vol*1.30795 if cat not in('deck slab','barrier') else 0; continue
    if m is None or 'carbon' in m or 'Cast' in m and False: pass
    k='C' if (m is None or 'carbon' in m) else 'S' if 'silicon' in m else 'cast' if m.startswith('Cast') else 'forge' if m.startswith('Forged') else 'stainless' if 'Stainless' in m else 'pile' if 'HP' in m else 'other'
    d[(b,k)]+=mass*LB
G={'PTC Girder spans 1-10':(2496000,2727000),'PTC Girder spans 28-31':(1004000,1093000),'PTC 3-span truss 11-13':(1532000,1324000),'PTC 3-span truss 25-27':(1532000,1324000),'PTC 4-span truss 14-17':(2423000,2736000),'PTC 4-span truss 21-24':(2423000,2736000),'PTC Arch truss 18-20':(5730000,10200000)}
rows=[];pt=0;mt=0
for b,(pc,ps) in G.items():
    for k,pv in (('C',pc),('S',ps)):
        mv=d[(b,k)]; 
        rows.append((42,'DRB-4 2/68','Quantity table items 1-6 (structural carbon / silicon steel)',b+' '+('carbon' if k=='C' else 'silicon')+' steel lb','sum of IFC part mass by material (excl. rivets, walkways, railing, grating, rope, castings, forgings, stainless)',pv,round(mv),round(mv-pv),'1% steel','fail' if abs(mv/pv-1)>.01 else 'pass','ratio %.1f%%; inputs/ptc-forcheck.sqlite'%(100*mv/pv)))
        pt+=pv;mt+=mv
rows.append((42,'DRB-4 2/68','Totals items 1-6','All structural carbon+silicon steel lb','sum',pt,round(mt),round(mt-pt),'1%','fail','ratio %.1f%%'%(100*mt/pt)))
# concrete superstructure: item 11 by unit; deck+barrier(+haunch none)
C11={'PTC Girder spans 1-10':2505,'PTC Girder spans 28-31':1035,'PTC 3-span truss 11-13':1300,'PTC 3-span truss 25-27':1300,'PTC 4-span truss 14-17':2165,'PTC 4-span truss 21-24':2165,'PTC Arch truss 18-20':2730}
for b,pv in C11.items():
    mv=d[(b,'conc')]  # deck+barrier+wing+backwall+pedestal
    import sqlite3
    dk=sum(v for (v,) in c.execute("select volume_m3*1.30795 from elements where bridge_group=? and category in ('deck slab','barrier')",(b,)))
    rows.append((42,'DRB-4 2/68','Item 11 Class A concrete (CY)',b+' deck slab + parapets CY','IFC volumes deck slab + barrier (substructure excluded: plan table does not say what item 11 includes)',pv,round(dk,1),round(dk-pv,1),'2%','pass' if abs(dk/pv-1)<=.02 else 'fail','ratio %.1f%%'%(100*dk/pv)))
for r in rows: print(r[3],r[5],r[6],r[10])
add(rows)
