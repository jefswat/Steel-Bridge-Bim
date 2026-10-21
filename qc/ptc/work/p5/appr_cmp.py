"""QC pass 5: approach truss member tables p.47 (3-span) / p.57 (4-span) (transcribed from evidence/p5/p47_*.png, p57_*.png)
vs the .sbm member sections: designation, material, cover plates (size, which flanges), orientation. Read-only.
  python -I appr_cmp.py out.json"""
import json, sys
SBM = r'C:\SBM\temp\qc5\ptc.sbm'
IN = 0.0254
A = {  # 3-span p.47: member -> (designation, mat)
 'U0-U1': ('14 WF 87', 'C'), 'U1-U3': ('14 WF 219', 'S'), 'U3-U5': ('14 WF 246', 'S'), 'U5-U7': ('14 WF 103', 'S'),
 'U7-U9': ('14 WF 246', 'S'), 'U9-U11': ('14 WF 87', 'C'), 'U11-U11\'': ('14 WF 111', 'S'),
 'L0-L2': ('14 WF 111', 'S'), 'L2-L4': ('14 WF 211', 'S'), 'L4-L6': ('14 WF 167', 'S'), 'L6-L8': ('14 WF 111', 'S'),
 'L8-L10': ('14 WF 142', 'S'), 'L10-L12': ('14 WF 87', 'S'),
 'L0-U1': ('14 WF 228', 'S'), 'U1-L2': ('14 WF 111', 'S'), 'L2-U3': ('14 WF 142', 'C'), 'U3-L4': ('14 WF 142', 'C'),
 'L4-U5': ('14 WF 87', 'S'), 'U5-L6': ('14 WF 202', 'S'), 'L6-U7': ('14 WF 184', 'S'), 'U7-L8': ('14 WF 342', 'S'),
 'L8-U9': ('14 WF 287', 'S'), 'U9-L10': ('14 WF 142', 'S'), 'L10-U11': ('14 WF 150', 'S'), 'U11-L12': ('14 WF 142', 'C'),
 'U0-L0': ('14 WF 84', 'S'), 'U8-L8': ('16 WF 96', 'S'),
}
for k in (2, 4, 6, 10, 12): A['U%d-L%d' % (k, k)] = ('14 WF 84', 'S')
for k in (1, 3, 5, 7, 9, 11): A['U%d-L%d' % (k, k)] = ('14 WF 61', 'C')
B = {  # 4-span p.57: member -> (designation, mat, cover plates 'w x t' or None)
 'U0-U1': ('14 WF 87', 'C', None), 'U1-U3': ('14 WF 287', 'S', None), 'U3-U5': ('14 WF 287', 'S', '16 x 15/16'),
 'U5-U7': ('14 WF 314', 'S', None), 'U7-U9': ('14 WF 95', 'S', None), 'U9-U11': ('14 WF 342', 'S', '16 1/2 x 13/16'),
 'U11-U13': ('14 WF 119', 'S', None), 'U13-U15': ('14 WF 150', 'S', None), 'U15-U17': ('14 WF 184', 'S', None),
 'U17-U19': ('14 WF 87', 'C', None), 'U19-U19\'': ('14 WF 314', 'S', None),
 'L0-L2': ('14 WF 142', 'S', None), 'L2-L4': ('14 WF 314', 'S', None), 'L4-L6': ('14 WF 314', 'S', None),
 'L6-L8': ('14 WF 167', 'S', None), 'L8-L10': ('14 WF 287', 'S', None), 'L10-L12': ('14 WF 314', 'S', None),
 'L12-L14': ('14 WF 87', 'S', None), 'L14-L16': ('14 WF 158', 'S', None), 'L16-L18': ('14 WF 111', 'S', None),
 'L18-L20': ('14 WF 193', 'S', None),
 'L0-U1': ('14 WF 287', 'S', None), 'U1-L2': ('14 WF 150', 'S', None), 'L2-U3': ('14 WF 150', 'S', None),
 'U3-L4': ('14 WF 142', 'C', None), 'L4-U5': ('14 WF 142', 'C', None), 'U5-L6': ('14 WF 158', 'S', None),
 'L6-U7': ('14 WF 158', 'S', None), 'U7-L8': ('14 WF 314', 'S', None), 'L8-U9': ('14 WF 264', 'S', None),
 'U9-L10': ('14 WF 342', 'S', '16 1/2 x 7/8'), 'L10-U11': ('14 WF 314', 'S', '16 x 11/16'), 'U11-L12': ('14 WF 211', 'S', None),
 'L12-U13': ('14 WF 246', 'S', None), 'U13-L14': ('14 WF 119', 'S', None), 'L14-U15': ('14 WF 142', 'C', None),
 'U15-L16': ('14 WF 142', 'C', None), 'L16-U17': ('14 WF 87', 'S', None), 'U17-L18': ('14 WF 202', 'S', None),
 'L18-U19': ('14 WF 184', 'S', None), 'U19-L20': ('14 WF 342', 'S', None),
 'U0-L0': ('14 WF 84', 'S', None),
}
for k in (2, 4, 6, 10, 20): B['U%d-L%d' % (k, k)] = ('16 WF 96', 'S', None)
for k in (8, 12, 14, 16, 18): B['U%d-L%d' % (k, k)] = ('14 WF 84', 'S', None)
for k in range(1, 20, 2): B['U%d-L%d' % (k, k)] = ('14 WF 61', 'C', None)
from fractions import Fraction as F
def f(s):
    tot = 0
    for p in s.split(): tot += F(p)
    return float(tot)
d = json.load(open(SBM))
out = []
for bname, tab in (('PTC 3-span truss 11-13', A), ('PTC 4-span truss 14-17', B)):
    b = [x for x in d['Bridges'] if x['Name'] == bname][0]
    t = b['Truss']; secs = {s['Name']: s for s in t['Sections']}
    mem = {}
    for m in t['Members']: mem.setdefault(m['Name'], m)
    for name, row in tab.items():
        des, mat = row[0], row[1]; cov = row[2] if len(row) > 2 else None
        m = mem.get(name)
        if m is None:
            out.append((bname, name, 'member missing', '', 'fail')); continue
        s = secs[m['Section']]
        probs = []
        mdes = (s.get('Designation') or '').strip()
        if mdes != des: probs.append('designation %s' % mdes)
        if s.get('Material') != mat: probs.append('material %s' % s.get('Material'))
        if not s.get('Rotated'): probs.append('not rotated (web in the truss plane)')
        tp = s.get('TopPlates') or []; bp = s.get('BottomPlates') or []
        mc = ['%.4gx%.4g%s%s' % (p['Width'] / IN, p['Thickness'] / IN, (' L=%.1f' % (p['Length'] / IN)) if p.get('Length') else '', ' btwG' if p.get('BetweenGussets') else '') for p in tp + bp]
        if cov:
            w, tt = [f(x.strip()) for x in cov.split('x')]
            if len(tp) != 1 or len(bp) != 1 or any(abs(p['Width'] / IN - w) > 0.01 or abs(p['Thickness'] / IN - tt) > 0.01 for p in tp + bp):
                probs.append('covers %s' % mc)
        elif tp or bp:
            probs.append('covers not on the plan %s' % mc)
        out.append((bname, name, '%s %s%s' % (des, mat, (' + 2-Pls ' + cov) if cov else ''), '%s %s %s' % (mdes, s.get('Material'), ' covers ' + ','.join(mc) if mc else ''), '; '.join(probs), 'fail' if probs else 'pass'))
for o in out: print(' | '.join(map(str, o)))
json.dump(out, open(sys.argv[1] if len(sys.argv) > 1 else 'appr_cmp.json', 'w'), indent=0)
