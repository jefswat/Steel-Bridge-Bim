# QC plan: checking models built from plans

Status: proposal for the user, 2026-10-19. Nothing here is built yet unless marked **exists** (`sbm measure` / `sbm section` exist since 2026-10-19).

## 1. Purpose and rules

The plans are the authority. A model is checked against the plans, never against the inputs that built it.

1. **Independent.** The checker works from the plan PDFs and the model's delivered outputs. It does not read the originator's extraction, scripts or notes (section 2).
2. **Every check names its source.** That means the PDF page, the sheet number, the view or table, and the printed value. A check with no source is not a check.
3. **Results only, nothing fixed.** The checker reports. The originator fixes, and the checker re-checks.
4. **A plan conflict is a result, not a failure.** When two sheets disagree, the checker records a PLAN CONFLICT with both pages. The model is not "wrong" there; the user decides.
5. **Tolerances are stated per check type** (section 4.4), and every result records value_plan, value_model, the difference and the tolerance.
6. **Coverage is measured.** Every sheet in the index ends up checked, not applicable (with a reason), or open. A model is not released (IFC upload or showcase) with open items.

## 2. Roles and independence

| Role | Who | Sees | Does not see |
|---|---|---|---|
| Originator | Build agent(s) (today: Opus code agents + Sonnet extraction agents) | Everything | — |
| **Checker** | Separate agent, fresh context, **different model from the originator of that work** (Opus checks Sonnet-built work and vice versa) | Plan PDFs (`C:\SBMData`, read-only), the model's **outputs** as CHECKING exports: IFC from `sbm export --ifc --for-check`, database from `sbm db --for-check` (no plan notes, page refs, size strings, UNRESOLVED reasons, assumption bases, sheet links; docs/API.md "Checking export"; verified with `tools/check_ifc_leaks.py --expect-clean`), sections / sheets / snapshots made by read-only `sbm` commands, standards in `dist\standards` | `samples/plans/*.csx`, `docs/plan-data/**`, `CLAUDE.md` bridge notes, originator transcripts, check-*.csx |
| Back-checker | The originator | The checker's issue list | — |
| Program verifier | Separate agent (section 9) | Source code, tests | — |
| Approver | The user | QC report, open issues, plan conflicts | — |

How independence is enforced:
- **Separate workspace.** The checker runs in its own worktree that contains only `qc/` plus the published program (`dist\SteelBridgeModeler`), and its prompt states the "does not see" list. Its tools are read-only `sbm` commands (section 3) plus PyMuPDF on the PDFs.
- **Own extraction.** The checker extracts plan values itself into `qc/<bridge>/extract/*.csv`, with page and quoted source text on every row. When the originator's extraction and the checker's disagree, that is itself a finding: one of them misread the plan.
- **Different method where possible.** Values read from the vector linework (`tools/pdfvec.py`) are checked against text values. Coordinates are recomputed from the IFC with IfcOpenShell, not from SBM's own code.
- **Recorded identity.** Each QC record stores the checker id (agent model + run id), so an originator never signs its own check.

## 3. How the checker interacts with the program

The checker uses the program read-only, through the CLI. All of the following produce files or JSON, never model changes.

| Command | Status | Use |
|---|---|---|
| `sbm export <model> --ifc out.ifc --for-check` | exists | the artifact handed to the checker: geometry identical to the normal export, originator text stripped (`--keep-designations` keeps section designations); coordinates and quantities read with IfcOpenShell. Run `python -I tools/check_ifc_leaks.py out.ifc --expect-clean` before handing it over |
| `sbm db <model> out.sqlite --for-check` | exists | element registry only (no sheet links, props text, model path); the checker builds its own sheet mapping |
| `sbm db <model> --sheets-for / --find-sheet` (normal db) | exists | which sheets an element claims to come from; given to the checker only AFTER its own mapping, to check the claim as a finding source |
| `sbm get`, `sbm info`, `sbm elevations`, `sbm blocking` | exist | model values, elevation chains |
| `sbm clash` | exists | steel / misc / concrete clashes (section 7) |
| `sbm sheets`, `sbm drawings` | exist | the model's own sheets (compare layout only, not as evidence) |
| `--snapshot --closeup / --camera / --section` | exists | images for the report and visual review |
| `sbm section <model> --station STA [--offset o] [--normal-to alignment\|girder:G\|member:M\|plumb] [--skew deg] \| --plane x,y,z,nx,ny,nz [--width/--height] [--dims] --svg/--dxf/--csv/--json` | **exists** (2026-10-19) | true section through every solid (`MeshSection`), plane normal = alignment / girder tangent incl. grade or member axis (not plumb), drawn in the plane's own u/v, loops per element with GlobalId, bars crossing as dots (mark, size, clear cover), bars in the plane as polylines; `--dims` = element extents and clear distances |
| `sbm measure <model> --part / --assembly / --member --at / --distance / --point / --station / --bar / --cover / --spacing / --laps` [--json] [--svg] [--batch]` | **exists** (2026-10-19) | plates flattened into their own plane (thickness, outline, angles, clips / copes, holes, bolt pattern along its own axes, edge distances perpendicular to the edges); assemblies collapsed into their planes (stack, fillers, joint gaps, bolts per side); min distance / overlap; point query; bar bends / hooks / developed length, clear cover per face type, spacing chains "n spa @ s", laps / stagger. Read-only, never saves (docs/API.md) |
| `sbm qc plan / record / report / issues` | **new** | check list, results into the database, report, issue round trip |

**Results go into the companion database (G1 / G9 tables):**
- Today the `qc` table holds one status per (element, sheet).
- It is extended to `qc_checks`: check_id, global_id(s), sheet_id, page, view, item, method, value_plan, value_model, diff, tolerance, result (pass / fail / conflict / n.a.), checker, time, evidence file (image / overlay / csv).
- An `issues` table holds failed checks with this status flow:
  - open → fixed by originator (commit hash) → re-checked → closed
  - or accepted by the user (with a note)
- The app shows the check status and issues per element in the database panel, next to the plan sheet window.
- Re-runs are cheap: `sbm qc run` re-evaluates all automatic checks after a model change and re-opens any that now fail (regression).

## 4. What is checked

### 4.1 Check list from the sheet index
`sbm qc plan` builds the list from the plan set, not from the model:
- For every sheet in the index (`sheet-links-*.json`), the checker lists the checkable items. These are dimensions, elevations, tables (top of slab, bearing data, camber, bar lists, quantities), section and detail views, and notes that constrain geometry or materials.
- Each item becomes a row with its page and view. Rows the model does not cover are "not modeled" (with a reason), not silently skipped.
- Coverage = checked + n.a. over all items, per sheet and per bridge.

### 4.1a Two directions: sheet by sheet, then element by element
The check runs in both directions, because each catches errors the other cannot.

1. **Sheet by sheet (primary): finds omissions and wrong values.**
   - Every checkable item on every sheet (4.1) is checked against the model. Each result records the element(s) it checked.
   - Coverage is complete when every item is pass / fail / n.a. / conflict.
2. **Element by element (reconciliation): finds things the plans do not support.**
   - Every element in the database must be backed by passed checks covering its size, position and marks or bars.
   - It is a query over `qc_checks`: elements with no covering check go on an **unverified** list. Examples: invented defaults, duplicated parts, bars where the plans show none, a mislabeled element.
   - An element's claimed source sheets (sheet links) that none of its checks used are also findings: either the link is wrong or the check is missing.
3. **Whole-model passes** that belong to no single sheet or element:
   - elevation chains, coordinates and alignment (4.2)
   - bar list and quantity totals
   - clash detection (7)

A bridge is complete when pass 1 has no open items, pass 2's unverified list is empty (or every entry is accepted by the user), and the whole-model passes are clean.

### 4.2 Overall geometry (alignment, elevations, northing / easting)
Computed independently from the IFC with IfcOpenShell geometry (`ifcopenshell.geom`, `tools/check_ifc_alignment.py` extended):

| Item | Plan source | Model value | Tolerance |
|---|---|---|---|
| Alignment PIs, PC/PT/PI stations, curve data (R, L, Δ, spirals), station equations | alignment / geometry sheets | IfcAlignment segments | 0.01 ft, 1" of arc |
| N/E of CL brg × girder points, work points, CL pier / abutment points | staking diagram, layout / coordinate tables | IFC points recomputed (bearing seats, girder ends) | 0.01 ft |
| Georeference: CRS, datum, projection, SAF / grid scale | title / general notes, survey control | IfcMapConversion / IfcProjectedCRS | exact |
| Profile: VPIs, grades, VC lengths, elevations at stations | profile sheet | IfcAlignmentVertical, gradient curve | 0.005 ft |
| Top of slab at the tabulated points (edges, gutters, girders) | top of slab tables | deck top surface at that station / offset (ray cast on the IFC deck) | 1/8" |
| Cross slopes and breaks, superelevation transitions | typical section / superelevation diagram | measured from the deck surface | 0.0005 ft/ft |
| Span lengths, skews, girder spacing, deck widths | framing plan, layout | measured | 1/16" for lengths, 1" of skew angle |
| Top of web / haunch / bearing seats, beam seat and cap elevations, footing tops / bottoms, pile cut-off and tips | bearing tables, pier / abutment sheets, pile data | IFC solids (top face at the point) | 0.01 ft (1/8" for steel seats) |
| Camber / blocking | camber tables | model camber table | 1/16" |

Coordinates are checked in plan units on the plan's grid (SAF applied as the plans state). A plan rounding difference within its printed precision counts as a pass and is noted as such.

### 4.3 Members and plates (steel and concrete)
- **Steel:** plate sizes (w × t × L) per mark, shop and field splice locations, taper rules, holes and bolt patterns (gage, pitch, edge), weld type and size, cross frame / diaphragm members, bearings (stack and sizes), studs (count, spacing, length).
- **Concrete:** element outlines (cap, column, footing, wall, deck, barrier), chamfers, keyways, blockouts.
- **Quantities:** steel weight, concrete CY and rebar lb per the plan quantity tables, by pay item. These are a sanity net, not proof of geometry.
- **Material and notes:** grades, coatings, toughness and NDT requirements against the general notes.

### 4.4 Tolerances (defaults, per check type)
- Steel detail dimensions: 1/16".
- Concrete outline: 1/4".
- Rebar position: 1/16" in cover and in spacing chains (user 2026-10-19: concrete cover and spacing 1/16" by default), ±1" for lap positions, unless the plan gives a tolerance.
- Elevations: 0.01 ft (steel seats 1/8").
- Coordinates: 0.01 ft. Angles: 1".
- Quantities: 1% steel, 2% concrete, 3% rebar weight with exact bar counts.
- A plan that prints fewer digits sets its own tolerance (half its last digit).

## 5. Rebar: modeled correctly, in the right place, with the right bends and laps

Every bar mark goes through all six checks, using the model's database / IFC bars (IfcReinforcingBar centerlines) and the checker's own extraction of the bar lists and bend diagrams.

1. **Bill reconciliation (per mark, per unit).** Size, count, scheduled length, shape type, coating and weight, against the plan bar list. Counts must be exact. A mark in the plans but not in the model, or the reverse, is a finding. Schedule-only marks (no 3D bar) are listed as "not placed" with their reason.
2. **Shape and bends.**
   - The bend diagram dimensions (out-to-out, as printed) against the model `BarShape`.
   - Pin diameter at every bend against the printed D, else the agency standard minimum (AASHTO LRFD 5.10.2.3 / the state's table).
   - Hooks: type (90°/135°/180°), tail extension (5.10.2.1), seismic hooks where noted.
   - 3D centerline: each bend is an arc of radius pin/2 + db/2 (checks the RebarBends step). No kinks above 2° remain except where a plan bend exists.
   - Developed length against the scheduled length less the bend deductions. The difference is expected and is checked against the computed deduction.
3. **Location.**
   - **Clear cover** to every face, by face type (top, bottom, earth face, formed, textured), using the exact distance to the host concrete (`RebarCover` method, but evaluated on the IFC solids).
   - **Spacing chains:** first bar from the reference (end face, CL brg, construction joint), spacing per zone, number of spaces, last bar. Measured along the bar layout line and compared to the "n SPA. @ s = L" notes.
   - **Zones:** start and end of each spacing zone (stirrup / hairpin zones, end zones of beams).
   - **Layers and order** at crossings (which mat is outside), against the sections.
   - **Placement in sections:** count and position of bars cut by each plan section (section 6).
4. **Laps and splices.**
   - Lap length per the plan lap table or the specified class (Class B per AASHTO 5.10.8) for the size, coating and concrete strength.
   - Stagger (no more than the allowed % spliced at one section).
   - Lap locations inside the allowed zones; laps not placed where the plans prohibit them.
   - Mechanical splices: type and location.
   - Lapped bars in contact or within the allowed spacing.
5. **Embedment and continuity.** Dowels and bars into adjoining members (column into cap / footing, deck into diaphragm): embedment length and hook location against the plan.
6. **Interference.** Bar-bar overlap beyond touching, bars vs steel (girder flanges, studs, anchor bolts, bearings, ducts, drains), and bars inside blockouts or openings. These are part of the clash run (section 7, rebar rules).

**Output:** a per-mark table (plan vs model with result columns), a per-unit summary, and images of failing bars (close-up snapshots, smooth tubes) next to the plan crop of the bend diagram or section.

## 6. Sections compared to the sheet drawings

It is practical for scaled sections and details, but not for symbolic or N.T.S. views.

1. **Find the cut.**
   - From the sheet index and the view titles ("SECTION A-A", "TYPICAL SECTION", "SECTION THRU PIER CAP"), the checker locates the section mark on its reference view (plan or elevation). The cut location is then a station, support line, girder and distance, or a plane through two points, read from the reference view's dimensions or linework.
   - The cut location is recorded with its page, so the cut itself is checkable.
2. **Cut the model.**
   - `sbm section` makes a true plane cut through every solid with `MeshSection`, hatching the closed loops. Bars crossing the plane become points labelled with their mark; bars lying in the plane are drawn as polylines.
   - The cut is drawn at the plan's printed scale and orientation (looking direction per the section arrows).
   - Limits: the cut uses the tessellated mesh. Curved faces are faceted to the chord tolerance, so exact cuts use `NurbsSection.Cut` where available (G5 / G6). Section 9 explains how this limit is tracked.
3. **Register to the plan view**, by priority:
   - Vector linework from the PDF (`pdfvec.py`): match 3+ reference features (CL girder lines, top of deck, CL pier, grid lines) with a similarity transform at the printed scale.
   - Raster: the same, using user-confirmed reference points, with the rendered page at 300 dpi.
   - Registration residuals are reported. A view whose residual is larger than its tolerance is "scale not reliable": only its dimensions are checked, not its overlay.
4. **Compare.**
   - **Dimensions:** every dimension text in the view is matched to its extension lines. The same distance is measured on the model cut and compared with tolerance. This is the primary check and works even on N.T.S. views when the dimension endpoints are identifiable.
   - **Outline overlay:** model outline vs plan outline (Hausdorff distance per edge). Differences above tolerance are highlighted.
   - **Bars in section:** bar dots counted and positioned per layer against the plan dots / bar callouts (mark + spacing). Cover is measured on both.
   - **Output:** an overlay PDF per view (plan in grey, model in color, differences in red, numbers in a side table), stored as evidence on the QC record.
5. **What it is not used for:** the model's own generated sheets are never evidence. The checker compares the model to the PLAN sheets.

**View engine (projected views for overlays).** Plans show more than the cut: what lies beyond it, plans and elevations. `sbm project`
(`SBM.Core.Drawing.Projection.ModelProjector`) projects the model solids onto any view with exact hidden-line removal: orthographic
(top / front / left / any direction, depth slab near / far, the near plane cutting like a section) or developed along an alignment /
girder (x = station or girder distance, y = elevation). Output = DXF / SVG / PDF with the 3D layer names (visible) and `-HIDDEN`
layers (dashed), every line tagged with its element key, so an overlay difference points straight at the model element. It
uses only the 3D solids, never the generator's 2D sheet linework, so it is independent of the generated sheets. `sbm section`
can add the "beyond" part by calling it with the view origin on the cut plane and `Near = 0`. Limit: like `MeshSection`, curved
faces are faceted to the chord tolerance (silhouettes of tessellated surfaces); plane-merged faces and `ExtrudedSolid` recipes keep
straight plate edges exact.

Practical first scope:
- Typical deck section, pier cap sections, abutment sections, girder elevation at splices, cross frame details, bearing details, and beam sections with strands.
- These cover most of the rebar position and steel detail checks.

## 7. Clash detection

**Engines.** Two engines run on the same model and their results are compared:
- `SteelClashCheck` (exists): BVH, exact triangle tests, a 1/16" depth threshold, steel / misc / drains / substructure concrete. It is extended to all IFC element types, including rebar and tendons.
- An independent second engine on the IFC: IfcOpenShell `ifcclash` or an equivalent BVH script. A clash found by one engine and not the other is itself a finding, about the engines (program verification, section 9).

**Rule matrix.** Every pair of element categories gets one rule:

| Pair (examples) | Rule |
|---|---|
| Welded / bolted / mill-to-bear parts of one assembly; studs in deck; girder ends cast into diaphragms / end blocks; embedded anchor bolts; bars through cast-in holes | **intended contact**: must touch, overlap only within the stated depth. A gap is a finding when the plans show contact |
| Steel vs steel (different assemblies), steel vs concrete, drain vs anything, bar vs steel / anchor / duct / bearing | **hard clash** if overlap > 1/16" |
| Bar vs bar | allowed to touch at crossings (center distance ≥ (d1 + d2)/2 − 1/16"); parallel bars closer than the minimum clear spacing (AASHTO 5.10.3.1) = clearance finding; bars passing through each other = hard clash |
| Bar vs concrete face | cover rule (section 5.3) |
| Bolt vs plate edges, bolt tightening clearance (wrench space), weld access, inspection access, drain slopes / clearances | **clearance**: minimum distance from the standards / plans (TIGHT < 1", CONFLICT < 0) |
| Joint opening, bearing movement range, deck / girder vs substructure | **movement clearance**: clear at the extreme movement / temperature range given on the plans |

**Run.**
- Each clash is grouped by element pair and location (clusters within 1 ft).
- Each group gets a close-up snapshot, the linked plan sheets (from the database) and the depth.
- Results are tracked between runs: new, resolved and unchanged.

**Triage.** Each group gets one disposition, kept with the clash:
- **model error:** goes to the originator.
- **plan conflict:** the plans themselves clash. Reported to the user with pages, and the model stays as drawn.
- **intended:** a rule was missing; the rule matrix is updated.
- **accepted:** user decision, with a note.

**Gate.** A model is not released while it has a new hard clash without a disposition. The I-80 run (1,232 clashes today) is triaged in groups, not one by one.

## 8. Workflow per bridge

1. **Intake.** The checker builds its own sheet index check list and extraction (4.1), with no access to the originator files. *Sonnet*: text and tables. *Opus*: views that need judgment.
2. **Automatic checks.** Geometry (4.2), members (4.3), rebar bill / shape / spacing / laps (5), and quantities. Sections 4.2–4.3 and 5 define what each one checks, and every check is re-runnable.
3. **Section comparisons** (6) for the first-scope views.
4. **Clash run** (7) and triage.
5. **Visual review.** Snapshots of every unit, close-ups of every detail type, compared with the plan views by the checker.
6. **Report.** The QC report per bridge holds:
   - coverage per sheet, results by category and open issues
   - plan conflicts and evidence files
   - an Artifact page for review, with links to the overlays
7. **Back-check.** The originator fixes or answers each issue (commit hash), and the checker re-runs the affected checks.
8. **User sign-off** on accepted items and plan conflicts. Release.

Model changes after release re-run step 2 automatically. An item that fails again is re-opened.

## 9. Checking the program itself (program verification)

The model checks also exercise the program. Every failure is classified:
- **input error:** the originator read the plan wrong.
- **program defect:** the generator built something other than its input says. Example: the 2026-10-19 Inverse bug, where a correct input produced an 8 m spike.
- **program limit:** a feature not modeled.

How program defects are handled:
- A program defect gets a unit test that reproduces it before it is fixed. The test goes into the regular suite (497 tests today), so the fix is permanent.
- A Program-verifier agent (not the checker, not the originator) owns:
  - reviewing generator changes;
  - differential tests: two independent computations of the same quantity (SBM vs IfcOpenShell geometry; mesh vs NURBS volumes; elevation chain vs generator) must agree;
  - IFC schema / EXPRESS / IDS validation of every exported model (exists: `validate_ifc.py`, `run_ids.py`);
  - byte-identical exports of the other bridges when one bridge's code changes (exists as practice).
- **Mesh limits** (faceting, chord tolerance) are stated per check, so a section difference caused by tessellation is classified as a program limit, not a model error.

## 10. Build order (proposal)

| Step | What | Agent |
|---|---|---|
| 1 | `qc_checks` / `issues` tables, `sbm qc record / report / issues`, panel display | Opus |
| 2 | Checker workspace + prompt (independence rules), checker extraction format | Opus (design), Sonnet (extraction) |
| 3 | Geometry checks from IFC (alignment, N/E, elevations, top of slab) | Sonnet |
| 4 | Rebar checks 1–4 (bill, shape / bends, cover / spacing / zones, laps) | Opus (measure engine), Sonnet (per-bridge runs) |
| 5 | `sbm section` + dimension matching + overlay | Opus |
| 6 | Clash rule matrix, rebar / tendon clashes, second engine, triage storage | Opus |
| 7 | First full QC of one bridge (proposed: Nebraska St, smallest steel set) | Checker on the other model family |
| 8 | Roll out to the other bridges; I-80 clash triage | Sonnet runs, Opus review |

## 11. Questions for the user
1. ~~Who signs off, report form~~ - user 2026-10-19: no formal report currently; the markdown QC-REPORT per bridge is enough for now.
2. Are the default tolerances right (4.4), or does an agency spec govern?
3. Is the first bridge for the pilot Nebraska St?
4. ~~Generated sheets~~ - user 2026-10-19: yes, checked against the plans as a separate deliverable. Checker exports keep section designations (`--keep-designations`).
