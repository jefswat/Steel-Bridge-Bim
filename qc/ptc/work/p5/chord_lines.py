"""chord_lines.py rivets.csv vmax [split_u] - chord rivet lines on the +n face of a truss joint: per line the count left / right of split_u (in.)
and the pitch. The chord slope (grade) is fitted so that the lines are straight. Read-only (input from rivets_multi.csx)."""
import csv, sys, collections
rows = list(csv.DictReader(open(sys.argv[1])))
vmax = float(sys.argv[2]); su = float(sys.argv[3]) if len(sys.argv) > 3 else 0.0
face = [(float(r['u_in']), float(r['v_in']), r['grip_in']) for r in rows if float(r['n_in']) > 0 and abs(float(r['shank_n'])) > 0.9 and abs(float(r['v_in'])) < vmax + 2]
best = None
for k in range(-60, 61):
    s = k / 1000
    keys = collections.Counter(round((v - s * u) / 0.75) for u, v, g in face if abs(v - s * u) < vmax)
    sc = sum(1 for c in keys.values() if c >= 3)
    big = sum(c for c in keys.values() if c >= 3)
    cand = (-big, sc, abs(s))
    if best is None or cand < best[0]: best = (cand, s)
s = best[1]
lines = collections.defaultdict(list)
for u, v, g in face:
    vv = v - s * u
    if abs(vv) < vmax: lines[round(vv / 0.75) * 0.75].append((u, g))
tot_l = tot_r = 0
print(f'fitted slope {s:+.3f}')
for v in sorted(lines, reverse=True):
    us = sorted(x[0] for x in lines[v]); l = [u for u in us if u < su]; r = [u for u in us if u >= su]
    if len(us) < 3: continue
    tot_l += len(l); tot_r += len(r)
    gaps = [b - a for a, b in zip(us, us[1:])]
    pitch = sorted(gaps)[len(gaps) // 2] if gaps else 0
    print(f'v {v:6.2f}: {len(l):3d} left / {len(r):3d} right of u={su}; u {us[0]:7.1f}..{us[-1]:7.1f}; median pitch {pitch:5.2f}; grips {sorted(set(x[1] for x in lines[v]))[:4]}')
print(f'total {tot_l} left / {tot_r} right (lines with >= 3 rivets)')
