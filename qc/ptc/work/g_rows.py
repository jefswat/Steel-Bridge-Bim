import csv,sys
sys.path.insert(0,r'C:\SBM\qc\ptc\work')
from rec4 import add
M={(r['bridge'],r['mark']):r for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\g_model.csv'))}
D={}
for r in csv.DictReader(open(r'C:\SBM\docs\plan-data\ptc-delaware-river\connections\gusset-outlines.csv',encoding='utf-8')):
    D[(r['structure'].split(' ')[0],r['joint'])]=r
BR={'approach3':'PTC 3-span truss 11-13','approach4':'PTC 4-span truss 14-17','main':'PTC Arch truss 18-20'}
SH={50:'DRB-4 10/68',52:'DRB-4 12/68',63:'DRB-4 23/68',67:'DRB-4 27/68',77:'DRB-4 37/68',78:'DRB-4 38/68',79:'DRB-4 39/68',81:'DRB-4 41/68',86:'DRB-4 46/68',87:'DRB-4 47/68',88:'DRB-4 48/68',89:'DRB-4 49/68'}
TOPIC={'approach3':'3-span truss joint (p.%d)','approach4':'4-span truss joint (p.%d)','main':'Main span joint (p.%d)'}
# joints measured: (struct, joint, mark, status, plan_w, plan_h, plan_note, thickness_plan)
J=[('approach3','U1','GU1','ok',None,None,'2-5/8 Gussets (16 in to in)',0.625),
   ('approach3','U7','GU7','ok',None,None,'2-5/8 Gussets',0.625),
   ('approach3','L8','GL8','ok',None,None,'2-7/8 Gussets (17 5/8 in to in), 2-3/4 Gussets inner',0.875),
   ('approach4','L10','GL10','ok',None,None,'2-7/8 Gussets (both layers)',0.875),
   ('approach4','L20','GL20','ok',None,None,'2-7/8 Gussets (17 5/8 in to in) outer, 2-3/4 inner',0.875),
   ('main','U9','GU9','fail',143.5,None,'2-1 Gussets',1.0),
   ('main','L10','GL10','ok',None,None,'2-3/4 Gussets outer, 2-1/2 Gussets inner',0.75),
   ('main','U11','GU11','ok',None,None,None,None),
   ('main','L12','GL12','ok',None,None,'2-1 Gussets',1.0),
   ('main','U12','GU12','ok',None,None,None,None),
   ('main','L15','GL15','fail',104.0,None,None,None),
   ('main','L17','GL17','fail',113.0,None,'2-7/8 Gussets',0.875),
   ('main','L18','GL18','ok',None,None,None,None),
   ('main','L20','GL20','ok',None,None,'2-1 Gussets',1.0)]
for st,j,mk,stt,pw,ph,tn,pt in J:
    d=D[(st,j)]; pg=int(d['page']); m=M[(BR[st],'N-'+mk+'-L')]
    wm=float(m['w_in']); hm=float(m['h_in']); am=float(m['area_sf']); thk=float(m['thk_in'])
    low=d['confidence']=='low'
    tol=6.0 if st=='main' else 2.5
    ev=f'evidence/gusset-{st}-{j}.png (sheet crop with the drawn-outline polygon overlaid in red; model = IFC plate N-{mk}-L)'
    meth='bbox of IFC gusset plate N-%s-L (s, z) vs width read on the sheet: gusset edges at %s'%(mk,'the overlay: red outline follows the drawn plate edges (scale 4.28 px/in = 3/4"=1\' sheet scale; panel U1-U3 26\'-11 1/4 measured 4.24 px/in on p.50)' if stt=='ok' else 'the sheet crop (drawn edge located by hand): the model outline extends beyond the drawn edge')
    if stt=='ok':
        add(pg,SH[pg],TOPIC[st]%pg,f'{j} gusset plate N-{mk}: width (in, bbox in plan)',meth,d['width_in'],wm,tol,evidence=ev)
        add(pg,SH[pg],TOPIC[st]%pg,f'{j} gusset plate N-{mk}: height (in, bbox)',meth,d['height_in'],hm,tol,evidence=ev)
    else:
        add(pg,SH[pg],TOPIC[st]%pg,f'{j} gusset plate N-{mk}: width (in, bbox in plan)',meth+' (low-confidence joint: drawn edge re-read)',pw,wm,tol,evidence=ev)
        add(pg,SH[pg],TOPIC[st]%pg,f'{j} gusset plate N-{mk}: outline follows the drawn plate edges',
            'overlay of the outline polygon on the sheet at 100-140 dpi',
            'all edges on the drawn edges',{'U9':'right edge ~20 in outboard of the drawn edge (corner 1715,335 vs drawn edge x~1610-1634 at y 335-450, p.77 150-dpi px)',
            'L15':'left edge ~17 in outboard of the drawn edge (vertex 290,300 vs drawn corner 370,300, p.86)',
            'L17':'right edge 11-16 in outboard of the drawn edge (vertices 1450,745 and 1575,960 vs drawn edge x~1380 / 1527, p.87)'}[j],'6 in','fail',ev,kind='str')
    add(pg,SH[pg],TOPIC[st]%pg,f'{j} gusset plate N-{mk}: area (sq ft) vs outline area',
        'IFC volume / thickness vs the drawn-outline area (outline area from the overlay-checked polygon)',d['area_sf'],am,max(0.03*float(d['area_sf']),0.3),
        result=None if stt=='ok' else 'fail',evidence=ev) if stt=='ok' else None
    if tn is not None:
        r=add(pg,SH[pg],TOPIC[st]%pg,f'{j} gusset thickness (in) at N-{mk}-L',f'sheet note "{tn}" vs IFC plate thickness (PCA extent)',pt,thk,0.02,evidence=ev)
print('rows added')
