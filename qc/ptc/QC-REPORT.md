# QC report: PTC / NJTA Delaware River Bridge (checker run, partial)

Checker: Claude Sonnet 5.5, independent run. Plan: `C:\SBMData\Original Existing Bridge Plans from PTC.pdf` (108 raster pages). Model inputs: `inputs/ptc-forcheck.ifc`, `ptc-forcheck.sqlite`. The `sbm` CLI was NOT used on the model (only `--help`/`get` on alignment values); all geometry was read from the IFC with IfcOpenShell and from the sqlite registry.

Independence note: when the session instructions were reloaded, the full text of `C:\SBM\CLAUDE.md` (with the originator's PTC notes) was shown to me automatically. I did not open it myself, and I did not use it for any check value; all plan values below were read from the plan images. Please treat this as an accidental exposure.

## Result counts (results/checks.csv, 223 rows after resume)
- pass 194, fail 28, conflict 1, n.a. 0 (rows recorded as n.a. in method text only), open: everything listed under "Not checked".
- Fails: 15 steel-weight rows (all units), 10 material rows (3-span units), 2 row pairs for L13 elevation (4 rows count in the 28: 2 L13, 1 girder depth) - see issues.csv.

## Coverage by sheet
Checked: p.1 (profile PVC/PVI/PVT), p.42 (quantities items 1-6 and 11; station of E abutment), p.43 (girder spans: all 11 + 4 pier/abutment stations, gutter and shoe-bottom elevations, G1 section: cover plate lengths/width/thickness/spacing), p.47 (3-span trusses: pier stations, gutter elevations, panel spacing 26'-11 1/4, depth 27'-6, 26 member gross areas and materials in both units), p.57 (4-span trusses: pier stations, panel spacing, depth; members NOT checked), p.69 (main truss lower-chord / wind-chord elevations at panel points 0-22 and mirror, panel stations 31'-0).
Not checked (open): p.2-41 substructure sheets (pier / abutment / footing / pile dimensions and elevations, priority f), p.44-46, 48-56, 58-68 (steel details, joints, floor system, stringers, brackets, expansion dams), p.70-100 (main truss member sections, gusset thicknesses, shoes p.73/p.96, bracing), p.101-108 (walkways, pavement section, parapet, railing, drainage), priority e. Stringers/floor beams/brackets, girder stiffeners and floor system (d) not checked. 4-span member sections not checked. Clash run (h) not done (memory risk in the sandbox) - open.
`extract/sheet_index.csv` is the earlier crashed run's index; I verified titles only for pages I used (1, 42, 43, 47, 57, 69). The others are unverified.

## Findings
PASS highlights: all 32 support stations (p.43, 47, 57, 69) match to 0.001 ft; gutter profile and shoe-bottom elevations at all girder-span supports match (<=0.002 ft); crest VC (PVC 675+98.38 El 143.390, M 10.23) matches; approach truss panel spacing and depth exact; main truss lower-chord elevations match the table within 0.01 ft (a constant -0.005 ft bias) except panel 13; cover plate lengths 86/51/90/74/51 ft, 22 x 13/16, girder spacing 58'-0; concrete deck+parapet CY within 2% of p.42 item 11 in every unit (+1.1 to +1.6%, girder 28-31 -1.7%).
FAIL / CONFLICT:
1. p.42 steel weight: 32.92 M lb vs 39.28 M lb (83.8%); per unit carbon 67-78%, silicon 85-97% (I001).
2. p.47 materials: L10-L12 and L4-U5 model carbon (plan silicon); L2-U3, L4-U3, L12-U11 model silicon (plan carbon), both 3-span units (I002, I003). Gross areas of all 26 members agree within 1.3%.
3. p.69 panel 13 lower chord: model 140.7557 vs plan 140.782 (-0.026 ft); the plan table also lists 140.762 for the wind chord at panel 13 (I004).
4. p.43 PLAN CONFLICT at E abutment: shoe 62.033 vs gutter 76.478 (14.445 vs 14.407); p.1 vs p.43 abutment station 711+94.046 vs 711+92.797 (I005).
5. G1 total depth 9.946 vs 9.906 ft, low confidence (I006).

## Method notes
Member axes were fitted from IFC meshes (slab bounding-box centres, least squares); axis evaluation carries about 0.005 ft bias, within tolerance. Station = distance along the IFC alignment tangent + 63800 ft. Gutter = IFC centerline profile - 0.400 (p.43 note). Scripts in `work/` (geom.csv, girder_prof.csv, chk_*.py). Temporary images in `work/` remain (cs_*.png contact sheets from the earlier run); delete as needed.

## Back-check (originator, 2026-10-20)
Answers per issue in `results/answers.csv`.
- I002 / I003 FIXED (commit 9362334): the data (`truss-approach/members.csv`) agreed with p.47 / p.57; `truss-ptc-approach.csx` keyed the 1954 sections by the printed name only, so 14 WF 87 and 14 WF 142 took the letter of their first row. The 4-span units had the same error (L12-L14, L16-U17 should be S; U3-L4, L4-U5, L14-U15, U15-L16 should be C). Sections are now per shape and letter; new check-truss-ptc-approach section 5b compares every truss-line member with its table row (old model 20 / 24 / 24 / 20 mismatches, now 0). Run order truss-ptc-approach .. rivets-ptc re-run.
- I006 ANSWERED: p.44 dimensions 9'-6 1/2 b. to b. Ls on the 114 in. web (angles 1/4 in. proud); 114 1/2 + 6 x 13/16 = 9.948 ft = model.
- I004 accepted plan conflict (p.69 L13 140.7820 vs member closure / wind chord 140.7620, already reported by check-truss-ptc).
- I005 accepted plan conflict (as-built E abutment 711+92.797 per p.38 / 32 / 42 / 43; p.1 superseded; printed abutment seats 0.07 ft below the p.44 stack at both abutments, model takes the printed values).
- I001 answered: open, list of unmodeled steel and per-item numbers in answers.csv.
Check counts before / after: check-truss-ptc-approach 1046 PASS / 12 DIFF -> 1050 / 12 (+4 new material rows, all PASS); check-ptc-delaware-river 376 / 44 -> 376 / 44 (approach C / S rows move 1-2 %, totals unchanged; structural steel 84.1 % of p.42); check-girders-ptc 131 / 6 (not affected). Please re-run the p.47 material rows and the p.42 carbon / silicon rows on a fresh export.
