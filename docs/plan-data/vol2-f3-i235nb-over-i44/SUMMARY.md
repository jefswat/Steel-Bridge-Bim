# vol2-f3-i235nb-over-i44 - plan data summary (Bridge F3)

Oklahoma DOT, Oklahoma County (Oklahoma City), job piece **09033(11)**, I-235 / I-44 interchange (I-235 Broadway Extension, Phase 2B per the
combined plan set "Vol 2_Phase 2B Bridge Combined.pdf", 300 pages); designer Benham Companies (Tulsa); sheets dated 11/21/2016, "FINAL FIELD MEETING".
**Bridge F3 = I-235 NORTHBOUND OVER I-44** (CL I-235 NB = "CRL I-235 NB"): 2-span continuous **48" welded plate girder bridge,
135'-8 3/8 + 130'-5 1/2 (266'-1 7/8 along CRL), 9 girder lines, 75'-0 clear roadway (78'-0 out to out), 42" F-shaped parapets, a 4-shaft
5'-0 drilled-shaft pier with one long cap, stub abutments on HP12x53 piles, skewed 78 to 82 deg, curved (R = 3700 ft).
Format per `docs/plan-data/EXTRACTION.md`; sheet index in `INDEX.md` and `docs/plan-data/vol2-sheet-index.csv`. "p." = PDF page, "F3-n" = sheet no.
Tags: TEXT / RASTER / DERIVED / UNVERIFIED / UNRESOLVED. This PDF has a text layer on most pages, but fractions are replaced by a "?" glyph, so every
fraction here was read on a raster (110-300 dpi). Ten pages have no text layer at all (p.20, 24, 26, 28, 31, 40-44: image tiles) and were read as rasters.
Standards referenced (listed, not assumed): AASHTO LRFD Bridge Design Specifications 2012 ed. with ANSI/AASHTO/AWS D1.5 (p.11); ODOT Standard
Specifications (sections 506, 724.05, 733.06(B)); ODOT bridge standards FSHP-42 / FSHP-42-2 (F-shaped parapet), HP1-2 (steel pile encasement),
EJ-SK / EJ-DTL (expansion joint). Loading HL-93 or Oklahoma overload truck + 20 psf future wearing surface + 5 psf stay-in-place forms;
LFD operating rating HS-52. Materials: Class AA f'c 4000 psi, Class A 3000 psi, rebar fy 60 ksi, M270 Gr 50W steel, stainless A240 type 316 (bearings).

Data files: `bar-lists.csv` (pier, both abutments, deck/parapet - each weighed against the plan quantity), `bearings.csv`, `girder-data.csv`,
`framing.csv`, `pedestal-elevations.csv`, `dead-load-deflection.csv`, `stations-coordinates.csv`, `top-and-profile-checks.csv`, `quantities.csv`.

## 1. Geometry control

| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Alignment | CRL I-235 NB (stationing of the bridge); CL Ramp 44C passes under at an angle (sta 301+62.22 CL Ramp 44C = 112+84.46 survey I-44) | - | p.11 / F3-1 | RASTER | station equations printed on the plan (Ramps 44G, 44C, 44H, 44A, survey I-44) belong to the under-roads |
| Horizontal curve | R 3700.00, T 1015.72, L 1982.60, D 1 deg 32'54.72", delta 30 deg 42'04.73" LT | ft | p.11 / F3-1 | RASTER | DERIVED R tan(A/2) = 1015.7, R*A = 1982.6 ok. PC / PT stations not printed (U2) |
| Spirals / station equations on CRL | none shown | - | p.11 | RASTER | |
| Coordinates / datum | N / E of the working points (next row); zone / datum not named | ft | p.16 / F3-6 | UNRESOLVED U3 | |
| Working points (front face of backwall at CRL / CL pier at CRL) | abut 1 STA 300+27.82 N 193004.71 E 2113206.89; pier 1 STA 301+63.52 N 193138.86 E 2113227.23; abut 2 STA 302+93.98 N 193268.47 E 2113242.14 | ft | p.16 / F3-6 | TEXT | DERIVED N/E distances 135.69 and 130.46 vs station differences 135.70 and 130.46 ok |
| Benchmarks | BM #08 N 192243.09 E 2113053.52 el 1111.10 (headwall); BM #06 N 193276.13 E 2113178.70 el 1125.73 (conc. retaining wall NE cor. br.) | ft | p.11 | RASTER | |
| Vertical profile | PVRC 288+00.00 el 1120.54; PVI 291+00.00 el 1116.34 (VC 600'); PVT 294+00.00 el 1122.04; PVC 296+40.00 el 1126.60; PVI 301+20.00 el 1135.72 (VC 960'); PVT 306+00.00 el 1126.98; high point STA 301+30.32 el 1131.26; grades -1.400 % / +1.900 % / -1.821 % | ft | p.11 / F3-1 (300 dpi clip) | RASTER | DERIVED: 1135.72 - 480 x 1.900 % = 1126.60 ok; 1135.72 - 480 x 1.821 % = 1126.98 ok |
| Profile at bridge points | begin bridge 300+27.82 el 1131.06; pier 1 301+63.52 el 1131.24; end bridge 302+93.98 el 1130.74 | ft | p.11 | RASTER | DERIVED from the 960' VC: 1131.05 / 1131.24 / 1130.74 ok |
| Cross slope | 4.70 % one plane across the 78'-0 deck (typical section: PGL / CRL I-235 NB at 27'-0 from the left edge; slab rises toward the right-hand shoulder) | - | p.11, p.31 / F3-21 | RASTER | no superelevation table in the pages read - pivot offset UNRESOLVED U4 |
| Approach slabs | begin STA 299+97.81 (30'-0 before begin bridge), end STA 303+36.99 (43'-0 after end bridge) | - | p.11 | RASTER | |
| Clearances | min vert. clr span 1 17.98 ft, span 2 17.81 ft, 21.54 ft (+/-) near abut 2 | ft | p.11 | RASTER | check values |

## 2. Layout

| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Supports | abut 1 (south, expansion), pier 1 (fixed, continuous), abut 2 (north, expansion) | - | p.11, p.37 | RASTER | sealed expansion joint at both abutments, one continuous unit |
| Spans along CRL | 135'-8 3/8, 130'-5 1/2; bridge length 266'-1 7/8 | - | p.11 | RASTER | sum DERIVED 266.1562 ft ok (300 dpi clip of the length string) |
| Bearing to bearing along girder | span 1 134'-0 3/4, span 2 129'-7 13/16, total 263'-8 9/16 | - | p.33-34 | RASTER | sum DERIVED ok; girder lines run square to the skewed bearing lines, not along the CRL |
| Skew | abut 1 78 deg 17'58.44" to tangent; pier 1 80 deg 24'02.9"; abut 2 82 deg 25'16.11" (p.11); framing sheet p.33 prints 78 deg 19'38.07" / 80 deg 24'07.55" / 82 deg 24'31.30" for the same supports; cross frame / girder angle 80 deg 23'16.54" | - | p.11, p.33 | RASTER | the two sheets disagree slightly (U5) |
| Girders | 9 lines G1..G9 (pedestal letters A,B,C,D,E,F,G,H,J), 8 @ 8'-11 3/4 = 71'-10 normal to the girders; 9'-1 5/16 along the bearing lines | - | p.31, p.33, p.17, p.21 | RASTER | 8 x 8'-11 3/4 = 71'-10 ok |
| Deck | 78'-0 out to out; 75'-0 clear roadway = 15'-0 shoulder + 4 lanes @ 12'-0 (27'-0 + 48'-0) + 12'-0 shoulder; 1'-6 parapet base each side | - | p.31 | RASTER | 15 + 48 + 12 = 75 ok; + 2 x 1'-6 = 78 ok |
| Deck edges / overhang | curved with the CRL; overhangs vary 2.03 to 4.61 ft (21 stations each side, `framing.csv`); girders are straight chords per span | ft | p.33 | TEXT | |
| Barrier | 42" F-shaped parapet both sides, 531.60 LF on the bridge (+147.80 LF on approach slabs), plain (no drain openings); std FSHP-42-2 | - | p.11, p.12, p.31 | RASTER | |
| Expansion joint setting | opening 2" at 60 F: 23 F 2 3/8, 35 F 2 1/4, 48 F 2 1/8, 60 F 2, 72 F 1 7/8, 85 F 1 3/4, 97 F 1 5/8, 109 F 1 1/2 | in | p.31 | RASTER | |
| Slab pour | sections 1 (101'-9) / 2 (62'-7 7/8 over the pier) / 1 (101'-9) along the baseline | - | p.33 | RASTER | |

## 3. Superstructure

| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Girder type | welded plate girder, web 3/4 x 48, M270 Gr 50W; span 1 top 1x18 + 1 1/8x24, bottom 1 1/4x24 + 2x24 (details in `girder-data.csv`); field splice 40'-0 each side of the pier | - | p.34, p.35 | RASTER | schedule reconciles: A = D - 40'-0, C = E - 40'-0 |
| Field splices | bolted 7/8" A325, 15/16" holes; welded alternative detailed; plate sizes in `girder-data.csv` | - | p.35 | RASTER | |
| Stiffeners | bearing 3/4x8 1/2x4'-0 (abutments), 1 1/2x11 1/2x4'-0 (pier); cross-frame stiffeners 3/4x8 1/2x4'-0, omitted at the outside face of exterior girders | - | p.34, p.36 | RASTER | |
| Cross frames | brace type 1: C9x15 chords + L4x4x1/2 K diagonals at end frames, L6x6x3/4 chords at intermediate frames; spacing span 1 18'-6 3/4, 4@19'-6, 3@12'-6; span 2 3@12'-6, 4@18'-3, 19'-1 13/16; 9'-1 5/16 ctr to ctr along the skew | - | p.33, p.36 | RASTER | sums 134'-0 3/4 and 129'-7 13/16 ok |
| Shear connectors | 3 studs 7/8" x 5" per row, zones 10" / 12" / 24" (`girder-data.csv`), none at splices | - | p.34 | RASTER | |
| Bearings | stainless-steel assemblies with 60 durometer elastomeric pad (1/8" A36 laminates): abutments EXP pad 20 x 9 1/2 x 4 1/8, bevel 0.4 % (abut 1) / 0.7 % (abut 2); pier FIX pad 26 x 14 x 4 1/8, bevel 0.2 %; 1 1/2" anchor bolts | in | p.37 / F3-27 | RASTER | `bearings.csv`; 9 fixed + 18 expansion assemblies = quantity sheet |
| Haunch | "H" (top of slab to bottom of bearing assembly at CL bearing): 5'-6 1/16 (abutments), 5'-7 5/8 (pier) as read at 130 dpi; theoretical only, actual haunch set after erection | - | p.31 | UNVERIFIED | U6 |
| Slab | 8" slab, 2 1/2" clear top; A#5 @12, AC #6 @6, E2 #7 @12 over the pier; 1/2" drip bead | - | p.31, p.32 | RASTER | deck bar list reconciles |
| Dead load deflection (steel + slab) | tenth points in `dead-load-deflection.csv` (max 3.852 in span 1 at 0.4; 2.385 in span 2 at 0.6), downward | in | p.34 | TEXT | |
| Camber / blocking / top-of-deck tables | not in this set | - | p.34 | UNRESOLVED U7 | |
| Steel quantity | 896,460 lb superstructure (+ 45 lb abutment) | lb | p.12, p.31 | TEXT | |

## 4. Substructure

| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Pier 1 | cap 85'-4 long on 4 drilled shafts 5'-0 dia at 10'-8 / 21'-4 / 21'-4 / 21'-4 / 10'-8; 4'-0 x 6'-5 crash wall tied to new Bridge F2 | ft | p.26 / F3-16, p.27 / F3-17 | RASTER | 10'-8 x 2 + 21'-4 x 3 = 85'-4 ok |
| Cap | 5'-0 wide, depth varies 4'-6 to 7'-9 (section B-B), stepped pedestals following the 4.70 % slope; WP elev 1125.32 at CRL; cap low end 1123.93 | ft | p.26-27 | RASTER | |
| Pedestals | one per girder line, 5'-0 along the bearing line; elevations in `pedestal-elevations.csv` (pier A 1124.36 to J 1127.72) | ft | p.17, p.21, p.26 | RASTER | steps about 0.42 ft per girder line = 4.70 % x 8.98 |
| Column / shaft | column 4'-0 x 7'-0 (section C-C) above a 5'-0 deep block (el 1098.92 to 1103.92) on the 5'-0 shaft; top of shaft 1099.50, bottom 1073.04, top of rock 1088.04, min 15'-0 into rock; peace shield medallion on the cap end | ft | p.27, p.11 | RASTER | quantity 104.00 LF = 4 x 26.00 vs 26.46 from the elevations (U9) |
| Shaft design | pier total factored reaction 1000 t/shaft, total factored resistance 1360 t; nominal bearing 55.21 tsf (phi 0.70), friction 8.63 tsf (phi 0.45) | - | p.11 | RASTER | |
| Abutments | stub abutment on HP12x53 piles: 17 piles (6 spa @ 4.74 = 28.44 + 10 spa @ 4.74 = 47.40), cap 4'-3 wide, 2'-0 pile embed, 1'-0 projection, backwall 1'-3 wide; wing piles HP10x42; seat elevations abut 1 sections: 1126.74 (A-A, B-B), 1125.46 (C-C), 1124.18 (D-D) | ft | p.16-18, p.21 | RASTER | abutment 2 step elevations not read (U10) |
| Piles | HP12x53 55'/56' (abut 1), 30'/31' (abut 2); HP10x42 60'/63'/58' west, 40'/34'/38' east; quantities HP12x53 1463 LF (944 + 519), HP10x42 293 LF (181 + 112), 39 pile shoes | - | p.11, p.12, p.20, p.24 | RASTER | |

## 5. Reinforcing

Bar lists transcribed from rasters (no fractions in the lengths) and weighed with ASTM bar weights - `bar-lists.csv`:

| list | PDF p. | calc weight lb | plan quantity lb | tag | note |
|---|---|---|---|---|---|
| Abutment 1 south (54 marks) | p.20 / F3-10 | 13,949.5 | 13,950 | RASTER | ok |
| Abutment 2 north (54 marks) | p.24 / F3-14 | 14,387 | 14,390 | RASTER | ok (-3 lb) |
| Superstructure deck + parapet (16 marks) | p.31 / F3-21 | 127,602 | 127,610 | RASTER | ok (-8 lb) |
| Pier 1 (32 epoxy marks, shaft bars excluded) | p.26 / F3-16 | 62,890 (+ 890 pier protection barrier = 63,780) | 64,150 | RASTER | 370 lb (0.6 %) not identified (U11); shaft bars DS1 96 x 32'-1 and DT1 W20 spiral not in that quantity |

Pier protection barrier (p.40), approach slab (p.41-42) and slope wall (p.43-44) bar lists: not transcribed (INDEX "not extracted").

## 6. Misc

| item | value | PDF p. | tag |
|---|---|---|---|
| Aesthetics | 1 1/4" trapezoidal fractured-fin form liner on parapets, abutment wings and piers (F3-35); peace shield medallion on the pier cap end and abutment wings; (SP) graffiti treatment on all exposed concrete (25,551 SF) | p.45, p.27, p.29-30 | RASTER |
| Drip plate | 3/8" plate on the outside of exterior girders at both abutments and the up-grade side of the pier | p.34 | RASTER |
| Drainage | no parapet drain openings; abutment CLSM backfill + 6" perforated 80 LF / non-perforated 100 LF underdrains per abutment | p.20, p.24 | RASTER |
| Approach / slope | approach slabs 646 SY, saw-cut grooving 567.40 SY, 5" slope walls 1351 SY | p.12, p.41-44 | RASTER |

## 7. Checks data

`quantities.csv` (every summed row reconciles; two plan misprints noted), `top-and-profile-checks.csv`, `pedestal-elevations.csv`,
`dead-load-deflection.csv`. Plan misprints: the summary of quantities (p.12) prints the TOTAL column shifted for the last rows (driven HP12x53
total 2.00 instead of 1463; drilled shafts total 1.10 = sealer resin GAL); sealer resin 1.10 GAL otherwise appears only on p.31.

## UNRESOLVED

| id | item | PDF p. | reason |
|---|---|---|---|
| U2 | PC / PT stations of the CRL I-235 NB curve (R 3700) | p.11 | not printed; the "geometric data sheet, I-235 NB, curve 6" it refers to is not in this set |
| U3 | coordinate system / datum of the N/E values | p.11, p.16 | no zone or datum text on the pages read |
| U4 | pivot / superelevation of the 4.70 % one-plane deck vs CRL | p.11, p.31 | no superelevation table in this set |
| U5 | skew angles differ between p.11 and p.33 | p.11, p.33 | two values printed for each support; not reconciled |
| U6 | haunch height H at abutments and pier | p.31 | read at 130 dpi only |
| U7 | camber / blocking / top-of-deck tables | p.34 | not provided |
| U9 | drilled shaft length 26.00 LF (quantity) vs 26.46 ft (elevations) | p.11, p.12 | does not reconcile |
| U10 | abutment 2 seat elevation steps | p.22-23 | sheet not read in detail |
| U11 | 370 lb pier epoxy rebar difference | p.26 | pedestal / peace shield bars not summed |

## Model mapping

| section | model inputs | notes / gaps |
|---|---|---|
| 1 Geometry | `PiDefinition` (one PI R 3700 LT delta 30 deg 42'04.73"), `VerticalProfile` PVIs 291+00 (VC 600) and 301+20 (VC 960), `CrossSlopeTable` 4.70 % | GAP: PC / PT (U2) - derive the PI from the working-point N/E (p.16); pivot / datum U3, U4 |
| 2 Layout | `BridgeDefinition.Supports` 3 with the skews above, `GirderLayoutDefinition` 9 steel plate girders 8 @ 8'-11 3/4, curved deck 78'-0, F-shaped parapet both sides | GAP: varying overhang table on a chord-girder deck (`framing.csv`) |
| 3 Superstructure | plate girder plates (`girder-data.csv`), K-type cross frames at the listed spacing, field splice 40'-0 each side of the pier, bearings (`bearings.csv`) with bevel | GAP: stainless assembly with anchor plate + contact angle, stud pattern, drip plate |
| 4 Substructure | `SubstructureDefinition` pier: cap 85'-4 x 5'-0 with 9 pedestals, 4 drilled shafts 5'-0; stub abutments on HP12x53 piles | GAP: crash wall to F2, peace shield, wingwalls, slope walls, approach slabs (program does not model them) |
| 5 Reinforcing | `bar-lists.csv` for checks / `PlanBarGroup` | |
| 6 Misc | 42" F parapet, form liner, graffiti treatment | GAP: form liner / medallion geometry |
| 7 Checks | `quantities.csv`; no top-of-slab / camber table in the set | |
