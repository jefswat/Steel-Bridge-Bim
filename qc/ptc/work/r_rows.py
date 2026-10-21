import json,csv,sys
sys.path.insert(0,r'C:\SBM\qc\ptc\work')
from rec4 import add
P=json.load(open(r'C:\SBM\qc\ptc\work\r6.json')); M=json.load(open(r'C:\SBM\qc\ptc\work\r4.json'))
D={}
for r in csv.DictReader(open(r'C:\SBM\docs\plan-data\ptc-delaware-river\connections\gusset-outlines.csv',encoding='utf-8')):
    D[(r['structure'].split(' ')[0],r['joint'])]=int(r['page'])
SH={}
for r in csv.DictReader(open(r'C:\SBM\qc\ptc\extract\sheet_index.csv',encoding='utf-8')): SH[int(r['page'])]=r['dwg']+' '+r['sheet']
meth_plan='plan count = filled + open rivet symbols inside the drawn gusset outline, detected on the 300 dpi raster (work/rdet.py) and checked on the overlay image; '
for key,(d,r) in P.items():
    st,j=key.split(':'); pg=D[(st,j)]
    m=M[key]; mod=max(m['clusters'].values()) if m['clusters'] else 0
    plan=d+r
    tol=max(3,round(0.05*plan))
    add(pg,SH.get(pg,'DRB-4 p.%d'%pg),{'approach3':'3-span truss joint','approach4':'4-span truss joint','main':'Main span joint'}[st],
        f'{j} gusset: rivets inside the gusset outline (per face) [{d} filled + {r} open symbols]',
        meth_plan+'model = IFC IfcMechanicalFastener RIVET placements inside the plate outline on the outer face (N-G%s-L); half of the placements (both faces) not counted twice'%j,
        plan,mod,tol,evidence=f'evidence/rivets-{st}-{j}.png; work/rdet.py; work/r4.py')
# model gussets without any rivet
import collections
z=collections.defaultdict(lambda:[0,0])
for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\r7.csv')):
    z[r['bridge']][0]+=1
    if int(r['n_rivets_hull'])==0: z[r['bridge']][1]+=1
for br,(n,k) in z.items():
    add('50-89','DRB-4 10/68 .. 49/68','Joint sheets (p.50-53, 59-67, 74-90): every gusset is drawn with rivets',f'{br}: N-side gusset plates (-L) that carry no rivet in the model (of {n})','IFC rivets inside the plate outline, any o within 1.5 ft of the plate; the plan draws rivets on every gusset (checked on 13 joints above)',0,k,0,evidence='work/r7.py; work/r7.csv')
print('ok')
