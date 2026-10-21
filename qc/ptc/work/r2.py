import ifcopenshell, ifcopenshell.util.unit as U, ifcopenshell.util.placement as P, numpy as np
f=ifcopenshell.open(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.ifc')
print('unit scale',U.calculate_unit_scale(f))
e=[x for x in f.by_type('IfcMechanicalFastener') if x.PredefinedType=='RIVET' or x.Name=='Rivet'][:2]
for x in e:
    M=P.get_local_placement(x.ObjectPlacement); print(x.Name,M[:3,3])
sh=ifcopenshell.geom.create_shape(ifcopenshell.geom.settings(use_world_coords=True),e[0]) if hasattr(ifcopenshell,'geom') else None
import ifcopenshell.geom as G
s=G.settings(); s.set('use-world-coords',True)
sh=G.create_shape(s,e[0]); v=np.array(sh.geometry.verts).reshape(-1,3); print('geom centre m',v.mean(0))
mc=f.by_type('IfcMapConversion'); print(mc[0].Eastings,mc[0].Northings,mc[0].OrthogonalHeight)
