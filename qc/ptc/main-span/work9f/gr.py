"""gusset outline (tris) + rivets in the truss plane view (u along the alignment ft->in, z up) for one gusset mark: python -I gr.py out tris.csv riv.csv MARK png"""
import csv, sys, math
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection
out, tf, rf, mark, png = sys.argv[1:6]
fr = next(csv.DictReader(open(f'{out}/frame.csv')))
nx, ny = float(fr['nx']), float(fr['ny']); tx, ty = -ny, nx
T = [r for r in csv.DictReader(open(tf)) if r['mark'] == mark]
P = [(float(r[k + 'x']), float(r[k + 'y']), float(r[k + 'z'])) for r in T for k in 'abc']
cx = sum(p[0] for p in P) / len(P); cy = sum(p[1] for p in P) / len(P); cz = sum(p[2] for p in P) / len(P)
nmin = min((p[0] - cx) * nx + (p[1] - cy) * ny for p in P); nmax = max((p[0] - cx) * nx + (p[1] - cy) * ny for p in P)
def L(x, y, z): return (((x - cx) * tx + (y - cy) * ty) / 0.0254, (z - cz) / 0.0254, ((x - cx) * nx + (y - cy) * ny))
fig, ax = plt.subplots(figsize=(11, 11))
ax.add_collection(PolyCollection([[L(float(r[k + 'x']), float(r[k + 'y']), float(r[k + 'z']))[:2] for k in 'abc'] for r in T], facecolor=(0.9, 0.5, 0.5), edgecolor='none', alpha=0.5))
n = 0
for r in csv.DictReader(open(rf)):
    u, z, nn = L(float(r['x']), float(r['y']), float(r['z']))
    dn = abs(float(r['dx']) * nx + float(r['dy']) * ny)
    if dn < 0.9 or abs(nn) > 1.0: continue
    ax.add_patch(plt.Circle((u, z), float(r['dia']) / 0.0254 / 2, fill=False, color='k', lw=0.6)); n += 1
us = [L(*p)[0] for p in P]; zs = [L(*p)[1] for p in P]
ax.set_xlim(min(us) - 15, max(us) + 15); ax.set_ylim(min(zs) - 15, max(zs) + 15); ax.set_aspect('equal'); ax.grid(True, lw=0.3)
ax.set_title(f'{mark}: gusset (red) and {n} rivets normal to it (u along the alignment in., z up in.)')
fig.savefig(png, dpi=80); print(png, n)
