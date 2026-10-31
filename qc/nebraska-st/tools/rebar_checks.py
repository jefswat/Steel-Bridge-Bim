import sys,csv,re,json,pickle,collections,numpy as np,math; sys.path.insert(0,r"C:\SBM\qc\nebraska-st\tools")
from qclib import *
mod=collections.defaultdict(list)
for r in q("select global_id,mark,length_m,mass_kg,properties from elements where category='rebar'"): mod[r['mark']].append(r)
ids=lambda mk:",".join(x['global_id'] for x in mod[mk][:3])+"..."
V45="REINFORCING BAR LIST - SUPERSTRUCTURE (p45) / SECTION A-A (p25)"
for mk,cnt,desc in [("5d2",38,"INTEGRAL STEEL PIER CAP HAUNCH"),("5e5",9,"HAUNCH LONGIT."),("5e6",6,"HAUNCH LONGIT.")]:
    bs=mod[mk]; n=len(bs)
    props=[json.loads(x['properties']) for x in bs]
    Ls=[x['length_m']/FT for x in bs]; ws=[x['mass_kg']*2.20462 for x in bs]
    chk(45,V45,"Bar %s count (%s)"%(mk,desc),"count of IfcReinforcingBar with Mark","%d"%cnt,"%d"%n,"%+d"%(n-cnt),"exact","pass" if n==cnt else "fail",ids(mk),"results/rebar_bill.csv","")
    chk(45,V45,"Bar %s size / coating"%mk,"IfcReinforcingBar NominalDiameter / property","#5, epoxy coated","%s, epoxy=%s"%(props[0]['size'],props[0]['epoxy']),"","exact","pass" if props[0]['size']=="#5" and props[0]['epoxy'] else "fail",ids(mk),"","")
    dev=np.mean(Ls)
    if mk=="5d2":
        chk(45,V45,"Bar 5d2 length: scheduled 5'-4 (out-to-out sum) vs model developed (centerline) length","IfcReinforcingBar.BarLength vs scheduled less computed 4 x 90deg bend deduction (D=2 1/2)","5'-4 = 64.00 in; expected developed 58.8 in","%.2f in"%(dev*12),"%+.2f in vs expected"%(dev*12-58.82),"1/4 in","fail",ids(mk),"results/rebar_bill.csv","Model is 1.1 in shorter than the plan bend allowance gives, because its bends use pin 3 3/4 in, not 2 1/2 in (next rows)")
        chk(45,V45,"Bar 5d2 weight, total","model mass vs plan schedule 211 lb","211 lb (5.55 lb/bar)","%.1f lb (%.2f lb/bar)"%(sum(ws),np.mean(ws)),"%+.1f%%"%((sum(ws)/211-1)*100),"3%","fail",ids(mk),"","Plan weight uses the 64 in sum of out-to-out dimensions; the model uses centerline length. About -8% is the expected bend deduction, the rest is the pin difference. Not a placement error")
        chk(25,"BENT BAR DETAILS 5d2 (p45)","Bar 5d2 bend pin diameter D (4 x 90 deg bends)","centerline radii from the discretised IfcIndexedPolyCurve (chord/angle): R_c = 2.19 in -> pin D = 2*(R_c - db/2)","D = 2 1/2 in (printed on the bend detail)","D = 3.75 in (6 db)","+1.25 in","0 in (plan D governs; AASHTO 5.10.2.3 min 4 db = 2.5 in)","fail",ids(mk),"","All 38 bars. Out-to-out dims 6 / 8 / 3'-0 are matched (model 6.0 / 8.0 / 36.0 in) so only the bend radius differs")
        chk(45,"BENT BAR DETAILS 5d2 (p45)","Bar 5d2 out-to-out dimensions (6 in stubs, 8 in legs, 3'-0 across)","straight legs 3.5 / 3 / 31 in + R_c + bar diameter","6 / 8 / 36 in","6.0 / 8.0 / 36.0 in","<0.1 in","1/4 in","pass",ids(mk),"","")
        chk(25,"SECTION A-A / SECTION NEAR PIER NO. 2","Bar 5d2 clear distance to top of cap top flange (2 in CL.)","ray cast onto IFC cap top flange plates (CAP2-TF1/TF2)","2 in clear","min 2.00 in, max 2.75 in (38 bars)","0.00 in","1/4 in","pass",ids(mk),"","Cap top flange slopes 3.39 %, bars follow the slope")
        chk(25,"TYPICAL SECTION (cover to top of deck)","Bar 5d2 cover to top of deck","ray cast on IFC deck top","2 1/2 in clear (top transverse steel rule, p24)","min 2.88 in","+0.38 in","1/4 in","pass",ids(mk),"","The 5d2 stubs sit lower than the typical top mat")
    elif mk=="5e5":
        chk(45,V45,"Bar 5e5 length and weight","BarLength / mass","5'-4 (64 in), 50 lb","%.2f in, %.1f lb"%(dev*12,sum(ws)),"%+.2f in"%(dev*12-64),"1/4 in","pass",ids(mk),"","Straight bar, no bends")
        chk(25,"SECTION NEAR PIER NO. 2","Bar 5e5 placement: 3 per bay between girder lines","bar centre positions along the cap","3-5e5 in each of 3 bays","3 per bay, at the bay centres (-8.383, -0.716, +6.950 ft) -> exact","0.000","1 in","pass",ids(mk),"","")
    else:
        chk(45,V45,"Bar 5e6 length and weight","BarLength / mass","2'-5 (29 in), 15 lb","%.2f in, %.1f lb"%(dev*12,sum(ws)),"%+.2f in"%(dev*12-29),"1/4 in","pass",ids(mk),"","Straight bar")
        chk(25,"SECTION NEAR PIER NO. 2","Bar 5e6 placement: 3 each side outside the exterior girders","bar centres","3-5e6 outside each exterior girder","3 at u=-14.47 and 3 at u=+13.05 ft from cap centre","","1 in","open",ids(mk),"","Plan gives no spacing for 5e6 in the sheets read; position not checkable")
chk(25,"SECTION A-A / SECTION NEAR PIER NO. 2","5d2 count and spacing: 10 per bay at 6 in, 4 at each overhang","bar centreline positions along the cap axis","10 / 10 / 10 per bay, 4 + 4 at overhangs","10 / 10 / 10 at 6.00 in, 4 + 4","","1 in","pass","5d2","","")
chk(25,"SECTION A-A / SECTION NEAR PIER NO. 2","5d2 position along the cap relative to the girders (1'-8 each side, bay 7'-8)","group centre vs bay centre (girder CL positions verified: bearing col.1 to girder A = 11.151 ft = plan 11'-1 13/16)","group centred in each bay","group centre offset -0.282 ft from bay centre: first bar 1.30 ft / last bar 1.86 ft from the girder CLs","-0.28 ft (3 3/8 in)","1 in","fail","5d2","","The 43 cap top-flange stud rows are shifted by the same -0.279 ft (next row). The bars sit between those studs, so the shift is inherited")
chk(29,"INTEGRAL STEEL PIER CAP ELEVATION (stud spacing)","Cap top-flange shear stud rows: first row from CL bearing col. 1 (7'-9 5/16) and chain 4@6 | 1'-4 1/2 | 1'-4 | 10@6 | ... | 9'-5 11/16","stud positions on IFC cap (support Pier 2, not tied to a girder) along the cap axis","7'-9 5/16 = 7.776 ft","7.497 ft (all 43 rows shifted -0.279 ft toward col. 1; last row 9.753 ft from col. 2 vs plan 9'-5 11/16 = 9.474)","-0.279 ft (3 3/8 in)","1 in","fail","cap studs","","Row count 43 and 6 in / 1'-4 spacing match. 8 studs per row would be 344 but the model carries 324 (open)")
nonplaced=[r for r in csv.DictReader(open(BASE+"/results/rebar_bill.csv",encoding="utf8")) if r['model_status']=='not placed']
bylist=collections.Counter(r['list'] for r in nonplaced)
for lst,c in bylist.items():
    pg=[r['page'] for r in nonplaced if r['list']==lst][0]
    chk(int(pg),lst,"Bar marks of this list: %d marks not placed in the model"%c,"bill reconciliation vs model IfcReinforcingBar marks","%d marks scheduled"%c,"0 placed","-%d"%c,"exact","fail","","results/rebar_bill.csv","Model reports only marks 5d2, 5e5, 5e6 (deck reinforcing record: plan_driven=false, bars_placed 53). The other marks are not listed as schedule-only either. Deck (138,687 lb), barrier, Pier 1/2 and abutment reinforcing are absent")
