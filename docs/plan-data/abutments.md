# Abutment plan data (Iowa DOT Design 116 WB I-80, Design 316 Ramp G)

Re-read at 200-500 dpi grayscale clips (second pass; supersedes the first, ~110 dpi pass). Values are ft-in as printed; elevations
exact. "pN" = PDF page N; "DS n" = printed "DESIGN SHEET NO. n" (PDF = DS + 2 for both sets; printed SHEET NUMBER = PDF - 1).
Items marked UNCERTAIN are best readings; "DERIVED" = computed here from printed values (not printed). Scratch clips and the render
script are in `temp\abut-verify\`.

Sheet mapping (verified from title blocks):
- Design 116 (B00168): West abutment p85-88 (DS 83-86), East abutment p89-92 (DS 87-90), Abutment Aesthetic p93 (DS 91).
- Design 316 (B00169): South abutment p80-83 (DS 78-81), Aesthetic p84 (DS 82).
- Content per abutment: sheet 1 = part plan + rear elevation + part section + anchor-bolt layout (West: p85 plan/rear elev, the
  section is on p86); sheet 2 = piling layout + pile uplift anchor detail (West p86, East p90, Ramp G p81; the East section is on p89);
  sheet 3 = wingwall views/sections + maskwall (West p87, East p91, Ramp G p82); sheet 4 = bent bars, bar list, abutment notes,
  H-H/J-J counterfort sections, concrete quantities (West p88, East p92, Ramp G p83).

General (Verified): all three are the same TYPE - pile-supported spread-style footing + vertical backwall with a corbel/paving notch
on the BACK face, beam steps (seat pedestals) on the footing top in FRONT of the backwall, modular (I-80) / strip-seal (Ramp G)
expansion device in the upper backwall (upper backwall poured after the device is set), full-height wingwalls as SEPARATE placements
doweled to the backwall and carried on 2 counterforts + 2 wing footings per wing (4 wing footings per abutment), and a maskwall (end
panel). Not MSE, not integral. HP14x73 vertical piles (no battered piles drawn anywhere).
Abutment Notes (p88 / p92 / p83 - same text; verified): standard abutment details modified to OFFSET THE FOOTING FROM THE BACKWALL
to tie the reinforcing; min clear 2" unless noted; maskwall poured before the slab; construction-joint keyways = beveled 2x8 (the
backwall-base keyway and maskwall keyway in the Ramp G section/C-C are 2x6); the part of the backwall holding the expansion-device
anchorage is placed AFTER the deck; concrete sealer on the bridge seat and the wash; beams and masonry plates set before the backwall
is placed; no welding of anchor bolts; barrier rails not shown in abutment details; paving-notch dowels are deformed stainless steel
grade 60 (5d5).
Bench marks: I-80 "BM 597 STA. 4298+67.763, 81.85' LT. FD IHC SE CORNER WING WESTBOUND I-80 BRIDGE OVER RR YARD, ELEV. 1012.698";
Ramp G "BM 510 STA. 4276+77.619, 254.61' LT. FD IHC BM IN SE WING I-80/I-29 OVER I-80 E., ELEV. 1007.367".

Corrections to the first-pass file (summary): West skew is 44 deg from RADIAL (not "46 deg from the normal"); the 6'-7 plan dim is
footing-front-to-backwall-back (NOT a pad; seat blocks are 4'-6 x 4'-1); West/East/Ramp G step pairing resolved; counterfort heights are
printed from BOTTOM of wing footing (see 1.5); the West/East K-K / A-A wing elevations were mixed up; the Ramp G wing is 22'-6 from the
BACK FACE of the backwall (= 25'-5 from CL bearing); 5g2 bend dims; pile stations for West and Ramp G are now exact; maskwall = end
panel resting on the footing (one per I-80 abutment, two at Ramp G).

---------------------------------------------------------------------------------------------------------------------

## 0. Cross-section common to all three (Verified; dims normal to the abutment line)

Coordinates: x = distance from the BACK FACE OF FOOTING toward the girders (front); y = height above BOTTOM OF FOOTING.

```
  BACK (approach fill)                                            FRONT (girder side)
   |<-1'-3->|                                (approach slab sits on the corbel ledge)
   +--------+  <- corbel top (paving notch)  .... top of upper backwall: ELEV @ CL bearing (ref), sloped 1'-6 deep face
   | 8" ledge|=======+                       ^ 1'-6 (W,E)  / 1'-1 (Ramp G)  top of backwall -> corbel ledge
   |        ||upper  |                       |
   |  1'-3  ||backwall 1'-6 thick            4'-0 (W,E)  /  3'-7 (Ramp G)  constant offset top -> bottom of taper
   |        ||(poured after expansion device)|
   \  1'-3 45deg taper (5d4)                 |
    \_______|  3" then construction joint (keyway 2x8 / Ramp G 2x6)
            |  lower backwall 1'-6 thick  (6d1 back face, 5d2 front face, 4d6 hairpins, 6g1 horizontals)
            |                                      +-----------+  beam step (pedestal), top = ELEV (see 1.3/2.3/3.3)
            |                                      | 5m1/5n1   |  step block: from backwall front face to 2'-3 beyond CL brg
            |_ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ +----+------+
       1.5" |  wash surface (seat base) = footing bottom + 4'-1  (1" WASH above the 4'-0 footing top)
    +-+-----+---+----------------+---------+--------------+
    | footing 4'-0 deep (4'-1 to wash)  piles: tops 2'-0 above bottom of footing; 3" clear to bars |
    +--------------------------------------------------------+
    x=0   1.5"  1'-7.5"        CL brg               step front     footing front
```
Dimension chain, I-80 (West and East identical): footing back -> backwall back face 1 1/2" ; backwall 1'-6 ; backwall front face -> CL
bearing *1'-10 ("this dimension may vary - tilting of the pavement support section may be needed to set the modular device opening");
CL bearing -> step front face 2'-3 ; step front -> footing front face 1'-0. Total 1.5" + 18" + 22" + 27" + 12" = 80.5" = 6'-8 1/2 (closes).
Ramp G: footing 6'-0 = 1 1/2" + 1'-6 + *1'-5 + (CL brg -> footing front 2'-11 1/2) (closes: 1.5+18+17+35.5 = 72"). Plan shows CL bearing ->
footing front face 3'-3 (I-80) / 2'-11 1/2 (Ramp G) and CL brg -> back face of backwall 3'-4 (I-80) / 2'-11 (Ramp G).
Footing cross-section width: I-80 6'-8 1/2 = 1'-6 + 3'-8 1/2 + 1'-6 (2 pile rows); Ramp G 6'-0 = 1'-6 + 3'-0 + 1'-6. Pile rows relative to CL
brg: I-80 back row 1'-11 1/2 behind, front row 1'-9 ahead (printed on East piling p90); Ramp G back row 1'-6 1/2, front row 1'-5 1/2 (DERIVED from
2'-11 1/2 and 1'-6 edges; the 1'-5 1/2 / 1'-6 1/2 strings are legible but small). Footing top is 4'-0 above bottom (4'-1 with the 1" wash in front
of the backwall); backwall bottom is 3'-10 above footing bottom (keyed: "3" below the wash, 1 1/2 key depth). Dimension 3'-10 = footing bottom to
bottom of backwall (printed, all three) and the elevation arithmetic confirms every step base = footing bottom + 4'-1 (checked on all steps).
Corbel/paving notch (back face; sections p86/p89/p80): corbel top ledge 1'-6 (West/East) / 1'-1 (Ramp G) below the top of the upper
backwall; ledge/notch width 8" measured from the upper-backwall back face (UNCERTAIN: the 8" and the 1'-3 may describe the ledge and the
projecting corbel respectively - corbel back face projects 1'-3 behind the upper-backwall back face, i.e. 1'-1 1/2 behind the footing back face);
corbel vertical face 1'-3, then a 1'-3 x 1'-3 (45 deg) taper back to the backwall back face (bar 5d4 follows it), then 3" to the construction joint;
1" chamfer at the corbel top-back corner; detail "C" = 1/8" radius on the notch edge; top of upper backwall carries a small drop: 3/8" (West,
East none printed) / 3/4" (Ramp G) (UNCERTAIN meaning: reads as the longitudinal grade over the 1'-6 thickness).
Reference elevation "ELEV x @ CL abut. brg." is the TOP of backwall at the crown line (West 1008.15; East 1018.71; Ramp G 1004.22 @ CL Ramp G);
four sloped lines in every rear elevation, parallel to the crown line = top of backwall, corbel ledge, bottom of vertical corbel face, bottom of
taper (the 4'-0 / 3'-7 offset is CONSTANT; the lower backwall height varies). Printed lower-backwall heights "to bottom of backwall at back
face": West 7'-5 1/4 (*), East 4'-9 1/2 (**), Ramp G 5'-6 1/8 (**).
Footing bar rows (Verified from section): top & bottom rows "6 EQ. SPA." (7 bars) West/East, "4 EQ. SPA." (5 bars) Ramp G; side faces "4 EQ. SPA."
(3 interior each) -> 20 rows (West/East, 2 laps x 20 = 40 bars of 9a1) and 16 rows (Ramp G, 8a1 single length) - matches the bar lists.
Pile tops are 2'-0 above the bottom of the footing (embedded 2'-0); printed "F.F. & B.F." min. dims 1'-10 / 1'-6 sit between pile top and
the backwall base (UNCERTAIN meaning: 3'-10 - 2'-0 = 1'-10 clear pile top -> backwall base; 1'-6 min = face-bar embedment).
Piles project below the footing (3" clear applies to bars). Steel pile points required.

---------------------------------------------------------------------------------------------------------------------

## 1. I-80 WB West abutment (Design 116, p85-88 = DS 83-86)  -- Verified

### 1.1 Location / plan (p85 PART PLAN VIEW; staking p15 = DS 13)
- CL West abutment bearing meets the CL WB approach roadway at STA. 7578+79.64, 41.08' LT. of I-80 (staking diagram p15); Pier 1WB is 207'-0 along
  the CL WB approach roadway from it (7580+86.64). Title block STATION 7595+67.24, 41.08' LT. is the structure reference, not the abutment.
- Skew 44 deg 00'00" measured from the RADIAL (normal to the approach CL) - verified: gutter-to-gutter along the bearing line 83'-4 7/8 x cos 44 =
  60.0 = the printed "60'-0 ROADWAY". Approach CL is straight here. In the p85 plan the bearing line rises to the up-right (NE); the NE end is
  the F-girder end.
- Gutter lines are 24'-0 (A / SW end) and 36'-0 (F / NE end) from the CL approach roadway (staking p15). Along the bearing line gutter-to-gutter 83'-4 7/8
  (= 30'-2 11/16 + 53'-2 3/16 printed on the FRONT-face line, split at CL roadway; = 33'-4 3/8 + 50'-0 9/16 along CL bearing from the CL roadway = 24'/cos44, 36'/cos44).
- Girders A-F, 5 spaces @ 15'-10 7/16 (+) = 79'-4 1/4 along the bearing line; gutter line to CL girder A and to CL girder F both 2'-0 5/16
  (check 2'-0 5/16 + 79'-4 1/4 + 2'-0 5/16 = 83'-4 7/8, closes). Girder positions along the bearing line from the A-end gutter point:
  A 2.026, B 17.896, C 33.766, D 49.636, E 65.505, F 81.375 ft (DERIVED).
- Footing/backwall ends are cut PARALLEL to the approach CL (line at 26'-11 / 37'-9 horizontal offsets from the CL approach CL, printed on p86):
  26'-11 = 24'-0 + 2'-11 on the A end (beyond the gutter line: 2'-11 = wing 1'-9 + maskwall zone 1'-2); 37'-9 = 36'-0 + 1'-9 on the F end.
  The acute corner of each footing end is chamfered (~1'-6 / 1'-9 on the F end). A end carries the MASKWALL; F end carries the BACKWALL EXTENSION.
- Seat blocks: 4'-6 along the abutment (2'-3 each side of CL girder) x 4'-1 normal (backwall front face to 2'-3 past CL brg; 6'-7 = footing front
  to backwall back, not a pad). "1" WASH (typ.)" between blocks. Bearing: disc (sheets elsewhere); anchor bolts 1 1/2" dia swedge, 4 bolts as a
  2'-6 x 1'-0 rectangle (1'-3 | 1'-3 and 6 | 6 from the CL; plan shows the long side along the abutment line - orientation UNCERTAIN), reinforcing 5m1 (transverse
  U, 3'-8 x 2'-3 legs) and 5n1 (longitudinal) in each step; hoop 5n2 at girders C & D only (the two tallest steps).
- Deck end: CL bearing to deck end 1'-4; 6" @ 50 F to the front face of the backwall (1'-10 - 1'-4 = 6", closes; slab p179 section B-B); blockout 1'-6 each side.

### 1.2 Rear elevation (p85) - bottom of footing LEVEL
- Bottom of footing 992.82 (level, full length). Bottom of backwall 996.65 (= +3'-10). Footing top 996.82; seat base/wash 996.90 (992.82 + 4'-1).
- Seat steps, left (A end) to right (F end), pitch = girder pitch. Each = step height above the seat base (prints "Δ - dependent on final bearing
  height"; elevations "to be determined by bearing manufacturer"):
  A 997.15 (3, LOW STEP) | B 997.51 (7 3/8) | C 997.87 (11 5/8) | D 997.78 (10 1/2) | E 997.62 (8 5/8) | F 997.45 (6 5/8)
  Check: 996.90 + h = 997.15, 997.52, 997.87, 997.78, 997.62, 997.45 (all within 0.01).
- Backwall: top = crown line (ref 1008.15 @ CL brg), 4 parallel sloped joint/ledge lines; backwall bottom 996.65 level. Backwall extension (F end, right): top
  ELEV 1007.65, bottom = footing bottom line (992.82); height 14'-10 3/8 (5d7 14'-5). Maskwall (A end, left): panel top = footing top (4'-1) + 14'-2 = 1011.07 (DERIVED).
- Backwall (between gutter-parallel end cuts) length: 6d1 171 spaces @ 6" = 85'-6 (172 bars) from the A end; 5d2 86 SPA @ 1'-0 (87 bars); 5d3/5d4 82 SPA @ 1'-0 (83);
  5d5 41 SPA @ 2'-0 = 82'-0 (42); 4d6 85 SPA @ 1'-0 = 85'-0 (86). 6g1: two bars lapped (min lap 2'-11) per row, 22 rows; 5g3 min lap 2'-2; 9a1 min lap 9'-1.
- Backwall total length along the skew from bar lengths: 2 x 45'-1 - 2'-11 + 4" = 87'-7 (6g1); footing 2 x 48'-4 - 9'-1 + 4" = 87'-11 (9a1) (DERIVED).

### 1.3 Piles (p86 WEST ABUTMENT PILING LAYOUT) -- dims at bottom of footing, along the footing CL (skew direction)
- 34 HP14x73, steel points; design bearing 122 tons; contract length 125 ft (4,250 LF, p4); required nominal 223 tons at end of drive (incl. 44 + 64 + 115);
  Pu = 244 kips; phi 0.55 soil / 0.7 rock (0.65 / 0.7 for construction control); downdrag 87 kips; tension 60 kips; never embedded < 55 ft. Tip elevation not printed
  (DERIVED: top of pile = 994.82; contract tip ~869.8).
- 13 stations x 2 rows (rows normal to the bearing line at 3'-8 1/2 apart, 1'-11 1/2 behind / 1'-9 ahead of CL brg) = 26, + 8 wing-footing piles = 34 (title/quantity match).
  Spacing 6'-10 throughout: SW group "4 PILE SPACES @ 6'-10 = 27'-4" (5 stations), central space 6'-10 split by the CL approach roadway as 6'-2 13/16 (SW) + 7 3/16" (NE),
  NE group "7 PILE SPACES @ 6'-10 = 47'-10" (8 stations). Along-CL positions from the CL-roadway/pile-line crossing (DERIVED): SW -6.234, -13.068, -19.901, -26.734, -33.568;
  NE +0.599, 7.432, 14.266, 21.099, 27.932, 34.766, 41.599, 48.432 ft. First/last pile to the footing end: ~3'-10 / ~4'-0 along the CL (DERIVED from 26'-11, 37'-9).
- Pile orientation drawn: flanges parallel to the bearing line, web normal to it (all three abutments).
- 5b1 hoops: per 6'-10 pitch a hoop 9" past each station, 8 spaces @ 8" = 5'-4 (9 hoops per pitch x 12 pitches = 108 = list count); end zones 4 SPA @ 1'-0 = 4'-0 with 5 x 5b2 each end (10 = list);
  5b3 x5 (NE end) and 5b4 x5 (SW end) "place with 9a1 bars". Pile uplift anchors 8j3: V-bars (2 per circled pile; 8 bars = the 4 circled wing-footing piles).
- Wing footing piles: each wing footing 7'-0 x 3'-0, 2 piles 4'-0 apart, 1'-6 from each end, one pile circled; footing centres 15'-9 and 25'-9 from CL brg along the gutter line (10'-0 apart).

### 1.4 Wingwalls (p85 plan; p87 views A-A, K-K, sections E-E, F-F; p88 H-H, J-J)
- Two wingwalls, each parallel to the approach CL, thickness 1'-9 with its FILL-SIDE face ON THE GUTTER LINE (wall outboard of the gutter by 1'-9; counterforts on the fill side).
  Length 31'-9 measured along the gutter line from the CL bearing line: 14'-3 | 3'-0 | 7'-0 | 3'-0 | 4'-6 = 31'-9 (both wings; closes). Near end meets the
  backwall back face (keyway beveled 2x8; dowels 6g4 6'-6 [5'-6 + 1'-0 hook], 44 total; 6f4 6'-0 into footing end 2'-0).
- A-A (SW wing, A end): top 1007.32 at the backwall -> 1006.97 at the free end (3" detail at end); end height 4'-0 (end bottom 1002.97 DERIVED); wing bottom 993.82 at backwall
  (level 1'-6, then sloped ~0.35 up to the free end, bars 6f3); height at backwall 13'-6. Wing footing tops 996.68 (No.1) and 1000.27 (No.2); keyway beveled 2x8; 5h1 dowels 2'-0 each way.
- K-K (NE wing, F end): top 1007.65 at the backwall -> 1007.30 at the free end; end height 4'-0 (end bottom 1003.30); bottom 994.17 at backwall (level 1'-6); footing tops 997.41 (No.1) and 1000.78 (No.2).
- Counterforts (E-E/F-F/H-H): No.1 centre 15'-9, No.2 centre 25'-9 from CL brg; plan 3'-0 wide (7" | 1'-10 | 7" at the outline) x projecting 3'-6 behind the wall face (footing 3'-9: 3" offset),
  heights ABOVE footing top: No.1 8'-10, No.2 5'-4 (overall incl. 3'-0 footing 11'-10 / 8'-4); bars 6h2 (No.1) / 6h3 (No.2) verticals, 5h4 hoops (9 equal spaces, 10 bars No.1; 5 equal spaces, 6 bars No.2), 5h1 dowels
  (6 per counterforts: 3 across at 1'-0, 6" in). Counterfort tops ~1.5 ft below the wing top (dashed in views).
- Wing footing (H-H): 7'-0 normal to the wall (3'-9 fill side + 1'-9 wall + 1'-6 outside) x 3'-0 along the wall x 3'-0 deep; pile top 1'-0 above footing bottom; 3" bar clear; bars 5j1 (4 per face), 5j2 (8, transverse), 8j3.
  Footing bottom = top - 3'-0: A-A 993.68 / 997.27; K-K 994.41 / 997.78 (DERIVED).
- Wall bars: A-A (SW wing) 5e1 vertical (13'-1 max ... 3'-9 min; 25 SPA @ 1'-0 = 26 bars x 2 faces = 52), 6f1 horizontal full (25'-8, 8), 6f2 horizontal varies (24'-8 ... 5'-2, 16), 6f3 bottom (26'-11, 2); K-K (NE wing) 5e4 (12'-10 ... 3'-9; 26 SPA = 27 x 2 = 54),
  6f5 (27'-0, 8), 6f6 (25'-11 ... 5'-0, 16), 6f7 (28'-5, 2); 4" clear to the wall faces; PVC 3" dia drain pipe through each wing, plugged with foam.
- Maskwall (A end, one): end panel 10" thick, 10'-4 13/16 wide x 14'-2 high above the footing (4'-1 lower portion = footing); recessed aesthetic panel with 1'-0 borders (1'-6 at top), recess 1 1/2" (3 1/2" clr.), keyway 2x6 at base, 1'-6 min bar embed; 5e2 verticals 21 x 15'-6
  (20 SPA @ ~ = 9'-10 13/16), 5e3 horizontals 30 x 10'-0 (15 per face, equal spaces); 5g2 dowels to backwall (22 = one per 6g1 row).
- Backwall extension (F end, one): ELEV 1007.65 top, to footing-bottom line; bars 5d7 11 x 14'-5 (6 back face @ 1'-0 + 5 front face), 4d8 hairpins 6, 6g5 horizontals 32 x 8'-0 (11 + 5 at each face per View B-B), PVC waterstop ribbed, center bulb. Plan dims printed at the F end:
  4'-6 3/8 | 2'-9 3/8 | 1'-9 (horizontal from the gutter line), 7'-5 9/16 and 3'-9 7/8 (UNCERTAIN which edges they span).

### 1.5 Reinforcing (p88) -- all epoxy except 5d5 stainless. Every row re-read at 230 dpi: identical to the first pass except as noted.
| Bar | Location | No. | Length | Wt | Bend / note |
|---|---|---|---|---|---|
| 9a1 | Footing, longitudinal | 40 | 48'-4 | 6,573 | straight; min lap 9'-1; 20 rows x 2 |
| 5b1 | Footing, hoops | 108 | 20'-7 | 2,319 | 6'-4 1/2 x 3'-5 box + 6" hook; D=2 1/2 |
| 5b2 | Footing, hoops (end) | 10 | VARIES | 168 | 2'-0 (min) to 6'-3 (max) wide x 3'-5 + 6" hook |
| 5b3 | Footing, end, horizontal | 5 | 11'-7 | 60 | zigzag: 7'-3 vertical, 1'-4 + 1'-1 legs, two 1'-6 diagonals (1'-0 1/2 rise) |
| 5b4 | Footing, end, horizontal | 5 | 19'-0 | 99 | 10'-0 vertical, 4'-6 top leg, 1'-4 / 2'-3 bottom, 3'-2 diagonal (2'-2 1/2 rise) |
| 6d1 | Backwall, B.F., vertical | 173 | 13'-1 | 3,400 | straight |
| 5d2 | Backwall, F.F., vertical | 88 | 12'-9 | 1,170 | straight |
| 5d3 | Paving notch | 83 | 4'-5 | 382 | 2'-5 x 1'-0 legs; D=2 1/2 |
| 5d4 | Paving notch | 83 | 3'-8 | 317 | 1'-0 leg, 1'-5 slope, 1'-0 / 1'-3 horizontal |
| 4d6 | Backwall hairpin | 86 | 8'-10 | 507 | 1'-2 wide x 3'-10 legs; D=2 |
| 5d7 | Backwall ext., vertical | 11 | 14'-5 | 165 | straight |
| 4d8 | Backwall ext., hairpin | 6 | 3'-2 | 13 | 1'-2 x 1'-0 legs |
| 5e1 | Wingwall (SW), vertical | 52 | VARIES | 456 | 13'-1 ... 3'-9 |
| 5e2 | Maskwall, vertical | 21 | 15'-6 | 339 | |
| 5e3 | Maskwall, horizontal | 30 | 10'-0 | 313 | |
| 5e4 | Wingwall (NE), vertical | 54 | VARIES | 467 | 12'-10 ... 3'-9 |
| 6f1 | Wingwall, horizontal | 8 | 25'-8 | 308 | |
| 6f2 | Wingwall, horizontal | 16 | VARIES | 358 | 24'-8 ... 5'-2 |
| 6f3 | Wingwall, bottom | 2 | 26'-11 | 81 | follows sloped bottom |
| 6f4 | Wingwall, dowels | 8 | 6'-0 | 72 | 2'-0 into footing end |
| 6f5 | Wingwall, horizontal | 8 | 27'-0 | 324 | |
| 6f6 | Wingwall, horizontal | 16 | VARIES | 371 | 25'-11 ... 5'-0 |
| 6f7 | Wingwall, bottom | 2 | 28'-5 | 85 | |
| 6g1 | Backwall, longitudinal | 44 | 45'-1 | 2,979 | min lap 2'-11; 22 rows x 2 |
| 5g2 | Backwall, dowels | 22 | 4'-7 | 105 | 10" leg, 1'-3, then 1'-9 1/2 rise, 2'-6 diag., 1'-9 horizontal; D=3 3/4 (one at each 6g1) |
| 5g3 | Paving notch, longit. | 4 | 42'-7 | 178 | min lap 2'-2 |
| 6g4 | Wingwall, dowels | 44 | 6'-6 | 430 | 5'-6 + 1'-0 leg; D=4 1/2 |
| 6g5 | Backwall ext., horiz. | 32 | 8'-0 | 385 | |
| 5h1 | Counterfort, dowels | 24 | 4'-0 | 100 | 2'-0 into footing + 2'-0 into counterfort; 4 counterforts x 6 |
| 6h2 | Counterfort No.1, vert. | 20 | 11'-5 | 343 | 2 x 10 |
| 6h3 | Counterfort No.2, vert. | 20 | 7'-11 | 238 | 2 x 10 |
| 5h4 | Counterfort, horizontal | 32 | 12'-0 | 401 | 6" tails, 1'-6 x 4'-9; 2 x 10 + 2 x 6 |
| 5j1 | Wing footing, longit. | 32 | 11'-6 | 384 | 6'-8 x 2'-5 legs; D=3 3/4; 4 footings x 8 |
| 5j2 | Wing footing, transv. | 32 | 11'-6 | 384 | 2'-8 x 2'-7 hoop + 6" hook; 8 per footing |
| 8j3 | Pile uplift anchors | 8 | 5'-0 | 107 | V: 1'-4 + 1'-4 wide, 2'-1 1/2 high, 2'-6 legs; D=6 |
| 5m1 | Beam steps, transverse | 24 | 8'-2 | 204 | 3'-8 x 2'-3 legs; D=2 1/2 (4 per step) |
| 5n1 | Beam steps, longitudinal | 24 | 4'-2 | 104 | straight (4 per step) |
| 5n2 | Beam steps, hoop | 2 | 16'-10 | 35 | 4'-2 x 3'-9 + 6" hook; girders C & D only |
| 5d5 | Paving notch dowels (stainless) | 42 | 3'-6 | 153 | |
Epoxy 24,724 lb; stainless 153 lb. Concrete (cy): footing and steps 92.4, backwall below c.j. 35.3, above 27.0, wingwalls 38.8, maskwall 4.1, wing ftgs 9.3, extension 3.8 = 210.7 (closes).
Count checks: 5m1 24 = 6 steps x 4; 5n1 24 = 6 x 4; 6g1 44 = 22 rows x 2 laps; 5g2 22 = 22 rows; 5b1 108 = 12 pitches x 9; 5h4 32 = 2 x 10 + 2 x 6; 6g4 44 = 22 per wing; 5e1 52 = 26 x 2, 5e4 54 = 27 x 2.
MISMATCHES: plan placement strings give 172 x 6d1 and 87 x 5d2, list gives 173 and 88 (+1 each: end/maskwall corner bars, not placed in the strings); 5d3/5d4 83, 5d5 42, 4d6 86 agree.

---------------------------------------------------------------------------------------------------------------------

## 2. I-80 WB East abutment (Design 116, p89-92 = DS 87-90)  -- Verified

### 2.1 Location / plan (p89 PART PLAN VIEW; staking p17 = DS 15)
- CL East abutment bearing meets the CL WB approach roadway at STA. 7612+54.84, 41.08' LT. of I-80 (stationing on I-80 CL R=2400'-0; approach CL R = 2358'-11; Pier 15WB 160'-0 1/2 back along the approach CL at 7610+92.01).
- Skew 84 deg 33'17" between the bearing line and the approach CL; "5 deg 26'43" (to local tangent)" = skew from radial, verified by 78'-4 3/16 (girder + end offsets along the line) x cos 5.45 = 78.0 = "78'-0 ROADWAY".
  Sign: bearing line rises to the right (toward the A end) in the p89 plan; map to alignment sense with the station direction (UNCERTAIN).
- Gutter lines: 36'-0 (J end) and 42'-0 (A end) from the CL approach roadway (roadway 78'-0); I-80 CL is 41'-1 from the approach CL. Wing gutter radii: J side R = 2394'-11, A side R = 2316'-11 (= 2358'-11 + 36'-0 / - 42'-0).
- Girders J, I, H, G (CL approach & CL G), F, E, D, A (8). J..G 3 spaces @ 11'-6 5/8 (-) = 34'-7 7/8; G..D 3 spaces @ 11'-6 5/8 (+) = 34'-7 7/8; D..A 6'-0 1/16 (+); CL girder to gutter 1'-6 1/16 (J) and 1'-6 5/16 (A).
  From the J-end gutter point along the line: J 1.505, I 13.057, H 24.609, G 36.161, F 47.713, E 59.265, D 70.817, A 76.818 ft (DERIVED).
- Footing ends: 37'-11 (J side of CL approach) and 45'-1 1/2 (A side) measured radially (these equal 36'-0 + 1'-9 and 42'-0 + 2'-11 over cos 5.45). A end = maskwall end (2'-11 beyond gutter),
  J end = backwall-extension end (1'-9 beyond gutter). Footing length 83'-0 1/2 (printed).
- Seat blocks same as West (2'-3 | 2'-3 each side of CL girder x 4'-1), 5m1 U-bars 3'-9 x 2'-0 legs (7'-9), 5n1; no 5n2.
- Deck end: CL brg to deck end 1'-4, 6" @ 50 F to backwall front face (same chain 1'-10 - 1'-4).

### 2.2 Rear elevation (p89) - footing SLOPED
- Bottom of footing 1008.27 at the J end of the footing, 1003.44 at the A end, linear (5.82% over 83'-0 1/2); parallel top; continues to 1008.46 at the end of the backwall extension (DERIVED check: slope x 3'-3). Backwall extension (J end): top 1021.09, bottom 1008.46.
- Seat base = footing bottom + 4'-1 following the slope; steps typ. 5" above it ("5" STEP (TYP.) Δ"; check: 5.3-5.5" at J and A). Step top elevations J to A: 1012.58, 1011.91, 1011.23, 1010.57, 1009.89, 1009.22, 1008.54, 1008.20 (A = LOW STEP).
- Top of backwall 1018.71 at CL brg (ref); lines: 4'-0 (to bottom of taper) + **4'-9 1/2 (to backwall bottom at back face) at the crown line; 1'-6 between the first two lines. Same keyways/joints as West.
- Bar placement: 6d1 159 SPA @ 6" = 79'-6 (160), 5d2 79 SPA @ 1'-0 (80), 5d3/5d4 78 SPA @ 1'-0 = 78'-0 (79), 5d5 38 SPA @ 2'-0 = 76'-0 (39), 4d6 79 SPA @ 1'-0 = 79'-0 (80). 6g1 min lap 2'-11, 5g3 min lap 2'-2.

### 2.3 Piles (p90)
- 36 HP14x73 (4,140 LF p4); design bearing 138 tons; contract length 115 ft; required nominal 270 tons (70 + 101 + 99); Pu = 276 kips; phi 0.55 / 0.7; downdrag 140 kips; tension 60 kips; min embedment 50 ft.
- 14 stations x 2 = 28 + 8 wing = 36. Spacing 6'-0 along the footing: end 2'-6 1/8, "5 SPA @ 6'-0 = 30'-0" (6 stations), 5'-4 7/8 to the CL approach roadway + 7 1/8" (= 6'-0 centre space),
  "7 SPA @ 6'-0 = 42'-0" (8 stations), end 2'-6 3/8. Check: 2'-6 1/8 + 30'-0 + 5'-4 7/8 = 37'-11; 7 1/8" + 42'-0 + 2'-6 3/8 = 45'-1 1/2; total 83'-0 1/2 (closes).
  Stations from the J end of the footing: 2.510, 8.510, 14.510, 20.510, 26.510, 32.510, 38.510, 44.510, ... 80.510 ft (DERIVED); footing end 83.04.
- 5b1 hoops: pitch 6'-0 = 8" + 7 SPA @ 8" = 4'-8 + 8" (8 per pitch x 13 = 104) + 2 SPA @ 8" = 1'-4 at each end (3 each, 6) = 110 = list; 5b2 x3 at each end (6) "place with 9a1".
- Wing footing piles: footing centres 11'-0 and 21'-0 from CL brg along the gutter line (10'-0 apart); 7'-0 x 3'-0, piles 4'-0 apart, 1'-6 from the ends.

### 2.4 Wingwalls (p91)
- Each wing 27'-0 along the gutter line from CL brg: 9'-6 | 3'-0 | 7'-0 | 3'-0 | 4'-6 = 27'-0 (closes); plan-curved at the gutter radii above; thickness 1'-9, fill-side face on the gutter line; "23 SPA @ 1'-0 = 23'-0" wall bars + 4" at the end.
- K-K (J wing): top 1020.80 at backwall -> 1020.67 at free end; end height 4'-0 (end bottom 1016.67); bottom 1008.80 at the backwall (height 12'-0, slope begins at the face - no level reach; 2'-0 dowel embed 6f4); footing tops 1010.78 (No.1) and 1014.15 (No.2).
- A-A (A wing): top 1016.24 at backwall -> 1016.11 at free end (end bottom 1012.11); bottom 1004.24 at backwall (12'-0); footing tops 1006.29 and 1009.61.
- Counterforts: No.1 centre 11'-0, No.2 21'-0 from CL brg; heights above footing top 8'-8 (No.1) / 5'-4 (No.2) (overall 11'-8 / 8'-4); hoops 5h4: 8 equal spaces 9 bars (No.1) / 5 equal spaces 6 bars (No.2); else as West.
- Bars: 5e1 96 VARIES (11'-7 max ... 3'-9) = 24 x 2 per wing; 6f1 16 x 23'-3; 6f2 28 VARIES (22'-1 ... 4'-3); 6f3 4 x 24'-5; 6f4 12 x 5'-3; 6g4 36 x 5'-5 (4'-5 + 1'-0).
- Maskwall (A end, one): 6'-8 7/8 wide (= footing width / cos skew) x 12'-4 above the 4'-1 footing; recessed panel 1'-0 borders, 1'-6 top, recess 1 1/2" (D-D: 1'-0 | 4'-8 7/8 | 1'-0); 5e2 15 x 13'-8 (14 SPA @ ~5 1/2 = 6'-2 7/8), 5e3 26 x 6'-4 (13 per face); 5g2 18 (= 6g1 rows). Top = 1003.44 + 4.083 + 12.333 = 1019.86 (DERIVED).
- Backwall extension (J end): top 1021.09, 5d7 8 x 12'-2, 4d8 4, 6g5 28 x 6'-5 (View B-B: 3 SPA @ ABT. 1'-0, 4-5d7 (E.F.) & 4-4d8; 9 + 5 spaced 6g5 per face).

### 2.5 Reinforcing (p92) -- re-read; matches the first pass except shape notes
9a1 40 x 45'-11 (6,245); 5b1 110 x 20'-7 (2,362); 5b2 6 x 9'-3 FOOTING END HORIZONTAL (58; U staple 6'-3 x 1'-6 legs, not hoops); 6d1 162 x 10'-6; 5d2 82 x 10'-2; 5d3 79 x 4'-5; 5d4 79 x 3'-8; 4d6 80 x 8'-10; 5d7 8 x 12'-2; 4d8 4 x 3'-2;
5e1 96 VARIES (768); 5e2 15 x 13'-8; 5e3 26 x 6'-4; 6f1 16 x 23'-3; 6f2 28 VARIES; 6f3 4 x 24'-5; 6f4 12 x 5'-3; 6g1 36 x 42'-5; 5g2 18 x 6'-1 (5'-3 + 10" leg; D=3 3/4); 5g3 4 x 40'-1; 6g4 36 x 5'-5;
6g5 28 x 6'-5; 5h1 24 x 4'-0; 6h2 20 x 11'-3; 6h3 20 x 7'-11; 5h4 30 x 12'-0 (2 x 9 + 2 x 6); 5j1 32 x 11'-6; 5j2 32 x 11'-6; 8j3 8 x 5'-0; 5m1 32 x 7'-9 (3'-9 x 2'-0 legs; 8 steps x 4); 5n1 32 x 4'-2; stainless 5d5 39 x 3'-6.
Epoxy 21,309 lb; stainless 142. Concrete cy: footing 84.8, backwall below 21.9, above 25.1, wing 31.6, mask 2.4, wing ftgs 9.3, extension 2.3 = 177.4 (closes).
MISMATCHES: placement strings 160 x 6d1 / 80 x 5d2 vs list 162 / 82 (+2 each); 5d3/5d4 79, 5d5 39, 4d6 80 agree. 9a1: 2 x 45'-11 - 9'-1 = 82'-9 vs footing 83'-0 1/2 (3 1/2" = 2" cover each end less rounding - OK).

---------------------------------------------------------------------------------------------------------------------

## 3. Ramp G South abutment (Design 316, p80-83 = DS 78-81; aesthetic p84)  -- Verified

### 3.1 Location / plan (p80; staking p14 = DS 12)
- CL South abutment bearing at STA. 67466+22.70 on the CL of Ramp G (staking p14); 145'-0 to the next pier at 67467+67.70. Title block STA. 67484+60.20 is the structure reference. Abutment is SQUARE (0 deg skew), bearing line normal to Ramp G.
- 32'-0 roadway gutter-to-gutter; CL Ramp G 6'-0 from the A gutter (26'-0 to the D gutter). Girders A-D, 3 SPA @ 9'-8 = 29'-0, A and D 1'-6 inside the gutter lines (1'-6 + 29'-0 + 1'-6 = 32'-0). Girder positions from the A gutter: A 1.5, B 11.167, C 20.833, D 30.5 ft.
- Footing/backwall length 37'-10 = 2'-11 + 32'-0 + 2'-11 (ends 2'-11 beyond the gutter lines = wing 1'-9 + maskwall 1'-2). 6g1 31'-8 (+ 4" cover = 32'-0): the backwall proper spans gutter-to-gutter, wings outboard.
- Plan normal dims: CL brg -> footing front 2'-11 1/2; CL brg -> backwall front 1'-5 (* may vary); backwall 1'-6; corbel 1'-3 beyond; (5'-10 1/2 = 2'-11 1/2 + 1'-5 + 1'-6; + 1 1/2" = 6'-0).
- Seat blocks: 4'-6 along the line (2'-3 | 2'-3 each side of the girder CL) x (backwall front face -> 2'-3 past CL brg = 3'-8 normal, DERIVED 1'-5 + 2'-3; the section prints only the 2'-3). Bearing anchors 1 1/2" dia swedge, 2'-6 x 1'-0 pattern as West.
- Deck end: CL brg to slab end 1'-2 1/2; gap 2 1/2" @ 50 F to backwall front face (1'-5 - 1'-2 1/2 = 2 1/2", closes); strip seal.

### 3.2 Rear elevation (p80) - footing LEVEL
- Bottom of footing 991.18; bottom of backwall 995.01; seat base 995.26 (+4'-1). Steps A-D: 995.51 (3, LOW STEP), 995.71 (5 3/8), 995.90 (7 5/8), 996.09 (10); check 995.26 + h = 995.51 / 995.71 / 995.90 / 996.09 (all within 0.01). 5n2 hoop at girder D only (tallest).
- Top of backwall 1004.22 at CL Ramp G (ref); 3'-7 offset (1'-1 + 1'-3 + 1'-3), "5'-6 1/8 **" below to bottom of backwall at back face; construction joints SLOPED parallel to crown; LEVEL at the wing ends.
- Placement: 6d1 62 SPA @ 6" = 31'-0 (63); 5d2 & 4d6 30 SPA @ 1'-0 (31); 5d3/5d4 30 SPA @ 1'-0 (31); 5d5 15 SPA @ 2'-0 = 30'-0 (16); backwall bars start 6" (6d1) / 1'-0 (others) from the A end.

### 3.3 Piles (p81)
- 20 HP14x73 (12 + 8 wing); design bearing 120 tons; contract length 125 ft; required nominal 227 tons (38 + 60 + 129); Pu = 239 kips; phi 0.55 / 0.7; downdrag 75 kips; tension 41 kips; min embedment 50 ft.
- 6 stations x 2 rows. From the A end: 1'-10, 6'-10, [CL Ramp G is 3" past station 2: 8'-11 total], 6'-7 (from CL), then 3 SPA @ 6'-10 = 20'-6, 1'-10 to the end: 28'-11 on the D side; total 37'-10 (closes with the 3" tick).
  Stations from the A end: 1.833, 8.667, 15.500, 22.333, 29.167, 36.000 ft (pitch 6'-10 throughout - DERIVED, closes). Hoops: pitch 6'-10 = 9" + 8 SPA @ 8" = 5'-4 + 9" (45 = 5 x 9) + 2 at each end = 49 = list; 5b2 x3 each end (6) with 8a1.
- Wing-footing piles: centres 10'-11 and 18'-11 from CL brg (8'-0 apart); inner pile 3'-9 inside the CL Ramp G offsets (= gutter 6'-0 - 2'-3); each footing 7'-0 x 3'-0, 4'-0 pile spacing, one circled pile.

### 3.4 Wingwalls (p82)
- Each wing 22'-6 from the BACK FACE of the backwall (= 25'-5 from CL brg): 6'-6 | 3'-0 | 5'-0 | 3'-0 | 5'-0 = 22'-6 (closes); first counterfort starts 9'-5 from CL brg (2'-11 + 6'-6). Wall 1'-9, fill-side face on the gutter line, straight, perpendicular to the abutment. Bars start/stop 3" ("22 SPA @ 1'-0, 23-5e1 (E.F.)").
- A-A (A wing): top 1003.98 at backwall -> 1003.08 at end; end height 4'-0 (end bottom 999.08); bottom 991.98 at the backwall (height 12'-0); footing tops 994.00 (No.1) and 996.54 (No.2).
- B-B (D wing): top 1004.66 at backwall -> 1003.74 at end (end bottom 999.74); bottom 992.66 at backwall (12'-0); footing tops 994.68 (No.1) and 997.21 (No.2).
  (Wing bottom slope ~0.316 in both; 8 SPA @ 1'-0 + 2 SPA @ 6" bar zone at the end.)
- Counterforts: heights above footing top No.1 8'-3, No.2 5'-6 (overall 11'-3 / 8'-6); 5h4: 9 bars (8 equal spaces) / 6 bars (5 equal spaces); 6h2/6h3 are L-bars with 1'-0 foot (10'-9 + 1'-0 = 11'-9; 8'-0 + 1'-0 = 9'-0).
- Maskwalls (TWO): 6'-0 wide x 12'-7 (left) / 13'-3 (right) above the 4'-1 footing; recess borders 1'-0, 1'-6 top; D-D 1'-0 | 4'-0 | 1'-0, recess 1 1/2"; 5e2 26 x 14'-7 (13 per wall; 12 SPA @ 5 1/2 = 5'-6); 5e3 56 x 5'-8 (14 per face per wall); 5g2 44 (2 per 6g1 row, 22 rows).
  Tops: left 991.18 + 4.083 + 12.583 = 1007.85, right 1008.50 (DERIVED).

### 3.5 Reinforcing (p83) -- re-read, all rows match first pass
8a1 16 x 37'-6 (1,602; straight, single length); 5b1 49 x 19'-2 (5'-8 x 3'-5 + 6" hook); 5b2 6 x 8'-7 (5'-7 x 1'-6 legs, staple); 6d1 67 x 11'-4; 5d2 35 x 11'-0; 5d3 31 x 4'-5; 5d4 31 x 3'-8; 4d6 31 x 8'-0 (1'-2 x 3'-5);
5e1 92 VARIES (736; 11'-7 ... 3'-9 = 23 x 2 per wing x 2); 5e2 26 x 14'-7; 5e3 56 x 5'-8; 6f1 20 x 22'-2; 6f2 24 VARIES (21'-1 ... 5'-2); 6f3 4 x 23'-1; 6f4 8 x 5'-3; 6g1 22 x 31'-8 (1,046); 5g2 44 x 6'-1 (5'-3 + 10" leg);
5g3 2 x 31'-8; 6g4 36 x 5'-5; 5h1 24 x 4'-0; 6h2 20 x 11'-9; 6h3 20 x 9'-0; 5h4 30 x 12'-0; 5j1 32 x 11'-6; 5j2 32 x 11'-6; 8j3 8 x 5'-0; 5m1 20 x 8'-1 (3'-11 x 2'-1 legs; 4 steps x 5 = 20, UNCERTAIN: West/East show 4 per step); 5n1 20 x 4'-2; 5n2 1 x 17'-5 (4'-2 x 4'-0 1/2 + 6" hook; girder D);
stainless 5d5 16 x 3'-6. Epoxy 11,341 lb; stainless 58. Concrete cy: footing 35.2, backwall below 11.5, above 10.0, wings 30.2, masks 4.4, wing ftgs 9.3 = 100.6 (closes).
Check: 8a1 16 = 5 + 5 + 3 + 3 rows; 37'-6 = 37'-10 - 4". MISMATCH: placement 63 x 6d1 / 31 x 5d2 vs list 67 / 35 (+4 each = 2 per maskwall end); 5d3/5d4 31, 5d5 16, 4d6 31 agree.

---------------------------------------------------------------------------------------------------------------------

## 4. Aesthetics (I-80 p93, Ramp G p84) -- Verified
Typical wingwall elevation: wing faces texture "A" (ashlar form liner, max depth 1 1/2"), maskwall recessed panel texture "B" (vertical fractured ribs ~2" centres, max 1 1/2"), 9" no-texture
strip along the top edge of the wing, 1 1/2 x 1 1/2 beveled rustication along the sloped bottom edge, end of wing 1'-0 (min) below grade line shown; mask panel straddles "CL BEARING"; lower part below proposed grade.

## 5. Approach slab / deck connection (brief)
Approach slab bears on the corbel ledge (paving notch); notch bars 5d3 (U 2'-5 x 1'-0), 5d4 (taper), 5g3 (longit.), and stainless deformed 5d5 dowels 3'-6 at 2'-0 (W 42, E 39, G 16);
slab/approach-slab details are in the deck and approach sheets (Design 116 p179 etc.), not here. Expansion device anchorage in the upper backwall (modular I-80, strip seal Ramp G); sheet details in
`docs/plan-data/expansion-joints.md`.

## 6. Remaining UNCERTAIN items
1. Meaning of the small top dims "3/8" (West) / "3/4" (Ramp G) at the top of the upper backwall (read as longitudinal-grade drop across 1'-6).
2. Meaning of "F.F. & B.F. 1'-10 MIN / 1'-6 MIN" (pile top -> backwall base clear / face-bar embedment).
3. 8" vs 1'-3 corbel/ledge assignment (corbel projects 1'-3 behind the upper-wall back face; 8" is measured from the same face).
4. East skew sign; bolt-pattern orientation (2'-6 along the abutment shown in plan; layout figure sign convention unclear).
5. West/East backwall-extension plan dims (7'-5 9/16, 3'-9 7/8, 4'-6 3/8, 2'-9 3/8, extension width ~5 ft) - printed but spans not unambiguous.
6. Pile tip elevations (not printed; contract length only); end-pile distance to footing end for West (derived ~3'-10 / 4'-0) and Ramp G (1'-10 printed to footing end).
7. Which wing-footing pile is circled in each footing (uplift anchors) - 8 bars = 4 circled piles.
8. Where each wing bottom leaves the backwall (West level 1'-6 then slope; East/Ramp G slope from the face) is read from elevation views; start of exposed wing along the gutter line not dimensioned from CL brg except Ramp G (6'-6 from back face).
9. Placement-string vs list bar count differences (6d1, 5d2: +1 West, +2 East, +4 Ramp G) - extra bars at the end/maskwall corners; positions not drawn.

## 7. Inputs the modeler needs (minimal parameters for an as-designed abutment + wingwalls)
Per abutment (support line):
- Identity/position: station of CL bearing on the reference alignment (W 7578+79.64 / E 7612+54.84 on I-80 CL offset 41.08' LT., G 67466+22.70); skew from radial (W 44 deg, E 5 deg 26'43", G 0); approach roadway offsets of the two ends:
  W 24'-0 + 2'-11 (A) / 36'-0 + 1'-9 (F); E 36'-0 + 1'-9 (J) / 42'-0 + 2'-11 (A); G 6'-0 + 2'-11 / 26'-0 + 2'-11 (footing/backwall ends parallel to the approach CL).
- Section (normal to the line): footing width 6'-8 1/2 | 6'-0, depth 4'-0 + 1" wash; backwall offset from footing back 1 1/2", thickness 1'-6, base at footing bottom + 3'-10; CL brg -> backwall front *1'-10 | *1'-5; CL brg -> step front 2'-3; step front -> footing front 1'-0 |
  (Ramp G: CL brg -> footing front 2'-11 1/2); corbel: ledge 1'-6 | 1'-1 below top of upper backwall, projection 1'-3, vertical 1'-3, 45 deg taper 1'-3, 3" to the construction joint, 1" chamfer; offset top -> construction joint 4'-0 | 3'-7 (constant, follows the crown line).
- Elevations: bottom of footing (W 992.82 level; E 1008.27 -> 1003.44 sloped; G 991.18 level); top of backwall at CL brg (1008.15 | 1018.71 | 1004.22), follows crown line; seat base = footing bottom + 4'-1.
- Seats: per girder along-line position (spacing as printed above), block 4'-6 x (backwall front face -> 2'-3 past CL brg), step height h above seat base (West 3, 7 3/8, 11 5/8, 10 1/2, 8 5/8, 6 5/8; East 5" typ. (top elevs listed); Ramp G 3, 5 3/8, 7 5/8, 10) or the step top elevations; 5n2 hoops at the 2 tallest.
- Piles: HP14x73 vertical, rows 1'-11 1/2 behind / 1'-9 ahead of CL brg (I-80) or 1'-6 1/2 / 1'-5 1/2 (Ramp G), pitch 6'-10 (W, G) / 6'-0 (E), station positions listed in 1.3 / 2.3 / 3.3, top 2'-0 above footing bottom, contract length 125 / 115 / 125 ft, flanges parallel to the line.
- Wings (x2): parallel to the approach CL (W straight, E curved on the gutter radii, G perpendicular to the line); thickness 1'-9 outboard of the gutter line; length along the gutter line from CL brg 31'-9 | 27'-0 | 25'-5; top elevation at the backwall end and at the free end; end height 4'-0; bottom elevation at the backwall end
  and straight slope to the end bottom; wing footings 7'-0 x 3'-0 x 3'-0 at 14'-3 & 24'-3 | 9'-6 & 19'-6 | 9'-5 & 17'-5 from CL brg (footing begins; centre +1'-6), footing top elevations given, 3'-9 fill side + 1'-9 wall + 1'-6 outside; counterforts 3'-0 wide x 3'-6 behind the wall, heights
  8'-10 / 5'-4 | 8'-8 / 5'-4 | 8'-3 / 5'-6 above footing top; 2 piles per footing at 4'-0.
- Maskwall: end panel 10" thick on the footing top at the A end (one on I-80, two on Ramp G): width 10'-4 13/16 | 6'-8 7/8 | 6'-0, height 14'-2 | 12'-4 | 12'-7 and 13'-3, recess borders 1'-0 / 1'-6 top, recess depth 1 1/2".
- Backwall extension (I-80 only, F / J end): top ELEV 1007.65 | 1021.09, extends to the footing-bottom line, ~5 ft wide.
- Rebar: bar list tables above (mark, size, count, length, bend dims) and placement strings in 1.2 / 2.2 / 3.2.
