# Br. 02069 - T.H. 65 S.B. - model (build + check)

Model `samples/plans/Br02069 TH65.sbm`, built FROM SCRATCH by `samples/plans/build-br02069.csx` from `SUMMARY.md` and the CSVs of this folder;
checked by `samples/plans/check-br02069.csx` (read-only). Plan precision 1/16", 0.01 ft. Nothing unstated is invented: plan items that are
not stated are listed as UNRESOLVED (with PDF page), program features that are missing are GAPs (closest existing input used, marked
`// GAP:` in the build script).

```
sbm new "samples\plans\Br02069 TH65.sbm"
sbm run "samples\plans\build-br02069.csx" "samples\plans\Br02069 TH65.sbm"   (from the repo root; re-runnable)
sbm run "samples\plans\check-br02069.csx" "samples\plans\Br02069 TH65.sbm"
```

**Horizontal datum (U1):** not stated in the plans (p34/p36/p37). The plan X/Y (easting/northing) are used as a LOCAL GRID in feet
(project foot); all plan distances and coordinates are reproduced, no geodetic meaning is attached.

## 1. What is modeled
| item | model input | plan source |
|---|---|---|
| Alignment SB65 | 2-point PI table = region R7 tangent POT 208+26.273 R7 -> PC 261+56.990 R7; starts at the last SB65 equation (208+26.909 R6 = 208+26.273 R7), so only R7 stations are carried; length 5330.717 ft, azimuth 0 deg 44'31.600" (plan 31.598"), PC misfit dY +0.001 ft | p36, p1 |
| Profile | VPI 249+00.000 EL 935.954, L 1250', grades +/-2.500 % carried to the alignment ends; VPC/VPT EL 920.329 reproduced | p1, p34 |
| Cross slope | crown at the working line, -0.02 both sides, constant | p2, p19 |
| Supports | South Abutment CL brg 248+34.75 (F1 fixed), North Abutment 249+34.00 (E1 expansion), skew 0, deck end 1'-10" from CL brg | p1, p3, p15 |
| Girders | 7 lines, first -21.00, 6 @ 6'-0"; Kind = prestressed | p3, p15 |
| Deck | 41'-0" (edges -23.50 / +17.50), slab 9", stool 4 1/4" | p1, p2, p3, p18, p19 |
| Barriers | SingleSlope 36" x 1'-6" base both edges (GAP Type S) | p1, p2, p24 |
| Beams | Custom PrecastIBeamDims ~30MH (H 30, top 34, bottom 39, web 6 1/2, verticals 5 / 1 5/8 / 12 7/16 / 5 7/16 / 5 1/2, chamfer 3/4), 44 x 0.6" Gr 300 straight strands from strands-30MH-B1.csv, f'c 9.5 / f'ci 8.0, 7 1/2" end to CL brg -> 100'-6" o-o; pad 12" x 30" x 3 1/4" | p16, p31 |
| Abutments | AbutmentDefinition per abutment: footing 59'-7" (-23.58..+36.00) x 6'-0" x 3'-0", bottom 917.75; infill wall 1'-6" (as backwall, front 1'-0" behind CL brg, top 927.73 S / 927.80 N); back ledge 922.35 / 922.15 (keyed zone); seat front 923.27 / 923.06 (wash); pedestals (beam steps) 2'-6" along the beam x 4'-8" S / 3'-6" N, tops from seat-elevations.csv; west mask wall 1'-5 1/2" to 930.61 / 930.67; 14 piles (2 rows x 7 @ 9'-5", 1'-6" from W end, rows 1'-9" behind / 1'-3" ahead of CL brg, 1'-0" embed, 95 / 90 ft) | p4, p5, p9, p10, p11 |

Build output (regeneration): 0 errors; 7 beams 100.500 ft, bearing span 99.250 ft, section area 630.1 in2 (approx. outline); deck area 4219.6 SF.
Concrete (model, approx. section): S abutment footing+stem 65.7 CY, infill 17.4 CY, mask 2.7 CY, pedestals 2.1 CY; N 63.4 / 18.2 / 2.7 / 1.6 CY.
The generator warns at all 7 North pedestals that the plan pedestal top is 3 1/4" below the model bearing seat (GAP bearing height).

## 2. Check results (tolerance 0.01 ft; stool 1/16"; deck area to the nearest SF)
```
SUMMARY: 71 PASS, 9 FAIL, 6 N/A (tolerance 0.01 ft; stool 1/16"; area nearest SF)
  Working points        16 pass   0 fail   0 n/a
  Top of roadway         8 pass   0 fail   0 n/a
  Bridge seat (W.P.)     2 pass   2 fail   0 n/a
  Pedestal tops         14 pass   0 fail   6 n/a
  Bearing seats          7 pass   7 fail   0 n/a
  Layout                20 pass   0 fail   0 n/a
  Stool                  4 pass   0 fail   0 n/a
```

FAIL rows (all from one GAP - a single prestressed bearing height per bridge: 3 1/4" (S, F1) used, North E1 is 6 1/2" -> N seats 0.273 ft high):
```
Group                Item                                              Plan        Model     Diff  Status  Note
Bridge seat (W.P.)   B G1 North Abutment                            923.550      923.823   +0.273  FAIL    model seat = bottom of bearing; GAP one bearing height (3 1/4") per bridge, N plan L = 6 1/2"
Bridge seat (W.P.)   F G7 North Abutment                            923.670      923.943   +0.273  FAIL    model seat = bottom of bearing; GAP one bearing height (3 1/4") per bridge, N plan L = 6 1/2"
Bearing seats        N G7 off +15.00 model seat                     923.670      923.943   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Bearing seats        N G6 off +9.00 model seat                      923.790      924.063   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Bearing seats        N G5 off +3.00 model seat                      923.910      924.183   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Bearing seats        N G4 off -3.00 model seat                      923.910      924.183   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Bearing seats        N G3 off -9.00 model seat                      923.790      924.063   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Bearing seats        N G2 off -15.00 model seat                     923.670      923.943   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Bearing seats        N G1 off -21.00 model seat                     923.550      923.823   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
```
N/A rows: the 6 future pedestals (+21/+27/+33 at each abutment) are not modeled (GAP).

Passing groups: working points A-F, CP1, CP2 X/Y (max 0.001 ft; A-F taken from the framing CL-bearing points, C/D/CP from the alignment);
top of roadway at A-F, begin / end bridge (max 0.004 ft vs the 0.01 ft plan values); South bridge seats (+0.001 ft); pedestal tops
(plan inputs); span 99.250 (stations and all 7 beams); beam length 100.500; begin / end of slab 248+32.917 / 249+35.833 (plan 248+32.92 /
249+35.83); deck edges -23.50 / +17.50; deck area 4220 SF; stool 4 1/4" at the CL bearings of G1 / G7.

<details><summary>Full check table</summary>

```
Group                Item                                              Plan        Model     Diff  Status  Note
Working points       A X (248+34.75, -21.00)                     508170.642   508170.642    0.000  PASS    CL brg G1 (framing)
Working points       A Y                                         153820.439   153820.440   +0.001  PASS    CL brg G1 (framing)
Top of roadway       A 248+34.75 off -21.00                         927.640      927.636   -0.004  PASS    PG - 0.02|off| (deck finished grade)
Bridge seat (W.P.)   A G1 South Abutment                            923.760      923.761   +0.001  PASS    model seat = bottom of bearing
Working points       B X (249+34.00, -21.00)                     508171.928   508171.928    0.000  PASS    CL brg G1 (framing)
Working points       B Y                                         153919.681   153919.681    0.000  PASS    CL brg G1 (framing)
Top of roadway       B 249+34.00 off -21.00                         927.700      927.698   -0.002  PASS    PG - 0.02|off| (deck finished grade)
Bridge seat (W.P.)   B G1 North Abutment                            923.550      923.823   +0.273  FAIL    model seat = bottom of bearing; GAP one bearing height (3 1/4") per bridge, N plan L = 6 1/2"
Working points       C X (248+34.75, 0.00)                       508191.640   508191.640    0.000  PASS    alignment sta/off
Working points       C Y                                         153820.167   153820.168   +0.001  PASS    alignment sta/off
Top of roadway       C 248+34.75 off 0.00                           928.060      928.056   -0.004  PASS    PG - 0.02|off| (deck finished grade)
Working points       D X (249+34.00, 0.00)                       508192.926   508192.926    0.000  PASS    alignment sta/off
Working points       D Y                                         153919.409   153919.409    0.000  PASS    alignment sta/off
Top of roadway       D 249+34.00 off 0.00                           928.120      928.118   -0.002  PASS    PG - 0.02|off| (deck finished grade)
Working points       E X (248+34.75, +15.00)                     508206.639   508206.639    0.000  PASS    CL brg G7 (framing)
Working points       E Y                                         153819.973   153819.973    0.000  PASS    CL brg G7 (framing)
Top of roadway       E 248+34.75 off +15.00                         927.760      927.756   -0.004  PASS    PG - 0.02|off| (deck finished grade)
Bridge seat (W.P.)   E G7 South Abutment                            923.880      923.881   +0.001  PASS    model seat = bottom of bearing
Working points       F X (249+34.00, +15.00)                     508207.925   508207.925    0.000  PASS    CL brg G7 (framing)
Working points       F Y                                         153919.215   153919.215    0.000  PASS    CL brg G7 (framing)
Top of roadway       F 249+34.00 off +15.00                         927.820      927.818   -0.002  PASS    PG - 0.02|off| (deck finished grade)
Bridge seat (W.P.)   F G7 North Abutment                            923.670      923.943   +0.273  FAIL    model seat = bottom of bearing; GAP one bearing height (3 1/4") per bridge, N plan L = 6 1/2"
Top of roadway       BEGIN 248+32.92 off 0.00                       928.050      928.052   +0.002  PASS    PG - 0.02|off| (deck finished grade)
Top of roadway       END 249+35.83 off 0.00                         928.120      928.116   -0.004  PASS    PG - 0.02|off| (deck finished grade)
Working points       CP1 X (248+97.047, 0.00)                    508192.447   508192.447    0.000  PASS    alignment sta/off
Working points       CP1 Y                                       153882.459   153882.460   +0.001  PASS    alignment sta/off
Working points       CP2 X (248+67.045, 0.00)                    508192.059   508192.059    0.000  PASS    alignment sta/off
Working points       CP2 Y                                       153852.460   153852.460    0.000  PASS    alignment sta/off
Pedestal tops        S ped 1 off +33.00 (future)                    923.520            -        -  N/A     GAP: future pedestals not modeled (p5/p10)
Pedestal tops        S ped 2 off +27.00 (future)                    923.640            -        -  N/A     GAP: future pedestals not modeled (p5/p10)
Pedestal tops        S ped 3 off +21.00 (future)                    923.760            -        -  N/A     GAP: future pedestals not modeled (p5/p10)
Pedestal tops        S ped 4 G7 as-designed step top                923.880      923.880    0.000  PASS    pedestal solid (plan input)
Bearing seats        S G7 off +15.00 model seat                     923.880      923.881   +0.001  PASS    PG - 2%|off| - 9 - 4 1/4 - 30 - 3 1/4"
Pedestal tops        S ped 5 G6 as-designed step top                924.000      924.000    0.000  PASS    pedestal solid (plan input)
Bearing seats        S G6 off +9.00 model seat                      924.000      924.001   +0.001  PASS    PG - 2%|off| - 9 - 4 1/4 - 30 - 3 1/4"
Pedestal tops        S ped 6 G5 as-designed step top                924.120      924.120    0.000  PASS    pedestal solid (plan input)
Bearing seats        S G5 off +3.00 model seat                      924.120      924.121   +0.001  PASS    PG - 2%|off| - 9 - 4 1/4 - 30 - 3 1/4"
Pedestal tops        S ped 7 G4 as-designed step top                924.120      924.120    0.000  PASS    pedestal solid (plan input)
Bearing seats        S G4 off -3.00 model seat                      924.120      924.121   +0.001  PASS    PG - 2%|off| - 9 - 4 1/4 - 30 - 3 1/4"
Pedestal tops        S ped 8 G3 as-designed step top                924.000      924.000    0.000  PASS    pedestal solid (plan input)
Bearing seats        S G3 off -9.00 model seat                      924.000      924.001   +0.001  PASS    PG - 2%|off| - 9 - 4 1/4 - 30 - 3 1/4"
Pedestal tops        S ped 9 G2 as-designed step top                923.880      923.880    0.000  PASS    pedestal solid (plan input)
Bearing seats        S G2 off -15.00 model seat                     923.880      923.881   +0.001  PASS    PG - 2%|off| - 9 - 4 1/4 - 30 - 3 1/4"
Pedestal tops        S ped 10 G1 as-designed step top               923.760      923.760    0.000  PASS    pedestal solid (plan input)
Bearing seats        S G1 off -21.00 model seat                     923.760      923.761   +0.001  PASS    PG - 2%|off| - 9 - 4 1/4 - 30 - 3 1/4"
Pedestal tops        N ped 1 off +33.00 (future)                    923.310            -        -  N/A     GAP: future pedestals not modeled (p5/p10)
Pedestal tops        N ped 2 off +27.00 (future)                    923.430            -        -  N/A     GAP: future pedestals not modeled (p5/p10)
Pedestal tops        N ped 3 off +21.00 (future)                    923.550            -        -  N/A     GAP: future pedestals not modeled (p5/p10)
Pedestal tops        N ped 4 G7 as-designed step top                923.670      923.670    0.000  PASS    pedestal solid (plan input)
Bearing seats        N G7 off +15.00 model seat                     923.670      923.943   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Pedestal tops        N ped 5 G6 as-designed step top                923.790      923.790    0.000  PASS    pedestal solid (plan input)
Bearing seats        N G6 off +9.00 model seat                      923.790      924.063   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Pedestal tops        N ped 6 G5 as-designed step top                923.910      923.910    0.000  PASS    pedestal solid (plan input)
Bearing seats        N G5 off +3.00 model seat                      923.910      924.183   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Pedestal tops        N ped 7 G4 as-designed step top                923.910      923.910    0.000  PASS    pedestal solid (plan input)
Bearing seats        N G4 off -3.00 model seat                      923.910      924.183   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Pedestal tops        N ped 8 G3 as-designed step top                923.790      923.790    0.000  PASS    pedestal solid (plan input)
Bearing seats        N G3 off -9.00 model seat                      923.790      924.063   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Pedestal tops        N ped 9 G2 as-designed step top                923.670      923.670    0.000  PASS    pedestal solid (plan input)
Bearing seats        N G2 off -15.00 model seat                     923.670      923.943   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Pedestal tops        N ped 10 G1 as-designed step top               923.550      923.550    0.000  PASS    pedestal solid (plan input)
Bearing seats        N G1 off -21.00 model seat                     923.550      923.823   +0.273  FAIL    GAP: bearing height 3 1/4" for both ends (N plan 6 1/2")
Layout               Span 1 CL brg - CL brg (support stations)       99.250       99.250    0.000  PASS    
Layout               G1-S1 bearing span                              99.250       99.250    0.000  PASS    
Layout               G2-S1 bearing span                              99.250       99.250    0.000  PASS    
Layout               G3-S1 bearing span                              99.250       99.250    0.000  PASS    
Layout               G4-S1 bearing span                              99.250       99.250    0.000  PASS    
Layout               G5-S1 bearing span                              99.250       99.250    0.000  PASS    
Layout               G6-S1 bearing span                              99.250       99.250    0.000  PASS    
Layout               G7-S1 bearing span                              99.250       99.250    0.000  PASS    
Layout               G1-S1 beam length o-o                          100.500      100.500    0.000  PASS    p16 100'-6"
Layout               G2-S1 beam length o-o                          100.500      100.500    0.000  PASS    p16 100'-6"
Layout               G3-S1 beam length o-o                          100.500      100.500    0.000  PASS    p16 100'-6"
Layout               G4-S1 beam length o-o                          100.500      100.500    0.000  PASS    p16 100'-6"
Layout               G5-S1 beam length o-o                          100.500      100.500    0.000  PASS    p16 100'-6"
Layout               G6-S1 beam length o-o                          100.500      100.500    0.000  PASS    p16 100'-6"
Layout               G7-S1 beam length o-o                          100.500      100.500    0.000  PASS    p16 100'-6"
Layout               Begin bridge (end of slab S) station         24832.920    24832.917   -0.003  PASS    p1 248+32.92
Layout               End bridge (end of slab N) station           24935.830    24935.833   +0.003  PASS    p1 249+35.83
Layout               Deck left edge offset                          -23.500      -23.500    0.000  PASS    
Layout               Deck right edge offset                          17.500       17.500    0.000  PASS    
Layout               Deck area (SF, SOQ nearest SF)                  4220.0       4220.0    0.000  PASS    model 4219.6 SF = 41'-0" x 102'-11"
Stool                G1-S1 stool at CL brg S (in)                    4.2500       4.2500    0.000  PASS    p3 4 1/4"; tolerance 1/16"
Stool                G1-S1 stool at CL brg N (in)                    4.2500       4.2500    0.000  PASS    p3 4 1/4"; tolerance 1/16"
Stool                G7-S1 stool at CL brg S (in)                    4.2500       4.2500    0.000  PASS    p3 4 1/4"; tolerance 1/16"
Stool                G7-S1 stool at CL brg N (in)                    4.2500       4.2500    0.000  PASS    p3 4 1/4"; tolerance 1/16"
```
</details>

## 3. GAP list (program feature missing -> closest input used)
| # | plan feature | what the model does | feature needed |
|---|---|---|---|
| 1 | MnDOT 30MH beam (p16) | TxGirderType.Custom + PrecastIBeamDims approximation: straight segments, no 6" R fillets / 1" and 2" R corners, bottom-flange 1 3/8" edge flat folded into the taper, 4 1/8" level ledge at the web | MnDOT 30MH in the beam catalog (in progress on another branch) - then set Type to it |
| 2 | Debonding 14 of 44 strands (A 4 @ 9'-0", B 6 @ 13'-0", C 4 @ 17'-0" each end, p16) | strands run full length (debond column of strands-30MH-B1.csv read and counted only) | debond length per StrandPoint (in progress on another branch) |
| 3 | Curved-plate bearings F1 (B310, L 3 1/4") / E1 (B311, L 6 1/2") (p31) | elastomeric pad 12" x 30" with the thickness = 3 1/4" for BOTH abutments -> North seats 0.273 ft high (9 FAIL rows) | per-support bearing height for prestressed beams / curved-plate bearing assembly |
| 4 | Sole plate B303 (p16, p30; U6) | not modeled | sole plate for prestressed beams |
| 5 | Camber / variable stool (p16; U10) | straight beams, constant 4 1/4" stool (plan value at CL brg) | prestressed camber in the haunch |
| 6 | Concrete end diaphragms, semi-integral ends (p15, p17, p20, p21; U2) | not modeled; deck end 1'-10" overlaps the infill wall zone (reported) | concrete end diaphragm / semi-integral abutment |
| 7 | Barrier Type S 36" (p24) | SingleSlope 36" x 1'-6" base, top width = program default 6" (not read) | MnDOT Type S profile |
| 8 | Barrier bars R501M..R612M (p24) | barrier reinforcement off | plan barrier bars |
| 9 | Deck bars S401M..S404M (p18, p19, p21; U4) | deck reinforcement off (generic bars would be invented) | data entry into DeckPlanReinforcement (feature exists) |
| 10 | Beam mild steel G301E..G806E, stirrups, optional top strands (p16) | not modeled | prestressed beam reinforcing |
| 11 | Abutment stem with front face 1'-3" behind the footing front, seat 1" slope (p11 A-A) | wash (seat level) runs to the footing front -> front toe built to the seat; seat level at the seat-front elevation | MnDOT stem / seat / back-ledge section in AbutmentDefinition |
| 12 | Infill wall without corbel (p11) | Iowa corbel set to zero size; three backwall pours remain as solid splits | optional corbel / plain wall |
| 13 | 3 future Type B pedestals per abutment (p5, p10) | not built (steps only under bearings) | standalone / future pedestals |
| 14 | 12" CIP pipe piles, 1/4" shell (p4, p9, p29) | 12" square precast concrete piles | round CIP pipe pile type |
| 15 | Test piles 105 / 100 ft (p4, p9) | all piles 95 (S) / 90 (N) ft | per-pile length |
| 16 | Abutment bars A/B series (bar-lists.csv; p6-p8, p12-p14) | substructure reinforcement off (Iowa placements would be invented) | MnDOT abutment bar placement |

## 4. UNRESOLVED (plan items not stated / not read)
- U1 horizontal datum (p34/36/37) - local grid used.
- U2 end diaphragm geometry (p17/20/21) - not modeled.
- U3 bend dimensions (p8/14/21/24) - bars not modeled.
- U4 deck line counts vs spacing (p19) - deck bars not modeled.
- U6 sole plate B303 dims (p30).
- U7 pedestal Type A/B details (p6) - pedestals are plain blocks at the plan size and top.
- U8 pile tip elevations (p4/p9) - piles hang the contract length from the head.
- U9 mask / infill wall east-end and corner extents (p3/5/10/11) - only the west mask wall is built; infill wall runs from it to the footing east end (+36.00).
- U10 camber shape (p16).
- Barrier Type S top width / face breaks (Fig. 5-397.138(A), p24) not read.

