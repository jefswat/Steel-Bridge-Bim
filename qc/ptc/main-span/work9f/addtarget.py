"""append a target named <name> at the top / bottom end of the tris of parts matching <regex> (top = max z end) to targets.csv"""
import csv, re, sys
out, name, pat, end = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
pts = []
for r in csv.DictReader(open(f'{out}/tris.csv')):
    if re.search(pat, r['mark']):
        for k in 'abc': pts.append((float(r[k + 'x']), float(r[k + 'y']), float(r[k + 'z'])))
z = max(p[2] for p in pts) if end == 'top' else min(p[2] for p in pts)
sel = [p for p in pts if abs(p[2] - z) < 0.6]
x = sum(p[0] for p in sel) / len(sel); y = sum(p[1] for p in sel) / len(sel)
with open(f'{out}/targets.csv', 'a') as f: f.write(f'"{name}",{x:.4f},{y:.4f},{z:.4f},{x:.4f},{y:.4f},{z:.4f}\n')
print(name, x, y, z)
