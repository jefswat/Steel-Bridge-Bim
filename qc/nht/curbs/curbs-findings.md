# Agent review - NHT approach curbs (Units 1-5)

Model: `samples/plans/Northaven NHT.sbm` (curbs set by `samples/plans/rail-joints-nht.csx` section 1, `BarrierType.Custom` + `IsCurb`; curb bars C / CS by
`samples/plans/rebar-nht-approach.csx`). Plan: `C:\SBMData\0047-07-228 NHT 100% Plans_09-04-2020_comments_Reduced.pdf` (raster; p. = PDF page).

How: the model was regenerated in memory with the debug CLI (`sbm run <script> --no-pipeline`, no save) and measured by the scripts in
`qc/nht/curbs/work/` (`measure.csx` section / ends / bars / railing base, `cover.csx` cover-violation locations + railing base jumps, `probe.csx` vertical
ray probes of the curb and deck meshes, `sagitta.csx` plan chord deviation of the curb loft). Output: `*.out` in the same folder. Plan sheets read on
PyMuPDF renders (`work/p*.png`): p.83 (general notes), p.89 (typical approach sections), p.109 / 111 / 113 / 115 / 117 (unit sheets: plan, typical
transverse section, bar table, BARS CS). IFC / database mapping and sheet links were read in the code / rules file (no IFC export and no database build
were run, to avoid writing next to the model).

## Summary

The curb section, curb line, height, top slope, ends and continuity match the plans. Problems found:

- **C01 (wrong):** the curb solid is a chorded loft with sections about every 5 ft. On the 60 ft radius curves of Units 1-2 the curb faces lie up to
  0.68" off the true curb line between sections. On Units 3-5 the offset is 0.13-0.18". The 1/16" rule is exceeded everywhere on curves. Most of the
  C / CS "cover violations" the model reports come from this.
- **C02 (wrong):** the first / last CS stirrup does not meet the plan's 2" end cover. It is about 1/4" short at the radial ends and 0.9" short at the
  skewed Pier 10 / 11 ends.
- **C03 (wrong, artifact):** the cover check also reports C bars on tangents where the probes show at least 2 1/2" clear.
- **C04 (wrong):** the sheet-link rule gives every curb all five unit sheets.
- **Unclear / plan conflicts:** CS top cover vs p.83 note 5 (2 1/2"); the re-entrant fillet and end chamfers of p.83 note 3; p.113's bar table lists
  "CS" twice and no "C"; the 4" bar dimension does not fit inside the 6" CS.

Counts: wrong 4, missing 1, unclear 4, plan conflict 2, OK 16.

## Findings

| ID | Location | What | Model vs plan | Sheet | Severity | Confidence | Proposed fix |
|---|---|---|---|---|---|---|---|
| C01 | All units, curves (worst Units 1-2, curves 103/104) | Curb solid is lofted through sections about every 5 ft and joined by straight chords, so the faces cut inside the arc between sections | Max plan deviation of the outer / inner face from the true line: U1 L +0.68" / R -0.53", U2 L +0.66" / R -0.52", U3 0.15", U4 0.14", U5 0.18" (ring spacing 4.8-5.5 ft). Plan: curb on the curved deck edge (R 67.833 / 52.167 ft edges p.109). Probe U1 sta 2144.93: curb top at 1" from the edge is 3.57" instead of 4.00" (outer chamfer moved inward); the deck edge is exact there (a 0.6" ledge) | p.109, 111, 113, 115, 117 | wrong | high | Generator: in `src/SBM.Core/Concrete/ConcreteBarrierCustom.cs` choose the section count from the edge curvature (sagitta <= 1/16", e.g. L <= sqrt(8 R x 1/16")), or record the exact sweep and tessellate from it, as the deck slab does (`AddAnalytic` / NURBS) |
| C02 | Every unit end, first / last CS each curb | End cover of the curb stirrups at the deck ends | CS centreline is 1.75" from the begin ends (hook tail laid toward the end: clear about 1.5") and 1.32" from the skewed Pier 10 / 11 ends (clear about 1.07"; a radial 6" stirrup crossing the 19.58 deg end). Reported excess 0.20-0.25" (radial ends), 0.90-0.93" (P10 / P11). Plan "2" END COV (TYP)" | p.113, 115 | wrong | high | Data `rebar-nht-approach.csx`: put the second hook tail on the inboard side (`along` toward the span), and start the end stirrups 2" + half width x tan(skew) from a skewed end (or turn them parallel to the end) |
| C03 | Units 1, 2, 4, 5 tangents and curves, C bars and CS top legs | Cover check reports violations ("bottom face" 0.5-0.64" for C) where the vertical probe shows the curb bottom and the deck top together, with 2.5" clear to the curb top and 2.5" to the inner face | e.g. U5 sta 2832.79, C at R 7.25" / z +1.25": curb 0.005..4.005", deck -8.495..0.005" (seam 0.005" between the chorded curb bottom and the analytic slab top). 154 / 174 / 37 / 26 / 74 C/CS rows per unit; on curves most come from C01 | p.113 | wrong (report artifact) | medium | Generator / check: `Concrete/RebarCover.cs` + `ConcreteGenerator.BuildPlanBarSets` - give the curb an analytic host on the same deck surface (or a 1 mm overlap tolerance at host seams). Re-check after C01 |
| C04 | `sheet-links-nht.json` rule `curbs` | The rule targets every element of category "curb" with all five unit sheets (no `unit` selector). Each curb then references four sheets that were not used to model it (user rule 2026-10-19) | Unit 1 curbs get p.111, 113, 115, 117 too | p.109-117 | wrong | high (rule text; db not built) | Data: split the rule - p.89 to all curbs, each unit sheet with `"unit": "Unit n"` as the `unit-n` rules do |
| C05 | `sheet-links-nht.json` rule `curbs` | p.83 (concrete note 3: the 3/4" chamfers modeled) is not linked | - | p.83 | missing | high | Data: add "BRIDGE GENERAL NOTES" to the curbs rule |
| C06 | Section, curb toe (inner face to deck top) | p.83 note 3: "re-entrant angles filleted 3/4" unless otherwise shown". The p.89 / p.113 sections draw a sharp toe. Model has no fillet | 0 vs 3/4" fillet (note) / sharp (drawn) | p.83, 89, 113 | unclear | medium | User decision. If the fillet is wanted: profile point `P(10, 0)` with a 3/4" fillet (Radius) in rail-joints-nht.csx |
| C07 | Curb ends at the joints / abutments | Note 3 chamfers apply to all exposed edges. The vertical end edges of the curb at the SEJ are square in the model. No detail in the set | not modeled | p.83 | unclear | low | User decision; the end edges are inside the SEJ seal upturn zone (TxDOT SEJ-B, not in the set) |
| C08 | CS / C vertical position | Script derives 2" clear top and bottom (2 + 8 1/2 + 2 = 12 1/2). p.83 note 5: deck top surface 2 1/2", bottom 1 1/4" "unless otherwise stated". p.113 states no curb cover; the CS drawn reaches about 0.8" from the slab bottom (scaled). 2 1/2" top + 1 1/2" bottom also fits the 8 1/2" CS | CS top leg 2.00" clear to the curb top (model) vs 2 1/2" (note 5, if the curb top counts as deck top); C 2.50" | p.83, 113 | unclear | medium | User decision. If note 5 governs: CS top at curb top - 2 1/2" (bottom then 1 1/2" >= 1 1/4") in rebar-nht-approach.csx |
| C09 | p.113 bar table | Lists "CS" twice and no "C" (p.109, 115, 117 list A, C, CS...; the p.113 section labels "C (CURB, TOP)") | Model places C on Unit 3 (12 pieces) | p.113 | plan conflict | high | None (typo; note it in the plan data) |
| C10 | Bars in the CS corners (C top, D bottom) | p.113 dimensions 4" between the two D bars in the CS. A 6" out-to-out #4 CS with #4 bars inside gives 4 1/2" centre to centre | Model C and D 4 1/2" apart (R 2.75 / 7.25") vs plan 4" | p.113 | plan conflict | medium | None (bars kept in the stirrup corners); record it in bar-lists / SUMMARY |
| C11 | Diagnostic "section 0.277 SF" | Curb diagnostic area is taken in profile space (H 4.1 at the inner face). The true section is 10 x 4 less two chamfers = 39.44 in2 = 0.2739 SF. Volumes come from the mesh and are right | 0.277 vs 0.274 SF (text only) | - | unclear (cosmetic) | high | Generator text: ConcreteBarrierCustom area from the mapped section (minor) |
| C12 | Section width | 10" curb | 10.000" (R 0..10) vs 10" | p.89, 113, 109 (0.833') | OK | high | - |
| C13 | Section height | 4" above the trail | 4.000" above the deck at the curb line and at the outer edge (top parallel to the 1 % slope) vs 4" (TYP) | p.89 | OK | high | - |
| C14 | Chamfers | 3/4" at both top corners | (9.25, 4.0) / (10, 3.25) and (0.75, 4.0) / (0, 3.25) vs p.89 drawn chamfers + note 3 | p.83, 89 | OK | high | - |
| C15 | Curb line / deck edge | Curb inner face 7'-0 from CL NHT, outer face flush with the slab edge 7'-10 (7.833') | edges -/+7.8333 ft, toe 10" inside (exact at the sections, see C01 between them) | p.89, 109-117 | OK | high | - |
| C16 | Top vs cross slope | Deck 1.00 % falling to the edges. Curb top parallel | slope under the curbs 1.000 % at all samples, all units | p.89, 113 | OK (plan does not say parallel or level; the sections draw it parallel) | medium | - |
| C17 | Begin / end at the unit ends | Curb ends on the slab end lines (FromBegin / FromEnd 0.000" for toe and outer face, all 10 curbs); slab ends at the SEJ armor face (-2 1/2" at abutments, -1 1/4" at P4 / P7 / P13, -10 1/2" = 9 1/4 + 1 1/4 at P10 / P11) | Unit 1 lengths: plan slab edges to FFBW / CL SEJ L 116.386 / R 99.370 ft -> toe 115.48 / 100.27; model toe 115.17 / 99.96 = 0.31 ft shorter = the 2 1/2" + 1 1/4" joint cut-backs (expected 0.325 ft) | p.109, 113, 115 | OK (joint cut-back from the SEJ standard; see the joints review) | high | - |
| C18 | Arch interface P10 / P11 | Unit 3 curbs end on the skewed slab end 9 1/4" + 1 1/4" before CL Pier 10 (outer L 24+14.29 / R 24+19.82); Unit 4 begins likewise after Pier 11 (26+17.70 / 26+23.23). Arch curbs (same 10 x 4 at 7'-0) are the arch model's | skew 19.578 deg followed | p.113 note 7, p.115, p.90 | OK | high | - |
| C19 | Continuity | One closed piece per curb per unit, continuous over the interior piers 2, 3, 5, 6, 8, 9, 12, 14, 15; breaks only at the SEJ joints (P4, P7, P10/P11, P13) | pieces = 1 | p.84-88, 109-117 | OK | high | - |
| C20 | IFC class | `IsCurb` -> IfcKerb .USERDEFINED., ObjectType = "Curb 10" x 4" (p.89, p.113)" (`IfcExporter.cs` 817). Minor: the curb's pset description says "Concrete barrier" | - | - | OK (label minor) | high (code) | Optional: description "Concrete curb" when IsCurb |
| C21 | Database | element category "curb", class IFCKERB, under the group "<unit> barriers" (category "barrier group") | - | - | OK | high (code) | - |
| C22 | Volume / quantity | 13.325 CY total (U1 2.18, U2 1.96, U3 3.30, U4 2.48, U5 3.40). The plans have no curb pay item (curbs are in 0422 6002 slab SF, p.91); 39.44 in2 x 1,314.6 ft toe length = 13.34 CY | no plan value | p.91 | OK (not applicable) | high | - |
| C23 | Curb bars CS | #4, 6" x 8 1/2" closed with 4 1/2" tails, 9" max spacing, 2" clear to the slab edge and the curb inner face | spacing 8.97-9.00"; legs at R 2.25 / 7.75"; 289 / 260 / 436 / 328 / 449 per unit | p.113 (BARS CS) | OK (vertical cover see C08, ends see C02) | high | - |
| C24 | Curb bars C | #4, 2 per curb in the CS top corners, laps 1'-7 | 2.50" clear below the curb top; 2" from the deck ends | p.109-117 | OK | high | - |
| C25 | Bar ownership | `Barrier.Reinforcement = false`; C / CS are deck plan bars (bar table on the unit sheets) | - | p.113 | OK | high | - |
| C26 | Railing base on the curb | Base probed at mid-curb: 3.89-4.01" above the deck at the curb line (curb top 4" less 0.05" of slope); 12 runs, 0 base jumps > 1/4" | - | p.89 | OK | high | - |
| C27 | Begin of rail at Abut 1 | p.89 gives RAIL (PED)(LOW) from STA 20+49.30 (FFBW); rail and curb start at 20+49.508 (slab end at the armor face, C17) | 2 1/2" | p.89 | OK (railing review) | medium | - |

Counts: wrong 4 (C01-C04), missing 1 (C05), unclear 4 (C06-C08, C11), plan conflict 2 (C09, C10), OK 16 (C12-C27).
