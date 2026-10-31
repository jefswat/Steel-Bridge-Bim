# PTC main truss (spans 18-20): rivets, agent review (2026-10-30)

Scope: the "PTC Arch truss 18-20" rivets in `samples/plans/PTC Delaware River Bridge.sbm`, checked against the PDF joint sheets (p.70 and p.73-90, plus p.80 notes and bracing p.97-99). The review covered:
- counts;
- positions;
- diameters;
- grips;
- member stitch rivets;
- connection rivets;
- flush heads;
- dropped / not-placed rivets;
- the Bolt x Bolt clash rows.

The review is read-only: nothing in `src/`, the scripts or the .sbm was changed.

**Method.** I regenerated a scratch copy of the model (debug CLI copied to `C:\SBM\temp\cli-rv`, heavy lock taken) and dumped from it:
- every fastener (frame, diameter, grip, flush, status);
- the parts' bounds;
- the panel points;
- `TrussModel.DroppedRivets`, `GussetRivetCovers`, the checks and the diagnostics.

`sbm clash --bolts` was then run on the same copy. Its result was 439,875 rows in total, of which 650 are Bolt x Bolt. Bolt x part rows are expected, because no holes are cut. Only rows deeper than the shank radius were looked at.

The drawn positions (`connections/rivet-positions.csv`) were overlaid on the sheets with the dropped positions marked: L10, L12, L13, U9 and L6, plus pairs at L13. Gage lines were measured on the p.81 raster.

Scratch scripts and outputs are in the session scratchpad `rv/`: `dump.csx`, `overlay.py`, `pairs.py`, `bb_class.csv`, `groups.csv`, `dropped.csv`.

## Numbers (south truss, one gusset face; the north truss is identical)

| Item | Count |
|---|---|
| Rivet symbols counted for the joints (rivet-counts / -extra, own + mirrored + similar) | 23,750 per face |
| Placed as drawn | 20,517 (86 %) |
| Dropped | 3,233 |
| - dropped: symbol outside the member section (profile rivets at member edges, edge-angle strips, text) | 1,878 |
| - dropped: inside the section, no clean stack (of these 370 chord edge rows at y = 16-18 in.) | 1,049 |
| - dropped: beyond the member's cut length | 267 |
| - dropped: on another rivet's head | 39 |
| Main truss rivets in the model (TRUSS-N / -S) | 83,868 (1 1/8: 49,552; 1 1/4: 34,316) |
| Floor / bracing / shoe fasteners | FLOOR 26,842, BR 21,382, SHOE 160 rivets + 208 tap bolts |
| Flush (countersunk) heads | 0 |
| Bolt x Bolt clash rows | 650 |

## Bolt x Bolt classification (650 rows)

Each pair was classified by axis direction, distance between the axes and axial offset:
- **288 rows, of which 544 are rivet-to-rivet:** parallel rivets with heads overlapping on the same face. Axis distance is 1.75 in. (1 1/8 in.) or 2.0 in. (1 1/4 in.), clash depth mostly 1/16 in.
  - 285 pairs are inside the joint fields: 260 have equal grips, spread over 55 joints (L12 14, L14 12, L10 10, L7 10 ...).
  - 24 pairs are BR-LOWER / UPPER-LATERALS vs truss joint rivets.
  - **Not double holes.** The main truss has NO member stitch rivet lines, so the earlier label "joint vs chord stitch" was wrong.
  - **Cause (data):** the detector reads two symbols 0.5-2.0 in. apart where the sheet has one rivet. 323 such same-member pairs are in `rivet-positions.csv`. Examples (overlay at L13): profile crosses next to dots, callout circles such as the "D" bubble, double rings at the post top.
  - **Cause (generator):** `TrussGenerator.Free()` lets button heads touch exactly (0.8 (d1 + d2)), and the faceted head mesh then reports a 1/16 in. overlap.
- **48 rows:** L11 shoe tap bolts (vertical, in the shoe angle's horizontal leg) crossing the 'rivets outer angles to gussets' (horizontal, in the vertical leg). Axis distance is 2.12 / 6.18 in. at the same U stations.
  - This is a real data conflict in `shoes-ptc.csx`: the two fastener sets of one angle sit at the same U.
- **4 rows:** 'same hole', axis distance 0.85 in., 1 1/4 rivets at about Sta 685+88 (L13' area). Real double rivets.
- **4 rows:** shank-touch, axis distance 1.19 in., at Sta 679+72 (L13 area). Real double rivets.
- **2 rows:** floor rivet 7/8 crossing a truss rivet at L13 (Sta 680+01). Real.

Bolt x part rows deeper than the shank radius:
- 104 + 104 shoe tap bolts in the forging (tapped holes, plan-consistent).
- 340 rivets buried in bracing-plate fills 1 in. to 11 7/8 in. thick at U11 / LM16 / LM22 (see R14).
- 8 rivet heads 0.65-0.67 in. into the L13-L14 angles (R15).

## Findings

| # | Joint / member | Item | Plan (page) | Model | Verdict | Proposed fix |
|---|---|---|---|---|---|---|
| R01 | all chord joints | outer chord gage rows | sheets draw outer gage lines about 1.5 in. inside the angle backs (p.81 L12: gage lines at +-15.7 in. from the chord centre on 34 in. webs; angle backs at +-17.25, 2'-10 1/2 b. to b.) | rows mapped to y = 16-18 in. have no clean stack (the head would sit on the outstanding leg); 370 chord rivets dropped per face (L12 20, L10 14, L15 14, L20 11 ...) | DIFF | Data (`rivets-ptc.csx` chord block, about line 355): after the shear, centre each chord member's drawn field on the model web. The drawn field centres are 0.5-2.4 in. off: U5 -2.35, U16 -2.13, U9 -1.76, L22 +1.63, U15 +1.65, U12 -1.59, L5 -1.49, U11 -1.41 (table in scratch output). The sheets draw the true geometry, so the offset is a work point / `chord_px` reading error; or re-read `wp_px` on those sheets. The 3/4 in. nudge then covers the rest. |
| R02 | L9, L10, L12, U9, U19, U20 (both halves, both trusses) | gusset edge angles L 4 x 3 1/2 x 7/16 (C) | drawn on the gusset edges with their rivet rows (p.77 U9 / L9, p.78 L10 left + right, p.81 L12 left + right, p.88 U19, p.89 U20); counts in rivet-counts-connections.csv: L10 6 + 14, L12 16 + 6 | no SKIP zone in rivet-count-zones.csv: edge angles NOT built (`connections-ptc.csx` 4d builds them only from zone rows); their rivets were counted into the diagonals and dropped as 'stack-outside' (L10 21, L12 21, U9 15, U20 11, L9 8, U19 6 per face) | DIFF | Data: add the 'bracing angle strip ... on the gusset edge' SKIP rows (page px polygons) for these joints in `connections/rivet-count-zones.csv` and re-run `tools/ptc_rivet_count.py` for the affected members. connections-ptc 4d then builds the angles with the counted rivets. |
| R03 | all joints | profile rivets at member edges | rivets in outstanding legs (cover plates / lacing to the angles) are drawn in profile (crosses) along every member edge | detector read them as rings: placed nowhere, but included in the counts (rivet-counts.csv totals inflated about 5-8 %) | DIFF | Data: as for L15 / U11-U17, add 'rivets drawn in profile' SKIP zones for the remaining joints (U0-U10, L0-L12, U18-U22, L18-L22) and re-count. The model stays the same (the drops already remove them); the counts in rivet-counts.csv become right. |
| R04 | 55 joints | near-duplicate drawn positions | one rivet per hole on the sheets (no spacing under 3 d is drawn) | 323 same-member position pairs 0.5-2.0 in. apart; 285 placed pairs with touching heads (Bolt x Bolt rows) | DIFF | Data: in `rivet-positions.csv` drop the weaker symbol of every pair closer than 0.8 (d1 + d2) (keep the one on the gage line), reviewed on overlays. Generator (optional): `TrussGenerator.Free` with a strict inequality plus a small clearance (e.g. 1/32 in.), so touching heads are not placed. |
| R05 | main truss members | stitch rivets (1 in. carbon) | p.70 'All Truss Members: 1 in. carbon steel stitch rivets, unless noted'; p.80 'Stitch rivets for truss members 1 in. carbon'; max pitch notes p.70 / p.80 (4 + 4t, 7 in.; 12t compression); gages drawn in the member views, not dimensioned | none: `rivets-ptc.csx` gives RivetLines only to the girder spans | DIFF / UNRESOLVED | Generator support exists (`TrussMemberDefinition.RivetLines`). Data needed: the gage lines of each section from the member views (p.69 sections, p.79 / p.80 member views). Pitch = p.70 maximum pitch (flag as rule-based like the girders, p.70 is a plan rule, not a code default). Until then: UNRESOLVED (p.70): stitch rivet gages not dimensioned. |
| R06 | U18, U19, L18, L19, U20, U21, L20, L21, U22 | cover (flange) splice plate rivets | flange plan views counted: U18 33, L18 35, U19 40, L19 26, U20 44, U21 41, L20 36, L21 34, U22 49 (rivet-counts-connections.csv, p.88-90) | splices.csv rivet_count blank ('counts not made'), so no rivets on these cover splice plates | DIFF | Data: copy the counts into `connections/splices.csv` rivet_count (rivet_rows 5) for these rows; diameter 1 1/8 (R07). Also U11, L12, L22: counts still not made (UNRESOLVED p.79 / p.81 / p.90). |
| R07 | U20, U21, L20, L21, U22 | cover splice rivet diameter | p.70 'All Cover Plate Splices: 1 1/8 in. dia high strength rivets' | rivet-counts-connections.csv lists 1.25 for the U20 / U21 / L20 / L21 / U22 flange plan views | DIFF (data) | `rivet-counts-connections.csv`: rivet_dia_in 1.125 for the 'flange plan view' rows at U20-U22, L20, L21 (p.70 governs; the 1 1/4 rule is for chord webs). |
| R08 | L11 bottom transverse strut | strut end fasteners | p.80 note: 'Provide 1 1/4 in. dia High Tensile Steel Bolts for connecting Bottom Transverse Strut at L11 ... A325-53T, washers carburized ... one edge clipped'; callout '46 - 1 1/4 in. High Tensile Steel Bolts' | `details-ptc.csx` section 6: L11 strut end tees with RIVETS (1 1/4 H.S. outstanding, 1 in. web), 2 x 9 per leg | DIFF | Data: `details-ptc.csx` L11 branch: fasteners = structural bolts 1 1/4 A325 (BoltKind.Structural, material A325-53T, washers), 46 per strut end as printed; that needs an OPTIONAL bolt-kind input on `TrussEndConnection` (generator). The rivet-counts-connections row 'L11 connection of truss to shoe 46 1.25' is these strut bolts: relabel it. |
| R09 | L11 shoes (S/N, L11 / L11') | shoe angle fasteners | p.80 Section F / Detail X: tap bolts in the horizontal legs, rivets in the vertical legs (staggered as drawn) | `shoes-ptc.csx` about line 280: tap bolt rows `outerRows` and rivet Us 3, 7, 11, 15, 21 coincide; 48 crossing clashes (2 per station) | DIFF | Data: take the rivet Us from the Detail X / elevation (stagger against the tap bolts) in `shoes-ptc.csx`. Count: model 40 rivets per shoe 'p.80 elevation'; re-count on p.80. |
| R10 | L13 / L13' | countersunk rivets | p.83 notes: 'For near half of chord: csk. N.S. in gusset and F.S. in 34 x 1 1/8 inlay pl.; for far half: field rivets', 'Csk. F.S. in near gusset', 'Csk. N.S. in near Gusset' (floor beam bracket zone) | no flush heads anywhere in the main span (`FlushEnd` only at shoe sole plates; 0 in this truss) | DIFF | Generator has `BoltInstance.FlushEnd`; needs an OPTIONAL per-group input (e.g. `TrussRivetGroup.Countersunk` = Near / Far side, `Zone`) and the bracket zone rivets (rivet-counts-connections L13 'bracket' 135 + 27, not placed). |
| R11 | U0 / U0' | top chord end beyond U0 | p.73: drawn chord field continues 3-21 in. past the U0 work point (end of chord) | 15 rivets per face dropped 'range' (S-U0-U1 s = -21 .. -3 in.): the chord starts at U0 | DIFF | Data: negative StartSetback on U0-U1 at U0 (as was done for L0, scaled from p.73, flagged), then the drawn rivets fit. |
| R12 | web member ends (U5-L6, U9-L10, U19-L20, U20-L21, U21-L22, U11-L11 at U11, L13-W14 ...) | web member end vs drawn rivets | drawn rivets reach 2-35 in. past the model member cut ends | 267 'range' drops per face; the members end short (clearances `WebMemberClearOfJointPlates` / setbacks) | DIFF (small) | Review per joint; where the sheet draws the member end (end lines visible) set the member setback to it; where the drawn rivets run into the chord splice plates, they are plate rivets (R06). |
| R13 | Sta 679+72 / 685+88 (L13 / L13') | double rivets | one rivet per hole | 8 rivets: 4 pairs 0.85 in. apart (same hole), 4 pairs 1.19 in. (shanks touching), 1 1/4 in., grips 2.81 vs 3.94 / 4.44 | DIFF | Generator: `MergeRivet` merges parallel rivets within 0.8 (d1 + d2) only for connection rivets placed after the joint rivets; these are two JOINT groups (chord / floor connection at L13). Extend the shared-hole merge to joint groups of different members at the same joint, or drop the second in data. |
| R14 | U11 / U11' upper laterals, LM16 / LM16' and LM22 lower laterals | bracing plate fills | p.71 / p.98 / p.99 draw no fills of 5-12 in. | fills 9 11/16 and 11 7/8 in. (U11, 'plate in the plane of the connected members'), 5 1/4-6 11/16 beveled (LM16), 1-2 11/16 (LM22); 340 rivets clash up to 5.9 in. deep (the rivet ends inside the fill: buried heads, N03) | DIFF | Generator / data (`bracing-ptc.csx` MemberFills / MemberPlane): a fill thicker than the plan's fills means the bracing plate plane and the member face do not meet. Use the drawn plate plane (on the member face), or flag the fill as UNRESOLVED and give the rivets the full stack grip. |
| R15 | L13-L14 S/N | rivet heads in the angle | - | 8 rivets 1 1/8 at Sta 680+01.5, heads 0.65-0.67 in. into L8x6x1 A1 / A3 | DIFF (small) | Same cause as R01 (edge row): a chord centring fix moves them. |
| R16 | all joints | diameters | p.70: chord webs 1 1/4 at U6-U13, U20-U22, L6-L15, L20-L22, else 1 1/8; web members 1 1/8 except U11-L11 both ends, L10-U11 / U11-L12 at U11 = 1 1/4; bracing 1 in., floor 7/8 unless noted | as p.70 at every joint and member (`MainChordDia`, `WebDia`); wind chord L13-W14 at L13 1 1/8 | OK / UNRESOLVED | L13-W14 at L13: p.83 zone note '1 in. Rivets - Carbon' at the wind chord / floor connection. The 11 drawn inside rivets are all dropped anyway: check the wind chord group diameter (1 in. carbon per p.83) when it is built. |
| R17 | all | grips | rivets take the steel along the shank (`JointRivetGripsFromSteel`) | 6,950 grips changed (head moved out 4,528, in 28, shorter 1,868, longer 5,082); Bolt x part rows deeper than d/2 only R09 / R14 / R15 | OK | - |
| R18 | L0, L11 sole plates | flush heads | QC I020 'heads that would bear into a shoe sole plate countersunk' | 0 flush: the main truss has no bottom cover-plate rivets (R05) and the L0 / L11 joint rivets do not reach a sole plate | OK (not applicable until R05) | - |
| R19 | L11 / L11' | chord rivet hand counts | p.80 (no drawn outline; rivet-counts-extra.csv, low confidence) | count grid as one field: 120 -> 135 and 184 -> 169 per face (total 304) | UNRESOLVED (p.80) | Unchanged: the plate edges are not separable at L11 (TRUSS-DESIGN 24). |
| R20 | 12 distributed groups | grid tighter than 3 d | - | 'Truss CHECK rivets: 12 distributed rivet groups need a grid under 3 diameters (tightest 1.86 d)' | DIFF (small) | These are count-grid joints (L11 and groups without drawn positions): the count includes non-rivet symbols (R03); re-count with the zones. |
| R21 | bracing / end connections | connection rivets not placed | rivets drawn (p.71, p.85, p.94, p.97-99) | 3,400 bracing positions pass one part only (not placed); 652 within 1 1/4 d of a plate edge; 380 not placed because the head overlaps a joint rivet head (BR-LOWER 254, BR-UPPER 112, FLOOR 14); 88 end-connection rivets through outstanding legs with no steel behind | UNRESOLVED | Plan-consistent as modeled (no counts or gages printed for most bracing groups). The 1,688 'angle leg on the stack' positions mean the edge angle sits where the generator expects the stack: review with R02. |
| R22 | p.77-90 connection groups | counted, not placed | rivet-counts-connections.csv: 41 main rows 'counted, not placed' (lugs, diaphragms, struts, brackets, pin plates, angle members) | none placed | UNRESOLVED | The parts they fasten are not modeled (lugs, pin plates, L22 diaphragms, L13 bracket); stays open with the parts. |

## Verdict counts

| Verdict | Count | Findings |
|---|---|---|
| DIFF | 15 | R01-R04, R06-R15, R20 (R12, R15 and R20 are small) |
| DIFF / UNRESOLVED | 1 | R05 |
| OK | 3 | R16, R17, R18 (not applicable until R05) |
| UNRESOLVED | 3 | R19, R21, R22 |
| CONFLICT | 0 | - |

## Fix list by kind

**Data fixes** (scripts / CSVs, no code):
- R01: centre the chord fields on the model web, or re-read `wp_px` / `chord_px` at U5, U9, U11, U12, U15, U16, L5, L22.
- R02: zones for the edge-angle strips at L9, L10, L12, U9, U19, U20.
- R03: profile-rivet zones.
- R04: remove the near-duplicate symbols.
- R06 / R07: cover splice counts for U18-U22, L18-L21, at 1 1/8 in.
- R09: L11 shoe rivet stations.
- R11: U0 chord end extension.
- R12: member setbacks where drawn.
- R14: bracing fills at U11 / LM16 / LM22.

**Generator fixes:**
- R08: bolt kind on `TrussEndConnection` (46 A325 1 1/4 at the L11 strut).
- R10: countersunk rivet groups.
- R13: shared-hole merge between joint groups.
- R04 (optional): `Free()` clearance.
- R05: stitch rivet lines (data, once the gages are read).

## Outcome (fix pass 2026-10-30, rebuilt model)

Bolt x Bolt (main span) 650 -> 184 rows. Rivets main span 131,904.

| id | outcome |
|---|---|
| R01 | NOT applied (open): centring the drawn chord fields on the model web moved 133 groups and put rivets outside their drawn gusset outlines (gusset-cover check 624 -> 604 PASS on the trial run); the offset is in the work point / chord reading of the sheet and must be fixed by re-reading `wp_px` / `chord_px` (outline and rivets together) |
| R02 | fixed with the gusset review G51 zone rows (edge angles built from the zones; counts not re-run with tools/ptc_rivet_count.py) |
| R03 | open: profile-rivet zones for the remaining joints not added |
| R04 | fixed (data): 123 near-duplicate symbols removed from rivet-positions.csv (second symbol within 1.8 in. of a kept one on a gage line; list in connections/rivet-positions-removed.csv); generator: rivet button heads now INSCRIBED in the 1.6 d circle so exactly touching heads do not clash |
| R06 / R07 | fixed (data): cover splice rivet counts U18-U21, L18-L21 into splices.csv at 1 1/8 in.; diameters 1 1/8 in rivet-counts-connections.csv; the plates themselves still have no length (G45), so no rivets are placed yet |
| R08 | fixed: generic `TrussEndConnection.OutBoltMaterial`; L11 strut end tees use 1 1/4 in. A325-53T bolts (72 bolts; p.80 prints 46 - layout not dimensioned, flagged) |
| R09 | open: L11 shoe rivet / tap bolt stagger needs a p.80 Detail X reading |
| R10 | open: countersunk L13 bracket rivets (bracket not modeled) |
| R11, R12 | open |
| R13 | fixed: generic `TrussDefinition.JointRivetsShareHoles` (`RivetHoles.MergeShared`), on for all PTC trusses |
| R14 | open: bracing fill thickness at U11 / LM16 / LM22 needs the drawn plate planes |
| R05, R19-R22 | unchanged (UNRESOLVED) |
