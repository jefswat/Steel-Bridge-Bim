import json, sys
import sys; sys.path.insert(0, "C:/SBM/qc/bonner-unit5/pt-profiles")
from analyze import faces, FT, IN
res = json.load(open("C:/SBM/qc/bonner-unit5/pt-profiles/work/analysis2.json"))
def fi(m):
    v = m / IN; s = "-" if v < 0 else ""; v = abs(v); f = int(v // 12); i = v - 12 * f; return f"{s}{f}'-{i:.3f}\""
def joints_crossed(r):
    from analyze import joints
    out = []
    raw = r["raw"]
    for j in joints:
        for k in range(1, len(raw)):
            if abs(raw[k][0] - j) < 1e-7: out.append((j, raw[k][1], raw[k][2]))
    return out
for name in sys.argv[1:]:
    r = res[name]
    print("==", name, r["group"], r["loc"])
    for lab, p in (("start", r["start"]), ("end", r["end"])):
        sg, b, t = faces(p[0]); print(f"  {lab}: {sg} fromBent {b} fromBulk {t}  X {fi(p[1])} Y {fi(p[2])}")
    print("  startDir plan/vert %.3f %.3f   endDir %.3f %.3f" % tuple(r["startDir"] + r["endDir"]))
    for e in r["els"]:
        sg, b, t = faces(e["S"])
        s = f"  {e['kind']:6s} {sg:7s} fromBent {b:7.4f} fromBulk {t:7.4f} X {fi(e['X'])} Y {fi(e['Y'])} defl {e['delta']:.3f} in/out plan {e['inplan']:+.3f}/{e['outplan']:+.3f} vert {e['invert']:+.3f}/{e['outvert']:+.3f}"
        if e["kind"] == "arc": s += f" R {e['R']/FT:.3f}' T {e['T']/IN:.2f}\" tp {faces(e['tpa'][0])[1]:.3f}..{faces(e['tpb'][0])[1]:.3f}"
        print(s)
    if "-j" in sys.argv[0:1] or True:
        for (j, X, Y) in joints_crossed(r):
            sg, b, t = faces(j + 1e-6)
            print(f"     joint {j/FT:.2f} (seg {sg} bentface? {b:.2f}) X {fi(X)} Y {fi(Y)}")
