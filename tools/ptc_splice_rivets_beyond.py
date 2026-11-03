"""Rivet symbols drawn on the chord web splice plates / fills BEYOND the gusset edge and beyond the drawn rivets already recorded in rivet-positions.csv.
python -I tools/ptc_splice_rivets_beyond.py docs/plan-data/ptc-delaware-river/connections/splice-rivets-beyond.csv
Per main-span joint with its own gusset outline and side of the joint (lower / higher panel number): zone = from max(gusset outline edge in the web band,
outermost recorded 'beyond' rivet of that chord) + 1.5 in. to the far end of the longest web-face splice plate / fill on that side (splices.csv u0 / u1);
symbols (tools/ptc_rivet_count.detect: solid dots = shop, open circles = field) inside the zone and |v| <= 15 in. of the chord CL are counted; rows = gage lines
(v clusters 1.5 in. apart)."""
import csv, math, os, sys
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import ptc_rivet_count as rc
CON = os.path.join(ROOT, r'docs\plan-data\ptc-delaware-river\connections')
PX = 4.28
outl = {r['joint']: r for r in csv.DictReader(open(os.path.join(CON, 'gusset-outlines.csv'), encoding='utf-8')) if r['structure'] == 'main'}
spl = [r for r in csv.DictReader(open(os.path.join(CON, 'splices.csv'), encoding='utf-8')) if r['structure'] == 'main']
pos = [r for r in csv.DictReader(open(os.path.join(CON, 'rivet-positions.csv'), encoding='utf-8')) if r['structure'] == 'main' and r['region'] == 'beyond']
rows_out = []
# U21 / L21 share the U20 / L20 outline entry: own W.P. and chord direction read on p.89 (reader D); gusset edge = plate end - the drawn ~30 in. beyond
# (agent review batch3: L21 field rivets about 21 in. left of the gusset edge, U21 about 24 in.)
MAN = {'U21': ((1411.5, 471.0), 1.6, {-1: 66.3 - 24, 1: 85.2 - 30}), 'L21': ((1411.5, 1025.0), 3.0, {-1: 71.2 - 21, 1: 78.6 - 30})}
for j in MAN:
    outl[j] = {'wp_px': '%f,%f' % MAN[j][0], 'chord_px': '0,0;%f,%f' % (math.cos(math.radians(MAN[j][1])), (-1 if j == 'U21' else 1) * math.sin(math.radians(MAN[j][1]))),
               'outline_uv_in': '', 'page': '89'}
for j, g in sorted(outl.items()):
    wx, wy = [float(v) for v in g['wp_px'].split(',')]
    (ax, ay), (bx, by) = [tuple(float(c) for c in q.split(',')) for q in g['chord_px'].split(';')]
    L = math.hypot(bx - ax, by - ay); dx, dy = (bx - ax) / L, (by - ay) / L
    nx, ny = dy, -dx   # up normal in page px
    poly = rc.pts(g['outline_uv_in'])
    page = int(g['page'])
    for side in (-1, 1):
        ps = [r for r in spl if r['joint'] == j and r['face'] in ('web-os', 'web-is', 'fill-is', 'fill-os') and r['u0_in'] and r['u1_in']]
        ends = [float(r['u1_in']) if side > 0 else -float(r['u0_in']) for r in ps]
        if not ends or max(ends) <= 0:
            continue
        E = max(ends)
        carrier = max(ps, key=lambda r: (float(r['u1_in']) if side > 0 else -float(r['u0_in']), r['face'] == 'web-os'))
        # gusset edge over the web band: farthest outline point along the side with |v| <= 17 (sampled edges)
        G = 0.0
        for i in range(len(poly)):
            (u1, v1), (u2, v2) = poly[i - 1], poly[i]
            for k in range(41):
                u = u1 + (u2 - u1) * k / 40; v = v1 + (v2 - v1) * k / 40
                if abs(v) <= 17:
                    G = max(G, side * u)
        if j in MAN:
            G = MAN[j][2][side]
        mem = [float(r['u_in']) for r in pos if r['joint'] == j and side * float(r['u_in']) > 0 and abs(float(r['v_in'])) <= 17]
        R = max([side * u for u in mem], default=0.0)
        Z0 = max(G, R) + 1.5
        if E - Z0 < 3:
            continue
        # page box of the zone
        cs = []
        for u in (Z0, E):
            for v in (-17, 17):
                cs.append((wx + PX * (u * side * dx + v * nx), wy + PX * (u * side * dy + v * ny)))
        x0 = min(c[0] for c in cs) - 10; x1 = max(c[0] for c in cs) + 10; y0 = min(c[1] for c in cs) - 10; y1 = max(c[1] for c in cs) + 10
        D, Rg, _ = rc.detect(page, (x0, y0, x1, y1), rc.CHAIN_PX['main'])
        hits = []
        for (px, py) in D + Rg:
            u = ((px - wx) * dx + (py - wy) * dy) / PX * side; v = ((px - wx) * nx + (py - wy) * ny) / PX
            if Z0 - 0.5 <= u <= E - 0.5 and abs(v) <= 15:
                hits.append((u, v))
        if len(hits) < 6:
            continue
        vs = sorted(v for _, v in hits); nrow = 1
        for a, b in zip(vs, vs[1:]):
            if b - a > 1.5:
                nrow += 1
        rows_out.append({'joint': j, 'side': 'L' if side < 0 else 'R', 'from_in': f'{side * Z0:.1f}' if side > 0 else f'{-E:.1f}', 'to_in': f'{E:.1f}' if side > 0 else f'{-Z0:.1f}',
                         'count': len(hits), 'rows': nrow, 'plate': carrier['item'], 'page': page,
                         'note': f'gusset edge {G:.1f} in., recorded beyond rivets to {R:.1f} in., plate end {E:.1f} in. from the W.P.; {len(D)} dots / {len(Rg)} rings in the box (symbol detector of tools/ptc_rivet_count.py)'})
        print(j, side, f'G {G:.1f} R {R:.1f} E {E:.1f}', len(hits), nrow, carrier['item'][:40])
with open(sys.argv[1], 'w', newline='', encoding='utf-8') as f:
    w = csv.DictWriter(f, fieldnames=['joint', 'side', 'from_in', 'to_in', 'count', 'rows', 'plate', 'page', 'note'], lineterminator='\n')
    w.writeheader(); [w.writerow(r) for r in rows_out]
