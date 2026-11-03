"""Agent review 2026-11-02: four views per main truss joint from the dump of dump_joints.csx.
  python -I render_joints.py <out dir> <png dir> [joint regex]
Views (inches from the joint working point; u = along the bridge, + = up-station / increasing station):
  A outside elevation (viewer outside the truss), B inside elevation (viewer at the bridge CL; drawn with u to the right, i.e. MIRRORED),
  C plan from above (north up), D plan from below (north up, MIRRORED like a reflected ceiling plan is not used: same axes as C).
Colours by part category, gussets red, connection plates purple, splice / fill plates orange / yellow; rivets = black circles (shank along the view)."""
import csv, sys, os, re, math, collections
from multiprocessing import Pool
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection

OUT, PNG = sys.argv[1], sys.argv[2]
PAT = re.compile(sys.argv[3]) if len(sys.argv) > 3 else None
os.makedirs(PNG, exist_ok=True)
F = 0.0254
COL = {'TrussGusset': (0.88, 0.30, 0.30), 'TrussConnectionPlate': (0.70, 0.40, 0.88), 'TrussConnectionAngle': (0.45, 0.25, 0.70),
       'SplicePlate': (0.95, 0.60, 0.15), 'FillerPlate': (0.92, 0.86, 0.25), 'TrussChord': (0.50, 0.55, 0.85), 'TrussDiagonal': (0.45, 0.72, 0.45),
       'TrussVertical': (0.40, 0.72, 0.75), 'TrussHanger': (0.40, 0.72, 0.75), 'TrussBracing': (0.78, 0.76, 0.55), 'TrussFloorBeam': (0.62, 0.62, 0.62),
       'TrussStringer': (0.70, 0.58, 0.45), 'TrussShoeFixed': (0.55, 0.35, 0.25), 'TrussShoeExpansion': (0.55, 0.35, 0.25), 'TrussPin': (0.3, 0.3, 0.3),
       'Suspender': (0.2, 0.2, 0.2), 'SuspenderSocket': (0.35, 0.35, 0.35)}
LABEL = {'TrussGusset', 'TrussConnectionPlate', 'SplicePlate', 'FillerPlate', 'TrussConnectionAngle'}
WALK = re.compile(r'(?i)walk|ladder|platform|grating|rail|cage|toe plate|catwalk|post')
light = (0.3, 0.5, 0.8)

def norm(a):
    l = math.sqrt(a[0] ** 2 + a[1] ** 2 + a[2] ** 2); return (a[0] / l, a[1] / l, a[2] / l)
light = norm(light)

def render(j):
    tag = f"{j['plane']}_{j['name'].replace(chr(39), 'p')}"
    P = (float(j['x']), float(j['y']), float(j['z']))
    t = (float(j['tx']), float(j['ty'])); nn = (float(j['nx']), float(j['ny']))
    s = 1 if j['plane'] != 'N' else -1          # outside = +n for S, -n for N
    tris = collections.defaultdict(list)
    for r in csv.DictReader(open(f'{OUT}/tris_{tag}.csv')):
        if WALK.search(r['mark']): continue
        L = []
        for k in 'abc':
            dx, dy, dz = float(r[k + 'x']) - P[0], float(r[k + 'y']) - P[1], float(r[k + 'z']) - P[2]
            L.append(((dx * t[0] + dy * t[1]) / F, (dx * nn[0] + dy * nn[1]) / F, dz / F))
        tris[(r['mark'], r['cat'])].append(L)
    riv = []
    for r in csv.DictReader(open(f'{OUT}/rivets_{tag}.csv')):
        dx, dy, dz = float(r['x']) - P[0], float(r['y']) - P[1], float(r['z']) - P[2]
        a = (float(r['ax']), float(r['ay']), float(r['az']))
        riv.append(((dx * t[0] + dy * t[1]) / F, (dx * nn[0] + dy * nn[1]) / F, dz / F,
                    (a[0] * t[0] + a[1] * t[1], a[0] * nn[0] + a[1] * nn[1], a[2]), float(r['dia']) / F))
    ext = [max(abs(q[0]), abs(q[2])) for (m, c), T in tris.items() if c in ('TrussGusset', 'TrussConnectionPlate') for tri in T for q in tri
           if abs(q[0]) < 140 and abs(q[2]) < 140 and abs(q[1]) < 140]
    W = float(os.environ.get('SBM_W', '0')) or min(140.0, max(72.0, (max(ext) if ext else 60) + 10))
    # view: (title, screen x fn, screen y fn, depth fn (toward viewer +), view axis (u,n,z), band filter)
    views = [
        (f"A  outside elevation (from {'S' if s > 0 else 'N'}, outside)", lambda p: p[0], lambda p: p[2], lambda p: s * p[1], (0, s, 0), lambda p: abs(p[1]) < 60),
        ("B  inside elevation (from the bridge CL; u to the right = MIRRORED)", lambda p: p[0], lambda p: p[2], lambda p: -s * p[1], (0, -s, 0), lambda p: abs(p[1]) < 60),
        ("C  plan from above (north up)", lambda p: p[0], lambda p: -p[1], lambda p: p[2], (0, 0, 1), lambda p: abs(p[2]) < 60),
        ("D  plan from below (north up)", lambda p: p[0], lambda p: -p[1], lambda p: -p[2], (0, 0, -1), lambda p: abs(p[2]) < 60),
    ]
    fig, axs = plt.subplots(2, 2, figsize=(17, 17))
    for ax, (title, fx, fy, fd, vd, band) in zip(axs.flat, views):
        polys, cols, depth = [], [], []
        centroid = collections.defaultdict(lambda: [0.0, 0.0, 0.0, -1e9])
        for (mark, cat), T in tris.items():
            c = COL.get(cat, (0.6, 0.6, 0.6))
            for tri in T:
                if not any(band(p) for p in tri): continue
                X = [fx(p) for p in tri]; Y = [fy(p) for p in tri]
                if max(X) < -W or min(X) > W or max(Y) < -W or min(Y) > W: continue
                e1 = [tri[1][i] - tri[0][i] for i in range(3)]; e2 = [tri[2][i] - tri[0][i] for i in range(3)]
                cr = (e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], e1[0] * e2[1] - e1[1] * e2[0])
                ln = math.sqrt(sum(v * v for v in cr))
                if ln < 1e-9: continue
                cr = (cr[0] / ln, cr[1] / ln, cr[2] / ln)
                sh = 0.45 + 0.55 * abs(sum(cr[i] * light[i] for i in range(3)))
                d = sum(fd(p) for p in tri) / 3
                polys.append(list(zip(X, Y))); cols.append(tuple(v * sh for v in c)); depth.append(d)
                if cat in LABEL:
                    ar = abs((X[1] - X[0]) * (Y[2] - Y[0]) - (X[2] - X[0]) * (Y[1] - Y[0])) / 2
                    cc = centroid[mark]; cc[0] += ar * sum(X) / 3; cc[1] += ar * sum(Y) / 3; cc[2] += ar; cc[3] = max(cc[3], d)
        order = sorted(range(len(polys)), key=lambda i: depth[i])
        ax.add_collection(PolyCollection([polys[i] for i in order], facecolors=[cols[i] for i in order],
                                         edgecolors=[tuple(v * 0.55 for v in cols[i]) for i in order], linewidths=0.2))
        top = max(depth) if depth else 0
        # rivets whose shank runs along the view, in front of most steel (heads visible on the near face)
        for (u, n, z, a, dia) in riv:
            p = (u, n, z)
            if abs(a[0] * vd[0] + a[1] * vd[1] + a[2] * vd[2]) < 0.85 or not band(p): continue
            x, y = fx(p), fy(p)
            if abs(x) > W or abs(y) > W: continue
            ax.add_patch(plt.Circle((x, y), dia / 2, fill=False, color='k', lw=0.35))
        for mark, (sx, sy, ar, d) in centroid.items():
            if ar < 20: continue
            ax.text(sx / ar, sy / ar, mark, fontsize=6.5, ha='center', va='center', color='k',
                    bbox=dict(boxstyle='round,pad=0.1', fc='white', ec='none', alpha=0.6))
        ax.set_xlim(-W, W); ax.set_ylim(-W, W); ax.set_aspect('equal'); ax.grid(True, lw=0.25)
        ax.axhline(0, lw=0.4, color='k', ls=':'); ax.axvline(0, lw=0.4, color='k', ls=':')
        ax.set_title(title, fontsize=10)
    fig.suptitle(f"PTC main span BRACING at {j['name']} ({j['plane']})  (sta {int(float(j['station']) / 0.3048 // 100)}+{(float(j['station']) / 0.3048) % 100:05.2f} ft; inches from the W.P.; u = up-station ->)", fontsize=12)
    fig.tight_layout(rect=(0, 0, 1, 0.97))
    fig.savefig(f'{PNG}/{tag}.png', dpi=75); plt.close(fig)
    return tag

if __name__ == '__main__':
    J = [j for j in csv.DictReader(open(f'{OUT}/joints.csv')) if not PAT or PAT.search(f"{j['plane']}_{j['name']}")]
    with Pool(12) as pool:
        for tag in pool.imap_unordered(render, J): print(tag, flush=True)
