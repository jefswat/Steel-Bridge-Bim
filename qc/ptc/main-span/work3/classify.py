import csv,collections,re,sys
IN=1/16
rows=list(csv.DictReader(open('rivet-all.csv')))
cls=collections.Counter(); bytj=collections.defaultdict(collections.Counter); amt=collections.defaultdict(float)
for r in rows:
    g=float(r['grip_in'])
    iv=[(m.group(1),float(m.group(2)),float(m.group(3))) for m in re.finditer(r'(\S+?)\[(-?[0-9.]+)\.\.(-?[0-9.]+)\]',r['stack'])]
    iv=[(p,max(a,0),min(b,g)) for p,a,b in iv if b>0 and a<g]
    iv.sort(key=lambda x:x[1])
    # merge
    gus=[x for x in iv if '-G' in x[0]]
    mem=[x for x in iv if '-G' not in x[0]]
    pos=0; kinds=[]
    if not gus: kinds.append('nogusset')
    cur=0.0
    for p,a,b in iv:
        if a-cur>IN:
            # gap before p
            prev_g = cur<= (max([x[2] for x in gus]) if gus else 0)+1e-3
            kinds.append(('gus-mem' if '-G' not in p and prev_g else 'in-member', a-cur))
        cur=max(cur,b)
    if g-cur>IN: kinds.append(('beyond', g-cur))
    for k in kinds:
        if isinstance(k,tuple):
            cls[k[0]]+=1; bytj[(r['point'])][k[0]]+=1; amt[k[0]]=max(amt[k[0]],k[1])
        else: cls[k]+=1
print(len(rows),cls,dict(amt))
for j in sys.argv[1:]:
    print(j, bytj[j])
