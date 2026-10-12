"""Vector geometry extraction from CAD-plotted PDF sheets (no text layer, vector strokes).

Collects the solid strokes of a given width in a page region, turns cubic Beziers into circular arcs (or lines), chains the
segments into closed loops and reports them in REAL inches (drawing scale), y up. Used for the NHT arch bracing gussets
(docs/plan-data/nht-0047-07-228, PDF p.129-140, 3/4" = 1'-0" on the 17 x 11 plot = 4.5 pt per real inch).
"""
import math, pymupdf

PDF = r'C:\SBMData\0047-07-228 NHT 100% Plans_09-04-2020_comments_Reduced.pdf'


def bez(p, t):
    a, b, c, d = p
    u = 1 - t
    return (u**3*a[0] + 3*u*u*t*b[0] + 3*u*t*t*c[0] + t**3*d[0], u**3*a[1] + 3*u*u*t*b[1] + 3*u*t*t*c[1] + t**3*d[1])


def circle3(a, b, c):
    ax, ay = a; bx, by = b; cx, cy = c
    d = 2 * (ax*(by-cy) + bx*(cy-ay) + cx*(ay-by))
    if abs(d) < 1e-12: return None
    ux = ((ax*ax+ay*ay)*(by-cy) + (bx*bx+by*by)*(cy-ay) + (cx*cx+cy*cy)*(ay-by)) / d
    uy = ((ax*ax+ay*ay)*(cx-bx) + (bx*bx+by*by)*(ax-cx) + (cx*cx+cy*cy)*(bx-ax)) / d
    return (ux, uy), math.hypot(ax-ux, ay-uy)


def seg_from_bezier(p, tol=0.06):
    """('L', a, b) when the Bezier is straight, else ('A', a, b, center, r, mid) when circular within tol (pt), else ('B', points)."""
    a, d = p[0], p[3]
    ln = math.hypot(d[0]-a[0], d[1]-a[1])
    def off(q):
        if ln < 1e-9: return math.hypot(q[0]-a[0], q[1]-a[1])
        return abs((d[0]-a[0])*(q[1]-a[1]) - (d[1]-a[1])*(q[0]-a[0])) / ln
    if max(off(p[1]), off(p[2]), off(bez(p, .5))) < tol: return ('L', a, d)
    m = bez(p, .5)
    c = circle3(a, m, d)
    if c:
        (cx, cy), r = c
        if all(abs(math.hypot(*(lambda q: (q[0]-cx, q[1]-cy))(bez(p, t))) - r) < tol for t in (.15, .3, .7, .85)):
            return ('A', a, d, (cx, cy), r, m)
    return ('B', p)


def strokes(page, region, width=0.72, wtol=0.05):
    """Solid strokes of the given width intersecting region (fractions x0,y0,x1,y1 of the page)."""
    d = pymupdf.open(PDF)
    pg = d[page-1]; r = pg.rect
    clip = pymupdf.Rect(region[0]*r.width, region[1]*r.height, region[2]*r.width, region[3]*r.height)
    out = []
    for x in pg.get_drawings():
        if x['type'] != 's': continue
        rr = x['rect']
        if rr.x1 < clip.x0 or rr.x0 > clip.x1 or rr.y1 < clip.y0 or rr.y0 > clip.y1: continue
        if abs((x.get('width') or 0) - width) > wtol: continue
        dashed = bool(x.get('dashes')) and x['dashes'] not in ('[] 0',)
        for it in x['items']:
            if it[0] == 'l': s = ('L', (it[1].x, it[1].y), (it[2].x, it[2].y))
            elif it[0] == 'c': s = seg_from_bezier([(q.x, q.y) for q in it[1:5]])
            else: continue
            pts = [s[1], s[2]] if s[0] != 'B' else [s[1][0], s[1][3]]
            if not all(clip.contains(pymupdf.Point(*q)) for q in pts): continue
            out.append((s, dashed))
    return out


def merge_arcs(segs, tol=0.08):
    """Consecutive arcs with the same centre / radius -> one arc (keeps the first start, last end and a true mid point)."""
    out = []
    for s in segs:
        if out and s[0] == 'A' and out[-1][0] == 'A':
            p = out[-1]
            if math.hypot(p[3][0]-s[3][0], p[3][1]-s[3][1]) < tol*3 and abs(p[4]-s[4]) < tol*3 and math.hypot(p[2][0]-s[1][0], p[2][1]-s[1][1]) < tol:
                out[-1] = ('A', p[1], s[2], p[3], p[4], p[2])   # mid = old joint (on the circle)
                continue
        out.append(s)
    return out


def is_circle(loop):
    return len(loop) >= 2 and all(s[0] == 'A' for s in loop) and max(s[4] for s in loop) - min(s[4] for s in loop) < 0.1


def loops(segs, tol=0.12):
    """Chains segments (with orientation flips) into closed loops; returns (loops, leftovers)."""
    rest = [s for s in segs]
    def rev(s):
        if s[0] == 'L': return ('L', s[2], s[1])
        if s[0] == 'A': return ('A', s[2], s[1], s[3], s[4], s[5])
        return ('B', list(reversed(s[1])))
    def ends(s): return (s[1], s[2]) if s[0] != 'B' else (s[1][0], s[1][3])
    def near(p, q): return math.hypot(p[0]-q[0], p[1]-q[1]) < tol
    found = []
    while rest:
        chain = [rest.pop(0)]
        progress = True
        while progress:
            progress = False
            if near(ends(chain[-1])[1], ends(chain[0])[0]) and len(chain) > 1: break
            for i, s in enumerate(rest):
                a, b = ends(s)
                if near(a, ends(chain[-1])[1]): chain.append(s); rest.pop(i); progress = True; break
                if near(b, ends(chain[-1])[1]): chain.append(rev(s)); rest.pop(i); progress = True; break
        closed = len(chain) > 1 and near(ends(chain[-1])[1], ends(chain[0])[0])
        found.append((chain, closed))
    return [c for c, ok in found if ok], [c for c, ok in found if not ok]


def all_paths(page):
    d = pymupdf.open(PDF)
    pg = d[page-1]
    out = []
    for x in pg.get_drawings():
        segs = []
        for it in x['items']:
            if it[0] == 'l': segs.append(('L', (it[1].x, it[1].y), (it[2].x, it[2].y)))
            elif it[0] == 'c': segs.append(seg_from_bezier([(q.x, q.y) for q in it[1:5]]))
        ds = x.get('dashes') or ''
        out.append({'type': x['type'], 'w': round(x.get('width') or 0, 2), 'dashed': ds not in ('', '[] 0'), 'segs': segs, 'rect': x['rect']})
    return out


def dedupe(segs, tol=0.05):
    out = []
    for s in segs:
        if s[0] == 'B': out.append(s); continue
        a, b = s[1], s[2]
        if math.hypot(a[0]-b[0], a[1]-b[1]) < 0.05: continue
        dup = False
        for t in out:
            if t[0] != s[0]: continue
            if (math.dist(t[1], a) < tol and math.dist(t[2], b) < tol) or (math.dist(t[1], b) < tol and math.dist(t[2], a) < tol): dup = True; break
        if not dup: out.append(s)
    return out
