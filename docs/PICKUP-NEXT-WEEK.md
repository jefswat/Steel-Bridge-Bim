# Pickup notes (written 2026-11-04 for the week of 2026-11-09)

Branch `bonner-pt-profiles-vertical-bars` (pushed through 8d06fb8 on 2026-11-03; later local commits listed at the end - push them first if they are
not on GitHub yet). Release build in `dist\SteelBridgeModeler` = 2026-11-03 18:19. Tests: 809 pass. User concern (2026-11-03): token use - work one
topic at a time, diagnose on a main-span-only copy, ask before running several agents (memory `limit-parallel-agents`).

## 1. Where the PTC main span stands

Two agent review rounds (2026-11-02 joints, 2026-11-03 bracing) and their fixes are in the model. Design notes: `docs/plan-data/ptc-delaware-river/TRUSS-DESIGN.md`
sections 33-47; review files `qc/ptc/main-span/work10` (joints) and `work11` (bracing: REVIEW2-BRIEF.md, review/B1-B6, J.csv); lessons in
`docs/AGENT-REVIEW.md` (last section).

- Bracing connections (generator audit `TrussModel.BracingConnections`, placed rivets whose shank crosses both parts; `work11/conn_report.csx` writes it to
  csv): member ends riveted 504 / 508, plates fastened 504 / 544, edge angles to the stack 468 / 514 (before the 2026-11-04 sway-top change).
- Clash (7 units): 3,031 rows (main span 995: 478 rail member x post, 189 inspection rope through posts, 60 socket x pin are by design; the rest are
  reported plan conflicts, walkway levels not dimensioned, and new connection parts). check-truss-ptc 613 PASS / 23 DIFF / 73 INFO / 2 CONFLICT
  (the new DIFFs: strut ends L1-L10 32-39 placed rivets vs p.71 40 / 46 - unclear what p.71 counts).
- Second-round fixes were verified by the agents that made them + the generator audit + images, NOT by an independent re-review yet.
- Before / after PDFs (joints, bracing) were sent to the user; regenerate with `qc/ptc/main-span/work11/make_pdf2.py` (env PDF_PART=joints|bracing).

## 2. Open PTC items with a known cause (do these first, one at a time)

Each needs a geometry change in `samples/plans/bracing-ptc.csx` / `details-ptc.csx` / `truss-ptc-arch.csx` or the bracing generator
(`src/SBM.Core/Truss/TrussGeneratorBracing.cs`). Diagnose with `qc/ptc/main-span/work9/dump.csx` (parts + triangles near marks, regex targets with `~`)
+ `work9/nrange.py` (extents in the bridge frame: u along, n across, z up, inches) on a copy; render with `work11/dump_bracing.csx` + `render_bracing.py`.

1. **Sway tops SW0-SW12: plate shape** - DONE 2026-11-04 in data (`details-ptc.csx` sway-top block: `CarryWholeLap`, `EdgeRivetCounts` 9 / 10 / 6 / 6,
   `RivetLines` 3; p.85 Section D-D). Verify the plate now reaches the post face and the floor beam (before: 4-8 of 24 angle-to-plate rivets). Results of
   the check run: see section 5.
2. **L11 / L11' up-station ('-b') bottom plates not fastened** (S/N-BGL11-b-B, -B-EA): the bottom edge angle L 8 x 7 1/2 x 1/2 runs 22 3/4 in. along the
   chord from the gusset edge OUTWARD (world x 717.115-717.734 vs gusset S-GL11-L max x 717.14) - its chord leg has no steel behind it beyond the gusset,
   so 0 rivets to the stack. p.80 View K / Section B: the plate pair lies on the chord inner web under the angle. Fix: place the angle over the chord web
   plies (rivets through angle + web plies) or keep it inside the gusset length; the L11 gusset outline itself is still the envelope (item 7).
3. **LM7-LM10 sway apex angles not fastened to the strut** (N-BGLMn-SW-B/T-EA, L 4 x 4 x 3/4): the sway plates are the lap envelope of two shallow
   diagonals, 146 in. long along the strut at LM7 (120 at LM9, 106 at LM10); the angles follow them. The lateral plate under them covers only n +-45 in.,
   beyond it the angle leg (+ its probed 1/4 in. fill) stands ~0.9 in. above the strut's top channel flange -> no rivet grips steel. p.97 draws
   '1-Pl 15 x 1 1/8' (L7 / L8; 15 x 1 at L10) under the angles = about that gap. Fix options: plate length along the strut as drawn on p.97 (scale it),
   and the fill = the drawn 1 1/8 / 1 plate on the channel flange where no lateral plate lies under it. LM0-LM6 have 1 of 2 plates fastened (same cause).
4. **LM12 / LM12' sway diagonals 0 rivets**: SW-N12 / SW-S12 (2-15[ 33.9) must stop ~67 in. from the CL above the rolled L12 strut (LS-L12 2-18[ 45.8), and
   their plates (N-BGLM12-SW-*) are clipped by the crossing laterals -> the diagonals do not reach them (10-17 in. gap). Check p.97 'similar' / p.71 for the
   LM12 sway bottom; probably the plate must run over the lateral plates (stack on them) instead of being clipped.
5. **LM13 / LM13' bottom plate not fastened to F8**: apex at the p.95 Section D W.P. (F8 web + 6 1/4 in.); the top plate has the L 9 x 4 x 1 shelf on F8
   (10 plate-leg rivets) but its web-leg rivets find no web behind the leg, and the bottom plate's connection to the F8 bottom extension (3/4 Pl. /
   2-Ls 7 x 4 x 7/8, p.95) is not built: F8 (`truss-ptc-arch.csx` `Fb("F8", ...)`) does not carry the extra angles (2-Ls 9x4x1 / 7x4x7/8 / 8x4x1, flagged
   'not built' because their legs / extents are not stated). Needs those pieces first.
6. **SW11 / SW11' floor beam angle 0 rivets**: p.79 'FLOOR BEAM CONNECTION TO U11-L11' Section A draws FB11's bottom with 2-Ls 9 x 4 x 5/8 and a
   1-Pl 37 x 1 that the model's FB11 (section F6, `truss-ptc-arch.csx` line ~456 picks F6 for every suspended beam) lacks. Needs an 'F6 at L11' section
   (or extra bottom pieces at the ends) - the extent along the beam is not dimensioned: scale from p.79 and flag.

Other open PTC items (flagged in the model, harder or need a user decision): LQ13 double K (p.95 plate in the inclined K plane, not dimensioned; the
new `TrussItemFrame.PitchDegrees / RollDegrees` can place it), W21 K at the shear-lock tongue beam (sheet 53 not in the set), L11 gusset outline (p.80
lower edges under the stacked layers), L13 / L13' splice plate fit (48 clash rows, which layer the plates occupy), 514 one-ply rivet symbols beyond the
gussets (`DropOnePlyBeyondRivets`; owner plate not identified on p.87 / p.88), U5 inside plate left end, strut rivet counts L1-L10 vs p.71, the
generator rule 'bracing members stop clear of their own plate's edge-angle legs' (sway-top fork worked around it with StartSetback data).
Approach trusses: 3-span L0 shoe sole 0.57 in. into the chord (p.59 work point readings disagree: reported, not moved).
Stale copies: `PTC main truss 18-20.sbm`, `PTC approach trusses.sbm` predate all of this (re-derive from the PTC model if they are still wanted).

## 3. PTC rebuild procedure

Order (CLAUDE.md): truss-ptc-arch -> deck-ptc -> connections-ptc -> shoes-ptc -> walkways-ptc -> floor-connections-ptc -> bracing-ptc -> details-ptc ->
components-ptc -> girder-details-ptc -> walkways-ptc (again) -> rivets-ptc; then clash, check-truss-ptc / -approach / check-girders-ptc /
check-ptc-delaware-river, `sbm pipeline <model> --no-terrain --no-index` for the database. Script: `temp/rb/chain5.sh` (full, ~25 min; per-step `--no-db`
so the database is built once at the end). A data change in one script only needs the chain from that script on. Heavy runs take
`C:\SBM\temp\heavy.lock` (noclobber, content = owner; delete only your own). Use `src/SBM.Cli/bin/Debug/net10.0/sbm.exe`, never `dist\sbm.exe`.

## 4. Other work queued (docs/TODO.md)

- Street view on an alignment, drop a person on the ground surface (needs a perspective camera; the viewport is orthographic).
- Plan sets stored as TIFF (scans: PyMuPDF opens TIFF; the app's sheet window uses Edge's PDF viewer -> convert once or add an image viewer).
- Databases: all sample models built 2026-11-03 except the two stale PTC copies; Blatnik / Richmond have no sheet-links file (concepts, no plans).
- `Br02069 TH65.agent-review.json` was created by the pipeline run and is not committed (CLAUDE.md says agent-review records are committed).

## 5. Last session's final state

- Item 2.1 (sway-top plate shape) applied and the PTC model rebuilt from details-ptc on (2026-11-04): sway-top angle-to-plate rivets median 2 -> 6 per
  angle (p.85 draws 10 in the floor beam angle's plate leg, 6 in the post angle's: still short - the plate still does not reach the full corner; check
  whether `CarryWholeLap` reaches the post face, else the generator change 'keep the crossing search with an explicit Outline' is the next step).
  All 104 sway-top plates fastened; clash 3,031 rows and check-truss-ptc 613 / 23 unchanged; bracing totals unchanged (ends 504 / 508, plates 504 / 544,
  angles to the stack 468 / 514).
- Items 2.2-2.6 NOT done: each needs a geometry change (diagnosis above), estimated one focused session each.
- Everything committed and pushed; release build `dist\SteelBridgeModeler` 2026-11-03 18:19 is current (no compiled code changed since).
