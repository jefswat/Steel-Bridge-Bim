# Audit - Northaven NHT and ODOT Bridge O tub unit: models vs plans (2026-10-12)

Read-only audit (no change to `src/`, `samples/`, `tests/`, `.sbm`). Models as on `main` (174e542). Plan values were read on fresh rasters
(PyMuPDF, 110-220 dpi) during this audit, then compared with queries of the regenerated models. Column "new" = `N` when the item is not
covered by `check-nht.csx` / `check-arch-nht.csx` / `check-odot-o-tub.csx` (i.e. an independent spot check), `Y` when the check scripts also compare it
(then the plan value was re-read here, not taken from the CSV). Page numbers are PDF pages (sheet numbers are in the NHT SUMMARY.md).

## 0. Existing check scripts (run on copies of the main models, regenerated, nothing saved)

| script | result |
|---|---|
| `check-nht.csx` (approaches, substructure, quantities) | 414 PASS, 4 FAIL. The 4 FAILs are the p.102 arch-pier concrete split (cap+pedestal / column of P10 and P11): see NHT row 15 - the cap+column SUM matches the plan to 0.02 CY, only the plan's split differs. Four arch-bearing warnings (B1-B3: plan pedestal tops 0.13-0.20 ft above WP - 2" - 11.75"). |
| `check-arch-nht.csx` (tied arch) | 501 OK, 213 DIFF (178 of them per-cable fin-plate rows, 16 cable-connector rows). Other DIFFs: chord CL1-CL2 (-9/16"), WP2 elevation (+3/16"), 2 bracing crossing angles (< 1 deg), deck bars A1/B1/D3/S1/S2, 4 bearing-underside rows (B1-B3), 3 tie-splice rows (U5), 3 steel-weight rows (floor system -15 %, bracing -37 %, knuckles +55 %), 2 catalog socket rows. |
| `check-odot-o-tub.csx` | 74 OK, 38 DIFF: 5 span lengths (<= 0.083"), G2 top tension zone over pier 14 (+29.9", plan string does not close), 8 'H' rows (+0.09" systematic), 8 bevel-percent rows (<= 0.06 %), 8 bearing-offset rows (0.07-0.13"), 6 pedestal-seat rows (P13/P14 +0.015..0.031 ft, **P16 +0.169 / +0.159 ft**). |

Environment: free RAM 19 GB, host free space 74 GB; temp outputs deleted.

## 1. Northaven NHT (CSJ 0047-07-228)

Verdict key: OK = within plan precision (0.01 ft / 1/16" / 30" of arc); DIFF = real difference; MISSING = plan feature absent from the model.

| # | item | plan value (PDF p.) | model value | diff | verdict | new |
|---|---|---|---|---|---|---|
| 1 | Curve 103 R / delta / L | R 60.00, 136 34'02" RT, L 143.01 (84) | R 60.00, L 143.013 | +0.003 ft | OK | N |
| 2 | Curve 104 R / L | R 250.00, 19 34'25", L 85.41 (84) | R 250.00, L 85.406 | -0.004 | OK | N |
| 3 | Curve 107 R / delta / L, PT 27+15.09 | R 250, 15 24'05" LT, L 67.20 (87) | R 250.00, L 67.202; PT X/Y +0.006 ft | +0.002 | OK | N |
| 4 | Pier stations P2 / P9 / P12 / P14 | 20+97.18, 23+38.74, 26+86.87, 27+98.87 (84, 87) | 2097.18, 2338.74, 2686.87, 2798.87 | 0.000 | OK | Y |
| 5 | Unit lengths SEJ-SEJ | 122.45 (87), 162.92 (113) | 122.452, 162.922 | +0.002 | OK | Y |
| 6 | Approach deck overall / clear / curb | 15'-8" / 14'-0" / 10" (89, 113) | edges +-7.833 ft = 15.667 ft | 0.000 | OK | N |
| 7 | Slab thickness, X at CL brg | 8 1/2", X = 0'-10 1/2" (113) | 8.50" slab + 2.00" haunch = 10.5" | 0 | OK | N |
| 8 | Z at midspan (top of slab - top of girder) | span 7 11 7/8" / 11 1/4", span 8 10 5/8" / 10 1/2", span 9 1'-1 1/8" (113) | constant 10.5" (no camber / haunch variation) | -1 3/8" to -2 5/8" | DIFF | N |
| 9 | Cross slope | 1.00 % crown each way (89, 90, 113) | 1.00 % about CL NHT | 0 | OK | N |
| 10 | Slab area per unit | 1691 / 1520 / 2554 / 1920 / 2634 SF (91) | 1690.1 / 1519.7 / 2552.4 / 1918.4 / 2632.0 | -0.9..-2.0 SF (0.1 %) | OK | Y |
| 11 | Tx28 LF total; span 9 girder report | 1304.25 LF; G1 C-C pier 78.046, C-C brg 75.199, BFL 76.73, slope -0.0018, angles 85 38'09" / 73 28'39" (91, 112) | 1304.258; 78.047 / 75.200 / 76.732 / -0.002; angle diff <= 5.4" | <= 0.002 ft | OK | Y |
| 12 | Girder offset / spacing at P9, P10 | 4.833 L / 9.667; 5.174 L / 10.253 (112) | same (layouts table) | 0 | OK | N |
| 13 | Pier 10 elevation table EL A..F | 534.779, 533.345, 533.219, 533.400, 534.726, 527.219 (102) | arch seats 534.779 / 534.726, G2 seat 533.344, cap 533.219, G1 533.400, column bottom 527.219 | <= 0.001 | OK | Y |
| 14 | Arch pier cap / column / shaft | cap 24'-0 x 6'-0, 2'-9" ends, 6'-0 at column, column 4'-6 sq, 78" shaft (102) | cap 24.000 x 6.000, column 4.5 x 4.5, shaft 6.5 ft; cap volume 24.96 CY = hand integration of the p.102 section | 0 | OK | N |
| 15 | Arch pier concrete table | P10 cap 26.0 / col 13.2; P11 26.0 / 10.8 CY (102) | P10 25.66 (cap+ped) / 13.55; P11 25.71 / 11.09 | -0.34/+0.35, -0.29/+0.29; sums 39.22 vs 39.2, 36.80 vs 36.8 | DIFF (split only) | Y |
| 16 | Arch pedestal size / angle | 3'-4" x 1'-7", long axis 63 deg to the pier (102) | footprint extents 3.689 x 2.924 = rectangle 3'-4 x 1'-7 at 26.9 deg (computed 3.688 x 2.920) | <= 0.004 ft | OK | N |
| 17 | Arch bearing positions | 9'-10" + 9'-8 1/8" from CL pier / shaft; CL brg 6 7/8" (102) | 19.510 ft apart; 6.875" | 0 | OK | Y |
| 18 | Approach pier P8: EL A/B/C/D, cap 15'-0 x 3'-6, column 3'-0 sq | 532.868 / 532.868 / 530.368 / 512.368 (97) | same; cap 15.000 x 3.5, 1.5" / 2.5" depths | 0 | OK | Y |
| 19 | Approach pier concrete (12 piers) | cap 57.6 CY, column 55.4 CY (91) | cap + pedestals 56.7 (55.04 + 1.7), columns 55.44 | -0.9 CY (-1.6 %) cap | DIFF (minor) | N |
| 20 | Abutment 1 | cap 14.583 x 3'-6 x 2'-6, 2 x 36" shafts 9.000 apart, EL C 521.637, tip 488.153, 6.5 CY (95) | 14.583, shafts +-4.50, cap top 521.637, tip 488.153; 4.73 + 1.67 + 0.08 = 6.48 CY | 0 | OK | N |
| 21 | Abutment 1 shaft top | 519.153 (95 table) | 519.137 | -0.016 ft | DIFF (plan table vs EL C - 2'-6" = 519.137; passes the script's 0.02 tolerance) | Y |
| 22 | Bearing seat elevations | P8 back G1 532.993; P15 back G1 519.625; Abut 16 back G1 516.881 (91) | 532.992; 519.623; 516.879 | -0.001..-0.002 | OK | Y |
| 23 | Drilled shaft LF 36" / 48" / 78" | 106 / 240 / 46 (91) | 105.96 / 240.00 / 46.00 | -0.04 | OK | Y |
| 24 | Arch work points WP1 / WP2 | 535.74 / 532.49 (119) | 535.737 / 532.503 | -0.003 / +0.013 ft (3/16") | DIFF (WP2) | Y |
| 25 | Rib R / chord / rise / inclination | R 126'-0, 201'-0, 50'-0, 5 deg (119) | 126.000, 50.002 rise, 5.0000 deg | 0.002 | OK | Y |
| 26 | Rib splice (RS) | 2 x 14 1/2 x 5/8 ext PL, 4 x 5 5/8 x 5/8 int PL, 2 x 10 x 3/4 curved web PL; 24 bolts per flange + 20 web (127) | 14 1/2 x 27 1/2, 5 5/8 x 27 1/2, 10 x 19 1/2 plates; 68 bolts (24 + 24 + 20 by axis count) | 0 | OK | N |
| 27 | Tie splice | 2 - 9 x 3/4 bent web PL, 6 x 5/8 + 14 x 5/8 flange PL, 1'-6 1/2" long, 18 web + 24 flange bolts (144) | 9 x 3/4 x 18 1/2, 6 x 5/8, 14 x 5/8 x 18 1/2; 42 bolts | 0 | OK | N |
| 28 | Tie / floorbeam sections | tie 7/8 x 11 web + 7/8 x 1'-2" flange; FB web 1", flange 7/8 x 12, 8 7/8"-9 7/8", 4 PT slots 2 1/4 x 5 1/2, 32 studs/face, conn. PL 5/8 x 7 x 1'-0 1/2 (143, 145, 146) | identical; 33 FB | 0 | OK | N |
| 29 | Arch bearing | masonry PL 2 x 1'-5 x 3'-2, elastomer 15 x 15 (2 x 1/4 cover, 6 x 3/8, 0.125 steel = 3 5/8"), H 11.75", sole 3", beveled 3 1/8/3/2 7/8, pin 3", 4 x 1 1/2" anchors (107) | PL 2 x 17 x 38, 15 x 15 x 3 5/8, 3", bevel 3 1/8/3/2 7/8, pin 3", 4 anchors 1.5" x 18" | 0 | OK | N |
| 30 | Longitudinal PT tendons | 4 tendons, pairs 1'-4" apart, pairs 2'-4" either side of CL1-CL2; strand 0.6", 0.217 in2 (155) | local Y +-2.307 / +-3.629 ft (pair spacing 1.322); strand 0.600 / 0.217 | -0.026 ft (5/16") offset, -0.011 ft (1/8") spacing | DIFF | N |
| 31 | Arch deck area / curb | 3,698 SF (91); curb 10" x 4" at 7'-0 each side (90, 152) | 3,726 SF; curb height 0.333 ft | +28 SF (+0.8 %) | DIFF | Y |
| 32 | Arch steel quantities | knuckles 9,700 / bracing 21,600 / floor 61,000 / fin plates 3,800 lb (123, 128, 143, 148) | 15,042 / 13,607 / 51,876 / 3,480 | +55 % / -37 % / -15 % / -8 % | DIFF | Y |
| 33 | Arch deck bars | A1 606 x 166", B1 303 x 218", D3 68 x 89", S1 606 x 39" (154) | 306 x 166", 306 x 218", D3 not modeled, 610 x 30" | see build note (bill vs section conflict) | DIFF | Y |
| 34 | Pedestrian rail RAIL (PED) low / high | 307.93 + 270.95 LF low, 571.28 + 572.97 LF high; 4'-3" / 9'-10" (84, 89, 91) | none (Barrier = None on all 5 units) | all | MISSING | N |
| 35 | Approach curbs | 10" curb each side (89, 113) | none; slab flat to +-7.833 | all | MISSING | N |
| 36 | Approach deck reinforcing | bars A, CS, D, G, H, J, M, OA, P, T, UP; A at 9" max; 5,876 lb for spans 7-9 = 2.3 lb/SF (113) | Marks = 0 on all units | all | MISSING | N |
| 37 | SEJ-B(MOD) 4" joints + BS-EJCP | 107 LF, 1,382 lb, 7 locations (91) | none (deck ends only) | all | MISSING | N |
| 38 | Substructure reinforcing | abutment 1 bar table 1,277 lb (95); piers A/B/S/U/V/Z (97); arch piers 17,130 lb (104) | no `Marks` on any support | all | MISSING | N |

Counts NHT: OK 24, DIFF 9, MISSING 5 (38 rows; rows 15 and 21 are split-only / tolerance-level DIFFs).

### 1.1 Missing from the NHT model, ranked
1. Pedestrian railing (RAIL (PED) low and high, 1,144 LF; high posts 9'-10" on curved posts) - dominant visible feature, pay item 5110.
2. Rebar everywhere: approach deck (about 5,876 lb per the p.113 pattern, 2.3 lb/SF), abutment and approach-pier bar tables, arch-pier bar list (`bar-lists.csv`, 17,130 lb is only a check file), drilled-shaft cages.
3. Curbs / thickened edges on the approach decks (10" curb, 4" high) and the PCP panels.
4. SEJ-B(MOD) joints (107 LF) and BS-EJCP strip seals (1,382 lb).
5. Arch pier traffic barrier T80SS(MOD) (78 LF, p.105-106), mow strips at pier faces (riprap 4" 6.1 CY, p.91 / p.97), galvanized dowels D (p.97).
6. Camber / dead-load deflection (haunch Z 11 7/8" to 1'-1 1/8", A/B deflections, p.113; arch none given).
7. Anti-graffiti coating areas (3,175 SF), PT duct / flat-duct geometry, stud layout on the knuckle end plate (p.126), bracing gusset connections (p.129-142), arch deck D3 edge bars.
8. Approach bearing pad layers ("N" of G-1-N, p.91 note; the model uses the IGEB default 2 3/4").

### 1.2 NHT UNRESOLVED items the plans do answer (or can be closed)
- **U2 (Pier 12 station 26+86.67 vs 26+86.87)**: three places agree on 26+86.87 (p.87 plan label "STA. 26+86.87", the p.97 "CL PIER STA" table, and 27+42.87 - 56.00); only the p.94 schedule differs. The build's choice is correct; the p.94 value is a typo - close the item.
- **U1 / B1-B3 / B7 (arch pedestal vs WP chain)**: p.102 note 1 and p.107 note 8 state "contractor to adjust pedestal elevations based on actual fabricated bearing system" - the p.102 values are provisional by design, so the 0.13-0.20 ft residuals are not a plan error to be resolved from the set; modeling the p.102 tops (as done) is the right call. Close as "by design".
- **U13 / PT**: p.155 Table 1 gives 0.6" strand, 0.217 in2, GUTS 270 ksi, E 28,500 ksi, anchor set 3/8", mu 0.25, wobble 0.0002/ft, single-end stressing, f'ci 4,500 psi, **jacking force 170 kips per tendon**. 170 kips / (0.217 x 270 = 58.6 kips per strand) = 2.9 strands at 100 % GUTS, so 3 strands would be 97 % of GUTS (not permissible) and 4 strands (72.5 % GUTS) is the only consistent count - the model's 4 strands per tendon is supported. The strand label "5/8"" in the model descriptions should read 0.6". The tendon positions on p.155 (1'-4", 2'-4", 2'-4", 1'-4") differ from the model by 5/16" (row 30).
- **U8 (sole-plate weld target)**: p.107 note 7 specifies BOTH welds ("upper sole plate ... welded to arch rib ...; lower sole plate ... welded to masonry plate", 5/16" fillet symbols on the elevation) - the erection-sequence wording is the only conflict; weld size 5/16" can be taken from p.107.
- **U9**: stays (stale "bent 7", "RS6"). Additionally a **documentation error**: SUMMARY.md says the rib W14x132 has its "web in the arch plane". The p.119 / p.127 sections show an "H" orientation (flanges in the arch plane, web perpendicular, bent about the weak axis); the model's web splice plates are perpendicular to the arch plane (plate-normal dot 0.02), so the model follows the drawing and the summary text is wrong.
- **Approach deck steel (U13 partly)**: p.113 gives bar sizes (all #4, OA #5), spacings (A 9" max, T 9" max, CS 9" max, OA between A bars) and the 2.3 lb/SF rule, with per-span weights 2,149 / 902 / 2,825 lb (= 934 / 392 / 1,228 SF x 2.3) - enough to build and check approach deck bars.

## 2. ODOT Bridge O spans 14-16 (steel tub unit, superstructure only by user scope)

| # | item | plan value (PDF p.) | model value | diff | verdict | new |
|---|---|---|---|---|---|---|
| 1 | Curve no. 9 | R 881.47, delta 77 57'36.04" LT (254) | R 881.47 solved through the pier 14 / 16 N,E; pier 15 and abutment points on the arc within 0.01 ft | 0.01 | OK | Y |
| 2 | Pier stations / spans | 302+93.00, 304+54.00, 306+15.00; 139'-3 3/8 / 161'-0 / 161'-0 (254) | 30293.000, 30454.000, 30614.000 (CL brg); pier 13 CL brg 30154.794 (= 301+53.72 + 1'-0 along the skewed line) | 0.000 | OK | Y |
| 3 | Profile elevations at piers | 1135.33 / 1132.14 / 1130.87 / 1129.96 / 1129.62 (254) | 1135.327 / 1132.139 / 1130.871 / 1129.954 / 1129.617 | <= 0.006 ft | OK | Y |
| 4 | Pier 13 working point on 44F vs Bridge N (44E) point | two equations on p.254: 301+09.58 (44E) = I-44 survey 115+56.82; 301+53.72 (44F) = 116+23.16 -> 66.34 ft apart | model 44F point vs p.148 44E point: dN -0.15, dE +66.68 (66.68 ft) | +0.34 ft | OK (explains the "not on arc" note) | N |
| 5 | Pier 13 line to tangent | 68 35'04.23" (254) | 68 39'07" (90 - 21.348 deg skew) | +4'03" | DIFF | N |
| 6 | Deck | 32'-0" o/o: 20'-6" left / 11'-6" right of CL, 8" slab, 6 % one plane (270) | -20.500 / +11.500 ft, 8.00", 6 % | 0 | OK | N |
| 7 | Parapet | F-shaped 1'-6", 42" (270) | FShape, 42", base 1.5 ft, both sides (profile generic, FSHP-42 not in the volume) | 0 | OK | N |
| 8 | Tub section | depth 4'-2", webs 4:1, WP 8'-0", bottom 77" (3" + 5'-11" + 3"), top flanges 2 x 21", webs 5/8" (273) | WebDepth 50", slope 0.25, 8'-0", 77", 21", 0.625" | 0 | OK | N |
| 9 | Girder layout | 17'-8" radial, G2 4'-4" right of CL, R 868'-1 11/16" / 885'-9 11/16" (273, 282) | spacing 17'-8", first offset -13'-4" | 0 | OK | Y |
| 10 | G1 span 16 CL brg - CL brg | 157'-6 7/8" (278) | 157'-6 15/16" | +0.083" | DIFF (1/12") | Y |
| 11 | G2 spans 15 / 16 | 161'-9 9/16", 160'-9 3/8" (279) | 161'-9 1/2", 160'-9 7/16" | -0.065", +0.064" | DIFF (1/16", plan strings disagree) | Y |
| 12 | Plate thicknesses and zone strings (re-read) | G1 top 1 1/4 / 1 7/8 / 1 1/4 / 1 3/8 / 2 1/4 / 1 3/8; bottom 1 1/4 / 1 7/8 / 1 3/8 / 2 1/4 / 1 3/8; webs 5/8, 3/4 at pier 15 (278) | identical; 20'-4 1/2, 15'-1 5/8, 37'-3 13/16, 14'-0 5/16 all match | 0 | OK | Y |
| 13 | G2 top tension zone over pier 14 | 39'-2 7/8" (279) | 41'-8 13/16" (between BOTH zones) | +29.9" | DIFF (plan string does not close; known) | Y |
| 14 | Field splice bolts | 1" A325, top 4 lines x 12 per flange, web 6 x 12 per web, bottom 18 x 10: 96 + 144 + 180 = 420 per splice (281) | 420 x 8 = 3,360 | 0 | OK | Y |
| 15 | Splice plates | top 3/4 x 21 x 3'-2 1/2 + 2 PL 3/4 x 9 1/2; web 2 PL 5/8 x 22 1/2 x 3'-6 1/2; bottom 63 1/2 x 3'-0 1/2 (3/4 splice 1, 7/8 splices 2-4); filler 1/8 x 1'-9 x 1'-7 1/8 splice 3 (281) | same; 4 fillers | 0 | OK | N |
| 16 | Intermediate bracing | L5x5x5/8 diagonals, WT6x29 top strut, gusset 3/4, stiffener 3/4 x 10, tab 3/4 x 10 x 1'-0, clip 1 1/2 x 3 (282); solid panel points = K frames | same; 42 K frames, 88 top struts, 94 laterals | 0 | OK | N |
| 17 | External diaphragms | PL 3/4 x 12 flanges, web 5/8 x 3'-2", 2-L8x8x3/4, 8 / 9 bolts (282) | same; 6 diaphragms (p.273 bold lines) | 0 | OK | N |
| 18 | Pier 13 end diaphragm | web 3/4", PL 1 x 12 top / bottom connection plates continuous with studs @ 1'-0", 1 x 8 bearing stiffeners (283) | web 0.75", flanges 12 x 1, stiffeners 8 x 1; connection plates and diaphragm studs not built (build U) | partial | DIFF | N |
| 19 | Bearing assemblies | P13 13x22x3 3/4, plate 2 1/2 x 23 x 14, 4.52 / -4.92 %; P14 20x30, 31 x 21, 6.00 / -1.48; P15 22x30, 31 x 23, 6.00 / -0.57; P16 15x23x5, 24 x 16, 6.00 / -0.57 (293) | same sizes; bevel % within 0.06 %; 24 anchors 2" | <= 0.06 % | OK | Y |
| 20 | 'H' top of slab to bottom of bearing (re-read) | 5'-10", 5'-10 5/8", 5'-11", 5'-11 3/8" (271) | each +0.092..0.094" | +1/16..1/8" systematic | DIFF | Y |
| 21 | Seat elevations vs pier sheets | P14 1125.44 / 1126.50 (262); P15 1124.16 / 1125.22 (264); P16 1123.05 / 1124.12 (266) | 1125.461 / 1126.521; 1124.162 / 1125.222; 1123.219 / 1124.279 | +0.021; +0.002; **+0.169 / +0.159 ft (2")** | DIFF (P16 large, P14 small) | Y |
| 22 | Pier 13 pedestals (Bridge N, 180) | 1128.81 / 1129.65 | 1128.825 / 1129.681 | +0.015 / +0.031 | DIFF (small) | Y |
| 23 | Girder end beyond CL brg at pier 13 | 9 11/16" (278) | 9.688" | 0 | OK | N |
| 24 | Studs | 3 per row, 7 zones per girder (278, 279, 290) | 7,008 + 82 other = 7,090 | 0 | OK | Y |
| 25 | Structural steel quantity | 904,820 lb (255 "SUMMARY OF QUANTITIES - BRIDGE O") | 879,089 lb excluding studs, bolts, welds (est. +9k studs, +12k bolts -> about 900k) | -25.7k lb (-2.8 %) | DIFF (explained, see 2.2) | N |
| 26 | Haunch | 4 1/2" at CL (270, 282) | 4.50" | 0 | OK | N |
| 27 | Dead load deflection | 70 rows (280) | max 0.010" | 0.010" | OK | Y |
| 28 | Drilled shafts piers 14-16 | 60" dia., 102 / 114 / 114 LF = 330 LF (255, 262-267) | none (substructure off by scope) | all | MISSING | N |
| 29 | Piers 14-16 (cap 39'-9", 2 columns, 7'-0 footings, sloped pedestals, peace shield) | 262-268 | none | all | MISSING | N |
| 30 | Deck reinforcing | epoxy bars A1 1,959 x 31'-8" etc., 155,440 lb (271-272, 255) | `Reinforcement.Enabled = false` | all | MISSING | N |
| 31 | Deck end details | sealed expansion joint EJ-SQ at pier 16 (30 LF), 2" joint, 3 1/2" thickened slab ends, drip bead, anti-graffiti 2,597 SF, water repellent (270, 271, 255) | none; deck ends at brg line + 1'-0 | all | MISSING | N |

Counts ODOT: OK 18, DIFF 9, MISSING 4 (31 rows; rows 28-31 are by-scope omissions).

### 2.1 Missing from the ODOT model, ranked
1. Piers 14-16 and the pier 13 pedestals (substructure, by user scope): sloped pedestals, caps, columns, footings, 330 LF of 60" shafts - plans fully dimensioned (p.262-268).
2. Deck reinforcing (155,440 lb epoxy-coated; bar list on p.271-272) - fully tabulated.
3. Deck ends: thickened slab (3 1/2" thicker, 2'-2" long), EJ-SQ sealed joint at pier 16 / EJ-SK at pier 13, 2" gap (the deck now ends 1'-0" beyond the CL brg; the plan joint is at CL pier 16 +- 1").
4. Pier diaphragm connection plates PL 1 x 12 and the diaphragm top studs (p.283-288); rodent screens, drip plates, drain holes (p.289-290).
5. Contact plates (fixed 4x4, expansion 8x8x1/2 with slots) and guide bars on the bearings (p.292).
6. Superelevation / parapet profile (FSHP-42 standard missing from the volume), water repellent areas, anti-graffiti.

### 2.2 ODOT UNRESOLVED items the plans answer
- **Structural steel quantity (build U "no quantity found")**: p.255 "SUMMARY OF QUANTITIES - BRIDGE O" lists STRUCTURAL STEEL 904,820 lb (superstructure; spans 14-16 are the only steel), 4 stainless fixed + 8 expansion bearing assemblies, 42" F-shaped parapet 1,038.6 LF, Class AA 451.7 CY, epoxy-coated reinforcing 155,440 lb, water repellent, graffiti SF, 330 LF of 60" shafts. The model's 879,089 lb is 2.8 % under; studs (about 9k lb) plus splice and diaphragm bolts (about 12k lb) bring it to about 900k lb, within about 0.5 % of the plan - the check can now be a real comparison.
- **Pier 13 coordinates (U)**: explained by p.254 itself - two station equations (44E at I-44 survey 115+56.82, 44F at 116+23.16, 66.34 ft apart); the p.148 N/E is the 44E point. The model's 44F pier-13 point is 66.68 ft from it (+0.34 ft), a cross-check that passes.
- **U2 datum**: converting the printed N/E (pier 14 N 193,275.14 E 2,113,643.09) as NAD83 Oklahoma North, US survey feet (LCC 35 34'/36 46', origin 35 00'N 98 00'W, FE 1,968,500 ftUS) gives 35.5299 N, -97.5122 W (pier 16 35.5308, -97.5122) - the I-235 / I-44 area in Oklahoma County. Probable datum = NAD83 Oklahoma North ftUS (EPSG:2267); confirm with a site GPS point as was done for NHT U12.
- **U3 (pier diaphragm sheets O-31..O-34 "not read")**: the build already carries values for them (1 1/2" interior webs, 12 x 1 1/2 flanges, 8 x 1 1/4 bearing stiffeners @ 2'-0", access opening 1'-6" x 2'-8" R 8" at 11") - the UNRESOLVED text is stale and should be reworded (only the PL 1 x 12 connection plates remain unbuilt). I re-read only p.283 (pier 13), not p.285-288.
- **Pier 13 skew (U, p.273)**: p.254 gives a third value (68 35'04.23" to tangent at the CL pier). The three plan sources (68 35'04", girder angles 69 02'38" / 69 18'54", elevation-sheet spans) cannot all be satisfied: the model is +4'03" from p.254 and -44' / -33' from p.273. Take p.254 (a surveyed pier line) as primary and report the others.
- **Cannot be answered from the volume**: U1 (PC/PT of curve 9), FSHP-42 section, superelevation transition, G2 top tension zone (+29.9"), P16 pedestal vs 'H' (plan 0.17 ft inconsistent: pedestals 1123.05 / 1124.12 on p.266 vs top of slab minus H 5'-11 3/8" = 1123.22 / 1124.28 - the model follows 'H').
- Plan inconsistency worth recording: the right-hand "TYPICAL TUB GIRDER SECTION" on O-16 (p.270) is a 10'-7" deep, 20'-3 13/16" wide tub, unlike the 4'-2" x 8'-0" tub of O-19 (p.273) and the "2-50" steel tub girders" label of O-1; the build correctly follows O-19 / O-28.

## 3. Overall assessment

**Northaven NHT.** Geometry and members that the plans dimension are modeled to plan precision: alignment (every curve and PC/PT), profile, supports, 14 approach girders (lengths, angles, offsets, seats to 0.002 ft), abutment / pier control elevations, shafts, and the arch rib, ties, 33 floorbeams, 64 hangers, fin plates, splices (bolt counts 68 / 42 verified from the rasters), bearings and the PT tendons. The arch work is exceptionally detailed. Remaining differences are small (WP2 +3/16", tendon spacing 5/16", abutment shaft top -0.016, cap/column concrete split) except for the missing items: no railing, no rebar anywhere, no curbs / joints, no camber, and the arch steel quantities (bracing -37 %, floor -15 %, knuckles +55 %) do not reconcile with the sheet tables although the sheet tables themselves reconcile in total. The documentation is correct apart from the rib-web wording.

**ODOT Bridge O tub unit.** The superstructure geometry (alignment, profile, plate runs, splices, bracing counts, bearings, studs, deflections) is built to the plans with sub-1/8" differences, with three real exceptions: the G2 tension zone string (plan inconsistent), the P16 pedestal / 'H' pair (0.17 ft), and the pier 13 line (4' to 44' depending on which plan angle is trusted). Substructure, deck reinforcing and deck-end details are absent by scope. The steel weight is within 3 % of the p.255 plan quantity and can be tightened by adding studs / bolt hardware.
