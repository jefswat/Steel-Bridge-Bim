# PTC main truss (spans 18-20) - gusset agent review, 2026-10-30

Scope: every main-span truss-plane joint (U0-U22, L0-L22; primed halves mirror the unprimed data, U22 / L22 single) plus W14, against the joint
sheets p.73-90 (PDF `C:\SBMData\Original Existing Bridge Plans from PTC.pdf`, raster, 3/4 in. = 1 ft-0, 4.28 px/in at 150 dpi). Checked: inner gusset
thickness, in-to-in, stacked layers, drawn outline vs `gusset-outlines.csv`, free-edge angles, plates under / beside the gussets (`splices.csv`, batch 8b
fills). Model values = the saved `samples/plans/PTC Delaware River Bridge.sbm` (2026-10-30 12:56 rebuild), joint definitions of "PTC Arch truss 18-20"
read from the file (no regeneration). Bracing / wind-chord / LM gussets (`BracingGussets`, 312 definitions, batch 5) were NOT re-read in this pass.
Work files (crops, overlays): scratchpad `gus/` (not kept).

Counts (56 rows; G35-G38 cover several joints each): OK 40, DIFF 12, UNRESOLVED 4, CONFLICT 0 (plan-vs-plan: none new; the known L11 Section E vs F conflict GN07 stays as reported).

## 1. Thickness / in-to-in per joint

| id | joint | item | plan (page) | model | verdict | fix |
|---|---|---|---|---|---|---|
| G01 | U0 | gusset / in-to-in | 2-7/8 Gussets (2'-5 in. to in.) (C) p.73 | 7/8, 29 | OK | - |
| G02 | L0 | gusset / in-to-in | 2-7/8 Gussets (C); no in-to-in printed p.73 | 7/8, section width | OK | - |
| G03 | U1 | gusset / in-to-in | 2-3/4 Gussets (C), 2'-5 in. to in. Guss. p.74 | 3/4, 29 | OK | - |
| G04 | L1 | gusset | "similar to L3"; L3 2-1/2 Gus. Pls. (C) p.74 / 75 | 1/2 | OK | - |
| G05 | L2 | gusset / in-to-in | 2-3/4 Gussets (C), 2'-5 p.74 | 3/4, 29 | OK | - |
| G06 | U2 | gusset / in-to-in | 2-5/8 Gussets (C), 2'-5 p.74 | 5/8, 29 | OK | - |
| G07 | U3 | gusset / in-to-in | 2-3/4 Gus. Pls., 2'-5 in. in. Gus. Pls. p.75 | 3/4, 29 | OK | - |
| G08 | L3 | gusset | 2-1/2 Gus. Pls. (C) p.75 | 1/2 | OK | - |
| G09 | U4 | gusset | 2-5/8 Gus. Pls. p.75 | 5/8, 29 | OK | - |
| G10 | L4 | gusset / in-to-in | 2-3/4 Gus. Pls., 2'-5 p.75 | 3/4, 29 | OK | - |
| G11 | U5 | gusset | 2-7/8 Gus. Pls. p.75 | 7/8, 29 | OK | - |
| G12 | L5 | gusset | 2-3/4 Gus. Pls. p.75 | 3/4, 29 | OK | - |
| G13 | U6 | gusset / in-to-in | 2-5/8 Gusset Pls., 2'-7 in. to in. over 2'-5 o. to o. webs (o.s. splice 34 x 1 under) p.76 | 5/8, 31 | OK | - |
| G14 | L6 | gusset / in-to-in | 2-7/8 Gusset Pls., 2'-5 p.76 | 7/8, 29 | OK | - |
| G15 | U7 | gusset / in-to-in | 2-1 1/8 Gusset Pls., 2'-5 p.76 | 1 1/8, 29 | OK | - |
| G16 | L7 | gusset / in-to-in | 2-5/8 Gusset Pls., 2'-7 in. to in. / 2'-5 o. to o. webs p.76 | 5/8, 31 | OK | - |
| G17 | U8 | gusset / in-to-in | 2-5/8 Gussets (2'-6 1/2 in. to in.), o.s. 34 x 3/4 under p.77 | 5/8, 30.5 + UG 3/4 | OK | - |
| G18 | L8 | gusset / in-to-in | 2-1 in. Gussets, 2'-5 p.77 | 1, 29 | OK | - |
| G19 | U9 | gusset / in-to-in | 2-1 in. Gussets, 2'-5 p.77 | 1, 29 | OK | - |
| G20 | L9 | gusset / in-to-in | 2-5/8 Gussets (2'-6 1/2 in. to in.), o.s. 34 x 3/4 p.77 | 5/8, 30.5 + UG 3/4 | OK | - |
| G21 | U10 | gusset / in-to-in | 2-3/4 Gussets, o.s. 34 x 1 1/8 (2'-7 1/4) p.78 | 3/4, 31.25 + UG 1 1/8 | OK | - |
| G22 | L10 | gusset layers | 2-3/4 Gussets (large outline) + 2-1/2 Gussets (central zone) p.78 | inner 3/4 + outer 1/2 layer | OK | - |
| G23 | L10 | in-to-in | 2'-5 in. to in. Gussets (plan) p.78 | NOT SET (0 -> chord section width) | DIFF | the whole-joint row has no thickness, so connections-ptc.csx section 1 skips it (`if (th == null) continue`) and its 29 is lost. gussets.csv line 56: `gusset_thk_in` "" -> 0.75 (= the inner layer, joints.csv / gusset-layers.csv) |
| G24 | U11 | gusset | 2-1 1/8 Gus. Pls. (S), edge Ls 5 x 3 1/2 x 7/16 (C) both sides p.79 | 1 1/8, 29, 2 edge angles | OK | - |
| G25 | L11 | stack | 1 1/8 + 3 x 3/4, 2'-2 3/4 in. to in. (batch 3a hi-res read) p.80 | same | OK (not re-read; GN07 conflict stays reported) | - |
| G26 | U12 | gusset | 2-1 1/8 Gussets, edge L 4 x 3 1/2 x 7/16 (C) both sides p.81 | 1 1/8, 29, 2 | OK | - |
| G27 | L12 | gusset / in-to-in | 2-1 in. Gussets p.81; in-to-in NOT printed (plan: 2'-5 o. to o. 1 1/8 webs); o.s. 34 x 1 splice + o.s. 34 x 1 1/8 inlay in the joint | 1, 29 | OK (29 kept: same layout as L14 / L15 where 2'-5 in. to in. IS printed; see G44) | - |
| G28 | U13 | gusset | 2-1 1/8 Gus. Pls., edge 2-Ls 4 x 3 1/2 x 7/16 (C) both sides p.82 | 1 1/8, 29, 2 | OK | - |
| G29 | U14 | gusset | 2-1 1/8 Gus. Pls. p.82 | 1 1/8, 29 | OK | - |
| G30 | U15 | gusset | 2-7/8 Gus. Pls. p.82 | 7/8, 29 | OK | - |
| G31 | L13 | gusset | 2-1 1/8 Gusset Pls., edge 2-Ls 4 x 3 1/2 x 7/16 (C) p.83 | 1 1/8, 29, 2 | OK | - |
| G32 | L14 | gusset / in-to-in | 2-1 1/8 Gusset Pls., 2'-5 in. to in. Gussets (plan) p.84 | 1 1/8, 29, 3 | OK | - |
| G33 | L15 | gusset / in-to-in | 2-1 1/8 Gussets, 2'-5 in. in. Gussets (plan) p.86 | 1 1/8, 29 | OK | - |
| G34 | L16 | gusset / in-to-in | 2-1 in. Gussets, 2'-5 p.86 | 1, 29, 1 edge L | OK | - |
| G35 | U16 / U17 / L17 | gusset / in-to-in | 2-7/8 Gussets, 2'-5, one stiff. L 4 x 3 1/2 x 7/16 (C) each p.87 | 7/8, 29, 1 each | OK | - |
| G36 | U18 / U19 / L18 / L19 | gusset / in-to-in | 2-7/8 Gus. Pls., 2'-5 in. in. Gus. Pls. p.88 | 7/8, 29 | OK | - |
| G37 | U20 / L20 | gusset / in-to-in | 2-7/8 Gussets / 2-1 in. Gusset Pls., 2'-5 p.89 | 7/8 / 1, 29 | OK | - |
| G38 | U21 / L21 | gusset / in-to-in | 2-3/4 / 2-1 in. Gussets (2'-7 1/4 in. to in.), o.s. 34 x 1 1/8 p.89 | 3/4 / 1, 31.25 + UG 1 1/8 | OK | - |
| G39 | U22 | gusset / pin plates | 2-1/2 Gus. Pls. (C) on the vertical; pin plates 34 x 1 1/8 + 34 x 1 + 34 x 1 o.s., 2'-11 1/4 o. o.; 2-3/8 Gus. Pls. (C) = temp. erection support p.90 | 1/2, 35.25, 3 UG plates | OK | - |
| G40 | L22 | layers | 2-1 in. Gus. Pls. (two halves, 1/8 apart) + 2-1 1/8 Gus. Pls. p.90 | inner 1 + outer 1 1/8 | OK | - |
| G41 | W14 / W14' | gusset | PLAN AT W14: "2'-5 In to In Guss." (gussets exist); no "Gus." thickness callout. The only plates at that place are "2-Pls 19 x 1 1/8" (tapered, floor beam to rigid hanger) p.85 | no gusset (GussetThickness 0) | UNRESOLVED | (p.85) the gussets are drawn but no thickness is printed; if the 2-Pls 19 x 1 1/8 are the W14 gussets the thickness is 1 1/8 - needs a plan reading / user decision, not set |

## 2. Plates in the joints (splice plates, fills)

| id | joint | item | plan (page) | model | verdict | fix |
|---|---|---|---|---|---|---|
| G42 | U5 | 2-Fills 18 x 5/8 (left) | p.75: third left fill, dimension line from the Fin. line leftward, about 54 1/2 in. (scaled at 300 dpi on the same line as 31 1/2 x 9/16 = 35.9 / 31 1/2 x 3/4 = 42.3, which match the built 35.6 / 41.8) | not built (length empty -> skipped) | DIFF | splices.csv line 28: `length_in` "" -> 54.5, `extent_note` -> "scaled from the Fin. line leftward at 300 dpi (agent review gussets G42), +-1 in.", confidence low |
| G43 | U7 | 2-Fills 31 1/2 x 9/16 and 2-Fills 18 x 3/4 | p.76: both dimension lines end at the Fin. line FROM THE LEFT (one-sided) | built centred on the joint (offset 0) | DIFF | splices.csv line 51: item -> "web fills 31 1/2 x 9/16 (left)"; line 52: item -> "web fills 18 x 3/4 (left)" (the extent notes already say leftward; the script reads the side from the item text) |
| G44 | L12 / L14 / L15 | o.s. splice plates 34 x 1 | p.81 / 84 / 86: elevation calls "2-Spl. Pls. 34 x 1 - o.s."; plans print 2'-5 in. to in. Gussets at L14 / L15 (so the plate is NOT under the gusset; the L14 plan draws it beyond the gusset end, in the gusset plane), not printed at L12 | not built (no length) | UNRESOLVED | (p.84 / 86) where the 34 x 1 plate ends against the gusset is not dimensioned and the elevation dimension line runs into the gusset zone; when these are built use a plate BESIDE the gusset (not `UnderGusset`) and keep in-to-in 29 |
| G45 | L5, U6, U7, L6, L7, U8, U9, L8, L9, U10, L10, U11, U12, L12, U13, U14, L13, U15, L14, L15, L16, U16, U17, L17, U18, U19, L18, L19, U20, U21, L20, L21, U22, L22 | chord COVER (flange) splice plates (2-Spl. Pls. 24 x t, cover fills) | drawn in every flange plan view, sizes in splices.csv lines 38, 42, 48, 53, 59, 66, 71, 78-79, 85, 90, 96, 101, 108, 114, 119, 125-126, 130, 135, 140, 143, 148-149, 153, 157, 160, 162, 164, 166, 168, 170, 172, 174, 176, 181, 182 | NOT BUILT at 34 joints (U1-U5, L2, L4 only): `length_in` empty -> skipped | DIFF | lengths are not printed; scale them from the plan views (rectangle ends / dimension arrows, along the chord), e.g. U6 about 27 in. (plan p.76, rectangle 115 px at 150 dpi perpendicular); suggested pass: detect the plan-view rivet symbols with tools/ptc_rivet_count.py and take span + 2 x 1 3/4 in. edge distance, flag dimension-only. The cover splice rivets (p.70 1 1/8 H.S.) of these joints are not placed either |
| G46 | U7, L8, U9, L9, L10, U11, U12, L12, U13, L13, U14, U15, L15, L16, U16-U21, L17-L21, L22 | inside (i.s.) web splice plates (31 1/2 x t) and their fills | splices.csv rows with `length_in` empty | not built (U8 on: by design, "inside the web stack"); at U7 the two fills ARE built (lengths scaled) but the 2-i.s. Spl. Pls. 31 1/2 x 15/16 they pack are not | DIFF | U7: scale the 15/16 plate (p.76, line from about 41 in. right of Fin. to the left end of the 18 x 3/4 fill) or drop the U7 fills until it is built; for U8 on keep the documented decision |
| G47 | L3 / L5 / L7 | 1-L 5x5x7/16 + 1-Fill 6 x 1/2 (L3), 2-3/4 Fills at the flange-angle corners (L5), 1-Fill 7 x 5/8 top / bottom (L7) | p.75 / 76 | not built (no length) | DIFF | lengths are drawn only (scale); low priority |

## 3. Outlines and gusset free-edge angles

| id | joint | item | plan (page) | model | verdict | fix |
|---|---|---|---|---|---|---|
| G48 | U14 | outline, right edge | p.82: the drawn edge leaves the chord top at about x 1617 (150 dpi) and runs down to (1657, 1080), then plumb to (1663, 1300); the open chord-splice rivets at x 1640-1680 are OUTSIDE it | vertex (1665, 915) = up to 12 in. outboard at the chord | DIFF | gusset-outlines.csv line 72 `outline_px`: "1200,1020; 1665,915; 1665,1300; ..." -> "1200,1020; 1617,925; 1657,1080; 1663,1300; 1540,1345; 1460,1360; 1355,1365; 1250,1188" (then the existing zone line 45 finds the edge and the second edge angle is built) |
| G49 | U15 | outline, right edge | p.82: drawn right edge is a sloped line (2228, 510) - (2250, 725), about (2213, 359) at the chord top | vertex (2250, 350) = about 4-5 in. outboard | DIFF | gusset-outlines.csv line 73 `outline_px`: "1845,452; 2250,350; 2250,725; ..." -> "1845,452; 2213,359; 2250,725; 2090,800; 1955,812; 1860,608" (zone line 52 then finds the edge) |
| G50 | L15 | second edge angle (upper-right, 2-Ls 4 x 3 1/2 x 7/16 (C)) | p.86 | not built: rivet-count-zones.csv line 5 note lacks the word "edge" (connections-ptc.csx line 468 filter) | DIFF | rivet-count-zones.csv line 5 note -> "bracing angle strip 2-Ls 4 x 3 1/2 x 7/16 (C) on the gusset edge" |
| G51 | U8, U9, L9 (2), L10 (2), L12 (2), L18 (+ L19 via its outline), U19, U20 | gusset free-edge angles drawn with callouts | U8 / U9 / L9 2-Ls 4 x 3 1/2 x 7/16 (C) p.77; L10 L 4 x 3 1/2 x 7/16 (C) both edges p.78; L12 L 4 x 3 1/2 x 7/16 (C) both edges p.81; L18 / L19 / U19 2-Ls 4 x 3 1/2 x 7/16 (C) p.88; U20 2-Ls 4 x 3 1/2 x 7/16 (C) p.89 | none (no strip zone rows for these joints) | DIFF | add the 11 zone rows of section 5 to rivet-count-zones.csv |
| G52 | L10 | edge angle size in gussets.csv | p.78 at 300 dpi: "L 4 x 3 1/2 x 7/16 (C)" both edges | gussets.csv line 56 text "angle member 4x3 1/2x7/8 (C)" (misread) | DIFF (data text) | gussets.csv line 56 group text: "4x3 1/2x7/8 (C)" -> "4x3 1/2x7/16 (C)" |
| G53 | U21 | edge angle | U21 uses the U20 outline (also_applies_to); U21 draws no strip at that edge on p.89 that I could read | would inherit U20's zone (G51) | UNRESOLVED | (p.89) with the G51 U20 row U21 / U21' also get the angle; check p.89 at 300 dpi before applying, or give U21 its own outline row |
| G54 | L21 | edge angle | p.89 draws 2-Ls 4 x 3 1/2 x 7/16 (C) at the upper-left of L21 | none; L21 shares the L20 outline (L20 draws no strip) | UNRESOLVED | (p.89) a zone on the shared outline would also put it on L20; L21 needs its own outline row first |
| G55 | U8, U9, L9, L10, L12, L15, L18, U19, U14, U15, U20 | outline vs drawn plate (overlay at 150 / 300 dpi) | - | within about 2 in. except G48 / G49 | OK | - |

## 4. Data file defects

| id | file / line | item | verdict | fix |
|---|---|---|---|---|
| G56 | gussets.csv line 55 (U10 vertical) and line 86 (L21) | unquoted commas in `outline_note` shift the columns: `page` = " batch 8); 5 in rad half holes" / " the value column held 29); ...", `confidence` = "78" / "89". The L21 row sets the gusset, so its UNRESOLVED flag text names a page "p. the value column held 29); ..." | DIFF | quote the `outline_note` field of both rows (no value change) |

## 5. Machine-applicable edits

```
# docs/plan-data/ptc-delaware-river/connections/gussets.csv
line 56 (main,L10,whole joint ...): gusset_thk_in "" -> 0.75 ; text "angle member 4x3 1/2x7/8 (C)" -> "angle member 4x3 1/2x7/16 (C)"     # G23, G52
line 55 (main,U10,vertical U10-L10 ...): wrap outline_note in double quotes                                                  # G56
line 86 (main,L21,gussets ...): wrap outline_note in double quotes                                                           # G56

# docs/plan-data/ptc-delaware-river/connections/splices.csv
line 28 (main,U5,web fills 18 x 5/8 (left)): length_in "" -> 54.5 ; extent_note -> "scaled from the Fin. line leftward at 300 dpi (agent review gussets G42), +-1 in." ; confidence -> low   # G42
line 51: item "web fills 31 1/2 x 9/16" -> "web fills 31 1/2 x 9/16 (left)"                                                     # G43
line 52: item "web fills 18 x 3/4" -> "web fills 18 x 3/4 (left)"                                                              # G43

# docs/plan-data/ptc-delaware-river/connections/gusset-outlines.csv (outline_px)
line 72 (U14): "1200,1020; 1617,925; 1657,1080; 1663,1300; 1540,1345; 1460,1360; 1355,1365; 1250,1188"                             # G48
line 73 (U15): "1845,452; 2213,359; 2250,725; 2090,800; 1955,812; 1860,608"                                                        # G49

# docs/plan-data/ptc-delaware-river/connections/rivet-count-zones.csv
line 5 (main,L15 ...): note -> "bracing angle strip 2-Ls 4 x 3 1/2 x 7/16 (C) on the gusset edge"                                   # G50
append (G51; zones = the outline edge +-15 px, 8-92 % of its length, 150 dpi page px):
main,U8,SKIP,"679,491;630,646;659,655;708,500",right bracing angle strip 2-Ls 4 x 3 1/2 x 7/16 (C) on the gusset edge (p.77; agent review gussets G51, outline edge 2)
main,U9,SKIP,"1599,467;1500,734;1529,745;1628,478",right bracing angle strip 2-Ls 4 x 3 1/2 x 7/16 (C) on the gusset edge (p.77; agent review gussets G51, outline edge 2)
main,L9,SKIP,"1175,1043;1246,917;1220,902;1149,1028",left bracing angle strip 2-Ls 4 x 3 1/2 x 7/16 (C) on the gusset edge (p.77; agent review gussets G51, outline edge 5)
main,L9,SKIP,"1343,919;1410,1074;1437,1062;1370,907",right bracing angle strip 2-Ls 4 x 3 1/2 x 7/16 (C) on the gusset edge (p.77; agent review gussets G51, outline edge 1)
main,L10,SKIP,"500,718;554,497;525,489;471,710",left bracing angle strip L 4 x 3 1/2 x 7/16 (C) on the gusset edge (p.78; agent review gussets G51, outline edge 1)
main,L10,SKIP,"978,503;1053,814;1082,807;1007,496",right bracing angle strip L 4 x 3 1/2 x 7/16 (C) on the gusset edge (p.78; agent review gussets G51, outline edge 5)
main,L12,SKIP,"496,869;554,466;525,461;467,864",left bracing angle strip L 4 x 3 1/2 x 7/16 (C) on the gusset edge (p.81; agent review gussets G51, outline edge 0)
main,L12,SKIP,"876,407;969,554;994,538;901,391",right bracing angle strip L 4 x 3 1/2 x 7/16 (C) on the gusset edge (p.81; agent review gussets G51, outline edge 4)
main,L18,SKIP,"827,829;919,957;943,939;851,811",right bracing angle strip 2-Ls 4 x 3 1/2 x 7/16 (C) on the gusset edge (p.88; agent review gussets G51, outline edge 2; L19 takes the L18 outline)
main,U19,SKIP,"1508,673;1449,534;1422,546;1481,685",left bracing angle strip 2-Ls 4 x 3 1/2 x 7/16 (C) on the gusset edge (p.88; agent review gussets G51, outline edge 6)
main,U20,SKIP,"571,689;476,570;452,589;547,708",left bracing angle strip 2-Ls 4 x 3 1/2 x 7/16 (C) on the gusset edge (p.89; agent review gussets G51, outline edge 6; see G53 for U21)
```

Notes for applying:
- rivet-count-zones.csv is also the SKIP list of `tools/ptc_rivet_count.py`; the counts in rivet-counts.csv are not recomputed by these rows (a re-count would
  drop the strip rivets from the member groups, which is what the existing rows already do).
- After the edits: run connections-ptc.csx onward (build order in CLAUDE.md), check the "gusset edge angles (batch 4d)" report line (expected +13 strips:
  U8, U9, L9 x2, L10 x2, L12 x2, L18 / L19, U19, U20 / U21, U14, U15, L15) and that L10 reports in-to-in 29.
- Not in this review: rivets (separate review), bracing / wind-chord / LM gussets (p.97-99), the L13 countersunk near-side heads ("csk. N.S. in gusset and
  34 x 1 1/16 inlay Pl.", p.83) - for the rivet review.

## 6. Outcome (fix pass 2026-10-30, rebuilt model; check-truss-ptc 624 / 11 -> 616 PASS / 19 DIFF / 73 INFO / 2 CONFLICT)

| id | outcome |
|---|---|
| G23 / G52 | fixed: gussets.csv L10 gusset_thk 3/4, edge angle L 4 x 3 1/2 x 7/16 |
| G56 | fixed: L10 vertical (U10) and L21 rows re-quoted; L21 flag now names p.89 |
| G42 | fixed: U5 left fill 18 x 5/8 x 54 1/2 (scaled, low confidence) |
| G43 | fixed: U7 fills one-sided (left) |
| G48 / G49 | fixed: U14 / U15 outline right edges re-traced; consequence: 8 new "gusset covers its rivets" DIFF rows (N / S x U14 / U14' / U15 / U15') - the chord-splice rivets drawn beyond the corrected edge belong to the cover splice plates (G45), reported |
| G50 / G51 | fixed: L15 zone note + 11 edge-angle strip zones (U8, U9, L9 x2, L10 x2, L12 x2, L18, U19, U20) |
| G53 / G54 | open (UNRESOLVED): U21 / L21 share the U20 / L20 outlines; not confirmed at 300 dpi |
| G44, G45, G46, G47 | open: splice / cover splice plate lengths not dimensioned (G45 scaling pass not attempted: the rivet-span + 2 edge rule is not reliable without the plan views read) |
| G41 | open: W14 gusset thickness (user decision) |
