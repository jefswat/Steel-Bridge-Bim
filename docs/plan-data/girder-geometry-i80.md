# I-80 WB girder plan geometry (Design 116) - 2026-10-10

Sources (PDF page = design sheet + 2/3): GIRDER RADIUS DATA and girder data tables on PDF 110 (sheet 107A, Unit 2 spans 6WB-9WB),
PDF 111 (sheet 108, Unit 2 spans 9WB-11WB), PDF 119 (sheet 115, Unit 3 radius table); structural steel layouts PDF 108 (106),
110/111 (107A/108), 115 (112), 117 (113A), 118 (114); girder data of Unit 1 PDF 107 (sheet 105). All fractions confirmed on
300 dpi grayscale clips. Plan stations/offsets are of CL I-80; the model alignment (WB approach roadway CL) is concentric, 41.08' LT.

## Which girders are not parallel to the roadway
- Unit 1 (I-80), Ramp G: none. Unit 1 has no radius table (constant 11'-6 spacing on tangent, span differences are skew only; closes
  within 0.11" top of slab). Ramp G A-D radii 954'-6 / 944'-10 / 935'-2 / 925'-6 (and 1545'-6 ... on the R 1550 curve) are the CL radius
  +-4'-6, +5'-2, ... exactly: concentric offset curves.
- Unit 2 F-K (R 2335'-11 ... 2393'-5) and Unit 3 D-J (R 2324'-5 ... 2393'-5) = CL radius 2358'-11 +- offset: concentric, as modeled.
- Unit 2 A-E and Unit 3 A-C have their own geometry (below).

## Unit 2 (girders begin on the CL BRG of Pier 6A; bearing points from the pier 6A cap plan, see pier-6a.csx)
| Girder | Geometry |
|---|---|
| A | tangent from the 6A bearing to P.C. 7597+72.96, 84.45' LT; R 1749'-6 (left) to P.C. 7601+39.20, 75.58' LT; R 2324'-5 (= CL R - 34.5', concentric) to the end |
| B | tangent to P.C. 7598+07.31, 76.58' LT; R 1480'-6 to the end of the girder (ends in span 10WB, 50'-3 1/2 past 9WB) |
| C | tangent to P.C. 7596+43.14, 75.46' LT (66'-9 1/2 past 7WB); R 8000'-0 to the end (ends in span 9WB, 59'-1 11/16 past 8WB) |
| D, E | straight (no P.C. given). D through 7WB (7'-4 3/8 left of F), E through 6WB (6'-11 3/4 left of F) - layout dims at the 90 deg piers |
Checks of the reading: |P.C.2 - center of R 1749'-6| = 1749.475 ft (plan 1749.5), the R 1749'-6 and R 2324'-5 arcs meet tangentially to 17"
(arc seconds); the girder-A offset at P.C.2 is the concentric -34.50 (plan 75.58 - 41.08).
Plan girder data (horizontal, along the girder; "model - plan" with the new geometry, inches): A 7WB -0.01, 8WB -0.03, 9WB +0.20, 10WB +0.12;
B 7WB +0.02, 8WB -0.08, 9WB +0.21; C 7WB +0.01, 8WB +0.10; D 7WB +0.01. Before: A 8WB -4.4, 9WB +2.0; B 8WB -3.2; C 8WB -2.0.

## Unit 3 (girders begin on Pier 11WB & 19G; A at 73'-1 1/4, B 59'-10 7/8, C 47'-2 5/8 left of G measured normal to the roadway - sheet 112)
| Girder | Geometry |
|---|---|
| A | R 2268'-5 (left) from the 11WB bearing to P.C. 7606+59.36, 89.13' LT (18'-2 1/16 before 13WB); R 1318'-5 1/4 to P.C. 7608+82.10, 81.56' LT; R 2318'-5 1/4 (concentric, 40.5' left of CL) to the abutment |
| B | one arc R 2796'-10 from 11WB through 12WB (ends 60'-3 11/16 past 12WB) |
| C | one arc R 2795'-0 from 11WB through 12WB (ends 53'-2 11/16 past 13WB) |
At 12WB the sheet gives the pier dims A-B 8'-5 1/4 and B-C 8'-7 7/8 (along the pier; the script places B and C from the A bearing). D-J R 2324'-5 ... 2393'-5 concentric.
Model - plan span lengths: A 13WB -0.25", 14WB -0.16"; C 13WB +0.18"; D 13WB -0.01". Spans 12WB (11WB end) read +2.9" to +3.4" on EVERY girder
including the parallel D-J: the 11WB support line of Unit 3 is about 0.25 ft off the plan (Unit 2 end: +0.4 ft, A 11WB +4.9" and F +4.75"),
not a girder-geometry matter (open, with the joint / Pier 11WB items).

## Earlier "x cos(skew)" pier offsets were the error
`girders-i80.csx` / `pier-6a.csx` converted the plan's dimensions along the skewed pier lines to offsets normal to the alignment as
x cos(skew) and forced the bearing to that offset; on the R 719 m curve and with the girders flaring, that placed Unit 2 8WB A/B/C
0.37 / 0.26 / 0.18 ft and Unit 3 11WB B/C 0.32 / 0.15 ft off. The girder radii/P.C.s above give the plan span lengths to <= 0.2".
