# Bonner Unit 6 (FIB-96, spans 30-35) - inventory of the Unit 6 sheets: built / not built

Plan set: `C:\SBMData\B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf` ("p." = PDF page). Model: `samples/plans/build-bonner-unit6.csx`
(+ the detail scripts it calls: `diaphragms-bonner-unit6.csx`, `joints-parapet-bonner-unit6.csx`, `deck-rebar-bonner-unit6.csx`, `beam-rebar-bonner-unit6.csx`),
agent review rows in `samples/plans/check-bonner-unit6.csx` (section 8 = the items of this pass). Inventory made 2026-10-30.

Status: **built** = in the 3D model from plan dimensions; **schedule** = in the bar schedule at the printed count / length, not placed in 3D;
**not built** = reason given; UNRESOLVED items are also raised by the generators with page + reason.

| p. | sheet | item | status | reason / note |
|---|---|---|---|---|
| 395 | S4-03 | foundation layout (bent stations, footings, piles) | built | first model pass |
| 398-400 | S4-06..08 | pile data table | built | tips at "no higher than" (installed tips blank: UNRESOLVED) |
| 401 | S4-09 | LRFR summary | not built | load rating data, no geometry |
| 404 | S4-12 | typical section: deck 42'-7", 9" slab, covers 3" / 2 1/2", build-up table, parapet, 2-bar metal rail, 6" PVC drains | built (deck, haunch, parapet, covers, rail, drains) | rail and drains: see p.452 / p.409-414 rows |
| 405 | S4-13 | typical sections with the EXPANSION JOINT DIAPHRAGMS (bent 29, bent 35), end posts 4'-8" | built (2026-10-31: sloped end faces with the new OPTIONAL `DiaphragmEnd.TopOffset` / `DiaphragmEnd.Sloped`; U301-U306 placed in them) | the face's lower end meets the bottom flange slope ~6" above the girder bottom (drawn): one plane from 19'-3" (+1/16") at the girder bottom line to the deck edge at the soffit (UNRESOLVED); E301-E304 schedule only (sloped lines, no position dimension) |
| 406 | S4-14 | typical section with the INTERIOR BENT DIAPHRAGMS + Section A-A (2'-4", ends 3'-7 1/2" outside CL G1 / G4) | built | bottom = bottom of the girders (back + ahead girders of a line merged, new generic rule in the generator) |
| 406 | S4-14 | partial section: formed opening (1'-4" wide) in the bent diaphragm, #5 S304 around it | not built | "where reqd." (temporary openings); bars schedule only |
| 407 | S4-15 | Sections K-K (bent 29), L-L / M-M (bents 30-34), blockout details around the sole plates, concrete inserts / eye bolts | built (diaphragm solids, bars K302 / S305 / U309); blockouts / inserts not built | blockouts drawn without depth / outline (UNRESOLVED); inserts 3/4" stainless: hardware not modeled |
| 408 | S4-16 | Section N-N (bent 35 expansion joint diaphragm, Transflex blockout 1'-3 1/2" x 4"), plan at expansion joints, 8" deck-to-diaphragm fillet | built (re-read 2026-10-31: the expansion joint diaphragm bottom is the girder bottom line; the 1'-3" is the lowest K302 row above it - the first pass had read it as the diaphragm bottom) | diaphragm width "1'-10\" (MIN.)" modeled at the minimum (UNRESOLVED) |
| 409-414 | S4-17..22 | plan of spans: A101 top 1924 @ 6" / bottom 2199 @ 5 1/4", bottom B107 / B108 / B109, top B101-B106, deck drains, temporary access openings (p.409) | built: A101, bottom B107-B109, top B101 / B102 / B103 (p.409 partial plan, 5 interior bents), B104 (all 261), B105, B106, 71 deck drains; openings not built | A101 first-line distance not dimensioned (lines centred, UNRESOLVED). Top B: line A / B mirrored per the 2'-2" stagger; spans 31-34 close exactly with 2 x B104 lapped 2'-2"; in spans 30 / 35 the B104 / B105 / B106 laps are given only as minimums: READING (not an invention, dimension-only flag) = laid from the deck end (3" cover) at the 2'-2" minimum #4 laps, the rest laps onto the #6 B102 (span 30 4'-5 1/2"; span 35 only 2'-0 1/8" < 2'-2" min = CONFLICT reported, not adjusted). Drains: new OPTIONAL `DeckDrainDefinition.SlabPipeOnly` (vertical 6" pipe through the slab), stations at the "approx." 13'-8 1/2", centre 3" from the parapet face (drawn against it, not dimensioned: UNRESOLVED dimension-only). Access openings 3'-2" x 3'-2" (temporary, repaired, p.445) not built |
| 415 | S4-23 | plan of spans details (arc offsets span 35, details A / B) | built (deck edges follow -L-) | arc offsets agree with the alignment (span 35 edge lengths 160'-10 9/16" / 160'-9 7/16" vs model edges) |
| 422-424 | S4-30..32 | framing plans | built | first model pass |
| 428-431 | S4-36..39 | girder sheets: chains, bar lists, bar types, girder data | built: S1 / S3 / S4 / S5 / S6 / S8; not built: S2, S7, V1, WWF | S3 = horizontal hairpins, 8 levels each end (p.438 A-A); S4 pairs from the "2\" CLR. (TYP. U.N.O.)" note (dimension-only note). Span 30 S4: re-read 2026-10-31 - p.428 and p.429 carry the same S2 / S4 chain labels (5 spa @ 3 1/2", 21 spa @ 6", with S1, with S5); the only difference is span 30's 36 + 1 + 36 S5 spaces (75 S5 stations vs 77), and the span 30 bill keeps S5 = 150 but S4 = 302 (= the 31-34 value). No misread: plan internal difference (298 by the chain), reported. S2 height in the bulb and V1 extent drawn, not dimensioned; S7 bent shape / rows not printed (UNRESOLVED); WWF: no wire-fabric input (GAP). Span 35 G1-G2 S1 projection "(+) varies" (UNRESOLVED; listed length used) |
| 438-439 | S4-46..47 | FIB-96 outline, sections A-A..E-E, projection table, strands, debonding, embedded plate B-1 | built (outline, strands, bar positions used); B-1 plates / formed holes / end bevel not built | first pass flags |
| 440-444 | S4-48..52 | elastomeric bearings E1 / E2 / E3, sole plates | built | anchor bolts / pipe sleeves / pockets not built |
| 445 | S4-52A | temporary access openings in the DECK (3'-2" top / 3'-0" bottom, 9:1 sides, STA 93+35.00 / 93+71.00, 0.17' RT p.409) and their repair bars | H7 placed (34 = 2 x (6 top + 7 bottom + 4 along)); H1-H6 schedule; openings recorded as TEMPORARY (repaired with MAP concrete), no hole cut | the openings are in the deck, not the girders |
| 446-447 | S4-53..54 | camber / dead load deflection | built (recorded + checked) | geometry uncambered (program rule) |
| 448 | S4-55 | concrete parapet plan Unit 6: 1/2" joints 28'-5" + 4 @ 26'-0" + 28'-5" per span from the bent lines, end posts at the modular joints, S1 @ 1'-0", B bars | built: parapet joints (new generic `BarrierDefinition.Joints`), end posts (zones), bars S1 (1924), B1 (384), B6 (160), B7 (16), end post E1 (32) / E2 (8) / F1 (24) / S2 (22) as deck plan bar sets (Element "barrier", parapet-rebar-bonner-unit6.csx); schedule: B8-B11 | B8 / B9 vs B10 / B11: which 2 of the 8 positions take the shorter bars is not shown (UNRESOLVED). Top B bar of each face in the S1 top corner (derived) |
| 450-451 | S4-57..58 | end post details (3'-9" x 1'-2" x 4'-8"), parapet bill (Unit 6 column), 209.2 CY | built (end posts and their bars) | E chains / F1 levels / S2 groups as printed; E bar ends, E2 bottom, S2 height and the bent 35 S2 start (scaled 3 1/4") derived / flagged dimension-only. B8-B11: p.451 plan of end of parapet shows the 8 B bars running into the end post without marking the 2 shorter ones (still schedule only); joint filler (1/2" exp. jt. matl.) not built |
| 37-38, 452 | S0-36 / S0-37, S4-59 | 2 bar metal rail (aluminum): section + post spacing chains along the outside edge of deck | built (rail-bonner-unit6.csx, generic railing, same section as Unit 5) | 157 + 157 = 314 posts = the p.38 anchor assembly table; every chain closes on its printed edge arc within 1/16". Posts / rails as outlines (aluminum T post and semi-elliptical rail: envelope, dimension-only flag), splices / expansion bars / end-of-rail attachments (p.39) not built; span 30 right side = left chain ("typical each side") |
| 454 | S4-61 | expansion joint seal details: DIM "A" table (bent 29 2'-10", bent 35 4 1/4" at 60 F), Section A-A (6'-0" modular blockout, 1'-1" min.), Section B-B, 3/4" cover plates in the parapet face, concrete inserts | built: deck ends, blockouts, generic device envelopes; not built: cover plates, inserts | devices are proprietary (special provisions; UNRESOLVED). Cover plates lie in a 3/4" recess of the parapet face: the barrier plate input wraps the whole section and there is no recess input (a recess would need a new cut in the barrier sweep - not simple; GAP, kept flagged). Bent 29 deck face = CL joint 1'-2 1/2" (+-) + 1'-5" = 2'-7 1/2" (UNRESOLVED: "+-"); the girder end (2'-3") is 4 1/2" inside it (drawn so on p.450 / p.407) |
| 455 | S4-62 | bill of material Unit 6 (299,412 lb stainless), concrete quantities | built / schedule as above | A101 bill 4125 = 1924 + 2199 + 2 (plan note: each plan-of-span bill carries 2 additional A101); model 4123 lines |
| 457 | S4-64 | deck pouring sequence, closures 4'-0" / 6'-0" | not built | construction sequence; one deck solid per unit |
| 458-469 | S4-65..76 | bents 30-35 (footings, piles, column base) | built | footing / column-base reinforcing not built (bars not transcribed, first pass) |
| 477-480 | S4-84..87 | precast column segments | built as solid columns | no segmental column feature (UNRESOLVED, first pass) |
| 481-486 | S4-88..93 | precast bent caps, pedestals | built | cap reinforcing not built (first pass) |
| 489-490 | S4-96..97 | substructure post-tensioning | not built | no PT bar feature for columns (first pass) |

## UNRESOLVED added in this pass (page + reason)
- p.407 / p.408: expansion joint diaphragm width "1'-10\" (MIN.)" - only a minimum is given; modeled at 1'-10".
- p.405: sloped end faces of the expansion joint diaphragms - no generator input for a sloped end; vertical end 19'-3" (+1/16") from -L-.
- p.407: blockouts around the sole plates in the bent diaphragms - drawn without depth / outline; not modeled.
- p.454 / p.407 / p.450: modular joint device + the Unit 6 deck face at bent 29 (CL joint "1'-2 1/2\" +-", DIM A at 60 F only).
- p.454 / p.408: Transflex device at bent 35 (proprietary; envelope only).
- p.409-414: first / last A101 line from the deck end - not dimensioned; lines centred at the maximum spacing.
- p.409 / p.414: top B104 / B105 / B106 in the end spans 30 / 35 - laps only given as minimums (schedule).
- p.405: lower end of the sloped end face (drawn ~6" above the girder bottom) - one plane from the girder bottom line; U301-U306 heights from that end (1'-0" + 1'-0" ... + 10", base scaled 6"); E301-E304 drawn as sloped lines without position dimensions - schedule only.
- p.407 / p.408: G302 at the supplier's modular joint support boxes (positions not in the plans), G301 without a dimension to the girder / blockout: schedule only.
- p.405-408: K301, K303-K307, K309 / K310, S301-S304, S307-S311, U308, U310 / U311: runs / patterns not dimensioned enough to place without guessing (K307: 32 per bent diaphragm vs 16 drawn; S309: 82 per bay): schedule only.
- p.406 / p.407: S305 top leg position and which overhang points carry S305 / U309 (dimension-only).
- p.445: H7 groups at maximum spacings, centred in the openings (dimension-only); H1-H6 (coupled / around the opening) schedule only.
- p.448 / p.451: parapet B8-B11 - which 2 of the 8 positions take the shorter B10 / B11 is not shown (schedule only).
- p.450 / p.451: end post E bar ends, E2 bottom, S2 projection and bent 35 S2 start (derived / scaled, dimension-only).
- p.428-431 / p.438: girder bars S2 (height), S7 (bent shape), V1 (extent) not dimensioned; WWF no input (GAP); p.430 span 35 G1-G2 S1 projection "varies".
- p.37-39: rail post / rail sections (envelopes), splices, end-of-rail attachments.
- p.454: parapet cover plates in the recess, concrete inserts (no fitting input).
- p.404 / p.409-414: deck drain stations ("approx.") and the position across (drawn against the parapet face).

## Program changes made for this pass (generic, OPTIONAL)
- `DiaphragmEnd.TopOffset` (+ `DiaphragmEnd.Sloped(bottom, top)`) = sloped diaphragm end face between the alignment offsets at the part bottom and top (Slice and open-below Panels paths); Woodbury / Br02069 IFC identical (FILE_NAME only).
- `DeckDrainDefinition.SlabPipeOnly` / `PipeBelowSlab` / `Unresolved` / `Basis` = a pipe drain through the slab only (no grate / downspout), dimension-only status.
- `BarrierDefinition.Joints` (`BarrierJoint`: support / station + distance along the deck edge + width) = open joints in a custom barrier; zones that end
  on a barrier end (end posts) are clamped to it (1 in. tolerance).
- `DeckLongZone.StartMark` / `StartLength` = first bar of every longitudinal line (with `AlternateEnds` the odd lines start it at the other end).
- `ConcreteDiaphragmKind.ExpansionJointEnd` (appended); the `BeamBottoms` riser rule merges the back and ahead girder of one line at a pier
  (continuity diaphragm) into one bottom (it threw "bottom flanges overlap" before).
- `BeamReinforcementDefinition.Unit` = bar schedule unit of a beam group (marks per unit); the girder sheets carry different bar lists for one designation
  (one mark per unit + mark used to crash the IFC export when a later group had a mark the first did not).
