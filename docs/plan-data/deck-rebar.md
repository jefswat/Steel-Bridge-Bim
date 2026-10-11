# Deck reinforcing from the plans (I-80 WB Design 116, Ramp G Design 316)

Branch `feature/deckrebar`. Input: `DeckReinforcementDefinition.Plan` (`DeckPlanReinforcement`, optional, null = the generic generator, other models unchanged).
Scripts (run LAST, after the build / joints / deck-edge / girder scripts - they use the joint blockouts, the deck edges and the girder lines):
`samples\plans\deck-rebar-i80.csx`, `samples\plans\deck-rebar-ramp-g.csx`; reports `report-deck-rebar.csx`, `check-deck-rebar.csx`; data `docs\plan-data\deck-bar-lists.csv`.

## 1. What the plans give (all read on 150-600 dpi clips; fractions are custom glyphs)

Superstructure notes (I-80 PDF 94, Ramp G PDF 85): top transverse steel 2 1/2" clear below the top of slab, bottom transverse steel 1" clear above the bottom,
longitudinal bars between them (transverse = outer layer of each mat), 2" clear to edges unless noted. Slab 8" (Ramp G 9" at the rail).

| | I-80 WB (Design 116, slab sheets 177-188 = PDF 184-195, list sheet 189 = PDF 196) | Ramp G (Design 316, sheets 135-138 = PDF 137-140, list sheet 139 = PDF 141) |
|---|---|---|
| Transverse | 7a #7 @ 7", one grid along the SOUTH edge of each unit (6 1/2" in the wedge of Unit 3's skewed begin), the bottom grid offset 3 1/2"; each line = 2-3 lapped pieces (3'-3 min. lap), fixed stock pieces 56'-4 / 50'-7 / 33'-1 / 38'-9 / 27'-4 ... + a "VARIES" remainder; runs "126 SPA. @ 7 = 73'-6, 127-7a1, 7a4 & 7a6 BARS" | 7a1 #7 @ 10" top and bottom, ONE bar across (34'-10), measured along the LEFT edge; Unit 4 has 3 runs (the last two measured along the RIGHT edge, "10" between runs) |
| At the rail | - (rail bars are on the rail sheets 217-225, not modeled) | 5j1 #5 x 6'-10 top, between the 7a bars, at both edges (2,080 / 1,800 / 2,600 / 2,376) |
| Longitudinal | 6b #6, alternating top / bottom: constant bays 24 equal spaces (25 bars) 8 1/2" inside the girder lines; narrowing bays north of the continuous girders have "N EQ. SPA." per bay (arrows on sheets 182/183/188), 9" inside the girders (Detail B); overhangs per Detail A; stock 39'-0 / 39'-10 / 39'-1 / 39'-9, min. lap 3'-0 | 6b1 #6: per bay 11 spa. @ 9" top + 10 spa. @ 9" bottom (23 bars, 8 1/2" / 1'-1 from the girders); stock 39'-2 / 38'-9 / 39'-11 |
| Joint area | 5d hoops/ties + 5e longitudinals (end dam diaphragms, sections B-B / C-C, "place 10 bars between girders...") | same (sections A-A, B-B, C-C, E-E) |
| Bar lists | PDF 196 (3 units) | PDF 141 (4 units) |

All seven bar lists are in `deck-bar-lists.csv` and add up to the printed unit totals exactly (849,698 / 1,204,071 / 903,702 and 284,978 / 248,221 / 357,015 / 327,696 lb).

## 2. How it is modeled

* `DeckTransverseZone`: lines normal to the alignment (the plans' radial layout - verified: the lengths of the skewed-end bars follow from the end-line skew alone), positions along an
  edge from the begin deck-end line, chained zones (`StartAfterPrevious`, `AnchorToEnd`, `BetweenZone` for 5j1), pieces (fixed length from a side, last = remainder + lap).
  Edge paths are extended beyond the deck ends (Unit 3's first lines lie before the south corner of the skewed begin line: 55'-10 3/4).
* `DeckLongZone`: equal spaces between two reference lines (girder web line / edge / offset) over a station range, alternating layers, lap-spliced stock chains; zones of one bay
  that abut form a RUN: a line continues as the nearest same-layer line of the next zone (so bar counts follow line length, not zone count). Overhang lines at explicit positions
  with explicit layers.
* `PlanBarList` + `Families`: every placed mark is compared with the bar list (count exactly, weight 3%) - differences are warnings; rows without a placed bar (5a drains, end dam
  5d/5e/4d) stay in the schedule at the printed count/length ("schedule only", no 3D bar, no bend diagram) and in the bar sheet / database group totals.
  The plans' 6b start/closure pieces (6b2-6b8) are compared as the family "6b" (`Families`).
* `DeckRebarCheck` (run in `RegenerationCache`, also for generic decks): top cover (grade to the top of the bar), bottom cover (bar underside to the soffit), edge distance, distance from
  the deck-end lines (blockouts), bars through the highest steel surface (top flanges, splice plates, fillers; stud shafts are not tested).
* Typical-section sheet text, bar sheet, IFC (IfcReinforcingBar), companion database (group "deck reinforcing" per unit with the unit bar schedule in its properties) work with it.

## 3. Calibrations (not printed on the plans - raised, not hidden)

1. **Where slab bars stop at a joint.** Transverse bar ends are held back from the deck-end line (`TransverseBeginClear/EndClear`): calibrated on the printed lengths of the
   skewed-end bars (first/last lines of the runs): Unit 1 begin 7a4/7a7 (1'-10 / 59'-10, 2'-2 / 59'-7) -> 17"; Unit 2 end 7a2/7a13 -> 27"; Unit 3 begin 7a4/7a15 -> 26.5-27";
   Unit 3 end 7a3/7a10/7a13 -> 17.5". That is the joint blockout D (abutments 18", piers 25") -1" / +2". Longitudinal bar ends (`LongitudinalBegin/EndCover`) are set to D: this reproduces
   the Ramp G 6b1 counts of Units 1, 2, 4 exactly. The check reports bars ending up to 1" inside a blockout (Unit 1 begin, Unit 3 end).
2. **Overhang bars.** Detail A (I-80 sheet 97) read from the vector section: 8 dots between the edge and the girder CL (5.1, 7.1, 10.1, 13.1, 15.1, 19.1, 23.4, 27.6" from the edge; top/bottom
   T B T B T T B T). Ramp G: the typical section shows only part of the overhang bars; **8 lines per overhang INFERRED** (4 1/2" apart, first 2" from the edge, alternating): the count is
   proven by the bar list (6b1 / pieces per line = 85 lines in all four units = 69 bay lines + 16), the positions are not.
3. **Zone boundaries of the narrowing north bays** (I-80 Units 2, 3) are not printed: the arrows give the count at a station; a zone runs between the midpoints to the neighbouring arrows
   (arrow x position -> station with the drawing scale from two pier lines, +-15 ft).

## 4. Result: model vs plan bar list (`report-deck-rebar.csx`, 2026-10-10)

Before = the generic layout (#5 @ 6" transverse, #4/#5 @ 9" longitudinal, all marks T/B/L/M/N), all marks; after = plan-driven, slab bars (7a, 6b, 5j, 5a) and all rows incl. end dam.

| Unit | before (lb) | plan list, all rows | after, all rows | slab bars (7a/6b/5j/5a) model vs plan |
|---|---|---|---|---|
| I-80 U1 | 557,435 | 849,698 | 849,420 | 843,051 vs 843,329 (-0.03%) |
| I-80 U2 | 735,330 | 1,204,071 | 1,205,156 | 1,196,216 vs 1,195,130 (+0.1%) |
| I-80 U3 | 561,828 | 903,702 | 908,398 | 899,191 vs 894,494 (+0.5%) |
| Ramp G U1 | 216,426 | 284,978 | 284,979 | 283,088 vs 283,089 |
| Ramp G U2 | 185,420 | 248,221 | 248,220 | 245,013 vs 245,013 |
| Ramp G U3 | 268,256 | 357,015 | 355,955 | 352,749 vs 353,807 (-0.3%) |
| Ramp G U4 | 246,317 | 327,696 | 328,068 | 324,582 vs 324,210 (+0.1%) |

Per mark (full tables: run the report): every fixed mark (7a1, 7a5, 7a9, 7a11, 7a12, 7a15, 7a16, 7a17, 7a21, 6b1 ...) matches in count and weight exactly; the "VARIES" marks of the main
runs are within 0.1-2.6%. Largest differences, all explained:

* I-80 U2 7a3 / 7a5 / 7a16 (2 + 4 + 4 lines at the begin, +170 / +115 / +154 lb): the slab end at Pier 6A is KINKED (sheet 177 Detail A: the north part of the end follows the 6A line); the deck outline has
  one straight begin line (known, HANDOFF) so the model's first lines are full width (114'-9) where the plan's are 77'-9 / 68'-8.
* Ramp G U4 7a2 / 7a3 (+19.8% / +19.4%, ~+370 lb): the kinked end at Pier 19G (sheet 139 Detail A, "STA. 67503+14.94 28.35' RT") is not in the deck outline; the plan's last bars are 33'-3 .. 3'-1 / 33'-10 .. 2'-6,
  the model's 34'-10 .. 6'-10. The model's right edge is also 2 5/8" shorter than the plan's 1002'-2 1/4 (left 3 1/4"). Check also warns 52 bars of that end inside the blockout.
* I-80 U3 6b2 (north bays, +10.3%, 1,145 vs 1,038 bars, +6,389 lb = 0.7% of the unit): the 6b1 (south) mark matches exactly (3,950); the north count is not reproduced. Candidates: arrow/zone boundaries (+-15 ft),
  the north overhang lines, the lines in the narrowing bays near the girder ends (in Unit 2 the plan's longitudinal layouts show rows C and B continuing roughly 50-60 ft past the model's termination stations of those girders, 7598+41 / 7600+38; Unit 3's B and C end at 7605+21 / 7607+19 in the model).
* I-80 wedge marks 7a2 (U3 -3.6%), 7a12 (-3.5%), 7a8/7a20 (-2.6/-2.2%), 7a6/7a9 U2 (-2.2/-1.8%): lap length / line position rounding in the "VARIES" pieces (the plan's laps are >= 3'-3).
* Ramp G U3 6b1: 2,532 vs 2,550 bars (-0.7%): 18 lines one stock bar short; the line length is at the threshold (the plan evidently places them slightly longer).

## 5. Not modeled / unresolved (nothing invented)

* **End dam diaphragm bars 5d / 5e / 4d** (I-80 sections B-B / C-C, Ramp G A-A, B-B, C-C, E-E): in the schedule at the printed count/length (6,369 / 8,941 / 9,208 and 1,889 / 3,208 / 3,208 / 3,486 lb), NOT placed in 3D. The sections give
  the bar positions (dots) and the notes the per-girder counts ("place 10 bars between girders A-D and F-K, 8 between D-F, 2 outside..."), but the per-end bar counts of 5d / 5e do not follow from them without
  decoding every section; coordinate with the deck end block solids (`EndBlock`) when done. 5a drain bars (5a23 / 5a4 "slab at drains", 5a24 / 5a5 insert anchors): schedule only (drain positions: sheets 205-220).
* **Barrier dowels / rail bars**: on the rail sheets (I-80 217-225, Ramp G 160-165), not on the slab sheets: not modeled. Ramp G 5j1 (at the rail, on the slab sheets) is modeled.
* **Kinked slab ends** at I-80 Pier 6A and Ramp G Pier 19G (see section 4).
* **Longitudinal bar start/closure pieces** 6b2-6b8 (I-80 U1/U2, Detail B sheets 178/184: 15-30 ft pieces per line at the skewed begin that stagger the laps): the model chains stock bars from the begin, closure bars at the end; compared as family 6b (-0.1% / +0.6%).
* Where slab longitudinals stop at an expansion joint is not dimensioned (calibration above); stud shafts are not clash-tested against the bars.

## 6. "Bar M1 near FS4-G1-P9" (HANDOFF)

M1 = the generic bottom longitudinal 60-ft stock bar (1,738 bars in I-80 Unit 1). FS4-G1 = Unit 1 field splice 4 on girder G1 (7582+73.87, offset -22.5 ft). The containment check on the generic layout found no defect there: the 22 M1
bars within 3 m of the splice are 60'-0 bars 2 3/16" above the soffit (1 1/4" cover to the bottom transverse layer), inside the deck edges, with no steel above them (0 clashes with the top flange / splice plates). The defect the user saw could not be
reproduced; the plan-driven layout replaces these bars (6b1 stock 39'-0, positions per girder bay). If it recurs with the plan-driven bars, the report to look at is `check-deck-rebar.csx` (min covers, clashes) and the note of the bar's mark.
