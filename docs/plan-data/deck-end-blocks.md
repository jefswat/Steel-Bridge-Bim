# Deck end blocks ("END DAM DIAPH.") at the expansion joints (I-80 WB Design 116, Ramp G Design 316)

Source: `dist/SteelBridgeModeler/B00168` (I-80) and `B00169` (Ramp G). PDF page = 1-based, design sheet in parentheses. Geometry was measured from the
PDF VECTOR paths of the section details (exact line coordinates, then scaled with the dimensioned 1'-2 / 1'-0 / 2'-2) and the dimension strings were
read on raster clips. Working files: `temp/deckend/` (paths.py, clips) in the worktree.

## 1. What the plans show
The plans call the thickened slab end the **end dam diaphragm** (bar list: "END DAM DIAPH."). It is a concrete diaphragm across the whole deck at every
expansion joint end; the girder ends run INTO it.

| Item | I-80 piers (5WB, 11WB) | I-80 abutments (West, East) | Ramp G piers (5G, 9G, 14G, 19G) | Ramp G South Abutment |
|---|---|---|---|---|
| Depth, top of slab to soffit at the deck end | 2'-2 (1'-2 blockout + 1'-0 below) | 2'-2 (same) | 2'-2 | 16 1/16" = 8" slab + 8 1/16" below the slab soffit |
| Soffit flat, deck-end face to start of 45 degree chamfer | 3'-4 1/2 (1'-3 1/2 + 2'-1 at 11WB; 2'-3 + 1'-1 1/2 at 5WB) | 2'-10 (1'-6 + 1'-4) | 3'-3 (1'-2 + 2'-1; the dim string 2'-2 + 1'-1 1/2 reads 3'-3 1/2) | ~1'-9 1/2 scaled, then a 6 1/2" step up and a short chamfer (not dimensioned) |
| Chamfer | 45 degrees up to the 8" slab soffit (drawn run = rise = 18") | same | same | step + 1" chamfer |
| Blockout (notch in the top) | D = 2'-1 wide, 1'-2 deep | 1'-6 wide, 1'-2 deep | 2'-1 / 1'-2 | strip seal pocket ~4", depth not dimensioned |
| Width across the deck | full deck width between the deck edges (sections show the block under the barrier; at the barrier "6" at the barrier, 1'-3 wide end block at the bottom" on Ramp G pdf 138 section C-C) | | | |
| Pages | pdf 185 (178) sections B-B (W Abut), C-C (5WB); pdf 190 (183) B-B, C-C (5WB / 11WB); pdf 195 (188) B-B (11WB), C-C (E Abut) | pdf 185 B-B, pdf 195 C-C | pdf 137 (135) B-B, pdf 138 (136) C-C, pdf 140 (138) D-D/E-E (Pier 19G) | pdf 137 section A-A |

Vector measurement (pdf 185 C-C, drawing points; 3 pt = 1" vertically): deck end x = 741.6, blockout edge 669.6 (72 pt = D), flat soffit to x = 627.6
(114 pt from the face), chamfer to (573.8, 611.2): dx = dy = 53.8 pt, i.e. exactly 45 degrees, ending 8" below the top. The abutment section (pdf 185 B-B) has
D 48 pt and the flat soffit 90 pt (1.875 D: 2'-10 for D = 1'-6). The identical outline repeats on pdf 190, 195 and Ramp G pdf 137, 140.

### Relation to the steel (the important part)
- **Girders:** the soffit (2'-2 below top of slab) is BELOW the girder top flange (top of flange ~10" below the top of slab) and about 14 1/2" below the
  top of web. The girder top flange, its shear studs and the upper web and the upper ends of the bearing stiffeners are cast into the block. Evidence:
  Ramp G pdf 138 "PARTIAL SECTION NEAR EXPANSION-EXPANSION PIER" (clip at 420 dpi): the cut top flange with studs sits above the heavy soffit line, the vertical
  5d hoop bars pass both sides of the web, the bearing stiffeners run up to the flange and the end cross frame gussets start BELOW the soffit. The plans
  give no separate flange/stud detail, no web-embedment dimension.
- **End cross frames / diaphragms:** the top strut of the end frames (Type 10 / Type 4) has its bolt group starting 2'-2 below the top of slab, that is at the
  soffit (sheets read earlier, see `samples/plans/cross-frames-*.csx`). At the Ramp G South Abutment (pdf 137 section A-A, 500 dpi clip) the W24x68 abutment
  diaphragm has its top flange CAST INTO the end block: the flange bottom is flush with the soffit (top of W24 = 4" below the top of web = 15 1/2" below
  top of slab, flange bottom 16 1/16" = soffit), the web hangs below. The "2" dimension in that detail is the 2" clear below the bottom bars.
  No such detail was found for the I-80 abutment frames; whether their W24x68 is also flush is OPEN.
- **Abutment backwall interface:** deck end to backwall face = C 6" at 50 F (I-80); Ramp G strip seal 2 1/2"; 3/4" drip groove with a 2" step in the soffit at
  the end face (Ramp G A-A); the block does not touch the backwall (the joint gap is between them). Paving notch is on the abutment sheets (abutments.md).

### Reinforcing of the end block (2026-10-11: Ramp G 5d2-5d5 at Piers 5G/9G/14G placed, the rest schedule only - deck-rebar.md section 6)
Bars are placed by the same bar lists as the slab (pdf 196 = design 189; Ramp G pdf 141 = design 139): hoops 5d1/5d2/5d5/5d6/5d10/5d12 (hoop around the
stirrup stack, D = 2" pin on the bend diagram), straight 5d3/5d4/5d9/5d11, 4d7/4d8 (small bars), longitudinal 5e1-5e11, all epoxy coated.
Placement: transverse 5d bars at 1'-0 along the joint ("@ 1'-0"), 10 bars between girders and 2 outside the exterior girders at equal spacing, positioned parallel
to the CL approach roadway; 5d3 "FIELD CUT OR SPACED AS REQUIRED AT EXPANSION JOINT SUPPORT BOXES"; 5e longitudinals: 3 or 4 bars each in the top (at the
barrier-notch shelf) and bottom of the block, min lap 2'-2, 2" clear, 3" / 1 1/2" at the drip groove; sloped 5e3/5e4/5e7/5e8 follow the chamfer.
I-80 Unit 1 quantities (pdf 196, interleaved text, assignments of the columns UNCERTAIN): 5d1 54 x 6'-4, 5d2 54 x 8'-2, 5d3 108 x 7'-3, 5d4 54 x 5'-5,
5d5 54 x 9'-0, 5d6 54 x 7'-0, 5d9 54 x 5'-11, 4d8 4 x 4'-6, 5e1 6 x 44'-3, 5e2 18 x 44'-9, 5e3 40 x 15'-6, 5e4 16 x 3'-10, 5e5 6 x 32'-0, 5e6 14 x 32'-6, 5e7 50 x 11'-2,
5e8 20 x 2'-8. Ramp G Unit 1 (pdf 141): 5d1 abut hoops 36 x 4'-2, 5d2 62 x 6'-2, 5d3 62 x 8'-0, 5d4 62 x 7'-3, 5d5 62 x 5'-5, 4d6 16 x 4'-1, 5e1 60 x 9'-4,
5e2 14 x 34'-10, 5e3 40 x 2'-9, 5e4 6 x 33'-4. The bend diagrams (hoop leg dimensions) are small images on the bar list; they were not read, so no end-block bars are
built. Per the user rule, nothing is guessed.
2026-10-11: the Ramp G bend diagrams (PDF 141) were read: 5d1 open hoop 10 1/2 x 1'-5 with 6" legs (4'-2), 5d2 hoop 11 x 1'-8 (6'-2), 5d3 hoop 2'-10 x 8 (8'-0), both with 6" tails at one
corner, 5d4 U 4'-0 / 1'-9 / 1'-6 (7'-3), 5d5 1'-6 + 2'-5 at 45 deg + 1'-6 (5'-5), 4d6 1'-10 + 3 + 1'-3 slanted + 9 (4'-1); D = 2 1/2" (4d6: 2"). The Ramp G partial section (PDF 138) dimensions the
5d layout along the joint at Piers 5G, 9G, 14G (10" + 8 @ 1'-0 + 10" per bay, 10" and 1'-0 outside): those bars are placed. I-80 has no such dimensions (counts per bay only): its end dam bars stay
schedule only (UNRESOLVED).

## 2. What the model builds
`ExpansionJointDefinition.EndBlock` (`DeckEndBlockDefinition`, optional, default off): `Depth`, `FlatLength`, `ChamferRun` (null = 45 degrees to the slab
soffit) or an explicit `Soffit` polyline (X from the deck-end face, Drop below top of slab). `ConcreteGenerator.BuildEndBlock` sweeps the closed outline
(blockout bottom at the face, soffit, back along the slab soffit to the blockout edge) across the full deck width (same columns as the deck solid), following
the finished grade and the skewed deck end. The blockout is then built to its full plan depth (1'-2) and the "blockout depth exceeds modeled slab" warning is
not given. Result: `ExpansionJointResult.EndBlock` (`DeckEndBlockResult`: mesh, soffit, `Probe(point)` = inside / height above soffit). Viewport layer `3D-JOINTS`, material
row "Deck end block", IFC `IfcBuildingElementPart` "DECK_END_BLOCK" aggregated into the deck `IfcSlab`.
Not done: block reinforcing except Ramp G 5d2-5d5 at Piers 5G/9G/14G (`DeckPlanReinforcement.EndDam`); haunch solids are not trimmed where they run through the block (a small overlap, counted twice in haunch volume).

`DeckEndBlockCheck` (run by `RegenerationCache` after steel and concrete exist): every steel part (vertices + triangle centroids), stud and bolt inside a block is
classified. INTENDED (Info, one line per block): girder top flange, upper web, stiffeners, welds, shear studs, and a rolled top strut whose flange is within
1" of the soffit (the Ramp G W24x68 detail). Everything else is a WARNING "CLASH not detailed in the plans" with the part count and the height above the soffit.
Nothing is moved. `ConcreteModel.EndBlockClashes` holds the rows.

## 3. Results on the plan models (`samples/plans/joints-i80.csx`, `joints-ramp-g.csx`)
Block volumes (CY): I-80 Unit 1 West Abut 15.0, Pier 5WB 12.0; Unit 2 5WB 22.0, 11WB 16.2; Unit 3 11WB 24.4, East Abut 14.0; Ramp G piers 6.5 each (19G 7.7),
South Abut 1.6. Embedded as intended: girder top flanges (top ~15 1/2" above the soffit), upper webs (14 1/2"), stiffener tops (10-15"), welds, studs.
Warnings (model vs plans, inputs not changed):
- I-80 piers: the W12x40 Type 10 end-frame top strut projects 2 5/8 to 2 7/8" above the soffit (flange 0.5"); bolts up to 3/8" above the soffit.
- I-80 West Abut: the W24x68 abutment strut is 3 1/4" above the soffit, 17 abutment-frame bolts, 2 gussets (12"), 2 top-chord pieces (10 3/16") and welds are
  inside the block; East Abut: W24x68 3 1/2" above, bolts 3/8". The abutment frames' top position was set from "bolt group 2'-2 below top of slab";
  the Ramp G detail has the W24 flange flush with the soffit. Check the I-80 abutment frame sheet.
- Ramp G piers: W10x33 Type 4 end strut 2 1/4 to 2 5/16" above the soffit, bolts 3/8". Ramp G South Abut: W24x68 flange flush with the soffit, no clash.

## 4. UNCERTAIN / open
- Ramp G pier flat soffit length 3'-3 vs 3'-3 1/2 (two dimension strings); I-80 abutment/pier lengths read from dimension strings and confirmed by vector scaling (+/- 1/2").
- Ramp G South Abutment block plan length (~1'-9 1/2) scaled from the drawing, not dimensioned.
- I-80 abutment end frame top elevation relative to the soffit; bar bend diagrams for 5d/5e marks; whether the block is full width under the barriers
  (modeled full width between the deck edges; Ramp G pdf 138 section C-C shows a reduced 1'-3 wide block at the barrier).

## 5. Kinked deck ends (2026-10-11)
The end blocks, blockouts and joint bodies follow a kinked end of slab (`SupportDefinition.DeckEndPoints`): I-80 Unit 2 at Pier 5WB / 6A (block 22.39 -> 22.40 CY, blockout 10.58 CY) and Ramp G Unit 4 at
Pier 19G (Details A/B of PDF 141; block 7.68 -> 7.78 CY, blockout 3.76 -> 3.82 CY). The Detail B step at the Ramp G left corner is not coordinated and is modeled perpendicular to the edge as drawn (READING).
The reduced end block under the Ramp G barriers (section C-C, PDF 138: "1'-3" at the bottom) is still not modeled (the block runs full width).
