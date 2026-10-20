import sys, json; sys.path.insert(0,'.'); from qc import *
E=load_el(); M={r['mark']:r for r in E if r['mark']}
def thr(mark):
    v,_=geom(M[mark]['guid']); a=np.arctan2(v[:,1]-C[1],v[:,0]-C[0]); return a
feat={}
for g in ['G1','G2']:
    Rg=RG[g]; F={}
    for i in range(1,5):
        b=M['B%d-%s-PAD'%(i,g)]; F['BRG%d'%i]=polar(b['cx'],b['cy'])[0]
    for k in range(1,5):
        ths=[polar(M[m]['cx'],M[m]['cy'])[0] for m in M if m.startswith('FS%d-%s-SP'%(k,g)) and ('SP7' in m or 'SP8' in m or 'SP9' in m or 'SP10' in m)]
        F['FS%d'%k]=float(np.mean(ths))
    # plates
    for kind in ['BF','TFL','TFR','WL','WR']:
        for s in range(1,6):
            for j in range(1,4):
                m='%s-%d-%s%d'%(g,s,kind,j)
                if m in M:
                    a=thr(m); F[m]=(float(a.min()),float(a.max()))
    feat[g]=F
json.dump(feat,open('girder_feat.json','w'),indent=0)
for g in feat:
    Rg=RG[g]; b1=feat[g]['BRG1']
    for k,v in feat[g].items():
        if isinstance(v,float): print(g,k,fmt_ftin(Rg*(v-b1)))
        elif 'BF' in k: print(g,k,fmt_ftin(Rg*(v[0]-b1)),fmt_ftin(Rg*(v[1]-b1)))
