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

(Superseded by section 8 - the partial lines are now placed from the plan dimensions; the table below is the first pass.)

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

## 7. Implementation (feature/xframes)

Inputs added: `CrossFrameDefinition.Links` (E-F frame, section 2), `CrossFrameDefinition.SupportBayStyles` (section 3), `CrossFrameStyle.BottomBoltPitch`
(I-80 abutment, 6"), `CrossFrameStyle.Unresolved` (notes raised as UNRESOLVED warnings once per bridge), plus a generator warning for every bay a plan line
lists but cannot generate. Scripts: `samples/plans/cross-frames-i80.csx`, `cross-frames-ramp-g.csx` (re-runnable).

Frame counts before -> after (all frames incl. support frames): I-80 Unit 2 467 -> 450 (extra bays removed, missing radials added, +1 E-F link, Type 4 +1, Type 9
now in bay I-J at 9WB/10WB); I-80 Unit 3 331 -> 331 (Type 6 13 -> 15 at the 14WB/15WB support frames, Type 5 295 -> 293); Ramp G Unit 4 144 -> 144 (Type 2 15 -> 16, Type 1 114 -> 113,
new B line); all other units unchanged.

Checks: the K-string distances land on the table values (8WB 734.771, 9WB 934.365, 10WB 1100.328 ft against 734.769 / 934.364 / 1100.329); the Ramp G L3 line through C's
bearing crosses A 21.939 ft ahead of A's bearing (plan 21'-11 1/4 = 21.9375), L4 +10.640, L5 +22.417; B's bearing at 17G 627.183 ft as in the report; the link frame's
plan length is 9.687 ft; no frame lines of the new inputs fall in splice plates or within 3 ft of a bearing (generator reports none).

Raised, not placed (UNRESOLVED) - first pass; see section 8 for what was settled since:
- 8WB K radial, bay B-F: the line lies 0.045 ft from the centre line of the cross girder XG-C (C ends 59'-1 1/16 past its 8WB bearing), so a B-F frame cannot be built there;
  the generator reports it. Either the plan's mark is the cross girder itself or the line is mis-read - check PDF page 110.
- 8WB A radial bay B-C (hidden by the "9'-0 13/16" dimension); 8WB G radial bay G-H and the I radial's types are entered as in the report's input block (the table text
  reads "0, 1, 2 = Type 4" for I, the input block gives Type 4 only for bays 2 and 5: the input block was followed).
- Link frame (E-F): which stiffener of the pair it bolts to (ahead/back, not dimensioned); the frame plane differs from the bearing stiffener planes by 1.90 deg (E) and
  1.75 deg (F) ("note location for fit up").
- Tie plate length (10" kept, warning), stud pitch on the W top struts (studs not modeled, warning), Ramp G abutment WT5x15 bottom row pitch (3" kept, warning).
- Unit 3 12WB/13WB partial lines (section 3) and the Pier 16G zone of Ramp G were not placed.
Resolved: top gusset 2 bolt rows (styles), I-80 abutment bottom rows 6" apart, Ramp G abutment W24x68 top 4" below the top of the top flange (work point 14.975" below the
top of web with tf = 7/8").

## 8. Second pass (2026-10-11): Unit 3 partial lines, Ramp G 16G zone, visual checks

Method: frame linework per bay from the PDF vectors (`temp\cf\gen2.py`: non-straddling 0.6 pt segments, x per bay), identified against
radial test lines generated in the model ONE LINE PER RUN (`temp\cf\probe1.csx`). Note: the first pass (section 3) probed several lines in one
run and matched frames by the crossing at the reference girder; a line within 6" of a bearing snaps to it, so frames of a different line were
picked up. That is why section 3 read "21.999 vs 21.932": both numbers are right, for two different lines (F's bearing radial / the A dim).

### 8.1 I-80 Unit 3, Pier 12WB (PDF 115) - placed

Drawn lines near the pier (x at mid-bay A-B, pt; model crossing relative to each girder's 12WB bearing):

| drawn line | bays drawn | model / plan |
|---|---|---|
| J - 66'-1 (556-565) | all | J string, unchanged |
| J - 45'-2 1/4 (596..632) | B-C..I-J (A-B absent: it would lie on the pier line, A-2.01 / B-5.50) | J string, bay A-B removed |
| A + 21'-11 3/16 (673..686) | A-B, B-C, C-D, D-E, G-H, H-I, I-J (one straight line; E-F, F-G absent) | new line (Girder A, A's bearing + 21.932); crosses E at +5.007 = the "5'-0" (E's bearing to the line), J at -20.320 |
| I + 6'-11 3/8 (714..727) | A-B..H-I (I-J absent) | new line (Girder I, I's bearing + 6.948); identical to the radial through J's ahead jacking stiffener (J + 2'-0, A +43.436 both ways) |
| J + 23'-0 (749..765) | C-D..I-J (A-B/B-C at B's cross girder) | J string, unchanged |

Checks: pier-line spacings A-B 8'-5 1/4, B-C 8'-7 7/8 = model 8.4375 / 8.6563 ft. The J-string line at J - 22'-7 1/8 (2 SPA @ 22'-7 1/8 = 45'-2 1/4)
is NOT drawn: the lower bays G-H..I-J lie on the A + 21'-11 3/16 line (collinear with the upper bays to 0.2 pt; scaled I-J crossing I - 15.2 vs
-15.26 for that line and -17.53 for J - 22.6). Modeled per the A / E dims and raised as UNRESOLVED (plan conflict) on that line.
The line is drawn through F's bearing; in the model F's bearing radial crosses A at +21.999 (0.067 ft from the dim) - noted in the same warning.
The I + 6'-11 3/8 line: frame linework scales about 2 ft further ahead than the dimension (A about +45.5); placed by the dimension, raised as UNRESOLVED.
The small "half circle" dims are not to scale on this sheet (5'-0 is drawn 6.7 pt = 3.9 ft, 6'-11 3/8 13.4 pt = 7.7 ft at 1.73 pt/ft).

### 8.2 I-80 Unit 3, Pier 13WB (PDF 117, sign support location plan PDF 180) - placed

| drawn line | bays drawn | model / plan |
|---|---|---|
| J - 33'-11 15/16 (356-361) | all | J string, unchanged |
| J - 16'-11 15/16 (397) | H-I, I-J only | J string, bays A-C..G-H removed (A-E is the G radial below; F-G, G-H absent) |
| A + 16'-1 15/16 (412) | A-C, C-D, D-E | new line (Girder A); crosses E at +7.995 = the "8'-0" along E |
| A + 29'-0 13/16 (+12'-10 7/8; 438-440) | A-C only | new line; it is the sign truss support bracket line (box on PDF 117, "CL sign truss support brackets" 7'-4 apart on PDF 180); the frame is drawn beside the bracket stiffener as on the J + 9'-4 line |
| A + 36'-5 (+7'-4 1/8) = J + 9'-4 (455-457) | all | J string, unchanged: the model crosses A at +36.416 = the chain 16'-1 15/16 + 12'-10 7/8 + 7'-4 1/8 to 0.000 ft |

UNRESOLVED (raised on the A + 16'-1 15/16 line): it is drawn through G's bearing, but G's bearing radial crosses A at +16.009 (0.15 ft from the
plan chain, consistent at A and E) - check G's 13WB bearing / girder geometry. Pier-line spacings at 13WB: A-C 6'-3 7/16, C-D 6'-9 1/16 (300 dpi
clips) vs model 6.345 / 6.707 ft; A-D 13.042 vs 13.052: girder C's 13WB bearing is about 0.05 ft off along the pier line (model input, not a cross
frame item).

### 8.3 Ramp G Unit 4, Pier 16G zone (sheet 98 = PDF 100) - transcribed

"3 SPA @ ABT. 14'-7 9/16 = 43'-10 5/8" along A and along D = one radial line through each girder's CL bearing (jacking stiffeners 3'-3 each
side): the model's radials through B, C, D cross A at +14.629, +29.258, +43.887 and D at -29.258, -14.629. Types (circle marks both sides of each
frame, 220 dpi): A line (2, 2, 2); B line (3, 3, 2); C line (2, 3, 3); D line (2, 2, 2). The earlier input had C-D = Type 3 on the B line (now 2) and
equal A-string spaces (now per-girder bearing lines). Pier 17G zone (sheet 99 = PDF 101): re-read, types and lines as section 4/7 (no change).

### 8.4 Visual checks on PDF 110 (8WB) - settled from the vectors

- A radial bay B-C: ABSENT. No 0.6 pt linework at x 788-789 between B (y 122.6) and C (y 135.6); only the 0.24 pt "9'-0 13/16" dimension crosses there.
  The connection ticks are one-sided (at B toward A-B, at C toward C-F), as on the C radial whose B-C bay is also empty. (Input unchanged.)
- G radial bay G-H: PRESENT. Frame pieces at x 850.1-850.3, y 175.4-179.8 and 188.0-195.2 either side of the "42 41'27"" text (y 181-188), and a
  one-sided tick at H. (Input unchanged.)
- I radial types: double lines + "4" marks only in C-F and F-G = Type 4 in bays 2 and 5; A-B, B-C single lines = Type 3 (the input block was right).
- K radial "bay B-F": the line inside the cross girder callout (obround) is the cross girder XG-C itself (C ends 59'-1 1/16 past 8WB; the radial is
  0.045 ft from its CL) - removed from the K line's bays; its "not generated" warning is gone.

### 8.5 Counts and warnings (model copy, scripts re-run: cross-frames-i80.csx then cross-frames-ramp-g.csx)

| unit | before | after |
|---|---|---|
| I-80 Unit 1 | 272 | 272 |
| I-80 Unit 2 | 450 | 450 (K line bay B-F was never generated) |
| I-80 Unit 3 | 331 (293 x 5) | 336 (298 x 5): 12WB +6 (A-B removed, J-22.6 line 8 -> A+21.93 line 7, I line +8), 13WB -1 (J-17 line 7 -> 2, +3, +1) |
| Ramp G Unit 4 | 144 (16 x 2, 9 x 3) | 144 (17 x 2, 8 x 3) |

Generator warnings: removed - the 8WB K radial B-F "listed but NOT generated" (Unit 2) and the five Unit 3 "closer than 3.0 ft to a bearing"
(113.12, 135.71 x2, 336.31 x2 along J). Added - three UNRESOLVED line notes (`CrossFrameLine.Unresolved`, above). Remaining frames inside splice
plates: five in Unit 3, all plan-consistent by dimension: J + 69'-2 3/8 (11WB string) A-B / B-C at 2.18 ft from FS22 on B (B: F.S. 22 to Pier 12WB
= 50'-0), J + 115'-0 (12WB string) D-E / E-F / F-G at 2.22 ft (E) and 2.02 ft (F) from FS24 - kept, not next to the piers. (The 7 near-splice
frames of the earlier handoff are gone; Ramp G and I-80 Units 1-2 report none.)

New input: `CrossFrameLine.Unresolved` (notes raised as "Frame line ... UNRESOLVED (plan, nothing invented) - note"; the frames are generated
as listed).
