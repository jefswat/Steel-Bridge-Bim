# PTC main span (bridge "PTC Arch truss 18-20") - gussets, rivets, lateral brace connections: review findings

Review only - nothing in the .sbm, csx, csv or src/ was changed. Date 2026-10-27.

**How the model was measured.** The PTC model was regenerated in memory with only the main span bridge kept (`work/dump.csx`, `work/tris.csx`, no save, heavy lock used). Results: 5,506 parts, 131,182 rivets, 1,191 members. Then a main-span clash ran with bolts and rivets on: 291,668 rows, of which 819 are non-bolt rows.

**What was compared.** The model was compared with:
- the sheets p.69-100 (crops in `work/pg`);
- `connections/*.csv`, overlaid on the sheets in `work/ovl` (red = the gusset-outlines.csv outline, green / blue = the rivet-positions.csv symbols, magenta = the drawn chord and work point);
- views of the model in the N truss plane (`work/mv`);
- the companion db `samples/plans/PTC Delaware River Bridge.sqlite`, opened read-only.

**Output files.** The scratch outputs are in `qc/ptc/main-span/work/`:
- `out/` holds the parts, rivets, members, points, clash, diagnostics, stacks and covers dumps.
- `brace_ends.csv`, `rivets_suspect.csv`, `rivet_count_diffs.csv`, `member_end_setbacks.csv` and `gusset_sheets.csv` hold the analysis results.

**Counts.** Unless a row says otherwise, the counts are for the N truss and the unprimed half. The model is symmetric: S and the primed half are the same.

**Columns.**
- Severity: wrong / missing / unclear.
- Confidence: high / medium / low.

## Summary - what makes it "look wrong"

1. **Lateral and strut bracing plates float off the laterals (B01, B02).**
   - 16,197 of the 20,838 rivets in the main-span lateral / strut connections (78 %) grip ONLY their own 1/2 in. plate or angle leg (grip 1/2 in.).
   - The 2-1/2 in. plates lie on the faces of the 19 in. deep strut. The laterals are 14 in. deep (2-13[ + plates), so the plates stand about 2-3 in. off them.
   - The edge angles lie beside the plate, not under it.
   - Result: the lateral ends are connected to nothing.
2. **Sway frames have no connections at 11 of 13 points (B04, B05).**
   - Apex plates at the strut exist only at L7, L8 and L11.
   - The sway TOP connection to the floor beam (p.85 "Bracing Connection 2-1/2 Gusset Pls, 4-Ls 6x4x1/2") is built nowhere.
   - p.97 says the L1-L6 / L9 / L10 / L12 apex details are "similar to L7 / L8".
3. **Members missing at L13 - L13 1/2 (B06).**
   - The lower lateral double K (33 WF 130) and the strut at L13 1/2 (2-18[ 45.8 + 2-Pls 31 x 1/2) with their plates are missing.
   - The model flag says L13 1/2 is "not dimensioned". p.98 dimensions it: "13'-1 Meas. along CL of Chord".
4. **Rivet counts at the bracing ends differ from p.71 (B03).**
   - Model per member end: laterals 40 (2-13[ 50: 48), struts 36.
   - p.71 diagram: laterals 26, struts 40 (32 / 36 / 40 / 28 / 38 / 46 / 50 at the special panels).
5. **15 % of the drawn joint rivets are missing (R01, R02).**
   - The model places 9,385 of the 11,008 drawn rivets per face (rivet-counts.csv); 6,522 drawn positions (both trusses and halves, per face) were dropped.
   - The worst groups are diagonals and verticals at L10, L12, L13 and L14, where 35-50 % are missing.
   - Cause found at L13 / L14: the rivets of the wind chord L13-W14 and of the rigid hanger L14-W14 are assigned to the DIAGONAL (rivets-ptc mapping), so they are laid out in the diagonal's frame and dropped. The hanger L14-W14 gets 0 of 86 drawn rivets.
6. **Web members overlap each other at almost every joint (G01).** Diagonal and vertical ends interpenetrate by 1/4 - 9/16 in. (108 clash rows; at U11, 9/16 in.).
7. **Drawn gusset edge angles are missing (G02).**
   - The joint sheets draw 2-Ls 4 x 3 1/2 x 7/16 (C) (U11: 2-Ls 5 x 3 1/2 x 7/16) along the free sloped edges of the gussets at about 20 joints, with their rivet lines.
   - None are modeled, so the drawn rivet lines along the edges are absent.
8. **Cover-plate splices are unriveted (R04).** 38 chord cover splice plates / fills (and the U5 inside splice plates) have no rivet through them. The "All Cover Plate Splices 1 1/8" rivets (p.70) are not placed.

## Counts by category

| Category | wrong | missing | unclear | plan conflict | total |
|---|---|---|---|---|---|
| G gussets | 3 | 1 | 3 | 0 | 7 |
| R rivets | 5 | 2 | 2 | 0 | 9 (+ R10 checked OK) |
| B bracing connections | 4 | 7 | 1 | 0 | 12 |
| C plan-vs-plan | 0 | 0 | 0 | 2 | 2 |
| S sheet links (db) | 1 | 0 | 1 | 0 | 2 |
| **Total** | **13** | **10** | **7** | **2** | **32** |

H layering = 4 handoff rows, not counted.

## G - Gussets (truss joints)

### Rows G01-G03: overlaps and missing edge angles

**G01 - Web member ends overlap each other inside the gussets.**
- Location: almost every web-member joint, both planes and halves: U5, L6, U7, L8, U9, L10, U11, L12, U13, L14, U15, U16, U17, U18 and their mirrors.
- Severity: wrong. Confidence: high.
- Model: diagonal x vertical clash 108 rows, depth 0.26-0.56 in. (U11 0.56, U16 / L16 0.53, U12-U15 0.44-0.50).
- Plan: the joint sheets draw the members ending clear of each other (dashed end lines with a gap). Examples: p.75 U5, p.78 L10, p.79 U11.
- Proposed fix (generator): add a web-to-web end clearance in `TrussGenerator.Setbacks`. Web members meeting at a point should stop clear of each other's section box, as `BracingCornerClearance` does for bracing. Optional input on `TrussJointDefaults`, e.g. `WebMemberClearance` of about 1/2 in.

**G02 - Gusset free-edge angles not modeled.**
- Location:
  - 2-Ls 4 x 3 1/2 x 7/16 (C) at U8, U9, L9, L10, U12, L12, U13-U21, L13, L14, L15, L16, L18, L19;
  - 2-Ls 5 x 3 1/2 x 7/16 (C) at U11.
- Severity: missing. Confidence: high (callouts), medium (extent).
- Model: no part; the rivets were excluded as "bracing angle strips" in rivet-count-zones.csv.
- Plan: p.77, 78, 79, 81-84, 86-89. The angles run along the sloped gusset edges with 1 1/8 / 1 1/4 rivet lines (L13 p.83: 17 + 13 rivets; L10 p.78: 6 + 14; L12 p.81: 16 + 6; listed in rivet-counts-connections.csv as not placed). The drawn lengths and "4 in." dimensions apply; the pitch is "2'-0 alt." on some sheets.
- Proposed fix (data + generator):
  - New optional `TrussJointDefinition.EdgeAngles` (angle size, edge of the outline, from / to along the edge, rivets per line).
  - Read the extents from the outlines already in gusset-outlines.csv (edge segments).
  - The lengths are drawn, not dimensioned, so they would be flagged.

**G03 - Rigid hanger L14-W14 wider than the W14 gusset gap.**
- Location: W14 / W14', both planes.
- Severity: wrong. Confidence: high.
- Model:
  - envelope gusset GW14 (no outline data), in-to-in 14 5/8 in.;
  - hanger 29 in. wide;
  - clash gusset x hanger 0.565 in.;
  - generator CHECK "29 in. wide, more than the gusset inside distance 14 5/8".
- Plan: p.85 "DETAILS FOR FLOOR BEAM AND RIGID HANGER AT PANEL PT W14" (bottom left) and p.84. The W14 joint is drawn there and has not been read.
- Proposed fix (data):
  - Read p.85 bottom-left / p.84 for the W14 gusset thickness, in-to-in and outline.
  - Add a gussets.csv / gusset-outlines.csv row; today W14 has no outline row.

### Rows G04-G07: outlines

**G04 - Main L11 / L11' gussets still an envelope.**
- Location: L11 / L11', both planes.
- Severity: unclear. Confidence: medium.
- Model: inner 1 1/8 plate 29.1 SF per face (the largest joint of the bridge has the smallest gusset, next to L10 105 SF and L12 96 SF).
- Plan: p.80. Edges are not separable at 150 dpi (TRUSS-DESIGN 24).
- Proposed fix (data): re-read p.80 at 300-400 dpi using the dimensioned 3/4 layer step lines. Layer part: see H01.

**G05 - U11 outline right edge inside the drawn plate.**
- Location: U11 / U11' / U11-mirror (from p.79).
- Severity: wrong. Confidence: medium.
- Model: the outline right edge (gusset-outlines.csv) lies about 30-50 px at 300 dpi (= 3-6 in.) left of the drawn plate edge along the 2-Ls 5 x 3 1/2 edge angles.
- Plan: p.79, overlay `work/ovl/U11_p79.png`. The drawn right edge runs from about x 1480 at the top to x 1410 at the bottom (300 dpi crop px).
- Proposed fix (data): re-trace the U11 right edge in gusset-outlines.csv. Consider G02 together: the angle lies on that edge.

**G06 - L10 inner 3/4 gusset top edge drawn about 7 in. lower than the data outline.**
- Location: L10 / L10'.
- Severity: unclear. Confidence: low.
- Model: the outline top edge is at the vertical's rivets about 7 in. above the drawn line through (395,425)-(700,300) / (950,300)-(1225,395).
- Plan: p.78, overlay `work/ovl/L10_p78.png`. The top two vertical rivets lie on the line drawn above; they may be the vertical's own field above the plate.
- Proposed fix (data): re-read p.78 at 300 dpi and decide which line is the 3/4 plate edge.

**G07 - U5 outline top-left corner extends about 8 in. left of the drawn plate edge.**
- Location: U5 / U5' and joints drawn alike.
- Severity: unclear. Confidence: low.
- Model: the outline corner (240,420) vs the drawn left edge at about x 310-318 (300 dpi crop).
- Plan: p.75, overlay `work/ovl/U5_p75.png`. The chord rivets at x 270-290 are left of the drawn edge (they may be on the fills / splice plates, not the gusset).
- Proposed fix (data): re-read; if confirmed, move the corner and mark those rivets as 'beyond'.

## R - Rivets

### Rows R01-R05: lost, misassigned, unriveted and edge distances

**R01 - Drawn joint rivets dropped ("no clean plate stack / free head spot").**
- Location: all main joints. Per-face drops (N plane, unprimed): L13 110 / 533, L10 105 / 488, L12 91 / 498, L14 85 / 422, U11 65 / 466, L22 63 / 407, L6 54 / 334, U1 49 / 126, L15 49, U21 49, U9 47, U5 46 ...
- Severity: wrong. Confidence: high (count), medium (cause).
- Model:
  - Generator summary: 6,522 of the drawn positions dropped. 3,728 lie outside the member section's in-plane extent and 1,346 inside-gusset drops are in the N plane.
  - Placed vs drawn per face (N, unprimed): 9,385 vs 11,008 (-15 %).
  - 93 member groups differ by more than 15 % (`work/rivet_count_diffs.csv`).
  - Worst: U9-L10 at L10 52 / 100, L10-U11 56 / 98, U12-L13 60 / 96, U13-L13 76 / 111, U12-L12 65 / 97, U14-L14 43 / 75.
- Plan: rivet-counts.csv / rivet-positions.csv (p.73-90).
- Proposed fix (generator / data): trace the dropped positions per group, starting with the worst ones. Causes found:
  - see R02 (members misassigned) and G01;
  - the vertical ends at L12 / L13 / L14 are 10-14 in. higher than drawn (R05).
  - Add a diagnostic CSV of the dropped positions to TrussGeneratorRivetPositions (dropped reason per symbol).

**R02 - Rivets of the wind chord L13-W14 and of the rigid hanger L14-W14 assigned to the diagonals.**
- Location: L13 (L13-W14 -> U12-L13) and L14 (L14-W14 -> U13-L14), both planes and halves.
- Severity: wrong. Confidence: high.
- Model:
  - diagnostics: "rivet-counts p.84 inside main L14 L14-W14 (41 shop + 23 field)" is placed under member N-U13-L14;
  - "p.83 inside main L13 L13-W14 (0 + 11)" is placed under N-U12-L13;
  - the hanger L14-W14 gets 0 of 64 inside + 22 beyond drawn rivets per face; the misplaced ones are dropped or land in the diagonal's frame.
- Plan: p.83 / p.84.
- Proposed fix (script): rivets-ptc.csx member mapping. Map the "L14-W14" and "L13-W14" groups to the model members N-L14-W14 (role Hanger) and the wind chord L13-W14 (LowerLateral FL-X13-15a1/b1). Today the member-class rule (vertical / diagonal / chord) does not know hangers or wind chords.

**R03 - "unassigned" drawn rivets not placed.**
- Location: L16 18, L17 24, L20 19 per face, plus their 'similar to' joints.
- Severity: missing. Confidence: medium.
- Model: 0.
- Plan: p.86, p.87, p.89 (rivet-counts.csv member "unassigned").
- Proposed fix (data): assign these symbols to members (hand zones in rivet-count-zones.csv) or record why they are not gusset rivets.

**R04 - Chord cover-plate splices have no rivets.**
- Location: 38 splice / fill plates per truss-half (N): cover splice plates top and bottom at U1-U5, L2, L4, U18-U21, L18-L21; cover splice fills; also the U5 inside web splice plates 31 1/2 x 7/8 and the 34 x 3/8 fills (WebInside).
- Severity: missing. Confidence: high.
- Model: no rivet passes through them (clash bolts x part = 0); list in the run output.
- Plan: p.70 "All Cover Plate Splices: 1 1/8 in. dia high strength rivets". The flange plan views on p.74-90 draw them (rivet-counts-connections.csv: about 26-49 per plan view, e.g. U18 33, L18 35, U19 40, U20 44, U21 41, L20 36, L21 34, U22 49).
- Proposed fix (data + generator): place the flange-plan-view rivets as rows on the Top / Bottom joint plates (TrussRivetGroup on the cover face, vertical axis). The rivet lines are drawn but not dimensioned (pitch "4 in. max").
- U5 inside splice: why the web rivets do not reach the WebInside plates is unclear (grip stops at the web).

**R05 - Joint rivets closer to the gusset edge than 1 11/16 in., some outside the plate.**
- Location: 41 joint faces, 374 rivets per face (N, unprimed). The tightest centre-to-edge distances are at U3 0.06, U6 0.03, L7 0.05, U8 0.05, U13 0.04 in., and U16 -0.05, U17 -0.06, U18 -0.10 in. (outside the plate).
- Severity: wrong. Confidence: medium.
- Model: covers.csv; generator CHECK "rivets per face lie outside the gusset outline or closer than their edge distance".
- Plan: drawn outlines vs drawn rivets (both read from the sheets, about +-1 in.). Concentrated at the upper chord joints: top edge on the chord top line vs the chord field.
- Proposed fix (data): re-check the U3-U21 outline top edges against the chord top line on p.75-89. The rivet heads (about 1.9 in. diameter) overhang there.
- Vertical ends at L12 / L13 / L14 (end 32.8 / 31.1 / 29.7 in. from the WP; drawn about 19 in. at L13, p.83): the drawn vertical rivets below the model end are dropped. That is a separate generator row, R06.

### Rows R06-R10: member ends, grips, head overlaps, diameters

**R06 - Vertical ends at the lower chord joints L12-L14 end higher than drawn.**
- Location: U12-L12, U13-L13, U14-L14, both planes.
- Severity: wrong. Confidence: medium.
- Model: the end is 32.8 / 31.1 / 29.7 in. from the WP.
- Plan: p.83 L13. The vertical rivet field reaches about 19 in. above the WP (scaled 300 dpi, 8.6 px/in.); p.81 / p.84 alike.
- Proposed fix (generator): the end rule (chord envelope + `WebMemberClearOfJointPlates`) is computed at the vertical's lower corner on the steep L12-L13 / L13-L14 chord. Consider the vertical end at the chord top at its CL with a cope, or an explicit `EndSetback` per member from the sheet.

**R07 - Rivets through one part only (grip = one ply) on the web members beyond the gussets.**
- Location: verticals (276 rivets with grip 1/2, 100 with 7/16, 120 with 9/16) and diagonals (168 with 1 1/16).
- Severity: unclear. Confidence: low.
- Model: the rivet touches only the member itself; with grip 1/2 that is a single plate, so it connects nothing.
- Plan: "beyond the gusset" symbols on p.73-90 (fills / splice / end diaphragms).
- Proposed fix (data): review which plate those drawn symbols belong to (fill, tie plate, end diaphragm); possibly not built (see R04).

**R08 - Rivet heads overlapping.**
- Location: L14 / L14' 52 per half (> 1/4 in.), L7 16, FB13 8, U6 / L10 / U12 / U16 / U19 4, SW13 4.
- Severity: wrong. Confidence: high.
- Model: bolt x bolt clash 739 rows, depths up to 0.55 in.
- Plan: drawn rivet spacing at least 3 in.
- Proposed fix (generator): at L14 the cause is the R02 mapping (hanger rivets laid in the diagonal frame over the diagonal field); re-check after R02. Elsewhere tighten `MergeRivet` / the head-clear rule for rivets from different groups.

**R09 - Upper chord joint U11 post rivet line along the right edge not in rivet-positions.csv.**
- Location: U11 (p.79).
- Severity: unclear. Confidence: low.
- Model: the column at the post right edge (about 15 solid dots) was skipped as "drawn in profile".
- Plan: p.79 (overlay U11). These dots are drawn like the field rivets; the left edge symbols are crossed (profile).
- Proposed fix (data): re-check the U11 SKIP zone (1510-1523 px) in rivet-count-zones.csv.

**R10 - Diameters.**
- Location: all joints.
- Severity: none.
- Model: the p.70 rule per JOINT is followed (the chord rivets at L6 / U6 / U13 / L15 / L20 / U20 are 1 1/4 as the rule lists the joint).
- Plan: p.70.
- Proposed fix: none - checked OK.

## B - Lateral / strut / sway / portal / floor lateral connections

### Rows B01-B06: plates, edge angles, counts, sway connections, L13 1/2

**B01 - Lateral / strut bracing plates do not bear on the laterals.**
- Location: all lower (BGL1-BGL22, BGLM) and upper (BGU11-BGU22, BGUM) plate pairs, both trusses.
- Severity: wrong. Confidence: high (model), medium (plan detail).
- Model:
  - 16,197 of 20,838 rivets of "main span lateral / strut gusset plates" grip only the 1/2 in. plate (`work/rivets_suspect.csv`).
  - Example at L5: N-BGL5-T underside at z 117.88 ft, lateral LL-NL5-LM4 top about 117.65 (2.8 in. gap); N-BGL5-B top 116.33, lateral bottom about 116.49 (1.9 in. gap).
  - The plates are set on the 19 in. strut (2-18[ 45.8 + 2-Pls 29 1/2) faces. The 14 in. laterals (2-13[ 31.8 + 2-Pls) lie between them.
- Plan: p.97 DETAILS SHOWN FOR L1 / Section A (Section A shows "1/2 Fill", 7/8 rivets); p.98.
- Proposed fix (generator + data): how the shallower laterals meet the plates is not dimensioned (the fill thickness is drawn at Section A only).
  - Option 1: put the laterals' cover-plate faces on the plates.
  - Option 2: add fills (`TrussBracingGussetDefinition` fill under each member lap = probed gap, like `OutFillPlanThickness`).
  - Either way, rivets must grip plate + fill + member.
  - Flag it, because the fill is not dimensioned except "1/2 Fill".

**B02 - Bracing plate edge angles 2-Ls 7 x 4 x 1/2 connect nothing.**
- Location: all BGL / BGU / BGFX edge angles (N and S).
- Severity: wrong. Confidence: high.
- Model:
  - The angle's horizontal leg lies BESIDE the plate (at L5: angle leg n -42.23..-41.90, plate edge at -41.83). It should lie under or over the plate.
  - The vertical-leg rivets grip 1/2 only (the gusset is not reached).
  - About 3,600 edge-angle rivets grip one part.
- Plan: p.97 L1 detail (the angles lap the plate and are riveted to the gusset Pl. at the chord: 13 rivets per leg).
- Proposed fix (generator): in `TrussGeneratorBracing` edge-angle placement, the plate leg must lie on the plate face (the plate outline should reach under the angle up to the stack `Gap`), and the stack leg must lie on the gusset / chord face. Rivet grips probed through angle + plate and angle + gusset stack.

**B03 - Rivet counts per bracing member end differ from the p.71 diagram.**
- Location: all laterals and struts.
- Severity: wrong. Confidence: high.
- Model:
  - per plate x 2 plates: lateral 2-13[ 31.8 = 20 x 2 = 40, lateral 2-13[ 50 = 24 x 2 = 48, strut = 18 x 2 = 36, strut at the apex 12 x 2;
  - edge angle 13 per leg.
- Plan, p.71 TOP CHORD LATERAL SYSTEM:
  - "26-Riv. each end of lateral diagonals", "40-Riv. each end of Struts", "16 Riv." at the U22 / CL ends.
- Plan, p.71 BOTTOM CHORD LATERAL SYSTEM, lateral diagonals:
  - 26 each end (L0-L8, L16-L22);
  - 32 (L8-L9), 36 (L10-L11), 40 (L11-L12), 28 (L13-L13 1/2), 38 (L15-L16).
- Plan, p.71 BOTTOM CHORD LATERAL SYSTEM, struts:
  - 40 each end;
  - 46 (L8-L10), 50 at L12, 46 at L13 1/2.
- Note A: detail sheets govern only where the diagram gives no number.
- Proposed fix (data): bracing-ptc.csx section 2. Use the p.71 counts per END (split over the 2 plates). The script comment "laterals 20, struts 18" (read on p.97 details) contradicts the diagram, which is the governing number per Note A.

**B04 - Sway frame apex connections at the strut missing.**
- Location: SW0-SW6, SW9, SW10, SW12 (and primes); only SW7 / SW8 (p.97) and L11 (p.80) are built.
- Severity: missing. Confidence: high.
- Model: no plate / angle / rivet at the apex (`work/brace_ends.csv`: 0 plates, 0 rivets).
- Plan: p.97 "CROSS SECTION AT CL BRIDGE - Details to be similar to those shown for L7, except as noted" (the section at L1), plus the sections at L9, L10 ("Other details similar to those shown for L8") and L12. p.71 SWAY BRACING "at P.P. 0 and 1 to 10 incl. and 12".
- Proposed fix (data): details-ptc.csx section 1. Apply the L7 / L8 sway plate definition to every sway point listed on p.71 / p.97 (strut top plate 1-Pl. 15 x 1 1/8 / 1 1/4 per section). The script flag "drawn at one point only" is contradicted by the 'similar' notes.

**B05 - Sway frame TOP connections (diagonal to floor beam / vertical) missing at every point.**
- Location: all sway diagonals SW-N/S 0-12, upper ends at the floor beam.
- Severity: missing. Confidence: high.
- Model: no part; the sway A ends only touch the lateral plates of the chord joint.
- Plan: p.85 Section D-D / elevation "Bracing Connection: 2-1/2 in. Gusset Pls, 4-Ls 6 x 4 x 1/2 (C), 1 in. Rivets (C)", 1'-4 / 6 in. / 1'-3 alt. dimensions, 6 x 12 hand holes 2'-6 c.c.; drawn at the typical framed floor beam P.P. 6-10 and 12. p.73 for L0 (not read).
- Proposed fix (data + generator): BracingGussets at the floor beam bottom flange end ("Transverse" plate pair + 4 angles on the floor beam), rivets as drawn. Outline scaled from p.85 (flag).

**B06 - Lower lateral double K L13 - L13 1/2 and the strut / 33 WF 130 at L13 1/2 not built.**
- Location: panel L13-L14, both trusses and halves.
- Severity: missing. Confidence: high.
- Model: generator flag "UNRESOLVED (p.71): ... the L13 1/2 point is not dimensioned on the sheets read".
- Plan:
  - p.98 BOTTOM CHORD BRACING ADJACENT TO L13: "13'-1 Meas. along CL of Chord" from L13 to the 13 1/2 point, 2-1/2 in. Pls., 2-Ls 7 1/2 x 4 x 1/2 (cut ...), 33 WF 130 diagonal, plan view with L 5 x 4 x 7/16 and 1 in. carbon rivets;
  - p.71: 2-18[ 45.8 (S) + 2-Pls. 31 x 1/2 strut at L13 1/2, "28" rivets lateral, "46 at L13 1/2" strut.
- Proposed fix (data): truss-ptc-arch.csx point L13 1/2 at 13'-1 along the chord, members + bracing-ptc plates. The flag reason is wrong.

### Rows B07-B12: L0, WM22, portal, floor laterals, L13 / L11

**B07 - L1-LM0 laterals: no plate at the LM0 apex (L0 strut).**
- Location: LM0 / LM0'.
- Severity: missing. Confidence: medium.
- Model: brace end LM0 has no plate (8 strut rivets nearby).
- Plan: p.71 "See Sketch" at L0; p.73 (L0 joint, sketch).
- Proposed fix (data): read the p.71 / p.73 sketch at L0; add the plate at LM0 or on the box strut.

**B08 - Floor lateral K 2-12[ 25 at WM22 (tongue beam): no connection.**
- Location: FL-K21-22N/S and primes, B end at WM22.
- Severity: missing. Confidence: high.
- Model: 0 plates, 0 rivets at WM22.
- Plan: p.71 FLOOR LATERAL SYSTEM "Shear Lock / Tongue Beam 16 WF 88, 2-Pls 11 1/2 x 1/2 x 5'-6" (See detail sheet 53)" -> p.93; p.99.
- Proposed fix (data): read p.93 / p.99 for the K apex plate at the tongue beam. The tongue beam itself is flagged (16 WF 88 not in AISC v16).

**B09 - Portal at U11: portal strut-to-post and knee-to-post connections, knee gussets, rivets missing.**
- Location: PORTAL-U11 / U11'.
- Severity: missing. Confidence: medium.
- Model: 4 rivets near the portal ends (from other groups); knee pieces built; connections flagged.
- Plan: p.100 (knee gussets 2-3/4 / 5/8, splice, 1 in. rivets); p.79 Section C.
- Proposed fix (data): read p.100 knee / post connection; add plates and rivets; a curved-part rivet layout is needed.

**B10 - Floor lateral connections at the wind chord W15-W21 and the floor beams FX14-FX20: partial.**
- Location: FX14-FX20, W15-W21.
- Severity: wrong. Confidence: medium.
- Model:
  - 139 of 640 floor lateral rivets grip one part only;
  - 6 clash rows FX plate x floor beam bottom cover (0.23-0.31 in.);
  - floor beam web slot / fillers / angles to the web not built (flag).
- Plan: p.99 W14 / W16-W20 details, Sections A / B.
- Proposed fix (data): finish p.99 (fills between the plate and the floor beam, the angle to the floor beam web); see C01.

**B11 - L13 lateral connection on F8.**
- Location: L13 / L13'.
- Severity: missing. Confidence: medium.
- Model: no BG plate at L13 (BGL13 absent; details-ptc sets 3/4 in. plates at L13, but no part results). Generator note: "bracing plate at L13 stops clear of LL-NL12-LM13".
- Plan: p.95 Sections B-E: lateral gussets riveted to the F8 bottom flange, 3/4 Gusset Pl.; p.98.
- Proposed fix (generator / data): find why the L13 BracingGusset produces no plate (clipped away by the F8 crossing rule?); add with p.95 / p.98 values.

**B12 - L11 box strut lateral plates: "a / b" plates per lateral, no edge angle; strut end tees modeled as angles.**
- Location: L11 / L11', L0.
- Severity: unclear. Confidence: low.
- Model: flagged in details-ptc / bracing-ptc.
- Plan: p.80 Section A ("Cut from 24 WF 94"), p.73 END VIEW / F / G.
- Proposed fix (data): keep; re-read with B01.

## C - Plan-vs-plan conflicts (reported, not adjusted)

**C01 - Wind chord top vs suspended floor beam bottom.**
- Sheets: p.69 elevations vs p.94 floor beam depth (p.99 beveled fill).
- Conflict: about 0.3 in. overlap; the floor lateral bottom plates sit 0.23-0.31 in. in the floor beam covers.
- Model: kept as printed (flag exists).

**C02 - Bracing rivet counts per end: the p.71 diagram vs the p.97 / p.98 detail drawings.**
- Sheets: p.71 vs p.97 / p.98.
- Conflict: the script read 20 / 18 per plate from the details; p.71 prints 26 / 40 per end; Note A says the details govern only where the diagram has no number.
- Model: the diagram should govern (B03); confirm with the user.

## S - Sheet links (companion db, read-only)

Query: every element of category "gusset plate" of bridge "PTC Arch truss 18-20" (396 parts, every layer, both planes and halves) through `v_element_sheets`, compared with the gusset-outlines.csv / gusset-layers.csv pages. All 396 have a sheet (direct or inherited from the "TRUSS-x Joint n" group). Full list: `work/gusset_sheets.csv`.

**S01 - W14 gussets linked to the wrong sheets.**
- Location: N-GW14-L/R, S-GW14-L/R and the primes (8 parts).
- Severity: wrong. Confidence: high.
- Model: linked to p.69 / p.70 only (stress sheet / member tables). Neither shows the W14 gusset.
- Plan: p.85 "DETAILS FOR FLOOR BEAM AND RIGID HANGER AT PANEL PT W14" (and p.84).
- Proposed fix (data): sheet-links-ptc-delaware-river.json, joint rule for W14 -> p.85 (+ p.84).

**S02 - L13-L17 gussets also linked to p.69.**
- Location: L13, L14, L15, L16, L17 (40 parts).
- Severity: unclear. Confidence: low.
- Model: p.69 + the joint sheet. p.69 is the half longitudinal section that gave the chord make-up, not the gusset.
- Proposed fix (data): keep or drop p.69 per the user rule "only the sheets used to model it" (p.69 was used for the inlaid L14 gusset layer, TRUSS-DESIGN 21).

L1 is linked to p.74 + p.75: its outline is taken from L3 (p.75) and p.74 says "similar to L3". That is OK.

## Layering (handoff to the dedicated GussetLayers agent; not deep-dived here)

**H01 - Main L11 / L11'.**
- Model:
  - inner 1 1/8 envelope 29.1 SF;
  - middle 3/4 = whole joint clip, 29.1 SF;
  - outer 3/4 = clip box "-200,-200;200,-200;200,40;-200,40", 21.6 SF.
- Plan: p.80 Section E; three dimensioned lines of 3/4 plate ends along L11-L12.
- Note: the outer layers are clipped to the inner envelope, so all three layers are small (the largest joint has the smallest stack).

**H02 - Main L10.**
- Model: 3/4 + 1/2; 1/2 layer 40.5 SF.
- Plan: p.78.
- Note: the inner outline top edge (G06) affects the clip of the 1/2 layer.

**H03 - Main L22.**
- Model: 1 + 1 1/8, 121 + 46.9 SF.
- Plan: p.90.
- Note: the 1/8 in. gap between the half gussets is not modeled (known).

**H04 - Gusset in-to-in values.**
- Model: in-to-in 29 in. at 75 of 92 N-R gussets; others 30 1/2 / 31 / 31 1/4 / 32 3/4 / 35 1/4 / 14 5/8 (W14) per gussets.csv / gusset-layers.csv.
- Note: all equal the data; the W14 value is the envelope default (G03).

## Not determined in this run

- The per-symbol reason for each dropped drawn rivet (R01). The generator only reports counts per group; a dropped-position dump is needed (proposed in R01).
- The p.73 L0 sketch (B07), p.93 tongue beam K connection (B08), p.100 knee connections (B09) and p.85 W14 joint (G03 / S01) were not read in detail. They are listed from the callouts.
- How the 14 in. laterals meet the 1/2 in. plates of the 19 in. struts (B01). The fill is drawn only as "1/2 Fill" at p.97 Section A.
- The outline overlays were checked by eye at L10, U5, U11 and L13 only. The other 37 overlays are in `work/ovl/*.png` for review. The model reproduces the data outlines exactly (all areas within 0.1 SF), so any outline error is a data (reading) error, not a placement error.
- Positions as built vs the S truss: S was not checked separately (the generator mirrors N).
