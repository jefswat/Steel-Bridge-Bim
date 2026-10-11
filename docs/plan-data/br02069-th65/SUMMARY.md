# Br. 02069 - T.H. 65 S.B. over 117th Ave (MnDOT) - plan data summary

Set `br02069-th65`, `Br02069_Certified Plans_20250926.pdf` (39 pages, PDF page = sheet). Standard: `../EXTRACTION.md`. Index: `INDEX.md`. CSVs: `working-points.csv`, `strands-30MH-B1.csv`, `seat-elevations.csv`, `bar-lists.csv`, `bend-diagrams.csv`. Scripts/caches: `temp/br02069/` (not committed; `checks.py` alignment/profile/WP recompute, `bars.py` bar CSV + weight check). Tags: TEXT / RASTER / DERIVED / UNVERIFIED / UNRESOLVED.

Tools: `python` on PATH has PyMuPDF but not pypdfium2; the pypdfium2 scripts (`words.py`, `clips.py`, `clip.py`) run with `C:\SBM\temp\pytools\python.exe -I`.

Bridge: single simple span 99'-3" CL-CL brg, 0 deg skew, seven MnDOT 30MH pretensioned beams @ 6'-0", 9" CIP slab 41'-0" o-o, concrete end diaphragms (no intermediate diaphragms), curved-plate bearings (fixed S / expansion N), pile-supported abutments (12" CIP pipe piles) behind MSE walls, Type S 36" barriers.

## 1. Geometry control
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Alignment | SB65 = working line | - | p1, p3, p36 | TEXT | |
| SB65 tangent | POT 208+26.273 R7 (X 508139.723, Y 149812.027) -> PC 261+56.990 R7 (X 508208.766, Y 155142.296) | ft | p36 pts 1074/1075, p1 | TEXT | |
| Azimuth | 0 deg 44'31.598" | - | p36 | TEXT | p1/p3: 0 deg 44'31.6" |
| Tangent check | PC from POT+az: dX 0.000 dY +0.001 | ft | - | DERIVED | checks.py |
| Station region | R7 (SB65 equations R1..R7, p36 1054-1074); last eq. 208+26.909 R6 BK = 208+26.273 R7 AH | - | p36 | TEXT | |
| Next curve | HPI 264+63.237 R7, 0 deg 23'23.73" LT, R 90000, PC 261+56.990 | - | p36 | TEXT | off bridge |
| Horizontal datum | not stated | - | p34, p36 | UNRESOLVED | plain X/Y |
| Vertical datum | NAVD 88 (adj.); BM 0208 G 907.780, BM 0208 AD 899.992 | ft | p34 | TEXT | |
| Profile | VPC 242+75.000 EL 920.329; VPI 249+00.000 EL 935.954; VPT 255+25.000 EL 920.329 | ft | p1, p34 | TEXT | crest |
| Grades / VC | G1 +2.500%, G2 -2.500%, L 1250.000', M -7.813' | - | p1, p34 | TEXT | M=(G2-G1)L/8 DERIVED ok |
| Cross slope | crown at working line, -0.02 ft/ft both sides, constant | ft/ft | p2, p19 | TEXT | no superelevation |
| Cross slope check | 8 plan top-of-roadway elevs = PG - 0.02|off| within 0.005 | ft | p1, p3 | DERIVED | |
| Station equations on bridge | none; control points CP1/CP2 | - | p1, p3, p34 | TEXT | working-points.csv |
| Begin/end bridge | 248+32.92 / 249+35.83, EL 928.05 / 928.12 | ft | p1 | TEXT | = CL brg -/+ 1'-10" ok |
| Working points A-F | coords, top of roadway, seat | - | p3 | TEXT | working-points.csv, max diff 0.001 |
| WP distances | A-B, C-D, E-F 99.25; A-C 21.00; C-E 15.00; A-D/B-C 101.45; C-F/D-E 100.38; A-F 105.58 | ft | p3 | TEXT | recomputed ok |
| Roads under | EB117, WB117A/B, ramps (p35, p37) | - | p35, p37 | TEXT | not extracted |
| Vertical clearance | points A and B: 16'-11 1/8" | - | p1 notes 3,4 | RASTER | |
| Horizontal clearance | 10'-3" min; 17'-9 7/8" min | - | p1 | UNVERIFIED | 7/8 not rasterized |

## 2. Layout
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Supports | S abut CL brg 248+34.75; N abut CL brg 249+34.00 | ft | p1, p3, p15 | TEXT | |
| Span | 99'-3" (Span 1) | - | p1, p15 | TEXT | 99.25 DERIVED ok |
| Skew | 0 deg both | - | p1, p3, p15 | TEXT | |
| Units / joints | 1 unit, no deck joints (end diaphragms + approach panels) | - | p15, p18 | TEXT | |
| Deck | 41'-0" x 102'-11" o-o | - | p1, p2, p18 | TEXT | 4220 SF = SOQ ok |
| Deck edges | W -23.50, E +17.50 from WL (+ = east/right) | ft | p1 | DERIVED | 1'-6"+22'-0"+16'-0"+1'-6" |
| Roadway | 38'-0": 22'-0" W + 16'-0" E of WL; 4' shldr, 2x12' lanes, 10' shldr | - | p1, p2, p18 | TEXT | |
| Barriers | Type S TL-4 36", 1'-6" wide, both edges; o-o W 142'-11", E 129'-7" | - | p1, p2, p18, p19, p24 | TEXT | 272.5 -> 273 LF ok |
| Girders | 7 x 30MH, 6 @ 6'-0" = 36'-0", overhangs 2'-6" | - | p2, p15, p19 | TEXT | |
| Girder offsets | -21, -15, -9, -3, +3, +9, +15 | ft | p3, p15 | DERIVED | |
| Marks | all B1; X end at N (E1X), F1 at S | - | p15 | TEXT | |
| End of slab | 1'-10" from CL brg | - | p1, p18 | TEXT | |
| Future widening | 18'-0" east (3 beams @ 6'); substructure to +36.00, 3 future pedestals | - | p2, p5, p10 | TEXT | |
| Companion | Br. 02070 NB, 72'-0" SB65-NB65; not in set | - | p1, p2 | TEXT | |

## 3. Superstructure
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Beam | MnDOT 30MH pretensioned "30MH-101", Fig. 5-397.501 mod. (drape removed, debonding added) | - | p16 | TEXT | |
| Length | 100'-6" o-o; 99'-3" brg; 7 1/2" end-CL brg | - | p16 | RASTER | 99.25+2x0.625 ok; 7x100.5=703.5 -> 704 LF ok |
| Section | H 30"; top flange 2'-10"; bottom 3'-3"; web 6 1/2" | - | p16 | RASTER | |
| Verticals (top down) | 5", 1 5/8", 1'-0 7/16", 5 7/16", 5 1/2" | in | p16 | RASTER | sum 30 ok |
| Horizontals | top: web face->5 5/16"->8 7/16" edge (13 3/4 ok); bottom: 4 1/8+10 3/4+1 3/8 (16 1/4 ok) | in | p16 | RASTER | |
| Corners | 6" R web fillets, 1" R top corners, 2" R bottom flange top corners, 3/4" bottom chamfer, 1/2" end chamfer | - | p16 | RASTER | |
| Strands | 44 x 0.6" Gr 300 LR straight; 18@y2, 18@y4, 8@y6; 2" grid | - | p16 | RASTER | strands-30MH-B1.csv |
| Debonding | A 4 @ 9'-0"; B 6 @ 13'-0"; C 4 @ 17'-0" each end; 14 total | - | p16 | RASTER | |
| Optional top strands | 2 x 1/2" @ 5 kips, not in Pi | - | p16 | TEXT | |
| Prestress | 0.72 fpu; Pi 2,062,370 lb | - | p16, p1 | RASTER | 44x0.217x0.72x300 ok |
| Losses | ES 24.93; LT 26.26; total 51.19 ksi | ksi | p16 | TEXT | sum ok |
| Concrete | beam f'ci 8.0, f'c 9.5; RC 4 ksi | ksi | p16, p1 | TEXT | |
| Camber | erection 7 3/8"; DL defl 5 1/4" int / 5 1/8" ext; residual 2 1/8" int / 2 1/4" ext | in | p16 | RASTER | differences ok |
| Beam bars | G301E, G402E, G303E, G505E, G508E, 4-G806E; stirrups 9@4", 19@9", 27@14" | - | p16 | RASTER | no bill |
| Intermediate diaphragms | none | - | p2, p16 | TEXT | temp. bracing |
| End diaphragms | concrete, both abutments, semi-integral (p17 "integral & semi-integral abutment") | - | p15, p17, p20, p21 | TEXT | detail rows below |
| End diaph. thickness | 3'-0" (plan: 1'-6" back face -> CL end diaph + 1'-6" -> front face) | - | p20 plan, p21 A-A/B-B | TEXT | |
| End diaph. stations across | back face = back face of abutment; CL brg 2'-6" from back face (1'-3" + 3" + 1'-0"); front face of diaph 6" ahead of CL brg; front face of abut 1'-6" ahead of CL brg | - | p21 A-A/B-B, p20 plan (10"+1'-0"+6"+1'-0" stack) | TEXT | 1'-3"+3"+1'-0"+1'-6" = 4'-0" stem ok; 2'-6"+6" = 3'-0" ok |
| End diaph. bottom | rear 1'-3" sits on the abutment back ledge on 1" Type A polystyrene; 3" Type B polystyrene against the seat riser (the 3" in the dim chain); 1" backer rod at back face | - | p21 A-A, p11 Detail A | TEXT | bottom elevation = back ledge + 1" (S 922.35 / N 922.15 DERIVED, sec. 4) |
| End diaph. depth | "VARIES" (top of deck to bottom); back face: 1'-4" paving-bracket notch, then S609E B.F. 2 eq. sps + 2 eq. sps + 4" | - | p21 A-A/B-B | TEXT | total not dimensioned; follows deck profile |
| Paving bracket | 8" wide x 1'-4" deep notch at back face top; 1/2" x 7" bit. felt; 4" (note 1: to approach panel dowel bar); 5 1/2" deck end to first S615M; 4 1/2" clr S617S; sawcut joint (note 4) | - | p21, p20 plan (8" paving bracket) | RASTER | 5 1/2, 4 1/2 confirmed on clip |
| Front fillet | between beams, front face of diaph meets slab underside with a 1'-0" (vert, A-A) x 1'-0" (horiz, B-B) 45 deg fillet; S405E 2 bars | - | p21 A-A, B-B | TEXT | slab 9" above fillet |
| Beam embedment | beam ends 7 1/2" behind CL brg inside diaph (std table E); 2 x 1 1/2" dia. holes per beam for 2-S506E thru beams: 30MH C = 3 1/2", D = 1'-1 1/4" above bottom of beam, E = 7 1/2" along CL beam; A/B (parapet abut.), X/Y NA | - | p17 table (30MH row), p20/21 | RASTER | |
| End diaph. length | not dimensioned (W end against mask wall, E end 1" Type B polystyrene, Detail C p11); S609E / S405E 40'-8" | - | p20, p11 | UNRESOLVED | 40'-8" + 2 x 2" clr = 41'-0" deck width would fit, but end clear not stated |
| PVC sleeve | 3" dia. PVC through diaph, 1'-0"+/- from WL, 2'-0" below top, S abut only (note 1) | - | p20 | TEXT | lighting conduit |
| Diaph. rebar sets | between beams 2 sps @ 1'-0" (3 sets S510E/S511E/S513E/S615M/S616M/S617S); at beams 4 sps @ 1'-0" (3 sets S510E/S512E/S514E/S615M); 1 set at each end; 5-S609E B.F., 4/3-S609E, S607E/S608E F.F. between beams | - | p20 | TEXT | |
| Slab | 9" min; top clr 2", bottom 1" | - | p2, p3, p19 | TEXT | |
| Stool at CL brg | 4 1/4" both | in | p3 | RASTER | |
| Roadway-to-seat | S 9+4 1/4+30+3 1/4=46 1/2" (3.88'); N 9+4 1/4+30+6 1/2=49 3/4" (4.15') | - | p3 | RASTER | ok |
| Bearings | 7 F1 fixed B310 (S), 7 E1 exp B311 (N) = 14 | - | p15, p31, p2 | TEXT | |
| F1 | pad 12x30x1/2; SF 8.6; brg plate 14x47x1 1/2; curved 4 1/2x32x1 1/4 R16; L 3 1/4"; A-3; anchor rods 1 1/2" @ +/-1'-9 1/2" | - | p31, p5 | RASTER | L sum ok |
| E1 | pad 12x30x3 3/4 (6 plates 1/8, 5 lam 1/2, 1/4 covers); SF 8.6; brg plate 14x33x1 1/2; curved 4 1/2x32x1 1/4 R16; L 6 1/2"; A-3 | - | p31 | RASTER | sums ok |
| Sole plate | B303 (std, MH row): 3'-2 3/4" (across beam) x 1'-3" (along beam) x 3/4"; sides chamfered (sides only); galvanized; cast with beam | - | p30 B303 | RASTER | |
| Sole plate CL brg | CL brg = CL shear studs at 7 1/2" from plate end (M/MN/MH/RB); plate end flush with beam end (beam end 7 1/2" to CL brg) | - | p30, p16 | RASTER | 1'-3" = 2 x 7 1/2" ok |
| Sole plate studs | 6 x 7/8" dia. x 4" to 5" unheaded shear studs: 3 across @ 6" (13 3/8" + 6" + 6" + 13 3/8" = 3'-2 3/4" ok) x 2 rows @ +/-3" from CL brg; AWS D1.1 | - | p30 | RASTER | note 2: may be modified to clear strands |
| Pintle holes | 2 x 1 3/4" dia. (for 1 1/2" pintles) at +/-8" from CL beam (11 3/8" from plate edge, 11 3/8 + 8 = 19 3/8 = half width ok), on CL brg; 1/4" bevel; 2 1/2" x 1 3/4" Type B polystyrene plug | - | p30 | RASTER | |
| Seats | S 923.76 W / 923.88 E fascia; N 923.55 / 923.67 | ft | p3 | TEXT | seat-elevations.csv |

## 4. Substructure (2 abutments, no piers)
Common: footing 59'-7" (offset -23.58 to +36.00), 6'-0" x 3'-0", bottom EL 917.75; stem 4'-0"; 2 rows x 7 piles.
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Footing | 59'-7" x 6'-0" x 3'-0", bot 917.75, top 920.75 | - | p4, p9, p11, p5 | TEXT | |
| Footing across | back toe 9", stem 4'-0", front toe 1'-3"; back->CL brg 3'-3", CL brg->front 2'-9" | - | p11 A-A, p4/p9 | RASTER | ok |
| Stem | back ledge 1'-6" + seat 2'-6" (1'-0" behind CL brg, 1'-6" to front face) | - | p11 A-A | RASTER | |
| Stem heights | footing->ledge 1'-7 1/4" S / 1'-4 3/4" N; ledge->seat 1'-0"; seat slopes 1" to front | - | p11 | RASTER | |
| Seat front | S 923.27, N 923.06 level | ft | p5, p10 | TEXT | derived chain ok |
| Back ledge | S 922.35, N 922.15 | ft | - | DERIVED | infill 5'-4 1/2" / 5'-7 7/8" ok |
| Pedestals S | 10 x 4'-8" (along abut.) x 2'-4" (across), gaps 1'-4", ends 8" E / 3" W; 7 beam + 3 future | - | p5, p6 | TEXT | sum ok; across = 1'-0" behind + 1'-4" ahead of CL brg (p6), flush with seat back edge, 2" short of seat front face (seat 2'-6"); earlier '2'-6"' was the seat width |
| Pedestals N | 10 x 3'-6" (1'-9" + 1'-9") x 2'-4" (1'-0" + 1'-4"), gaps 2'-6", ends 1'-3" E / 10" W; 2" short of seat front | - | p10, p12 | TEXT | end assignment DERIVED |
| Pedestal types | S: Type A U.N.O., Type B per note 6 (future) (p5 notes 5/6, details p6); N: Type C U.N.O., Type D per note 6 (p10 notes 5/6, details p12) | - | p5, p6, p10, p12 | TEXT | |
| Type A (S) | 8-A525E trans. (3 3/8" + 7" + 5 sps abt. @ 7" = 2'-11 1/4" + 7" + 3 3/8" = 4'-8" ok); 5-A526E longit. (3 5/8" + 5" + 7 1/4" + 2 sps @ 4 1/4" + 3 5/8" = 2'-4" ok); 2" clr; 1 set 2-A529E ties (note 1: only where pedestal >= 9" tall, 2 pedestals), 4" / 4" in E-E; 2 anchor rods on CL brg | - | p6 plan + E-E | RASTER | 5 x 7 = 2'-11" vs '2'-11 1/4" abt.' as printed |
| Type B (S, future) | 8-A527M trans. (same 3 3/8"/7" layout); 5-A528M longit. (4" + 4 1/2" + 7" + 2 sps @ 4 1/4" + 4" = 2'-4" ok); 3" clr; no A529E | - | p6 plan + F-F | RASTER | |
| Type C (N) | 6-B525E trans. (4 3/8" + 5 sps @ abt. 6 5/8" = 2'-9 1/4" + 4 3/8" = 3'-6" ok); 4-B526E longit. (4 3/8" + 3 sps @ abt. 6 3/8" = 1'-7 1/4" + 4 3/8" = 2'-4" ok); 2" clr; B529E (note 1, 2 pedestals) | - | p12 | RASTER | |
| Type D (N, future) | 6-B527M trans., 4-B528M longit., same layout as C; 3" clr; no B529E | - | p12 | RASTER | |
| Pedestal heights | not dimensioned; top = seat-elevations.csv, bottom = seat | - | p5, p10 | DERIVED | |
| Pedestal tops | PG - 2%|off| - 3.88/4.15 | ft | p5, p10 | TEXT | seat-elevations.csv |
| Infill wall | 1'-6", top 927.73 S / 927.80 N | - | p5, p11 | TEXT | |
| Mask walls | top 930.61 S / 930.67 N; h 12'-10 3/8" S / 12'-11" N; 1'-5 1/2" x 6'-4"; coping 1'-1"x2" | - | p11 | RASTER | ok |
| Joints | contraction B801 at 29'-4" (S) / 30'-10" (N) from east end; 2"x12" keyways | - | p5, p10, p32 | TEXT | |
| Piles | 12" CIP, 1/4" shell, B201 splice; 2 rows x 7 @ 9'-5", ends 1'-6" W / 1'-7" E | - | p4, p9, p29 | RASTER | sum ok |
| Pile rows | back 1'-9" behind CL brg, front 1'-3" ahead; 1'-0" embed; plumb | - | p4, p9, p11 | RASTER | no batter noted |
| Lengths | S 13 x 95 + test 105; N 13 x 90 + test 100 (back row, +6.17) | ft | p4, p9 | TEXT | 2405 LF ok |
| Loads | 68.5 + 26.9 = 95.4 t; lateral 5.70 / 0.82 k | - | p4, p9 | TEXT | |
| Resistance | MPF12 phi 0.50 Rn 190.8; PDA 0.65 146.8 | t | p4, p9 | TEXT | ok |
| Wingwalls | none; MSE walls 35/36 | - | p1, p3 | TEXT | |
| Borings | B23 248+11.256 24.7' RT; B24 249+38.524 37.5' RT | - | p39 | TEXT | |

## 5. Reinforcing
bar-lists.csv: 93 marks (A 29 p8, B 29 p14, S 17 p21, R 18 p24; 10 R rows RASTER). E epoxy, M Cr-epoxy, S stainless.
| item | value | tag | note |
|---|---|---|---|
| S abut | 7200 / 480 lb (p4) | DERIVED | ok |
| N abut | 6950 / 390 (p9) | DERIVED | 6945/394 |
| Superstructure | 5050 / 19100 / 250 (p18) | DERIVED | 5046/19097/250 |
| Bridge | 19200 / 19970 / 250 (p2) | DERIVED | ok |
| Deck transverse | 137 S403M top, 137 S404M bot, 136 @ 9" (p18) | TEXT | |
| Deck longit. | S401M 60'-0" + S402M 45'-2", 2'-7" lap; 55 bot + 29 top lines (p18/19) | TEXT | length ok; U4 |
| Bend diagrams | bend-diagrams.csv: 52 bent marks of the bills (A 17 p8, B 17 p14, S 8 p21, R 10 p24) + 5 beam G bars (p16, no bill). 25 OK (sum of out-to-out dims = listed length exactly); 22 have one leg / hook not dimensioned on the sheet (listed - dims given as DERIVED, not checkable); 5 barrier loops R505M/R517E/R506E/R507E/R518E NOT RECONCILED (-2 9/16 .. +3/16 in vs true-length sum; std barrier sheet length convention not stated) | RASTER | convention p8/p14 note: "ALL BENT BAR DIMENSIONS ARE GIVEN OUT-TO-OUT"; p21/p24 no note; pin diameters not shown on any sheet |
| Beam bars | p16 | TEXT | |

## 6. Misc
Barrier Type S (TL-4 deck / TL-3 approach panel), control joints 11 sps @ about 9'-4 1/4" (UNVERIFIED) p22/24/25; pipe railing 18 LF per abutment, posts 3 @ 5'-0" (p5, p10, p23); approach panel cover plates p27; no expansion devices, 1" polystyrene diaphragm/mask wall (p19, p21); no drains; underpass lights about 248+40 / 249+28, 1" PVC-coated RMC, 3" sleeves (p18, p20, p26); slope paving 15 SY (p28); nameplate B101, 2 emblems (p29, p11); sign by others (p1).

## 7. Checks data
SOQ p2: barrier 273 LF; struct. conc. 140 CY (71+69 ok); bars 19200/19970/250; slab 4220 SF; planing 4954 SF; 14 bearings; 704 LF 30MH; test piles 100 + 105 ft; piling 2405 LF; pipe railing 36 LF; slope paving 15 SY; conduit LS. working-points.csv, seat-elevations.csv, camber (p16), pile loads (p4/p9).

## UNRESOLVED
U1 horizontal datum (p34/36/37). U2 end diaphragm geometry (p20/21/17). U3 bend dimensions (p8/14/21/24). U4 deck line counts vs spacing, 55 vs 53 bot, 29 vs 27 top (p19). U5 17'-9 7/8", 9'-4 1/4" not rasterized (p1, p22). U6 sole plate B303 dims (p30). U7 pedestal Type A/B details (p6). U8 pile tip elevations (p4/p9). U9 mask/infill wall east-end and corner extents (p3/5/10/11). U10 camber shape (p16).

## Model mapping
| section | model inputs | notes |
|---|---|---|
| 1 | Alignment (SB65 PI chain R1..R7 or R7 tangent), VerticalProfile (1 crest VC), cross slope crown pivot 0, -2%/-2% | datum U1; US ft |
| 2 | supports (2 abutments, skew 0), GirderLayoutDefinition 7 lines -21..+15, deck edges -23.5/+17.5, BarrierDefinition, end of slab 1'-10" | asymmetric girders |
| 3 | GirderLayoutDefinition.Kind = TxDotPrestressed; PrestressedBeamDefinition Type=Custom + PrecastIBeamDims or PGSuper import; StrandLayout 44 StrandPoints from strands-30MH-B1.csv (Harped=false); f'c 9.5 / f'ci 8.0; 0.6" / 300 ksi; BearingToEnd 7 1/2"; pad inputs; haunch 4 1/4"; GirderLayoutDefinition.Camber | gaps |
| 4 | SubstructureDefinition x2 + AbutmentDefinition (stem, seat, ledge, MaskwallDefinition); footing 59'-7"x6'x3' with explicit FootingPoint piles (14, plumb); BearingSeat tops from seat-elevations.csv | gaps |
| 5 | PlanBarGroup (bar-lists.csv), abutment schedules | deck/barrier schedule only |
| 7 | ElevationCheck chain (p3), span 99.25, seats, bar weights | |

Program gaps: (1) no MnDOT shapes in TxGirderCatalog / TxGirderType; 30MH needs Custom dims or PGSuper, straight-taper outline can't do 6" R fillets, fit to 1/16" unchecked; (2) no debonding in StrandPoint, PgSuperImport models full length (14/44 strands); (3) TxDOT-specific IGND / IGEB / naming; (4) no curved-plate bearing assemblies (pad only); (5) no concrete end diaphragm / semi-integral abutment; (6) PileType has no round CIP pipe pile; (7) no standalone future pedestals or per-abutment pedestal sizes; (8) AbutmentDefinition (Iowa) vs ledge/infill/mask-wall abutment not verified; (9) only TxDOT SSTR barrier profile; (10) prestressed camber not modeled in the haunch.
