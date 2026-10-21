"""PTC Delaware River Bridge - rivet symbols counted per member inside the drawn gusset outlines (2026-10-21, QC I029 / step 2 of the rivet work).

Method (docs/plan-data/ptc-delaware-river/TRUSS-DESIGN.md section 11): the joint sheets draw every rivet (solid dot = shop, open / crossed circle = field)
but print no counts, pitch or gage. For every gusset outline read from the sheets (connections/gusset-outlines.csv: page, work point, drawn chord, outline
in page pixels at 150 dpi) the page is rendered at 300 dpi, rivet symbols are detected (filled dots by a morphological opening, open / crossed circles
by small enclosed holes, the detector of the QC check qc/ptc/work/rdet.py) and every symbol inside the outline is given to a member:
  - chord band: perpendicular distance from the drawn chord line through the work point <= the chord half depth (the distance from the work point to
    the outline's chord edge: the top edge of an upper-chord joint / bottom edge of a lower-chord joint is the chord top / bottom line) -> the chord
    member on that side of the work point (toward the lower / higher panel number);
  - otherwise the web member (diagonal / vertical / hanger) whose axis ray from the work point is nearest; ray directions = the MODEL member directions
    at the joint (temp geometry dump: point stations / elevations), rotated so the model chord lies on the drawn chord.
Symbols just outside the outline in a member's band (chord within 4 ft, web members within 3 ft of the outline) are counted as 'beyond' (fills / splice
plates / member ends; reported, not placed).

  python -I tools/ptc_rivet_count.py run geom.csv outlines.csv out.csv overlay_dir [structure:joint ...] [ovr=zones.csv] [pos=positions.csv]
      pos= also writes every counted symbol with its drawn position (sheet frame of the outline, inches) -> connections/rivet-positions.csv
      geom.csv = sbm run temp/dump3.csx model.sbm -- geom.csv (points + in-plane members per structure)
  The overlay PNG per joint colours the symbols by member (legend printed on the image) for the visual check; corrections go to
  connections/rivet-count-corrections.csv and are applied by rivets-ptc.csx.

One page is opened per joint (the raster PDF is large); run it as ONE process.
"""
import csv, math, os, sys

PDF = r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf"
DPI = 300
# reject elongated enclosed holes (rivets drawn in PROFILE at member edges: half-circle pairs, shank parallel to the sheet - not through the gusset)
ASPECT = False
# main truss web members (riveted boxes) draw the rivets of their outstanding angle legs IN PROFILE along the member edges (half circles on a line):
# rings found on a web member's edge line (band half depth - 1 in. .. + 2 1/2 in.) are dropped
EDGE_PROFILE = {}
# open-circle validation: fraction of a circle about the hole that must be dark (0 = off)
RING_DARK = 0
# largest box (px at 300 dpi) of the holes of one open circle (0 = off)
RING_BOX = 0
# chain filter distance per structure (px at 300 dpi; 0 = off): the main truss sheets draw the inlay / fill plate edges as dashed double lines
# whose loops read as rings; the approach sheets have staggered gage lines closer than that (1 3/4 in.)
CHAIN_PX = {'main': 20, 'approach3': 20, 'approach4': 20}
COLORS = [(230, 0, 0), (0, 0, 230), (0, 150, 0), (200, 0, 200), (0, 160, 170), (230, 120, 0), (120, 70, 0), (90, 90, 90)]


def pts(s):
    return [tuple(float(c) for c in q.split(',')) for q in s.split(';') if q.strip()]


def inside(p, poly):
    x, y = p; c = False
    for i in range(len(poly)):
        ax, ay = poly[i]; bx, by = poly[i - 1]
        if (ay > y) != (by > y) and x < ax + (y - ay) * (bx - ax) / (by - ay):
            c = not c
    return c


def seg_dist(p, a, b):
    ax, ay = a; bx, by = b; px, py = p
    dx, dy = bx - ax, by - ay
    l2 = dx * dx + dy * dy
    t = 0 if l2 == 0 else max(0, min(1, ((px - ax) * dx + (py - ay) * dy) / l2))
    return math.hypot(ax + dx * t - px, ay + dy * t - py)


def poly_dist(p, poly):
    return min(seg_dist(p, poly[i - 1], poly[i]) for i in range(len(poly)))


def detect(page, box, chain_px=0):
    """Rivet symbols in a crop (box in 150-dpi page px) -> (dots, rings) lists of (x, y) in 150-dpi page px + the gray image."""
    import pymupdf, numpy as np
    from scipy import ndimage as ndi
    x0, y0, x1, y1 = box
    k = 72 / 150; s = DPI / 150
    d = pymupdf.open(PDF)
    pm = d[page - 1].get_pixmap(matrix=pymupdf.Matrix(DPI / 72, DPI / 72), clip=pymupdf.Rect(x0 * k, y0 * k, x1 * k, y1 * k), colorspace=pymupdf.csGRAY)
    a = np.frombuffer(pm.samples, dtype=np.uint8).reshape(pm.height, pm.width)
    dark = a < 140
    R0 = 3
    yy, xx = np.mgrid[-R0:R0 + 1, -R0:R0 + 1]
    disk = (xx * xx + yy * yy) <= R0 * R0
    O = ndi.binary_opening(dark, structure=disk)
    lab, _ = ndi.label(O)
    dots = []
    for i, sl in enumerate(ndi.find_objects(lab), 1):
        h = sl[0].stop - sl[0].start; w = sl[1].stop - sl[1].start
        if 6 <= h <= 22 and 6 <= w <= 22 and (lab[sl] == i).sum() >= 30:
            dots.append(((sl[1].start + sl[1].stop) / 2, (sl[0].start + sl[0].stop) / 2))
    H = ndi.binary_fill_holes(dark) & ~dark
    lab2, _ = ndi.label(H)
    pieces = []
    for i, sl in enumerate(ndi.find_objects(lab2), 1):
        h = sl[0].stop - sl[0].start; w = sl[1].stop - sl[1].start
        if h <= 22 and w <= 22 and (lab2[sl] == i).sum() >= 12 and (not ASPECT or 0.55 <= w / h <= 1.8):
            pieces.append(((sl[1].start + sl[1].stop) / 2, (sl[0].start + sl[0].stop) / 2, (sl[1].start, sl[0].start, sl[1].stop, sl[0].stop)))
    rings = []
    for cx, cy, bb in pieces:
        if any((cx - dx) ** 2 + (cy - dy) ** 2 < 40 for dx, dy in dots):
            continue
        for g in rings:
            if (g[0] - cx) ** 2 + (g[1] - cy) ** 2 < 110:
                g[2] += 1; g[0] = (g[0] * g[2] + cx) / (g[2] + 1)
                g[3] = (min(g[3][0], bb[0]), min(g[3][1], bb[1]), max(g[3][2], bb[2]), max(g[3][3], bb[3])); break
        else:
            rings.append([cx, cy, 0, bb])
    if RING_BOX > 0:
        # the hole(s) of one drawn circle (open, crossed or with a centre line) fit in a small, roughly square box; slits between the double lines of
        # plate outlines / fills form long chains of thin holes
        def ok(g):
            w = g[3][2] - g[3][0]; h = g[3][3] - g[3][1]
            return max(w, h) <= RING_BOX and max(w, h) <= 2.3 * max(1, min(w, h))
        rings = [g for g in rings if ok(g)]
    # two symbols closer than 14 px at 300 dpi (about 1 1/2 in. at 3/4 in. = 1 ft; rivets are 3 in. or more apart) are one symbol: dots win, then the
    # first ring (ring pieces split by the hatch / centre lines, clipped plate corners)
    keepD = []
    for p in dots:
        if all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 >= 14 ** 2 for q in keepD):
            keepD.append(p)
    def round_ring(cx, cy):
        # a drawn rivet circle: the best circle of radius 4..7 px (300 dpi) about the hole centre is dark on >= RING_DARK of its length
        best = 0.0
        for rr in (4, 4.5, 5, 5.5, 6, 6.5, 7):
            n = 0; k = 0
            for i in range(32):
                t = 2 * math.pi * i / 32
                x = int(round(cx + rr * math.cos(t))); y = int(round(cy + rr * math.sin(t)))
                if 0 <= y < dark.shape[0] and 0 <= x < dark.shape[1]:
                    n += 1; k += 1 if dark[max(0, y - 1):y + 2, max(0, x - 1):x + 2].any() else 0
            if n: best = max(best, k / n)
        return best >= RING_DARK
    keepR = []
    for g in rings:
        p = (g[0], g[1])
        if RING_DARK > 0 and not round_ring(*p):
            continue
        if all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 >= 14 ** 2 for q in keepD + keepR):
            keepR.append(p)
    if chain_px > 0:
        # chains of small closed loops along double lines (inlay / fill plate edges, centre lines through dashes) read as rings: a ring with two or more
        # symbols closer than CHAIN_PX (about 2 3/8 in.; rivets are 3 in. or more apart, staggered gage lines 2 3/4 in.) is not a rivet
        allp = keepD + keepR
        def chained(p):
            nb = [(q[0] - p[0], q[1] - p[1]) for q in allp if q is not p and (p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 < chain_px ** 2]
            for i in range(len(nb)):
                for j in range(i + 1, len(nb)):
                    a, b = nb[i], nb[j]
                    c = (a[0] * b[0] + a[1] * b[1]) / (math.hypot(*a) * math.hypot(*b) + 1e-9)
                    if c < -0.87:   # two neighbours on opposite sides (a line of loops), not a staggered pair
                        return True
            return False
        keepR = [p for p in keepR if not chained(p)]
    D = [(x0 + cx / s, y0 + cy / s) for cx, cy in keepD]
    R = [(x0 + cx / s, y0 + cy / s) for cx, cy in keepR]
    return D, R, a


def load_geom(path):
    P, M = {}, {}
    for r in csv.DictReader(open(path, encoding='utf-8')):
        st = r['structure']
        if r['kind'] == 'P':
            P.setdefault(st, {}).setdefault(r['name'], (float(r['a']), float(r['b'])))
        else:
            M.setdefault(st, []).append((r['name'], r['a'], r['b'], r['c'], float(r.get('half_in') or 0)))
    return P, M


def ordinal(pt, C):
    n = int(''.join(ch for ch in pt if ch.isdigit()))
    return 2 * C - n if pt.endswith("'") else n


CEN = {'main': 22, 'approach3': 12, 'approach4': 20}
CHORD = ('UpperChord', 'LowerChord')


def joint_members(st, pt, P, M):
    """In-plane members at the point: list of (name, role, (du, dv) unit in model (u = toward the higher ordinal), chordside)."""
    C = CEN[st]
    pts_ = P[st]
    o = ordinal(pt, C)
    # station sign of increasing ordinal (same letter)
    let = pt[0]
    cands = [(ordinal(n, C), v[0]) for n, v in pts_.items() if n[:1] == let and n[1:].rstrip("'").isdigit()]
    cands.sort()
    sgn = 1 if cands[-1][1] > cands[0][1] else -1
    x0, z0 = pts_[pt]
    res = []
    for name, a, b, role, half in M[st]:
        if pt not in (a, b):
            continue
        other = b if a == pt else a
        if other not in pts_:
            continue
        x1, z1 = pts_[other]
        du, dv = sgn * (x1 - x0), z1 - z0
        L = math.hypot(du, dv)
        res.append((name, role, (du / L, dv / L), half))
    if not any(r[1] in CHORD for r in res):
        # chord passing through the point
        for name, a, b, role, half in M[st]:
            if role in CHORD and a[:1] == let and b[:1] == let and a in pts_ and b in pts_:
                oa, ob = ordinal(a, C), ordinal(b, C)
                if min(oa, ob) < o < max(oa, ob):
                    xa, za = pts_[a]; xb, zb = pts_[b]
                    du, dv = sgn * (xb - xa), zb - za
                    if du < 0:
                        du, dv = -du, -dv
                    L = math.hypot(du, dv)
                    res.append((name, role, (du / L, dv / L), half))
                    res.append((name, role, (-du / L, -dv / L), half))
                    break
    return res


OVR = []


def run(argv):
    geom, outl, out, odir = argv[:4]
    only = set(a for a in argv[4:] if not a.startswith('ovr=') and not a.startswith('pos='))
    posf = next((a[4:] for a in argv[4:] if a.startswith('pos=')), None)
    posrows = []
    for a in argv[4:]:
        if a.startswith('ovr=') and os.path.exists(a[4:]):
            for r in csv.DictReader(open(a[4:], encoding='utf-8')):
                OVR.append((r['structure'].split()[0], r['joint'], r['member'], r['zone_px']))
    P, M = load_geom(geom)
    rows = list(csv.DictReader(open(outl, encoding='utf-8')))
    os.makedirs(odir, exist_ok=True)
    res = []
    for r in rows:
        st = r['structure'].split()[0]
        pt = r['joint']
        if only and f"{st}:{pt}" not in only:
            continue
        if st not in P or pt not in P[st]:
            print(f"{st} {pt}: no model point"); continue
        page = int(r['page']); s = float(r['px_per_in'])
        wx, wy = pts(r['wp_px'])[0]
        (ax, ay), (bx, by) = pts(r['chord_px'])[:2]
        cdx, cdy = bx - ax, -(by - ay)
        if cdx < 0:
            cdx, cdy = -cdx, -cdy
        drawn = math.atan2(cdy, cdx)
        poly = pts(r['outline_px'])
        mem = joint_members(st, pt, P, M)
        chords = [m for m in mem if m[1] in CHORD]
        if not chords:
            print(f"{st} {pt}: no chord"); continue
        # model chord angle (pointing +u)
        cu = [m[2] if m[2][0] >= 0 else (-m[2][0], -m[2][1]) for m in chords]
        mch = math.atan2(sum(c[1] for c in cu), sum(c[0] for c in cu))
        rot = drawn - mch

        def to_px(d):
            u, v = d
            cr, sr = math.cos(rot), math.sin(rot)
            return (u * cr - v * sr, -(u * sr + v * cr))   # px direction (x right, y down)
        rays = [(m[0], m[1], to_px(m[2])) for m in mem]
        halfs = {m[0]: m[3] for m in mem}
        # chord line (px) and the chord half depth from the outline's chord edge
        cx_, cy_ = math.cos(drawn), -math.sin(drawn)          # px unit along the chord
        nx_, ny_ = -cy_, cx_                                   # px unit normal (pointing "down" when the chord is level)
        upper = pt.startswith('U')
        def dn(p):  # signed normal distance in px (+ = down)
            return (p[0] - wx) * nx_ + (p[1] - wy) * ny_
        h_out = max((-dn(q) if upper else dn(q)) for q in poly) / s
        hm = max(m[3] for m in chords)
        h = hm + 0.75 if hm > 0 else h_out   # model chord half depth (+ angle backs) - the outline edge can include extensions
        margin = 60 * s / 4.28  # about 5 ft of drawing around the outline (px)
        xs = [q[0] for q in poly]; ys = [q[1] for q in poly]
        box = (min(xs) - margin, min(ys) - margin, max(xs) + margin, max(ys) + margin)
        D, R, img = detect(page, box, CHAIN_PX.get(st, 0))
        reach_u = max(abs((q[0] - wx) * cx_ + (q[1] - wy) * cy_) for q in poly) / s
        lo, hi = -h, h
        for (st_, j_, m, z) in OVR:   # chord band override (in., + = down on the sheet): drawn work point off the chord centre
            if st_ == st and j_ == pt and m == 'BAND':
                lo, hi = (float(v) for v in z.split(','))
        zones = [(m, pts(z)) for (st_, j_, m, z) in OVR if st_ == st and j_ == pt and m not in ('BAND',)]

        def assign(p, isRing=False):
            for m_, z_ in zones:      # hand-drawn member zones (overrides file) win
                if inside(p, z_):
                    return m_, ('chord' if any(m_ == r_[0] and r_[1] in CHORD for r_ in rays) else 'web'), 0
            d_c = dn(p) / s
            u = ((p[0] - wx) * cx_ + (p[1] - wy) * cy_) / s
            if lo - 0.25 <= d_c <= hi + 0.25:
                side = 1 if u >= 0 else -1
                best = None
                for nme, role, (dx, dy) in rays:
                    if role in CHORD and (dx * cx_ + dy * cy_) * side > 0:
                        best = nme
                if best is None:   # end joint: the only chord member
                    best = next((nme for nme, role, _ in rays if role in CHORD), None)
                return best or 'chord?', 'chord', u
            best, bd, bt = None, 1e9, 0
            for nme, role, (dx, dy) in rays:
                if role in CHORD:
                    continue
                t = ((p[0] - wx) * dx + (p[1] - wy) * dy) / s
                d = abs((p[0] - wx) * dy - (p[1] - wy) * dx) / s
                dd = d - halfs.get(nme, 0)   # distance outside the member band
                if t > -2 and dd < bd:
                    best, bd, bt = nme, dd, t
            if best is not None and EDGE_PROFILE.get(st) and isRing and -1.0 <= bd <= 2.5:
                return 'SKIP', 'web', bd     # ring on the member edge line: a rivet drawn in profile (outstanding angle leg), not through the gusset
            return best or 'unassigned', 'web', bd
        counts = {}
        ext = {}
        marks = []
        for kind, L in (('dot', D), ('ring', R)):
            for p in L:
                ins = inside(p, poly)
                nme, cls, q = assign(p, kind == 'ring')
                if nme == 'SKIP':   # zone of symbols that are not gusset rivets (profile rivets at member edges, bracing strips, text)
                    continue
                if ins:
                    reg = 'inside'
                else:
                    dist = poly_dist(p, poly) / s
                    if cls == 'chord' and dist <= 48:
                        reg = 'beyond'
                    elif cls == 'web' and q <= 1.5 and dist <= 36:
                        reg = 'beyond'
                    else:
                        continue
                key = (nme, reg)
                if reg == 'beyond':
                    ext[nme] = max(ext.get(nme, 0), poly_dist(p, poly) / s)
                c = counts.setdefault(key, [0, 0])
                c[0 if kind == 'dot' else 1] += 1
                marks.append((p, nme, reg, kind))
                # position in the sheet frame of the gusset outline (gusset-outlines.csv outline_uv_in: u = drawing x, v up, origin = work point, inches)
                wu, wv = (p[0] - wx) / s, -(p[1] - wy) / s
                posrows.append(dict(structure=r['structure'], joint=pt, member=nme, region=reg, kind=kind, u_in=f"{wu:.2f}", v_in=f"{wv:.2f}", t_in='', y_in='', page=page, _w=(wu, wv)))
        # web members: also the member frame (t along the member from the work point, y = the perpendicular offset, + = up; a near-vertical member: + = toward
        # drawing +x). The axis direction is that of the member's own drawn gage lines (>= 8 symbols inside the outline: the direction within 12 deg of the
        # model ray that lines the symbols up into the sharpest rows), else the model ray rotated onto the drawn chord: keeps the drawn rows on the member when the drawn chord / member angles differ
        # from the model (the joint-frame shear keeps verticals plumb but turns diagonals)
        for nme_, ro_, ray in rays:
            if ro_ in CHORD:
                continue
            mine = [x for x in posrows if x['joint'] == pt and x['structure'] == r['structure'] and x['member'] == nme_ and '_w' in x]
            if not mine:
                continue
            rx, ry = ray[0], -ray[1]
            rl = math.hypot(rx, ry); rx, ry = rx / rl, ry / rl
            ins_ = [x['_w'] for x in mine if x['region'] == 'inside']
            if len(ins_) >= 8:
                # the drawn gage lines: the direction within +-12 deg of the ray whose perpendicular projections bunch the symbols into the sharpest rows
                # (histogram of 0.4 in. bins, sum of squared counts); robust to the gusset edge clipping the field obliquely
                def sharp(a):
                    ca, sa = math.cos(a), math.sin(a)
                    dx, dy = rx * ca - ry * sa, rx * sa + ry * ca
                    h = {}
                    for q in ins_:
                        k = int(math.floor((-dy * q[0] + dx * q[1]) / 0.4))
                        h[k] = h.get(k, 0) + 1
                    return sum(c * c for c in h.values())
                cands = [math.radians(0.25 * i) for i in range(-48, 49)]
                base = sharp(0.0)
                best = max(cands, key=lambda a: (sharp(a), -abs(a)))
                if sharp(best) > 1.15 * base:
                    ca, sa = math.cos(best), math.sin(best)
                    rx, ry = rx * ca - ry * sa, rx * sa + ry * ca
            nx, ny = -ry, rx
            if abs(ny) < 0.2:
                if nx < 0: nx, ny = -nx, -ny
            elif ny < 0: nx, ny = -nx, -ny
            for x in mine:
                wu, wv = x['_w']
                x['t_in'], x['y_in'] = f"{wu * rx + wv * ry:.2f}", f"{wu * nx + wv * ny:.2f}"
        for x in posrows:
            x.pop('_w', None)
        names = sorted({k[0] for k in counts})
        for (nme, reg), (nd, nr) in sorted(counts.items()):
            res.append(dict(structure=r['structure'], joint=pt, member=nme, region=reg, shop_dots=nd, field_rings=nr, total=nd + nr, page=page,
                            beyond_extent_in=f"{ext[nme]:.1f}" if reg == 'beyond' else '', chord_band_in=f"{lo:.1f}..{hi:.1f}"))
        tot_in = sum(v[0] + v[1] for k, v in counts.items() if k[1] == 'inside')
        print(f"{st:9s} {pt:5s} p.{page}: band {lo:5.1f}..{hi:4.1f} inside {tot_in:4d} | " + ', '.join(f"{n}:{counts.get((n, 'inside'), [0, 0])[0]}+{counts.get((n, 'inside'), [0, 0])[1]}"
              + (f"(b{sum(counts[(n, 'beyond')])})" if (n, 'beyond') in counts else '') for n in names))
        # overlay
        from PIL import Image, ImageDraw
        im = Image.fromarray(img).convert('RGB'); dr = ImageDraw.Draw(im)
        k = DPI / 150
        X = lambda p: ((p[0] - box[0]) * k, (p[1] - box[1]) * k)
        for gx in range(int(box[0] // 20 + 1) * 20, int(box[2]), 20):
            dr.line([X((gx, box[1])), X((gx, box[3]))], fill=(255, 215, 215) if gx % 40 else (255, 170, 170))
            if gx % 40 == 0: dr.text((X((gx, 0))[0] + 2, 2), str(gx), fill=(200, 0, 0))
        for gy in range(int(box[1] // 20 + 1) * 20, int(box[3]), 20):
            dr.line([X((box[0], gy)), X((box[2], gy))], fill=(215, 215, 255) if gy % 40 else (170, 170, 255))
            if gy % 40 == 0: dr.text((2, X((0, gy))[1] + 2), str(gy), fill=(0, 0, 200))
        dr.polygon([X(q) for q in poly], outline=(0, 200, 0))
        for m_, z_ in zones:
            dr.polygon([X(q) for q in z_], outline=(255, 140, 0))
        col = {n: COLORS[i % len(COLORS)] for i, n in enumerate(names)}
        for (p, nme, reg, kind) in marks:
            c = col[nme]; q = X(p); rr = 9 if kind == 'dot' else 11
            if reg == 'inside':
                dr.ellipse([q[0] - rr, q[1] - rr, q[0] + rr, q[1] + rr], outline=c, width=3)
            else:
                dr.rectangle([q[0] - rr, q[1] - rr, q[0] + rr, q[1] + rr], outline=c, width=2)
        for i, n in enumerate(names):
            dr.text((60, 20 + 14 * i), n, fill=col[n])
        wpx = X((wx, wy)); dr.line([wpx[0] - 15, wpx[1], wpx[0] + 15, wpx[1]], fill=(0, 200, 0), width=2)
        f = 1.0
        if im.width > 1400:
            f = 1400 / im.width; im = im.resize((1400, int(im.height * f)))
        print(f"   overlay: page px = ({box[0]:.1f} + ox / {k * f:.4f}, {box[1]:.1f} + oy / {k * f:.4f})")
        im.save(os.path.join(odir, f"{st}_{pt.replace(chr(39), 'p')}.png"))
    if posf:
        # every symbol counted (inside the outline or 'beyond' in a member band) with its drawn position: the rivet layout AS DRAWN (QC I052, rivets-ptc.csx)
        with open(posf, 'w', newline='', encoding='utf-8') as f:
            w = csv.DictWriter(f, fieldnames=['structure', 'joint', 'member', 'region', 'kind', 'u_in', 'v_in', 't_in', 'y_in', 'page'])
            w.writeheader()
            for x in posrows:
                w.writerow(x)
    with open(out, 'w', newline='', encoding='utf-8') as f:
        w = csv.DictWriter(f, fieldnames=['structure', 'joint', 'member', 'region', 'shop_dots', 'field_rings', 'total', 'page', 'beyond_extent_in', 'chord_band_in'])
        w.writeheader()
        for x in res:
            w.writerow(x)


if __name__ == '__main__':
    {'run': run}[sys.argv[1]](sys.argv[2:])
