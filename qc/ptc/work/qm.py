import pickle,numpy as np
R=pickle.load(open(r'C:\SBM\qc\ptc\work\sub.pkl','rb'))
def find(sub,cat=None):
    return [(g,v) for g,v in R.items() if sub in v[0][1] and (cat is None or v[0][0]==cat)]
def ext(P,z0=-1e9,z1=1e9):
    m=(P[:,2]>=z0)&(P[:,2]<=z1); Q=P[m]
    if len(Q)==0: return None,None
    return Q.min(0),Q.max(0)
def sl(name,cat,z0,z1):
    out=[]
    for g,v in find(name,cat):
        mn,mx=ext(v[1],z0,z1)
        if mn is None: continue
        out.append((v[0][1],mx-mn,mn,mx))
    return out
