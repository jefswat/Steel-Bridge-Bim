"""grid_balance.py grid.csv [n] - per joint face: the chord-side rivet grids (inside the gusset) and their ratio; worst n by ratio and by min grid / d."""
import csv, re, sys, collections
rows = list(csv.DictReader(open(sys.argv[1])))
n = int(sys.argv[2]) if len(sys.argv) > 2 else 10
def chord(m):
    m = re.sub(r"^[NS]-", "", m)
    a = m.split('-')
    return len(a) == 2 and a[0][0] == a[1][0] and a[0][0] in 'LUW' and a[0][0] in 'LU'
j = collections.defaultdict(list)
for r in rows:
    if r['region'] == 'inside' and chord(r['member']):
        j[(r['bridge'], r['joint'], r['plane'])].append(r)
res = []
for k, g in j.items():
    if len(g) < 2: continue
    gr = [float(x['grid_in']) for x in g]
    res.append((max(gr) / min(gr), min(float(x['grid_over_dia']) for x in g), k, ' | '.join(f"{x['member']} {x['per_face']} @ {x['grid_in']}" for x in g)))
print(f'{len(res)} joint faces with two chord sides; ratio > 1.25: {sum(1 for r in res if r[0] > 1.25)}, > 1.5: {sum(1 for r in res if r[0] > 1.5)}; min grid < 3 d: {sum(1 for r in res if r[1] < 3)}')
print('worst by side ratio:')
for r in sorted(res, key=lambda r: -r[0])[:n]: print(f'  {r[0]:.2f}  min {r[1]:.2f} d  {r[2][0]} {r[2][1]} ({r[2][2]}): {r[3]}')
print('worst by min grid / d:')
for r in sorted(res, key=lambda r: r[1])[:n]: print(f'  {r[0]:.2f}  min {r[1]:.2f} d  {r[2][0]} {r[2][1]} ({r[2][2]}): {r[3]}')
