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
| Horizontal clearance | 10'-3" min; 17'-9 7/8" min | - | p1 (plan + section) | RASTER | 7/8 confirmed at 500 dpi, both occurrences |

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
| Beam bars | G301E, G402E, G303E, G505E, G508E, 4-G806E (shapes in bend-diagrams.csv) | - | p16 | RASTER | no bill (beam bars paid with the beam) |
| Stirrup spacing (each half, from beam end) | 2" + 4 sps @ 2 1/2" (= 3'-10" with the next group) + 9 sps @ 4" + 19 sps @ 9" + 27 sps @ 14" + 6" to CL span; symm. about CL span -> 60 positions per half, 120 per beam | - | p16 elevation | RASTER | 2 + 10 + 36 + 171 + 378 + 6 = 603" = 50'-3" = 100'-6"/2 ok; 2" + 3'-10" = 48" ok |
| End-zone bars | 2-G505E (C bar: 2'-1" vertical, 3'-6" top flange leg, 1'-0" bottom leg; 1 1/4" clr to web faces, 2" clr top) then 2-G508E (L: 2'-10" + 1'-0" leg, projects 7" above beam; note 11: each pair may be one No. 5 bulbed bar); G402E (hairpin 2'-10" x 4" o-o, projects 7" above beam) at all other positions ("G402E SPG. except as noted") | - | p16 elevation, end view, section | RASTER | which of the 5 end positions take G505E vs G508E is only shown by leaders (drawing not to scale there) |
| G301E | straight 2'-6", top flange transverse, 1 1/2" clr top, one at each stirrup position ("space with G505E, G508E or G402E") | - | p16 | RASTER | |
| G303E | bottom flange pairs (2'-11" + 3 1/2" offset + 1'-10" @ 4:12, crossed): 2-G303E sp. w/ G505E, 2-G303E sp. w/ G508E, 2-G303E sp. w/ G402E over the end region; 1 1/8" clr entire bottom flange (note 10) | - | p16 | RASTER | extent of the G402E-paired G303E zone not dimensioned (UNRESOLVED) |
| G806E | 4 longit. No. 8 in top flange at 3 1/2" + 3 sps @ 9" = 2'-3" + 3 1/2" (= 2'-10" ok); permissible splice 5'-1" min. lap; note 7: two inside bars may be placed against the stirrup | - | p16 end view / elevation | RASTER | length not given (no bill) |
| Beam top / ends | top: 6" bond-breaker strips at flange tips (note 8), 1'-10" rough-floated center (note 9); ends: 1/2" chamfer, no chamfer at top | - | p16 | TEXT | |
| Intermediate diaphragms | none | - | p2, p16 | TEXT | temp. bracing |
| End diaphragms | concrete, both abutments, semi-integral (p17 "integral & semi-integral abutment") | - | p15, p17, p20, p21 | TEXT | detail rows below |
| End diaph. thickness | 3'-0" (plan: 1'-6" back face -> CL end diaph + 1'-6" -> front face) | - | p20 plan, p21 A-A/B-B | TEXT | |
| End diaph. stations across | back face = back face of abutment; CL brg 2'-6" from back face (1'-3" + 3" + 1'-0"); front face of diaph 6" ahead of CL brg; front face of abut 1'-6" ahead of CL brg | - | p21 A-A/B-B, p20 plan (10"+1'-0"+6"+1'-0" stack) | TEXT | 1'-3"+3"+1'-0"+1'-6" = 4'-0" stem ok; 2'-6"+6" = 3'-0" ok |
| End diaph. bottom | back part (x -2'-6" .. -1'-3") on the back ledge on 1" Type A polystyrene (bottom = ledge + 1"); FRONT part (x -1'-3" .. +6") bottom = BOTTOM OF THE BEAMS: p21 A-A (between beams) draws the diaphragm bottom as the continuation of the beam-bottom line (vector: one line), B-B the same at the beams, p11 A-A draws it 3" (the bearing) above the pedestal top; 3" x 3" chamfer at the back-bottom corner (p11 Detail A); 3" Type B polystyrene between the face x -1'-3" and the seat riser x -1'-0" from the ledge up to the chamfer; 1" backer rod at the back face | - | p21 A-A / B-B, p11 A-A + Detail A | TEXT + vector | ledge = footing top + 1'-7 1/4" S (922.354) / 1'-4 3/4" N (922.146), p11 note 3 |
| End diaph. depth | "VARIES" (top of deck to bottom); back face: 1'-4" paving-bracket notch, then S609E B.F. 2 eq. sps + 2 eq. sps + 4" | - | p21 A-A/B-B | TEXT | total not dimensioned; follows deck profile |
| Paving bracket | 8" wide x 1'-4" deep notch at back face top; 1/2" x 7" bit. felt; 4" (note 1: to approach panel dowel bar); 5 1/2" deck end to first S615M; 4 1/2" clr S617S; sawcut joint (note 4) | - | p21, p20 plan (8" paving bracket) | RASTER | 5 1/2, 4 1/2 confirmed on clip |
| Front fillet | between beams, front face of diaph meets slab underside with a 1'-0" (vert, A-A) x 1'-0" (horiz, B-B) 45 deg fillet; S405E 2 bars | - | p21 A-A, B-B | TEXT | slab 9" above fillet |
| Beam embedment | beam ends 7 1/2" behind CL brg inside diaph (std table E); 2 x 1 1/2" dia. holes per beam for 2-S506E thru beams: 30MH C = 3 1/2", D = 1'-1 1/4" above bottom of beam, E = 7 1/2" along CL beam; A/B (parapet abut.), X/Y NA | - | p17 table (30MH row), p20/21 | RASTER | |
| End diaph. length | 41'-0" (WL -23'-6" .. +17'-6") = between mask wall inner face (-23'-7") and infill wall W end (+17'-7") less 1" polystyrene each end (p3 corners, p11 Detail C); S609E / S405E 40'-8" = 41'-0" - 2 x 2" clr ok | - | p5, p3, p11, p20 | DERIVED | no direct dimension on p20 |
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
| Future-beam pedestals | built NOW (only the beams are future): bars in the bar lists A527M 24 = 3 x 8, A528M 15 = 3 x 5 (p8), B527M 18 = 3 x 6, B528M 12 = 3 x 4 (p14); in the abutment Cr-epoxy quantities (p4 480 lb = A508M + A613M + A527M + A528M; p9 390 lb); p2 SOQ footnote 2 "includes ... future abutment pedestal reinforcement"; no sheet defers them | - | p2, p4, p5, p8, p9, p10, p14 | TEXT | "(future)" in seat-elevations.csv = for a future beam |
| Type A (S) | 8-A525E trans. (3 3/8" + 7" + 5 sps abt. @ 7" = 2'-11 1/4" + 7" + 3 3/8" = 4'-8" ok); 5-A526E longit. (3 5/8" + 5" + 7 1/4" + 2 sps @ 4 1/4" + 3 5/8" = 2'-4" ok); 2" clr; 1 set 2-A529E ties (note 1: only where pedestal >= 9" tall, 2 pedestals), 4" / 4" in E-E; 2 anchor rods on CL brg | - | p6 plan + E-E | RASTER | 5 x 7 = 2'-11" vs '2'-11 1/4" abt.' as printed |
| Type B (S, future) | 8-A527M trans. (same 3 3/8"/7" layout); 5-A528M longit. (4" + 4 1/2" + 7" + 2 sps @ 4 1/4" + 4" = 2'-4" ok); 3" clr; no A529E | - | p6 plan + F-F | RASTER | |
| Type C (N) | 6-B525E trans. (4 3/8" + 5 sps @ abt. 6 5/8" = 2'-9 1/4" + 4 3/8" = 3'-6" ok); 4-B526E longit. (4 3/8" + 3 sps @ abt. 6 3/8" = 1'-7 1/4" + 4 3/8" = 2'-4" ok); 2" clr; B529E (note 1, 2 pedestals) | - | p12 | RASTER | |
| Type D (N, future) | 6-B527M trans., 4-B528M longit., same layout as C; 3" clr; no B529E | - | p12 | RASTER | |
| Pedestal heights | not dimensioned; top = seat-elevations.csv, bottom = seat | - | p5, p10 | DERIVED | |
| Pedestal tops | PG - 2%|off| - 3.88/4.15 | ft | p5, p10 | TEXT | seat-elevations.csv |
| Infill wall | 1'-6", top 927.73 S / 927.80 N (level) | - | p5, p11 | TEXT | |
| Infill wall extent | only in the east 18'-5" (future widening): from abutment E end (WL +36'-0") to WL +17'-7" (18'-5" + 17'-7" = 36'-0" ok); its W end = deck/end diaph E end +17'-6" + 1" Type B polystyrene (p11 Detail C, p3 SE corner: 1" at slab, 1 1/2" at approach panel); under the superstructure the end diaphragm sits on the back ledge instead | - | p5/p10 plan + elev., p3, p11 | TEXT | both abutments (N mirrored); pipe railing posts 1'-8 1/4" + 3 @ 5'-0" + 1'-8 3/4" = 18'-5" ok |
| Abutment E end | footing, stem and infill wall end flush at WL +36'-0" with 2" x 12" keyways (note 4) for the future widening; no mask wall at the E end | - | p5, p10 | TEXT | |
| Mask walls | W end only; top 930.61 S / 930.67 N; h 12'-10 3/8" S / 12'-11" N; 1'-5 1/2" x 6'-4"; coping 1'-1"x2" | - | p11 | RASTER | ok |
| Mask wall position across | front (outer, W) face 25'-0 1/2" W of WL; inner face at WL -23'-7" = footing W end (25'-0 1/2" - 1'-5 1/2") = deck edge -23'-6" - 1" polystyrene (1 1/2" at approach panel, p3); coping projects 2" beyond front face | - | p5, p10, p3 | RASTER | 1/2 fractions confirmed on p5 clip |
| Mask wall position along | 6'-4" long; plan p5: from footing front edge (2'-9" ahead of CL brg) to footing back edge (3'-3" behind CL brg) = 6'-0", outer 7" (incl. 2" coping) extends 4" further back (D-D: 1'-0 1/2" + 7", 4" step) -> 6'-4" ok; stands beside the footing W end down to 917.75, 5" x 1'-0 1/2" toe notch with 2" Type B polystyrene (C-C) | - | p5 plan, p11 D-D / C-C, p3 SW corner | DERIVED | ends not dimensioned directly; from footing lines 9" + 4'-0" + 1'-3" = 6'-0" + 4" = 6'-4"; p3 scales the same |
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
| Deck longit. | S401M 60'-0" + S402M 45'-2", 2'-7" lap; 55 bot + 29 top lines (p18/19) | TEXT | length ok |
| Deck longit. layout | top: 3 1/2" + 8 1/2" + 26 sps @ 1'-6" = 39'-0" + 8 1/2" + 3 1/2" = 41'-0" -> 27 + 1 extra line 3 1/2" from each deck edge = 29; bottom: 7" + 5" + 52 sps @ 9" = 39'-0" + 5" + 7" = 41'-0" -> 53 + 1 extra line 7" from each edge = 55 (U4 closed) | RASTER | p19 transverse section | 84 lines x (1 S401M + 1 S402M) = 168 = 84 + 84 bill ok |
| Bend diagrams | bend-diagrams.csv: 52 bent marks of the bills (A 17 p8, B 17 p14, S 8 p21, R 10 p24) + 5 beam G bars (p16, no bill). 25 OK (sum of out-to-out dims = listed length exactly); 22 have one leg / hook not dimensioned on the sheet (listed - dims given as DERIVED, not checkable); 5 barrier loops R505M/R517E/R506E/R507E/R518E NOT RECONCILED (-2 9/16 .. +3/16 in vs true-length sum; std barrier sheet length convention not stated) | RASTER | convention p8/p14 note: "ALL BENT BAR DIMENSIONS ARE GIVEN OUT-TO-OUT"; p21/p24 no note; pin diameters not shown on any sheet |
| Beam bars | p16, see sec. 3 rows + bend-diagrams.csv (G bars) | RASTER | no bill |

## 6. Misc
Barrier Type S (TL-4 deck / TL-3 approach panel), control joints 11 sps @ abt. 9'-4 1/4" = 102'-11" (RASTER p22, 1/4 confirmed at 500 dpi; 11 x 9'-4 1/4" = 102'-10 3/4", 'ABT.') p22/24/25; pipe railing 18 LF per abutment, posts 3 @ 5'-0" (p5, p10, p23); approach panel cover plates p27; no expansion devices, 1" polystyrene diaphragm/mask wall (p19, p21); no drains; underpass lights about 248+40 / 249+28, 1" PVC-coated RMC, 3" sleeves (p18, p20, p26); slope paving 15 SY (p28); nameplate B101, 2 emblems (p29, p11); sign by others (p1).

## 7. Checks data
SOQ p2: barrier 273 LF; struct. conc. 140 CY (71+69 ok); bars 19200/19970/250; slab 4220 SF; planing 4954 SF; 14 bearings; 704 LF 30MH; test piles 100 + 105 ft; piling 2405 LF; pipe railing 36 LF; slope paving 15 SY; conduit LS. working-points.csv, seat-elevations.csv, camber (p16), pile loads (p4/p9).

## UNRESOLVED
| id | open item | pages | status |
|---|---|---|---|
| U1 | horizontal datum not stated | p34, p36, p37 | UNRESOLVED |
| U2 | end diaphragm geometry | p17, p20, p21, p11, p3, p5 | closed (sec. 3); length 41'-0" DERIVED from mask wall / infill wall faces - no direct dimension; front-part bottom (user decision 14) closed by p21 A-A / B-B + p11 A-A / Detail A: bottom of the beams, 3" x 3" chamfer (sec. 8) |
| U3 | bend dimensions | p8, p14, p21, p24 | closed for 25 marks (bend-diagrams.csv); still UNRESOLVED: 22 marks with one leg / hook not dimensioned on the sheet (A/B 504E, 505E, 514E, 420E, 423E, 502E hooks, 503E hooks; S615M, S616M, S617S; R501M-R504E foot; R408E 2:12 leg); barrier loops R505M/R517E/R506E/R507E/R518E do not reconcile with the true-length sum (std barrier sheet length convention not stated, p24); pin diameters not shown on any sheet |
| U4 | deck line counts 55/29 vs spacing | p19 | closed: 1 extra line 7" (bot) / 3 1/2" (top) from each deck edge |
| U5 | 17'-9 7/8", 9'-4 1/4" raster | p1, p22 | closed (RASTER) |
| U6 | sole plate B303 | p30 | closed |
| U7 | pedestal Type A/B (and N Type C/D) | p6, p12 | closed; pedestal is 2'-4" across (not 2'-6" = seat width) |
| U8 | pile tip elevations | p4, p9 | UNRESOLVED |
| U9 | mask / infill wall E end and corners | p3, p5, p10, p11 | closed: infill wall only in the E 18'-5"; mask wall W only; mask wall along-bridge ends DERIVED from footing lines (not dimensioned directly) |
| U10 | camber shape | p16 | UNRESOLVED |
| U11 | beam bars: extent of the G402E-paired G303E zone; G806E bar length; which end positions take G505E vs G508E | p16, Fig. 5-397.501(B) | PARTLY CLOSED: G505E at the first station, G508E at the other four of the 2 1/2" group (standard figure: that group is the splitting reinforcement, G402E "spg. except as noted"); G303E-with-G402E extent and G806E length stay UNRESOLVED (not placed) |
| U12 | pedestal heights (only tops given) | p5, p10, p6 | DERIVED from seat-elevations.csv minus seat; no direct dimension |

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

## 8. Semi-integral abutment - p21 SECTION A-A / B-B (read 2026-10-12, vector linework, 3 pt per inch)
x from the CL brg (+ toward the span), z from the deck top in the schematic (deck 9", haunch drawn 3", beam 30").
| item | value | source |
|---|---|---|
| Back face of abutment AND diaphragm | x -2'-6" (chain 1'-3" + 3" + 1'-0") | A-A / B-B dims |
| CL end diaphragm / front face of diaphragm | -1'-0" / +6" (1'-6" + 1'-6", 6") | dims |
| Front face of abutment (stem) | +1'-6" | dim |
| Back part | x -2'-6" .. -1'-3", bottom = ledge + 1" (1" Type A polystyrene), top = deck soffit; paving bracket 8" (x -2'-6" .. -1'-10" = slab end) with its top 1'-4" under the deck top, 1/2" x 7" bit. felt | dims |
| Seat riser / 3" Type B polystyrene | riser at x -1'-0" (= seat back edge, ledge + 1'-0"); polystyrene x -1'-3" .. -1'-0" from the ledge to the chamfer | dims, Detail A |
| Front part bottom | bottom of the beams (one line with the beam bottom beyond), 3" x 3" chamfer at x -1'-3" | vector, Detail A |
| Front fillet | 1'-0" (vertical, A-A) x 1'-0" (horizontal, B-B) under the slab at the front face | dims |
| Beams | end 7 1/2" behind CL brg inside the diaphragm; drawn bottom 3" above the pedestal top (bearing) | p17 table, vector |
| Pedestal | x -1'-0" .. +1'-4" on the seat (2" short of the front face), top >= 2" above the seat back edge (p11 A-A "2" MIN.") | dims |
| Seat | 1'-0" above the ledge at the riser, 1" slope down to the front face (seat front 923.27 S / 923.06 N) | p11 A-A, p5 / p10 |
| Stem / footing | stem 4'-0" (back ledge 1'-6" + seat 2'-6") on the 6'-0" x 3'-0" footing, 9" back toe / 1'-3" front toe at the footing top 920.75, 2" x 12" keyway | p11 A-A |
| Infill / mask wall | infill wall 1'-6" on the back ledge only WL +17'-7" .. +36'-0" (1" Type B polystyrene to the diaphragm end, Detail C); W mask wall beside the footing end (inner face -23'-7"), 917.75 .. 930.61 / 930.67 | p5 / p10 / p11 |
| Approach panel | rests on the paving bracket; S617S (stainless) ties the diaphragm into it; 5 1/2" from the deck end to the first S615M, 4 1/2" clr S617S | A-A |
| Expansion | semi-integral: diaphragm + beams move on the curved-plate bearings (F1 fixed S, E1 expansion N); polystyrene on the ledge / at the riser / at the ends, backer rod at the back face | A-A, p31 |

## 9. Reinforcing placed (2026-10-12) - see MODEL.md
All 93 bill marks are in the model: abutments A / B (58, `rebar-abutments-br02069.csx`), deck S401M-S404M (`rebar-deck-br02069.csx`, DeckPlanReinforcement),
end diaphragm S405E-S617S (`rebar-diaphragms-br02069.csx`), barrier on the deck R501M / R505M / R411M / R413M / R612M / R614M (`rebar-barrier-br02069.csx`);
approach-panel barrier bars (12 marks) scheduled only (no approach panel in the model). Counts = bills, weights within 0.5 lb per mark, coating totals = SOQ.

