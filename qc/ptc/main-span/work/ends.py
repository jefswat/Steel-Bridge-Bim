import csv,collections,math
pts={r['name']:(float(r['u']),float(r['z'])) for r in csv.DictReader(open('out/points.csv')) if r['plane']=='N'}
V=collections.defaultdict(list)
for r in csv.DictReader(open('out/tris.csv')):
    if r['cat'] in ('TrussDiagonal','TrussVertical','TrussHanger','TrussChord'):
        for k in '123': V[r['mark']].append((float(r['u'+k]),float(r['z'+k])))
M=[m for m in csv.DictReader(open('out/members.csv')) if m['plane']=='N' and m['role'] in ('Diagonal','Vertical','Hanger')]
out=[]
for m in M:
    vs=V.get(m['mark']); 
    if not vs: continue
    for end,other in ((m['from'],m['to']),(m['to'],m['from'])):
        if end not in pts or other not in pts: continue
        a=pts[end]; b=pts[other]; L=math.dist(a,b); d=((b[0]-a[0])/L,(b[1]-a[1])/L)
        t=min((x-a[0])*d[0]+(y-a[1])*d[1] for x,y in vs if (x-a[0])*d[0]+(y-a[1])*d[1]<L/2)
        out.append((end,m['mark'],round(t*12,1)))
w=csv.writer(open('member_end_setbacks.csv','w',newline='')); w.writerow(['joint','member','end_from_wp_in']); w.writerows(out)
for o in out:
    if o[0] in ('L10','U5','L13','L14','L12','U9','U11'): print(o)
