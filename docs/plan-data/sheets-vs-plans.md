# Generated sheets vs the real plan sets (I-80 WB / Ramp G 2026-10-10, re-checked 2026-10-11; Woodbury I-29 and Br02069 TH65 2026-10-11)

## Top 10 fixes by impact (all four bridges)

Status 2026-10-12 (branch sheet-fixes, section 17): 1 PARTIAL, 2 PARTIAL, 3 FIXED (drawing), 4 FIXED grouping + model gaps listed, 5 FIXED, 6 FIXED, 7 FIXED, 8 FIXED (drawing), 9 FIXED / PARTIAL, 10 FIXED.

1. **Prestressed sheet sets lack most of the plan content** (Woodbury W9, W11-W13; Br02069 B4, B6, B7, B12): no beam layout/framing plan, no deck / barrier / slab bar list sheets, no abutment reinforcement, no steel diaphragm sheet, no beam detail (stirrups, debonding, camber). Add the prestressed framing plan, deck and abutment bar lists and the diaphragm sheet first.
2. **Typical sections of the prestressed bridges**: Woodbury has no barrier and a flat deck (W4); Br02069 lacks roadway dimensions, deck steel, future widening (B2).
3. **Abutment sheets for integral / pile-supported prestressed abutments are shells** (W20, B11): empty 'BEAM STEP TOPS:' and 'support line at -' fields, no diaphragm / backwall / wing / rebar.
4. **Quantity tables disagree with the plan item lists**: Woodbury reinforcing 54,907 lb vs 161,585 lb; Br02069 substructure concrete 249 CY vs 140 CY (W23, B13).
5. **Pier cap Section A-A prints a generic 7 #11 cage** although the bar list holds the plan cap bars (I-80 N4; Woodbury W22).
6. **Dimension formatter drops the zero inches** ('8'-5/16"', '10'-13/16"', '227'-15/16"'): one fix, every sheet (N1).
7. **Wrong agency / values in prestressed notes**: 'TxDOT BTD / 30MH', 'Class H', f'c 5.0/f'ci 4.5 for BTD105 (plan 7.0/6.0), 'Elastomeric pad 9 x 28 1/2 x 1' (W6, W7, B5).
8. **Woodbury pile layout and tip elevations**: outer rows 3" off the plan (60" vs 63" from CL), tip EL 966.0 vs 969.7 on different sheets of the same pier (W18).
9. **Collisions still left**: girder elevation plate labels at every pier zone (3.1, N7), bent elevation bar labels (N3), bend diagrams on all bar sheets (6.10, N5), pile/splice labels running off the border (N2).
10. **Items modeled today but not documented**: girder end daps, pier drains, sign brackets, light pole bases (N6); bearing data 'Generic' / '-' on all bearings of the prestressed bridges (W15, B9).

## Result summary (2026-10-11)

* `sbm sheets` regenerated with a Release CLI built from this branch (main d21a33c): I-80 WB U1/U2/U3 = 63 / 85 / 60 sheets (10/10: 31 / 45 / 32), Ramp G U1/U2/U3/U4 = 58 / 50 / 62 / 52 (10/10: crash / 27 / 32 / 30), Woodbury 19, Br02069 10. **No crashes** (Ramp G Unit 1 now generates 58 sheets and 12 DXF views; the Woodbury and Br02069 DXF sets also generate). Generation 0.1-1.2 s per PDF.
* 10/10 numbered items (sections 1-7 and D, 44 rows): 14 FIXED / ok / largely fixed; still open or only partly fixed by severity: H 7, M 15, L 7.
* New findings on I-80 WB / Ramp G (section 13, N1..N11): H 2, M 6, L 3.
* Woodbury (section 14, W1..W25): H 8, M 11, L 6.
* Br02069 (section 15, B1..B14): H 6, M 5, L 3.
* Automated text-overlap pairs per PDF (approximate): Br02069 9, Woodbury 24, I-80 U1/U2/U3 133/217/95, Ramp G U1..U4 153/144/264/131 (mostly S-1 layout sheets and bar-list bend diagrams).

## Original review (2026-10-10). The Status column is as of that day; the last column is the 2026-10-11 re-check

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

Skews on S-1: I-80 U2 8WB/9WB/10WB/11WB = 42Â°41'27", 38Â°38'00", 33Â°43'51", 27Â°27'55" and U3 27Â°27'55" ... 5Â°26'43"; U1 44Â°/45Â°30' (all appear in the plan text).
U2 deck: 11'-6 girder spacing x5, 3'-1 overhang, 8" slab, 3 1/2" haunch (plan PDF 95). Ramp G U2: 3 spaces at 9'-8, 3'-1 overhang, 35'-2 out-to-out (PDF 85).
Girder A span lengths along the girder U2 (205'-8, 260'-3 15/16), plate sizes (web 11/16 x 102, flange 7/8 x 18 ... 2 x 30), cross frame Type 3 sections
(L6x6x5/8 chord, L6x6x3/4 diagonals, 2-MC10x25, 5/8 gusset, 6" and 10" work-point offsets). Pier 6WB: 4 columns 8'-0 x 4'-6, 27'-4 spacing, column
offsets 13'-7 5/8 / 13'-9 3/8, cap top 1013.07 / 1014.92, footings 18' x 20', 21 HP14x73, footing bottom 961.28. Pier 6G: cap 37'-11 5/16, 1036.62 / 1034.55 / 1026.24,
footing bottom 970.57. Pier 6A: 24 HP14x73, cap 1011.50 / 1002.94. Disc bearing anchors 4 - 1 1/2" dia, 18" embed, X = 22" / Y = 9" (masonry plate 50 x 24).
West abutment seat elevations 997.15 / .51 / .87 / .78 / .62 / .45. Bolts 7/8" in 15/16" holes (plan PDF 94 note, same as the notes on S-9).

## 1. Layout sheet (S-1: plan developed along CL + elevation) <-> plan layout/situation, steel layout PDF 108, longitudinal section PDF 101

| # | Finding | Cat. | Sev. | Status 2026-10-10 | Re-check 2026-10-11 |
|---|---|---|---|---|---|
| 1.1 | Text collides: span labels overprinted by "CL BRG PIER" labels (SPAN 3 / SPAN 4 / SPAN 5 unreadable on I-80 U2); the field-splice station labels in the elevation overlap into a solid black strip (`FIELD SPLICE STA ...` x 8 on one line, same on Ramp G); pile tip labels run into the right margin. | drawing | H | FIXED: LabelPlacer + staggered splice labels (verified on I-80 U2, Ramp G U3) | PARTLY OPEN: spans/CL BRG labels and the splice labels no longer form a black strip, but on I-80 U2 and Ramp G U3 S-1 there are still 8-9 'FIELD SPLICE STA' labels crossing the pier lines and each other, and the last 'PILES, TIP EL' label runs past the right border. Framing plan S-4: 'CL BRG PIER 5WB' overprints CF2-CF4, 'STA 7591+06.10 SKEW' overprints G8. |
| 1.2 | The plan strip draws only girder lines + deck edge; no cross frames, stiffeners, splices, skew angles at piers, girder offsets at the bearing line, cross frame spacing. The plan set has a structural steel LAYOUT for each unit (U2: PDF 108-111, Ramp G: PDF 90-101) = missing view (see section 9). | missing | H | FIXED: steel framing plan sheets (girders, CF marks, splices, bearings); the layout strip itself is unchanged | FIXED (framing plan sheets, 1 per span; S-4..S-8 on I-80 U2). Layout strip itself unchanged. |
| 1.3 | Station at the pier labels is the CL BRG at the alignment (e.g. 8WB 7598+10.48); the plans give CL pier stations/offsets (e.g. 8WB "7598+11.13, 41.08' LT"). Different reference, not comparable; print both. | drawing | L | open | still open |
| 1.4 | Bridge length label "1216'-8 1/8" (end of deck to end of deck)" vs plan "1216'-5 5/8" (Pier 5WB to 11WB)". Different definitions; label the plan one too. | drawing | L | open | CHANGED: label now '1215'-8 1/4"' on I-80 U2 (was 1216'-8 1/8"; equals begin 7591+06.76 to end 7603+22.45); plan 1216'-5 5/8" is pier to pier. Cause of the -11 7/8" change not checked (deck-end change?). |
| 1.5 | No bearing and expansion device setting table (plan PDF 101 "Longitudinal section" = joint/bearing setting at 10/50/90 F, joint openings). | missing | M | open (no joint/bearing setting table; not modeled) | still open |

## 2. Typical section (S-2) <-> plan typical sections (I-80 PDF 94-97, Ramp G PDF 85)

| # | Finding | Cat. | Sev. | Status 2026-10-10 | Re-check 2026-10-11 |
|---|---|---|---|---|---|
| 2.1 | **Deck reinforcing differs from the plan.** Sheet says "#5 @ 6" top transv., #4 @ 9" longit. (2 1/2" clr); #5 @ 6" bott. transv., #5 @ 9" longit. (1 1/4" clr)". Plan: 7a (#7) transverse at 10" (Ramp G) / 6b (#6) longitudinal, 25 bars across 11'-6 (I-80) or @ 9" (Ramp G); cover 2 1/2" top, **1" clear above bottom** (PDF 94 note). Model uses the TxDOT SSTR default. Slab bar list totals: model U2 735,869 lb vs plan U2 1,204,071 lb (PDF 196; U1 849,698, U3 903,702). Known (HANDOFF 3.5). | model | H | open (model) | FIXED: note prints '#7 @ 7" top/bott. transv., #6 longit.'; superstructure bar total 1,204,684 lb vs plan 1,204,071 (+0.05%). |
| 2.2 | One station per unit (e.g. U2 at 7592+12.14, girders G1-G11). The plan shows two sections for U2 (start of unit to 7594+03 with 11 girders, 7594+03 to 7596+41 with girder E dropped) plus Part Typical Section. A section at a second station (after a girder terminates) is needed; Ramp G shows "half section near cross frame / near abutment". | missing | M | PARTLY: a second typical section at the span with the fewest girders | PARTLY: second section (S-3, '7 girders', STA 7600+95.32) is the span with the fewest girders, not the plan's break (U2: E dropped at 7594+03). |
| 2.3 | The plan dims lane/shoulder tangent widths (12'-0 tangent on 2.0% / variable slope), roadway width, 34'-0 / 7'-1 offsets from P.G.L., slab area, haunch detail, overhang bracket detail, barrier orientation detail. Sheet has only girder spacing, out-to-out, cross slopes. | missing | M | open | still open |
| 2.4 | The barrier drawn is the TxDOT SSTR "F-shape" profile (CLAUDE.md); the plans use the Iowa rail (sheets 217-225 / Ramp G 160-163, separate rail bar list: 6d1 624 x 38'-8, ...). Shape, rail rebar and quantities not as designed. | model | M | open (model) | still open (F-shape barrier; barrier concrete 312 CY on S-71) |
| 2.5 | Sheet scale 1/16" for the 113 ft wide I-80 deck leaves 60% of the sheet empty (1/8" would fit on 11x17). Labels overlap: "INVERTED K-FRAME CF50 (BEYOND)..." runs through the dimension string, "WEB 102 x 11/16" and "3 1/2" HAUNCH..." leaders cross the section; Ramp G: cross slope note "5.9%" overprinted by the frame leader. | drawing | M | PARTLY: overlaps resolved; scale still limited by the paper-sized notes | PARTLY: overlaps with the frame leader/dimension strings remain on S-2 (two stacked dimension rows overprint each other: '10'-8 7/8" 10'-7 15/16"'); scale 1" = 20' (S-2), 1" = 10' (S-3); 60% of the sheet empty. |
| 2.6 | Girders are named G1..G11 (north to south); plan uses A..K (U2), A..F (U1). Put the plan letter in labels (A..K) to make the sheets cross-checkable. | drawing | L | open | still open (G1..G11) |

## 3. Girder elevations (S-3..S-8) <-> plan girder elevations (I-80 U2 PDF 112-114, Ramp G U1 PDF 92-93) and GIRDER DATA (PDF 108)

| # | Finding | Cat. | Sev. | Status 2026-10-10 | Re-check 2026-10-11 |
|---|---|---|---|---|---|
| 3.1 | Labels overprint heavily wherever plates change near splices and piers ("TOP FL PL 18 x 7/8" etc. become unreadable black), and the field-section line (G1-1: 42'-11 ... lb) is garbage over the pier zones; stiffener/CF mark row also collides near splices (CF362/CF366/CF369...). Developed length is drawn at 1" = 100'; the plate-length labels do not fit. | drawing | H | FIXED: dims/labels staggered with leaders (dense at 1" = 100') | PARTLY: field-section weights and F.S. dimensions are staggered and readable; the plate labels ('TOP FL PL 30 x 1 3/8"', 'BOT FL PL 34 x 1 1/2"') still overprint each other in every pier zone (I-80 U2 S-9..S-13: 13-17 overlapping text pairs per sheet). |
| 3.2 | Span along girder: matches the plan GIRDER DATA (A: 205'-8, 260'-3 15/16; B: 260'-2 3/4) except **G2 span 6A-6WB: model 206'-3 7/8, plan B 206'-3 15/16** (1/16", at the stated tolerance; not looked into). | model | L | open | still open (G2 6A-6WB 206'-3 7/8") |
| 3.3 | Plan splice/plate dimensions (bearing to F.S., F.S. to F.S., top/bottom flange tension/compression zones, web changes with 18'-0 transitions, shear stud spacing and counts "26 SPA @ 10; 87 EQ. SPA.", 2'-6 stud-free zone, Detail A girder end, field splice NO.) are not shown. Sheet gives plate size + CF/stiffener marks only. | missing | H | open | PARTLY: F.S.-to-F.S. and plate dimensions now shown; no stud counts, tension/compression zones, Detail A girder end. |
| 3.4 | Plan web plate changes (web PL thickness/height, e.g. 11/16 x 102 with thicker 18' pier zone) are not labelled as separate segments; only one "WEB 102" x 11/16"" label. | missing | M | open | still open |
| 3.5 | Cut-off girders (U2 E ends at 9WB with a cross girder) and the 6A beginning have no "cross girder" callout; see PDF 112 note "FOR CROSS GIRDER CONNECTION DETAILS, SEE DESIGN SHEET 121". | missing | M | FIXED: cross girder detail sheet + XG marks on the framing plan | FIXED (S-24/S-25 cross girder details; XG marks) |
| 3.6 | Camber/blocking and field haunch data sheets (plan PDF 132-157, 111-126): not produced (camber deferred by the user). Moment/reaction tables N/A. | missing | L | PARTLY: camber and blocking tables, estimated quantities; field haunch data not produced | PARTLY: camber and blocking tables (17 sheets on I-80 U2); field haunch data not produced |

## 4. Steel details sheet (S-9) <-> plan cross frame details (I-80 PDF 122-124), welding details (120-121), cross girder (125), field splice (126)

| # | Finding | Cat. | Sev. | Status 2026-10-10 | Re-check 2026-10-11 |
|---|---|---|---|---|---|
| 4.1 | **Wrong bolt label.** Leader says "3 - 7/8" DIA A325 BOLTS (TYP), 3" PITCH" taken from the DEFAULT definition (`SteelDetailViews.cs` line 103 uses `def.Bolts`, not the as-designed style). I-80 cross frames in the model are 1" bolts, 5 spaces @ 3 1/2" (6 bolts) at the bottom gusset per the plan (PDF 122/123 Type 3: "5 SPA. @ 3 1/2 = 1'-5 1/2"). On Ramp G it will say 7/8" which is right, but pitch/count are still the default. | drawing | H | FIXED: bolt notes from the detailed frame (rows x columns, pitch, diameter, grade); steel notes from the model materials | FIXED: I-80 '4 - 1" DIA A325 BOLTS (2 rows x 2 col.), 3 1/2" PITCH' top, 12 bolts bottom; Ramp G 7/8" (4 + 8 bolts, 3" pitch). |
| 4.2 | Only ONE intermediate and ONE support cross frame are drawn (CF243 / CF98 on U2) with no type number. The plan has Types 1-10 (I-80) / 1-5 (Ramp G) + abutment frames, each with its own section, bolt layout, tie plates, weld symbols (5/16 fillets 7/8/9/11 callouts), WP dimensions (4 or 5 SPA @ 3 1/2). Add one detail per used `CrossFrameStyle`, plus "Type n" list/count by bay. | missing | H | open: still one intermediate + one support frame per sheet | still open: one intermediate + one support frame (CF235/CF98), no type number |
| 4.3 | No cross girder detail sheet (plan PDF 125 with L5x5x1/2 connection angles, 3'-0 x 2'-0 R6 web opening), no field splice detail/schedule (plan PDF 126: Unit schedule with plates B/C/D/E, bolt spacing/gage, web plates, DIM "S"). All data exist in the model (`FieldSpliceDetail`, `GirderSection.Layouts`). | missing | H | FIXED: field splice details (one per distinct type) + schedule, cross girder details | FIXED: splice details A-E (+ schedule 3 sheets) and cross girder sheets. Cross girder drawing is crude (wire outlines crossing, '85 3/8"' dimension on the outline). |
| 4.4 | Stiffener details: model shows bearing stiffener PL 7 x 7/8 and connection plate PL 7 x 1/2 with 1 1/2" clips and 5/16 welds; plan welding details (PDF 120-121) cover many more cases (flange-web, stiffener end welds, shop splices, studs). Not compared in detail. | missing | M | open | still open |
| 4.5 | Steel note #1/#3.. are generic (A709 Gr.50, D1.5). Plan superstructure notes (PDF 94: fill plates, shop splice spacing, MT inspection, slab bracket loads) and bearing notes are not reproduced. | missing | L | PARTLY: steel notes now come from the model (materials, bolts, studs); plan notes not reproduced | PARTLY (notes from model materials) |
| 4.6 | "TOP OF WEB" / "TOP OF BOTT. FL." leader text overprints the 6"/10" vertical dims; frame diagonals cross at the top apex so the K frame reads as an "A" with overlapping members (plan draws members side by side with the gusset). | drawing | M | PARTLY: label collisions resolved; K-frame drawing unchanged | PARTLY: K-frame now reads correctly with gusset; residual '1/4" AT CLIP' overprints 'TO WEB'; a stray '7"' dimension floats above the stiffener details. |

## 5. Bearing sheets (S-10..: "PIER xWB BEARING DETAILS") <-> plan disc bearing details (I-80 PDF 158-162, Ramp G PDF 127-129)

| # | Finding | Cat. | Sev. | Status 2026-10-10 | Re-check 2026-10-11 |
|---|---|---|---|---|---|
| 5.1 | Anchor geometry matches the plan data table (4 - 1 1/2", 18" embed, X/Y 22/9, masonry plate 50 x 24). | ok | - | ok | ok |
| 5.2 | **Every bearing on every pier is flagged "CHECK: pedestal edge, hits rebar"** (Pier 6WB: clear to bars -11/16" ... -1/4", edge-to-pedestal 6" vs target). The model's cap rebar is the generic cage (see 6.3); the plan has pedestal rebar 5m1/5n1 under each girder and clears the anchors. These warnings are artifacts of the generic cage, not evidence against the plan. | model | M | open (model) | still open (all 11 bearings on 6WB 'CHECK: hits rebar', model rebar vs plan pedestal bars) |
| 5.3 | The plan BEARING DATA TABLE (type GE/MD/FX per girder line, top of bearing elevation per girder, longitudinal slope of sole plate in %, bearing height, loads, movement, anchor number/size/embed) is not reproduced; the sheet gives only "bottom of steel at CL BRG" and "BRG SEAT EL" for the one bearing shown (these agree with the plan table, e.g. 6WB D 1015.12 vs 1015.13). A top-of-bearing table per girder is the highest-value missing table (model has it: `ElevationCheck`). | missing | H | FIXED in part: bearing data table (type, fixity, station, elevations, stack height); loads/movement not modeled | PARTLY FIXED: S-27/28 give type ('Generic', not GE/MD/FX), fixity, station, offset, bottom of steel, top of bearing, stack height; no sole-plate slope, loads, movement, anchors. |
| 5.4 | No bearing type drawing (GE/MD/FX with guide bars, thermal movement line, orientation angle). The section shows a box "bearing by manufacturer, assumed height 9"". Acceptable as generic, but state fixity (GE/MD/FX) on the plan view. | missing | M | PARTLY: fixity is in the bearing data table | PARTLY |
| 5.5 | Bearing plan labels overlap ("TOP BARS A (#11)" runs across "CL BRG PIER 6WB"; "24" PLATE" hit by CL G4 label). Section drawing uses a vertical scale 1/2" with hatch only; OK. | drawing | L | PARTLY: label collisions resolved | PARTLY: bearing plan labels still crowded (S-50) |

## 6. Pier / abutment bent sheets (S-17..) <-> plan pier details (I-80 PDF 43-46 for 6WB; 39-42 for 6A; Ramp G PDF 34 for 6G; abutment PDF 85-88)

| # | Finding | Cat. | Sev. | Status 2026-10-10 | Re-check 2026-10-11 |
|---|---|---|---|---|---|
| 6.1 | Geometry that matches: see "Summary" (cap elevations, column sizes/positions, footing sizes, pile counts and tip elevations, flare elevation). Bottom of cap at 6A end and 6G match. Possible 0.02 ft difference: Pier 6A right end top of cap model 1013.260 vs plan 1013.28 (plan note "ELEVATIONS SHOWN AT FACE OF CAP"; unresolved). | ok/model | L | ok | ok - re-verified on S-64: cap top 1013.070 / 1014.920, footing bottom 961.280, 96 x 54 columns, 21 HP14x73 |
| 6.2 | Sheet shows only elevation + one cap section; it LACKS: cap plan (girder offsets from the cap end, pedestal layout, anchor bolt layout), reinforcing plan, sections A-A / B-B (cap) with real bars, C-C / D-D column sections (octagon + flare + bar layout), pile layout plan with batters/uplift anchors, footing reinforcing, table of column heights, quantities, pier notes (the program has all the data as `Footings`, `CapProfile`, `ColumnRebar`...). | missing | H | PARTLY: cap plan, pile layout, column heights added; sections B-B..D-D and the rebar plan missing | PARTLY: cap plan + piles + column heights; sections B-B..D-D, rebar plan, quantities per pier still missing |
| 6.3 | **Cap reinforcing is the generic cage**: Pier 6WB model A = 14 #11 top x 56'-5 / B = 14 #11 bottom (bent) / C #5 side / S #5 stirrups at 6" vs plan 11a1+11a2 = 60 #11 top (45'-6 / 43'-3), 11b1 48 #11 bottom (36'-1), 6a3-6a5 #6 side bars, 6c1 #6 hoops (186 x 22'-6), 6c2 cantilever hoops, 5c hairpins, pedestal bars 5m1/5n1. Section A-A on the sheet shows "7 #11 top, 7 #11 bottom, 2 #5 each face" in a plain rectangle. | model | H | open (model) | FIXED in the model (11A1/11A2/11B1/6C1.. in the bar list). BUT the sheet's Section A-A still prints generic '7 #11 top, 7 #11 bottom, 2 #6 each face' (new finding I-5). |
| 6.4 | **Pier 6WB bar total: model 86,513 lb vs plan 135,462 lb (-36%).** Column verticals: model V1-V4 51'-1 .. 52'-6 continuous (35,236 lb) vs plan 11d1/3/4/5 45'-0 .. 46'-5 plus footing dowels 11d2 128 x 15'-1 (lapped; 31,072 + 10,258 lb). Footing: model #8 mats 19'-6 / 17'-6 (7,882 lb bottom, same top) vs plan #10 bottom (10g1 140 x 23'-2, 10g2 80 x 21'-2 = 21,242 lb) and #7 top (7f1/7f2), 7f3 side, 7f4/7f5, 8f6 uplift anchors (48), plinth bars 7d12/7d13. Column hoop count in the model (324 "COLUMN 1 HOOPS") ~ plan 6e1 total for all 4 columns (332): label says column 1 only; check that columns 2-4 are listed or counted. | model | H | open (model) | FIXED: Pier 6WB 134,747 lb vs plan 135,462 lb (-0.5%) |
| 6.5 | Abutments (West Abut S-12 in U1; East in U3): model is a 54" x 49" cap with "3 PILES" and cap top "sloped 0.309% best fit through the seats"; plan: 6'-8 1/2 x 4'-0 pile footing (bottom 992.82), 1'-6 backwall with corbel, 34 piles (26 + 8 wing), stepped seats, wingwalls, 44Â° skew shown. Seat elevations match (997.15..997.87). Sheet shows model cap bottom 992.389 vs plan footing bottom 992.82. Known (HANDOFF section 3 item 1, "needs an as-designed abutment feature"). | model | H | open (model) | LARGELY FIXED: West abutment S-49: 34 piles, wing layout, footing 80 1/2" x 48", bottom 992.820 (plan 992.82), seats 997.15..997.87; the rear elevation does not show wingwalls/corbel/backwall detail. |
| 6.6 | Bent elevation text: seat/pedestal callouts ("G1 SEAT 1013.793, PED 4 7/16"...) overprint each other and the cap on every pier (worst on 6WB, 6A, 6G); "BARS A / B1 / C1 / S1" leaders cross. The label "TOP OF CAP EL" is attached to the beak tips on as-designed caps (HANDOFF 2.1, cosmetic). | drawing | H | FIXED: seat/pedestal table beside the section when a bent has more than 2 bearings; collisions resolved | PARTLY: seat/pedestal table fixed; rebar leader labels (BARS R1/M2/E1/A1/B1) still overprint on every bent; '109'-4 15/16" CAP' dimension overprints the pile note |
| 6.7 | Scale choice: tall piers (6G: top of cap 1036 to pile tip 866) force 1" = 20' so the sheet shows a tiny bent; the piles are drawn full length. Use a break in the pile/column, or a separate larger cap detail. | drawing | M | open | still open (1/16" bents) |
| 6.8 | Pile layout: footing plan with pile spacing/batters is not drawn at all. Plan: 21 piles in 5 rows (4'-0 / 3'-6 spacing), 4 corner piles circled for uplift anchors. | missing | M | FIXED: footing and pile layout (S/N positions, batter lines, spacings, tip elevations) | FIXED (S-57..) |
| 6.9 | Bar list sheets: model marks (A, B1, C1, S1..S31, V1..V4, P1-P3, F1-F2, G1-G2) are NOT the plan marks (11a1, 6c1, 11d1, 6e1, 7f1, 10g1 ...), so a checker cannot reconcile; stirrup sizes (#5 vs plan #6) differ. A per-pier bar list in the plan format (BAR / LOCATION / SHAPE / NO. / LENGTH / WEIGHT) with the plan's bent-bar details would help. S1 "varies" family S2..S31 (30 stirrups of one each) is unreadable as a schedule (plan uses 6c2 "VARIES"). | drawing | M | open | PARTLY: plan marks appear in the LOCATION column ('CAP 11A1 TOP'), the MARK column still A1/B1/S1 |
| 6.10 | Bend diagrams (B1/B2/C1..C4) have dimension text overprinted at the short legs ("A = 99 13/16"" on top of "B"), and leader text "R = 1 1/4" (INSIDE)" exceeds its cell on S2..S18 (spills past the page margin). | drawing | M | open (bar sheets are excluded from the label placer) | still open: bend-diagram dimension text collides (Ramp G U3 S-53: 32 overlapping text pairs); tables start left of the border and LOCATION is cut with '...' |

## 7. Superstructure bar list sheets (S-24..S-29) <-> plan slab bar list (I-80 PDF 196, Ramp G PDF ~141)

| # | Finding | Cat. | Sev. | Status 2026-10-10 | Re-check 2026-10-11 |
|---|---|---|---|---|---|
| 7.1 | Mark system T1..T78 (192 + 40 per size, 1" length steps), B, L, N marks; plan marks 7a1..7a24, 6b1..6b8, 5d (end dam diaphragm), 5e, ... with SLAB TRANSVERSE TOP/BOTTOM counts 2,070 (U2, 56'-4 / 50'-7 ...). Sizes differ (#5 vs #7 transverse, #4/#5 vs #6 longitudinal), see 2.1. Model total 735,869 lb vs plan 1,204,071 lb. | model | H | open (model) | FIXED in the model (1,204,684 vs 1,204,071); bar sheet S-72 uses the plan marks (6b1 6469 x 39'-10", 7a1 2070 x 56'-4", 7a12 2070 x 50'-7" = plan list) |
| 7.2 | Plan also lists END DAM DIAPH. bars (5d, 5e), slab at drains (5a23), inserts (5a24), concrete placement quantities by section (U2 2,932 CY). Model sheets list negative-moment "N" bars at piers which the plan does not have (continuous #6 top+bottom longitudinal). | model/missing | M | open | open |
| 7.3 | Slab quantities by placement section (concrete CY per pour) and "SUMMARY" are not produced. | missing | M | PARTLY: estimated quantities table (model quantities) | PARTLY |

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

| # | Finding | Cat. | Sev. | Status 2026-10-10 | Re-check 2026-10-11 |
|---|---|---|---|---|---|
| D1 | **`sbm sheets` and `sbm drawings` crash for "Ramp G Unit 1"**: `InvalidOperationException: Sequence contains no matching element` at `SectionViews.cs` line 118 (`det.Members.First(m => m.Code is "TC" or "DIA")` for the bay-0 cross frame). That frame detail has no top chord/diaphragm member (likely a W strut / WT or abutment-type frame with a bottom chord only). The whole unit's sheet set and DXF set fail. Use `FirstOrDefault` and skip the leader or take any member. Not seen in the other 8 units. | drawing | H | FIXED: guard in SectionViews; Ramp G Unit 1 now produces 52 sheets | FIXED and re-verified: Ramp G Unit 1 -> 58 sheets (and 12 DXF views, no crash). |
| D2 | `sbm sheets` without a bridge name builds all bridges? (not tested). Output file names for `drawings` are `<bridge> - Pier6A.dxf` (support names without spaces): fine. | - | - | - | not retested |
| D3 | Per-pier DXF files are 1.7-3.0 MB (rebar bars as polylines) vs 0.3 MB for typical section: acceptable, but the Pier DXF includes both 3D-derived rebar and cap: check AutoCAD load speed. | - | L | - | - |

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

## 13. New findings on I-80 WB / Ramp G (2026-10-11)

Today's changes (girder end daps, pier drains, sign brackets, light pole bases, wingwalls, NURBS-derived meshes) broke no sheet. Wingwalls and the 34-pile abutment layout improved the abutment sheet (6.5); NURBS-derived column/footing geometry draws correctly (96 x 54 columns, footing 961.28 on Pier 6WB). None of the new items is documented on a sheet (N6).

| # | Sheet | Finding | Category | Sev. | Plan ref. | Suggested fix |
|---|---|---|---|---|---|---|
| N1 | All sheets (steel) | Feet-inch strings with 0 whole inches drop the zero: '10'-13/16"', '3'-7/16"', '112'-15/16"' (I-80 U2 S-2, S-57, S-64). Plans print 10'-0 13/16". Ambiguous for a reader (10'-13/16" can be read as 10'-1 3/16"). | content | M | all plan dimensions | In the length formatter print '0' before a bare fraction when feet > 0 (ft'-0 n/d"). |
| N2 | I-80 U2 S-1; Ramp G U3 S-1 | Layout elevation: pile-tip and splice leaders still overlap; 'PILES, TIP EL 902.010' / '904.841' run past the right border. | appearance | M | I-80 PDF 101 | Clamp label extents to the drawing frame in LabelPlacer; push labels left. |
| N3 | S-64 (piers, I-80 U2) | Bent elevation: bar leader labels (BARS T/R1/M2/E1/S2) overprint each other; '109'-4 15/16" CAP (ALONG SKEW)' collides with the '21 PILES ... TIP EL' note. | appearance | M | I-80 PDF 43 | Place the bar labels in a column with leaders; move the pile note. |
| N4 | S-64 Section A-A (all piers) | Section A-A is still a generic rectangle with '7 #11 top / 7 #11 bottom / 2 #6 each face / S1 #6 @ 6"' although the bar list now holds the plan cap bars (60 #11 top, 48 bottom, 6a/6c/5c ...). Sheet and bar list disagree. | content | H | I-80 PDF 43-45 (Sections A-A, B-B) | Draw the section from the placed bars (cut the cap bars at the section) and take the legend from them. |
| N5 | All steel bar-list sheets | Table runs 8 pt left of the border and LOCATION text is truncated with '...'; bend diagrams have overprinted dimension text on every bent mark (Ramp G U3 S-53/55/57/59/61: 31-32 text overlaps per sheet). | appearance | M | I-80 PDF 196; pier sheets | Start tables at the border margin, wrap LOCATION, run the placer on bend diagrams. |
| N6 | All units | Not on any sheet although modeled today: girder end daps, pier drains, sign brackets, light pole bases (no text on any sheet; only 'STRUCTURAL STEEL - INSPECTION ACCESS 768,916 lb' appears in S-71). Wingwalls appear only on the abutment piling plan. | missing | M | I-80 PDF drainage / sign / lighting sheets; girder end 112 | Add dap detail to the girder-end view, drain/bracket/light-base tables and a miscellaneous-items sheet. |
| N7 | S-9..S-13 (I-80 U2) | Plate-size labels still overprint at every pier zone (see 3.1); worst on G3-G6 (13-17 overlapping pairs). | appearance | H | I-80 PDF 112-114 | Stack plate labels in two rows with leaders at 1" = 100' or split the sheet at the pier. |
| N8 | S-15 / S-10 (Ramp G U1) | Steel note 5 says '7/8" DIA ... (FRAME BOLT NOTES GOVERN)' while the splice sheet says A325 7/8 and I-80 frames use 1": reader has to reconcile. | content | L | I-80 PDF 94 | Print one bolt table (frame, splice, stud). |
| N9 | Quantities S-71 | Deck + haunch 2,831.6 CY vs plan U2 2,932 CY (-3.4%); barrier 312.1 CY not comparable (F-shape vs Iowa rail); substructure reinforcing 754,893 lb cannot be tied to plan sheets (plan gives per pier). | content | L | I-80 PDF 189 | Print per-pier totals and the plan quantity next to each row. |
| N10 | All sheets | Title block lacks county, design/project number, designer/checker, revision block; unit title is 'I-80 WB AND RAMP G' (project) not the plan's 'Design 116'. | form | L | title block | Add project metadata fields. |
| N11 | I-80 U2 S-1 (see 1.4) | Bridge length changed by -11 7/8" since 10/10 without a recorded model change. | content | M | I-80 PDF 101 | Confirm the deck-end definition; print the CL pier to CL pier length too. |

## 14. Woodbury I-29 NB over Floyd Blvd (Iowa DOT Design 817, 3 spans BTD55 / BTD105 / BTD60, integral abutments)

19 sheets generated. Plan: 48 PDF pages (`woodbury-i29-floyd/INDEX.md`, PDF page p = design sheet p-2).

### 14.1 Sheet mapping

| Generated | Title | Plan page(s) | Match quality |
|---|---|---|---|
| S-1 | Bridge layout | p.6 situation plan / longitudinal section, p.7 staking | partial (no profile, curve data, clearance) |
| S-2 | Typical section at 463+72.83 | p.18 (near abutment), p.19 (at pier) | partial: no barrier, no pier section |
| S-3 | Prestressed girder details (BTD105) | p.25 beam data, p.27-30 BTD details | partial: section + strands only |
| S-4 | Girder spacing at supports | p.21 beam layout (dimension strings) | values match (8'-1 7/8) |
| S-5 | Bearing data | p.25 beam information, p.20 | empty bearing columns |
| S-6 | Cross slope | p.6 | table only |
| S-7, S-8 | Top of slab, top of web | p.33 top of deck, p.34 haunch data | quarter points vs plan points |
| S-9, S-10 | Pier 1 / 2 cap plan and piles | p.9 cap plan, p.11 footing layout | partial |
| S-11, S-14 | S. / N. abutment | p.15, p.16, p.17 | partial (no reinforcing, sections) |
| S-12, S-13 | Pier 1 / 2 bent | p.9, p.10 | partial |
| S-15 | Estimated quantities | p.3, p.4 | different content |
| S-16, S-17 | Bar list S. / N. abutment (pile spiral only) | p.15-17, p.26 | nearly empty |
| S-18, S-19 | Bar list Pier 1 / 2 | p.12 | epoxy total matches |

Plan sheets with no generated counterpart: p.1 title, p.2 revision, p.5 general notes, p.8 staging, p.13-14 pier aesthetics, p.21 beam layout, p.22-23 deck plan and transverse deck reinforcing, p.24 camber / deck thickness, p.26 deck / abutment / diaphragm quantities, p.27-30 BTD beam sheets, p.31-32 steel diaphragms, p.33-34 top of deck / haunch (partly S-7/S-8), p.35-37 lighting, p.38-39 barrier rail, p.40-44 slope protection, subdrain, backfill, texture, p.45-48 soils / packaging. Generated sheets with no plan counterpart: S-4, S-5 (as tables), S-6 (cross slope table).

### 14.2 Findings

| # | Sheet | Finding | Category | Sev. | Plan ref. | Suggested fix |
|---|---|---|---|---|---|---|
| W1 | S-1 | Layout: bridge length printed '227'-15/16"' (should read 227'-0 15/16"); plan 226'-11 3/4" along CL NB roadway (model along CL I-29, different reference, 1.2" apart). Plan p.6 gives CL pier stations 463+95.88 etc.; sheet gives CL BRG stations. | content | L | p.6, p.7, p.22 | Print both CL pier and CL bearing stations and state the reference line. |
| W2 | S-1 | Whole sheet is a postage stamp: plan strip 1" = 80', elevation axis 940-1200 for a 5 ft deep bridge; about 60% blank. Labels (pile tips, 'G1 (BEYOND)') overlap each other and the section at the piers. | appearance | M | p.6, p.20 | Use 1" = 40' for 224 ft; crop the ground/pile range; separate pile labels. |
| W3 | S-1 | Elevation shows no grade/profile data (PVI 462+50.00, 1124.11, VC 700', +1.9791/-0.6143), no clearance 20.52', no Floyd Blvd underpass, no superelevation diagram; plan p.6 has all of these. | missing | M | p.6 | Add profile grade line, VPI data and vertical clearance to S-1 from the alignment. |
| W4 | S-2 | Typical section has NO barrier (plan: Iowa aesthetic barrier 1'-7 x 3'-8, east edge) and no integral abutment/diaphragm section; deck drawn as a flat slab with sloped top (plan: 8" + haunch, deck bottom follows the beams, 8 3/4" edge, 3/4" drip groove). | content | H | p.18, p.19, p.38 | Draw the barrier from the rail definition, deck edge detail and haunch dimensions; add a second section at the pier. |
| W5 | S-2 | Annotation floats outside the deck: 'CL I-29' line, 'PGL EL 1122.399' and two 3.296% arrows sit 3-10 ft left of the deck with no tie to the geometry; spacing '8'-5/16"' prints without the 0 inches (8'-0 5/16"). | appearance | M | p.18 | Anchor the notes to the deck; fix the length formatter. |
| W6 | S-2 / S-3 | Beam called 'TxDOT BTD (IGD)' / notes cite 'TxDOT standard IGD / IGND', 'Class H concrete', 'TxDOT IGEB'; the source is the Iowa BTD standard (Iowa DOT 4730/4732/4742). | content | M | p.27-30 | Print the StandardBeamCatalog key and its agency; make the notes data driven (agency, standard number). |
| W7 | S-3 | Girder notes: 'f'c = 5.0 ksi, f'ci = 4.5 ksi' for the shown BTD105; plan BTD105 is f'ci 6.0 / f'c 7.0 (BTD55/60 4.5/5.0). Bearing note 'Elastomeric pads 9" x 28 1/2" x 1"' vs plan plain neoprene 9 x 1 x 2'-4 1/2 (pier) and 3x3 bar at abutments. | content | M | p.25, p.27, p.30, p.20 | Take f'c/f'ci per beam type and the pad size per support from the model. |
| W8 | S-3 | Section data: model Ix = 284,901 in4 vs printed 285,860 (0.3%), A 748 vs 748.8; 'HOLD-DOWNS L/20 EACH SIDE OF CL (5'-0 MIN)' contradicts the drawn '21'-0 BETWEEN HOLD-DOWNS' (plan 10'-6 each side). Only BTD105 is drawn; BTD55 and BTD60 patterns (14 straight; 12 + 2) are only in the schedule. | content | M | p.28-30 | Print the printed section properties; draw one beam per type; fix the hold-down note. |
| W9 | S-3 | No beam detail content: no stirrups/bars (4c1, 5b1, 4h1, 6b3..), no top-flange bar layout, no sections A-A..C-C, no beam data table (initial prestress 596/596/1446 kips, camber 0.37/1.95/0.39 in, weight 22/42/24 t), no lifting loops, no strand table for BTD55/60. | missing | H | p.25-30 | Add a beam data sheet per type from the model's strand and stirrup definitions. |
| W10 | S-3 | Girder elevation text collides: 'END OF GIRDER, 4" FROM CL BENT' / '8" TO CL BRG' leaders overprint each other at both ends; 'e = 21"' overprints the strand note. | appearance | M | p.30 | Stagger end-of-girder notes. |
| W11 | (missing sheet) | Framing / beam layout: steel bridges get framing plan sheets, prestressed ones get none. Plan p.21 'Beam layout' (BTD55/105/60 spans, beam lines D-H, W.P. offsets, spacing 4 @ 8'-0 5/16 / 7'-11 13/16 / 7'-11, skews to local tangent, steel diaphragm lines). | missing sheet | H | p.21 | Add a PrestressedViews beam layout (chorded per span) with beam marks, spacing, W.P. offsets and diaphragm lines. |
| W12 | (missing sheet) | Steel intermediate diaphragms are modeled (quantity 3,937 lb, plan 4,258 lb, -7.5%) but there is no drawing: plan p.31-32 (C15x33.9, W21x50, W14x38, bolt-hole chains, 7/8" A449). | missing sheet | M | p.31, p.32, std 1036-1-BTD | Add a diaphragm detail sheet from PsSteelDiaphragmModel (hole chains, member list, bays). |
| W13 | (missing sheet) | No deck sheets: deck plan / placement diagram (5 pours, 305.6 CY), transverse deck reinforcing, deck bar list (77,253 lb epoxy), barrier rail details, beam camber / deck thickness at beams, top-of-deck / haunch data tables, superstructure and general notes. | missing sheet | H | p.3-5, p.22-24, p.26, p.33-34, p.38-39 | Deck rebar and barrier are not modeled for prestressed; add them, then the tables. Haunch and top-of-deck data exist (check-woodbury 216/216). |
| W14 | S-4 | Girder spacing table: 8'-1 7/8" at every support line matches the plan (4 @ 8'-1 7/8"); skews are to the radial of CL I-29 (10 deg 31'21" / 13 deg 09'54" / 14 deg 40'18") while the plan prints 10 deg 24'01" / 13 deg 00'40" / 14 deg 29'57" to the local tangent of CL NB roadway. | content | L | p.21 | State the skew reference (radial vs local tangent of the roadway) and print both. |
| W15 | S-5 | Bearing data: BEARING / FIXITY / TOP OF BEARING / STACK HEIGHT columns are all '-' for every row (table built for steel). Bottom of beam values agree with plan seat + bearing height (S abut D 1117.750 = 1117.50 + 3"; pier 1 D 1118.031 vs 1117.94 + 1" = 1118.023). | content | M | p.25 beam information, p.20 | Fill bearing type (neoprene pad / bar), pad height, top of bearing; add the plan seat elevations. |
| W16 | S-6 | Cross slope sheet: two rows (2%, 5%) on a blank page; plan p.6 gives the same transition with a diagram. | form | L | p.6 | Merge into S-1 or the typical section sheet. |
| W17 | S-7 / S-8 | Top of slab / top of web agree with plan p.33 (G1 S abut 1123.000 vs plan west edge 1122.92 + 3'-2" x ~2%); but locations are 25/50/75% points; the plan lists numbered equal-spaced points (216 values), so values cannot be cross-checked line by line. Pages are 80% blank. | content | L | p.33, p.34 | Emit the plan's equal-spaced points (and haunch elevations at beam lines). |
| W18 | S-9 / S-10 | Cap plan and footing: cap 41'-9 5/8" (plan 42'-1 5/8" incl. 4" end reveal); each pedestal labelled 'G1 G1', 'G2 G2' (back and ahead pads indistinguishable); pile rows at 39"/42"/39" (outer rows 5'-0 from CL pier) vs plan 1'-6/3'-6/1'-9/1'-9/3'-6/1'-6 (outer rows 5'-3 from CL); footing size 10'-0 x 13'-6 and 12 HP12x84 match; tip EL 966.000 here vs 969.732 on the bent sheets S-12/S-13 for the same piles. | content | H | p.9, p.11, p.12 | Fix the pile row offsets; label back/ahead pads; use one tip elevation (plan: 125 ft contract length, friction piles). |
| W19 | S-9 / S-10 | Cap plan lacks plan content: reinforcing plan, sections A-A..D-D, W.P. and beam-line offsets (3'-3 1/2, 2'-10 15/16 ...), column CL dimension 2'-11 13/16, maskwall, 8f6 uplift anchors (circled piles). | missing | M | p.9-11 | Add the cap reinforcing plan and uplift anchor marks. |
| W20 | S-11 / S-14 | Abutments: 'BEAM STEP TOPS:' heading printed with no values; 'Normal to the support line at -;' station missing; section is a 3'-0 x 48" block (no diaphragm/beam end embed, keyway, 3" bearing bar), no wing elevation, rebar, A-A..C-C sections, pile notes (155/150 ft), step diagram. Footing bottom 1113.710/1114.600 (S) and 1115.640/1113.910 (N) match the plan; 8 piles at 5'-7" (S) / 5'-9" (N) match. | content | H | p.15-17, p.42 | Fill the empty fields; build the integral abutment drawing (backwall/diaphragm, wing extension, step diagram). |
| W21 | S-11 / S-14 | Pile layout: wing pile drawn detached below the row with no dimension; end spacings missing; sheet title 'S. ABUT. ABUTMENT' (duplicate word). | appearance | L | p.17 | Dimension from the abutment end; fix title. |
| W22 | S-12 / S-13 | Bent elevation: bar labels 'BARS F1 (#9)' and 'BARS U (#8)' overprint, 'BARS A1 (#10)' sits on the cap; pile note collides with the cap dimension; section A-A is a 7 #10 / 7 #10 / 2 #5 generic cage (plan 10a1/10a2/10b1/10b2, 6d, 5c1 ...). Seats (1117.942 / 1118.237 ..) and cap tops (1117.250 / 1118.748; plan 1117.25 / 1118.76) agree within 0.02 ft. Scales differ between the two piers (1/8" vs 1" = 10'). | content | M | p.9-11 | Use the same scale; draw the cap section from the placed bars. |
| W23 | S-15 | Quantities: reinforcing 54,907 lb substructure only (plan 161,585 lb: 139,094 epoxy + 10,618 stainless + 11,873 plain); no deck/beam/barrier steel, no beam concrete or strand; deck+haunch 229.8 CY vs plan structural concrete 187.6 + HPC 478.0 CY (bridge items); no beams (15 BTD), neoprene, barrier 236.8 LF; steel 3,937 vs 4,258 lb. | content | H | p.3, p.4 | Add prestressed beam, strand, deck rebar and barrier rows; show the plan item next to each row. |
| W24 | S-16 .. S-19 | Bar lists: Pier 1 total 27,317 lb vs plan epoxy 27,324 (stainless 1,639 and plain 5,854 not modeled; plan total 34,817); abutment list holds only the pile spiral (51 lb; plan footing/diaphragm/wing bars 8g/5d/5k ...). Mark column truncated 'pile sp...'; location text clipped; bend diagram dimension text overprints (A1). | content | H | p.12, p.15, p.16, p.26 | Model abutment and pier stainless/plain marks; wrap mark/location text; avoid text overprint in bends. |
| W25 | All sheets | Title block PROJECT = 'UNTITLED' (model has no project name); no county / design no. 817 / file no. | form | L | title block | Take project/design/county from the project info. |

## 15. Br02069 TH65 SB over 117th Ave (MnDOT, one 99'-3" span, seven 30MH beams)

10 sheets generated. Plan: 39 PDF pages (`br02069-th65/INDEX.md`, PDF page = sheet).

### 15.1 Sheet mapping

| Generated | Title | Plan sheet(s) | Match quality |
|---|---|---|---|
| S-1 | Bridge layout | 1 General plan and elevation, 3 Bridge layout | partial; stations / lengths agree |
| S-2 | Typical section at 248+84.37 | 2 Transverse section | geometry agrees; no dims, notes, SOQ |
| S-3 | Prestressed girder details | 16 30MH beam, 17 diaphragm connection (std) | geometry agrees; no bars / debonding / camber |
| S-4 | Girder spacing | 3 (W.P. table) | agrees |
| S-5 | Bearing data | 3 seat table, 29-32 bearing details | columns empty |
| S-6, S-7 | Top of slab / top of web | 3 (W.P. elevations) | agrees to 0.01 ft at W.P. A, B, E, F |
| S-8, S-9 | South / North abutment | 4-8 S abutment, 9-14 N abutment, 11 | partial: footing / piles agree |
| S-10 | Estimated quantities | 2 (SOQ), 4 / 9 quantities | different items |

Plan sheets with no generated counterpart: 2 (notes, SOQ), 6-8 and 12-14 abutment reinforcement, 15 framing plan, 17 diaphragm connection, 18-21 superstructure details and bar list, 22-25 barrier layout / pipe railing / barrier standards, 26 lighting, 27 approach panel cover plates, 28 slope paving, 29-32 bridge details (bearings, sole plate), 33 as-built data; 34-39 survey, profiles, alignments, topography, borings (not bridge-model sheets). Generated sheets without a plan counterpart: S-4, S-5, S-6, S-7 (data tables).

### 15.2 Findings

| # | Sheet | Finding | Category | Sev. | Plan ref. | Suggested fix |
|---|---|---|---|---|---|---|
| B1 | S-1 | Layout: stations (begin 248+32.92, end 249+35.83, CL BRG 248+34.75 / 249+34.00), 99'-3" span, 102'-11" length, pile tips 823.750 / 828.750 are consistent with the plan; plan view has a blank interior (no working points, roadway under, MSE walls, retaining walls 35/36, clearance), elevation axis 810-990 for a 6 ft bridge, 7 text overlaps. | content | M | p.1, p.3 | Add W.P. A-F and offsets, profile grade, clearance and the MSE walls; crop the elevation vertical range. |
| B2 | S-2 | Typical section agrees: 7 beams @ 6'-0", overhangs 2'-6", 41'-0" o-o, 9" slab, 2% crown, barrier Type S 36". Missing: roadway dims (10' shoulder, 12' lanes, 38'-0 roadway), 18'-0 future widening, deck reinforcing; haunch note '4 1/4"' overprints G2; note 'TxDOT 30MH (IGD)'. | content | M | p.2 | Add roadway dimensions and reinforcement; label as MnDOT 30MH. |
| B3 | (missing sheet) | Plan sheet 2 content (construction notes, schedule of quantities, 14 pay items) has no counterpart. | missing sheet | M | p.2 | Add a general-notes + quantities sheet (partly available from S-10). |
| B4 | S-3 | Beam: 30MH depth 30", top flange 34", bottom 39", 44 strands (18/18/8 at 2/4/6"), 99'-3" CL-CL, 100'-6" overall, 7 1/2" end distance, f'ci 8.0 / f'c 9.5 ksi all agree. Missing: end debonding table (14 strands, 4 @ 9', 6 @ 13', 4 @ 17'), stirrups/bars G301E..G808E, camber diagram (7 3/8" erection, 5 1/4" DL), initial prestress 2,062,370 lb (0.72 fpu), losses, minimum concrete strength block, top strand option, overhang support sketch, sole plate. | missing | H | p.16 | Draw debonding (StrandPoint.DebondStart/End), stirrup layout, camber diagram and prestress/losses data. |
| B5 | S-3 | Notes are TxDOT/generic: 'Class H concrete', 'Depressed strands from the A positions, 2" spacing at the ends' (no depressed strands on this beam), 'Elastomeric pads 12" x 30" x 3 1/4" (verify with TxDOT IGEB)' vs plan curved-plate bearings (B310/B311), 'camber is not modeled'. Section outline simplified (no 6" R fillets); 'e' labels overprint 'C.G. STRANDS'; end notes overprint. | content | M | p.16, p.29-32 | Notes from the model data (agency, bearing type); use the exact profile (SectionProfile) in the section. |
| B6 | (missing sheet) | Framing plan (plan p.15): beam marks, 'X' end marking, diaphragm positions: no generated counterpart. | missing sheet | H | p.15 | Add the prestressed beam framing plan. |
| B7 | (missing sheet) | Beam diaphragm connection details (p.17), deck details 1-4 and slab bar list (p.18-21), barrier layout/railing/standard barriers (p.22-25, 27), underpass lighting (p.26), slope paving (p.28), bridge details 1-4 (bearings B310/B311, sole plate B303, p.29-32): none generated. The model has no concrete end diaphragm. | missing sheet | H | p.17-32 | Priority: deck/slab reinforcement + bar list (4 sheets), end diaphragm detail, bearing/sole plate details. |
| B8 | S-4 | Girder spacing table: 6'-0 at both abutments (plan 6'-0); skew 0; a stray '0' cell appears in the extracted text for the north abutment row. | content | L | p.3 | Remove stray '0'; print 'SKEW 0 deg'. |
| B9 | S-5 | Bearing table: bearing, fixity, top-of-bearing and stack height columns '-' everywhere. Plan: fixed S / expansion N curved-plate bearings, top of roadway to bridge seat table (S abut 46 1/2" = 3.88', N abut 49 3/4" = 4.15'), bearing height 3 3/4" S / 6 1/2" N. Model bottom of beam G1 S abut 924.032 vs plan seat 923.76 + 3 3/4" = 924.073 (-0.04 ft; pad 3 1/4" modeled). | content | M | p.3 (seat table), p.29-32 | Model the curved-plate bearing heights; fill fixity (fixed S / expansion N). |
| B10 | S-6 / S-7 | Top of slab and top of web agree with the plan working-point table (A 927.64, B 927.70, E 927.76, F 927.82; model G1 927.636 / 927.698, G7 927.756 / 927.818). Plan prints C 928.06 / D 928.12 at CL; the sheet has no CL column (G4 at 3' LT = 927.996). Large blank pages. | content | L | p.3 | Add CL / working-point columns; combine S-6 and S-7. |
| B11 | S-8 / S-9 | Abutments: footing 59'-7", 14 piles in 2 rows @ 9'-5", bottom EL 917.750, 36" footing depth, 72" width agree with the plan. Missing: pile end dims (1'-7 / 1'-6), test pile, stem/back wall/ledge/MSE wall tie-in, wing, abutment bars, pile loads. 'BEAM STEP TOPS' line runs to the right margin; 'FUTURE BEAM' labels overprint G7; title 'SOUTH ABUTMENT ABUTMENT'. | content | H | p.4-14 | Add the stem/ledge detail from AbutmentDefinition and dimension from the abutment end; fix title and label placement. |
| B12 | (missing sheet) | No abutment reinforcement sheets: plan has 6 sheets (S reinforcement 1-3, N reinforcement 1-3; A and B bent marks) and abutment bar lists (7,200 lb epoxy + 480 lb chromium per abutment); the generated set has no bar list at all. | missing sheet | H | p.6-8, p.12-14, p.21 | Place abutment bars (PlanBarGroup) and emit bar list sheets. |
| B13 | S-10 | Quantities: deck 117.2 CY = plan slab 4,220 sq ft x 9" (agrees); haunch 28.5 CY; barrier 22.8 CY (plan 273 LF); substructure concrete 249.2 CY vs plan structural concrete 140 CY (71 + 69), +78%; no reinforcing (plan 19,200 + 19,970 + 250 lb), no beams (704 LF), piling (2,405 LF), bearings (14), pipe rail (36 LF). | content | H | p.2, p.4 | Check the substructure volume (stem + footing, diaphragm in deck?) and add rows with the plan SOQ item numbers. |
| B14 | All sheets | Title block shows 'BR02069 TH65 SB'; the plan has bridge no. 02069, S.P. 0208-02069, Anoka County, City of Blaine, designer/checker: none printed. | form | L | title block | Add project fields (bridge number, S.P., county). |

## 16. Method and limits of the 2026-10-11 re-check

* CLI: `dotnet build src/SBM.Cli -c Release` on this branch; model COPIES in `temp\sr`; PDFs rendered with PyMuPDF at 80-150 dpi and compared on screen with the plan pages named in the tables; text overlap counts by script (pairs of text spans overlapping > 25% of the smaller span; approximate).
* Numbers come from the sheet text and the plan data files (`docs/plan-data/*/SUMMARY.md`, `*.csv`) and plan pages as printed. Items marked 'not checked' were not re-derived.
* `sbm compare-sheets` / `sbm qc-sheet` were not run (they need split plan folders and a companion db); side-by-side excerpts were made by hand (not committed).
* Not reviewed individually: I-80 U1/U3 and Ramp G U2-U4 sheets beyond sheet lists and automated overlap counts; DXF output beyond 'generates without crash'.

## 17. Fixes on branch sheet-fixes (2026-10-12)

Branch `sheet-fixes` (from main adff49f, main cb41e91 merged in for the prestressed beam bars). Every item was checked on sheets generated
with `sbm sheets` from COPIES of the shipped models (Release CLI from the branch) and rendered with PyMuPDF at 100-150 dpi; the "before"
column is the same CLI built from adff49f. Text overlap pairs / off-border spans are counted by the section-16 script (spans overlapping
> 25 % of the smaller one; spans crossing the sheet border).

| Bridge | Sheets before -> after | Overlap pairs before -> after | Off-border spans before -> after | Sheets with fit warnings before -> after |
|---|---|---|---|---|
| I-80 WB Unit 1 / 2 / 3 | 63 / 85 / 60 -> 66 / 89 / 63 | 133 / 223 / 95 -> 41 / 99 / 33 | 241 / 283 / 200 -> 0 / 0 / 0 | 7 / 10 / 6 -> 1 / 1 / 1 (the old intermediate cross frame panel) |
| Ramp G Unit 1 / 2 / 3 / 4 | 58 / 50 / 62 / 52 -> 61 / 53 / 65 / 55 | 153 / 144 / 264 / 131 -> 48 / 35 / 69 / 41 | 155 / 33 / 49 / 92 -> 0 | 9 / 6 / 7 / 8 -> 0 |
| Woodbury | 19 -> 30 | 25 -> 7 | 38 -> 0 | - -> 0 |
| Br02069 | 10 -> 15 | 9 -> 1 | 0 -> 0 | - -> 0 |

Status of the top-10 list and the H items (fixed / partial / open with the reason):

| # | Item | Status | What changed / why open |
|---|---|---|---|
| T1 | Prestressed sheet sets (W9, W11-W13, B4, B6, B7, B12) | PARTIAL | New sheets: **beam layout** per span group (chorded beams with marks and types, back / ahead CL BRG lines with the beam spacing, CL piers, span and CL BRG-CL BRG lengths, steel diaphragm lines with marks, concrete diaphragm footprints), **deck plan** (outline, barriers, widths at the supports, area, volumes, reinforcing status), **one sheet per beam type** (sections at midspan / near the end with strands, debonded strands ringed and the beam bars cut at the section; elevation with strand profile, hold-downs, debonded lengths and the beam bars; beam data, debonding and beam bar tables), **schedule + notes**, **steel intermediate diaphragms** (true section of the two beams through a bay with the parts / bolts projected, one per member type; bay table; cast-hole chains per beam line), **concrete diaphragm** sheet per support (true sections at a beam and mid-bay: diaphragm, beams, cap / pile cap, pads, fillers, deck; part table). Beam bars are read generically (`PrestressedModel.BarMarks`, `PrestressedBeam.Bars`, by reflection): Woodbury BTD55/105/60 and Br02069 30MH show their bar lists and stirrups since the beam-rebar merge. OPEN (model): slab / deck bar list (no deck bars in the prestressed models; the bar-list sheet appears by itself when `ConcreteModel.Marks` exist), camber table (camber not in the model), abutment / diaphragm / barrier reinforcement (not modeled), barrier rail details. |
| T2 | Prestressed typical sections (W4, B2) | PARTIAL | Barrier drawn from the model (Woodbury Iowa aesthetic barrier, Br02069 single slope), labelled by its profile name; ROADWAY dimension (gutter line to gutter line); cross slope arrows on the deck; staged deck: the far CL is a note at the deck edge instead of a line 27 ft away (scale 1/8" -> 3/16"); beam callout "Iowa DOT BTD55" / "MnDOT 30MH-101". OPEN: deck steel (not modeled for prestressed), future widening (not modeled), deck bottom following the beams, second section at a pier. |
| T3 | Abutment shells (W20, B11) | FIXED (drawing) | Integral and prestressed-beam abutments: **true section along the CL bearing** (pile cap with its steps / footing and beam steps, diaphragm with the beams cut across, pads, deck, barriers, piles to 4 ft below the cap with pile type / length / tip note, step-top or seat elevation per beam line, cap bottom at both ends) + **section at a beam** + piling layout. No empty 'BEAM STEP TOPS:' or 'support line at -' (step list wraps; integral caps list `StepTop` per beam; section title omits the girder when there is none). Title 'S. ABUT.' (no duplicate word); a single wing pile is dimensioned from the nearest pile (W21). Stub abutments (I-80, Ramp G) also show wingwalls / counterforts on the rear elevation. OPEN (model): abutment reinforcement, wing elevations. |
| T4 | Quantity tables (W23, B13) | FIXED (grouping) / model gaps reported | Rows in plan item groups: superstructure (steel by kind, **steel diaphragms** separately, beams per type EACH + LF + CY, strand LF and debonded LF, pads by size, deck / haunch / barrier / diaphragm concrete), reinforcing **by coating and unit** with **NOT MODELED rows** for what the model lacks, substructure **structural concrete per unit without pile fill**, **piling** LF per pile type and the **pile fill** separately. **Br02069**: the 249.2 CY included 74.9 CY concrete fill of the 28 CIP pipe piles (part of the piling item) = category error, now under PILING; structural concrete 174.4 CY (88.2 + 86.2) vs plan 140 (71 + 69): the remaining +34 CY is model geometry: the footing is built up to the seat in front of the backwall (65.7 / 63.4 CY vs a 39.7 CY 59'-7 x 6'-0 x 3'-0 prism; MODEL.md item 11) - model issue, not a sheet issue. Piling 2,590 LF (plan 2,405 + 205 test piles). **Woodbury**: 54,907 lb = the two pier bar lists (27,317 + 27,487) + 102 lb pile spirals only; the plan's 161,585 lb also holds the deck / barrier bars (77,253 lb epoxy, p.26), the abutment / diaphragm bars, 10,618 lb stainless and 11,873 lb plain - listed as NOT MODELED rows; since the beam-rebar merge the beam bars add 13,213 lb epoxy + 17,048 lb plain (usually part of the beam item). Piling 11,440 LF = plan. |
| T5 | Pier cap Section A-A generic cage (N4, W22) | FIXED | Section A-A is its own view at its own scale (1/2" on I-80 Pier 6WB, was 1/16") and the legend counts the bars actually cut from the placed cap bars with the plan marks: Pier 6WB '20 #11 A1 (CAP 11A1 TOP), 40 #11 A2 (11A2 TOP), 48 #11 B1 (11B1 BOTTOM)', side bars, hoops 6C1 / ties 6C3 6C4 (plan 60 top / 48 bottom); Woodbury Pier 1 '8 + 6 #10 (10A1 / 10A2 TOP), 14 #10 (10B1 BOTTOM), 8 #5 (5A5)'. |
| T6 | Zero inches | FIXED on main before this branch | - |
| T7 | Agency / values in prestressed notes (W6, W7, B5) | FIXED | Notes, subtitles and callouts from the beam standard: 'Iowa DOT BTD ... (Iowa DOT std 4730-4733 ...)', 'MnDOT 30MH'; TxDOT IGD / IGND / IGEB / Class H only for Tx shapes; f'c / f'ci **per beam type** (BTD105 7.0 / 6.0, BTD55 and BTD60 5.0 / 4.5; 30MH 9.5 / 8.0); pads from the generated pads per support; hold-down note from the beam ('10'-6" each side of midspan'); printed section properties (748.8 in², 285,860 in⁴). |
| T8 | Woodbury pile layout / tip elevations (W18) | FIXED (drawing) | Pile plans are now at the bottom of the footing (the pile top is 1 ft up a 1:4 batter): outer rows 63 in from CL (plan 5'-3), rows 42 / 42 / 42 in; batter arrows with 'BATTER 1:4' instead of 30-ft tip lines (scale 1" = 10' -> 1/8"). The 966.000 / 969.732 'conflict' was two different piles: 125 ft plumb piles end at 966.000, 125 ft battered piles at 969.732; every sheet now prints 'TIP EL 966.000 (PLUMB), 969.732 (BATTERED)'. Pedestals labelled once ('G1 BK + AH'). OPEN (model): cap 41'-9 5/8 vs plan 42'-1 5/8 (4" end reveal). |
| T9 | Collisions (3.1, N2, N3, N5, N7, 6.10) | FIXED / PARTIAL | Label placer keeps labels inside the panel frame (no text off the border); girder elevation plate labels in rows with leaders (plate size only, top above / bottom below); bent bar labels in one column with leaders; pile note below the dimension rows; bending diagrams laid out per cell with the label placer (radius in the note line) and bar-list cells cut to the column width; layout sheet: numbered field-splice tags + one wrapped station list, elevation grid step by scale, girder names only where there is room. Remaining overlaps (see table) are mostly cross-frame marks at 1" = 80-100' and dense S-1 support labels. |
| T10 | Items modeled but not documented (N6, W15, B9) | FIXED | Girder end dap details (true-scale lowered flange, slope, bends, cope tabs, CL BRG, dims and the dap definition; one detail per dapped support), miscellaneous items location plan + schedule (deck / pier drains per inlet with their pipes, sign truss brackets, crash cushion end posts, pole bases, conduits; station / offset, description), wingwalls on the abutment rear elevation. Bearing data of prestressed bridges from the prestressed seats and pads (pad description, BACK / AHEAD rows at piers, fixity from the support definition or INTEGRAL, 'NOT MODELED' when the model uses its default); steel bearings 'BY MANUFACTURER' + FX / GE / MD. |
| 2.6 | Plan girder letters | FIXED for I-80 / Ramp G | Optional `GirderLayoutDefinition.PlanNames` (sheets only, omitted from the .sbm when null) via `ViewContext.GirderName`: typical sections, framing plans, girder elevations ('GIRDER K ELEVATION (G11)'), layout, tables, bents. Set for I-80 Units 1-3 (A-F, A-K, A-J) and Ramp G Units 1-4 (A-D) in the shipped model and the build scripts. OPEN: Woodbury beam lines D-H / Br02069: not set (the model files belong to the beam-rebar work; one line `Girders.PlanNames = new() { "D", "E", "F", "G", "H" }` in build-woodbury.csx). |
| DXF | DXF zip export (user request) | DONE | App Sheets tab 'Export sheets DXF (zip)...' (asks whether to add the 2D model DXFs under models/); `sbm sheets <model> <out.pdf> ["bridge"] --dxf <folder|file.zip> [--models]`; `sbm drawings <model> <folder|file.zip> ["bridge"] [--zip]`; API `ExportSheetsDxf`, `ExportDrawings(... .zip)`; docs/API.md. |
| W8 | One beam per type, hold-down note | FIXED | see T1 / T7 |
| W10, W21, B8 | End notes, wing pile, skew '0' | FIXED | end notes point outward; 'SKEW 0 DEG (SQUARE)' |
| W24 | Bar lists | PARTIAL | cells cut by width, diagrams clean; abutment / stainless / plain marks not modeled |
| N1 | Zero inches | FIXED (main) | - |
| N8-N11, W1-W3, W14, W16, W17, W19, W25, B1, B3, B10, B14 | M / L items | OPEN | not in this pass (title block fields, profile / clearance on S-1, skew reference, equal-spaced elevation points, cap reinforcing plan) |
