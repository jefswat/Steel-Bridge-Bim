# nht-0047-07-228 - plan data summary

TxDOT Dallas District, CSJ 0047-07-228 (bridge items also under CSJ 0918-47-242), **US 75 Pedestrian Underpass at
Northaven Trail** (Northaven Trail Phase 1B, City of Dallas), Dallas County, Texas. Bridge by HNTB (arch: T. P. Zoli III;
approaches: Z. Plinario; foundations: Q. Liu), roadway by EJES. 100% plans, sealed 09/02-09/04/2020, "comments_Reduced"
PDF (155 pages; see `INDEX.md`). Format per `docs/plan-data/EXTRACTION.md`. "p." = PDF page, "sh" = sheet number.
Tags: TEXT / RASTER / DERIVED / UNVERIFIED / UNRESOLVED. The text layer holds only sheet numbers and dates, so every
value is RASTER (read on 300-500 dpi clips) unless DERIVED.

**What the bridge is:** an 861.57 ft pedestrian / shared-use-path bridge (12'-0 trail, 14'-0 clear, H10 + 90 psf
pedestrian loading) on a strongly curved, reverse-curved horizontal alignment (R 60 ft hairpin on the west approach,
then R 250 / 385 / 385 / 250 / 200 / 200 ft), crossing US 75 (North Central Expressway) with **one steel TIED-ARCH span**
(201.68 ft CL pier 10 - CL pier 11; 201'-0 chord, R 126'-0 circular W14x132 ribs, 50'-0 rise, ribs inclined 5 deg inward,
tie girders CURVED IN PLAN following the reverse curve, 32 crossed (network) spiral-strand hangers per rib, built-up
floorbeams, CIP deck on corrugated steel deck, longitudinally post-tensioned) and **14 prestressed concrete approach
spans** in 5 units (2 Tx28 girders each, chorded, 8 1/2 in CIP slab on PCP panels). Supports: 2 abutments on 2 x 36 in
drilled shafts, 12 single-column approach piers on 48 in shafts, 2 hammerhead arch piers on 78 in shafts. Skew: 90 deg
(radial) at every approach support; the arch pier lines are 19 deg 34'41" skewed to the tangent (nearly N-S). The arch
is designed to be built off-line on falsework and rolled in on SPMTs.

Standards referenced (taken from the standard, not extracted here): AASHTO LRFD 8th ed. (2017), AASHTO LRFD Guide Spec
for Pedestrian Bridges 2nd ed. (2015 interims), PTI stay-cable recommendations (2018), ACI 318-14, TxDOT Bridge Design
Manual LRFD (2018), TxDOT Std Specs 2014; TxDOT bridge standards IGND (non-standard span Tx girders; p.118 included),
IGEB, IGTS, IGMS, PCP / PCP-FAB, PMDF, RAIL (PED) / PR11(MOD), SEJ-B(MOD), BS-EJCP, FD (common foundation details),
T80SS(MOD); roadway / wall / drainage standards CCCG-12, CPCD-14, JS-14, PED-18, REPCP-14, RW(MSE)DD, RW(TRF)(MOD),
RW(EM), RW(MSE), PAZD, PB, PCO (p.37-78).

## 0. Set survey
| item | value | PDF p. / sheet | tag | note |
|---|---|---|---|---|
| Bridge sheets | p.83-155 = sheets 134-205 | INDEX.md | TEXT | roadway / walls / drainage / utilities p.1-82 (not extracted) |
| Bridge types | 1 steel tied arch (span 10) + 14 Tx28 prestressed spans (units 1-3, 4-6, 7-9, 11-12, 13-15) | p.84-88 | RASTER | |
| Duplicate | p.130 = p.129 (sheet 180) | p.129-130 | RASTER | |
| Missing referenced sheets | arch bearing sheet 1 of 2; approach bearing standard; survey control 1-2 of 3 | p.107, 17 | RASTER | |
| Review comments | none present in the file (see "Review comments") | all | DERIVED | |

## 1. Geometry control
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Alignment | chain TRAILCLNEW = CL NHT; bridge stationing on it | - | p.18-19 / sh 69-70 | RASTER | `horizontal-alignment.csv` |
| Coordinates | state-plane-like X ~2,500,000 / Y ~7,016,000 ft; only survey control sheet 3 of 3 in the set | ft | p.17-19 | RASTER | datum not in the set; RESOLVED 2026-10-11 (U12): TxDOT SURFACE coordinates, NAD83 Texas North Central (EPSG:2276, ftUS) x Dallas County SAF 1.000136 (user GPS check, see U12) |
| Curves on the bridge | 102 (R 60 RT, PT 20+49.30 = begin bridge), 103 (R 60 RT, 20+92.01-22+35.03), 104 (R 250 RT, 22+74.36-23+59.77), 105 (R 385 RT, 24+00.44-25+18.73), 106 (R 385 LT, 25+18.73-26+47.89), 107 (R 250 LT, 26+47.89-27+15.09), 108 (R 200 RT, 28+33.68-28+72.73), 109 (R 200 LT, 28+90.27-29+29.31) | - | p.18-19 | RASTER | DERIVED: every R (PC/PT to CC) and every L (from stations) reconcile within 0.01 ft |
| Reverse curve at the arch | PRC 25+18.73 (curve 105 RT -> 106 LT) = arch local origin station | - | p.18-19, 119 | RASTER | |
| Vertical profile | TRAILNEW: VPI 7 20+00.00 523.10; VPI 8 23+40.01 538.08 L 140 (g +4.41 / -1.61, VPC 22+70.01 535.00, VPT 24+10.01 536.96); VPI 9 26+50.01 533.10 L 50 (g -1.61 / -4.90, VPC 26+25.01 533.50, VPT 26+75.01 531.88); VPI 10 32+25.01 504.92 L 100 | ft | p.20 / sh 71 | RASTER | |
| Profile check | PGL begin bridge 20+49.30 = 525.27, end 29+10.87 = 520.32 (layout) | ft | p.84, 88 | RASTER | DERIVED from VPIs 525.272 / 520.316 OK |
| Cross slope | crown at CL NHT & PGL, 1.00 % down each way (approach + arch) | % | p.89, 90, 152 | RASTER | no superelevation |
| Arch local CS | origin L0 = 13 in below PGL at PC STA 25+18.73 (global elev 534.12); X parallel to chord CL1-CL2; XY = global horizontal rotated DOWN 1.039 deg about a line parallel to the pier CL; ties and floorbeams flat in local; 180 deg rotation about the origin reproduces the geometry | - | p.119 / sh 170 | RASTER | notes say "CL PIER 7 / CL PIER 8 / bent 7" (stale, = piers 10/11) -> U9 |
| Arch work points | L0 534.12 (0,0,0); T34' 535.74 (-105.000, 8.658, 0); CL1 535.74 (-100.500, 0, 0); T0 535.74 (-96.000, -8.658, 0); T0' 532.49 (96.000, 8.658, 0); CL2 532.49 (100.500, 0, 0); T34 532.49 (105.000, -8.658, 0) | ft | p.119 | RASTER | DERIVED: L0 = mean 534.115; PGL(25+18.73) - 13 in = 534.126 OK; WL slope 3.25/201 = 1.617 % = tan(1.039) x sin(63.1 deg), consistent with the ~63 deg pier angle (p.102) |
| Arch chord in plan | 201'-0 local = 200'-11 11/16 in plan | ft | p.119 | RASTER | DERIVED 201 cos(0.926 deg) = 200.974 OK; but layout: 201.103 between CL pier points on CL NHT, 201.638 between the 7 in offset shafts -> U3 |

## 2. Layout
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Supports | `supports.csv` | - | p.94 / sh 145 | RASTER | approach supports radial (90 deg) |
| Units / spans | `units-spans.csv`: 107.88 (47.88-30-30), 97.00 (30-30-37), 162.92 (59.56-25.00-78.36), arch 203.32 joint-to-joint / 201.68 CL-CL pier, 122.45 (66.45-56.00), 168.00 (56-56-56); total 861.57 | ft | p.84-88 | RASTER | station sums DERIVED OK |
| Expansion joints | SEJ-B(MOD) 4 in + BS-EJCP at abut 1, P4, P7, P10, P11, P13, abut 16 | - | p.84-88, 91 | RASTER | P10/P11 joint 9 1/4 in off CL pier (perp.) on the approach side |
| Approach deck | 15'-8 overall = 10 + 14'-0 + 10; 2 Tx28; overhangs 2.000 min to 3.73-4.05 max | - | p.89, 109, 113 | RASTER | |
| Approach girder layout | chords per span; girder spacing / angle per support in PIER REPORT and GIRDER REPORT tables (e.g. abut 1 9.667, P2 9.703 / 9.929, P3-P4 9.929; angles 90, 85 04'08", 75 40'34") | - | p.108, 110, 112, 114, 116 | UNVERIFIED | read at 100 dpi, not transcribed -> U13 |
| Arch deck | 18'-4 edge to edge (CL tie to CL tie, 9'-2 + 9'-2 radial from CL structure); curbs 10 x 4 in at 7'-0 each side of CL NHT; curb lines R 392'-0 / R 378'-0; 19'-6 at the knuckle along the skew | - | p.90, 151, 152 | RASTER | CL structure to CL NHT varies 0 to 7 1/2 (p.90) |
| Rails | ped rail low 307.93 LT / 270.95 RT; high 571.28 LT / 572.97 RT | LF | p.84 | RASTER | low 4'-3, high posts 9'-10 (p.89) |

## 3. Superstructure
### 3a. Tied arch (span 10)
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Rib geometry | circular arc R 126'-0, chord 201'-0, rise 50'-0 in the arch plane, central angle 105 deg 48'13" | - | p.119 | RASTER | DERIVED chord 200.996, rise 49.999 from R + angle (OK 0.01); arc length 232.68 ft |
| Rib inclination | S and N ribs inclined 5 deg inward about their chords; 17'-3 13/16 c-c at the base | - | p.119 | RASTER | |
| Rib splices | RS1 (-93.537, -8.383, 3.140), RS2 (-48.171, -5.305, 38.318), RS3 (7.840, -4.304, 49.767), RS4 (58.079, -5.342, 37.898), RS5 (102.537, -8.383, 3.140) (south rib, local ft) | ft | p.120 | RASTER | DERIVED: R about (x +4.5, 76 ft below the chord in the inclined plane) = 126.001-126.002, 0.000 off the 5 deg plane; S rib springs at x -96 / +105, centre x +4.5 |
| Rib section | W14x132, web in the arch plane (bent about Y-Y, induction bent), WP at web centre; GR 50-T1 | - | p.83, 119, 127 | RASTER | |
| Rib splice | 14 1/2 x 5/8 outside + 5 5/8 x 5/8 inside flange plates, 2 - 10 x 3/4 CURVED web plates, 1/2 gap, 7/8 A325 SC | - | p.127 | RASTER | note "RS1 and RS6" (no RS6) -> U9 |
| Upper lateral bracing | W8x28 X-bracing, panel points U1-U11 / U1'-U11', crossings UL1-UL10; 21,600 lb | - | p.128-142 | RASTER | `upper-lateral-bracing-points.csv`; DERIVED U points 0.685 ft off the rib WP plane, R 126.07-126.08 -> U6 |
| Tie girder | built-up T: 7/8 x 11 web + 7/8 x 1'-2 bottom flange (11 7/8), WP at bottom of flange on web CL; GR 50-F1 FRACTURE CRITICAL | - | p.119, 121, 143 | RASTER | |
| Tie in plan | CURVED: south tie R 342'-4 (16 spa @ 5'-10 = 93'-4) then R 360'-8 (16 spa @ 6'-1 3/4 = 98'-4), straight ends 1'-7 15/16 / 8'-4 3/16; north tie mirror; CL structure R 351'-6 | - | p.143 | RASTER | inflection at FB17 / origin |
| Tie splices | table TS1 (-44.892, -5.614), TS2 (1.736, -9.498), TS3 (50.757, -12.756) vs framing plan TS(1)-TS(4) and erection "tie segments 1-5" | ft | p.120, 143, 93 | RASTER | conflict -> U5 |
| Tie splice detail | 2 - 9 x 3/4 bent web plates; flange 2 - 6 x 5/8 top + 5/8 bottom, 1'-6 1/2 long; 7/8 bolts | - | p.144 | RASTER | |
| Floorbeams | FB1-FB33 at T1-T33, radial, built-up T (1 in web, 7/8 x 12 flange), 8 7/8 at ends to 9 7/8 at crown, bolted to the tie web, bottom flange level with tie flange; 7/8 studs; 4 PT slots | - | p.143, 145, 146 | RASTER | `floorbeam-schedule.csv` (A/B symmetric, FBn = FB34-n) |
| Knuckle / end floorbeam | 2 in end flange, 1 in end web (21'-2 along the bent line), rib milled to bear (1'-11 5/8), 1/2 gussets, 4 PT anchorages; 9,700 lb; NW = SE, SW = NE | - | p.123-126 | RASTER | |
| Hangers | 32 per rib, crossed network C1-C32; 1.0 in (24.4 mm) galvanized spiral strand; open sockets at rib, adjustable sockets + turnbuckle at tie; lengths TC-HC 9.640-52.356 ft | - | p.147 | RASTER | `cable-hanger-coordinates.csv`: all 32 lengths = distance TC-HC within 0.001 ft; north rib = (-X,-Y,Z) |
| Hanger connections | rib fin plates (GR 50-F1, FCM, 1.378 in pin hole; angle B, P1-P4, weld D per cable); tie fin plates (TC at local Z 1.325-1.333); UHMW-PE cable connectors at crossings | - | p.148-150 | RASTER | `cable-fin-plates-arch-rib.csv`: D = P3 - 0.5, P3 = 0.7 P2, P4 = 0.1 P2 all rows; H(n) match p.147 |
| Hanger rib points | H points on R 125.16-125.19 (0.81-0.84 ft inside the rib WP), +0.12 to -0.16 ft off the 5 deg plane, linear in x | ft | p.147 | DERIVED | -> U6 |
| Deck | CIP class H (HPC) 5000 psi on 3 in, 8 in pitch, 12 ga galvanized composite steel deck (Gr 33, G165; Ip/In 3.06, Sp 1.66, Sn 1.67; 7.4 psf) spanning floorbeams (panels 5'-7 to 5'-10); 11 in above tie flange at tie; 1 % crown; 2 1/2 top cover; 27-D1 top / 27-D2 bottom @ 8; #5 in each corrugation | - | p.151-152 | RASTER | 3,698 SF |
| Longitudinal PT | 4 tendons (2 pairs 1'-4 apart) through FB slots, anchored in the knuckles; A416 Gr 270 LR, grouted | - | p.83, 145, 152, 155 | RASTER | strand count / force (p.155) not transcribed |
| Camber / no-load geometry | none given | - | p.119-150 | UNRESOLVED | U4 |
| Erection | off-line on falsework (tie segments 1-5, rib segments 1-5, props), deck cast from each end, PT at 4500 psi, props out at 5000 psi / >= 20 days, survey + hanger adjustment; SPMT roll-in, temporary blocking at bearings, survey + final hanger adjustment, weld sole plate, deck joints | - | p.92-93 | RASTER | |
| Member forces | rib / tie DC, DW, LL, WS, TU + limit states; hanger forces | - | p.121-122 | RASTER | check data, not transcribed |

### 3b. Approach spans (Tx28)
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Girders | TxDOT Tx28, 2 per span, IGND non-standard spans (strands from the IGND table) | - | p.108-118 | RASTER | lengths = bottom-flange lengths |
| Slab | 8 1/2 in class S (HPC) 4000 on PCP panels; X at CL brg 10 1/2, Y 3'-2 1/2; Z at mid-span unit 7-9: 11 7/8 / 11 1/4, 10 5/8 / 10 1/2, 1'-1 1/8 / 1'-1 1/8 | in | p.109, 113 | RASTER | per-unit tables |
| DL deflection unit 7-9 | A / B: span 7 0.051/0.072, 0.042/0.059; span 8 0.001/0.002; span 9 0.152/0.214, 0.145/0.204 | ft | p.113 | RASTER | |
| Bearings | standard pads; seats level 3'-0, 1 1/2 in min at CL brg | - | p.95, 97, 99 | RASTER | |

## 4. Substructure
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Abutment 1 | cap 14.583 ft x 3'-6 + backwall, 2 x 36 in shafts 9.000 apart; EL A/B 524.699, EL C/D 521.637; shaft top 519.153, tip 488.153, 31 ft | - | p.95 | RASTER | abutment 16 (p.96) not transcribed |
| Approach piers | single column 3'-0 sq (P2-5, 8, 9, 12, 13) or 3'-0 x 4'-0 (P6, 7, 14, 15; shaft 3.782 RT), cap 15.000 x 3'-6 (2'-6 / 1'-6 deep), 48 in shaft | - | p.97-100 | RASTER | `approach-piers.csv`; DERIVED EL C - EL D = layout H for all 12 |
| Arch piers 10 / 11 | hammerhead cap 24'-0 x 6'-0, 6'-0 deep (2'-9 + 3'-3), 9'-9 cantilevers; column 4'-6 sq; 78 in shaft; arch pedestals 3'-4 x 1'-7 LEVEL; approach pedestals 1'-6 x 1'-6; cap top 1/4 in/ft drain | - | p.102-104 | RASTER | bar list `bar-lists.csv` (17,130 lb = 8,937 + 8,193 OK) |
| Arch pier layout | CL arch brg 6 7/8 from CL pier; CL approach brg 1'-9 1/4; arch bearings 9'-10 and 9'-8 1/8 from CL pier & shaft; CL NHT 0'-7 from CL pier point; 19 deg 34'41" to tangent | - | p.102 | RASTER | DERIVED 19.510 vs T34'-T0 19.515 ft OK |

## 5. Reinforcing
| item | value | PDF p. | tag | note |
|---|---|---|---|---|
| Arch piers 10 & 11 | `bar-lists.csv` (47 marks; 12 rows differ 3-13 lb from no x length x unit weight; totals reconcile) | p.104 | RASTER | bend A table S5-S36 |
| Approach / abutments / arch deck | small per-sheet bar tables; arch deck bar bends p.154 | p.95-100, 109-117, 154 | - | not transcribed -> U13 |

## 6. Misc
| item | value | PDF p. | tag |
|---|---|---|---|
| Rails | RAIL (PED) low/high, SS316 posts on the arch; T80SS(MOD) at arch piers 78 LF | p.83, 89, 91 | RASTER |
| Joints | SEJ-B(MOD) 107 LF, BS-EJCP 1,382 lb | p.91 | RASTER |
| Paint / coating | system III-B, FS 17925; anti-graffiti 3,175 SF | p.83, 91 | RASTER |

### 6a. Railing, curbs, joints, mow strips - read 2026-10-12 (branch nht-rail-joints; model `samples/plans/rail-joints-nht.csx`)
| item | value | PDF p. | tag | model |
|---|---|---|---|---|
| Railing material / weight | posts and hardware stainless steel AISI 316; design dead load HIGH rail 100 PLF, LOW 50 PLF | p.83 | RASTER | envelope mass = 50 / 100 PLF |
| Rail limits | LOW 20+49.30 - 23+38.74; HIGH 23+38.74 - 29+10.87 (incl. arch, p.90) | p.89, 90 | RASTER | radial line at 23+38.74 |
| Rail LF | LOW 307.93 LT / 270.95 RT, HIGH 571.28 LT / 572.97 RT, measured on the face of rail = curb line 7'-0 from CL NHT (289.44 + 7.00 x 2.6411 rad = 307.93) | p.84-88 | RASTER / DERIVED | 307.928 / 270.952 / 571.284 / 572.976 |
| Rail pay items | 5110 LOW 579.0 (215.8 + 194.0 + 169.2), HIGH 1,144.2 (152.9 + 336.0 / 156.4 + 91.7 + 407.2) | p.91 | RASTER | 578.88 (p.91 is 0.12 more than p.84-88) / 1,144.26 |
| Post heights | LOW 4'-3, HIGH 9'-10 above the trail at the curb line; handrail 3'-0 | p.89 | RASTER | dimensioned values held |
| Post / handrail shapes | not dimensioned; from the p.89 page vectors (1.5 pt/in): LOW outline 6" at the base to 4 5/16" at the top, outer face 2" inside the deck edge, inner 2" outside the curb line; HIGH same to 69" above the curb, then curved inward (two cubic Beziers per face) to the tip 1'-1" inside the curb line; handrail pipe 1.87" OD | p.89 | VECTOR (scaled) | flagged envelope + handrail |
| Post spacing, base plates, anchors, rails, fencing | "the number and spacing of pedestrian railing posts is diagrammatic only"; details on the 'Pedestrian railing details' / RAIL (PED) sheets | p.84 note 2, p.89, 90 | RASTER | UNRESOLVED (sheets not in the set) |
| Curb | 10" x 4", curb line 7'-0 from CL NHT, exposed edges 3/4" chamfer | p.89, 113, 83 | RASTER | custom barrier IsCurb, 13.35 CY |
| SEJ-B(MOD) 4" + BS-EJCP | 7 joints (Abut 1, P4, P7, P10, P11, P13, Abut 16); CL SEJ 9 1/4" from CL pier at P10 / P11; 107 LF (75 + 32); BS-EJCP 194 lb per joint, 206 at P10 / P11 | p.84-88, 91, 113 | RASTER | 4 in placeholders on the CL SEJ, 111.57 LF at the full deck width; opening / armor / blockout / IGTS end UNRESOLVED |
| Mow strips | 4" reinforced concrete (#3 @ 18"), "2'-0 max, each face of column", at existing ground, 1'-0 min over the shaft top; 6.1 CY | p.97, 99, 91 | RASTER | 2'-0 at 12 piers = 6.12 CY; top EL. D + 1'-0 flagged |
| Abutment riprap | "riprap, see retaining wall sheets" | p.85 | RASTER | not modeled (wall sheets) |
| T80SS(MOD) at Piers 10 / 11 | 4'-6 high, 9'-1 + 4'-6 + 7'-11 straight + 7'-6 transition + taper, 2'-4 footings, on existing US 75 pavement; 78 LF | p.105-106 | RASTER | UNRESOLVED (T80SS section not in the set, pavement elevation not given) |

## 7. Checks data (CSV)
- `quantities.csv`: arch steel reconciles with the sheet tables (Gr 50-T1 120,100 = 61,000 + 59,100; Gr 50-F1 37,000 = 33,200 + 3,800; Gr 50 31,300 = 21,600 + 9,700).
- `cable-hanger-coordinates.csv`, `upper-lateral-bracing-points.csv`, `cable-fin-plates-arch-rib.csv`: arch geometry.
- `bearing-seat-elevations.csv`, `approach-piers.csv`: seat / cap / column checks.

## 8. Bearing elevations - reconciliation
Sources: p.91 (approach seat tables), p.95/97/99 (control elevations), p.102 (arch pier EL A-F), p.107 (arch bearing
height, sole plates), p.119 (arch WP elevations), p.123-125 (knuckle sections), p.20 (profile), p.92-93 (erection). No
arch seat table, no arch camber / deflection data, no review comment (none survive in the file).

**Approach spans - reconcile.**
| check | result | tag |
|---|---|---|
| seat - cap top (EL A/B), approach piers | min 0.125 ft (= 1 1/2 in min at CL brg) at every pier | DERIVED OK |
| PGL - average seat | 3.483-3.490 ft at all interior approach piers; P10 back 3.456 / P11 fwd 3.518 = same after the 1'-9 1/4 bearing offset x 1.61 % grade | DERIVED OK |
| P10 BACK seats (p.91) vs p.102 EL D / B | 533.400 / 533.345 both | RASTER OK |
| P11 FWD seats (p.91) vs p.102 EL B / D | 530.106 / 530.036 both | RASTER OK |
| column heights EL C - EL D vs layout H | all 12 equal | DERIVED OK |

**Arch span - does NOT reconcile.**
| item | P10 (west) | P11 (east) | source |
|---|---|---|---|
| tie WP elevation at both bearing ends | T34' 535.74 / T0 535.74 | T0' 532.49 / T34 532.49 | p.119 RASTER |
| PGL at CL pier - 13 in | 536.828 - 1.083 = 535.745 | 533.589 - 1.083 = 532.505 (plan 532.49, -0.015) | DERIVED |
| arch pedestal tops (LEVEL) EL A / EL E | 534.779 / 534.726 | 531.546 / 531.494 | p.102 RASTER |
| bearing height H (concrete to top of upper sole at CL brg) | 11.75 in (1/8 + 2 + 3 + 3.625 + 3, DERIVED OK) | 11.75 in | p.107 RASTER |
| upper sole bevel A / B / C | 3 1/8 / 3 / 2 7/8 (1.67 % vs WL 1.62 %) | same | p.107 RASTER |
| steel between WP and bearing | WP line inside the 2 in end flange (~1 in above its bottom, scaled) + continuous 1 in plate below = ~2 in; 1 in cheese plates only at the jacks; CL brg 2 in from T toward the span | same | p.124-125 UNVERIFIED (not dimensioned) |

| id | finding | numbers | pages |
|---|---|---|---|
| B1 | Pedestals of ONE pier differ although both tie ends of that pier have the same WP elevation (plane tilted about a pier-parallel line; pedestals drawn LEVEL). | P10 534.779 vs 534.726 (0.053 ft = 5/8 in); P11 531.546 vs 531.494 (0.052) | p.102, 119 |
| B2 | WP-to-pedestal drop inconsistent with the bearing stack. Expected WP - 2 in - 11.75 in = WP - 1.146 ft, i.e. P10 534.594, P11 531.344; plan pedestals are 0.132-0.202 ft (1 9/16 to 2 7/16 in) HIGHER. Even with no steel below the WP the residuals are +0.018 / -0.035 (P10), +0.035 / -0.017 (P11). | WP - mean pedestal: P10 0.9875 ft (11.85 in), P11 0.9700 ft (11.64 in) | p.102, 107, 119, 124 |
| B3 | P10 and P11 disagree with each other by 0.0175 ft (0.21 in) in WP-to-pedestal drop although the knuckles are rotationally identical. | 11.85 vs 11.64 in | p.102, 119, 123 |
| B4 | Bearing table labels "PIER 10 (BACK)" / "PIER 11 (AHEAD)" but the arch sits AHEAD of P10 and BACK of P11 (p.91 uses P10 BACK / P11 FWD for the approach girders). | - | p.107 vs p.91, 102 |
| B5 | Fixity naming: title "partially fixed" (sheet 2 of 2; sheet 1 of 2 not in set), pay item "ELASTOMERIC BEARING (FIXED)(ARCH)" 4 EA, p.102/103 notes "fixed bearing details". | - | p.91, 102, 107 |
| B6 | Sole plate weld: bearing note "upper sole plate welded to the arch rib"; erection stage 4 "weld bearing sole plate to deck plate"; the bearing is under the knuckle end flange / plate. | - | p.107, 93 |
| B7 | No camber / no-load geometry; WP coordinates not labelled final; p.107 note 8: contractor to adjust pedestal elevations to the fabricated bearing. | - | p.92-93, 107, 119 |
| B8 | Bearing line: p.119 T line 4 13/16 + p.124 CL brg 2 in = 6 13/16 vs p.102 6 7/8 from CL pier (1/16 in). Spacing along pier 19.510 vs 19.515 (0.06 in) OK. | - | p.102, 119, 124 |

Model recommendation: take the p.102 pedestal tops as plan values (what gets built); report B1-B3 against the p.119 WP
chain as warnings; fix neither side.

## Review comments
None found: 0 PDF annotations on 155 pages; optional-content layer "Review" (xref 130) is defined but empty (toggling it
changes no pixel); colour scan finds no red/blue markups (only blue PE signatures). Grey clouds such as "CAUTION OVERHEAD
ELECTRIC TO BE RELOCATED BY OTHERS" (p.87) are plan content. The file name says "comments" but the reduced export appears
to have stripped them. PDF bookmarks differ from the sheets ("162.77' ... UNIT", "122.30' ... UNIT" vs sheet titles
162.92' / 122.45', +0.15 ft each) - a revision trail, not a markup.

## UNRESOLVED
| id | item | PDF p. | reason |
|---|---|---|---|
| U1 | Arch pedestal / bearing elevations (B1-B3, B7) | p.102, 107, 119, 124 | pedestals do not reconcile with WP + bearing stack (0.13-0.20 ft; A vs E 0.053 ft) |
| U2 | Pier 12 station | p.94 vs p.87, 97 | foundation schedule 26+86.67 vs layout / pier table 26+86.87 (27+42.87 - 56.00 supports 26+86.87) |
| U3 | Arch chord CL1 / CL2 relative to piers | p.94, 102, 119 | plan chord 200.974 needs CL1/CL2 1.7 in RIGHT of the CL NHT pier points; shafts are 7 in LT (P10) / RT (P11); p.102 bearings put CL1 15/16 in from the shaft; no combination reproduces 200.974 |
| U4 | Arch camber / no-load geometry | p.119-150 | not given |
| U5 | Tie girder splices | p.120, 143, 93, 150 | TS1-TS3 table + p.120 / p.150 elevations vs TS(1)-TS(4) framing plan + 5 erection segments; v2 builds the framing-plan set at mid panel (flagged), north set not the rotation of the south set |
| U6 | Rib WP vs hanger H points and bracing U points (0.685 ft off plane) | p.147, 128 | hangers RESOLVED (v2): H(n) is the rib fin plate pin (P2 along the cable from HC(n), P1 off the arch plane = p.148 table, all 64 to 1/16"), HC(n) lies on the rib working line - the p.147 column labels are reversed (CONFLICT reported); bracing U points still open |
| U7 | Arch bearing labels / sheet 1 of 2 / fixity naming (B4, B5) | p.107, 91, 102 | sheet missing |
| U8 | Sole plate weld target (B6) | p.107, 93 | conflicting notes |
| U9 | Stale text: "CL PIER 7 / 8, bent 7", "RS6", deck quantity "span 7" for the arch | p.119, 127, 123, 151 | numbering inconsistent with layout |
| U10 | WP vertical position in the tie (bottom of flange p.119 vs inside the 2 in end flange p.124) | p.119, 124 | not dimensioned |
| U11 | Arch pier angle table (** 73 20'28" / 76 09'10", *** 73 28'39" / 76 31'36") | p.102 | reference lines of the ** / *** angles not identified |
| U12 | Horizontal datum / projection / scale factor - RESOLVED 2026-10-11 | p.17-19 | survey sheets 1-2 not in set; user GPS of the approximate arch-span centre 32.901380, -96.769012 -> EPSG:2276 X 2499728.33, Y 7015245.07 -> x SAF 1.000136 (TxDOT Dallas County) = 2500068.29, 7016199.14 vs model arch centre 2500079.15, 7016198.76 (10.9 / 0.4 ft = approximate GPS) => plan X/Y are TxDOT surface coordinates, NAD83 Texas North Central, SAF 1.000136. Model unchanged; `Doc.Project.Georeference` (build-nht.csx) writes IfcProjectedCRS EPSG:2276 + IfcMapConversion.Scale 1/1.000136. U1 (pedestal / bearing ELEVATIONS) is vertical and stays open. |
| U14 | Railing posts / spacing / base plates / anchors / fencing; low-high transition (2026-10-12) | p.84 note 2, 85, 89, 90 | 'Pedestrian railing details' / RAIL (PED) sheets not in the set, post spacing "diagrammatic only" - modeled as flagged envelopes of the p.89 section + handrail |
| U15 | SEJ-B(MOD) opening / armor / blockout, BS-EJCP plate, IGTS thickened slab end (2026-10-12) | p.84-88, 91, 113 | standards not in the set - 4 in placeholder on the CL SEJ, deck ends kept at the CL SEJ |
| U16 | Mow strip top (existing ground) and width (2'-0 max) (2026-10-12) | p.97, 99 | ground not given - 2'-0 (reproduces 6.1 CY), top EL. D + 1'-0 (minimum), flagged |
| U17 | T80SS(MOD) barrier at Piers 10 / 11 (2026-10-12) | p.105-106 | T80SS section not in the set, US 75 pavement elevation / lanes not given - not modeled |
| U13 | (2026-10-11: the PIER / GIRDER REPORTs p.108-116, IGND table p.118, section depths X/Y p.109/113/115, approach / arch pier shaft tables p.98/100/101 and abutment 16 p.96 are now transcribed in `samples/plans/build-nht.csx` / `check-nht.csx`.) Not transcribed (scope): approach PIER / GIRDER REPORTs (p.108-116), other unit depth / deflection tables, abutment 16, arch deck bar bends (p.154), PT details (p.155), knuckle stud layout (p.126), bracing connections (p.129-142), arch pier barrier (p.105-106), force tables (p.121-122) | listed | legible at 300-400 dpi; time |

## Model mapping
| section | model inputs | notes / GAPs |
|---|---|---|
| 1 Geometry | `Alignment` from `PiDefinition` (curves 102-109, no spirals); `VerticalProfile` (p.20); `CrossSlopeTable` 1 % crown about CL NHT | fits existing inputs |
| 2 Layout | `BridgeDefinition.Supports` (16; approach radial, arch piers 19 deg 34'41"); units with SEJ at P4, P7, P10, P11, P13 | GAP: joint 9 1/4 in off CL pier at P10/P11; two structure types on one pier |
| 3b Approach | prestressed path, Tx28 (`TxGirderCatalog`), 2 girders, chorded spans, IGND strands, PCP + 8 1/2 slab | per-span girder angles / spacing from PIER REPORTs (U13); one `PrestressedBeamDefinition` for all spans (known GAP) |
| 4 Substructure | approach piers: single-column bent + `CapProfile` + shaft; abutments: cap + backwall on 2 shafts; arch piers: hammerhead `CapProfile` 24 x 6 ft, 4'-6 column, 78 in shaft, `PedestalSizeAlong/Across` | GAP: arch pedestal tops are fixed plan values not driven by `SteelModel.BearingSeats` (needs a fixed-seat-elevation input + chain warning) |
| 5 Reinforcing | arch pier bar list as check for `SubstructureRebar` | rest not transcribed |
| 8 Bearings | approach: standard pads (seat table = check); arch: 15 x 15 elastomeric H 11.75 with beveled sole | GAP: beveled-sole elastomeric bearing with masonry plate + anchor pin under a knuckle |

**Tied arch - what SBM lacks (program work), in build order:**
1. Local structure frame - origin (station / offset / elevation), X azimuth along the chord, tilt (1.039 deg about a pier-parallel line); all arch members input in local coordinates.
2. Arch rib generator - circular (later parabolic) rib from chord + radius / rise, inclination about the chord (5 deg), splice positions; W-shape swept on a 3D arc with the web in the arch plane (weak-axis bend); curved splice plates. No curved-in-elevation member exists today.
3. Tie girder generator - built-up T swept along its own plan curve (R 342'-4 / 360'-8 + straights) in the tilted plane, splices, fin plates, FCM tags. The girder generator is alignment-offset driven; the ties are not offsets of CL NHT.
4. Hangers (cables) - straight cables between table anchor points (crossed network), sockets / turnbuckles, rib and tie fin plates, cable connectors at crossings. New element type (IFC IfcTendon / IfcMember CABLE).
5. Floor system - floorbeams between the ties at panel points (radial, crowned, variable depth, PT slots, studs) and skewed end floorbeams; the cross-frame generator only frames between alignment-driven girders.
6. Bracing - upper lateral W8x28 X-bracing between inclined ribs from a panel-point table, gussets.
7. Knuckle / end assembly - rib-tie-end floorbeam joint, milled bearing, PT anchorages, jacking points; defines the arch bearing positions (6 7/8 in off CL pier) and the seat chain.
8. Deck on steel deck - CIP slab on corrugated deck spanning floorbeams, edges = curved tie lines, curbs, longitudinal PT tendons (deck generator assumes girders + haunch).
9. Arch bearings - elastomeric + masonry / lower sole / beveled upper sole + anchor pin and bolts on a skewed pier.
10. Camber / erection geometry - none in the plans (camber is also a known global GAP); model final geometry only.
11. Checks - hanger lengths (reproduced exactly), rib radius / plane, pedestal chain (section 8), steel quantities vs `quantities.csv`.
