# Full-model IFC validation (I-80 WB and Ramp G)

Date 2026-10-11. Model `samples/plans/I-80 WB and Ramp G.sbm` (main as of merge 1908fbe), exported with
`sbm export model.sbm --ifc final.ifc` (plain STEP, IFC4X3_ADD2), validated with IfcOpenShell 0.9.0:

```
python -I tools/validate_ifc.py final.ifc --geom 500 --geom-all "IfcReinforcingBar.*Pier [4-7]WB column" --json final.json
```

## File

- 346.6 MB, 3,755,510 entities; export 18 s, open 25 s, schema validation 334 s, EXPRESS rules (every instance) 410 s,
  geometry 633 s (7,740 products). Peak memory of the validator about 6 GB.
- Products: IfcMechanicalFastener 240,287 · IfcReinforcingBar 121,160 · IfcMember 25,967 · IfcPlate 22,131 ·
  IfcRailing 6,282 · IfcFastener 4,307 · IfcElementAssembly 4,244 · IfcPile 1,693 · IfcBearing 488 · IfcColumn 298 ·
  IfcPipeSegment 104 · IfcOpeningElement 92 · IfcFooting 87 · IfcBeam 70 · IfcWasteTerminal 67 · IfcBridgePart 59 ·
  IfcCourse 47 · IfcWall 33 · IfcBuildingElementPart 28 · IfcDiscreteAccessory 14 · IfcCableCarrierSegment 11 ·
  IfcBuildingElementProxy 10 · IfcTank 8 · IfcJunctionBox 8 · IfcBridge 7 · IfcSlab 7 · IfcPipeFitting 3 · IfcAlignment 2.
- Geometry: 121,160 IfcSweptDiskSolid (bars), 45,834 IfcExtrudedAreaSolid (18,020 with bolt-hole voids),
  31,372 IfcTriangulatedFaceSet, 145 mapped fastener types.

## Schema / EXPRESS issues

| Rule group | Before fixes | After fixes |
|---|---|---|
| Schema (types, cardinalities, inverses, parse errors) | 0 | 0 |
| EXPRESS WHERE rules + global rules (all instances) | 0 | 0 |

The file was already schema- and rule-clean. EXPRESS was run on the full file (no sampling needed).

## Exporter problems found anyway (fixed)

1. **Bolt-hole voids outside the profile** (`src/SBM.IO/Ifc/IfcExporter.cs`). 8 of 18,030 IfcArbitraryProfileDefWithVoids
   had a hole circle crossing or outside the outline: narrow skewed bearing stiffeners (`PL 7" x 7/8", skewed 44–49° (pair)`,
   G1-6-BS1, G2-6-BS1, G3-1-BS1, G3-6-BS1, G5-1-BS1, G6-3-BS1, G6-5-BS1, G6-7-BS1) whose cross-frame holes run off the
   plate. Not caught by the EXPRESS rules, but an invalid area (viewers drop faces). Now a solid is written as an extrusion
   only when every hole is strictly inside the outline and clear of the other holes; otherwise as triangles. After: 0.
2. **Bar role** (`IfcExporter.cs`). Only shape codes TIE/SPIRAL/SSTR-S were `.LIGATURE.`; HOOP and CROSSTIE (column hoops,
   crossties) went out as `.MAIN.`. Now LIGATURE too: 6,585 → 17,381 LIGATURE bars.
3. **Non-reproducible export** (`src/SBM.Core/Steel/SteelModelGenerator.cs`, `src/SBM.Core/Concrete/ConcreteGenerator.cs`,
   `ConcretePlanRebar.cs`). Stiffener numbers (G2-2-CS1, CS2 ... → GlobalIds), girder hole order and plan-rebar bar order
   came from ConcurrentBags filled by parallel tasks, so two exports of the same model differed (and a GlobalId could name a
   different plate/bar on the next export). Now numbered/sorted in a fixed geometric order. Test
   `Ifc_RegeneratedModel_ExportsIdentically`. GlobalId/IfcKey patterns unchanged (`GlobalIds_AreTheIfcGlobalIds` passes).

## Geometry (ifcopenshell.geom, use-world-coords off)

Sample: up to 500 products per class plus all 2,874 column bars of Piers 4WB–7WB. Reference volume: bars
pi/4·d²·BarLength; other products SBM_Fabrication Mass / density (steel 7850, concrete 2400; gratings skipped).
Volume is taken about the mesh centroid. "Open" = edges not shared by exactly two triangles.

| Class | meshed | failed | open | compared | > 2 % |
|---|---|---|---|---|---|
| IfcBeam | 70 | 0 | 38 | 70 | 0 |
| IfcBearing | 488 | 0 | 0 | – | – |
| IfcBuildingElementPart | 28 | 0 | 0 | 28 | 0 |
| IfcCableCarrierSegment | 11 | 0 | 6 | – | – |
| IfcColumn | 298 | 0 | 0 | 298 | 0 |
| IfcFooting | 87 | 0 | 3 | 83 | 0 |
| IfcMechanicalFastener | 500 | 0 | 0 | 1 | 0 |
| IfcMember | 500 | 0 | 69 | 500 | 26 |
| IfcPile | 500 | 0 | 0 | 500 | 0 |
| IfcPlate | 500 | 0 | 6 | 472 | 0 |
| IfcRailing | 500 | 0 | 0 | 500 | 0 |
| IfcReinforcingBar (sample) | 491 | 0 | 22 | 491 | 56 |
| IfcReinforcingBar (Piers 4WB–7WB columns, all) | 2,874 | 0 | 643 | 2,874 | **0** |
| IfcSlab | 7 | 0 | 2 | 7 | 0 |
| IfcWall | 33 | 0 | 3 | 33 | 0 |
| others (Course, Fastener, Opening, Pipe*, Tank, ...) | 377 | 0 | 0 | – | – |

**No element fails to tessellate.** Volume mismatches over 2 % (82 in the sample), none an exporter fault:

- Deck bars `6b1/6b2 #6 SLAB, LONGITUDINAL` (long polylines with 0.1–2° kinks), ratio 0.70–0.95, open mesh: the
  OpenCascade sweep drops faces at tiny kinks (kernel issue, e.g. #762388, #1667891, #2584349).
- Deck bars `7a2/7a5 #7 SLAB, TRANSVERSE` ratio 0.964–0.968, closed: BarLength is the detailed (out-to-out) length of a
  bent bar, longer than the swept centerline, plus the −1.1 % polygonal disk.
- `5b1 #5 abutment footing hoops` 0.95–0.97 (lap overlap / open mesh).
- WT5x15/WT5x44 cable brackets 0.943–0.959 and MC10x25 / W24x68 cross-frame members 1.02–1.045: catalogue weight per foot
  (fillets, rounding) vs. the modeled section; basis difference, not geometry.

Before the fixes the sample also showed 7a12 bars at 1,300× and gratings at 5.8× — both artefacts of the first script
version (volume about the origin for open meshes far from it; grating mass is not a solid). Fixed in the script.

## Round-column ties (HANDOFF §4: "2–4× their volume")

Not reproducible with IfcOpenShell 0.9.0 on the current model: all 2,874 column bars of Piers 4WB–7WB (hoops, flare ties,
crossties, verticals, dowels) mesh within 2 % (about −1.1 %, the polygonal disk). Synthetic flat round hoops (4×90° arcs,
450° with lap, single arcs up to 340°) mesh within 1.1 %. The hoops are reported "open" (643) because the 450° hoop's lap
overlaps itself (non-manifold edges where the sweep re-enters its own volume); the volume is still right. The old 2–4×
figure is most likely the same volume-about-origin artefact on such non-closed meshes. Nothing to fix in the exporter.

## IFC 4.3 alignment (2026-10-11, branch ifc-alignment)

`IfcAlignmentWriter` replaces the 3D polyline axis with the bSI alignment layout (horizontal LINE / CIRCULARARC / CLOTHOID,
vertical CONSTANTGRADIENT / PARABOLICARC, zero-length end segments), IfcCompositeCurve + IfcGradientCurve and stationing
referents. Checked with `tools/ifc-alignment-points.csx` + `tools/check_ifc_alignment.py` (ifcopenshell.geom
function_item_evaluator vs SBM `Alignment.PointAtDistance` / `VerticalProfile.ElevationAt`, every 2.5 m plus element and
vertical breakpoints), `tools/validate_ifc.py` and the US bridge IDS `usBrSpec_General.ids`:

| Model | Alignment | Horizontal | Vertical segs | Max plan / height dev | Schema + EXPRESS (alignment) | IDS Alignment specs |
|---|---|---|---|---|---|---|
| Synthetic (spirals L/R, sym + unsym VC, station equation) | Spiral test | 10 | 9 | 0.0005 / 0.0000 mm | 0 | 7/7 pass |
| Woodbury I-29 NB over Floyd (arc, PGL 5 ft RT) | CL I-29 | 4 | 4 | 0.0000 / 0.0000 mm | 0 | 7/7 pass |
| Br02069 TH65 | SB65 | 2 | 4 | 0.0000 / 0.0000 mm | 0 | 7/7 pass |
| I-80 WB and Ramp G (alignments only, abv mode) | I-80 WB, Ramp G | 4, 6 | 188, 9 | 0.0000 / 0.0000 mm | 0 | 7/7 pass |

Layout vs geometry (ifcopenshell.api.alignment maps each layout segment to geometry; end vs next segment start): max 0.0012 mm.
The IfcPolynomialCurve x² coefficient is written in full-precision exponent form (10 decimals cost up to 0.005 mm on I-80).
The full Woodbury / Br02069 exports have 15 / 7 schema issues, all `IfcBeam` `.GIRDER.` (not in ADD2 IfcBeamTypeEnum,
prestressed beams) — unrelated to the alignment.
(Fixed on branch ifc-abv: prestressed girders are now `IfcBeam` `.GIRDER_SEGMENT.` in every mode -> 0 schema issues.)

## ReferenceView / Alignment-based View geometry (2026-10-11, branch ifc-abv)

`sbm export model.sbm --ifc x.ifc --geometry abv` (`IfcGeometryMode.ReferenceView`, `Ifc/IfcReferenceViewGeometry.cs`):
recorded extrusions -> IfcPolygonalFaceSet (end caps IfcIndexedPolygonalFaceWithVoids, bolt holes = equal-area polygon
void loops, sides / hole walls planar quads), arc-profile prestressed beams -> one face set cut exactly by the skewed end
planes. Chord tolerance 1/32 in (0.79 mm) -> 9-sided 15/16 in bolt holes. Checks: `tools/validate_ifc.py`,
`tools/run_ids.py x.ifc "usBrSpec_AbV Exclusions.ids"` (261 forbidden entities), `tools/compare_ifc_volumes.py par.ifc
abv.ifc --changed-only --exact-ref` (AbV face-set mesh volume vs the analytic Parametric extrusion volume, same GlobalId).

| Model | Mode | Size | Entities | Export | Schema + EXPRESS | AbV Exclusions IDS |
|---|---|---|---|---|---|---|
| Br02069 TH65 | Parametric | 0.46 MB | 4,988 | 0.1 s | 0 + 0 | 257/261 (IfcExtrudedAreaSolid, IfcBooleanClippingResult, IfcHalfSpaceSolid, IfcPlane) |
| Br02069 TH65 | ReferenceView | 0.50 MB | 5,323 | 0.1 s | 0 + 0 | 261/261 |
| Woodbury I-29 NB over Floyd | Parametric | 0.26 MB | 2,011 | <0.1 s | 0 + 0 | 260/261 (IfcExtrudedAreaSolid) |
| Woodbury I-29 NB over Floyd | ReferenceView | 0.27 MB | 2,080 | <0.1 s | 0 + 0 | 261/261 |
| I-80 WB and Ramp G | Parametric | 345.7 MB (ifczip 72 MB) | 3,757,439 | 3.7 s | (0 + 0, see above) | 45,834 IfcExtrudedAreaSolid |
| I-80 WB and Ramp G | ReferenceView | 765.5 MB (ifczip 177 MB) | 6,031,963 | 6.6 s | 0 + 0 (every instance, 1,125 s) | 261/261 |

(Export = the IfcExporter call only, after regeneration.) The I-80 growth is the ~324,000 bolt holes: 9 wall quads,
18 points and two void loops each (4.36 M IfcIndexedPolygonalFace). Geometry, I-80 changed products (face sets), up to
1,500 per class (5,489 products): 0 over 0.5 %, max |ratio - 1| about 1e-5 (coordinate rounding), 0 tessellation
failures; Br02069 beams 1.00004, Woodbury beams 1.0000000. Earlier variants for the record: inscribed hole polygons left
2" conduits 10.6 % and web splices 0.5 % too heavy -> equal-area polygons. Mesh comparisons against the Parametric file
are misleading for tubes (OpenCascade meshes the IfcCircle bores coarsely: 8" pipes then look 4 % off) -> `--exact-ref`.
"Open" AbV meshes (angles = two touching leg solids, a few plates) are the same shapes the Parametric mesh reports open.
Consumers: ifcopenshell.geom needs ~0.15 s per AbV plate with holes vs ~0.07 s for the extrusion.
GlobalIds: every element GlobalId is the same in both modes; SBM_Fabrication property sets are keyed by the STEP id of
their first element, so their GlobalIds differ between modes (pre-existing keying).
