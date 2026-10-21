"""QC pass 5: main truss web members, p.70 SECTION columns (transcribed from evidence/p5/p70_*.png) vs the .sbm sections. Read-only."""
import json, sys
from fractions import Fraction as F
SBM = r'C:\SBM\temp\qc5\ptc.sbm'
IN = 0.0254
def f(s):
    s = s.strip()
    if ' ' in s:
        a, b = s.split(); return float(int(a) + F(b))
    return float(F(s))
# member: (webs [(n_total, depth, t)] or channel, angles, cover (w, t), mat, printed gross)
P = {
 'U0-L0': ([(2,'23','9/16')], 'L6x6x7/16', ('24','7/16'), 'C', 67.12),
 'U1-L1': ('C18x42.7', None, ('28','3/8'), 'C', 45.98),
 'U2-L2': ('C18x45.8', None, ('28','1/2'), 'C', 54.76),
 'U3-L3': ('C18x42.7', None, ('28','3/8'), 'C', 45.96),
 'U4-L4': ('C18x45.8', None, ('28','1/2'), 'C', 54.76),
 'U5-L5': ('C18x45.8', None, ('28','1/2'), 'C', 54.76),
 'U6-L6': ('C18x45.8', None, ('28','1/2'), 'C', 54.76),
 'U7-L7': ('C18x45.8', None, ('28','1/2'), 'C', 54.76),
 'U8-L8': ('C18x45.8', None, ('28','1/2'), 'C', 54.76),
 'U9-L9': ([(2,'21','1/2')], 'L5x5x1/2', ('27','7/16'), 'C', 63.63),
 'U10-L10': ([(2,'26','1/2')], 'L7x4x1/2', ('27','1/2'), 'C', 74.00),
 'U12-L12': ([(4,'28','1')], 'L7x4x7/8', ('24','9/16'), 'S', 174.44),
 'U13-L13': ([(2,'26','15/16'),(2,'26','7/8')], 'L7x4x7/8', ('25','9/16'), 'S', 156.32),
 'U14-L14': ([(4,'26','13/16')], 'L7x4x3/4', ('25','9/16'), 'S', 143.39),
 'U15-L15': ([(4,'26','11/16')], 'L7x4x3/4', ('25','5/8'), 'S', 133.51),
 'U16-L16': ([(2,'26','1 1/8')], 'L7x4x3/4', ('26','5/8'), 'S', 121.76),
 'U17-L17': ([(2,'26','15/16')], 'L7x4x3/4', ('26','9/16'), 'S', 108.76),
 'U18-L18': ([(2,'26','13/16')], 'L7x4x5/8', ('28','1/2'), 'S', 94.17),
 'U19-L19': ([(2,'24','9/16')], 'L7x4x9/16', ('27','1/2'), 'S', 77.48),
 'U20-L20': ('C18x51.9', None, ('28','9/16'), 'S', 61.86),
 'U21-L21': ('C18x45.8', None, ('28','1/2'), 'C', 54.76),
 'U22-L22': ('C18x42.7', None, ('28','3/8'), 'C', 45.96),
 'L0-U1': ('C18x45.8', None, ('28','1/2'), 'C', None),
 'U1-L2': ('C18x58', None, ('28','9/16'), 'C', None),
 'L2-U3': ([(2,'21','9/16')], 'L7x4x1/2', ('27','3/8'), 'S', None),
 'U3-L4': ([(2,'24','5/8')], 'L7x4x1/2', ('27','1/2'), 'S', None),
 'L4-U5': ([(2,'26','3/4')], 'L7x4x9/16', ('27','1/2'), 'S', None),
 'U5-L6': ([(2,'26','7/8')], 'L7x4x9/16', ('26','1/2'), 'S', None),
 'L6-U7': ([(2,'26','13/16')], 'L7x4x5/8', ('26','1/2'), 'S', None),
 'U7-L8': ([(2,'26','13/16')], 'L7x4x9/16', ('27','1/2'), 'S', None),
 'L8-U9': ([(2,'26','5/8')], 'L7x4x1/2', ('27','1/2'), 'S', None),
 'U9-L10': ([(2,'26','9/16')], 'L7x4x1/2', ('27','1/2'), 'S', None),
 'L10-U11': ([(2,'26','7/16')], 'L7x4x7/16', ('27','3/8'), 'S', None),
 'U11-L12': ([(2,'26','1')], 'L7x4x7/8', ('26','9/16'), 'S', None),
 'U12-L13': ([(2,'26','1 1/16')], 'L7x4x7/8', ('26','9/16'), 'S', None),
 'U13-L14': ([(2,'26','1')], 'L7x4x7/8', ('26','9/16'), 'S', None),
 'U14-L15': ([(2,'28','1')], 'L7x4x7/8', ('26','9/16'), 'S', None),
 'U15-L16': ([(2,'26','1 1/16')], 'L7x4x7/8', ('26','9/16'), 'S', None),
 'U16-L17': ([(2,'26','1 1/16')], 'L7x4x7/8', ('26','9/16'), 'S', None),
 'U17-L18': ([(2,'26','1 1/16')], 'L7x4x7/8', ('26','9/16'), 'S', None),
 'U18-L19': ([(2,'26','1')], 'L7x4x3/4', ('26','9/16'), 'S', None),
 'U19-L20': ([(2,'24','7/8')], 'L7x4x3/4', ('26','1/2'), 'S', None),
 'U20-L21': ([(2,'21','5/8')], 'L7x4x9/16', ('27','1/2'), 'S', None),
 'U21-L22': ('C18x51.9', None, ('28','1/2'), 'C', None),
}
d = json.load(open(SBM))
b = [b for b in d['Bridges'] if b['Name'].startswith('PTC Arch')][0]
t = b['Truss']; secs = {s['Name']: s for s in t['Sections']}
mem = {}
for m in t['Members']:
    mem.setdefault(m['Name'], m)
def r16(x): return round(x / IN * 16) / 16
out = []
for name, (webs, ang, cov, mat, area) in P.items():
    m = mem.get(name)
    if not m:
        out.append((name, 'missing', '', '', 'fail')); continue
    s = secs[m['Section']]
    probs = []
    if isinstance(webs, str):
        ch = (s.get('Channel') or '').replace('MC', 'C')
        if ch.replace('.0', '') != webs.replace('.0', ''): probs.append('channel %s' % s.get('Channel'))
        mw = 'channel ' + (s.get('Channel') or s['Kind'])
    else:
        plies = [(r16(p['Depth']), r16(p['Thickness'])) for p in s.get('WebPlies') or []]
        want = []
        for n, dd, tt in webs: want += [(f(dd), f(tt))] * (n // 2)
        if sorted(plies) != sorted(want): probs.append('plies %s vs %s' % (plies, want))
        mw = ' + '.join('%gx%g' % p for p in plies) + ' /side'
        if (s.get('Angle') or '') != (ang or ''): probs.append('angle %s' % s.get('Angle'))
        if s.get('AnglesOutside'): probs.append('angles outside')
    covs = [(r16(p['Width']), r16(p['Thickness']), p.get('Perforated'), p.get('Length') or 0) for p in (s.get('TopPlates') or []) + (s.get('BottomPlates') or [])]
    if len(covs) != 2 or any((c[0], c[1]) != (f(cov[0]), f(cov[1])) or not c[2] or c[3] for c in covs): probs.append('covers %s' % covs)
    if (s.get('Material') or '') != mat: probs.append('mat %s' % s.get('Material'))
    model = '%s; %s; covers %s; %s' % (mw, s.get('Angle') or '-', ','.join('%gx%g%s' % (c[0], c[1], ' perf' if c[2] else '') for c in covs), s.get('Material'))
    plan = '%s; %s; 2-Pls (%s-12)x%s; %s' % (webs if isinstance(webs, str) else ' + '.join('%d-Pls %sx%s' % w for w in webs), ang or '-', cov[0], cov[1], mat)
    out.append((name, plan, model, '; '.join(probs), 'fail' if probs else 'pass'))
for o in out: print(' | '.join(map(str, o)))
json.dump(out, open(sys.argv[1] if len(sys.argv) > 1 else 'web_cmp.json', 'w'), indent=0)
