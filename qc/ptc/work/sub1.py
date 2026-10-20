import sys; sys.path.insert(0,r'C:\SBM\qc\ptc\work')
from qm import *
import numpy as np
def one(name,cat):
    r=find(name,cat)
    assert len(r)==1,(name,len(r),[x[1][0][1] for x in r]); return r[0][1][1]
def dims(P): mn=P.min(0);mx=P.max(0);return mx-mn,mn,mx
def inch(f,i,n=1,d=1): return f+(i+n/d)/12
rows=[]
def add(pg,sh,view,item,method,plan,model,tol,note=''):
    diff=model-plan
    res='pass' if abs(diff)<=tol+1e-9 else 'fail'
    rows.append([pg,sh,view,item,method,round(plan,3),round(model,3),round(diff,3),f'{tol} ft',res,note])
S_={'10':'DRB-2 7/11','9':'DRB-2 6/11','8':'DRB-2 5/11','7':'DRB-2 4/11','6':'DRB-2 3/11','17':'DRB-1 3/11','20':'DRB-1 6/11','27':'DRB-1 10/11','33':'DRB-3 3/10','34':'DRB-3 4/10','35':'DRB-3 5/10','36':'DRB-3 6/10','37':'DRB-3 7/10','11':'DRB-2 8/11','38':'DRB-3 8/10'}
M='IFC mesh extents (work/sub.pkl)'
def ft(a,b,c=0,dd=1): return a+ (b+c/dd)/12
# footing + column + top for piers 1-9
p10={1:(44.437,8.0,13.0,ft(7,9,1,2),85,16),2:(47.381,12.0,17.0,ft(7,9,1,2),85,16),3:(50.836,12.0,17.0,ft(7,11),80,22),4:(54.568,12.5,17.5,ft(8,0,1,2),80,22),
 5:(58.300,14.0,19.0,ft(8,1,3,4),80,22),6:(62.033,16.0,21.0,ft(8,2,1,2),80,22),7:(65.765,18.0,23.0,ft(8,3,1,2),80,22),8:(69.498,21.5,26.5,ft(8,3,1,2),80,22),9:(73.230,20.5,25.5,ft(8,5,3,4),80,22)}
for n,(top,fb,ft_,base,L,W) in p10.items():
    f=one(f'Pier {n} footing','footing'); d,mn,mx=dims(f)
    add(10,S_['10'],f'Pier {n} details',f'Pier {n} footing bottom El.',M,fb,mn[2],0.01)
    add(10,S_['10'],f'Pier {n} details',f'Pier {n} footing top El.',M,ft_,mx[2],0.01)
    add(10,S_['10'],'Footing plan',f'Pier {n} footing length along pier (O)',M,L,d[1],0.021)
    add(10,S_['10'],'Footing plan',f'Pier {n} footing width along bridge (S)',M,W,d[0],0.021)
    cs=find(f'Pier {n} shaft','column')
    zt=max(v[1][:,2].max() for g,v in cs)
    add(10,S_['10'],f'Pier {n} details',f'Pier {n} shaft top El.',M,top,zt,0.01)
    for g,v in cs:
        P=v[1]; zb=P[:,2].min(); Q=P[P[:,2]<zb+0.02]; d2=Q.max(0)-Q.min(0)
        add(10,S_['10'],f'Pier {n} details',f'{v[0][1]} base size (square)',M,base,d2[0],0.021)
        T=P[P[:,2]>P[:,2].max()-0.02]; d3=T.max(0)-T.min(0)
        add(10,S_['10'],'Elevation',f'{v[0][1]} top size (6\'-6" square)',M,6.5,d3[0],0.021)
        break
    # c-c of shafts
    c=[ (v[1][:,1].min()+v[1][:,1].max())/2 for g,v in cs]
    add(10,S_['10'],'Elevation','Pier %d shaft c-c across (58\'-0)'%n,M,58.0,abs(c[0]-c[1]),0.02)
import pickle
pickle.dump(rows,open('rows_sub1.pkl','wb'))
for r in rows:
    if r[9]!='pass': print(r)
print(len(rows))
