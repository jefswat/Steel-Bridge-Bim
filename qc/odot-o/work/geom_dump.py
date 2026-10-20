import ifcopenshell, ifcopenshell.geom, numpy as np, pickle, multiprocessing, ifcopenshell.util.element as ue
f=ifcopenshell.open(r'C:/SBM/qc/odot-o/inputs/odot-o.ifc')
s=ifcopenshell.geom.settings(); s.set('use-world-coords',True)
it=ifcopenshell.geom.iterator(s,f,multiprocessing.cpu_count())
out={}
if it.initialize():
    while True:
        sh=it.get()
        v=np.array(sh.geometry.verts).reshape(-1,3); fa=np.array(sh.geometry.faces).reshape(-1,3)
        out[sh.guid]=(v.astype(np.float64),fa.astype(np.int32))
        if not it.next(): break
pickle.dump(out,open('geom.pkl','wb'))
print(len(out))
