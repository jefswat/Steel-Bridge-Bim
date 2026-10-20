import sys,json; sys.path.insert(0,'.'); from qc import *
E=load_el(); M={r['mark']:r for r in E if r['mark']}
F=json.load(open('girder_feat.json'))
dv,df=geom(M['DECK']['guid'])
def ztop(x,y,mesh=(dv,df),which=max):
    v,f=mesh; a,b,c=v[f[:,0]],v[f[:,1]],v[f[:,2]]
    # barycentric in xy
    d=(b[:,1]-c[:,1])*(a[:,0]-c[:,0])+(c[:,0]-b[:,0])*(a[:,1]-c[:,1])
    ok=np.abs(d)>1e-12
    l1=((b[:,1]-c[:,1])*(x-c[:,0])+(c[:,0]-b[:,0])*(y-c[:,1]))/np.where(ok,d,1)
    l2=((c[:,1]-a[:,1])*(x-c[:,0])+(a[:,0]-c[:,0])*(y-c[:,1]))/np.where(ok,d,1)
    l3=1-l1-l2; m=ok&(l1>=-1e-9)&(l2>=-1e-9)&(l3>=-1e-9)
    z=l1*a[:,2]+l2*b[:,2]+l3*c[:,2]
    return which(z[m]) if m.any() else None
H={1:5+10/12,2:5+(10+5/8)/12,3:5+11/12,4:5+(11+3/8)/12}
out=[]
for g,off in [('G1',13.3333333),('G2',-4.3333333)]:
    for i in range(1,5):
        th=F[g]['BRG%d'%i]; p=C+RG[g]*np.array([math.cos(th),math.sin(th)])
        s,o=sta_off(*p)
        tos_plan=profile(s)-0.06*o   # 6% down to the left (+offset)
        tos_model=ztop(p[0],p[1])+Z0
        bot_model=ztop(p[0],p[1],which=min)+Z0
        pad=M['B%d-%s-PAD'%(i,g)]; padbot=pad['zmin']+Z0
        # top of web WP: bottom of top flanges at CL: use TF plates, find bottom surface z at web top lines
        print(g,i,'sta %.2f off %.3f'%(s,o),'TOS plan(PGL-6%%) %.3f model %.3f d=%.3f'%(tos_plan,tos_model,tos_model-tos_plan),
              'slab bottom %.3f thick %.3f in'%(bot_model,(tos_model-bot_model)*12),
              'pad bottom %.3f ; TOS-H plan %.3f d=%.3f'%(padbot,tos_plan-H[i],padbot-(tos_plan-H[i])))
from cut import cut
print('--- haunch at CL brg')
for g in ['G1','G2']:
    for i in range(1,5):
        th=F[g]['BRG%d'%i]; p=C+RG[g]*np.array([math.cos(th),math.sin(th)])
        tang=np.array([-math.sin(th),math.cos(th),0.])
        o=cut(E,np.array([p[0],p[1],0.]),tang,lambda r: r['ptype'] in ('FLANGE_PLATE','WEB_PLATE') and (r['mark'] or '').startswith(g))
        tf=[];bf=[];webtop=[]
        for gid,(r,s) in o.items():
            a=np.array([q for sg in s for q in sg])
            if '-TF' in r['mark']:
                # bottom surface at plate center: take min z points
                zc=np.polyfit(a[:,0],a[:,1],1); tf.append((a[:,0].mean(), a[:,1].min(), r['mark'], a[:,0].max()-a[:,0].min(), a[:,1].max()-a[:,1].min()))
            if '-BF' in r['mark']: bf.append((a[:,0].min(),a[:,0].max(),a[:,1].min(),a[:,1].max(),r['mark']))
        tos=ztop(p[0],p[1]); sb=ztop(p[0],p[1],which=min)
        if len(tf)==2:
            wp=(tf[0][1]+tf[1][1])/2
            print(g,i,[t[2] for t in tf],'TF-bottom avg z %.3f'%(wp+Z0),'haunch(slab bottom - TFbottom@CL) %.3f in'%((sb-wp)*12),'TOS-WP %.3f in'%((tos-wp)*12),
                  'BF',[ (round((b[1]-b[0])*12,2),b[4]) for b in bf], 'girder depth WP->BF bottom %.3f in'%((wp-min(b[2] for b in bf))*12))
