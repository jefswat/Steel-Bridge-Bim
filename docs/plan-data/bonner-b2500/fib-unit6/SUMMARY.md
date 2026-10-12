# bonner-b2500 - Unit 6 (spans 30-35): 96" precast prestressed Florida I-Beam (FIB) units, continuous for live load

Source: "B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf", sheets S4-03, S4-06/07/09, S4-12, S4-17..23, S4-30..32, S4-36..39, S4-46/47, S4-48..52, S4-53, S4-55, S4-62, S4-64, S4-65..76, S4-84..93 (PDF p.395-489).
"p." = PDF page. Format per docs/plan-data/EXTRACTION.md. Plan precision 1/16" / 0.01 ft. No fallback guesses: open items are in UNRESOLVED.
Tags: `TEXT` text layer (stacked fractions decoded with the glyph table of ../segmental-unit5/SUMMARY.md; the glyph U+2022 is shared by 1/2, 11/16 and 15/16 and was always confirmed on raster or by calculation), `RASTER` read on a raster clip (60-220 dpi, deleted after reading), `DERIVED` computed (formula shown), `UNRESOLVED`.

Why Unit 6: it is the 96" FIB unit adjacent to the segmental unit (bent 29 is shared), has four uniform girder lines, six equal spans of 160'-10", and its sheet set is complete (typical section, plan of spans, framing, girder + camber, bearings, parapet, BOM, pour sequence, bents 30-35, columns, caps, piles). It is also the only 96" unit with the unit-end bearing at bent 29 (modular joint) and a tangent-to-arc span (35).

CSV files in this folder: `unit6-spans-bents.csv`, `girders.csv`, `bearings.csv`, `bearing-parts.csv`, `camber-dead-load-deflection.csv` (168 cells), `seat-to-deck-check.csv` (40 rows), `superstructure-bill-of-material-unit6.csv`, `bent-columns-caps.csv`, `pedestals-cap-elevations.csv`, `piles.csv`.

## Standards referenced
- NO FDOT Standard Plans / Design Standards index number is cited anywhere in the 633 sheets (text search for FDOT, Florida Department, Standard Plans, Design Standards, Index No: no hit). The plans use the name "Florida I-Beam" / "FIB" only on the general drawing p3 ("96" FIB GIRDERS", "45" FIB GIRDERS"); the unit sheet titles (S4-36..47) say "96" PRESTRESSED CONCRETE MODIFIED BULB TEE". The beam is dimensioned on the plan sheet (S4-46, p438) instead of by reference. FDOT index numbers to list: none. (Nothing is assumed about the contents of any FDOT index.)
- Cited on the sheets: AASHTO M203 (7-wire LR Grade 270 strand), ASTM A449 / AASHTO M291-DH or M292-2H / M293 (anchor bolts, nuts, washers), AASHTO M169 grades 1010-1020 and ANSI/AASHTO/AWS D1.5 type B (anchor studs), ASTM D1785 (Sched 40 PVC sleeves), ASTM A722 (standard PT bars), NCDOT Standard Specifications (galvanizing; bridge deck grooving item 420-14), pile submittal D-068R1. Sources p438, p440, p456, p458.

## 1. Unit layout
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Unit | 6: spans 30-35, bents 29-35, 965'-0" along -L- (TAN/ARC) = 6 x 160'-10" | | p395 S4-03, p409 S4-17 | TEXT | continuous for live load; span 35 tangent then arc (PC STA 102+46.84, R = 6050.00') |
| Bent stations (-L-) | 29 92+95.00, 30 94+55.83, 31 96+16.67, 32 97+77.50, 33 99+38.33, 34 100+99.17, 35 102+60.00 (W.P.) | | p395 | TEXT | = ../bents.csv |
| Skew | all bents radial; framing angles 90 00'00" typ.; span 35 shows 89 59'42" and 90 07'10" (to short chord) at bents 34 / 35 | | p424 | TEXT | |
| Fixity | bent 32 FIX (E3 elastomeric, P8 / P35); all other bents EXP | | p423 S4-31 | TEXT | matches ../bents.csv |
| Unit ends | bent 29: modular expansion joint (6'-0" block-out) at the Unit 5 end, bearing line 3'-2" ahead of the bent line; bent 35: CL joint and bent 35 control line (to Unit 7) | | p409 S4-17, p422, p457 | RASTER | joint hardware is in the general / S3-85 sheets |
| Continuity | simple-span girders made continuous for live load by cast-in-place bent diaphragms (2'-4" wide) and the deck; end diaphragms 1'-10" (min); no permanent intermediate diaphragms | | p409, p415 S4-23, p422 note | RASTER/TEXT | |
| Span length along -L- | 160'-10" each (bent CL to bent CL, horizontal) | | p422-424 | TEXT | span 35 arc: chord 160'-10 1/2" (left edge) / 160'-9 3/8" (right edge) |
| Bearing-to-bearing | span 30: 156'-6"; spans 31-34: 158'-6"; span 35 per girder G1 158'-4 7/16", G2 158'-4 1/8", G3 158'-3 7/8", G4 158'-3 9/16" | | p422, p423, p424 | TEXT/RASTER | DERIVED: 160'-10" - 2 x 1'-2" = 158'-6"; 160'-10" - 1'-2" - 3'-2" = 156'-6" |

## 2. Alignment over the unit
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Horizontal | -L- tangent from bent 29 to PC STA 102+46.84, then curve 3 (RT) R = 6050.00' | | p395, ../SUMMARY.md | TEXT | curve 3 data in ../SUMMARY.md section 1 |
| Vertical | PVI 91+45.00 el 90.40, VC 760' (0 to -4.9991 %); PVI 103+02.00 el 32.56, VC 1554' (-4.9991 % to 0); the unit lies on the -4.9991 % tangent between them (BVC of 2nd VC 95+25.00) | | ../SUMMARY.md | TEXT | used in the seat check |
| Cross slope | 0.025 ft/ft for STA 70+82.93 - 102+46.34 (spans 30-34 and the start of span 35); varies 102+46.34 - 102+59.84; 0.030 beyond | ft/ft | p404 S4-12 | RASTER | G1 is the HIGH side (pedestal elevations fall A to D by 0.29 ft = 0.025 x 11.5 ft); left / right of G1: UNRESOLVED |
| Deck width | 42'-7" out to out; 40'-0" clear roadway; 1'-3 1/2" gutter line to deck edge each side | | p404, p409 | TEXT/RASTER | |

## 3. Beam layout
| item | value | unit | PDF p. / sheet | tag | note |
|---|---|---|---|---|---|
| Girder lines | 4 per span, G1-G4 (labels 30G1..35G4); 3 spaces @ 11'-6" = 34'-6" (normal to short chord) | | p404 S4-12, p409 | TEXT | |
| Offsets from the work line | G1 +17'-3", G2 +5'-9", G3 -5'-9", G4 -17'-3" (framing: 11'-6" / 5'-9" from the unit CL) | ft | p422-424 | TEXT | sign convention of G1 not resolved |
| Overhang | (42'-7" - 34'-6") / 2 = 4'-0 1/2" each side; printed 4'-0 1/2" (tangent spans); span 35: one edge varies 4'-0 1/2" to 4'-6 1/2", the other 4'-0 1/2" to 3'-6 1/16" | | p404 dimension L / R table | RASTER/DERIVED | which side is which: UNRESOLVED |
| Beam type per span | 96" FIB, 4 girders x 6 spans = 24 | | p428-431 | RASTER | |
| Girder lengths | see girders.csv: A = 158'-3 1/2" (span 30); 160'-1 7/8" (31, 32); 160'-1 1/8" (33, 34); span 35 160'-1 1/4" / 160'-0 7/8" / 160'-0 5/8" / 160'-0 3/8" (G1..G4) | | p428-431 | RASTER/TEXT | |
| CL bearing from the girder end | 9" and 11": span 30 back end 11", ahead end 9"; span 31 9" both ends; span 35 9" back, 11" ahead | in | p428, p429, p430 (clips) | RASTER | other spans / ends not clipped |
| Deck drains | 6" PVC at about 13'-8 1/2" cts along -L- | | p410 | TEXT | |

## 4. FIB section (as printed; no FDOT standard cited)
p438 S4-46 "Girder outside dimensions", RASTER:
| item | value |
|---|---|
| Depth | 8'-0" (96") |
| Top flange | 4'-0" wide (2'-0" each side of CL); 3 1/2" thick at the tip, underside slopes down 1 1/2" over 1'-5" from the tip, then a 3 1/2" x 3 1/2" chamfer into the web |
| Web | 7" thick; 6'-1" high between chamfer and bulb; vertical stack 3 1/2" + 1 1/2" + 3 1/2" + 6'-1" + 7 1/2" + 7" = 8'-0" (checked) |
| Bottom flange | 3'-2" wide; 7" vertical edge, 7 1/2" slope, 1'-3 1/2" each side of CL at the top of the slope, R = 1'-3" fillet to the web |
| Other | top surface raked 1/4" (except the outside 4"); end bevel; four 1 1/2" dia formed holes in the web (3 spaces @ 11" = 2'-9", lowest 2'-3 3/4" above the bottom, parallel to the end; theta 90.0 span 30, 90.1 span 35) |
| Properties | NOT PRINTED (area, Yb, Ix) |
Cross-check: concrete 49.9 CY (span 30, A = 158'-3 1/2") implies an average gross area of 49.9 x 27 / 158.29 = 8.51 ft2; a polygon of the printed dimensions (fillet approximated as a sharp corner at 14.5" above the bottom) gives about 8.9 ft2 - NOT reconciled (UNRESOLVED).

## 5. Strands, debonding, camber, mild steel
| item | value | PDF p. / sheet | tag | note |
|---|---|---|---|---|
| Strand | 0.6" dia 7-wire LR Grade 270: area 0.217 in2, ultimate 58,600 lb, applied prestress 43,950 lb per strand; 72 per girder (spans 30-35) + 4 x 1/2" dia "slack" strands (top flange) tensioned to 10,000 lb max | p428 etc. | RASTER | |
| Concrete / release | 8,000 psi; transfer not less than 6,400 psi | p428, p438 | RASTER | |
| Pattern | bottom width 16 spaces @ 2" = 2'-8", 3" from the flange edges; vertical dimensions 3" to the first row, 6 spa @ 2" = 1'-0" and 7 spa @ 2" = 1'-2" (end view); at CL 7 spa @ 2" = 1'-2" above the 3" | p439 S4-47 | RASTER (90 dpi view) | row counts and coordinates are NOT tabulated: UNRESOLVED |
| Draping | draped strands hold down 5'-0" each side of the CL; estimated hold-down force 29,000 lb; devices within 6" of location; draped-group CG within 1/2" of theory | p428 S4-36 | RASTER | end / CL elevations of the draped group not tabulated |
| Debonding | strand symbols: fully bonded, or 0.6" strands debonded 4'-0", 14'-0", 16'-0", 18'-0", 30'-0" from the end of girder (symbols on the "at end of girder" picture; none at CL) | p439 S4-47 | RASTER | counts per length not tabulated: UNRESOLVED |
| Camber | table per span at twentieth points: girder-alone camber (ft), SDL deflection (ft, incl. FWS), final camber (in); span 30 exterior: camber 0.392 ft, SDL 0.214 ft, final 2 1/8" at 0.50; spans 31-34 exterior 2 5/16"; "age 90 days at deck placement" | p446 S4-53 | TEXT+RASTER | full table camber-dead-load-deflection.csv; 168 of 168 final-camber cells equal (camber - SDL) x 12 to 1/16" |
| Mild steel per girder (span 30) | S1 #5 x 148 (9'-7"), S2 #3 x 54, S3 #3 x 16, S4 #3 x 302, S5 #4 x 150, S6 #4 x 111, S7 #6 x 10, V1 #5 x 16, WWF 156 lb; total 3,854 lb; end spacing 5 spa @ 3 1/2" then 21 spa @ 6", 8-S3 @ 10"; D16 @ 18", 4-W6.5 | p428 | RASTER | spans 31-34: S1 148 (9'-6 1/2"), S5 154, S6 36, S7 20, S8 77, total 3,941 lb (p429) |
| Projections above girder (S1 / S5 / S6 / S8) | span 30: 10 1/4", 10 1/4", 8 1/2", n/a; spans 31-34: 9 3/4", 7 3/4", 8 1/2", 7"; span 35 G1-G2: S1 varies, 7 3/4", 8 1/2", 7"; G3-G4: 10 1/4", 8 3/4", 8 1/2", 7" | p438 S4-46 | RASTER | |
| Embeds | plate B-1, 2 per girder, 3/4" thick, 3'-2" x 2'-1", 12 studs 1/2" dia x 2" after welding, bevel edge 3/4", galvanized | p439 | RASTER | |

## 6. Build-up / haunch and deck
| item | value | PDF p. / sheet | tag | note |
|---|---|---|---|---|
| Build-up at CL bearing | span 30 all bents 5"; span 31: bent 30 4 1/2", bent 31 5 1/2"; span 32: bent 31 5 1/4", bent 32 5 3/4"; span 33: bent 32 5 1/2", bent 33 5 3/4"; span 34: 5 3/4"; span 35: bent 34 5 3/4", bent 35 6" | p404 S4-12 | RASTER | "varies along length of girder": values between bearings not tabulated |
| Slab | 9" uniform (includes grinding and grooving), sand-light concrete, stainless reinforcing, 3" clr top / 2 1/2" clr bottom | p404, p455 | RASTER | |
| Deck bars (typical) | top 29-#4 "B" @ 1'-6" = 42'-0" (#6 where bracketed); #5 "A" @ 6" max top (1924 per unit) and @ 5 1/4" max bottom (2199 per unit); bottom 12-#5 "B" @ 8 1/2" per bay, 4-#5 "B" per overhang; 1" or 3/4" bolsters @ 3'-0" | p404, p409-410 | RASTER/TEXT | A101 4125 = 1924 + 2199 + 2 (note: each plan-of-span BOM contains 2 additional A101) |
| Bar list | superstructure-bill-of-material-unit6.csv (p455) | p455 S4-62 | TEXT | weights sum to the printed 299,412 lb; each weight = n x L x plf within 2 lb |
| Continuity diaphragm | bent diaphragm 2'-4" wide: 4-#6 K, 8-#4 K, 7-#4 U, #5 E, #4 S bars; end diaphragm 1'-10" min, 8" deck-to-diaphragm fillet; 1 1/2" formed web hole passes bars | p409, p415 S4-23 | RASTER/TEXT | |
| Pour sequence | one pour per span (option: staggered 10'-0" overlaps), 4'-0" closure strips at the bents, 6'-0" at bent 29 | p457 S4-64 | RASTER | |
| Quantities | sand-light concrete 1,541.1 CY, stainless reinforcing 299,412 lb, magnesium ammonium phosphate concrete 0.6 CY | p455 | RASTER | |
| Parapet | 1'-2" concrete parapet, joints 28'-5" + 4 x 26'-0" + 28'-5" per 160'-10" | p448 S4-55 | RASTER | parapet reinforcement and rail not extracted |

## 7. Bearings
| item | value | PDF p. / sheet | tag |
|---|---|---|---|
| Per bent / side | bearings.csv (E1 at bent 29 ahead and bent 35 back; E3 + P8 / P35 at fixed bent 32; E2 elsewhere) | p422-424 | TEXT |
| E1 | 3'-0" x 1'-2" plan, 15 plates, 9 1/16" | p440 S4-48 | RASTER |
| E2 | 2'-8" x 1'-0", 9 plates, 6 7/16" | p442 S4-50 | RASTER |
| E3 | 2'-4" x 1'-0", 4 plates, 2 7/16" | p444 S4-52 | RASTER |
| Sole plates P (taper to the girder slope) | thicknesses and lengths in bearing-parts.csv; 1 1/4" at the thin end; 2 1/2" dia x 2'-7" anchor bolts (E2) | p440-444 | RASTER |
Stack at the CL bearing: pedestal el + elastomer + mean sole plate + 96" girder + build-up + 9" slab = deck el at the girder (seat-to-deck-check.csv).

## 8. Substructure (as shown for Unit 6)
| item | value | PDF p. / sheet | tag |
|---|---|---|---|
| Bents 30-35 | 2 columns at 10'-3" each side of the work line on a CIP footing (top el 5.50, 6'-0" thick, BOF -0.50, MHW 0.40) on battered 36" sq P/C piles (2:12) - 14 / 14 / 12 / 6 / 8 / 6 piles | p395, p458-467 | RASTER |
| Footings | bents 30, 31: 43'-6" x 21'-0"; bent 32: 37'-0" x 21'-0"; bents 33-35: 28'-6" x 14'-0"; stay-in-place precast bottom soffit form | p395, p459, p461 | RASTER |
| Piles | piles.csv (tip no higher than -110.0 to -130.0, scour critical -85.0, peak factored axial 400-545 tons; installed tip fields blank) | p398-399 | RASTER |
| Columns | precast segmental: 6'-0" x 6'-0" (bents 30-32), 5'-0" x 5'-0" (bents 33-35), 4" radius; segments in bent-columns-caps.csv; PT bars 1 3/4" dia (stainless below the first coupler, A722 above) in 3" ID ducts; 2" grout beds | p458, p477, p479 | RASTER |
| Cap | precast 42'-0" x 6'-6" x 5'-6", 8.0 ksi, 55.6 CY; 4 CIP pedestals per side (5'-2" wide), elevations in pedestals-cap-elevations.csv | p481, p483, p484, p486 | RASTER |
| Footing quantities (bent 30 / 31) | reinforcing 36,551 lb; footing 180.1 CY; column base 18.2 CY; piles 14 = 1,611 LF | p460 S4-67 | RASTER |

## Cross-checks
| check | result |
|---|---|
| 6 x 160'-10" = 965'-0" | ok |
| bearing-to-bearing vs bent spacing and bearing offsets | ok (158'-6", 156'-6") |
| 3 x 11'-6" + 2 x 4'-0 1/2" = 42'-7" | ok |
| Seat to deck (seat-to-deck-check.csv, 40 girder ends of bents 30-34) | stack minus deck within -0.011 / +0.014 ft (mean +0.001 ft) = printed 0.01 ft rounding plus P-plate mean thickness |
| Column heights: cap top - 5'-6" - 2" grout = column top | 63.92 -> 58.25, 56.08 -> 50.41, 49.25 -> 43.58, 42.58 -> 36.91, 37.17 -> 31.50, 32.17 -> 26.50 (all within 0.01 ft) |
| Column segments sum: el 12.50 + segments = top of column | ok bents 30-35 |
| Cap volume 42 x 6.5 x 5.5 ft = 1,501.5 ft3 = 55.6 CY | ok (printed 55.6 CY) |
| A101 4125 = 1924 + 2199 + 2 | ok |
| BOM weights sum 299,412 lb | ok |
| Final camber = (camber - SDL) x 12 | 168 of 168 ok |
| Girder concrete vs section polygon | NOT reconciled |
| Footing 43'-6" x 21'-0" x 6'-0" = 203 CY vs 180.1 CY printed | not reconciled (pile pockets / top slope not quantified) |

## Model mapping
| section | SBM input | status |
|---|---|---|
| 1-2 | supports at the W.P. stations, 6 spans, skew 0, deck 42'-7" with 4 girders at +-5'-9" / +-17'-3" | fits (alignment per ../SUMMARY.md) |
| 3 beams | `GirderLayoutDefinition.Kind = Prestressed`; `PrestressedBeamDefinition` with `Standard` = a NEW FIB-96 entry in `StandardBeamCatalog` (not there today); `SpanBeams` per span for lengths and end distances (9" / 11"); layout bearing points from the framing plan | GAP: FIB-96 catalog entry |
| FIB-96 dims to add | 96" deep; top flange 48" wide, 3 1/2" tip, 1 1/2" slope over 17", 3 1/2" x 3 1/2" chamfer; web 7"; bottom flange 38" wide, 7" edge, 7 1/2" slope to 15 1/2" each side of CL, R = 15" fillet. Printed properties: none | catalog dims as printed on p438; confirm against the FDOT drawing that the plans do not cite |
| Strands | `HarpedColumns`, `HoldDownFromMidspan` (5'-0" each side of CL), 72 x 0.6" + 4 slack, debond 4 / 14 / 16 / 18 / 30 ft | partial: row counts, draped coordinates, debond counts not extracted |
| Haunch | `Haunches` per girder = build-up + 9" slab at the bearings (table 6) | partial: variation between bearings not given |
| Beam mild steel | `BeamReinforcementDefinition` (S1-S8, V1, WWF) | partial: counts / lengths per girder read; positions and shapes not transcribed |
| Deck | typical section, A101 / B / K / S / U bars | fits `DeckPlanReinforcement`; shapes partial |
| Bents | precast cap with 4 pedestals per side, segmental columns, CIP footing on 36" sq piles (2:12 batter), elastomeric bearings E1-E3 with tapered sole plates | partial; precast segmental columns / PT bars are a GAP |
| Integral end bents | none in Unit 6 (expansion joints at both ends) | n/a |

## UNRESOLVED
| item | pdf page | reason |
|---|---|---|
| FDOT Standard Plans index number(s) for the FIB-96 | all | none cited on any sheet; only the dimensions of p438 are given |
| FIB-96 section properties and exact fillet geometry | p438 | not printed; polygon of printed dimensions gives about 8.9 ft2 vs 8.51 ft2 implied by the 49.9 CY quantity |
| Strand count per row, draped-strand coordinates, debonded counts per length | p439 S4-47 | drawn with symbols only; not counted dot by dot |
| Build-up between the bearings | p404 | only the values at CL bearings are tabulated |
| Side of G1 (left / right of -L-), cross-slope direction, which overhang (L / R) is the 4'-6 1/2" side | p404, p409 | arrows / labels not resolved at the clip resolution |
| CL-bearing end distances for spans 32-34 and the remaining ends | p429 | clips taken only for spans 30, 31, 35 |
| Span 35 G3 / G4 girder data (p431) | p431 | read from the text layer with the glyph table, not raster clipped |
| Girder A minus bearing-to-bearing (span 31: 160'-1 7/8" - 158'-6" = 1'-7 7/8" vs 2 x 9" = 1'-6") | p422, p429 | 1 7/8" not explained by the printed end distances |
| Span 35 G1-G2 S1 projection "VARIES" | p438 | value not printed |
| Pile installed tips / driving resistance | p398-399 | fields blank in the plans (filled during construction) |
| Bent 29 footing and piles | p395 | on the Unit 5 sheets, not extracted here |
| Footing concrete 203 CY computed vs 180.1 CY printed | p459, p460 | difference not explained |
| Bar shapes of the beam steel and deck K / U / S bars | p428, p455 | not transcribed in detail |
| Parapet reinforcement, rail posts, joint seals | p448-454 | not extracted for this unit |

Method: PyMuPDF text for tables, fractions decoded with the glyph table and confirmed on raster; every raster clip deleted after reading (scripts in temp\fib, not committed).
