# Northaven (NHT) generated sheets: status vs the plan set (2026-10-12)

Read-only status check. Nothing in `src`, `samples` or the model was changed; a COPY of `samples/plans/Northaven NHT.sbm` was used.
Plans: `0047-07-228 NHT 100% Plans_09-04-2020_comments_Reduced.pdf` (raster, no text layer), bridge sheets PDF p.83-155 (sheets 134-205,
p.130 duplicates p.129 -> 73 pages / 72 unique sheets). Index: `docs/plan-data/nht-0047-07-228/INDEX.md`. Method as in `sheets-vs-plans.md`
(PyMuPDF renders at 100 dpi of the generated and the plan pages; numbers read on the raster; text-overlap pairs counted on the generated
PDFs' horizontal text lines).

## 1. Generation

CLI `dist\SteelBridgeModeler\sbm.exe`, `sbm sheets m.sbm out.pdf "<bridge>"` per bridge (bridge names listed with a small `sbm run` script).
**No crashes, no warnings** (stdout only prints the sheet count); 35-66 ms per PDF. 11x17 landscape, vector, with text.

| Bridge | Sheets | Titles |
|---|---|---|
| NHT Unit 1 (spans 1-3, Abut 1 - Pier 4) | 19 | Bridge layout; Typical section; Beam layout spans 1-3; Deck plan; Prestressed beam Tx28 (8 strands); Prestressed beam schedule; Girder spacing at supports; Bearing data; Cross slope geometrics; Top of slab elevations; Top of web elevations; Pier 2/3/4 cap plan and piles (3); Abutment 1 abutment; Pier 2/3/4 bent (3); Estimated quantities |
| NHT Unit 2 (spans 4-6, Pier 4 - 7) | 17 | same families, no cross-slope sheet; Pier 5/6/7 cap plans and bents (no Pier 4 sheet: drawn in Unit 1) |
| NHT Unit 3 (spans 7-9, Pier 7 - 10) | 19 | same; three beam-type sheets (Tx28 14 / 10 / 28 strands); Pier 8/9/10 cap plans and bents |
| NHT Unit 4 (spans 11-12, Pier 11 - 13) | 18 | same; two beam types (20 / 12 strands); Pier 11/12/13 cap plans and bents |
| NHT Unit 5 (spans 13-15, Pier 13 - Abut 16) | 16 | same; one beam type (12 strands); Pier 14/15 cap plans and bents, Abutment 16 |
| Arch span 10 (Pier 10 - 11) | 9 | Bridge layout; Typical section; Arch elevation; Arch framing plan; Girder spacing at supports; Top of slab elevations; Top of web elevations; Estimated quantities; Bar list superstructure |

98 generated sheets (5 approach units 89, arch 9). Automated text-overlap pairs (approximate, horizontal text only): Unit 1 29, Unit 2 34,
Unit 3 44, Unit 4 39, Unit 5 26, Arch 4. The worst sheets are the beam layouts (5-7 pairs), the bridge layouts (1-6) and the bent / cap plans (2-4 each).

## 2. Plan sheet -> generated sheet map (plan p.83-155)

Tier A = counterpart with the right content (form simplified); tier B = counterpart that is partial or wrong; none = no generated sheet.

| Plan p. (sheet) | Plan title | Generated counterpart | Tier |
|---|---|---|---|
| 83 (134) | Bridge general notes and abbreviations | none (only per-sheet notes on the beam schedule) | none |
| 84-88 (135-139) | Bridge layout 1-5 (plan, elevation) | S-1 Bridge layout of each unit (developed straight plan + elevation); arch S-1 | B |
| 89 (140) | Typical sections approach spans (2 types: low rail, high rail) | S-2 Typical section per unit (one type, no rail) | A |
| 90 (141) | Typical sections arch span | Arch S-2 (generic 43'-0" slab section, wrong) | B |
| 91 (142) | Summary of quantities + seat elevation tables | last sheet "Estimated quantities" per bridge + S-8 Bearing data | B |
| 92-93 (143-144) | Suggested erection sequence 1-2 | none | none |
| 94 (145) | Foundation layout and schedule | none (shaft tips only as labels on S-1) | none |
| 95 (146) | Abutment 1 details | Abutment 1 abutment (elevation + section, no rebar) | A |
| 96 (147) | Abutment 16 details | Abutment 16 abutment (Unit 5) | A |
| 97 (148) | Piers 2-5, 8, 9, 12, 13 details 1 of 2 | Pier n cap plan and piles + Pier n bent (2 sheets per pier) | A |
| 98 (149) | same, details 2 of 2 (drilled shafts) | none | none |
| 99 (150) | Piers 6 & 7, 14 & 15 details 1 of 2 | Pier 6/7/14/15 cap plan + bent | A |
| 100 (151) | same, details 2 of 2 | none | none |
| 101 (152) | Drilled shaft details arch piers | none | none |
| 102 (153) | Arch piers 10 & 11 elevations and sections | Pier 10 / Pier 11 cap plan + bent in Units 3 / 4 (no arch pedestals) | A |
| 103-106 (154-157) | Arch piers reinforcement, details, traffic barrier (2) | none | none |
| 107 (158) | Elastomeric bearings, arch span (2 of 2) | none (arch bearings are modeled, not drawn) | none |
| 108, 110, 112, 114, 116 (159, 161, 163, 165, 167) | Framing plan spans 1-3 / 4-6 / 7-9 / 11-12 / 13-15 | S-3 Beam layout of Units 1-5 | A |
| 109, 111, 113, 115, 117 (160, 162, 164, 166, 168) | Prestressed girder unit sheets (plan, slab bars, tables, typ. section, deflections) | S-4 Deck plan + S-2 typical (no bars, no tables) | B |
| 118 (169) | IGND girder designs (non-standard spans) | Prestressed beam Tx28 sheets + Beam schedule | A |
| 119-120 (170-171) | Arch span geometry and layout 1-2 | Arch S-3 Arch elevation (geometry only) | B |
| 121-122 (172-173) | Member force tables | none (design data, not a drawing sheet) | none |
| 123-126 (174-177) | Knuckle joint (4) | none | none |
| 127 (178) | Arch rib details | none | none |
| 128-142 (179-192) | Upper lateral bracing 1-14 (+ duplicate p.130) | none (dashed lines on the framing plan only) | none |
| 143 (193) | Arch span framing plan | Arch S-4 Arch framing plan | B |
| 144 (194) | Tie girder details (splice) | none | none |
| 145-146 (195-196) | Typical floorbeam details / connections | none | none |
| 147-150 (197-200) | Cable hanger geometry / anchorage (2) / connector | none | none |
| 151 (201) | Arch span deck plan | none | none |
| 152 (202) | Arch span deck section | Arch S-2 typical section (wrong content) | B |
| 153 (203) | Deck placement sequence | none | none |
| 154 (204) | Deck bar bends and table | Arch S-9 Bar list (counts differ, no bends) | B |
| 155 (205) | Deck post-tensioning | none | none |

**Coverage of the 73 plan bridge pages**: any counterpart 29 (**40 %**): tier A 13 (18 %), tier B 16 (22 %); no counterpart 44 (60 %).
For the arch span proper (p.107, 119-155 = 38 pages) 5 pages (13 %) have a counterpart, all tier B; the other 33 (rib, tie, hanger, floorbeam,
knuckle, bracing, bearing, deck plan, PT, placement) have none.

Generated sheets with **no plan counterpart**: Cross slope geometrics (Unit 1), Top of slab elevations and Top of web elevations (all bridges;
staking-type tables, values are consistent, e.g. Unit 1 PGL 526.327 at STA 20+73.24 = plan PGL 525.27 at 20+49.30 + 4.41 % x 23.94 ft),
Girder spacing at supports (the plan carries it as pier reports on the framing plans), Beam schedule, and the three EMPTY arch sheets S-5/S-6/S-7.

## 3. What matches the plan (checked on the raster)

Caps 15'-0" x 3'-6" (Piers 2-9, 12-15) and 24'-0" x 6'-0" (Piers 10/11); control elevations EL A-D at Piers 2, 3, 4, 6, 8, 10 and the other piers
spot-checked (e.g. Pier 10 cap top 533.219 / bottom 527.219 / shaft top 509.150); column heights H = 7, 8, 10, 12, 12, 14, 18, 19 (p.85); shaft diameters
36" / 48" / 78" and the Abutment 1 tip EL 488.153; columns 36x36 / 48x36 / 54x54; offset columns at Piers 6, 7, 14, 15 (11.282 ft from the cap end);
skews 19°34'41" at Piers 10/11; girder strand data, f'ci / f'c and "e" for all spans (IGND p.118: 8 / 14 (2 depressed) / 10 / 28 (4 depressed) / 20 (4 depressed) /
12 strands, 4.0-5.8 / 5.0-7.8 ksi); girder lengths CL BRG to CL BRG (46.307 / 45.473 / 30.575 / 25.662 ft, p.108) and girder LF per unit within 0.2 %
(213.3 vs 213.45; 322.8 vs 322.95; 332.9 vs 333.29); slab area (1,690 vs 1,691 SF; 2,552 vs 2,554 SF); girder spacing 9'-8" / 9'-8 7/16" / 9'-11 1/8";
deck 15'-8" overall, 8 1/2" slab, 1.0 % crown; arch chord 201'-0", rise 50'-0", R 126'-0", 33 floorbeams, pier-to-pier 201'-8 3/16" (201.68);
abutment cap 14'-7", abutment substructure concrete 6.5 CY (plan 13.0 CY for two abutments).

## 4. Findings (generated vs plan)

Category: Content / Form / Appearance / Missing. Severity H / M / L.

| # | Sheet | Finding | Cat | Sev | Plan p. | Suggested fix |
|---|---|---|---|---|---|---|
| 1 | Arch S-2 Typical section | Draws a generic 43'-0" out-to-out slab with 40'-7" roadway and "TYPE SSTR RAIL" (1 % crown, #5 / #4 bars); the plan shows tie girders 18'-4" CL to CL, 12'-0" trail, corrugated steel deck + 11" CIP deck, floorbeam, PED (HIGH) rail posts, cable hangers, ribs / lateral bracing, 10'-6" clearance. The model deck itself is ~18'-4" wide (117 CY; plan 3,698 SF) so the drawn section is not the modeled deck | Content | H | 90, 152 | Build the arch typical section from the model parts (tie, floorbeam, deck, rail, hangers) |
| 2 | Arch S-1 Bridge layout | Elevation shows only the slab edge and a rail "(beyond)": no arch rib, tie, hangers, piers, shafts, ground line, vertical clearances; plan shows only CL and pier lines (no tie girders, ribs, curves 105/106, PC/PT stations, test holes, S-curve) | Content | H | 86 | Reuse the arch elevation / framing plan geometry in the layout; add piers, shafts, ground, clearances |
| 3 | (none) Arch detail sheets | No sheets for arch rib, tie girder + splice, floorbeams (FB1-33 schedule), cable hangers / anchorage / connectors (32 cables), knuckle joints (4 pages), upper lateral bracing (15 pages), member geometry point tables (T / C / H / U) | Missing | H | 123-150 | Add arch detail generators (section 5) |
| 4 | (none) Arch deck | No deck plan, deck placement sequence, PT layout, steel-deck panel / closed-end details; the arch has no Deck Plan sheet although every approach unit has one | Missing | H | 151-153, 155 | Add arch deck plan, section, PT anchorage sheets |
| 5 | Arch S-8 Quantities | "STRUCTURAL STEEL - TOTAL 3,311 LB" is the bearings only; plan arch steel is 188,400 LB (31,300 Gr 50 + 120,100 Gr 50-T1 + 37,000 Gr 50-F1), cable hangers LS, PT; ribs / ties / floorbeams / bracing are drawn but not summed (or not modeled as parts) | Content | H | 91, 143 | Sum the arch members by pay item and add hangers and PT |
| 6 | Arch bearings, Piers 10 / 11 | Model has bearings (3,311 lb, 16 anchor bolts) but there is no bearing sheet; the Pier 10 / 11 sheets (Units 3 / 4) show only the approach G1 / G2 pedestals - the arch pedestals (EL A 534.779, E 534.726 at Pier 10; 531.546 / 531.494 at Pier 11), their cap-plan layout and the traffic barrier are missing | Missing | H | 102, 106, 107 | Add arch bearing sheet; draw the arch pedestals in the Pier 10 / 11 plans and elevations |
| 7 | Arch S-3 Arch elevation | Geometry correct (201'-0" chord, 50'-0" rise, R 126'-0") but only "SOUTH ARCH", no panel points T0-T34 / C / H / U, no member sizes (W14x132 rib, W8x28 bracing), no rib / tie splice coordinate tables, no 5° inward inclination or end view; splices TS(1)-(4) vs plan elevation table TS1-TS3 (the plan's own framing plan has TS(1)-(4)) | Content | M | 119, 120 | Add point labels, splice tables, end view, member callouts |
| 8 | Arch S-4 Framing plan | Floorbeam count (33) and S-curve match p.143, but no dimensions (16 spa @ 5'-10" / 6'-1 1/4", R 342'-4" / 360'-8" / 351'-6", end bays 8'-4 3/16", 18'-4"), only FB1 / FB17 / FB33 labelled, no TG1 / TG2 / TS marks, no line of inflection, no legend / notes, no typical tie section (7/8" web, 1'-2" flange), no steel quantity table | Form | M | 143 | Add dimension strings, all FB labels, tie section, legend, notes |
| 9 | Arch S-5, S-6, S-7 | Girder spacing / top of slab / top of web tables are empty (header rows only; no girders in the arch) | Content | M | none | Suppress for girderless bridges; tabulate deck top EL at floorbeams / tie CLs instead |
| 10 | Arch S-9 Bar list | vs plan bill: A1 306 pcs vs 606; B1 306 vs 303; S1 610 x 2'-6" vs 606 x 3'-3"; S2 2'-6 1/2" vs 2'-9"; D1-D4 lengths printed as ranges "16'-9 1/8"-40'-0"" vs 40'-0"; bar D3 (68 x 7'-5", hooked) absent; total 25,333 lb vs 29,377 lb (-14 %); every bar "STRAIGHT", no bend diagrams although D3 / S1 / S2 are bent | Content | M | 154 | Fix bar counts / lengths in the model, add D3, draw bends |
| 11 | (none) Arch geometry sheet | Global position (Pier 10 / 11 plan with CL structure, north / south tie chords), local coordinate system, work-point table (L0, CL1, CL2, T0, T0', T34, T34') not reproduced | Missing | M | 119 | Add geometry sheet from the arch definition |
| 12 | Unit S-1 Bridge layout | Plan is "developed along the CL" as a straight strip, but the bridge is curved (R 60' curve 103, R 250' curve 104; p.84 shows the true layout); no pier numbers in circles, PGL grades (+4.41 % / -1.61 %), curve data, overall length 861.57', rail limits, retaining walls, test holes | Form | M | 84-88 | Draw the real plan (curved CL and offsets) or at least add curve data and grades |
| 13 | Unit S-1 labels | Shaft-tip labels run into each other and the axis ticks (Units 1 and 3: "48" DIA DRILLED SHAFT, TIP EL ..." stacked), "TOP OF DECK AT CL" collides with the CL BRG tick and the 540 label; pier-line labels run through the span dimension (Unit 3) | Appearance | M | 85 | Stagger / leader the labels, clip against dimensions |
| 14 | Unit S-2 Typical section | One section only, no curb / railing: plan has 10" curbs, low railing post (4'-3") and high railing (9'-10", on spans 7-9 and 11-12; two typicals), shared-use and emergency-vehicle clearance envelopes, 12'-0" trail, 1'-0" shy. Slab 8 1/2", 15'-8", 9'-8" girder spacing and 3'-0" overhangs match | Content | M | 89 | Add rail / curb parts and the second typical for the high-rail stations |
| 15 | Unit S-3 Beam layout | Lengths and spacing agree with the framing plans, but no girder angles (90°, 85°04'08", 75°40'34"), no "true distance / bottom flange" and slope table, no pier reports, no hint that the layout is curved, no 1'-0" end-offset callouts | Form | M | 108-116 | Add pier / girder report tables from the model |
| 16 | Unit S-3 labels | STA / skew labels sit on beam outlines and dimension text; two stacked "CL BRG PIER n" labels at every interior pier; the "Tx28 (14 strands): CL BRG - CL BRG ..." line overlaps "STA 23+13.74" (Unit 3); 5-7 overlap pairs per sheet | Appearance | M | 159 | One label per pier, placed per side of the bearing line |
| 17 | Unit S-4 Deck plan | "DECK REINFORCING: NOT IN THE MODEL". The plan girder-unit sheets carry slab bars (A, C, CS, D, G, H, J, M, OA, P, T #4 / #5), bar table, thickened slab ends (IGTS), section depth table, dead-load deflection diagram and per-span reinforcing weights (Unit 1: 3,890 lb; spans 7-9: 5,876 lb) | Missing | H | 109-117 | Model the approach slab bars; emit bar list / bar table |
| 18 | Unit S-8 Bearing data | Interior piers use one seat elevation for BACK and AHEAD: Pier 3 ahead 525.171 / 525.176 vs plan 525.249 / 525.268; Pier 5 back 527.892 vs plan 527.814; Pier 6 ahead 529.136 vs 529.214; Piers 8, 9, 14, 15 (0.03-0.10 ft); Pier 12 back 527.768 / 527.751 vs plan 527.869 / 527.849 (0.10 ft = 1 3/16"). All other values match to 0.003 ft | Content | H | 91 | Model separate back / ahead seat elevations (pedestal heights) from the plan table |
| 19 | Unit S-8 Bearing data | Fixity column "NOT MODELED" for every bearing; pad 8" x 21" x 2 3/4" (approach bearing is a standard, no sheet in this set, so unverified) | Content | L | 107, 91 | Set type / fixity in the model or drop the column |
| 20 | Unit cap-plan sheets | Right panel titled "Footing and pile layout" is a bare circle (no diameter, no reinforcement); the supports are drilled shafts, no piles; subtitle talks about battered piles and N / S positions | Content | M | 97, 99 | Retitle "Drilled shaft", add diameter, column and reinforcement, drop pile wording |
| 21 | Unit cap-plan sheets | Dimensions (0'-7 3/16" / 9'-4" / 0'-4 7/16") are pedestal-to-pedestal and do not sum to the 15'-0" cap or match the plan LF1-3 / LB1-3 table (1.667 / 9.929 / 3.404); at shared piers (Unit 1 Pier 4, Unit 2 Pier 7, Unit 4 Pier 13) the neighbour unit's bearings are labelled with internal marks "G1003" / "G1004" | Content | M | 97, 99 | Dimension from the cap ends to the bearing CLs; label by unit ("G1 UNIT 3") |
| 22 | Unit pier / abutment sheets | No reinforcement anywhere: Section A-A is a hatch block (42" x 30" / 72" x 72"), no bars A, B, S, T1, T2, U, V, Z, no bar tables, mow strip, dowels D, anti-graffiti limits, galvanizing note; quantities print "BARS NOT IN THE MODEL" | Missing | H | 95-100, 104 | Model cap / column / abutment cages; add bar tables and sections |
| 23 | Abutment 1 / 16 sheets | Cap 14'-7", shaft 36" and EL 521.637 / 488.153 agree; no backwall (EL A 524.699), no plan, bearing-seat detail, shaft spacing (9'-0"), shaft table (DS 1-2, 31 LF, 6 ft into limestone), retaining-wall coping note; shaft length 30'-11 13/16" vs plan 31 LF | Content | M | 95, 96 | Add abutment plan / backwall / shaft table |
| 24 | (none) Foundation / shafts | No foundation layout and schedule (station, offset, diameter, skew, bearing for 28 shafts), no drilled-shaft detail sheets (sheets 2 of 2, p.152), no test-hole / load-test info; shaft concrete is lumped into "structural concrete" | Missing | M | 94, 98, 100, 101 | Add a foundation schedule sheet from the support data |
| 25 | Unit Estimated quantities | Model-basis table, not plan pay items (0416 drill shaft LF, 0420 Cl C conc abut / cap / column, 0422 slab SF, 0425 girder LF, 0434 bearings). Values that can be compared agree (girder LF, slab SF, abutment 6.5 CY) but reinforcing is "NOT MODELED" for deck, caps, abutments, beams, and shaft CY is not split from cap / column | Content | M | 91, 142 | Add a pay-item view; split shaft / cap / column concrete |
| 26 | (none) General notes, erection | No bridge general notes (design spec, concrete classes, galvanizing, PCP), no suggested erection sequence | Missing | L | 83, 92, 93 | Optional general-notes sheet from project data |
| 27 | All sheets Title block | "PROJECT NHT" only: no CSJ / highway / county / plan sheet number, designer / checker, seal, revisions; date is the run date 10/12/2026; numbering "S-n (n OF N)" restarts per unit | Form | L | all | Fill the title block from project info |
| 28 | Unit Beam schedule | Type column truncated ("Tx28 (8 stran..."); notes cite IGND but omit the plan's design stresses, depressed-strand pattern and optional-design / fabrication notes ("Class H" is given) | Appearance | L | 118 | Widen the column; carry the plan notes |
| 29 | Unit Beam sheets | Elevation drawn with depth exaggerated 5x; camber and dead-load deflections "not in the model" while the plan prints a deflection diagram and tables per span | Content | L | 109-117 | Add a deflection table if camber data become available |
| 30 | Unit bent sheets | Leaders and labels cross linework ("BOTT. OF CAP EL ..." over the cap line, seat leaders through pedestals, shaft leader through the "15'-0" CAP" dimension), cap haunch drawn without dimensions; 2-4 overlap pairs per sheet | Appearance | L | 97 | Offset labels, dimension the haunch |
| 31 | Unit set boundaries | Shared piers (4, 7, 13) have bent / cap sheets in one unit only; Piers 10 / 11 are drawn as ordinary approach bents in Units 3 / 4 although the plan treats them as arch piers (24'-0" x 72" cap, 54" x 54" column, 78" shaft: values agree) | Form | L | 102 | Cross-reference note on the unit that does not carry the pier |
| 32 | Arch S-2 labels | "40'-7" ROADWAY" collides with "CL NHT (TRAILCLNEW)"; "1.0%" overlaps the bar leader | Appearance | L | 90 | Move the dimension / label |
| 33 | Unit deck / substructure notes | Concrete classes and strengths absent (slab Class S HPC 4,000 psi, substructure Class C HPC 3,600 psi, Gr 60 steel) | Content | L | 109, 97 | Add material notes |
| 34 | Unit S-1 elevation | No rail (PED low / high), existing ground, minimum vertical clearance or "H" dimensions (the plan shows them; the H values themselves match) | Content | L | 85-88 | Add rail and ground line |

**Counts by severity: H 9, M 15, L 10 (34 findings).** By category: Content 16, Missing 8, Appearance 5, Form 5.

## 5. What is needed for a usable Northaven sheet set (ranked by impact)

1. **Arch detail family** (#3, #6): rib, tie girder + splice, floorbeam, hanger anchorage, knuckle, bearing and upper lateral bracing sheets (33 of the 38 arch plan pages). Without them the arch has 5 of 38 pages.
2. **Arch cross-section and deck set** (#1, #4): correct typical section from the model parts (tie girders 18'-4", steel deck, rail, hangers), deck plan, placement and PT sheets.
3. **Arch quantities** (#5, #10): steel by pay item (188,400 lb in the plan vs 3,311 lb printed) and correct deck bar counts / bends.
4. **Reinforcing in the approach spans** (#17, #22): slab bars on the girder units, cap / column / abutment cages, bar tables and sections.
5. **Seat elevations** (#18): separate back / ahead values at interior piers (up to 0.10 ft off).
6. **Arch layout and elevation content** (#2, #7, #8, #11): arch in the layout, point labels, splice tables, framing dimensions and marks, geometry sheet.
7. **Curved plan layouts** (#12, #15): true plan view or curve data, girder angles / pier reports.
8. **Railing and curbs on the typical sections and elevations** (#14, #34).
9. **Foundation schedule / drilled shaft sheets** (#20, #23, #24).
10. **Hygiene**: suppress the empty arch tables (#9), label collisions (#13, #16, #30, #32), internal mark names (#21), title block (#27), pay-item quantities (#25).

## 6. Notes

* Not verifiable from the raster: approach bearing pad size, shaft lengths at Piers 2-15 (only the Abutment 1 shaft table and the H values were checked), arch pedestal / PGL elevations at the arch ends (L0 534.12 and "13' below PGL" on p.170 vs typical-section PGL 535.208 not reconciled).
* Plan p.130 duplicates p.129, p.158 is sheet 2 of 2 (sheet 1 absent); no review markup exists in the PDF, so no comment items were available.
* Working files (PDFs, PNG renders, scripts) were kept in `temp\ns\` of the worktree (git-ignored); PDFs and bulk renders were deleted, a few excerpts kept uncommitted.
