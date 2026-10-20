# QC report: ODOT Bridge O, steel tub unit (spans 14-16): independent check (pilot)

- Checker: Claude Opus (independent checker agent), run 2026-10-19. Procedure: docs/QC-PLAN.md sections 1, 2, 4, 4.1a, 5, 6, 7 and 8.
- Plans: `C:\SBMData\Vol 2_Phase 2B Bridge Combined.pdf`. Bridge O is on pp. 254-300 (O-1 .. O-43, BD-1, PS-1/2). Pier 13 is on Bridge N sheets (pp. 148 and 180).
- Model outputs checked: `inputs/odot-o.ifc` (IFC4X3, project unit FOOT, IfcMapConversion E 2113517.060 / N 193241.470 / 1115.486), `inputs/odot-o.sqlite`, and read-only `sbm` commands (`db`, `clash`, `elevations`, `blocking`, snapshot).
- Scope (set by the user): superstructure of the steel unit only. That covers girders, internal and external bracing, laterals, support and pier diaphragms, bearings, deck, studs, welds and splices. Substructure, barriers, approach work and span 17 (U54 concrete beams) are marked n.a.

## 1. Results summary

| | count |
|---|---|
| Check rows (`results/checks.csv`) | 172 |
| pass | 111 |
| fail | 13 |
| conflict (plan vs plan) | 7 |
| open | 7 |
| n.a. (out of scope or not modeled by scope) | 34 |
| Issues for the originator (`results/issues.csv`) | 17 (1 high, 6 medium, 10 low) |
| Plan conflicts and scope questions for the user | 7 (P01-P07) |
| Elements in the IFC (parts, elements, fasteners) | 15,315 |
| Elements verified for both size and position (pass 2) | 6,193 |
| Unverified (`results/unverified.csv`) | 9,120 (6,712 bolts, 1,688 studs, 604 welds, 116 others) |
| Clashes (`sbm clash --bolts --welds`) | 1,407: 1,320 intended or program limit, 87 model error |

### Coverage per sheet (pass 1)

The pages are: 254 O-1, 259 O-5, 270-273 O-16..O-19, 278-290 O-24..O-36, 292-293 O-38/O-39, and BD-1.

| Sheet | pass | fail | conflict | open | n.a. |
|---|---|---|---|---|---|
| O-1 General plan and elevation (p.254) | 6 | | | | 1 |
| O-5 Staking (p.259) | 4 | | | | 1 |
| O-16 Superstructure 1/3 (p.270) | 4 | | 2 | | 2 |
| O-17 Superstructure 2/3 (p.271) | 8 | 1 | | | 2 |
| O-18 Superstructure 3/3 (p.272) | | | | | 1 |
| O-19 Framing plan (p.273) | 8 | | 2 | | |
| O-24 G1 elevation (p.278) | 26 | 3 | | | |
| O-25 G2 elevation (p.279) | 23 | 1 | 2 | | |
| O-26 DL deflections (p.280) | 1 (62 values) | | | | |
| O-27 Field splices (p.281) | 6 | | | 1 | |
| O-28 Intermediate cross frames (p.282) | 4 | 1 | | | |
| O-29/O-30 P13 end diaphragm (pp.283-284) | 3 | | | 2 | |
| O-31/O-32 P16 end diaphragm (pp.285-286) | 1 | | | 1 | |
| O-33/O-34 P14/P15 pier diaphragm (pp.287-288) | 2 | | | 1 | |
| O-35 Lateral bracing (p.289) | 1 | | 1 | | |
| O-36 Misc. details (p.290) | 3 | 2 | | | |
| O-38 Bearing details (p.292) | 2 | 4 | | | |
| O-39 Bearing schedule (p.293) | 9 | | | | |
| BD-1 Bridge drains (p.298) | | | | 1 | |
| Program / IFC output (no sheet) | | 1 | | 1 | |

All other sheets (O-1A, O-2..O-4, O-6..O-15, O-20..O-23, O-37, O-40..O-43, PS-1/2, N-16, N-48) are n.a.: substructure, span 17, approach and architectural work.

## 2. FAIL items

| check | page | item | plan | model | evidence |
|---|---|---|---|---|---|
| C123 | 282 O-28 | Bolts in the external intermediate diaphragm (XD) end connection | 2 columns x 9 on the diaphragm web, plus 8 per angle on the girder: 34 per end | 1 x 9 + 2 x 8 = 25 per end (108 bolts missing over 6 XDs) | evidence/xd1_bolts.txt, crop p282 |
| C139 | 290 O-36 | Access opening in the bottom flange | 18" x 30" (R 9" ends, centres 6" each side of CL) | 18" x 33" (+ padlock slot) | work/ hatch script |
| C144 | 292 O-38 | Anchor-bolt holes in the bottom flange | 6" dia (expansion) / 2 1/2" (fixed) | no holes; anchor bolts penetrate the BF up to 1 1/8" | results/clash_spotcheck_ifcopenshell.csv |
| C145 | 292 O-38 | Contact plates | 8x8x1/2 top contact PL with slot (expansion); 4x4 bottom contact PL (fixed) | not modeled | - |
| C147 | 292 O-38 | Sole plate material | AASHTO M270 Gr 50W | ASTM A709 Gr. 50 | IFC material |
| C143 | 292 O-38 | Anchor bolt length | 3'-0" | 23.7-27.8" | - |
| C101 / C106 | 278 / 279 | Studs, field section 1 | G1 125 rows x 6 = 750; G2 149 x 6 = 894 | 746 / 892 (right-flange studs dropped in the first 2 rows at the skewed P13 end) | bolt_groups.txt |
| C059 / C064 | 278 O-24 | G1 span 16 and FS4 to CL brg P16 | 157'-6 7/8" / 123'-3 13/16" | 157'-6.97" / 123'-3.91" (+0.09" / +0.10") | evidence/girder_positions.txt |
| C040 | 271 O-17 | Deck ends at P13 / P16 | 2" joint opening at CL pier | deck ends exactly on CL pier | - |
| C140 | 290 O-36 | Minor items | drain holes, drip plates, lighting holes, rodent screens | not modeled | - |
| C171 | program | `sbm elevations` chain | H 5'-10" / 5'-10 5/8" / 5'-11" / 5'-11 3/8" | report gives 5'-9" .. 5'-9 13/16" from a default pad and sole plate. The IFC itself matches (see C035-C038) | evidence/sbm_elevations.txt |

## 3. Plan CONFLICT items (for the user; the model stays as drawn)

| id | pages | conflict | model follows |
|---|---|---|---|
| P01 / C031 | 270 vs 273, 254 | O-16 "Typical tub girder section" is a 10'-7" deep tub (20'-3 13/16" across the webs), apparently from another bridge | O-19 50" tub |
| P02 / C032 | 270 vs 273, 271 | O-16 haunch detail shows a 1'-8 1/4" slab and 1 7/16" haunch, against 8" slab and 4 1/2" haunch | O-19 / O-17 |
| P03 / C049 | 273 (+283) | P13 skew: 69°02'38" / 69°18'54" cannot both hold together with 18'-11 7/8" along CL pier through WP 301+53.72 | WP + distance (68°13'52" / 68°41'12") |
| P04 / C088 | 279 vs 273 | G2 span 16: 160'-9 3/8" vs 160'-9 1/2" | between the two (160'-9.45") |
| P05 / C076 | 279 | G2 FS1-FS2: 79'-0 7/8" is printed, but the segments sum to 79'-0 15/16" | segments |
| P06 / C048, C137 | 273, 289, 290 | Drafting: swapped radius labels in span 16, "girder no. 9", "girder no. 7/8", and a drip-plate pier list from another bridge | - |
| P07 | 270/271/298, 254 | Scope: deck rebar and the 2 deck drains are not modeled; the barriers are in the IFC although out of scope | - |

## 4. Whole-model passes

- **Alignment and coordinates (4.2):** pass.
  - The IFC alignment (R 881.47 ft arc, start 301+33.72) reproduces the P14, P15, P16 and Abut 1 WP N/E from O-5 within 0.008 ft.
  - The profile (PVC/PVI/PVT, 200 ft VC, -2.93 % / -0.57 %) and the PGL elevations at the piers match within 0.006 ft (printed precision).
  - Spans along the PGL are 139.28 / 161.00 / 161.00 ft.
- **Girder geometry:**
  - Measured as arc length about the alignment centre on R_girder (G1 R 868'-1 11/16", inner; G2 R 885'-9 11/16").
  - 31 of 33 printed girder dimensions are within 1/16" (splices, flange breaks, spans, girder ends 9 11/16" / 9").
  - Girder spacing is 17'-8" and PGL to G2 is 4'-4". The cross slope is 6.00 %, with the girders rotated about the top WP. The bearings sit 3" outboard, as O-38 shows.
- **Elevation chain:**
  - Top of slab (deck ray cast) equals PGL - 6 % x offset at all 8 CL bearings (0.000 ft).
  - The slab is 8.000" and the haunch at CL brg is 4.50".
  - The bearing seat is TOS - H within +0.09" (tolerance 1/8").
- **Camber:** `sbm blocking --all` reproduces all 62 tenth-point TOTAL values of O-26 exactly. The splice columns of the table are not reported by the model. The IFC geometry is final (no camber), which is expected.
- **Quantities:**
  - Structural steel: plates + members are 883,065 lb against 904,820 lb (-2.4 %). Adding an estimate for bolts, studs and welds (the fasteners carry no mass in the IFC) gives about +0.3 %. This passes only if the pay item includes fasteners.
  - Bearings: 2 fixed + 6 guided, which matches.
  - Deck concrete is not comparable (the plan total covers span 17 and the haunches).
- **Clash (section 7):**
  - `sbm clash` (parts only) gives 159; with bolts and welds, 1,407. The triage is in `results/clash_triage.csv`:
    - 1,320 are intended or program limit: bolts without modeled holes, idealized weld solids, and the square-cut sloped web corner entering the flange by up to 0.09".
    - 87 are model errors: laterals into the top flanges up to 7/16" (52); diaphragm flanges overlapping girder flanges up to 5/8"; connection plates, bent plates and stiffeners up to 3/8"; anchor bolts through the BF with no holes (24).
  - Independent IfcOpenShell Monte-Carlo overlap check on 7 pairs (`results/clash_spotcheck_ifcopenshell.csv`):
    - It confirms the TF / SD-TF overlap (0.52", where sbm gives 0.524"), the anchor bolt in the BF, the bent plate in the web, and the bearing stiffener in the BF.
    - It also confirms that 2 non-clash pairs (XD web / girder web, top strut / TF) are clean.
    - No engine disagreement was found.

## 5. Sections (section 6)

These are IfcOpenShell plane cuts (`work/cut.py`), set side by side with the plan crop:

1. `evidence/S1_tub_section_side_by_side.png` (O-19 typical tub section vs radial cut at STA 303+80):
   - Webs at top are 95.83" horizontal (= 96.0" on the 6 % slope) and at the bottom 70.87" (= 71.0").
   - BF 77", TF 21", BF thickness 1 3/8".
   - Girder spacing 17.667 ft, deck 32.00 ft.
   - Result: pass.
2. `evidence/S2_pier14_diaphragm_side_by_side.png` (O-33 pier diaphragm vs cut along CL brg P14):
   - Interior webs have access openings, the exterior diaphragm has 2 bolt columns per angle, and the pads sit under the tub bottom flange.
   - Result: layout matches; plate sizes pass by attribute.
3. `evidence/S3_ext_interm_diaphragm_side_by_side.png` (O-28 intermediate bracing detail vs cut at XD3):
   - Web 3'-2", flanges and angles match.
   - The bolt-column deficit was found from the bolt positions (C123).

`SteelBridgeModeler.exe --snapshot --section` produced a plan view with the section dialog, not a section image (evidence/sec_303p80*.png), so it was not usable as evidence.

## 6. Pass 2: element by element (4.1a)

- **Verified for size and position:** 6,193 elements:
  - all girder plates, BF/web splice plates, fillers, bearings (pads and beveled plates);
  - cross-frame members, tabs, gussets (size only from attributes) and stiffeners at panel points;
  - XD / SD / XSD plates, laterals (size), hatches, the deck;
  - 5,364 studs (field sections 2-5).
- **Unverified (9,120):**

  | Elements | count | Why unverified |
  |---|---|---|
  | All bolts | 6,712 | Counts pass for splice bolts (420 per splice), but no gage/pitch/position check was possible. Cross-frame, lateral and diaphragm bolts have neither size nor position checks. |
  | Studs in field section 1 | 1,638 | The count check failed. |
  | Studs on the XSD diaphragms | 50 | Open. |
  | Welds | 604 | Size is passed by attribute only for the web-to-flange welds; no weld geometry was checked. |
  | Top-flange splice plates | 48 | Length is open. |
  | SD connection plates | 24 | Open. |
  | Anchor bolts | 24 | Length failed. |
  | P13 skew-dependent parts | 19 | Plan conflict. |

  - Laterals: section verified; the W.P. 8" offset was not measured, but the counts and panel spacing pass.
- **Sheet links** (`results/sheet_link_findings.csv`, issue I14):
  - Girder field sections link only to O-36. Under the nearest-ancestor rule, the flange and web plates and the studs therefore do not carry O-24 / O-25 (pp. 278/279), where their sizes and stud rows are defined.
  - Cross-frame parts do not carry O-19.
  - Bolts and welds carry links (pp. 282, 287-290) that no check used, because no bolt or weld check exists yet.

## 7. Issues list

See `results/issues.csv`: I01-I17 for the originator, P01-P07 for the user.

- **High:** I01 (XD bolts).
- **Medium:**
  - I02 access opening;
  - I03 anchor-bolt holes;
  - I04 contact plates;
  - I05 laterals into the top flange;
  - I06 diaphragm and connection overlaps at P13;
  - I13 `sbm elevations` program defect.

## 8. PROCESS (pilot lessons)

### What worked
- IfcOpenShell plus the IFC alignment gave a fully independent geometry route:
  - arc length on R_girder for every printed girder dimension;
  - N/E and profile checks;
  - ray-cast TOS, slab and haunch;
  - plane cuts for sections.
- The DB parent/child structure made counts fast (bolts per splice, studs per field section).
- PDF text extraction with positions gives most values directly, including the rotated camber table (62 values auto-compared).

### What was slow
- Stacked fractions are lost in the PDF text (special font), so every dimension with a fraction needed a rendered crop. This took most of the intake time.
- Reading bolt patterns and small details (O-28, O-30, O-34) needed 300-900 dpi crops.
- Plate outline measurement on meshes with holes is unreliable. The TF splice plates stay open.
- A skewed end (P13) needs custom geometry for every check.

### Missing tools that would help most (QC-PLAN sections 3 / 10)
1. **`sbm measure`** (new): plate outline, hole patterns (gage/pitch/edge), distances between named features. This would close all 7 open items and the 6,712 unverified bolts.
2. **`sbm section`** (new) with a true cut, dimensions and an overlay. The existing `--snapshot --section` did not give a section image.
3. **`sbm qc record/report`** plus `qc_checks` tables. This pilot used CSV; pass 2 had to re-derive the check-to-element mapping by regex.
4. A **PDF fraction/linework reader** (`pdfvec.py`-style), so that plan dims come out with fractions without raster crops.
5. A **clash triage store** with group rules, plus a second engine that runs as a tool (the spot check here was a small Monte-Carlo script).

### Time spent (approximate, one session)
| Step | Share |
|---|---|
| Intake: sheet survey, crops, extraction | 40 % |
| Geometry and elevation checks (alignment, girders, deck, bearings) | 25 % |
| Bolts, studs and details | 10 % |
| Clash run and spot check | 7 % |
| Sections | 8 % |
| Pass 2 and report | 10 % |

### Independence
- I did not read `samples/plans/*.csx`, `docs/plan-data/**`, `CLAUDE.md`, `docs/HANDOFF*.md`, `src/**`, the `.sbm` JSON, or the check scripts. I did not use `sbm get` or `sbm set`.
- **Leaks noticed in the model outputs.** These are originator interpretation that reaches the checker through the outputs:
  - IFC `SBM_FabricationAttributes.PlanNotes` cite the originator's note ids and pages ("OT1 p.278 note 3 ...").
  - Some Size strings carry originator judgements ("outline not dimensioned on the plans", "tension flange (FCM) over ...").
  - The IfcAlignment description says "PC/PT not printed U1; local grid U2".
  - The DB `sheets` table and links give the originator's sheet titles and element-to-sheet claims.
  - `sbm elevations` prints the input-side bearing defaults.
- I used these only to locate elements, never as the plan value. A stricter checker export could strip PlanNotes and descriptions.

### Not checked (time-box)
- Bolt gage, pitch and edge distances for every connection.
- Weld geometry.
- W.P. offsets of the laterals (8" / 1'-0").
- Connection plate outlines on O-30, O-32 and O-34.
- Stud end distances on the external support diaphragms.
- Access opening elevation (11") on O-34.
- Deck rebar (scope).
- The barrier geometry that is present in the IFC (out of scope).

## Files
- `extract/*.csv`: the checker's own plan extraction, with page and quoted text. This includes `camber_p280.csv` and `checklist_by_sheet.csv`.
- `results/checks.csv`, `results/unverified.csv`, `results/issues.csv`, `results/sheet_link_findings.csv`
- `results/clash_sbm.csv`, `results/clash_sbm_parts.csv`, `results/clash_triage.csv`, `results/clash_spotcheck_ifcopenshell.csv`
- `evidence/`: section side-by-side images, girder positions, P13 skew, elevation chain, haunch, sbm outputs, XD bolts.
- `work/`: all scripts (re-runnable: `geom_dump.py`, `el_table.py`, then the check scripts, then `make_checks.py`, `pass2.py`, `make_issues.py`).
