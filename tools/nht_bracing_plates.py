"""NHT arch upper lateral bracing connections (PDF p.129-140): plate geometry from the VECTOR drawing.

The sheets have no text layer but the linework is vector (CAD plot, coordinates rounded to 0.1 pt = 0.022" at 3/4" = 1'-0",
4.5 pt per real inch). For every detail sheet (U1 ... U11; p.129 and p.130 are the same sheet) this script reads
  * the top / bottom GUSSET views (closed 0.72-pt outline incl. arcs, the bend line chord when bent, 15/16" holes,
    the dashed W8x28 outline under it = brace web centre line and brace end), drawing frame x right / y up, inches;
  * VIEW A-A (rib inner flange face seen from the bracing): rib flange band edges, web centre line, WP dot, the two
    7/8" connection plates (outline + holes) and the dashed 1" stiffeners; frame: origin WP, x along the web centre line
    tangent at the WP, y radial (up), inches.
and writes docs/plan-data/nht-0047-07-228/upper-lateral-bracing-plates.csv (+ overlay PNGs into temp/ for checking).
Arcs: signed radius (+ = counter-clockwise from the vertex to the next one, y up); measured from the vector, the
printed radii (R 2", R 2'-0") are applied in the model when they agree.

usage: python -I tools/nht_bracing_plates.py <repo root>
"""
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pdfvec as v

PT_IN = 4.5          # pt per real inch (3/4" = 1'-0" on the 17 x 11 plot)
H = 792.0
SHEETS = {1: 129, 2: 131, 3: 132, 4: 133, 5: 134, 6: 135, 7: 136, 8: 137, 9: 138, 10: 139, 11: 140}
HOLE_R = (1.7, 2.5)  # pt


def solid(paths, w=0.72):
    out = []
    for p in paths:
        if p['type'] != 's' or p['dashed'] or abs(p['w'] - w) > 0.05: continue
        out.extend(s for s in p['segs'] if s[0] != 'B')
    return out


def on_seg(q, s, tol=0.12):
    a, b = s[1], s[2]
    l = math.dist(a, b)
    if l < 1e-9: return False
    t = ((q[0] - a[0]) * (b[0] - a[0]) + (q[1] - a[1]) * (b[1] - a[1])) / l / l
    return -1e-3 <= t <= 1 + 1e-3 and abs((b[0] - a[0]) * (q[1] - a[1]) - (b[1] - a[1]) * (q[0] - a[0])) / l < tol


def _ang(c, p): return math.atan2(p[1] - c[1], p[0] - c[0])


def _arc_param(s, q):
    """Parameter (0..1) of point q along arc s (None when off the arc)."""
    c, r = s[3], s[4]
    if abs(math.dist(c, q) - r) > 0.2: return None
    a0, am, a1 = _ang(c, s[1]), _ang(c, s[5]), _ang(c, s[2])
    def norm(x): return (x + 2 * math.pi) % (2 * math.pi)
    sweep_ccw = norm(a1 - a0)
    ccw = norm(am - a0) < sweep_ccw          # going a0 -> a1 counter-clockwise (pdf frame) passes the mid point
    sweep = sweep_ccw if ccw else 2 * math.pi - sweep_ccw
    t = norm(_ang(c, q) - a0) if ccw else norm(a0 - _ang(c, q))
    return t / sweep if t <= sweep + 1e-3 else None


def _arc_point(s, t):
    c, r = s[3], s[4]
    a0, am, a1 = _ang(c, s[1]), _ang(c, s[5]), _ang(c, s[2])
    def norm(x): return (x + 2 * math.pi) % (2 * math.pi)
    sweep_ccw = norm(a1 - a0)
    ccw = norm(am - a0) < sweep_ccw
    a = a0 + (sweep_ccw if ccw else -(2 * math.pi - sweep_ccw)) * t
    return (c[0] + r * math.cos(a), c[1] + r * math.sin(a))


def split_at_dangling(segs, tol=0.15):
    """A chord end that stops ON another segment (not at its end) splits that segment there."""
    ends = {}
    for s in segs:
        for q in (s[1], s[2]):
            k = next((e for e in ends if math.dist(e, q) < tol), None)
            if k is None: ends[q] = 1
            else: ends[k] += 1
    out = list(segs)
    for q, n in ends.items():
        if n != 1: continue
        for i, s in enumerate(out):
            if math.dist(s[1], q) < tol or math.dist(s[2], q) < tol: continue
            if s[0] == 'L' and on_seg(q, s, 0.15):
                out[i:i + 1] = [('L', s[1], q), ('L', q, s[2])]; break
            if s[0] == 'A':
                t = _arc_param(s, q)
                if t is not None and 0.01 < t < 0.99:
                    out[i:i + 1] = [('A', s[1], q, s[3], s[4], _arc_point(s, t / 2)), ('A', q, s[2], s[3], s[4], _arc_point(s, (1 + t) / 2))]; break
    return out


def prune_spurs(segs, tol=0.15, lmax=3.0):
    """Removes short dangling pieces (overshoots left after splitting)."""
    segs = list(segs)
    while True:
        cnt = []
        def deg(q): return sum(1 for s in segs for e in (s[1], s[2]) if math.dist(e, q) < tol)
        bad = [s for s in segs if math.dist(s[1], s[2]) < lmax and (deg(s[1]) == 1 or deg(s[2]) == 1)]
        if not bad: return segs
        segs = [s for s in segs if s not in bad]


def drop_covered(segs):
    """Removes straight pieces lying on a longer straight segment (overdrawn lines)."""
    ls = [s for s in segs if s[0] == 'L']
    out = []
    for s in segs:
        if s[0] == 'L' and any(t is not s and math.dist(t[1], t[2]) > math.dist(s[1], s[2]) + 0.05 and on_seg(s[1], t) and on_seg(s[2], t) for t in ls): continue
        out.append(s)
    return out


def holes_of(paths):
    """Bolt holes: arcs of radius HOLE_R (any 0.72 solid path) grouped by centre, covering >= 300 deg."""
    arcs = []
    for p in paths:
        if p['type'] != 's' or p['dashed'] or abs(p['w'] - 0.72) > 0.05: continue
        arcs.extend(s for s in p['segs'] if s[0] == 'A' and HOLE_R[0] < s[4] < HOLE_R[1])
    groups = []
    for s in arcs:
        g = next((g for g in groups if math.dist(g[0], s[3]) < 0.3), None)
        if g is None: groups.append([s[3], [s]])
        else: g[1].append(s)
    hs = []
    for c, ss in groups:
        cover = 0.0
        for s in ss:
            a0, a1 = _ang(s[3], s[1]), _ang(s[3], s[2])
            cover += min(abs(a1 - a0) % (2 * math.pi), 2 * math.pi - abs(a1 - a0) % (2 * math.pi))
        # duplicates (overdrawn circles) count twice: require 300 deg
        if cover >= math.radians(300):
            cx = sum(s[3][0] for s in ss) / len(ss); cy = sum(s[3][1] for s in ss) / len(ss)
            hs.append(((cx, cy), sum(s[4] for s in ss) / len(ss)))
    return hs


def graph_components(segs, tol=0.15):
    nodes = []
    def node(p):
        for i, q in enumerate(nodes):
            if math.dist(p, q) < tol: return i
        nodes.append(p); return len(nodes) - 1
    edges = [(node(s[1]), node(s[2]), s) for s in segs]
    adj = {}
    for k, (a, b, s) in enumerate(edges):
        adj.setdefault(a, []).append(k); adj.setdefault(b, []).append(k)
    seen, comps = set(), []
    for k in range(len(edges)):
        if k in seen: continue
        stack, comp = [k], []
        while stack:
            e = stack.pop()
            if e in seen: continue
            seen.add(e); comp.append(e)
            for n in edges[e][:2]: stack.extend(adj[n])
        comps.append(comp)
    return nodes, edges, adj, comps


def walk(nodes, edges, comp):
    """Single cycle through the component's edges -> ordered segments (reoriented)."""
    adj = {}
    for k in comp:
        a, b, _ = edges[k]; adj.setdefault(a, []).append(k); adj.setdefault(b, []).append(k)
    if any(len(x) != 2 for x in adj.values()): return None
    start = edges[comp[0]][0]
    cur, used, out = start, set(), []
    while True:
        k = next((e for e in adj[cur] if e not in used), None)
        if k is None: break
        used.add(k)
        a, b, s = edges[k]
        if a != cur: s = rev(s); a, b = b, a
        out.append(s); cur = b
        if cur == start: break
    return out if len(used) == len(comp) else None


def rev(s):
    if s[0] == 'L': return ('L', s[2], s[1])
    return ('A', s[2], s[1], s[3], s[4], s[5])


def loop_with_chord(segs):
    """Outline loop of a plate drawn with (at most) one straight chord = bend line. Returns (loop, chord|None)."""
    nodes, edges, adj, comps = graph_components(segs)
    # parallel edges between the same two nodes (an arc overshooting along a line): keep the line
    seen = {}
    for k, (a, b, s) in enumerate(edges):
        key = (min(a, b), max(a, b))
        if key in seen and a != b:
            j = seen[key]
            drop = k if s[0] == 'A' or edges[j][2][0] == 'L' else j
            comps = [[e for e in c if e != drop] for c in comps]
            if drop == j: seen[key] = k
        else: seen[key] = k
    best = None
    for comp in comps:
        if len(comp) < 3: continue
        deg = {}
        for k in comp:
            for n in edges[k][:2]: deg[n] = deg.get(n, 0) + 1
        if all(d == 2 for d in deg.values()):
            lp = walk(nodes, edges, comp)
            if lp and (best is None or len(lp) > len(best[0])): best = (lp, None)
            continue
        odd = [n for n, d in deg.items() if d == 3]
        if len(odd) == 2 and all(d in (2, 3) for d in deg.values()):
            for k in comp:
                a, b, s = edges[k]
                if s[0] == 'L' and {a, b} == set(odd):
                    lp = walk(nodes, edges, [e for e in comp if e != k])
                    if lp and (best is None or len(lp) > len(best[0])): best = (lp, s)
    return best


def inside(pt, loop):
    poly = [s[1] for s in loop]
    c = False
    for i in range(len(poly)):
        a, b = poly[i], poly[i - 1]
        if (a[1] > pt[1]) != (b[1] > pt[1]) and pt[0] < (b[0] - a[0]) * (pt[1] - a[1]) / (b[1] - a[1]) + a[0]: c = not c
    return c


def signed_r(s):
    """Signed radius in the y-UP frame: + = counter-clockwise from s[1] to s[2]."""
    a, b, c, r, m = s[1], s[2], s[3], s[4], s[5]
    # orientation of (a, m, b) in pdf (y down) coordinates; y flip reverses it
    cr = (m[0] - a[0]) * (b[1] - a[1]) - (m[1] - a[1]) * (b[0] - a[0])
    return r / PT_IN * (1 if cr > 0 else -1)


def area(loop):
    p = [s[1] for s in loop]
    return 0.5 * sum(p[i - 1][0] * p[i][1] - p[i][0] * p[i - 1][1] for i in range(len(p)))


def dashed_lines(paths, box):
    out = []
    for p in paths:
        if p['type'] != 's' or not p['dashed']: continue
        for s in p['segs']:
            if s[0] != 'L' or math.dist(s[1], s[2]) < 0.5: continue
            if all(box[0] <= q[0] <= box[2] and box[1] <= q[1] <= box[3] for q in (s[1], s[2])): out.append((p['w'], s))
    return out


def brace_legs(dl, loop):
    """W8x28 outlines (dashed 0.24) under a gusset: web lines (pairs ~1.28 pt apart) -> axis, end line (~29.4 pt) -> brace end."""
    lines = [s for w, s in dl if abs(w - 0.24) < 0.05]
    # unique
    uniq = []
    for s in lines:
        if not any((math.dist(s[1], t[1]) < 0.2 and math.dist(s[2], t[2]) < 0.2) or (math.dist(s[1], t[2]) < 0.2 and math.dist(s[2], t[1]) < 0.2) for t in uniq): uniq.append(s)
    def d(s): return ((s[2][0] - s[1][0]), (s[2][1] - s[1][1]))
    def unit(x): l = math.hypot(*x); return (x[0] / l, x[1] / l)
    ends = [s for s in uniq if abs(math.dist(s[1], s[2]) - 6.535 * PT_IN) < 1.0]
    legs = []
    for e in ends:
        n = unit(d(e)); ax = (-n[1], n[0])
        mid = ((e[1][0] + e[2][0]) / 2, (e[1][1] + e[2][1]) / 2)
        # web lines: parallel to ax, starting on the end line within 1 pt of its middle
        web = [s for s in uniq if abs(abs(unit(d(s))[0] * ax[0] + unit(d(s))[1] * ax[1]) - 1) < 2e-3
               and min(abs((q[0] - mid[0]) * n[0] + (q[1] - mid[1]) * n[1]) for q in (s[1], s[2])) < 1.0
               and min(abs((q[0] - mid[0]) * ax[0] + (q[1] - mid[1]) * ax[1]) for q in (s[1], s[2])) < 0.3]
        if len(web) < 2: continue
        offs = sorted(((s[1][0] - mid[0]) * n[0] + (s[1][1] - mid[1]) * n[1]) for s in web)
        c = (offs[0] + offs[-1]) / 2
        p0 = (mid[0] + n[0] * c, mid[1] + n[1] * c)
        # axis direction: from the end into the brace (away from the gusset weld edge = the far side of the end line)
        far = max((q for s in web for q in (s[1], s[2])), key=lambda q: abs((q[0] - p0[0]) * ax[0] + (q[1] - p0[1]) * ax[1]))
        sgn = 1 if (far[0] - p0[0]) * ax[0] + (far[1] - p0[1]) * ax[1] > 0 else -1
        legs.append({'end': p0, 'dir': (ax[0] * sgn, ax[1] * sgn), 'web_gap_in': (offs[-1] - offs[0]) / PT_IN, 'flange_in': math.dist(e[1], e[2]) / PT_IN})
    return legs


def gussets(page, paths, hs):
    segs = [s for s in solid(paths) if s[1][0] > 0.58 * 1224 and s[1][1] > 0.36 * H and s[1][0] < 0.88 * 1224 and math.dist(s[1], s[2]) > 0.05]
    segs = [s for s in segs if not (s[0] == 'A' and s[4] < 2.6)]
    segs = prune_spurs(split_at_dangling(split_at_dangling(drop_covered(v.dedupe(segs)))))
    nodes, edges, adj, comps = graph_components(segs)
    found = []
    for comp in comps:
        sub = [edges[k][2] for k in comp]
        if len(sub) < 5: continue
        lc = loop_with_chord(sub)
        if not lc: continue
        loop, chord = lc
        if not any(s[0] == 'A' and 6 < s[4] < 12 for s in loop): continue    # R 2" corners
        hin = [h for h in hs if inside(h[0], loop)]
        if len(hin) < 4: continue
        xs = [s[1][0] for s in loop]; ys = [s[1][1] for s in loop]
        box = (min(xs) - 2, min(ys) - 2, max(xs) + 2, max(ys) + 2)
        legs = brace_legs(dashed_lines(paths, box), loop)
        found.append({'loop': loop, 'chord': chord, 'holes': hin, 'legs': legs, 'box': box})
    return found


def view_aa(paths, hs):
    """VIEW A-A: band of the rib inner flange (top region of the sheet)."""
    top = [p for p in paths if p['rect'].y1 < 0.36 * H and p['rect'].x0 > 0.40 * 1224 and p['rect'].x1 < 0.76 * 1224]
    long_arcs = []
    for p in top:
        if p['type'] != 's' or abs(p['w'] - 0.72) > 0.05: continue
        a = [s for s in p['segs'] if s[0] == 'A' and s[4] > 1000]
        if len(a) >= 3: long_arcs.append((p['dashed'], a))
    web = [a for d, a in long_arcs if d]
    edges = [a for d, a in long_arcs if not d]
    def yat(arcs, x):
        for s in arcs:
            lo, hi = sorted((s[1][0], s[2][0]))
            if lo - 0.01 <= x <= hi + 0.01:
                (cx, cy), r = s[3], s[4]
                dy = math.sqrt(max(0, r * r - (x - cx) ** 2))
                return cy - dy if abs(cy - dy - s[1][1]) < abs(cy + dy - s[1][1]) else cy + dy
        return None
    # unique web lines / edges by y at their mid x
    def uniq(arcs_list):
        out = []
        for a in arcs_list:
            xm = (a[0][1][0] + a[-1][2][0]) / 2
            y = yat(a, xm)
            if y is not None and not any(abs(y - yat(b, xm)) < 0.3 for b in out if yat(b, xm) is not None): out.append(a)
        return out
    web = uniq(web); edges = uniq(edges)
    if len(edges) > 2:
        xm = (edges[0][0][1][0] + edges[0][-1][2][0]) / 2
        ys = sorted(edges, key=lambda a: yat(a, xm) if yat(a, xm) is not None else 0)
        edges = [ys[0], ys[-1]]
    # CP loops: closed solid loops of straight lines inside the band, deduped
    segs = v.dedupe([s for p in top if p['type'] == 's' and not p['dashed'] and abs(p['w'] - 0.72) < 0.05 for s in p['segs'] if s[0] == 'L'])
    nodes, ed, adj, comps = graph_components(segs)
    cps = []
    for comp in comps:
        lp = walk(nodes, ed, comp)
        if not lp or len(lp) < 4: continue
        if abs(area(lp)) < 100: continue
        hin = [h for h in hs if inside(h[0], lp)]
        if len(hin) < 3: continue
        cps.append({'loop': lp, 'holes': hin})
    # stiffeners: dashed 0.24 rectangles ~1" wide
    dl = [s for p in top if p['type'] == 's' and p['dashed'] and abs(p['w'] - 0.24) < 0.05 for s in p['segs'] if s[0] == 'L']
    dl = v.dedupe(dl)
    vert = [s for s in dl if abs(s[1][0] - s[2][0]) < 0.3 and abs(s[1][1] - s[2][1]) > 3]
    # dots (filled, ~2.2 pt)
    dots = []
    for p in top:
        r = p['rect']
        if p['type'] == 'f' and 1.8 < r.width < 2.6 and 1.8 < r.height < 2.6:
            c = ((r.x0 + r.x1) / 2, (r.y0 + r.y1) / 2)
            if not any(math.dist(c, q) < 0.3 for q in dots): dots.append(c)
    return {'web': web, 'edges': edges, 'cps': cps, 'vert': vert, 'dots': dots, 'yat': yat}


def collinear(a, b, c, tol=0.12):
    return abs((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])) / max(1e-9, math.dist(a, c)) < tol


def clean(loop, rmax_in=200.0):
    """Arcs flatter than rmax -> lines; merge collinear lines and pieces of the same circle."""
    out = []
    for s in loop:
        if s[0] == 'A' and s[4] / PT_IN > rmax_in: s = ('L', s[1], s[2])
        if out:
            p = out[-1]
            if p[0] == 'L' and s[0] == 'L' and collinear(p[1], p[2], s[2]):
                out[-1] = ('L', p[1], s[2]); continue
            if p[0] == 'A' and s[0] == 'A' and abs(p[4] - s[4]) / p[4] < 0.05 and math.dist(p[3], s[3]) < 0.05 * p[4] and (signed_r(p) > 0) == (signed_r(s) > 0):
                c3 = v.circle3(p[1], p[2], s[2])
                if c3: out[-1] = ('A', p[1], s[2], c3[0], c3[1], p[2]); continue
        out.append(s)
    if len(out) > 2 and out[0][0] == 'L' and out[-1][0] == 'L' and collinear(out[-1][1], out[0][1], out[0][2]):
        out[0] = ('L', out[-1][1], out[0][2]); out.pop()
    return out


def fmt(x):
    return f'{x:.4f}' if isinstance(x, float) else str(x)


def run(root):
    rows, over = [], []
    for det, page in SHEETS.items():
        P = v.all_paths(page)
        hs = holes_of(P)
        G = gussets(page, P, hs)
        G.sort(key=lambda g: g['box'][1])
        if len(G) != 2:
            print(f'U{det} p.{page}: {len(G)} gussets found'); continue
        for name, g in zip(('G-TOP', 'G-BOT'), G):
            loop = clean(g['loop'])
            if area(loop) < 0: loop = [rev(s) for s in reversed(loop)]
            ymin = min(s[1][1] for s in loop)
            if g['chord']:
                top = min((g['chord'][1], g['chord'][2]), key=lambda q: q[1])
                weld = [i for i, s in enumerate(loop) if s[0] == 'L' and (math.dist(s[1], top) < 0.2 or math.dist(s[2], top) < 0.2)]
            else:
                weld = [i for i, s in enumerate(loop) if s[0] == 'L' and max(s[1][1], s[2][1]) - ymin < 4.5 and math.dist(s[1], s[2]) > 3 * PT_IN]
            if not weld: print(f'U{det} {name}: no weld edge'); continue
            ox = min(min(loop[i][1][0], loop[i][2][0]) for i in weld); oy = ymin
            X = lambda q: ((q[0] - ox) / PT_IN, -(q[1] - oy) / PT_IN)
            for i, s in enumerate(loop):
                x, y = X(s[1])
                rows.append((f'U{det}', name, 'V', i, x, y, signed_r(s) if s[0] == 'A' else 0.0, 1 if i in weld else 0, 0, f'p.{page}'))
            for i, (c, r) in enumerate(sorted(g['holes'])):
                x, y = X(c); rows.append((f'U{det}', name, 'H', i, x, y, 2 * r / PT_IN, 0, 0, f'p.{page}'))
            if g['chord']:
                for i, q in enumerate((g['chord'][1], g['chord'][2])):
                    x, y = X(q); rows.append((f'U{det}', name, 'BEND', i, x, y, 0.0, 0, 0, f'p.{page}'))
            for i, l in enumerate(g['legs']):
                x, y = X(l['end']); rows.append((f'U{det}', name, 'BRACE', i, x, y, l['flange_in'], l['dir'][0], -l['dir'][1], f'p.{page}'))
            over.append((page, name, g, loop, (ox, oy)))
        A = view_aa(P, hs)
        if len(A['web']) != 2 or len(A['edges']) != 2 or len(A['cps']) != 2:
            print(f'U{det} p.{page}: A-A web {len(A["web"])} edges {len(A["edges"])} cps {len(A["cps"])}'); continue
        cps = sorted(A['cps'], key=lambda c: min(s[1][1] for s in c['loop']))
        xs = [s[1][0] for c in cps for s in c['loop']]
        def webcl(x):
            return sum(A['yat'](w, x) for w in A['web']) / 2
        cand = [d for d in A['dots'] if min(xs) < d[0] < max(xs)]
        wp = min(cand, key=lambda d: abs(d[1] - webcl(d[0])))
        t = (2.0, webcl(wp[0] + 1) - webcl(wp[0] - 1)); tl = math.hypot(*t); t = (t[0] / tl, t[1] / tl)
        n = (t[1], -t[0])
        if n[1] > 0: n = (-n[0], -n[1])
        def AX(q):
            d = (q[0] - wp[0], q[1] - wp[1]); return ((d[0] * t[0] + d[1] * t[1]) / PT_IN, (d[0] * n[0] + d[1] * n[1]) / PT_IN)
        rows.append((f'U{det}', 'AA', 'WEB', 0, 0.0, AX((wp[0], webcl(wp[0])))[1], 0.0, 0, 0, f'p.{page}'))
        for k, e in enumerate(sorted(A['edges'], key=lambda a: A['yat'](a, wp[0]))):
            rows.append((f'U{det}', 'AA', 'EDGE', k, 0.0, AX((wp[0], A['yat'](e, wp[0])))[1], 0.0, 0, 0, f'p.{page}'))
        for name, c in zip(('CP-TOP', 'CP-BOT'), cps):
            lp = c['loop']
            if area(lp) < 0: lp = [rev(s) for s in reversed(lp)]
            for i, s in enumerate(lp):
                x, y = AX(s[1]); rows.append((f'U{det}', name, 'V', i, x, y, 0.0, 0, 0, f'p.{page}'))
            for i, (cc, r) in enumerate(sorted(c['holes'])):
                x, y = AX(cc); rows.append((f'U{det}', name, 'H', i, x, y, 2 * r / PT_IN, 0, 0, f'p.{page}'))
        vert = sorted(A['vert'], key=lambda s: s[1][0])
        rects = []
        for i, a in enumerate(vert):
            for b in vert[i + 1:]:
                if 3.5 < abs(b[1][0] - a[1][0]) < 5.5:
                    ya = sorted((a[1][1], a[2][1])); yb = sorted((b[1][1], b[2][1]))
                    if abs(ya[0] - yb[0]) < 0.5 and abs(ya[1] - yb[1]) < 0.5: rects.append([a[1][0], b[1][0], ya[0], ya[1]])
        merged = []
        for r in sorted(rects, key=lambda r: (round(r[0]), r[2])):
            m = next((q for q in merged if abs(q[0] - r[0]) < 0.3 and abs(q[1] - r[1]) < 0.3 and r[2] <= q[3] + 0.3 and r[3] >= q[2] - 0.3), None)
            if m: m[2] = min(m[2], r[2]); m[3] = max(m[3], r[3])
            else: merged.append(list(r))
        for r in merged:
            name = 'ST-TOP' if (r[2] + r[3]) / 2 < webcl((r[0] + r[1]) / 2) else 'ST-BOT'
            for i, q in enumerate(((r[0], r[3]), (r[1], r[3]), (r[1], r[2]), (r[0], r[2]))):
                x, y = AX(q); rows.append((f'U{det}', name, 'V', i, x, y, 0.0, 0, 0, f'p.{page}'))
        over.append((page, 'AA', A, cps, wp, merged))
    path = os.path.join(root, 'docs', 'plan-data', 'nht-0047-07-228', 'upper-lateral-bracing-plates.csv')
    with open(path, 'w', newline='') as f:
        f.write('detail,plate,kind,seq,x_in,y_in,r_in,a,b,source\n')
        for r in rows: f.write(','.join(fmt(x) for x in r) + '\n')
    print(f'{len(rows)} rows -> {path}')
    return over


def overlay(over, outdir, dpi=200):
    from PIL import Image, ImageDraw
    import pymupdf
    d = pymupdf.open(v.PDF)
    pages = {}
    k = dpi / 72.0
    def P(q): return (q[0] * k, q[1] * k)
    def seg_pts(s):
        if s[0] == 'L': return [s[1], s[2]]
        return [_arc_point(s, t / 12) for t in range(13)]
    for item in over:
        page = item[0]
        if page not in pages:
            pix = d[page - 1].get_pixmap(dpi=dpi)
            im = Image.frombytes('RGB', (pix.width, pix.height), pix.samples)
            pages[page] = (im, ImageDraw.Draw(im))
        im, dr = pages[page]
        if item[1] == 'AA':
            _, _, A, cps, wp, st = item
            for c in cps:
                for s in c['loop']: dr.line([P(q) for q in seg_pts(s)], fill=(255, 0, 0), width=2)
                for h in c['holes']: x, y = P(h[0]); r = h[1] * k; dr.ellipse((x - r, y - r, x + r, y + r), outline=(0, 0, 255), width=2)
            for r in st:
                dr.rectangle((r[0] * k, r[2] * k, r[1] * k, r[3] * k), outline=(0, 160, 0), width=2)
            x, y = P(wp); dr.ellipse((x - 6, y - 6, x + 6, y + 6), outline=(255, 0, 255), width=3)
        else:
            _, name, g, loop, org = item
            for s in loop: dr.line([P(q) for q in seg_pts(s)], fill=(255, 0, 0), width=2)
            if g['chord']: dr.line([P(g['chord'][1]), P(g['chord'][2])], fill=(255, 128, 0), width=3)
            for h in g['holes']: x, y = P(h[0]); r = h[1] * k; dr.ellipse((x - r, y - r, x + r, y + r), outline=(0, 0, 255), width=2)
            for l in g['legs']:
                e = l['end']; dd = l['dir']
                dr.line([P(e), P((e[0] + dd[0] * 60, e[1] + dd[1] * 60))], fill=(0, 160, 0), width=2)
                x, y = P(e); dr.ellipse((x - 4, y - 4, x + 4, y + 4), fill=(0, 160, 0))
            x, y = P(org); dr.ellipse((x - 5, y - 5, x + 5, y + 5), outline=(255, 0, 255), width=2)
    os.makedirs(outdir, exist_ok=True)
    for page, (im, _) in pages.items():
        w, h = im.size
        im.crop((int(0.42 * w), int(0.05 * h), int(0.86 * w), int(0.93 * h))).save(os.path.join(outdir, f'overlay-p{page}.png'))


if __name__ == '__main__':
    root = sys.argv[1] if len(sys.argv) > 1 else '.'
    over = run(root)
    if len(sys.argv) > 2: overlay(over, sys.argv[2])
