import sys,collections
from clash_an import load
rows=load('clash')
pairs=sys.argv[1:]
for p in pairs:
    rs=[r for r in rows if r['category_pair']==p]
    print('###',p,len(rs), collections.Counter(r['bridge'][4:] for r in rs).most_common())
    rs.sort(key=lambda r:-float(r['depth_in']))
    for r in rs[:3]+rs[len(rs)//2:len(rs)//2+1]:
        print('  ',r['depth_in'],r['station'],'|',r['mark_a'],r['desc_a'][:50],'|',r['mark_b'],r['desc_b'][:50],'|',r['location'][:30])
