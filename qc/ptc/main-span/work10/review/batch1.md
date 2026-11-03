# Agent review batch 1: PTC main span joints L0 - L7 (2026-11-02)

Joints: L0 U0 L1 U1 L2 U2 L3 U3 L4 U4 L5 U5 L6 U6 L7, both truss planes. Sheets p.73-76 (joint elevations), p.97 (lateral / strut details).
Rows: `batch1.csv` (65 rows: 36 CONFIRMED (16 major, 18 minor, 2 info), 26 OK, 2 UNSURE, 1 NOT SHOWN).

## Method
- The model's outline (u, z) for each joint was laid over the sheet scan. Calibration: the W.P. pixel, 4.28 px/in. at 150 dpi (p.73 U0 post 1'-11 1/2 b-b) and
  the drawn chord direction, all from `docs/plan-data/ptc-delaware-river/connections/gusset-outlines.csv`. Drawn red = inner gusset, orange = splice, gold = fill,
  magenta = connection plate, blue = members, lime = model rivets with the shank normal to the plane. The overlays are `batch1_<joint>_p<page>.png`. The L1
  overlay is on the p.75 L3 drawing, because the sheet says L1 is similar to L3.
- Fill / splice lengths were scaled from the dimension lines at 250-300 dpi (`batch1_*_fills.png`, `_splice.png`). They are about +-2-3 in.: the W.P. reads carry
  a steady 2.5 in. offset at U1.
- Scripted checks on `out/tris_*.csv` / `rivets_*.csv`:
  - N vs S: every part compared with its mirror (category + bounding box within 0.5 in.).
  - Part pair interference: point-in-solid test, with the depth of each hit measured.
  - Each rivet shank sampled along its grip to find voids.
  - Rivets inside the gusset counted and compared with the sheet symbol counts (`rivet-counts.csv`).

## Systematic patterns (counts over the 15 joints)
1. **Gusset outlines are NOT the problem in this batch.** All 15 inner gussets follow the drawn plates within about 1-3 in. (U2's top-right corner is the worst,
   at 2-3 in.). So the user's belief that the same gusset issues exist everywhere is not confirmed for L0-L7: 0 of 15 outlines are wrong. The problems are the
   material around the gussets.
2. **Splice / fill / inlay material differs from the sheet at 9 of the 15 joints** (8 joints with a chord splice + L5):
   - L5: no splice material at all (web splice 31 1/2 x 1 1/8, fills 18 x 7/8, cover splice 24 x 5/8, 3/4 fills).
   - U5: no inlay plates. The 34 x 3/8 fill and the I.S. splice plates float 6 in. inside the chord box, touch nothing and carry no rivet.
   - U3: the right 18 x 5/8 fill is under the splice plate. The sheet puts it beyond the splice plate's end.
   - L2: the web splice plate is 24 in. long; the sheet draws about 38 in.
   - U6: the o.s. splice plates are 97 in. long; the sheet draws about 122 in.
   - L6 / L7: the inlay plates are not separate parts.
   - L7: the i.s. splice is about 8 in. off position.
   - U4 / L4: fills are about 5 in. short.
   - U1, U2 and L6 fills / splices are within +-3 in.
3. **Bottom strut connection angles L 5x5x7/16 are missing at all 7 lower-chord strut joints L1-L7.** p.97 draws them in the L1 cross section ("use similar
   details for L2 to L8 incl."), and p.75 calls them out at L3. In the model the strut channels stop 4.7 in. from the gusset and are held only by the lateral
   plates.
4. **Rivets:**
   - The model has fewer rivets inside the gusset than the sheet draws at 11 of 14 joints with counts (10-25 % fewer; U0 has 55 against 73 drawn).
   - The rivets drawn beyond the gusset on the o.s. splice plates are absent at U6 and L7 (about 46 / 57 drawn per face).
   - L0's strut box rivet pattern differs from the drawing.
   - At U5, 25 rivets per truss have a 9/16 in. void in the grip, because the 3/4 fill is longer than the 9/16 fill under it.
   - No model rivet lies in empty space.
5. **U0 end post:** the post stops at the chord bottom and the top chord ends square. p.73 runs the post to the chord top and cuts the chord on a bevel, so 9 post
   rivets per face have no steel behind them.
6. **N vs S:** the parts are exact mirrors at all 15 joints. Only some rivet rows differ: the top cover splice rows at U3 / U4 / U5 are 0.8-2.2 in. apart, and 2
   bracing plate rivets per L joint are 3 in. apart.
7. **Interference:** no part pair penetrates by more than 0.03 in. (the L0 shoe pin in the solid sole is the documented "pin hole not cut").

Not checked in depth: the lateral / strut plate outlines in plan (views C / D) against p.97. Section 33 has already traced them, and the elevations do not show them.
The two UNSURE rows are the L3 "1-Fill 6x1/2" and the L7 "1-Fill 7x5/8": it is not clear from the scan what they pack.

## Crops used
- Overlays: `batch1_{L0,U0}_p73.png`, `batch1_{U1,L2,U2}_p74.png`, `batch1_{L1,L3,U3,L4,U4,L5,U5}_p75.png`, `batch1_{L6,U6,L7}_p76.png`.
- Zooms: `batch1_U0_p73_post-zoom.png`, `batch1_U0_p73_gusset.png`, `batch1_L0_p73_strut-zoom.png`.
- Dimension crops: `batch1_U1_p74_fills.png`, `batch1_L2_p74_splice.png`, `batch1_U3_p75_fills.png`, `batch1_U4_p75_fills.png`, `batch1_U5_p75_splice.png`.
- p.97 strut section: `batch1_L1_p97_strut-section.png`.
