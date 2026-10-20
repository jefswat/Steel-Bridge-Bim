import sys,statistics,pickle
sys.path.insert(0,r'C:\SBM\qc\ptc\work')
from main_cmp import *
rows=[]
SH={'top chord':'DRB-4 29/68','bottom chord':'DRB-4 29/68','vertical':'DRB-4 30/68','diagonal':'DRB-4 30/68'}
PG={'top chord':69,'bottom chord':69,'vertical':70,'diagonal':70}
VIEW={'top chord':'Top chords table','bottom chord':'Bottom chords table','vertical':'Verticals table','diagonal':'Diagonals table'}
ev='work/main_cmp.py ; work/p/c69t*.png c69b*.png c70v*.png c70d*.png'
for kind,lst in res.items():
    for k,L,A,mt,v in lst:
        Ls=statistics.mean(x[2] for x in v); As=statistics.mean(x[3] for x in v); ms=sorted(set(x[4] for x in v))
        pg,sh,vw=PG[kind],SH[kind],VIEW[kind]
        meth='model: sqlite volume / member length (4 instances N/S x both halves)'
        if A is not None:
            d=As/A-1
            res_='pass' if abs(d)<=0.01 else 'fail'
            note=''
            if k in('U13-L14','U14-L15'):
                res_='conflict'; note='plan internal conflict: section text of U13-L14 (2-Pls 26x1) equals U11-L12 (116.69) but prints 118.69; U14-L15 (2-Pls 28x1) prints 116.69 but 28 in plates add 4 sq in -> 120.69; model follows the printed section text'
            if k=='U11-L11':
                note='plan lists 293.67 (and 217.17 on the 2nd row); main material list (p.69) = 4-L 5x5x7/8, 2-cover pl (23-12)x1 1/8, 2-web pl 18x7/8, 4-web pl 34x3/4, 2-web pl 34x1 1/8, 2-web pl 34x9/16 = about 305 sq in with the perforated cover counted net; model 332 = cover plates counted solid (23 in)'
            rows.append([pg,sh,vw,f'{kind} {k} full gross area (sq in)',meth,A,round(As,2),round(As-A,2),'1%',res_,ev+(' ; '+note if note else '')])
        rows.append([pg,sh,vw,f'{kind} {k} material (C carbon / S silicon)','model material string',mt,'/'.join(ms),'', 'exact','pass' if ms==[mt] else 'fail',ev])
        if kind in('top chord','bottom chord'):
            d=Ls-L
            note='' if k!='L0-L2' else 'model chord extends beyond the L0 working point (pin) by about 1.4 ft; plan elevation p.73 shows the end gusset only about 0.5-0.7 ft beyond the vertical through L0 (scaled, not dimensioned)'
            rows.append([pg,sh,vw,f'{kind} {k} length joint to joint (ft)','model member length (sqlite length_m)',L,round(Ls,4),round(d,4),'0.02 ft','pass' if abs(d)<=0.02 else 'fail',ev+(' ; '+note if note else '')])
rows.append([70,'DRB-4 30/68','Verticals / diagonals tables','vertical and diagonal lengths (plan = joint to joint) vs model net member length','model length_m',0,0,'','n.a.','n.a.','model builds web members between chord faces (3.0-6.0 ft shorter than the printed joint-to-joint lengths); not comparable, areas / materials checked instead'])
pickle.dump(rows,open('rows_main.pkl','wb')); 
import collections; print(len(rows),collections.Counter(r[9] for r in rows))
for r in rows:
    if r[9] in('fail','conflict') and 'length' not in r[3]: print(r[3],r[5],r[6],r[7],r[9])
