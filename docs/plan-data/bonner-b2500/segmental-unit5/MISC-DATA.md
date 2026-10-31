# Bonner Unit 5 segmental box - piles, drains, expansion joints and miscellaneous details (plan data + inventory, 2026-10-30)

PDF `C:\SBMData\B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf` (vector sheets with a text layer); "p." = PDF page. TEXT = printed,
VECTOR = measured on the vector linework (scaled, flagged). Model inputs: `SegmentalDefinition.Misc` (`src/SBM.Core/Bridge/SegmentalMisc.cs`), generator
`src/SBM.Core/Segmental/SegmentalGeneratorMisc.cs`, data script `samples/plans/misc-bonner-unit5.csx` (called by `build-bonner-unit5.csx`), agent review rows in
`samples/plans/check-bonner-unit5.csx` section 10. Files: `piles.csv` (284 piles, `tools/bonner_piles.py`), `piles-bents.csv` (per bent).

## 1. Inventory: what the plans have on / in the Unit 5 superstructure and bents

| sheet (p.) | item | in the set | model | reason / note |
|---|---|---|---|---|
| S3-03..05 (251-253), S3-87 / 90 / 93 / 96 / 101 / 104 (335-352) | 36" sq PS piles, 2:12 batter, footing plans per bent | yes | BUILT (284) | positions + batter arrows read on the vector footing plans; heads 4'-0" above the footing bottom (Section D-D "4'-0" (TYP.)"), bents 23 / 24: 1'-3" for the 4 piles under the column base (note + leaders, p.344) |
| S3-06..11 (254-259) | pile data tables (max factored axial, tip no higher than, scour critical) | yes | BUILT (tips) | installed tips are blank (filled in during construction) -> modeled tip = "no higher than" elevation (UNRESOLVED flag on every pile) |
| S0-41 (42) | 36" square PS pile standard (21" void, 13'-0" / 11'-0" solid ends, 1" chamfers, 36-0.6" strands) | yes | BUILT | 1" chamfers + 21" void (32-gon, inner shell) between the solid end sections; void ends flat (conical fittings) and the head solid length measured from the modeled head (field cut-off up to 2'-0") flagged |
| Detail C on S3-87..104 (335-352) | CL footing / column / cap offset from -L- | yes | BUILT | 4 7/8" (18), 10 3/8" (19-22), 5 3/4" (23-28), 2 11/16" (29) to the LEFT (uphill = box soffit CL, = DIM "B"); bearings "SYM. ABOUT CL" of the cap (p.359) -> footing, column, cap and bearings all shifted |
| S3-74 (322) | 6" deck drains, one per segment, low side, CL 1'-7 1/2" from the deck edge, normal to the deck, top 1/2" below the surface, PVC | yes | BUILT (242, typical segments) | position along the segment = segment centre (note 5 "centred between the transverse PT tendons" - tendons drawn only, flagged); bent / end segments / closures: note 4 unclear (UNRESOLVED) |
| S2-19 (161) Section N-N, S4-15 (407) Section K-K | CL modular expansion joint assembly @ 60 F: 1'-0 1/2"+- (bent 18, Unit 4 side) / 1'-2 1/2"+- (bent 29, Unit 6 side) from the bent control line | yes (the S2 / S4 sheets ARE in the combined set) | BUILT (placeholder) | modular joint by the manufacturer (special provisions); its "MODULAR JOINT MOVEMENT AND SETTINGS" table (DIM "A") is NOT in the 633 pages (text search) -> opening derived = 2 x (anchor face - CL joint) = 3'-9" (18) / 4'-1" (29), flagged |
| S3-23 (271) Section C-C + half plan | end segment expansion joint blockout 2'-2" from the anchor face x 1'-4" deep | yes | BUILT (opening of the end segment) | ends 9" from the deck edges scaled (VECTOR, 3/8" = 1'-0"); the end segment top slab is 2'-2" thick at the anchor face (Section C-C) but modeled with the typical outline -> removed concrete clipped to the modeled wing |
| S3-79 (327), S3-85 (333) | parapet end posts at the modular joints: 3'-9" long, 1'-2" wide, 4'-8" high = 2'-2" above the 2'-6" parapet | yes | BUILT (2 per joint, segmental side) | the part above the parapet only (the parapet below is the parapet solid); the end posts on the adjoining FIB units belong to those units |
| S3-85 (333) revision 1 | 3/4" cover plates on the parapet traffic faces, 4'-5" high from 3" above the deck, 6'-1" (left) / 6'-3" (right), 3'-4 1/2" on one side of the CL joint, M270 Gr 50 metallized, 3/4" stainless bolts in ferrule inserts | yes | BUILT (2 per joint) | which unit the 3'-4 1/2" side belongs to is not stated -> long part on the segmental side (flagged); bolts / inserts / backing plates not modeled |
| S3-85 (333) Section A-A | FIB-side blockout 2'-8" MIN. x 1'-1" MIN. | yes | not built | belongs to the adjoining FIB units (not in this bridge) |
| S0-36 / 37 (37-38), S0-38 (39), S3-80..83 (328-331) | NCDOT 2-bar metal rail (aluminum): posts, base plates, 4-bolt anchor assemblies, two rails; Unit 5 post spacing along the theoretical edge of deck | yes (DETAILS-DATA.md said "not in the set" - corrected) | BUILT (generic railing, 2 runs, 1,189 posts = the p.38 anchor assembly table) | every post chain closes on its printed edge arc; posts as their side-outline envelope (T-section), rails as 4 3/4" x 4" bars (weight not counted), post position on the base derived; splices / expansion bars / clamp bars / end-of-rail attachments not modeled (flagged) |
| S3-20 (268) note 3 + Interior Section, S3-74 (322) | 2" vent holes: centre of each typical segment, low side of the cell, CL 4'-1 1/2" in from the soffit corner (W/2); 1/4" screen + caulking | yes | BUILT (bottom slab openings, every typical segment) | located by note 3 (first inventory said "drawn only" - wrong); end segment 2 1/2" top slab vent (p.271) drawn only, not built; screen not modeled |
| S3-18 (266) SURVEY MARKERS table, S3-15..18 (263-266) legend, S0-55 (58) | survey disc blockouts: 66 table rows (55 N, 11 TP) by station; N = pair 14'-0" left / right of CL, TP 2'-0" (min.) from the barrier face on the high side; 3 1/2" square, 3/4"-1" deep | yes | BUILT (110 N + 11 TP top slab recesses) | the station table was missed in the first inventory; depth 1" (max.) and TP at exactly 2'-0" flagged |
| S3-84 (332), S3-74 (322) + S3-144 (392) tables, S.A.P. on S3-16..18 | solar array support platforms: 9 (spans 20-28, segment XX-U4, CL stations 59+92.83 + 350'), W8X18 / W8X31 / L3X3 framing, 1" x 4" grid, P plates, ladder rungs; solar array + battery box by others | yes | BUILT (framing + placeholders) | located by the station table (first inventory said "not located on the segment" - wrong); side = LEFT read from the p.392 plan + north arrow (flagged); clips P1 / P2, B6, bolts, ferrule loop inserts not modeled; array / battery sizes scaled from the NTS p.392 section (flagged) |
| S3-144 (392), N.L. on S3-15..18 | navigational lights (green CL, red margins 100'-0" each side, both parapets), 7 junction boxes, 10/3 MC cable (all by others), 1 1/4" stainless conduit in 2 1/2" PVC sleeves cast in the slab at each platform station | yes | sleeves BUILT (9, approximate location flagged); lights / boxes / cable not built | the lights are located only relative to the navigation channel CL, whose station is not given anywhere in the 633 pages (text search) |
| S3-140..143 (388-391) box girder lighting PLANS, S3-137..139 (385-387) | luminaires on the right web at arcs along CL span (span 19: 21'-6 3/16" + 3 x 50'; spans 20-28: 24'-0 1/16" + 6 x 50'; span 29: 24'-0 1/16" + 3 x 50'), types A (5'-0" / 20 deg) / B (7'-0" / 15 deg), receptacles, 18x18x6 junction boxes, Panels A (span 19) / B (span 29), service boxes in the railing | yes | BUILT (71 luminaires + boxes + receptacles, 2 panels, 2 service boxes) | the first inventory called this NTS - only the system sheets 385-387 are NTS, the plans 388-391 are dimensioned; envelopes / box heights flagged; conduit runs (shop drawings, p.387 note 1), hoist receptacles at the bents, grounding not built |
| S3-66..68 (314-316) | vermin screens (end segment anchor faces, 10'-2" x 6'-0" with door), A.O. / A.O.B. access doors (3'-3" x 4'-4" two leaves, 3'-2" dia.) | yes | BUILT (2 screens, 14 + 10 doors as envelopes) | hinges / stop bars / slide bolts / A.O. ladder not modeled; screen on the anchor face of the solid end segment envelope (flagged) |
| S3-69..72 (317-320) | disc bearing details / replacement details | yes | generic block (build script) | vendor-designed bearings |
| S0-43..54 (44-57) | utility conduit system (bridge-wide electrical + fiber optic, by the utility owner): hangers on -L- under the top slab in spans 19-29, optional floor splice boxes | yes | not built | hanger spacing / profile "varies", splice box locations per the utility working drawings (not in the set); the bent diaphragm utility openings are built |

Inspection access inside the box = the A.O. / A.O.B. hatches + the bent diaphragm / end segment diaphragm openings (built as openings); no external walkways.
No approach slab meets Unit 5 (its ends are the modular joints to the FIB Units 4 and 6).

## 2. Piles (piles.csv, piles-bents.csv)

| bent | piles | footing (S x N) | Detail C offset | tip no higher than | max factored axial (t) | scour critical | BOM LF | model LF |
|---|---|---|---|---|---|---|---|---|
| 18 | 18 | 36'-0" x 36'-0" | 4 7/8" | -95.0 | 455 | -72.0 | 1797 | see check |
| 19 | 20 | 43'-6" x 36'-0" | 10 3/8" | -100.0 | 550 | -72.0 | 2099 | |
| 20 | 24 | 51'-0" x 36'-0" | 10 3/8" | -110.0 | 520 | -85.0 | 2762 | |
| 21 | 24 | 51'-0" x 36'-0" | 10 3/8" | -120.0 | 570 | -85.0 | 3005 | |
| 22 | 24 | 51'-0" x 36'-0" | 10 3/8" | -120.0 | 520 | -85.0 | 3005 | |
| 23 | 30 | 51'-0" x 58'-6" octagon | 5 3/4" | -120.0 | 505 | -85.0 | 3752 | |
| 24 | 30 | 51'-0" x 58'-6" octagon | 5 3/4" | -120.0 | 505 | -85.0 | 3752 | |
| 25 | 24 | 51'-0" x 36'-0" | 5 3/4" | -130.0 | 535 | -85.0 | 3248 | |
| 26 | 24 | 43'-6" x 36'-0" | 5 3/4" | -115.0 | 540 | -85.0 | 2883 | |
| 27 | 24 | 43'-6" x 36'-0" | 5 3/4" | -115.0 | 555 | -85.0 | 2883 | |
| 28 | 24 | 43'-6" x 36'-0" | 5 3/4" | -125.0 | 515 | -85.0 | 3127 | |
| 29 | 18 | 36'-0" x 36'-0" | 2 11/16" | -115.0 | 440 | -85.0 | 2162 | |

Pile grid (from the CL footing, ft): S on +-3.75 / 7.5 / 11.25 / 15 / 18.75 / 22.5, N on +-3.75 / 7.5 / 11.25 / 15 / 18.75 / 26.25; every pile is battered 2:12
outward (end-row piles along N, side piles along S; bents 23 / 24 the 4 piles at S +-7.5, N +-3.75 along S). The bills of material reproduce exactly
(head at the footing bottom + 4'-0" to the "no higher than" tip along the 2:12 axis), so the plans' pile lengths ARE the minimum-tip lengths.

## 3. Geometry differences found (bents)

- Footing / column / cap / bearings were centred on the bent WP (except the footings of 22-25, p.252 raster): every bent's Detail C puts them on the box
  soffit CL, left of -L- by 2 11/16" .. 10 3/8" (= the model's DIM "B" within 1/8"). Fixed by misc-bonner-unit5.csx (data shift + `ColumnOffsetS`).
- Footing orientation and sizes: the footing plans confirm the model (larger size along the bent control line; 23 / 24 octagon 51'-0" along the bent line x
  58'-6" along -L-). The "footing orientation / position" flags of bents 18-21 and 26-29 are resolved and removed.
- End segment face vs joint: S3-23 Section A-A puts the anchor face 10" from the bent control line (model); the S2-19 / S4-15 joint sections draw it about 3"
  from it (scaled) - reported, model unchanged.
- DETAILS-DATA.md "2-bar metal rail (NCDOT standard, not in the set)" is wrong: S0-36 / S0-37 (p.37-38) are the 2-bar metal rail sheets.

## 4. UNRESOLVED (page + plain reason)

- p.254-259: installed pile tips blank -> tips at the "no higher than" elevation; pile positions at the footing bottom plane (level not stated).
- p.42: pile void conical ends and the head solid length vs the field cut-off.
- p.37-39, 331: rail post / rail sections as envelopes, splices / expansion bars / end attachments not modeled, span 29 right chain = left.
- p.322 / 272 / 282: drain position along the segment (centred between transverse tendons drawn only) -> segment centre; p.322 note 4: drains in bent /
  end segments / closures not stated -> typical segments only.
- p.333 / 161 / 407: modular joint DIM "A" (setting table bound with the transition plans, not in the set) -> opening derived; device by others = envelope.
- p.271: blockout ends 9" from the deck edges scaled; end segment top slab thickening (2'-2") not modeled (blockout clipped to the modeled wing).
- p.333: which unit the 3'-4 1/2" part of each cover plate belongs to.
- p.271 vs p.161 / 407: anchor face position (10" vs ~3" drawn).
- p.392 / 332: solar platform side (LEFT, from the plan + north arrow), wedge plate 1", end rail posts, array / battery envelopes (by others, scaled).
- p.392: nav light sleeves at the platform station ("approximate location"), mid-depth of the top slab; nav lights / junction boxes / MC cable not built (channel station not given).
- p.385-391: luminaire / box envelopes and heights (42" / 60" junction boxes), right web from the plans, arc chains close 1'-0" to 2'-6" short of the spans; panels 6'-0" read as the top; service boxes at the panel stations; conduit runs / hoist receptacles / grounding not built.
- p.315-316 / 314: door leaves only; vermin screen envelope on the solid end segment's anchor face (bottom at the 10" slab).
- p.268 / 271: vent holes in typical segments only; p.58 / 266: survey blockouts 1" deep, TP at 2'-0".
- Second plan search (2026-10-30, user question): the whole 633-page text layer (every page has text) was searched for navigation / nav light / pier
  light / channel / USCG / solar / battery / lighting / luminaire / junction box / conduit / hatch / access door / vermin / vent hole / survey / platform.
  The first inventory's "not located" / "NTS" / "drawn only" findings for the platforms, box lighting, vent holes and survey blockouts were WRONG
  (p.322 / 392 platform tables, p.388-391 dimensioned lighting plans, p.268 vent note, p.266 survey marker table). Electrical sheets outside the S3
  series: S0-43..54 (p.44-57) = the bridge-wide electrical + fiber optic utility conduit system; in spans 19-29 it hangs from the top slab on -L-
  (p.49-50, 57) with OPTIONAL floor-mounted splice boxes (3'-6" x 5'-7", p.57), but the hanger spacing / conduit profile "VARIES" and the splice box
  locations are "per the utility working drawings" (p.50, 51, 56 notes) - not built (UNRESOLVED); the bent diaphragm utility openings are built.
  No E- / L- / ITS / USCG sheets are in the set.
