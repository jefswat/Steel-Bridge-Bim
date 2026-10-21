"""rivet_map.py rivets.csv [cell_in]  - ASCII map (u right, v up) of the rivets on the +n face of a truss joint (one char per cell; digit = count)."""
import csv, sys
rows = list(csv.DictReader(open(sys.argv[1])))
cell = float(sys.argv[2]) if len(sys.argv) > 2 else 2.0
# one face: the rivet heads on the outer face (largest n) - keep n > 0 side, shanks across the plane
face = [r for r in rows if float(r['n_in']) > 0 and abs(float(r['shank_n'])) > 0.9]
us = [float(r['u_in']) for r in face]; vs = [float(r['v_in']) for r in face]
if not face: sys.exit('no rivets')
u0, u1, v0, v1 = min(us), max(us), min(vs), max(vs)
nu, nv = int((u1 - u0) / cell) + 1, int((v1 - v0) / cell) + 1
g = [[0] * nu for _ in range(nv)]
for u, v in zip(us, vs):
    g[int((v - v0) / cell)][int((u - u0) / cell)] += 1
print(f'{len(face)} rivets on the +n face, u {u0:.1f}..{u1:.1f} in, v {v0:.1f}..{v1:.1f} in, cell {cell} in (u = 0 at the point: column {int(-u0 / cell)})')
for j in range(nv - 1, -1, -1):
    print(f'{v0 + j * cell:7.1f} ' + ''.join('.' if c == 0 else (str(c) if c < 10 else '#') for c in g[j]))
