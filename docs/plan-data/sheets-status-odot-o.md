# Generated sheets vs plans - ODOT Bridge O (steel tub unit, spans 14-16, superstructure only), status 2026-10-12

Read-only check (no code or model changed). Model `samples/plans/ODOT Bridge O tub unit.sbm` (a copy was used), dist CLI
`sbm sheets <model> <pdf>` (no bridge argument needed: the file holds one bridge), output PDF rendered with PyMuPDF at 100 dpi and compared with
"Vol 2_Phase 2B Bridge Combined.pdf" p.254-298 (fractions checked on rasters; the plan text layer has '?').
Method and format follow `sheets-vs-plans.md`. Every generated sheet and every in-scope plan sheet was viewed.

## 1. Generation result

* `sbm sheets` exit 0, **17 sheets in 0.4 s, no crash, no warning** printed. Sheets are 11x17 landscape vector pages, numbered S-1..S-17 (not the plan O-n numbers).
* Titles: S-1 Bridge layout (plan + elevation along CL) | S-2 Typical section | S-3 Framing plan spans 1-2 | S-4 Framing plan span 3 | S-5 Girder elevations G1 and G2 |
  S-6 Steel details (cross frames and stiffeners) | S-7, S-8, S-9 Field splice detail A, B, C | S-10 Field splice schedule | S-11 Girder spacing at supports |
  S-12 Bearing data | S-13 Cross slope geometrics | S-14 Camber and blocking | S-15 Top of slab elevations | S-16 Top of web elevations | S-17 Estimated quantities.
* Environment at start: free RAM 19 GB, C: free 73 GB.

### Does the generator handle tub girders?  Mostly NO - it falls back to I-girder views

| Item asked | Result |
|---|---|
| Framing plan | Draws only two straight lines (G1, G2), field splice ticks, bearings, spans, stations. No cross frames, top struts, diaphragms, lateral bracing, hatches. The legend still mentions "CF". |
| Girder elevations with tub plates | One web rectangle with a top-flange and a bottom-flange line, labelled `PL 21" x 1 1/4"`, `WEB 50" x 5/8"`, `PL 77" x 1 1/4"` (a single thickness for the whole length, same as an I-girder). No tub-specific drawing (two webs, flange widths). Plan plate transitions are not shown. |
| Tub cross section | **None.** The typical section draws two I-girders (one web, 21 in. top and 77 in. bottom flange, vertical web, not rotated about the top working point). The plan shows a tub: two 1:4 inclined webs, 8'-0 between working points, 4'-2 deep. |
| Internal / external bracing | **None.** S-6 prints "INTERMEDIATE CROSS FRAME: NONE IN THIS BRIDGE" and "SUPPORT CROSS FRAME: NONE IN THIS BRIDGE" although S-17 counts 69,672 lb of cross frames / diaphragms and 38,123 lb of lateral bracing in the model: the model holds them, the sheet code does not draw them (or only looks for I-girder cross frames). |
| Splices | **Tub-aware in text** (left / right web plates inside / outside, 2 top flange splice plates per side, bottom flange plate). Plate sizes match the plan. Drawing is small and the section through the tub web is missing. |
| Bearings | Table only (S-12). No bearing detail sheet. |
| Camber / deflection tables | Present (S-14), values match the plan sheet O-26. |

## 2. Coverage of the in-scope plan sheets (superstructure of spans 14-16)

In scope: 19 plan sheets (O-1, O-16, O-17, O-19, O-24 .. O-36, O-38, O-39). Out of scope / not counted: substructure O-1A..O-15, span 17 U54 O-18, O-20..O-23, O-37, O-40,
approach slab, slope wall, form liner O-41..O-43, drains BD-1 (user scope: not modeled), peace shield PS-1/2.
(Note: `INDEX.md` calls p.289 / O-35 "support diaphragm"; the title block reads LATERAL BRACING DETAIL. p.290 / O-36 is STEEL GIRDER MISCELLANEOUS DETAILS, p.292-293 / O-38, O-39 are
the steel girder bearing details and bearing assembly schedule, p.285-288 are the pier 16 end diaphragm and piers 14 & 15 diaphragm sheets.)

| Plan sheet (PDF p.) | Title | Generated sheet | Match |
|---|---|---|---|
| O-1 (254) | General plan and elevation | S-1 | partial (layout only; no curve / profile data, straight plan) |
| O-16 (270) | Superstructure details 1 of 3 (typical sections, deck reinforcing, tub section, haunch) | S-2 | partial (deck geometry right, tub section wrong, no rebar) |
| O-17 (271) | Superstructure details 2 of 3 (deck reinforcing plan, bar list, quantities, H table) | S-17 | weak (steel / concrete quantities only; rebar NOT MODELED) |
| O-19 (273) | Framing plan | S-3, S-4 | partial (girders / splices only) |
| O-24 (278) | Steel girder elevation, girder 1 | S-5 | partial |
| O-25 (279) | Steel girder elevation, girder 2 | S-5 | partial |
| O-26 (280) | Dead load deflections | S-14 | **good** (values match) |
| O-27 (281) | Field splice details | S-7, S-8, S-9, S-10 | **good** data, weak drawing |
| O-28 (282) | Intermediate cross frame details | (S-6 says none) | **none** |
| O-29, O-30 (283, 284) | End diaphragm pier 13 (2 sheets) | - | **none** |
| O-31, O-32 (285, 286) | End diaphragm pier 16 (2 sheets) | - | **none** |
| O-33, O-34 (287, 288) | Pier diaphragms piers 14 and 15 (2 sheets) | - | **none** |
| O-35 (289) | Lateral bracing detail | - | **none** |
| O-36 (290) | Steel girder miscellaneous details (support diaphragm, rodent screen, drip plate) | - | **none** |
| O-38 (292) | Steel girder bearing details | S-12 | partial (data table only) |
| O-39 (293) | Bearing assembly schedule | S-12 | partial (no pad / bevel plate schedule) |

* Coverage with any generated counterpart: **10 / 19 = 53 %** (O-1, O-16, O-17, O-19, O-24, O-25, O-26, O-27, O-38, O-39).
* Coverage with a substantially right counterpart (O-26, O-27): **2 / 19 = 11 %**; about 30 % if the 7 partial matches count half.
* Not covered: 9 / 19 = 47 % (all bracing, diaphragm and miscellaneous steel sheets).
* Generated sheets with no plan sheet: S-11 girder spacing, S-13 cross slope, S-15 top of slab, S-16 top of web (useful extras for checking; the plan prints only the spacing in O-19 / O-29).

## 3. Things that match the plan (no action)

Deck 32'-0 out-to-out, 29'-0 roadway, 17'-8 girder spacing, 7'-2 overhangs, 8" slab, 4 1/2" haunch, 6 % cross slope, F-shape parapet (S-2 vs O-16); spans / stations
(begin bridge 301+53.72, piers 302+93.00, 304+54.00, 306+14/15; pier 13 spacing 18'-11 13/16"); girder section lengths (141'-4", 158'-6 3/4", 137'-2 5/16" G2 span 14);
G1 first field section 106'-7 9/16" = plan 105'-9 7/8" splice + 9 11/16" end; splice plates (web 5/8 x 22 1/2 x 42 1/2, top 3/4 x 21 and 3/4 x 9 1/2 x 38 1/2, bottom 3/4 at splice 1 and
7/8 at splices 2-4, 63 1/2 x 36 1/2, filler at splice 3 only, 1" A325 bolts); camber / steel / slab deflections (G1 1.52 / 2.84 / 3.78 / 4.28 / 4.27, G2 1.53 / 2.85 / 1.32 / 3.13 / 5.02 ...);
bearing fixity (pier 14 fixed, others guided).

## 4. Findings

Category: drawing = drawing wrong or unreadable; missing = plan content absent; wrong = content contradicts model / plan; data = value difference; form = format / appearance.

| # | Sheet | Finding | Cat. | Sev. | Plan p. | Suggested fix |
|---|---|---|---|---|---|---|
| 1 | S-2 | Typical section draws I-girders (one vertical 50 x 5/8 web, 21" top and 77" bottom flange) instead of the tub: two 1:4 inclined webs, two 21" top flanges, 77" bottom flange, 4'-2 deep, 8'-0 between working points, girder rotated about the top WP with the cross slope. | drawing | H | 270 | Add a tub section view (from `TubSection`) to the typical section builder, rotated to the deck plane; keep the I view for plate girders. |
| 2 | S-5 | Girder elevation is an I-girder elevation (web + flange lines): no tub-specific content (two webs, flange widths, top flange pairs, bottom flange width, tension zones). | drawing | H | 278, 279 | Branch the elevation view on girder type; add top and bottom flange plan strips for tubs. |
| 3 | S-5 | Plate transitions missing: plan has top PL 1 1/4 / 1 3/8 / 1 7/8 / 2 1/4 x 21 and bottom PL 1 1/4 .. 2 1/4 x 77 in segments, web 5/8 with 3/4 near piers; sheet prints one `PL 21 x 1 1/4`, `PL 77 x 1 1/4`, `WEB 50 x 5/8` for the whole length (the splice sheets show 7/8 plates, so the model has the variation). | missing | H | 278, 279 | Label each plate segment (thickness, length, shop-splice location) from the girder plate list; show web thickness changes. |
| 4 | S-3, S-4 | Framing plan has no cross frames, top struts, internal / external diaphragms, lateral bracing, access hatches, equal-space (EQ. SPA.) dims or working-point dims; the legend mentions CF anyway. S-17 shows the model holds 69,672 lb frames and 38,123 lb lateral bracing. | missing | H | 273 | Draw the `FramingGenerator` members (K frames, top struts, XD, SD, LAT) in the framing plan with spacing dimensions and marks. |
| 5 | S-6 | States "INTERMEDIATE CROSS FRAME: NONE IN THIS BRIDGE" and "SUPPORT CROSS FRAME: NONE IN THIS BRIDGE" (contradicts the model quantities and the plan); half of the sheet is blank. | wrong | H | 282 | Detect tub framing (internal K, top strut, external diaphragm) and render those details instead of the I-girder cross-frame lookup. |
| 6 | (none) | No intermediate bracing detail sheet (internal cross frame, top strut WT6x29, external intermediate diaphragm, details A, B1, C). | missing | H | 282 | New tub bracing detail sheet from the model's frame parts. |
| 7 | (none) | No end / pier diaphragm sheets: end support diaphragms pier 13 and pier 16 (2 sheets each), pier diaphragms 14 and 15 (2 sheets): bent plates, connection plates, bearing stiffeners, top / bottom connection details, stud detail. | missing | H | 283-288 | New support-diaphragm sheet type (one per pier) from the G#-SD# / XSD# parts. |
| 8 | (none) | No lateral bracing detail (WT9x38 bracing, typical layout, layout at pier 13, connection detail A). | missing | H | 289 | New lateral bracing detail sheet from the G#-LAT parts. |
| 9 | S-12 | No bearing detail sheet: typical section at bearing, bearing plans at expansion / fixed piers, beveled plate detail, anchor bolt (2" dia, 1'-3 embed), contact plates, pad elevation. | missing | H | 292 | Extend the bearing views (BearingViews.cs) to tub bottom-flange bearings. |
| 10 | (none) | Steel girder miscellaneous details (support diaphragm top, rodent screen, drip plate, access door) not generated. | missing | M | 290 | Add as a misc steel details sheet once the parts are modeled. |
| 11 | S-1, S-3, S-4 | Plan view is "developed along CL": girders are straight parallel lines; the real girders are curved (R 868 / 885 / 881 ft) and the plan gives arc dimensions (14 EQ. SPA. = 141'-3 5/16" etc.). Not a true plan. | drawing | M | 273, 254 | Draw girders / frames in true plan on the alignment curve; keep the developed strip only as an option. |
| 12 | S-3 | Single skew label "21 deg 20'53.46" RT" at pier 13; plan gives the end diaphragm angles per girder (69 deg 02'38" and 69 deg 18'54") and radial dims (4'-4" radial, 18'-11 13/16" along bearing). | data | M | 273, 283 | Print the angle per support line in the plan convention (and the skew). |
| 13 | S-3, S-4 | Framing split at pier 15 with two scales (1"=30' and 1"=20'), no match line; the drawing fills about 25 % of the sheet height, labels tiny. | form | L | 273 | One scale, match-line notation, use the sheet height. |
| 14 | S-5 | Missing dimensions that the plan gives: girder overhang beyond CL bearing (9 11/16" / 9"), splice position from the pier (105'-9 7/8"), tension flange zones (BOTTOM / TOP / BOTH), stud spacing (124 spa @10" max ...), stud omission at splices, web weld symbol. | missing | M | 278, 279 | Add dimension rows (splice location, tension flange, stud spacing, ends) above and below the girder. |
| 15 | S-5 | Span lengths along the web differ by 1/16": 157'-6 15/16" vs 157'-6 7/8", 161'-9 1/2" vs 161'-9 9/16", 160'-9 7/16" vs 160'-9 3/8". | data | L | 278, 279 | Rounding of the web-CL length; accept or compare at 1/16 tolerance. |
| 16 | S-5 | "FIELD SPLICE" labels float about 100 px above the girder with no leader; bearing stiffeners (legend says solid / dashed) are not drawn; girder depth about 4 % of length (stated "not to scale"). | form | L | 278 | Add leaders; draw stiffener lines or remove the legend text. |
| 17 | S-7, S-8, S-9 | Splice detail views are tiny (1/4" = 1') and dimension strings pile on each other (web splice plan: bolt-gauge dims overprint); no section through the tub web, no Detail 1 (bolt stack / filler); plan has 6 views at 3/4" and 1". | form | M | 281 | Rescale to 3/4"; split web / flange views; add the web section and Detail 1. |
| 18 | S-9 | Filler plate printed `2 - FILLER PL 1/8" x 21" x 19 1/8"`; plan `1-FILLER PLATE 1/8 x 1'-9" x 1'-7 7/8"` (19 7/8", one filler, splice 3 only). | data | L | 281 | Check the model's filler length (19 1/8 vs 19 7/8) and count (per flange side vs 1). |
| 19 | S-10 | Schedule text truncated with "..." in the top flange, bottom flange and web columns (full plate lists cannot be read); 80 % of the sheet is empty. | form | M | 281 | Wrap text in the cells or split columns (outside / inside / filler). |
| 20 | S-12 | Bearing table has no pad size (plan P13 13 x 22, P14 20 x 30, P15 22 x 30, P16 15 x 23), laminates / layers / pad height (3 3/4" or 5"), bevel plate (J, K, L, M %, N %: 4.52 / -4.92 at P13, 6.00 ...), assembly marks (G1, F1, G2) or the bearing location schematic. | missing | M | 293 | Add a bearing assembly schedule and schematic from the bearing definitions (pad sizes may need to be modeled). |
| 21 | S-12 | Fixity prints as the enum text "GUIDEDEXPANSION" (no space) next to "FIXED (FX)". | form | L | 293 | Format the fixity text ("GUIDED EXPANSION"). |
| 22 | S-14 | Camber table uses 10 equal points per span only: the plan also lists the field splice points (FS1..FS4) as columns and shows the deflection curve; plan has concrete / steel / total rows. | missing | M | 280 | Add FS points and a deflection curve graphic. |
| 23 | S-14 | "BLOCKING" column is "-" everywhere; scale box empty. | form | L | 280 | Fill blocking or drop the empty column. |
| 24 | S-1 | No curve data / profile grade data (R 881.47, T, A, D, L; PVC 301+70, PVI 302+70, PVT 303+70, grades -2.93 % / -0.57 %) that the plan prints on the layout. | missing | M | 254 | Add alignment and profile data tables to the layout sheet. |
| 25 | S-1 | Span labels are CL bearing to CL bearing along the CL (138'-2 1/2") vs plan CL pier (139'-3 3/8"); bridge length 461'-3 3/8" (spans 14-16) vs plan 520'-3 3/8" (incl. span 17); "G1 (BEYOND)" leader overprints the field splice text line. | form | L | 254 | Print both CL pier and CL bearing spans (known convention, see sheets-vs-plans.md). |
| 26 | S-2 | No deck reinforcing section (A bars #6 @ 6", E1 #5, EB1, ET #6 bars), no haunch detail for tubs, drip bead, water repellent detail, tub girder notes. Deck bars are NOT MODELED (S-17). | missing | M | 270 | Model deck bars (see deck-rebar.md), then add the reinforcing section and the haunch detail. |
| 27 | S-2 | Lane / shoulder dimensions (4'-0 shoulder, 15'-0 lane, 10'-0 shoulder, 1'-6 parapet) not given; only 7'-2 / 17'-8 / 7'-2. | missing | L | 270 | Add the roadway breakdown dims from the cross-section definition. |
| 28 | S-6 | Steel notes differ: sheet "ASTM A709 GR. 50", plan AASHTO M270 Gr 50W (weathering), Charpy V-notch Zone 2 / fracture critical (bottom flange, webs), interior surfaces painted light colored epoxy, fillet welds terminated 3/8", girder ends plumb after dead load; stiffeners `PL 7 x 7/8 (pair)` / `PL 7 x 1/2` vs plan bearing stiffener 1 x 8, connection plate PL 3/4 x 10. | wrong | M | 278, 281-283 | Take the project steel notes from the model settings; check the modeled stiffener sizes against the plan. |
| 29 | S-6 | Stiffener detail labels collide ("1 1/2" CLIP" over "WELD HELD BACK AT CLIP", weld size "1/4" stacked on "TO WEB", rotated dims overprint the plate). | form | L | 282 | Offset the leader texts. |
| 30 | S-17 | Quantities not in the plan item format: sheet steel 879,713 lb vs plan structural steel 904,820 lb (-2.8 %); plan items parapet LF, sealed expansion joint, epoxy rebar 155,440 lb, Class AA CY 451.7 (includes span 17), stainless bearing assemblies, saw-cut grooving; sheet gives rebar "NOT MODELED". | data | M | 271 | Reconcile steel mass (bolts, studs, allowances), add an item-format table; deck / barrier rebar to be modeled. |
| 31 | (none) | No superstructure bar list, deck reinforcing plan, longitudinal section, 'H' dimension table (5'-10" .. 6'-5 3/4"), bar bending shapes. | missing | M | 271 | Depends on a deck rebar model for the tub unit. |
| 32 | all | Title block is generic (SCALES / PROJECT / title / sheet), not the ODOT block (design / drawn / checked, job piece 09033(11), county, sheet O-n); sheet ids S-1..S-17 unrelated to the plan's O-n. | form | L | all | Optional agency title block; show the plan sheet id through the sheet-link table. |
| 33 | S-3 | Duplicate "CL CL Ramp 44F" text in the framing plan; station labels rotated and overprint FS labels at 304+00. | form | L | 273 | Fix the label text and offset. |

Counts by severity: **H 9, M 13, L 11 (33 findings)**.

## 5. What is needed for a usable tub-girder sheet set (ranked)

1. **Tub cross-section view** (typical section and every cross-section detail): inclined webs 1:4, 2 top flanges, bottom flange, rotated about the top working point with the cross slope (findings 1, 17).
2. **Tub-aware girder elevation**: per-segment top / bottom flange thickness, web thickness changes, tension zones, studs, stiffener positions, flange plan strips (2, 3, 14).
3. **Framing plan with the framing members**: internal K frames, top struts, external / internal diaphragms, lateral bracing, access hatches, EQ. SPA. dims; true curved plan (4, 11, 12).
4. **Intermediate bracing detail sheet** (K frame, top strut, external intermediate diaphragm, details A / B1 / C) and removal of the false "NONE IN THIS BRIDGE" text (5, 6).
5. **Support diaphragm sheets**: end diaphragms pier 13 / 16, pier diaphragms 14 / 15 (7).
6. **Lateral bracing detail** (8).
7. **Bearing detail + assembly schedule** with pad / bevel plate data (9, 20, 21).
8. **Splice sheets at a readable scale** with the web section and Detail 1 (17, 18, 19).
9. **Steel notes and fabrication notes** from the project (M270 50W, Charpy zone, fracture critical, paint) and an ODOT title block (28, 32).
10. **Deck reinforcing section / bar list** (needs deck bars in the model), curve / profile data on the layout, deflection curve and FS points in the camber sheet, quantity table in plan item format (22, 24, 26, 30, 31).

The existing data sheets (S-11 spacing, S-14 camber, S-15 / S-16 elevations, S-17 quantities) and the splice plate content are sound and can be kept.

## Model inputs for the missing sheets (2026-10-19, handoff items 5 / 7a-7g)

The model behind the sheets now carries what items 5 (lateral bracing detail), 7 (support diaphragm sheets) and 9 (steel / fabrication notes) of the list above need as data:
WT 9x38 laterals with the Detail A 6-hole pattern (end setback scaled and flagged, p.289), pier 13 / 16 and pier 14 / 15 top and bottom connection plates with bolt holes
(p.284, 286, 288), interior diaphragm corner clips, the Section A-A strut plates (thickness / width flagged, p.287), 5/16" fillet welds (web to flanges, stiffeners, diaphragms,
bearing stiffeners), girder plates ending on the pier 13 skew line, and fabrication rules OT1-OT12 (M270 Gr 50W, Charpy zone 2 / fracture critical, faying surfaces, interior paint,
weld notes). The sheets themselves are still to be generated; see `CLAUDE.md` (Steel TUB girders) for the inputs and what is not built.
