"""Agent review: per-family plan-vs-model comparison values from work/analysis2.json (cantilever T, bottom continuity B, closure C, end span)."""
import sys; sys.path.insert(0, "C:/SBM/qc/bonner-unit5/pt-profiles")
import json, re, collections, math
from analyze import faces, FT, IN
res = json.load(open("C:/SBM/qc/bonner-unit5/pt-profiles/work/analysis2.json"))
rowA = [15*12+8.5, 14*12+11.5, 14*12+2.5, 13*12+5.5, 12*12+4.5, 11*12+3.5, 10*12+2.5, 9*12+1.5, 8*12+4.5, 7*12+7.5, 6*12+10.5]
def lane_of(xin):
    for i, v in enumerate(rowA):
        if abs(abs(xin) - v) < 0.02: return i + 1
    return None
agg = collections.defaultdict(list)
def add(key, val, who): agg[key].append((val, who))
for name, r in res.items():
    m = re.match(r"(Bent|Span) (\d+) (\w+)( [LR])?$", name)
    kind, num, tn = m.group(1), int(m.group(2)), m.group(3)
    els = r["els"]
    if r["group"] == "Cantilever":
        fam = tn
        for lab, p in (("start", r["start"]), ("end", r["end"])):
            sg, b, t = faces(p[0])
            add((fam, "anchor setback from bulkhead face (in)"), t * 12, name)
            add((fam, "anchor depth below top (in)"), p[2] / IN, name)
            add((fam, "anchor lateral from CL (in)"), abs(p[1]) / IN, name)
        add((fam, "anchor exit plan angle (deg)"), abs(r["startDir"][0]), name)
        add((fam, "anchor exit plan angle (deg)"), abs(r["endDir"][0]), name)
        add((fam, "anchor exit vertical angle (deg)"), abs(r["startDir"][1]), name)
        arcs = [e for e in els if e["kind"] == "arc"]
        corners = [e for e in els if e["kind"] == "corner"]
        for e in (arcs[0], arcs[-1]):
            sg, b, t = faces(e["S"])
            add((fam, "anchor P.I. from bulkhead face (ft)"), t, name)
            add((fam, "anchor curve R (ft)"), e["R"] / FT, name)
            add((fam, "anchor P.I. depth (in)"), e["Y"] / IN, name)
        for e in arcs[1:-1]:
            sg, b, t = faces(e["S"])
            add((fam, "lane-change P.I. min(distance to bent face, to bulkhead face) (ft)"), min(b, t), name)
            add((fam, "lane-change curve R (ft)"), e["R"] / FT, name)
            add((fam, "lane-change P.I. on a lane (1) / off (0)"), 1 if lane_of(e["X"] / IN) else 0, name)
        for e in corners:
            add((fam, "direction change at a joint (deg)"), e["delta"], name)
        add((fam, "row at anchor P.I. (A=7.25 in, B=16.25 in)"), round(arcs[0]["Y"] / IN, 2), name)
    elif r["group"] in ("Bottom", "Closure"):
        sp = num
        fam = tn + (" end span" if sp in (19, 29) else "")
        arcs = [e for e in els if e["kind"] == "arc"]
        corners = [e for e in els if e["kind"] == "corner"]
        for lab, p, d in (("start", r["start"], r["startDir"]), ("end", r["end"], r["endDir"])):
            sg, b, t = faces(p[0])
            add((fam, "anchor from bent-side face (ft)"), b, name)
            add((fam, "anchor depth below top (in)"), p[2] / IN, name)
            add((fam, "anchor lateral from CL (in)"), abs(p[1]) / IN, name)
            add((fam, "anchor leg plan angle (deg)"), abs(d[0]), name)
            add((fam, "anchor leg vertical angle (deg)"), abs(d[1]), name)
        if arcs:
            for e in (arcs[0], arcs[-1]):
                sg, b, t = faces(e["S"])
                add((fam, "anchor curve R (ft)"), e["R"] / FT, name)
                add((fam, "anchor curve deflection (deg)"), e["delta"], name)
                add((fam, "anchor P.I. from bent-side face (ft)"), b, name)
                add((fam, "anchor P.I. depth (in)"), e["Y"] / IN, name)
            for e in arcs[1:-1]:
                sg, b, t = faces(e["S"])
                add((fam, "lane-change P.I. from bent-side face (ft)"), round(b, 4), name)
                add((fam, "lane-change curve R (ft)"), e["R"] / FT, name)
        for e in corners:
            add((fam, "direction change at a joint (deg)"), e["delta"], name)
out = []
for (fam, item), vals in sorted(agg.items()):
    vs = [v for v, _ in vals]
    distinct = sorted(set(round(v, 3) for v in vs))
    out.append((fam, item, len(vs), min(vs), max(vs), distinct[:12]))
    print(f"{fam:12s} | {item:70s} | n {len(vs):4d} | {min(vs):9.4f} .. {max(vs):9.4f} | {distinct[:10]}")
json.dump({f"{f}|{i}": [[v, w] for v, w in agg[(f, i)]] for (f, i) in agg}, open("C:/SBM/qc/bonner-unit5/pt-profiles/work/families.json", "w"))
