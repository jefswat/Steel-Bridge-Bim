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

2D views / sheets; shear keys, alignment keys, epoxy joints; mild reinforcing; temporary PT bars and their blocks; bent / end segment PT bars;
future PT pipes F1-F4; anchor / deviation blocks; diaphragm and access openings; parapets and rail; expansion joints; lighting; piles; casting curve
and camber (not in the plans); tendon profiles beyond straight joint-to-joint runs (drawn, not tabulated).
