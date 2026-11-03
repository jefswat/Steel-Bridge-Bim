# Agent review 2: PTC main span lateral / strut / sway bracing connections (2026-11-03)

User: "Perform another review. Include the lateral bracing. Pay special attention to lateral bracing with missing connections. Example L11'."
Call it an AGENT REVIEW (never "check" / "QC"). READ-ONLY: write only your own output files; do not run sbm.exe, do not rebuild, do not edit project files.

## What "missing connection" means here
A bracing member end (lower / upper lateral, strut, sway, portal, floor lateral) is CONNECTED only when, as the sheets draw it:
1. the member reaches its connection plate(s) (top and / or bottom plate, or end angles) with the drawn lap,
2. the member is riveted to that plate with the drawn number of rivets (p.71 BOTTOM / TOP CHORD LATERAL SYSTEM diagrams give rivets per member END;
   p.97 / p.98 / p.80 / p.83 / p.95 / p.99 details draw them),
3. the plate itself is fastened to the truss (gusset stack via the edge angle, chord flange, strut, floor beam, wind chord) with rivets - a plate that hangs
   off the member end without reaching the stack is NOT connected,
4. nothing overlaps (members into plates / chords) and nothing floats with a gap.
Example L11' (image `png\S_L11p.png`, views C / D): the two laterals stop about 25-30 in. short of the chord and their purple plates hang off the lateral
tips without reaching the gusset stack - a missing connection. Check whether the same happens at L11 and everywhere else.

## Inputs
- Bracing images, one per bracing end point: `C:\SBM\qc\ptc\main-span\work11\png\{S|N}_<point>.png` (prime ' written as p; truss panel points are shown
  on the S plane, CL / work points on their own plane). Four views like the joint review: A outside elevation, B inside elevation (u to the right =
  MIRRORED), C plan from above (north up), D plan from below (north up); window +-65 in. around the point; inches from the point; u = up-station.
  Purple = bracing plates / connection angles (labels = marks; `-BG<point>-T/-B` top / bottom plates, `-EA` edge angle, `-F` fills, `-EC` end
  connections), khaki = bracing members, red = gussets, blue = chords, grey = floor beams; black circles = rivets whose shank runs along the view.
- Raw data per point: `work11\out\tris_<tag>.csv`, `rivets_<tag>.csv`, `parts.csv` (mark, category, description, UNRESOLVED reason), `joints.csv`.
- Automatic audit of every bracing member end: `work11\bracing_audit.csv` (member, role, end A/B, point, gap_in = distance from the working point to the
  member's cut end, n_conn = connection parts touching the last 0.6 m, n_rivets = rivets inside that end zone (crude box count: may miss rivets just
  outside the member box), conn_parts, flag NO_CONNECTION_PART / NO_RIVETS). Use it to find ends to look at; confirm each in the image / data.
- Truss joint images after the 2026-11-02 fixes: `C:\SBM\qc\ptc\main-span\work10\png_after\` (before: `png\`).
- Sheets: `C:\SBMData\Original Existing Bridge Plans from PTC.pdf` (raster; PyMuPDF, `python -I`): p.71 (systems + rivets per end), p.97 (lower laterals
  L1-L12, sway L7 / L8), p.98 (lower L15-L22, upper U12-U22), p.80 (L11 joint + bottom strut), p.79 (U11 portal), p.73 (L0), p.83 / p.95 (L13, F8,
  double K), p.99 (floor laterals), p.100 (portal knee braces), p.90 (L22 / U22). `C:\SBM\qc\ptc\main-span\work10\joint_sheets.json` maps truss joints.
  Calibrate any scaled length on a printed dimension and say so.
- Design notes: `C:\SBM\docs\plan-data\ptc-delaware-river\TRUSS-DESIGN.md` sections 19, 30, 33, 39 (bracing), 35, 38, 40, 41 (joints).
- The L22 gussets are being changed to the final condition (flame cut on lines X, p.90) by another agent: do not report the L22 1 in. gusset outline.

## Output (your batch id X)
- `C:\SBM\qc\ptc\main-span\work11\review\batchX.csv`: `point,member_or_part,item,verdict,severity,evidence_sheet,evidence_model,proposed_fix`
  verdict CONFIRMED | OK | NOT SHOWN | UNSURE; severity major (missing connection, member not reaching its plate, plate not fastened, wrong part,
  interference > 1/4 in.) | minor | info. One row per finding; a point with nothing wrong gets one OK row. Cite sheet page + what is drawn, and the image
  view + location.
- `review\batchX.md`: summary, counts, the systematic patterns (how many member ends are not connected, by point type), crops you used
  (`review\batchX_<point>_p<page>.png`).
Final message: verdict counts and the top systematic issues with the points, nothing else.
