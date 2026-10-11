# Cross frame gaps - plan data extraction (read-only)

Sources: Design 116 (I-80 WB, PDF pages 108 = sheet 106, 110 = 107A, 111 = 108, 115 = 112, 117 = 113A, 118 = 114, 121 = 117,
122-124 = 118-120) and Design 316 (Ramp G, PDF pages 101 = sheet 99, 104-105 = 102-103). Vector linework and text read with
PyMuPDF (`temp\cfgaps\gen.py`, `genr.py`, `r.py`), model queried with `temp\cli-main\sbm.exe run` on ONE copy
(`temp\cfgaps\m.sbm`, a copy of `temp\subelev-now.sbm`; nothing saved). Query scripts: `temp\cfgaps\q*.csx`.
Standing rule: anything not dimensioned / not drawn is UNRESOLVED, with the PDF page.

Bay index = index of the LEFT girder crossed (Unit 2: A0 B1 C2 F5 G6 H7 I8 J9 K10; a bay that bridges a discontinued girder
is named by its left girder, e.g. C-F = 2, B-F = 1, A-F = 0). "Absent" below means no drawn linework; where an annotation
covers the area it is flagged.

## 0. Key finding - what the "measured along girder" dimensions are

At the skewed Piers 8WB, 9WB, 10WB (Unit 2) every girder has its own bearing (staggered along the skewed support line) and
the plans put a frame LINE (perpendicular to the CL WB approach roadway = the model's radial line) through EACH girder's
bearing, with frames only in some bays. The ring-dimension "n'-n" with the half-black circle (horizontal cross frame
spacing along girder) is the distance, measured ALONG THE PREVIOUS GIRDER, from that girder's bearing to the radial line
through the NEXT girder's bearing.

Verified with the model (line through each bearing, crossings reported; `temp\cfgaps\q5.out`):

| plan dim | meaning | model | 
|---|---|---|
| 8WB 6'-2 1/16 | B bearing -> radial thru C bearing, along B | 6.170 ft |
| 8WB 10'-8 3/4 | F -> radial thru G, along F | 10.728 |
| 8WB 10'-6 7/16 | H -> radial thru I, along H | 10.535 |
| 9WB 9'-3 3/8 | F -> radial thru G | 9.280 |
| 9WB 9'-1 9/16 | H -> radial thru I | 9.133 |
| 10WB 7'-8 7/8 | F -> radial thru G | 7.742 |
| 10WB 7'-7 5/8 | H -> radial thru I | 7.633 |

All seven agree to 0.003 ft, so no new geometry is needed: a line = (Girder X, Distance = X's cumulative bearing distance
from its first bearing). The existing K-string lines at 8WB (A, F, H, J radials), 9WB (A, G, H, J, K) and 10WB (A, G, H, K)
already coincide with such radials (model station equals the radial's station to 0.01-0.1 m), but they were entered with
Bays = null (every bay), whereas the plan has only some bays on each line.

(Unit 3 dims do NOT reconcile the same way - see section 3.)

## 1. Unit 2 skewed Piers 8WB-10WB

Bearing cumulative distances (ft, from the girder's first bearing; A-E start at 6A, offsets 3.10/2.36/1.65/0.91/0.30 ft
from the model's girder start already removed; verified because model station of each line = station of the existing K-string
line). Line Distance = this value.

| girder | 8WB | 9WB | 10WB |
|---|---|---|---|
| A (0) | 659.127 | 868.425 | 1039.826 |
| B (1) | 665.840 | 875.009 | (ends) |
| C (2) | 672.561 | (ends) | (ends) |
| F (5) | 680.918 | 882.668 | 1052.074 |
| G (6) | 691.776 | 893.065 | 1061.759 |
| H (7) | 702.589 | 903.432 | 1071.426 |
| I (8) | 713.358 | 913.770 | 1081.077 |
| J (9) | 724.084 | 924.081 | 1090.711 |
| K (10) | 734.769 | 934.364 | 1100.329 |

In a script compute them: `d = gi.DistanceAt(j,0) - first(g)`, `first(g) = gi.DistanceAt(0,0)` for F-K,
`+ {3.10,2.36,1.65,0.91,0.30} ft` for A-E (6A begin offset), j = 3, 4, 5 for 8WB, 9WB, 10WB.

### 1a. Pier 8WB (sheet 107A, PDF 110) - plan frames per radial line

Types: default Type 3 unless marked. Circled marks read from the PDF text layer (positions in `temp\cfgaps\e110.json.words`).

| radial through | plan bays (Type) | model now (Kd = distance along K from 7WB bearing) |
|---|---|---|
| (regular, Kd 149.92, x 703) | 0,1,2,5,6 all Type 4 (A-B,B-C,C-F,F-G,G-H); 7,8,9 Type 3 | same - OK |
| (regular, Kd 171.33, x 746) | 0 (3), 1 (3), 5,6,7 Type 4, 8 (3), 9 (3); C-F (2) ABSENT | has bay 2 (C-F) Type 3 extra |
| A (Kd 192.90) | 0, 2, 5, 6 = Type 9 strut, 7, 8, 9; bay 1 (B-C) not visible - obscured by the "9'-0 13/16" dimension, UNRESOLVED | all bays (1 extra or UNRESOLVED); 9 at 6 OK |
| B | none | none |
| C | 0, 2 | missing |
| F (Kd 212.80) | 5, 7, 8, 9 (G-H absent, A-B/B-C/C-F absent) | all bays: extra 6 and sp3 bays 0,1,2 |
| G | 0, 1, 2, and 6 (only a 4.6 pt stub visible, rest hidden by the "42 41'27" triangle note: UNRESOLVED) | missing |
| H (Kd 234.64) | 7, 9 | has 7,8,9 + sp3 0..6: extras 8, 0..6 |
| I | 0, 1, 2 = Type 4, 5 = Type 4, 6, 8 | only a partial line {2,5}=4 at station 231593.65, 0.66 ft ahead of the radial (231593.45); bays 0,1,6,8 missing |
| J (Kd 255.87) | 9 | 9 + sp3 0..8: extras 0..8 |
| K | 0, 1 (B-F), 5,6,7 = Type 4, 8 = Type 9, 9 | 0, 5,6,7 =4, 8 =9, 9; B-F (1) missing - generator drops it (reason UNRESOLVED) |
| K + 23'-8 line | 6,7,8,9 all Type 4 (labels at y 180,203,226,249) | 6,7,8 only; bay 9 (J-K) is Type 3 in the model: WRONG |

Input (replace the K-string lines at the A/F/H/J radials by explicit-bay lines, add C/G/I):
```
// line through a bearing: Girder g, Distance d (table), Bays + BayStyles
L(g:0, d:659.127, bays:{0,2,5,6,7,8,9}, {6:"9"});              // A  (bay 1 UNRESOLVED)
L(g:2, d:672.561, bays:{0,2});                                  // C
L(g:5, d:680.918, bays:{5,7,8,9});                              // F
L(g:6, d:691.776, bays:{0,1,2,6});                              // G   (6 UNRESOLVED stub)
L(g:7, d:702.589, bays:{7,9});                                  // H
L(g:8, d:713.358, bays:{0,1,2,5,6,8}, {2:"4",5:"4"});           // I   (replaces the 714.5 ft partial line)
L(g:9, d:724.084, bays:{9});                                    // J
L(g:10,d:734.769, bays:{0,1,5,6,7,8,9}, {5:"4",6:"4",7:"4",8:"9"}); // K  (existing line: also fix bay 2 -> 1)
// K + 23'-8: bays (6,7,8,9) = Type 4 (add bay 9)
// (regular Kd 171.33 line: drop bay 2)
```
(Do NOT keep the old null-Bays K-string lines at these stations.)

### 1b. Pier 9WB (sheet 108, PDF 111): girders A,B,F,G,H,I,J,K

| radial through | plan bays (all Type 3 unless noted) | model now |
|---|---|---|
| A | 0, 5, 6, 7, 8, 9 (B-F absent) | all bays: extra 1 |
| B | none | none |
| F | 1, 5 | missing |
| G | 0, 1, 6, 8, 9 (F-G, H-I absent) | 6,7,8,9 + sp4 0,1,5: extras 5, 7 |
| H | 7, 9 | 7,8,9 + sp4 0,1,5,6: extras 8,0,1,5,6 |
| I | 0, 1, 5, 6, 8 | missing |
| J | 9 | 9 + sp4 0..8: extras |
| K | 5,6,7,8,9; Type 9 in bay 8 (I-J, label y 265-271, between girder ticks I 255.8 and J 278.7) | 5..9 with Type 9 in bay 9 (J-K): WRONG bay |

Inputs: L(0,868.425,{0,5,6,7,8,9}), L(5,882.668,{1,5}), L(6,893.065,{0,1,6,8,9}), L(7,903.432,{7,9}),
L(8,913.770,{0,1,5,6,8}), L(9,924.081,{9}), L(10,934.364,{5,6,7,8,9},{8:"9"}).

### 1c. Pier 10WB (sheet 108, PDF 111): girders A,F,G,H,I,J,K (B ended)

| radial through | plan bays | model now |
|---|---|---|
| A | 0 (A-F), 6, 7, 8, 9 (F-G absent) | all bays: extra 5 |
| F | 5 | missing |
| G | 0, 6, 8, 9 | 6..9 + sp5 0,5: extras 5? (sp5 F-G), 7 |
| H | 7 | 7,8,9 + sp5 0,5,6: extras |
| I | 0, 5, 6, 8 | missing |
| J | 9 | missing |
| K | 0 (A-F), 5,6,7, 8 = Type 9, 9 (Type 9 label y 263-270 between I and J) | 0.., Type 9 at bay 9: WRONG bay |

Inputs: L(0,1039.826,{0,6,7,8,9}), L(5,1052.074,{5}), L(6,1061.759,{0,6,8,9}), L(7,1071.426,{7}),
L(8,1081.077,{0,5,6,8}), L(9,1090.711,{9}), L(10,1100.329,{0,5,6,7,8,9},{8:"9"}).

Note: Type 9 labels (struts) are in bay I-J at 8WB, 9WB and 10WB; the model script puts them in J-K at 9WB/10WB.

Absence caveat (all three piers): lines are cut where circles/annotations sit; the bay lists above are drawn linework only.
Items marked UNRESOLVED need a visual check of the original drawing (PDF 110: A radial bay 1 and G radial bay 6).

## 2. Unit 2 bay E-F at Pier 5WB / 6A (sheet 117 detail, PDF 121, section A-A "Girders E & F at Pier Nos. 6A & 5WB")

- Note on sheet 106 (PDF 108): frames A-E at 6A are parallel to the 6A line (model has them: 4 frames A-B..D-E);
  "Delta for orientation of cross frame between girders E and F, see detail on design sheet 117".
- Geometry as dimensioned: frame is the STRAIGHT LINE between E (at 6A) and F (at 5WB): F stiffener pair 3 1/2 each side of
  "CL ahead brg Pier 5WB", E stiffener pair 3 1/2 each side of "CL ahead brg Pier 6A"; girder ends 1'-0 beyond; welds 5/16 both
  sides. Angles given: at F 90 00'00" (5WB line to girder F) and 88 16'49" (E-F line to girder F); at E 90 59'34" (E-F line
  to girder E) and 87 04'37" (6A bearing line to girder E).
- Model check: plan chord from F@5WB bearing to E@6A bearing = 9.687 ft; angle to F tangent 88.2528 deg (88 15'10"),
  to E tangent 91.0034 deg (91 00'12"): plan 88 16'49" / 90 59'34" -> agree to 0.03 deg (the plan chord joins the 3 1/2"
  offset stiffeners, not the CL bearing points).
- Type: Type 10 end frame ("End cross frame Type 10 (typ. at Pier 5WB & at Pier 6A), note location for fit up").
  Members/gusset as Type 10 (W12x40 top, L5x5x5/8 diagonals, 2-MC6x15.3 bottom).
- Model now: NO frame in bay E-F at 5WB/6A (support frames: F-K at 5WB (bays 5-9), A-E at 6A (bays 0-3); the E-F bay has only
  the intermediate frame at 24 ft).
- Not dimensioned: which of the two stiffeners per girder (ahead 3 1/2" or back) the frame bolts to - drawn on the upper
  stiffener of the section; the section does not say if "upper" is ahead or back = UNRESOLVED (PDF 121).
- Current input cannot express this (a frame belongs to ONE support line; here its ends are on two lines). Minimal new input:
  `CrossFrameDefinition.Links` = list of { LeftGirder, LeftSupport (name), RightGirder, RightSupport (name), Style, Side
  (Ahead/Back) }; frame plane = vertical plane through the two bearing points (+ side offset 3.5"), connection stiffener
  planes follow the angles above. Sample: `{ LeftGirder: 4 (E), LeftSupport: "Pier 6A", RightGirder: 5 (F), RightSupport:
  "Pier 5WB", Style: "10" }`.

## 3. Unit 3 (sheets 112, 113A, 114 = PDF 115, 117, 118)

Does NOT follow the Unit 2 rule exactly - the plan dims do not equal the model bearing offsets:

| plan | plan value | model (line thru next bearing) |
|---|---|---|
| 12WB A-string "21'-11 3/16" (A bearing -> F radial?) | 21.932 | 21.999 |
| 12WB 5'-0 half circle (E -> F) | 5.000 | 5.074 |
| 12WB 6'-11 3/8 half circle (at J) | 6.948 | no bearing radial gives it (4.958) |
| 13WB 8'-0 half circle (E -> G radial) | 8.000 | 7.841 |
| 13WB A-string 16'-1 15/16, 12'-10 7/8, 7'-4 1/8 | 16.161, 12.911, 7.344 | A crossings of bearing radials: 2.087, 4.281, 8.231, 12.140, 16.009 (G), 19.838, 23.628, 27.380 - none gives 29.07 or 36.4 |

Either the model's Unit 3 bearings at 12WB/13WB are 0.07-0.16 ft off the plan (to be checked, girder data tables on PDF 115
and 117) or these dims have a different origin; UNRESOLVED. Hence the exact distances of the Unit 3 partial lines are
UNRESOLVED; what is drawn (frame linework, bays by girder letters, PDF 115 / 117):

- 12WB (PDF 115): support frames on the (straight) skewed pier line - model has them (all bays Type 5).
  Partial lines, drawn bays: line through A's bearing: B-C..I-J (bay 1..8); line crossing A at the "21'-11 3/16" dim:
  A-B, B-C, C-D, D-E (0..3); a line G-H,H-I,I-J (6,7,8) near the pier; a line A-B..H-I (0..7) ending 6'-11 3/8 before J's
  bearing (the 6'-11 3/8 dim); a line D-E,E-F,F-G beyond. Tolerances: positions to +-1 pt (0.5 ft) only.
- 13WB (PDF 117): lines drawn: radial through G's bearing crossing E at 8'-0: bays A-C, C-D, D-E (0,2,3 in unit-3 indexing);
  further lines H-I, I-J near H/I bearings; the three A-string dims are on lines in bays A-C (and below).
  Unit 3 bay indexing (model): A0 B1 C2 D3 E4 F5 G6 H7 I8 J9 (B ends before 13WB, so A-C = bay 0 there).
- Type 6 at the pier frames (support frames), bay A-D at 14WB and 15WB:
  PDF 117 text layer: Type 6 circle pairs at x=840.0/852.7 (pt) = Pier 14WB line, top bay only; PDF 118: pair at 525.1/553.2
  = Pier 15WB, top bay only; single circles (intermediate lines, top bay) at x = 730, 785, 902, 957, 1012 (PDF 117) and
  302.8 ... 719.7 (PDF 118: 8 lines up to the diaphragm line, whose 7 Type 8 circles are at x ~ 780).
  Model now: support frames at 14WB (sup3) and 15WB (sup4): bays A-D,D-E..I-J all Type 5; plan: bay A-D = Type 6.
  CANNOT be expressed: SupportStyles is per support. Minimal new input:
  `CrossFrameDefinition.SupportBayStyles`: Dictionary<string supportName, Dictionary<int bay, string style>>, e.g.
  `["Pier 14WB"] = { [0] = "6" }, ["Pier 15WB"] = { [0] = "6" }` (unlisted bays use SupportStyles).

## 4. Ramp G Unit 4, Pier 17G zone (Design 316 sheet 99 = PDF 101)

Same principle as Unit 2: radial lines through the bearings of A, B, C, D at the skewed Pier 17G (pier line 49 00'00" ;
girders A-D, bays 0 A-B, 1 B-C, 2 C-D). Drawn frames (labels from the text layer; positions pt):

| line (radial through) | A-B (0) | B-C (1) | C-D (2) | A-string |
|---|---|---|---|---|
| L1 = A's bearing (also end of the "9 SPA @ 21'-5 13/16 = 193'-4 1/16") | Type 3 | Type 3 | Type 2 | P0 |
| L2 = B's bearing | - | Type 2 | - | not dimensioned |
| L3 = C's bearing | Type 2 | - | Type 2 | P0 + 21'-11 1/4 |
| L4 = D's bearing | Type 2 | Type 3 | Type 3 | P3 + 10'-7 11/16 |
| L5 (regular) | Type 1 (no mark) | Type 1 | Type 2 | + 22'-5 |
| L6 on | Type 1 all bays | | | 7 SPA @ 22'-1 |

(D string dims 22'-4 1/8, 10'-10 1/16, 22'-10 1/16 between L1/L3, L3/L4, L4/L5 confirm.)
Model now (Unit 4 lines in sp2/sp3, `temp\cfgaps\q8.out`): P0 (A's bearing, Ad 259.02 in the 17G span) all Type 1;
+21'-11 1/4: (3,3,2); +10'-7 11/16: all Type 2; +22'-5: (2,3,3). I.e. the types are one line late and L2 is missing.
Inputs (Girder 0 = A; distances from A's first bearing; A bearing at 17G = 616.529 ft; B 627.183, C 637.747, D 648.226):
```
L(g:0,d:616.529,            bays{0,1,2}, {0:"3",1:"3",2:"2"})   // L1 (replace the default Type 1 line)
L(g:1,d:627.183,            bays{1},     {1:"2"})              // L2 (new)
L(g:0,d:616.529+21.9375,    bays{0,2},   {0:"2",2:"2"})        // L3  (21'-11 1/4)
L(g:0,d:616.529+21.9375+10.6406, bays{0,1,2}, {0:"2",1:"3",2:"3"}) // L4 (10'-7 11/16)
L(g:0,d:...+22.4167,        bays{0,1,2}, {2:"2"})              // L5 (22'-5); A-B,B-C default Type 1
```
The L3-L5 positions are the plan A-string dims; I did not run the "thru bearing" crossing check on Ramp G (UNRESOLVED
check: line through C's bearing should cross A 21.9375 ft ahead of A's bearing).
Not covered: the Pier 16G zone (sheet 98).

## 5. Remaining assumptions - plan values

- Tie plate length (10" assumed): NOT dimensioned (I-80 Types 1-4 sheet 118 PDF 122, Ramp G Types 2,3 PDF 104). Plate is
  1/2" x ... at 1'-4 each side of the bay CL, bolted with 2 bolts across (Section A-A). Elevation view scales ~4.7" along the
  chord (I-80 Type 1, PDF 122, x 255-345 pt) - graphic only: UNRESOLVED.
- Top gusset bolt rows: CONFIRMED 2 rows x 2 columns, WP in the middle. I-80 Types 1-4 (PDF 122): rows 1 3/4 above/below WP
  = pitch 3 1/2, gage 3 1/2, edge 1 3/4 (matches the csx). Ramp G Types 1-3: 2 x 2, gage 3, rows ~3 (graphic; edge 1 1/2).
- Abutment frame bottom bolts: I-80 abutment (Units 1,3; PDF 124): bottom gusset 2 columns x 2 rows, rows 3" above and
  below the WP = pitch 6" (csx uses the common pitch 3.5" -> needs a per-connection bottom pitch of 6"), W.P. 8" above the bottom
  flange, top gusset 6 rows @ 3 1/2 (OK), top W24x68 WP 2'-2 below top of slab (OK). Ramp G abutment (PDF 105): 2 x 2 bolts
  on the WT5x15 chord (rows pitch not dimensioned: UNRESOLVED), top W24x68 end: 7 rows @ 3 = 1'-6 (OK, 2 columns, gage 3).
- Ramp G abutment W24x68: the "4 (TYP.)" dimension is from the TOP OF THE TOP FLANGE (top of steel) down to the top of the
  W24x68 / W.P. (PDF 105). The csx assumed 4" below the top of WEB: the correct offset below top of web is 4" - tf (tf =
  the girder's top flange plate thickness at the abutment end; read from the girder elevation: sheet 90 / sheet 100).
  Then TopWorkPointOffset = (4" - tf) + 23.7"/2 (below top of web).
- Studs on W top struts: SHOWN. I-80 abutment W24x68 (Section B-B, PDF 124): 7/8" dia x 0'-5" end-welded studs, 2 across, 2" from
  the flange edge, 1" radius cope 8 1/2" long. Type 10 W12x40 (Section C-C): 7/8" x 5" studs, 2 across, 1 1/2" from the edge,
  1 1/2" x 1 1/2" corner cut. Ramp G (PDF 105): W24x68 (Sect A-A) 2" from edge, cope 7 1/2"; W10x33 (Sect B-B) 1 1/2" from edge.
  Longitudinal stud pitch / number per strut is NOT dimensioned (2 rows are drawn with ~10" spacing, graphic only) = UNRESOLVED.

## 6. New inputs needed (summary)

1. Nothing new for Unit 2 Piers 8-10WB: explicit Bays lines (section 1) - but the existing null-Bays K-string lines at
   the A/F/H/J (8WB), A/G/H/J (9WB), A/G/H (10WB) radials must be replaced by the explicit lines above (they coincide).
2. `Links` (frame between two support lines) for Unit 2 bay E-F at 5WB/6A.
3. `SupportBayStyles` (style per support per bay) for Unit 3 Type 6 at 14WB/15WB.
4. Per-connection bottom bolt pitch (or `BottomBoltPitch`) for abutment frames (6" on I-80).
5. Generator: bay B-F at K's 8WB radial is dropped (needs a reason / BayValidAt check).
