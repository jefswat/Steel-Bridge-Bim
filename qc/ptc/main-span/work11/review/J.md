# Agent review J: PTC main span joints after the fixes of TRUSS-DESIGN sections 38-41 (2026-11-02)

Scope: S truss, joints L0-L22 and U0-U22 (45 joints; the N truss and the primed half are exact mirrors). Every CONFIRMED row of the first review
(`work10/review/batch1-6.csv`, non-info, at these joints: 114 rows) got a verdict. Rows: `J.csv` (118 = 114 verdicts + 4 NEW problems).
Read-only: nothing outside this folder was changed.

## Counts

| Verdict | First-review findings | Notes |
|---|---|---|
| FIXED | 70 | strut angles L1-L7, U0 post + rivets, chord splice material at 25 joints, lug angles (11 rows), suspender diaphragms L15-L22 (8), U21 / L21 fills and O.S. plates, gusset edges U9 / U13 / U12 / U14 / U16 / U20 / U21 / L18, L11 lateral end, U22 pin plates |
| PARTLY | 16 | splices built but a stacking void (L10, U12, U13, L14, L20), U7 i.s. 31 1/2 x 3/4 not built, U5 I.S. length unsure, L5 corner fills, L13 / L15 inlays open, U3 fill start, U8 edge angle now too short, U0 / U3 rivet counts, one-ply rivets U15 / U16 |
| NOT FIXED | 26 | 9 rivet-count rows (no fix was made for them), 6 items the design notes list as open (L11 outline + edge angles, U11 lateral plate, L12 web fills, L13 floor lateral, L22 diaphragms), U5 9/16 void, L0 strut-box rivets, L10 left-edge rivets, U8 bottom-right corner, L15 / L16 gusset-only rivets, one-ply rivets L17 / U17 / L18 / L19 / U19 |
| FIX WRONG | 2 | L12 o.s. splice 34 x 1 in the gusset layer; L22 inlay 34 x 1 1/16 in the gusset layer |
| NEW problems | 4 | stacking voids (6 joints), U11 post ply cut back, drawn rivets dropped at L6 / U5, cover splice rivet grips |

## Method
- Model before / after: `work10/out` vs `work10/out_after` (triangles, rivets, parts). Images `png_after\S_<joint>.png`.
- Sheet overlays: the after model drawn on p.73-90 at 150 / 200 dpi, W.P. and drawn chord from `connections/gusset-outlines.csv` (4.28 px/in),
  a vertical shear from the model's mean chord slope onto the drawn chord. Plate END TICKS (perpendicular to the chord) laid over the dimension
  lines (`J_<joint>_p<page>.png`, `J_*_ticks.png`, `J_*_overlay.png`; at U5 the model inlay ticks fall on the drawn extension lines within about 1 in.).
- Scripted checks on every joint, before and after: plate / angle / gusset sample points inside other parts (ray parity, 6 directions, depth
  to the surface); every rivet shank sampled along its grip (voids, parts crossed, rivets that touch one part only); rivets in the gusset layer
  outside the gusset; rivets of the old model with no rivet within 1 in. in the new one (dropped); rivet count inside the gusset (batch 1 method).
- No rivet lies in empty space; no lug angle, diaphragm, strut angle or new fill interferes with a member.

## NEW problems introduced by the fixes
1. **Stacking voids (major, systematic).** A joint plate is one flat plate at the level of the highest steel under its whole reach. Where two
   inside plates (or fills) of different lengths share a face, the longer one rides on the shorter one and leaves a void beyond it. The rivet grips
   run through the void. Rivets per face: L10 95 (3/4 in.), L14 111 (1/2), U12 59 (3/4), U13 56 (5/8), L20 35 (3/4), U5 13 (9/16).
   Before the fixes only U5 (9), L8 (3) and L13 (2) had voids. Fix: a generic order (the longer plate against the member), or follow the sheet's order where it is drawn.
2. **Plates built in the gusset layer (major, FIX WRONG rows).**
   - L12 2-Splice Pls. 34 x 1 o.s. (207 in.) sits at n 14.50-15.50, the same layer as the 1 in. gussets. At L14 the same callout is outside the gusset.
   - L22 2-Inlay 34 x 1 1/16 sits outside the outer ply, inside the gussets, while the W2 layer it replaces is empty (58 rivets per face cross a 1 1/16 in. void).
     This is separate from the L22 gusset change by the other agent.
3. **U11 post ply cut back (major).** S-U11-L11-W2 (34 x 1 1/8) now ends at z -49.3, where it used to run to -25.5. It looks like the chord inlay trim also cut the post ply of the same size.
   20 post rivets per face cross a 1 1/8 in. void.
4. **Drawn rivets dropped in the inlay zone.** At L6, 31 per face are lost (u -33..-2, inside count 273 -> 242). At U5, 15 per face are lost (238 -> 231).
5. **Cover splice rivet grips (minor).** The cover splice lies on the thicker chord cover. Over the thinner cover the 1/16 in. step stops the grip probe:
   925 cover-rivet entries (82 before) grip the splice plate only (U14: 1/4 in. at the 4 in. 24 x 1/4 fill).
- Unsure, not in the CSV count: at U11 a 3/8 in. void under the 31 1/2 x 1 1/8 inside plate between the gusset edge and the drawn 4 5/8 in. fill (10 rivets per face).
  p.79 does not show what packs it.

## Other observations
- U5 2-I.S. Spl. Pls. 31 1/2 x 7/8: at 300 dpi (`J_U5_p75_dims.png`) its dimension line appears to end on the inlay's extension lines (about -21..+17).
  The splices.csv read is 15-17 in. longer at each end (-35.6..+37.7). The two reads need to be reconciled.
- U8: the right edge and the bottom corner are still 6.5 in. outboard of the vertical (p.77 at 300 dpi). The edge angle was shortened past the drawn
  band: it now ends at z -29, but p.77 and section 38's own pixel read put the end at about z -38.
- One-ply rivets beyond the gussets at L17 / U17 / L18 / L19 / U19 (10-16 per joint) still touch one web ply only. The splice plates now built
  (about +-15 in.) do not reach them, so section 38(f)'s explanation does not hold for these.
- Rows the first review marked info (N vs S rivet rows, image titles) were not re-checked. This review covers the S truss only.

## Crops / images used (this folder)
`J_U8_p77_rightedge.png`, `J_U5_p75_dims.png`, `J_U5_p75_ticks.png`, `J_U3_p75_ticks.png`, `J_L6_p76_overlay.png`, `J_L7_p76_ticks.png`,
`J_L10_p78.png`, `J_U11_p79_overlay.png`, `J_L12_p81_ticks.png`, `J_U12_p81.png`, `J_U13_p82.png`, `J_L14_p84.png`, `J_U16_p87_overlay.png`,
`J_L20_p89.png`, `J_L20_p89_overlay.png`, `J_L22_p90.png` (red = model gusset, coloured lines = model plate ends perpendicular to the chord,
green circles = model rivets).
