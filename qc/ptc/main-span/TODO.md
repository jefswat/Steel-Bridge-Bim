# PTC main span: fix to-do (user 2026-10-27: "fix it all in the order it came up")

Row ids -> `FIX-LIST.md` and the three findings files. One fix agent at a time works down this list (batches share the truss
generator and the PTC scripts). Each item is ticked only after: code / data changed, PTC main span regenerated and re-measured,
the affected review rows re-checked, tests green. Plan conflicts are reported, never adjusted. Nothing is committed until the user asks.

## Batch 1 - generator changes (optional inputs; other models unchanged)
- [x] 1a Joint rivet grip from the contiguous steel along the shank (GL08) - `TrussDefinition.JointRivetGripsFromSteel` (set in rivets-ptc.csx). Re-measured with a CORRECTED
  probe (work4/layers.csx: the work3 probe paired hits by parity and dropped long faces, so touching plies read as air - the GL08 count 57,016 was mostly that): before 8,004 of
  76,090 face rivets with air (6,962 gusset-to-member gaps, 866 grips past the last steel, 384 inside the member), 4,180 heads buried / 88 floating; after (saved model,
  work4/out-saved): grips past the steel 866 -> 43, heads buried 4,180 -> 222 (heads under bracing edge-angle legs standing on the gusset: kept on the stack face and
  warned as a clash = batch 5), floating 88 -> 4; air left 7,301 (6,960 gusset-to-member gaps = missing fills / splice / inlay plates, batch 3; one CHECK warning).
  Test `TrussBatch1Tests.JointRivetGrips_*`.
- [x] 1b Built-up bracing members turned 90 deg: STRUT, LAT31, LAT50, L13-W14 floor lateral, FLAT21 (BW-S1..S3) - `TrussSectionDefinition.Rotated` now also turns
  LacedChannels / Box; set in truss-ptc-arch.csx (FLAT21 b.b. 13 -> 1'-1 5/8, plates (13-6) x 3/8). Depth x width 19 x 32 1/4 / 14 x 32 1/4 -> 32 1/4 x 19 / 32 1/4 x 14,
  L13-W14 16 x 13 5/8 -> 13 5/8 x 16, FLAT21 12 3/4 x 13 -> 13 5/8 x 12 3/4. MOVED (batch 5): 394 bracing plates (up to 24 in.), 252 edge angles (13 in.), 182 bracing members
  re-trimmed (LAT31 +58.6 k lb, LAT50 -3.6 k lb), bracing rivets 23,120 -> 16,776 (3,684 positions now within 1 1/4 d of a plate edge, not placed). Test `TrussBatch1Tests.LacedChannels_*`.
- [x] 1c Continuous stringers over several floor beams, p.72 (BW-M2) - `TrussStringerLine.ContinuousBays` / `RunTypes`; p.72 diagram read: pavement breaks at 2-6-10-14-18-22,
  2-span S13-S16 over 0-2 / 2'-0', 4-span S9-S12 between (12 members per line, not 11 / 22 as the review row said). 484 bay pieces -> 132 members, mass / volume unchanged; one straight
  solid per bay kinked at the floor beams. Open: which inner 24 WF 94 line is S12 (labels between lines, "S11 / S12" written), the p.72 stringer splice at the middle support not built.
  Test `TrussBatch1Tests.ContinuousStringers_*`.
- [x] 1d Every ply / angle / cover / fill / gusset LAYER its own element; member and joint gusset stack = parent assembly (BW-M1; user confirmed 2026-10-27) - done as designed
  below (`TrussDefinition.PartPerPiece`). PTC: 33,539 -> 41,484 parts + 5,991 member / stack rows in the DB; IFC 277.6 MB / 2.75 M entities, 5,279 'BUILT_UP_MEMBER' + 712 'GUSSET_STACK' assemblies; DB 556,566
  elements, every element still on a detail sheet (rules at level part also take level member); masses / volumes / rivets unchanged by the split; checks unchanged except the
  stringer count row (now 11 x 12 continuous members). Tests `TrussBatch1Tests.PartPerPiece_*`.
  DESIGN (2026-10-27):
  - Input: OPTIONAL `TrussDefinition.PartPerPiece` (omitted when false; other models byte-identical). Set by truss-ptc-arch / truss-ptc-approach / girders-ptc.csx.
  - Generator: `BuildMember` emits one `SteelPart` per `TrussSectionPiece` (and per lacing face); new OPTIONAL `SteelPart.Member` (parent: member mark or joint stack)
    and `SteelPart.Piece` (web ply / angle / cover plate / channel / rolled shape / plate / lacing); mark `<member>-<code><n>` (W web ply, A angle, C cover plate,
    CH channel, R rolled, P plate, LB lacing); category = the member's (layers, clash, quantities unchanged); end cuts (mitre, vertical, notches, holes, perforations,
    TrimMember) per piece; nominal lb/ft split by piece area. Gussets (every layer, both faces) and joint fills / splice plates keep their marks and get
    `Member` = `<plane>-J<point>` (the joint stack); member fills / plates on one member get that member. Probes that excluded "the member's part" exclude all its
    pieces. Rivet grips, steel mass, volumes: unchanged (same solids).
  - IFC: one IfcElementAssembly per member (USERDEFINED 'BUILT_UP_MEMBER', name = member mark, new key pattern `IfcKeys.Member`) and per joint stack ('GUSSET_STACK')
    aggregated into the truss-line / bracing / floor assembly; pieces IfcPlate (plies, covers, plates, lacing) / IfcMember (angles, channels, rolled) inside it;
    part GlobalIds stay `IfcKeys.Part(bridge, mark)`.
  - DB: new level "member" row (category = the member's, mark = member / stack mark) between the panel / joint group and the pieces (level "part", subtype = piece
    kind). Sheet links: PTC rules targeting level "part" become ["part", "member"]: the member is linked, its pieces inherit (nearest linked ancestor).
  - App (1e): pick level (default MEMBER: a click on a piece selects the member / joint stack; Alt+click or ribbon toggle "Pick: member | part" = the piece);
    the database panel lists the member's pieces (row click = highlight it); view option "Show sub-elements" = boundary edges between pieces (+ alternating tints);
    Isolate on a member = its pieces. Pure logic `SBM.Core/Geometry/SubElements.cs` (pick-level resolution, piece listing) unit-tested; UI built, not visually verified.
  - Size on PTC (count on today's model): 5,631 members -> 13,928 pieces = +8,297 parts (33,486 -> 41,783 steel parts, +25 %); + ~5,600 member and ~1,800 stack
    assemblies in IFC (est. +6-8 MB on ~260 MB) and DB rows; triangles unchanged (1.57 M member triangles), pick-table entries +25 %.
- [x] 1e App: pick member by default, Alt+click (or ribbon toggle) = single sub-element; sub-element list in the panel; "Show sub-elements" edges / tints; isolate member = its sub-elements (UI not visually verifiable in the sandbox) - `MainWindow.SubElements.cs` + DB panel list, builds; logic tested (`TrussBatch1Tests.SubElements_*`); NOT visually verified

## Batch 2 - bearings
- [x] 2a L11 / L11': gussets to the p.80 Section E line + shoe angles 1-L 8x6x7/8, 1 1/2 tap bolts; Section F conflict reported (BW-B1..B3) - every gusset layer to the forging shoulder 2'-4 (`TrussGussetExtension`, per layer); FIN line is 2'-5 in a 1 in. seat that fits the plan stack, not the model's (CONFLICT, batch 3a); outer 1-L 8x8x7/8 x 4'-1 + 4 inner L 8x6x7/8, 52 tap bolts 1 1/4 (p.80 note: 1 1/4, not 1 1/2) + 40 rivets per shoe; gap 10.1 -> 0. Test `TrussBatch2Tests` (batch 3: the two plates at the shoe now go to the 2'-5 FIN line in the 1 in. forging seat; Section F conflict resolved)
- [x] 2b L0 / L0': sole 3/8 lower, 7/8 plate under the chord, pin clear of the sole (BW-B4, B5) - sole 5 3/4 to the pin (2 1/2 flat + 3 1/4), beveled to the 2.864 % chord, block with a half bore on the pin; 7/8 x 24 x 19 plate flagged (the '7/8 Tension Pl.' callout is on top of the end strut, not under the chord); gap 0.49 -> 0 (1.49 under the angle legs beyond the 24 in. plate)
- [x] 2c Pier 19 masonry plate 9'-2 (BW-B6) - 104 -> 110 in. across (p.96 View A, as-built revision 1 3-9-56); masonry -> pier 0

## Batch 3 - gusset layering
- [x] 3a L11: 4th 3/4 layer, in-to-in 26 3/4, chord outer plies end at the joint, layer extents per p.80 (hi-res read of B, D, G, H, K, L) (GL01-GL04, H01) - 1 1/8 + 3 x 3/4 at 26 3/4 in-to-in, outer 1 1/8 chord plies stop at the L11 gusset (`TrussPly.InlaidAt`), second / outer 3/4 to the Section E steps (~7'-0 / ~3'-9, scaled), 1 1/8 + first 3/4 to the 2'-5 FIN in the forging seat (Section E vs F CONFLICT, F governs); 3/4 ply ends, L11 splice plates / fill NOT built (arrangement not determinable, flagged); 12 inner-ply mitre clashes remain
- [x] 3b Plates under the gussets from U8 / L8 on: web splice plates, L10 inlays, U22 pin plates (GL07) - o.s. splice plates under the gussets (`TrussJointPlate.UnderGusset`, lengths flagged) at U8 / L9 / U10 / U21 / L21 + U22 pin plates; gusset air points per face U8 262->70, L9 329->84, U10 312->80, U21 473->136, L21 546->159; inlays / i.s. plates / fills not built (inside the web stack); L21 34x1 vs 1 1/8 CONFLICT reported
- [x] 3c gusset-layers.csv clean-up; read W14 (p.85) and L5 fills (p.75) (GL09-GL11, G03) - U0 34->29, rows L1 / U22 / W14 added; W14 2'-5 in-to-in (thickness not given, flagged; hanger overlap gone); L5 fills: no dimensions and no step to fill - not built, reported

## Batch 4 - joint gussets and rivets
- [x] 4a rivets-ptc.csx: wind chord / rigid hanger rivets wrongly given to the diagonals (R08, R01) - members to a non-chord point (hanger L14-W14, wind chord L13-W14) get their own class by role; the hanger now takes its drawn groups (its end now follows the chord, 4c)
- [x] 4b Drawn joint rivets not placed (15 %): dump dropped positions, fix causes (R01, R02) - `TrussModel.DroppedRivets` (reason per position); N-plane drops 3,261 -> 3,233 (range 297 -> 253, head 57 -> 39); the rest = symbols outside the member section's in-plane extent / no clean stack (1,892 / 1,049), not gusset rivets or members narrower than drawn - left, listed per group
- [x] 4c Web member end overlaps; verticals L12-L14 ending 10-14 in. high (G01, R06) - `TrussEndCut.ParallelToChord` (U12-L12 / U13-L13 / U14-L14 + hanger L14-W14: 32.8 / 31.1 / 29.7 -> 12.0 / 14.1 / 13.0 in. above the WP) and `WebToWebClearance` 0 (setback <= 6 in. / corner cut / abut): web overlap clash rows 1,448 -> 124 (U11 post x L10-U11, L10 U9-L10 x U10-L10 left, reported)
- [x] 4d Gusset free-edge angles + rivets (~20 joints) (G02) - `TrussJointDefinition.EdgeAngles`: 36 definitions from the 20 drawn strips (U11 5x3 1/2, others 4x3 1/2 x 7/16), 144 parts, flagged; rivets only where counted (L13, L15); U14 / U15 strips and joints without a read strip not built
- [x] 4e Chord cover splice plate rivets (R04) - `TrussJointPlate.RivetCount/RivetRows`: the counted 1 1/8 rivets on every built cover splice plate (splices.csv counts, grip probed); plates without a scaled length still not built
- [x] 4f Rivets near / past gusset edges; outline misreads U11 / L10 / U5; L11 envelope (R05, G04-G07) - L10 top edge 7 in. lower and U5 top-left corner 8 in. right (re-traced on the 300 dpi overlays, 11 rivets moved to 'beyond'); U11 re-trace put drawn rivets outside it (the line is the chord end) -> kept; L11 envelope kept (edges not separable); edge-distance CHECK rows unchanged elsewhere

## Batch 5 - lateral / sway / portal bracing connections
- [x] 5a Lateral / strut plates on the laterals, edge angles on the plate (B01, B02; after 1b) - members rolled into the lateral plane, plate in the member plane, beveled fills, 4 rivet lines across each member, faces probed: rivets gripping only their own plate / angle 11,874 -> 144 (FX only), member ends without a rivet 392 -> 56, heads under angle legs 222 -> 20 (TRUSS-DESIGN 30)
- [x] 5b Rivet counts per p.71 (B03, C02) - p.71 counts per end in bracing-ptc; check-truss-ptc 15 PASS rows + CONFLICT rows C01 / C02
- [x] 5c Sway frame connections L1 / L9 / L10 / L12 + top connections to the floor beam, p.85 (B04, B05) - sway V re-framed (top at the floor beam, apex on the strut CL, p.71 / p.97 / p.85), 26 apex + 52 top plate pairs; sway ends without a rivet 92 -> 4 / 104
- [x] 5d L13 - L13 1/2 lower lateral double K + strut, p.98 (B06) - L13 1/2 at 13'-1, strut + 4 x 33 WF 130 per half; F8 apex gussets not built and W33 ends at L13 1/2 weakly connected (open, flagged)
- [x] 5e U11 portal connections + built-up portal strut; LM0 apex plate; tongue beam WM22; F8 plate at L13 (B07-B11, BW-S4) - portal box from the p.71 sketch (CustomPieces, -0.29 M lb), LM0 plates built, LM13 plates 3/4 (p.95); portal end / knee connections and WM22 not built (flags); knee pieces clash the new box <= 0.28 in.

## Batch 6 - walkways
- [x] 6a FB11 walkway El. 141.6035 + offsets 3'-0 3/8 to 5'-0 3/8; ladder length (BW-W4, W5) - El. 141.6035 = 26.5 in. above the FB11 flange angles at the truss end (plan 2'-2 1/2); the 27.74 in. first printed was measured to the 46 ft bottom cover plate (bws-recheck N4)
- [x] 6b Top-chord catwalk + wire-rope handline; U22 beacon platform; nav-light platforms; south ladder piers 18/19; cages, toe plates (BW-W1..W3, W9, W10, W13) - built, hardware / hangers / cage depth flagged
- [x] 6c Pier platform height 8 13/16; cross walk channel conflict reported (BW-W7, W11)

## Batch 7 - sheet links
- [x] 7a W14 -> p.84 / 85; main floor beams / stringers off p.55; wind chords + p.71; LL-CL16 under the right truss (S01, S02, BW-M4..M6) - done: W14 joint rule p.84 / 85, wind chord rule p.69-71, main floor system without p.55 (only the stringer seats keep p.55 + p.72, they were modeled from it), walkways / platforms + p.105 / 106; LL-CL16 already in BR-LOWER-LATERALS (no change); S02 p.69 kept on L13-L17 (used for the L14 inlaid layer)

## Batch 8 - re-review of the rebuilt model (2026-10-28 evening; files connections-recheck.md, gusset-layering-recheck.md, bearings-walkways-sections-recheck.md)
Status of the old rows: connections 10 fixed / 10 partly / 12 open; gusset layering 4 fixed / 4 partly / 2 open (+2 OK); bearings-walkways-sections 20 fixed / 9 partly / 14 open (+2 OK). NEW rows:
- [x] 8a (2026-10-30) Rivet heads overlapping: bracing / floor beam connection rivets vs joint rivets 1,806 rows (1,300 > 1/4 in.; L11, L9, L6, L8, L16, L0, F8 at L13); joint heads under edge-angle / end-connection legs 346 (conn N01 / N02, gus GN05)
- [x] 8b (2026-10-30: 21 fill definitions from the joint sheets, scaled + flagged, connections-ptc.csx BATCH 8b) Web-member FILLS between members and gussets not built (U6 / L7 18 x 1 p.76, U8 18 x 3/4, L9 21 x 3/4 p.77, U10 26 x 1 1/8 p.78, U21 / L21 18 / 21 x 1 1/8 p.89): 2,554 rivets over air (gus GN01, conn N05)
- [x] 8c (2026-10-30) U13-L13 and hanger L14-W14 penetrate the kinked lower chord 0.51 / 0.57 in. (batch 4c web clearance; conn N04)
- [ ] 8d W14 outer gusset plates unriveted - read p.85 (conn N06); W14 thickness not given (GL10)
- [~] 8e (2026-10-30: GN02 = the U22-L22 stepped fills, built with 8b; GN04 layer step 44 in.; GN03 = generator ply span, open) U22 rivets under the pin plates 3.13 in. air (GN02); U11 only one post ply under the gusset, 17 rivets / face with air (GN03); L11 rivet row 0.56 in. from the outer layer edge (GN04)
- [x] 8f (2026-10-30: posts added; p.106 walk 'Parallel to Wind Chord' -> the walk rises to the platform, TrussLineItemDefinition.Rise) Nav-light walks: posts missing (rails float), 20.5-23.3 in. below the platforms with no step - re-read p.106 (bws N1 / N2)
- [~] 8g (2026-10-30: rail L 3x2 1/2x5/16 per p.106 re-read; connection angles / FB11 hangers / pier platform rails read in work8/batch8-data.md, not built) CL walkway single rail: connection to the floor beams (6x4x3/8 Ls, PL 14 x 3/8) not built (bws N3); FB11 walkway hangers (W6); pier platform rails on the 8'-0 side per View B + hangers (W8)
- [ ] 8h Still open: drawn joint rivets not placed 13.8 % (R01: L10 U9-L10 51/97, L13 U12-L13 58/96); bracing connections WM22 (B08), U11 portal ends (B09), floor laterals FX14-20 / F8 (B10 / B11), L13 1/2 K far ends (B06); edge angles 12 joints, 40 / 144 riveted (G02); L11 1 1/8 + first 3/4 layer share one envelope (GL03); inlay plates (U5, GL07); L22 split at CL (GL06); L5 fills (GL11); L0 1.49 in. under the chord angle legs beyond the 24 in. plate (B4); shear lock / hold-downs (B8), forging key (B7); L13-W14 12 x 1/2 web plates; sections waiting on user decisions S5-S9, S14
- [~] 8i (2026-10-30: L11 shoe angles / tap bolts / rivets -> p.80 + p.96 rule `main-shoes-l11-angles-fasteners`; GN08 CHECK now measures the member width over the gusset lap only; GN07 stays a reported plan conflict for the user) Links: L11 shoe angles -> p.80 (bws N5); plan conflict L11 Section E vs F plates to the shoe (GN07, report); U11-L11 width CHECK wording (GN08)

Batch 8 results (2026-10-30, `PTC Delaware River Bridge.sbm` rebuilt from connections-ptc through rivets-ptc; main span `sbm clash --bolts`):
- Root cause of 8a: the shared-hole rivet index was built at the first MergeRivet (joint PLATE rivets) and the joint rivets placed afterwards were never added, so
  connection / bracing rivets could neither share their holes nor see their heads. Fixed (AddRivet indexes); plus OPTIONAL `TrussDefinition.ConnectionRivetsClearOfJointHeads`
  (set in rivets-ptc.csx): a connection rivet whose head would overlap a joint rivet head without sharing the hole is not placed (counted per assembly).
  Bolt x Bolt 2,406 -> 650; bracing / floor x joint ~1,700 -> 50; the rest = 552 joint-vs-joint rows (chord stitch rivets, R07, unchanged) + 48 at the L11 shoes.
- 8c / N04: `ParallelToChord` cuts clear EVERY chord face at a kinked chord (corner test, not the mean face): Chord x Vertical 76 -> 0, Chord x Hanger 40 -> 0.
- N08 (part): explicit `ParallelToChord` now also works on a chord-role member; the wind chord L13-W14 / W14'-L13' end at L13 uses it (connections-ptc.csx):
  Chord x Chord 124 -> 84 (left: the L11 mitre inner plies).
- check-truss-ptc unchanged 624 PASS / 11 DIFF / 73 INFO / 2 CONFLICT.
- Second pass (2026-10-30): N07 portal knee top on the lowest p.71 box bottom (knee clashes 6 -> 0), sway apex edge angles on the lateral plate, bracing fills clipped clear of
  the edge angle (L13 1/2 fill clash gone; the angle stays 0.075 in. into the strut channel); L21 O.S. splice 34 x 1 1/8 (CONFLICT row dropped). check-truss-ptc 624 / 11 / 73 / 2.
- Still open (plan reading / generator): 8b fills, 8d W14 outline + rivets (p.85 read: floor beam + rigid hanger detail, 2'-5 in-to-in gussets, 1 1/8 rivets; not traced),
  8e, 8g, 8h, N03 buried heads, N07, L11 mitre plies, sloped nav walks.

## Batch 9 - gusset / rivet agent reviews (2026-10-30; gussets-review/, rivets-review/, ../approach/review/, ../girders/review/ outcome sections)
- done: gusset data (G23, G42, G43, G48-G52, G56), near-duplicate rivet symbols (R04), cover splice counts (R06 / R07), L11 strut A325 bolts (R08), joint rivets share holes (R13),
  inscribed rivet heads, approach printed fills + Omit Fills + similar-to grid fallback, girder shoe heights / rivet zones / stiffener / splice / lateral rivets.
- results: check-truss-ptc 616 / 19 (8 new U14 / U15 gusset-cover rows from the corrected outlines), location 389 / 37, approach 1792 / 16, girders 128 / 9; Bolt x Bolt main 650 -> 184,
  3-span 36 -> 22, 4-span 86 -> 20; tests 738 / 738.
- open: R01 (re-read work points), G45 cover splice plate lengths, G53 / G54, R05 stitch rivets, R09, R10, R14, approach R04 / R08 / R10 / A11-A15, girder G12.

## Wrap-up
- [x] Full PTC rebuild through the whole script chain (also attaches the plan PDF and the floor-system category), `sbm pipeline`, agent review
- [ ] Re-run the three main-span reviews on the rebuilt model; new rows go back on this list
- [x] Full test suite; IFC validation of the PTC export
- [x] Rebuild temp\cli-main with all changes; CLAUDE.md / TRUSS-DESIGN.md updated

Wrap-up results (2026-10-28): rebuild from `sbm new` = identical check rows to the incremental model; checks main 624 / 11 / 73 / 2, approach 1792 / 16 / 168 (4 more DIFF than the
Oct-21 1796 / 12: gusset-covers-rivets U11' N / S in both 4-span units, from the earlier batches), girders 131 / 6, location 390 / 36; pipeline: agent review 1,245 open (11 unlinked
sheets = geology / borings / test piles / abutment sections / culvert, 64 inputs at the starting value - mostly explicitly set values equal to the default (Side = 1, AngleCount = 2):
confirm with `sbm review --confirm`, 1,170 generator flags); IFC 289.4 MB / 2.80 M entities, 0 schema + 0 EXPRESS issues; suite 681 / 682 (DeckSolidsTests.KinkedEnd_* = Rhino WIP).

## Other open work in this session (not main span)
- [x] Standard cross frames implementation + twist evaluation (2026-10-27; full suite not re-run after the last sheet fix)
- [x] Cross frames: connection plates flat on the frame plane, frame station at the reference girder's top of web, new default for all models (legacy switch `PlatesAtEachTopOfWeb`)
- [x] Cross frames: default frame plane MIDWAY (renders as exact extrusions), Warped selectable
- [x] Rhino export: deck elements closed B-reps / extrusions (Woodbury, Nebraska, NHT: 0 meshes); suite 668/668
- [x] Rhino: slabs I-80 U1-U3 / Ramp G U4 closed, NHT arch deck / concrete diaphragms / tub haunches closed, kinked-end blocks closed except below; regen of kinked-end units 160-410 s -> 19 s (2026-10-28)
- [ ] Rhino leftovers: I-80 U3 11WB blockout + end block and Ramp G 19G blocks = closed meshes (fit across 3.4 mm after a split / sliver piece with a seam trim); U2 5WB end block 3dm +0.22 % (sliver piece overlap); test KinkedEnd_* failing on the same fit
- [ ] Cross frames follow-up (answers 2026-10-28; queued): bearing ends stay on the stiffener; plate / channel diaphragm warp option (default no); families at 1/16"; family select in the app (click frame, select family, Alt+click part); misfit / twist status layer; standard frames only when plans / input define them; confirm Nebraska cross frames are as the plans define
- [ ] Plumb bearing stiffener bevel on a grade: user said BEVEL IT (queued)
- [ ] I-80 / Ramp G bearing stiffener mill to bear: arrow to the top of the bottom flange on the bearing stiffener detail, misc. steel details sheet - read it, set EndFits with the page (queued)
- [ ] (old) Cross frames: remaining answers on family rounding, WP convention, bearing / other stiffener orientation = from the plans per support (flag when not called out, user 2026-10-27), misfit layer
- [ ] Plumb bearing stiffener vs sloped flange at zero skew (0.76 mm) - user to decide
- [ ] I-80 / Ramp G bearing stiffener fit: page with a mill-to-bear note, if the plans have one
- [ ] Commit when the user asks

## After PTC (queued 2026-10-28, user: "design now, build after PTC")
- [x] LOD selector stage 1 (2026-10-28): settings, cache key, reduction, truss rivet skip, IFC Pset, CLI --lod, app button
- [ ] LOD stage 2: concrete solid reduction, per-output levels, generator skipping (I-girder bolts, rebar)
- [x] Roads stage 1 (2026-10-28): template / courses / curbs / side slopes, IFC IfcRoad, sbm roads, tests
- [x] Roads stage 2 (2026-10-29): viewport layers, DB rows, earthwork volumes, approach slabs (Owner per bridge end), road ends tied to the bridge, clearance check under the bridge
- [ ] Roads: LandXML corridor / surface import
- [x] Cantilever retaining walls stage 1 (2026-10-29): stem / footing / key / steps / joints, IFC, DB, viewport, sbm walls
- [ ] MSE walls (contract-plans envelope), wall coping / piles / rebar; utilities - design note `docs/plan-data/roads-walls-utilities-lod-design.md`, build order section 6, answers section 7a (generic test models)
- [ ] PTC agent review: 54 starting values confirmed (qc/ptc/agent-review-defaults.md); 10 open = not on the plans (deck concrete strength x7, girder-span web clearance x2, E abutment batter); suspender 'one rope' flag can be closed (p.94 / p.70 / p.42)
