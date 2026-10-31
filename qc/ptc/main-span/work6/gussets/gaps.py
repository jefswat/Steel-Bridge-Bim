import csv,re,sys,collections
f=sys.argv[1]; joints=sys.argv[2].split(',')
rx=re.compile(r'([^\s|\[]+)\[(-?[\d.]+)\.\.(-?[\d.]+)\]')
res=collections.defaultdict(collections.Counter); ex={}
for r in csv.DictReader(open(f)):
    if r['plane']!='N' or r['point'] not in joints or r['side']!='1': continue
    iv=[(m.group(1),float(m.group(2)),float(m.group(3))) for m in rx.finditer(r['stack'])]
    g=[x for x in iv if re.search(r'-G'+re.escape(r['point'])+r'-R\d*$',x[0])]
    if not g: continue
    s0=min(x[1] for x in g)
    under=[x for x in iv if x not in g and x[2]<=s0+0.03 and x[2]>s0-2]
    if not under: res[r['point']]['none']+=1; continue
    u=max(under,key=lambda x:x[2]); gap=s0-u[2]
    if gap>1/32:
        k=re.sub(r'\d+$','#',u[0]); k=f"{k} gap{gap:.3f}"
        res[r['point']][k]+=1; ex.setdefault((r['point'],k),(r['a_in'],r['b_in'],r['stack'][:300]))
for j in joints:
    print(j, res[j].most_common(8))
    for (jj,k),e in ex.items():
        if jj==j: print('   ',k,e)
