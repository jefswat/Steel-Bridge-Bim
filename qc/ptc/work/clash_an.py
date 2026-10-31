import csv, glob, os, collections, sys
def load(d):
    rows=[]
    for f in glob.glob(d+'/*.csv'):
        if os.path.basename(f).startswith('c3a'): continue
        rows+=list(csv.DictReader(open(f,encoding='utf-8')))
    return rows
if __name__=='__main__':
  for d in ['clash','clashb']:
    rows=load(d); print('=====',d,len(rows))
    g=collections.defaultdict(list)
    for r in rows: g[r['category_pair']].append(float(r['depth_in']))
    for k,v in sorted(g.items(),key=lambda x:-len(x[1]))[:45]:
        v.sort(); print(f'{len(v):7d} {k:50s} max {v[-1]:.3f} med {v[len(v)//2]:.3f}')
    b=collections.Counter(r['bridge'] for r in rows); print(b)
