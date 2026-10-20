import sys; sys.path.insert(0,'.'); from qc import *
E=load_el()
WP,t=align_pt(30153.72)
def inter(phi,Rg):
    d=np.array([math.cos(phi),math.sin(phi)])
    # solve |WP + s d - C| = Rg
    w=WP-C; b=w@d; c=w@w-Rg*Rg; disc=b*b-c
    s=-b+math.sqrt(disc) if abs(-b+math.sqrt(disc))<abs(-b-math.sqrt(disc)) else -b-math.sqrt(disc)
    p=WP+s*d; rv=(p-C)/Rg; tang=np.array([-rv[1],rv[0]])
    ang=math.degrees(math.acos(abs(d@tang))); return p,ang,s
tg=math.atan2(t[1],t[0])
from math import radians
best=None
for k in range(-900000,900000):
    phi=tg+radians(90)+k*1e-6
    p,ang,s=inter(phi,RG['G1'])
    if sta_off(*p)[0]<30153.72 and (best is None or abs(ang-(69+2/60+38/3600))<best[0]): best=(abs(ang-(69+2/60+38/3600)),phi)
phi=best[1]
p1,a1,s1=inter(phi,RG['G1']); p2,a2,s2=inter(phi,RG['G2'])
print('phi deg from tangent',math.degrees(phi-tg),'angles',a1,a2, 'G2 plan 69.3150')
print('dist between girders along pier',fmt_ftin(np.linalg.norm(p1-p2)),'plan 18-11 7/8')
# also try the other root of angle (supplement)
for g,p,off in [('G1',p1,(12+15/16)/12),('G2',p2,(12+7/8)/12)]:
    th,_=polar(*p); Rg=RG[g]
    # bearing: move along girder toward increasing station (span 14) -> station increases with theta
    thb=th+off/Rg
    b=[r for r in E if r['mark']=='B1-%s-PAD'%g][0]; thm,rm=polar(b['cx'],b['cy'])
    print(g,'CLpier-girder pt sta',sta_off(*p),'model brg - plan brg along girder (in):',round((thm-thb)*Rg*12,3))
