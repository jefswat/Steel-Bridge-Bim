# Roads, retaining walls, utilities and LOD - design note (2026-10-28)

Status: section 1 (LOD) stage 1 BUILT 2026-10-28 (docs/API.md "Level of detail"; 100-400, no 500 per answer 7a.2). Roads stage 1 BUILT 2026-10-28 (corridor template, courses, curbs, side slopes / daylight, IFC IfcRoad, `sbm roads`; docs/API.md "Roads"); stage 2 BUILT 2026-10-29 (viewport, database, earthwork, clearances under the bridge, road ends tied to the bridge, approach slabs with the per-end Owner option). Open for roads: LandXML corridor / surface import (section 2.3). Cantilever retaining walls stage 1 BUILT 2026-10-29 (docs/API.md "Retaining walls"). MSE walls BUILT 2026-10-29 (contract-plans envelope + supplier reinforcements at LOD 400; abutment-on-MSE interface open). Grading stage 1 BUILT 2026-10-29 (section 3b; docs/API.md "Grading"). Utilities: design only.
User choices 2026-10-28: LOD = BIMForum LOD 100-500 per element group; roads = approach corridor + roads under / beside the
bridge + Civil 3D import; walls = cantilever CIP + MSE; utilities = storm drainage + other utilities (water, sewer, gas,
electric, telecom, incl. those hung on the bridge).

Rules that apply throughout (standing): every value from the plans or an explicit input; anything missing = UNRESOLVED flag
with page + plain reason; plan-vs-plan = CONFLICT row; no code-rule defaults (no AASHTO / MASH / FHWA MSE rule filled in -
flag "potential noncompliance" where a rule would apply); all new inputs OPTIONAL and omitted from the .sbm when unset;
existing outputs byte-identical (Woodbury / Sample IFC checked).

## 1. LOD selector (BIMForum)

### 1.1 What the levels mean in this program
BIMForum LOD Specification levels, mapped to what the generators build. LOD is about geometry reliability; information
(LOI) stays complete at every level (properties, sheet links, flags are always written).

| LOD | BIMForum meaning | What SBM builds |
|---|---|---|
| 100 | Symbol / massing, approximate | Envelope solids: girder as one box per span, deck as one slab, piers as column + cap blocks, walls as one prism, pipes as centerlines, roads as the top surface only |
| 200 | Generic system, approximate size / shape / location | Plates per girder field section without holes, frames as members without gussets, wall stem + footing without joints, pipes as solids without fittings, road layers as one pavement body |
| 300 | Specific: size, shape, location, orientation as designed | Today's model without fasteners / welds / holes / bars: exact plates, gussets, splices, stiffeners, walls with joints and panels, pipe network with structures, road courses |
| 350 | + interfaces to other elements (connections, supports) | 300 + bolt / rivet holes, connection parts, bearings complete, wall drainage / coping / leveling pad, pipe connections to structures, hangers on the bridge |
| 400 | Fabrication / installation detail | Everything the generators make today: bolts, rivets, welds, studs, rebar with true bends, strands, MSE straps / panels as cast |
| 500 | Field verified | NOT a geometry level. Same geometry as 400; an element is marked 500 only when its agent review / as-built check has a confirmed record. Selecting 500 never invents verification |

### 1.2 Inputs
`BridgeProject.Lod` = `LodSettings` (OPTIONAL, null = today = 400 everywhere, output unchanged):
- `Default` level.
- `Groups` = level per element group: Girders, CrossFrames / Bracing, Connections (splices, gussets), Fasteners (bolts, rivets,
  studs), Welds, Bearings, Deck, Barriers / railings, Reinforcing, Prestressing, Substructure, Foundations, Misc / drainage,
  Truss, Arch, Segmental, Roads, Walls, Utilities, Terrain.
- `Overrides` = selector rules (same selector language as the sheet links: bridge / unit / support / category / mark) -> level,
  for "LOD 400 at Pier 19, 300 elsewhere".
- Separate optional levels per OUTPUT (viewport, IFC, Rhino, DXF / sheets, database): an output level can only be LOWER than
  the model level (you cannot export detail that was not generated).

### 1.3 How it is applied
- Generation: each generator receives its group's level (`LodContext`) and skips work above it (fasteners, welds, holes, bars at
  < 400; connection parts at < 350; envelopes at 100). This makes low LOD regeneration much faster (rivets / bars dominate
  PTC and I-80 regeneration). Part of the RegenerationCache key.
- Outputs: viewport / IFC / Rhino / DB filter by their own level. IFC writes Pset `SBM_LOD` {Level, Group, Basis} on every
  element (only when `Lod` is set, so default IFC stays byte-identical).
- App: ribbon "Level of detail" panel: model default, per-group grid, override list, per-output levels; quick buttons 100 /
  200 / 300 / 350 / 400. Status bar shows the active level. The existing VRAM ladder (`VramPlanner`) stays separate - it is
  automatic memory relief, not a deliverable level.
- CLI: `sbm export --lod 300`, `sbm regen --lod Groups.Fasteners=200`. Agent review reports the LOD actually generated
  per group, so a deliverable at "LOD 350" cannot silently lack connections.

## 2. Roads

### 2.1 Approach roadway corridor (`BridgeProject.Roads` = list of `RoadDefinition`)
- Alignment + profile (existing `Alignment`, `VerticalProfile`; LandXML import already works) and `CrossSlopeTable` (already has
  pivot offsets and breaks = superelevation).
- `Templates` by station range (`RoadTemplateRange` from / to station, transition length): left / right lists of
  `RoadComponent` = Lane, Shoulder, CurbAndGutter (section polygon, like `BarrierProfilePoint`), Sidewalk, Median, Barrier
  (reuse the custom barrier profile), Ditch, SideSlope (cut / fill slopes + daylight to terrain), each with width (constant or
  station table), slope (from the cross-slope table or its own), and `Courses` (surface / base / subbase thickness + material).
- Ends: begin / end at stations, or at the bridge deck end / approach slab (`BridgeName` + end) so the road meets the bridge.
- Generator `Road/RoadGenerator` (pure): template evaluated at stations (sample rule like the deck loft, exact NURBS when
  ExactCurvedGeometry is on), course solids per component, top surface TIN, daylight lines against the terrain
  (`ElevationGrid`; no terrain = side slopes not built, UNRESOLVED), earthwork cut / fill volumes (average end area + solid).
- Output: `RoadModel` (courses, kerbs, surfaces, daylight lines, volumes, diagnostics).
- IFC 4.3: IfcRoad > IfcRoadPart (CARRIAGEWAY / SHOULDER / SIDEWALK / ...) > IfcPavement / IfcCourse (PAVEMENT / BASE_COURSE ...),
  IfcKerb, IfcEarthworksFill / IfcEarthworksCut, alignment from the existing writer. Database group "road" > parts. Viewport
  layers 3D-ROAD-*. Quantities: SY / CY per course, LF curb.

### 2.2 Roads under / beside the bridge
Same `RoadDefinition` (often lane + shoulder only). Plus `Clearances`: required vertical / horizontal clearance VALUES as
inputs from the plans (no default; missing = UNRESOLVED), checked against the lowest steel / soffit / substructure faces
over each lane edge -> table + CONFLICT rows. Lane edge lines feed the existing `GroundBarrierDefinition` / pier protection.

### 2.3 Civil 3D import
- LandXML `Surfaces` (TIN) -> `ImportedRoadSurface` element (not only a reference mesh): layer name, the surface, optional
  thickness / course list from an input to make solids; without thickness = surface only, flagged.
- LandXML `PlanFeatures` / corridor feature lines -> breaklines that drive a `RoadDefinition` (widths from the feature line
  offsets per station), so an imported corridor becomes a parametric road the user can edit.
- LandXML `PipeNetworks` -> utilities (section 4.3).
- IFC 4.3 road import from Civil 3D: later (reference only).

## 3. Retaining walls (`BridgeProject.Walls` = list of `RetainingWallDefinition`)
Common: wall control line (own alignment, or offset from a road / bridge alignment), begin / end stations, `FaceSide`,
`TopOfWall` table (station, elevation or offset from a profile / road edge), `BottomOfWall` / footing top table, joint
stations (expansion / contraction), `Source`, `Unresolved`.

### 3.1 Cantilever cast-in-place
Stem (top thickness, front / back batter), footing (toe, heel, thickness, key: width / depth / position), footing steps (station
table), coping / barrier on top (custom barrier profile), drainage (weep holes, drain pipe behind the heel: utilities
link), piles under the footing (reuse `FootingDefinition` piles). Rebar later through the generic `PlanBarSet` (already used
for Br02069 / NHT). IFC: IfcWall .RETAININGWALL. + IfcFooting .STRIP_FOOTING. (+ piles), DB "retaining wall" > stem /
footing / panels between joints.

### 3.2 MSE
Facing (precast panel type / size / joint pattern, or block / wire), leveling pad (width, thickness, steps), coping (profile),
soil reinforcement: type (strip / grid), length table (station or height ranges), vertical spacing, connection - ALL from
the plans or the wall supplier's drawings; reinforcement length is never derived from a height ratio (flag). Reinforced
zone envelope always built (LOD 200-300); individual straps at LOD 400. Abutment-on-MSE interface (bearing seat on the
wall, sleeves around piles) as later inputs. IFC: IfcWall .RETAININGWALL. (facing panels as IfcBuildingElementPart /
IfcPlate), IfcReinforcedSoil for the zone, straps as IfcReinforcingElement / IfcMember 'SOIL_REINFORCEMENT', IfcFooting
leveling pad.

## 3b. Grading (`BridgeProject.Gradings` = list of `GradingDefinition`; added 2026-10-29 at the user's request, built before storm drainage)
Feature lines with elevations (station / offset / elevation on an alignment, or X / Y / elevation), open (berm, bench, toe line: slope to the Left or
Right) or closed (pad: flat-triangulated top, slope to the Outside); slope to the existing ground at the plan's cut / fill ratios (rays every
SampleSpacing, fans at convex corners, bisector rays at re-entrant corners). Slope protection (riprap, slope paving) on the slope or the top, top = finished
grade, thickness normal to the surface. Earthwork cut / fill against the existing ground (plan grid sampling). FINISHED GROUND query = road surfaces, then
grading, then the existing ground (storm drainage rims / cover read it). IFC: IfcElementAssembly 'GRADING' in the site > IfcGeographicElement TERRAIN
(open face sets) + IfcCourse PROTECTION. Later: merged finished-ground TIN export (LandXML), grading between two feature lines, ditches with a section,
walls / abutments as grading targets.

## 4. Utilities (`BridgeProject.Utilities` = list of `UtilityNetworkDefinition`)
### 4.1 Model
- Network: system (Storm, Sanitary, Water, Gas, Electrical, Telecom, Lighting, Other), owner, `Source`.
- `Structures` (nodes): manhole, inlet, catch basin, junction box, headwall, cleanout, valve, handhole; location (station /
  offset or N / E), rim / top, sump / invert, shape and size (round / box), wall thickness, frame / grate.
- `Links`: pipe, conduit, duct bank (rows x cols of conduits + encasement), casing; from / to structure or free end, invert
  in / out (or slope), shape (circular / elliptical / box), size, wall thickness, material, bedding / cover.
- Bridge-hung runs: link `Support` = hangers on girders / cross frames / truss floor beams at a spacing from the plans, or a
  conduit cast in the barrier / deck (the existing lighting conduit becomes one case). Expansion fittings at joints.
### 4.2 Checks
Clash and clearance against the structure, walls, roads and other utilities (existing `SteelClashCheck` engine + concrete),
minimum cover / separation VALUES only from inputs (none assumed), hydraulic sanity (slope sign, invert drops at structures)
as INFO rows, never changed.
### 4.3 Sources
Plan profile tables (structure / pipe tables -> CSV -> script, like the drain data), LandXML `PipeNetworks` (Civil 3D export:
structures, pipes, inverts, sizes) importer, and the existing Ramp G drain / pier drain items stay where they are.
### 4.4 Outputs
IFC 4.3: IfcDistributionSystem (STORMWATER / SEWAGE / WATERSUPPLY / GAS / ELECTRICAL / COMMUNICATION) grouping
IfcPipeSegment / IfcPipeFitting / IfcDistributionChamberElement (MANHOLE / INSPECTIONCHAMBER / ...) / IfcCableCarrierSegment /
IfcCableSegment, IfcDiscreteAccessory hangers; DB group "utility" > network > structures / links; viewport layers
3D-UTIL-<system> (colour per system, APWA colours); quantities LF by size, EA structures.

## 5. Cross-cutting
Plan sheet links (new categories road / retaining wall / utility in the rules files), agent review (starting values, flags),
`sbm pipeline` (roads / walls / utilities regenerate in phase 3 after the bridges: they need deck ends and steel for
clearances), DXF plan / profile views later, Rhino export through the existing mesh / NURBS paths.

## 6. Build order (after PTC)
1. LOD framework (settings, generator gating for fasteners / welds / holes / bars, output filters, Pset, app panel). Test on
   PTC and I-80 (regen time at 300 vs 400).
2. Road corridor (template, courses, daylight, IFC IfcRoad) + clearance check for roads under the bridge.
3. Cantilever walls, then MSE.
3b. Grading (feature lines, pads, slopes to the ground, protection, finished ground) - added 2026-10-29.
4. Storm drainage + LandXML PipeNetworks import. (Stage 1 built 2026-10-30: structures, pipes, checks, LandXML import, IFC / DB / viewport. Stage 2
   built 2026-10-30: clash / clearance, cones / frames / covers, wall openings, duct banks, profiles.)
5. Other utilities + bridge-hung runs. (Built 2026-10-30: valves / hydrants / handholes / vaults, conduit classes, cover-defined inverts, hangers.)
6. Civil 3D corridor / feature line import. (Built 2026-10-30: feature lines -> road between the lines, template conversion; TIN surfaces stay terrain / references.)

## 7. Open questions for the user
1. Test plan set: which project has roadway, wall and utility sheets to build against (I-80 / Ramp G roadway sheets? Woodbury?)
   - none of the current PDFs is known to include them.
2. LOD 500: OK to tie it to a confirmed agent review / as-built record (never set by geometry)?
3. Road ends at the bridge: does the approach slab belong to the road or the bridge (today: neither is modeled)?
4. MSE: model to the supplier's drawings (straps) or the contract plans (zone envelope only) by default?
5. Utilities on the bridge: are hanger types and spacing normally on the bridge plans, or on separate utility plans?

### 7a. User answers (2026-10-28)
1. Test set: generic test models only (made-up test inputs) until a real plan set with roadway / wall / utility sheets is supplied.
2. LOD: the selector STOPS AT 400. No LOD 500 for now (user: 500 is typically as-built / asset management; not a modeling level here).
3. Approach slab: per project setting (an option on each bridge end; default chosen per plan set).
4. MSE walls: contract plans envelope by default (panels / coping / leveling pad + reinforced-zone envelope); straps only when supplier drawings are given.
5. Utility hangers: either - hanger inputs are always optional and the source page (bridge plans or utility plans) is recorded.
