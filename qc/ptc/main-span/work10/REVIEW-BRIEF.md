# Agent review brief: PTC main span truss joints (2026-11-02)

User request: "These same gusset issues exist everywhere. Take screenshots of each joint with an agent to confirm. Main span only."
Call this work an AGENT REVIEW (never "check" or "QC"). You are a READ-ONLY reviewer: do not edit any file outside your own output files below,
do not run sbm.exe, do not rebuild anything.

## Inputs
- Model images (one per joint and truss plane), rendered from the CURRENT model `samples/plans/PTC Delaware River Bridge.sbm`, bridge "PTC Arch truss 18-20":
  `C:\SBM\qc\ptc\main-span\work10\png\{S|N}_{point}.png` (prime ' written as p, e.g. `S_L21p.png`). Four views, inches from the joint working point (W.P.),
  u = along the bridge, + = up-station (increasing station):
  - A outside elevation (viewer outside the truss plane)
  - B inside elevation (viewer at the bridge CL), drawn with u to the right = MIRRORED relative to what that viewer sees
  - C plan from above, D plan from below (north up; the N truss is at the top of C / D, the S truss at the bottom)
  Colours: gussets red (labels = part marks), bracing connection plates purple, connection angles dark purple, splice plates orange, fills yellow, chords blue,
  diagonals green, verticals / hangers teal, bracing members khaki, floor beams grey, stringers brown, shoes / pins dark. Black circles = rivets whose shank runs
  along the view direction. Walkways / ladders / rails are left out. Painter's algorithm: the nearest face is on top (no hidden lines).
- Raw data per joint if a number is needed: `C:\SBM\qc\ptc\main-span\work10\out\tris_{tag}.csv` (triangles, world metres), `rivets_{tag}.csv`,
  `joints.csv` (W.P. world point, tangent tx/ty, right normal nx/ny), `parts.csv` (mark, category, description, UNRESOLVED reason).
  You may write your own python (run with `python -I`, matplotlib + PyMuPDF available) to measure or to render extra views into YOUR output folder.
- Plan sheets: `C:\SBMData\Original Existing Bridge Plans from PTC.pdf` (raster scans). Sheets per joint: `C:\SBM\qc\ptc\main-span\work10\joint_sheets.json`
  (main truss joint sheets p.73-90; lateral / sway bracing p.97 / p.98; floor beam connections p.85, p.93-95; truss diagram + member table p.69 / p.70; bracing
  systems p.71). Render pages with PyMuPDF (e.g. `page.get_pixmap(dpi=150, clip=...)`), crop the joint detail, and read it. Calibrate any scaled length on a printed
  dimension and say so. The primed joints (L1'..U21') are the other half of the span: the sheets draw one half ("other half symmetrical" / "opposite hand").
- Design notes: `C:\SBM\docs\plan-data\ptc-delaware-river\TRUSS-DESIGN.md` (sections 11-36 describe how gussets, layers, splices, fills, rivets, bracing plates were
  modeled and what is flagged UNRESOLVED). Section 33 = lateral / strut plates traced from p.97 / p.98 (2026-11-02), section 35 = joint fit (2026-11-02).

## What to look for at every joint (both truss planes N and S, compare them too)
1. Gusset outline vs the drawn plate (edges, corners, chamfers, which members the plate covers, how far along each member).
2. Rivets: do the drawn rivet fields lie inside the modeled gusset; are there modeled rivets outside any plate / in empty space.
3. Gusset layers / splice plates / fills: the stack (inner gusset, outer layers, splice plates, fills) vs the sheet's material list and sections.
4. Members at the joint: ends overlapping each other, overlapping the gusset stack, ending short of their connection, wrong side of the gusset.
5. Lateral / strut / sway connection plates at the joint (views C / D): outline, thickness, edge angles, member landing, plates floating or cut into members.
6. Anything else plainly wrong (missing part that the sheet draws, part the sheet does not draw, mirror / hand error between halves, N vs S difference).
Report ONLY what you can support from the sheet (page + what is drawn / dimensioned) and the image (view letter + location). If the sheet does not show it,
say "not shown on the sheet" instead of guessing. Never propose invented dimensions; a fix proposal must cite its plan evidence or say what is missing.

## Output (your batch number N)
- `C:\SBM\qc\ptc\main-span\work10\review\batchN.csv` with header
  `joint,plane,item,verdict,severity,evidence_sheet,evidence_model,proposed_fix`
  verdict = CONFIRMED (model differs from the sheet) | OK | NOT SHOWN (sheet does not show it) | UNSURE; severity = major (wrong part / missing / large
  interference) | minor (shape off a few inches, small overlap) | info. One row per finding; a joint with no finding gets one OK row.
- `C:\SBM\qc\ptc\main-span\work10\review\batchN.md`: short summary, the systematic patterns you saw (the user believes the same gusset issues exist everywhere -
  confirm or refute with counts), and the sheet crops you used (save crops as `review\batchN_<joint>_p<page>.png`).
Final message: the summary table of verdict counts and the top systematic issues (with joints), nothing else.
