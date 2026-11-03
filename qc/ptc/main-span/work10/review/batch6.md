# Agent review batch 6: PTC main span, primed half L6' / U6' ... L0' / U0' (2026-11-02)

Scope: 14 joints x both truss planes (N, S). Read-only; nothing in the model or scripts was changed.

## How the review was done
- **Mirror / opposite hand (data check).** For each joint and plane I compared every part of the primed joint, mirrored about the W.P. (u -> -u), with the same part of the unprimed joint (`out/tris_*`: member bounding boxes in u / n / z, pieces pooled per member). I compared rivets by their shank mid-points (`out/rivets_*`; the CSV gives the head end, so raw positions do not mirror). For N vs S I compared the primed joint with n flipped. Scratch scripts are in my scratchpad and nothing went into the repo.
- **Sheets.** I looked at the S images of all 14 primed joints next to the p.73-76 crops. The sheets draw the unprimed half, so I compared each primed joint with the sheet read in the opposite hand.
- **Scale.** Lengths were scaled at 4.28 px/in at 150 dpi (3/4 in. = 1 ft). I re-checked that against the printed 2'-10 1/2 b.-b. chord on p.73: 223 px at 220 dpi. Cover-plate lengths were scaled by ratio to the printed 24 in. plate width, which needs no scale.

## Result
30 rows: **16 OK**, **11 CONFIRMED** (3 major, 6 minor, 2 info) and **3 UNSURE** (1 minor, 2 info).

**Mirror: correct everywhere.**
- Every truss part of all 14 primed joints mirrors its unprimed joint to within 0.75 in., in both planes.
- N and S are identical once n is flipped. Only some names differ (FB end connection EC1A / EC1B, SW-S / SW-N).
- Rivet counts match except at L5 (351 vs 350), L4 (662 vs 663) and L2 (617 vs 619). All mid-points agree within 0.8 in., except one lateral-plate rivet pair at L1', L3', L4', L5' and L6', which sits one 3 in. pitch away from its mirror position.
- There is no hand error. Every finding below also applies to the unprimed joint, because the two halves are identical.

## Systematic patterns (user: "the same gusset issues exist everywhere")
1. **Gussets: refuted for this half.**
   - I found no outline, thickness or rivet-containment error that the sheets support at the 14 primed joints.
   - Thicknesses match the callouts: L0 / U0 / L6 7/8; L1 / L3 1/2; L2 / L4 / U1 / U3 3/4; U2 / U4 / U6 5/8; U5 7/8; L5 3/4.
   - The outlines match the drawn plates. L0's right edge was re-read at 220 dpi and lands on page x 937, against 936 in gusset-outlines.csv.
   - The drawn rivet fields lie inside the plates.
   - My first quick scaled reads that suggested 5-15 in. differences (L3, L4, U3, U0) were wrong on re-check, so I did not report them.
2. **Splice material that was never built: confirmed, 3 joints major + 2 more partial.**
   - L5: the whole chord field splice is missing (cover splice plates 24x5/8 with 20 rivets each side, web splice plates 31 1/2x1 1/8, fills).
   - L6: cover splice plates 24x11/16 are missing (about 48 in. long by ratio).
   - U6: cover splice plates 24x5/8 are missing.
   - Inlay plates are not built at L6, U6 and U5. At L6 / U6 the continuous ply fills the same layer, so this is info only. At U5 the sheet staggers the ply ends, so the geometry may differ (UNSURE).
   - L1 / L3: the 1-L 5x5x7/16 (C) angles and 1-Fill 6x1/2 (C) at the vertical are missing.
   - All of these are rows in splices.csv with no length, which TRUSS-DESIGN says were not built.
3. **Fills placed straddling the joint: confirmed at 2 joints.** The sheets put these fills on the lighter member's side only, ending at the FIN line.
   - U1: Fill 24x5/16 (C) runs u -8.7..+6.5 across the joint.
   - L4: 2-Fills 24x1/8 (C) run the full 24 in. across the joint.
   - In both, the splice plate sits on the fill on the heavier member and stands 1/8-1/4 in. clear of the lighter member's cover.
4. **Splice plate lengths: 2 too short.**
   - L4 cover splice: model 24 in., sheet about 37 in. (9 columns x 5 gage lines = 45 rivets).
   - L2 web splice: model 24 in., sheet dimension line about 42 in.
   - U1, U2, U3, U4 and U5 match.

## Sheet crops used (review\)
- Joints: batch6_L0_p73.png, batch6_U0_p73.png, batch6_U1_p74.png, batch6_L2_p74.png, batch6_U2_p74.png, batch6_L1_p74.png, batch6_U3_p75.png, batch6_L3_p75.png, batch6_U4_p75.png, batch6_L4_p75.png, batch6_U5_p75.png, batch6_L5_p75.png, batch6_U6_p76.png, batch6_L6_p76.png
- Zooms: batch6_L4_p75_plan.png (L4 cover splice length), batch6_L2_p74_splice.png (L2 web splice dimension), batch6_L0_p73_gusset.png (L0 gusset edge, 2-7/8 callout), batch6_plans_p74-76.png (top / bottom flange plans of U1-U6, L2, L6 for the cover splice ratios)
