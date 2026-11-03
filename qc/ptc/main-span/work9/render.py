"""Painter's renderer of the dumped triangles (agent review). Views in the bridge frame: u = along the alignment (ft from the target),
n = right of the alignment (ft), z = up (ft).
  python -I render.py out <target name> <view> <width ft> <png> [highlight regex] [hide regex] [only regex]
  view: S (from the south = right side looking left... i.e. from +n), N (from -n), PLAN (from above), BELOW, E (from +u), W (from -u),
        or 'iso:az,el' (azimuth deg from +u toward +n, elevation deg)."""
import csv, sys, math, re
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection

out, tname, view, W, png = sys.argv[1], sys.argv[2], sys.argv[3], float(sys.argv[4]), sys.argv[5]
hl = re.compile(sys.argv[6]) if len(sys.argv) > 6 and sys.argv[6] else None
hide = re.compile(sys.argv[7]) if len(sys.argv) > 7 and sys.argv[7] else None
only = re.compile(sys.argv[8]) if len(sys.argv) > 8 and sys.argv[8] else None
fr = next(csv.DictReader(open(f'{out}/frame.csv')))
x0, y0, nx, ny = float(fr['x0']), float(fr['y0']), float(fr['nx']), float(fr['ny'])
tx, ty = -ny, nx  # tangent = left of right normal rotated: right normal (nx, ny) -> tangent (ny, -nx)? fix below
tx, ty = ny * -1, nx  # placeholder
# alignment tangent: right normal = tangent rotated clockwise -> tangent = (-ny, nx)
tx, ty = -ny, nx
T = [t for t in csv.DictReader(open(f'{out}/targets.csv')) if t['name'] == tname]
if not T:
    T = [t for t in csv.DictReader(open(f'{out}/targets.csv')) if re.search(tname, t['name'])]
cx = sum((float(t['x0']) + float(t['x1'])) / 2 for t in T) / len(T)
cy = sum((float(t['y0']) + float(t['y1'])) / 2 for t in T) / len(T)
cz = sum((float(t['z0']) + float(t['z1'])) / 2 for t in T) / len(T)
F = 0.3048
def L(x, y, z):
    dx, dy = x - cx, y - cy
    return ((dx * tx + dy * ty) / F, (dx * nx + dy * ny) / F, (z - cz) / F)
if view.startswith('iso:'):
    az, el = [math.radians(float(v)) for v in view[4:].split(',')]
    d = (math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el))  # toward the viewer
else:
    d = {'S': (0, 1, 0), 'N': (0, -1, 0), 'PLAN': (0, 0, 1), 'BELOW': (0, 0, -1), 'E': (1, 0, 0), 'W': (-1, 0, 0)}[view]
up = (0, 0, 1) if abs(d[2]) < 0.99 else (0, -1, 0) if d[2] > 0 else (0, 1, 0)
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def norm(a):
    l = math.sqrt(dot(a, a)); return (a[0] / l, a[1] / l, a[2] / l)
xr = norm(cross(up, d)); yr = cross(d, xr)
col = {'TrussGusset': (0.85, 0.35, 0.35), 'TrussConnectionPlate': (0.75, 0.45, 0.85), 'TrussConnectionAngle': (0.5, 0.3, 0.7), 'TrussChord': (0.55, 0.55, 0.8),
       'TrussDiagonal': (0.5, 0.72, 0.5), 'TrussVertical': (0.5, 0.72, 0.72), 'TrussHanger': (0.5, 0.72, 0.72), 'TrussBracing': (0.75, 0.75, 0.6),
       'TrussFloorBeam': (0.65, 0.65, 0.65), 'TrussStringer': (0.7, 0.6, 0.5), 'SplicePlate': (0.85, 0.65, 0.25), 'FillerPlate': (0.8, 0.8, 0.3),
       'Grating': (0.3, 0.6, 0.9), 'WalkwayPost': (0.2, 0.5, 0.8), 'WalkwayRail': (0.1, 0.4, 0.7), 'WalkwayStringer': (0.2, 0.3, 0.6), 'WalkwayPlate': (0.3, 0.7, 0.9)}
polys, cols, depth = [], [], []
light = norm((0.3, 0.5, 0.8))
for r in csv.DictReader(open(f'{out}/tris.csv')):
    m = r['mark']
    if hide and hide.search(m): continue
    if only and not only.search(m): continue
    P = [L(float(r[k + 'x']), float(r[k + 'y']), float(r[k + 'z'])) for k in 'abc']
    S = [(dot(p, xr), dot(p, yr), dot(p, d)) for p in P]
    if all(abs(s[0]) > W / 2 for s in S) or all(abs(s[1]) > W / 2 for s in S): continue
    e1 = [P[1][i] - P[0][i] for i in range(3)]; e2 = [P[2][i] - P[0][i] for i in range(3)]
    nn = cross(e1, e2)
    if dot(nn, nn) < 1e-18: continue
    nn = norm(nn)
    sh = 0.45 + 0.55 * abs(dot(nn, light))
    c = col.get(r['cat'], (0.6, 0.6, 0.6))
    if hl and hl.search(m): c = (1.0, 0.15, 0.1)
    polys.append([(s[0] * 12, s[1] * 12) for s in S]); cols.append((c[0] * sh, c[1] * sh, c[2] * sh)); depth.append(sum(s[2] for s in S) / 3)
order = sorted(range(len(polys)), key=lambda i: depth[i])
fig, ax = plt.subplots(figsize=(12, 12))
ax.add_collection(PolyCollection([polys[i] for i in order], facecolors=[cols[i] for i in order], edgecolors=[tuple(v * 0.6 for v in cols[i]) for i in order], linewidths=0.15))
h = W * 6
ax.set_xlim(-h, h); ax.set_ylim(-h, h); ax.set_aspect('equal'); ax.grid(True, lw=0.25)
ax.set_title(f'{tname} view {view} (inches from the target centre)')
fig.savefig(png, dpi=90); plt.close(fig); print(png, len(polys))
