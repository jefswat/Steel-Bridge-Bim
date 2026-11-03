# Agent review batch 2: PTC main span joints U7 - U14 (2026-11-02)

Joints: U7 L8 U8 L9 U9 L10 U10 L11 U11 L12 U12 L13 U13 L14 U14, both truss planes (N and S). Read-only. Rows in `batch2.csv`.
Each finding applies to both planes (column `plane` = `N;S`). I compared the N and S part sets per joint: the marks, counts and rivet counts
are the same mirrored set. The only differences in the dumped extents (for example the L13 `BGL13H` plates) come from the dump's
world-axis box (`dump_joints.csx` keeps triangles that touch a +-3 m box aligned to world X / Y, which is not symmetric about the truss).
They do not come from the model.

## Verdict counts (48 rows)

| verdict | major | minor | info | total |
|---|---|---|---|---|
| CONFIRMED | 23 | 6 | 1 | 30 |
| UNSURE | 1 | 4 | 1 | 6 |
| OK | - | - | 12 | 12 |

## Systematic patterns

1. **Chord splice material is missing at 14 of the 15 joints.** The exception is L11, where the sheet draws no chord splice.
   - Every joint sheet (p.76-84) lists and dimensions the following, and none of it is built:
     - cover (flange) splice plates (24 x t);
     - inside web splice plates (31 1/2 x t);
     - inlay plates (34 x 1 1/8, 34 x 13/16, 31 1/2 x t);
     - web / cover fills.
   - The model has only:
     - the outside web splice pair at U8, L9 and U10;
     - the fills at U7.
   - The cause: `connections-ptc.csx` builds only the `splices.csv` rows that have a length. The rows here have no length, so they are listed as UNRESOLVED.
   - This confirms the user's "same issue everywhere" for this item.
2. **Rivets sit outside the traced gusset edge by a few inches at 5 joints.** This is a gusset-edge problem:
   - U9: about 100 rivets above the top edge.
   - U13: the top edge.
   - U12, U14: corner cuts / bottom edge, 12 - 44 rivets.
   - L10: one column on the left edge.

   At the two kinked top-chord joints, U9 and U13, the traced top edge is one straight line at the down-station chord direction. It drops 7 - 10 in. below the steeper up-station chord top, while the outline note says the edges run to the chord top.
   The other off-plate rivets in the counts are drawn fields beyond the gusset, which the sheet shows too: fill / splice fields, hanger cover fields and post fills.
3. **Bracing plates are generated as long tapered tongues where the lateral is set back far from the truss:**
   - L11 up-station lateral: it ends 87 in. inside the truss line, with a 96 in. plate pair. p.80 View K draws a short 5/8 plate on 1-L 8 x 7 1/2 x 1/2.
   - L13H: 116 in., flagged envelope. I did not read p.95 / p.98, so this row is UNSURE.

   Related, at the same joints:
   - U11: the top lateral plate is drawn as ONE 7/8 "Spl. & Lat. Pl." with R 3'-0 on the chord top (p.79 plan). The model has a 1/2 plate pair in the lateral plane, rising above the chord.
   - L13: the floor lateral has no plate. p.83 Section B draws a 1 3/4 bent gusset with 1-L 8 x 8 x 3/4 and 1-L 8 x 4 x 1.
4. **Some drawn parts are not built:**
   - L11: the gusset outline (envelope; p.80 draws the edges with 2-Ls 5 x 3 1/2 edge angles and 2'-0 radii, about 97 in. up the post) and the edge angles themselves.
   - L12: web member fills 2-Fills 26 x 1 / 28 x 1 beyond the gusset.
   - U13 / U14: the 2-Ls 8 x 4 x 1/2 (12 / 16 rivets in the o.s. leg) on the verticals. Their purpose is not stated on p.82; section 20 lists "main sway at other points" as open.
5. **The gusset outlines otherwise match the drawn plates** within the stated +-1-2 in.:
   - U7, L8, L9, U10, L10, L12, L13, L14, U14 shapes.
   - The edge angles 2-Ls 4 x 3 1/2 x 7/16 on the right edges.
   - The vertical laps, and the L10 two-layer stack.

   Small outline misfits:
   - U8: the bottom-right corner is about 6 in. outboard of the vertical face, and the edge angle is about 10 in. too long.
   - L9: the left side may be up to 5 in. wide (UNSURE).

   No member end overlaps or wrong-side members were seen at these joints.

So the user's statement holds for chord splice material (14 / 15 joints) and partly for gusset edges / rivets (5 / 15). It does not hold for the gusset shape in general (9 / 15 OK within tolerance). The bracing plate problem is specific to joints with set-back laterals: L11, L13H and the U11 portal.

## Review-tool notes (not model findings)
- The `render_joints.py` titles round the station hundreds. L9 prints 679+77.38 (it is 678+77.38), and L12 / U12 print 680+70.38 (679+70.38).
- Views C / D draw the far-end triangles of long diagonals that touch the z band (the green under the chord at U7 / U8 / U9).

## Sheet crops used (`review/`)
- p.76: `batch2_U7_p76.png`, `batch2_U7_p76_vertical.png`
- p.77: `batch2_L8_p77.png`, `batch2_L8_p77_gusset.png`, `batch2_U8_p77.png`, `batch2_U8_p77_gusset.png`, `batch2_U8_p77_rightedge.png`, `batch2_L9_p77.png`, `batch2_L9_p77_gusset.png`, `batch2_U9_p77.png`
- p.78: `batch2_L10_p78.png`, `batch2_L10_p78_wp.png` (W.P. check of the traced outline), `batch2_U10_p78.png`, `batch2_U10_p78_post.png`
- p.79: `batch2_U11_p79.png` (7/8 Spl. & Lat. Pl., R 3'-0), `batch2_U11_p79_sectionB.png`
- p.80: `batch2_L11_p80.png` (View K), `batch2_L11_p80_elevation.png`
- p.81: `batch2_L12_p81.png`, `batch2_L12_p81_fills.png`, `batch2_U12_p81.png`
- p.82: `batch2_U13_p82.png`, `batch2_U14_p82.png`, `batch2_U14_p82_bottom.png`
- p.83: `batch2_L13_p83.png` (Sections B / C)
- p.84: `batch2_L14_p84.png`, `batch2_L14_p84_hanger.png`
- p.97: `batch2_L8_p97.png` (DETAILS SHOWN FOR L1), `batch2_L10_p97.png`

Scales: 3/4 in. = 1 ft reduced = 2.054 pt / in. (4.28 px / in. at 150 dpi, the trace basis in `gusset-outlines.csv`). I checked it on the vertical widths and the chord 2'-10 1/2 b.-b. Ls. The L10 W.P. was overlaid at the traced pixel to confirm the frame.
