import csv, collections, sys
def load(p): return list(csv.DictReader(open(p)))
a, b = sys.argv[1], sys.argv[2]
A, B = load(a), load(b)
ca = collections.Counter(r['category_pair'] for r in A); cb = collections.Counter(r['category_pair'] for r in B)
for k in sorted(set(ca) | set(cb)):
    if ca[k] != cb[k]: print(f'{ca[k]:5d} -> {cb[k]:5d}  {k}')
print(len(A), '->', len(B))
ka = {(r['mark_a'], r['mark_b']) for r in A}
new = [r for r in B if (r['mark_a'], r['mark_b']) not in ka]
print('new pairs', len(new))
for r in new[:40]: print('  NEW', r['category_pair'], r['mark_a'], r['mark_b'], r['depth_in'])
show = sys.argv[3].split(',') if len(sys.argv) > 3 else []
for r in B:
    if r['category_pair'] in show: print('  LEFT', r['category_pair'], r['mark_a'], r['mark_b'], r['depth_in'])
