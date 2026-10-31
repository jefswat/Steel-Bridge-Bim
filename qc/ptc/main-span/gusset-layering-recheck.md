# PTC main span ("PTC Arch truss 18-20") - multi-layered gusset plates: RE-CHECK of the review rows

Re-run of `gusset-layering-findings.md` (2026-10-27) on the model rebuilt 2026-10-28 evening after fix batches 1-7 (`TODO.md`). Review only: nothing in the
.sbm, csx, csv or src/ was changed; the old findings file is untouched. Date 2026-10-28.

**How it was measured.**
- `work6/gussets/layers.csx` = the corrected probe of `work4/layers.csx` (depth counting by triangle normals; the work3 probe paired hits by parity and read
  touching plies as air) + a dump of `TrussModel.DroppedRivets`. Run once through `temp\cli-main\sbm.exe run` on `samples/plans/PTC Delaware River Bridge.sbm`
  with only the main span bridge kept, no save, heavy lock held for the run only. Outputs `work6/gussets/out/` (gusset-parts, stacks, stack-summary,
  stack-points, rivet-summary, rivet-all, diag-all, parts, dropped). Analysis scripts `work6/gussets/gaps.py`, `inner.py`.
- Comparison base: `work4/out-saved` (the model after batch 1, same probe) and the old findings.
- Sheets p.76 / 77 / 78 / 81 / 89 looked at again at 110 dpi (`work3/pg`) for the fill callouts of the new rows.
- Companion db `PTC Delaware River Bridge.sqlite` opened read-only for the sheet links.
- Counts are per plane (N; S identical) unless stated; "per face" = one gusset face.

## 1. Status of the old rows

| Row | Old finding (short) | Status | Measured now (old) |
|---|---|---|---|
| GL01 | L11 four plates per face, model three | **FIXED** | `GussetStacks` L11 / L11' = 1 1/8 + 3/4 + 3/4 + 3/4 (35.5 + 35.5 + 31.5 + 25.7 SF), both planes; layers adjacent (0 gaps, 0 floats, 0 overlaps over 561 grid points per face) |
| GL02 | L11 stack 1 1/8 in. too far out | **FIXED** | inner face of the 1 1/8 at 13.375 in. from the truss CL (26 3/4 in. to in., plan Half Section E), was 14.5; post-to-gusset gap points 120 -> 0; L11 rivets with air 95 -> 0 per face; grips 7.25 -> 6.875 in. Residual: GN06 |
| GL03 | L11 layer outlines all clipped to the 29 SF envelope | **PARTLY** | inner 1 1/8 and first 3/4 are still the same envelope (35.5 SF each, flag "not separable"); second / outer 3/4 now end at the scaled Section E steps (v = 84 / 45 in., 31.5 / 25.7 SF) and run down to the chord bottom, 1 1/8 + first 3/4 to the 2'-5 FIN line (Section F). Plan: four extent lines along L11-L12, each ending at a different distance (1 1/8 longest) - the first 3/4 is still not shorter than the 1 1/8, and no extent is dimensioned |
| GL04 | L11 outer-layer clip edge on a rivet row | **PARTLY** | no rivet lies on a layer edge any more (head off steel 0, rivets missing a layer 0). The outer 3/4 edge is at v = 45.0; rows at v = 42.78 (2.2 in. edge distance, ok for 1 1/4 in.) and 45.56 (on the third layer, 0.56 in. outside the outer plate edge): see GN04 |
| GL05 | L10 stack checked OK | **still OK** | 3/4 inner at 14.5 in. + 1/2 outer (40.5 SF), adjacent; 208 / 234 rivets per face pass both layers; inner outline 105.3 -> 101.7 SF (batch 4f: top edge 7 in. lower). Rivets with air 0 -> 2 per face (0.44 in. at L10-U11) |
| GL06 | L22 half gussets as one plate | **STILL OPEN** (accepted simplification) | one 1 in. plate per face (121.0 SF) + 1 1/8 splice gusset (46.9 SF); the 1/8 in. split at the CL is not modeled, flag text unchanged |
| GL07 | Plates between chord and gusset not built from U8 on | **PARTLY** | o.s. splice plates under the gussets built (`UnderGusset`) at U8 / L9 / U10 / U21 / L21 and the U22 pin plates: gusset-to-steel air points per face U8 262 -> 70, L9 329 -> 84, U10 312 -> 80, U21 473 -> 136, L21 546 -> 159, U22 nothing under the gusset -> pin plates. The remaining air is at the web members (fills, GN01) and at U22 below the pin plates (GN02). Inlay / i.s. plates are still not built (e.g. U5: 31 rivets per face with 0.63 in. air between the chord web and the U5 fills, inlay 34 x 3/4 p.75 not built). The old L10 "1 1/8 in. air layer inside the chord web" was a work3 probe artifact: the corrected probe finds the chord plies contiguous (5 ft left of L10: 11.25-14.5 in. steel, no gap) - that sub-claim is NOW WRONG |
| GL08 | Joint rivet grips do not follow the steel (57,016 with air) | **FIXED** (old count overstated) | `JointRivetGripsFromSteel`: rivets over gusset faces with air > 1/16 in. 8,004 (batch-1 base, corrected probe; the old 57,016 was mostly the work3 parity artifact) -> 2,554 of 77,488; grips past the steel 2,562 = the same rivets; all of them are gusset-to-web-member gaps (GN01, GN02, GN03) - none inside a member stack |
| GL09 | gusset-layers.csv hygiene (U0, missing L1 / U22 / W14) | **FIXED** | U0 in_to_in 29; rows L1 (1/2), U22 (1/2, 35.25), W14 (29, thickness blank) present |
| GL10 | W14 gusset make-up not read | **PARTLY** | p.85 read: 2'-5 in. to in.; hanger overlap 20 points -> 0, gusset-to-hanger gap 0. Thickness not printed: 3/4 default kept and flagged (UNRESOLVED, low) |
| GL11 | L5 "2-3/4 Fills (C)" | **STILL OPEN** (reported) | not built; batch 3c: no dimensions and no step to fill, reported. One grid point per face shows 1.125 in. under the gusset at the L4-L5 angle edge |
| GL12 | Sheet links of the layer parts OK | **still OK** | section 3 |

Counts: FIXED 4 (GL01, GL02, GL08, GL09), still OK 2 (GL05, GL12), PARTLY 4 (GL03, GL04, GL07, GL10), STILL OPEN 2 (GL06, GL11), NOW WRONG 1 sub-claim (GL07: the L10 inlay hole).

## 2. New rows

Severity: wrong / missing / unclear. Confidence: high / medium / low.

**GN01 - Fills between the web members and the gussets are not built in the main span; the gusset stands off the member by the fill thickness.** (both planes, both faces, primed halves) - NEW (exposed by GL07 / GL08: the splice plates now carry the gusset, and the grips now stop at the steel)
- Severity: missing. Confidence: high for the gaps and for the callouts read; medium for the 110 dpi readings of the sizes.
- Model (rivets per face with air / gap): U6 16 (1.02 in., U6-L6), L7 21 (1.02, U7-L7), U8 20 (0.77, U8-L8), L9 24 (0.75, U9-L9), U10 24 (1.125, U10-L10),
  U21 29 + 22 (1.14 / 1.125, U21-L21 / U21-L22), L21 29 + 27 (1.14 / 1.125, U21-L21 / U20-L21); smaller (2-7 per face, 0.8-2.0 in., verticals / diagonals at the
  gusset edge) at U7, U9, U12-U17, L8, L13, L15-L18; in all 639 rivets per face, 2,554 in the model. The air equals the gap between the gusset inner face and the
  member's outer face (e.g. U8: gusset 15.25 in. from CL, U8-L8 channels 14.5).
- Plan: fills drawn and called out on the members at these joints: p.76 U6 "2-Fills 18 x 1 (C)" on U6-L6, L7 "2-Fills 18 x 1 (C)" on U7-L7; p.77 U8 "2-Fills 18 x 3/4 (C)"
  on U8-L8, L9 "2-Fills 21 x 3/4 (C)" on U9-L9; p.78 U10 "2-Fills 26 x 1 1/8 (C)" on U10-L10; p.81 L12 "2-Fills 28 x 1" / "26 x 1" (built? L12 shows no air); p.89 U21 / L21
  "2-18 x 1 1/8 Fills (C)" (U21-L21, U21-L22) and "2-21 x 1 1/8 Fills" (U20-L21). Each callout thickness = the measured gap.
- Proposed fix: connections-ptc.csx: member fills as `TrussJointPlate` with `Member` (QC pass 5 mechanism, as the approach section 6a "in-to-in minus depth" fills), width / thickness
  from the callouts, length = the gusset lap (flag the length). A generic CHECK (gusset-to-member gap > 1/16 in. with rivets through it) would catch the rest.

**GN02 - U22: the vertical below the pin plates is 3 1/8 in. off the gusset.** (U22, both planes) - NEW
- Severity: wrong. Confidence: medium.
- Model: 16 rivets per face on U22-L22 under the 1/2 gusset pass 0.5 in. of gusset, then 3.13 in. of air, then the channel web (grip 4.06 in.). The pin plates (2'-11 1/4 o. o.)
  are built (lengths flagged), the rivets lie below them.
- Plan: p.90 Section H; whether the pin plates (or fills) run down over these rivet rows is not read here.
- Proposed fix: read the p.90 elevation for the pin plate lengths; extend the plates or add fills. Until then a flag.

**GN03 - U11: post U11-L11 under the U11 gusset is only its inner ply; 12 rivets per face with 2 5/8 in. of air.** (U11 / U11', both planes) - NEW (rivets with air at U11 4 -> 17 per face)
- Severity: wrong. Confidence: medium.
- Model: rivets 26-31 in. below U11 pass the edge angle (1/2), the 1 1/8 gusset and then 2.63 in. of air to the post ply W5; the post's outer plies are absent there.
  Probably the post ply arrangement changed with batch 3a (L11 post webs 2'-2 3/4 o. o.) or the QC-pass-2 partial plies (34 x 1 1/8 from U11 / 34 x 9/16 o.s.).
- Plan: p.79 Section C "2'-5 in. Gus. Pls." (post 2'-5 o. o. at U11); p.69 post plies.
- Proposed fix: check the post U11-L11 ply runs near U11 in truss-ptc-arch.csx against p.69 / p.79; the plies the gusset bears on must reach the U11 gusset.

**GN04 - L11 outer 3/4 layer edge 0.56 in. from a rivet row: the heads of that row overlap the plate edge.** (L11 / L11', both planes, outer face)
- Severity: wrong (clash). Confidence: medium (the edge is the scaled Section E step, +-1 in.).
- Model: outer layer edge at v = 45.0 in.; row at v = 45.56 (11 rivets per face, 1 1/4 in., on the third layer, grip 4.125 / 5.0) has its head (about 2 in. dia.)
  over the outer plate edge by about 0.4 in. Row at 42.78 has 2.2 in. edge distance.
- Plan: p.80 draws no plate edge through a rivet field; the step lies between two rows.
- Proposed fix: put the scaled edge midway between the rows (about v = 44.2) or snap layer edges to >= 1.5 d from the rivets of the layer below; the generic guard of GL04
  (warn when a layer edge passes within 1.5 d of a rivet, or a head overlaps a plate edge) is still missing.

**GN05 - Joint rivet heads under the outstanding legs of bracing edge angles / floor beam end connection angles on the gusset outer face.** (both planes) - NEW / worse (222 -> 346 in the model)
- Severity: wrong. Confidence: high (measured).
- Model (per N plane, outer face): L1 12, L2 11, L3 12, L4 5 (+ primes; heads 4 in. inside `N-BGLn-T/B-EA` edge angles), L11 16 (inside `LS-L11-EC1A` / `-EC1AF`, the lower strut end
  angles), L13 20 (inside `FB13-EC1A` end angles and on the FB13 web line - the ray runs 1,012 in. inside the floor beam web), single ones at U11, U15-U17, L18, U19, L21.
  These are chord / member rivets placed as drawn on the gusset where a connection angle now sits.
- Plan: the drawn rivets under an angle leg are the angle's connection rivets (one rivet per hole) or lie beside the leg.
- Proposed fix: merge the joint rivet with the connection rivet when the angle leg lies on the gusset over it (grip through the leg, `MergeRivet` rule), else drop / shift it with a
  `DroppedRivets` reason. Overlaps the bracing / connections review.

**GN06 - L11: chord corners with no web ply under the 1 1/8 gusset.** (L11 / L11')
- Severity: unclear (minor). Confidence: medium.
- Model: 18 of 561 grid points per face show 1.5-1.625 in. of air between the gusset and the chord cover / angle (e.g. u -22.8, v -12.5: angle only, web plies W2 / W3 absent) - where the
  chord plies end (mitre / `InlaidAt`); TODO 3a already lists 12 inner-ply mitre clashes. No rivet has air there.
- Proposed fix: none needed for the layering; follows the ply-end flags (UNRESOLVED p.78 / 80 / 81, splice arrangement not determinable).

**GN07 - Plan-vs-plan conflict at L11: Section E draws all four plates down to the FIN line, Section F (2'-6 1/2 b. to b. of the shoe angles) leaves room for two.**
- Severity: plan conflict (reported). Confidence: high (the model raises it: "CONFLICT Section E vs Section F ... the printed Section F governs").
- Model: 1 1/8 + first 3/4 to the seat, second / outer 3/4 end at the chord bottom. Reported, never adjusted - for the user's decision.

**GN08 - Misleading CHECK "member N-U11-L11 is 30 1/8 in. wide, more than the gusset inside distance 26 3/4 / 29 in." at L11 / U11.** (both planes, 8 warnings)
- Severity: unclear (pre-existing; the W14 warnings are gone). Confidence: high.
- Model: measured overlap of any part with the L11 / U11 gussets = 0. The 30 1/8 is the widest section of the post (o.s. 9/16 partial plates near the floor beam), not
  the width inside the gusset band.
- Proposed fix (generator): compare the member width over the gusset lap only.

## 3. Sheet links (GL12)

**GL12 - still OK.** Checked read-only with `sbm db <model> --sheets-for <mark>` on the existing database. The full `v_element_sheets` query over every gusset was too slow in the sandbox and was stopped, so this is a sample.
- Every layer part inherits its joint sheet through "TRUSS-N Joint n": N-GL11-R4 and N-GL11'-L3 p.80, N-GL10-R2 (main) p.78, N-GL22-R2 p.90, N-GU22-R p.90, N-GW14-R p.84 + p.85.
- The new plates under the gussets do the same: N-SPU8-1 p.77, N-SPU22-1 p.90.
- No layer part checked had no link.

## 4. Counts

| Status | Rows |
|---|---|
| FIXED | GL01, GL02, GL08, GL09 (4) |
| still OK | GL05, GL12 (2) |
| PARTLY | GL03, GL04, GL07, GL10 (4) |
| STILL OPEN | GL06, GL11 (2) |
| NOW WRONG | GL07 sub-claim (L10 inlay air layer = old probe artifact) (1) |
| NEW | GN01-GN08 (8: wrong 4 = GN02, GN03, GN04, GN05; missing 1 = GN01; unclear 2 = GN06, GN08; plan conflict 1 = GN07) |

## 5. Not determined

- The fill lengths at GN01 (callouts give width x thickness only) and the U22 pin plate lengths (GN02).
- The post ply arrangement at U11 against p.69 / p.79 (GN03): only the measured gap is reported.
- L11 extents in inches (GL03): still scaled; the first 3/4 extent was not separated.
- Rivet-by-rivet membership of the L10 1/2 and L22 1 1/8 layers vs the drawn rivets (as before).
