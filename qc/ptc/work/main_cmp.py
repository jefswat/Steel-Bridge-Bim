import sqlite3,re,sys,pickle,statistics
sys.path.insert(0,r'C:\SBM\qc\ptc\work')
from plan_main import *
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
rows=c.execute("select category,name,length_m/0.3048,volume_m3/length_m/0.0254/0.0254,material,mass_kg*2.20462 from elements where bridge_group like '%Arch%' and category in ('truss chord','truss vertical','truss diagonal')").fetchall()
mod={}
for cat,name,L,A,mat,m in rows:
    nm=name.split(' ',2)[2]  # N-U1-U2
    side,rest=nm.split('-',1)
    a,b=rest.split('-')
    pr=a.endswith("'")
    # normalise primes: mirror member U1'-U0' == U0-U1
    def num(s): return int(re.sub(r"\D","",s)), s[0]
    ka,kb=a.rstrip("'"),b.rstrip("'")
    key=ka+'-'+kb
    ia,ib=int(ka[1:]),int(kb[1:])
    if pr and ia>ib: key=kb+'-'+ka
    if key not in mod: mod[key]=[]
    mod[key].append((side,pr,L,A,mat[-2:-1],name))
def sel(plan,kind):
    out=[]
    for k,(L,A,mt) in plan.items():
        # key normalisation: plan chord 'U1-U2'; model key ordering follows a-b as in name (non-primed); diagonals L0-U1 etc
        v=mod.get(k)
        if v is None:
            # try reversed
            a,b=k.split('-'); v=mod.get(b+'-'+a)
        out.append((k,L,A,mt,v))
    return out
res={}
for kind,plan in (('top chord',TOP),('bottom chord',BOT),('vertical',VERT),('diagonal',DIAG)):
    res[kind]=sel(plan,kind)
if __name__=='__main__':
    for kind,lst in res.items():
        print('==',kind)
        for k,L,A,mt,v in lst:
            if not v: print(k,'NO MODEL'); continue
            Ls=[x[2] for x in v]; As=[x[3] for x in v]; ms=set(x[4] for x in v)
            print(k,'L plan %.4f model %.4f (%+.3f)'%(L,statistics.mean(Ls),statistics.mean(Ls)-L),'| A plan',A,'model %.2f'%statistics.mean(As),'(%+.2f%%)'%(100*(statistics.mean(As)/A-1)) if A else '', '| mat',mt,ms, len(v))
