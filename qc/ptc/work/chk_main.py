import csv,re
R=list(csv.DictReader(open('geom.csv')))
grav=[143.3900,144.2989,145.1655,145.9898,146.7718,147.5116,148.2091,148.8643,149.4773,150.0480,150.5764,151.0625,151.5064,151.9080,152.2673,152.5843,152.8591,153.0916,153.2818,153.4298,153.5355,153.5989,153.6200]
low=[113.1400,114.0278,114.9155,115.7186,116.5218,117.1172,117.0744,116.0790,113.8895,110.2998,105.1280,98.2010,120.1218,140.7820,159.6509,176.3176,190.7620,202.9842,212.9842,220.7620,226.3176,229.6509,230.7620]
wind={13:140.7620,14:141.1215,15:141.4385,16:141.7133,17:141.9458,18:142.1360,19:142.2840,20:142.3897,21:142.4531,22:142.4742}
S0=67598.38
def idx(lbl):
    m=re.match(r"([ULW])(\d+)('?)$",lbl); k,i,p=m.group(1),int(m.group(2)),m.group(3)
    return k,(44-i if p else i), i
def plan(k,i22):
    j=i22 if i22<=22 else 44-i22
    if k=='L': return low[j]
    if k=='W': return wind.get(j)
    return None
res=[]
for r in R:
    if 'Arch' not in r['bridge']: continue
    mk=r['mark']
    m=re.match(r"([NS])-([ULW]\d+'?)-([ULW]\d+'?)$",mk)
    if not m or r['cat']!='truss chord': continue
    a0=[float(r[k]) for k in ('a0s','a0o','a0z')];a1=[float(r[k]) for k in ('a1s','a1o','a1z')]
    ds=a1[0]-a0[0]
    def zat(s): return a0[2]+(s-a0[0])*(a1[2]-a0[2])/ds
    for lbl in (m.group(2),m.group(3)):
        k,i44,_=idx(lbl)
        if k=='U': continue
        pz=plan(k,i44)
        if pz is None: continue
        s=S0+31*i44
        sm=zat(s)
        res.append((mk,lbl,round(s,2),pz,round(sm,4),round(sm-pz,4), 'in' if min(a0[0],a1[0])-0.5<=s<=max(a0[0],a1[0])+0.5 else 'out'))
import collections
w=csv.writer(open('main_joint_z.csv','w',newline=''));w.writerow(['member','point','station_ft','plan_z','model_axis_z','diff','within_member'])
for x in res: w.writerow(x)
d=[abs(x[5]) for x in res]
print(len(res),max(d),sum(1 for v in d if v>0.01),sum(1 for v in d if v>0.0625))
for x in sorted(res,key=lambda x:-abs(x[5]))[:25]: print(x)
