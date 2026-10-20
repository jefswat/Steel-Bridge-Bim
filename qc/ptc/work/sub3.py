exec(open('sub1.py').read().split("# footing + column")[0])
rows=[]
def base_of(name,cat='column'):
    out=[]
    for g,v in find(name,cat):
        P=v[1]; zb=P[:,2].min(); Q=P[P[:,2]<zb+0.02]; d=Q.max(0)-Q.min(0); out.append((v[0][1],d,P))
    return out
# Piers 28-30 p.37
for pn,top,fb,base,pn_f in ((28,73.230,17.0,ft(8,7,1,4),'Pier 28 footing'),(29,69.498,21.0,ft(8,3,3,4),'Pier 29 footing'),(30,65.765,24.5,ft(8,0),'Pier 30 footing')):
    sh=S_['37']
    f=one(pn_f,'footing'); d,mn,mx=dims(f)
    add(37,sh,'Pier details / typical footing',f'Pier {pn} footing bottom El.',M,fb,mn[2],0.01)
    add(37,sh,'Pier details',f'Pier {pn} footing top El.',M,fb+5,mx[2],0.01)
    add(37,sh,'Typical footing',f'Pier {pn} footing length (80 ft)',M,80.0,d[1],0.021)
    add(37,sh,'Typical footing',f'Pier {pn} footing width (22 ft)',M,22.0,d[0],0.021)
    cs=base_of(f'Pier {pn} shaft'); zt=max(c[2][:,2].max() for c in cs)
    add(37,sh,'Pier details',f'Pier {pn} shaft top El.',M,top,zt,0.01)
    add(37,sh,'Pier details',f'Pier {pn} shaft base size',M,base,cs[0][1][0],0.021)
# Piers 17 / 20
for pn,nm in ((17,'Pier 17 W brg'),(20,'Pier 20 main brg')):
    f=one(nm+' pile cap','footing'); d,mn,mx=dims(f)
    add(17,S_['17'],'Elevation',f'{nm} pile cap bottom El. (-7.0)',M,-7.0,mn[2],0.01)
    add(17,S_['17'],'Elevation',f'{nm} pile cap top El. (+5.0)',M,5.0,mx[2],0.01)
    add(17,S_['17'],'Plan',f'{nm} pile cap length (121 ft 1 1/2 in)',M,ft(121,1,1,2),d[1],0.021)
    add(17,S_['17'],'Plan',f'{nm} pile cap width (33 ft)',M,33.0,d[0],0.021)
    leg=base_of(f'{nm} leg W')[0]
    add(17,S_['17'],'Elevation / typical section',f'{nm} leg top El.',M,107.682,leg[2][:,2].max(),0.01)
    add(17,S_['17'],'Typical section',f'{nm} leg base along bridge (16 ft 3 in)',M,16.25,leg[1][0],0.025)
    add(17,S_['17'],'Elevation',f'{nm} leg base across (14 ft 1 1/2 in)',M,ft(14,1,1,2),leg[1][1],0.021)
    T=leg[2][leg[2][:,2]>leg[2][:,2].max()-0.02]; dt=T.max(0)-T.min(0)
    add(17,S_['17'],'Elevation / typical section',f'{nm} leg top size across (12 ft)',M,12.0,dt[1],0.021)
    add(17,S_['17'],'Typical section',f'{nm} leg top size along bridge (12 ft)',M,12.0,dt[0],0.021)
    cap=find(f'{nm} cap beam','pier cap')[0][1][1]
    zc=lambda y: cap[np.abs(cap[:,1]-y)<0.01][:,2]
    add(17,S_['17'],'Elevation',f'{nm} cap length between legs (75 ft)',M,75.0,cap[:,1].max()-cap[:,1].min(),0.021)
    add(17,S_['17'],'Elevation',f'{nm} cap depth at leg faces (15 ft)',M,15.0,zc(37.5).max()-zc(37.5).min(),0.021)
    add(17,S_['17'],'Elevation',f'{nm} cap depth at centre (10 ft)',M,10.0,cap[:,2].max()-zc(0.0).min(),0.021)
    add(17,S_['17'],'Elevation / plan',f'{nm} cap width along bridge (12 ft)',M,12.0,cap[:,0].max()-cap[:,0].min(),0.021)
# Piers 18 / 19
for pn,pre in ((18,'Pier 18'),(19,'Pier 19')):
    ts=one(f'{pre} tremie seal','footing'); d,mn,mx=dims(ts)
    add(17,S_['17'],'Elevation',f'{pre} tremie seal bottom El. (-25.0)',M,-25.0,mn[2],0.01)
    add(17,S_['17'],'Elevation',f'{pre} tremie seal top El. (-19.0)',M,-19.0,mx[2],0.01)
    add(17,S_['17'],'Plan',f'{pre} footing length (158 ft 10 in)',M,ft(158,10),d[1],0.021)
    add(17,S_['17'],'Plan',f'{pre} footing width (53 ft)',M,53.0,d[0],0.021)
    sl_=one(f'{pre} pile cap slab','footing'); d,mn,mx=dims(sl_)
    add(20,S_['20'],'Section B-B',f'{pre} base slab top El. (-15.0)',M,-15.0,mx[2],0.01)
    pb=find(f'{pre} pier body','footing')
    top=[x for x in pb if x[1][0][1].strip().endswith('pier body')][0][1][1]
    zt=top[:,2].max()
    add(20,S_['20'],'Section A-A / B-B',f'{pre} pier body top El. (+12.0)',M,12.0,zt,0.01)
    T=top[top[:,2]>zt-0.02]; dt=T.max(0)-T.min(0)
    add(20,S_['20'],'Section B-B',f'{pre} pier body top width along bridge (36 ft)',M,36.0,dt[0],0.021)
    low=[x for x in pb if 'lower' in x[1][0][1]][0][1][1]
    zb=low[:,2].min(); B=low[low[:,2]<zb+0.02]; db=B.max(0)-B.min(0)
    add(20,S_['20'],'Section B-B',f'{pre} pier body base width along bridge (40 ft 6 in)',M,40.5,db[0],0.021)
    sh=base_of(f'{pre} shaft W')[0]
    add(20,S_['20'],'Section / Elevation',f'{pre} shaft top El. (+87.701)',M,87.701,sh[2][:,2].max(),0.01)
    add(20,S_['20'],'Section B-B',f'{pre} shaft base along bridge (23 ft 3 in)',M,23.25,sh[1][0],0.04)
    add(17,S_['17'],'Elevation',f'{pre} shaft base across (18 ft 1 1/2 in)',M,ft(18,1,1,2),sh[1][1],0.021)
    T=sh[2][sh[2][:,2]>sh[2][:,2].max()-0.02]; dt=T.max(0)-T.min(0)
    add(20,S_['20'],'Section B-B',f'{pre} shaft top along bridge (14 ft)',M,14.0,dt[0],0.021)
    add(20,S_['20'],'Section A-A',f'{pre} shaft top across (13 ft 6 in)',M,13.5,dt[1],0.021)
# abutments
f=one('W Abutment footing','footing'); d,mn,mx=dims(f)
add(11,S_['11'],'Section A-A / front elev.','W abutment footing bottom El. (16.0)',M,16.0,mn[2],0.01)
add(11,S_['11'],'Section A-A','W abutment footing top El. (20.5)',M,20.5,mx[2],0.01)
add(11,S_['11'],'Plan / front elev.','W abutment footing length (99 ft 6 in)',M,99.5,d[1],0.021)
add(11,S_['11'],'Section A-A','W abutment footing width along bridge (52 ft)',M,52.0,d[0],0.021)
pw=find('W Abutment pedestal W','pedestal')[0][1][1]
add(11,S_['11'],'Section A-A','W abutment pedestal top El. (41.974 final)',M,41.974,pw[:,2].max(),0.01)
for side in 'WE':
    w=find(f'W Abutment wing extension {side}','wingwall')[0][1][1]; dw=w.max(0)-w.min(0)
    add(11,S_['11'],'Section A-A','W abutment wing extension length beyond footing (14+8 ft)',M,22.0,dw[0],0.021)
    add(11,S_['11'],'Section A-A / front elev.',f'W abutment wing ext. {side} top El. at front end (56.378 curb line)',M,56.378,w[:,2].max(),0.01)
    lo=w[:,2].min(); add(11,S_['11'],'Section A-A',f'W abutment wing ext. {side} bottom El. under first 14 ft (42.00)',M,42.0,lo,0.01)
    xs=np.unique(w[:,0].round(1)); xfar=xs.min()
    hi=w[np.abs(w[:,0]-w[:,0].min())<0.05][:,2].min()
    add(11,S_['11'],'Section A-A',f'W abutment wing ext. {side} bottom El. under last 8 ft (49.00)',M,49.0,hi,0.01)
f=one('E Abutment footing','footing'); d,mn,mx=dims(f)
add(38,S_['38'],'Section A-A / pile plan','E abutment footing bottom El. (51.154)',M,51.154,mn[2],0.01)
add(38,S_['38'],'Section A-A','E abutment footing top El. (55.154)',M,55.154,mx[2],0.01)
add(38,S_['38'],'Pile plan / plan','E abutment footing length (88 ft 8 in)',M,ft(88,8),d[1],0.021)
add(38,S_['38'],'Pile plan','E abutment footing width along bridge (24 ft 6 in)',M,24.5,d[0],0.021)
pe=find('E Abutment pedestal W','pedestal')[0][1][1]
add(38,S_['38'],'Section A-A','E abutment pedestal top El. (62.033)',M,62.033,pe[:,2].max(),0.01)
for r in rows:
    if r[9]!='pass': print(r)
print(len(rows))
import pickle; pickle.dump(rows,open('rows_sub3.pkl','wb'))
