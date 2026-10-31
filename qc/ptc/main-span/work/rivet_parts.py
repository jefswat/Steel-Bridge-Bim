import csv,collections,re,json
R=list(csv.DictReader(open('rivets.csv')))
P={p['mark']:p for p in csv.DictReader(open('parts.csv'))}
hits=collections.defaultdict(list)
for r in csv.DictReader(open('clash.csv')):
    a=r['kindA']=='Bolt'; b=r['kindB']=='Bolt'
    if a and not b: hits[int(r['markA'].rsplit('-B',1)[1])-1].append((r['kindB'],r['markB'],float(r['depth_in'])))
    elif b and not a: hits[int(r['markB'].rsplit('-B',1)[1])-1].append((r['kindA'],r['markA'],float(r['depth_in'])))
cnt=collections.Counter()
out=[]
for i,r in enumerate(R):
    h=hits.get(i,[])
    n=len(h); cats=sorted(set(x[0] for x in h))
    cls='air' if n==0 else ('one' if n==1 else 'ok')
    # reason short: extract joint/member info from reason text
    cnt[cls]+=1
    if cls!='ok': out.append((i,r['u'],r['n'],r['z'],r['au'],r['an'],r['az'],r['dia_in'],r['grip_in'],cls,'|'.join(f"{k}:{m}" for k,m,d in h),r['reason'][-160:]))
print(cnt)
w=csv.writer(open('../rivets_suspect.csv','w',newline='')); w.writerow(['i','u','n','z','au','an','az','dia','grip','class','parts','reason_tail']); w.writerows(out)
# single-part classes
c=collections.Counter(x[10].split(':')[0] for x in out if x[9]=='one'); print('one:',c.most_common())
# by reason source (text group)
c=collections.Counter(re.sub(r'\d+','#',x[11][-90:]) for x in out if x[9]=='air'); [print(v,k) for k,v in c.most_common(15)]
