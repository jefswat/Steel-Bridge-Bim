# Precast segmental box girder - design note (v1, Bonner Unit 5)

Feature for NCDOT B-2500 navigation Unit 5 (spans 19-29, 3,550'-0" along -L-, balanced cantilever from bents 19-28). Data: `SUMMARY.md` and the CSVs in
this folder; "p." = PDF page of "B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf". Model: `samples/plans/build-bonner-unit5.csx` ->
`samples/plans/Bonner Unit 5 segmental.sbm`, check `samples/plans/check-bonner-unit5.csx`. Code: `src/SBM.Core/Bridge/SegmentalDefinition.cs`,
`src/SBM.Core/Segmental/` (`SegmentalSection`, `SegmentalLoft`, `SegmentalGenerator`, `SegmentalModel`). Tests: `tests/SBM.Tests/SegmentalTests.cs`.

User rules applied: model the plans; nothing invented - every open item is an UNRESOLVED warning with page + plain reason (and `Assumption` on the element
that was modeled anyway); GAP = not modeled; 1/16" / 0.01 ft; plan-vs-model differences are reported, never adjusted.

## 1. Data model (`BridgeDefinition.Segmental` = `SegmentalDefinition`, OPTIONAL, omitted from the .sbm when null)

Like the tied arch, a segmental bridge is its own `BridgeDefinition` with `Girders.Count = 0` (allowed only with `Segmental`; FramingGenerator builds the
support lines only). Supports = the bents (here "Bent 18" .. "Bent 29", radial, `Substructure.Enabled = false` - the segmental generator builds the bents).

| input | meaning | Bonner value / source |
|---|---|---|
| `Box` (`SegmentalBoxSection`) | top width, top slab + flat width, top haunch run, wing tip / flat / root, tip-to-web, web slope (V:H) and normal thickness, bottom haunch rise / run, fillet R | 42'-7", 9", 5'-1", 5'-6", 10" / 4'-1" / 2'-2", 9'-4", 4:1, 1'-4", 6" x 2'-5 1/2", R 1'-0" (S3-20 p268) |
| `DepthTable` (`SegmentalDepthSection`) | section no., x from the bent CL, depth H (normal to the top), printed W/2 (check only), bottom slab T | box-sections.csv (sections 1-12) |
| `ParabolaVertexX / VertexDepth / Coefficient` | depth curve between the sections: H = Hv + c (xv - x)^2 below xv, Hv beyond | xv 159 ft, Hv 9'-0", c = 120 / 154^2 in/ft^2 - RECONSTRUCTION, flagged |
| `BentSoffitTransition`, `BentDiaphragm`, `SegmentalBent.DepthAtCl` | bent halves: soffit steps from the joint depth to the CL depth over the transition from the bulkhead face; solid pier diaphragm from the bent CL | 1'-6", 3'-6", 19'-3" (S3-29 p277, raster) |
| `ConstantDepth`, `ConstantBottomSlab` | closure joints, end segments | 9'-0", 10" |
| `Spans[]` (`SegmentalSpan` -> `SegmentDefinition`) | name, type (BentHalf / Typical / Closure / End), x0..x1 along -L- from the span's start bent, cantilever bent, segment number k | segments.csv (275 rows) |
| `Tendons[]` (`TendonDefinition`) | name, group (Cantilever / Bottom / Closure), layout rule, location, count, strands, plan length / weight, jacking force, stressing end, anchor segments (Dk / Uk), duct (lateral offset, depth below top or height above soffit) | pt-tendon-schedule.csv, cantilever-tendon-anchors.csv, duct grid S3-35 p283 |
| `Transverse[]` | per segment type (closures by nominal length): count, strands, length, plan weight per segment, depth | transverse-pt-stressing.csv |
| `Bents[]` (`SegmentalBent`) | CL depth, `Bearings[]` (`SegmentalBearing`: type, offset across, distance along, assumed height, body size, pedestal top + size), `Pier` (`SegmentalPier`: cap outline / top / height / taper, column envelope, footing outline / top / bottom, pile count, `Unresolved`) | bearing-data.csv, cap sheets p354-375, column sheets p376-381, foundation layout p252, piers.csv |
| `Unresolved`, `Reported` | plan items not modeled / not dimensioned (warnings), plan-vs-plan notes (Info) | build script |

Interpolation rules: H at a joint = the printed table value; between joints the parabola shape plus the linearly interpolated residual (table - parabola);
T linear between sections; W/2 always from the 4:1 web rule (matches the table within 1/16"). Closure joints / end segments: constant.

## 2. Geometry (generator)

- **Joints on -L-**: x is along -L- from the span's start bent; the joint WP = alignment point at that station, Z = profile grade (the PGL is the top of box).
  Joint section plane normal to the 3D alignment tangent (horizontal tangent + grade), rotated by the cross slope: the box is cast square to the sloped
  top (S3-19 p267: column vertical, box rotated; DIM "A" / "B" = H sin(atan(slope))). The soffit CL therefore lies UPHILL of the vertical through the WP.
- **Segments = straight 3D chords** between their joint WPs (S3-15 / S3-20 notes "segment chording"); intermediate rings on the chord (4 per variable-depth
  segment, extra rings at the bent-half breaks / parabola vertex / cross-slope transition). Adjacent segments share the joint frame exactly.
- **Arc vs chord (UNRESOLVED p263, p268)**: the plans state "spans are horizontal projections along CL girder" and "segment lengths are chord lengths" without
  a conversion. Chosen: the alignment-following chain of chords with joints at their -L- stations. Reported: max plan offset of a chord from -L- 0.104"
  (mid-segment, 14 ft chord on R 2,815), typical 3D chord 14.0000 .. 14.0091 ft (max 0.11" longer than its station length on the 3.75 % grade).
  A single straight chord per 350 ft span would be 5.44 ft off -L- at mid-span - not used.
- **Cross slope**: from the alignment's `CrossSlopeTable` (box top = one plane: (Δright - Δleft) / width; a non-planar table is warned). Bonner: 4.5 % to
  70+28.93, linear to 2.5 % at 70+82.93 (S0-03 says "varies": UNRESOLVED), down to the RIGHT (resolved from the p267 section "looking upstation").
- **Solids**: one closed shell per segment (`SegmentalLoft`): outer outline at every ring, void on the hollow rings, annular caps split into two simple
  half polygons at the CL, a void cap where a solid diaphragm closes it. End segments = solid outer envelope (interior p271 not fully dimensioned,
  flagged); bent halves = hollow 1'-6" bulkhead zone with the 3" soffit step + 3'-6" solid diaphragm (openings not modeled, flagged).
- **Tendons**: straight runs joint to joint in their duct (so they follow the box crest / soffit), Douglas-Peucker 2 mm. Cantilever Tk: tip-side faces of
  the anchor segments Dk / Uk (T14: D13 / U12 at bent 19, D12 / U13 at bent 28). B / C: centred on the closure (spans 20-28) or from the end segment
  anchor face (19 / 29), plan length - flagged. Ducts: S3-35 grid (rows A 7 1/4" / B 16 1/4" below the top at 15'-8 1/2" .. 6'-10 1/2"; row C 9" pitch
  from 1'-6" inside the mid-span soffit corner, 5" above the soffit); the designation-to-duct assignment is not in the plans (flagged). Transverse:
  equally spaced, straight 41'-7" at 4 1/2" below the top (flagged).
- **Bearings**: plan point = bent WP + offset across (end bents 8'-3", cantilever bents 5'-3") + along (end bents 5'-6" into the span); top = pedestal top
  (plan elevation tables) + assumed height; body = masonry plate max C x D x height (generic, flagged); bents 23 / 24 record only (size not tabulated).
- **Bents** (`SubstructureModel`, so viewport / IFC / database treat them like any substructure): footing (plan outline, flat top at the CL elevation,
  bottom -0.50; 23 / 24 octagon; lateral offsets of bents 22-25 from p252), column envelope 16'-0" x 11'-0" (hollow core and orientation flagged; none
  at 23 / 24), cap (18'-0" x 13'-0" x 5'-6" with the 4" taper over 3'-0"; end bents T-shaped 23'-0" / 43'-6"), CIP pedestals (6'-0" sq; end bents
  6'-2" x 4'-2") to the plan pedestal tops. Piles: GAP (counts only).

## 3. Hooks

| consumer | hook |
|---|---|
| framing | `FramingGenerator.Run`: `Arch != null || Segmental != null` with no girders -> support lines only |
| validation | `BridgeDefinition.Validate`: segmental needs `Girders.Count = 0`; `SegmentalDefinition.Validate(spans)` |
| regeneration | `RegenerationCache` phase 1: `SegmentalGenerator.Generate(f, units)` -> `ConcreteModel { Segmental = sm }` (no deck); phase 2: `sub = sm.Substructure` (cached under the sub key) |
| API | `SbmDocument.Segmental(name)`; segmental warnings in the regeneration report; IFC export includes bridges with a segmental model |
| IFC | `IfcExporter.ExportSegmental`: IfcElementAssembly `.GIRDER.` (.SITE.) per cantilever aggregating its segments, its cantilever tendons and the transverse tendons of its segments; segments IfcBeam `.GIRDER_SEGMENT.` (IFC4X3_ADD2 IfcBeamTypeEnum has no SEGMENT) with ObjectType TYPICAL_SEGMENT / BENT_SEGMENT / CLOSURE_JOINT / END_SEGMENT; tendons IfcTendon `.STRAND.` (swept disk of the equal-area bundle, NominalDiameter / CrossSectionArea, Pset SBM_Tendon: group, location, strands, plan / model length and weight, jacking force, stressing end, anchors); bearings IfcBearing `.DISK.` (ObjectType TYPE_I/II/III; no body when not sized); flags -> SBM_Status; keys `IfcKeys.SegCantilever / Segment / SegTendon / SegBearing` |
| database | `ModelDatabase.Segmental`: group "segmental box" (counts, CY) > assembly "segmental cantilever" > parts (typical / bent segment), elements (cantilever / transverse tendon); closure joints / end segments under the group; groups "<span> continuity tendons", "Disc bearings"; status / reason / assumed in props |
| sheet links | `samples/plans/sheet-links-bonner-unit5.json` (144-sheet S3 index, 31 rules) |
| viewport | `SceneBuilder.AddConcrete`: layers 3D-SEGMENTS, 3D-UNRESOLVED-SEGMENTS (orange: bent halves, end segments), 3D-PT-TENDONS, 3D-SEGMENTAL-BEARINGS; bents through the normal substructure layers |
| 2D views / sheets | GAP (v1): no segmental views; SheetSet has no segmental branch |

## 4. Checks (`check-bonner-unit5.csx`, 2026-10-12: 169 PASS / 23 DIFF)

PASS: segment counts 242 / 11 / 20 / 2 and all 275 lengths (0.00 ft), span lengths, depths at the 12 sections (every cantilever joint within 1/16"),
W/2 rule, gross areas (no fillet) within 0.02 ft2, cross slope / box height / DIM A / DIM B at all 12 bents (1/16"), bents 23-27 soffit - TOB 2.32,
tendon counts and plan weights T 774,968 / B 267,524 / C 25,716 lb (1,068,208), transverse typical + closures 121,610 vs 121,624 lb (rounding),
end segment E1-E4, cap tops, footing bottoms. DIFF (reported): soffit - TOB at bents 18-22 / 28 0.01-0.03 ft (the extraction used PGL - H vertically;
the box is normal to the sloped top), cantilever strand weight from model lengths +0.8 % (plan lengths stop short of the anchor faces; L / R ducts on the
curve), F1 & F2 count 40 vs 36 (bent segment count 20 halves vs 18 printed, UNRESOLVED p278), end segment 147 vs 110 ton and bent half 122 vs 100 ton
at 150 pcf (solid envelopes), footing CY +9 % vs the CIP BOM (sloped tops modeled flat). Bearing set-up: soffit - (pedestal + assumed height) -0.67 ..
+2.20 in. IFC: 0 schema / EXPRESS issues; all 287 IfcBeam closed, volumes = model; 5 of 50 sampled tendon swept disks mesh "open" in OpenCascade
(polyline mitres, not a schema issue).

## 5. Not in v1 (GAP)

2D views / sheets; shear keys, alignment keys, epoxy joints; temporary PT bars; bent / end segment PT bars; future PT pipes F1-F4; deviation blocks
(recorded, see 6); end segment diaphragm opening; 2-bar metal rail and parapet end posts; expansion joints; lighting; piles; casting curve and camber
(not in the plans); tendon profiles beyond straight joint-to-joint runs (drawn, not tabulated); mild reinforcing outside the XX-D3 / XX-U3 bill.

## 6. v2 details (2026-10-30): blocks, openings, parapet, critical-section bars

Data `DETAILS-DATA.md` + CSVs, script `samples/plans/details-bonner-unit5.csx` (called by the build script with `--no-regen`, re-runnable standalone).
Generic OPTIONAL inputs (`Bridge/SegmentalDetails.cs`; null = none, omitted from the .sbm, output unchanged): `SegmentalDefinition.BlockTypes` /
`Blocks` / `Openings` / `Parapet` / `Bars`; generator `Segmental/SegmentalGeneratorDetails.cs` (partial `SegmentalGenerator`).

- **Blocks** (`SegmentalBlockType` + `SegmentalBlockPlacement`): the block section at each station = the box VOID (right half, section coordinates)
  clipped by the station limits (`InnerFromCl` / `OuterFromCl`, `WidthFromWeb` at the block top, `TopAboveSlab` / `TopAboveSoffit`, top-slab blocks
  `BottomBelowDeck` + `BottomSquareToWeb` = bottom face square to the web, vertical inner face), lofted between stations (points equalized by edge
  splitting, caps from their own polygons) -> a closed solid that touches the segment and never overlaps it; mirrored for the left web; a station
  clipped to nothing closes the block to an edge on the host face; a first station clipped to nothing = block buried in the slab (Info). Placement
  by segment name + side, measured from the bent-side or tip-side (bulkhead) joint. `NotBuilt` types are counted only. Bonner: bottom slab anchor
  blocks (p.297) for every B tendon end in the arms, top slab anchor blocks (p.298) for C1 / C2, erection bar block Type B (p.299) in every typical
  segment (buried where the bottom slab is >= 1'-0 1/2"); Type A / Type C / deviation blocks recorded (dimension references not resolved).
- **Openings** (`SegmentalOpeningDefinition`): slab openings (rectangle / round 32-gon of equal area) centred on listed segments or at a bent CL (one
  void per bent half), bent diaphragm openings (outline above the bottom slab top or below the deck top) through the solid diaphragm of each half;
  result = void solid (2 cm beyond the concrete) + the removed concrete (`RemovedVolume`, 0 for a `Temporary` opening - T.A.O. is repaired).
  `SegmentalModel.NetConcreteVolume` = segments + blocks - openings (the segment solids themselves are not cut).
- **Parapet** (`SegmentalParapetDefinition`): plumb 1'-2" x 2'-6" sweep with 3/4" chamfers on the box top, inside face 20'-0" from -L-, rings at
  every joint (follows the chords); goes into `ConcreteModel.Barriers` (+ `BarrierInfo` material / flag) so IFC (IfcRailing), DB, viewport and
  quantities take it like a barrier. Joints / end posts / rail not built (flagged).
- **Bars** (`SegmentalBarDefinition`): one mark per `Mark`; schedule count = `CountPerSegment` x matched segments (regex), placed straight bars
  (`TransverseTop` / `TransverseSoffit` / `LongitudinalTop` / `LongitudinalSoffit`, centred at the printed spacing, `FaceDistance` from the face
  to the bar CL) checked for clear cover against their segment solid (`RebarCover`, `SegmentalModel.CoverViolations`, warning per mark, never moved);
  bent shapes schedule-only (`BarShape` with the listed length, printed dims in the description). Copied into `ConcreteModel.Marks / Bars` by
  RegenerationCache (RebarBends, IFC IfcReinforcingBar, DB "deck reinforcing", viewport). Bonner: the p.270 bill for the 20 XX-D3 / XX-U3 segments
  (17 marks, 8,511 lb each); placed A1 (top transverse), B1 top-slab top, A7 (bottom transverse), B1 bottom-slab bottom; the rest scheduled with
  the UNRESOLVED reason (top slab bottom cover not printed, type-1 diagram offsets, positions drawn only).
- Consumers: IFC `ExportSegmental` (blocks IfcBuildingElementPart USERDEFINED 'ANCHOR_BLOCK' / 'ERECTION_PT_BAR_BLOCK' / 'DEVIATION_BLOCK'
  aggregated into the segment IfcBeam; openings IfcOpeningElement .OPENING. + IfcRelVoidsElement; keys `IfcKeys.SegBlock` / `SegOpening`), database
  (elements under their segment: anchor block / erection PT bar block / opening / temporary opening; group props blocks / openings / net CY),
  viewport layers 3D-SEGMENT-BLOCKS / 3D-SEGMENT-OPENINGS (see-through), Rhino, projection. Tests `SegmentalDetailsTests`.

## 7. v3 details (2026-10-30, continued): joint keys, parapet joints, A4 bars, Unit 6 parapet

- **Joint keys** (OPTIONAL `SegmentalDefinition.Keys` = `SegmentalKeyDefinition`, generator `Segmental/SegmentalGeneratorKeys.cs`): p.283 Section C-C shows the segment face
  cast against the bulkhead FEMALE, so the MALE keys sit on the match-cast face = the face toward the cantilever bent of every typical segment (at the bent
  control line the up-station bent half, flagged). Trapezoid prisms (1 1/4" deep, 3 7/8" base, 2 5/8" flat; the R = 1" transitions straight, flagged),
  protruding toward the bent from the joint plane. Web keys: one row per section (`SegmentalWebKeyRow`, p.283 tables: 'A' count, 'B' zone, 'C' bottom
  distance), 1'-2" across the web centred in its thickness, @ 6 1/2" along the web; 'C' read along the web from the soffit (outside corner) to the
  first key centre - every row then ends 2'-7 7/8" below the deck top (consistent for rows 1-12 and the bent control line). Slab keys: top 3'-0 at CL
  and 19'-0 each side, 2 1/2" + 3 7/8" / 2 below the deck top; bottom Detail B (section 1: 1'-6 keys at 4'-1 each side, two levels 1'-0 apart), Detail A
  (sections 2-5: 3'-0 keys 2'-6 .. 5'-6 from CL), mid-span (sections 6-12: one 3'-0 key on CL) - the detail edge is taken as CL girder (flagged);
  none at the bent CL (not dimensioned). Joint section = the depth-table section at the joint (beyond the table = the last one). Results are
  `SegmentalBlockResult` with Kind `ShearKey` (appended enum value): recorded closed solids, the segments are NOT cut (male = female volume), so they
  are excluded from `BlockVolume` / `NetConcreteVolume` (`SegmentalModel.KeyVolume`). IFC IfcBuildingElementPart 'SHEAR_KEY' aggregated into the
  male segment, DB element "shear key", layer 3D-SEGMENT-BLOCKS. Bonner: 9,682 keys in 1,018 sets on 252 joints, 13.86 CY; check rows reproduce the
  p.283 'A' column at every section (27 .. 11 per web, 27 at the bent CL) and 3 top keys per joint.
- **Parapet joints** (OPTIONAL `SegmentalParapetDefinition.JointRuns` + `JointEdgeOffset`): per run (support to support) the deck-edge arc at the
  theoretical edge of deck (21'-3 1/2" from -L-) is split into `Spaces` x 30'-0 with equal ends (the plans' symmetric chains, p.323-326); 1/2" gaps
  (the parapet is built in pieces); printed arcs / ends = check rows (matched to the closer side). Bonner: 111 joints per side; arcs reproduce the
  printed 201'-6 1/8 / 198'-5 7/8 / 352'-7 3/4 / 347'-4 1/4 / 350'-6 3/4 / 349'-5 1/4 / 350'-0 / 200'-0 within 0.03"; span 19 second edge: the printed
  ends 24'-2 1/2" do not add up to the printed arc 198'-5 7/8" (CONFLICT reported, equal ends from the arc used). End posts / joints at the bents and
  the 2-bar rail (NCDOT standard, not in the set) stay UNRESOLVED.
- **A4 bars** placed (15 per XX-D3 / XX-U3 segment, top slab underside between the haunches at the general "2\" CLR. (TYP. U.N.O.)", flagged reading);
  0 cover violations. A2 / A3 / A8 / A5 / A6 / W1-W4 / S1 / A9 / A10 / A12 / the rest of B1 stay schedule-only (leg positions on the wing / haunch /
  web not dimensioned, type-1 diagram offsets).
- **Unit 6** (`build-bonner-unit6.csx`): 1'-2" x 2'-6" parapet (p.448 / p.450) as a custom barrier, outside face 1 1/2" inside the deck edge, 3/4"
  chamfers, plumb, continuous (joints 28'-5 + 4 @ 26'-0 + 28'-5, end posts and rail UNRESOLVED); check-bonner-unit6 unchanged (143 / 12).
- Checks: check-bonner-unit5 197 / 24 -> 231 PASS / 25 DIFF (new DIFF = the span 19 chain conflict above). IFC 0 schema / EXPRESS issues; all
  1,018 SHEAR_KEY parts closed (64 of 344 anchor blocks mesh open in IfcOpenShell - pre-existing, the bottom blocks' tapered end).
- Still open: erection PT bars / Type A / C blocks, deviation blocks + their 4" pipes and the future PT pipes F1-F4 (table dimension references -
  heights above which face, position along the closure / bent - not tied on the sheets), piles (footing plans p.334+ not extracted), end segment
  interior, rebar outside the typical-segment bill (no bills of material), Unit 6 diaphragms / joints / beam and deck bars.


## 9. Misc (2026-10-30): piles, bent centring, deck drains, modular expansion joints

Inventory of every misc plan item (built / not built + reason): `MISC-DATA.md`. Generic OPTIONAL inputs `SegmentalDefinition.Misc` (`Bridge/SegmentalMisc.cs`),
generator `Segmental/SegmentalGeneratorMisc.cs` (runs after BuildDetails), data `samples/plans/misc-bonner-unit5.csx` (called by the build script).

- **Piles** (`Foundations`): square precast piles at plan positions given at the footing bottom plane with the tip's plan direction; head = footing bottom +
  `Embedment` (per-pile override), tip at `TipElevation`, axis at `BatterRatio`; added to the bent's SubstructureUnit (SubSolid Pile + PileInfo) so IFC IfcPile,
  database "pile", viewport 3D-SUB-FOUNDATION and quantities take them. Bonner: 284 x 36" sq PS piles read from the VECTOR footing plans p.335-352 (batter arrow
  centroid - 2.9 ft x direction, snapped to the 3'-9" dimension grid; `tools/bonner_piles.py`), heads 4'-0" up (1'-3" for the 4 column-base piles at 23 / 24),
  tips at the pile data "no higher than" elevations (p.254-259); the bills of material reproduce (34,461 vs 34,475 LF), so the plan lengths are the minimum-tip lengths.
- **Bent centring**: Detail C of every substructure sheet puts CL footing / column / cap 2 11/16" .. 10 3/8" left of -L- (= DIM "B", box soffit CL); the
  bearings are symmetric about the cap CL (p.359). The script shifts the footing / cap outlines and bearings; `ColumnOffsetS` moves the column (BuildPiers).
- **Deck drains** (`Drains`): 6" PVC, one per typical segment at its centre, low side, CL 1'-7 1/2" from the deck edge, normal to the deck, top 1/2" below
  the surface (p.322) -> MiscModel DrainTrench (IfcWasteTerminal) on `Substructure.Misc`.
- **Modular joints** (`Joints`): CL joint 1'-0 1/2" (bent 18) / 1'-2 1/2" (bent 29) from the bent control line toward the FIB units (S2-19 p.161, S4-15 p.407);
  DIM "A" not in the set -> device envelope (placeholder, flagged) = 2 x (anchor face - CL joint) = 45" / 49" x 40'-0" x 1'-4"; end segment blockout 2'-2" x
  1'-4" (p.271 Section C-C, ends 9" scaled) as an end segment opening (removed concrete clipped to the modeled wing: 4.05 CY each); parapet end posts above the
  parapet (3'-9" x 1'-2" x 2'-2", p.327 / 333) and 3/4" cover plates (p.333). New appended `MiscGroup.ExpansionJoint`, `MiscKind.JointDevice / JointEndPost /
  JointCoverPlate` (IFC IfcDiscreteAccessory 'EXPANSION_JOINT_DEVICE' / IfcRailing 'PARAPET_END_POST' / IfcPlate 'JOINT_COVER_PLATE'; DB group "expansion joints";
  layer 3D-EXPANSION-JOINTS).
- Agent review (check-bonner-unit5 section 10, 90 rows, all PASS): pile counts / LF / tips / batter per bent, column centre vs Detail C, Detail C vs model DIM B
  (within 1/32"), heads inside the footings, 242 drains on the low side (offset within 0.37" across the slope), end posts / cover plates per joint.
- **Piles, hollow** (p.42): 1" chamfers, 21" void between 13'-0" (head) / 11'-0" (tip) solid sections (inner cavity shell; IfcOpenShell meshes all 284 closed, volumes = model).
- **2-bar metal rail** (S0-36 / 37, S3-80..83): two generic railing runs (reference line = parapet traffic face 20'-0" from -L-) between the end post faces; posts at the printed
  chains converted from the deck-edge arc to stations (every chain closes on its printed arc); 599 + 590 = 1,189 posts = the p.38 Unit 5 anchor assembly count, 4,756 anchors.
- **Fixtures, vents, survey blockouts** (second whole-set search 2026-10-30; the first inventory's "not located" was wrong): 9 solar array support platforms
  (p.392 / 322 station table, p.332 framing: B1 / B2 / B3 W8X18, B11 W8X31, L3X3 rails / posts, toe plates, rungs, grid; array + battery placeholders), 9 nav light
  conduit sleeves (p.392), 71 box girder luminaires with junction boxes + receptacles on the right web (p.388-391 arc chains, p.385 heights), panels A / B + service
  boxes, 24 access doors (A.O. / A.O.B.), 2 vermin screens, 242 vent holes (p.268 note 3), 110 N + 11 TP survey blockouts (p.266 table). Generic inputs: fixtures
  (`SegmentalFixtureDefinition`) and slab opening `Stations` / `Side` / `FromSoffitCorner` / `Depth`. Nav lights not built (channel station not given).
  Review: 370 PASS / 34 DIFF (20 new fixture rows pass; the 8 extra DIFFs are hardware / tendon / footing rows from other changes).
- Agent review: 343 PASS / 26 DIFF (rail posts / anchors added). IFC (validate_ifc, 1.49 M entities): 0 schema + 0 EXPRESS issues; aluminum parts show volume ratio 2.907 only
  because the validator divides the mass by 7,850 kg/m3. Tests `SegmentalMiscTests` (5).

## 8. Reinforcing in every segment (2026-10-30)

Before: 2,320 placed bars in the 20 XX-D3 / XX-U3 segments only (A1, A4, A7, B1 top / bottom straight bars; the other 13 bill marks schedule-only); the
other 255 segments had no bars. Now every one of the 275 segments has placed bars: 183,180 placed + 10,060 counted (193,240 scheduled, 2.10 M lb).

- **Input** (OPTIONAL `SegmentalDefinition.SectionBars` = `SegmentalSectionBarSet`, `Bridge/SegmentalSectionBars.cs`; null = none, omitted from the .sbm):
  a bar = a path of `SegmentalBarPoint`s on NAMED faces of the box section (outer TOP / TIP / WINGFLAT / WING / WEBOUT / SOFFIT, void TOPIN / HAUNCH /
  WEBIN / BHAUNCH / BOTIN, "L:" = mirror; offsets normal to the face into the concrete = clear cover + bar radius + outer layers): corners (two offset
  faces), points on a face at X / Y, free legs (printed out-to-out leg along a face / H / V; a free FIRST leg hangs back from the second point), hidden
  anchors, `FitLeg` (one leg solved for the listed out-to-out length). The path is resolved in EACH segment's own section at the bar's station, so one
  definition follows the variable depth / bottom slab; a void face at a solid diaphragm station = bar counted, not placed. Transverse sets: count +
  spacing (centred or from a joint), explicit `Positions`, equal spaces (`Spacing` 0, `First` from both joints), `Mirror`; longitudinal sets: a
  distribution polyline in the section, `Count` equal (or centred at `Spacing`), `EndClear`, `LongSamples` stations.
- **Generator** `Segmental/SegmentalGeneratorBars.cs` (after BuildDetails; per segment in parallel, output in segment order): marks per (Unit, Mark),
  length = listed or the model out-to-out (segment sum + db tan(defl./2) per bend; `VaryingLengths` beyond 1"), section-plane shape for the schedule,
  clear cover with `RebarCover` against the segment solid (transverse sets: first / middle / last bar per segment; all longitudinal bars), never moved.
  Consumers that looked marks up by mark alone now use `Concrete/BarMarkLookup` ((Unit, Mark), then Mark): IFC deck bars, Rhino, database, measure,
  viewport rebar tiles (same plan mark = #6 or #7 per segment group).
- **Bonner data** `samples/plans/rebar-bonner-unit5.csx` (called by details-bonner-unit5.csx; replaces the old `Bars`), `segment-rebar-variables.csv`
  (p.269 REINFORCING VARIABLES), `bent-end-segment-bars.csv` (bent / end readings):
  - typical segments, one unit per segment number k (XX-Dk / XX-Uk): A1, A2, A3 (type 4, outer leg fitted to 15'-10"), A4, A5 (fitted to 5'-9"),
    A6, A7, A9 (32 @ 5 1/4" derived), S1 (16 E.F. per edge, every second A9), W1-W4 28 @ 6" each web (W #6 / #7 per k), B1 (top 41 @ 1'-0, bottom of
    the top slab 39, bottom slab B = 17 / 19 per face, webs A per face); W5 / W6 (Detail B, no dims), A8, A10 / A12 counted only. Finding: the
    type-1 diagrams are NOT offsets - flat + leg reproduces the listed lengths (A6 11'-0 + 10" = 11'-10", W3 12'-3 1/2" vs 12'-4", W4 6'-4 1/4" vs
    6'-5"). W1 / W2 web legs in segment k = the printed XX-D3 legs + the web length difference (depth table, linear) so the laps stay as in D3;
    the model then reproduces the D3 bill: W1 172 / W2 162 / W3 147 / W4 76 in vs 172 / 163 / 148 / 77. W1's 2'-4 1/4" flat runs INWARD over the haunch
    (outward its 1'-6" leg would leave the 19" wing; flagged). B1 per D3 segment 166 placed vs 176 in the bill (DIFF reported: counts on the drawn faces).
  - closure joints (p.281): W1-W4, A6 (E.S.), A7 8 each at equal spaces 3" from the faces, B1 41 / 39 / 19 / 19; A1-A4, A8, A9, A13-A15, S1 UNRESOLVED
    (no counts).
  - bent halves (p.279-280, no bill): N1 8 / N4 8 paired at equal spaces 3" from the faces, N38 20 per side, diaphragm horizontals N3 / N5 / N6 / N7
    (#9 paired E.F.), N12 2 @ 1'-0 E.F., N9 19 sets @ 6" - depths SCALED from the left dimension chain; the other 35 marks UNRESOLVED.
  - end segments (p.273, no bill): E45 21 per side, E9-E15 @ 10" (3 each, E9 1'-11 1/2" above the soffit, scaled); the other marks UNRESOLVED.
  - block J / T / P / D / H bars, A16 and the D-4 wire: UNRESOLVED (no bill, no bend dimensions).
- Checks (check-bonner-unit5 section 9): every segment type has placed bars (242 / 20 / 11 / 2), D3 counts / lengths vs the bill (DIFF: B1 count and the
  D3 weight 8,396 vs 8,511 lb), 0 cover violations (56,917 bars checked). IFC: 183,180 IfcReinforcingBar (IfcSweptDiskSolid on IfcIndexedPolyCurve with
  IfcArcIndex bends; mark location heads only, the full flags are warnings), 17 -> 211 MB, IfcOpenShell 0 schema / EXPRESS issues. Rhino 3dm: bars =
  183,180 curves (lines + arcs) on `Bonner Unit 5::3D-REBAR-DECK` (export 456 MB); `--bar-solids` adds 131,491 closed pipe B-reps + 51,689 mesh tubes
  (exporter "kink in the centerline": W1 / W2 / A5 / A9 / S1 - bends over 90 deg or short ones that ArcFit does not keep as arcs), 2.95 GB.
  Tests `SegmentalSectionBarsTests`.

## 10. v4 (2026-10-30): closed block solids, bulkhead-face decision, PT hardware, tendon lanes

**USER DECISION (2026-10-30):** "Dimensions are measured from the bulkhead face. These are short line match cast segments. So the form is built off the face
further from the pier." Block / pipe / opening dimensions along a typical segment are measured from its TIP-SIDE (bulkhead) joint; the match-cast face is
the pier side (male keys there, unchanged). Where "farther from the pier" is ambiguous (closure joints, bent halves, end segments) the items stay flagged.

- **Reference faces re-checked:** erection bar blocks Type A / B / C: 2'-11" from the bulkhead face (= `TipSide`, already so; the Type B flag is dropped).
  Bottom / top slab anchor blocks (p.297 / p.298): the printed chains 4'-0" + 8'-6" + 1'-6" and 3'-5" + 6'-9" + 3'-10" span the whole 14'-0" segment, so the
  reference face does not move them; their orientation follows the tendon (anchor face toward the bent), unchanged. Openings are centred on their segment
  (no face reference). Deviation pipes / blocks: P.I. / CL on the CL closure joint (no face reference). Future PT pipes F1-F4 lie in the bent halves: no
  bulkhead face farther from the pier - flagged (outer end on the bent half's end face as drawn in Detail A, p.308).
- **Closed block solids:** a block whose last station clips to nothing (bottom slab anchor block taper end) was closed with a 0.2 % scaled copy of the
  previous section - zero-area triangles that OpenCascade drops, so 64 of 344 anchor blocks meshed OPEN in IfcOpenShell. `Loft` now ends such a block in an
  APEX at the section's lowest point and fans convex caps from their centroid (no zero-area triangles at the collinear points the ring equalization adds).
  Result (test model): every IfcBuildingElementPart (2,228: 1,210 blocks + 1,018 key sets) meshes closed (was 64 open); Rhino 3dm: all on 3D-SEGMENT-BLOCKS are
  closed VALID planar B-reps (10,892 = 1,210 blocks + 9,682 keys, one B-rep per shell).
- **New blocks:** Type A (p.300 Section B-B read on the drawing: bottom square to the web, 3'-3" long, free end 2'-8 1/4" below the deck top, inner face vertical;
  check rows: drop 9 1/2" vs printed 9 3/4", inner face vs 1'-5"), Type C (p.299 D-D: bar 6'-6" from CL girder, block face 9" inside the bar, top 9" above it;
  "14'-6" TO TOP SLAB" = to the deck top, which closes Section C-C 4'-9" + 14'-6" = 19'-3" = the bent depth; the resulting face height is a CONFLICT row vs the
  printed "1'-6 5/8" (MIN.)"), deviation blocks (p.302 read on the drawing: face parallel to the web 1'-8" + 7" = 2'-3" (spans 20-22) / 1'-6" + 3" = 1'-9" (23-28)
  normal to the original web inside face; with the 1'-4" web they reproduce the printed 3'-7" / 3'-1" bottom widths and the 44.4" vector width; 3'-0" centred on
  the CL closure joint). New station limits `TopBelowDeck`, `ThicknessFromWeb`; `SegmentalJointRef.Centre`.
- **PT hardware** (`SegmentalDefinition.PtBars` / `Pipes` -> `SegmentalModel.PtHardware`): erection bars straight between the far faces of the two blocks (top 2
  per web per joint, bottom 1 per web per joint alternating the two ducts, D1 - U1 through the bent: couplers not modeled); deviation pipes (deviation-pipes.csv)
  and future PT pipes (future-pt-pipes.csv): legs from the table offsets and the tangent lengths L1 / L3 + R tan(delta/2), arc of the printed R; 4" Sch. 40 =
  ASME B36.10M 4.500" OD x 0.237" (p.302 note 1). The table's printed delta does not follow from its own A-G offsets (deviation pipes 3.0 vs 4.0 deg etc.):
  check rows, pipes built on the dimensioned offsets.
- **Tendon lanes** (user: "PT ... should change lanes ... which hole it's going through in the bulkhead"; clarification: lane changes WITHIN a segment, already in
  the new lane at the next joint): `DuctLanes` + `TendonDefinition.Lanes` / `Profile`. The lane of every T tendon (row A) at every joint and of every B tendon at
  the closure end is read from the VECTOR layout plans p.285-296 (`tools/bonner_tendon_lanes.py`: crossings of the drawn tendon lines with each joint, offset
  from the CL curve at the lateral plan scale 1.499 pt/in checked on the duct grid, ducts of one row cannot cross so the order is kept from the labelled end;
  joints whose crossing count does not match are written '?' and keep the previous lane). Cantilever tendons: anchor 9" inside the tip (bulkhead) face 1'-1 1/2"
  below the deck top in the lane of the plan's anchor (A5 = P2 / A7 = P1, p.303 Section B-B), P.I. 9'-0" beyond it, lane changes between P.I.s 3'-6" from both
  faces, R = 35'-0" (p.303; note 3: minimum radius - clamped curves are counted). Bottom continuity tendons (spans 20-28): anchor on the block's anchor face 1'-1"
  above the slab top, 1'-7" from the web face at the block top, P.I. 6'-0" from it, R = 23'-0" (p.297), in the closure-end lane (row C lanes from the soffit
  corner: 1'-6" + 9" steps) with a point at every joint (the lanes follow the soffit). Check rows per tendon: largest duct offset from its lane hole at a joint,
  largest angle change at a joint, CONFLICT when the strand bundle leaves the concrete (never moved).
- UNRESOLVED (page + reason): p.299-300 temporary bars bent - U1 and the permanent bent PT bars + pockets (anchor positions drawn only); p.285-296 bents 21 / 23
  / 25 / 27 not drawn (sheets of 20 / 22 / 24 / 26 used); p.304 lane changes of B2-B6 (per-tendon P.I. distances not entered: the B tendons keep the closure
  lane); C1 / C2 and end-span continuity tendons keep the straight duct (p.304 profiles / end segment anchors not read); row B ducts (T5, T6, T14) are read at
  the bent only; p.308 future PT pipe position along the bent half; p.302 deviation pipe A / D end taken down-station (Section B-B); Type A / C heights (check rows).
- Results (test model, 2026-10-30): check-bonner-unit5 350 PASS / 34 DIFF (new DIFF rows = reported plan / reading differences: pipe deltas from the table
  offsets vs the printed delta (worst 4.4 vs 5.9 deg deviation, 2.8 vs 3.5 deg future PT), pipe length +0.27", Type A drop 9 1/2" vs 9 3/4", Type C face
  CONFLICT 24 5/8" vs 18 5/8" MIN., 24 tendons whose lane-change curve reaches 0.07" past a joint face, 44 CONFLICT B tendons whose deviation from the
  closure lane to the blister crosses the box void - their p.304 lane changes toward the web are not entered). 368 tendons through their lanes (lane offset at
  the joints <= 0.072", angle change at a joint <= 3.4 deg where a neighbour joint lane was not read); 1,392 erection bars, 36 deviation + 80 future PT pipes,
  18 deviation blocks, 484 Type A / 40 Type C blocks. IFC 224 MB: 0 schema / EXPRESS issues, 2,228 IfcBuildingElementPart + 116 IfcTendonConduit all closed
  in IfcOpenShell. Pre-existing (not v4): IfcBeam 19-UB / 28-UB mesh empty in IfcOpenShell (the bent-half opening voids; same without the v4 inputs).

## 11. Tendon lane read v2 + anchor exit angles (2026-10-31)

- **Lane table confirmed, read fixed.** The row A lanes (A1-A11 = 15'-8 1/2" ... 6'-10 1/2" from the CL, steps 9, 9, 9, 13, 13, 13, 13, 9, 9, 9 in) are the
  p.283 DUCT LOCATION dimensions and the lane ticks on every layout joint line (p.291: 13.5 / 19.5 pt at 1.5 pt/in, CL 629.8). The p.291 errors came from
  the read: joints were taken where many line pieces end (p.291 draws tendons as long lines broken at P.I. circles, so "joint 10" was a P.I.) and the
  crossings were zipped in lane order (the C2 line drawn solid on lane 8 near the closure took a slot: T12 / T13 read A7 / A8 instead of A4 / A6).
- **New read** (`tools/bonner_tendon_lanes.py`): joint = lane ticks / label centre +- half a typical segment snapped to a drawn joint; each row A tendon
  TRACED from its bent label along its own line (pieces joined end to end, P.I. branches by the longest unbroken continuation - the T14 dash-dot starts on
  the T13 P.I.; gaps under other lines bridged); the end of the trace = the anchor. Result: every T12 anchors in A5 (P2, 12'-4 1/2"), T13 / T11 / T9 / T7 in
  A7 (P1); every crossing at a joint is ON a lane (|resid| <= 0.1"), i.e. the drawn diagonals pass the joint holes. Row B T5 / T6 are traced along their
  dashes from where they leave the row A line of their bent-end lane (accepted only when the trace ends at P1 / P2; otherwise '?' + MATCHED fill from the
  cantilevers that read it). p.292 prints '24-11' for 24-U11 (read). T14 (dash-dot, drawn over T13) keeps its bent lane.
- **PLAN CONFLICT (names only):** the p.303 plan figures label the T3 / T5 / T8 / T10 / T12 anchor 'P1' and T7 / T9 / T11 'P2'; their lane ticks and the
  layouts put them at A5 / A7, which Section B-B calls P2 / P1. Lane positions modeled.
- **Anchor exit angle** (OPTIONAL `SegmentalTendonProfile.AnchorExits`): p.303 T7-T12 5.40 deg, T3-T6 / T14 6.86 deg (plan); straight from the anchor 9"
  inside the tip face to the P.I. 9'-0" beyond it, then the 3.07 deg diagonal through the joint hole (5.40 deg x 9'-0" = 10.21" + 2 3/4" = 1'-1"; 6.86 deg x
  9'-0" = 1'-1" = the next lane). Check row per tendon end vs the P.I. on the lane / diagonal: largest 0.056" (PASS). The elevation figures' 3.31 / 1.46 deg
  are the 1'-1 1/2" anchor depth to the row A / B duct depth over 9'-0" (already so).
- Results (fresh test model): 414 tendons routed, lane offset at the joint holes 0, outside concrete 0, misfit (CONFLICT 'not in its new lane') 116 -> 36
  (the 36 = B2 / B4 in spans 20-28 where R 22'-0" fits only 20.0-20.7' between the p.304 P.I.s - plan geometry, reported), largest angle change at a joint
  12.9 -> 0.89 deg (B tendons following the soffit chords; kinks relative to the lane are 0). check-bonner-unit5 373 PASS / 33 DIFF.
- **End-span tendons (spans 19 / 29, done 2026-10-31 for B1 / B2).** Read (`read_end_span`): B1-B3 traced on p.285 / p.296 from the start label nearer the
  end segment; vector text strokes (< 5 pt) and lane ticks dropped; a trace stops at an anchor symbol (filled triangle / circle, or an open triangle drawn as two
  strokes) unless its line runs straight through. Where two lines meet at one anchor and one line leaves it (p.285 / p.296 at XX-9: the B2 diagonal and the
  B3 lane-2 line), the line ending there is the one anchor-block-locations.csv assigns to that segment; a line ending elsewhere = CONFLICT, no lanes. Result:
  both sheets agree with the table (B1 end segment -> XX-7, B2 end segment -> XX-8, B3 XX-13 -> XX-9; on p.296 the B3 start symbol lies on the line B1
  continues, so its start segment is taken from the table, tagged). Lanes: B1 C2 at joint 13, C1 at 12-8, C2 at 7; B2 C4 at 13-9, C2 at 8; B3 C2.
  Rule (`EndSpanCtrl`, OPTIONAL `EndFaceAnchors` p.272: B1 8'-0" / B2 6'-6" from CL, 1'-7" above the soffit on the end segment anchor face, span face 8'-4 3/4" /
  7'-0", R = 10'-0", 18 deg): anchor -> span face of the end segment at the closure-joint lane height (UNRESOLVED: p.272 prints no exit height / bend position)
  -> every joint in its lane (lane changes inside the segment between the 4'-0" bent-face / 3'-0" bulkhead-face P.I.s, R 22') -> P.I. 6'-0" from the
  anchor in its block (p.297). Results: 8 tendons routed, lane offset 0, outside concrete 0; every one is a CONFLICT row 'not in its new lane' (6.45-8.33 deg
  corner at the first joint after the span face) and the bend at the span face is 11.8 deg (B1) / 13.7 deg (B2) vs the printed 18 deg - the pipe geometry in
  the end segment needs the exit height / bend position (p.272) to close this. B3 keeps the straight duct: its start block in XX-13 is not built (the
  anchor-block table reads that label as the 18-ES / 29-ES end). C1 / C2 end-span keep the straight duct (lanes not traced, no anchor height, p.272 / 304).
  check-bonner-unit5 373 PASS / 33 DIFF; misfit rows 44 (36 B2 / B4 R 22' + 8 end span); largest joint angle 8.33 deg (end-span corners), 0.89 elsewhere.

### PT profile agent review fixes (2026-10-31)
Agent review `qc/bonner-unit5/pt-profiles/findings.md` (52 items) found the 36 B2 / B4 R 22' misfits to be a generator error (shared legs split in halves) and several
model inputs off the plans. Applied: shared-leg tangents by need (all B2 / B4 at R 22'); 3.07 deg diagonal for T7-T12 only (T13 lane-parallel S-curve, p.303);
B2 / B4 after a 5.00 deg crossing straight in the lane from the P.I. where the line meets it (check rows: 0.48-0.71 ft before the printed 8'-6 7/8" / 8'-7", which assume
9" lane steps); B1 end-span lane changes at 2'-6" / 7'-0" (p.304 B1); p.272 Section A-A pipe bends: B1 / B2 P.I. 3'-6" from the anchor face at the row C level (18.4 deg vs
18), C1 / C2 anchors 6'-6" above the soffit at 7'-7 1/2" / 9'-1 1/2", P.I. 3'-10" (C1 R 10' 26.3 deg vs 26, C2 R 12' 16.6 deg vs 16); C1 / C2 spans 20-28 in lane 8,
anchor 3'-4" from the bent-side face 2'-7 1/4" below the deck top (plan exit 2.86 deg, vertical 14.04 deg), C1 row B -> A in XX-11 (3'-3" P.I.s, R 50'; PLAN
CONFLICT p.304 elevation vs p.290 / 285 / 296, p.290 modeled); end-span C lanes read from the p.285 / p.296 vectors (C1 lane 10 -> 8 in XX-12 at the p.304 3'-0" /
4'-0" P.I.s, R 22'); B3 start block in XX-13 (p.297 block mirrored, flagged). Remaining: 8 end-span B1 / B2 CONFLICT rows (p.272 span-face offsets vs the joint-13 lanes).
check-bonner-unit5 375 PASS / 31 DIFF.
