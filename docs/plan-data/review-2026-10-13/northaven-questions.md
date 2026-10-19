# Northaven NHT tied arch (CSJ 0047-07-228) - plan-review answers, items 9a-9i

Read-only review, 2026-10-19. "p." = PDF page (sheet in brackets). Plan: `0047-07-228 NHT 100% Plans_09-04-2020_comments_Reduced.pdf`.
Model state = `samples/plans/arch-nht.csx` + `src/SBM.Core/Arch/*` + `ARCH-DESIGN.md` sections 7-10. Nothing was edited.
Angles below were measured on the vector linework of the sheet (1" = 1'-0" plan views = 6 pt/in); +-0.05 deg.

## Q1 (9c) Masonry plate under the arch bearing? Full stack and elevation CONFLICT

**Answer: YES, there is a 2" masonry plate. The bearing does NOT sit straight on the pedestal.** Under the masonry plate is a 1/8" bearing pad on the
"CONC. BRG. SURFACE" (pedestal top). Evidence: p.107 [158] Partially Fixed Bearing - Elevation and Section A-A (labels "2" MASONRY PL", "BEARING PAD",
"CONC. BRG. SURFACE", 1/8" dimension) and Anchor Bolt Detail (masonry plate / top of conc. brg. surface / bearing pad / top of cap).

Full stack, top to bottom, at CL bearing (p.107, Section A-A + Partially Fixed Elastomeric Bearing Table + Upper Sole Plate Thickness table):

| # | item | thickness | p.107 source |
|---|---|---|---|
| 0 | arch steel (knuckle splice plate underside, p.124-125 A-A) | - | p.124 / 125 |
| 1 | 3" beveled sole plate (UPPER), A / B / C = 3 1/8 / 3 / 2 7/8" (Pier 10 and Pier 11 identical) | 3" at CL (B) | A-A, bevel table |
| 2 | elastomeric bearing 15 x 15, 60 duro: 2 cover layers 1/4" + 6 internal layers 3/8" + 7 internal steel plates 0.125" (A1011 Gr 40) | 3 5/8" (derived 0.5 + 2.25 + 0.875) | table (N cover 2, N int 6, int steel 0.125) |
| 3 | 3" sole plate (LOWER) | 3" | A-A |
| 4 | 2" masonry plate (3'-2 x 1'-5 in plan) | 2" | A-A, plan |
| 5 | bearing pad | 1/8" | A-A, 1/8" dim |
| 6 | concrete bearing surface (pedestal top) | - | A-A |

H (concrete surface to top of upper sole at CL brg) = 11.75" in the table = 1/8 + 2 + 3 + 3 5/8 + 3 (derived, OK, 1/16"). Anchor pin 3" dia A829 (AISI 4140) press fit full
depth into the lower sole plate; 4 anchor bolts 1 1/2" dia F1554 Gr 105, 1'-6" min embedment, 6" from the plate ends (p.107). Note 11: hole in masonry plate 1/8" over the bolt.
Note 7: upper sole plate welded to the arch rib, lower sole plate welded to the masonry plate, both at mean temperature (5/16" fillet symbols both sides, "SEE NOTE 7").
Sheet 1 of 2 of the bearing details is NOT in the set (p.107 says "SHEET 2 OF 2").

**Elevation CONFLICT (known, confirmed).** p.102 [153] pedestal tops are LEVEL and tabulated: Pier 10 EL A / E 534.779 / 534.726, Pier 11 EL A / E 531.546 / 531.494.
Steel WP (p.119) 535.74 (P10) / 532.49 (P11). WP - end flange/plate under it (~2", p.124 A-A, not dimensioned) - 11.75" gives 0.13-0.20 ft (1 9/16" to 2 7/16") LOWER than the p.102 pedestals
(SUMMARY section 8, B2). **The plans themselves resolve it**: p.107 note 8 and p.102 note 1 both say the contractor adjusts the pedestal elevations to the actual fabricated bearing
(p.107 Anchor Bolt Detail leaves the cap-to-conc.-brg.-surface distance as "SEE NOTE 8", i.e. variable). So the p.102 EL A-E are provisional; the stack governs.
The pedestal is tall enough (EL A - EL C = 1.56 ft above the cap at the pier CL, p.102) to be lowered by 2 7/16".

**Model now:** `ArchBearingDefinition` (ArchGenerator.cs ~1428-1495) builds pad 1/8 + masonry plate 2 + lower sole 3 + elastomer + beveled upper sole + pin + anchor bolts, HUNG FROM THE STEEL
(`HangFromSteel`: upper sole top in the plane of the splice plate underside, WP - 2 1/8"; upper sole ends then come out 3 1/8 / 2 7/8 = table). Pedestal concrete stays at the p.102 EL, so the stack bottom is
1 3/4" (P10 NW) / 2 7/16" (P10 SW) / 2 3/8" (P11 NE) / 1 3/4" (P11 SE) BELOW the pedestal top; the generator prints "Arch CONFLICT bearing ..." warnings.
**Proposal (not applied):** keep the stack as is (it matches the plan table), but set the modeled pedestal top = bottom of the bearing pad (= "conc. brg. surface") and report the p.102 EL as
"superseded by p.107 note 8 / p.102 note 1; differs by 1 3/4-2 7/16" (CHECK rows). That removes the pad/masonry plate sinking into concrete in the model. Whether to lower the pedestal in the model or keep the p.102 value is the
user's decision (rule: no guesses); the plan tolerates both, since it delegates the final value to the contractor. UNRESOLVED: the true as-built pedestal EL (not in the set), sheet 1 of 2.

## Q2 (9h) Fin plates on top of the rib (e.g. FB26-FPB)?

**Answer: NO. The plans show no fin plate on the top (extrados) of the rib, and none on the top of the tie.**
- **Arch rib fin plates**: p.148 [198] "Cable fin plate at arch rib" + Section A-A: ONE plate per hanger end (64 = 32 x 2 ribs, table of 32 rows for the south rib, p.147 note 1: north = -X, -Y), placed BETWEEN THE TWO RIB FLANGES
  (rib W14x132 with flanges in the arch plane), on the cable (tie) side of the web, 1/2" min clear of the web, welded to the flange inner faces, weld length D (5/16" fillets both sides, p.148 symbol), 1.378" pin hole, R 2 1/4", 1/2" x 1/2" clip, "7" in perpendicular plane of arch rib".
  GR 50-F1 FCM (p.147 note 2, p.83 note 1).
- **Tie girder fin plates**: p.149 [199] + p.146 [196] Detail 1 / Section C-C: 7/8" bent plate (bend line), lower leg 8 x 7 bolted (4 bolts, 4" x 4" gages, p.149) to the tie web outside face at the floorbeam CL; no weld symbol.
- No fin plate on the floorbeams (p.145-146), none on the bracing.

**FB26-FPB is not a fin plate.** In the generator (`ArchGenerator.cs` line 952) `{asm}-FP{end}` = "2 - PL ... top flange connection plates (p.146)": the floorbeam-to-tie TOP FLANGE connection plates (p.146 Section B-B), `-WP` = web plates,
`-FB` = bottom flange plates, `-ST` = stiffener. "FB26-FPB" = floorbeam 26, flange plates, end B. Rib/tie fin plates carry the hanger-based names. The name is easy to misread; proposal: rename the part description to
"top flange CONNECTION plates (floorbeam-to-tie), not a cable fin plate" or the id suffix FCP. No geometry change.

**Model:** rib fin between the flange inner faces, top P4 below the working line, sides P3 (ArchRibFin), tie fin bent 7/8" plate - matches p.148/149 (ARCH-DESIGN section 7, 9, 10). OK. UNRESOLVED (already open): fin thickness 7/8" scaled (p.148 not dimensioned), slot width.

## Q3 (9b) Tie girder end cut at the knuckles (p.124-125)

**Answer: the tie flange and the tie web both end in a cut that is SKEWED IN PLAN, parallel to the knuckle end web, and VERTICAL (square) in elevation, with a 1/2" joint gap to the knuckle.**
- Plan, p.124 [175] Partial Plan at Knuckle Joint T34' and p.125 [176] at T0: the tie flange outline ends on a straight line at 117.47 deg; the 1" end web, the 2" x 32" end flange edge and the dashed splice outline edges are at 117.45-117.49 deg;
  the tie CL runs at 7.42 deg. So the cut makes 110.05 deg with the tie (= 20.05 deg off square, the pier skew 19 deg 34'41" p.102 + 0.5 deg chord). The gap (dimension 1/2") is measured between this line and the knuckle-side edge.
- Flange corners: no clip shown on the tie flange (square corners to the skew line, 5 1/2" / 3" bolt gages, "3 SPA @ 3", 1 1/2" edge). The acute corner is cut only on the dashed splice/fill plate (see Q5).
- Web, p.124 Section A-A [175]: vertical end at the joint with a 1/2" gap to the knuckle web stub (dim 1/2" at the top), connected by 2 - 1/2" gusset plates (bolts 2 SPA @ 3" x 3 rows, 2 1/2" edge, R 2" / 3/4" corner clips on the gusset, p.124-125 A-A) on both faces.
- Under the flange: 1 1/8" fill + 1" splice plate (A-A), bolted; flange bolts "3 SPA @ 3".
No other end preparation (no bevel/weld prep) is shown for the tie.

**Model:** the tie field pieces start/end at `KnuckleJoint` = where the tie CL crosses the end flange inner edge + JointGap/2 (ArchGeneratorKnuckle.cs 26-30, ArchGenerator.cs 252-254), i.e. a SQUARE cut across the tie.
**Proposed fix (not applied):** cut the tie flange and web with a vertical plane through the joint line parallel to the end web (20.05 deg off the tie normal in plan, gap 1/2" measured perpendicular to that line), keep the joint station on the CL as is.
Bolt groups of the 1/2" gussets are then aligned to the skew as drawn (the plan holes lie on lines parallel to the skewed edge). UNRESOLVED: whether "1/2"" is measured perpendicular to the skew line or along the tie (not stated); the corner treatment at the web ends.

## Q4 (9f) Knuckle plates in contact and welded to each other?

**Answer: YES for the welded steel plates - they are in contact (mill to bear) and joined with 5/16" fillet welds, both sides. No CJP, no field weld, no all-around, no tail on any symbol on p.124-126.**
General note p.83 (sheet 134) note 2: AWS D1.5, CJP only where shown - none shown here. Weld symbol sizes read at 260-450 dpi.

| joint (p.124 NW / p.125 SW, same details) | contact / weld shown | evidence |
|---|---|---|
| 1" arch flange plates (outside + inside) to 2" end flange | MILL TO BEAR (both plate ends) + fillet 5/16" both sides | p.124 Section B-B (symbol, arrowheads on both plate feet), Section A-A |
| arch flange plates to the stub web (milled web, 1'-11 5/8") | MILL TO BEAR; fillet 5/16" both sides (the upper A-A symbol has a leader with 5 arrowheads; the individual targets cannot be told apart reliably) | p.124 A-A, plan symbol at the end web |
| 1" arch stiffener (R 2" clips) to arch flange plates / stub web | fillet 5/16" both sides (same leader) | p.124 A-A |
| 1" stiffener (vertical, at the end web) to 2" end flange | fillet 5/16" both sides (lower A-A symbol, leader forks to the stiffener and the web/flange junction) | p.124 A-A |
| 1" end web to 2" end flange | fillet 5/16" both sides (lower A-A symbol; plan symbol 5/16 both sides at the end web) | p.124 A-A, plan |
| 1" splice plate / 1 1/8" fill to end flange and tie flange | bolted (bolts through all; "7 SPA @ 4 3/8", "3 SPA @ 3"), no weld | p.124-125 A-A, plan |
| jack cheese plates 1" under the splice plate | MILL TO BEAR (jacks) , bolted (bolts through splice plate) - no weld | p.124 A-A |
| 1/2" gusset plates to tie web and knuckle web | bolted, no weld | p.124-125 A-A |
| 1" deck (end) plate on end / tie / FB flanges | overlaps 3" (end flange) and 5"/6" (tie / FB2), studs; no weld symbol | p.124 plan, p.125 plan, p.126 |
| upper sole plate to arch rib / lower sole to masonry plate | fillet 5/16" both sides, note 7 | p.107 |

Where a weld size is "not shown": stub web to arch flange plates is covered by the 5-arrow leader (size shown, individual joints not resolvable); end plate to flange welds NOT shown (UNRESOLVED, p.126 has stud layout only).
**Model:** v4 builds the plates (bent 1" arch flange plates end web to RS joint, stub web, stiffeners, splice, gussets); welds are not generated for the knuckle (ARCH-DESIGN section 10: `ArchWeldRule` / `WeldFinder.TJoint` exist, no rules set; knuckle extents "not dimensioned"). I did not re-measure plate-to-plate gaps numerically.
**Proposal (not applied):** (1) add `ArchWeldRule` T-joint 5/16" both sides for: arch flange plates to end flange, arch flange plates to stub web (best-reading, flag extents UNRESOLVED), arch stiffener to flange plates + stub web, end stiffener to end flange, end web to end flange; (2) zero gap (mill to bear) at flange-plate/end-flange and stub-web/end-flange contacts, and check by a CHECK row; (3) keep bolted joints unwelded.

## Q5 (9e) Plate under the last floorbeam bay at the knuckle - orientation / skew

Two plates are in that bay; both are skewed like the end web:
1. **1" splice + fill plate (dashed, p.124 plan "1" SPLICE PL AND FILL PL", p.125 plan "1" SPLICE PL")**: its end edges are parallel to the end web / joint line, 117.45-117.49 deg vs tie 7.42 deg, i.e. 20.05 deg off square to the tie; width 1'-10 (+-11" about the chord through T34'/T0, p.124 plan); runs from the end web 11 3/4" past the joint (one edge 18.9" and 14.7" segments on the skew line); the acute corner is cut by a 45 deg chamfer (12.3" chamfer line, ~72.5 deg to the skew edge) - 13 1/4 x 9 13/16" legs per v4 reading. P.125 (SW) is drawn mirrored.
2. **1" deck (end) plate**: p.126 [177] "End plate and stud layout" (1/2" = 1'-0): FB1 line (the radial floorbeam, perpendicular to the tie: ties at 69.95 deg, FB line at 159.96 deg, 90.01 deg apart) closes the bay at the top; the end web / end flange edge (the "knuckle line", horizontal in the sheet) closes it at the bottom, so the bay is a trapezoid whose end edge is skewed 20.05 deg from the radial FB1 line; sides follow the tie flanges with 3" / 6" overlap on the end/tie flanges and 5" on the floorbeam (p.124 / 125 plan; the floorbeam there is labelled "CL FB2" on p.125 but "CL FLOORBEAM FB1" on p.126 - plan labelling CONFLICT, geometry the same). Studs 7/8 x 7 in rows 10 5/8" apart (p.126), 7/8 x 8 near the end.
**Model:** deck plate polygon T34', T33', T1, T0 (`DeckPlate`, arch-nht.csx 381) = this trapezoid; splice outline per v4 (SpliceHalfWidth 11", SpliceBeyondJoint 11.75", chamfer 13.25 x 9.8125, ArchKnuckleDetail). Consistent with the plan within the scaled precision; no change proposed. UNRESOLVED: splice/fill plate outline is dashed and only the bolt pitch is dimensioned (p.124-125; U flagged in arch-nht.csx), end plate studs columns (p.126).

## Q6 (9a/1.1) Pedestrian fence / fabric type

**UNRESOLVED - the plans do not give the fence/fabric type.**
- p.89 [140] typical approach sections: low railing post 4'-3" and high railing post 9'-10" (curved inward at the top), leader "FOR RAILING AND FENCING DETAILS, SEE 'PEDESTRIAN RAILING DETAILS' SHEETS". p.84 [note 2]: post spacing "diagrammatic only". p.90 (arch section) same reference. p.85-88 draw only post symbols / RAIL (PED) stations.
- p.3 [index sheet 2 of 2]: bridge sheets 224-226 "PEDESTRIAN RAILING DETAILS RAIL (PED)" - NOT in the 155-page set (set ends at sheet 205 = p.155). Roadway sheets 314-316 (ornamental steel fence layout / details, chain link fence layout) and 317 CLF-10 are also not in the set and are roadway fences, not tied to the bridge by any note.
- p.83 [134] note 8 and structural steel note 1: "pedestrian railing posts and connection hardware shall be stainless steel AISI 316"; design dead load HIGH rail 100 PLF, LOW 50 PLF. p.91 [142]: pay items 5110 6001 / 6002 RAIL (PED) LOW / HIGH (579.0 / 1,144.2 LF); no fabric/mesh item, no wire size.
- TxDOT standards cross-check (SUMMARY 6b): not RL-PR3/PR11/PR22/CLF-RO.
Facts in the plans: height, post count per sheet 84-88 is diagrammatic, stainless material. NOT in the plans: fence type, mesh opening, wire diameter, rails, base plates, anchors, post spacing.
**Model:** envelopes of the p.89 posts + handrail (`rail-joints-nht.csx`), no fence on the arch (railing GAP). ARCH-DESIGN v5 "2 fence solid: NOT DONE" - the idea of a see-through fabric panel between posts has no plan basis; **proposal: do not model a fabric type/mesh; if shown, draw only a flagged see-through panel (UNRESOLVED, p.89/84/3) or leave the high rail as posts + handrail until the user supplies the missing sheets 224-226.**

## Q7 (9i) Bracing gusset edge against the connection plate (p.129-140)

**Answer: the gusset edge butts against the 7/8" connection plate face (contact, no gap dimensioned), and the joint is welded with 5/16" fillet welds on both sides, typical.**
- Sheet pattern (p.129/130 [180] duplicates, p.131 [181] U2 / U2' shown as example, p.132-140 same): Views D-D / E-E (top gusset) and I-I / J-J (bottom gusset) show the gusset end touching the connection plate; the symbol is "5/16 | 5/16" fillet both sides, tail "TYP."; arrow on the gusset-to-plate corner. Angles between plate and gusset plane are printed (e.g. 111 deg 13'43" and 108 deg 52'13" on p.131).
- View B-B (rib stiffeners): 5/16 both sides "TYP." to the rib flanges; 1 1/2" clips; View A-A: connection plate bolted to the rib flange (4-10 holes).
- No CJP, no field-weld flag, no weld length or intermittent spacing; no weld between gusset and brace (bolted through the brace flange); the bend line is a fold line of one plate (not a joint). The gusset corners: R 2" and R 9 3/4" - 2'-0 cut-outs (views G-G / L-L, "in plane of plate").
**Model:** one bent 7/8" plate per face, welded edge placed on the connection plate face with the edge checks (<= 1/8" off, 1 1/4-1 11/16" to the connection plate edge, D-D 1"; ARCH-DESIGN section 8/9). Welds not generated (section 10: bracing stiffener / gusset weld rules are a next step). **Proposal (not applied):** `ArchWeldRule` 5/16" fillet both sides along the gusset welded edge on the connection plate face (44 gussets x 2 sides) and for the stiffeners to rib (B-B); length = the edge/contact length (not dimensioned: report as derived).
UNRESOLVED: weld length/extents not printed; sheet titles U(n) vs table U(12-n) already reported (U14).

## Summary of changes proposed (none applied)
1. Q1: pedestal top = pad bottom in the model (p.107 note 8 / p.102 note 1), p.102 EL as CHECK rows.
2. Q2: no fin plates on rib top; rename FB26-FPB description (it is the floorbeam top flange connection plate).
3. Q3: tie flange + web cut skewed 20.05 deg (parallel to end web), vertical, 1/2" gap.
4. Q4: add 5/16" both-sides fillet weld rules + mill-to-bear contact in the knuckle.
5. Q5: model consistent; flag FB1 vs FB2 label difference (p.125 vs p.126).
6. Q6: UNRESOLVED, sheets 224-226 missing; do not invent a fabric.
7. Q7: add 5/16" both-sides fillets gusset/stiffener edges.
