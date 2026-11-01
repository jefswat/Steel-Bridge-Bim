"""Converts the p.277 vector coordinates read in this review to section / elevation inches (3/8" = 1'-0" = 2.25 pt/in).
Section: CL girder X 885.40 pt, deck top Y 291.04 pt.  Elevation (same Y): CL split X 2034.50, bulkhead face X 1899.60."""
K = 2.25; CX = 885.40; TOP = 291.04; SPLIT = 2034.50
x = lambda X: round((X - CX) / K, 2)
y = lambda Y: round((Y - TOP) / K, 2)
s = lambda X: round((SPLIT - X) / K, 2)
pts = {
 "top slab underside (CL)": (None, 311.24), "top block bottom": (None, 405.74), "bottom slab top (bent)": (None, 736.44),
 "bottom slab top (bulkhead CL)": (None, 749.94), "soffit (bulkhead)": (None, 803.94), "soffit (bent)": (None, 810.74),
 "opening top": (None, 466.54), "opening chamfer bottom": (None, 520.54), "opening side": (952.90, None), "opening top corner": (919.10, None),
 "utility top/bottom": (None, 347.24), "utility bottom": (None, 403.54), "utility side": (903.40, None), "utility chamfer": (894.40, 356.24),
 "top block edge": (972.60, None), "web inner @block bottom": (1157.10, 405.74), "web inner @bottom slab": (1074.40, 736.44),
 "bulkhead haunch start": (1008.0, 749.94), "conduit 1": (1073.25, 663.39), "conduit 2": (1090.15, 636.39),
 "web outer @bent soffit": (1092.90, 810.74), "pintle 1 (plan)": (1000.10, None), "pintle 2 (plan)": (1040.60, None),
 "access circle edge": (925.90, None), "insert": (973.15, 446.24), "temp PT duct": (1060.90, 682.44),
}
for k, (X, Y) in pts.items():
    print(f"{k:32s} x={x(X) if X else '':>8} y={y(Y) if Y else '':>8}")
for k, X in {"bulkhead face": 1899.60, "block end (bulkhead side)": 1913.10, "diaphragm face (bulkhead side)": 1940.10, "diaphragm face (split side)": 1994.00}.items():
    print(f"{k:32s} s={s(X)}")
print("plan: pintle s =", round((1256.74 - 1227.44) / K, 2), " access R =", round((1297.24 - 1256.74) / K, 2))
