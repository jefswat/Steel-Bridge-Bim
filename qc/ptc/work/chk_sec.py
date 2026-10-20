import csv,re,sqlite3,math,collections
from rec import add
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
vol={g:(v or 0,m) for g,v,m in c.execute("select global_id,volume_m3,material from elements")}
R=list(csv.DictReader(open('geom.csv')))
plan={'U0-U1':(25.56,'C'),'U1-U3':(64.36,'S'),'U3-U5':(72.33,'S'),'U5-U7':(30.26,'S'),'U7-U9':(72.33,'S'),'U9-U11':(25.56,'C'),'U11-U11':(32.65,'S'),'L0-L2':(32.65,'S'),'L2-L4':(62.07,'S'),'L4-L6':(49.09,'S'),'L6-L8':(32.65,'S'),'L8-L10':(41.85,'S'),'L10-L12':(25.56,'S'),
'L0-U1':(67.06,'S'),'L2-U1':(32.65,'S'),'L2-U3':(41.85,'C'),'L4-U3':(41.85,'C'),'L4-U5':(25.56,'S'),'L6-U5':(59.39,'S'),'L6-U7':(54.07,'S'),'L8-U7':(100.59,'S'),'L8-U9':(84.37,'S'),'L10-U9':(41.85,'S'),'L10-U11':(44.08,'S'),'L12-U11':(41.85,'C'),'L0-U0':(24.71,'S')}
def canon(mk):
    m=re.match(r"[NS]-([UL])(\d+)'?-([UL])(\d+)'?$",mk)
    if not m: return None
    a,i,b,j=m.group(1),int(m.group(2)),m.group(3),int(m.group(4))
    if a==b: x,y=sorted([(a,i),(b,j)],key=lambda t:t[1])
    else: x,y=((a,i),(b,j)) if a=='L' else ((b,j),(a,i))
    return '%s%d-%s%d'%(x+y)
rows=[];
for br in ('PTC 3-span truss 11-13','PTC 3-span truss 25-27'):
    res=collections.defaultdict(list)
    for r in R:
        if r['bridge']!=br or r['cat'] not in('truss chord','truss diagonal','truss vertical'): continue
        k=canon(r['mark'])
        if k is None: continue
        if k not in plan: continue
        L=math.dist([float(r['a0s']),float(r['a0z'])],[float(r['a1s']),float(r['a1z'])])
        L=math.sqrt(L**2+0)
        v=vol[r['guid']][0]
        area=v/ (L*0.3048) /0.0254**2
        res[k].append((area,vol[r['guid']][1]))
    for k,(pa,pm) in plan.items():
        if k not in res: rows.append((47,'DRB-4 7/68','3-span member table gross area',br+' '+k,'IFC volume / axis length vs "GROSS SQ.IN."',pa,'not found','','1%','n.a.','no model member with that mark')); continue
        av=sum(a for a,_ in res[k])/len(res[k]); mat=res[k][0][1] or ''
        mm='C' if 'carbon' in mat else 'S' if 'silicon' in mat else '?'
        rows.append((47,'DRB-4 7/68','3-span member table gross area',br+' '+k+' (mat plan %s / model %s)'%(pm,mm),'IFC volume / axis length vs plan "GROSS SQ.IN." (model area includes any fills / extra plates in member solid)',pa,round(av,2),round(av-pa,2),'3 %','pass' if abs(av/pa-1)<=.03 and pm==mm else 'fail','work/geom.csv; n=%d'%len(res[k])))
for r in rows: print(r[3][-40:],r[5],r[6],r[9])
add(rows)
