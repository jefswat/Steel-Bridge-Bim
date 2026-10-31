import csv,collections,math,sys
D=sys.argv[1]
R=list(csv.DictReader(open(D+'/rivets.csv',encoding='utf-8')))
pts=[(r['plane'],r['name'],float(r['u']),float(r['n']),float(r['z'])) for r in csv.DictReader(open(D+'/points.csv'))]
c=collections.Counter(); deep=collections.Counter(); asm=collections.Counter()
for r in csv.DictReader(open(D+'/clash.csv',encoding='utf-8')):
    if r['kindA']!='Bolt' or r['kindB']!='Bolt': continue
    i=int(r['markA'].rsplit('-B',1)[1])-1; b=R[i]
    u,n,z=float(b['u']),float(b['n']),float(b['z'])
    p=min(pts,key=lambda p:(p[2]-u)**2+(p[3]-n)**2+(p[4]-z)**2)
    k=(p[0],p[1].replace("'",""),r['markA'].rsplit('-B',1)[0]+' x '+r['markB'].rsplit('-B',1)[0])
    c[k]+=1
    if float(r['depth_in'])>0.25: deep[k]+=1
tot=sum(c.values()); print('bolt-bolt rows',tot,'>1/4',sum(deep.values()))
for k,v in c.most_common(30): print(v,deep[k],k)
agg=collections.Counter(); ad=collections.Counter()
for k,v in c.items():
    a=tuple(sorted(x.split('-')[0] for x in k[2].split(' x ')))
    agg[a]+=v; ad[a]+=deep[k]
print({k:(v,ad[k]) for k,v in agg.items()})
