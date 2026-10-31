"""Agent review: recover P.I.s / radii / deflections of every modeled longitudinal tendon from the box-coordinate dump (work/tendon-points.csv)."""
import csv, math, collections, json, sys
W = "C:/SBM/qc/bonner-unit5/pt-profiles/work/"
FT = 0.3048; IN = FT / 12
segs = list(csv.DictReader(open(W + "segments.csv")))
for s in segs:
    s["sta0"] = float(s["sta0"]); s["sta1"] = float(s["sta1"]); s["index"] = int(s["index"])
bents = {r["bent"].replace("Bent ", ""): float(r["station"]) for r in csv.DictReader(open(W + "bents.csv"))}
joints = sorted(set([s["sta0"] for s in segs] + [s["sta1"] for s in segs]))
def seg_at(S):
    for s in segs:
        if s["sta0"] - 1e-7 <= S <= s["sta1"] + 1e-7: return s
def faces(S):
    """segment name, distance from bent-side face, distance from bulkhead (far-from-bent) face (ft)."""
    s = seg_at(S)
    if s is None: return ("?", None, None)
    if s["bent"]:
        bs = bents[s["bent"].replace("Bent ", "")]
        if bs >= s["sta1"] - 1e-6: bf, tf = s["sta1"], s["sta0"]
        else: bf, tf = s["sta0"], s["sta1"]
    else: bf, tf = s["sta0"], s["sta1"]
    return (s["name"], abs(S - bf) / FT, abs(S - tf) / FT)
pts = collections.OrderedDict()
for r in csv.DictReader(open(W + "tendon-points.csv")):
    pts.setdefault(r["tendon"], {"group": r["group"], "loc": r["location"], "p": []})["p"].append((float(r["S"]), float(r["X"]), float(r["Y"])))
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def nrm(a): return math.sqrt(a[0] ** 2 + a[1] ** 2 + a[2] ** 2)
def unit(a): n = nrm(a); return (a[0] / n, a[1] / n, a[2] / n)
def ang(a, b):
    c = sum(x * y for x, y in zip(unit(a), unit(b))); return math.degrees(math.acos(max(-1, min(1, c))))
def pang(a, b, k):  # angle change in projection (k=1 plan S-X, k=2 elevation S-Y)
    return math.degrees(math.atan2(b[k], b[0]) - math.atan2(a[k], a[0]))
def analyse(P):
    # drop duplicate points
    Q = [P[0]]
    for p in P[1:]:
        if nrm(sub(p, Q[-1])) > 1e-6: Q.append(p)
    n = len(Q)
    turn = [0.0] * n
    for i in range(1, n - 1): turn[i] = ang(sub(Q[i], Q[i - 1]), sub(Q[i + 1], Q[i]))
    curves = []; i = 1
    while i < n - 1:
        if turn[i] > 0.005:
            j = i
            while j + 1 < n - 1 and turn[j + 1] > 0.005: j += 1
            curves.append((i, j)); i = j + 1
        else: i += 1
    out = []
    for (i, j) in curves:
        din = sub(Q[i], Q[i - 1]); dout = sub(Q[j + 1], Q[j])
        delta = ang(din, dout)
        # radius from interior chords
        rs = []
        for k in range(i, j):
            c = nrm(sub(Q[k + 1], Q[k])); t = turn[k + 1] if k + 1 <= j else 0
            # use vertices whose both neighbours are arc points
        for k in range(i + 1, j):
            a, b, c = Q[k - 1], Q[k], Q[k + 1]
            ab, bc, ca = nrm(sub(b, a)), nrm(sub(c, b)), nrm(sub(a, c))
            cr = [ab*0+ (sub(b, a)[1]*sub(c, a)[2]-sub(b, a)[2]*sub(c, a)[1]), (sub(b, a)[2]*sub(c, a)[0]-sub(b, a)[0]*sub(c, a)[2]), (sub(b, a)[0]*sub(c, a)[1]-sub(b, a)[1]*sub(c, a)[0])]
            area2 = nrm(cr)
            if area2 > 1e-12 and min(ab, bc) > 0.01: rs.append(ab * bc * ca / (2 * area2))
        R = sorted(rs)[len(rs) // 2] if rs else 0.0
        corner = (j == i)
        # P.I. = closest points of the two legs
        p1, u = Q[i], unit(din); p2, v = Q[j], unit(dout)
        w0 = sub(p1, p2); a_ = 1; b_ = sum(x*y for x, y in zip(u, v)); c_ = 1; d_ = sum(x*y for x, y in zip(u, w0)); e_ = sum(x*y for x, y in zip(v, w0))
        den = a_ * c_ - b_ * b_
        if abs(den) < 1e-12: pi = Q[i]
        else:
            sc = (b_ * e_ - c_ * d_) / den; tc = (a_ * e_ - b_ * d_) / den
            q1 = tuple(p1[k] + u[k] * sc for k in range(3)); q2 = tuple(p2[k] + v[k] * tc for k in range(3))
            pi = tuple((q1[k] + q2[k]) / 2 for k in range(3))
        out.append(dict(S=pi[0], X=pi[1], Y=pi[2], delta=delta, plan=pang(din, dout, 1), vert=pang(din, dout, 2), R=R, corner=corner,
                        T1=nrm(sub(pi, Q[i])), T2=nrm(sub(Q[j], pi)), tp1=Q[i][0], tp2=Q[j][0],
                        inplan=math.degrees(math.atan2(din[1], din[0])), outplan=math.degrees(math.atan2(dout[1], dout[0])),
                        invert=math.degrees(math.atan2(din[2], din[0])), outvert=math.degrees(math.atan2(dout[2], dout[0]))))
    # joint crossings: direction change at joints that are not inside curves
    jk = []
    for i in range(1, n - 1):
        if any(abs(Q[i][0] - j) < 1e-6 for j in joints):
            jk.append((Q[i][0], turn[i], Q[i][1], Q[i][2]))
    first, last = Q[0], Q[-1]
    return Q, out, jk
if __name__ == "__main__":
    res = {}
    for name, d in pts.items():
        Q, cur, jk = analyse(d["p"])
        res[name] = dict(group=d["group"], loc=d["loc"], start=Q[0], end=Q[-1], n=len(Q), curves=cur, joints=jk,
                         startDir=[math.degrees(math.atan2(Q[1][1]-Q[0][1], Q[1][0]-Q[0][0])), math.degrees(math.atan2(Q[1][2]-Q[0][2], Q[1][0]-Q[0][0]))],
                         endDir=[math.degrees(math.atan2(Q[-1][1]-Q[-2][1], Q[-1][0]-Q[-2][0])), math.degrees(math.atan2(Q[-1][2]-Q[-2][2], Q[-1][0]-Q[-2][0]))])
    json.dump(res, open(W + "analysis.json", "w"), indent=0)
    print(len(res), "tendons")
