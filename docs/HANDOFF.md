# Handoff: remaining items (as of 2026-10-10)

Use this to start the next session. Architecture notes are in `CLAUDE.md`. The user's working rules are in memory:

- **No silent adjustments.** Detect and warn; never auto-move a modeled element.
- **For as-built plan sets, model the plans.** A conflict means my inputs are wrong.

## Current state

- **Program:** `dist\SteelBridgeModeler\` (app plus `sbm.exe`), republished 2026-10-10 evening from main (205 tests).
- **Test model:** `samples\plans\I-80 WB and Ramp G.sbm`, saved 13:48. Iowa DOT Designs 116 (WB I-80) and 316 (Ramp G), georeferenced to UTM 15N.
  - Rebuild order:
    1. `build-i80-wb.csx`, then `support-lines-i80.csx` (11WB bearing lines parallel to the CL pier, 1'-8 normal each side; re-runnable)
    2. `build-ramp-g.csx`
    3. `geolocate-cbis.csx`
    4. `cross-slope-i80.csx`, then `deck-edges-i80.csx`
    5. `girders-i80.csx`, then `pier-6a.csx`, then `field-splices-i80.csx` and `field-splices-ramp-g.csx`
    6. `piers-i80.csx`
    7. `piers-ramp-g.csx`
    8. `bearings.csx` (plan bearing data tables; check with `check-top-of-bearing.csx`)
    9. `column-rebar.csx` (then `pier-aesthetics.csx`, `girder-geometry-i80.csx` after girders-i80/pier-6a, then `joints-i80.csx`, `joints-ramp-g.csx`; checks: `check-spans-i80.csx`, `check-top-of-bearing.csx`, `check-top-of-slab.csx`) (plan column / flare cages of the as-designed piers)
    10. `cross-frames-i80.csx`, then `cross-frames-ramp-g.csx` (plan cross frame types and framing plan lines)
  - Backups are in `C:\SBMData\CouncilBluffs_I80\`. The latest is "…before Ramp G offset fix.sbm" (2026-10-10).
  - Ramp G only (re-runnable on the georeferenced model; Ramp G is placed by best fit to the I-80 WB alignment):
    `tools\rebuild-ramp-g.ps1 [-Model path] [-FromOriginal]` = build-ramp-g → field-splices-ramp-g → piers-ramp-g →
    bearings → column-rebar → cross-frames-ramp-g → check-top-of-bearing.
  - Plan GIRDER DATA span check: `samples\plans\check-spans-ramp-g.csx` (Units 1-4; all 19 spans within 0.04" as of 2026-10-10).
  - **Tolerance rule (user):** plan checks must close to plan precision (1/16", 0.01 ft); 0.5" is NOT a match.
  - The text cache is also at `temp\plan-text\` (reads outside the project folder are blocked by the user's settings).
- **Latest IFC:** `C:\SBMData\CouncilBluffs_I80\I-80 WB and Ramp G.ifczip` (50.1 MB).
  - The full-model schema check last returned 0 issues on the pre-pier export; not re-run on this one.
- **Plan PDFs:** in `dist\SteelBridgeModeler\`. PDF page ≠ a fixed sheet offset: Ramp G = sheet + 2; I-80 drifts from
  +2 to +7 (inserted sheets like 113A). **Use the sheet indexes** `C:\SBMData\plan-text\design-116\INDEX.md` and
  `design-316\INDEX.md` (PDF page → design sheet → drawing title) and the per-page text cache `pNNN.txt` there
  (built by `python -I tools/plan_index.py <pdf> <outdir>`). Read cached text first; rasterize only small clips.
  - Pier detail sheets: I-80 pages 19–82, Ramp G pages 19–77.
  - Rasterize with PyMuPDF (`import pymupdf`). poppler/pdftotext aren't installed.
  - Extracted plan data from subagents goes to `docs\plan-data\*.md`.
  - **Caveat:** stacked fractions are custom font glyphs; the text layer shows them as `ƒ`, `Ž`, `‡`, `�` etc. Use text to
    find a note/table, then confirm every fraction on a small grayscale clip (e.g. 300 dpi of the note line). Example:
    PDF 85 Superstructure Notes = 15/16" holes, 7/8" bolts. Helpers: `tools\words_near.py` (locate words), `tools\notes_probe.py` (note blocks).
  - Temp files (scripts, rasters, snapshots, test models) go in `temp\` (user preference).

## 0a. State at the end of 2026-10-10 (evening) — START HERE

- **2026-10-11 (Windows Sandbox session): both branches below are MERGED into main** (232 tests pass). Model updated with
  `temp\apply-pedestals.csx` (only the new pedestal blocks of the pier scripts, so later pier scripts are not undone) +
  `abutments-i80.csx` / `abutments-ramp-g.csx`; substructure elevation check: 49 flags (was 70), only the known Pier 5G
  plan misprint fails closure. Backup: `temp\backups\...before abutfix merge.sbm` (C:\SBMData is read-only in the sandbox).
  NOT pushed from the sandbox (no GitHub credentials there): push `main` from the host. Still open from abutfix:
  `docs\plan-data\pier-pedestals.md` (referenced by the scripts, missing), notched footing outline in AbutmentViews,
  South backwall 19.4 vs 21.5 CY.
  -> branch `worktree-agent-aefd082a948c047f5`: notched outline drawn (`AbutmentGeometry.FootingOutline`); South backwall now 21.6 CY
  (backwall maskwall to maskwall behind the wings per rear elevation p80, corbel only between the wings: `CorbelLeft/RightOffset`;
  same for I-80). Pour split 11.1/10.5 vs plan 11.5/10.0 = minor CONFLICT (plan split computed without the 3" c.j. offset).
  Main model must re-run `abutments-i80.csx` + `abutments-ramp-g.csx` after merge. See docs/plan-data/abutments-model.md.
- (was) **PAUSED FOR RESTART (2026-10-11):** two branches committed but NOT merged — verify (full build + tests), merge
  (keep main's .sbm, re-run scripts with temp\cli-main), push:
  - `feature/abutfix` (c8e2515, last commit WIP): counterfort 1'-10, maskwall flush, extensions, West footing notches,
    wing uplift anchors 8j3, placed maskwall/counterfort bars, pile-head check fix, plan PEDESTAL sizes
    (PedestalSizeAlong/Across/Shared; containment 633 -> 565). TODO: docs/CLAUDE.md, pier-pedestals.md, notched outline
    in AbutmentViews, full tests, regenerate model. South backwall 19.4 vs 21.5 CY unresolved.
  - `feature/misc` (488994a): general UNRESOLVED flag (Assumption record; viewport layer/filter, IFC Pset SBM_Status,
    DB view v_unresolved) on plan-undefined drain parts (Pier 19G drop modeled + flagged), drains 22-25 at the plan's
    1'-8 from CL girder A (conflict with gutter position reported). Agent did not send its final report - read the
    commit and check before merging.
- Release v0.1 (self-contained win-x64, 92 MB, private): https://github.com/jefswat/Steel-Bridge-Bim/releases/tag/v0.1
  (`gh` CLI installed and logged in as jefswat).
- **Git + GitHub:** local repo, `main` pushed to https://github.com/jefswat/Steel-Bridge-Bim (private) after every verified
  merge (user-approved routine). Feature work in `.worktrees\<name>` (branch `feature/<name>`), merged one at a time:
  keep main's `.sbm`, re-run the branch's scripts with a CLI built to `temp\cli-main` (`dotnet build src/SBM.Cli -o
  temp\cli-main` — read-only agents lock `src\SBM.Cli\bin\Debug\sbm.exe`), build, test, commit, push.
- **Agent limits** (a crash from memory / open files happened with 6 builders): ≤3 builders at once, `-nodeReuse:false`,
  no concurrent GPU snapshots, one model copy per agent. Every brief carries the user's rule: **no fallback guesses —
  raise unreadable / non-reconciling plan data as warnings**.
- **Merged today (all applied to the model):** expansion joints (placeholder IfcDiscreteAccessory EXPANSION_JOINT_DEVICE
  + Pset SBM_Placeholder), battered piles, I-80 gore from Ramp A2, cap aesthetics + 6" section step, plan girder geometry
  (north girders), deck end blocks + clash check, companion SQLite DB (`sbm db`, plan-page links per group, QC, GUI
  panel, 80 MB), inspection access + cross girders as designed, viewport (snap pruning, near-view smooth rebar), pier
  dowels/couplers + pile web orientation + plan footing/cap reinforcing (within ~1-2.5% of bar lists) + uplift rings read
  from the sheets + clear-cover containment, Pier 11WB support lines (1'-8 parallel) + I-80 span check (109 spans, worst
  0.21") + joint openings = plan, as-designed abutments + wingwalls, camber input + calculated blocking (595 values, none
  > 0.02 ft except U2 girder A), level strip under the barrier on the low side (project option `DeckDefinition.EdgeStrip`)
  + sheet-190 cross-slope rows.
- **Checks (all in samples\plans):** check-top-of-bearing (worst 0.31" = U2 girder A at 6A, see below), check-spans-i80 /
  -ramp-g, check-top-of-slab-all (4842 points; I-80 139 / Ramp G 8 beyond 0.01 ft; 3496 unresolved edge/gutter points),
  check-blocking, substructure-elevation check (running).
- **OPEN:** (1) Pier 6A: girder A inside the level strip (plan follows the plane) — agent checking whether the gore edge
  has a barrier (`docs\plan-data\gore-6a-edge.md`); (2) ~0.008 ft low I-80 U2 7WB-9WB unexplained by sheet 190;
  (3) cap bars outside cover: 2,103 -> 565 (bend diagrams + pedestal sizes) -> 178 (cap-cover branch 2026-10-11: pedestal U bars in the pedestal top,
  5n2 hoops at the noted girders, 6b2 / 5a13 / 5a15 / 5a11 / 5a12 / hairpin-zone fixes, per-face relief lines); remaining 178 = 11WB / 9G pedestals not set,
  6A (stepped top vs relief, undecoded end bars), Ramp G 5a12 relief bars, 19G 5c4 - `docs/plan-data/cap-cover-containment.md`;
  (4) plan's own camber vs deflection inconsistent at ~177 I-80 U2 points (plan issue); (5) 3 1/2" clear at the textured
  cap face — ANSWERED: the plans print 3 1/2" CL. only to the 5c hairpin legs, at both faces (checked); a production cover for other bars at the textured
  face is not stated (mockup note only: 2" clear to the bottom of the texture) - UNRESOLVED warning, 2" kept; (6) abutment wing/counterfort/step/corbel/maskwall rebar schedule-only, abutment
  uplift piles unidentified.
- **Running branches:** feature/bends (bend diagrams, every printed dimension), feature/deckrebar (deck reinforcing to
  the plans — biggest model error), feature/sheets (overprinting, model-driven notes, missing sheets).
- **Backlog:** Ramp G drains (25 inlets modelable), sign supports / crash cushion end posts / lighting (placeholders),
  DB refresh + "missing detail" report, rule-placed rebar positions, full-model IFC validation (IfcOpenShell not
  installed — needs user OK), cross-frame approximations (partial lines at skewed piers, bay E-F 5WB/6A, 6A joint),
  pier close-up rebar display bug, prestressed plan sets.
- **Unknown-origin local files:** `sandbox/` and docker lines in `CLAUDE.md` (stashed around merges, never committed).

## 0. Session 2026-10-10 (afternoon) results

- **Ramp G girder line was 1'-6 off (input error, fixed in `build-ramp-g.csx`):** typical section (PDF 85) = girder A
  4'-6 left of CL Ramp G / P.G.L., 5'-2 to B, 3'-1 overhangs → A at -4'-6 (was -3'-0), deck edges -7'-7 / +27'-7.
  Confirmed by GIRDER DATA radii (A 954'-6 vs CL 950'-0) and span lengths. This was the cause of the Pier 16G "2.26 ft"
  span error (1.5 ft × tan 56.5°) AND of the deferred Ramp G -1" top-of-bearing mismatch (section 7): Ramp G top of
  bearing is now within ±0.1" everywhere (largest difference in the model 0.32", I-80 Pier 8WB).
- **Shared-pier bearing lines are the CL pier line shifted PARALLEL** by the plan's normal offset (`ParallelLine` in
  build-ramp-g), not radial lines at a station: fixed a 0–0.47" A→D span error. Pier 19G CL BRG–CL pier = 1'-8 (PDF 101),
  not the assumed 1'-6. The 16G/17G skews (56°32'38", 49°) were already right.
- **3.3 Ramp G field splices done** (`field-splices-ramp-g.csx`): schedule (A vs B-D definitions with the same number),
  locations, flange plates of all 4 units from the girder elevations, webs 9/16 (U1-2) / 5/8 (U3-4). 7/8" bolts,
  15/16" holes (PDF 85). Open: U1 B-D pier-zone extents assumed = girder A; all spans checked.
- **`--closeup SUBn@Bridge`** now cuts along the unit's own support line (works for Ramp G) and frames as-designed caps.
- **Plan data extracted by Sonnet subagents** (`docs\plan-data\`): `ramp-g-girder-plates.md`, `ramp-g-splice-schedule.md`,
  `ramp-g-piers-16g-18g.md` (16G/17G = two-column bents with cantilever caps, 18G hammerhead with 22 battered piles),
  `abutments.md` (needs a checking pass; "??" items), `expansion-joints.md` (modular joints = manufacturer items:
  blockout C/D + 1'-2 depth and deck-end dims given → model as blockout + generic joint body; Ramp G south abutment
  strip seal similar), `pier-cap-aesthetics.md` (form-liner band bounded by tangent-arc S-curve, 3'-6 deep at the reverse points; per-pier L1-L4/R1-R5 tables I-80 PDF 84 and Ramp G 16G/17G PDF 79; 6A and 19G explicit; open: which face is textured per pier, Ramp G "VARIES" lengths).
- **3.4 Piers 16G, 17G, 18G built** (`piers-ramp-g.csx`, column cages in `column-rebar.csx`): 16G/17G two-column bents (6'-0 cap, beak ends, 8'-0 x 4'-6 octagonal columns, 2 footings 22'x16'x5'-6 with 20 piles each), 18G hammerhead (32'x42'-6x7' footing, 34 piles). All 12 seats within 0.01 ft of the plan pedestal tops and on the plan girder offsets; vertical bar counts = bar lists (28/column, 46). Not modeled: battered piles (18G: 22 at 1:4; also 1G-15G, 19G), the 5'-0 lower cap section of 16G/17G, footing dowels/couplers.
- **Cut-off girder cross girders / inspection access** (`docs\plan-data\cutoff-diaphragms-and-inspection.md`): I-80 only (sheet 121 / PDF 125). Model vs plans: Unit 2 web 5/8 vs plan 9/16; cross girder top ~11" (U2) / ~8" (U3) BELOW the top flange in the plans (model flush, UNCERTAIN reading); connection angles L5x5x1/2 64"/52" (model 76/56, 94/68); missing bolts/holes, stiffeners, 3'-0 x 2'-0 R6" web opening, edge plates. Inspection: 1/2" wire rope 4'-6 above bottom of web on WT brackets, walkways Type A (C7, I-80 U1-2) / Type B (L8x4x1/2, I-80 U3, Ramp G) in every bay; no ladders/platforms; modelable as simplified solids.
- `dist` republished 2026-10-10 (closeup fix; 131/131 tests).

## 1. ✅ Girder bearings on their plan positions (done in the follow-up session)

`girders-i80.csx` now iterates until every targeted bearing is within 0.03" of its cap-plan position. The model was
rebuilt from the "before I-80 piers" backup with scripts 5–7 and the IFC re-exported. The previous model is backed up
as "…before bearing iteration.sbm". The run confirmed item 2.3: girder C has no bearing at 13WB, and B none at 12WB.

## 2. Model errors the as-designed piers exposed (my inputs vs. the plans)

| # | Issue | Evidence | Fix needed |
|---|---|---|---|
| 2.1 | ✅ **Pier 6A built.** New feature: auxiliary support lines plus girder beginnings. Unit 2 girders A–E start on Pier 6A (`pier-6a.csx`); bearings on plan positions (0.01"); as-designed hammerhead with a stepped cap (Unit 2 on the lower half), 10'×6'-6" column, 24 piles. Gore slope 3.4% from 6A to 7591+50 added. | 6A pedestals 6.0–6.3"; 5WB carries Unit 2 F–K only; no seats out of range anywhere. | Bent view labels the beak tips as "top of cap" on as-designed caps (cosmetic). No editor UI for auxiliary supports yet (script/API only). |
| 2.2 | ✅ **Cross slopes fixed** (`cross-slope-i80.csx`, sheet 190). Gore slope break: 3% beyond 12' LT from 7591+50 (corrected in 2.4). South side transitions -2% → +2% over 410 ft (7591+48 to 7595+58) before going to +3% and +5.9%. The CL profile is re-derived from the PGL. New `CrossSlopeRow.LeftBreak/LeftOuterSlope` (and Right*). | 6WB/7WB pedestals now 6.8–15.6" and 7.3–9.8"; no seats out of range anywhere. | Unit 1 south 2.73% now modeled (2.4). The deck mesh has no vertex at the slope break (surface interpolates across it). |
| 2.3 | ✅ **Girder terminations fixed.** Unit 3 B ends 60'-3 11/16" past 12WB and C 53'-2 11/16" past 13WB (design sheet 115); I had read B's span length as its end. Unit 2 E/D checked on sheet 114: correct. | B now bears on 12WB, C on 13WB. | — |
| 2.4 | ✅ **Seat bias = bearing height.** The model stack was 6.6"; the plans assume 9" (bottom of flange to bottom of the ⅛" pad; 9⅝" at 5WB ahead F–K, 10 3/16" at 5G ahead, 9⅜" at 14G back). Bearings are vendor-designed, so they are now **generic** (`BearingType.Generic`): sole plate + one body + masonry plate sized from the anchors + pad. The anchors are per the tables (X/Y, 1½"/2½", 1'-6/2'-6) as per-support `SupportDefinition.Bearings`; MD/GE/FX per girder via `GirderFixities` (`bearings.csx`). | 13WB G seat 1013.556 vs plan pedestal top 1013.56. `check-top-of-bearing.csx`: Unit 3 and Unit 2 8WB–11WB are within ±0.1" of the table's top of bearing. | ✅ **I-80 cross slopes corrected from sheet 190** (`cross-slope-i80.csx`): Unit 1 south 2.73% beyond 12' RT; north lane 2 (12'–24' LT) 2%→3% over 102.50'; from 7591+50 3% beyond 12' LT (was 24'); crash-cushion curb 24'–27.68' and 3.4% gore near 6A (new `LeftMoreBreaks`). I-80 top of slab ±0.2" vs the plan table at 5WB/6A/6WB/7WB; top of bearing ±0.4" except below. Remaining mismatches moved to section 7 (deferred). |
| 2.5 | ✅ **Unit 3 north edge — modeled per plan, kink is in the plans.** R = 2350' through the Detail B corner meets R = 1315'-4" at the P.C.C. (7607+18.69, 88.74' LT) with a 1.1° break. The plan's north-edge lengths (sheets 185–186, total 924.55 ft) confirm the points: corner→2nd P.C.C. 567.77 ft in the model vs 567.73 ft on the plan. Tangency would need the P.C.C. at 90.93' LT or R = 1928.5' — both contradict the plan, so no change. | `deck-edges-i80.csx`; analysis in this session. | — |
| 2.6 | ✅ **Cross frames near field splices**: with the plan frames only 5 warnings remain at I-80 FS6 (Unit 1 line 705.05 ft along A, ~3 ft from the splice on the plan too) and Ramp G Unit 4 FS1/FS3 lines — frames kept as on the plans; check the splice locations there. | `cross-frames-*.csx` output. | — |

## 3. Not yet built from the plans (still generator defaults)

1. **Abutments and wingwalls.**
   - East abutment (sheets 89–92): 6'-8½"×4'-0" stepped pile footing, 36 HP14x73 piles 115 ft long, corbelled backwall, stepped seats, 27' wingwalls.
   - West abutment (sheets 85–88) and Ramp G south abutment (sheets 80–83).
   - Needs an as-designed abutment feature and wingwalls.
2. ✅ **Cross frames from the plans** (new styles + frame lines; `cross-frames-i80.csx`, `cross-frames-ramp-g.csx`):
   I-80 Types 1–10 + abutment frames (1" bolts, 2 columns), Ramp G Types 1–5 + abutment (7/8" bolts, WT5x15 chord).
   Frame counts: I-80 U1 272, U2 467, U3 331; Ramp G 129 / 120 / 165 / 144. Lines checked against the girder data
   (bearings fall on the strings to < 0.01 ft on I-80 U1). **Open (approximations):**
   - I-80 Unit 2 skewed Piers 8WB–10WB: frames on K's string and K's bearing lines only; the plans' extra partial frames
     placed from other girders' bearings ("measured along girder" dims) are not transcribed; Type 4 bays partly read.
   - I-80 Unit 2 bay E–F frame at Pier 5WB/6A (special orientation, sheet 117 detail) not modeled.
   - ✅ (2026-10-11) I-80 Unit 3 partial lines at Piers 12WB/13WB placed from the plan dims, Ramp G Pier 16G zone transcribed
     (per-bearing lines, B line C-D = Type 2), PDF 110 visual checks settled; 3 plan conflicts raised as line UNRESOLVED notes;
     5 plan-consistent frames inside splice plates remain (Unit 3 FS22/FS24). See `docs/plan-data/cross-frame-gaps.md` section 8.
   - Ramp G Pier 17G zone: verified against the plan (no change). **Ramp G Span 16G along girder A is 193.48 ft in the model vs
     191'-2 9/16 in the plan girder data (2.26 ft)** — Pier 16G location/skew in my inputs likely off (straddle bent, 3.4).
   - Assumed: tie plate length 10"; top gusset bolt rows; abutment frame bottom bolt rows; Ramp G abutment W24x68 4"
     below the top of web; inspection walkways not modeled; studs on W top struts not modeled.
   - ✅ Rolled sections now from the AISC Shapes Database v16.0 (downloaded with approval to C:\SBMData\AISC, embedded as
     `AiscShapes.csv`): root fillets, C flange slope, workable gages stored; weights = AISC lb/ft. Open: toe radii (not
     published by AISC) and MC flange slope (not in the database) — MC profile area runs ~2% over AISC A (mass uses AISC
     weight, so tonnage is unaffected).
3. Field splices + flange plates from the plans:
   - ✅ **I-80 done** (`field-splices-i80.csx`, run after build/pier-6a, before cross-frames): per-girder splice locations
     from the girder data tables (new `FieldSpliceDefinition.Girders` Support+Offset, `Number`), scheduled bolt patterns
     (new `FieldSpliceDetail.Top/Bottom/Web` patterns, 7/8" bolts, 2" first row, 1 1/2" edges, staggered 18" flanges,
     1/4" gap), and **flange plates per the girder elevations** (new `GirderSection.Layouts` / `PlateRun` ending at
     splices or at offsets from bearings; framing resolves `GirderResult.PlateRuns`, bearings use the actual plate).
     Result: all I-80 top of bearing within +-0.3" of the plan table (the deferred Unit 1 / 12WB mismatches are gone),
     0 elevation-chain mismatches on both bridges. Open: corner clips of staggered 18" plates; 7 frames still inside
     splice plates next to skewed piers (partial frame lines not transcribed, see 3.2).
   - ✅ **Ramp G done 2026-10-10** (`field-splices-ramp-g.csx`, see section 0). Original notes: Schedule = Design 316 sheet 104
     (PDF 106; "A" = girder A, "B" = girders B-D; 7/8" bolts; line offsets 18" [2.5,7.5 staggered], 20" [3,8],
     22" [3,9], 24" [3,6.5,10], 26" [3,7,11], 34" [3,6,9,12,15]). Locations (girder data PDF 91/94/97/101):
     U1 FS1/2 = 1G -/+50, FS3/4 = 2G -/+50 (all girders); FS5..8 = 3G-,3G+,4G-,4G+ with A 50, B 49'-5 15/16,
     C 48'-11 7/8, D 48'-5 3/4. U2 FS9..14 = 6G-,6G+,7G-,7G+,8G-,8G+ same set. U3 FS15..18 = 10G-,10G+,11G-,11G+ with
     A 65, B 64'-4 1/8, C 63'-8 3/16, D 63'-0 5/16; FS19..22 = 12G-,12G+,13G-,13G+ 50-set. U4: FS23 15G- 50-set;
     FS24 15G+ A 50'-0, B 50'-0 1/8, C 49'-10 3/4, D 49'-10 3/16; FS25 16G- 68'-6 all; FS26 16G+ A 62'-0 3/16,
     B 61'-10 7/16, C 61'-6 5/16, D 61'-0; FS27 17G- A 69'-9 5/8, B 69'-2 1/8, C 68'-6 7/8, D 68'-0; FS28 17G+ A 63'-4 5/8,
     B 62'-10 13/16, C 62'-5 3/16, D 62'-0; FS29/30 18G -/+ A 49'-0 15/16, B 49'-4 5/8, C 49'-8 5/8, D 50'-0.
     Plates (girder elevations PDF 92-93 read; 95, 98, 99, 102 still to read): U1 girder A top 7/8x18 | 1x20 /
     1 7/8x20 (1G +-19) | 7/8x18 | 1 1/8x22 / 2x22 (2G 19/17) | 7/8x18 | 1 1/8x24 / 2x24 (3G, 4G +-17) | 7/8x18 end;
     A bottom 7/8x18 | 1 1/8x20 / 1 7/8x20 | 7/8x18 | 1 1/8x22 / 2x22 | 1 1/8x20 | 1 1/4x24 / 2x24 | 1 1/8x20 |
     1 1/4x24 / 2x24 | 1 1/8x22 end. B-D: top as A but 1 1/8x20/2x20 at 2G and 1x20/2x20 at 3G, 4G; bottom
     7/8x18 | 1 1/8x20 / 1 7/8x20 | 7/8x18 | 1 1/8x20 / 2x20 | 7/8x20 | 1 1/4x20 / 2x20 | 7/8x18 | 1 1/8x20 / 2x20 | 7/8x18.
4. **Ramp G Piers 16G and 17G** (data extracted: `docs\plan-data\ramp-g-piers-16g-18g.md`; two-column bents, pages 64–71) and **18G** (pages 72–74; data partly read: 31'-6"×42'-6"×7'-0" footing, 34 HP14x73).
5. **Deck reinforcing** per the plan layout: DONE on `feature/deckrebar` (`docs/plan-data/deck-rebar.md`): plan-driven transverse (7a), longitudinal (6b), 5j1 and the plan bar lists for all units of both bridges; slab bars within 0.5% of the lists (I-80) / 0.3% (Ramp G). 2026-10-11: kinked slab ends at Pier 5WB/6A and 19G modeled (`SupportDefinition.DeckEndPoints`, `deck-end-kinks.csx`, run before the deck-rebar scripts), Ramp G end dam 5d2-5d5 at 5G/9G/14G placed in 3D
   (rest of the end dams schedule only, UNRESOLVED: deck-rebar.md sections 5-7; Ramp G U3 end dam bars meet its 20" top flanges). Open: I-80 U3 north bays 6b2 +10%, overhang positions of Ramp G (count proven, positions inferred). Bar M1 near FS4-G1-P9: not reproducible (generic layout passes the new containment check; see section 6 of deck-rebar.md).
6. **Drain routing** per sheets 205–220: downspouts, collectors, pipe hangers. The current piped drains run generically to the nearest pier.
7. **Modular expansion joints** (sheets 203–204). Plans give blockouts/deck ends only; hardware is manufacturer-designed (`docs\plan-data\expansion-joints.md`). **User decision pending** on whether to model them (blockout + generic body).
8. **Battered piles** (1:4 corner and edge piles): modeled plumb. Needs a batter direction per pile in `FootingDefinition`.

## 4. Generator / tooling gaps

- ✅ **Column rebar follows the octagonal / flared columns** (`column-rebar.csx`, all 32 as-designed piers): plan bar
  groups per face (#11 / #14 / #18), paired 6e1 hoops + 5e2 crossties, flare cages (#7, 6e3). Clear cover checked: 2"
  hoops, 2.75" verticals, 3½"/4.25" flare. Approximations: flare 6e3 lapped pairs modeled as single closed hoops at half
  the spacing; column bars run continuous from the footing (plans: 11d2 dowels lapped, #18 mechanical couplers);
  11G/12G inner 6e4 ties not modeled. No editor UI for the new column-rebar inputs yet (script/API).

- **Close-ups:** `--closeup SUBn@Bridge` uses a station section cut, which is wrong for Ramp G (other alignment). The free `--camera E,N,Z,dx,dy,dz,width[,dist]` often frames poorly; it needs testing.
- **IfcOpenShell meshing of round-column ties** (Piers 4WB–7WB): those ties come out 2–4× their volume, cause unknown. Spirals and stirrups are fine.
- **Possible further IFC size cuts:** rebar typed by mark, about −35 MB; coordinates trimmed to 5 decimals, about −12 MB.
- **Elevation check** (`Checks/ElevationCheck`) doesn't know about as-designed caps yet.

## 4a. To do: column rebar approximations (from `column-rebar.csx`)

- Flare 6e3 hoops: the plans have lapped pairs at the column tie spacing; modeled as single closed hoops at half the spacing (same count).
- Column verticals run continuous from the footing. The plans have 11d2 footing-to-column dowels with an 8'-4 min. lap, and mechanical couplers on the #18 bars (18d3/18d4 below, 18d1/18d2 above).
- Pier 11G/12G inner 6e4 ties are not modeled.
- Bar 18d9 (terminates at the column top to clear the cap bars) is modeled running full height.
- Chamfer bar groups on the plans are slightly off-centre (e.g. 8 / 9¼ ends); exact positions are used where given, centred groups elsewhere.
- Editor UI for the new column-rebar inputs (faces, chamfer positions, hoop overlap, crossties, flare cage).
- Generic bearings: bearing body 16"×16" and masonry plate 1¾" are assumptions (vendor items).

## 4a-bis. Open decisions page (2026-10-11)

All pending user decisions (staking 1-4, Ramp G 19G 27" bar end calibration, Ramp G U3 end dam bars vs 20" flanges,
standard beam sheets download, Woodbury standard-beam placement) are on https://claude.ai/artifact/HGTVUujx3prgBrmWAjn5ra
(source temp/decisions/). The user saves choices there: read them with ArtifactData list, collection "decisions".

## 4b. Staking check findings: AWAITING USER REVIEW (2026-10-11; user is checking on the computer, do not change yet)

From `check-staking.csx` / `docs/plan-data/haunch-staking-check.md`:
1. Pier 11WB & 19G is 10 11/16" left along the pier line vs the staking footing centres (PDF 16: -31'-1 15/16, +9'-5 1/16,
   +30'-11 1/16; model -31'-11 5/8, +8'-6 3/8, +30'-0 3/8). PDF 63 gives CL WB 11 5/16" from CL pier at one bearing row;
   `piers-i80.csx` uses 11 5/8" at the pier CL. Staking vs pier sheet also differ 1" on footing 1-2 (40'-7 vs 40'-6).
2. Pier 6A: 13'-2 15/16 from CL Ramp A2 on PDF 15, 39, 41; `pier-6a.csx` uses 13'-2 5/16.
3. Pier 6WB footings: 2'-2 3/8 on PDF 16, 43, 45; `piers-i80.csx` uses 2'-2 3/16.
4. `docs/plan-data/camber/RG-unit2-defl_slab_in.csv` point 7 holds the unnumbered "midpoint of field section" column
   (3.13/2.95/2.78/2.61); PDF 116 point 7 = 3.05/2.87/2.71/2.55. Check the steel deflection / camber CSVs for the same shift.
Plan-only (no input change): Ramp G 5G footing staking 31'-6 x 22'-0 vs details 29'-0 x 24'-0 (model follows details);
allowable field haunch MAX in tension zones below the plans' nominal geometry (365 points).

## 5. Prestressed plan sets: extracted, program improvements QUEUED (user, 2026-10-11)

Plan data: `docs/plan-data/woodbury-i29-floyd/` (Iowa Design 817, 3-span BTD, integral abutments) and
`docs/plan-data/br02069-th65/` (MnDOT, 1 span 7 x 30MH). Each SUMMARY.md "Model mapping" lists the gaps in detail.
**User rule:** standard beams (Iowa BTx, MnDOT MH, ...) are taken from the state standard, not designed per bridge —
model them at the STANDARD length/strands/details; differences vs the layout are check warnings, never stretched beams.

Queue (shared by both sets first):
1. Beam shape catalog beyond Tx: Iowa BTA-BTE bulb tees (BTD first) and MnDOT MH (30MH), with true radius fillets
   (R 8 / R 2 BTD, 6" R 30MH) — the WBFL PrecastIBeam dims can't do them; per-shape standard data (lengths, strands).
2. Per-span (per beam line) prestressed beam definitions: type, standard length, strand pattern (BTD55/105/60).
3. Strand debonding (`StrandPoint` debond lengths; PGSuper import too) — Br02069 14 of 44 strands.
4. Harped strands at given grid columns + hold-down distance from midspan (vs `HarpPointFraction`).
5. Integral / semi-integral abutments with concrete end diaphragms (beams embedded), pier continuity diaphragms on pads.
6. Steel intermediate diaphragms between prestressed beams (C15 / W21 / W14; the prestressed path skips cross frames).
7. Straight chord girders with different spacing per span / different offsets each side of a pier (Woodbury).
8. Profile grade line offset from the stationing line (Woodbury pivot 5.00 ft RT).
9. Smaller: round CIP pipe piles, curved-plate bearings, future-beam pedestals, MnDOT Type S / Iowa aesthetic
   barriers, columns with brick-reveal projections, stage-joint deck edge, prestressed camber in the haunch check,
   beam bar generator (stirrups, end bars).
- Also: include borings in the IFC geotech representation where they're shown.

## 6. Last: cap aesthetic features (requested by the user)

- Model the architectural treatment of the pier caps, which is currently omitted ("aesthetic only" in the pier scripts):
  - the curved relief / reveal lines on the cap faces (I-80 pier elevations, e.g. sheet 22 for Pier 2WB);
  - the Ramp G cap relief ("pier cap elevation, showing reinforcing in architectural relief", e.g. sheet 19);
  - Detail A at the cap ends.
- Source: pier cap aesthetic details on I-80 design sheet 81 (PDF page 82) and the matching Ramp G sheet.
- Keep it an option on the as-designed cap (e.g. relief recesses cut into the cap faces), so the standard parametric bent is unaffected.

## 7. Deferred (user: "push these issues to the back")

Elevation mismatches left after the 2.4 cross-slope fix (`check-top-of-bearing.csx`, `check-top-of-slab.csx`):
- Unit 1 exterior girders A, E, F +0.5–0.9" top of bearing at Piers 1WB–3WB (likely flange thickness inputs).
- Unit 3 Pier 12WB girders A–C +0.6".
- **Conflict:** girder A at Pier 6A top of slab −1.0": plan A 1021.62 at 76.6' LT vs north edge 1021.47 at 77.6' LT (≈15%);
  my girder A position or deck edge there disagrees with the plan. Not adjusted.
- ✅ Ramp G top of bearing −1" through 3G–14G, +0.4 to +1.5" at 1G and 16G–19G — FIXED 2026-10-10 (girder line was
  1'-6 off, see section 0); now ±0.1".











