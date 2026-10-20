import sqlite3,re,statistics,pickle,collections
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
# plan p.57: key -> (gross area, section, material)
CH={'U0-U1':(25.56,'14WF87','C'),'U1-U3':(84.37,'14WF287','S'),'U3-U5':(114.37,'14WF287+2Pl16x15/16','S'),'U5-U7':(92.30,'14WF314','S'),'U7-U9':(27.94,'14WF95','S'),
 'U9-U11':(127.40,'14WF342+2Pl16.5x13/16','S'),'U11-U13':(34.99,'14WF119','S'),'U13-U15':(44.08,'14WF150','S'),'U15-U17':(54.07,'14WF184','S'),'U17-U19':(25.56,'14WF87','C'),
 'U19-U19p':(92.30,'14WF314','S'),
 'L0-L2':(41.85,'14WF142','S'),'L2-L4':(92.30,'14WF314','S'),'L4-L6':(92.30,'14WF314','S'),'L6-L8':(49.09,'14WF167','S'),'L8-L10':(84.37,'14WF287','S'),'L10-L12':(92.30,'14WF314','S'),
 'L12-L14':(25.56,'14WF87','S'),'L14-L16':(46.47,'14WF158','S'),'L16-L18':(32.65,'14WF111','S'),'L18-L20':(56.73,'14WF193','S')}
DG={'L0-U1':(84.37,'14WF287','S'),'U1-L2':(44.08,'14WF150','S'),'L2-U3':(44.08,'14WF150','S'),'U3-L4':(41.85,'14WF142','C'),'L4-U5':(41.85,'14WF142','C'),'U5-L6':(46.47,'14WF158','S'),
 'L6-U7':(46.47,'14WF158','S'),'U7-L8':(92.30,'14WF314','S'),'L8-U9':(77.63,'14WF264','S'),'U9-L10':(129.47,'14WF342+2Pl','S'),'L10-U11':(114.30,'14WF314+2Pl','S'),
 'U11-L12':(62.07,'14WF211','S'),'L12-U13':(72.33,'14WF246','S'),'U13-L14':(34.99,'14WF119','S'),'L14-U15':(41.85,'14WF142','C'),'U15-L16':(41.85,'14WF142','C'),
 'L16-U17':(25.56,'14WF87','S'),'U17-L18':(59.39,'14WF202','S'),'L18-U19':(54.07,'14WF184','S'),'U19-L20':(100.59,'14WF342','S')}
VT={}
for i in range(0,21):
    k=f'U{i}-L{i}'
    if i==0: VT[k]=(24.71,'14WF84','S')
    elif i in (2,4,6,10,20): VT[k]=(28.22,'16WF96','S')
    elif i in (8,12,14,16,18): VT[k]=(24.71,'14WF84','S')
HG={f'U{i}-L{i}':(17.94,'14WF61','C') for i in range(1,20,2)}
rows=[]
ev='work/four_rec.py ; work/p/c57c*.png c57d*.png c57e.png c57dm.png c57cm.png'
def norm(name):
    nm=name.split(' ',2)[2]; side,rest=nm.split('-',1); a,b=rest.split('-')
    ka,kb=a.rstrip("'"),b.rstrip("'")
    if a.endswith("'") and b.endswith("'"):
        # mirror: U1'-U0' == U0-U1 ; U19'-U17' == U17-U19
        ia,ib=int(ka[1:]),int(kb[1:])
        key=ka+'-'+kb if ia<ib else kb+'-'+ka
        return side,key
    if a.endswith("'") or b.endswith("'"):
        # includes L0'-U1' etc. diag mirror, or U19-U19'
        if ka==kb or (ka[1:]==kb[1:] and ka[0]==kb[0]): return side,ka+'-'+kb+'p'
        return side,None
    return side,ka+'-'+kb
for bridge in ('PTC 4-span truss 14-17','PTC 4-span truss 21-24'):
    for cat,plan,label in (('truss chord',CH,'chord'),('truss diagonal',DG,'diagonal'),('truss vertical',VT,'vertical'),('truss hanger',HG,'hanger')):
        d=collections.defaultdict(list)
        for name,L,A,mat in c.execute("select name,length_m/0.3048,volume_m3/length_m/0.0254/0.0254,material from elements where bridge_group=? and category=?",(bridge,cat)):
            if not name.split(' ',2)[2].startswith('N-') and not name.split(' ',2)[2].startswith('S-'): continue
            side,key=norm(name)
            if cat=='truss diagonal' and key and key not in plan:
                # mirror diagonals are named U1'-L0' (plan L0'-U1'): flip
                a,b=key.split('-'); key=b+'-'+a if b+'-'+a in plan else key
            if key in plan or (key and key.endswith('p') and key in plan): d[key].append((A,mat[-2:-1],side))
            elif cat=='truss chord' and key is None: pass
        for k,(pa,sec,pm) in plan.items():
            if k not in d: rows.append([57,'DRB-4 17/68',f'{label} table',f'{bridge}: {label} {k} NOT FOUND in model names','','','','','','open',ev]); continue
            v=d[k]; A=statistics.mean(x[0] for x in v); ms=sorted(set(x[1] for x in v))
            dd=A/pa-1
            rows.append([57,'DRB-4 17/68',f'{label} table',f'{bridge}: {label} {k} ({sec}) full gross area (sq in)','model sqlite volume / length (N and S, both halves)',pa,round(A,2),round(A-pa,2),'1%','pass' if abs(dd)<=0.01 else 'fail',ev])
            rows.append([57,'DRB-4 17/68',f'{label} table',f'{bridge}: {label} {k} material (C carbon / S silicon)','model material string',pm,'/'.join(ms),'','exact','pass' if ms==[pm] else 'fail',ev])
pickle.dump(rows,open('rows_four.pkl','wb'))
print(len(rows),collections.Counter(r[9] for r in rows))
for r in rows:
    if r[9]!='pass': print(r[3],r[5],r[6],r[9])
