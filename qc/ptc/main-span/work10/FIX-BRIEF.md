# Fix brief: PTC main span joints after the 2026-11-02 agent review

User: "Fix anything you have evidence to support. These same gusset issues exist everywhere ... main span only. Continue."
Evidence: the six agent review batches `C:\SBM\qc\ptc\main-span\work10\review\batch{1..6}.csv` / `.md` + sheet crops (read the rows of your topic in ALL six batches;
primed joints are exact mirrors of the unprimed ones: fix the data once, the scripts mirror it). Joint images: `work10\png\{S|N}_<joint>.png`
(see `work10\REVIEW-BRIEF.md` for the views); re-render after your fix with `work10\dump_joints.csx` + `render_joints.py` (copy them; untracked).
Plan sheets: `C:\SBMData\Original Existing Bridge Plans from PTC.pdf` (raster; PyMuPDF, `python -I`; calibrate on printed dimensions and record the scale).
Design notes: `docs/plan-data/ptc-delaware-river/TRUSS-DESIGN.md` (read sections 11-37 first), CLAUDE.md "Steel TRUSS" / "PTC ..." paragraphs.

## Rules (user's standing rules)
- Never invent values. A value read from a dimension = fact; a length scaled from the raster = allowed only with a dimension-only UNRESOLVED flag naming page,
  scale and reason (`UnresolvedFlag` / `U(...)` helper in the scripts). Not on the sheets = UNRESOLVED, not built or built as the documented rule says.
- Plan conflicts are reported (CONFLICT text), never adjusted. No code-rule defaults.
- New generator inputs OPTIONAL and omitted from the .sbm JSON when unset; other bridges' output unchanged (approach trusses / girder spans use the same generator:
  their clash counts must not rise).
- Call the work an agent review, never "check" or "QC" in new text.

## Working method
- You work in your own git worktree (isolated). First `git reset --hard 8bd1cf5` if your branch starts elsewhere (it has every fix so far).
- `dotnet build SteelBridgeModeler.sln` in YOUR worktree (dotnet is on PATH at C:\sbmcache\dotnet); never run dist\sbm.exe.
- Test on a COPY of `samples/plans/PTC Delaware River Bridge.sbm` under your worktree's temp/. PTC build order (CLAUDE.md): truss-ptc-arch -> deck-ptc ->
  connections-ptc -> shoes-ptc -> walkways-ptc -> floor-connections-ptc -> bracing-ptc -> details-ptc -> girder-details-ptc -> walkways-ptc (again) -> rivets-ptc;
  run from the first script you changed. A main-span-only diagnostic dump takes ~1 min; the full chain ~15 min.
- Heavy runs (any sbm run / clash on the PTC model): take `C:\SBM\temp\heavy.lock` with noclobber, content = your fork name; wait (sleep loop) if taken; delete it only
  if its content is your name. Other forks run at the same time (24 GB RAM).
- Verify: re-render the joints you changed and compare with the sheet crops; `sbm clash <copy>` (all bridges) before / after; `check-truss-ptc.csx` before / after;
  `dotnet test tests/SBM.Tests` must pass (add a focused test for any generator change).
- Commit in your worktree branch: code, scripts, data CSVs, docs (one new TRUSS-DESIGN.md section titled with your topic; CLAUDE.md one-line note only if a
  generic input is added). Do NOT commit .sbm / .sqlite / IFC / images. Do not push. Do not edit files owned by another fork (listed in your prompt).
- Final report: what was wrong (joints, sheet evidence), what changed, counts before / after (clash rows by category, check-truss-ptc), still-open items with
  their flags, branch + commit ids.
