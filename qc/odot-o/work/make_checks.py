# Builds extract/*.csv, results/checks.csv, results/issues.csv from the checker's own extraction and measurements.
# All model values were measured from the IFC (IfcOpenShell geometry, see work/*.py and evidence/*) unless 'method' says otherwise.
import csv, os
Q = r'C:/SBM/qc/odot-o/'
EV = 'evidence/'
checks = []
def add(page, sheet, view, item, method, vp, vm, diff, tol, result, elems, evidence, note=''):
    checks.append(dict(check_id='C%03d' % (len(checks) + 1), sheet_page=page, sheet_no=sheet, view=view, item=item, method=method,
                       value_plan=vp, value_model=vm, diff=diff, tolerance=tol, result=result, element_ids=elems, evidence=evidence, note=note))
GEO = 'IfcOpenShell geometry (arc length about the IFC alignment curve centre, R_girder)'
# ---------------- p254 O-1 general plan & elevation
add(254,'O-1','Curve No. 9 data','Horizontal curve radius R','IfcAlignmentHorizontalSegment radius','881.47 ft','881.47 ft','0','0.01 ft','pass','IfcAlignment CL Ramp 44F','work/qc.py')
add(254,'O-1','Profile grade data Ramp 44F','PVC 301+70.00 elev 1134.85 / PVI 302+70.00 1131.92 / PVT 303+70.00 1131.35, 200 ft VC, -2.93% / -0.57%','IfcAlignmentVertical segments + MapConversion OrthogonalHeight','as printed','1134.850 / 1131.920 / 1131.350; L=200; g=-2.93/-0.57','0.000','0.005 ft','pass','IfcAlignment','work/qc.py profile()')
add(254,'O-1','Elevation - PGL at supports','PGL elev at P13 1135.33, P14 1132.14, P15 1130.87, P16 1129.96, 306+74 1129.62','gradient curve evaluated at station','1135.33/1132.14/1130.87/1129.96/1129.62','1135.327/1132.139/1130.871/1129.954/1129.617','max -0.006','0.005 ft (half of printed 0.01)','pass','IfcAlignment','work/qc.py','P16 -0.006 is the plan rounding; computed profile reproduces PVC/PVI/PVT exactly')
add(254,'O-1','Elevation - span lengths along PGL','Span 14 139\'-3 3/8", span 15 161\'-0", span 16 161\'-0"','station differences of the model CL brg / WP points','139.281 / 161.000 / 161.000 ft','139.280 (WP 301+53.72 to 302+93.00) / 161.000 / 161.000','0.001 ft','1/16"','pass','B2-*,B3-* bearing stations','evidence/girder_positions.txt')
add(254,'O-1','Material notes','Structural steel M270 Grade 50W; Class AA concrete f\'c 4,000 psi','IfcMaterial names','M270 Gr50W / 4 ksi','girders, members: AASHTO M270 Gr. 50W; deck: f\'c 4 ksi','-','exact','pass','IfcPlate/IfcMember/IfcSlab materials','work (material query)')
add(254,'O-1','Plan/elevation','Superstructure type: 2-50" steel tub girders spans 14-16','IFC element count / girder depth','2 tubs, 50" web depth','2 girders G1/G2, web 50" vertical (51 9/16" on slope)','0','-','pass','G1,G2','evidence/S1_tub_section_side_by_side.png')
add(254,'O-1','Loading / design notes','HL-93 loading, AASHTO LRFD, 20 psf FWS','-','-','-','-','-','n.a.','-','-','design data, not geometry')
# ---------------- p255 O-1A pay quantities, p256-258 foundation
add(255,'O-1A','Summary of pay quantities','Pay items (structural steel, concrete, bearings)','see p271 superstructure quantities','-','-','-','-','n.a.','-','-','duplicated on O-17 (checked there); substructure items out of model scope')
for p,s in [(256,'O-2'),(257,'O-3'),(258,'O-4')]:
    add(p,s,'Foundation report','boring logs','-','-','-','-','-','n.a.','-','-','n.a. - out of model scope (substructure)')
# ---------------- p259 O-5 staking
for name,sta,N,E,mn,me in [('P14',302.93,193275.14,2113643.09,193275.140,2113643.090),('P15',304.54,193434.63,2113663.38,193434.630,2113663.384),('P16',306.15,193595.16,2113654.37,193595.154,2113654.371)]:
    add(259,'O-5','Substructure staking diagram','N/E of %s working point (CL Ramp 44F, STA %s)'%(name,sta),'IfcAlignment curve point at station + IfcMapConversion (E0 2113517.060, N0 193241.470, ft)','N%.2f E%.2f'%(N,E),'N%.3f E%.3f'%(mn,me),'dN %.3f dE %.3f'%(mn-N,me-E),'0.01 ft','pass','IfcAlignment / IfcMapConversion','work/qc.py')
add(259,'O-5','Substructure staking diagram','N/E of Abut. 1 F.F. bkwl WP STA 306+73.25 (alignment beyond the steel unit)','same','N193652.45 E2113643.91','N193652.448 E2113643.918','dN -0.002 dE 0.008','0.01 ft','pass','IfcAlignment','work/qc.py')
add(259,'O-5','Staking - piles / drilled shafts / abutment','shaft and pile layout','-','-','-','-','-','n.a.','-','-','n.a. - out of model scope (substructure)')
# ---------------- p260-269 substructure
for p,s,t in [(260,'O-6','Abutment 1'),(261,'O-7','Abutment 1'),(262,'O-8','Pier 14'),(263,'O-9','Pier 14'),(264,'O-10','Pier 15'),(265,'O-11','Pier 15'),(266,'O-12','Pier 16'),(267,'O-13','Pier 16'),(268,'O-14','Typical pier'),(269,'O-15','Underdrain')]:
    add(p,s,t+' details','substructure geometry / rebar / anchor bolt layout / pedestal elevations','-','-','-','-','-','n.a.','-','-','n.a. - out of model scope (substructure). Bearing-seat elevation is checked through the H-dimension chain (O-17)')
# ---------------- p270 O-16
add(270,'O-16','Typical deck section spans 14-16','Out-to-out deck width 32\'-0" (20\'-6" left / 11\'-6" right of PGL)','radial IFC cut at STA 303+80 + vertex offsets','32.000 ft; +20.500 / -11.500','31.998 ft; +20.501 / -11.497','0.002 ft','1/4"','pass','DECK','evidence/S1_tub_section_side_by_side.png')
add(270,'O-16','Typical deck section spans 14-16','Slab thickness 8"','ray cast top and bottom of IFC deck at 8 CL-brg points','8"','8.000" (all 8 points)','0','1/4"','pass','DECK','evidence/elevation_chain.txt')
add(270,'O-16','Typical deck section spans 14-16','Cross slope 6% (down to the left / inside of curve)','TOS ray cast at G1 and G2 vs PGL; TF bottoms in section','6.0% down left','6.000% (TOS); 5.99% (top flanges)','0','0.0005 ft/ft','pass','DECK, G*-TF*','evidence/elevation_chain.txt')
add(270,'O-16','Typical deck section spans 14-16','Girder spacing 17\'-8" horiz.; PGL to CL right tub 4\'-4"','section cut STA 303+80: CL of top-of-web points','17.667 ft; 4.333 ft','17.667 ft; G2 CL at -4.337 ft','0.004 ft','1/16"','pass','G1-3-*, G2-3-*','evidence/S1_tub_section_side_by_side.png')
add(270,'O-16','Typical tub girder section','Tub dims 20\'-3 13/16" top webs, 15\'-0 5/16" bottom, 10\'-7" depth','compare with O-19 typical tub section','20\'-3 13/16" / 10\'-7"','8\'-0" / 5\'-11" / 4\'-2" (model follows O-19)','-','-','conflict','G1,G2','evidence/plan_p270_deck.png','PLAN CONFLICT: O-16 tub section (and its notes) belongs to a different, 10\'-7" deep tub; O-19 p.273 and O-1 ("2-50\" steel tub girders") show the 50" tub. Model follows O-19.')
add(270,'O-16','Typical haunch detail steel tub girder','slab 1\'-8 1/4", haunch 1 7/16"','compare with O-19 / O-17','1\'-8 1/4" / 1 7/16"','8" slab / 4 1/2" haunch at CL brg','-','-','conflict','DECK','evidence/haunch.txt','PLAN CONFLICT: O-16 haunch detail values contradict O-19 (8" slab, 4 1/2" haunch) and O-17 longitudinal section. Model follows O-19/O-17.')
add(270,'O-16','Typical deck reinforcing section','E1/EB1/ET/A bars, 2" clr','-','-','not modeled (no IfcReinforcingBar in IFC)','-','-','n.a.','-','-','n.a. - deck rebar not in the stated model scope list; flagged in report as a scope question')
add(270,'O-16','F-shaped parapet / drip bead / water repellant','parapet','-','-','2 IfcRailing present','-','-','n.a.','Left barrier, Right barrier','-','n.a. - barriers out of model scope (but the IFC contains them; not checked)')
# ---------------- p271 O-17
H = [('P13',"5'-10\"",0.007),('P14',"5'-10 5/8\"",0.008),('P15',"5'-11\"",0.008),('P16 span 16',"5'-11 3/8\"",0.007)]
for i,(sup,h,d) in enumerate(H,1):
    add(271,'O-17',"'H' dimension table",'Top of slab to bottom of bearing assembly at CL brg, %s, both girders: %s'%(sup,h),'IFC deck top (ray cast) minus IFC pad bottom at CL brg (computed TOS = PGL - 6% x offset also matches deck to 0.001 ft)',h,'pad bottom %+.3f ft vs TOS-H (G1 and G2)'%d,'%+.3f ft (%.2f in)'%(d,d*12),'1/8" (steel seat)','pass','B%d-G1-PAD, B%d-G2-PAD, DECK'%(i,i),'evidence/elevation_chain.txt','model seat consistently ~0.09" high; within tolerance')
add(271,'O-17','Longitudinal section / typical haunch note','Haunch 4 1/2" at CL brg (bottom of slab to top of web at CL girder)','IFC deck bottom (ray cast) minus top flange underside at CL girder, extrapolated to CL brg (low-edge value corrected by 21"/2 x 6%)','4.50"','5.13" raw - 0.63" = 4.50" (all 8 brgs)','0.00','1/8"','pass','G*-TF*, DECK','evidence/haunch.txt')
add(271,'O-17','Longitudinal section','Deck end / expansion joint at Pier 13 and Pier 16: 2" joint opening at 60 F (note 1) at CL pier','deck vertex stations','deck ends 1" each side of CL pier (2" opening)','deck ends exactly on CL pier 13 (skewed line through WP 301+53.72) and CL pier 16 (306+15.00)','~1"','1/4"','fail','DECK','work (deck vertex stations)','joint opening not represented; low severity')
add(271,'O-17','Longitudinal section','CL pier 13 to CL brg 1\'-0" (girders, measured along girder: 1\'-0 15/16" G1 / 1\'-0 7/8" G2 on O-19); CL pier 16 to CL brg 1\'-0"','bearing station vs pier line','P16: 1\'-0"','P16 CL brg at STA 306+14.00 on both girders (1\'-0" along BL, radial)','see span 16 checks','1/16"','pass','B4-*','evidence/girder_positions.txt','measured along BL; along G1 this is 11.84" - see span 16 check')
add(271,'O-17','Superstructure quantities','Structural steel 904,820 LB','sum of IFC SBM_Fabrication Mass (plates + members) + estimate for fasteners','904,820 lb','883,065 lb plates+members (-2.4%); + bolts ~12.8k + studs ~9.9k + welds 1.7k = ~907k (+0.3%)','-2.4% / +0.3%','1%','pass','all steel','work (quantity query)','pass only if the pay item includes bolts/studs/welds (fasteners carry no mass in the IFC); plates+members alone are 2.4% low')
add(271,'O-17','Superstructure quantities','Stainless steel fixed bearing assembly 4 EA / expansion 8 EA (Bridge O incl. span 17 U-beams)','count of IfcBearing + schedule types','steel unit: 2 fixed (P14) + 6 guided','B2-G1/G2 fixed (F1), 6 guided (G1/G2 types)','0','exact','pass','B1..B4 *','evidence (bearing schedule p.293)','span-17 U-beam bearings out of scope')
add(271,'O-17','Superstructure quantities','Class AA concrete 451.7 CY (whole Bridge O incl. span 17 and haunches)','IFC deck volume','451.7 CY (all spans)','394.7 CY (spans 14-16 slab only)','-','2%','n.a.','DECK','-','not comparable: quantity covers span 17 and haunch concrete; model deck excludes both')
add(271,'O-17','Superstructure bar list / deck reinforcing plan','bar marks A1..H3','-','-','not modeled','-','-','n.a.','-','-','n.a. - deck rebar not in stated model scope')
# ---------------- p272 O-18
add(272,'O-18','Slab pouring order / cantilever layout span 17 / end of slab section','pour sequence, span 17','-','-','-','-','-','n.a.','-','-','construction sequence and span 17 (U-beams) - out of model scope')
# ---------------- p273 O-19 framing plan
add(273,'O-19','Framing plan','Girder radii: G1 R 868\'-1 11/16" (13\'-4" left of BL), G2 R 885\'-9 11/16" (4\'-4" right)','bearing / web radial positions about the alignment centre','13.333 / -4.333 ft offsets','13.330 / -4.337 (CL tub from section); bearings +3" outward (cross-slope rotation, O-38 "3\" horiz. offset")','<=0.004 ft','1/16"','pass','G1-*, G2-*, B*','evidence/S1_tub_section_side_by_side.png')
add(273,'O-19','Framing plan','Span-16 radius labels: "R 885\'-9 11/16 CL GIRDER NO. 1" / "R 868\'-1 11/16 CL GIRDER NO. 2" (swapped vs spans 14-15); "ALONG CL WP GIRDER NO. 9"','consistency with lengths','-','model uses G1 inside (R 868) in all spans, consistent with the span lengths 157\'-6 7/8" / 160\'-9 1/2"','-','-','conflict','G1,G2','img p273','PLAN CONFLICT (drafting): labels swapped in span 16 and "girder no. 9" labels; lengths prove G1 is the inner girder')
add(273,'O-19','Framing plan','P13 skew: angle CL pier to G1 69d02\'38", to G2 69d18\'54"; 18\'-11 7/8" between girders along CL pier; WP 301+53.72','model CL brg points moved back 1\'-0 15/16" / 1\'-0 7/8" along girders -> implied pier line','69d02\'38" / 69d18\'54" / 18\'-11 7/8"','68d13\'52" / 68d41\'12" / 18\'-11.92"; line passes WP (0.002 ft)','-49\' / -38\' of angle; +0.04"','1" of angle','conflict','B1-G1, B1-G2, G*-1 skewed ends, G*-SD1, XSD1','evidence/pier13_skew.txt','PLAN CONFLICT: a straight line through the WP with the printed 18\'-11 7/8" cannot have the printed angles (needs 68d14\'); and the two printed angles are mutually inconsistent for concentric girders. Model honours WP + distance. User to decide.')
add(273,'O-19','Framing plan - CL brg offsets at P13','CL pier 13 to CL brg 1\'-0 15/16" (G1) / 1\'-0 7/8" (G2); 18\'-11 13/16" along CL brg (O-29)','distance between model CL brg points','18\'-11 13/16"','computed from C-construction: pier line consistent (see P13 skew check)','-','1/16"','pass','B1-*','evidence/pier13_skew.txt','offsets were used to derive the pier line; distance along pier matches to 0.04"')
add(273,'O-19','Framing plan','Lateral / cross-frame spacing: G1 14/17/16 eq. spa. = 141\'-3 15/16" / 158\'-6 3/4" / 157\'-6 7/8"; G2 2 eq sp = 13\'-7 11/16" + 12 eq spa = 123\'-6 5/8" / 17 = 161\'-9 1/2" / 16 = 160\'-9 1/2"','transverse stiffener (panel point) arc positions','10.0949 / 9.3272 / 9.8483 ft; G2 6.8203, 10.2960 / 9.5172 / 10.0495 ft','10.095 / 9.327 / 9.849; G2 6.820, 10.296 / 9.517 / 10.049 ft','<=0.001 ft','1/16"','pass','G*-*-TS*.*, G*-K*, G*-T*','work (panel spacing script)')
add(273,'O-19','Framing plan','Cross frame pattern: internal cross frames every 2nd panel point, top struts between, 2 laterals (V) per bay','counts by span','span14 13 pts (6 K + 7 T), span15 16 (8+8), span16 15 (7+8) per girder; 47 laterals/girder','K 21 + T 23 per girder; 94 WT9x38 laterals (47/girder); 84 L5x5x5/8 diagonals','0','exact','pass','G*-K*, G*-T*, G*-LAT*','bolt_groups.txt')
add(273,'O-19','Framing plan','External intermediate diaphragms: 2 per span at internal cross-frame points (span 15: first K after FS2)','visual position vs model XD stations','2/2/2','XD1/XD2 span14 (K1.4,K1.10), XD3/XD4 span15 (K2.6,K2.12), XD5/XD6 span16 (K3.6,K3.10)','-','panel point','pass','XD1..XD6','img p273 crops','positions read visually from the framing plan (not dimensioned)')
add(273,'O-19','Framing plan','Access hatch 4\'-3" from CL brg, position normal to CL girder (P13 and P16 ends)','hatch centroid arc distance','4\'-3"','51.00" (all 4)','0','1/16"','pass','G1-1-HATCH1,G2-1-HATCH3,G1-5-HATCH2,G2-5-HATCH4','work (hatch script)','plan shows hatches only at P13 and P16 ends: model G1-5-HATCH2 / G2-5-HATCH4 at P16, G*-1 at P13')
add(273,'O-19','Typical tub girder section','8\'-0" CL webs at top, 5\'-11" at bottom, 4\'-2" depth, web 4\'-3 9/16" on slope, 4:1, BF 77", TF 21", 3" BF overhang','IFC section cut STA 303+80 (horizontal values / cos 3.43 deg)','96" / 71" / 50" / 51 9/16" / 77" / 21"','95.83 (=96.0 on slope) / 70.87 (=71.0) / web PL 51 9/16" / BF 77.08 / TF 21.07','<=0.08"','1/16" (mesh)','pass','G1-3-*, G2-3-*','evidence/S1_tub_section_side_by_side.png','BF/TF widths within mesh + slope-correction error')
add(273,'O-19','General notes 2','Girders rotated about top WP with the plane of the deck (6%)','TF underside slope in section','6%','5.99%','0.01%','0.0005','pass','G*','evidence/S1_tub_section_side_by_side.png')
# ---------------- p278 / p279 girder elevations
G1 = [
 ('Span 14 CL brg P13 - CL brg P14',"141'-4\"","141'-4.008\"",0.008),
 ('Span 15 CL brg P14 - P15',"158'-6 3/4\"","158'-6.785\"",0.035),
 ('Span 16 CL brg P15 - P16',"157'-6 7/8\"","157'-6.966\"",0.091),
 ('CL brg P13 - FS1',"105'-9 7/8\"","105'-9.879\"",0.004),
 ('FS1 - FS2',"77'-6\"","77'-6.004\"",0.004),
 ('FS2 - FS3',"65'-2 3/4\"","65'-2.754\"",0.004),
 ('FS3 - FS4',"85'-7 3/16\"","85'-7.209\"",0.021),
 ('FS4 - CL brg P16',"123'-3 13/16\"","123'-3.913\"",0.100),
 ('FS1 - flange break (1 1/4 -> 1 7/8)',"20'-4 1/2\"","20'-4.503\"",0.003),
 ('flange break - CL brg P14',"15'-1 5/8\"","15'-1.626\"",0.001),
 ('CL brg P14 - flange break (1 7/8 -> 1 3/8 BF / 1 1/4 TF)',"13'-11 15/16\"","13'-11.939\"",0.001),
 ('flange break - FS2',"27'-11 15/16\"","27'-11.936\"",-0.001),
 ('FS3 - flange break (1 3/8 -> 2 1/4)',"37'-3 13/16\"","37'-3.843\"",0.031),
 ('flange break - CL brg P15',"14'-0 5/16\"","14'-0.312\"",0.0),
 ('CL brg P15 - flange break (2 1/4 -> 1 3/8)',"14'-8 15/16\"","14'-8.941\"",0.004),
 ('flange break - FS4',"19'-6 1/8\"","19'-6.113\"",-0.012),
]
G2 = [
 ('Span 14 CL brg P13 - CL brg P14',"137'-2 5/16\"","137'-2.320\"",0.008),
 ('Span 15 CL brg P14 - P15',"161'-9 9/16\"","161'-9.506\"",-0.056),
 ('CL brg P13 - FS1',"100'-11 1/2\"","100'-11.503\"",0.003),
 ('FS1 - FS2',"79'-0 7/8\"","79'-0.942\"",0.067),
 ('FS2 - FS3',"66'-6 5/8\"","66'-6.626\"",0.001),
 ('FS3 - FS4',"87'-4 1/8\"","87'-4.121\"",-0.004),
 ('FS4 - CL brg P16',"125'-10 1/16\"","125'-10.082\"",0.020),
 ('FS1 - flange break',"20'-9 15/16\"","20'-9.941\"",0.004),
 ('flange break - CL brg P14',"15'-4 7/8\"","15'-4.876\"",0.001),
 ('CL brg P14 - flange break',"14'-3 9/16\"","14'-3.563\"",0.001),
 ('flange break - FS2',"28'-6 9/16\"","28'-6.562\"",0.0),
 ('FS3 - flange break',"38'-1 3/16\"","38'-1.193\"",0.006),
 ('flange break - CL brg P15',"14'-3 9/16\"","14'-3.562\"",-0.001),
 ('CL brg P15 - flange break',"15'-1 1/2\"","15'-1.503\"",0.003),
 ('flange break - FS4',"19'-9 7/8\"","19'-9.863\"",-0.012),
]
for g,rows,page,sheet in [('G1',G1,278,'O-24'),('G2',G2,279,'O-25')]:
    for item,vp,vm,d in rows:
        res='pass' if abs(d)<=0.0625 else 'fail'
        note=''
        if g=='G1' and 'Span 16' in item: note='G1 inner girder: model CL brg P16 is 1\'-0" from CL pier along the BL (11.84" along G1); plan value lies between the two conventions. Low severity.'
        if g=='G1' and 'FS4 - CL brg P16' in item: note='same cause as span 16'
        if g=='G2' and 'FS1 - FS2' in item:
            res='conflict'; note='PLAN CONFLICT (rounding): the printed 79\'-0 7/8" differs by 1/16" from the sum of its own segments 20\'-9 15/16"+15\'-4 7/8"+14\'-3 9/16"+28\'-6 9/16" = 79\'-0 15/16"; model follows the segments'
        add(page,sheet,'Steel girder elevation (%s)'%g,'%s: %s'%(g,item),GEO,vp,vm,'%+.3f in'%d,'1/16"',res,'%s field sections / splices / bearings'%g,'evidence/girder_positions.txt',note)
add(279,'O-25','Steel girder elevation (G2)','G2 span 16 CL brg P15 - P16 (also O-19 160\'-9 1/2")',GEO,"160'-9 3/8\" (O-25) / 160'-9 1/2\" (O-19)","160'-9.447\"","+0.072 / -0.053 in",'1/16"','conflict','G2 B3/B4','evidence/girder_positions.txt','PLAN CONFLICT: O-25 and O-19 differ by 1/8"; model lies between them')
add(278,'O-24','Steel girder elevation (G1)','G1 span 14: 141\'-4" (O-24) vs 14 eq spa = 141\'-3 15/16" (O-19)',GEO,"141'-4\" / 141'-3 15/16\"","141'-4.008\"","0.008 / 0.071",'1/16"','pass','G1','evidence/girder_positions.txt','1/16" plan rounding between sheets, noted')
for page,sheet,g in [(278,'O-24','G1'),(279,'O-25','G2')]:
    add(page,sheet,'Steel girder elevation (%s)'%g,'%s girder end at P13 9 11/16" before CL brg (perp. to BF, along CL girder)'%g,'tangent-plane cut of BF1 at CL-girder radius','9 11/16" (9.688)','9.682" / 9.684"','-0.005"','1/16"','pass','%s-1-BF1'%g,'work (end cut)')
    add(page,sheet,'Steel girder elevation (%s)'%g,'%s girder end at P16 9" beyond CL brg'%g,'web end arc positions','9"','9.006"','+0.006"','1/16"','pass','%s-5-*'%g,'work (end cut)')
    add(page,sheet,'Steel girder elevation (%s)'%g,'%s plate schedule: TF 2-PL 1 1/4 / 1 7/8 / 1 1/4 / 1 3/8 / 2 1/4 / 1 3/8 x 21"; BF PL 1 1/4 / 1 7/8 / 1 3/8 / 1 3/8 / 2 1/4 / 1 3/8 x 77"; webs 5/8 except 3/4 in the P15 2 1/4" zone (sections 1..5)'%g,'IFC plate Size + section-cut thickness at 8 brgs and STA 303+80','as printed per segment','all 82 flange plates and 46 web plates match by segment (e.g. BF over P15 2.25" cut thk 6.856" incl. 6% slope over 77" = 2.25")','0','1/16"','pass','%s-*-TF*/BF*/W*'%g,'evidence/haunch.txt; work/elements.csv')
    add(page,sheet,'Steel girder elevation (%s)'%g,'%s web 2-PL 5/8" / 3/4", 4\'-2" vertical'%g,'IFC size + section','4\'-2" vertical, web 51 9/16 on slope','web PL 51 9/16" on slope; 50" vertical (sbm elevations chain and section)','0','1/16"','pass','%s-*-WL*/WR*'%g,'evidence/S1_tub_section_side_by_side.png')
    add(page,sheet,'Steel girder elevation (%s)'%g,'%s fracture-critical tension flange zones (BOTTOM / BOTH / TOP)'%g,'IFC Size text ranges vs printed zones','as printed','TF and BF tension ranges match printed zones (e.g. G1 TF tension from 85\'-9 3/4" = 86.620 ft plate pos.)','0','1/16"','pass','%s-*-TF*/BF*'%g,'work/elements.csv','G1-1-BF1 text splits its (fully tension) range at 81.349 ft, which is G2\'s 80\'-6 1/2" value - cosmetic attribute error, union is correct')
add(278,'O-24','Weld callout','Web-to-flange fillet 5/16" both sides (typ.)','IFC weld Size','5/16" both sides','G*-*-FW "Fillet weld 5/16\", flange to web, both sides"','0','exact','pass','G*-*-FW','work/elements.csv')
# studs
for g,page,sheet,sec in [('G1',278,'O-24',[('1','124 spa @10" max -> 125 rows x 6 = 750',750,746),('2','40+48 spa -> 90 rows = 540',540,540),('3','62 spa -> 63 rows = 378',378,378),('4','59+39 spa -> 100 rows = 600',600,600),('5','162 spa -> 163 rows = 978',978,978)]),
                         ('G2',279,'O-25',[('1','148 spa @8" max -> 149 rows x 6 = 894',894,892),('2','45+54 spa -> 101 rows = 606',606,606),('3','76 spa -> 77 rows = 462',462,462),('4','67+44 spa -> 113 rows = 678',678,678),('5','186 spa -> 187 rows = 1122',1122,1122)])]:
    for s,txt,p_,m_ in sec:
        res='pass' if p_==m_ else 'fail'
        add(page,sheet,'Stud spacing row','%s field section %s shear studs: %s (3 studs/flange, O-36 detail)'%(g,s,txt),'count of IfcMechanicalFastener STUDSHEARCONNECTOR per section (DB parent)',str(p_),str(m_),str(m_-p_),'exact count',res,'%s-%s shear studs'%(g,s),'bolt_groups.txt',
            'studs omitted on the right flange at the first two rows near the skewed P13 end (rows at 12" / 22" from CL brg) - plan shows full rows' if res=='fail' else '')
add(278,'O-24','Stud spacing','First stud row 1\'-0" from CL brg P13; 1\'-10" clear each side of field splices; 8" each side of CL brg at piers','stud arc positions','12" / 22" / 8"','12.00" first row; rows resume per splice/pier gaps (counts per section match)','0','1"','pass','G*-* shear studs','work (stud script)')
add(290,'O-36','Detail of stud shear connectors','3 - 7/8" dia x 7" studs at 7" across each top flange','stud geometry extents and transverse offsets','7/8" x 7"; 7" spacing','length 7.00", head 1 3/8"; transverse spacing 6.96"','0.04"','1/4"','pass','all 7052 studs','work (stud dims)')
# ---------------- p280 camber
add(280,'O-26','Dead load deflection tables G1/G2','Total DL deflection at tenth points (31 per girder)','`sbm blocking --all` camber column vs printed TOTAL row','62 values','62 values','max 0.00"','1/16"','pass','G1,G2','extract/camber_p280.csv; evidence/sbm_blocking.txt','field-splice columns of the table (4 per girder) are not reported by the model; IFC geometry carries no camber (final geometry)')
# ---------------- p281 field splice
add(281,'O-27','Field splice details','TF splice: 1 PL 3/4 x 21 x 3\'-2 1/2" outside + 2 PL 3/4 x 9 1/2 x 3\'-2 1/2" inside','IFC Size + PCA/hull of plate meshes','21 x 38.5 / 9.5 x 38.5 x 3/4','Size text matches; mesh thk 0.751; hull long edge 38.29-38.65" (mesh non-planar, hull unreliable)','-0.21..+0.15"','1/16"','open','FS*-G*-SP1..SP6','work/outline.py','measurement not reliable with the available tools (needs `sbm measure`); width and thickness pass')
add(281,'O-27','Field splice details','Web splice 2 PL 5/8 x 22 1/2 x 3\'-6 1/2" each web','IFC Size + hull','5/8 x 22.5 x 42.5','42.50 x 22.43 (+3.7" corner clip edge) x 0.625','-','1/16"','pass','FS*-G*-SP7..SP10','work/outline.py','hull shows a clipped corner not drawn on O-27 (probably weld clearance) - minor, noted')
add(281,'O-27','Field splice details','BF splice 2 PL 3/4 x 63 1/2 x 3\'-0 1/2" (splice 1); 7/8 x 63 1/2 x 3\'-0 1/2" (splices 2-4)','IFC Size + arc length / radial width','63.5 x 36.5','63.47-63.50 x 36.51 x 0.751/0.876','<=0.03"','1/16"','pass','FS*-G*-SP11/SP12','work (plate arc dims)')
add(281,'O-27','Field splice details','Filler PL 1/8 x 1\'-9 x 1\'-7 1/8" (splice 3 only)','IFC Size + arc dims','1/8 x 21 x 19 1/8','0.125 x 21.00 x 19.04; only FS3 (4 plates)','<=0.09"','1/16"','pass','FS3-G*-FP*','work (plate arc dims)','length 19.04 vs 19.125 within mesh tolerance of the arc measure')
add(281,'O-27','Field splice details','Bolts per splice: TF 2 x 4 x 12 = 96, webs 2 x 6 x 12 = 144, BF 18 x 10 = 180 -> 420; 1" A325 type 3 w/ DTI','count per splice bolt group','420 per splice, 3360 total','420 each (8 splices)','0','exact','pass','FS*-G* bolts','bolt_groups.txt','pitch/gage pattern not measured (no measure tool)')
add(281,'O-27','Field splice details','1/4" max gap between girder sections','plate ends at splice','<=1/4"','1/4" (each plate end 1/8" short of CL splice)','0','1/16"','pass','G*-*-*','work (plate length analysis)')
add(281,'O-27','Field splice notes','M270 Gr50W splice plates','IfcMaterial','M270 50W','AASHTO M270 Gr. 50W','-','exact','pass','FS*','-')
# ---------------- p282 cross frames
add(282,'O-28','Intermediate bracing detail','Internal cross frame: top strut WT6x29, diagonals L5x5x5/8, tab PL 3/4 x 10, gusset PL 3/4, stiffener PL 3/4 x 10','IFC Size + member extents (d, bf, area)','WT6x29 / L5x5x5/8 / PL 3/4x10','WT6x29 (d 6.10, bf 10.0); L5x5x5/8 (A 5.85 in2); tab 3/4x10x12; gusset 3/4 x 22 x 14; stiffener 10 x 3/4','<=0.05 in2','section','pass','G*-K*-*, G*-*-TS*','work (dims table)','gusset outline is not dimensioned on the plans (model note says so) - size open')
add(282,'O-28','Intermediate bracing detail','External intermediate diaphragm: web PL 5/8 x 3\'-2", flanges PL 3/4 x 12, 2-L8x8x3/4 each end','IFC Size + section S3','5/8 x 38 / 3/4 x 12 / L8x8x3/4','5/8 x 38.00 / 3/4 x 12.00 / L8x8x3/4 (A 11.17 in2)','0','1/16"','pass','XD*-W/TF/BF/CL*/CR*','evidence/S3_ext_interm_diaphragm_side_by_side.png')
add(282,'O-28','Intermediate bracing detail - XD connection','Bolts per XD end: diaphragm side 2 columns x 9 (8 spa @ 3 1/2"), girder side 8 per angle (7 sp @ 3 1/2") -> 18 + 16 = 34 per end, 68 per diaphragm','bolt positions grouped by axis direction','2 x 9 on the diaphragm web per end','1 x 9 on the diaphragm web + 2 x 8 on the girder web = 25 per end (50 per XD, 300 total)','-9 per end (-108 total)','exact count','fail','XD1..XD6 bolts','evidence/xd1_bolts.txt','second bolt column through the 2-L8x8x3/4 and diaphragm web is missing')
add(282,'O-28','Intermediate bracing notes 5/6','Bolt holes 1 1/16"; fillet welds terminated 3/8" +/- 1/8" from edges','IFC attribute text','as printed','SBM_FabricationAttributes text matches','-','-','pass','members/welds','work/elements.csv','attribute text only; hole geometry not checked')
add(282,'O-28','Detail A','WT6x29 top strut 1 1/2 x 3 clip, bolts to stiffener (2x2)','bolt group count','8 per top strut T-point (2 stiffeners x 4)','8 per T group; 22 per K group','-','exact','pass','G*-T* bolts','bolt_groups.txt','K: 4+4 strut + 7 per diagonal? pattern not verified individually')
# ---------------- p283-288 support diaphragms
add(283,'O-29','End support diaphragm P13','Interior support diaphragm web PL 3/4, flanges PL 1 x 12, 1" x 8" bearing stiffeners NS & FS (4/diaphragm)','IFC Size + counts','3/4 / 1x12 / 1x8 x4','3/4 / 12x1 / 8x1 x4 (G*-SD1)','0','1/16"','pass','G*-SD1-*','work (dims table)')
add(283,'O-29','End support diaphragm P13','Exterior support diaphragm web PL 3/4, flanges PL 1 x 12, 2 bent PL 1/2 x 16 each end','IFC Size','3/4 / 1x12 / bent 1/2x16','3/4 x 50 1/2 / 12x1 / bent PL 1/2 x 16 (legs 8) x4','0','1/16"','pass','XSD1-1-*','work (dims table)')
add(283,'O-29','End support diaphragm P13','18\'-11 13/16" along CL brg between WPs','model CL-brg points','18\'-11 13/16"','18\'-11.92" (pier line)','<0.1"','1/16"','pass','B1-*','evidence/pier13_skew.txt','value computed on the CL pier line offset by the brg offsets; acceptable')
add(283,'O-29','Detail of stud shear connectors (ext. diaphragm)','2 - 7/8 x 7 studs at 3 1/2" @ 1\'-0" max, 6" end distance','stud positions along XSD top flange','<=12" spacing; 6" ends','11.1-11.8" spacing; first stud 17" from diaphragm TF end; XSD1 14, XSD2-4 12 studs','-','1"','open','XSD*-1 shear studs','work (XSD stud script)','6" end dimension on O-29/O-33 references the outer girder flange line - cannot be resolved without a measure tool / clarification')
for p,s,pier in [(285,'O-31','P16'),(287,'O-33','P14/P15')]:
    if pier=='P16':
        add(p,s,'End support diaphragm P16','Interior web PL 3/4, flanges 1x12, brg stiffeners 1x8; exterior web 3/4, 2-L8x8x1/2','IFC Size','as printed','G*-SD4: 3/4, 12x1, 8x1 x4; XSD4: 3/4 x 50 3/4, L8x8x1/2 x4','0','1/16"','pass','G*-SD4-*, XSD4-1-*','work (dims table)')
    else:
        add(p,s,'Pier diaphragm P14/P15','Interior web PL 1 1/2 with access opening 1\'-6" x 2\'-8" R 8", 11" above BF; flanges PL 1 1/2 x 12; brg stiffeners 1 1/4 x 8 NS&FS; strut PL 1 1/4 x 8 with 1x1 clips; exterior web 3/4, flanges 1x12, 2-L8x8x1/2','IFC Size + section S2','as printed','G*-SD2/SD3 web 1 1/2 opening 18x32 R8; TF 12 x 1 1/2; BS 8 x 1 1/4 (x4); ST 1 1/4 x 8 x 22 3/4 clips 1x1; XSD2/3 web 3/4, 12x1, L8x8x1/2','0','1/16"','pass','G*-SD2-*, G*-SD3-*, XSD2-1-*, XSD3-1-*','evidence/S2_pier14_diaphragm_side_by_side.png','opening elevation (11") not measured')
add(287,'O-33','Pier diaphragm P14/P15','17\'-8" along CL brg between WPs; 4\'-4" PGL to G2','model CL brg points','17\'-8"','17.667 ft (radial supports)','0','1/16"','pass','B2-*,B3-*','evidence/S1_tub_section_side_by_side.png')
for p,s in [(284,'O-30'),(286,'O-32'),(288,'O-34')]:
    add(p,s,'Top / bottom connection details, typical angle-to-web','Top connection PL 1x12 (interior 4\'-3" / exterior), bottom connection PL 1", bolt spacings, corner clips 3 x 1 1/2 (P13/P16) / 3 1/2 x 1 1/2 (P14/15)','IFC Size text','as printed','TCPR 1x12x51 (24 holes), TCPL 1x12x17 1/2 (8 holes), BCPR 1 x 16/12 x 26 (16 holes); clips 3 x 1 1/2 / 3 1/2 x 1 1/2','-','1/16"','open','G*-SD*-TCPL/TCPR/BCPR','work/elements.csv','thickness/width and clip sizes match by text; outline lengths and bolt patterns not measured (geometry of skewed plates needs a measure tool)')
# ---------------- p289 lateral bracing
add(289,'O-35','Lateral bracing detail','Laterals WT9x38, top strut WT6x29, 1" A325 bolts, holes 1/16" larger; W.P. 8" from CL stiffener along CL TF (1\'-0" at P13 support diaphragm)','IFC Size + member extents','WT9x38','WT9x38 (d 9.11, A 10.05 in2 net of holes/copes); 12 bolts per lateral','-','section','pass','G*-LAT*-L*','work (dims table)','W.P. offsets (8" / 1\'-0") not measured - position unverified')
add(289,'O-35','Lateral bracing detail','Note: "GIRDER NO. 7 SHOWN, GIRDER NO. 8 SIMILAR"','-','-','-','-','-','conflict','-','img p289','PLAN CONFLICT (drafting): girder numbers 7/8 do not exist on Bridge O (template text)')
# ---------------- p290 misc
add(290,'O-36','Plan - access door','Access door PL 3/8" 2\'-0" x 3\'-0"','IFC hatch plate extents','24 x 36 x 3/8','24.01 x 36.01 x 0.378','0.01"','1/16"','pass','G*-*-HATCH*','work (hatch script)')
add(290,'O-36','Plan - access door','Bottom flange opening: 1\'-6" wide, R 9" ends with centres 6" either side of CL (=> 2\'-6" long), plus 1" x 3" padlock slot','BF1 hole vertex extents along / across girder','18" x 30"','18.0" x 33.0" (plus slot to 35.5")','+3.0"','1/16"','fail','G1-1-BF1, G2-1-BF1, G1-5-BF1, G2-5-BF1','work (hatch script)','opening length scaled/derived from R 9" and the 6" centre dims; model description also states 18" x 33"')
add(290,'O-36','Drain hole / drip plate / maintenance lighting openings / rodent screens / padlock / handle / eyebolts','2" drain holes in BF every 50 ft; 3/8 drip plates; 1" lighting holes; expanded-metal screens','IFC element search','present','not modeled','-','-','fail','G*-*-BF*','work/elements.csv','minor omissions (low severity); drip-plate note lists piers 5,7,12,13,14 (template text from another bridge - plan conflict to confirm)')
add(290,'O-36','Top flange coping / bottom corner clips','coping of top strut flange, corner clips','IFC Size text','as detailed','"flange coped between the stiffeners"; corner clips on SD webs','-','-','pass','G*-K*-TS, G*-SD*-W','work/elements.csv','by attribute only')
# ---------------- p292 bearing details
add(292,'O-38','Bearing plans G1/G2/F1','Anchor bolts: G1 2 (at expansion end piers), G2 4, F1 4; 2" dia; at +/-1\'-9 1/2" transverse and 9" from CL brg','anchor bolt centroids relative to pad centre (arc / radial)','2/4/4/2; +/-21.5"; 9"','B1 2, B2 4, B3 4, B4 2; B2-B4 at +/-21.50" and 8.98-9.02"; B1 (skewed P13) 43.0" apart along the skew, centre 8.35" / -3.3"','<=0.02" (B2-B4)','1/16"','pass','24 anchor bolts','work (anchor bolt script)','B1 layout follows the P13 skew - plan does not detail the skewed case (open item for user)')
add(292,'O-38','Detail of anchor bolt','Anchor bolt 3\'-0" long, 2" dia, 1\'-3" min embedment','anchor bolt mesh length','36"','23.7-27.8"','-8..-12"','1/4"','fail','24 anchor bolts','work (anchor bolt script)','embedded part is substructure - may be intentionally trimmed; confirm with originator (low)')
add(292,'O-38','Typical section at bearing','6" dia hole in BF at expansion brgs, 2 1/2" dia at fixed brgs (for anchor bolts)','sbm clash AnchorBolt x FlangePlate + IfcOpenShell overlap','holes present','no holes: 24 anchor bolts penetrate the BF up to 1 1/8" (spot check B2-G1: 6.9 in3 overlap)','-','-','fail','24 anchor bolts, G*-*-BF*','results/clash_spotcheck_ifcopenshell.csv','missing BF holes (or anchor bolts too long above seat)')
add(292,'O-38','Typical section at bearing','Top contact PL 8 x 8 x 1/2 with 2 1/2 x 6 slot (expansion) / 4 x 4 bottom contact PL (fixed)','IFC element search','present','not modeled','-','-','fail','B*','work/elements.csv','bearing contact plates missing')
add(292,'O-38','Typical section at bearing','Horizontal offset 3" CL brg from CL tub girder','pad centre radius vs girder radius','3"','3.01" / 3.02" outward (all 8)','0.02"','1/16"','pass','B*-PAD','work (bearing positions)')
add(292,'O-38','Notes 3','Sole plates and contact plates AASHTO M270 Gr 50W','IfcMaterial of beveled bearing plates','M270 Gr 50W','ASTM A709 Gr. 50','grade W missing','exact','fail','B*-G*-BP','work (material query)')
# ---------------- p293 bearing schedule
for b,sup,pad,np_,pl,tr,lo in [('B1','P13','13 x 22 x 3 3/4 (5 x 1/2, 6 x 1/8, 1/4)','13/22/3.75','2 1/2 x 23 x 14','4.52','-4.92'),('B2','P14','20 x 30 x 3 3/4','20/30/3.75','2 1/2 x 31 x 21','6.00','-1.48'),('B3','P15','22 x 30 x 3 3/4','22/30/3.75','2 1/2 x 31 x 23','6.00','-0.57'),('B4','P16','15 x 23 x 5 (7 x 1/2, 8 x 1/8, 1/4)','15/23/5','2 1/2 x 24 x 16','6.00','-0.57')]:
    mv={'B1':('13.000 x 22.000 x 3.750','2 1/2 x 23 x 14 (mesh 22.99 x 14.00)','4.48/4.54','-4.98/-4.89'),'B2':('20 x 30 x 3.75','31 x 21','6.00','-1.47/-1.44'),'B3':('22 x 30 x 3.75','31 x 23','6.00','-0.58/-0.57'),'B4':('15 x 23 x 5.00','24 x 16','6.00','-0.58/-0.56')}[b]
    add(293,'O-39','Bearing assembly schedule','%s (%s) pad %s'%(b,sup,pad),'IFC pad mesh extents + Size','%s'%np_,mv[0],'0','1/16"','pass','%s-G1-PAD, %s-G2-PAD'%(b,b),'work (dims table)')
    add(293,'O-39','Bearing assembly schedule','%s (%s) beveled plate %s, bevel trans %s%% / long %s%%'%(b,sup,pl,tr,lo),'IFC plate Size + mesh','%s; %s / %s'%(pl,tr,lo),'%s; %s / %s'%mv[1:],'<=0.06% slope (<0.01")','1/16" over plate','pass','%s-G1-BP, %s-G2-BP'%(b,b),'work (dims table)','model computes the bevel per girder; plan gives one value per support (difference < 0.01" over the plate)')
add(293,'O-39','Bearing location schematic','Types: P13 G1, P14 F1, P15 G2, P16 G1','IFC assembly names','G1/F1/G2/G1','G1/F1/G2/G1','-','exact','pass','B1..B4','work/elements.csv')
# ---------------- out of scope sheets
for p,s,t in [(274,'O-20','U54 beam details'),(275,'O-21','U54 beam details'),(276,'O-22','U54 beam details'),(277,'O-23','U54 beam elevations'),(291,'O-37','54" U-beam bearing details'),(294,'O-40','U-beam bearing schedule'),(295,'O-41','Approach slab'),(296,'O-42','Slope wall'),(297,'O-43','Form liner'),(299,'PS-1','Peace shield'),(300,'PS-2','Peace shield')]:
    add(p,s,t,'all items','-','-','-','-','-','n.a.','-','-','n.a. - out of model scope (span 17 / approach / substructure / architectural)')
add(298,'BD-1','Detail of bridge drains','bridge drains on Bridge O deck (deck drain STA 302+89.85, 306+10.91 on O-1)','IFC element search','2 deck drains','none','-','-','open','-','-','drains are not in the stated scope list; O-1 shows deck drains in the steel unit - scope question for the user')
add(148,'N-16','Substructure layout (Bridge N)','Pier 13 location','-','-','-','-','-','n.a.','-','-','out of scope (substructure); P13 WP station taken from O-1/O-19')
add(180,'N-48','Pier 13 east details','pedestal elevations / layout','-','-','-','-','-','n.a.','-','-','out of scope (substructure)')
# ---------------- program / IFC output checks (whole model)
add('-','-','Program output: sbm elevations','Elevation chain deck-to-seat vs plan H dims (O-17) and vs IFC','`sbm elevations` output','5\'-10" / 5\'-10 5/8" / 5\'-11" / 5\'-11 3/8"','report: 5\'-9" / 5\'-9 1/2" / 5\'-9 13/16" / 5\'-8 15/16" using pad 14x20x3 1/2 and a computed sole plate; IFC seat differs by 15/16"..2 3/8" (reported by the command itself)','-1" .. -2 7/16"','1/8"','fail','B*','evidence/sbm_elevations.txt','PROGRAM DEFECT (sec. 9): the elevations report ignores the modeled bearing assemblies (O-39 schedule) and uses defaults; the IFC itself is correct')
add('-','-','IFC identification','Bolts, studs, anchor bolts carry no Mark/Tag in the IFC','IFC SBM_Fabrication pset','marks expected for QC traceability','13,788 fasteners without Mark (marks exist only in the DB)','-','-','open','IfcMechanicalFastener','-','traceability gap, not a plan item')
# write
os.makedirs(Q+'results',exist_ok=True)
keys=['check_id','sheet_page','sheet_no','view','item','method','value_plan','value_model','diff','tolerance','result','element_ids','evidence','note']
with open(Q+'results/checks.csv','w',newline='',encoding='utf-8') as f:
    w=csv.DictWriter(f,keys); w.writeheader(); [w.writerow(c) for c in checks]
import collections
print(len(checks), collections.Counter(c['result'] for c in checks))
