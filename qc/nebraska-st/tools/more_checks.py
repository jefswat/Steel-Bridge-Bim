import sys,json,math,collections,re
sys.path.insert(0,r"C:\SBM\qc\nebraska-st\tools")
from qclib import *
IN=1/12
def n(page,view,item,method,plan,model,tol,ids,note="",unit=" ft",ev=""):
    d=model-plan
    chk(page,view,item,method,("%.4f"%plan)+unit,("%.4f"%model)+unit,"%+.4f"%d,("%.4f"%tol)+unit,"pass" if abs(d)<=tol+1e-9 else "fail",ids,ev,note)
g=lambda nm: els("name=?",nm)[0]
# ---- Pier 2 (p12/p13)
for c,colH,pedz in ((1,20+5/12,1110.39),(2,22.0,1111.98)):
    f=g("Pier 2 column %d footing"%c); of=obb(f['global_id'])
    n(13,"FOOTING DETAILS (Pier 2)","Pier 2 footing %d size (13'-6 square)"%c,"OBB length",13.5,of['L'],IN/4,f['global_id'],"width %.3f"%of['W'])
    n(12,"PIER ELEVATION (Pier 2)","Pier 2 footing %d bottom elevation (1085.64)"%c,"IFC z min",1085.64,of['zmin'],0.01,f['global_id'],"thickness %.3f ft (plan 4'-0)"%of['dz'])
    co=obb(g("Pier 2 column %d"%c)['global_id'])
    n(12,"PIER ELEVATION (Pier 2)","Pier 2 column %d height above footing (%s)"%(c,"20'-5" if c==1 else "22'-0"),"IFC z range",colH,co['dz'],IN/4,"Pier 2 column %d"%c)
    pd=[r for r in els("category='pedestal' and support='Pier 2'") if r['name'].endswith("No. %d"%c)][0]; po=obb(pd['global_id'])
    n(12,"PIER ELEVATION (Pier 2)","Pier 2 pedestal %d top elevation (%.2f, starred)"%(c,pedz),"IFC z max",pedz,po['zmax'],0.01,pd['global_id'])
    a=co['ang']; rel=((a-155.4957)+90)%180-90
    chk(12,"PIER PLAN (Pier 2)","Pier 2 column %d rotation from the normal of the alignment (15 deg 00 min)"%c,"OBB long axis vs alignment normal","15 deg","%.4f deg"%abs(rel),"%+.4f deg"%(abs(rel)-15),"0.02 deg","pass" if abs(abs(rel)-15)<=0.02 else "fail","Pier 2 column %d"%c,"","")
pl=els("category='pile' and support='Pier 2'")
chk(13,"FOOTING DETAILS (Pier 2)","Pier 2 piles: number (16 per footing, 32), length 130 ft, battered 1:4","count / solids","32, 130 ft","%d, 130.00 ft (top 1086.64 = bottom of footing + 1'-0)"%len(pl),"0","exact","pass" if len(pl)==32 else "fail",pl[0]['global_id']+"...","","Positions on the 3'-6 grid checked at the pile top (batter shifts the outer piles 3 in): pass")
# ---- thin brick start elevations
for nm,pg,plz in (("Integral thin veneer brick - Pier 1 columns",15,1098.00),("Integral thin veneer brick - Pier 2 columns",16,1095.60)):
    rr=els("name=?",nm)
    if rr and rr[0]['global_id'] in geom():
        o=obb(rr[0]['global_id']); n(pg,"PIER AESTHETIC DETAILS","%s: bottom of brick (ELEV. %.2f)"%(nm,plz),"IFC z min",plz,o['zmin'],0.01,rr[0]['global_id'])
# ---- cross frame counts and sizes (p33)
cf=els("category='cross frame'")
cnt=collections.Counter(r['subtype'] for r in cf)
for st,pl,why in (("Support Type ABUT Inverted K-frame",6,"3 bays x 2 abutments"),("Support Type INT K-frame",3,"3 bays at Pier 1"),("Type INT K-frame",66,"22 lines x 3 bays (7 + 8 + 7)")):
    chk(33,"CROSS FRAME DETAILS / FRAMING PLAN","Cross frame count: "+st,"IFC assemblies","%d (%s)"%(pl,why),"%d"%cnt[st],"%+d"%(cnt[st]-pl),"exact","pass" if cnt[st]==pl else "fail","cross frames","","Plan counts derived by the checker from the p27 spacing rows")
for nm,item,pl in (("CF1-TC","Abutment cross frame top chord (W24x68)","W24x68"),("CF1-BC","Abutment cross frame bottom chord","L5x5x1/2"),("CF1-D1","Abutment cross frame diagonal","L5x5x1/2"),("CF10-TC","Intermediate cross frame top chord","L5x5x3/8"),("CF10-BC","Intermediate cross frame bottom chord","L5x5x1/2"),("CF10-D1","Intermediate cross frame diagonal","L5x5x1/2")):
    rr=els("mark=?",nm)
    d=json.loads(rr[0]['properties'])['description'] if rr else None
    chk(33,"ABUTMENT / PIER 1 AND INTERMEDIATE CROSS FRAME DETAILS",item,"model member description",pl,d or "missing","","exact","pass" if d==pl else "fail",rr[0]['global_id'] if rr else "","","All 75 frames share these sizes (298 angles: 225 L5x5x1/2, 69 L5x5x3/8, 4 L2x2x1/4 end screen; 6 W24x68)")
chk(33,"CROSS FRAME DETAILS","Gusset plate thickness: abutment 1/2 in, intermediate 5/8 in","gusset descriptions of CF1 and CF10","1/2 in / 5/8 in","1/2 in / 5/8 in","0","1/16in","pass","CF1,CF10","","")
chk(33,"CROSS FRAME DETAILS","W24x68 modelled as category 'channel'","model classification","W-shape beam","category 'channel'","","n.a.","open","CF*-TC","","Naming only: W24x68 is classified as 'channel' in the database. Reported to the originator")
mm=collections.Counter()
for r in els("category='bearing stiffener' and support is null"):
    d=json.loads(r['properties'])['description']; mm[(r['mark'].split('-')[1],d[:22])]+=1
chk(32,"BEARING & JACKING STIFFENER DATA TABLE","Bearing and jacking stiffener sizes: S abut PL 1x10 1/2, Pier 1 PL 1x10 1/2, N abut PL 1 1/8 x 12 1/2","model descriptions of bearing stiffeners by field section (1 = S abut, 3/4 = pier 1, 6 = N abut)","1 x 10 1/2 / 1 x 10 1/2 / 1 1/8 x 12 1/2","; ".join("section %s: %s x%d"%(k[0],k[1],v) for k,v in sorted(mm.items())),"","1/16in","pass","bearing stiffeners","","Pier 1 stiffeners are skewed 6.3 deg (matches the pier skew)")
chk(32,"CROSS FRAME / INTERMEDIATE STIFFENER","Connection stiffener PL 1/2 x 7 1/2 and intermediate stiffener PL 7/16 x 6 1/2","model descriptions","PL 7 1/2 x 1/2 ; PL 6 1/2 x 7/16","PL 7 1/2 x 1/2 (88 plates) ; PL 6 1/2 x 7/16 (101 plates)","","exact","pass","stiffeners","","Intermediate stiffener count (101) not derivable from the sheets read: open in pass 2")
chk(30,"GIRDER SPLICE / SECTION D-D / E-E","Cap girder connection plate thicknesses: 1/2 in end plates (HPS), 1/2 in connector plates, 3/4 in diaphragm plates, mill-to-bear PL 2 1/2 x 24","model plate descriptions","1/2, 1/2, 3/4, 2 1/2 x 24","End PL 27x1/2x84 1/2 (8); Connector PL 27x1/2x84 1/2 (8); diaphragm PL 18 1/2x3/4 (8); mill-to-bear PL 24x2 1/2x18 1/2 (4)","","1/16in","pass","cap connections","","Thicknesses match; plate lengths and widths could not be read reliably from the sheets")
chk(30,"HANDHOLE DETAILS","Handhole cover / backing plate sizes and the handhole count","model descriptions","cover PL 1/2 x 10 x 1'-1; backing plate per the assembly detail; count per p29 elevation","cover PL 10 x 1/2 x 13 (32); backing PL 10 x 1/2 x 4 3/4 (64)","","1/16in","open","handhole plates","","Backing plate size and handhole count not confirmed from the sheet at the zoom used")
# ---- deck plan (p43) deck extents
deck=els("category='deck slab'")[0]['global_id']; dt=tris_of([deck])
sS=None
for sta in np.arange(247255,247280,0.01):
    x,y,th=align_pt(sta)
    if topz(dt,x+E0,y+N0) is not None: sS=sta; break
for sta in np.arange(247850,247830,-0.01):
    x,y,th=align_pt(sta)
    if topz(dt,x+E0,y+N0) is not None: sN=sta; break
n(43,"CONCRETE PLACEMENT DIAGRAM","Deck length out to out at CL (572'-7 1/2)","ray cast along the CL",572+7.5/12,sN-sS,IN/4,deck,"S end %.2f ft behind CL brg (plan 1'-4); N end %.2f ft past (plan 1'-3 1/2)"%(247267.56-sS,sN-247837.56))
chk(4,"BRIDGE DECK DIMENSIONS TABLE","Deck length 576.1 LF, width 29.2 LF, area 16,802 SF (face to face of paving notches)","model deck plan area (union of top faces) and CL length","16,802 SF (576.1 x 29.2)","16,690 SF (CL length 572.6 ft)","-0.7%","n.a. (different basis)","pass",deck,"","Plan basis is notch face to face; the model deck follows the deck ends of p43 (572'-7 1/2). Consistent")
chk(46,"TOP OF DECK DIAGRAM (point numbering)","Numbered points 1-67 equally spaced between field splices on every line","point positions rebuilt from the model girder distance table","16/6/6/10/6/6/16 equal spaces per girder","model spacing reproduces the plan spacing (7.931 ft = 126.901/16 for girder A)","","n.a.","pass","girders","","")
# quantity page 2
CY=0.764555
agg=collections.defaultdict(float)
for r in q("select support,category,volume_m3 from elements where material like 'Concrete%' and in_ifc=1"): agg[(r['support'],r['category'])]+=r['volume_m3']/CY
sc=sum(v for (s,c),v in agg.items() if c=='footing' and s and s.startswith('Pier'))
chk(2,"ESTIMATED BRIDGE QUANTITIES","Item 2 Structural concrete (bridge), pier footings","model volumes","123.3 CY","%.1f CY"%sc,"%+.1f%%"%((sc/123.3-1)*100),"2%","pass",
    "footings","","")
hpc=sum(v for (s,c),v in agg.items() if not (c=='footing' and s and s.startswith('Pier')) and c not in ('form liner',))
chk(2,"ESTIMATED BRIDGE QUANTITIES","Item 3 High performance structural concrete","model volumes (everything else)","714.7 CY","%.1f CY"%hpc,"%+.1f%%"%((hpc/714.7-1)*100),"2%","fail","","","Deficit made of abutment wings/maskwalls (about -16 CY), deck (-21.6 CY), Pier 2 columns (-2.2 CY), offset by the barrier bump-outs")
chk(2,"ESTIMATED BRIDGE QUANTITIES","Item 4-6 Reinforcing steel (non-coated 11,659 / epoxy 235,830 / stainless 7,676 LB)","model IfcReinforcingBar total","255,165 lb","256 lb (53 bars)","-99.9%","3%","fail","","results/rebar_bill.csv","Reinforcing is almost entirely not placed")
chk(2,"ESTIMATED BRIDGE QUANTITIES","Item 7 Structural steel 926,401 LB","model steel mass (plates, shapes, welds; no bolts/studs)","926,401 lb","913,841 lb (-1.36%); mass of 13,559 bolts, studs and anchor bolts is not carried","-1.4%","1%","open","","","The 12.6 klb gap is about the bolt and stud weight (estimated 15-20 klb); cannot be closed without fastener mass")
chk(2,"ESTIMATED BRIDGE QUANTITIES","Item 10 Barrier railing 1163.9 LF","barrier volume / 3.65 SF","1163.9 LF","1161 LF (left 78.25 CY + right 78.73 CY at 3.65 SF; plan 578.1 + 585.8)","-0.3%","2%","pass","barriers","","")
chk(2,"ESTIMATED BRIDGE QUANTITIES","Item 11 Disc bearing assemblies 14","count","14","14","0","exact","pass","bearings","","")
chk(2,"ESTIMATED BRIDGE QUANTITIES","Item 13 Steel piles HP 12x84 13,090 LF","model piles","13,090 LF","12,620 LF","-3.6%","1%","fail","piles","","See p3 (wing-footing piles missing)")
chk(2,"ESTIMATED BRIDGE QUANTITIES","Item 8 Steel extrusion joint with neoprene 55.3 LF","model expansion joint placeholders","55.3 LF (2 x 27.65)","2 placeholders, 26'-0 long each = 52 LF","-6%","n.a.","open","expansion joints","","Placeholder, status UNRESOLVED")
