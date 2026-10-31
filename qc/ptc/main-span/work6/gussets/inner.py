import csv,re,sys,collections
rx=re.compile(r'([^\s|\[]+)\[(-?[\d.]+)\.\.(-?[\d.]+)\]')
f=sys.argv[1]; js=sys.argv[2].split(',')
res=collections.defaultdict(collections.Counter); ex={}
for r in csv.DictReader(open(f)):
    if r['plane']!='N' or r['point'] not in js or r['side']!='1': continue
    iv=sorted([(m.group(1),float(m.group(2)),float(m.group(3))) for m in rx.finditer(r['stack'])],key=lambda x:x[1])
    g=[x for x in iv if '-G' in x[0]]
    if not g: continue
    s0=min(x[1] for x in g)
    under=[x for x in iv if x[2]<=s0+0.03]
    # find gaps between consecutive under pieces within 4 in. of gusset
    under=[x for x in under if x[2]>s0-4]
    hi=-1e9
    for a,b in zip(under,under[1:]):
        pass
    # merge
    segs=[]
    for x in under:
        if segs and x[1]<=segs[-1][1]+0.03: segs[-1][1]=max(segs[-1][1],x[2]); segs[-1][2].append(x[0])
        else: segs.append([x[1],x[2],[x[0]]])
    if len(segs)>=2:
        a,b=segs[-2],segs[-1]
        gap=b[0]-a[1]
        if gap>1/32:
            k=f"{re.sub(r'\d+$','',a[2][-1])}->{re.sub(r'\d+$','',b[2][0])} {gap:.3f}"
            res[r['point']][k]+=1; ex.setdefault((r['point'],k),(r['a_in'],r['b_in']))
for j in js: print(j,res[j].most_common(6),[ (k,v) for (jj,k),v in ex.items() if jj==j][:3])
