# Bonner Unit 5 - substructure part 2: precast columns, CIP column bases, footings, reinforcing (agent review plan reading)

Plan set `C:\SBMData\B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf`, PDF page numbers. Scripts and renders: `work-cols\`.
Tags: **VECTOR** = text layer and/or vector linework (dimension reproduced to < 0.1 in); **SCALED** = drawn only, measured from vectors;
**NOT GIVEN** = not on the sheets; **DERIVED** = arithmetic from printed values (shown). CSVs beside this file:
`columns-footings.csv` (per bent geometry), `substructure-bars.csv` (every bill-of-material row, 268 rows).

Frame: S along the bent control line (+ right looking up-station), N along -L- (+ up-station), elevations absolute (NGVD as printed).
All footing plans draw the bent control line HORIZONTAL and -L- vertical; every footing / column / cap is "SYMMETRICAL ABOUT" both the bent
control line and CL footing, column and cap, which is offset LEFT of -L- by the Detail C value (p.335 / 338 / 341 / 344 / 349 / 352;
4 7/8" bent 18, 2 11/16" bent 29, 10 3/8" 19-22, 5 3/4" 23-28 - already in `piles-bents.csv` and applied by `misc-bonner-unit5.csx`).

Text-layer caveat: the hidden text layer encodes fractions with font glyphs that do not map one-to-one (the same glyph prints 1/2 in one place and
11/16 in another). Every fraction quoted here was checked on a rendered crop or by the bill weight; `bom3.py` + `makecsv.py` carry the fixes.

## 1. Columns

### 1.1 Precast hollow segment (all bents 18-29, p.376-381) - VECTOR

| item | value | how |
|---|---|---|
| outside | **16'-0" along S x 11'-0" along N** (all four sheets, incl. bents 23 & 24 p.380) | text dims + vectors (575.9 x 395.9 pt at 36.0 pt/ft) |
| orientation | 16'-0" along the bent control line (PLAN, SECTION A-A label "BENT CONTROL LINE" on the long axis; p.334 elevation looking ahead shows 8'-0" + 8'-0", side view 5'-6" + 5'-6") | VECTOR |
| outer corners | small chamfer about 3/4" x 3/4" (outline lines stop 2.3 pt = 0.77" short of each corner on p.376 / 378 / 380 / 381) | SCALED (no dimension) |
| hollow core | **13'-0" x 8'-0"**, walls **1'-6" all four sides**, core corners chamfered **1'-0" x 1'-0"** ("1'-0" (TYP.)" both ways) | text + vectors (467.9 x 288.0 pt; 36.0 pt chamfer legs) |
| solid zones | none: every segment is the same hollow section full height (elevations show the inside face full height, no diaphragm). The column is solid only in the CIP base (below EL 17.00). | VECTOR (elevations / side views p.376-381) |
| joints | match cast ("SEGMENTS ARE TO BE VERTICALLY MATCH CAST", p.376 note 5), typical + alternate shear keys (p.384 S3-136), segment 1 bottom roughened 1/4" in lieu of keys; 3" non-shrink grout bed under segment 1 on UHMW shims (p.383 notes 3-5) | text |
| PT | stainless / A722 PT bars in 4" ducts in groups A-D (A-E at 23 & 24), terminated per segment (tables p.376/378/380/381; notes V-Z); couplers above the first coupler level; dead ends cast into the CIP base 5'-0" below the footing top (p.334 "5'-0" (TYP.)") | positions only - PT agent |

Segment stack (bottom of segment 1 = EL 17.25 = 17.00 + 3" grout bed, printed "3" GROUT BED"; heights VECTOR text on p.334-351 / 376-381):

| bent | segments (bottom to top) | column top EL | cap | top of bent (printed) | check |
|---|---|---|---|---|---|
| 18 | 12'-0", 12'-0", 12'-0", 6'-0", 4'-0" (18-1..18-5) | 63.25 | 5'-0" | 68.25 | 68.25 |
| 29 | 12'-0", 12'-0", 12'-0", 7'-9", 4'-0" | 65.00 | 5'-0" | 70.00 | 70.00 |
| 19 | 12'-0" x 3, **5'-6"** (p.379 "BENT 19 SEGMENT 4" 5'-6") | 58.75 | 5'-6" | 64.25 | 64.25 |
| 20-22 | 12'-0" x 3, 10'-0" | 63.25 | 5'-6" | 68.75 | 68.75 |
| 23, 24 | 12'-0" x 3, 10'-1" | 63.33 | 5'-6" | 68.83 | 68.833 |
| 25, 26, 27 | 12'-0" x 3, 10'-1" | 63.33 | 5'-6" | 68.83 | 68.833 |
| 28 | 12'-0" x 3, **6'-6"** (p.379 "BENT 28 SEGMENT 4") | 59.75 | 5'-6" | 65.25 (p.351 ELEVATION DATA table) | 65.25 |

piers.csv marked 19-4 = 5'-6" as DERIVED; p.379 prints it - now VECTOR. The 3" grout bed is printed (p.334 and every sheet 1) - no longer an assumption.

### 1.2 CIP column base (all bents) - VECTOR

| item | value | how |
|---|---|---|
| outline | **16'-0" (S) x 11'-0" (N), solid, square corners** = same outside as the segments | Section A-A p.336 / 339 / 343 / 351 text + vectors (431.9 x 296.9 pt, no corner cut) |
| top | EL 17.00 (all bents) | text |
| bottom | = footing top at CL (6.00 / 7.00 / 7.50) - the base sits on the level zone of the footing | text |
| height | 11'-0" (18, 19, 26-29), 10'-0" (20-22, 25), 9'-6" (23, 24) | text "x'-x" SOLID CIP COLUMN BASE" |
| concrete | lower part 4.5 ksi, top **2'-6"** 6 ksi (8 ksi at 23 & 24) with optional construction joint; whole base may be the higher strength (note *). Drawn "2'-6" TO 3'-0"" ; the bill volumes give exactly 2'-6": 16 x 11 x 2.5 / 27 = 16.3 CY (every sheet) and 55.5 / 48.9 / 45.7 CY = 8.5 / 7.5 / 7.0 ft lower lifts | text + DERIVED |
| pier protection | "PIER PROTECTION MATERIAL, SEE SPECIAL PROVISIONS (TYP. ALL 4 SIDES)" on the footing sides at bents 19-28 (p.337 / 340 / 343 / 348 / 351); not on p.334 (18 & 29); extent / thickness NOT GIVEN | text |
| other | stay-in-place precast bottom soffit form under every footing (by others, p.334), grounding plate + lighting conduit (note 8), vertical clearance gauges NW corner bent 26 and SE corner bent 27 (p.351 note **), fall protection inserts at the column top (p.384) | text |

## 2. Footings - VECTOR

| bents | plan (S x N) | outline | bottom EL | top at CL (level zone) | top at edges | piles |
|---|---|---|---|---|---|---|
| 18, 29 | 36'-0" x 36'-0" | rectangle | -0.50 | 6.00 | 5.75 | 18 |
| 19 | 43'-6" x 36'-0" | rectangle | -0.50 | 6.00 | 5.73 | 20 |
| 20, 21, 22 | 51'-0" x 36'-0" | rectangle | -0.50 | 7.00 | 6.65 | 24 |
| 23, 24 | 51'-0" x 58'-6" | octagon, vertices (S, N) = (+-10.5, +-29.25), (+-25.5, +-8.0); diagonal edges 26'-0" | -0.50 | 7.50 | 7.03 | 30 |
| 25 | 51'-0" x 36'-0" | rectangle | -0.50 | 7.00 | 6.65 | 24 |
| 26, 27, 28 | 43'-6" x 36'-0" | rectangle | -0.50 | 6.00 | 5.73 | 24 |

Sizes: footing plan text dims, reproduced by vectors at 13.50 pt/ft (p.335 485.9 x 485.9 pt; p.338 / 352 587.2 x 485.9; p.341 / 349 688.4 x 485.9;
p.344 688.4 x 789.7, top / bottom edges 283.4 pt = 21'-0", side edges 215.9 pt = 16'-0"). The larger size is along S (bent control line) everywhere,
and 58'-6" is along N at 23 & 24 (model text above is right).

**Top surface ("BREAK LINE ON TOP OF CONCRETE", "SLOPE" arrows, Sections D-D / E-E):** LEVEL over exactly the CIP column base footprint
(S +-8'-0" x N +-5'-6"; vectors 216.0 x 148.5 pt on every footing plan p.335-352) at the CL elevation, then plane facets falling straight to the
footing edges at the edge elevation. Break (hip) lines run from each corner of the level rectangle to the footing corners. Rectangular footings: 4
trapezoidal facets (one per edge). Octagon (23 / 24): the level rectangle corners connect to BOTH ends of each diagonal edge, giving 4 trapezoids
(top / bottom edges, side edges) + 4 triangles (diagonal edges). One drawn hip line on p.344 ends 0.95 ft short of the inner corner
(710.0, 624.8 vs 697.1, 626.6 pt) - drafting, ignore. Slopes (DERIVED): 18/29 0.25 ft over 10.0 (S) / 12.5 ft (N); 23/24 0.47 ft over 17.5 / 23.75 ft.

Volume check (`work-cols\ftgvol.py`: this top surface, bottom -0.50, minus 36" x 36" x 4'-0" per embedded pile):

| bents | model CY | bill CY | |
|---|---|---|---|
| 18/29 | 282.0 | 282.4 | OK |
| 19 (20 piles) | 342.2 | 338.1 | -1.2 %; 26-28 (24 piles) 336.9 vs the same 338.1 - the 19 bill looks copied from 26-28 (minor) |
| 20-22, 25 | 465.4 | 467.2 | OK |
| 23/24 | 633.3 (all 30 piles 4'-0") | 631.6 | OK; with the 4 piles at 1'-3" it is 637.0 |

Pile embedment 4'-0" (TYP.) above the footing bottom (Sections D-D, p.334 elevation); bents 23 / 24 the 4 piles under the column base 1'-3"
("PILE PENETRATION 1'-3" INTO CIP FOOTING, THESE FOUR PILES ONLY", p.344; top 1'-3" roughened 1/4", note 1) - as in piles-bents.csv. No seal /
tremie concrete drawn; the footing is cast on the stay-in-place precast soffit form (by others) - NOT GIVEN as a dimensioned element.

## 3. Reinforcing

### 3.1 Bills of material (CIP: footing + column base; all bars stainless "S") - VECTOR

Every row in `substructure-bars.csv` (bents, page, mark, count, size, type, length, DIM A, weight, recomputed weight, element, out-to-out shape from the
BAR TYPES diagram). Recomputed weights match within 1 lb on every row except the two conflicts below; sheet totals match the printed totals:
18/29 47,416; 19 98,356 (sum 98,359); 20-22 110,826; 23/24 196,360; 25 106,045; 26-28 93,322 lb (per bent).

Marks by element (sheet-specific numbering!):

| bents | footing | CIP base top mat (#8 under segment 1) | base verticals | base ties / stirrups / edge bars | footing stirrup grid |
|---|---|---|---|---|---|
| 18/29 (p.336) | T1-T8, T11-T13 | T9, T10 | M1 60 #11 | S1, S2 #5 ties; S3 #4 stirrup; S8, S9 #3; S10, S11 #6 | S4 + S6 lapped with S7 (484 #6: 199 S4 + 285 S6/S7) |
| 19 (p.339) | T1-T9, T14-T18 | T10-T13 | M1 44 | S1, S2, S3, S8-S11 | S4, S6, S7 (572) |
| 20-22 (p.342) | T1-T9, T14-T18 | T10-T13 | M1 44 | S1, S2, S3, S8-S11 | S4, S6, S7 (786) |
| 23/24 (p.347) | T1-T87, T94-T112 | T90-T93 | M1 66 | S1-S4, S8-S11 | S5, S6, S7 (1004: 359 S5 + 645 S6/S7) |
| 25 (p.350) | T1-T9, T14, T16-T18 (no T15) | T10-T13 | M1 60 | S1-S4, S8-S11 | S5, S6, S7 (786) |
| 26-28 (p.353) | T1-T9, T14-T17 | T10-T13 | M1 60 | S1-S4, S8-S11 | S5, S6, S7 (572) |

Bar types (NCDOT, out to out, "ALL DIMENSIONS ARE OUT TO OUT OF BAR EXCEPT FOR DIM. SHOWN ON STANDARD 135 deg HOOK"): 1 = U (run + 2 equal legs;
23/24 run = DIM A column), 2 = L, 3 = straight + short leg + 135 deg hook (ties / stirrups), 4 = straight + 135 deg hook (S7 4'-6" + 8", S8 1'-6" + 1'-6"),
5 (23/24 only, T65-T71 / T74-T83) = #4 bent edge bars along the octagon top (1'-0" drops, two diagonals, run, small offsets - table p.347), STR.

### 3.2 Placement (from the plan callouts; spacings are MAXIMUM, note 7) - text VECTOR, positions between callouts SCALED

Footing (Sections D-D / E-E, footing reinforcement plans):
- Cover 4" clear top, bottom and sides (4" CLR. typ.); bottom bars 3" MIN. clear of the pile sides; bottom mat sits over the pile heads at 4'-0".
- **Bottom mat** = bundles / pairs of #11 in strips between the pile rows (18/29: 7-#11T3 PAIRS AT 8" N-running strips and 9-#11T4 AT 6" S-running
  strips between piles, 1-#11T3/1-#11T4 edge bars or 5-#6T8/5-#6T7 side bars; 19 / 26-28: "#11 3-BAR BUNDLES", 9 x 3-bar bundles at 6" (2-#11T3 +
  1-#11T14 per bundle), 2 layers 7-#11T4 pairs lapped with 7-#11T5 pairs at 8" (alt. lap, 9'-10" min. lap); 20-22 / 25: 9-#11T3 pairs at 6", 23-3 bar bundles
  (25), 2 layers 7-#11T4 pairs lapped with 7-#11T5; 23/24 (p.346): 3 layers of #11 pairs at 8" (T24 / T25 / T26 alternating laps), 2 layers 4-#11T14 /
  9-#11T14 lapped with #11T15 at 6", 7-#11T12 pairs lapped with 7-#11T13, T18-T21 pairs, T1-T11 / T16-T17 edge U bars, T84 or 6-#6T85..T87 side bars).
- **#8 bars at the piles** (bottom, around the pile heads): 18/29 5-#8T11 / 5-#8T12 / 5-#8T13 at 8" each corner + 3-#8T12 at 6"; 19 6-#8T16 at 6", 5-#8T17 /
  5-#8T18 at 8"; 20-22 / 25 6-#8T16 at 6", 5-#8T16 / 5-#8T18 at 8", 2-#8T17; 26-28 6-#8T15 at 6", 5-#8T17 at 8", 2-#8T16; 23/24 5-#8T94 at 8", 6-#8T95 at 6",
  #8T96-T101 at 6", #8T102-T107 at 6", #8T108-T112 at 8" (3" min. from piles).
- **Top mat** (U bars, legs down the faces): 18/29 45-#6T1 at 12" (N-running, spaced along S, "align top and bottom bars at stirrup locations") and
  27-#6T2 at 8" each half (55 total, S-running), 17-#4T5 at 1'-5" and 7-#4T6 at 1'-6" (short bars where the top slopes, between break lines); 19 44-#8T1 at
  12", 22-#8T2 lapped with 22-#8T15 (alt. lap), 19-#4T6 at 1'-6", 7-#4T7 at 1'-6"; 20-22 52-#8T1 at 12", 22-#8T2 / 22-#8T15, 24-#4T6, 8-#4T7; 25 69-#6T1
  at 9", 22-#6T2 at 12" (lapped with T17 2-#8, see conflict), 24-#4T6, 8-#4T7; 26-28 59-#6T1 at 9", 22-#6T2 at 12", 19-#4T6, 7-#4T7; 23/24 (p.345) #11 top
  bars T27-T62 (T42-T47 lapped pairs at 9", T58-T61 lapped at 8", 12-#11T60 / T61 / T62 alternating), #4T63-T83 bent edge bars on the diagonal edges.
- **Side face bars**: 5-#6T7 / 5-#6T8 at eq. spa. between 1'-0" from top and bottom (18/29, Section D-D: 1'-0", 5 at eq. spa., 1'-0", 4" clr.);
  19 / 26-28 5-#6T8, 5-#6T9; 20-22 / 25 6-#6T8, 6-#6T9; 23/24 6-#6 (T85-T87).
- **Shear stirrup grid**: #6 stirrups as pairs, interior grid 2'-0" x 3'-9" (TYP., U.N.O., Detail B), edge reinforcing #6 stirrups at 8" along every
  edge (18/29 52 + 54; 19 / 26-28 52 + 66; 20-22 / 25 52 + 77; 23/24 33 + 38 + 25 on the octagon edges); each = 1 S4 (S5) or 1 S6 lapped with 1 S7
  (3'-4" min. lap, p.345 LAPPED STIRRUP DETAIL) at the contractor's discretion; grid totals printed (484 / 572 / 786 / 1004) = S4(S5) + S6 counts.

CIP column base (Section A-A / B-B / C-C, Detail A):
- **M1 #11 verticals** (type 2, 2'-0" hook leg in the footing) around the perimeter, 3" clear, in groups: 18/29 60 (Section A-A p.336: 3-#11M1 at 8" = 1'-4" at the corners, 5- / 6- / 7-#11M1 groups between, 3'-0" laps; the group spacings carry fraction glyphs - read them on a crop before placing);
  19-22 44; 25-28 60 (3 / 4 / 6 / 10-#11M1 groups p.351); 23/24 66 incl. 9-#11M1 PAIRS AT 1'-3 1/2" = 10'-4" (p.343) with 3'-0" laps in the
  long faces. Hook depth = the "MIN." dimension below the footing top: 5'-10" (18/29), 5'-6" (19, 26-28), 6'-6" (20-22, 25), 6'-7" (23/24). DERIVED check:
  footing top - MIN + vertical leg puts every M1 top 2-3" below EL 17.00 (18: 0.17 + 16.58 = 16.75; 20-22: 0.50 + 16.33 = 16.83; 23/24: 0.92 + 15.83 = 16.75),
  which matches Detail A's 2" top cover - so the MIN. dims are the M1 hook level (the vectors alone do not separate the extension lines; reading = SCALED + DERIVED).
- **Ties**: #5S1 / #5S2 (S2 / S3 at 25-28 and 23/24) closed with 135 deg hooks engaging both the #4 stirrup and the M1 bars (note 4), counts per sheet 1
  (18: 91 ties, 8-#5S1 + 5-#5S2 per set; 19-22: 112; 25-28: 98; 23/24: 126 + 60); #4S3 (S4) stirrup sets 4 bars per set: 18 "7-#4 stirrup sets at 6"",
  "8-#4 stirrup sets at eq. spa.", 48-#5 ties in the lower part; spacing zones per sheet elevations (SCALED).
- **Top mat under segment 1** (Section C-C): 18/29 12-#8T10 pairs + 8-#8T9 pairs and 4-#8T9 bars; 20-22 16-#8T10 pairs top + 16-#8T11 bottom, 12-#8T12 pairs top +
  12-#8T13 pairs bottom; 25 14 / 14 / 10 / 10; 23/24 12-#8T90 pairs top + 12-#8T91 pairs bottom and 20-#8T92 pairs top + 20-#8T93 bottom (p.346).
- **Top edge bars** (Detail p.336 / 346): 12-#3S8 at approx. 1'-0" (long face) and 17-#3S8 (trans. face), #3S9 at each corner, #6S10 (long face) and #6S11
  (trans. face) with 2'-9" min. lap, 2" clear to the #3S8, 6" from the top.

### 3.3 Precast column segments (p.376-382) - counts NOT GIVEN

p.382 "PRECAST COLUMN SEGMENTS" bill: S1 #5 type 3, S2 #5 type 3, S3 #5 type 4, S4 #4 type 3, S5 #4 type 3, V1 #4 STR, all black (B); NO., LENGTH and
WEIGHT are "--" (note 3: quantities vary and are not shown; note 2: the reinforcing detailer develops shop drawings). Drawn layout (VECTOR text):
- Horizontal #5 hoops in each face (S1 / S2 outside / inside face, S3 bent corner bars at the core chamfers), 2" clear; levels per segment:
  12'-0" segments 27 levels (segment 4 of 19-28 / 23-24: 23 levels), chain 4" top and bottom zones (5 spa. at 4" = 1'-8"), then 6" (5 / 6 spa. at 6");
  18-5 / 29-5 (4'-0") 11 levels (10 spa. at 4"), 18-4 15, 29-4 19 (8 equal spaces 3'-9"), 19-4 14, 28-4 16.
- Verticals #4V1: 18/29 and 25-28 12-#4V1 at 12" each long face, 6-#4V1 at 10" each short face; 19-22 4-#4V1 at 8" + 4-#4V1 at 8 3/4" + 4 at 8"
  (long faces) and 10-#4V1 at 8" (short faces); 23/24 10-#4V1 at 8" + 10-#4V1 at 8" (long), 12-#4V1 at 8" (short); per face layer.
- #4S4 tie at each corner in each layer, #4S5 through-wall ties "placed as shown" (elevation grid), ties engage both hoops and verticals.
Bar lengths / counts per segment would have to be derived from this layout - not printed.

## 4. Bents 23 & 24

- Why they differ: not stated on the sheets (presumably the navigation-channel piers - an inference). What differs in the design: Type I pinned bearings at both (p.319), octagonal 51'-0" x 58'-6" footing with 30 piles, heavier footing steel (196,360 lb vs 93-111 k), 8 ksi top of base,
  more M1 (66 incl. pairs), 5 PT duct groups (A-E), the four piles under the base embedded only 1'-3".
- **Precast column size: 16'-0" x 11'-0", same hollow section (walls 1'-6", core 13'-0" x 8'-0", 1'-0" chamfers)** - p.380 text + vectors identical to
  p.376 / 378 / 381; p.343 Sections A-A / B-B 16'-0" x 11'-0". Segments 12'-0" x 3 + 10'-1", top of column 63.33, cap 5'-6", TOB 68.83. -> the
  UNRESOLVED "precast column size" flag in build-bonner-unit5.csx can be closed.
- **Bearing size (outside the listed pages, p.318 S3-70):** MASONRY PLATE PLAN - BENTS 23 & 24: plate **5'-6" (MAX) x 5'-6" (MAX)** (vs C x D
  3'-10" x 3'-6" (18), 5'-6" x 5'-6" (19-22, 25-28), 3'-8" x 3'-6" (29) in the summary table), 6 anchor rods in 2 rows 2'-2" each side of CL bearing,
  rows at 8 1/2" spacing near CL and 2'-1 1/2" (labels; exact rod layout to be read by the bearing agent); p.319 bearing data: Type I at 23 / 24 with "-"
  offsets. Bearing body heights / disc sizes are vendor designed (p.317 note 15) - NOT GIVEN.

## 5. Differences vs piers.csv / piles*.csv / the model

| item | model / data | plans | action |
|---|---|---|---|
| column | solid 16'-0" x 11'-0" from the footing top to the cap | CIP base solid 16x11 to EL 17.00, 3" grout, hollow precast segments (walls 1'-6", 1'-0" core chamfers) | build hollow segments as listed |
| 23 / 24 column | no column solid (size unresolved) | 16'-0" x 11'-0" same section | close the flag |
| footing top | flat at CL elevation | level over the 16x11 base, hip planes to edge elevation | build per section 2 |
| 19-4 segment | DERIVED 5'-6" | printed 5'-6" (p.379) | tag VECTOR |
| grout bed | assumed 0.25' | printed 3" | tag VECTOR |
| base concrete | 4.5 + 6 ksi CY in piers.csv | top lift 2'-6" (bill volumes) | geometry of the two lifts |
| 23/24 octagon | (+-25.5, +-8), corner cuts 15'-0" x 21'-3" | same (vectors) | none |
| footing size label "36'-0" x 43'-6"" (piers.csv) | ambiguous order | 43'-6" along S, 36'-0" along N | none (the script already takes the larger along S) |
| footing 19 volume | bill 338.1 CY | geometry 342.2 CY | report (bill same as 26-28) |
| pile embedment | 48" / 15" (4 piles at 23-24) | same | none |

## 6. Open / conflicting items

1. **PLAN CONFLICT** p.342 (20-22) and p.350 (25) T17: 15 #8 x 28'-10" printed with 385 lb = 5 bars; sheet totals use 385 lb.
2. **PLAN TYPO** p.342 T16 length "13'-18"" = 13'-10" (bar type 1 + weight).
3. Precast segment bar counts / lengths NOT GIVEN (p.382 "--"; shop drawings).
4. Exact positions of many footing bars between callouts (strip widths at the piles, short top bars between break lines) only SCALED; stirrup grid
   2'-0" x 3'-9" U.N.O. with drawn exceptions (Detail B) - SCALED.
5. M1 hook level: the "MIN." dims are read as the M1 hook level (consistent with lengths), not separated by vectors - SCALED / DERIVED.
6. Precast outer corner chamfer ~3/4" - SCALED, not dimensioned.
7. Pier protection material (bents 19-28 footing sides): extent / thickness NOT GIVEN (special provisions).
8. Footing 19 concrete 338.1 CY vs geometry 342.2 CY (20 piles) - minor, bill probably copied from 26-28.
9. Bearing details for 23 / 24 (anchor-rod layout, body) belong to the bearing sheets p.317-320 - only the masonry plate size was read here.

## 7. Data for a script

Per bent: `columns-footings.csv` (footing S / N, outline, bottom, level zone, CL / edge top elevations, pile embedment, base outline / elevations /
concrete lifts, M1 hook depth, grout bed, segment outline, every segment's bottom-top elevation, column top, cap height, TOB check).

Polygons (S, N in ft, about CL footing / column / cap):
- Segment outside: (-8,-5.5) (8,-5.5) (8,5.5) (-8,5.5) [optional 3/4" corner chamfers, SCALED]
- Segment void: (-5.5,-4) (5.5,-4) (6.5,-3) (6.5,3) (5.5,4) (-5.5,4) (-6.5,3) (-6.5,-3)
- CIP base: (-8,-5.5) (8,-5.5) (8,5.5) (-8,5.5)
- Footing level zone: same as the CIP base; footing outline per section 2; top facet edges = straight lines from the level-zone corners to the
  footing corners (octagon: each level-zone corner to the two ends of its diagonal edge).

Bars: `substructure-bars.csv`.
