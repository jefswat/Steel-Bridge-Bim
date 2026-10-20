import csv
Q = 'C:/SBM/qc/odot-o/'
I = [
 ('I01','C123','XD1..XD6 bolts',282,'External intermediate diaphragm connection: plan shows 2 columns x 9 bolts through the 2-L8x8x3/4 and the diaphragm web at each end; model has 1 column x 9 (25 bolts per end instead of 34). 108 bolts missing in total.','high'),
 ('I02','C139','G*-1-BF1, G*-5-BF1 (access holes)',290,'Bottom-flange access opening is 18" x 33" in the model; O-36 gives R 9" ends with centres 6" either side of CL = 18" x 30". Hole in an FCM tension flange - confirm and correct.','medium'),
 ('I03','C144','24 anchor bolts / bottom flanges',292,'No anchor-bolt holes in the bottom flange (6" dia at expansion, 2 1/2" at fixed bearings, O-38); anchor bolts penetrate the flange up to 1 1/8" (sbm clash, confirmed by IfcOpenShell spot check).','medium'),
 ('I04','C145','bearings B1..B4',292,'Top contact PL 8x8x1/2 with 2 1/2 x 6 slot (expansion) and 4x4 bottom contact PL (fixed) are not modeled.','medium'),
 ('I05','clash','G*-LAT*-L* vs G*-*-TF*',289,'52 WT9x38 laterals penetrate the girder top flanges by up to 7/16" (O-35 Detail A: lateral bolted to the underside of the flange).','medium'),
 ('I06','clash','G*-SD1/SD4-TF, XSD*-TF/BF vs girder flanges',283,'Support diaphragm flanges overlap girder top / bottom flanges by up to 5/8" (8 groups), connection plates overlap flanges by up to 3/16" (12), bent plates / angles enter webs up to 3/8" (7), bearing stiffeners enter the BF up to 3/16" (4), SD webs enter girder webs up to 3/16" (4) - mostly at the skewed P13 end.','medium'),
 ('I07','C101/C106','G1-1 / G2-1 shear studs',278,'Field section 1 (P13 skewed end): 746 studs vs 750 (G1) and 892 vs 894 (G2); studs on the right flange are dropped in the first two rows (12" and 22" from CL brg). Plan shows full rows (124 / 148 SPA).','low'),
 ('I08','C059/C064','G1 bearings P16',278,'G1 span 16 is 157\'-6.97" in the model vs 157\'-6 7/8" (O-24 and O-19), 0.09" over the 1/16" tolerance; FS4 to CL brg 123\'-3.91" vs 123\'-3 13/16". Model places CL brg P16 1\'-0" from CL pier along the BL (11.84" along G1). Confirm the convention.','low'),
 ('I09','C147','B*-G*-BP',292,'Beveled bearing (sole) plates are ASTM A709 Gr. 50 in the IFC; O-38 note 3 requires AASHTO M270 Gr 50W.','low'),
 ('I10','C143','24 anchor bolts',292,'Anchor bolt solids are 23.7-27.8" long; Detail of Anchor Bolt (O-38) shows 3\'-0". Confirm whether the embedded part is intentionally trimmed.','low'),
 ('I11','C040','DECK',271,'Deck ends exactly on CL pier 13 and CL pier 16; O-17 shows a 2" joint opening (note 1).','low'),
 ('I12','C140','G*-*-BF*',290,'Minor items not modeled: 2" drain holes in BF every 50 ft (low side), 3/8" drip plates, 1" maintenance lighting holes, rodent screens, padlock / handle / eyebolts.','low'),
 ('I13','C171','sbm elevations (program)','-','PROGRAM DEFECT: `sbm elevations` builds the bearing chain with a default 14x20x3 1/2 pad and a computed sole plate, ignoring the modeled O-39 bearings; its deck-to-seat totals (5\'-9" .. 5\'-9 13/16") contradict O-17 H dims, while the IFC seats match O-17 to 0.09". The command itself flags "3D bearing seat differs by -15/16 .. -2 3/8".','medium'),
 ('I14','sheet links','girder plates, studs, cross frames','278/279/273','DB sheet links: girder field sections are linked only to O-36 (p.290), so (nearest-ancestor rule) flange / web plates and studs do not carry O-24 / O-25 (p.278/279) where their sizes and stud rows are defined; cross-frame parts lack O-19 (p.273). Bolts / welds carry links (282, 289, 287/288, 290) that no check used.','low'),
 ('I15','C172','all fasteners','-','13,788 IfcMechanicalFastener / IfcFastener ... bolts, studs and anchor bolts have no Mark/Tag in the IFC (only in the DB), which blocks per-element QC traceability from the IFC.','low'),
 ('I16','C114','FS*-G*-SP1..SP6',281,'Top flange splice plate meshes are slightly non-planar (thickness 0.751") and their hull lengths read 38.29-38.65" vs 38 1/2"; could not be resolved with the available tools - originator to confirm the plate geometry.','low'),
 ('I17','G1-1-BF1 attrs',"G1-1-BF1",278,'Size text of G1-1-BF1 splits its tension range at 81.349 ft (= G2\'s 80\'-6 1/2" value); G1 bottom tension zone is 85\'-9 3/4". Union is correct (whole plate in tension) - cosmetic attribute error.','low'),
 # plan conflicts for the user
 ('P01','C031','G1,G2',270,'PLAN CONFLICT: O-16 "Typical tub girder section" (20\'-3 13/16" / 10\'-7" deep) and its notes belong to another bridge; O-19 and O-1 show the 50" tub. Model follows O-19.','user'),
 ('P02','C032','DECK',270,'PLAN CONFLICT: O-16 typical haunch detail (1\'-8 1/4" slab, 1 7/16" haunch) vs O-19 / O-17 (8" slab, 4 1/2" haunch). Model follows O-19 / O-17.','user'),
 ('P03','C049','P13 bearings, skewed girder ends',273,'PLAN CONFLICT: P13 skew angles 69d02\'38" / 69d18\'54" cannot both hold with 18\'-11 7/8" along CL pier through WP 301+53.72 (requires about 68d14\' / 68d41\'). Model honours WP + distance (angles 68d13\'52" / 68d41\'12"). User to decide.','user'),
 ('P04','C088','G2 span 16',279,'PLAN CONFLICT: G2 span 16 is 160\'-9 3/8" on O-25 but 160\'-9 1/2" on O-19; model 160\'-9.45".','user'),
 ('P05','C076','G2 FS1-FS2',279,'PLAN CONFLICT (rounding): O-25 prints 79\'-0 7/8" but its segments add to 79\'-0 15/16"; model follows the segments.','user'),
 ('P06','C048/C137','-','273/289','PLAN CONFLICT (drafting): span-16 girder radius labels swapped and "girder no. 9" on O-19; "girder no. 7 / 8" on O-35; drip-plate note on O-36 lists piers of another bridge.','user'),
 ('P07','C168/C016','scope','270/298','SCOPE QUESTION: deck reinforcement (O-16/O-17 bar list) and the two deck drains (O-1, BD-1) are not in the model; barriers are in the IFC although out of scope. Confirm scope.','user'),
]
with open(Q + 'results/issues.csv', 'w', newline='', encoding='utf-8') as f:
    w = csv.writer(f); w.writerow(['id', 'check_id', 'element', 'page', 'description', 'severity']); w.writerows(I)
print(len(I))
