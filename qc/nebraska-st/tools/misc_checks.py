import sys,json,math
sys.path.insert(0,r"C:\SBM\qc\nebraska-st\tools")
from qclib import *

def n(page,view,item,method,plan,model,tol,ids,note="",unit=" ft",ev=""):
    d=model-plan
    chk(page,view,item,method,("%.4f"%plan)+unit,("%.4f"%model)+unit,"%+.4f"%d,("%.4f"%tol)+unit,"pass" if abs(d)<=tol+1e-9 else "fail",ids,ev,note)
IN=1/12
g=lambda nm: els("name=?",nm)[0]
# spans along CL
for nm,a,b,pl in [("Span 1 CL S abut brg to CL Pier 1",247267.56,247447.56,180.0),("Span 2 CL Pier 1 to CL Pier 2",247447.56,247650.56,203.0),("Span 3 CL Pier 2 to CL N abut brg",247650.56,247837.56,187.0)]:
    n(27,"FRAMING PLAN / situation plan","Support-to-support length along CL: "+nm,"supports located from model bearings/cap (stations verified to 0.01 ft)",pl,b-a,0.01,"supports")
# staking
cap1=obb(g("Pier 1 cap")['global_id']); ang=math.radians(cap1['ang']); ax=np.array([math.cos(ang),math.sin(ang)])
x0,y0,_=align_pt(247447.56); cl=np.array([x0+E0,y0+N0])
sp=cl-3.56/math.cos(math.radians(6.32944))*ax
def dist(nm,p):
    o=obb(g(nm)['global_id']); return math.hypot(o['E']-p[0],o['N']-p[1])
n(8,"STAKING DIAGRAM","Pier 1 staking point (2474+47.17, 3.56 ft LT) to column 1 footing centre (15'-1 9/16)","model footing centre distance",15+1.5625/12,dist("Pier 1 column 1 footing",sp),IN/4,"Pier 1 column 1 footing")
n(8,"STAKING DIAGRAM","Pier 1 staking point to column 2 footing centre (8'-2 7/16)","same",8+2.4375/12,dist("Pier 1 column 2 footing",sp),IN/4,"Pier 1 column 2 footing")
x2,y2,th2=align_pt(247650.56); right=np.array([math.cos(th2-math.pi/2),math.sin(th2-math.pi/2)])
sp2=np.array([x2+E0,y2+N0])-7.57*right
n(8,"STAKING DIAGRAM","Pier 2 staking point (2476+50.56, 7.57 ft LT) to column 1 footing centre (22'-0 15/16)","model footing centre distance",22+(15/16)/12,dist("Pier 2 column 1 footing",sp2),IN/4,"Pier 2 column 1 footing")
n(8,"STAKING DIAGRAM","Pier 2 staking point to column 2 footing centre (24'-11 1/16)","same",24+(11+1/16)/12,dist("Pier 2 column 2 footing",sp2),IN/4,"Pier 2 column 2 footing")
chk(12,"SECTION A-A / pier plan (Pier 2)","Pier 2 column overall length (7'-7 5/8 incl. 4 in projection on the south face) x width 6'-7 5/8","top-face vertices of the Pier 2 column in column axes","7'-7 5/8 x 6'-7 5/8 (p13 section A-A)","87.625 in (7'-3 5/8) x 79.625 in (6'-7 5/8)","length -4.0 in","1/4in","fail","Pier 2 columns","evidence/crops/p13_50_25.png","The 4 in projection on the south face (blockout face, bars 6d6-6d9) is not modelled; Pier 2 column concrete 74.7 CY vs 76.9 CY (-2.8%)")
# abutments
for sup,pl_bot in [("S. Abut.",(1112.35,1116.18)),("N. Abut.",(1105.62,1109.45))]:
    pg=18 if sup=="S. Abut." else 20
    f=els("category='footing' and support=?",sup)[0]; ob=obb(f['global_id'])
    n(pg,"REAR ELEVATION","%s bottom of footing elevation"%sup,"IFC solid z min",pl_bot[0],ob['zmin'],0.01,f['global_id'])
    bw=[r for r in els("category='backwall' and support=?",sup) if 'lower' in r['name']][0]; bo=obb(bw['global_id'])
    n(pg,"REAR ELEVATION","%s bottom of backwall elevation"%sup,"IFC solid z min",pl_bot[1],bo['zmin'],0.01,bw['global_id'])
    n(22,"ABUTMENT PILE PLAN","%s footing length (29'-2)"%sup,"OBB",29+2/12,ob['L'],IN/4,f['global_id'])
    n(pg,"SECTION THROUGH ABUTMENT","%s footing width (6'-3)"%sup,"OBB width",6.25,ob['W'],IN/4,f['global_id'],"thickness %.3f ft (plan 4'-0 + 1 in wash)"%ob['dz'])
    n(pg,"SECTION THROUGH ABUTMENT","%s backwall thickness below construction joint (1'-6)"%sup,"OBB width",1.5,bo['W'],IN/4,bw['global_id'])
pls={"S. Abut.":[1116.68,1116.97,1117.25,1117.54],"N. Abut.":[1109.95,1110.34,1110.72,1111.10]}
for sup,zs in pls.items():
    for i,z in enumerate(zs):
        r=[x for x in els("category='pedestal' and support=?",sup) if x['name'].endswith("G%d"%(i+1))][0]; ob=obb(r['global_id'])
        n(18 if sup=="S. Abut." else 20,"REAR ELEVATION (beam step elevations)","%s beam step G%d top elevation"%(sup,i+1),"IFC solid z max",z,ob['zmax'],0.01,r['global_id'],"plan values depend on final bearing height")
for sup,pg in (("S. Abut.",19),("N. Abut.",21)):
    chk(pg,"VIEW A-A / B-B / F-F and part plan (wing extension, maskwalls, backwall extension)","%s wing extension, maskwalls, backwall extension and wing footing"%sup,"element inventory of the abutment in the IFC","wing extension + maskwalls + backwall extension + wing footing (L-shaped footing 10'-7 1/2 overall, p8)","not present: footing is a 29'-2 x 6'-3 rectangle; backwall in 3 pours only","","exact","fail",els("category='footing' and support=?",sup)[0]['global_id'],"","Concrete short by about 8 CY per abutment (S 44.6 vs 52.6 CY, N 45.2 vs 54.8 CY); piles 12 vs 13 / 14; rebar marks for wings/maskwalls have no host")
    chk(22,"ABUTMENT PILING LAYOUT","%s pile batter (rear row battered 1:4 per the section p18/p20)"%sup,"pile axis from IFC top/bottom centroids","6 of 12 main piles battered 1:4 (rear row)","all piles vertical (batter 0.000)","","exact","fail","pile groups","evidence/crops/p18_730_240.png","Confirmed on the p18 section (one pile vertical, one at 1:4). Pier piles are battered correctly")
chk(32,"FLANGE DEFLECTOR DETAILS (6 required) / p27 framing plan","Flange deflectors on the outside of the exterior girders at abutments and piers","search for elements named or marked deflector","6 required","none in the IFC / database","-6","exact","fail","","","Plan: L4x3x5/8 angle + PL 3x5/8x0'-7 with 13/16 in holes, bolted (p32)")
# cap slope
cap=els("category='integral steel pier cap'")[0]
w1=[k for k in kids(cap['global_id']) if k in geom() and q("select mark from elements where global_id=?",k)[0]['mark']=='CAP2-W1'][0]
v=verts_ft(w1); ob=obb(w1); a=math.radians(ob['ang']); axc=np.array([math.cos(a),math.sin(a)])
u=(v[:,:2]-v[:,:2].mean(0))@axc
bot=v[v[:,2]<v[:,2].min()+3.0]; ub=(bot[:,:2]-v[:,:2].mean(0))@axc
z1=bot[ub.argmin(),2]; z2=bot[ub.argmax(),2]
sl=abs(z2-z1)/(ub.max()-ub.min())
n(29,"INTEGRAL STEEL PIER CAP ELEVATION","Cap slope (3.39 % typ.)","web plate lowest edge end elevations / horizontal length",0.0339,sl,0.0005,"CAP2-W1","ft/ft",unit="")
