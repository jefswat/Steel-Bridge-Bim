import ifcopenshell, ifcopenshell.geom, numpy as np, pickle, multiprocessing, sys, time
f=ifcopenshell.open("inputs/nebraska-st.ifc")
s=ifcopenshell.geom.settings()
s.set("use-world-coords",True)
t=time.time()
prods=[p for p in f.by_type("IfcProduct") if p.Representation and p.is_a()!="IfcMechanicalFastener"]
print(len(prods))
it=ifcopenshell.geom.iterator(s,f,multiprocessing.cpu_count(),include=prods)
out={}
if it.initialize():
    while True:
        sh=it.get()
        g=sh.geometry
        v=np.array(g.verts).reshape(-1,3); fa=np.array(g.faces).reshape(-1,3)
        out[sh.guid]=(sh.id, sh.type if hasattr(sh,'type') else '', v.astype(np.float64), fa.astype(np.int32))
        if not it.next(): break
print(len(out), time.time()-t)
pickle.dump(out,open("cache_geom.pkl","wb"))
