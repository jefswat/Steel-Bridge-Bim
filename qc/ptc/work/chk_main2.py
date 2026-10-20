import csv,re,collections
exec(open('chk_main.py').read().split("res=[]")[0])
mem=collections.defaultdict(list)
for r in R:
    if 'Arch' not in r['bridge'] or r['cat']!='truss chord' or not r['mark'].startswith('N-'): continue
    m=re.match(r"N-([ULW]\d+'?)-([ULW]\d+'?)$",r['mark'])
    a0=[float(r[k]) for k in ('a0s','a0z')];a1=[float(r[k]) for k in ('a1s','a1z')]
    mem[(m.group(1),m.group(2))]=(a0,a1)
def line(a0,a1):
    k=(a1[1]-a0[1])/(a1[0]-a0[0]); return k,a0[1]-k*a0[0]
def inter(A,B):
    k1,b1=line(*A);k2,b2=line(*B)
    if abs(k1-k2)<1e-6: return None
    s=(b2-b1)/(k1-k2); return s,k1*s+b1
# build chains for L (and W) : members containing two endpoints; find pairs sharing point
byp=collections.defaultdict(list)
for (a,b),v in mem.items():
    for p in (a,b): byp[p].append(((a,b),v))
out=[]
for p,lst in byp.items():
    k,i44,_=idx(p)
    if k=='U': continue
    # only members of same chain letter
    ch=[x for x in lst if x[0][0][0]==k and x[0][1][0]==k]
    if len(ch)!=2: continue
    r=inter(ch[0][1],ch[1][1])
    if not r: continue
    s,z=r
    ps=S0+31*i44; pz=plan(k,i44)
    out.append((p,ch[0][0],ch[1][0],round(ps,2),round(s,3),round(s-ps,3),pz,round(z,4),round(z-pz,4) if pz else None))
w=csv.writer(open('main_joint_inter.csv','w',newline=''));w.writerow(['point','m1','m2','plan_station','model_station','ds','plan_z','model_z','dz']);[w.writerow(x) for x in out]
for x in sorted(out,key=lambda x:-abs(x[8] or 0))[:25]: print(x)
print(len(out), max(abs(x[5]) for x in out))
