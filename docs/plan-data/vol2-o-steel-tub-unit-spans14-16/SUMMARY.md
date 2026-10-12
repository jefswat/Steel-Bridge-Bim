# vol2-o-steel-tub-unit-spans14-16 - plan data summary (ONE steel tub girder unit, proof of concept)

Scope (user, 2026-10-11): one STEEL tub girder unit only; concrete U54 units skipped entirely; other bridges only for shared data.
Source: `Vol 2_Phase 2B Bridge Combined.pdf` (300 pp.), Oklahoma DOT (title block "OKLAHOMA DEPARTMENT OF TRANSPORTATION"), Oklahoma County,
job piece 09033(11), I-235 / I-44 interchange, Oklahoma City; Benham / HDR; sheets dated 11/21/2016 "FINAL FIELD MEETING"; the steel sheets carry
"preliminary, not a final signed and sealed document".
Unit chosen: **Bridge 'O' (Ramp 44F over I-44 & Ramps 44N, 44M2), spans 14-16 = 3 continuous spans of 2 x 50" steel tub girders**,
139'-3 3/8 + 161'-0 + 161'-0 along CL Ramp 44F (461'-3 3/8). Bridge O continues with span 17 (59'-0, 2 x 54" concrete U beams), skipped.
Why: smallest unit with a complete steel set (framing p.273, girder elevations p.278-279, deflections p.280, splices p.281, intermediate bracing p.282,
pier diaphragms p.283-288, bearings p.292-293, deck p.270-272, piers 14-16, staking). Tags: TEXT / RASTER / DERIVED / UNRESOLVED.
Fractions are "?" glyphs in the text layer, so every fraction was read on a 200-300 dpi raster.
Standards referenced (listed, not assumed): AASHTO LRFD Bridge Design Specifications 2004 3rd ed. (bearing pads per AASHTO Standard Specifications 16th ed.,
1996 interims); ODOT Standard Specifications sections 506.04.F, 724.02.D, 724.05; ODOT std FSHP-42 (F-shaped parapet), EJ-SK / EJ-SQ (expansion joint);
AASHTO/AWS D1.5 fracture control plan (p.278 note 6: bottom tension flange and webs fracture critical); M270 Gr 50W.
Files: `bar-lists.csv`, `dead-load-deflections.csv`, `girder-plates-and-splices.csv`; `INDEX.md`; whole set `../vol2-sheet-index.csv`.

## Answers to the scope questions
1. **Type / owner**: Oklahoma DOT. The volume holds steel TUB girder spans (Bridge K spans 3 and 10-15, Bridge N spans 6-7 and 12-15, Bridge O spans 14-16),
   concrete U54 beam spans, and Bridge F3 (I-235 NB over I-44, 48" plate girders, 2 spans, p.11-45).
2. **Unit**: Bridge O spans 14-16.
3. **Substructure for this unit IS in the volume**: general plan O-1 (p.254), design data O-1A (p.255), foundation report O-2..O-4 (p.256-258), staking O-5 (p.259),
   piers 14 / 15 / 16 details O-8..O-13 (p.262-267), peace shield pedestal O-14 (p.268), form liner O-43 (p.297). Abutment 1 (O-6/O-7, p.260-261) is at the span-17
   (concrete) end. Pier 13 (begin of Bridge O, shared with the end of Bridge N) is drawn on Bridge N sheets N-46..N-49 (p.178-181, "PIER NO. 13 WEST / EAST").
   Not in the volume: roadway plans, Ramp 44F alignment (PI) table, drainage, pavement.

## 1. Geometry control (shared data)
| item | value | PDF p. / sheet | tag | note |
|---|---|---|---|---|
| Alignment | CL Ramp 44F = profile grade line | p.254 / O-1 | RASTER | |
| Curve No. 9 | R 881.47, T 713.29, delta 77 deg 57'36.04" LT, D 6 deg 30'00", L 1199.38 | p.254 | RASTER | DERIVED R tan(A/2) = 713.3 ok; R*A = 1199.35 (0.03 rounding) |
| PC / PT stations | not printed in this volume | p.254 | UNRESOLVED U1 | no alignment table |
| Girder radii | girder 1 R 868'-1 11/16, CL Ramp 44F R 881'-5 11/16, girder 2 R 885'-9 11/16 (concentric curved girders) | p.273 / O-19 | RASTER | CL radius = 881.47 |
| Coordinates / datum | N / E of working points below; zone / datum not printed | p.259 / O-5 | UNRESOLVED U2 | |
| Working points (CL pier x CL ramp) | pier 13 STA 301+53.72 (= STA 301+09.58 Ramp 44E; N 193142.24 E 2113535.71 from Bridge N p.148); pier 14 302+93.00 N 193275.14 E 2113643.09; pier 15 304+54.00 N 193434.63 E 2113663.38; pier 16 306+15.00 N 193595.16 E 2113654.37; abut 1 F.F. backwall 306+73.25 N 193652.45 E 2113643.91 | p.259, p.148 | TEXT | |
| Station equations | 301+09.58 Ramp 44E = 301+53.72 Ramp 44F; 304+77.01 Ramp 44F = 303+77.56 Ramp 44N; 305+52.42 Ramp 44F = 304+00.30 Ramp 44M2 | p.254 | RASTER | |
| Vertical profile | PVC 301+70.00 el 1134.85; PVI 302+70.00 el 1131.92; PVT 303+70.00 el 1131.35; VC 200'; grades -2.93 % then -0.57 % | p.254 | RASTER | DERIVED: 1131.92 + 100 x 2.93 % = 1134.85 ok; 1131.92 - 100 x 0.57 % = 1131.35 ok |
| Profile at supports | pier 13 1135.33; pier 14 1132.14; pier 15 1130.87; pier 16 1129.96; end bridge 306+74.00 1129.62 | p.254 | RASTER | all DERIVED ok: 1134.85 + 16.28 x 2.93 % = 1135.33; VC at 302+93 = 1134.85 - 123 x 2.93 % + 123^2 x 2.36 % / 400 = 1132.14; 1131.35 - 84 x 0.57 % = 1130.87; 1131.35 - 245 x 0.57 % = 1129.95 (1129.96); - 304 x 0.57 % = 1129.62 |
| Cross slope | 6 % one plane across the 32'-0 deck, profile grade at CL ramp; clear roadway 29'-0 = 4'-0 shoulder + 15'-0 lane + 10'-0 shoulder; parapets 1'-6 each side | p.270 / O-16 | RASTER | |
| Loading / materials | HL-93 or Oklahoma overload; 20 psf future wearing surface; 5 psf steel stay-in-place forms; LFD operating HS-57.0; Class AA f'c 4000 psi, Class A 3000 psi, fy 60 ksi, M270 Gr 50W | p.254 | RASTER | |

## 2. Layout
| item | value | PDF p. / sheet | tag | note |
|---|---|---|---|---|
| Supports | pier 13 (EXP), pier 14 FIXED (F1, continuous), pier 15 (G2 guided), pier 16 (G1 guided, EXP to span 17) | p.254, p.293 | RASTER | |
| Spans along CL | 139'-3 3/8, 161'-0, 161'-0; Bridge O 520'-3 3/8 with span 17 | p.254 | RASTER | station differences 139.28 / 161.00 / 161.00 ok; 139.28 + 161 + 161 + 59 = 520.28 ok |
| Skew | end diaphragm lines at pier 13: 69 deg 02'38" (girder 1) and 69 deg 18'54" (girder 2); piers 14-16 lines 90 deg typ.; 17'-8 between girders along piers 14-16, 18'-11 7/8 along CL pier 13 | p.273 / O-19 | RASTER | |
| Girders | 2 tub girders rotated about the top working point with the plane of the deck | p.273 | RASTER | |
| Deck | 8 in slab, 32'-0 out to out; pour sections 1 / 3 / 2 / 3 / 1, sections 3 straddle piers 14 and 15 (35'-0 + 35'-0 along baseline at pier 14; 45'-0 + 35'-0 at pier 15), joints parallel to CL pier, 48 h lapse | p.270-272 | RASTER | |
| Barrier | 42" F-shaped parapet both sides (std FSHP-42); sealed expansion joint at the span-17 end | p.270-271 | RASTER | |

## 3. Superstructure (steel tub unit)
| item | value | PDF p. / sheet | tag | note |
|---|---|---|---|---|
| Tub section | depth 4'-2, web length on the slope 4'-3 9/16, webs 4 vertical : 1 horizontal, WP at top of web on girder CL, top-flange WPs 8'-0 apart, bottom flange 77 in wide (web CLs at bottom 2'-11 1/2 each side of CL, 3" to the edge), top flanges 21 in wide (2 PL) | p.273 (section), p.281 | RASTER | the 10'-7 deep tub on p.270 is the 76 in tub of Bridge K/N, not this unit |
| Webs | 2 ~ PL 5/8 typical; 2 ~ PL 3/4 in the end zones at piers 15 and 16 | p.278-279 | RASTER | |
| Plates, tension zones, studs | `girder-plates-and-splices.csv` | p.278-279 | RASTER | compression flange welded only |
| Field splices | 4 per girder; 1" A325 type 3 bolts with direct tension indicators, 1 1/16" holes; top: 1 splice PL 3/4x21x3'-2 1/2 + 2 PL 3/4x9 1/2x3'-2 1/2; web 2 PL 5/8x22 1/2x3'-6 1/2; bottom 2 PL 3/4x63 1/2x3'-0 1/2 (splice 1), 7/8x63 1/2x3'-0 1/2 (splices 2-4); filler PL 1/8x1'-9x1'-7 1/8 at splice 3 only; 1/4 in max gap | p.281 / O-27 | RASTER | |
| Cross frames, bracing, diaphragms | internal cross frames (L5x5x5/8 diagonals, 3/4 gusset, WT6x29 top strut), bottom lateral bracing, 1 1/2 x 3 clips, external intermediate diaphragms (PL 3/4x12 flanges, 5/8 web, 2-L8x8x3/4, stiffener PL 3/4x10) | p.273, p.282 / O-28 | RASTER | bay strings: G1 span 14 14 eq. spa = 141'-3 15/16; G2 span 14 12 eq. spa = 123'-6 5/8 + 2 eq. spa = 13'-7 11/16; spans 15-16 17 eq. spa = 158'-6 3/4 / 161'-9 1/2, 16 eq. spa = 157'-6 7/8 / 160'-9 1/2 |
| End / pier diaphragms | pier 13 span 14: top and bottom connection PL 1x12, webs PL 3/4, bearing stiffeners 1x8, 18'-11 13/16 along CL bearing, bent plates 1/2x16; O-31..O-34 (piers 14, 15 diaphragms) not read | p.283-284 / O-29, O-30 | RASTER | U3 |
| Bearings | O-39: P13 pad 13 x 22 x 3 3/4 (5 layers 1/2, 6 laminates 1/8, 1/4 exterior), plate 2 1/2 x 23 x 14, M 4.52 % N -4.92 %; P14 20 x 30 x 3 3/4, plate 31 x 21, 6.00 / -1.48; P15 22 x 30 x 3 3/4, plate 31 x 23, 6.00 / -0.57; P16 15 x 23 x 5 (7 layers, 8 laminates), plate 24 x 16, 6.00 / -0.57; 2" anchor bolts 1'-9 1/2 each side of CL bearing; girder end 9" beyond CL bearing; fixed: 4x4 bottom contact PL, expansion: 8x8x1/2 top contact PL with 2 1/2 x 6 slot | p.292-293 / O-38, O-39 | RASTER | |
| 'H' (top of slab to bottom of bearing assembly at CL bearing) | pier 13 span 14 5'-10; pier 14 5'-10 5/8; pier 15 5'-11; pier 16 span 16 5'-11 3/8 | p.271 / O-17 | RASTER (300 dpi) | theoretical; haunch set after erection (p.270); nominal haunch 4 1/2 in |
| Dead load deflection | `dead-load-deflections.csv`: girder 1 max 4.28 (span 14), -0.44..0.52 (span 15), 6.43 (span 16); girder 2 4.26 / -0.54..0.50 / 7.46 in; concrete + steel = total in all 70 rows | p.280 / O-26 | TEXT | slab + 5 psf forms + parapet; not beam weight or wearing surface |
| Top-of-slab / haunch tables | not in the volume | - | UNRESOLVED U5 | |
| Fabrication notes | Charpy V-notch Zone 2 for tension flanges, webs, splice plates (fracture critical members per the fracture control plan); girder ends and end diaphragm webs plumb after total dead load; interior surfaces white polyamide cured epoxy, faying surfaces unpainted; blast clean SSPC-SP10; fillet welds stop 3/8 (+- 1/8) from edges; access hatches per span; drain hole + 2" slot every 50 ft on the low side tub (not within 5' of a pier); rodent screens at support diaphragms | p.278-282, p.289-290 | RASTER | |

## 4. Substructure (piers 14-16; pier 13 on Bridge N sheets)
| item | value | PDF p. / sheet | tag |
|---|---|---|---|
| Pier 14 | 2 columns on 7'-0 footings at 13'-0, cap 39'-9 long, 2 drilled shafts 5'-0 dia, footing bottom el 1095.00, shaft bottom el 1044.00 (10' min into rock), pedestals 1125.44 / 1126.50, cap ends el 1124.51 to 1127.32, 102 LF shafts | p.262 / O-8 | RASTER |
| Pier 15 | same layout, pedestals 1124.16 / 1125.22, footing el 1096.00, shaft bottom 1039.00, 114 LF | p.264 / O-10 | RASTER |
| Pier 16 | pedestals 1-4: 1123.05 / 1124.12 / 1122.65 / 1123.75; footing el 1101.00; shaft bottom 1044.00; 114 LF | p.266 / O-12 | RASTER |
| Pier 13 | N-46 west / N-48 east: 8'-0 dia shafts, footing el 1101.00, shaft bottom 1079.50 / 1044.00; pedestals 1126.27 / 1127.33 (west), 1128.61 / 1129.65 / 1128.81 (east) | p.178, p.180 | RASTER |
| Bar lists | pier 14 12,875 lb, 15 12,427 lb, 16 11,187 lb (calc) + 744 lb peace shield pedestals (2 x 372, p.268) = 13,619 / 13,171 / 11,931 vs plan 13,630 / 13,180 / 11,940 lb | p.262-268 | DERIVED ok (-0.08 %) |
| Shaft design table | not extracted | p.255 | - |

## 5. Reinforcing
Superstructure bar list (all Bridge O incl. span 17): calc 155,929 lb vs plan 155,440 lb (+0.3 %, U6).

## UNRESOLVED
| id | item | PDF p. | reason |
|---|---|---|---|
| U1 | PC / PT stations of curve no. 9 | p.254 | not printed; no alignment sheet in the volume |
| U2 | coordinate zone / datum | p.259 | not printed |
| U3 | pier diaphragm sheets O-31..O-34 (piers 14, 15) | p.285-288 | not read (time-boxed proof of concept) |
| U4 | start datum of each plate-segment stack on O-24 / O-25 | p.278-279 | strings read as drawn; datum of the TENSION FLANGE row not confirmed |
| U5 | haunch heights, top-of-slab elevation table | - | no table in this volume |
| U6 | deck bar weight +0.3 % | p.271 | E1 / EB1 lap counts do not reconcile it |

## Model mapping
| section | existing SBM inputs | GAP |
|---|---|---|
| 1 | `PiDefinition` (R 881.47 LT), `VerticalProfile` PVI 302+70 VC 200, `CrossSlopeTable` 6 % | PC / PT (U1), datum |
| 2 | `BridgeDefinition` 4 supports, 3 spans, deck 32'-0, parapets | spans along CL ramp; concentric curved girders |
| 3 | steel plate girder generator (I-section) | GAP: tub cross-section (4:1 webs, 21 in x 2 PL top flanges, 77 in bottom), rotation about the top WP, internal K bracing + top struts, external diaphragms, tub splices, pier support diaphragms with bent plates, access hatches, bearing assemblies with beveled plates |
| 4 | multi-column bent, drilled shafts | sloped pedestals, peace shield |
| 5 | bar lists | |