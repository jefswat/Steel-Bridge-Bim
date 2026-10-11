# woodbury-i29-floyd - plan data summary

Iowa DOT Design 817 (File 30377), I-29 NB over Floyd Blvd, Sioux City, Woodbury County; HDR; Sept 2015, Rev. 1 04-07-16.
223'-10 7/8 x 36'-0 pretensioned prestressed concrete beam bridge (Iowa BTD bulb tees), STAGE 1 only (stage 2 =
Design 217, other project, not in this set). Format per `docs/plan-data/EXTRACTION.md`. Sheet index: `INDEX.md`.
"p." = PDF page, "DS" = design sheet (DS = p - 2). Tags: TEXT / RASTER / DERIVED / UNVERIFIED / UNRESOLVED.
Text-layer fraction glyphs in this set are not unique (same code for 15/16 and 1/2), so all fractions are RASTER.

## 1. Geometry control

| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Alignment | CL I-29 (stationing for the whole bridge) | - | p.6 / DS 4 | TEXT | Floyd Blvd crossing STA 464+51.66 CL I-29 = STA 103+29.91 CL Floyd |
| Horizontal curve PI | STA 469+93.34 | sta | p.6 / DS 4 | RASTER | I-29 curve data |
| Delta | 29 deg 19'58.71" LT | - | p.6 / DS 4 | RASTER | |
| R | 2320.00 | ft | p.6 / DS 4 | RASTER | |
| T / L / E | 607.19 / 1187.74 / 78.14 | ft | p.6 / DS 4 | RASTER | DERIVED check: R tan(D/2) = 607.19, R*D = 1187.75 |
| PC / PT | STA 463+86.15 / STA 475+73.89 | sta | p.6 / DS 4 | RASTER | PC lies in span 1 (9.73 ft before pier 1) |
| Spirals | none shown | - | p.6 | TEXT | simple curve |
| Station equations | none shown | - | p.6 | TEXT | |
| Coordinates / datum | none in this set (only lat 42.486925, long -96.395158; FHWA 700310; maint. 9747.5R029) | - | p.6 / DS 4 | UNRESOLVED | no N/E of PI/PC; road plans IM-NHS-029-7(49) referenced |
| Benchmark | BM 634, STA 464+46.17, 320.14' LT, elev 1100.54 | ft | p.6 | TEXT | |
| Profile grade line | P.G.L. I-29 N.B., 5.00 ft RT of CL I-29 (27'-0 - 22'-0) | ft | p.6, p.18 / DS 4, 16 | RASTER | typical section: CL I-29 -5'-0- PGL NB -22'-0- CL NB roadway |
| Vertical curve | PVI STA 462+50.00, elev 1124.11, VC 700', G1 = +1.9791 %, G2 = -0.6143 % | - | p.6 / DS 4 | RASTER | BVC 459+00 (1117.183), EVC 466+00 DERIVED; whole bridge inside the VC |
| Superelevation | 2 % at STA 462+89.05, linear transition 194'-0 to 5 % at STA 464+83.05, constant 5 % beyond | - | p.6 / DS 4 | RASTER | plane slope rising to the right (east) across the full stage-1 deck, pivot = PGL NB |
| Profile check | CL NB roadway at S abut brg / pier 1 / pier 2 / N abut brg = 1122.92 / 1123.25 / 1123.49 / 1123.27 | ft | p.6 / DS 4 | TEXT | DERIVED from VC + 22.00 ft x e: 1122.92 / 1123.25 / 1123.49 / 1123.27 - all reconcile to 0.01 |
| Profile check (east edge) | S abut east edge of deck 1123.88 (p.33) | ft | p.33 / DS 31 | TEXT | DERIVED 1123.88 using the edge point 6.7 ft back-station (skew) and e = 2.68 % |
| Floyd Blvd profile | PVI 103+00.00, elev 1096.78, VC 100', G1 -1.0000 %, G2 +1.0000 % | - | p.6 / DS 4 | RASTER | underpass |
| Min. vertical clearance | 20.52' (overhead STA 464+49.38 30.61' RT elev 1123.61; underpass 103+60.29 4.00' LT elev 1097.56; superstructure depth 5.53') | ft | p.6 / DS 4 | RASTER | check value |

### Staking (support points on CL NB roadway / extended tangent)
| support | station, offset (CL I-29) | tag | note |
|---|---|---|---|
| CL S abut brg | STA 463+39.99, 27.00' RT | RASTER p.7 | on tangent |
| CL pier 1 | STA 463+95.87, 27.02' RT | RASTER p.7 | situation plan p.6 gives 463+95.88, 27.00' RT (point on curved CL NB roadway) |
| CL pier 2 | STA 465+00.94, 29.88' RT | RASTER p.7 | p.6: 465+01.60, 27.00' RT |
| CL N abut brg | STA 465+60.17, 33.62' RT | RASTER p.7 | p.6: 465+61.85, 27.00' RT |

Staking points lie on the EXTENDED TANGENT of CL NB roadway (27.00' RT of the back tangent, continued straight past the PC).
DERIVED check: a point on the extended tangent at distance x past the PC has offset sqrt(x^2+2347^2)-2320 and station
PC + 2320 atan(x/2347): x = 9.84 / 116.22 / 176.38 ft reproduce 463+95.87 27.02 / 465+00.94 29.88 / 465+60.17 33.62.

## 2. Layout

| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Supports | S abut (integral), Pier 1, Pier 2, N abut (integral) | - | p.6, p.21 | TEXT | continuous 3-span (piers: beams made continuous by deck + pier diaphragm, "fixed pier") |
| Skew | all support lines parallel, 10 deg 09'36" (L.A.) to the extended tangent | - | p.7 / DS 5 | RASTER | |
| Skew to local tangent | S abut 10 deg 09'36", pier 1 10 deg 24'01", pier 2 13 deg 00'40", N abut 14 deg 29'57" | - | p.21 / DS 19, p.6 | RASTER | DERIVED from 10 deg 09'36" + deflection to the CL NB point: pier 1 10 deg 24'01" ok; pier 2 12 deg 59'50", N abut 14 deg 27'28" - see UNRESOLVED U4 |
| Spans along CL NB roadway (curved) | 55'-11 15/16, 106'-11 7/16, 60'-11 1/2; total 223'-10 7/8 | - | p.6, p.21 | RASTER | sum = 223'-10 7/8 DERIVED ok |
| Spans along extended tangent | 55'-11 7/8, 106'-4 5/8, 60'-1 13/16 | - | p.7 / DS 5 | RASTER | DERIVED from staking x: 56.00, 106.38, 60.16 ft (within 0.01) |
| Beam CL-CL bearing (along CL NB) | span 1 54'-11 15/16, span 2 104'-11 7/16, span 3 59'-11 1/2 | - | p.21 / DS 19 | RASTER | pier bearings 1'-0 each side of CL pier (along CL NB) |
| Beam bearing lines at piers | 1'-0 CL pier to CL brg (dimension string "measured along CL NB"); 11 13/16 and 11 3/4 (pier 1 back / ahead), 11 3/4 and 11 5/8 (pier 2) measured perpendicular to the pier | in | p.21, p.9 | RASTER | DERIVED (2026-10-11): the four perpendicular values = 1'-0 x cos(beam angle) of each side's beams (1'-0 ALONG THE BEAM: 11.810 / 11.745 / 11.745 / 11.647); 1'-0 along CL NB would give 11.80 / 11.80 / 11.69 / 11.69. Model: 1'-0 along the beam (CL NB string then 11.94-12.05 in, within 1/16) |
| Units / joints | one continuous unit, no deck joints (integral abutments) | - | p.18-20 | TEXT | |
| Deck length | 226'-11 3/4 face to face of paving notches / out to out of deck along CL NB; deck end 1'-6 1/4 beyond CL S abut brg, 1'-6 5/8 beyond CL N abut brg | - | p.6, p.22 | RASTER | DERIVED 1'-6 1/4 + 223'-10 7/8 + 1'-6 5/8 = 226'-11 3/4 ok; general notes table: 227.0 LF, 37.6 LF, 8,528 SF |
| Deck width | 37'-7 out to out = 36'-0 roadway (CL NB roadway to east gutter line) + 1'-7 barrier | - | p.8, p.18 | RASTER | west edge of deck = CL NB roadway = longitudinal construction joint to stage 2 (no barrier, 3'-3 bar projection) |
| Deck edges | curved: west edge R = 2347.00' (CL NB roadway); CL I-29 R 2320.00' | ft | p.22 / DS 20 | RASTER | beams are straight chords per span |
| Barrier | east side only, Iowa aesthetic barrier, 3'-8 high, 1'-7 base; 236'-9 3/8 end to end (bid 236.8 LF) along gutter line; abut diaph. ext. 4'-11 3/8 (S) / 4'-11 3/4 (N) + 226'-10 1/4 end to end of slab | - | p.38 / DS 36 | RASTER | bump-outs at piers (Detail C p.22: 5'-2 long, 2'-7 + 2'-7, 1'-3) |
| Girders | 5 beam lines D, E, F, G, H (west to east); lines A-C are stage 2 | - | p.21 | TEXT | |
| Girder spacing (perp. to beams) | span 1: 4 @ 8'-0 5/16 = 32'-1 1/4; span 2: 4 @ 7'-11 13/16 = 31'-11 1/4; span 3: 4 @ 7'-11 = 31'-8 | - | p.21 / DS 19 | RASTER | sums DERIVED ok |
| Girder spacing along bearing lines | abutments: 4 @ 8'-1 7/8 = 32'-7 1/2 (both); pier caps: 4 @ 8'-1 7/8 (pier 1), 8'-0 11/16 + 3 @ 8'-1 7/8 (pier 2, along CL pier) | - | p.21, p.15, p.9 | RASTER | |
| Beam D from CL NB roadway (along CL brg) | S abut 3'-1 11/16; pier 1 back 3'-3 1/2, ahead 2'-10 15/16; pier 2 back 3'-0 11/16, ahead 2'-7 3/8; N abut 2'-8 1/16 | - | p.21 / DS 19 | RASTER | W.P. = CL pier x CL NB roadway |
| Cap layout along CL pier | cap 42'-1 5/8 long; cap end (stage joint) to beam D 6'-2 7/8 (3'-1 3/4 + 3'-1 1/8); beam H to east end 3'-3 1/4 | - | p.9 / DS 7 | RASTER | pier 2: 6'-4 1/16 + 35'-9 9/16 |
| Staging | stage 1 builds 37'-7 wide NB segment; stage 2 (61'-4, Design 217) later; traffic on existing bridges | - | p.8 / DS 6 | TEXT | splice bars (stainless) project at the stage joint |

## 3. Superstructure

| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Girder type | Iowa PPC bulb tee "D" (BTD): span 1 BTD55, span 2 BTD105, span 3 BTD60 (5 each) | - | p.21, p.25, p.27 | TEXT | Iowa standards 4730 / 4732 / 4733 / 4742 |
| BTD section | depth 4'-6; top flange 2'-10 wide, 3 13/16 edge + 1 11/16 taper; web 6 1/2; bottom flange 2'-6 wide, 7 1/2 + 5 1/2 taper; straight web 2'-11 1/2; fillets R = 8 (web-flange), R = 2 (bottom), 3/4 chamfer; flange widths each side 1'-1 3/4 (top), 11 3/4 (bottom) | in | p.28 / DS 26 | RASTER | sum of heights = 54.0 DERIVED ok; outline details right side: 4 5/8, 7 15/16, 7 1/16, 10 13/16, 2 13/16, 6 3/16, 1 13/16, 4 5/8, 6 |
| BTD properties | A = 748.8 in2, yb = 24.64 in, I = 285,860 in4 | - | p.28 / DS 26 | RASTER | |
| BTD beam data | see table below | - | p.27 / DS 25 | RASTER | |
| End of beam to CL bearing | 8 | in | p.28-30, p.20 | RASTER | beam ends 8 in apart over piers (Detail A p.20) |
| Strand | 0.60 in, Gr 270 low relaxation, As 0.217 in2, initial 72.6 % f's | - | p.27 | TEXT | min breaking 58.6 kips |
| Strand pattern BTD55 | 14 straight: row 2" at x = 2, 6, 10, 12, 18, 20, 24, 28 (8); row 6" at x = 2, 6, 10, 20, 24, 28 (6) (x from flange edge, bottom flange 30 in) | in | p.28 / DS 26 | RASTER | |
| Strand pattern BTD60 | 12 straight + 2 deflected: row 2" straight at 2, 6, 10, 12, 18, 20, 24, 28; deflected at 14, 16 (row 2"); row 6" at 2, 12, 18, 28; deflected strands at end 4 in below top; hold down 6'-0 each side of midspan | in | p.29 / DS 27 | RASTER | |
| Strand pattern BTD105 | 28 straight + 6 deflected on a 2 in grid: row 2" x = 2-12 and 18-28 (12); row 4" x = 2, 6, 8, 10, 12, 18, 20, 22, 24, 28 (10); row 6" x = 2, 6, 12, 18, 24, 28 (6); deflected at x = 14, 16 in rows 2/4/6; at end deflected rows 2 @ 2 starting 4 in below top; hold down 10'-6 each side of midspan | in | p.30 / DS 28 | RASTER | counts reconcile with table (28 / 6) |
| Camber (design, beam info) | BTD55 0.37 / 0.65; BTD105 1.95 / 3.45; BTD60 0.39 / 0.69 (initial / final, in) | in | p.25 / DS 23 | TEXT | |
| Beam camber data (deck sheet) | span 1 as erected 11/16, deck in place 1/2; span 2 3 7/16 / 1 11/16; span 3 11/16 / 7/16 | in | p.24 / DS 22 | RASTER | |
| Intermediate diaphragms | steel, one line per span near midspan, staggered per bay; C15x33.9 (11), W21x50 (1), W14x38 (1); 7/8 H.S. bolts (A449) through cast holes; galvanized; 4,258 lb | - | p.31 / DS 29 | RASTER | bolt hole chain per beam: `diaphragm-bolt-holes.csv` |
| Pier / abutment diaphragms | cast-in-place, monolithic with deck; pier diaphragm 2'-0 wide between beam ends region (bars 5d13-5d18, 5e) | - | p.18-20 | TEXT | |
| Bearings at piers | plain neoprene pad 9 x 1 x 2'-4 1/2, one per beam end (2 per beam line per pier), CL pad = CL beam bearing 1'-0 off CL pier | in | p.20 / DS 18, p.4 | RASTER | quantity sheet: "PLAIN NEOPRENE 1"" |
| Bearings at abutments | beam on 3x3 x 2'-4 1/2 steel bar (integral abutment) | in | p.18, p.20 | RASTER | "BEAM BEARING (IN.)" = 3 at abutments, 1 at piers (p.25) |
| Beam seat elevations | `beam-information.csv` (S abut 1117.50-1118.31, pier 1 1117.94-1119.06, pier 2 1118.21-1119.82, N abut 1117.89-1119.51) | ft | p.25 / DS 23 | TEXT | DERIVED check: top of deck - T - 4.5' - bearing reproduces seats (e.g. D pier 1: 1123.35 - 9 7/8" - 4.50 - 1" = 1117.94; H N abut 1125.02 - 9 1/8" - 4.50 - 3" = 1119.51) |
| Deck slab | 8 in structural (incl. 1/2 in integral wearing surface), nominal deck thickness at beams 9 in = 8 + 1 haunch | in | p.18, p.24 | RASTER | detailing deflections assume 8 in slab + 1.5 in haunch (p.27) |
| Deck thickness at beams (T) | table, 15 points x 5 lines, 9 to 10 in | in | p.24 / DS 22 | RASTER | `deck-thickness-at-beams.csv` |
| Overhang / edge | east overhang to edge: beam H to east edge VARIES (p.18); edge thickness 8 3/4, 3/4 drip groove 3 in from edge | in | p.18 / DS 16 | RASTER | exact overhang = from deck edge geometry |
| Haunch data | beam line haunch elevations (27 points x 5 lines), anticipated deflection, cross-slope adjustment, field haunch max 2 1/2 (0.208), min 0 to 3/8 | - | p.34 / DS 32 | TEXT/RASTER | `haunch-elevations.csv`, `haunch-misc-data.csv` |
| Studs / shear | beam stirrups 4d1, 5b1 hairpins project into deck | - | p.27-30 | TEXT | |
| Beams set vertical | yes | - | p.18 notes | TEXT | |

BTD beam data (p.27 / DS 25, RASTER):

| beam | span CL-CL brg | overall L | f'ci / f'c ksi | strand | straight / deflected | initial prestress kips | hold down kips | camber release / after losses in | defl. immediate / time in | max spacing | weight t | concrete cy | rebar lb |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| BTD55 | 55'-0 | 56'-4 | 4.50 / 5.00 | 0.60 | 14 / - | 596 | - | 0.37 / 0.65 | 0.17 / 0.04 | 9'-3 | 22.0 | 10.8 | 1602 |
| BTD60 | 60'-0 | 61'-4 | 4.50 / 5.00 | 0.60 | 12 / 2 | 596 | 13.6 | 0.39 / 0.69 | 0.24 / 0.06 | 9'-3 | 23.9 | 11.8 | 1709 |
| BTD105 | 105'-0 | 106'-4 | 6.00 / 7.00 | 0.60 | 28 / 6 | 1446 | 21.9 | 1.95 / 3.45 | 1.86 / 0.47 | 9'-3 | 41.5 | 20.5 | 2741 |

Note: standard beam lengths (55'-0 / 105'-0 / 60'-0 CL-CL bearing) differ from the layout CL-CL beam bearing
lengths along CL NB roadway (54'-11 15/16 / 104'-11 7/16 / 59'-11 1/2) - see U3.
**USER DECISION (2026-10-11, U3 closed):** the beams are the Iowa standard BTD beams, taken from the standard, not
designed for this bridge. Model every beam at the STANDARD length (55'-0 / 105'-0 / 60'-0 CL-CL bearing, 56'-4 /
106'-4 / 61'-4 overall) with the standard's strands and details. The layout bearing distances are NOT beam lengths;
report the difference against the layout per beam line as a check (warning), do not stretch the beams.

**Decision 8 (2026-10-11, user: "these beams are chorded ... something is wrong with the geometry"): FOUND.** The model read the
p.21 beam-D offsets "measured along CL bearing" from the point where each PIER BEARING LINE crosses CL NB; they are aligned
dimensions from the W.P. (note on p.21: W.P. = CL pier x CL NB), i.e. from the W.P.'s foot on the bearing line. At the piers the
bearing line is 1'-0 off the pier, so the two zero points are 12 sin(skew) apart along the line (2.2" at pier 1, 2.7" at pier 2,
opposite directions back / ahead): every span's beams were rotated 0.19-0.21 deg. Verified independently (python, from the plan
values only) and in the model. With the W.P. origin (and the pier bearing lines 1'-0 along the beam, see the row above) the
CL-CL bearing CHORD of every beam (3D, along the sloped beam) is the standard length within 1/16":

| span | standard (p.27; std sheets 4732 / 4742 / 4733 = Iowa Beams.pdf p.100 / 110 / 101) | chord before | chord now (D..H) | layout along curved CL NB (p.21) |
|---|---|---|---|---|
| 1 BTD55 | 55'-0 | 54'-11.60 (-0.40") | 54'-11.98 to 55'-0.01 (-0.017 to +0.013") | 54'-11 15/16 |
| 2 BTD105 | 105'-0 | 104'-10.95 (-1.05") | 104'-11.95 to 104'-11.98 (-0.053 to -0.024") | 104'-11 7/16 |
| 3 BTD60 | 60'-0 | 59'-11.31 (-0.69") | 60'-0.00 (0.000") | 59'-11 1/2 |

The p.21 CL NB values are along the CURVED CL NB at a different angle from the beams, not beam lengths. The standard-length beam
therefore sits on the layout bearings; no placement rule is needed (generator: Info "Standard beam fit", no UNRESOLVED). The same
error caused the 48 check-woodbury fails next to the piers (beam-line top of deck, haunch elevations, pier seats) and the along-bearing
spacing 8'-1.80 vs 8'-1 7/8: now spacing 8'-1.86 / 8'-1.90 / 8'-1.88, top of deck 216/216, seats 30/30. Hole chains (p.31) sum to the
standard end-to-end lengths 56'-4 / 106'-4 / 61'-4, consistent.

## 4. Substructure

### Piers 1 and 2 (stage 1 portion; columns 2, 3, 4)
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Type | 3-column pier with cap; footing per column; thin veneer brick aesthetic | - | p.9-14 | TEXT | column no. 1 = stage 2 |
| Cap | 4'-3 5/8 wide x 5'-0 deep (at stage-joint end; 4'-11 + 1/2 at east end), 42'-1 5/8 long along CL pier; top and bottom sloped with cross slope; low step / steps per beam | - | p.9, p.11 | RASTER | section A-A p.11 |
| Cap top elevations | pier 1 / pier 2: D (low step) 1117.94 / 1118.21, E 1118.23 / 1118.62, F 1118.52 / 1119.03, G 1118.80 / 1119.43, H 1119.06 / 1119.82, J 1117.25 / 1117.42, K 1118.76 / 1119.54 | ft | p.9 / DS 7 | TEXT | table of pier elevations (layout confirmed RASTER) |
| Cap bottom at columns | L 1112.36 / 1112.57, M 1113.01 / 1113.48, N 1113.65 / 1114.39 | ft | p.9 | TEXT | |
| Columns | section 5'-3 5/8 (along pier) x 4'-3 5/8, with 4 in projections on the long faces (overall 5'-11 5/8) | - | p.9, p.11 (B-B) | RASTER | column CLs: col 2 at 2'-11 13/16 from cap west end, spacing 18'-1, 18'-1, col 4 CL 2'-11 13/16 from east end |
| Column heights (DIM A) | pier 1: 17'-4 5/16, 18'-0 1/8, 18'-7 13/16; pier 2: 17'-6 13/16, 18'-5 3/4, 19'-4 11/16 (col 2, 3, 4) | - | p.9 / DS 7 | RASTER | DERIVED: L/M/N - (1090.00 + 5.00) reproduce all six |
| Footings | 13'-6 (perp. to pier) x 10'-0 (along pier) x 5'-0, square to pier; bottom elev 1090.00 (both piers) | - | p.9, p.11 | RASTER | 3 footings per pier; footing CL = column CL |
| Piles | 12 HP12x84 per footing, grid 3 (along pier: 1'-6, 3'-6, 3'-6, 1'-6) x 4 (perp: 1'-6, 3'-6, 1'-9 + 1'-9 (no pile at CL), 3'-6, 1'-6); 6 battered + 6 vertical per footing: the 3 piles of each of the two outer rows (rows parallel to the pier, 5'-3 each side of CL pier = 1'-6 from the footing faces) batter 1:4 AWAY from the CL pier, perpendicular to the pier (plan shows their shafts extended beyond the footing outward on both the Footing Layout and the Pile Layout); the two middle rows (1'-9 each side of CL pier) are vertical | - | p.11 / DS 9 | RASTER | the CIRCLED piles are NOT the batter piles: circles = 'SEE DETAIL 1' = 8f6 uplift anchors, on the 8 piles of the two outer columns (3'-6 from CL footing); 3 footings x 8 x 2 = 48 8f6 = bar list ok. Closes U6 |
| Pier pile notes (Rev. 1) | contract length 125 ft (was 165), friction, nominal 163 t, min. embed 65 ft, Pu 176 k, phi 0.55, tension 50 k; steel pile points; 8f6 uplift anchors through drilled web holes | - | p.12 / DS 10 | TEXT | |

### Abutments (integral, std 2087-BTCD modified)
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Type | integral abutment, pile cap/footing 3'-0 wide with diaphragm cast with deck, 6'-6 wing extension at east (gutter) end, no wing at west (stage joint) | - | p.15-17, p.20 | RASTER | |
| Length along CL brg | S: 36'-10 3/16 = 3 5/16 (abut. end / construction joint to CL NB roadway) + 36'-6 7/8 (CL NB to intersection CL abut. x gutter line); N: 37'-10 1/2 (abut. end / construction joint to intersection CL abut. x gutter line) = 7'-6 9/16 (end to extended tangent) + 30'-3 15/16 | - | p.17 / DS 15 | RASTER | DERIVED: 36'-0 / cos(10 deg 09'36") = 438.87 in = 36'-6 7/8 (S on tangent); S end 3 1/4 from CL NB measured perpendicular to the roadway (bottom chain) |
| Seat (low step) elevations | S: D 1117.50, E 1117.71, F 1117.92, G 1118.11, H 1118.31; N: D 1117.89, E 1118.29, F 1118.70, G 1119.10, H 1119.51 | ft | p.17 / DS 15 | RASTER | steps S 2 1/2, 2 1/2, 2 1/4, 2 3/8; N 4 13/16, 4 15/16, 4 13/16, 4 15/16 (DERIVED from elevations ok) |
| Bottom of footing | S: 1113.71 (west end) to 1114.60 (east end); N: 1113.91 (west) to 1115.64 (east); sloped | ft | p.15, p.16 | TEXT | ends confirmed RASTER |
| Wingwall elevations | A / B / C: S @ CL NB 1122.93 / - / 1122.91; N @ CL NB 1123.28 / - / 1123.26; S @ east gutter 1123.86 / 1123.76 / 1123.82; N @ east gutter 1125.12 / 1125.08 / 1125.11 | ft | p.20 / DS 18 | RASTER | |
| Piles | 8 HP12x84 per abutment, in 24" CMP casings (sand + bentonite top 10 ft) through EPS fill; S: 7 on CL brg: abut. end -2'-6- P1 (= 3 5/16 + 2'-2 11/16 from CL NB) -6 @ 5'-7 = 33'-6- P7 -10 3/16- CL abut. x gutter line (sum 36'-10 3/16 ok); N: 7 on CL brg: end -2'-6- P1 -5'-9 (split 5'-0 9/16 + 8 7/16 at the extended tangent)- P2 -5 @ 5'-9 = 28'-9- P7 -10 1/2- CL abut. x gutter (sum 37'-10 1/2 ok; 30'-3 15/16 = ext. tangent to gutter ok) | - | p.17 / DS 15 | RASTER | P1-P2 at N is a full 5'-9 (DERIVED 5'-0 9/16 + 8 7/16) |
| Wing pile (both abutments) | one pile in the wing extension: CL pile 5'-0 from CL abut. brg measured along the wing (parallel to the gutter line), wing end 1'-6 beyond (6'-6 CL brg to wing end); centred in the 3'-0 wing (1'-6 / 1'-6), i.e. 1 in outside the gutter line; S bottom chain: CL NB to CL wing pile 36'-1, to wing outer face 37'-7 (perpendicular to the roadway) | - | p.17 / DS 15 | RASTER | DERIVED: wing inner face 1'-5 inside the gutter + 1'-6 = 1 in outside; 36'-0 + 1 = 36'-1, 36'-0 + 1'-7 = 37'-7 ok. Closes U7 |
| Wing angle to abutment back face | S 100 deg 09'36" (= 90 + 10 deg 09'36" skew); N 75 deg 38'46" (wing to back face; = 90 - 14 deg 21'14") | - | p.17 / DS 15 | RASTER | N value implies a local skew of 14 deg 21'14" between CL N abut. and the gutter/wing direction, vs 14 deg 29'57" (p.21) - adds to U4 |
| Wing extension (both, plan section A-A) | 3'-0 wide = 1'-5 inside + 1'-7 outside the gutter line; wing end to abutment rear-face corner: S 3'-0 9/16 + 4'-11 11/16 = 8'-0 1/4, N 3'-1 1/8 + 4'-11 7/16 = 8'-0 9/16; 6'-6 from the wing end to the CL abut. brg (both); 6g4 bars 5 each face @ 1'-0 (4 spa = 4'-0) from 6 above the wing end, top gap S 5 11/16 / N 5 7/16 | - | p.15 / DS 13, p.16 / DS 14 | RASTER | sums DERIVED: 6 + 4'-0 + 5 11/16 = 4'-11 11/16 (S), 6 + 4'-0 + 5 7/16 = 4'-11 7/16 (N) ok |
| Abutment section (B-B, both) | pile cap/footing 3'-0 wide (level), 2 1/2 cl. sides, 3 cl. bottom; pile embedded 2'-0 into footing; 8g1 min. embedment 3'-3; diaphragm above the 3x3 x 2'-4 1/2 bearing bar, constant-depth top 1'-1 + 1'-3 + 1'-3 (bar spacings), 1'-3 horizontal; 1 in chamfer; keyway beveled 2x8 at the footing/diaphragm joint | - | p.15, p.16 | RASTER | same dims both sheets; footing depth not dimensioned (U8) |
| Rear elevation (both) | barrier 1'-7 over the wing; 6g4 min. embedment 1'-9; 2 3/8 cl. (5p2), 2 cl.; wing footing 3'-0 LEVEL; bottom of footing S 1113.71 (west) / 1114.60 (east), N 1113.91 (west) / 1115.64 (east); deck edge 1'-1 (perp. to slope) at both ends | - | p.15, p.16 | RASTER | |
| Section A-A bar chains (both) | 8g1 at 1'-9 + 1'-9 around beams, 5d2 & 5d3 1'-5 (typ.) + 5 eq. spa. + 2'-2 (typ.); stage-joint end: S 3 / 1'-0 / 2'-2 (= 3'-5 to beam D), N 1'-5 + 2 eq. spa. + 10 (= 3'-4 5/8 to beam D), 3 to 8g5; wing end: N 3 + 2 @ 9 (= 1'-7), S 8 + 3; 5d9/5d11/5d12 stainless projection 2'-4, min. lap 2'-2; 8f2 projection 4'-11, min. lap 4'-9 (p.17) | - | p.15, p.16, p.17 | RASTER | |
| Beam positions on the abutment sheets (A-A) | S: 3 5/16 + 3'-1 11/16 to beam D, 4 @ 8'-1 7/8 = 32'-7 1/2, beam H +6 7/16, overall 36'-6 15/16; CL beam H 9 1/2 from the gutter line (perp.); 2'-4 1/2 = 9 1/2 + 1'-7 to wing outer face. N: wing reference -2'-3- beam H -3 @ 8'-1 7/8 = 24'-5 5/8- beam E -3'-11 15/16- extended tangent -4'-1 15/16- beam D -3'-4 5/8- abut. end; overall 38'-3 1/8 | - | p.15, p.16 | RASTER | chain sums DERIVED ok (S 36'-6 15/16, N 38'-3 1/8). These dimension lines are drawn away from the CL brg and are not referenced to the same points as the p.17 pile plan chains (N 38'-3 1/8 vs 37'-10 1/2); S see U5 |
| Bar marks differing N vs S | footing longit. 8f1 (S) / 8f5 (N); diaph. longit. B.F. 5d6 (S) / 5d20 (N); diaph. end 5d5 (S) / 5d19 (N); wing ext. longit. 8f3 & 8f4 (S) / 8f3 & 8f6 (N); spacing tables: S 38-8g1 B.F., 28-8g1 F.F., 36-8g3 B.F., 36-5k1 & 5k2 B.F.; N 40-8g1 B.F., 29-8g1 F.F., 38-8g3 B.F., 38-5k1 & 5k2 B.F., 1-8g5 F.F. | - | p.15, p.16 | RASTER | N section C-C (also referenced from p.15): 1'-6 high curb, 3 5/8 cl., 5d8 bars, 2 to barrier face; 5c3 / 5c14 barrier bars |
| Pile spiral | 7 turns of #2 bar, 21 in diameter, 3 in pitch, 3 - L 7/8 x 7/8 x 1/8 spacers, at the top of each abutment pile | - | p.15, p.16 | RASTER | 7 x pi x 21 = 38'-6 = bar list #2 length ok |
| Abutment pile notes (Rev. 1) | S: 155 ft (was 195), nominal 179 t, phi 0.55, Pu 197 k; N: 150 ft, mixed soil, nominal 152 t, phi 0.65; start elevation = bottom of EPS | - | p.17 / DS 15, p.6 | TEXT/RASTER | |
| Concrete | S footing 18.1 cy, N footing 19.5 cy | cy | p.17 | RASTER | |

## 5. Reinforcing
| item | value | PDF p. / sheet | tag | note |
|---|---|---|---|---|
| Bar lists | `bar-lists.csv`: Pier 1, Pier 2 (epoxy / stainless / non-coated), deck + abutments + diaphragms (epoxy / stainless / non-coated), BTD beams (per beam), barrier (one rail) | p.12, 26, 27, 39 | RASTER | every group weight total reconciles with the plan total (pier 1 27,324 / 1,639 / 5,854; pier 2 27,521 / 1,639 / 5,854; deck 77,253 / 5,818 / 165; rail 6,996 / 1,522) |
| Quantity reconciliation | plain 11,873 = 2 x 5,854 + 165; epoxy 139,094 = 27,324 + 27,521 + 77,253 + 6,996; stainless 10,618 = 2 x 1,639 + 5,818 + 1,522 | p.3 | DERIVED | all exact |
| Bend diagrams | `bend-diagrams.csv`: every bent mark of `bar-lists.csv` (piers p.12, deck/abutments/diaphragms p.26, BTD beams p.27, barrier p.39) + #2 pile spiral: shape, every printed segment in path order, other printed dims, pin D, listed vs summed length | p.12, 26, 27, 39 | RASTER | convention (sheet notes on all four sheets): "ALL DIMENSIONS ARE OUT TO OUT", "D = PIN DIAMETER"; listed length = sum of the out-to-out segment dims (hook/return legs included). Std 4730 (p.27) and rail (p.39) round the sum UP to the next inch (6b3 5'-8 1/2 -> 5'-9, 4d1 6'-4 1/2 -> 6'-5, 4e1, 5c1, 5c14). 50 of 57 rows reconcile; NOT reconciled: 5c5 (pier), 5b1 and 4h1 (beam std), 5c2 (rail, bottom leg not printed) -> U9 |
| Pier diaphragm 5e1/5e2/5e6/5e7 | open U stirrups with two 6 in inward top returns (not closed hoops as `bar-lists.csv` shape says): 6 + leg + bottom + leg + 6 = listed length | p.26 | RASTER | |
| Variable-length deck bars (VARIES) | no length table in the set; p.23 prints only the shortest / longest bar of each fan at the skewed deck ends: 7-6a2 @ 10 1/2 (= 5'-3) 7'-8 to 36'-11 (S end, top); 6-6a5 @ 10 1/2 (= 4'-4 1/2) 10'-2 to 34'-6 (S end, bottom); 10-6a4 (9 spa @ 10 1/2 = 7'-10 1/2) 4'-5 to 34'-10 (N end, top); 9-6a6 (8 spa @ 10 1/2 = 7'-0) 6'-1 to 33'-2 (N end, bottom) | p.23 / DS 21 | RASTER | end lengths RASTER (6a2 7'-8 / 36'-11, 6a5 10'-2 / 34'-6, 6a4 4'-5 / 34'-10, 6a6 6'-1 / 33'-2); fans start 1'-3 1/2 (6a2) / 1'-8 3/4 (6a5) from the deck end corner measured along the edge of deck |
| VARIES - intermediate lengths | DERIVED linear between the printed end lengths (straight skewed end line, constant 10 1/2 spacing): 6a2 step 4'-10 1/2; 6a5 step 4'-10 7/16 (58.4 in); 6a4 step 40.56 in; 6a6 step 3'-4 5/8. Weight check with 1.502 lb/ft: 6a2 7 x avg 22.29 ft = 234 lb (list 234), 6a4 10 x 19.63 = 295 (295), 6a5 6 x 22.33 = 201 (201), 6a6 9 x 19.63 = 265 (265) | p.23, p.26 | DERIVED | totals reconcile exactly; individual intermediate lengths are not printed (no table), so per-bar lengths stay DERIVED, not RASTER |
| Deck bar placement | 6a1 + 6a17 transverse @ 10 1/2 top & bottom (parallel to supports at ends), 5b1 longitudinal (17 top/bottom between beams), 7b2 over piers 28'-0 centered, 6j1 @ 10 1/2 at rail; top transverse 2 1/2 clr, bottom 1 clr | p.18, 22, 23 | RASTER/TEXT | |
| Stainless splice bars | at stage-2 joint: 6a17 deck, 5d9/5d11/5d12/5d15/5d16/5d18, 8f2, 8g5, pier 10a3/10b2 (threaded for couplers) and 5a6 (lap) | p.10, 26 | TEXT | |
| Pier 2 bar list text difference | 5a6 described "CAP, TOP, LONGITUDINAL" on pier 2 vs "CAP, SIDES, LONGITUDINAL" on pier 1 (same no./length) | p.12 | RASTER | plan inconsistency, recorded as printed |

### Barrier rail outline (U10; Iowa aesthetic barrier, east side only)
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Standard section (Part Sections C-C / F-F, identical outline) | base 1'-7 (back reference line = deck edge to gutter line); height 3'-8 at the gutter (front toe) and 3'-7 5/8 at the back | in | p.38 / DS 36 | RASTER | 1'-7 and the toe position marked as "see Barrier Rail Orientation Detail" (p.19 / DS 17) |
| Top | horizontal chain from the back reference line: 2 (to the recessed back plane) + 8 1/2 (flat top) + 3 1/2 (horizontal run of the upper front face) + 5 (run of the lower front face) = 1'-7; top corners chamfered, 3/8 (typ.) | in | p.38 | RASTER | sum DERIVED ok |
| Front face (traffic side) | from the top: upper face sloping 3 1/2 out over 2'-10 down to the 1st point of intersection (R = 10 in); lower face sloping 5 out over 7 down to the 2nd point of intersection (R = 1 1/2 in); vertical toe 3 (* max., may vary) to the deck at the gutter line; 7 + 3 = 10 (* max.); 2'-10 + 7 + 3 = 3'-8 | in | p.38 | RASTER | sum DERIVED ok |
| Back face (rustication) | recessed plane 2 in from the back reference line (= 1'-5 from the gutter); three horizontal rustication strips projecting 1 1/2 (to 1/2 in from the reference line), 3/8 chamfers (typ.); vertical chain from the top: 6 1/8 strip + 5 1/2 recess + 6 1/2 strip + 5 1/2 recess + 6 1/2 strip + 1'-1 1/2 recess to the deck = 3'-7 5/8 | in | p.38 | RASTER | sum DERIVED ok; concrete texture on both faces per DS 42 |
| Conduit / bars | 2 x 2 in rigid steel conduits (DS 33); 6d1 at 3 + 6 equal spa. + 5 (= 3'-7 5/8); 5c1 / 5c2 @ 1'-0, 5c8 @ 3'-0; deck below 1'-6 min. | - | p.38 | RASTER | bend dims: `bend-diagrams.csv` |
| Bump-out section (Part Section D-D) | back face 2'-1 from the gutter line (recessed plane, thin veneer brick) with rustication strips projecting a further 4; height 3'-7 1/4 at the back, top sloping 3/4; back chain 5 3/4 + 5 1/2 + 6 1/2 + 5 1/2 + 6 1/2 + 1'-1 1/2 = 3'-7 1/4; same front face as the standard section; deck widened 1'-3 beyond the edge of deck under the bump-out; 7-4d bars @ 6 (6 spa = 3'-0), bottom 5 | in | p.38 | RASTER | sum DERIVED ok |
| Bump-out plan (Part Sections G-G upper / H-H lower) | G-G: standard back 1'-6 1/2 from gutter, bump-out 10 1/2 deeper (= 2'-5 = 2'-1 + 4 ok), length 2'-11 + 2'-11 = 5'-10 centred on CL bump-out, 2 cl.; H-H: standard back 1'-5 from gutter, bump-out 8 deeper (= 2'-1 ok), brick face length 2'-3 13/16 + 2'-3 13/16 = 4'-7 5/8, 3 13/16 brick returns, 3 cl.; 4c15 @ 1'-0 (4 spa = 4'-0), 4d2 / 4d3 loops | in | p.38 | RASTER | bump-out locations at the CL piers (Detail C p.22 / DS 20: deck bump 5'-2 long, 2'-7 + 2'-7, 1'-3) |
| Lengths along the gutter line (Elevation, looking east) | bid / standard section 236'-9 3/8 = 4'-11 3/8 (S abut. diaph. ext.) + 226'-10 1/4 (end to end of slab) + 4'-11 3/4 (N abut. diaph. ext.); 5c8: 1'-4 3/4 + 78 @ 3'-0 = 234'-0 + 1'-4 5/8; 5c1/5c2: 4 3/4 + 4 @ 1'-0 (5-5c1, 5-5c3, 5-5c14) + 1'-0 + 226 @ 1'-0 = 226'-0 (227 - 5c1 & 5c2) + 1'-0 + 4 @ 1'-0 + 4 5/8 | - | p.38 | RASTER | all three chains sum to 236'-9 3/8 (DERIVED ok); construction joints: min. lap 2'-7, 6 +/- , joint details (1/4 min. sealer, bond breaker) |

## 6. Misc
| item | value | PDF p. | tag |
|---|---|---|---|
| Deck joints | none (integral); paving notches at both ends (5k1/5k2) | p.18, 20 | TEXT |
| Deck placement | 5 sections, numbered in placement order: 1 south end, 2 middle of span 2, 3 north end, then 4 and 5 over piers 1 and 2; west edge chain 43'-4 3/16, 14'-2, 14'-2, 78'-7 7/16, 14'-2, 14'-2, 48'-4 1/8 (sum 226'-11 3/4 DERIVED ok); east edge 43'-4 1/4, 13'-10 1/2, 14'-5 7/16, 78'-7 1/2, 13'-9 11/16, 14'-6 5/16, 48'-3 15/16; concrete 66.9 / 77.3 / 71.3 / 45.1 / 45.0 = 305.6 cy | p.22, 26 | RASTER |
| Drains | none on deck; subdrains behind abutments (DS 40) | p.42 | TEXT |
| Lighting | 2" rigid steel conduit in barrier/deck, junction boxes in pier diaphragm, light mounting stems (by others) at STA 464+03.56 57.81' RT and STA 464+76.36 57.35' RT | p.35-37 | TEXT |
| Aesthetics | integral thin veneer brick on pier columns/caps (2,124 SF), concrete texture & rustication on abutments, wings, barrier | p.13-14, 44 | TEXT |
| Slope protection | concrete slope protection 495 SY, berm slope location table (A1-A3, B1-B3 per abutment) | p.6, 40-41 | RASTER |
| EPS / CMP | EPS fill embankments by others; 24" CMP pile casings 281 LF | p.5, 17 | TEXT |
| Temporary shoring | LS, by contractor design | p.5 | TEXT |
| Soils | SPS.1 (raster scan), SPS.2-3 boring logs; SPS.1 title block shows superseded design (12 deg skew, 36'-0/107'-0/61'-0 spans) | p.45-47 | RASTER |

## 7. Checks data (CSV)
- `top-of-deck-elevations.csv` - 8 lines x 27 points (p.33 / DS 31).
- `haunch-elevations.csv`, `haunch-misc-data.csv` (p.34 / DS 32).
- `deck-thickness-at-beams.csv` (p.24 / DS 22).
- `beam-information.csv` - beam sizes, cambers, bearing thickness, seat elevations (p.25 / DS 23).
- `quantities.csv` - estimated bridge quantities (p.3 / DS 1); piles 11,440 LF = 8 x 155 + 8 x 150 + 72 x 125 (DERIVED ok).
- `diaphragm-bolt-holes.csv` (p.31 / DS 29).
- `bar-lists.csv`.
- `bend-diagrams.csv` - bend diagram dimensions of every bent mark (p.12, 26, 27, 39) with listed-vs-summed length check.

## UNRESOLVED
| id | item | PDF p. | note |
|---|---|---|---|
| U1 | Horizontal coordinates / datum of the alignment (N/E of PC/PI, projection) | p.6 | not in this set (road plans IM-NHS-029-7(49)); model can be built in local coordinates from stations |
| U2 | Exact reference line of the cross-slope pivot | p.6, p.18 | DERIVED: PGL NB at 5.00 ft RT of CL I-29, plane slope to the east edge reproduces p.6 and p.33 to 0.01 ft; not stated as text |
| U3 (CLOSED by user: standard length governs; chords = standard within 1/16", see decision 8) | Standard beam lengths (55'-0 / 105'-0 / 60'-0 CL-CL brg, 56'-4 / 106'-4 / 61'-4 overall) vs layout CL-CL beam bearings along CL NB (54'-11 15/16 / 104'-11 7/16 / 59'-11 1/2) and per-beam actual lengths along chords | p.21, 27 | plan gives no per-beam length table; true chord lengths per beam line must be computed from the bearing points |
| U4 | (also: N wing angle 75 deg 38'46" on p.17 implies 14 deg 21'14") Local-tangent skews at pier 2 (13 deg 00'40") and N abut (14 deg 29'57") do not reproduce from 10 deg 09'36" + deflection at the staked CL NB point (12 deg 59'50", 14 deg 27'28") | p.21, p.7 | reference point of the "local tangent" not stated; the parallel 10 deg 09'36" to the extended tangent (p.7) reconciles with staking |
| U5 (CLOSED) | Abutment CL NB to gutter along CL S abut. brg: 36'-6 7/8 governs | p.7, p.15, p.17 | proven by geometry: 36'-0 roadway / cos(10 deg 09'36") = 438.871 in -> 36'-6 7/8 (15/16 would be 0.066 in off); p.17 chain 3 5/16 + 36'-6 7/8 = 2'-6 + 33'-6 + 10 3/16 = 36'-10 3/16 closes. p.15's 36'-6 15/16 is only the sum of its own rounded chain 3 5/16 + 3'-1 11/16 + 32'-7 1/2 + 6 7/16 on a dimension line drawn off the CL brg (not a design value) |
| U6 (CLOSED) | Pier pile batter: outer two rows (3 + 3 piles) batter 1:4 away from CL pier, perpendicular to the pier; middle rows vertical; circled piles = Detail 1 uplift anchors, not batter | p.11 | see Piers table |
| U7 (CLOSED) | Wing pile: 5'-0 from CL abut. brg along the wing, centred in the 3'-0 wing (1 in outside the gutter line), wing end 1'-6 beyond | p.17 | see Abutments table |
| U8 | Integral abutment footing depth: only bottom-of-footing end elevations given (sloped); top = seat/step elevations | p.15-17 | DERIVED ~3.8 ft; no explicit depth dimension found |
| U9 (mostly CLOSED) | Bend diagrams transcribed (`bend-diagrams.csv`), 50/57 reconcile. Open: U9a pier 5c5 printed segments sum 9'-7, list 9'-11; U9b std 4730 5b1 (sum 10'-4 / 10'-10 vs 10'-8) and 4h1 (7'-10 / 8'-1 vs 8'-0) - model from the standard; U9c rail 5c2 bottom leg not dimensioned. VARIES 6a2/6a4/6a5/6a6: no table - end lengths printed, intermediates DERIVED linear (weights reconcile) | p.12, 23, 27, 39 | see Reinforcing rows |
| U10 (CLOSED) | Barrier outline incl. rustication and bump-outs transcribed (Barrier rail outline table) | p.38 | only the radii tangency points are graphic: R = 10 / R = 1 1/2 at the 'points of intersection' as printed |
| U11 | Overhang at the east edge per support (beam H to east edge) | p.18 | "VARIES" - follows from curved edge R and chord beams |
| U12 (CLOSED) | North abutment p.16 extracted (rear elevation, A-A, B-B, C-C, wing, bar marks, spacing table) - mirror of S with the differences listed in the Abutments table | p.16 | remaining open items are shared with S: U8 (footing depth), N dimension-line references (38'-3 1/8 on p.16 vs 37'-10 1/2 on p.17 are different reference points) |

## Model mapping
| section | model inputs | notes / gaps |
|---|---|---|
| 1 Geometry | `Alignment` from `PiDefinition` (one PI, R 2320, no spirals) in local coordinates; `VerticalProfile` (1 PVI, VC 700); `CrossSlopeTable` with pivot offset +5.00 (PGL NB) and rows 2 % @ 462+89.05 -> 5 % @ 464+83.05 | the profile is on PGL NB (5 ft RT of CL I-29) while stations are CL I-29: needs profile offset / pivot support (check `CrossSlopeTable` pivot) - no coordinates (U1) |
| 2 Layout | `BridgeDefinition.Supports` (4; `SkewDegrees` per support relative to the radial: from 10 deg 09'36" to the extended tangent + deflection), deck edges (west R 2347 = CL NB, east 37'-7), barrier east only; `GirderLayoutDefinition` 5 girders; `PlanGeometry` per girder could carry the per-span chords | GAP: girders here are straight chords per span with different spacing per span and different offsets on the back/ahead bearing lines at a pier (3'-3 1/2 vs 2'-10 15/16 for D at pier 1); `OffsetsAtSupports` has one offset per girder per support and `PlanGeometry` is a smooth tangent/arc chain - neither expresses a kinked, re-spaced chord layout with two bearing lines per pier; supports defined parallel to a chord (extended tangent) rather than by local skew |
| 3 Superstructure | `GirderLayoutDefinition.Kind` = prestressed; `PrestressedBeamDefinition` (StrandDiameter 0.6, StrandStrength 270, ConcreteStrength / ReleaseStrength, DepressedStrands, HarpPointFraction, BearingToEnd 8 in, EndGapAtBent, Pad 9 x 1 x 2'-4 1/2); `CustomSection` (`PrecastIBeamDims`) for BTD; PGSuper import (`PgSuperReader`) if a .pgs is made; deck 8 in + haunch table; camber via `GirderCamberDefinition` | GAPS: (a) no Iowa BTx (BTA-BTE) bulb-tee shapes in `TxGirderCatalog` (Tx28-Tx70 only); WBFL PrecastIBeam dims have no R = 8 / R = 2 radius fillets, so BTD can only be approximated; (b) one `PrestressedBeamDefinition` for all spans - this bridge needs per-span beam type/length/strands (BTD55 / BTD105 / BTD60, 14+0, 28+6, 12+2); (c) explicit strand grid (`StrandLayout`) exists but harped strands at specific grid columns with end positions 4 in below top + hold-down distance from midspan (6'-0, 10'-6) need checking vs `HarpPointFraction`; (d) prestressed path skips cross frames - no steel intermediate diaphragms (C15 / W21 / W14 bolted to beams) for prestressed girders; (e) continuity: pier diaphragm cast with deck over beams on neoprene pads, integral abutment ends (beam embedded in abutment diaphragm, 3x3 bar bearing) - IGD/IGEB end types only; (f) deck thickness / haunch / seat table checks (`ElevationCheck`) usable as checks |
| 4 Substructure | piers: `SubstructureDefinition` multi-column bent (3 columns, cap 4'-3 5/8 x 5'-0, sloped cap following cross slope, low steps per beam), `FootingDefinition` per column (13'-6 x 10'-0 x 5'-0, bottom 1090.00), piles HP12x84 12 per footing with `BatterRatio` 4 + `FootingPoint.BatterDirS/N`; abutments: `SubstructureDefinition.Abutment` (`AbutmentDefinition`) | GAPS: integral abutment (pile cap + diaphragm monolithic with deck, beams embedded, wing extension 6'-6 on one side only, sloped footing bottom, step seats) - check `AbutmentDefinition` support for integral type; CMP pile casings / EPS fill not modeled; column section with 4 in projections (brick veneer reveals) not a standard column shape; thin veneer brick; pier cap ends at a stage joint (no cantilever on west end, stage-2 cap later) |
| 5 Reinforcing | `PlanBarGroup` (FootingDefinition.Bars, CapBars), plan bend diagrams (`SubstructurePlanShapes`), deck rebar | bend dims in `bend-diagrams.csv` (U9 mostly closed); stainless splice bars with couplers at the stage joint; BTD beam bars (5b1 hairpins, 4d1 stirrups, 6b3/6b4 end bars) have no generator in the prestressed path |
| 6 Misc | barrier (custom aesthetic profile 3'-8 x 1'-7 with bump-outs), lighting conduit, slope protection | GAP: no Iowa aesthetic barrier profile / bump-outs; one-sided barrier with an open stage-joint edge |
| 7 Checks | `ElevationCheck` (top of deck 8 lines x 27 points, haunch elevations, seats), span check (along CL NB and along extended tangent), quantities | all CSVs ready for comparisons |
