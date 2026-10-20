exec(open('sub1.py').read().split("# footing + column")[0])
rows=[]
def base_of(name,cat='column'):
    out=[]
    for g,v in find(name,cat):
        P=v[1]; zb=P[:,2].min(); Q=P[P[:,2]<zb+0.02]; d=Q.max(0)-Q.min(0); out.append((v[0][1],d,P))
    return out
def top_of(P): T=P[P[:,2]>P[:,2].max()-0.02]; return T.max(0)-T.min(0)
# Pier 10 (p.9) and 27 (p.36)
for pn,pg,ftg,fb,ftop,top,brk,lo,ls in ((10,9,'Pier 10 girder brg footing',20.5,27.5,76.918,55.962,ft(8,6,3,4),12.5),(27,36,'Pier 27 truss brg footing',11.5,18.5,76.918,55.962,ft(8,11,1,4),12.875)):
    sh=S_[str(pg)]
    f=one(ftg,'footing'); d,mn,mx=dims(f)
    add(pg,sh,'Elevation / plan',f'Pier {pn} footing bottom El.',M,fb,mn[2],0.01)
    add(pg,sh,'Elevation / plan',f'Pier {pn} footing top El.',M,ftop,mx[2],0.01)
    add(pg,sh,'Plan',f'Pier {pn} footing length (86\'-6")',M,86.5,d[1],0.021)
    add(pg,sh,'Plan',f'Pier {pn} footing width (24\'-0")',M,24.0,d[0],0.021)
    up=find(f'Pier {pn}','column'); 
    upper=[x for x in up if 'upper' in x[1][0][1]][0][1][1]; lower=[x for x in up if 'lower' in x[1][0][1]][0][1][1]
    add(pg,sh,'Elevation',f'Pier {pn} upper shaft top El.',M,top,upper[:,2].max(),0.01)
    add(pg,sh,'Elevation',f'Pier {pn} shaft change El. (55.962)',M,brk,lower[:,2].max(),0.01)
    t=top_of(upper); add(pg,sh,'Elevation / Sect B-B',f'Pier {pn} upper shaft top width across (6\'-6")',M,6.5,t[1],0.021)
    add(pg,sh,'Sect B-B',f'Pier {pn} upper shaft top width along bridge (4\'-8 3/4")',M,ft(4,8,3,4),t[0],0.021)
    bu=upper[upper[:,2]<upper[:,2].min()+0.02]; bd=bu.max(0)-bu.min(0)
    add(pg,sh,'Elevation',f'Pier {pn} upper shaft base across (7\'-4 1/2")',M,ft(7,4,1,2),bd[1],0.021)
    bl=lower[lower[:,2]<lower[:,2].min()+0.02]; bd=bl.max(0)-bl.min(0)
    add(pg,sh,'Elevation',f'Pier {pn} lower shaft base across',M,lo,bd[1],0.021)
    add(pg,sh,'Section B-B',f'Pier {pn} lower shaft base along bridge',M,ls,bd[0],0.021,'')
# pier 11-16 trussed piers: (pn, page, footing name, ftgL,ftgW, fb, ftop, capTop, capH, capW, capL, leg O, leg S)
T=[(11,8,'Pier 11 footing',94,24,16.5,24.5,62.312,ft(11,3,1,2),9.0,ft(49,2,1,2),ft(9,2,1,4),ft(10,7)),
   (12,8,'Pier 12 footing',94,24,15.5,23.5,68.339,ft(11,3,1,2),9.0,ft(49,2,1,2),ft(9,4),ft(10,10,1,2)),
   (13,7,'Pier 13 W brg footing',96,24,11.0,19.0,75.357,ft(12,2),10.0,ft(47,7,0),11.0,ft(12,4)),
   (14,7,'Pier 14 footing',98,28,8.0,17.0,83.005,ft(13,1),11.0,ft(46,1,1,2),ft(12,6,3,4),ft(13,9)),
   (15,6,'Pier 15 footing',98,40,2.0,10.0,91.525,13.0,11.0,ft(44,10),ft(14,0,1,2),ft(14,4,3,4)),
   (16,6,'Pier 16 footing',100,30,4.0,14.0,99.168,13.0,11.0,ft(44,10),ft(14,1,1,4),ft(14,6,1,2)),
   (21,33,'Pier 21 footing',100,30,-5.0,5.0,99.168,13.0,11.0,ft(44,10),ft(14,3,1,2),ft(14,11)),
   (22,33,'Pier 22 footing',98,40,2.0,10.0,91.525,13.0,11.0,ft(44,10),ft(14,0,1,2),ft(14,4,3,4)),
   (23,34,'Pier 23 footing',98,28,5.5,14.5,83.005,ft(13,1),11.0,ft(46,1,1,2),ft(12,7,1,4),ft(13,10)),
   (24,34,'Pier 24 W brg footing',96,26,3.0,11.0,75.357,ft(12,2),10.0,ft(47,7,0),ft(11,2),ft(12,8)),
   (25,35,'Pier 25 footing',94,27,5.0,13.0,68.339,ft(11,3,1,2),9.0,ft(49,2,1,2),ft(9,6,1,2),ft(11,3,1,2)),
   (26,35,'Pier 26 footing',94,27,6.0,14.0,62.312,ft(11,3,1,2),9.0,ft(49,2,1,2),ft(9,4,3,4),11.0)]
for (pn,pg,ftg,L,W,fb,ftop,ct,ch,cw,cl,legO,legS) in T:
    sh=S_[str(pg)]
    f=one(ftg,'footing'); d,mn,mx=dims(f)
    add(pg,sh,'Elevation / half plan',f'Pier {pn} footing bottom El.',M,fb,mn[2],0.01)
    add(pg,sh,'Elevation',f'Pier {pn} footing top El.',M,ftop,mx[2],0.01)
    add(pg,sh,'Half plan footing',f'Pier {pn} footing length across (O)',M,L,d[1],0.021)
    add(pg,sh,'Half plan footing',f'Pier {pn} footing width along bridge (S)',M,W,d[0],0.021)
    cap=find(f'Pier {pn}','pier cap')[0][1][1]; dc,mnc,mxc=dims(cap)
    add(pg,sh,'Elevation',f'Pier {pn} cap top El.',M,ct,mxc[2],0.01)
    add(pg,sh,'Elevation',f'Pier {pn} cap depth (soffit at leg)',M,ch,dc[2],0.021)
    add(pg,sh,'Plan top view',f'Pier {pn} cap width along bridge',M,cw,dc[0],0.021)
    add(pg,sh,'Elevation',f'Pier {pn} cap length between leg faces',M,cl,dc[1],0.03)
    legs=base_of(f'Pier {pn}')
    legs=[x for x in legs if 'leg' in x[0]]
    nm,dd,P=legs[0]
    add(pg,sh,'Half plan footing',f'{nm} base size across (O)',M,legO,dd[1],0.021)
    add(pg,sh,'Half plan footing',f'{nm} base size along bridge (S)',M,legS,dd[0],0.021)
for r in rows:
    if r[9]!='pass': print(r)
print(len(rows))
import pickle; pickle.dump(rows,open('rows_sub2.pkl','wb'))
