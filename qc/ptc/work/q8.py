import csv,sys,collections,re
br=sys.argv[1]; cat=sys.argv[2]
R=[r for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\geom3.csv')) if r['bridge']==br and r['cat']==cat]
g=collections.defaultdict(list)
for r in R:
    k=re.sub(r'-P\d+$','',r['mark'])
    g[k].append(((float(r['smin'])+float(r['smax']))/2,(float(r['omin'])+float(r['omax']))/2,r['mark']))
for k,v in g.items():
    v.sort(); d=[v[i+1][0]-v[i][0] for i in range(len(v)-1)]
    cnt=collections.Counter(round(x,2) for x in d)
    print(k,len(v),'s %.2f..%.2f'%(v[0][0],v[-1][0]),'o %.2f'%v[0][1],'spacings',dict(cnt.most_common(6)))
