"""Agent review: recover the arcs (radius, tangent points, P.I., deflection plan / vertical) of every modeled tendon from the box-coordinate dump."""
import sys; sys.path.insert(0, "C:/SBM/qc/bonner-unit5/pt-profiles")
import csv, math, collections, json
from analyze import segs, bents, joints, faces, pts, FT, IN
def sub(a, b): return tuple(x - y for x, y in zip(a, b))
def add(a, b): return tuple(x + y for x, y in zip(a, b))
def mul(a, s): return tuple(x * s for x in a)
def dot(a, b): return sum(x * y for x, y in zip(a, b))
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def nrm(a): return math.sqrt(dot(a, a))
def unit(a): n = nrm(a); return mul(a, 1 / n)
def ang(a, b): return math.degrees(math.acos(max(-1, min(1, dot(unit(a), unit(b))))))
def circ(a, b, c):
    ab, ac = sub(b, a), sub(c, a); n = cross(ab, ac); n2 = dot(n, n)
    if n2 < 1e-20: return None, None, None
    cen = add(a, mul(add(mul(cross(n, ab), dot(ac, ac)), mul(cross(ac, n), dot(ab, ab))), 1 / (2 * n2)))
    return cen, nrm(sub(a, cen)), unit(n)
def turnat(Q, i): return ang(sub(Q[i], Q[i - 1]), sub(Q[i + 1], Q[i]))
def analyse(P):
    Q = [P[0]]
    for p in P[1:]:
        if nrm(sub(p, Q[-1])) > 1e-7: Q.append(p)
    # drop collinear (inserted joint) points
    k = 1
    while k < len(Q) - 1:
        if turnat(Q, k) < 2e-4: Q.pop(k)
        else: k += 1
    n = len(Q)
    r = [None] * n
    for i in range(1, n - 1): r[i] = circ(Q[i - 1], Q[i], Q[i + 1])[1]
    used = [False] * n; arcs = []
    i = 1
    while i < n - 1:
        if r[i] is not None and i + 1 < n - 1 and r[i + 1] is not None and abs(r[i] - r[i + 1]) < 1e-3 * r[i] and abs(nrm(sub(Q[i], Q[i-1])) - nrm(sub(Q[i+1], Q[i]))) < 1e-4 :
            j = i + 1
            while j + 1 < n - 1 and r[j + 1] is not None and abs(r[j + 1] - r[i]) < 1e-3 * r[i] and abs(nrm(sub(Q[j+1], Q[j])) - nrm(sub(Q[j], Q[j-1]))) < 1e-4: j += 1
            a, b = i - 1, j + 1
            cen, R, nn = circ(Q[i - 1], Q[i], Q[i + 1])
            ta = unit(cross(nn, sub(Q[a], cen))); ta = ta if dot(ta, sub(Q[a + 1], Q[a])) > 0 else mul(ta, -1)
            tb = unit(cross(nn, sub(Q[b], cen))); tb = tb if dot(tb, sub(Q[b], Q[b - 1])) > 0 else mul(tb, -1)
            delta = ang(ta, tb); T = R * math.tan(math.radians(delta) / 2)
            pi = add(Q[a], mul(ta, T))
            arcs.append(dict(kind="arc", a=a, b=b, R=R, delta=delta, T=T, S=pi[0], X=pi[1], Y=pi[2], tpa=Q[a], tpb=Q[b],
                             inplan=math.degrees(math.atan2(ta[1], ta[0])), outplan=math.degrees(math.atan2(tb[1], tb[0])),
                             invert=math.degrees(math.atan2(ta[2], ta[0])), outvert=math.degrees(math.atan2(tb[2], tb[0]))))
            for q in range(a, b + 1): used[q] = True
            i = b
        else: i += 1
    corners = []
    for i in range(1, n - 1):
        if not used[i]:
            t = turnat(Q, i)
            if t > 1e-3:
                d0, d1 = sub(Q[i], Q[i - 1]), sub(Q[i + 1], Q[i])
                corners.append(dict(kind="corner", S=Q[i][0], X=Q[i][1], Y=Q[i][2], delta=t,
                                    inplan=math.degrees(math.atan2(d0[1], d0[0])), outplan=math.degrees(math.atan2(d1[1], d1[0])),
                                    invert=math.degrees(math.atan2(d0[2], d0[0])), outvert=math.degrees(math.atan2(d1[2], d1[0]))))
    els = sorted(arcs + corners, key=lambda e: e["S"])
    # straight legs between consecutive elements (tangent point to tangent point)
    return Q, els
if __name__ == "__main__":
    res = {}
    for name, d in pts.items():
        Q, els = analyse(d["p"])
        d0, d1 = sub(Q[1], Q[0]), sub(Q[-1], Q[-2])
        res[name] = dict(group=d["group"], loc=d["loc"], start=Q[0], end=Q[-1], els=els, raw=d["p"],
                         startDir=[math.degrees(math.atan2(d0[1], d0[0])), math.degrees(math.atan2(d0[2], d0[0]))],
                         endDir=[math.degrees(math.atan2(d1[1], d1[0])), math.degrees(math.atan2(d1[2], d1[0]))])
    json.dump(res, open("C:/SBM/qc/bonner-unit5/pt-profiles/work/analysis2.json", "w"))
    print(len(res))
