import ifcopenshell, collections, time
t=time.time()
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
print('open',time.time()-t)
c=collections.Counter(e.is_a() for e in f.by_type('IfcProduct'))
print(c.most_common(40))
g=[e for e in f.by_type('IfcPlate') if 'gusset' in (e.Name or '').lower()][:3]
for e in g: print(e.Name,e.Description,e.ObjectType,e.PredefinedType)
r=f.by_type('IfcMechanicalFastener')[:2]
for e in r: print(e.Name,e.Description,e.ObjectType,e.PredefinedType)
