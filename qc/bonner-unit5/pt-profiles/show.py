import json, sys, math
sys.path.insert(0, "C:/SBM/qc/bonner-unit5/pt-profiles")
from analyze import faces, FT, IN
W = "C:/SBM/qc/bonner-unit5/pt-profiles/work/"
res = json.load(open(W + "analysis.json"))
def fi(m): 
    v = m / IN; s = "-" if v < 0 else ""; v = abs(v); f = int(v // 12); i = v - 12 * f; return f"{s}{f}'-{i:.3f}\""
for name in sys.argv[1:]:
    r = res[name]
    print("==", name, r["group"], r["loc"], "pts", r["n"])
    for lab, p in (("start", r["start"]), ("end", r["end"])):
        sg, b, t = faces(p[0]); print(f"  {lab}: seg {sg} fromBent {b if b is None else round(b,4)} ft fromBulkhead {t if t is None else round(t,4)} ft  X {fi(p[1])} Y {fi(p[2])}")
    print("  startDir plan/vert %.3f %.3f  endDir %.3f %.3f" % tuple(r["startDir"] + r["endDir"]))
    for c in r["curves"]:
        sg, b, t = faces(c["S"])
        print(f"  PI seg {sg:7s} fromBent {b:7.4f} fromBulk {t:7.4f} X {fi(c['X'])} Y {fi(c['Y'])} delta {c['delta']:.3f} plan {c['plan']:+.3f} vert {c['vert']:+.3f} R {c['R']/FT:.3f} ft T1 {c['T1']/IN:.2f} T2 {c['T2']/IN:.2f} in{' CORNER' if c['corner'] else ''}  inplan {c['inplan']:+.3f} outplan {c['outplan']:+.3f}")
    for (S, tu, X, Y) in r["joints"]:
        sg, b, t = faces(S)
        if tu > 0.01: print(f"  joint kink at {sg} ({b:.3f}/{t:.3f}) turn {tu:.3f} deg X {fi(X)} Y {fi(Y)}")
