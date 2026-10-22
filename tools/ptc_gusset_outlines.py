"""PTC Delaware River Bridge - gusset plate outlines read from the raster joint sheets (2026-10-20).

Reading method (docs/plan-data/ptc-delaware-river/connections/README.md, "Gusset outlines"): the gusset is read as a polygon in ABSOLUTE page pixels
at a chosen dpi from grid-overlaid renders, together with the work point (intersection of the member centre lines), two points on the chord
centre line (direction only) and the sheet scale (px per inch from a printed dimension). `convert` turns the pixel readings into the joint frame
in inches (u = drawing x = toward the higher panel number, v up, origin = work point) and adds the drawn chord angle, size and area.

  python -I tools/ptc_gusset_outlines.py grid PAGE DPI X0 Y0 X1 Y1 out.png [grid=20] [poly="x,y;x,y;..."] [marks="x,y;..."]
      render a crop (absolute page pixels at DPI) with a labelled grid (labels = absolute coordinates), optional polygon (green) / marks (blue)
  python -I tools/ptc_gusset_outlines.py prof PAGE DPI Y X0 X1          dark runs along row Y   (centre[width])
  python -I tools/ptc_gusset_outlines.py prof PAGE DPI x=X Y0 Y1        dark runs along column X
  python -I tools/ptc_gusset_outlines.py convert raw.csv out.csv        derive outline_uv_in etc. (rows keep their pixel data)
  python -I tools/ptc_gusset_outlines.py overlay out.csv ROWINDEX out.png [margin_px]   the row's polygon + work point over the page

One page is opened per call (the 108-page raster PDF is large).
"""
import csv, math, sys

PDF = r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf"


def _page(pg):
    import pymupdf
    return pymupdf.open(PDF)[pg - 1]


def _render(pg, dpi, x0, y0, x1, y1):
    import pymupdf
    from PIL import Image
    p = _page(pg)
    k = 72.0 / dpi
    pix = p.get_pixmap(matrix=pymupdf.Matrix(dpi / 72.0, dpi / 72.0), clip=pymupdf.Rect(x0 * k, y0 * k, x1 * k, y1 * k), colorspace=pymupdf.csGRAY)
    return Image.frombytes("L", (pix.width, pix.height), pix.samples)


def pts(s):
    return [tuple(float(c) for c in q.split(',')) for q in s.split(';') if q.strip()]


def grid(argv):
    from PIL import ImageDraw
    pg, dpi = int(argv[0]), int(argv[1])
    x0, y0, x1, y1 = [float(v) for v in argv[2:6]]
    out = argv[6]
    opts = dict(a.split('=', 1) for a in argv[7:])
    g = int(opts.get('grid', 20))
    z = float(opts.get('zoom', 1))  # render at dpi * zoom, grid labels stay in page pixels at DPI
    src = _render(pg, dpi * z, x0 * z, y0 * z, x1 * z, y1 * z)
    im = src.convert("RGB")
    d = ImageDraw.Draw(im)
    if g > 0:
        for x in range(int(math.ceil(x0 / g)) * g, int(x1) + 1, g):
            major = x % (2 * g) == 0
            d.line([((x - x0) * z, 0), ((x - x0) * z, im.height)], fill=(255, 110, 110) if major else (255, 185, 185), width=1)
            if major:
                d.text(((x - x0) * z + 2, 2), str(x), fill=(220, 0, 0))
                d.text(((x - x0) * z + 2, im.height - 12), str(x), fill=(220, 0, 0))
        for y in range(int(math.ceil(y0 / g)) * g, int(y1) + 1, g):
            major = y % (2 * g) == 0
            d.line([(0, (y - y0) * z), (im.width, (y - y0) * z)], fill=(110, 110, 255) if major else (185, 185, 255), width=1)
            if major:
                d.text((2, (y - y0) * z + 2), str(y), fill=(0, 0, 220))
                d.text((im.width - 30, (y - y0) * z + 2), str(y), fill=(0, 0, 220))
        px, sp = im.load(), src.load()
        for yy in range(im.height):
            for xx in range(im.width):
                if sp[xx, yy] < 128:
                    px[xx, yy] = (sp[xx, yy],) * 3
    if 'poly' in opts:
        P = pts(opts['poly'])
        d.line([((x - x0) * z, (y - y0) * z) for x, y in P + [P[0]]], fill=(0, 170, 0), width=2)
    if 'marks' in opts:
        for x, y in pts(opts['marks']):
            d.ellipse([(x - x0) * z - 5, (y - y0) * z - 5, (x - x0) * z + 5, (y - y0) * z + 5], outline=(0, 140, 255), width=2)
    im.save(out)
    print(im.size)


def prof(argv):
    import numpy as np, pymupdf
    pg, dpi = int(argv[0]), int(argv[1])
    k = 72.0 / dpi
    p = _page(pg)
    if argv[2].startswith('x='):
        x = float(argv[2][2:]); a0, a1 = float(argv[3]), float(argv[4])
        pix = p.get_pixmap(dpi=dpi, clip=pymupdf.Rect((x - 1) * k, a0 * k, (x + 2) * k, a1 * k), colorspace=pymupdf.csGRAY)
        arr = np.frombuffer(pix.samples, np.uint8).reshape(pix.height, pix.width).min(axis=1)
    else:
        y = float(argv[2]); a0, a1 = float(argv[3]), float(argv[4])
        pix = p.get_pixmap(dpi=dpi, clip=pymupdf.Rect(a0 * k, (y - 1) * k, a1 * k, (y + 2) * k), colorspace=pymupdf.csGRAY)
        arr = np.frombuffer(pix.samples, np.uint8).reshape(pix.height, pix.width).min(axis=0)
    dark = arr < 140
    runs, i = [], 0
    while i < len(dark):
        if dark[i]:
            j = i
            while j < len(dark) and dark[j]:
                j += 1
            runs.append((a0 + i, a0 + j - 1)); i = j
        else:
            i += 1
    print(' '.join(f"{(a + b) / 2:.1f}" + (f"[{int(b - a + 1)}]" if b - a > 2 else '') for a, b in runs))


def area(P):
    return 0.5 * sum(P[i][0] * P[(i + 1) % len(P)][1] - P[(i + 1) % len(P)][0] * P[i][1] for i in range(len(P)))


def derive(r):
    s = float(r['px_per_in'])
    wx, wy = pts(r['wp_px'])[0]
    (ax, ay), (bx, by) = pts(r['chord_px'])[:2]
    dx, dy = bx - ax, -(by - ay)
    if dx < 0 or (dx == 0 and dy < 0):
        dx, dy = -dx, -dy
    r['drawn_chord_deg'] = f"{math.degrees(math.atan2(dy, dx)):.2f}"
    uv = [((x - wx) / s, -(y - wy) / s) for x, y in pts(r['outline_px'])]
    if area(uv) < 0:
        uv.reverse()
    r['outline_uv_in'] = '; '.join(f"{u:.1f},{v:.1f}" for u, v in uv)
    r['width_in'] = f"{max(u for u, _ in uv) - min(u for u, _ in uv):.1f}"
    r['height_in'] = f"{max(v for _, v in uv) - min(v for _, v in uv):.1f}"
    r['area_sf'] = f"{area(uv) / 144:.2f}"
    return r


COLS = ['structure', 'joint', 'also_applies_to', 'page', 'dpi', 'px_per_in', 'scale_basis', 'wp_px', 'chord_px', 'outline_px',
        'drawn_chord_deg', 'outline_uv_in', 'width_in', 'height_in', 'area_sf', 'evidence', 'confidence', 'note']


def convert(argv):
    rows = list(csv.DictReader(open(argv[0], newline='', encoding='utf-8')))
    out = [derive(dict(r)) for r in rows]
    with open(argv[1], 'w', newline='', encoding='utf-8') as f:
        w = csv.DictWriter(f, fieldnames=COLS, extrasaction='ignore')
        w.writeheader()
        for r in out:
            w.writerow({c: r.get(c, '') for c in COLS})
    for r in out:
        print(f"{r['structure']:22s} {r['joint']:6s} p.{r['page']:3s} {r['width_in']:>6s} x {r['height_in']:>6s} in  {r['area_sf']:>6s} SF  chord {r['drawn_chord_deg']} deg")


def overlay(argv):
    rows = list(csv.DictReader(open(argv[0], newline='', encoding='utf-8')))
    r = rows[int(argv[1])]
    m = float(argv[3]) if len(argv) > 3 else 40
    P = pts(r['outline_px'])
    x0, y0 = min(x for x, _ in P) - m, min(y for _, y in P) - m
    x1, y1 = max(x for x, _ in P) + m, max(y for _, y in P) + m
    grid([r['page'], r['dpi'], str(x0), str(y0), str(x1), str(y1), argv[2], 'grid=0', 'poly=' + r['outline_px'], 'marks=' + r['wp_px']])


def row_direction(q, a0, span_deg=20.0):
    """Direction (rad, sheet frame u right / v up) within +-span_deg of a0 that bunches the points' perpendicular projections into the sharpest
    rows (sum of squared bin counts, bins 0.33 / 0.4 / 0.5 in.): the drawn rivet gage lines. Returns (angle, score per point)."""
    def sharp(a, b):
        dx, dy = math.cos(a), math.sin(a)
        h = {}
        for x, y in q:
            k = int(math.floor((-dy * x + dx * y) / b))
            h[k] = h.get(k, 0) + 1
        return sum(c * c for c in h.values())
    n = int(span_deg * 10)
    cands = [a0 + math.radians(0.1 * i) for i in range(-n, n + 1)]
    sc = [sharp(a, 0.33) + sharp(a, 0.4) + sharp(a, 0.5) for a in cands]
    i = max(range(len(cands)), key=lambda k: (sc[k], -abs(k - n)))
    return cands[i], sc[i] / (3 * len(q))


def chordrows(argv):
    """python -I tools/ptc_gusset_outlines.py chordrows outlines.csv rivet-positions.csv [structure] [write] [min=0.75]
    Re-reads the DRAWN CHORD DIRECTION of every outline row from the chord rivet gage lines (rivet-positions.csv: symbols of the chord members, both
    ends of the member name with the joint's letter, >= 8 symbols): per chord member the row direction (row_direction), mean over the chords. The
    chord centre-line points read by eye (chord_px) were up to 12 deg off on the main truss arch sheets (the drawn chord is the TRUE chord: the rows
    agree with the model chords within 1/2 deg). 'write' replaces chord_px (two points through the work point on the row direction) where the
    difference exceeds min (deg) and notes it."""
    out, pos = argv[0], argv[1]
    opts = [a for a in argv[2:]]
    write = 'write' in opts
    mind = float(next((a[4:] for a in opts if a.startswith('min=')), 0.75))
    only = next((a for a in opts if a not in ('write',) and not a.startswith('min=')), None)
    rows = list(csv.DictReader(open(out, newline='', encoding='utf-8')))
    grp = {}
    for p in csv.DictReader(open(pos, newline='', encoding='utf-8')):
        grp.setdefault((p['structure'], p['joint'], p['member']), []).append((float(p['u_in']), float(p['v_in'])))
    changed = 0
    for r in rows:
        st = r['structure'].split()[0]
        if only and st != only:
            continue
        old = float(r['drawn_chord_deg'])
        let = r['joint'][:1]
        dirs = []
        for (s_, j_, m_), q in grp.items():
            if s_ != r['structure'] or j_ != r['joint'] or len(q) < 8:
                continue
            a, _, b = m_.partition('-')
            if not (a[:1] == let and b[:1] == let):
                continue
            ang, scr = row_direction(q, math.radians(old))
            dirs.append((m_, len(q), math.degrees(ang), scr))
        if not dirs:
            print(f"{st:9s} {r['joint']:5s} p.{r['page']}: no chord rivet rows"); continue
        new = sum(d[2] for d in dirs) / len(dirs)
        flag = abs(new - old) > mind
        print(f"{st:9s} {r['joint']:5s} p.{r['page']}: drawn {old:6.2f} rows {new:6.2f} ({', '.join(f'{m}:{n} {a:.2f}' for m, n, a, _ in dirs)}){'  <- re-read' if flag else ''}")
        if write and flag:
            wx, wy = pts(r['wp_px'])[0]
            c, s = math.cos(math.radians(new)), math.sin(math.radians(new))
            r['chord_px'] = f"{wx - 250 * c:.1f},{wy + 250 * s:.1f}; {wx + 250 * c:.1f},{wy - 250 * s:.1f}"
            r['note'] = (r['note'] + '; ' if r['note'] else '') + f"drawn chord direction re-read 2026-10-22 from the chord rivet gage lines ({new:.2f} deg; the centre-line points read by eye gave {old:.2f})"
            derive(r)
            changed += 1
    if write:
        with open(out, 'w', newline='', encoding='utf-8') as f:
            w = csv.DictWriter(f, fieldnames=COLS, extrasaction='ignore', lineterminator=chr(10))
            w.writeheader()
            for r in rows:
                w.writerow({c: r.get(c, '') for c in COLS})
        print(f"{changed} rows re-read")


def symmetric_centre(vals, span=5.0):
    """Centre (in.) about which the clustered rows of the values are most nearly symmetric (rows with < 3 symbols ignored)."""
    vals = sorted(vals)
    rows = []
    for v in vals:
        if rows and v - rows[-1][-1] < 0.6:
            rows[-1].append(v)
        else:
            rows.append([v])
    rows = [(sum(r) / len(r), len(r)) for r in rows if len(r) >= 3]
    if len(rows) < 4:
        return None, rows
    # evenly pitched rows are symmetric about several centres half a pitch apart: search only near the midpoint of the extreme full rows
    big = [r for r, w in rows if w >= max(3, 0.5 * max(w_ for _, w_ in rows))]
    mid = (min(big) + max(big)) / 2
    best = None
    for i in range(int((mid - 1.2) * 20), int((mid + 1.2) * 20) + 1):
        c = i / 20.0
        if abs(c) > span:
            continue
        sc = sum(w * min(1.0, min(abs(2 * c - r - q) for q, _ in rows)) for r, w in rows)
        if best is None or sc < best[0] - 1e-9 or (abs(sc - best[0]) < 1e-9 and abs(c) < abs(best[1])):
            best = (sc, c)
    if best is None:
        return None, rows
    return best[1], rows


def wpaxes(argv):
    """python -I tools/ptc_gusset_outlines.py wpaxes outlines.csv rivet-positions.csv [structure] [write] [min=0.75] [joints=U8,L13,...]
    Re-reads the WORK POINT from the drawn rivet fields (2026-10-22). Every member's rivet field is symmetric about the member's working line (box
    chords: the rows about the web centre = the model working line; posts, hangers and diagonals: the gage lines about the member axis). Per member
    with >= 8 symbols inside the outline: the gage-line direction (row_direction: chords near the drawn chord, web members near the direction of their
    drawn (t, y) frame) and the symmetric centre of the rows across it (symmetric_centre) give the member's axis line; the work point is the least-squares
    intersection of the axis lines (weights = symbols). Prints the move and the residual per member; 'write' moves the work point (and the chord_px points
    with it) where the move exceeds min (in.) and every axis passes within 1 in. of the new point."""
    out, pos = argv[0], argv[1]
    opts = argv[2:]
    write = 'write' in opts
    mind = float(next((a[4:] for a in opts if a.startswith('min=')), 0.75))
    jsel = next((a[7:].split(',') for a in opts if a.startswith('joints=')), None)
    only = next((a for a in opts if a != 'write' and '=' not in a), None)
    rows = list(csv.DictReader(open(out, newline='', encoding='utf-8')))
    grp = {}
    for p in csv.DictReader(open(pos, newline='', encoding='utf-8')):
        if p['region'] != 'inside':
            continue
        grp.setdefault((p['structure'], p['joint'], p['member']), []).append(
            (float(p['u_in']), float(p['v_in']), float(p['t_in']) if p['t_in'] else None, float(p['y_in']) if p['y_in'] else None))
    changed = 0
    for r in rows:
        st = r['structure'].split()[0]
        if only and st != only or jsel and r['joint'] not in jsel:
            continue
        a0 = math.radians(float(r['drawn_chord_deg']))
        let = r['joint'][:1]
        lines = []
        for (s_, j_, m_), q in grp.items():
            if s_ != r['structure'] or j_ != r['joint'] or len(q) < 8 or m_ in ('unassigned', 'chord?'):
                continue
            a, _, b = m_.partition('-')
            chord = a[:1] == let and b[:1] == let
            if chord:
                g0 = a0
            else:
                tq = [x for x in q if x[2] is not None]
                if len(tq) < 4:
                    continue
                # (t, y) frame: t = u rx + v ry -> least-squares direction of the drawn member frame
                suu = sum(x[0] * x[0] for x in tq); svv = sum(x[1] * x[1] for x in tq); suv = sum(x[0] * x[1] for x in tq)
                sut = sum(x[0] * x[2] for x in tq); svt = sum(x[1] * x[2] for x in tq)
                det = suu * svv - suv * suv
                if abs(det) < 1e-9:
                    continue
                rx, ry = (sut * svv - svt * suv) / det, (svt * suu - sut * suv) / det
                g0 = math.atan2(ry, rx)
            uv = [(x[0], x[1]) for x in q]
            ang, _ = row_direction(uv, g0, 8.0)
            nx, ny = -math.sin(ang), math.cos(ang)            # axis normal (left of the direction)
            c, rr = symmetric_centre([u * nx + v * ny for u, v in uv], 8.0)
            if c is None:
                continue
            lines.append((m_, nx, ny, c, len(q)))
        if len(lines) < 2:
            print(f"{st:9s} {r['joint']:5s}: fewer than two member axes - skipped"); continue
        def solve(ls):
            # least squares: sum w (n . d - c)^2
            A11 = sum(w * nx * nx for _, nx, ny, c, w in ls); A12 = sum(w * nx * ny for _, nx, ny, c, w in ls); A22 = sum(w * ny * ny for _, nx, ny, c, w in ls)
            b1 = sum(w * nx * c for _, nx, ny, c, w in ls); b2 = sum(w * ny * c for _, nx, ny, c, w in ls)
            det = A11 * A22 - A12 * A12
            if abs(det) < 1e-6 * max(1.0, A11 * A22):
                return None
            return (b1 * A22 - b2 * A12) / det, (b2 * A11 - b1 * A12) / det
        dropped = []
        sol = solve(lines)
        if sol is None:
            print(f"{st:9s} {r['joint']:5s}: member axes parallel - skipped"); continue
        # outliers (fields of two members mixed by the symbol assignment, fields cut by the outline): drop the worst axis while it misses by > 1 in. and
        # three axes in two directions remain
        while True:
            res = [(m, nx * sol[0] + ny * sol[1] - c) for m, nx, ny, c, w in lines]
            k = max(range(len(res)), key=lambda i: abs(res[i][1]))
            if abs(res[k][1]) <= 1.0 or len(lines) <= 3:
                break
            rest = lines[:k] + lines[k + 1:]
            s2 = solve(rest)
            if s2 is None:
                break
            dropped.append(res[k][0]); lines = rest; sol = s2
        du, dv = sol
        res = [(m, nx * du + ny * dv - c) for m, nx, ny, c, w in lines] + [(m + ' (dropped)', float('nan')) for m in dropped]
        worst = max(abs(x) for _, x in res if x == x)
        mv = math.hypot(du, dv)
        num = ''.join(ch for ch in r['joint'] if ch.isdigit())
        vert_dropped = any(a_[:1] != b_[:1] and a_.rstrip("'")[1:] == num == b_.rstrip("'")[1:] for a_, _, b_ in (m.partition('-') for m in dropped))
        # moved only when the axes agree: residuals <= 1 in., at most one axis dropped and never the vertical / post / hanger (the most reliable u)
        flag = mv > mind and worst <= 1.0 and len(dropped) <= 1 and not vert_dropped
        print(f"{st:9s} {r['joint']:5s} p.{r['page']}: move u {du:+.2f} v {dv:+.2f} in. (|{mv:.2f}|), residuals {', '.join(f'{m} {x:+.2f}' for m, x in res)}"
              + ('  <- re-read' if flag else ('  (axes disagree: not moved)' if mv > mind else '')))
        if write and flag:
            s = float(r['px_per_in'])
            wx, wy = pts(r['wp_px'])[0]
            nwx, nwy = wx + du * s, wy - dv * s
            (ax, ay), (bx, by) = pts(r['chord_px'])[:2]
            r['wp_px'] = f"{nwx:.1f},{nwy:.1f}"
            r['chord_px'] = f"{ax + du * s:.1f},{ay - dv * s:.1f}; {bx + du * s:.1f},{by - dv * s:.1f}"
            r['note'] = (r['note'] + '; ' if r['note'] else '') + (f"work point re-read 2026-10-22 = intersection of the member axes found from the drawn rivet fields "
                                                                  f"(each field symmetric about its member's working line): moved {du:+.1f} / {dv:+.1f} in. (was {wx:g},{wy:g})")
            derive(r)
            changed += 1
    if write:
        with open(out, 'w', newline='', encoding='utf-8') as f:
            w = csv.DictWriter(f, fieldnames=COLS, extrasaction='ignore', lineterminator=chr(10))
            w.writeheader()
            for r in rows:
                w.writerow({c: r.get(c, '') for c in COLS})
        print(f"{changed} rows re-read")



if __name__ == '__main__':
    cmd = sys.argv[1]
    {'grid': grid, 'prof': prof, 'convert': convert, 'overlay': overlay, 'chordrows': chordrows, 'wpaxes': wpaxes}[cmd](sys.argv[2:])
