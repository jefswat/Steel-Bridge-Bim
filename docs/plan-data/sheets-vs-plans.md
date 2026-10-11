# Generated sheets / drawings vs the real plan sets (2026-10-10)

Read-only comparison of `sbm sheets` (PDF) and `sbm drawings` (DXF) output against Iowa DOT Design 116 (WB I-80) and
Design 316 (Ramp G). Nothing in `src`, `samples` or the model was edited; a COPY of the model was used.

## Method

- Model copy: `temp\sheet-compare\model.sbm` (copy of `samples\plans\I-80 WB and Ramp G.sbm`); CLI `src\SBM.Cli\bin\Debug\net10.0\sbm.exe`
  (built 14:03; dist copy is 13:46).
- `sbm sheets model.sbm out.pdf "<bridge>"` and `sbm drawings model.sbm <folder> "<bridge>"` per bridge. Output in `temp\sheet-compare\`:
  `i80u1.pdf` (31 sheets), `I-80_WB_Unit_2.pdf` (45), `I-80_WB_Unit_3.pdf` (32), `Ramp_G_Unit_2/3/4.pdf` (27/32/30), and `dxf_*` folders.
  Generation time: 1-7 s per PDF, 8-20 s per `drawings` run. DXFs are R12 ASCII with layers `S-STEEL, S-CONC, S-REBAR, S-DIMS, S-TEXT, ...`; they carry the same
  linework as the sheets, so findings below apply to both.
- **Ramp G Unit 1 FAILS** (both `sheets` and `drawings`), see D1.
- Plan pages used (PDF page): I-80 typical sections 94-97, steel layout U2 108, girder elevation U2 112, cross frames 122, field splices 126,
  disc bearings 158-162, Pier 6WB 43-46, Pier 6A 39, West Abutment 85-86, bar list slab 196, longitudinal section 101;
  Ramp G typical section/notes 85, girder elevation U1 93, Pier 6G 34, abutment, barrier rail 162. Fractions were confirmed on 200+ dpi grayscale clips.
- Convention to keep in mind: the generated sheets label spans between CL BEARING lines along the CL alignment; the plans print CL PIER to CL PIER (e.g. I-80
  U2 span 1: model 208'-6, plan 210'-0; Ramp G U2: 173'-6 vs 175'-0; U3 span 1 158'-11 1/2 vs 160'-7 5/8). The differences are exactly the shared-pier CL BRG
  offsets (1'-6 / 1'-8, /cos(skew) at skewed lines), so they are NOT errors, but a plan reader cannot compare them without the CL pier span as well.
- Many model quantities were checked in earlier sessions (see HANDOFF, `i80-elevation-investigation.md`); items below marked "known" are already on the HANDOFF list.

## Summary of what matches (no action)

Skews on S-1: I-80 U2 8WB/9WB/10WB/11WB = 42°41'27", 38°38'00", 33°43'51", 27°27'55" and U3 27°27'55" ... 5°26'43"; U1 44°/45°30' (all appear in the plan text).
U2 deck: 11'-6 girder spacing x5, 3'-1 overhang, 8" slab, 3 1/2" haunch (plan PDF 95). Ramp G U2: 3 spaces at 9'-8, 3'-1 overhang, 35'-2 out-to-out (PDF 85).
Girder A span lengths along the girder U2 (205'-8, 260'-3 15/16), plate sizes (web 11/16 x 102, flange 7/8 x 18 ... 2 x 30), cross frame Type 3 sections
(L6x6x5/8 chord, L6x6x3/4 diagonals, 2-MC10x25, 5/8 gusset, 6" and 10" work-point offsets). Pier 6WB: 4 columns 8'-0 x 4'-6, 27'-4 spacing, column
offsets 13'-7 5/8 / 13'-9 3/8, cap top 1013.07 / 1014.92, footings 18' x 20', 21 HP14x73, footing bottom 961.28. Pier 6G: cap 37'-11 5/16, 1036.62 / 1034.55 / 1026.24,
footing bottom 970.57. Pier 6A: 24 HP14x73, cap 1011.50 / 1002.94. Disc bearing anchors 4 - 1 1/2" dia, 18" embed, X = 22" / Y = 9" (masonry plate 50 x 24).
West abutment seat elevations 997.15 / .51 / .87 / .78 / .62 / .45. Bolts 7/8" in 15/16" holes (plan PDF 94 note, same as the notes on S-9).

## 1. Layout sheet (S-1: plan developed along CL + elevation) <-> plan layout/situation, steel layout PDF 108, longitudinal section PDF 101

| # | Finding | Cat. | Sev. | Status 2026-10-10 |
|---|---|---|---|---|
| 1.1 | Text collides: span labels overprinted by "CL BRG PIER" labels (SPAN 3 / SPAN 4 / SPAN 5 unreadable on I-80 U2); the field-splice station labels in the elevation overlap into a solid black strip (`FIELD SPLICE STA ...` x 8 on one line, same on Ramp G); pile tip labels run into the right margin. | drawing | H | FIXED: LabelPlacer + staggered splice labels (verified on I-80 U2, Ramp G U3) |
| 1.2 | The plan strip draws only girder lines + deck edge; no cross frames, stiffeners, splices, skew angles at piers, girder offsets at the bearing line, cross frame spacing. The plan set has a structural steel LAYOUT for each unit (U2: PDF 108-111, Ramp G: PDF 90-101) = missing view (see section 9). | missing | H | FIXED: steel framing plan sheets (girders, CF marks, splices, bearings); the layout strip itself is unchanged |
| 1.3 | Station at the pier labels is the CL BRG at the alignment (e.g. 8WB 7598+10.48); the plans give CL pier stations/offsets (e.g. 8WB "7598+11.13, 41.08' LT"). Different reference, not comparable; print both. | drawing | L | open |
| 1.4 | Bridge length label "1216'-8 1/8" (end of deck to end of deck)" vs plan "1216'-5 5/8" (Pier 5WB to 11WB)". Different definitions; label the plan one too. | drawing | L | open |
| 1.5 | No bearing and expansion device setting table (plan PDF 101 "Longitudinal section" = joint/bearing setting at 10/50/90 F, joint openings). | missing | M | open (no joint/bearing setting table; not modeled) |

## 2. Typical section (S-2) <-> plan typical sections (I-80 PDF 94-97, Ramp G PDF 85)

| # | Finding | Cat. | Sev. | Status 2026-10-10 |
|---|---|---|---|---|
| 2.1 | **Deck reinforcing differs from the plan.** Sheet says "#5 @ 6" top transv., #4 @ 9" longit. (2 1/2" clr); #5 @ 6" bott. transv., #5 @ 9" longit. (1 1/4" clr)". Plan: 7a (#7) transverse at 10" (Ramp G) / 6b (#6) longitudinal, 25 bars across 11'-6 (I-80) or @ 9" (Ramp G); cover 2 1/2" top, **1" clear above bottom** (PDF 94 note). Model uses the TxDOT SSTR default. Slab bar list totals: model U2 735,869 lb vs plan U2 1,204,071 lb (PDF 196; U1 849,698, U3 903,702). Known (HANDOFF 3.5). | model | H | open (model) |
| 2.2 | One station per unit (e.g. U2 at 7592+12.14, girders G1-G11). The plan shows two sections for U2 (start of unit to 7594+03 with 11 girders, 7594+03 to 7596+41 with girder E dropped) plus Part Typical Section. A section at a second station (after a girder terminates) is needed; Ramp G shows "half section near cross frame / near abutment". | missing | M | PARTLY: a second typical section at the span with the fewest girders |
| 2.3 | The plan dims lane/shoulder tangent widths (12'-0 tangent on 2.0% / variable slope), roadway width, 34'-0 / 7'-1 offsets from P.G.L., slab area, haunch detail, overhang bracket detail, barrier orientation detail. Sheet has only girder spacing, out-to-out, cross slopes. | missing | M | open |
| 2.4 | The barrier drawn is the TxDOT SSTR "F-shape" profile (CLAUDE.md); the plans use the Iowa rail (sheets 217-225 / Ramp G 160-163, separate rail bar list: 6d1 624 x 38'-8, ...). Shape, rail rebar and quantities not as designed. | model | M | open (model) |
| 2.5 | Sheet scale 1/16" for the 113 ft wide I-80 deck leaves 60% of the sheet empty (1/8" would fit on 11x17). Labels overlap: "INVERTED K-FRAME CF50 (BEYOND)..." runs through the dimension string, "WEB 102 x 11/16" and "3 1/2" HAUNCH..." leaders cross the section; Ramp G: cross slope note "5.9%" overprinted by the frame leader. | drawing | M | PARTLY: overlaps resolved; scale still limited by the paper-sized notes |
| 2.6 | Girders are named G1..G11 (north to south); plan uses A..K (U2), A..F (U1). Put the plan letter in labels (A..K) to make the sheets cross-checkable. | drawing | L | open |

## 3. Girder elevations (S-3..S-8) <-> plan girder elevations (I-80 U2 PDF 112-114, Ramp G U1 PDF 92-93) and GIRDER DATA (PDF 108)

| # | Finding | Cat. | Sev. | Status 2026-10-10 |
|---|---|---|---|---|
| 3.1 | Labels overprint heavily wherever plates change near splices and piers ("TOP FL PL 18 x 7/8" etc. become unreadable black), and the field-section line (G1-1: 42'-11 ... lb) is garbage over the pier zones; stiffener/CF mark row also collides near splices (CF362/CF366/CF369...). Developed length is drawn at 1" = 100'; the plate-length labels do not fit. | drawing | H | FIXED: dims/labels staggered with leaders (dense at 1" = 100') |
| 3.2 | Span along girder: matches the plan GIRDER DATA (A: 205'-8, 260'-3 15/16; B: 260'-2 3/4) except **G2 span 6A-6WB: model 206'-3 7/8, plan B 206'-3 15/16** (1/16", at the stated tolerance; not looked into). | model | L | open |
| 3.3 | Plan splice/plate dimensions (bearing to F.S., F.S. to F.S., top/bottom flange tension/compression zones, web changes with 18'-0 transitions, shear stud spacing and counts "26 SPA @ 10; 87 EQ. SPA.", 2'-6 stud-free zone, Detail A girder end, field splice NO.) are not shown. Sheet gives plate size + CF/stiffener marks only. | missing | H | open |
| 3.4 | Plan web plate changes (web PL thickness/height, e.g. 11/16 x 102 with thicker 18' pier zone) are not labelled as separate segments; only one "WEB 102" x 11/16"" label. | missing | M | open |
| 3.5 | Cut-off girders (U2 E ends at 9WB with a cross girder) and the 6A beginning have no "cross girder" callout; see PDF 112 note "FOR CROSS GIRDER CONNECTION DETAILS, SEE DESIGN SHEET 121". | missing | M | FIXED: cross girder detail sheet + XG marks on the framing plan |
| 3.6 | Camber/blocking and field haunch data sheets (plan PDF 132-157, 111-126): not produced (camber deferred by the user). Moment/reaction tables N/A. | missing | L | PARTLY: camber and blocking tables, estimated quantities; field haunch data not produced |

## 4. Steel details sheet (S-9) <-> plan cross frame details (I-80 PDF 122-124), welding details (120-121), cross girder (125), field splice (126)

| # | Finding | Cat. | Sev. | Status 2026-10-10 |
|---|---|---|---|---|
| 4.1 | **Wrong bolt label.** Leader says "3 - 7/8" DIA A325 BOLTS (TYP), 3" PITCH" taken from the DEFAULT definition (`SteelDetailViews.cs` line 103 uses `def.Bolts`, not the as-designed style). I-80 cross frames in the model are 1" bolts, 5 spaces @ 3 1/2" (6 bolts) at the bottom gusset per the plan (PDF 122/123 Type 3: "5 SPA. @ 3 1/2 = 1'-5 1/2"). On Ramp G it will say 7/8" which is right, but pitch/count are still the default. | drawing | H | FIXED: bolt notes from the detailed frame (rows x columns, pitch, diameter, grade); steel notes from the model materials |
| 4.2 | Only ONE intermediate and ONE support cross frame are drawn (CF243 / CF98 on U2) with no type number. The plan has Types 1-10 (I-80) / 1-5 (Ramp G) + abutment frames, each with its own section, bolt layout, tie plates, weld symbols (5/16 fillets 7/8/9/11 callouts), WP dimensions (4 or 5 SPA @ 3 1/2). Add one detail per used `CrossFrameStyle`, plus "Type n" list/count by bay. | missing | H | open: still one intermediate + one support frame per sheet |
| 4.3 | No cross girder detail sheet (plan PDF 125 with L5x5x1/2 connection angles, 3'-0 x 2'-0 R6 web opening), no field splice detail/schedule (plan PDF 126: Unit schedule with plates B/C/D/E, bolt spacing/gage, web plates, DIM "S"). All data exist in the model (`FieldSpliceDetail`, `GirderSection.Layouts`). | missing | H | FIXED: field splice details (one per distinct type) + schedule, cross girder details |
| 4.4 | Stiffener details: model shows bearing stiffener PL 7 x 7/8 and connection plate PL 7 x 1/2 with 1 1/2" clips and 5/16 welds; plan welding details (PDF 120-121) cover many more cases (flange-web, stiffener end welds, shop splices, studs). Not compared in detail. | missing | M | open |
| 4.5 | Steel note #1/#3.. are generic (A709 Gr.50, D1.5). Plan superstructure notes (PDF 94: fill plates, shop splice spacing, MT inspection, slab bracket loads) and bearing notes are not reproduced. | missing | L | PARTLY: steel notes now come from the model (materials, bolts, studs); plan notes not reproduced |
| 4.6 | "TOP OF WEB" / "TOP OF BOTT. FL." leader text overprints the 6"/10" vertical dims; frame diagonals cross at the top apex so the K frame reads as an "A" with overlapping members (plan draws members side by side with the gusset). | drawing | M | PARTLY: label collisions resolved; K-frame drawing unchanged |

## 5. Bearing sheets (S-10..: "PIER xWB BEARING DETAILS") <-> plan disc bearing details (I-80 PDF 158-162, Ramp G PDF 127-129)

| # | Finding | Cat. | Sev. | Status 2026-10-10 |
|---|---|---|---|---|
| 5.1 | Anchor geometry matches the plan data table (4 - 1 1/2", 18" embed, X/Y 22/9, masonry plate 50 x 24). | ok | - | ok |
| 5.2 | **Every bearing on every pier is flagged "CHECK: pedestal edge, hits rebar"** (Pier 6WB: clear to bars -11/16" ... -1/4", edge-to-pedestal 6" vs target). The model's cap rebar is the generic cage (see 6.3); the plan has pedestal rebar 5m1/5n1 under each girder and clears the anchors. These warnings are artifacts of the generic cage, not evidence against the plan. | model | M | open (model) |
| 5.3 | The plan BEARING DATA TABLE (type GE/MD/FX per girder line, top of bearing elevation per girder, longitudinal slope of sole plate in %, bearing height, loads, movement, anchor number/size/embed) is not reproduced; the sheet gives only "bottom of steel at CL BRG" and "BRG SEAT EL" for the one bearing shown (these agree with the plan table, e.g. 6WB D 1015.12 vs 1015.13). A top-of-bearing table per girder is the highest-value missing table (model has it: `ElevationCheck`). | missing | H | FIXED in part: bearing data table (type, fixity, station, elevations, stack height); loads/movement not modeled |
| 5.4 | No bearing type drawing (GE/MD/FX with guide bars, thermal movement line, orientation angle). The section shows a box "bearing by manufacturer, assumed height 9"". Acceptable as generic, but state fixity (GE/MD/FX) on the plan view. | missing | M | PARTLY: fixity is in the bearing data table |
| 5.5 | Bearing plan labels overlap ("TOP BARS A (#11)" runs across "CL BRG PIER 6WB"; "24" PLATE" hit by CL G4 label). Section drawing uses a vertical scale 1/2" with hatch only; OK. | drawing | L | PARTLY: label collisions resolved |

## 6. Pier / abutment bent sheets (S-17..) <-> plan pier details (I-80 PDF 43-46 for 6WB; 39-42 for 6A; Ramp G PDF 34 for 6G; abutment PDF 85-88)

| # | Finding | Cat. | Sev. | Status 2026-10-10 |
|---|---|---|---|---|
| 6.1 | Geometry that matches: see "Summary" (cap elevations, column sizes/positions, footing sizes, pile counts and tip elevations, flare elevation). Bottom of cap at 6A end and 6G match. Possible 0.02 ft difference: Pier 6A right end top of cap model 1013.260 vs plan 1013.28 (plan note "ELEVATIONS SHOWN AT FACE OF CAP"; unresolved). | ok/model | L | ok |
| 6.2 | Sheet shows only elevation + one cap section; it LACKS: cap plan (girder offsets from the cap end, pedestal layout, anchor bolt layout), reinforcing plan, sections A-A / B-B (cap) with real bars, C-C / D-D column sections (octagon + flare + bar layout), pile layout plan with batters/uplift anchors, footing reinforcing, table of column heights, quantities, pier notes (the program has all the data as `Footings`, `CapProfile`, `ColumnRebar`...). | missing | H | PARTLY: cap plan, pile layout, column heights added; sections B-B..D-D and the rebar plan missing |
| 6.3 | **Cap reinforcing is the generic cage**: Pier 6WB model A = 14 #11 top x 56'-5 / B = 14 #11 bottom (bent) / C #5 side / S #5 stirrups at 6" vs plan 11a1+11a2 = 60 #11 top (45'-6 / 43'-3), 11b1 48 #11 bottom (36'-1), 6a3-6a5 #6 side bars, 6c1 #6 hoops (186 x 22'-6), 6c2 cantilever hoops, 5c hairpins, pedestal bars 5m1/5n1. Section A-A on the sheet shows "7 #11 top, 7 #11 bottom, 2 #5 each face" in a plain rectangle. | model | H | open (model) |
| 6.4 | **Pier 6WB bar total: model 86,513 lb vs plan 135,462 lb (-36%).** Column verticals: model V1-V4 51'-1 .. 52'-6 continuous (35,236 lb) vs plan 11d1/3/4/5 45'-0 .. 46'-5 plus footing dowels 11d2 128 x 15'-1 (lapped; 31,072 + 10,258 lb). Footing: model #8 mats 19'-6 / 17'-6 (7,882 lb bottom, same top) vs plan #10 bottom (10g1 140 x 23'-2, 10g2 80 x 21'-2 = 21,242 lb) and #7 top (7f1/7f2), 7f3 side, 7f4/7f5, 8f6 uplift anchors (48), plinth bars 7d12/7d13. Column hoop count in the model (324 "COLUMN 1 HOOPS") ~ plan 6e1 total for all 4 columns (332): label says column 1 only; check that columns 2-4 are listed or counted. | model | H | open (model) |
| 6.5 | Abutments (West Abut S-12 in U1; East in U3): model is a 54" x 49" cap with "3 PILES" and cap top "sloped 0.309% best fit through the seats"; plan: 6'-8 1/2 x 4'-0 pile footing (bottom 992.82), 1'-6 backwall with corbel, 34 piles (26 + 8 wing), stepped seats, wingwalls, 44° skew shown. Seat elevations match (997.15..997.87). Sheet shows model cap bottom 992.389 vs plan footing bottom 992.82. Known (HANDOFF section 3 item 1, "needs an as-designed abutment feature"). | model | H | open (model) |
| 6.6 | Bent elevation text: seat/pedestal callouts ("G1 SEAT 1013.793, PED 4 7/16"...) overprint each other and the cap on every pier (worst on 6WB, 6A, 6G); "BARS A / B1 / C1 / S1" leaders cross. The label "TOP OF CAP EL" is attached to the beak tips on as-designed caps (HANDOFF 2.1, cosmetic). | drawing | H | FIXED: seat/pedestal table beside the section when a bent has more than 2 bearings; collisions resolved |
| 6.7 | Scale choice: tall piers (6G: top of cap 1036 to pile tip 866) force 1" = 20' so the sheet shows a tiny bent; the piles are drawn full length. Use a break in the pile/column, or a separate larger cap detail. | drawing | M | open |
| 6.8 | Pile layout: footing plan with pile spacing/batters is not drawn at all. Plan: 21 piles in 5 rows (4'-0 / 3'-6 spacing), 4 corner piles circled for uplift anchors. | missing | M | FIXED: footing and pile layout (S/N positions, batter lines, spacings, tip elevations) |
| 6.9 | Bar list sheets: model marks (A, B1, C1, S1..S31, V1..V4, P1-P3, F1-F2, G1-G2) are NOT the plan marks (11a1, 6c1, 11d1, 6e1, 7f1, 10g1 ...), so a checker cannot reconcile; stirrup sizes (#5 vs plan #6) differ. A per-pier bar list in the plan format (BAR / LOCATION / SHAPE / NO. / LENGTH / WEIGHT) with the plan's bent-bar details would help. S1 "varies" family S2..S31 (30 stirrups of one each) is unreadable as a schedule (plan uses 6c2 "VARIES"). | drawing | M | open |
| 6.10 | Bend diagrams (B1/B2/C1..C4) have dimension text overprinted at the short legs ("A = 99 13/16"" on top of "B"), and leader text "R = 1 1/4" (INSIDE)" exceeds its cell on S2..S18 (spills past the page margin). | drawing | M | open (bar sheets are excluded from the label placer) |

## 7. Superstructure bar list sheets (S-24..S-29) <-> plan slab bar list (I-80 PDF 196, Ramp G PDF ~141)

| # | Finding | Cat. | Sev. | Status 2026-10-10 |
|---|---|---|---|---|
| 7.1 | Mark system T1..T78 (192 + 40 per size, 1" length steps), B, L, N marks; plan marks 7a1..7a24, 6b1..6b8, 5d (end dam diaphragm), 5e, ... with SLAB TRANSVERSE TOP/BOTTOM counts 2,070 (U2, 56'-4 / 50'-7 ...). Sizes differ (#5 vs #7 transverse, #4/#5 vs #6 longitudinal), see 2.1. Model total 735,869 lb vs plan 1,204,071 lb. | model | H | open (model) |
| 7.2 | Plan also lists END DAM DIAPH. bars (5d, 5e), slab at drains (5a23), inserts (5a24), concrete placement quantities by section (U2 2,932 CY). Model sheets list negative-moment "N" bars at piers which the plan does not have (continuous #6 top+bottom longitudinal). | model/missing | M | open |
| 7.3 | Slab quantities by placement section (concrete CY per pour) and "SUMMARY" are not produced. | missing | M | PARTLY: estimated quantities table (model quantities) |

## 8. Presentation (all sheets)

- Title block: project, sheet "S-n (n OF N)", scale list, date. Missing from the plan style: county, design/project number, designer/checker, revision block. The footer note "GEOMETRY FROM THE PARAMETRIC MODEL. NOT FOR CONSTRUCTION UNLESS SEALED." collides with "STEEL BRIDGE MODELER" in the title block on every sheet.
- Sheet size 17x11 (matches 11x17) good; borders, stacked title look professional. Text weight is much lighter than the plan's, the lines are all hairline gray at 1/16" scale (sections are readable only at 150+ dpi). Dimension text is arial-size independent of scale (good) but long labels (leader + "TYP" notes) overlap everything at 1/16" and 1" = 100' scales.
- Fractions are printed "7/8", "1 1/2" (plan: stacked glyphs). OK, but "1 1/4" (INSIDE)" is easy to misread; consider "1-1/4" or the stacked style used on plans.
- Every pier gets its own sheet at 1/16" (6A, 6WB .. at the same scale), but the unit's elevation sheets use 4 different scales (1" = 20' ... 100') so G1/G2 on one sheet differ by 5x: acceptable since plans use one scale per sheet, but draw a length scale bar.

## 9. Completeness: plan information absent from the generated sets (ranked by use to a bridge engineer)

1. Structural steel LAYOUT / framing plan per unit (cross frame types and spacing, skews at each support, girder offsets, field splice numbers, stiffener marks, girder data table with spans/FS distances). Plan: I-80 PDF 103-118, Ramp G PDF 90-100. Model has frames (467/331/272, 129...), splices, girder offsets.
2. Bearing data table with top of bearing per girder, type GE/MD/FX, sole plate slope, height, anchors (I-80 PDF 158-162, Ramp G 127-129). `ElevationCheck` already computes it and compares with the plan.
3. Top-of-slab elevation tables (I-80 PDF 199-208, Ramp G 142-149) and top-of-bearing chain: the model reproduces them to 0.1-0.2" (HANDOFF); emit them (equal-spaced points along each girder between splices/piers) so they can be cross-checked line by line.
4. Girder elevations with full dimensioning: F.S. locations, flange/web plates, studs, tension/compression zones, Detail A girder end (I-80 PDF 112-114, Ramp G PDF 92-100).
5. Field splice details + schedule (PDF 126 / Ramp G 106); one detail per distinct cross frame type; cross girder details; welding details.
6. Pier detail set: cap plan, reinforcing plan, sections A-A/B-B/C-C/D-D, footing/pile layout, per-pier bar list in plan marks, column heights table, concrete quantities.
7. Abutment details (plan, rear elevation, section through abutment, piling layout, wingwalls) once the as-designed abutment feature exists.
8. Camber and blocking, field haunch data, moment/reaction tables (camber deferred by user).
9. Situation plan / staking diagram / drainage / inspection access / expansion joint settings / sign supports (not modeled or partially modeled).
10. Estimated quantities (structural steel weight, concrete CY, reinforcing). Model has them; plan sheets 1-2 list them.

## 10. Defects found while generating

| # | Finding | Cat. | Sev. | Status 2026-10-10 |
|---|---|---|---|---|
| D1 | **`sbm sheets` and `sbm drawings` crash for "Ramp G Unit 1"**: `InvalidOperationException: Sequence contains no matching element` at `SectionViews.cs` line 118 (`det.Members.First(m => m.Code is "TC" or "DIA")` for the bay-0 cross frame). That frame detail has no top chord/diaphragm member (likely a W strut / WT or abutment-type frame with a bottom chord only). The whole unit's sheet set and DXF set fail. Use `FirstOrDefault` and skip the leader or take any member. Not seen in the other 8 units. | drawing | H | FIXED: guard in SectionViews; Ramp G Unit 1 now produces 52 sheets |
| D2 | `sbm sheets` without a bridge name builds all bridges? (not tested). Output file names for `drawings` are `<bridge> - Pier6A.dxf` (support names without spaces): fine. | - | - | - |
| D3 | Per-pier DXF files are 1.7-3.0 MB (rebar bars as polylines) vs 0.3 MB for typical section: acceptable, but the Pier DXF includes both 3D-derived rebar and cap: check AutoCAD load speed. | - | L | - |

## 11. Prioritized fix list

1. D1 crash on Ramp G Unit 1 (`SectionViews.cs:118`): one-line guard; blocks all Ramp G Unit 1 sheets/DXF.
2. Collision cleanup: S-1 elevation field-splice labels (suppress or stagger / leader), span labels vs CL BRG labels, girder elevation plate/field-section labels (pier zones), bent elevation seat/pedestal callouts, title block footer vs project name. These make the sheets look unfinished more than any content gap.
3. S-9: use the as-designed style data for the bolt note (diameter, count, pitch), draw one frame per used style (Types 1-10 / Ramp G 1-5) with the plan type number and where it is used; add field splice schedule + detail (data in model) and cross girder detail.
4. Add the bearing data table (type, top of bearing per girder, slope %, height, anchors) and the top-of-slab / top-of-bearing tables from `ElevationCheck`.
5. Add the steel layout / framing plan per unit with spans, skews, girder offsets, field splice numbers and cross frame lines.
6. Model: as-designed deck reinforcing (#7 transverse @10", #6 longitudinal; 1" bottom clear) and the Iowa barrier rail; until then the superstructure bar quantities are 20-40% low and the typical section note is wrong.
7. Model: as-designed cap rebar (plan 11a/11b/6a/6c/5c marks, pedestal bars 5m/5n), column dowels (11d2 lapped, not continuous), footing mats (#10 bottom / #7 top, 8f6 uplift bars); then bearing anchor "hits rebar" checks (5.2) should clear. Use plan marks on the bar list sheets.
8. Pier sheet content: cap plan, sections, pile plan, scale/break for tall piers, per-pier bar list in the plan format.
9. Typical section: second station after a girder drops (U2), lane widths/tangents, haunch detail, plan girder letters.
10. Abutment as-designed feature (footing 6'-8 1/2 x 4'-0, 26+8 piles, backwall, corbel, wingwalls) then its sheets.
11. Print both CL pier and CL bearing span lengths so the sheets compare directly with the plan notes.


## 12. Status of the fix list (branch feature/sheets, 2026-10-10)

| # | Item | Status |
|---|---|---|
| 1 | D1 crash on Ramp G Unit 1 | fixed |
| 2 | Collision cleanup | done generically: `LabelPlacer` (Drawing/Views) moves overprinted labels (dimension texts first, then other texts) perpendicular to their baseline with a leader; all non-table views on every sheet go through it (also the DXF views). Specific fixes: staggered field-splice labels, bent seat table, title-block footer, wider text-width estimate in `PlanDrawing.Extents`. Bar-list tables are left alone. |
| 3 | S-9 bolt note, frames per style, splice schedule/detail, cross girder detail | bolt notes (rows x columns, pitch, diameter, grade) come from `CrossFrameDetail2D` bolt positions; splice details/schedule and cross girder details added; one frame per style still open |
| 4 | Bearing data + top of slab / top of bearing tables | added: bearing data, top of slab and top of web tables at supports and quarter points (the plan lists its own numbered points) |
| 5 | Steel layout / framing plan | added (spans grouped to about 330 ft per sheet) plus a girder spacing table |
| 6, 7 | Model: as-designed deck / cap rebar | not a sheet task, open |
| 8 | Pier sheet content | cap plan, pile layout, column heights added; B-B..D-D sections, rebar plan, plan-format bar list open |
| 9 | Second typical section | added (span with the fewest girders) |
| 10 | Abutment as-designed feature | open |
| 11 | CL pier and CL bearing span lengths | open |

Also added (not in the original list, they were DATA gaps in `detail-inventory.md`): cross slope geometrics, camber and blocking, estimated quantities.
