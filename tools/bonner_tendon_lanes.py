"""Bonner B-2500 Unit 5: duct LANE of every cantilever (T) and bottom continuity (B) tendon at every segment joint, read from the VECTOR
linework of the LONGITUDINAL POST-TENSIONING LAYOUT plans (PDF p.285-296, one cantilever arm per sheet: 19D, 19U, 20D, 20U, 22D, 22U, 24D,
24U, 26D, 26U, 28D, 28U).

Method (no raster scaling; top slab rewritten 2026-10-31): joint = the lane ticks drawn on each joint line, or the segment label centre +- half a
typical segment snapped to the nearest drawn joint (line breaks alone are not joints: some sheets break the lines only at P.I. circles). Every row A
tendon is TRACED along its drawn line from its bent-end label toward the tip (pieces joined end to end, branches at P.I.s by the longest unbroken
continuation, short gaps under other lines bridged along the direction); its offset at each joint and at its end (= the anchor) gives the lanes.
Row B T5 / T6 are traced along their dashes from where they leave the row A line of their bent-end lane (accepted only when they end at P1 / P2).
Bottom slab (row C): at every joint the tendon lines crossing it are converted to an offset from CL girder (CL curve of the plan, lateral scale 1.499 pt / in checked on the
top slab duct grid: 106" over 158.9 pt) and snapped to the nearest duct (top slab row A ducts 1-11 of S3-35, bottom slab row C ducts 1-7).
Ducts of one row cannot cross, so the order of the tendons across the lanes is kept from the bent (top slab, labels at the bent end) or from
the closure (bottom slab, labels at the closure end): at joint k the tendons still present are zipped with the crossings in lane order. Row B
(dashed) tendons are drawn in short dash pieces: only their lane at the labelled end is read. A joint whose crossing count does not match is
written with lane '?' (UNRESOLVED).

usage: python -I tools/bonner_tendon_lanes.py [out.csv]   (default docs/plan-data/bonner-b2500/segmental-unit5/tendon-lanes.csv)
"""
import csv, math, re, sys, collections, os
import pymupdf

PDF = r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf"
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, '..', 'docs', 'plan-data', 'bonner-b2500', 'segmental-unit5')
IN = 12.0
ROW_A = [15*IN+8.5, 14*IN+11.5, 14*IN+2.5, 13*IN+5.5, 12*IN+4.5, 11*IN+3.5, 10*IN+2.5, 9*IN+1.5, 8*IN+4.5, 7*IN+7.5, 6*IN+10.5]
ROW_C = [18 + 9*i for i in range(7)]          # from the soffit edge (p.304: 1'-6" + 9" steps from the web side)
ROW_B_TENDONS = {'T5', 'T6', 'T14'}          # p.285 note 1 (C2, T5, T14 through row B ducts; T6 drawn dashed with T5, p.289-292)
LAT = 1.499
DEBUG = os.environ.get("BTL_DEBUG") == "1"
SHEETS = {285: (19, 'D'), 286: (19, 'U'), 287: (20, 'D'), 288: (20, 'U'), 289: (22, 'D'), 290: (22, 'U'),
          291: (24, 'D'), 292: (24, 'U'), 293: (26, 'D'), 294: (26, 'U'), 295: (28, 'D'), 296: (28, 'U')}


def page_geometry(pg):
    segs = []
    for d in pg.get_drawings():
        dash = d.get('dashes') or ''
        dashed = dash not in ('', '[] 0', None)
        for it in d['items']:
            if it[0] == 'l':
                segs.append((it[1].x, it[1].y, it[2].x, it[2].y, dashed, False, round(d.get('width') or 0, 2)))
            elif it[0] == 'c':
                P = it[1:5]; prev = None
                for k in range(9):
                    t = k / 8; u = 1 - t
                    x = u**3*P[0].x + 3*u*u*t*P[1].x + 3*u*t*t*P[2].x + t**3*P[3].x
                    y = u**3*P[0].y + 3*u*u*t*P[1].y + 3*u*t*t*P[2].y + t**3*P[3].y
                    if prev: segs.append((prev[0], prev[1], x, y, dashed, True, round(d.get('width') or 0, 2)))
                    prev = (x, y)
    return segs


def tick_joints(segs, y0, y1):
    """Joint x = centre of the short lane ticks (9 pt horizontal strokes) drawn on each joint line; >= 5 ticks per joint."""
    T = [s for s in segs if not s[4] and not s[5] and abs(s[3] - s[1]) < 0.05 and 7 < abs(s[2] - s[0]) < 11 and y0 < s[1] < y1]
    xs = sorted((s[0] + s[2]) / 2 for s in T)
    cl = []
    for x in xs:
        if cl and x - cl[-1][-1] <= 1.0: cl[-1].append(x)
        else: cl.append([x])
    return [sum(c) / len(c) for c in cl if len(c) >= 5]


def trace_pool(pg, y0, y1, ticks, dashed=False):
    """Solid (or, dashed=True, dashed) stroked lines in the band (tendon pieces); joint ticks and isolated dash-dot pieces removed."""
    P = []
    for d in pg.get_drawings():
        dash = d.get('dashes') or ''
        if dash not in ('', '[] 0', None) or d.get('fill') is not None: continue
        its = d['items']
        if any(it[0] not in ('l', 'c') for it in its): continue
        first, last = its[0][1], (its[-1][2] if its[-1][0] == 'l' else its[-1][4])
        if len(its) >= 3 and abs(first.x - last.x) < 0.5 and abs(first.y - last.y) < 0.5: continue        # closed: circles, triangles
        pieces = []
        for it in its:
            if it[0] == 'l': pieces.append((it[1].x, it[1].y, it[2].x, it[2].y, 0.5))
            else:
                Q = it[1:5]; prev = None
                for k in range(9):
                    t = k / 8; u = 1 - t
                    xx = u**3*Q[0].x + 3*u*u*t*Q[1].x + 3*u*t*t*Q[2].x + t**3*Q[3].x
                    yy = u**3*Q[0].y + 3*u*u*t*Q[1].y + 3*u*t*t*Q[2].y + t**3*Q[3].y
                    if prev: pieces.append((prev[0], prev[1], xx, yy, 0.05))
                    prev = (xx, yy)
        for (ax, ay, bx, by, lmin) in pieces:
            if not (y0 - 40 < ay < y1 + 40 and y0 - 40 < by < y1 + 40): continue
            L = math.hypot(bx - ax, by - ay)
            if L < lmin or abs(bx - ax) < 0.3 * L: continue          # dots of dash-dot lines / joint lines / steep strokes
            if abs(by - ay) < 0.05 and 7 < L < 11 and any(abs((ax + bx) / 2 - x) < 1.0 for x in ticks): continue
            P.append((ax, ay, bx, by))
    ends = [(q[0], q[1]) for q in P] + [(q[2], q[3]) for q in P]
    def linked(x, y, me):
        return sum(1 for (u, v) in ends if abs(u - x) < 0.8 and abs(v - y) < 0.8) > 1
    keep = [q for q in P if math.hypot(q[2] - q[0], q[3] - q[1]) >= 16 or linked(q[0], q[1], q) or linked(q[2], q[3], q)]
    if not dashed: return keep
    ks = set(keep)
    return [q for q in P if q not in ks and math.hypot(q[2] - q[0], q[3] - q[1]) >= 1.0]        # the dashes of dashed lines (drawn as separate strokes)


def trace(pool, x, y, sgn, gap=45.0, near_r=4.0, stops=None):
    """Polyline from (x, y) along the drawn line toward sgn * x: start on the piece through (x, y), join pieces end to end (smallest turn),
    bridge gaps up to `gap` pt along the current direction."""
    best = None
    for q in pool:
        if min(q[0], q[2]) - 0.3 <= x <= max(q[0], q[2]) + 0.3 and abs(q[2] - q[0]) > 1e-6:
            yy = q[1] + (q[3] - q[1]) * (x - q[0]) / (q[2] - q[0])
            if abs(yy - y) < 1.5 and (best is None or abs(yy - y) < best[0]): best = (abs(yy - y), q, yy)
    if best is None: return [(x, y)]
    q = best[1]
    far = (q[0], q[1]) if (q[0] - q[2]) * sgn > 0 else (q[2], q[3])
    pts = [(x, best[2]), far]; used = {q}
    while True:
        px, py = pts[-1]; dx, dy = px - pts[-2][0], py - pts[-2][1]; dl = math.hypot(dx, dy) or 1.0
        at_mark = bool(stops) and any(r[0] - 1.5 <= px <= r[2] + 1.5 and r[1] - 1.5 <= py <= r[3] + 1.5 for r in stops)
        cand = []
        for r in pool:
            if r in used: continue
            for (ax, ay, bx, by) in ((r[0], r[1], r[2], r[3]), (r[2], r[3], r[0], r[1])):
                if abs(ax - px) < 0.8 and abs(ay - py) < 0.8 and (bx - ax) * sgn > 0.3:
                    turn = abs(math.atan2(dx * (by - ay) - dy * (bx - ax), dx * (bx - ax) + dy * (by - ay)))
                    cand.append((turn, r, (bx, by)))
        if at_mark:
            # at an anchor symbol the line ends unless it runs on straight through (another tendon's line passing the anchor of this one)
            cand = [c for c in cand if c[0] < 0.1]
            if not cand: return pts
        if not cand:
            # broken at a P.I. circle / anchor symbol: a piece starting within 4 pt (circle radius) of the end
            near = []
            for r in pool:
                if r in used: continue
                for (ax, ay, bx, by) in ((r[0], r[1], r[2], r[3]), (r[2], r[3], r[0], r[1])):
                    dd = math.hypot(ax - px, ay - py)
                    if dd < near_r and (bx - ax) * sgn > 0.3: near.append((dd, r, (bx, by), (ax, ay)))
            if near:
                c = min(near, key=lambda c: c[0]); used.add(c[1]); pts += [c[3], c[2]]; continue
            # gap: nearest piece starting on the extension of the current direction
            for r in pool:
                if r in used: continue
                for (ax, ay, bx, by) in ((r[0], r[1], r[2], r[3]), (r[2], r[3], r[0], r[1])):
                    along = ((ax - px) * dx + (ay - py) * dy) / dl
                    perp = abs((ax - px) * dy - (ay - py) * dx) / dl
                    if 0.5 < along <= gap and perp < 0.8 and (bx - ax) * sgn > 0.3:
                        cand.append((along, r, (bx, by), (ax, ay)))
            if not cand: return pts
            c = min(cand, key=lambda c: c[0]); used.add(c[1]); pts += [c[3], c[2]]; continue
        if len(cand) > 1:
            # branch (a dash-dot line starts on the P.I. of a solid one): the continuation that runs on longest without a gap wins, then the smaller turn
            def run(r, end, depth=0):
                L = math.hypot(r[2] - r[0], r[3] - r[1])
                if depth > 40: return L
                nxt = [q for q in pool if q is not r and q not in used and any(abs(e[0] - end[0]) < 0.8 and abs(e[1] - end[1]) < 0.8 and (o[0] - e[0]) * sgn > 0.3
                       for e, o in (((q[0], q[1]), (q[2], q[3])), ((q[2], q[3]), (q[0], q[1]))))]
                best = 0.0
                for q in nxt[:3]:
                    o = (q[2], q[3]) if abs(q[0] - end[0]) < 0.8 and abs(q[1] - end[1]) < 0.8 else (q[0], q[1])
                    best = max(best, run(q, o, depth + 1))
                return L + best
            c = max(cand, key=lambda c: (round(run(c[1], c[2]), 0), -c[0]))
        else: c = cand[0]
        used.add(c[1]); pts.append(c[2])


def anchor_marks(pg, y0, y1):
    """Bounding boxes of the anchor symbols (small closed triangles / filled circles) in the band."""
    R = []
    for d in pg.get_drawings():
        r = d['rect']
        if y0 < r.y0 < y1 and r.width < 14 and r.height < 14 and r.width + r.height > 4:
            its = d['items']
            if d.get('fill') is not None or (len(its) >= 3 and all(it[0] == 'l' for it in its)): R.append((r.x0, r.y0, r.x1, r.y1))
    # open triangles drawn as two separate strokes from a common tip (the non-stressing end symbol)
    L = []
    for d in pg.get_drawings():
        its = d['items']
        if len(its) == 1 and its[0][0] == 'l':
            a, b = its[0][1], its[0][2]
            if y0 < a.y < y1 and 8 < math.hypot(b.x - a.x, b.y - a.y) < 12 and abs(b.y - a.y) > 1.5: L.append((a.x, a.y, b.x, b.y))
    for i, q in enumerate(L):
        for r in L[i + 1:]:
            for (ax, ay) in ((q[0], q[1]), (q[2], q[3])):
                if any(abs(ax - bx) < 0.3 and abs(ay - by) < 0.3 for (bx, by) in ((r[0], r[1]), (r[2], r[3]))):
                    xs = [q[0], q[2], r[0], r[2]]; ys = [q[1], q[3], r[1], r[3]]
                    R.append((min(xs), min(ys), max(xs), max(ys)))
    return R


def poly_y(pts, x):
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        if min(ax, bx) - 0.3 <= x <= max(ax, bx) + 0.3 and abs(bx - ax) > 1e-6:
            return ay + (by - ay) * (x - ax) / (bx - ax)
    return None


def read_sheet(page, anchors):
    bent, arm = SHEETS[page]
    pg = pymupdf.open(PDF)[page - 1]
    segs = page_geometry(pg)
    words = pg.get_text('words')
    # CL girder: the 2.16 pt line between the top and bottom slab plans ("SYMMETRICAL ABOUT CL GIRDER")
    clseg = [s for s in segs if abs(s[6] - 2.16) < 0.05 and 540 < s[1] < 700 and 540 < s[3] < 700 and abs(s[2] - s[0]) > abs(s[3] - s[1])]
    cl = sorted([(s[0], s[1]) for s in clseg] + [(s[2], s[3]) for s in clseg])

    def ycl(x):
        lo = max([p for p in cl if p[0] <= x], default=cl[0], key=lambda p: p[0])
        hi = min([p for p in cl if p[0] >= x], default=cl[-1], key=lambda p: p[0])
        return lo[1] if hi[0] - lo[0] < 1e-6 else lo[1] + (hi[1] - lo[1]) * (x - lo[0]) / (hi[0] - lo[0])

    seglab = []
    for w in words:
        m = re.match(rf'^{bent}-{arm}?(\d+)$', w[4])          # p.292 prints '24-11' for 24-U11
        if m and w[1] < 300: seglab.append((int(m.group(1)), (w[0] + w[2]) / 2))
    lab = [w for w in words if w[1] < 300 and re.match(r'^(\d+-(D|U)\w*|\d+-ES|C\.J\.)$', w[4])]
    px0, px1 = min(w[0] for w in lab) - 70, max(w[2] for w in lab) + 70
    segs = [s for s in segs if px0 < min(s[0], s[2]) and max(s[0], s[2]) < px1 or s[6] > 2]
    rows = []
    for part, (y0, y1), lanes, sign in (('top', (280, 560), ROW_A, 1), ('bottom', (660, 860), ROW_C, -1)):
        H = [s for s in segs if not s[5] and y0 < s[1] < y1 and y0 < s[3] < y1 and abs(s[2] - s[0]) > 14 and abs(s[3] - s[1]) < 0.6 * abs(s[2] - s[0])]
        xs = sorted(x for s in H for x in (s[0], s[2]))
        cl_ = []
        for x in xs:
            if cl_ and x - cl_[-1][-1] <= 2.5: cl_[-1].append(x)
            else: cl_.append([x])
        clusters = [(sum(c) / len(c), len(c)) for c in cl_]
        joints = [x for x, n in clusters if n >= 4]
        tj = tick_joints(segs, y0, y1)
        if os.environ.get('BTL_JOINTS') == '1': print('JOINTS', page, part, [round(x) for x in joints], '| ticks', [round(x) for x in tj])
        if len(tj) >= 4:
            # the lane ticks drawn on every joint line (sheets p.290-296): the joint itself, not where the tendon lines happen to be broken
            joints = tj

        def crossings(xj, dashed=False):
            hits = []
            for s in H:
                if s[4] != dashed: continue
                a, b = (s[0], s[1]), (s[2], s[3])
                if a[0] > b[0]: a, b = b, a
                if a[0] - 0.6 <= xj <= b[0] + 0.6:
                    t = 0 if b[0] - a[0] < 1e-9 else min(1, max(0, (xj - a[0]) / (b[0] - a[0])))
                    hits.append(a[1] + (b[1] - a[1]) * t)
            hits.sort(); ded = []
            for y in hits:
                if ded and abs(y - ded[-1]) < 1.2: continue
                ded.append(y)
            yc = ycl(xj)
            if part == 'bottom':
                # bottom slab lanes are measured from the soffit edge (the outermost 1.8 pt box line), which narrows toward the bents
                ed = []
                for s in segs:
                    if abs(s[6] - 1.8) < 0.05 and y0 < s[1] < y1 + 60 and abs(s[2] - s[0]) > abs(s[3] - s[1]) and min(s[0], s[2]) <= xj <= max(s[0], s[2]):
                        t_ = 0 if abs(s[2] - s[0]) < 1e-9 else (xj - s[0]) / (s[2] - s[0])
                        ed.append(s[1] + (s[3] - s[1]) * t_)
                if not ed: return []
                yc = max(ed)
            out = []
            for y in ded:
                off = (yc - y) / LAT * (1 if part == 'bottom' else sign)
                k = min(range(len(lanes)), key=lambda i: abs(lanes[i] - off))
                if abs(lanes[k] - off) < 3.0: out.append((k + 1, off - lanes[k], y))
            return sorted(set(out))

        # segment k: the joint interval containing its label centre; tip face of segment k = joint farther from the bent
        def interval(xc):
            for i in range(len(joints) - 1):
                if joints[i] <= xc <= joints[i + 1]: return joints[i], joints[i + 1]
            return None
        # segment k = its label centre +- half a typical segment (labels are centred; typical segments are all 14'-0"), each face snapped to the
        # nearest drawn joint (lane ticks / line breaks) within 8 pt. The line breaks alone are not the joints: some sheets draw a tendon as
        # one line over several segments and break it at P.I. circles (p.291 read joint 10 at a P.I.: T12 / T13 two lanes off).
        labs = sorted(seglab, key=lambda q: q[0])
        steps = sorted(abs(b[1] - a[1]) for a, b in zip(labs, labs[1:]) if b[0] == a[0] + 1)
        half = steps[len(steps) // 2] / 2 if steps else None
        cand = sorted(set([x for x, n in clusters if n >= 2] + list(tj)))
        def snap(x):
            c = min(cand, key=lambda q: abs(q - x), default=None)
            return c if c is not None and abs(c - x) <= 8 else x
        segx = {k: ((snap(xc - half), snap(xc + half)) if half else interval(xc)) for k, xc in seglab}
        if part == 'top':
            group = [t for t in anchors if t['bent'] == bent]
            order_x = segx.get(1)
            if not order_x: continue
            j0 = order_x[1] if arm == 'D' else order_x[0]          # bent face of segment 1
            # labels at the bent end: T.. words outside the plan's bent end
            edge = [w for w in words if re.match(r'^T\d+,?$', w[4]) and y0 < w[1] < y1 and ((w[0] > j0 + 20) if arm == 'D' else (w[2] < j0 - 20))]
            # lanes where the drawn lines END at the bent (labels sit just beyond the line ends)
            band = [s for s in H if not s[4]]
            many = [x for x, n in clusters if n >= 5]
            xe = (max(many) - 5) if arm == "D" else (min(many) + 5)
            c0 = crossings(xe)
            if DEBUG: print("DBG", page, round(j0), round(xe), [(c[0], round(c[2])) for c in c0], [(w[4], round((w[1]+w[3])/2)) for w in edge], [round(j) for j in joints])
            lane0 = {}
            for w in edge:
                yw = (w[1] + w[3]) / 2
                near = min(c0, key=lambda c: abs(c[2] - (yw - 3)), default=None)
                if near and abs(near[2] - (yw - 3)) < 6: lane0[w[4].rstrip(',')] = near[0]
            idx = {t['tendon']: t['down' if arm == 'D' else 'up'] for t in group}
            rowA = sorted([t for t in lane0 if t not in ROW_B_TENDONS and t in idx], key=lambda t: lane0[t])
            for t in lane0:
                rows.append(dict(bent=bent, arm=arm, tendon=t, joint=0, lane=lane0[t], row='B' if t in ROW_B_TENDONS else 'A', resid='', page=page,
                                 tag='VECTOR: bent-end label on the drawn line'))
            kmax = max(idx.values(), default=0)
            # every row A tendon is TRACED along its drawn line from its bent-end crossing toward the tip (pieces joined end to end, P.I.s included;
            # short gaps where another line is drawn over it are bridged along the line's direction). Zipping the crossings at a joint in lane order
            # mixed in other lines (C tendons drawn solid near the closure, p.291 C2 on lane 8) and read T12 / T13 two lanes off.
            toward_tip = -1 if arm == 'D' else 1
            pool = trace_pool(pg, y0, y1, tj)
            c0map = {}
            for w in edge:
                t = w[4].rstrip(',')
                if t in rowA:
                    yw = (w[1] + w[3]) / 2
                    near = min(c0, key=lambda c: abs(c[2] - (yw - 3)), default=None)
                    if near and abs(near[2] - (yw - 3)) < 6: c0map[t] = near[2]
            # row B tendons (T5 / T6, p.285 note 1) are drawn DASHED from their anchor to where they join the row A line of their bent-end lane
            # (drawn over it from there to the bent): trace the dashed line from its bent-side end, the joints between it and the bent = the bent lane
            dpool = trace_pool(pg, y0, y1, tj, dashed=True)
            for t in [t for t in lane0 if t in ROW_B_TENDONS and t in idx and t != 'T14']:
                yl = ycl(xe) - ROW_A[lane0[t] - 1] * LAT
                ends = [(q[i], q[i + 1]) for q in dpool for i in (0, 2)]
                # bent-side ends of dashed runs in the bent lane, inside the cantilever (between the anchor segment and the bent)
                ends = [e for e in ends if abs(e[1] - (ycl(e[0]) - ROW_A[lane0[t] - 1] * LAT)) < 1.5]
                if idx[t] in segx and segx[idx[t]]:
                    xa_tip = segx[idx[t]][0] if arm == 'D' else segx[idx[t]][1]
                    ends = [e for e in ends if (e[0] - xa_tip) * toward_tip < 0]
                def unread(why):
                    for k in range(1, idx[t] + 1):
                        rows.append(dict(bent=bent, arm=arm, tendon=t, joint=k, lane='?', row='B', resid='', page=page, tag=f'UNRESOLVED: {why}'))
                if not ends: unread('no dashed line of this row B tendon found in its bent-end lane'); continue
                e = max(ends, key=lambda e: -e[0] * toward_tip)                 # nearest the bent
                pts = trace(dpool, e[0] - 0.01 * toward_tip, e[1], toward_tip, gap=12.0, near_r=18.0)
                if len(pts) < 2: unread('dashed line not traceable'); continue
                # the anchors sit only at P1 / P2 (A7 / A5, p.303 Section B-B): a dashed trace that ends elsewhere strayed onto another line - not used
                xa, ya = pts[-1]; offa = (ycl(xa) - ya) / LAT
                if not any(abs(ROW_A[i] - offa) < 1.5 for i in (4, 6)): unread('the traced dashed line does not end at an anchor position (P1 / P2)'); continue
                n0 = len(rows)
                for k in range(1, idx[t] + 1):
                    if k not in segx or segx[k] is None: continue
                    xk = segx[k][0] if arm == 'D' else segx[k][1]
                    if k == idx[t]:
                        xa, ya = pts[-1]; off = (ycl(xa) - ya) / LAT
                        ln = min(range(len(ROW_A)), key=lambda i: abs(ROW_A[i] - off))
                        if abs(ROW_A[ln] - off) < 3.0 and (xa - xk) * toward_tip < 0:
                            rows.append(dict(bent=bent, arm=arm, tendon=t, joint=k, lane=ln + 1, row='B', resid=round(off - ROW_A[ln], 1), page=page,
                                             tag='VECTOR: anchor lane = end of the traced dashed line'))
                        continue
                    if (xk - e[0]) * toward_tip < 0:
                        rows.append(dict(bent=bent, arm=arm, tendon=t, joint=k, lane=lane0[t], row='B', resid='', page=page,
                                         tag='VECTOR: drawn over the row A line of its bent-end lane (dashed line joins it nearer the anchor)'))
                        continue
                    yk = poly_y(pts, xk)
                    if yk is None: continue
                    off = (ycl(xk) - yk) / LAT
                    ln = min(range(len(ROW_A)), key=lambda i: abs(ROW_A[i] - off))
                    if abs(ROW_A[ln] - off) < 3.0:
                        rows.append(dict(bent=bent, arm=arm, tendon=t, joint=k, lane=ln + 1, row='B', resid=round(off - ROW_A[ln], 1), page=page,
                                         tag='VECTOR: traced dashed line at the joint'))
            for t in rowA:
                if t not in c0map: continue
                pts = trace(pool, xe, c0map[t], toward_tip)
                for k in range(1, idx[t] + 1):
                    if k not in segx or segx[k] is None: continue
                    xk = segx[k][0] if arm == 'D' else segx[k][1]
                    if k == idx[t]:
                        # anchored in segment k (9" inside its tip face): the lane at the end of the drawn line
                        xa, ya = pts[-1]
                        off = (ycl(xa) - ya) / LAT
                        ln = min(range(len(ROW_A)), key=lambda i: abs(ROW_A[i] - off))
                        ok = abs(ROW_A[ln] - off) < 3.0 and (xa - xk) * toward_tip < 0
                        rows.append(dict(bent=bent, arm=arm, tendon=t, joint=k, lane=ln + 1 if ok else '?', row='A', resid=round(off - ROW_A[ln], 1) if ok else '',
                                         page=page, tag='VECTOR: anchor lane = end of the traced line' if ok else 'UNRESOLVED: traced line does not end in its anchor segment'))
                        continue
                    yk = poly_y(pts, xk)
                    if yk is None:
                        rows.append(dict(bent=bent, arm=arm, tendon=t, joint=k, lane='?', row='A', resid='', page=page,
                                         tag='UNRESOLVED: traced line does not reach the joint'))
                        continue
                    off = (ycl(xk) - yk) / LAT
                    ln = min(range(len(ROW_A)), key=lambda i: abs(ROW_A[i] - off))
                    if abs(ROW_A[ln] - off) < 3.0:
                        rows.append(dict(bent=bent, arm=arm, tendon=t, joint=k, lane=ln + 1, row='A', resid=round(off - ROW_A[ln], 1), page=page,
                                         tag='VECTOR: traced line at the joint'))
                    else:
                        rows.append(dict(bent=bent, arm=arm, tendon=t, joint=k, lane='?', row='A', resid='', page=page,
                                         tag=f'UNRESOLVED: traced line crosses the joint {off:.1f}" from CL, between lanes'))
        else:
            # bottom slab continuity tendons: labels B1.. at the closure end; B_m anchors in segment m + 4 (m + 6 next to an end segment,
            # p.285-296 / anchor-block-locations.csv); joint k (tip face of segment k) is crossed by the tendons anchored in segments <= k
            if not joints or not segx: continue
            nseg = max(segx)
            end_span = (bent == 19 and arm == 'D') or (bent == 28 and arm == 'U')
            first = 7 if end_span else 5
            many = [x for x, n in clusters if n >= 3]
            if not many: continue
            xe = (min(many) + 5) if arm == 'D' else (max(many) - 5)          # closure end of the drawn lines
            c0 = crossings(xe)
            edge = [w for w in words if re.match(r'^B\d$', w[4]) and y0 < w[1] < y1 and ((w[2] < xe - 3) if arm == 'D' else (w[0] > xe + 3))]
            lane0 = {}
            for w in edge:
                yw = (w[1] + w[3]) / 2
                near = min(c0, key=lambda c: abs(c[2] - (yw - 3)), default=None)
                if near and abs(near[2] - (yw - 3)) < 6: lane0[w[4]] = near[0]
            if DEBUG: print('DBG-B', page, round(xe), [(c[0], round(c[2])) for c in c0], sorted(lane0.items()))
            anch = {t: first + int(t[1:]) - 1 for t in lane0}
            order = sorted(lane0, key=lambda t: lane0[t])
            for t in lane0:
                rows.append(dict(bent=bent, arm=arm, tendon=t, joint=nseg, lane=lane0[t], row='C', resid='', page=page,
                                 tag='VECTOR: closure-end label on the drawn line (joint = closure end)'))
            for k in range(nseg - 1, 0, -1):
                if k not in segx or segx[k] is None: continue
                xk = segx[k][0] if arm == 'D' else segx[k][1]
                live = [t for t in order if anch[t] <= k]
                after = [t for t in order if anch[t] < k]
                if not live: continue
                outward = -1 if arm == 'D' else 1                      # toward the closure
                # the joint itself, then a little to either side (P.I. circles / text / the blister line ending at the joint); the full set first,
                # reading toward the closure (the tendon anchored in segment k is then surely drawn)
                use, cr, how = None, None, 0
                for dx in (0, 2, 4, 6, 8, -2, -4):
                    c_ = crossings(xk + outward * dx)
                    if len(c_) == len(live): use, cr, how = live, c_, dx; break
                if use is None:
                    for dx in (0, 2, -2, 4, -4):
                        c_ = crossings(xk + outward * dx)
                        if after and len(c_) == len(after): use, cr, how = after, c_, dx; break
                if use is None:
                    for t in live:
                        rows.append(dict(bent=bent, arm=arm, tendon=t, joint=k, lane='?', row='C', resid='', page=page,
                                         tag=f'UNRESOLVED: no x near the joint where the crossing count matches ({len(live)} or {len(after)} tendons)'))
                    continue
                for t, c in zip(use, cr):
                    rows.append(dict(bent=bent, arm=arm, tendon=t, joint=k, lane=c[0], row='C', resid=round(c[1], 1), page=page,
                                     tag='VECTOR: crossing at the joint, order kept from the closure' + (f' (read {outward * how:+} pt from the joint)' if how else '')))
    return rows


def read_end_span(page, blocks, starts=None):
    """End-span continuity tendons B1-B3 (p.285 span 19 = bent 19 down-station, p.296 span 29 = bent 28 up-station): each line is traced from its
    start label (the label nearer the end segment) along the drawn line (text strokes < 5 pt and lane ticks removed) to its anchor symbol. Where two
    lines meet at an anchor symbol and one line leaves it, the line ending there is the one the anchor block table assigns to that segment
    (anchor-block-locations.csv); a line end in another segment than the table's = CONFLICT (lanes not written)."""
    bent, arm = SHEETS[page]
    pg = pymupdf.open(PDF)[page - 1]
    segs = page_geometry(pg); words = pg.get_text('words')
    y0, y1 = 660, 860
    pool = [q for q in trace_pool(pg, y0, y1, []) if math.hypot(q[2] - q[0], q[3] - q[1]) >= 5 and not (abs(q[3] - q[1]) < 0.5 and 7 < abs(q[2] - q[0]) < 11)]
    marks = anchor_marks(pg, y0, y1)
    def edge(x):
        ed = []
        for s in segs:
            if abs(s[6] - 1.8) < 0.05 and y0 < s[1] < y1 + 60 and abs(s[2] - s[0]) > abs(s[3] - s[1]) and min(s[0], s[2]) <= x <= max(s[0], s[2]):
                t_ = 0 if abs(s[2] - s[0]) < 1e-9 else (x - s[0]) / (s[2] - s[0]); ed.append(s[1] + (s[3] - s[1]) * t_)
        return max(ed) if ed else None
    sgn = 1 if arm == 'D' else -1
    seg = {int(m.group(1)): (w[0] + w[2]) / 2 for w in words for m in [re.match(rf'^{bent}-{arm}?(\d+)$', w[4])] if m and w[1] < 300}
    labs_ = sorted(seg.items()); half = sorted(abs(b[1] - a[1]) for a, b in zip(labs_, labs_[1:]))[len(labs_) // 2] / 2
    def segof(x): return next((k for k, c in seg.items() if abs(x - c) <= half), None)
    es = [(w[0] + w[2]) / 2 for w in words if w[1] < 300 and w[4].endswith('-ES')][0]
    labs = [w for w in words if re.match(r'^B\d$', w[4]) and y0 < w[1] < y1]
    rows, notes = [], []
    for name in sorted({w[4] for w in labs}):
        w = min((w for w in labs if w[4] == name), key=lambda w: abs((w[0] + w[2]) / 2 - es))
        xc, yb = (w[0] + w[2]) / 2, w[3]
        best = None
        for q in pool:
            if min(q[0], q[2]) <= xc <= max(q[0], q[2]) and abs(q[2] - q[0]) > 1e-6 and math.hypot(q[2] - q[0], q[3] - q[1]) >= 20:
                yy = q[1] + (q[3] - q[1]) * (xc - q[0]) / (q[2] - q[0])
                if 0 < yy - yb < 14 and (best is None or yy < best): best = yy
        if best is None: notes.append(f'{name}: no drawn line under its start label'); continue
        bwd = trace(pool, xc, best, -sgn, stops=marks)
        want = blocks.get((bent, arm, name))
        fwd, st = trace(pool, xc, best, sgn, stops=marks), list(marks)
        for _ in range(3):
            k_end = segof(fwd[-1][0])
            if want is None or k_end == want: break
            # through the anchor symbol where the line ends onto the line that leaves it (the table puts this tendon's anchor farther on)
            hit = [r for r in st if r[0] - 1.5 <= fwd[-1][0] <= r[2] + 1.5 and r[1] - 1.5 <= fwd[-1][1] <= r[3] + 1.5]
            nxt = [q for q in pool if any(r[0] - 4 <= e[0] <= r[2] + 4 and r[1] - 4 <= e[1] <= r[3] + 4 for r in hit for e in ((q[0], q[1]), (q[2], q[3])))
                   and ((max(q[0], q[2]) if sgn > 0 else min(q[0], q[2])) - fwd[-1][0]) * sgn > 5]
            if not hit or not nxt: break
            q = max(nxt, key=lambda q: math.hypot(q[2] - q[0], q[3] - q[1]))
            a, b = ((q[0], q[1]), (q[2], q[3])) if (q[2] - q[0]) * sgn > 0 else ((q[2], q[3]), (q[0], q[1]))
            st = [r for r in st if r not in hit]
            fwd = fwd + trace(pool, a[0] + 0.01 * sgn, a[1], sgn, stops=st)
        k_end = segof(fwd[-1][0])
        # the start symbol (open triangle) may sit on a line another tendon continues: cut the backward trace at the first symbol it passes
        for i in range(1, len(bwd)):
            (ax_, ay_), (bx_, by_) = bwd[i - 1], bwd[i]
            hitm = [r for r in marks if min(ax_, bx_) - 1 <= (r[0] + r[2]) / 2 <= max(ax_, bx_) + 1 and abs(bx_ - ax_) > 1e-6
                    and abs(ay_ + (by_ - ay_) * ((r[0] + r[2]) / 2 - ax_) / (bx_ - ax_) - (r[1] + r[3]) / 2) < 2.5 and abs((r[0] + r[2]) / 2 - xc) > 3]
            if hitm:
                r = min(hitm, key=lambda r: abs((r[0] + r[2]) / 2 - ax_)); xm = (r[0] + r[2]) / 2
                bwd = bwd[:i] + [(xm, ay_ + (by_ - ay_) * (xm - ax_) / (bx_ - ax_))]
                break
        start = bwd[-1]; k0 = segof(start[0]); where = 'end segment' if k0 is None and abs(start[0] - es) < 2 * half else (f'segment {k0}' if k0 else '?')
        if want is not None and k_end != want:
            notes.append(f'CONFLICT {name}: the drawn line ends in segment {k_end}, the anchor block table puts its block in segment {want} - lanes not written')
            continue
        ks = (starts or {}).get((bent, arm, name))
        if ks is not None and k0 != ks:
            # the table's start block (p.285 / p.296 B3 in segment 13): the open triangle there sits on a line another tendon continues
            where = f'segment {ks} (anchor block table; the drawn start symbol lies on a line B1 continues)'
            start = (seg[ks] + sgn * half - sgn * 1.0, start[1])
        path = bwd[::-1] + fwd[1:]
        for k in sorted(seg):
            if ks is not None and k >= ks: continue
            xk = seg[k] - sgn * half
            if not ((xk - start[0]) * sgn > 0 and (fwd[-1][0] - xk) * sgn > 0): continue
            yk = poly_y(path, xk); ed = edge(xk)
            if yk is None or ed is None: continue
            off = (ed - yk) / LAT; ln = min(range(len(ROW_C)), key=lambda i: abs(ROW_C[i] - off))
            if abs(ROW_C[ln] - off) < 3.0:
                rows.append(dict(bent=bent, arm=arm, tendon=name, joint=k, lane=ln + 1, row='C', resid=round(off - ROW_C[ln], 1), page=page,
                                 tag=f'VECTOR: end-span line traced from its start ({where}) to its anchor in segment {k_end}'))
        notes.append(f'{name}: starts in the {where}, anchored in segment {k_end}' + (' (= anchor block table)' if want == k_end else ''))
    return rows, notes


def fill_bottom_from_typical(rows):
    """Bottom slab continuity tendons (row C) of the interior cantilevers (B_m anchored in segment m + 4) follow ONE lane pattern on every sheet
    where all joints are read. Joints a sheet does not let us read (label at the closure end missing, line broken, joint not located) take that
    pattern when every fully read cantilever agrees on it; otherwise they stay '?'."""
    from collections import defaultdict
    by = defaultdict(list)
    for r in rows:
        if r['row'] == 'C' and not ((r['bent'] == 19 and r['arm'] == 'D') or (r['bent'] == 28 and r['arm'] == 'U')): by[(r['bent'], r['arm'])].append(r)
    nseg = max((int(r['joint']) for rr in by.values() for r in rr), default=0)
    pg = {}
    for page, (bent, arm) in SHEETS.items():
        pg[(bent, arm)] = page
        if not ((bent == 19 and arm == 'D') or (bent == 28 and arm == 'U')) and (bent, arm) not in by: by[(bent, arm)] = []
    want = {(f'B{m}', k) for m in range(1, 8) for k in range(m + 4, nseg + 1)}
    full = [rr for rr in by.values() if {(r['tendon'], int(r['joint'])) for r in rr if r['lane'] != '?'} >= want]
    pat = {}
    for key in want:
        vals = {next(r['lane'] for r in rr if (r['tendon'], int(r['joint'])) == key) for rr in full}
        if len(vals) == 1: pat[key] = vals.pop()
    pages = sorted({rr[0]['page'] for rr in full})
    out = [r for r in rows if not (r['row'] == 'C' and r['lane'] == '?' and (r['bent'], r['arm']) in by and (r['tendon'], int(r['joint'])) in pat)]
    for (bent, arm), rr in by.items():
        have = {(r['tendon'], int(r['joint'])) for r in rr if r['lane'] != '?'}
        page = pg[(bent, arm)]
        for key in sorted(want - have, key=lambda q: (q[0], q[1])):
            if key in pat:
                out.append(dict(bent=bent, arm=arm, tendon=key[0], joint=key[1], lane=pat[key], row='C', resid='', page=page,
                                tag=f'TYPICAL: joint not readable on p.{page}; lane of the {len(full)} fully read interior cantilevers (p.{", ".join(map(str, pages))}), all identical'))
    return out


def fill_top_from_matching(rows, anchors):
    """Top slab cantilever tendons (row A): a joint the sheet does not let us read takes the lane of (1) the other arm of the same bent at the same
    joint when the two arms agree at every joint both read, else (2) the same tendon of the other bents with the same anchor segments when they
    agree with this arm at every joint both read and with each other at this joint. Otherwise it stays '?'."""
    from collections import defaultdict
    anc = {(a['bent'], a['tendon']): (a['down'], a['up']) for a in anchors}
    lane = defaultdict(dict)
    for r in rows:
        if r['row'] in ('A', 'B') and r['lane'] != '?': lane[(r['bent'], r['arm'], r['tendon'])][int(r['joint'])] = r['lane']
    def agree(a, b, need=3):
        com = set(a) & set(b)
        return len(com) >= need and all(a[j] == b[j] for j in com)
    out = []
    for r in rows:
        if r['row'] not in ('A', 'B') or r['lane'] != '?': out.append(r); continue
        key = (r['bent'], r['arm'], r['tendon']); j = int(r['joint']); me = lane[key]
        other = lane.get((r['bent'], 'U' if r['arm'] == 'D' else 'D', r['tendon']), {})
        if j in other and agree(me, other):
            out.append(dict(r, lane=other[j], tag=f"MATCHED: joint not readable on p.{r['page']}; lane of the other arm of bent {r['bent']} (the arms agree at every joint both read)"))
            continue
        sim = [(b, a2) for (b, a2, t) in lane if t == r['tendon'] and (b, a2) != (r['bent'], r['arm']) and anc.get((b, t)) == anc.get((r['bent'], r['tendon']))]
        vals = {lane[(b, a2, r['tendon'])][j] for b, a2 in sim if j in lane[(b, a2, r['tendon'])] and agree(me, lane[(b, a2, r['tendon'])])}
        n = sum(1 for b, a2 in sim if j in lane[(b, a2, r['tendon'])] and agree(me, lane[(b, a2, r['tendon'])]))
        if len(vals) == 1 and n >= 2:
            out.append(dict(r, lane=vals.pop(), tag=f"MATCHED: joint not readable on p.{r['page']}; lane of {n} cantilevers with the same anchor segments that agree with this arm at every joint both read"))
            continue
        # short tendons near the bent (few joints read): at least 4 cantilevers with the same anchors, unanimous, agreeing with every joint this arm read
        ok = [lane[(b, a2, r['tendon'])] for b, a2 in sim if j in lane[(b, a2, r['tendon'])] and agree(me, lane[(b, a2, r['tendon'])], 1)]
        vals = {x[j] for x in ok}
        if len(vals) == 1 and len(ok) >= 4:
            out.append(dict(r, lane=vals.pop(), tag=f"MATCHED: joint not readable on p.{r['page']}; lane of all {len(ok)} cantilevers with the same anchor segments that read it (unanimous, consistent with this arm)"))
        else: out.append(r)
    return out


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(DATA, 'tendon-lanes.csv')
    anchors = []
    for r in csv.DictReader(open(os.path.join(DATA, 'cantilever-tendon-anchors.csv'))):
        k = int(r['anchor_segment_index_each_cantilever (XX-Dk and XX-Uk)'])
        dn, up = k, k
        if r['bent'] == '19' and r['tendon'] == 'T14': dn, up = 13, 12
        if r['bent'] == '28' and r['tendon'] == 'T14': dn, up = 12, 13
        anchors.append(dict(bent=int(r['bent']), tendon=r['tendon'], down=dn, up=up))
    rows = []
    for page in SHEETS:
        rr = read_sheet(page, anchors)
        bad = sum(1 for r in rr if r['lane'] == '?')
        print(f'p.{page} {SHEETS[page]}: {len(rr)} rows, {bad} unresolved')
        rows += rr
    blocks, starts = {}, {}
    for r in csv.DictReader(open(os.path.join(DATA, 'anchor-block-locations.csv'))):
        m = re.match(r'^(\d+)-(D|U)(\d+)$', r['segment'])
        if m and r['block'].startswith('BOTTOM') and r['pdf_page'] in ('285', '296'):
            key = (int(m.group(1)), m.group(2), r['tendon'])
            if key in blocks: starts[key] = max(blocks[key], int(m.group(3)))                            # B3 also has its start block in 13
            blocks[key] = int(m.group(3)) if key not in blocks else min(blocks[key], int(m.group(3)))
    for page in (285, 296):
        rr, notes = read_end_span(page, blocks, starts)
        for n in notes: print(f'p.{page} end span: {n}')
        rows += rr
    rows = fill_bottom_from_typical(rows)
    rows = fill_top_from_matching(rows, anchors)
    with open(out, 'w', newline='') as f:
        w = csv.DictWriter(f, fieldnames=['bent', 'arm', 'tendon', 'joint', 'lane', 'row', 'resid', 'page', 'tag'])
        w.writeheader(); w.writerows(rows)
    print('wrote', out, len(rows))


if __name__ == '__main__':
    main()
