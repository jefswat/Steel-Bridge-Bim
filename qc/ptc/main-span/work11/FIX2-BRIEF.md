# Fix brief 2: PTC main span bracing connections (agent review 2, 2026-11-03)

Same rules and working method as `C:\SBM\qc\ptc\main-span\work10\FIX-BRIEF.md` (read it), with these changes:
- Reset your worktree to commit f98613e first (`git reset --hard f98613e`).
- Evidence: `C:\SBM\qc\ptc\main-span\work11\review\B1..B6.csv / .md` (+ crops), the audit `work11\bracing_audit.csv`, images `work11\png\`. Brief of the
  review: `work11\REVIEW2-BRIEF.md` ("missing connection" definition). User: "Pay special attention to lateral bracing with missing connections. Example L11'."
- A bracing connection is complete only when (1) the member reaches its plate with the drawn lap, (2) member-to-plate rivets as drawn / p.71 per end,
  (3) the plate is fastened to the truss / strut / floor beam / wind chord (edge angle riveted to the stack, plate riveted to the flange, end angles),
  (4) no float, no overlap. Count PLACED rivets whose shank crosses both parts, not definition counts.
- The primed half and the N truss are exact mirrors: fix the data once.
- Re-render the bracing points you change with `work11\dump_bracing.csx` + `render_bracing.py` (copy; set the env var SBM_W=130) and re-run
  `work11\bracing_audit.csx`; report before / after per point (rivets per end, connected yes / no).
- Another agent is changing L22's 1 in. gussets (fork-l22): do not touch L22 joint data. Keep generator changes generic and small; if two of you need
  the same generic change in TrussGeneratorBracing.cs, the one named owner below makes it and the others report what they need.
