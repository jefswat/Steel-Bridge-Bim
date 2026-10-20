import sys; sys.path.insert(0,'.'); from qc import *
E=load_el()
pts={}
for g,off in [('G1',(12+15/16)/12),('G2',(12+7/8)/12)]:
    b=[r for r in E if r['mark']=='B1-%s-PAD'%g][0]; th,rm=polar(b['cx'],b['cy']); Rg=RG[g]
    th2=th-off/Rg; pts[g]=C+Rg*np.array([math.cos(th2),math.sin(th2)])
    print(g,'model brg sta',sta_off(b['cx'],b['cy']))
d=pts['G2']-pts['G1']; L_=np.linalg.norm(d); d/=L_
print('dist along pier',fmt_ftin(L_),'plan 18-11 7/8 (18.9896)')
for g in ['G1','G2']:
    rv=(pts[g]-C)/RG[g]; tang=np.array([-rv[1],rv[0]]); a=math.degrees(math.acos(abs(d@tang)))
    print(g,'angle',a, '%dd%02dm%04.1fs'%(int(a),int((a%1)*60),((a*60)%1)*60))
WP,_=align_pt(30153.72); w=WP-pts['G1']; print('WP distance from model pier line (ft)', w[0]*d[1]-w[1]*d[0])
