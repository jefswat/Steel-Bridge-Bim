# Bonner Unit 5 - vertical (and longitudinal) PT bars: plan reading

Plan set: `C:\SBMData\B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf` (PDF page numbers).
Method: PyMuPDF text layer + vector linework (dimension extension lines, duct outlines, plate lines); crops rendered to verify.
Tags: **VECTOR** = dimensioned and/or reproduced by vector coordinates (to < 0.1 in); **SCALED** = only drawn, measured from vectors;
**NOT GIVEN** = not on the sheets. Rows per bar: `vertical-pt.csv`.

Sign convention: x from CL girder, + = right looking up-station. Every section on both sheets is drawn
"SYMMETRICAL ABOUT CL GIRDER", so each bar has a mirror bar at -x (both listed in the csv). Both sheets draw the deck top and soffit
LEVEL (no cross slope): offsets are in the box section frame (normal to the top); the model's box is square to the sloped top.

## 1. Bent segment vertical PT bars (p.278, S3-30)

Scale of SECTION / A-A / B-B: 2.25 pt per inch (3/8" = 1'-0"); CL girder at x = 885.35 pt (SECTION), CL split at x = 2034.5 pt (B-B).

| item | value | how |
|---|---|---|
| bar / duct | 1 3/4" dia. PT bar, grade 150; 2 3/4" O.D. duct | text (details); duct outline 6.2 pt = 2.76 in VECTOR |
| transverse | +-3'-8" and +-6'-0" from CL girder (dims 3'-8" + 2'-4"), at top AND bottom | VECTOR (duct centres 723.4 / 786.4 / 984.4 / 1047.4 pt -> -72.0 / -44.0 / +44.0 / +72.0 in) |
| inclination | plumb (normal to the drawn deck top); ducts run straight from y 269.9 to 743.5 at constant x | VECTOR |
| along | 2'-6" from CL split bent segments in EACH half (B-B: 1'-6" to the diaphragm-opening edge + 1'-0" (TYP.)); upstation half = mirror | VECTOR (B-B duct centres 1967.05 / 2102.0 pt = 29.98 / 30.0 in; A-A plan circles 67.45 pt = 29.98 in each side of the split) |
| all 4 bars of a half at one station | yes: A-A half plans show 2 bars per side of CL per half, all on the 2'-6" line; B-B shows one duct per half (the +-3'-8 / +-6'-0 bars overlap) | VECTOR |
| top anchor | plate bearing face (dot on the bar axis) 10 1/2" below the deck top; stressing end in a top pocket | text "10 1/2" (TOP detail); section vectors 23.6 pt = 10.49 in |
| bottom anchor | plate bearing face 6 1/2" above the soffit; plate cast in, nut below (dead end) | text "6 1/2" (BOTTOM detail, to the bottom face of the bottom slab); vectors 14.6 pt = 6.49 in to the soffit line y 762.02 |
| soffit at the bar | the LOWERED bent soffit: in B-B the box bottom is y 755.3 at the bulkhead face (19'-0" = p.268 section 1 depth) and y 762.02 from about 1'-6" inside it to the split, the same line the 1'-5" and 6 1/2" dims use; drawn depth at the bar 19'-2 15/16" | VECTOR |
| plate-to-plate length | 230.93 - 10.5 - 6.5 = 213.9 in = 17'-9.9"; table 17'-10" | consistent |
| anchor plate / pocket size | NOT GIVEN. Drawn: plate about 5" wide x 1 3/4" thick (SCALED), top pocket about 8 1/4" wide at the deck top tapering to about 5 1/2" at the plate (SCALED) | SCALED |
| protection | note 6: stress, fill pockets, grout in the precast yard | text |

**Count ("4 per segment", "18 segments"):** "segment" = one HALF (DB or UB). The A-A half plans show, for EACH half, two bars on each
side of CL (4 per half, all 2'-6" from the split), and B-B shows a duct in each half. Table: 4 x 18 = 72 bars, 658.4 lb per half
(164.6 lb per bar = 9.23 lb/ft x 17'-10"), total 11,851.2 = 18 x 658.4.
**CONFLICT:** Unit 5 has 10 cantilever bents (19-28, segments 19-DB/UB .. 28-DB/UB = 20 halves; repo `segments.csv`), so the table
should read 20 halves (80 bars, 13,168 lb). Likewise the LONGITUDINAL PT BAR row (9 segments) should be 10 bent pairs. The sheet itself
gives no hint of a bent without these bars - plan inconsistency, user / reported item.

## 2. End segment vertical PT bars (p.272, S3-24; 18-ES at bent 18, 29-ES at bent 29)

Scales: SECTION / PLAN 2.25 pt/in (CL girder x = 819.6 pt); SECTION A-A 3.0 pt/in.

| item | value | how |
|---|---|---|
| table | VERTICAL PT BAR: 4 per segment, 1 3/4" dia., length 7'-10", 288.8 lb per segment, 2 segments, 577.6 lb, 283 kip | text; 288.8 / 4 / 7.833 = 9.22 lb/ft = same bar as p.278 |
| layout | 2 bars in each web (section symmetrical), INCLINED in the web plane, parallel to the web (top outward) | VECTOR |
| top (at the top anchor, on the bar axis) | 8'-11" and 8'-11" + 1'-4 3/4" = 10'-3 3/4" from CL | VECTOR: dims end at x 1060.3 / 1098.0 = where the duct axis meets y 316.7 = 10 1/2" below the deck top (y 293.26) |
| bottom (at the bottom anchor, on the axis) | 7'-0" and 7'-0" + 1'-4 3/4" = 8'-4 3/4" from CL | VECTOR: dims at x 1007.6 / 1045.3 = axis at y 521.8 = 6.43" above the soffit (y 536.26) |
| slope | 23" out over 91" rise = 1 : 3.96 (14.2 deg from vertical); vector duct slope 0.257 = 14.4 deg | VECTOR |
| along | 2'-0" from the end segment's CLOSURE-JOINT face (= 6'-8" from the anchor face, segment 8'-8"); both bars of a web at the same station | PLAN dim "2'-0"" from the segment edge to the bar dots (dots at x 1009 / 1047 = the bottom points); A-A duct centre 2001.3 pt is 72.0 pt = 24.0 in from the CJ face (2073.3) and 104 in from the anchor face (1761.3); bars drawn plumb in A-A (inclination is transverse only) |
| A-A crop (`pt-profiles/img/p272_AA.png`) | the bar lies between E2 and E1: E1 is 1'-4" from the CJ face, the bar 8" beyond E1 (2'-0" total), 1'-4" before E2 | VECTOR (the bar itself is not in the A-A dim chain; its position is dimensioned in the PLAN) |
| anchor elevations | p.272 does not dimension them; note 7 refers to the p.278 VERTICAL PT BAR DETAILS -> 10 1/2" below the deck top, 6 1/2" above the soffit; the p.272 vectors agree (10.4" / 6.4") | text + VECTOR check |
| depth | drawn 108.0" = 9'-0" (= repo geometry-control box height 9.00 at bents 18 / 29) | VECTOR |
| length | sqrt(91^2 + 23^2) = 93.9" = 7'-9.9" vs 7'-10" | consistent |
| 29-ES | opposite hand (anchor face at bent 29, CJ face down-station): same dims from its CJ face | per the 18 / 29 layout (sheet draws one end segment) |
| plates / pockets | NOT GIVEN (same drawn symbols as p.278) | - |

## 3. Bent segment LONGITUDINAL PT bars (p.278; also p.299 S3-51)

| item | value | how |
|---|---|---|
| table | 4 per bent (one bar through both halves), 1 3/4" dia., 8'-3", 304.6 lb, 9 "segments", 2741.4 lb, 200 kip (rev. 1) | text; 9 vs 10 bents - see section 1 |
| transverse | +-3'-4" and +-6'-4" (dims 3'-4" + 3'-0" in SECTION and in the A-A bottom slab half plan; p.299 Section C-C same) | VECTOR (plate centres 795.5 / 714.7 pt -> 39.9 / 75.9 in) |
| height | axis 1'-5" above the bent soffit (SECTION and B-B; p.299 C-C) | VECTOR (38.2 pt) |
| along | horizontal along CL girder through the CL split; anchor plate bearing face 8 1/2" inside each bulkhead face (bulkhead faces at +-5'-0") -> plates at +-4'-3 1/2" | VECTOR (B-B plate faces 1918.7 / 2150.4 pt) + LONGITUDINAL PT BAR DETAILS (8 1/2", 10" x 10" plate, 10 1/2" x 10 1/2" pocket) |
| duct | 2 3/4" O.D. | text |
| **CONFLICT** | plate to plate = 103.0" = 8'-7" (both B-B vectors and 2 x (60 - 8 1/2)), table 8'-3" (99"). p.299 "2 3/4" duct for permanent 1 3/4" PT bar (MIN. LENGTH = 9'-11 1/2")" = 103 + 2 x 8 1/4 supports 8'-7" | reported, not resolved |
| role | stressed in erection step 6 (p.313) to squeeze DB / UB together ("permanent PT bar in the bottom") | text |

## Not given anywhere read

Anchor plate sizes of the vertical bars, top pocket size, coupler / nut hardware, the vertical bars' plate orientation on the
inclined end-segment bars (detail drawn generic), the cross-slope frame (all sections drawn level).
