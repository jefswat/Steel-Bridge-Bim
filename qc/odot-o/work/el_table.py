import ifcopenshell, ifcopenshell.util.element as ue, numpy as np, pickle, csv, json
f=ifcopenshell.open(r'C:/SBM/qc/odot-o/inputs/odot-o.ifc')
G=pickle.load(open('geom.pkl','rb'))
def vol(v,fa):
    a,b,c=v[fa[:,0]],v[fa[:,1]],v[fa[:,2]]
    return abs(np.einsum('ij,ij->i',a,np.cross(b,c)).sum()/6)
rows=[]
for e in f.by_type('IfcProduct'):
    if e.is_a('IfcAlignmentSegment') or e.is_a('IfcReferent'): continue
    ps=ue.get_psets(e); fab=ps.get('SBM_Fabrication',{})
    r=dict(guid=e.GlobalId,cls=e.is_a(),name=e.Name,otype=e.ObjectType,ptype=getattr(e,'PredefinedType',None),
           mark=fab.get('Mark'),size=fab.get('Size'),flen=fab.get('Length'),mass=fab.get('Mass'))
    cont=ue.get_aggregate(e); r['parent']=cont.Name if cont else None
    if e.GlobalId in G:
        v,fa=G[e.GlobalId]
        if len(v):
            mn,mx=v.min(0),v.max(0); r.update(xmin=mn[0],ymin=mn[1],zmin=mn[2],xmax=mx[0],ymax=mx[1],zmax=mx[2])
            c=v.mean(0); r.update(cx=c[0],cy=c[1],cz=c[2]); r['vol']=vol(v,fa)
            # PCA extents
            if len(v)>=3:
                w,U=np.linalg.eigh(np.cov((v-c).T)); p=(v-c)@U; ext=np.sort(p.max(0)-p.min(0))[::-1]
                r.update(d1=ext[0],d2=ext[1],d3=ext[2])
    rows.append(r)
keys=['guid','cls','name','otype','ptype','mark','size','flen','mass','parent','xmin','ymin','zmin','xmax','ymax','zmax','cx','cy','cz','vol','d1','d2','d3']
with open('elements.csv','w',newline='',encoding='utf-8') as fo:
    w=csv.DictWriter(fo,keys); w.writeheader(); [w.writerow(r) for r in rows]
print(len(rows))
