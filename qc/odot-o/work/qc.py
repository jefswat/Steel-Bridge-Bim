import numpy as np, csv, math, pickle
FT=0.3048
E0,N0,Z0=2113517.06036745,193241.469816273,1115.4855643045
P0=np.array([77.7733739,-117.8966176]); D0=np.array([0.3881140323,0.9216113595]); R=881.47; STA0=30133.72; L=549.53
th0=math.atan2(D0[1],D0[0])
C=P0+R*np.array([-D0[1],D0[0]])  # left turn center
def align_pt(sta):
    s=sta-STA0; th=th0+s/R
    p=C+R*np.array([math.sin(th),-math.cos(th)])
    return p, np.array([math.cos(th),math.sin(th)])
def sta_off(x,y):
    v=np.array([x,y])-C; r=np.linalg.norm(v); ang=math.atan2(v[1],v[0])
    # point on circle at angle ang corresponds to th = ang+pi/2
    th=ang+math.pi/2; s=(th-th0)*R
    return STA0+s, R-r   # offset + = left (toward center)
def profile(sta):
    s=sta-STA0
    if s<=36.28: return 20.4274397+Z0 -0.0293*s
    if s<=236.28:
        u=s-36.28; return 19.3644357+Z0-0.0293*u+(0.0236/(2*200))*u*u
    return 15.8644357+Z0-0.0057*(s-236.28)
def NE(x,y): return N0+y, E0+x
def fmt_ftin(ft):
    sgn='-' if ft<0 else ''; ft=abs(ft); i=ft*12; f=int(i//12); r=i-12*f
    return f"{sgn}{f}'-{r:.3f}\""
def load_el():
    R_=list(csv.DictReader(open(r'C:/SBM/qc/odot-o/work/elements.csv',encoding='utf-8')))
    for r in R_:
        for k in ['xmin','ymin','zmin','xmax','ymax','zmax','cx','cy','cz','d1','d2','d3']:
            if r.get(k): r[k]=float(r[k])/FT
        if r.get('vol'): r['vol']=float(r['vol'])/FT**3
    return R_
_G=None
def geom(guid):
    global _G
    if _G is None: _G=pickle.load(open(r'C:/SBM/qc/odot-o/work/geom.pkl','rb'))
    v,f=_G[guid]; return v/FT,f
RG={'G1':868.140625,'G2':885.807292}
def polar(x,y):
    v=np.array([x,y])-C; return math.atan2(v[1],v[0]), math.hypot(*v)
def theta_sta(sta):  # angle at station
    th=th0+(sta-STA0)/R; return th-math.pi/2
