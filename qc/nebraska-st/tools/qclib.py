import sqlite3, pickle, numpy as np, math, json, csv, os
FT=0.3048
E0,N0,Z0=2341863.51706037,15443897.6377953,1115.4855643045
BASE=r"C:\SBM\qc\nebraska-st"
_db=sqlite3.connect("file:"+BASE+"/inputs/nebraska-st.sqlite?mode=ro",uri=True); _db.row_factory=sqlite3.Row
def q(sql,*a): return [dict(r) for r in _db.execute(sql,a)]
_geom=None
def geom():
    global _geom
    if _geom is None: _geom=pickle.load(open(BASE+"/cache_geom.pkl","rb"))
    return _geom
def verts_ft(gid):
    """vertices in ft: x=grid E (ft), y=grid N (ft), z=elevation (ft) -- GRID coords (E0/N0 added)"""
    g=geom()[gid]; v=g[2]/FT+np.array([E0,N0,Z0])
    return v
def bbox_ft(gid):
    v=verts_ft(gid); return v.min(0),v.max(0)
# alignment (own evaluation from IFC definitions, local ft)
SEGS=[(643.2507131,-306.4302991,2.6943224601,0.0,424.999558913),
 (260.0578858,-122.6154951,2.6943224601,2514.53,49.2593067895),
 (215.4383289,-101.7469339,2.7139123265,0.0,379.4099999962),
 (-129.7983289,55.6176052,2.7139123265,1500.0,500.6692943916),
 (-611.2958,184.1139823,3.0476918561,0.0,299.999999996)]
# NOTE: SEGS start points above are the IFC local coords (ft). Recorded here from ifcopenshell dump of IfcAlignmentHorizontalSegment
STA0=246800.0
def seg_cum():
    c=[];s=0
    for x in SEGS: c.append(s); s+=x[4]
    return c
CUM=seg_cum()
def align_pt(sta):
    d=sta-STA0
    for i,(x,y,th,R,L) in enumerate(SEGS):
        if d<=CUM[i]+L+1e-9 or i==len(SEGS)-1:
            s=d-CUM[i]
            if R==0: return x+s*math.cos(th), y+s*math.sin(th), th
            # left curve (th increases)
            a=s/R; cx=x-R*math.sin(th); cy=y+R*math.cos(th)
            th2=th+a
            return cx+R*math.sin(th2), cy-R*math.cos(th2), th2
def to_local(E,N): return E-E0, N-N0
def sta_off(E,N):
    """grid ft -> (station ft, offset ft +right) by dense search+refine"""
    x,y=E-E0,N-N0
    best=None
    for sta in np.arange(STA0,STA0+1654,1.0):
        px,py,th=align_pt(sta); d=(x-px)*(-math.sin(th))+(y-py)*math.cos(th); s=(x-px)*math.cos(th)+(y-py)*math.sin(th)
        if best is None or abs(d)<abs(best[1]) and abs(s)<30: pass
    # refine: iterate projection
    sta=STA0+800
    for _ in range(60):
        px,py,th=align_pt(sta); s=(x-px)*math.cos(th)+(y-py)*math.sin(th); sta+=s
    px,py,th=align_pt(sta); left=-(x-px)*math.sin(th)+(y-py)*math.cos(th)
    return sta,-left   # +right
def fmt_sta(s):
    s=round(s,2); return "%d+%05.2f"%(int(s//100),s-100*(s//100))
def fi(inches):
    f=int(inches//12); r=inches-12*f; return "%d'-%s\""%(f,("%.3f"%r).rstrip('0').rstrip('.'))

# ---------- check / extract recording ----------
CHK=BASE+"/results/checks.csv"; EXT=BASE+"/extract/"
CHK_COLS="check_id,sheet_page,sheet_no,view,item,method,value_plan,value_model,diff,tolerance,result,element_ids,evidence,note".split(",")
def _append(path,cols,row):
    new=not os.path.exists(path)
    with open(path,"a",newline="",encoding="utf8") as f:
        w=csv.writer(f)
        if new: w.writerow(cols)
        w.writerow([row.get(c,"") for c in cols])
_ctr={}
def chk(page,view,item,method,vplan,vmodel,diff="",tol="",result="open",ids="",evidence="",note="",cid=None):
    n=_ctr.get(page,0)+1; _ctr[page]=n
    cid=cid or "P%02d-%03d"%(page,n)
    _append(CHK,CHK_COLS,dict(check_id=cid,sheet_page=page,sheet_no=page,view=view,item=item,method=method,value_plan=vplan,value_model=vmodel,diff=diff,tolerance=tol,result=result,element_ids=ids,evidence=evidence,note=note))
def num(page,view,item,method,vplan,vmodel,tol,ids="",evidence="",note="",unit=""):
    d=vmodel-vplan; res="pass" if abs(d)<=tol else "fail"
    chk(page,view,item,method,("%.4f"%vplan).rstrip('0').rstrip('.')+unit,("%.4f"%vmodel).rstrip('0').rstrip('.')+unit,"%+.4f"%d,tol,res,ids,evidence,note)
def ext(name,cols,row):
    _append(EXT+name+".csv",cols,dict(zip(cols,row)))

if os.path.exists(CHK):
    for r in csv.DictReader(open(CHK,encoding="utf8")):
        try: _ctr[int(r["sheet_page"])]=max(_ctr.get(int(r["sheet_page"]),0),int(r["check_id"].split("-")[1]))
        except: pass

def obb(gid):
    """oriented box in plan: returns dict(L,W,dz, ang(deg of long axis), center(E,N,Z), zmin,zmax) using min-area rectangle of convex hull of XY"""
    v=verts_ft(gid); xy=v[:,:2]
    # convex hull
    pts=sorted(set(map(tuple,np.round(xy,5))))
    def cross(o,a,b): return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    lo=[]
    for p in pts:
        while len(lo)>=2 and cross(lo[-2],lo[-1],p)<=0: lo.pop()
        lo.append(p)
    up=[]
    for p in reversed(pts):
        while len(up)>=2 and cross(up[-2],up[-1],p)<=0: up.pop()
        up.append(p)
    h=np.array(lo[:-1]+up[:-1])
    best=None
    for i in range(len(h)):
        e=h[(i+1)%len(h)]-h[i]; a=math.atan2(e[1],e[0]); c,s=math.cos(a),math.sin(a)
        r=h@np.array([[c,-s],[s,c]]); w=r.max(0)-r.min(0); area=w[0]*w[1]
        if best is None or area<best[0]: best=(area,a,w,r.min(0),r.max(0),c,s)
    area,a,w,mn,mx,c,s=best
    L,W=max(w),min(w); ang=math.degrees(a if w[0]>=w[1] else a+math.pi/2)%180
    cx=(mn[0]+mx[0])/2; cy=(mn[1]+mx[1])/2
    ctr=np.array([[c,s],[-s,c]]).T@np.array([cx,cy]) if False else np.array([c*cx - s*cy, s*cx + c*cy])
    return dict(L=L,W=W,ang=ang,E=ctr[0],N=ctr[1],zmin=v[:,2].min(),zmax=v[:,2].max(),dz=v[:,2].max()-v[:,2].min())
def els(where="1=1",*a): return q("select global_id,name,mark,category,subtype,support,girder,unit,mass_kg,volume_m3,properties from elements where "+where,*a)

_kids=None
def kids(gid,deep=True):
    global _kids
    if _kids is None:
        _kids={}
        for r in q("select global_id,parent_global_id from elements"):
            _kids.setdefault(r['parent_global_id'],[]).append(r['global_id'])
    out=[]
    for k in _kids.get(gid,[]):
        out.append(k)
        if deep: out+=kids(k)
    return out
def union_bbox(gids):
    lo=None
    for g in gids:
        if g not in geom(): continue
        a,b=bbox_ft(g)
        lo=a if lo is None else np.minimum(lo,a); hi=b if lo is None or 'hi' not in dir() else None
    return None
def ubox(gids):
    pts=[verts_ft(g) for g in gids if g in geom()]
    v=np.vstack(pts); return v.min(0),v.max(0)
OFFS={'G1':-18.5,'G2':-10.8333333,'G3':-3.1666667,'G4':4.5}
_path={}
def gpath(g):
    if g in _path: return _path[g]
    o=OFFS[g]; sts=np.arange(STA0+300,STA0+1500,0.05); P=[]
    for s in sts:
        x,y,th=align_pt(s); P.append((x-o*0+(-math.sin(th))*(-o), y+math.cos(th)*(-o)))
    # offset +right: right normal = (sin th, -cos th); left normal=(-sin,cos); point=CL+ o*right = CL + o*(sin th,-cos th)
    P=[]
    for s in sts:
        x,y,th=align_pt(s); P.append((x+o*math.sin(th), y-o*math.cos(th)))
    P=np.array(P); seg=np.hypot(*np.diff(P,axis=0).T); cum=np.concatenate([[0],np.cumsum(seg)])
    _path[g]=(sts,P,cum); return _path[g]
def garc(g,E,N):
    """arc length along girder g path (ft) from sta STA0+300 start, of the projection of grid point"""
    sts,P,cum=gpath(g); d=np.hypot(P[:,0]-(E-E0),P[:,1]-(N-N0)); i=int(d.argmin()); return cum[i],sts[i]

def tris_of(ids):
    out=[]
    for i in ids:
        if i in geom(): out.append(verts_ft(i)[geom()[i][3]])
    return np.vstack(out)
def topz(tri,E,N,which='max'):
    A=tri[:,0];B=tri[:,1];C=tri[:,2]
    d=(B[:,1]-C[:,1])*(A[:,0]-C[:,0])+(C[:,0]-B[:,0])*(A[:,1]-C[:,1])
    ok=np.abs(d)>1e-12; dd=np.where(ok,d,1)
    l1=((B[:,1]-C[:,1])*(E-C[:,0])+(C[:,0]-B[:,1]*0-C[:,0])*0+(C[:,0]-B[:,0])*(N-C[:,1]))/dd
    l2=((C[:,1]-A[:,1])*(E-C[:,0])+(A[:,0]-C[:,0])*(N-C[:,1]))/dd
    l3=1-l1-l2
    m=ok&(l1>=-1e-9)&(l2>=-1e-9)&(l3>=-1e-9)
    if not m.any(): return None
    z=l1*A[:,2]+l2*B[:,2]+l3*C[:,2]; return z[m].max() if which=='max' else z[m].min()

def purge(pred):
    rows=list(csv.DictReader(open(CHK,encoding="utf8")))
    keep=[r for r in rows if not pred(r)]
    with open(CHK,"w",newline="",encoding="utf8") as f:
        w=csv.DictWriter(f,fieldnames=CHK_COLS); w.writeheader(); w.writerows(keep)
    return len(rows)-len(keep)

def patch(cid,**kw):
    rows=list(csv.DictReader(open(CHK,encoding="utf8")))
    for r in rows:
        if r["check_id"]==cid:
            for k,v in kw.items(): r[k]=v
    with open(CHK,"w",newline="",encoding="utf8") as f:
        w=csv.DictWriter(f,fieldnames=CHK_COLS); w.writeheader(); w.writerows(rows)
