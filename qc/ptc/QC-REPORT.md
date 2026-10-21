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

---

# Pass 2 (checker: Claude Sonnet 5.5, independent run)

Independence: the session again showed me the full text of `C:\SBM\CLAUDE.md` (originator PTC notes) automatically. I did not open it and did not use it for any plan value, method or expectation. I did not open `samples/plans/*.csx`, `docs/plan-data/**`, git history or other bridges' qc folders. Model data come from `inputs/ptc-forcheck.ifc` (IfcOpenShell, mesh extents, cached in `work/sub.pkl`, `piles.pkl`, `main.pkl`, `fb.pkl`, `dam.pkl`, `susp.pkl` by `work/ext2.py`) and `inputs/ptc-forcheck.sqlite` (section area = volume / length). No `sbm` command, no clash run, no app. Items I002 / I003 / I006 (being fixed by the originator) were not re-checked.

## Result counts
| | pass | fail | conflict | n.a. | total |
|---|---|---|---|---|---|
| Pass 2 (C0224-C1080) | 751 | 100 | 2 | 4 | 857 |
| Combined (C0001-C1080) | 945 | 128 | 3 | 4 | 1080 |

Fails fall into 11 findings (I007-I017, issues.csv). 80 of the 100 fails are two systematic patterns (chord length bias I008, 4-span material / cover plate I009, I010); the real count of distinct defects is about 11.

## Coverage by sheet (pass 2)
- Substructure, checked: p.6 (piers 15 / 16: footing bottom / top, plan size, cap, leg sizes), p.7 (13 / 14), p.8 (11 / 12), p.9 (pier 10), p.10 (piers 1-9: footing and shaft top elevations, footing sizes, shaft base and top sizes, c-c of shafts), p.11 (W abutment footing, wing extensions, pedestal top), p.17 and p.20 (piers 17-20: pile cap, tremie seal, pier body, shafts, legs, caps), p.27 (piers 17 / 20 pile plan and cut-off El), p.33 / 34 / 35 (piers 21-26), p.36 (pier 27), p.37 (piers 28-30), p.38 (E abutment footing, pedestal, pile plan). 
- Substructure NOT checked: p.12-14 (W abutment sections, misc details, culvert) and p.39-40, p.18-19 and 21-26, 28-30 (cofferdam, fender, shaft details, boring logs, test piles), pile counts for piers 18 / 19 (498 each, plan not read), pile tip elevations, rebar. E abutment pile count is a visual count (LOW CONFIDENCE).
- Main span: p.69 / p.70 (all member lengths, areas and C / S materials of top chords, bottom chords, verticals, diagonals), p.73 (shoe at L0 / L0': masonry plate, pin, sole, rocker, pin elevation 111.098, shoe station), p.96 (shoes at piers 18 / 19: masonry plate, pin 10 in at 94.284, shoe bottom 87.701), suspender ropes (count, 4 in diameter), plate hanger area. NOT checked: gusset thicknesses (p.74-90), splice plates, bracing p.71 / 97-99, portal p.100, floor beam / suspended floor p.72, 94, 95.
- 4-span trusses p.57: all chord, diagonal, vertical and hanger areas and materials in both units (248 rows). NOT checked: 3-span member sections beyond pass 1 (p.47), joints p.59-67, p.50-53 gussets.
- Floor system: p.44 / 45 (girder spans: stringer lines, bracket tip 39'-9, outside stringer 39'-1, girder spacing), p.48 / 55 / 58 (stringer count and sections, floor beam length 58'-0 and bracket taper 6'-3 1/2 to 2'-0 1/2, floor beam count). NOT checked: girder stiffener count / spacing on p.44 (n.a. row), stringer connection details, p.56 bracing.
- Deck / curb / parapet (p.107): slab 6 3/4 in, curb to CL 38'-3, parapet 10 in, crown 1/8 in per ft. Expansion dams (p.46 / 54 / 68): plan dam length, count, 2'-6 c-c. NOT checked: dam details p.51 / 52 / 91 / 92 (teeth, plate thickness), walkways / ladders / platforms p.101-106, railing p.108.
- Sheet index: all 108 titles verified from the title blocks (p.1 to p.108, composites in `work/p/cs*.png`, `d*.png`). `extract/sheet_index.csv` is correct as it stands.

## Findings (every FAIL / CONFLICT; ids in results/issues.csv)
1. I007 (medium) p.69 / p.73: L0-L2 lower chord 63.444 ft vs 62.025 ft plan (+1.42 ft). Model chord projects past the L0 pin by about 1.4 ft; p.73 elevation (scaled) shows about 0.5-0.7 ft. Plan value low confidence.
2. I008 (low) p.69: chord lengths +0.01 to +0.11 ft vs the p.69 table (top chords 21 of 22, bottom chords 19 of 20 beyond 0.02 ft). Systematic, grows with chord slope / distance from L0; may be a definition difference (working-line vs solid length).
3. I009 (medium) p.57: in both 4-span units chords U3-U5, U9-U11 and diagonals U9-L10, L10-U11 have no cover plates in the member solid (plan 114.37 / 127.40 / 129.47 / 114.30 sq in, model 83.92 / 99.95 / 99.95 / 91.84).
4. I010 (medium) p.57: 4-span material C / S wrong on 6 members in both units (L12-L14, U3-L4, L4-U5, L14-U15, U15-L16, L16-U17), the same kind of error as I002 / I003 in the 3-span units.
5. I011 (medium) p.70: vertical U11-L11 332.2 vs 293.67 sq in (+13 %); perforated covers counted solid.
6. I012 (low) p.70: 13 members systematically +0.58 sq in (1.0-1.3 %).
7. I013 (info, PLAN CONFLICT) p.70: diagonals U13-L14 / U14-L15 printed areas 118.69 / 116.69 disagree with their own section arithmetic (116.69 / 120.69); the model follows the sections.
8. I014 (low) p.9 / p.36: piers 10 and 27 lower shaft base along the bridge 12.31 / 12.69 ft vs plan 12.50 / 12.875 ft (-0.19 ft).
9. I015 (medium, low confidence) p.27: pier 17 / 20 pile count 194 vs 196 (column 8 has 8 piles on 1'-6 offset rows in the plan, 7 on the 3'-0 rows in the model).
10. I016 (low) p.58: floor beam F3 area 63.0 vs 69.8 sq in (cover plates partial length?). Low confidence.
11. I017 (low) p.57: 14 WF 61 hangers 17.67 vs 17.94 sq in (-1.5 %).

PASS highlights: all 25 piers footing bottom / top elevations, footing plan sizes (to 1/4 in), shaft top elevations and sizes (p.6-10, 33-37), pile cap -7.0 / 5.0 and tremie seal -25.0 / -19.0 at the arch piers, abutment footing and pedestal elevations, shoe pin elevations, main span member areas (within 1 % in 205 of 215 rows), 4-span member areas (208 of 248 rows), stringer offsets and sections, floor beam 58'-0 and bracket taper, deck 6 3/4 in and curb / parapet geometry.

## Method notes
Plan values read from page crops at 3-12 x zoom (`work/p/*.png`), dimensions typed into `work/sub1.py`, `sub2.py`, `sub3.py`, `man1-4.py`, `plan_main.py` (p.69 / p.70 tables), `four_rec.py` (p.57 tables). Model: footing / cap / shaft extents from mesh bounding boxes of the named IFC elements in the station / offset frame of pass 1 (S0 = 63800, same alignment transform as `extract_geom.py`); member length and area from the sqlite registry (volume / length is the mean gross area of the solid, so it excludes anything modelled as a separate plate). Tolerances: elevations 0.01 ft, plan sizes 1/4 in, member areas 1 %, lengths 0.02 ft.
Bookkeeping: while recording I mistakenly removed rows C0224-C1041 for about 15 minutes (my confusion over images no longer displayed) and restored them from `work/invalid_unseen_rows.csv`; the rows are intact and renumbered C0256-C1073; C0224-C0255 are the first rows of this pass (piers p.10 etc. were re-verified against the plan crops after the restore: pier 1-9 elevations read again and agree).

## Still open after pass 2
Gusset thicknesses and joint plates (p.50-53, 59-67, 74-90), splices and fills, lateral / sway bracing (p.56, 71, 97-99), portal p.100, suspended floor p.72 / 94 / 95, girder stiffeners (p.44), walkways / platforms (p.101-106), railing and parapet details (p.108), expansion dam details (p.51 / 52 / 91 / 92), abutment sections (p.12, 39), culvert (p.14), pile counts at piers 18 / 19, shoes at piers 11 / 26 (p.52), 14 / 16 / 21 / 23 (p.63), 15 / 22 (p.67), 10 / 13 / 17 / 20 / 24 / 27 (p.59), rivets (226,632 in the registry; not checked), the clash run (not allowed), element-by-element reconciliation (unverified-element list not produced).

---

# Back-check round 2 (originator, 2026-10-20)
Answers per issue in `results/answers.csv` (I007-I017). Fix commit 399bd51 (generic truss inputs + PTC data; run order substructure-ptc, truss-ptc-arch, truss-ptc-approach, girders-ptc, deck-ptc, connections-ptc, shoes-ptc, walkways-ptc, rivets-ptc re-run; the model copies `PTC main truss 18-20.sbm` / `PTC approach trusses.sbm` updated by their truss scripts).
- I009 FIXED: 4-span cover plates (p.57) built, one per flange, gusset edge to gusset edge (p.60; new `TrussPlate.BetweenGussets`). Section areas = 114.37 / 127.40 / 129.47 / 114.30; the volume / length mean is about 3 % lower because the plates stop at the end gussets (by plan). 3-span p.47 has no plated members.
- I010 already FIXED in round 1 (9362334; the checker's IFC predates it): section 5b 0 mismatches in all four approach units.
- I011 FIXED: the p.70 293.67 / 217.17 are the post without the 34 x 9/16 o.s. plates / without the 34 x 1 1/8 too (perforated covers are counted at full width in the table, as in the model); both plates are now partial (p.69 elevation, flagged). Mean area 332.17 -> 277.25.
- I012 FIXED: the 13 members are all 2-18[ channel members; AISC v16 MC18 polygons were 0.29 sq in per channel larger than the 1954 channels; now built at the 1954 areas from p.70 (new `ShapeArea`): 45.98 / 54.76 / 61.86 / 58.36 / 65.46 exact.
- I014 FIXED: p.9 / p.36 B-B stepped top read again (low step 1'-9 / 3'-2 about the truss shoe line): bases 12.4975 / 12.8725 ft = 12'-6 / 12'-10 1/2.
- I015 FIXED: column 8 = 8 piles on the 1'-6 offset rows; piers 17 / 20 196 piles.
- I016 FIXED: F3 / F4 partial cover plates per p.55 (F3 bottom 20'-0; F4 top 28'-0, bottom 40'-0 + 28'-0), centred (flagged).
- I017 FIXED: every approach 14 / 16 WF at its printed area (sharp-root polygons lacked the fillets).
- I007 ANSWERED, model kept: the chord / gussets end 1'-5 1/2 (17.7 in.) past the pin line on p.73 (scale from 1'-6 3/4 and 3'-8); 0.5-0.7 ft is the end strut face. p.69 62.0254 = working points; solid = 62.0254 + 1'-5 + 0.007.
- I008 ANSWERED: solid length = working-point length + half depth x tan(deflection / 2) per end (square cuts closing the joint); working-point lengths match p.69 (generator checks PASS); L21-L22 31.0199 + 0.1084 = 31.1283.
- I013 ACCEPTED plan conflict: rows U13-L14 / U14-L15 print 118.69 / 116.69 for 2-Pls 26 x 1 / 28 x 1 (arithmetic 116.69 / 120.69) with the same net area 82.35; model follows the section text.

Check counts before / after (same session, before = previous CLI on the round-1 model):
| script | before | after |
|---|---|---|
| check-truss-ptc (main truss model) | 405 PASS / 11 DIFF / 35 INFO | 405 / 11 / 35 |
| check-truss-ptc-approach | 1050 PASS / 12 DIFF / 100 INFO | 1192 / 12 / 108 (+142 section 5c gross-area rows, all PASS; +8 INFO mean areas) |
| check-ptc-delaware-river | 376 PASS / 44 DIFF | 378 / 42 (piers 10 / 27 bases) |
| check-girders-ptc | 131 PASS / 6 DIFF / 14 INFO | 131 / 6 / 14 |

p.42 steel by group (model / plan): 4-span silicon 84.3 % -> 89.7 %, 3-span silicon 87.9 % -> 92.0 % (floor beam plates), arch silicon 97.2 % -> 96.5 % (post plies now partial). Please re-check I009 / I011 / I012 / I014-I017 on a fresh export; for areas read the mid-member section (or volume / length knowing that cover plates stop at the gussets), and for chord lengths use the working points.


---

# Pass 3 (independent checker, 2026-10-20 / 21)
Checker: Claude Sonnet 5.5. Model: `samples/plans/PTC Delaware River Bridge.sbm` copied to `inputs/ptc.sbm`; fresh `sbm export --ifc --for-check` and `sbm db --for-check` (CLI rebuilt in `qc\bin-ptc`). Geometry read with IfcOpenShell from the checking IFC (sqlite registry for areas); `sbm measure --member` (sections of three floor beams) was run on the live .sbm and used for geometry only (its descriptions were not used as plan values). The gusset-outline polygons in `docs/plan-data/ptc-delaware-river/connections/gusset-outlines.csv` were used as DATA to locate the joints and as the comparison outline, never as evidence of themselves: every outline was overlaid on the plan raster and judged by eye (evidence/gusset-*.png).

## Rows and results (results/checks.csv, 1,792 rows in total)
| Block | Rows | pass | fail | other |
|---|---|---|---|---|
| Pass 3 first part (re-checks I002-I017, gusset thicknesses, shoes; C1073-C1679) | 607 | 568 | 30 | 4 conflict, 3 info, 2 n.a. |
| 3b-1 Gusset outlines at 14 joints (C1680-C1728) | 49 | 42 | 6 | 1 n.a. (L12 height: bbox in another frame, area row used) |
| 3b-2 Bracing and suspended floor (C1729-C1754) | 26 | 17 | 9 | |
| 3b-3 Walkways, railing, dams, W abutment (C1755-C1774) | 20 | 14 | 6 | |
| 3b-4 Rivet counts, 13 joints + rivet-less gussets (C1775-C1790) | 16 | 3 | 13 | |
| 3b-5 Steel total, L0 chord end (C1791-C1792) | 2 | 1 | 1 | |
| All rows | 1,792 | 1,585 | 192 | 7 conflict, 3 info, 5 n.a. |

## Method notes (3b)
- Gussets (3b-1): joints = 3-span U1 U7 L8, 4-span L10 L20, main U9 L10 U11 L12 U12 L15 L17 L18 L20 (+ L0 in 3b-5). Scale 4.28 px/in at 150 dpi (3/4" = 1'-0", checked 4.24 on p.50 from the U1-U3 panel 26'-11 1/4). Model bbox / area from the IFC plate; the outline polygon overlaid on the sheet crop. Result: 11 of 14 follow the drawn plate edges; U9 (right edge about 20 in outboard), L15 (left edge about 17 in), L17 (right edge 11-16 in) do not (I028). Gusset thickness notes read on the same crops: all 7 compared agree.
- Bracing (3b-2): section areas of the printed members (channel weight / 3.4 + plates; AISC areas for W, BP, WT, L) vs mean section area (volume / length) of the registry members; counts from the registry. Floor lateral X bracing is one panel off the plan (p.71 floor lateral system, p.99 conns at W14 / W16 / W18 / W20) and the suspended floor beams carry reduced sections (F6 / F8: 123.6 / 134.85 vs 157.0 / 220.7 sq in). Stringers (S9-S16 sections, 21 WF 62 / 24 WF 84 / 24 WF 94) agree.
- Walkways / railing / dams / abutment (3b-3): rail and stringer sections, post spacing 6'-0 (all three walkways), the 22'-0 strut walkway, 87'-0 c.c. and 76'-6 curb to curb, dam teeth pitch / count / width and W abutment footing and pedestal agree. Fails: railing post spacing at the abutment panels, tooth length / plate thickness at Pier 20 (low confidence), walkway at floor beam U11-L11 (low confidence).
- Rivets (3b-4): the plan count is a raster detection of filled and open rivet symbols inside the drawn gusset outline at 300 dpi (`work/rdet.py`; overlays `evidence/rivets-*.png` reviewed, the detector found every symbol on the three overlays opened; crossed-circle symbols can be missed by a few). Model count = IFC rivet placements inside the plate outline on one face. 88 of 224 N-side gusset plates have no rivets at all in the model; at 9 of 13 sampled joints the count differs by more than max(3, 5 %).

## Open issues after pass 3 (results/issues.csv, now with status and evidence columns)
- Closed: I002 I003 I007 I008 (accepted as a definition) I009 I010 I011 I012 I014 I015 I016 I017 (12).
- Accepted plan conflicts, need the user: I004 I005 I013 (3).
- Open (20): I001 (steel 86.3 % of the plan, was 83.8 %), I006, I018-I025 (clash groups from `clash-summary.csv`, 469,761 clashes in 86 groups: 46 intended contact, 40 model error), and new I026-I035:
  - I026 floor laterals shifted one panel (p.71 / p.99), medium
  - I027 suspended floor beams with reduced sections (F6 / F8), medium
  - I028 gusset outlines U9 / L15 / L17 outboard of the drawn edges, medium
  - I029 rivets: 88 of 224 N gussets without rivets, counts differ at 9 of 13 sampled joints, medium
  - I030 4-span gusset stack in-to-in 0.25-0.5 in off at 12 joints, main gusset layer / thickness mismatches, low
  - I031 shoe pin length 42.63 vs 42 1/8 in at piers 11 / 15, low
  - I032 bracing section areas 1.0-1.4 % off (14 BP 73, 13[31.8, 18[42.7), low
  - I033 railing post spacing 5.71 vs 5'-11 1/2 ft at the abutment panels, low
  - I034 / I035 low-confidence dam tooth and floor beam walkway dimensions, low

## Not checked in pass 3b
Portal p.100 (U11 portal bracing section and connection), sway bracing counts and positions (dotted lines illegible on p.71), bottom lateral diagonal counts and the L10-L12 diamond (p.71), lateral / sway connection details p.97-98, suspended floor beam F5 / F7 positions (the p.72 panel grid is not legible at the scan resolution), rigid suspender sections, stringer line count (10 vs 11 rows on p.72), walkway ladders / platforms p.102-106 beyond the items above, girder-span walkways, E abutment section p.39, W abutment wall / wing sections (p.12) other than footing and pedestal, culvert p.14, dams p.46 / 51 / 52 / 54 / 68 / 92 other than Pier 20 (p.91), rivet counts at the other joints (only 13 sampled plus the all-gusset presence check), rivet grips, element-by-element reconciliation (unverified-element list not produced), second clash engine (IfcOpenShell ifcclash not run).
