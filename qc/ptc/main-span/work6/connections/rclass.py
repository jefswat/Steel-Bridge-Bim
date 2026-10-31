import csv,collections,re,sys
hits=collections.defaultdict(set)
for r in csv.DictReader(open('clash.csv',encoding='utf-8')):
    a=r['kindA']=='Bolt'; b=r['kindB']=='Bolt'
    if a and not b: hits[int(r['markA'].rsplit('-B',1)[1])-1].add(r['markB'])
    elif b and not a: hits[int(r['markB'].rsplit('-B',1)[1])-1].add(r['markA'])
R=list(csv.DictReader(open('rivets.csv',encoding='utf-8')))
P={p['mark']:p for p in csv.DictReader(open('parts.csv',encoding='utf-8'))}
pm={p['mark']:(p['member'] or p['mark']) for p in csv.DictReader(open('partmember.csv',encoding='utf-8'))}
c=collections.Counter(); one=collections.Counter(); grip=collections.Counter(); air=collections.Counter()
out=[]
for i,r in enumerate(R):
    if r['status']!='Rivet': continue
    h=hits.get(i,set()); mem=set(pm.get(x,x) for x in h)
    cls='air' if not h else ('one' if len(mem)==1 else 'ok')
    c[cls]+=1
    if cls=='one':
        x=next(iter(h)); k=P[x]['category'] if x in P else '?'; one[(r['assembly'],k)]+=1; grip[(k,r['grip_in'][:5])]+=1
        out.append((i,r['u'],r['n'],r['z'],r['grip_in'],r['dia_in'],'|'.join(sorted(mem))))
    if cls=='air': air[r['assembly']]+=1; out.append((i,r['u'],r['n'],r['z'],r['grip_in'],r['dia_in'],'AIR'))
print(c); print('one by asm/cat',one.most_common(20)); print('air',air.most_common()); print(grip.most_common(10))
csv.writer(open('../rivets_suspect.csv','w',newline='')).writerows([('i','u','n','z','grip','dia','parts')]+out)
