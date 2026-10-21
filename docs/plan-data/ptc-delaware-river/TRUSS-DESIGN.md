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


## 11. Rivets (2026-10-20, user decision d39; script `rivets-ptc.csx`, run last)
- Counts: connections/gussets.csv image counts per group and gusset FACE (solid = shop, ring = field). Each row is placed on both gusset faces of both trusses, and on both halves
  of a symmetric unit (data joint n -> n and n'; rows naming a primed joint are used as given). Rows of the third pass replace the whole-joint / sheet-half totals of the same joint
  (L10, L12-L15, U18-L21 sheet halves, U22 + L22 sheet portions). Multi-joint rows are split equally.
- Group text -> member: "chord ... left / west" = chord member toward the lower panel number (mirrored in the primed half), "right / east" = higher, "(A / B)" chord fields =
  field count on the lower-panel member and shop count on the other (as drawn), no side = halves; "diagonal / vertical / hanger" = the named member or the diagonal on that side /
  the vertical at the joint; rows drawn just outside the gusset (fill / splice blocks, member ends, free ends) are added to that member's lap (flag says so).
  NOT placed (no part in the model; counts reported per bridge and flagged): flange-plan cover plate splices (338 per face main), member splice details (approach 159 / 386),
  lug / diaphragm / bracket / strut / shoe connections, secondary angle members, stubs, pin plates, x / star symbol holes.
- Diameters: p.70 rule for the main truss (chord webs 1 1/4 in. U6-U13, U20-U22, L6-L15, L20-L22, else 1 1/8; web members 1 1/8 except U11-L11 and the U11 ends of L10-U11 /
  U11-L12 = 1 1/4); approach 1 in. H.S. (p.50); girders 1 in. carbon (p.43). 57 main row placements (rows x halves) carried another size than the p.70 rule (rule used).
- Positions: count exact, even square grid over the lap the gusset shares with the member (gusset envelope clipped to the member band), 1.5 d edges. DIMENSION-ONLY flag on every
  rivet (page + reason). Grids under 3 d: main 150 groups (tightest 2.17 d), 3-span 52 (1.71 d), 4-span 166 (1.61 d) - the envelope gussets are smaller than the drawn ones and the
  dense layers L8 p.52 / L10 p.63 / L20 p.67 are over-counted 15-25 % (flagged). Not adjusted.
- Girder spans: rule-based lines (p.43 max sealing pitch 6 + 4t, 7 in.): flange angles to web 2 gage lines per vertical leg, cover plates to the horizontal legs 2 lines each side,
  7 in. pitch, alternate lines staggered 3 1/2 in., 2 in. from the ends; grip follows the partial cover plates. Stiffener, splice, floor beam, lateral rivets not placed.
- Totals: 226,632 rivets (main 56,872 = 30,104 x 1 1/8 + 26,768 x 1 1/4; 3-span 2 x 13,880; 4-span 2 x 35,360; girders 50,940 + 20,340), heads 107,610 lb.
  p.42 structural steel: 82.6 % (model on main) -> 84.0 % (run order re-applied: the saved model had lost the mid-panel 15[33.9 diaphragms, +557 k lb) -> 84.3 % with the rivet heads
  (H.S. heads counted with the silicon items, carbon heads with the carbon items: p.42 has no rivet line - assumption).
- Corrections of the third pass applied: U0 gusset in-to-in 29 in. (connections-ptc reads the CORRECTION row), approach U20 gusset 5/8 (rivets-ptc, still flagged),
  shoes-ptc: L20 fixed shoe sole 10 3/16, wings 29 7/8, stems 2'-5 7/8 (both sheets), main L0 rocker wings / stiffeners 1 1/2 thick and base 6 3/4 (stack flag: 1/4 in. over the
  drawn pin height). Splice-plate corrections L19-L21 / U20-U21 change rows that have no length, so no built plate changes; p.50 splice bars 6 x 1'-8 kept flagged (not built).

## 12. Review fixes (2026-10-20): edge stringer, floor beam / bracket length, chord ends at the end shoes

User questions: "Is there an edge stringer missing?", "Is the floor beam too long?", "The main truss bearings are in the wrong spot, or the lower chord is
trimmed too short." Checked in every unit (main 18-20, approach 11-13 / 14-17 / 21-24 / 25-27, girder spans 1-10 / 28-31).

| Item | Plan | Model before | Model now |
|---|---|---|---|
| Outside stringer S5 21 WF 62, approach trusses (all 4 units) | 33'-0 + 6'-1 = 39'-1 from CL, 10 in. outside the 38'-3 gutter line, 8 in. inside the bracket tip (p.48 / p.58 half plans, p.55 bearing details; pixel row profile of p.48: 11 lines, outermost at ~38.5 ft drawn) | 33'-8 (8 in. outside the 33'-0 line - the 8 in. read as stringer spacing): no stringer under the curb / parapet | 39'-1 |
| Outside stringer, main span (S13) / girder spans (S1) | 39'-1 (p.72 / p.85: 6'-1 + 4 @ 8'-3; p.43 Section A-A: 6'-1 + 8 @ 8'-3) | 39'-1 | unchanged |
| B1 bracket tip, approach | 10'-9 beyond the truss = 39'-9 (p.48 / p.58 / p.55) | 39'-9 (but 6'-1 bare past the misplaced S5) | 39'-9 |
| B1 bracket depth | 6'-3 1/2 b.b. angles at the truss, bottom angles sloping straight up to 2'-0 1/2 at the tip (p.55 elevation, extension sketches) | full 6'-3 1/2 to the tip (flagged) | tapered (`DepthReductionStart` 4'-3) |
| B1 bracket, girder spans | p.45: "details and parts not shown same as shown for Approach Truss Spans, Sh. 15" (= p.55); girder sheets draw the taper without dimensions | tip S1 + 6 in. (guess), full depth | tip 39'-9, taper as p.55 (flagged: via the p.45 note) |
| Floor beam ends, main (framed F5 / F6) | "Fin. Floor Beam" at the inside face of the truss vertical, scaled 1'-2 3/4 from the truss CL (p.85, 4'-5 = 308 px) | 1'-2 1/2 | unchanged (check row) |
| Floor beam ends, approach (F3 / F4) | end angles on the post flange, ~8 in. from the truss CL (p.55, scaled) | 9 1/2 in. (flagged) | unchanged |
| Floor beam ends, girder spans | cover plates flush with the girder F.L. cover plate (22 in. wide, p.45) | 11 in. from the girder CL | unchanged |
| Main shoes L0 / L0' (piers 17 / 20) | rocker pin on the vertical line through L0, 2'-0 1/2 below the chord CL, 3'-5 to the bottom El. 107.682 (p.73), L0 = 675+98.380 / L0' = 689+62.380 (p.1 / p.69, CL pier +- 2'-0, p.16) | as plan | unchanged (check rows) |
| Main shoes L11 / L11' (piers 18 / 19) | pin under L11, 3'-11 below, bottom El. 87.701 (p.96) | as plan | unchanged (check rows) |
| Main lower chord end at L0 / L0' | chord L0-L1 with its end diaphragm (4-Cann. Ls 8x8x3/4, 7/8 web) and the 7/8 gussets end ~1'-5 beyond the vertical line through L0 (p.73 ELEVATION scaled: CL chord to pin 2'-0 1/2 = 51.1 pt -> 25.0 pt/ft; chord end 35.7 pt before the pin line) | cut square AT L0 (generator: a chord with no neighbour at its end point stops at the point) -> the 19 in. sole plate / rocker half outside the steel | extended 1'-5 (flagged dimension-only) |
| Approach bottom chord end at L0 / L0' | chord + gussets end ~10 1/2 in. beyond the pin line (p.59 ELEVATION: the two units' pins 4'-0 apart = 97.7 pt; chord ends 21.3 / 22.1 pt from the pins) | cut at the pin | extended 10 1/2 in. (flagged) |

Root cause of question 3: the generator stops a chord exactly at its last panel point when no other chord continues there (automatic setback 0); the
shoes were in the right place. The chord end extension past an end point is plan data (negative StartSetback / EndSetback); the generator now (a) carries
the envelope gusset to an extended end chord and (b) warns "Truss CHECK shoe ... projects ... beyond the end of chord" when a shoe top sticks out more
than 1 in. past an end chord (it fired at all 20 end shoes before the fix, none after). Generic taper input `TrussMemberDefinition.DepthReductionStart/End`.
Checks after the full run order (truss-ptc-arch -> ... -> rivets-ptc): location 376 PASS / 44 DIFF (unchanged), main 381 / 11 -> 405 / 11 (+24 new rows),
approach 1014 / 12 -> 1046 / 12, girders 125 / 6 -> 131 / 6.

## 13. QC pass 2 back-check (2026-10-20; answers in `qc/ptc/results/answers.csv` I007-I017)
- Generic OPTIONAL inputs: `TrussPlate.BetweenGussets` (cover plates that stop where the end gussets begin), `TrussPlate.Start` / `TrussPly.Start` / `TrussPly.Length`
  (partial pieces from the start working point), `TrussSectionDefinition.ShapeArea` (rolled shape / channel at a printed gross area: web thickness adjusted),
  Rolled sections take `TopPlates` / `BottomPlates` as flange cover plates. Test `TrussTests.RolledCoverPlates_StopAtTheGussets_PartialPlies_ShapeArea`.
- 4-span units (p.57): U3-U5 14 WF 287 + 2-Pls 16 x 15/16, U9-U11 14 WF 342 + 2-Pls 16 1/2 x 13/16, U9-L10 14 WF 342 + 2-Pls 16 1/2 x 7/8, L10-U11 14 WF 314 + 2-Pls
  16 x 11/16: one plate per flange, gusset edge to gusset edge (p.60 U3: the cover plates begin where the gusset ends; mid joints U4 / U10 have the gussets over the
  plates: in-to-in 18 3/8 / 19 1/8 = d + 2 t). Section area = the printed area; the member's mean area is 3 % lower (plates absent under the end gussets). 3-span (p.47):
  no plated members.
- Approach 14 / 16 WF at the printed gross areas (`ShapeArea`): the interpolated sharp-root polygon lacked the fillets (14 WF 61 17.67 vs 17.94).
- Floor beams (p.55): F3 = top 14 x 1/2 F.L. + bottom 14 x 1/2 x 20'-0; F4 = top F.L. + 28'-0, bottom 40'-0 + 28'-0 (centred on the CL bridge, flagged).
- Main post U11-L11 (p.69 / p.70): 34 x 1 1/8 plies from U11 to 9'-0 below the floor beam, 34 x 9/16 o.s. from 5'-0 above to 5'-0 below it; table areas 293.67 (upper)
  / 217.17 (lower) are those sections exactly (the perforated covers count at their gross 23 in. width in the table, as in the model). Mean area 332.17 -> 277.25.
- Main 18 in channel members at the 1954 areas from p.70 (12.49 / 13.38 / 15.18 / 16.98 sq in for 18[ 42.7 / 45.8 / 51.9 / 58.0; AISC v16 MC18 polygons were 0.29 larger).
- I007 / I008 answered (no change): L0 chord end 1'-5 re-measured (17.7 in. at 10.3 px/in., p.73 scale from the 1'-6 3/4 / 3'-8 dimensions); chord solid lengths
  = working-point length + half depth x tan(deflection / 2) per end (square cuts closing the joint); working-point lengths = p.69 (generator checks PASS).

## 14. Stacked (multi-layer) gusset plates (2026-10-20; data `connections/gusset-layers.csv`, script `connections-ptc.csx` section 5)
User observation: "the gusset plates in the plans are multi-layer". Every joint sheet (p.49-53, 59-67, 73-90) was re-checked for more than one gusset callout per joint.

| Joint (both trusses) | Plates per face, inner -> outer | Outer plate extent (scaled, flagged dimension-only) | Page, confidence |
|---|---|---|---|
| 3-span L8 / L8' (piers 11 / 12, 26 / 25) | 7/8 (17 5/8 in. to in.) + 3/4 | lower central zone, chord bottom to ~3'-1 above the W.P., ~6'-0 wide (revision 1: "outer gusset plates at L8 extended") | p.52 / p.49, medium |
| 3-span end joints L0 / L0' | 5/8 (16 1/8) + 1/2 | lower part from the chord end ~3'-0 into the span, up to ~2'-6 | p.59 (p.49: similar), medium |
| 4-span end joints L0 / L0' | 3/4 (16 7/8) + 1/2 | as the 3-span ends, opposite hand | p.59, medium |
| 4-span L10 / L10' (piers 14 / 16, 23 / 21) | 7/8 (17 5/8) + 7/8 | lower zone, 4'-0 past the W.P. toward the higher panel (over the chord splice) | p.63, medium - NEW (the earlier extraction had one pair) |
| 4-span L20 (piers 15 / 22) | 7/8 (17 5/8) + 3/4 | lower central zone, ~5'-3 wide, up to ~2'-8 | p.67, medium |
| main L10 / L10' | 3/4 (2'-5) + 1/2 | central zone over the chord and the lower member ends | p.78, medium |
| main L11 / L11' (river piers) | 1 1/8 + 3/4 + 3/4 | middle 3/4 = whole joint, outer 3/4 below the Section E step (~3'-4 above the W.P.) | p.80 Section E, LOW |
| main L22 (crown) | 1 (the two half gussets, 1/8 in. apart) + 1 1/8 splice gusset | central hexagon across the closure, ~6'-9 wide | p.90, medium |

All other joints have one gusset per face (one thickness callout). Chord web plies, inlay and splice plates of the main chords are splice material, not gusset
layers. Stack order: the plate whose in-to-in is printed lies against the members; the second plate lies outside it (its edges are drawn solid across the
member rivet fields - a plate between member and gusset could not stop inside a member lap without a fill). This supersedes the "outer 7/8 / inner 3/4"
wording of the earlier L8 rows ("outer" meant the larger outline).

Generic input (OPTIONAL, omitted from the .sbm when null; other trusses unchanged): `TrussJointDefinition.GussetLayers` = list of `TrussGussetLayer`
{Thickness, Outline (joint u, v; empty = the joint's gusset outline), ClipToGusset (default on: clipped to the inner gusset outline), Label, Material,
Unresolved (-> dimension-only flag), Source}. The `GussetThickness` gusset stays the inner layer; each further layer is one plate per face (marks
`{plane}-G{pt}-L2` / `-R2`, `-L3` ...) on the outer face of the one under it (constant offset = sum of the thicknesses under it; a layer reaching past a
partial layer under it raises "Truss CHECK ... extends past layer under it"). Rivets: the head sits on the outer face of the outermost layer present at the
rivet; grip = the full stack there + fills / plies to the member's inner web face. Results `TrussModel.GussetStacks` (plane, point, thickness and area per
layer). IFC / database / quantities need nothing new (layers are TrussGusset parts: IfcPlate GUSSET_PLATE, one database part each, panel "Joint n").
Test `TrussTests.GussetLayers_StackOutward_VolumesGripAndRoundTrip`.

PTC data: outlines scaled in the sheet frame (u = drawing x = toward the higher panel number, v up, origin = work point, inches), mirrored for the primed
half and for the New Jersey units; each layer carries an UNRESOLVED dimension flag with page + reason. Main L11 gets its inner gusset 1 1/8 (was the 3/4
default: no joints.csv row), flagged: "2'-5 o. to o. 1 1/8 Gus. Pls." suggests the 1 1/8 plates may be inlaid in the chord web plane.

Results (full run order re-run from truss-ptc-arch; the "before" run reproduced the committed model byte for byte): gusset plates main 368 -> 396 parts,
403,857 -> 430,177 lb; 3-span 200 -> 216 parts per unit, 93,235 -> 99,707 lb; 4-span 328 -> 348, 181,880 -> 190,408 lb. Structural steel +56,300 lb:
33.35 -> 33.41 M lb = 84.9 % -> 85.0 % of p.42 (silicon 93.3 -> 93.5 %, carbon 74.1 % unchanged). Checks: location 378 / 42 (unchanged), main
405 / 11 / 47 -> 426 / 11 / 62 (layer count + stack thickness per joint and plane), approach 1192 / 12 / 108 -> 1268 / 12 / 144, girders 131 / 6 (unchanged).
The layers are clipped to the inner ENVELOPE gussets, which are smaller than the drawn plates (e.g. 3-span L8 outer layer 20.5 SF built of ~22 SF drawn), so
the gusset weight stays low: the drawn inner outlines remain the open item (section 10). Not modeled: the 1/8 in. gap between the L22 half gussets, the L11
3/4 plate ends along the chords, fills between layers (none drawn). The copies "PTC main truss 18-20.sbm" / "PTC approach trusses.sbm" are unchanged
(connections-ptc.csx needs the full PTC model).


## 15. Gusset plate outlines read from the joint sheets (2026-10-20; data `connections/gusset-outlines.csv`, script `connections-ptc.csx` section 4b)
The inner gusset of every joint was an ENVELOPE (convex hull of the member laps, flagged). It is now the plate DRAWN on the joint sheet (p.50-53 3-span,
p.59-67 4-span / unit ends, p.73-89 main), read with `tools/ptc_gusset_outlines.py` (grid renders with absolute page-pixel labels at 150 dpi x zoom 0.75-6,
`prof` = dark-run profiles to locate chord lines / dimension ticks; one page per call). Per row: page, scale (px per inch from a printed dimension: p.50 / p.60
rivet chains 15 in. and 22 1/2 in., p.59 the 4'-0 shoe pin spacing, main sheets the p.73 U0 post 1'-11 1/2 b.-b. Ls; all 3/4 in. = 1 ft, 4.25-4.29 px/in at
150 dpi), work point, two points on the drawn chord, outline polygon, evidence and confidence. `convert` gives the sheet-frame outline in inches (u = drawing x =
toward the higher panel number, v up, origin = work point).

Fitting rules used while reading: edges on the drawn plate edges; edges hidden under splice plates / fills continue the drawn edges (corner cuts taken symmetric);
the bottom (top) edge of a lower (upper) chord joint is the chord bottom (top) line; every rivet of the chord / diagonal / vertical groups inside with about
1 1/4 - 1 1/2 in. edge distance; pier joints carry the drawn extension round the shoe pin (11 in. each side of the pin CL, dimensioned on p.52 / p.67).
Joints the sheets give only as "details not shown, similar to X" take X's outline: `also_applies_to` (3-span L3 / L7 = L1; 4-span L3 / L5 = L1; main L1 = L3,
L16 = L15, L19 = L18, U21 = U20, L21 = L20) and the `truss-approach/joints.csv` notes (3-span U4 / U6 / U10 / U12 = U2, L5 / L11 = L1; 4-span U4 / U6 / U8 /
U12 / U14 / U16 / U18 = U2, L7 / L9 / L11 / L13 / L17 / L19 = L1; 4-span U10 and U20: p.63 / p.67 "similar to joint U8 - sheet 12" = the 3-span U8).

Placement (generic, in the script): sheet frame -> model joint by a VERTICAL SHEAR v += u (tan(model chord) - tan(drawn chord)) about the work point, then mirrored
for the primed half of a unit and for the New Jersey units (the NJ 3-span / 4-span units are the same sheets, opposite hand). The sheets draw plumb posts; the
approach sheets draw one half of each unit on its grade, so the mirrored half would tilt the chord edges by twice the grade under a mirror alone, and a rotation
would tilt the post edges. The shear keeps the chord edges on the model chord and the post edges plumb. It also absorbs the reading error of the drawn chord
direction on the main sheets (drawn vs model up to 9 deg at the steep arch joints; largest shear 12.2 deg at main L12). The stacked layers of section 14 are
placed with the same shear (`jointShear`). Every outline carries a dimension-only UNRESOLVED flag: page + "the plate is drawn, not dimensioned: outline scaled
from a 150 dpi render (confidence, about +-1 in.; evidence ...)" + the 'similar to' source when taken from another joint.

Kept as ENVELOPES (8 model joints x 2 planes): main L11 / L11' (river pier joint: the 1 1/8 + 3/4 + 3/4 stack runs up the L11-L12 arch chord splice; the inner
plate edges cannot be separated from the layers and the splice material at the read resolution, p.80), L13 / L13' and L14 / L14' (p.83 / p.84: the floor beam,
hanger, suspender and lateral connections overlap the gusset edges; not separable), U22 and L22 (p.90 crown: half gussets cut on line "X", splice gusset and the
jacking scheme drawn on top of each other).

Rows (79): 3-span 18 drawn joints (U1-U3, U5, U7-U9, U11, L1, L2, L4, L6, L8-L10, L12, U0', L0'), 4-span 25 (U0, L0, U1-U3, U5, U7, U9, U11, U13, U15, U17,
U19, L1, L2, L4, L6, L8, L10, L12, L14-L16, L18, L20), main 36 (U0-U20, L0, L2-L10, L12, L15, L17, L18, L20). Model: 240 joints from their own drawing +
106 from a "similar to" joint (both trusses, both halves) = all approach joints and 164 of 184 main joint x plane.

Results (connections -> shoes -> walkways -> rivets re-run on the committed model): gusset plates main 430,177 -> 704,719 lb (the drawn plates are larger than
the member-lap envelopes: inner gussets 5,678 -> 8,466 SF per face + 622 SF envelopes), 3-span 99,707 -> 81,491 lb per unit (2,972 -> 2,472 SF), 4-span
190,408 -> 169,042 lb per unit (1,753 -> 1,372 SF; the envelopes reached over the whole member laps, the drawn plates stop at the rivet fields). Structural steel
33.41 -> 33.60 M lb = 85.0 % -> 85.5 % of p.42 (main 86.9 -> 88.6 %, 3-span 85.3 / 84.9 -> 84.7 / 84.3 %, 4-span 85.0 -> 84.6 %). Checks: main 558 / 11 / 63,
approach 1548 / 12 / 148, location 378 / 42 - unchanged counts (the new rows "gusset covers its rivets" were already in the WIP check scripts: 412 PASS, all
rivets >= 1 1/2 in. from the edge; note the joint rivets are DISTRIBUTED over the outline (section 11), so this checks the grid, not the drawn rivet positions).
Tests 568 passed. The model copies "PTC main truss 18-20.sbm" / "PTC approach trusses.sbm" are unchanged.

## 16. QC pass 3 back-check (2026-10-21; answers in `qc/ptc/results/answers.csv` I006, I021-I028, I030-I035; rivet issues I018-I020 / I029 handled separately)
Generic code (OPTIONAL / additive; other trusses unchanged; test `TrussTests.LateralStopsClearOfPortal_OpenSocketJaw`):
- `TrussGenerator.Setbacks`: an upper / lower lateral that meets a `Portal` member at the same point stops where every corner of its end section lies beyond the
  portal's side face ((portal half width + lateral reach along the portal's x) / |lateral axis . portal x| + clearance). Only the PTC main span has portals.
- `SuspenderDefinition.SocketBeyondPin` (eye beyond the pin centre), `SocketJawWidth` / `SocketJawLength` (open socket: two lugs = the socket circle outside the
  jaw gap, from the socket end at the pin to the jaw length; the basket beyond), omitted from the .sbm when 0. Rope length = pin to pin - 2 x (socket - beyond).

PTC data:
- Main floor laterals (p.71 / p.99): two-panel X of 14 BP 73 L13-W15, W15-W17, W17-W19, W19-W21 crossing at the CL of floor beams W14 / W16 / W18 / W20, K of
  2-12[ 25 from W21 to the CL point WM22 (tongue beam 16 WF 88 + shear lock flagged, not built). Was shifted one panel (X centred on L15 / L17 / L19).
- Main floor beams (p.72 schedule, p.94 / p.95): all partial cover plates built, centred on the CL bridge (flagged): F5 bottom 40'-0; F6 top 46'-0, bottom 60'-0 +
  46'-0; F7 56'-0 + 40'-0 each flange; F8 top 46'-0, bottom 76'-0. F8's 2-Ls 9x4x1 / 2-Ls 7x4x7/8 F.L. / 2-Ls 8x4x1 x 76'-0 are NOT built (Section B p.95 does not
  fix the legs / position; flag). F6 mid-span 157.0 sq in = plan.
- Suspender sockets (p.94 detail, p.86): 7 1/4 below the pin, 1'-8 jaw with 4 in. between the lugs, 1'-3 basket; pins ALONG the bridge (RopesAlongBridge). The lugs
  still overlap the floor beam top flange angles (~2 1/2 in.): the beam end at the socket is not dimensioned (flag).
- Bracing areas: 14 BP 73 at 21.4 sq in (ShapeArea), laced channels at their 1954 areas (p.70 for the 18 in, else weight / 3.4).
- Stringer diaphragms 15[ 33.9: one channel per bay between adjacent stringer webs (was one channel through all stringer webs).
- Gusset data re-read: 4-span in-to-in at L7 / L8 / U9 / L9 / L12 / U13 / L14 / U16 / L17 / U19 / U4 (fraction misreads 1/8 <-> 5/8; the corrected values match
  the chord depths), 4-span L5 = L3 (5/8, 17 1/4); main L1 = L3 1/2, L6 7/8 / 29, U10 / U21 / L21 31 1/4 in. to in. Outlines U9 / L15 / L17 re-read (were 11-20 in.
  outboard of the drawn edges).
- Approach fixed shoe pin 3'-6 1/8 (p.52 / p.67 material lists).
- Walkways: main pier 18 / 19 platforms 8'-0 along centred on the pin, 5'-4 to 8'-7 1/16 inboard of the truss (p.104), ladder 6'-6 3/8 inboard (p.103); approach
  platforms from 2'-0 beyond the CL bearing, 3'-6 3/8 long (p.102 View B), ladders at piers 13 / 17 4'-9 3/8 from the 4-span bearing; FB11 walkway from the north
  ladder line to the CL (p.103 key plan).
- Rejected with evidence: I006 girder depth (angle backs 1/4 in. beyond the web edges), I025 bearing plate let 1 in. into the masonry plate (plan), I034 dam tooth
  1'-8 3/4 = tip to support, not the tooth length. Open: I033 railing post layout per panel (needs explicit post stations), the small bracing / walkway connection
  overlaps (connection details not built).

Results (full run order from truss-ptc-arch): structural steel 33.60 -> 33.81 M lb = 85.5 % -> 86.1 % of p.42 (main 88.6 -> 90.0 %); checks main 558 / 11 / 63 ->
560 / 11 / 63, approach 1548 / 12 / 148 (unchanged), girders 131 / 6 (unchanged), location 378 / 42 -> 379 / 41. sbm clash (all PTC bridges, no bolts) 14,293 ->
11,706 rows; every walkway x shoe and portal x lateral row gone, socket x floor beam 6.82 -> 2.43 in.
