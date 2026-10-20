# Steel truss - design note (generic `TrussDefinition`, first use: PTC Delaware River main spans 18-20)

User decision 2026-10-19: riveted trusses become GENERIC program features (a truss generator), first for the original 1954-57 PA Turnpike
Delaware River Bridge (as-built set only). This note is the design; the implementation is in `SBM.Core/Bridge/TrussDefinition.cs`,
`SBM.Core/Truss/` and the hooks listed in section 6. Plan data: `truss/*.csv` beside this note, `samples/plans/truss-ptc-arch.csx`,
check `samples/plans/check-truss-ptc.csx`.

## 1. Model in one paragraph

A truss is its OWN bridge with `Girders.Count = 0` (like the tied arch and the segmental box): `BridgeDefinition.Truss` (OPTIONAL, omitted from
the .sbm when null). `FramingGenerator` builds the support lines only; `RegenerationCache` runs `Truss.TrussGenerator` (pure function
framing -> `TrussModel`), whose `SteelModel` becomes the bridge's steel and whose `ConcreteModel` carries the deck slab. Substructure is
not generated for a truss bridge (phase 2 skips it, as for the arch); the shoes publish `BearingSeat`s with plan location + axis so piers
built later (or by another bridge through `SharedPier`) take them as foreign seats. Geometry is given as NAMED PANEL POINTS (station along the
alignment + elevation) instantiated on each truss PLANE, MEMBERS between named points with generic riveted / rolled SECTIONS, JOINTS
(gussets + rivet groups), a FLOOR (floor beams are members; stringer lines + deck are generated), rope SUSPENDERS and SHOES. Units: meters.

## 2. Inputs (`TrussDefinition`)

| Input | Content |
|---|---|
| `Planes` | truss lines: name, `Offset` from the alignment (right +), `TiltDegrees` (about the longitudinal line; 0 = vertical) |
| `Points` | `TrussPointDefinition` name, `Station`, `Elevation`, optional `Offset` from the plane (CL points of laterals), optional `Planes` filter (a later entry with a filter overrides). Arch-type chords = the point list from the plan elevation table (no curve fitting) |
| `Sections` | `TrussSectionDefinition` by name, kinds below; `Label` = printed text; `Material` = plan letter (-> `Materials`) ; `Unresolved` = dimension flag |
| `Members` | `TrussMemberDefinition` name, `From` / `To` ("U3" on the member's plane or "N:U3" between planes), `Plane` ("" = one per plane, mark "{plane}-{name}"), `Section`, `Role`, `Panel` (database group), `System` (bracing assembly), `StartSetback` / `EndSetback` (null = automatic), `OffsetZ`, `PlanLength` (check), `Unresolved` |
| `Joints` | `TrussJointDefinition` point (+ plane), `GussetThickness`, `Outline` (u along the bridge / v up from the point; empty = envelope, flagged), `GussetsInsideFaceDistance`, `Rivets` (`TrussRivetGroup` member, count, rows, pitch, gage, first from the member end, diameter), `NoGusset` |
| `JointDefaults` | `AutoGussets` (every point where a web member meets a chord), `GussetThickness` (0 = 3/4" flagged), `Lap` (gusset length along each member beyond its end, 0 = member depth), `Margin`, `WebMemberClearance`, `RivetDiameter`, `Unresolved` |
| `Floor` | `TrussFloorDefinition`: `Stringers` (`TrussStringerLine` offset, section, own top, station limits), `StringerTopBelowGrade`, `StringersOnTop` (continuous stringers ON the floor beams, pieces CL to CL) or framed (`StringerEndClearance` to the floor beam web), `Deck` (`TrussDeckDefinition` thickness, left / right offsets, begin / end stations; top = finished grade) |
| `Suspenders` | `SuspenderDefinition` (generic rope hanger): upper / lower point, pin offsets below / above the points, rope diameter, rope count + spacing (across or along), socket length / diameter, pin diameter, rope text, weight per foot, plan length |
| `Shoes` | `TrussShoeDefinition` point, support, `Type` Fixed / Expansion / HoldDown, pin (diameter, length, below the point), height pin -> masonry bottom, body (bottom length, top length, width, top above the pin), masonry plate, `SeatElevation` (plan; checked against the pin chain), anchors (count only) |
| `PlanSteelWeightLb` + source, `Materials`, `DefaultMaterial`, `Unresolved`, `Source` | checks and flags |

### Section kinds (`TrussSectionKind`; local x = across the truss plane, y = up in the plane, normal to the axis)

| Kind | Riveted practice it covers | Pieces |
|---|---|---|
| `Box` | chords / posts / diagonals: two web groups (`WebPlies` of one side, stacked from the OUTSIDE in, different depths centred), 4 angles at the web edges (inside: backs on the innermost ply deeper than the angle leg; or outside), `AngleProjection` above the web edge, cover plates top / bottom (perforated = flagged, built solid), lacing on open faces | web plies, angles (AISC v16 polygon with root fillet; not in AISC -> parsed designation, sharp root), covers, lacing bars |
| `H` | web plate + 4 flange angles (+ flange plates): `WebVertical` = floor beams / plate girders; web across = truss members connected through their flanges | web, angles, plates (top edge `TopPlates`, bottom `BottomPlates`) |
| `LacedChannels` | 2 channels (backs `BackToBack` apart, flanges in / out) with lacing or perforated cover plates | channels (AISC C / MC), covers, lacing |
| `Angles` | 2 back-to-back angles or 4 angles at the corners of a box (laced) | angles, lacing |
| `Rolled` | AISC v16 W / C / MC / L / WT by exact designation; a shape not in the catalog (1950s rolled shapes, HP extract gap) needs `Depth` / `FlangeWidth` / `WebThickness` / `FlangeThickness` (+ `WeightLbPerFt`) or becomes a flagged envelope | one polygon |
| `Plate`, `Round` | bars, eyebars, rods, ropes | one polygon |
| `Nominal` | undimensioned member: rectangular ENVELOPE, always flagged | one polygon |

Every piece is a planar polygon swept straight along the member axis between square end planes, so each member mesh is fully described by
`ExtrudedSolid` recipes (IFC: one IfcExtrudedAreaSolid per piece; validated 0 issues). Mass = volume x 7849 kg/m3, or AISC nominal
weight x length for a catalog rolled shape.

### Automatic member ends
- Chords: square cut at the panel point, lengthened by half depth x tan(deflection / 2) toward the neighbour chord member (deflection < 25 deg)
  so the joint has no gap; members passing THROUGH a point (L0-L2 through L1) count as the chord there.
- Web members (diagonal, vertical, post, hanger): end where they leave the chord envelope: t = (h + y_member cos a) / sin a + clearance, max over
  the chords at the point (h = chord extent on the member's side, a = angle between the axes).
- Cross-plane members (floor beams, laterals, struts, sway, portals): end at the outer face of the in-plane members at the point
  (half width / |axis . plane normal|); explicit setbacks override (floor beams framed into verticals).
- Setbacks larger than 80 % of the length are reduced (warning).

### Joints
Two gusset plates per joint outside the chord webs (x = chord half width, or `GussetsInsideFaceDistance` / 2), thickness from the joint or the
defaults (3/4" flagged when neither). Outline: the plan polygon (u, v) or the convex hull of every member at the joint over [end, end + lap]
(chords and through-chords both ways) - flagged `Assumption.UnresolvedDimension` (stays on its layer). A member wider than the gusset inside
distance is reported (CHECK). Rivet groups: `BoltInstance` Kind `Rivet` (appended enum value) per gusset side, grip = gusset + outer ply.

## 3. Results (`TrussModel`, `SteelModel.Truss`)
Points (world), members (mark, plane, ends, work length, cut length, role, section, mass, flagged), suspenders, shoes, checks (`TrussCheck`
plan vs model, never adjusting), bearing names, `PanelOf` (part mark -> panel / joint label), gusset count, rivets, slab area.

## 4. Mapping

| Item | SteelModel | IFC 4.3 | Database | Viewport layer |
|---|---|---|---|---|
| truss line (plane) | `AssemblyKind.TrussLine` "TRUSS-{plane}" | IfcElementAssembly `.TRUSS.` | assembly "truss line" under group "truss" | - |
| chord / end post | `PartCategory.TrussChord` | IfcMember `.CHORD.` | "truss chord" in panel group | 3D-TRUSS-CHORDS |
| diagonal | `TrussDiagonal` | IfcMember `.BRACE.` | "truss diagonal" | 3D-TRUSS-WEB-MEMBERS |
| vertical / post | `TrussVertical` | IfcMember `.STRUT.` | "truss vertical" | 3D-TRUSS-WEB-MEMBERS |
| hanger (rigid) | `TrussHanger` | IfcMember `.MEMBER.` 'HANGER' | "truss hanger" | 3D-TRUSS-WEB-MEMBERS |
| laterals / sway / portal / strut | `TrussBracing`, assembly `TrussBracing` per system | IfcMember `.BRACE.`, assembly `.CROSS_BRACING.` | "truss bracing" > "truss bracing member" | 3D-TRUSS-BRACING |
| floor beam | `TrussFloorBeam`, assembly `TrussFloor` "FLOOR" | IfcBeam `.USERDEFINED.` 'FLOOR_BEAM', assembly `.BEAM_GRID.` | "floor system" > "floor beam" | 3D-TRUSS-FLOOR-BEAMS |
| stringer | `TrussStringer` | IfcMember `.STRINGER.` | "stringer" in "Bay n" groups | 3D-TRUSS-STRINGERS |
| gusset | `TrussGusset` | IfcPlate `.GUSSET_PLATE.` | "gusset plate" in "Joint X" groups | 3D-TRUSS-GUSSETS |
| rivet | `BoltInstance` Kind `Rivet` | IfcMechanicalFastener `.RIVET.` (type per size) | "rivet" in "{asm} rivets" | 3D-BOLTS (button-head mesh) |
| rope suspender / socket / pin | `Suspender` / `SuspenderSocket` / `TrussPin`, assembly `TrussSuspender` | IfcMember `.SUSPENDER.`, IfcMechanicalFastener 'CABLE_SOCKET' / 'PIN', assembly `.SUSPENSIONASSEMBLY.` | "suspender" > "suspender rope / socket / pin" | 3D-TRUSS-SUSPENDERS |
| shoe | `TrussShoeFixed` / `TrussShoeExpansion` + `MasonryPlate` + `TrussPin`, `AssemblyKind.Bearing` "SHOE-x" | IfcBearing 'FIXED_PIN_SHOE' / `.ROCKER.`, IfcPlate `.BASE_PLATE.` | "bearing" (sub "truss shoe", support from " at <pier>") | 3D-TRUSS-SHOES / 3D-BEARINGS |
| deck slab | `ConcreteModel.Deck` | IfcSlab (description "... deck on stringers") | deck | 3D-DECK |
| flagged dimension | `SteelPart.Assumption` DimensionOnly | Pset SBM_Status | status / reason / assumed props | normal layer (pick shows the flag) |

Ropes, sockets, shoe castings and pins are not structural steel (`SteelModel.IsTrussNonStructural`, excluded from `SteelMass`), as the plan
quantities list them apart (rope LF, castings, forgings). Sheets: none yet (the 2D projector `ModelProjectionSource` already picks up the new
layers; truss elevations / joint sheets are a later step).

## 5. Later (design only)
- Cofferdam-built footings (piers 18 / 19: tremie seal, timber / steel piles, sheet pile cofferdam, p.17-23): fit the substructure as an
  as-designed `FootingDefinition` with a SEAL layer below it (new optional `SealDefinition`: outline = cofferdam inside line, top / bottom
  elevation, concrete "tremie"), piles from the footing (or seal) bottom with their own cut-off, and the cofferdam as a TEMPORARY
  placeholder (`MiscElement.Placeholder`, IfcCivilElement 'COFFERDAM', not in quantities); the fender / dolphins as misc items. Twin
  tapered shafts = two columns with `ColumnFlare` / taper inputs; shoe seats from the truss `BearingSeat`s.
- Approach trusses (spans 11-17, 21-27): same generator; see section 8.
- Pin-connected trusses / eyebars: `Plate` sections + pins as `TrussPin` parts at the points (pin holes as plate holes later).
- Gusset outlines from vector linework where a set has it (`tools/pdfvec.py` route used for NHT); for raster sets scale at 3/4" = 1'-0.

## 6. Code hooks (all OPTIONAL / additive; other bridges byte-identical, Woodbury IFC checked)
`BridgeDefinition.Truss` (+ Validate: no girders, sections defined; Clone), `FramingGenerator` support-lines path, `RegenerationCache`
(truss branch; phase 2 skip; `ForeignSeats` for truss seats; SupportLinesOnly bridges with a truss generate the truss), `SteelModel`
(appended `PartCategory` / `AssemblyKind` values, `BoltKind.Rivet`, `RivetMesh`, `Truss` back-reference, `IsTrussNonStructural`),
`IfcExporter` (styles, assembly / part types, rivet fastener type kind 4, deck description, concrete strength), `ModelDatabase` (truss
group, assembly categories, panel / joint groups from `PanelOf`, rivet fasteners kind 4 = same GlobalId numbering as IFC),
`ModelLayers.Steel` (8 layers, shared by the viewport and the projector), `SceneBuilder` (rivets instanced with the bolts). Tests `TrussTests`.

## 7. PTC main spans 18-20 (truss-ptc-arch.csx) - modeled vs flagged

Modeled from the sheets (page in every row of `truss/*.csv`):
- Geometry: 45 panel points per chord per truss, stations 675+98.380 + 31'-0 x n (p.69 / p.71), lower chord from the p.69 ELEVATIONS table,
  upper chord = lower + vertical length (p.70; verticals plumb), wind chord W14-W22 from the table, mirror about the crown (p.69 "symmetrical").
  Trusses at +/- 43'-6 (p.72 / p.94). Member lengths reproduce the p.69 / p.70 tables (346 checks) except two printed values (below).
- Sections: all 44 chord members (p.69 + joint sheets p.74-88; L13-L17 "See Sketch" webs from the half longitudinal section / base areas),
  all verticals and diagonals (p.70), post U11-L11 (p.69 elevation, upper section), rigid suspender 14 (p.70), wind chord (14 BP 73 = HP14x73,
  L13-W14 laced 15[ 50), floor beams F5 / F6 / F7 / F8 (p.72, full-length plates), 11 stringer lines 21 / 24 WF (p.72, offsets p.94),
  bracing members (p.71: upper K laterals U11-U22, struts, lower K laterals incl. the L10-L12 diamond, sway V frames 0-12, floor X laterals,
  L0 / L11 bottom struts), 7 3/4" slab between the curbs (p.94 / p.107).
- Gussets at every web-chord joint; thickness from p.69 / p.74 / p.78 / p.79 / p.81 / p.88 where read.
- 30 rope suspenders 15-22-15' (4" bridge rope, sockets 3'-6 1/4 x 1'-1 3/4, 7 1/4" pins, lower pin 6'-10 1/4 above the wind chord, p.94).
- 8 shoes: L0 / L0' expansion rockers (p.73, bottom El. 107.682), L11 fixed / L11' expansion (p.96, 10" pin, bottom El. 87.701).

CONFLICT / plan differences (reported, never adjusted):
- L13: table prints CL lower chord 140.7820, the four members meeting there close on 140.7620 (= the wind chord value): modeled 140.7620.
- U3-L4 printed 46.1806 vs 46.1606 from the points; U9-L10 printed 79.0893 vs 79.0674 (both verified at 600 dpi; all neighbours close).
- U18-L18 cover printed (28-12) on p.70 but (26-12) on p.88 and in the p.70 area: 26 used.

UNRESOLVED (flags with page + reason; dimension-only flags keep the parts on their layers): gusset outlines (drawn, not dimensioned); web member
out-to-out (2'-5 = gusset in-to-in assumed) and angle leg orientation; 1954 18[ channels as AISC MC18 of the same weight; ply order; perforations
built solid; splice / inlay / fill plates, lug angles, diaphragms, stitch rivets not built; rivets not placed (no counts printed); post U11-L11 varying
section; rigid suspender 14 covers (p.84 not read); floor beam partial cover plates and end connections; stringer bearings / diaphragms; upper pin
of the ropes (p.86 not read) and rope count per point; floor lateral pattern; L13 1/2 double K and strut, inverted portal L13 1/2-L15; sway above the
floor beams and portal knee bracing (p.100); U11 portal strut = envelope; shoe bodies as envelopes, pier 19 roller nest, masonry plate size
conflict; hold-downs / shear locks; expansion dams, curbs, parapets, walkways.

Check (`check-truss-ptc.csx`, on the PTC model): 381 PASS / 11 DIFF / 35 INFO - panel lengths, every table elevation, the PTC profile at the gutter
line vs the p.69 gutter column (within 0.001 ft at all 45 points), member counts by role, member lengths, shoe seats; DIFF = the 3 plan
differences above (x planes / halves), carbon steel and the C + S total (model 13.52 M lb = 84.9 % of the p.42 15.93 M lb: the missing items
above); rope 1,745 LF pin to pin vs p.42 1,760 LF.

## 8. What the approach trusses (spans 11-17, 21-27) need next
- Data only, same generator: 3-span units 3 x 215'-6 (9 panels @ 26'-11 1/2, p.47 stress sheet, p.48 floor / bracing, joints p.49-53, p.59) and
  4-span units 4 x 269'-4 1/2 (p.57 / p.58, joints p.59-67): panel point table (p.47 / p.57 elevation tables or chord lengths), member tables, gusset
  thicknesses, shoes at piers 10-17 / 20-27 (p.49, p.52, p.59, p.63, p.67), floor system (typical details p.55, bracing p.56), expansion dams
  (p.54, p.68). Script per unit type (`truss-ptc-approach.csx`) reusing the parsers of truss-ptc-arch.csx (move them to a shared .csx).
- Bridges: the PTC model's "PTC 3-span truss 11-13 / 25-27" and "PTC 4-span truss 14-17 / 21-24" support-line bridges become truss bridges
  (supports already at the shoe stations, incl. the derived 2'-0 offsets at the truss-to-truss piers).
- Program: rivet counts / gusset outlines would need the joint sheets scaled (raster); sheet-link rules for the truss groups (truss / joint / panel ->
  stress sheet and joint sheets) in `sheet-links-ptc-delaware-river.json`.

### Status (2026-10-19): approach trusses built
`samples/plans/truss-ptc-approach.csx` + `check-truss-ptc-approach.csx` (see CLAUDE.md, PTC APPROACH trusses); data in `truss-approach/`. Items listed above that are still open: the 3-span L0 / U0 joint sheet (p.49 says
'similar to L0' / U0'': p.59 values used), joint sheets p.61-66 only medium confidence, rivet counts, gusset outlines, expansion dams (p.54 / p.68), partial floor beam cover plates and the tapered brackets.

## 9. Deck, visible connections, quantities (2026-10-20; scripts `deck-ptc.csx`, `connections-ptc.csx`)
Generic additions (all OPTIONAL, omitted from the .sbm when unset; trusses without them build exactly as before; test `TrussTests.DeckItems_SplicePlates_Stiffeners_ShoeMaterial`):
- `TrussDeckDefinition.HaunchDepth / HaunchBottomWidth / HaunchSideSlope`: slab thickening under every stringer line (top on the slab soffit, follows the crown;
  bottom level at the line) -> `ConcreteModel.Haunches` (IFC: second item of the deck IfcSlab).
- `TrussDefinition.LineItems` (`TrussLineItemDefinition`): constant section (U, V) swept ALONG the alignment at a reference offset (U outward toward `Side`) or
  ACROSS it at a station (`Transverse`, U along the alignment, rings at the range ends + the crown); V above the finished grade at the reference point (or
  absolute, `FromGrade = false`). Concrete -> `ConcreteModel.Barriers` (IfcRailing GUARDRAIL / database barrier), steel -> a `SteelPart` of `Category`
  (`Density` / `NominalWeightLbPerFt`; assembly created by name: JointArmor -> ExpansionJoint kind, railing -> Railing, else InspectionWalkway).
- `TrussJointDefinition.Plates` (`TrussJointPlate`, faces Top / Bottom / WebInside / WebOutside): splice and fill plates on the chord(s) at a joint, along the
  mean chord direction, `Offset` + toward the higher station; fills sit on the face of the member on their side, splice plates on the outermost of the chord
  faces and the fills under them, further splice plates stack. Web plates are one per web (pair). Parts SplicePlate / FillerPlate, panel "Joint n".
- `TrussMemberDefinition.Stiffeners` (`TrussStiffenerSet`): angles (one or two per web face, first leg out, second on the web) or web plates at distances along
  the member from its start working point, height centred on the web; parts IntermediateStiffener / SplicePlate.
- `TrussShoeDefinition.BodyMaterial` (null = cast steel): shoes built up of plates.
- `TrussBuiltSection.WebOuterX / WebInnerX / WebDepth / WebCenterY` (box sections) for the web faces.

PTC data (every flag names page + reason):
- Deck (p.107): 6 3/4 slab between the outside faces of the parapets (82'-2), 1 in x 1'-5 thickening with 12:12 sides under every stringer; curb 2'-0 x 9 in +
  parapet wall 10 in x 1'-9 as one concrete section per side; aluminum railing (p.107 / p.108) as `PedestrianRailingDefinition` per side (oval rail 8 x 5 5/8
  envelope with the tube mass, cast posts 10 -> 6 in x 7 in equally spaced <= 5'-8 3/4: the per-panel layout of p.108 is flagged). Class A concrete reproduces
  p.42 item 11 within 3 % in all 7 groups.
- Expansion dams: one steel band per dam (total width at 68 F x plate thickness, top flush with the grade, curb to curb): sliding plate 8 1/4 x 3/8 at piers
  1-9 / 28-30 (p.46), fingers 1'-4 1/2 x 3 at piers 10 / 27 (p.54; stations from p.1, flagged), 2'-2 x 3 at 13 / 17 / 24 (p.68), 3'-8 1/2 x 3 1/2 at 20
  (p.91), 1'-8 x 3 at W22 (p.92). Teeth, edge channels, supports, parapet ends flagged.
- Walkways: approach units one 2'-0 grating along the CL at the floor beam bottom flange level (through the floor beam manholes, webs not cut) + cross walkways
  along the pier floor beams (3-span: joints 0, 8, 8'; 4-span: 10, 10'); main span: on the L11 / L11' bottom struts (22'-0) and along FB11 / FB11' (p.103).
  Grating 7 psf flagged (no weight printed). Girder spans: no walkway sheet (flag; p.42 lists grating for them).
- Main truss joints: gusset thickness + in-to-in from gussets.csv on 153 joint definitions (no conflict with joints.csv); outlines stay envelopes (drawn,
  not dimensioned). Chord splices: 110 plate definitions (cover splice plates top + bottom, inside / outside web splice plates, fills with scaled lengths,
  both halves); 133 callouts without a length (and the inlay plates) not built, flagged.
- Girder spans: end stiffeners 4-Ls 7x4x3/4 at the CL bearings, 2-Ls 6x4x1/2 at the floor beams, intermediate 2-Ls 6x4x7/16 at 9 / 6 / 6 / 9 equal
  spaces per panel, web splice 2-Pls 21 x 3/8 at mid-span (heights 9'-4 1/4 / 8'-2 derived, flagged).
- Stringer diaphragms 15 [ 33.9 at mid-panel in all spans (one continuous channel across the stringer lines, top 3 in. below the stringer tops, flagged).
- Shoe bodies of plates = carbon steel (only the main L11 shoes are castings, p.96).
- Rivets are NOT placed: counts are image estimates without pitch / gage per group and would add ~10^5 instances to the viewport.

Quantities (check-ptc-delaware-river.csx section 7, p.42 by span group): structural steel 32.78 M lb of 39.28 M lb = 83.5 % (carbon 72.0 %, silicon
92.3 %); by group: girder spans 78 %, 3-span units 80 %, 4-span units 80 %, main spans 86.7 %. Concrete within 3 % everywhere; railing LF within 1 % on the
truss units (girder units short by the abutment wing railing, not modeled); grating 3-span 1,612 / 1,650 SF, 4-span 2,368 / 2,660 SF, main 301 / 3,860 SF.
Castings: the main span shoe envelopes / sockets are 2.8 x the p.42 figure (envelopes, flagged).

## 10. Second pass (2026-10-20): shoes, dams, walkways, abutment parapets
Generic additions (OPTIONAL, omitted from the .sbm when unset; tests `TrussTests.ShoePieces_ReplaceTheEnvelope_ExactVolumes`, `LineItemRepeat_BuildsCombTeeth`):
- `TrussShoeDefinition.Pieces` (`TrussShoePiece`): polygon or circle (segmental roller: `ClipHalfWidth`) in the shoe frame (origin = pin centre, u along the bridge, n = plane
  normal made horizontal, z up) swept Across (profile (u, z) along n), Along (profile (n, z) along u) or Vertical; copies (`ShiftsProfileX`, `ShiftsSweep`), `MirrorN` / `MirrorU`;
  Kind -> category (Body = shoe category by type, Plate = bearing plate, Sole, Masonry, Pin / Roller = pin, Rocker = expansion shoe body). Pieces replace the body envelope;
  the masonry plate and the pin of the fields are still built (MasonryThickness 0 = none, e.g. the pier 18 casting with its own base).
- `TrussLineItemDefinition.RepeatPitch / RepeatWidth / RepeatStart` (separate pieces along the range: dam teeth, rail posts) and `CopiesV` (V-shifted copies: ladder rungs).

PTC (every open item is a flag with page + reason):
- Shoes (`shoes-ptc.csx`, 100 shoes): girder spans p.44 (fixed: sole, stem, 2 wings, 2 stiffeners; expansion: sole + rocker R 1'-6), approach fixed p.52 / p.67 (sole, 2 stems, 3 wings,
  4 end stiffeners - position along the truss scaled; masonry 5'-6 along x 60 across, the previous model had them swapped), unit-end rockers p.59, intermediate rockers p.49 / p.63
  (pin below the W.P. now from the printed chains 8 3/4 + 9 / 8 3/4 + 10 1/4: model total 60.25 in. vs printed 5'-0 1/4 within 0.005 in.; bearing plates 1 1/2 in. proud, 1 in. let in),
  main L0 rockers p.73, L11 fixed cast shoe (pier 18: cast base 9'-2 x 8'-8 x 6, walls 4 3/4 / 3 1/2 / 4 3/4 to the 10 in. cradle, diaphragm, ribs @ 1'-9) and L11' roller nest (pier 19:
  5 forged segmental rollers 2'-0 x 1'-3 in two 3'-6 pieces, cast stem 4 in. walls, masonry 6 in. per the side view vs 6 3/4 listed = CONFLICT), forged tops 4'-1 x 4'-4.
  Not built: keys, dowels, pin nuts / rings, link bars, inlays (rocker lens), anchor bolts, hold-down / shear lock, fillets / bosses. Main castings 255 k lb vs p.42 190 k (shoes 150 k +
  sockets 105 k as solid cylinders), forgings 137 k vs 134 k.
- Expansion dams (`deck-ptc.csx`): two finger plates per dam, solid part behind the tooth roots + teeth 2 in. wide @ 4 1/4 (the opposite plate shifted half a space), plate data from
  each Section A at 68 F: p.46 (piers 1-9, 28-30: roots 8 1/4 apart, teeth 6 3/4, plate 1'-3 from the CL, 1 1/2 thick - NOT a sliding plate as first read), p.54 (piers 10 / 27: 16 1/2,
  12 1/2, 18 1/2 behind, 3 in.), p.68 (13 / 17 / 24: 26, 19 1/4, 3 1/2 in.), p.91 (pier 20: 44 1/2, 31 1/2), p.92 (W22: 20, 15). 18 I 70 (back) / 18 [ 58 (ahead) support members under the
  finger dams, continuous, web 6 in. behind the root (scaled). Tooth width derived from the 1 1/8 R slot (flag).
- Walkways (`walkways-ptc.csx`, p.101-106): grating 2'-0 between C10x15.3 walk channels, rails L3x3x5/16 (3'-0) and L3x2 1/2x5/16 (1'-6), posts @ 6'-0 both sides; approach CL walkway
  centred 1'-0 north of the CL at the floor beam bottom flange; cross walkways per the p.101 key plan (pier 10 / 27 truss to truss, the others CL to the north truss), at the p.102 ladder
  stations (4'-9 3/8 / 6'-0 3/8 from the pier / bearing CL); safety ladders (Ls 3x2 1/2x3/8, rungs @ 12) down to the inspection platforms on the pier tops (grating on 8 [ 13.75, 9 3/8 above
  the pier top); main span CL walkway FB15 - FB15', cross walks at W15 / W20 / W19' / W15' (40'-9 1/2 each side), floor beam and strut walkways at L11 / L11', platforms N / S at
  piers 18 / 19 with a ladder at the north. Not built: hangers, connection angles, ladder cages, catwalk on the north top chord (p.105), navigation light platforms (p.106),
  girder-span walkways (no sheet; p.42 lists them).
- Abutment wing parapets (p.108): the curb + parapet section continued from the deck ends to the wing ends (67'-9 / 34'-9 from the CL bearing) with the railing on them; railing
  LF 2,618 / 1,059 vs 2,620 / 1,061. The abutment concrete itself was already in substructure-ptc.csx.
- Gusset outlines: NOT replaced. The raster joint sheets (p.50-90) draw two gusset layers whose edges coincide with member flange, fill and splice outlines; where the outline follows
  the diagonal ends it cannot be separated at the scan resolution (a test read of L8 p.52 and L10 p.78 gave +-1-2 in. and ambiguous vertices), so the envelopes stay (flagged).
- Railing check after the main-branch railing base change (upward-facing surfaces only): rail LF and post bottoms unchanged (posts 30.25 in. above the gutter grade = parapet top, all 7 bridges).

