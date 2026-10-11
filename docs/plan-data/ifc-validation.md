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
