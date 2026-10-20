exec(open('elevlib.py').read())
from cut import cut
def wp_at(g,th):
    p=C+RG[g]*np.array([math.cos(th),math.sin(th)]); tang=np.array([-math.sin(th),math.cos(th),0.])
    o=cut(E,np.array([p[0],p[1],0.]),tang,lambda r: (r['mark'] or '').startswith(g+'-') and ('-TFL' in r['mark'] or '-TFR' in r['mark'] or '-BF' in r['mark']) and r['ptype']=='FLANGE_PLATE')
    tf=[];bf=[]
    for gid,(r,s) in o.items():
        a=np.array([q for sg in s for q in sg])
        (tf if '-TF' in r['mark'] else bf).append((a,r['mark']))
    if len(tf)!=2 or not bf: return None
    wp=np.mean([a[:,1].min() for a,m in tf]); spacing=abs(tf[0][0][:,0].mean()-tf[1][0][:,0].mean())
    return p,wp,spacing,[(m,(a[:,0].max()-a[:,0].min())*12,(a[:,1].max()-a[:,1].min())*12) for a,m in tf+bf], min(a[:,1].min() for a,m in bf)
res=[]
for g in ['G1','G2']:
    for i in range(1,5):
        th0_=F[g]['BRG%d'%i]; vals=[]
        for d in ([2.5,4.0] if i==1 else [-4.0,-2.5] if i==4 else [-3,3]):
            w=wp_at(g,th0_+d/RG[g])
            if w: vals.append((d,w))
        # linear interp to d=0
        (d1,w1),(d2,w2)=vals[0],vals[-1]
        wp0=w1[1]+(w2[1]-w1[1])*(0-d1)/(d2-d1)
        p=C+RG[g]*np.array([math.cos(th0_),math.sin(th0_)])
        tos=ztop(p[0],p[1]); sb=ztop(p[0],p[1],which=min)
        print(g,'BRG%d'%i,'WP z %.3f'%(wp0+Z0),'haunch %.3f in (plan 4.5)'%((sb-wp0)*12),'TF spacing %.3f in'%(w1[2]*12), 'depth WP-BFbot %.3f in'%((w1[1]-w1[4])*12), [ (m,round(a,2),round(b,3)) for m,a,b in w1[3]])
