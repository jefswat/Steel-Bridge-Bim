# Nebraska St. Ramp B - abutment wing extensions, maskwalls, backwall extension, wing footing + wing piles; Pier 1 soffit brick

Source: `B00167 - Nebraska Ramp B - Design 116.pdf` (Iowa DOT Design 1617), PDF page = sheet number (p.18 = sheet 18 / design sheet 17).
Read from: p.18 (S abutment details), p.19 (S views A-A, B-B, F-F, C-C, E-E, wingwall table), p.20 / p.21 (N abutment, same views), p.22 (pile plans + notes),
p.23 (abutment bar lists + concrete quantities), p.3 (summary quantities), p.9 / p.15 (Pier 1 cap and thin brick), p.54 (rail, only noted).
Dimensions decoded with `tools/pdf_dims.py` (text layer + fraction glyphs), geometry read from rendered crops of the vector drawing.
Data: `wings.csv` (65 rows: element, dimension, value, frame, elevation, page, confidence, note) and `piles.csv` (27 piles).

## Frames used
- **F0** (section through the abutment): u = distance from the CL abutment bearing along the alignment, + REAR (backfill), - FRONT (bridge side).
  Evidence: the footing FRONT face is the sloped one (1:4, p.18 section / p.19 A-A) and the wing extends toward the rear; wing pile 6'-0 1/2 + 1'-6 = rear end at
  u = +7'-6 1/2, maskwall front face = footing top front edge = u = -2'-1 (= the "BearingToStepFront 2'-1" of the build script), footing toe at the bottom u = -3'-1.
- **SL** (along the support line from the CL Nebraska St. Ramp B point): + toward the RIGHT of CL (east, the 6'-0 side), - left (west, the 20'-0 side).
  S plan (p.18) looks up-station; N plan (p.20) looks back-station (left/right swap). Both footings: 21'-7 to the west end, 7'-7 to the east end of the CL point (p.22).

## What the plans show (south = 1 wing + 1 backwall extension, north = 2 wings)
| item | S | N |
|---|---|---|
| wing extension | east end only (6'-1 side) | both ends |
| maskwall | both ends (0.8 / 0.9 CY) | both ends (0.8 / 1.0 CY) |
| backwall extension | west end (2.8 CY) | none ("-") |
| wing piles | 1 (13 = 12 + 1) | 2 (14 = 12 + 2) |

### Wing footing (L-shaped in plan)
- Main footing 6'-3 wide (1'-6 + 3'-3 + 1'-6), bottom S 1112.35 / N 1105.62, depth 4'-1 at the sloped front face and 3'-10 at the rear (medium confidence on the 3'-10).
- At each wing end the footing steps out 4'-4 1/2 beyond the rear face (total front toe to rear end 10'-7 1/2 = 6'-0 wing + 4'-7 1/2 at the bottom, p.19 A-A, p.18 plan).
  In the along-support direction the step-out is 3'-6 (pile 2'-0 from the inner edge, 1'-6 from the outer end face); the outer end face is the footing end face (no extra length).
- Wing wall 6'-0 long (u = +1'-6 1/2 .. +7'-6 1/2), 1'-7 thick (outer face flush with the footing end, 1'-11 footing projection on the bridge-interior side).
  1/2" preformed expansion joint filler between the wing wall and the maskwall (p.19 A-A). Bars 8a2, 8a3, 6a4, 5b2 (footing), 5d9 / 5e2 (wall).
- Top of wing wall at the gutter line, sloped parallel to the theoretical grade; corner elevations (table p.19 / p.21):
  SW G 1124.75 / H 1124.74; SE G 1125.73 / H 1125.73 / I 1125.76; NW G 1118.02 / H 1117.92; NE G 1119.32 / H 1119.23. The barrier-rail wing extension sits on top (p.54, p.55).

### Maskwall
- 3'-7 1/2 long along the alignment (u = -2'-1 .. +1'-6 1/2), 10" thick (reading of the C-C / E-E dimension), top sloped to grade; height above the footing 8'-2 1/2 (S wing end),
  7'-4 1/2 (S west end), 7'-4 1/2 and 8'-6 1/2 (N views). Poured before the deck (p.23). Bars 5e1 / 5f3 / 5f4.
- Maskwall + wing wall + backwall are three different blocks: the backwall front face is at u = +1'-6 1/2 (p.18 "1'-6 1/2"), 1'-6 thick, to u = +3'-0 1/2.

### Backwall extension (S west end only)
- 1'-6 thick, 4'-3 1/4 long (long edge; 4'-2 1/16 the short edge, p.19 E-E), axis turned 5 deg 54'10" from the backwall line, top 1124.36 at the far end, bottom 1113.54 at the far end
  (bottom slopes up from the footing bottom 1112.35). No footing / pile of its own.

### Wing piles
- HP12x84, plan position under the wing footing: 1'-6 from the outer footing end, 2'-0 from the inner wing footing edge, 6'-0 1/2 rear of the CL brg.
  Along the support line: S east 6'-1 right of CL Ramp B; N east 6'-1, N west 20'-1 left of CL (footing ends 7'-7 / 21'-7 less 1'-6).
- No batter symbol (plumb as drawn - the p.22 note says batter piles are shown "in the direction shown"; the wing pile has no dashed tip footprint). Web turned 90 deg to the main piles (web along the support line).
- Top at the bottom of the footing + 2'-0 (p.18 "2'-0 TOP OF PILES"): S 1114.35, N 1107.62. Contract length 190 ft (S) / 140 ft (N) for ALL piles (p.3: 2,470 = 13 x 190, 1,960 = 14 x 140; p.22 notes).
- Main piles (12 each, 2 per row, 6 rows): rows at 1'-6 3/4 from the end, 3 spaces @ 5'-2 1/2, 4'-4 3/4 to the CL Ramp B point, 9 3/4 past it, 5'-2 1/2, 1'-6 3/4 to the other end
  (check: 4 x 5'-2 1/2 + 9 3/4 ... all sums close to 29'-2). Across: 1'-7 front / 1'-8 rear of the CL brg at the footing bottom plane. Front row battered 1:4 toward the bridge (p.18 section), rear plumb.

### Pier 1 cap soffit thin brick (t54)
- No soffit callout and no area on the plan. Pier 1 note (p.15): thin veneer brick and feature bands wrap around ALL sides of the columns and cap beam.
- Soffit between the columns: 17'-4 3/8 clear (23'-4 c-c less 5'-11 5/8 column) x cap width 4'-11 5/8 (8 + 3'-7 5/8 + 8) = **86.3 SF**, at 1110.03 ft (cap top 1115.03 less the 5'-0 cap depth). Cap ends flush with the column outer faces (cap length 29'-3 5/8 = 23'-4 + 5'-11 5/8), so no overhanging soffit.
- p.3 summary: Pier 1 822.9, Pier 2 759.8, barrier rails 43.3 = 1,626.0 SF. Model 1,492 SF -> missing 134 = 86.3 (soffit) + 43.3 (rails) + 4.4 (not identified).

## Conflicts
- Backwall extension top: 1124.36 (p.18 rear elevation, far end) vs SW gutter-line corner 1124.75 / 1124.74 (p.19 table). Different positions (stub end vs gutter line), both recorded.
- p.21 note refers to "DESIGN SHEET 19" for bottom-of-footing elevation (= PDF p.20 N); p.19 refers to design sheet 17 (= p.18 S). Consistent intent.
- p.18 / p.20 plan prints 5 deg 54'10" / 5 deg 53'33"; p.22 S prints 1 deg 17'03" instead. Which line each angle is measured from is not stated.

## UNRESOLVED
- UNRESOLVED (p.19): which point of the SE wing top G / H / I label - the table gives three letters for SE, View A-A labels only I.
- UNRESOLVED (p.19, p.21): wing wall height - not dimensioned, only top (gutter line) elevations and the footing.
- UNRESOLVED (p.19): maskwall top elevation at each end; PVC pipe elevation (only the 6" / 6" setback is dimensioned).
- UNRESOLVED (p.22): pile tip elevations - the contract length is counted from a start elevation at the bottom of the EPS backfill, which is in the road plans.
- UNRESOLVED (p.2): EPS block geometry in the wing extensions (road plans).
- UNRESOLVED (p.15): whether the soffit is meant to be brick (no explicit callout) and Detail A's bottom band.
- Not read: p.53 / p.55 rail wing-extension details (5c3 / 5c14), sheet 65 texture.
- UNRESOLVED (p.18): bevelled bottom mentioned in the build script (p.19 / p.21 views show a flat bottom with the 1:4 front face) - not found as a separate bevel.

## Modeling now
S: east wing footing + wall + maskwall, west maskwall + backwall extension, 13 piles; N: two wings + maskwalls, 14 piles. All needed dimensions above except wall heights (derive from gutter-line elevations less footing top) and pile tips.
