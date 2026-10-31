# PTC main span ("PTC Arch truss 18-20") - multi-layered gusset plates: review findings

Review only. Nothing in the .sbm, csx, csv or src/ was changed. Date 2026-10-27. Starts from the handoff rows H01-H04 in `connections-findings.md` ("Layering (handoff)"); the non-layering G / R / B / C / S rows there are not repeated.

**How it was measured.**
- The model was regenerated in memory with only the main span bridge kept, using `work3/layers.csx` (no save, heavy lock used, `temp\cli-main\sbm.exe run`). Outputs are in `work3/out/`.
- Per joint x plane x face, the script reports:
  - each gusset part: layer, thickness and offset band along the plane normal from the work point (`gusset-parts.csv`);
  - a ray-cast stack profile on a 3 in. grid over the gusset area: gaps / overlaps between layers, the gap between the inner gusset and the steel under it, and steel outside the outer layer (`stack-summary.csv`, `stack-points.csv`);
  - every joint rivet over the gusset: head on the outer face?, layers passed, and air inside the grip (`rivet-summary.csv`, `rivet-all.csv`, classification with `work3/classify.py`).
- Sheets p.73-90 were read at 110 dpi for every gusset callout. The stacked joints were read at 220-600 dpi (crops in `work3/pg`). A dark-line profile across p.80 Half Section E was taken with `work3/prof.py`.
- The companion db `PTC Delaware River Bridge.sqlite` was opened read-only for the sheet links.
- The N and S planes give identical numbers (checked), so the counts below are for one plane unless a row says otherwise.

## 1. Plan layers vs model layers, per joint

The thicknesses are per face, listed inner -> outer. "Callout" = the gusset thickness callout on the joint sheet.

| Joint (and primed half) | Page | Plan | Model (`GussetStacks`, SF per plate) | Agrees? |
|---|---|---|---|---|
| U0 | 73 | 7/8 single ("2-7/8 Gussets (2'-5 in. to in.)") | 7/8 (15.3) at 29 in. to in. | yes (csv in_to_in stale: GL09) |
| L0 | 73 | 7/8 single (End View fills / tension plates belong to the strut) | 7/8 (29.7) | yes |
| U1, L2, U2 | 74 | 3/4, 3/4, 5/8 single | same | yes |
| L1 | 74 (= L3, p.75) | 1/2 single ("similar to L3") | 1/2 | yes |
| U3, L3, U4, L4, U5, L5 | 75 | 3/4, 1/2, 5/8, 3/4, 7/8, 3/4 single (L5: "2-3/4 Fills (C)" are chord fills, GL11) | same | yes |
| U6, L6, U7, L7 | 76 | 5/8, 7/8, 1 1/8, 5/8 single | same | yes |
| U8, L8, U9, L9 | 77 | 5/8, 1, 1, 5/8 single | same | yes, but the stack under the gusset is missing at U8 / L9 (GL07) |
| U10 | 78 | 3/4 single | 3/4 | yes, stack under it missing (GL07) |
| **L10 / L10'** | 78 | **3/4 inner + 1/2 outer** (1/2 edges drawn solid over the dashed diagonal outlines = outside) | 3/4 (105.3) + 1/2 (40.5) | yes (GL05) |
| **L11 / L11'** | 80 | **1 1/8 + 3/4 + 3/4 + 3/4** (Section E: four labels; elevation: four extent lines along L11-L12); 1 1/8 plates 2'-5 o. to o. | 1 1/8 (29.1) + 3/4 (29.1) + 3/4 (21.6) at 29 in. to in. | **no** (GL01-GL04) |
| U11 | 79 | 1 1/8 single (Section C 2'-5 in. Gus. Pls.) | 1 1/8 | yes |
| U12, L12 | 81 | 1 1/8, 1 single (L12 o.s. splice 34 x 1 = continuation in the gusset layer, not a layer) | same | yes |
| U13, U14, U15 | 82 | 1 1/8, 1 1/8, 7/8 single | same | yes |
| L13 | 83 | 1 1/8 single (the "1-3/4 Gusset Pl. (Bent)" is the bottom lateral strut plate) | 1 1/8 inlaid in the outer chord ply layer | yes |
| L14 | 84 | 1 1/8 single | same | yes |
| W14 | 85 | not read in detail ("2-Pls 19 x 1 1/8" near the hanger top, 2'-5 in. to in.) | 3/4 envelope, in. to in. 14 5/8 | unclear (GL10, = G03) |
| L15, L16 | 86 | 1 1/8, 1 single | same | yes |
| U16, U17, L17 | 87 | 7/8 single | same | yes |
| U18, L18, U19, L19 | 88 | 7/8 single | same | yes |
| U20, L20, U21, L21 (+ U21' / L21' part elevations) | 89 | 7/8, 1, 3/4, 1 single | same | yes, stack under the gusset missing at U21 / L21 (GL07) |
| **L22** (crown) | 90 | **1 in. (two half gussets per face, 1/8 in. apart) + 1 1/8 splice gusset outside** (the 1 1/8 edges are drawn solid over the half gussets) | 1 (121.0, one plate) + 1 1/8 (46.9) | order and outlines yes; 1/8 in. split not modeled (GL06) |
| U22 | 90 | 1/2 single on the vertical, outside the pin plates (Section H: 2'-11 1/4 o. o. pin plates); "2-3/8 Gus. Pls. (C)" = temporary erection support | 1/2 at 35 1/4 in. to in. | layer yes; pin plates under it missing (GL07) |

**Summary.**
- Stacked per plan: L10 / L10', L11 / L11', L22, in both planes (5 joints per plane).
- Stacked in the model: the same 5 joints.
- No other main joint has a second gusset thickness callout. The re-check agrees with gusset-layers.csv except at L11 (four plates, not three).

**What the model does right at the stacked joints (all 5, both planes, both faces):**
- No air gap and no overlap between layers.
- No outer layer bridges past a missing lower layer. The float count is 0: the outer layers are clipped inside the inner plate.
- No layer is buried in a ply or in another plate. Overlaps with any part: 0. The only gusset overlap in the whole main span is W14 x hanger (G03).
- Rivet heads lie on the outer face of the outermost layer present. The only exception is the L11 edge row (GL04).
- Every rivet in the layer area passes through all layers: L10 209 / 226 per face, L22 177 / 192, L11 388 / 389.

## 2. Problem rows

Severity: wrong / missing / unclear. Confidence: high / medium / low.

### L11 / L11' (river pier joints, p.80) - continues H01

**GL01 - L11 has four gusset plates per face, the model three.** (L11 / L11', both planes, both faces)
- Severity: missing. Confidence: medium.
- Model: 1 1/8 + 3/4 + 3/4.
- Plan: four plates.
  - Half Section E carries four labels: "1 1/8 Guss. Pl.", "3/4 Guss. Pl.", "3/4 Guss. Pl." (at the step) and "3/4 Guss. Pl." (lower).
  - The elevation has four separate extent lines parallel to L11-L12: "2-1 1/8 Gus. Pls. (S)" and three "2-3/4 Gus. Pls. (S)". Each ends at a different distance.
  - Dark-line profile across Section E (600 dpi): below the step there are 6 plate spaces (= 2 post webs 34 x 3/4 + 4 gusset plates); above the step there are 5.
- gusset-layers.csv lists the four labels in its note but models three: it took the step label and the lower label as the same plate.
- Proposed fix: add a fourth row (3/4) for main L11 in gusset-layers.csv, with the extents from GL03. Keep the dimension-only flag.

**GL02 - L11 stack modeled 1 1/8 in. too far out.** (L11 / L11')
- Severity: wrong. Confidence: medium for the offset, low for the exact chord-ply arrangement.
- Model: `GussetsInsideFaceDistance` = 29 (1 1/8 plate inner face 14.5 in. from the truss CL, outer face 15.625).
  - A 1 1/8 in. air gap separates the 1 1/8 plate from the post U11-L11 over 120 of 469 grid points per face. The modeled post is 26.75 o. to o., so it matches the plan.
  - Rivets of the post group have air in their grip.
- Plan: Half Section E prints "2'-5 o. to o. 1 1/8 Gus. Pls." and "2'-2 3/4 o. to o. Web Pls.".
  - So the 1 1/8 plates bear directly on the post webs, with outer faces 14.5 in. from the CL. Their in-to-in is 26 3/4.
  - "2'-6 1/2 b. to b. Ls" = 29 + 2 x 3/4 fits one 3/4 layer outside the 1 1/8 at the angle level.
  - The chords L10-L11 / L11-L12 are "2'-5 o. to o. Web Pls" (Views G / K, Section L), and the elevation prints "End of 1 1/8 webs" and "End of 2-o.s. 3/4 webs" at the joint. So the 1 1/8 gusset is inlaid in the chord's outer web layer.
  - Where the "1-3/4 Fill (C)" goes is not resolved.
- This is what the csx flag already suspects ("may be inlaid"). It is now supported by the printed o. to o. dimension and by the measured gap.
- Proposed fix:
  - connections-ptc.csx section 5: set L11 `GussetsInsideFaceDistance` = 26 3/4.
  - truss data: end the outer chord plies (o.s. 3/4 + 1 1/8) of L10-L11 / L11-L12 at the joint (`TrussPly.Start` / `Length`) or use `BetweenGussets`.
  - Re-read Sections B / D / G / H / K / L at 400-600 dpi first.

**GL03 - L11 layer outlines: all three modeled layers are clipped to a 29 SF envelope; the plates are drawn much larger.** (L11 / L11')
- Severity: wrong. Confidence: medium.
- Model:
  - inner 1 1/8 = envelope, u -32..+34 in., v -16..+65 in., 29.1 SF;
  - middle 3/4 = the same envelope, 29.1 SF;
  - outer 3/4 = clip box "-200,-200; 200,-200; 200,40; -200,40" (666.7 SF) clipped to 21.6 SF (Info line in the diagnostics).
- Plan:
  - The four extent lines run several feet up L11-L12. The 1 1/8 is longest, then 3/4, 3/4, 3/4, each ending further down. They carry no figures (scalable).
  - Section E: the middle 3/4 stops about 7'-2 above the El. 98.2010 line, not "whole joint"; the 1 1/8 continues higher; the outer 3/4 starts at the step about 3'-5 above that line (scaled 350 dpi, +-3 in.).
- Proposed fix: scale the four extent lines and the Section E steps into per-layer outlines (gusset-layers.csv `outline_uv_in`), together with G04 (inner outline). This is data only.

**GL04 - L11 outer-layer clip edge at v = +40 in. lies on a rivet row.** (L11 / L11', both planes, both faces)
- Severity: wrong. Confidence: high.
- Model: 11 rivets per face lie exactly on the clip line v = 40.00 (88 rivets in all).
  - Their grip counts the outer 3/4 layer, but the steel at the rivet axis ends at the middle layer.
  - Result: the head floats 3/4 in. off the steel, with zero edge distance on the outer layer.
- Plan: no plate edge drawn through a rivet row (p.80).
- Proposed fix: goes away with GL03. A generic guard would also help: warn when a layer edge passes within 1.5 d of a rivet.

### L10 / L10' (p.78) - H02

**GL05 - L10 stack checked OK.**
- Severity: none (checked). Confidence: medium.
- Plan: 3/4 inner on the chord + 1/2 outer. The 1/2 plate edges are drawn solid across the dashed diagonal outlines, so the plate is outside.
- Model:
  - inner face at 14.5 in. (2'-5 in. to in.), on the chord outer ply;
  - layers adjacent;
  - the 1/2 outline equals gusset-layers.csv (40.5 SF), not clipped;
  - grips 4.5 / 4.875 = 1/2 + 3/4 + chord plies.
- G06 (inner top edge) still applies to the 3/4 plate only.
- Proposed fix: none for the layering.

### L22 (p.90) - H03

**GL06 - L22 half gussets modeled as one plate per face.**
- Severity: unclear (known simplification). Confidence: high.
- Model: one 1 in. plate across the CL. The order (1 in. inside, 1 1/8 splice gusset outside) and both outlines match the sheet:
  - the 1 in. outline is 169 in. wide, equal to the extent to the jacking-frame line, with the lugs cut on Line "X" excluded;
  - the 1 1/8 outline is the drawn hexagon, 46.9 SF.
- Plan: "2-1 in. Gus. Pls. | 1/8 | 2-1 in. Gus. Pls." The half gussets are separate per half truss, 1/8 in. apart, and flame cut on lines "X" after field riveting.
- Proposed fix: optional. Two 1 in. layers split at the CL with a 1/8 in. gap; generic support would be a per-layer `Outline` for two pieces. Otherwise keep the flag text.

### Plates in the gusset stack (all main joints)

**GL07 - The plates between the chord web and the gusset (o.s. web splice plates, inlay plates, U22 pin plates) are not built from U8 / L8 on, so the gusset floats or the chord web layer has a hole.**
- Joints: U8 / U8', L9 / L9', U10 / U10', U21 / U21', L21 / L21', U22. Inlay holes at L10 / L10', L12, L22 and others. Both planes.
- Severity: missing. Confidence: high (the gaps are measured and the callouts read).
- Model: joint plates exist only at U1-U7 / L2-L7 / L15 (`N-SP*`, `N-F*` parts). Measured air directly under the inner gusset, against the chord:
  - U8 0.75 in. (262 of 337 grid points per face);
  - L9 0.75 (329 / 453);
  - U10 1.125 (312 / 398);
  - U21 1.125 (about 470 / 677; 1.625 near the members);
  - L21 1.125 (546 / 980);
  - U22: nothing within 2 in. under the 1/2 gusset; rivet grips 6.81 in. with 4.1 in. of air.
  - At L10 the second 34 x 1 1/8 web ply is absent near the joint ("End of 34 x 1 1/8 Webs") and the "2-Inlay Plates 34 x 1 1/8" are not built: a 1 1/8 in. air layer inside the chord web stack under both gusset layers (e.g. 5 ft left of L10: steel 13.375-14.5 and 11.25-12.25 in. from the CL, air between). Grip 4.5 in. contains 1.125 in. of air.
- Plan:
  - p.77 U8 / L9: "2-Spl. Pls. 34 x 3/4 - o.s." with gussets 2'-6 1/2 in. to in. over a chord 2'-5 o. to o.;
  - p.78 U10: "2-Splice Plates 34 x 1 1/8 - o.s.", 2'-7 1/4 in. to in. Gussets, 2'-5 out to out Webs;
  - p.89 U21 / L21: "2-O.S. Splice Pls 34 x 1 1/8", 2'-7 1/4 in. to in.;
  - p.90 Section H U22: pin plates 34 x 1 1/8 + 34 x 1 + 34 x 1 (2'-11 1/4 o. o. pin plates);
  - p.78 / 81 / 90: inlay plates.
- The space the plates fill equals the printed (in-to-in - chord o. to o.) / 2 at every joint.
- Proposed fix: data + script. splices.csv already has rows for U8, L9, U10, L10, L12, U21, L21, L22 and U22. Extend the connections-ptc.csx splice section (the one that builds `N-SPU*` / `N-F*` for U1-U7) to all main joints as `TrussJointPlate` WebOutside / inlay plates. The gussets then bear on them and the grips close.

**GL08 - Joint rivet grips do not follow the steel present: most joint rivets have air inside their grip.** (whole main span, both planes)
- Severity: wrong. Confidence: high (measurement).
- Model: 57,016 of the 76,090 joint rivets over gusset faces have more than 1/16 in. of air inside the grip:
  - 35,145 have air inside the member stack (missing plies / inlays, rivets outside the ply band);
  - 35,323 have a grip running past the last steel;
  - 7,128 have a gap between the gusset and the member (GL02 / GL07, missing fills);
  - a rivet can fall in more than one class.
- The grip is computed from the section definition + the gusset stack (`StackAt`), not from the steel along the shank.
- At the stacked joints the layer part of the grip is right: heads on the outer layer, every layer counted. The layering itself does not cause the air.
- Plan: the grip is the plates actually connected (p.70 rules).
- Proposed fix (generator): for joint rivets, take the grip from the contiguous steel along the shank (`TrussGenerator.SteelLineProbe`, as the connection rivets already do) and warn when air remains inside it. Most of the air disappears with GL07. This overlaps the main-span sections review; the two need coordinating.

### Data / unclear

**GL09 - gusset-layers.csv data hygiene.**
- Severity: unclear (data only, no model effect). Confidence: high.
- Model: the joint is built at 29 from gussets.csv; the U0 row of gusset-layers.csv is stale.
  - gusset-layers.csv U0 `in_to_in_in` 34 (= the chord web width).
  - The csv has no rows for L1 (single 1/2 per "similar to L3"), U22 (single 1/2) and W14.
- Plan: p.73 "2-7/8 Gussets (2'-5 in. to in.)"; gussets.csv already has the CORRECTION row.
- Proposed fix: U0 in_to_in 34 -> 29; add single-layer rows for L1, U22 and W14 (after G03) so the csv covers every joint.

**GL10 - W14 gusset make-up not read.** (W14 / W14')
- Severity: unclear. Confidence: low.
- Model: 3/4 envelope, overlapping the hanger by 0.75 in. (= G03).
- Plan: p.85 bottom-left detail shows "2-Pls 19 x 1 1/8" and "2'-5 In to In Guss.". Whether these are the W14 gussets (single, 1 1/8) or connection plates is not settled.
- Proposed fix: read p.85 / p.84 Section E with G03 before adding a layer row.

**GL11 - L5 "2-3/4 Fills (C)" at the chord ends of the gusset.** (L5 / L5')
- Severity: unclear. Confidence: low.
- Model: not checked against parts.
- Plan: p.75, two callouts at the top / bottom chord corners left of the joint. These are chord fills, not gusset layers.
- Proposed fix: check with the fills work (splices.csv has no L5 fill row).

**GL12 - Sheet links checked OK.**
- Every gusset layer part (`-L2` / `-R2` / `-L3` / `-R3`, both planes, both halves) inherits its joint sheet through the "Joint n" group: L10 / L10' p.78, L11 / L11' p.80, L22 p.90; U22 p.90.
- Severity: none.

**Plan-vs-plan conflicts:** none found. At L11, Section E, the elevation and the "o. to o." dimensions agree with each other. The disagreement in GL01 / GL02 is between the plan and the csv / model.

## 3. Counts

| Severity | Rows |
|---|---|
| wrong | GL02, GL03, GL04, GL08 (4) |
| missing | GL01, GL07 (2) |
| unclear | GL06, GL09, GL10, GL11 (4) |
| checked OK | GL05, GL12 (2) |
| plan conflict | 0 |

## 4. Not determined

- **Exact plate order and chord-ply arrangement at L11.** It needs a 400-600 dpi read of Sections B / D / G / H / K / L on p.80. Not settled: which chord plies end at the joint, where the 1-3/4 fill goes, and whether the 1 1/8 is the innermost plate everywhere. The drawn line spacings in Section E are too coarse to tell 3/4 from 1 1/8 reliably.
- **L11 layer extents in inches.** The extent lines carry no figures, and they were not scaled here: GL03 gives the reading method and the Section E steps only roughly (+-3 in.).
- **W14 gusset (p.85)** and the **L5 fills (p.75)**.
- **Which rivets of each drawn group lie under the L10 1/2 plate and the L22 splice plate.** The plan per-rivet layer membership was not compared; the model counts are given in section 1.
- **Gusset-to-vertical gap at L10 vs L10'.** The model gives 0.5 vs 1.0 in.; the mirrored outline / member width was not traced.
