import csv, sys, collections
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection
pts = {r['name']: r for r in csv.DictReader(open('out/points.csv')) if r['plane'] == 'N'}
T = collections.defaultdict(list)
for r in csv.DictReader(open('out/tris.csv')):
    T[(r['mark'], r['cat'])].append([(float(r['u1']), float(r['z1'])), (float(r['u2']), float(r['z2'])), (float(r['u3']), float(r['z3']))])
R = []
for r in csv.DictReader(open('out/rivets.csv')):
    n = float(r['n'])
    if -46 < n < -41 and abs(float(r['an'])) > 2:
        R.append((float(r['u']), float(r['z']), float(r['dia_in']), float(r['grip_in'])))
col = {'TrussGusset': '#e06060', 'TrussChord': '#9090c0', 'TrussDiagonal': '#80b080', 'TrussVertical': '#80b0b0', 'TrussHanger': '#80b0b0', 'SplicePlate': '#d0a040',
       'FillerPlate': '#c0c040', 'TrussConnectionPlate': '#a060a0', 'TrussConnectionAngle': '#604080', 'TrussFloorBeam': '#999999', 'TrussBracing': '#bbbbbb'}
order = ['TrussFloorBeam', 'TrussBracing', 'TrussConnectionAngle', 'TrussConnectionPlate', 'TrussChord', 'TrussDiagonal', 'TrussVertical', 'TrussHanger', 'FillerPlate', 'SplicePlate', 'TrussGusset']
W = 7.5
for j in sys.argv[1:]:
    p = pts[j]; u0, z0 = float(p['u']), float(p['z'])
    fig, ax = plt.subplots(figsize=(11, 11))
    for cat in order:
        for (m, c), tris in T.items():
            if c != cat: continue
            if not any(abs(x - u0) < W and abs(y - z0) < W for t in tris for x, y in t): continue
            ax.add_collection(PolyCollection([[((x - u0) * 12, (y - z0) * 12) for x, y in t] for t in tris], facecolor=col[c], edgecolor='none', alpha=0.35 if c == 'TrussGusset' else 0.6))
    for u, z, d, g in R:
        if abs(u - u0) < W and abs(z - z0) < W:
            ax.add_patch(plt.Circle(((u - u0) * 12, (z - z0) * 12), d / 2, fill=False, color='k', lw=0.6))
    ax.set_xlim(-W * 12, W * 12); ax.set_ylim(-W * 12, W * 12); ax.set_aspect('equal'); ax.grid(True, lw=0.3)
    ax.set_title(f'model N plane {j} (inches from WP; u along bridge, z up)')
    fig.savefig(f'mv/{j}.png', dpi=80); plt.close(fig); print(j)
