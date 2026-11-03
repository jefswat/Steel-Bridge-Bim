import csv,sys,re,collections
out,tname,pat=sys.argv[1],sys.argv[2],sys.argv[3]
fr=next(csv.DictReader(open(f'{out}/frame.csv'))); nx,ny=float(fr['nx']),float(fr['ny']); tx,ty=-ny,nx
T=[t for t in csv.DictReader(open(f'{out}/targets.csv')) if t['name']==tname][0]
cx=(float(T['x0'])+float(T['x1']))/2; cy=(float(T['y0'])+float(T['y1']))/2; cz=(float(T['z0'])+float(T['z1']))/2
R=collections.defaultdict(lambda:[1e9,-1e9,1e9,-1e9,1e9,-1e9,''])
for r in csv.DictReader(open(f'{out}/tris.csv')):
    if not re.search(pat,r['mark']): continue
    a=R[r['mark']]; a[6]=r['cat']
    for k in 'abc':
        dx=float(r[k+'x'])-cx; dy=float(r[k+'y'])-cy; z=float(r[k+'z'])-cz
        u=(dx*tx+dy*ty)/0.0254; n=(dx*nx+dy*ny)/0.0254; z/=0.0254
        a[0]=min(a[0],u);a[1]=max(a[1],u);a[2]=min(a[2],n);a[3]=max(a[3],n);a[4]=min(a[4],z);a[5]=max(a[5],z)
for m,a in sorted(R.items(),key=lambda kv:kv[1][2]):
    print(f'{m:34s} {a[6]:22s} u {a[0]:8.2f} {a[1]:8.2f}  n {a[2]:7.3f} {a[3]:7.3f}  z {a[4]:7.2f} {a[5]:7.2f}')
