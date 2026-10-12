# bonner-b2500 - plan data summary (PARTIAL: general layout only)

NCDOT TIP B-2500 (Phase I), plans by HDR for PCL Civil Constructors, "Revision 3" combined unofficial set (index rev 3: 09/25/17, copy 12-1-17).
Replaces Bridge No. 11 (Bonner Bridge). NC 12 over Oregon Inlet, Dare County, NC. F.A. project BRNHF-0012(48)/(36). Total bridge length 14,799'-6 1/8" (STA 28+68.00 to 176+67.51 along -L-).
Format per docs/plan-data/EXTRACTION.md. "p." = PDF page. All 633 sheets are current (see INDEX.md); no deleted/superseded sheet exists in this PDF.

SCOPE NOTE: 82 spans in 15 units: precast prestressed concrete Florida I-Beam (FIB 45" and 96") girders on precast bent caps / columns / 54" cylinder piles / 36" square pile footings,
plus a 3550'-0" post-tensioned precast SEGMENTAL CONCRETE BOX girder navigation unit. There is NO steel superstructure.
This pass extracted sections 1-2 (geometry + layout from all general-drawing sheets p3-16). Sections 3-7 (girder/strand/camber, bearings, bent/column/cap/footing details, bar lists, quantities, segment/PT data)
were NOT extracted (about 600 detail sheets): see UNRESOLVED.

## 1. Geometry control
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Alignment | -L- (NC 12): C1 (LT), tangent N 80 29'29.1" W, C2 (RT), tangent N 45 04'44.5" W, C3 (RT), tangent N 0 44'23.4" E, C4 (RT) | | p3-16 | TEXT | all bents radial; stations along -L- |
| Curve 1 | PI 27+27.13, delta 42 32'29.0" LT, R 2,347.00, T 913.67, L 1,742.62, D 2 26'28.4" | ft | p3 / S0-02 | TEXT | PT 35+56.07 per p3 label |
| Curve 2 | PI 62+28.30, delta 35 24'44.6" RT, R 2,815.00, T 898.72, L 1,739.85, D 2 02'07.3"; PC 53+29.58, PT 70+69.43 | ft | p5, p6 | TEXT | |
| Curve 3 | PI 128+03.63, delta 45 49'07.9" RT, R 6,050.00, T 2,556.79, L 4,838.13, D 0 56'49.3"; PC 102+46.84, PT 150+84.96 | ft | p10-14 | TEXT | |
| Curve 4 | PI 173+87.60, delta 7 38'59.6" RT, R 5,000.00, T 334.29, L 667.58, D 1 08'45.3"; PC 170+53.32, PT 177+20.90 | ft | p16 | TEXT | |
| Spirals / station equations | none shown | | p3-16 | TEXT | |
| Datum | all elevations NAVD88; MHW el 0.4, MLW el -0.5 | ft | p3-16 | TEXT | |
| Grade points | EB1 fill face STA 28+68.00 el 12.87; EB2 fill face STA 176+67.51 el 16.39 | ft | p3, p16 | TEXT | |
| Vertical curves (PVI sta / el / VC / G1 to G2) | 27+18.00 / 6.57 / 490 / +0.4117 to +3.9830 %; 33+65.00 / 32.34 / 602 / +3.9830 to 0; 47+73.00 / 32.34 / 1564 / 0 to +4.9966; 59+35.00 / 90.40 / 760 / +4.9966 to 0; 91+45.00 / 90.40 / 760 / 0 to -4.9991; 103+02.00 / 32.56 / 1554 / -4.9991 to 0; 172+75.00 / 32.56 / 650 / 0 to -4.1620; 178+80.00 / 7.38 / 560 / -4.1620 to -0.0637 | | p3-5, 8, 10, 16 | TEXT | 70'-0" min vertical clearance spans 21-27 |
| Cross slope | 28+00.00-35+11.07 .050; 35+11.07-35+78.57 varies; 35+78.57-51+99.00 .025; 51+99.00-53+81.00 varies; 53+81.00-70+28.93 .045; 70+28.93-70+82.93 varies; 70+82.93-102+46.34 .025; 102+46.34-102+59.84 varies; 102+59.84-150+71.46 .030; 150+71.46-150+84.96 varies; 150+84.96-170+53.32 .025; 170+53.32-170+66.82 varies; 170+66.82-176+39.90 .030; 176+39.90-177+20.90 varies; 177+20.90 on 0.000 | ft/ft | p4 / S0-03 | RASTER (110 dpi clip) | slope DIRECTION (arrows) not resolved; "see roadway plans" for transitions |
| Coordinates (N/E) | not on the general drawings; long chord layouts S0-20..S0-33 (p21-34) not read | | p21-34 | UNRESOLVED | |
| Roadway | 40'-0" clear roadway; left-turn lane transition widens clear roadway 52'-0 to 40'-0 near EB1 | ft | p3 | TEXT | |
| Reference station | Bridge ID STA 102+67.76 -L- | | all sheets | TEXT | |

## 2. Layout
Per-bent and per-span tables: bents.csv (station, foundation designation, fixity, top of footing), spans.csv (length along -L-, derived from stations).
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Supports | EB1 (STA 28+68.00, integral, fixed, 20" sq PS piles), bents 1-81, EB2 (STA 176+67.51, integral) | | p3-16 | TEXT | span n = bent n-1 to n (EB1 = bent 0) |
| Span lengths along -L- | 85 (spans 1-2), 157 (3-6), 167 (7-13), 182 (14-18), 200 (19, 29), 350 (20-28), 160'-10" (30-80), 85.01 (81), 85.00 (82) | ft | p3-16 | TEXT / DERIVED | sum = 14,799.51 = 14,799'-6 1/8" ok; span 81 plan label UNVERIFIED Re-checked 2026-10-12 (p9, p30-32): the general drawing p9 prints 160'-10" along -L-; the 160'-10 1/16", 160'-7 1/16" and 803'-2 1/16" on p30-32 are LONG CHORD LAYOUT dimensions along the unit / span chords (labelled CHORD / ALONG CHORD), not lengths along -L- - no change. |
| Units (continuous for live load) | 1: spans 1-2 (170'); 2: 3-6 (628'); 3: 7-13 (1169'); 4: 14-18 (910'); 5: 19-29 (3550', segmental box); 6: 30-35 (965'); 7: 36-40 (804'-2"); 8: 41-45 (804'-2"); 9: 46-51, 10: 52-57, 11: 58-63, 12: 64-69, 13: 70-75 (965' each); 14: 76-80 (804'-2"); 15: 81-82 (170') | ft | p3-16 | RASTER + DERIVED | unit ends inferred from labels and stations (965 = 6 x 160'-10") |
| Skew | all bents radial to -L- | | p3 | TEXT | curved alignment; spans labelled ARC / TAN-ARC |
| Foundations | P-3 (3 x 54" cylinder piles), P-4, P-4W (4); footings on 36" sq PS piles: S-10, S-12, S-16, S-16W, C-18, C-20, C-24, C-24W, C-30, N-6, N-8, N-12, N-14 (number = piles); batter 2:12 | | p3 foundation table | TEXT | designation per bent in bents.csv (RASTER) |
| Girders | 45" and 96" deep P/C P/S Florida I-beams; navigation unit variable-depth P/C P/T segmental box | | p3 superstructure table | TEXT | girder counts per span not extracted (labels: 4-96" FIB units 6-14, 5-96" unit 3, 6-96" unit 4, 5-45" unit 1, 4-FIB unit 2) |
| Navigation | 2450'-0" navigation zone STA 62+95.00 to 87+45.00 | | p6, p8 | TEXT | |

## 3-7. Superstructure, substructure, reinforcing, misc, checks data
NOT EXTRACTED. Where to look: FIB data S1-26..S1-37, S2-41..S2-52 (S4/S5 equivalents); bearings S1-38..40, S2-53..57, S3-69..72; parapets/rail S1-43..51 etc.;
typical sections S1-06..14, S2-10..19, S3-14; precast bent caps/columns S1-59..66, S2-89..104, S3-106..134; bills of material S1-53/54, S2-71/72; segmental data and PT S3-15..66.

## UNRESOLVED
| item | pdf page | reason |
|---|---|---|
| Girder strand/camber/haunch tables, girder lengths | p59-142, 143-248, 393-490, 491-633 | not read (about 100 repeating FIB sheets); outside what this pass completed |
| Bearing data, top of bearing, seat elevations | S1-38..40 etc. | not read |
| Bent, cap, column, footing, pile data and bar lists | S1-56..73, S2-74..104, S3-86..136, S4/S5 | not read |
| Quantities / bills of material | S1-53/54, S2-71/72 etc. | not read |
| Segmental box unit 5 (segments, PT, erection) | p249-392 | extracted in segmental-unit5/SUMMARY.md (open items listed there); SBM has no feature for it |
| Coordinates N/E | p21-34 | not read |
| Cross slope direction | p4 | arrows not decipherable on the 110 dpi clip. Unit 5 (bents 18-29): RESOLVED from S3-19 p267 (section 'looking upstation': slope down to the RIGHT) - see segmental-unit5/SEGMENTAL-DESIGN.md |
| Fixity / foundation designation / top of footing per bent | p3-16 | read on 50-72 dpi strips, not confirmed at 300 dpi; pile bents have no footing elevation |
| Bent 23 station | p7 | printed "73+4500" (typo); taken as 73+45.00 (equals 350' spacing) |

## Model mapping
| section | SBM input | status |
|---|---|---|
| 1 alignment | PI table (C1-C4 + tangents), VerticalProfile (8 PVIs), CrossSlopeTable | existing inputs fit; superelevation transitions and direction missing; no coordinates |
| 2 layout | supports at W.P. stations, skew 0, 82 spans, 15 units | fits (83 supports) |
| 3 superstructure | steel plate girder framing | GAP: FIB prestressed girders (needs FIB 45/96 shapes) and the post-tensioned precast segmental box unit (no feature) |
| 4 substructure | precast bent caps, columns, 54" cylinder piles, 36" sq pile footings | GAP / partial |
| 5-7 | bar lists, quantities | not extracted |
