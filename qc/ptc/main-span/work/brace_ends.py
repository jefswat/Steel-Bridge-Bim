import csv,collections,re,numpy as np
M=list(csv.DictReader(open('members.csv')))
P=list(csv.DictReader(open('parts.csv')))
R=np.loadtxt('rivets.csv',delimiter=',',skiprows=1,usecols=(1,2,3,4,5,6,7,8))
conn=[p for p in P if p['category'] in ('TrussConnectionPlate','TrussConnectionAngle')]
B=np.array([[float(p[k]) for k in ('minu','minn','minz','maxu','maxn','maxz')] for p in conn])
roles={'UpperLateral','LowerLateral','Strut','Sway','Portal'}
pos=R[:,0:3]; ax=R[:,3:6]
out=[]
for m in M:
    if m['role'] not in roles: continue
    A=np.array([float(m['au']),float(m['an']),float(m['az'])]); Bp=np.array([float(m['bu']),float(m['bn']),float(m['bz'])])
    L=np.linalg.norm(Bp-A); d=(Bp-A)/L
    for end,P0,dd,pn in (('A',A,d,m['from']),('B',Bp,-d,m['to'])):
        tol=0.12
        first=None; hit=set()
        for t in np.arange(0.25,min(10,L/2),0.25):
            q=P0+dd*t
            ins=np.where((B[:,0]-tol<=q[0])&(q[0]<=B[:,3]+tol)&(B[:,1]-tol<=q[1])&(q[1]<=B[:,4]+tol)&(B[:,2]-1.2<=q[2])&(q[2]<=B[:,5]+1.2))[0]
            if len(ins):
                if first is None: first=t
                hit.update(conn[i]['mark'] for i in ins)
        # rivets near axis, not across-bridge axis
        rel=pos-P0; t=rel@dd; perp=np.linalg.norm(rel-np.outer(t,dd),axis=1)
        sel=(t>0)&(t<min(10,L/2))&(perp<1.7)&(np.abs(ax[:,1])<0.5)
        out.append((m['name'],m['role'],m['section'],end,pn,first,len(hit),int(sel.sum()),';'.join(sorted(hit))[:150]))
w=csv.writer(open('../brace_ends.csv','w',newline=''))
w.writerow(['member','role','section','end','point','first_plate_ft','plates','rivets_near','plate_marks'])
w.writerows(out)
c=collections.Counter((o[1],o[6]>0, o[7]>0) for o in out)
for k,v in sorted(c.items()): print(k,v)
