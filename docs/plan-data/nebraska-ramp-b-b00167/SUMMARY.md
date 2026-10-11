# nebraska-ramp-b-b00167 - plan data summary

Iowa DOT **Design No. 1617**, File 30377, project IM-NHS-029-6(206)147--03-97 (tied roadway project IM-NHS-029-7(49)149--03-97),
Woodbury County, Sioux City; HDR Omaha (structural seal A. T. Nelson, sheets 1-66; soils M. D. Cushman, SPS.1-4); October 2015, letting 12-15-2015.
**NEBRASKA ST. RAMP B over Virginia St. and Virginia St. Ramp D: 570'-0 x 26'-0 continuous welded plate girder bridge,
180'-0, 203'-0, 187'-0 spans, "design for variable skew"**, weathering steel (A709 50W; HPS 70W at the pier 2 cap).
Format per `docs/plan-data/EXTRACTION.md`; sheet index `INDEX.md` ("p." = PDF page = sheet number, "DS" = design sheet = p - 1).
Tags: TEXT / RASTER / DERIVED / UNVERIFIED / UNRESOLVED. Text-layer fractions are drawn glyphs; every fraction below was read on a raster clip (200-450 dpi).

**Relation to I-80 WB / Ramp G (B00168 Design 116 / B00169 Design 316): none.** The PDF file name says "Design 116", but every title block
says DESIGN NO. 1617 / FILE NO. 30377 / Woodbury County. I-80 WB is Design 116 / File 30169 in Council Bluffs (Pottawattamie County).
No shared alignment, piers, units, datum or georeference. The companion set in this repo is **`woodbury-i29-floyd`** (Design 817, File 30377,
same I-29 Sioux City reconstruction, same tied roadway project (49), benchmark series BM 634 there / BM 635 here; p.6 also shows the adjacent
Virginia St. Ramp D PPCB bridge Design 1017, project (203)). Shared with the I-80 / Ramp G pipeline is only the *kind* of structure (Iowa
welded plate girders, Iowa standards 4305 / 1021W / 1026, disc bearings, weathering steel, stub abutments, Iowa camber / blocking / field haunch
tables in the same 67-point format), so the scripts and checks can be reused, not the data.

Data files: `girder-data.csv`, `field-splices.csv`, `camber-deflection.csv`, `blocking.csv`, `haunch-elevations.csv`,
`top-of-deck-elevations.csv`, `bearings.csv`, `bar-lists.csv`, `quantities.csv`, `fabrication-notes.csv`.

## 1. Geometry control

| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Alignment | CL Nebraska St. Ramp B = P.G.L. | - | p.6, p.24 | RASTER | |
| Curve 1 | PI 2472+49.63, Delta 1 deg 07'20.70" LT, T 24.63, L 49.26, E 0.12, R 2514.53, PC 2472+25.00, PT 2472+74.26 | ft | p.6 / DS 5 | RASTER | DERIVED T/L/E reproduce. CL S abut brg lies ON curve 1, 6.70 ft before PT |
| Tangent | PT 2472+74.26 to PC 2476+53.67 | sta | p.6 | RASTER | pier 1 on the tangent; CL pier 2 3.11 ft before PC2 |
| Curve 2 | PI 2479+06.35, Delta 19 deg 07'26.97" LT, T 252.68, L 500.67, E 21.13, R 1500.00, PC 2476+53.67, PT 2481+54.34 | ft | p.6 / DS 5 | RASTER | DERIVED T/L/E reproduce; span 3 on curve 2 |
| Spirals / station equations | none shown | - | p.6-8 | TEXT | |
| Virginia St. Ramp D (under) | PI 4475+28.11, Delta 7 deg 58'35.78" LT, T 149.60, L 298.72, E 5.21, R 2145.73, PC 4473+78.50, PT 4476+77.23; profile PVI 4476+40.00 el 1091.96, VC 330', G1 -1.0000 %, G2 +2.1890 %; crossing STA 2476+38.68 CL Ramp B = STA 4476+17.59 CL Ramp D | - | p.6-7 | RASTER | |
| Virginia St. (under) | PVI 201+50.00 el 1093.36, VC 100', G1 -1.0000 %, G2 +1.0000 %; crossing STA 2473+98.82 CL Ramp B = STA 202+43.20 CL Virginia St. | - | p.6-7 | RASTER | |
| Vertical profile | PVI 2473+62.45 el 1125.25, VC 274.90', G1 -0.2855 %, G2 -1.2973 %; PVI 2479+00.00 el 1118.28, VC 200', G3 -2.6315 % | - | p.6 | RASTER | DERIVED 1125.25 - 537.55 x 1.2973 % = 1118.28 ok |
| Grade at CL bearings | S abut 1125.49, pier 1 1124.10, pier 2 1121.52, N abut 1119.04 | ft | p.6-7, p.47 | TEXT | = CL points 1 / 23 / 45 / 67 of the top of deck table |
| Superelevation | 5 % at 2472+27.46, 62.40' transition, 3 % at 2472+89.86, constant 3 %; 3 % at 2476+39.57, 56.40' transition, 5 % at 2476+95.97, constant 5 % | - | p.6-7 | RASTER | pivot CL/PGL; deck rises to the RIGHT (girder D side) everywhere per top-of-deck table (pt 1: left edge 1124.75, CL 1125.49, right edge 1125.77) |
| Datum / coordinates | NO N/E or state-plane data in the set; only LATITUDE 42.488579, LONGITUDE -96.398648 (location block, point not named), FHWA 700325, T-89N R-47W Sec. 33 | - | p.6 | UNRESOLVED U1 | the CBIS georeference (`geolocate-cbis.csx`, UTM 15N, Council Bluffs) does NOT apply; the lat/long falls in UTM 14N (EPSG:26914) |
| Benchmark | BM 635, STA 474+73.57, 197.84' LT, "X" in NE corner concrete intake N side of Larsen Park Rd. N of Argosy Casino, elev 1094.91 | ft | p.6 | RASTER | station on another (Larsen Park / I-29) stationing |
| Min. vertical clearance A | OH STA 2473+50.24 18.50' LT el 1124.45; UP STA 202+18.31 46.00' RT el 1094.81; depth of superstructure 7.38'; min 22.26' | - | p.6 | RASTER | check value |
| Min. vertical clearance B | OH STA 2477+83.78 18.50' LT el 1118.86; UP STA 4477+62.27 14.00' RT el 1094.45; depth 7.42'; min 16.99' | - | p.7 | RASTER | check value |
| Berm table (S abut) | A1 2473+23.43 21.58' LT 1095.79; A2 +25.82 0.00' 1095.87; A3 +26.66 7.58' RT 1095.87; B1-B3 2472+73.06 21.58' LT / 0.00 / 7.58' RT, 1114.35 | - | p.6 | RASTER | N abut: none |

### Staking (p.8 / DS 7, RASTER) - dimensions along the "chord line between abutments"
| support | CL point | skew to chord | DERIVED check (local frame: x along the tangent PT1-PC2) |
|---|---|---|---|
| CL S abut brg | STA 2472+67.56 on CL | 1 deg 17'03" | = 0 deg 09'09" to the tangent -> framing plan 89 deg 50'51" ok |
| CL pier 1 | STA 2474+47.17, 3.56' LT (where CL pier 1 crosses the chord; CL station 2474+47.56) | 5 deg 11'53" | chord angle to tangent 1 deg 07'53" (DERIVED 1.1315 deg); pier 1 to girders 5 deg 11'53" + 1 deg 07'53" = 6 deg 19'46" (framing plan) ok; chord offset DERIVED 3.55-3.56 ok |
| CL pier 2 | STA 2476+50.56, 7.57' LT | 1 deg 07'53" | = square to the tangent (bearing diagram 90 deg 00'00"); chord offset DERIVED 7.57 ok |
| CL N abut brg | STA 2478+37.56 on CL | 5 deg 53'33" | = RADIAL to curve 2 (DERIVED, 0.0001 deg) |
| chord spans | 179'-7 11/16, 203'-5 3/16, 186'-6 15/16 (sum 569'-7 13/16) | | DERIVED chord length 569.651 ft = 569'-7 13/16 ok |
| pier 1 footings | 17'-0 x 11'-0; CL footings 15'-1 9/16 LT and 8'-2 7/16 RT of the chord along CL pier | | |
| pier 2 footings | 13'-6 x 13'-6, rotated 15 deg 00'00" (TYP.); CL footings 22'-0 15/16 LT and 24'-11 1/16 RT of the chord | | = 47'-0 column to column (p.12) ok |
| abutments | 29'-2 out to out (21'-7 LT / 7'-7 RT of CL), 1'-7 gutter offsets, seat 10'-7 1/2, 3'-2 / 3'-1, 3'-6 | | |

## 2. Layout

| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Supports | S abut (stub, expansion GE), pier 1 (concrete 2-column pier with cap, FIXED FX), pier 2 (2 columns + integral steel pier cap, expansion GE), N abut (stub, GE) | - | p.26, p.42 | RASTER | assumed point of zero movement = CL pier 1 |
| Spans along CL | 180'-0, 203'-0, 187'-0; 570'-0 CL-CL abut brgs | - | p.6-7, p.27 | RASTER | |
| Units / joints | ONE continuous unit; steel extrusion joint with neoprene gland at both abutments (55.3 LF) | - | p.2, p.48-49 | TEXT | |
| Deck length | 576'-1 face to face of paving notches; 3'-0 1/2 CL abut brg to notch, each end, along CL | - | p.6-7 | RASTER | DERIVED 570'-0 + 2 x 3'-0 1/2 = 576'-1 ok; GN table 576.1 LF |
| Deck width | 29'-2 out to out = 1'-7 + 26'-0 roadway + 1'-7; 20'-0 left / 6'-0 right of CL/PGL | - | p.24-25, p.4 | RASTER | GN table 29.2 LF, 16,802 SF |
| Girders | 4 welded plate girders A-D (A = left; exterior face of A painted S abut - FS2) | - | p.24, p.27 | RASTER | |
| Girder spacing | 3 @ 7'-8 = 23'-0, overhangs 3'-1; CL ramp 3'-2 right of C, 4'-6 left of D | - | p.24, p.27 | RASTER | at S abut 3'-1 7/8 / 4'-6 1/8 (girders tangent, CL on curve 1) |
| Girder plan geometry | straight from S abut to PC2 ("detailed tangent between CL S abut and P.T."); curved PC2 - N abut: R A 1481'-6, B 1489'-2, C 1496'-10, D 1504'-6 | - | p.27 / DS 26 | RASTER | DERIVED R = 1500 - offset ok |
| Girder span lengths | `girder-data.csv` | - | p.27 | RASTER | DERIVED: span 1 = 180 + off x (tan 6.3294 + tan 0.1528 deg), span 2 = 203 + off x tan 6.3294 deg, span 3 = 3.1094 + R_g x 183.89 / 1500 -> all 12 within 1/32" |
| Barrier | Iowa aesthetic barrier both sides, 1'-7 base, CIP HPC (no slipform), 1163.9 LF bid, bump-outs at piers | - | p.2, p.24, p.54-56 | TEXT | deck under rail level + rail vertical where the deck slopes toward the rail (p.24) |

## 3. Superstructure

| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Web | PL 9/16 x 76 throughout | in | p.28 / DS 27 | RASTER | |
| Top flange | S abut-FS1 1x18; FS1 to 20'-0 before CL pier 1: 1x22; 20'-0 + 19'-0 about pier 1: 2x22; to FS2: 1 1/8x22; FS2-FS3: 7/8x16; FS3 to 23'-3 before CL pier 2: 1 1/8x24; 23'-3 + 20'-6 about pier 2: 2x24; to FS4: 1x24; FS4-N abut: 1x24 | in | p.28 | RASTER | one typical elevation for all 4 girders |
| Bottom flange | 1x22; 1 1/8x22; 2x22; 1 1/4x22; 1x20; 1 1/8x24; 2 1/4x24; 1 1/8x24; 1 1/2x26 (same breaks) | in | p.28 | RASTER | |
| Girder ends | 1'-0 beyond CL abut brg | - | p.28 | RASTER | |
| Field splices | FS1 51'-0 before CL pier 1; FS2 54'-6 after; FS3 59'-0 before CL pier 2; FS4 PC2 + 53'-6 11/16 (A) .. 54'-4 11/16 (D) | - | p.27-28 | RASTER | `field-splices.csv`; DERIVED splice plate lengths and web plate heights from bolt patterns ok |
| Depth / haunch check | 1 + 76 + 1 = 78" at abutments; top of bearing + girder depth + (3 1/2 nominal haunch - top flange) = girder-line haunch elevation: S abut A 1117.43 + 6.50 + 0.21 = 1124.14; pier 1 A 1116.11 + 6.667 + 0.125 = 1122.90; N abut A 1110.70 + 6.542 + 0.208 = 1117.45 | ft | p.38, p.42 | DERIVED | all match the p.38 table |
| Cross frames | abutment: W24x68 top strut (7/8 x 5 studs, 7 eq spa) + L5x5x1/2 diagonals, 1/2 gussets; pier 1 and intermediate: L5x5x3/8 top chord, L5x5x1/2 V diagonals and bottom chord, 5/8 gussets; all 90 deg to the girders | - | p.33 / DS 32 | RASTER | |
| Cross frame spacing | along A: 22'-5 7/16, 6 @ 22'-6, 20'-5 3/8 / 23'-0 5/8, 7 @ 22'-9, 22'-9 / 22'-8 15/16, 7 @ 23'-1 11/16; along D: 22'-6 1/8, 6 @ 22'-6, 23'-0 / 20'-6, 7 @ 22'-9, 22'-9 / 23'-0 5/8, 7 @ 23'-6 | - | p.27 | RASTER | DERIVED sums = girder span lengths ok; pier 1 frame along CL pier; pier 2 none (girders frame into the cap) |
| Stiffeners | bearing + jacking: S abut PL 1x10 1/2, pier 1 PL 1x10 1/2, N abut PL 1 1/8x12 1/2 (jacking stiffener 1'-5 from CL brg at abutments, 3'-3 each side at pier 1); intermediate PL 7/16x6 1/2; cross frame connection PL 1/2x7 1/2; first stiffener 9'-6 from abut brg | in | p.27, p.32 | RASTER | |
| Shear studs | 7/8 x 5, 3 per row (1 1/2, 2 eq spa, 1 1/2); spacing groups 24 eq spa = 19'-0 at each abutment end, 90 / 84 / 73 / 40 eq spa, 8'-6 + 8'-6 at pier 2 (cap studs p.29), 39 / 95 eq spa; 1'-9 each side of field splices | - | p.28, p.32 | RASTER | |
| Deck | 8" slab incl. 1/2" IWS; 9" at overhang edge; 3 1/2" nominal haunch (11 1/2" top of deck to top of web); top bars 2 1/2" clear, bottom 1" clear; deck area 20.50 SF / ft (excl. haunch) | - | p.24 / DS 23 | RASTER | std 4305 modified |
| Camber + deflections | `camber-deflection.csv`: 4 lines x 67 points; EVERY column carries a circled number (1 S abut, 17 FS1, 23 pier 1, 29 FS2, 39 FS3, 45 pier 2, 51 FS4, 67 N abut), 27/27/13 values per row = headers: no unnumbered "midpoint of field section" column | in | p.36-37 / DS 35-36 | TEXT | camber from a chord between field splices; web cut for DL deflection + VC. Pier 2 deflections are NOT zero (deck 0.17-0.25 in, steel -0.07..0.10): the girders bear on the deflecting cap |
| Blocking | `blocking.csv` (points A-H at supports and field splices) | ft | p.36 | RASTER | |
| Field haunch | `haunch-elevations.csv`: girder-line haunch elevations (TEXT), cross-slope adjustment and allowable field haunch ranges (RASTER) | - | p.38-39 / DS 37-38 | TEXT/RASTER | anticipated-deflection fraction rows of the misc table not transcribed (duplicate of the p.37 decimals; the rounding differs by up to 1/16, e.g. D pt 2 0.78 vs 13/16) |
| Top of deck | `top-of-deck-elevations.csv`: 9 lines x 67 points | ft | p.47 / DS 46 | TEXT | girder line - 8" = haunch elevation at the bearings (A pt 1 1124.81 - 0.67 = 1124.14) DERIVED ok |
| Bearings | disc, 14: S abut 4 GE, pier 1 4 FX, pier 2 2 GE (one per column under the cap), N abut 4 GE; `bearings.csv` | - | p.40-42 / DS 39-41 | RASTER | height 9" (pier 2: 14") incl. 1/8 preformed pad; seats DERIVED = TOB - height: S abut 1116.68 / .97 / 1117.25 / .54 (calc .53), pier 1 1115.36 / .58 / .80 / 1116.02, pier 2 1110.39 (calc .38) / 1111.98, N abut 1109.95 / 1110.34 / .72 / 1111.10 - all within 0.01 |
| Bearing orientation | thermal movement lines from CL pier 1: S abut 89 deg 51'01", pier 2 90 deg 00'00", N abut 84 deg 37'26" | - | p.42 | RASTER | DERIVED S abut 89 deg 51'01" ok, pier 2 ok, N abut 84 deg 37'52" (U9) |
| Settings | 10 / 50 / 90 F: S abut joint D 3 1/16 / 2 1/2 / 1 15/16; S abut sole -9/16 / 0 / 9/16; pier 1 0; pier 2 -5/8 / 0 / 5/8; N abut sole -1 1/4 / 0 / 1 1/4; N abut joint D 4 1/4 / 3 / 1 3/4 | in | p.26 / DS 25 | RASTER | temperature of structure |

## 3a. Integral steel pier cap (pier 2) - p.29-31 / DS 28-30

| item | value | tag | note |
|---|---|---|---|
| Type | TWIN I-GIRDER steel cap, transverse to the bridge, 47'-0 between CL bearings on column 1 and column 2, 1'-6 beyond each bearing (50'-0 overall); the 4 longitudinal girders frame INTO the cap (end-plate bolted connections) | RASTER | p.29 elevation, p.30 girder splice; p.40 note "TWIN CAP BEAM MEMBERS" |
| Section (each cap girder) | web PL 7/8 x 90 (HPS) = 7'-6 web depth; top flange PL 1 3/4 x 20 (HPS); bottom flange PL 1 3/4 x 20 (HPS); continuous submerged-arc web-flange welds (size not shown, U3) | RASTER | section A-A p.31 |
| Twin arrangement | webs 10 3/16 each side of CL pier cap (20 3/8 apart); flanges meet at CL with a 3/16 (-0, +1/8) gap each side, butyl tape before concrete; cap studs 7/8 x 9 (2, 3 eq spa, 2 3/16 at CL) | RASTER | p.31 |
| Slope / camber | cap sloped 3.39 % (TYP.) (= bearing transverse slope 3.39 %); camber 3/16 (-0,+1/16), 3/8 (-0,+1/8), 7/16 (-0,+3/16), 3/8 (-0,+1/8), 3/16 (-0,+1/16) at 6 spa @ 7'-10 = 47'-0 from the chord between supports; plumb under full DL; bearing stiffeners vertical under full DL | RASTER | DERIVED (1113.15 - 1111.55) / 47.0 = 3.40 % |
| Girders on the cap | from CL brg col 1: 11'-1 13/16 to A, 7'-8, 7'-8, 3'-2 to CL ramp, 4'-6 to D, 12'-10 3/16 to CL brg col 2; cap studs 4 @ 6, 10 spa @ 6 = 5'-0 per bay, 1'-4 / 1'-4 1/2 at girders, 7'-9 5/16 / 9'-5 11/16 at the ends | RASTER | DERIVED sum 47'-0 ok |
| Girder-to-cap connection | 1/2" END PL (HPS) on the girder ends + 1/2" CONNECTOR PL (HPS), bolted to the cap; bottom: PL 2 1/2 x 24 centred on the girder bottom flanges, matching slope, MILL TO BEAR with 5/16 fillets, TIGHT FIT; top: girder top flanges (PL 2x24 / PL 1 3/8x24) spliced over the cap with 2-PL 1 3/8 x 11, tapered filler PL, 2" rad corners, 7/8 x 4 studs; top bolt pattern 21 spa @ 3 = 5'-3 each side, 4 1/2, 5-6-5 over the cap, 1'-8 3/16 + 1'-8 3/16 + 1" + 1" = 3'-6 3/8; flange 2'-0 wide, rows 2-7-3-3-7-2 | RASTER | p.30; fabrication tolerances +/-1/16 and +/-1/64, mill from oversized plate after welding |
| Web bolting at the girder (section E-E) | 11 spa @ 6 = 5'-6 vertical; 2-4-4 / 4-4-2 and 3 1/2 horizontal; 1'-10 + 1'-10; end plate 1 1/2 edge | RASTER | p.30 |
| Handholes | both cap webs, near- and far-side, at 1'-9 vertical, 1'-6 each side of the CL girder; cover PL 1/2 x 10 x 1'-1 (3/4 bolts), backing PL 1/2 x 4 1/2 x 0'-10 resin bonded, ground smooth | RASTER | p.29-30 |
| Stiffeners | PL 1 1/2 x 9 each face of each web: bearing (section B-B) and jacking (C-C, 5'-6 inboard of the CL brg), mill to bear, 5/16 fillets, 4 3/8" no weld at the top, 1" 45 deg clip | RASTER | p.29, p.31 |
| Cover plates | PL 3/4 x 40 3/8 (HPS) top and bottom near the cap ends (over both twin flanges), bolted (views E-E / F-F: 16 spa @ 3 = 4'-0 col 1 / 23 spa @ 3 = 5'-9 col 2, 20 spa @ 3 = 5'-0, 4 spa @ 3 = 1'-0; 19 spa @ 3 = 4'-9 col 1 / 26 spa @ 3 = 6'-6 col 2, 10 spa @ 3 = 2'-6); detail B 4'-5 (col 1) / 6'-2 (col 2) + 5'-6 + 1'-6; detail C 5'-7 13/16 (col 1) / 7'-4 3/16 (col 2) jacking to CL exterior girder + 2'-10 1/2; bottom plate starts 1 1/2" from the sole plate edge (bearing dependent) | RASTER | detail C DERIVED 11'-1 13/16 - 5'-6 = 5'-7 13/16 and 12'-10 3/16 - 5'-6 = 7'-4 3/16 ok; detail B start point U8 |
| Ends | galvanized + painted 1x1 wire mesh screen on L2x2x1/4, stainless 3/8 bolts | RASTER | p.31 |
| Materials | main tension members CVN zone 2 (flange, web, splice, girder end, cover, connector, girder connection diaphragm plates); HPS 70W for girder webs/flanges at the cap, end + connector plates, bearing + jacking stiffeners, cover plates; rest 50W; stainless A193 hardware | RASTER | p.29 |
| Columns / bearings | 2 columns 6'-7 5/8 x 7'-3 5/8 (2 x 3'-3 13/16; 2 x 3'-7 13/16) rotated 15 deg 00'00", 20'-5 (col 1) / 22'-0 (col 2) + 4'-0 footing 13'-6 sq, bottom 1085.64; 16 HP12x84 x 130' per footing; disc GE, disc 28" min, rotation lock-up at 0.02 rad (twin member failure), 4-1 3/4 removable anchor bolts (F1554 Gr 55) | RASTER | p.12-14, p.40-42; DERIVED 1085.64 + 24.42 + 4/12 = 1110.39 ok, col 2 1111.97 vs 1111.98 |
| Coating | exterior and inside ends to the bearing stiffeners FS 20045 brown; interior white | RASTER | p.29 |

## 4. Substructure

| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Pier 1 cap | concrete, 29'-3 5/8 long (18'-1 3/8 + 11'-2 1/4 about the chord), 4'-11 5/8 wide, 5'-0 deep, level top el 1115.03, cap ends 3'-0 15/16 (+) beyond girders A / D along CL pier, 4" end steps | - | p.9 / DS 8 | RASTER | girder spacing along CL pier 7'-8 9/16 = 23'-1 11/16 (DERIVED 92 / cos 6.329 deg = 92.57") |
| Pier 1 pedestals | 5'-0 (2'-6 + 2'-6) along pier, 4'-5 5/8 across (3 from each cap face; the cap end block is 3'-7 5/8 with 8 each side); heights 4 / 6 5/8 / 9 1/4 / 11 7/8; tops 1115.36 / 1115.58 / 1115.80 / 1116.02 | - | p.9 | RASTER | DERIVED 1115.03 + h ok |
| Pier 1 columns | 2 columns 5'-11 5/8 (4'-6 1/2 + 2 x 4 9/16 veneer) along pier, 18'-1 high; CL column 2'-11 13/16 from column face | - | p.9 | RASTER | section dims / layout chain partly read (U7) |
| Pier 1 footings | 17'-0 x 11'-0 x 5'-0, bottom 1086.95; 15 HP12x84 per footing, 150' contract length, batter 1:4, 8f6 uplift anchors | - | p.6, p.9, p.11, p.14 | RASTER/TEXT | DERIVED 1086.95 + 5 + 18.083 + 5 = 1115.03 ok |
| Pier 2 | 2 columns + integral steel cap (3a) | - | p.12-14 | RASTER | |
| Abutments | stub abutments, footing 6'-3 (1'-6 + 1'-8 + 1'-7 + 1'-6) x 3'-10, bottom S 1112.35 / N 1105.62; bottom of backwall S 1116.18 / N 1109.45; S 13 / N 14 HP12x84 (190' / 140'), 3 pile spaces @ 5'-2 1/2 = 15'-7 1/2; wing extensions; EPS backfill | - | p.18-22 | RASTER | DERIVED pile LF 13x190 + 30x150 + 32x130 + 14x140 = 13,090 = item 13 ok |
| Abutment seats | S: 1116.68 (low step, A), 1116.97, 1117.25, 1117.54; N: 1109.95 (low step, A), 1110.34, 1110.72, 1111.10 | ft | p.18, p.20 | RASTER | = TOB - 9" DERIVED |
| Pile design | pier 1 Pu 191 k, 345 t nominal, tension 66 k; pier 2 Pu 200 k, 299 t, tension 62 k; abutments Pu 183 k, 344 t (S) / 281 t (N); phi 0.7 rock end bearing | - | p.14, p.22 | TEXT | |
| Concrete placement | pier 1 139.5, pier 2 130.9, S abut 52.6, N abut 54.8, deck 460.2, rails 159.0 CY | CY | p.14, p.23, p.45, p.56 | TEXT | |

## 5. Reinforcing

`bar-lists.csv` (pier 1, pier 2, south / north abutment, superstructure incl. abutment diaphragms and cap haunch bars, west / east rail).
DERIVED: every row's weight = no x length x unit weight (0 mismatches); totals by coating epoxy 235,830 / non-coated 11,659 / stainless 7,676 lb =
estimated quantity items 5 / 4 / 6 exactly (p.2). Bend diagrams (p.14, p.23, p.45, p.56) not transcribed (G9).

## 6. Misc

Expansion devices: Iowa std 1026 F-shape steel extrusion with neoprene gland, galvanized after welding (p.48-49). Lighting p.50-53 (conduit in the barrier),
drainage p.57-60 (3/8 galvanized steel drains, collection pipe on a 1483.15' radius), subdrain p.61, backfill p.62-63, slope protection p.64-65,
texture / rustication p.66, thin veneer brick and terra cotta panels on the piers p.15-17. Only the notes were extracted (G10).

## 7. Checks data

`quantities.csv` (p.2; structural steel 926,401 lb), `camber-deflection.csv`, `blocking.csv`, `haunch-elevations.csv`, `top-of-deck-elevations.csv`,
`bearings.csv`. Moment & reaction table p.35 not transcribed (G11).

## 8. Fabrication notes

`fabrication-notes.csv`: 74 notes / callouts, each with source page, element, category, verbatim text ([...] marks omitted non-fabrication sentences)
and the model attribute it would map to. Categories: fit-up/assembly 15, weld type/size 11, material 10, surface prep/coating 10, other 8, bolt/hole 7,
NDT 4, mill to bear 4, tolerance 3, camber/heat curving 2.
Notes that are element PROPERTIES in a model:
- mill to bear / tight fit / full contact after bolting (F36, F43, F44, F51, F55): contact-face finish on stiffener ends and end / bearing plates;
- toughness class: CVN (F23), main tension member CVN zone 2 at the cap (F28);
- grade per part: 50W default, HPS 70W at the cap (F01, F03, F29, F30), 36 for drains (F04);
- coating per face / zone: girder A exterior face S abut - FS2 (F12), cap exterior brown / interior white (F41, F42), galvanize + paint (F40), galvanize after welding (F72, F74);
- weld NDT: MT (F19), RT full width in tension / reversal, 50 % in compression (F58) by stress zone (F27, F59);
- assembly: shop assembly of cap + framing girders with holes drilled in the assembled position (F35); tapered fills + hardened beveled washers (F33, F34);
- tolerances: fill plates to 1/16 (F17), end plates +/-1/16 and +/-1/64 (F45), cap camber tolerances (F32);
- camber: girder web cut (F64), cap camber (F32).

## Model mapping

| section | existing input / script to reuse | status |
|---|---|---|
| 1 Alignment, profile, superelevation | `build-woodbury.csx` / `build-i80-wb.csx` pattern (PI table, VCs, cross-slope table) | ready: 2 curves + tangent, 2 VCs, 2 transitions |
| 1 Georeference | `geolocate-cbis.csx` pattern (lat/long anchor -> UTM, rotation) | UNRESOLVED U1 (anchor point and bearing not in the plans; zone 14N, not CBIS 15N) |
| 2 Supports, skews, spans | support lines with skews to the chord (S abut on curve 1, N abut radial); `check-spans-i80.csx`, `check-staking.csx` | ready (DERIVED staking + girder lengths reconcile) |
| 2 Girder layout | 4 girders at 7'-8; straight S abut - PC2 through the end of curve 1, curved after (`GirderLayoutDefinition`, like `girder-geometry-i80.csx`) | ready |
| 3 Plates, splices | `girders-i80.csx`, `field-splices-i80.csx` + `girder-data.csv`, `field-splices.csv` | ready |
| 3 Cross frames | `cross-frames-ramp-g.csx` (as-designed lines; W24x68 abutment frame; V frames) | ready (spacing reconciles); style mapping to the detailer to check |
| 3 Camber / blocking / haunch | `camber-ramp-g.csx`, `check-blocking.csx`, `check-haunch.csx` (same Iowa 67-point format) | ready; pier 2 support deflection != 0 (G4) |
| 3 Bearings | `bearings.csx` (Disc / Generic, FX at pier 1), `check-top-of-bearing.csx` | ready for abutments + pier 1 (`bearings.csv`); pier 2 G2 |
| 3 Deck, top of deck | `check-top-of-slab.csx`, deck edges | ready (`top-of-deck-elevations.csv`) |
| 4 Pier 1, abutments | `piers-ramp-g.csx`, `abutments-ramp-g.csx`, `check-substructure-elevations.csx` | mostly ready; pier 1 sections partly read (U7) |
| 4 Pier 2 + integral steel cap | - | GAP G1-G4 |
| 5 Rebar | `pier-rebar-*.csx`, `deck-rebar-*.csx` + `bar-lists.csv` | lists ready; bend shapes not transcribed (G9) |
| Fabrication notes | `SteelPart.Material` (string), `SteelPart.Role`; IFC pset `SBM_Fabrication` = {Mark, Size, Length, Mass} | GAP G5-G7 |

GAPs:
- **G1 Integral steel pier cap carrying girders.** The girders end at a TRANSVERSE twin I-girder cap (end plates + connector plates bolted to the cap,
  top flanges spliced over the cap, bottom flange bearing on a mill-to-bear PL 2 1/2 x 24). Closest feature: the cross girder (`CrossGirderDetailer`,
  I-80 Units 2/3, girder framing into a continuing girder with L5x5x1/2 angles) - no twin-member section, no end-plate moment connection, no top-flange
  continuity splice over the cap, no cap camber / slope, no cover plates, no mesh-screen ends.
- **G2 Bearings under a steel cap.** `BearingSeat` / `BearingDetailer` put one bearing per girder on a support line; pier 2 has 2 bearings at the cap ends
  over the columns (not on girder lines), transverse slope 3.39 %, 14" high, rotation lock-up.
- **G3 Pier without a concrete cap.** Substructure units are cap + columns + footings; pier 2 is 2 rotated columns with pedestals under the cap bearings
  and separate footings (`SubElementKind` has no steel cap / cap-less bent).
- **G4 Deflecting interior support.** Camber / haunch logic assumes rigid supports; the plan gives non-zero deflections at pt 45 (pier 2). The
  per-point tables still allow comparison, but N - chord(N) between splices must be checked with a moving support.
- **G5 Fabrication attributes.** No part / weld fields for contact finish (mill to bear, tight fit, full contact), toughness (CVN, main tension member, zone 2),
  NDT (RT 100 % / 50 %, MT), per-face / per-zone coating, weld size + process per weld (welds are meshes + IfcFastener WELD without properties), tolerances,
  shop-assembly / drilled-in-assembly flags. `SBM_Fabrication` could carry them as extra properties (e.g. Grade, Toughness, ContactFinish, Coating, NDT,
  WeldSize/Process) or as companion psets; `SteelPart.Material` can already hold "ASTM A709 HPS 70W".
- **G6 Tapered fill plates and beveled washers** (2-direction tapers at the cap connection); fills are prismatic.
- **G7 Web handholes with bolted cover / backing plates** (`PlateBuilder.AddPlanarWithOpenings` exists for cross-girder webs; the plates do not).
- G8 pier 1 sections, G9 bend diagrams, G10 drains / lighting / joints / aesthetics, G11 moment & reaction table: not extracted in this pass.

## UNRESOLVED

| id | item | PDF p. | reason |
|---|---|---|---|
| U1 | Project coordinates / georeference | p.6 | no N/E or state-plane data; lat/long 42.488579 / -96.398648 given without the point it refers to and without an alignment bearing |
| U2 | Weld at the girder end plate / connector plate at the cap (upper groove-type symbol) | p.30 | symbol drawn with no size or type text at 400 dpi; cannot tell CJP from PJP |
| U3 | Cap web-to-flange weld size | p.31 | "CONTINUOUS SUBMERGED ARC WELD" without a size |
| U4 | Identity of the "PL 3/4" plates in the girder-to-cap elevation and section D-D | p.30 | labelled only by thickness; probably the "girder connection diaphragm plates" of the p.29 notes, but no title or size is given |
| U5 | Bearing component thicknesses (sole, masonry, disc) | p.40-42 | vendor designed; only total height 9" / 14" and minimum sole thickness 1" / 1 1/4" given |
| U6 | Bolt path of the girder end-plate connection through the twin cap (one web or both; bolt length / grip) | p.30 | section D-D does not show where the bolts terminate and no grip / length note exists |
| U7 | Pier 1 column section and cap width breakdown, maskwall | p.9-11 | text layer jumbled; sections not rasterized in this pass |
| U8 | Start point of the cap top cover plate dimension 4'-5 / 6'-2 (detail B) | p.31 | the dimension chain runs into the cap end and its origin is not legible |
| U9 | N abutment thermal movement angle 84 deg 37'26" | p.42 | DERIVED from the alignment (line from CL pier 1 x CL to CL N abut brg x CL; abutment radial) gives 84 deg 37'52", 26" off; the other two angles reconcile |
