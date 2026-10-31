# Steel Bridge Modeler — automation API

There are three ways to drive the modeler from code. All three use the same engine as the desktop app and read and
write the same `.sbm` project files.

| Use | Tool | Best for |
|---|---|---|
| **Command line** | `sbm.exe` (next to the app in `dist\SteelBridgeModeler`) | Python, Excel/VBA, batch files, CI |
| **C# scripts** (`.csx`) | `sbm run script.csx file.sbm`, or **Home ▸ Run Script** in the app | Parametric studies and bulk edits |
| **.NET library** | `SBM.Api.dll` → `SbmDocument` | Custom tools and add-ins |

## Property paths and units

Every input in the model has a path:

```
Bridges[0].Supports[1].SkewDegrees
Bridges["Sample Bridge"].Girders.Section.WebDepth
Bridges[0].Girders.Section.PlateChanges
Bridges[0].CrossFrames.ChordAngle
Bridges[0].Bearings.Type
Bridges[0].LateralBracing.Bays
Alignments[0].StartStation
References[0].Opacity
```

You can index lists by number or by name: `Bridges[0]` and `Bridges["Sample Bridge"]` are equivalent.
`sbm get file.sbm <path>` lists what is available at any level.

Values are text with engineering units:

| Kind | Examples |
|---|---|
| Lengths | `12'-6 1/2"`, `12.5'`, `54"`, `1.2 m`, `300 mm` |
| Stations | `12+34.56` (SI: `1+234.567`) |
| Angles | `20`, `20.5`, `15°30'00"` |
| Slopes | `-2%`, `-0.02` |
| Enums | `Disc`, `Elastomeric`, `GuidedExpansion`, `Top` (not case-sensitive) |

A plain number takes the unit the input windows use for that field:
- inches for plate sizes, depths, gaps and clearances;
- feet for spans, offsets and spacings (meters and millimeters in SI projects).

Add the unit whenever there could be doubt.

JSON values (starting with `{` or `[`) replace a whole object or list, and are written in internal SI units
(meters, degrees).

## Command line

```bat
sbm new  "C:\Jobs\Br12\Br12.sbm" --sample
sbm info "C:\Jobs\Br12\Br12.sbm"
sbm get  "C:\Jobs\Br12\Br12.sbm" "Bridges[0].Supports[1]"
sbm set  "C:\Jobs\Br12\Br12.sbm" "Bridges[0].Supports[1].SkewDegrees=20" "Bridges[0].Girders.Section.WebDepth=60\""
sbm add  "C:\Jobs\Br12\Br12.sbm" "Bridges[0].FieldSplices" "Station=22+90"
sbm remove "C:\Jobs\Br12\Br12.sbm" "Bridges[0].FieldSplices[2]"
sbm import-landxml "C:\Jobs\Br12\Br12.sbm" "C:\Jobs\Br12\alignments.xml"
sbm regen  "C:\Jobs\Br12\Br12.sbm"
sbm export "C:\Jobs\Br12\Br12.sbm" --ifc Br12.ifc --dxf Br12.dxf --landxml Br12.xml
sbm export "C:\Jobs\Br12\Br12.sbm" --ifc Br12.ifczip   # compressed IFC-ZIP (about 5x smaller; same model inside)
sbm export "C:\Jobs\Br12\Br12.sbm" --ifc Br12.ifc --geometry abv [--chord-tol 0.8]
                                                 # IFC 4.3 Alignment-based View: no extrusions / booleans / half spaces; plates with
                                                 # holes and clipped beam profiles as IfcPolygonalFaceSet (holes = void loops), hole
                                                 # circles and profile arcs polygonized to --chord-tol mm (default 1/32" = 0.79 mm).
                                                 # --geometry parametric (default) = exact extrusions. Prints time, size, entity count.
sbm export "C:\Jobs\Br12\Br12.sbm" --3dm Br12.3dm [--bridges "Unit 2,Unit 3"] [--no-detail] [--mesh-only] [--terrain] [--bar-solids] [--3dm-version 7] [--centerlines] [--no-exact]
                                                 # Rhino 3DM (see "Rhino export" below): exact extrusions / B-reps, fastener blocks,
                                                 # bar + alignment curves, layers per bridge, IFC GlobalIds as user text
sbm 3dm-info   Br12.3dm [--blocks]              # read a 3DM back: units, objects by layer and type, blocks, document text
sbm run    "samples\scripts\web-depth-study.csx" "C:\Jobs\Br12\Br12.sbm"
sbm bench  "C:\Jobs\Br12\Br12.sbm"
sbm set    "C:\Jobs\Br12\Br12.sbm" "Geometry.ExactCurvedGeometry=true"   # project setting: exact NURBS for curved parts (default off)
sbm nurbs  "C:\Jobs\Br12\Br12.sbm" [--bridge "Br12 Unit 2"] [--chord 1/32"] [--tol 1/256"] [--csv nurbs.csv]
                                                 # regenerates with the setting off and on: timings, what became NURBS, fit deviation, exact vs mesh volumes
sbm sheets     "C:\Jobs\Br12\Br12.sbm" "C:\Jobs\Br12\Br12 Sheets.pdf"   # 11x17 plan set (vector PDF)
sbm sheets     "C:\Jobs\Br12\Br12.sbm" "Br12 Sheets.pdf" "Br12 Unit 2" --dxf "Br12 Sheets DXF.zip" [--models]
                                                 # + every sheet as a paper-space DXF (inches, border + title block) in one zip
                                                 # (a folder path instead of *.zip writes the files); --models adds the full-size
                                                 # 2D model DXFs under models/ in the zip / folder
sbm compare-sheets "C:\Jobs\Br12\Br12.sbm" "Br12 Unit 2" --plans "C:\Jobs\Br12\plans" --db Br12.sqlite --out compare.pdf
                                                 # each generated sheet side by side (vector) with the matching plan page + checklist/QC strip; needs Python + PyMuPDF
sbm qc-sheet   Br12.sqlite "Br12 Unit 2:S-17" design-116:43 ok|issue|unchecked "note"   # store the check result in the companion database (qc table)
sbm asset      "C:\Jobs\Br12\Br12.sbm" photo add IMG_0412.jpg --element CAP2   # asset management: photos, documents, inspections, findings, calcs
                                                 # (see "Asset management" below; "sbm asset <model> help" lists the commands)
sbm drawings   "C:\Jobs\Br12\Br12.sbm" "C:\Jobs\Br12\dxf"   # 2D DXF models: layout, plan, typical section, bents, bar bending
sbm drawings   "C:\Jobs\Br12\Br12.sbm" "C:\Jobs\Br12\dxf" --zip   # same views as one zip (dxf.zip); a target ending in .zip does the same
sbm project    "C:\Jobs\Br12\Br12.sbm" --view top --bridge "Unit 2" --transparent 3D-DECK --pdf plan.pdf --dxf plan.dxf --svg plan.svg
                                                 # 3D -> 2D projection of the model solids with hidden-line removal (view engine):
                                                 # visible edges on the 3D layer names, hidden edges on <layer>-HIDDEN (dashed), every
                                                 # line tagged with its element (DXF layer, SVG data-tag). --view top|front|left|x,y,z
                                                 # [--up x,y,z] [--origin x,y,z]; depth slab --near/--far (model units, from the origin
                                                 # = center of the selection, along the view direction; the near plane cuts and caps
                                                 # solids like a section); --layers 3D-ARCH,-3D-ARCH-PT (prefixes, '-' excludes),
                                                 # --kinds, --girder N; --bolts --welds --rebar (centerlines); --hidden show|none|off;
                                                 # --method exact|zbuffer; --no-holes; --window x0,y0,x1,y1
sbm project    "C:\Jobs\Br12\Br12.sbm" --bridge "Unit 2" --developed girder:C --girder 3 --window "100',-1000',160',2000'" --pdf gC.pdf
                                                 # developed elevation: x = distance along the girder (or --developed alignment[:name]
                                                 # = station), y = elevation, viewer on the right (--from-left); girder default near
                                                 # plane just right of its widest flange
sbm elevations "C:\Jobs\Br12\Br12.sbm"          # grade → deck → haunch → web → flange → sole plate → seat, every bearing
sbm blocking   "C:\Jobs\Br12\Br12.sbm"          # camber, deflections, girder-line haunch elevation and blocking at each key point (--all: every plan point)
# Camber data in the IFC (no geometry change; the model is the final shape): Pset SBM_Camber - steel girders: one set per girder on its field
# section assemblies (table values by distance along the girder: PlanPoint, Camber, SteelDeflection, SlabDeflection, NoLoadTopOfWeb, Blocking,
# HaunchElevation); prestressed beams with a plan camber table: on the IfcBeam (Camber, SdlDeflection, FinalCamber by span fraction).
```

**Exit codes:** `0` means OK, `1` means a usage or input error, and `2` means the model has errors.
`set`, `add`, `remove` and `import-landxml` save the file.

**Python:** see `samples/scripts/skew_study.py`. It calls `sbm` through `subprocess`. Project files are plain JSON
(SI units), so you can also read them directly.

## Rhino export (`--3dm`)

`sbm export model.sbm --3dm out.3dm`, `SbmDocument.Export3dm(path, bridges, detail, meshOnly, terrain, barSolids)` or the app
(Output > CAD > Export Rhino (.3dm)) write the whole generated model with McNeel's open-source **rhino3dm 8.17.0** (NuGet `Rhino3dm`,
MIT; openNURBS; the native `librhino3dm_native.dll` ships beside `sbm.exe` / the app). Rhino 6 or later opens the file (archive version 8).

**Geometry (best fidelity first).**

| Model data | Rhino object |
| --- | --- |
| Recorded exact extrusion (`ExtrudedSolid`: plates with bolt holes, rolled shapes, boxes, welds, piles ...), sweep normal to the profile | Extrusion: outer profile (lines; true arcs from `ArcPolygon`) + one circle per hole, capped |
| Same, sweep oblique to the profile (plates along a grade with plumb ends, battered piles ...) | Extrusion along the sweep, both ends MITERED back to the profile plane (right section = profile projected along the sweep: hole circles become ellipses, arcs ellipse arcs - exact rational curves) |
| Same, swept almost parallel to its profile plane (no valid miter) | closed B-rep (planar caps, ruled sides, half-cylinder hole walls) |
| Exact NURBS solid (`Mesh.Nurbs`, project setting Geometry.ExactCurvedGeometry) | closed B-rep: NURBS faces, planar faces, holes trimmed by the hole walls' edges (shared edges) |
| Prestressed beam with a true-arc profile (`PrestressedBeam.Body`) | closed B-rep: profile caps projected onto the skewed end planes, ruled sides |
| Round sections (columns, drilled shafts, piles: regular n-gon of 16+ vertices on the circle) | Extrusion of the TRUE circle (vertex radius = the plan diameter / 2; the volume is the circle's, 0.6 % above the 32-gon mesh) |
| Closed polyhedral meshes (caps, footings, walls, pedestals, bent plates ...: no recorded description) | closed B-rep of their planar faces, one per shell (coplanar triangles merged, T-junctions resolved, shared edges), grouped per element |
| Curved parts (deck, haunches, barriers, curved girder plates) | NURBS B-reps: the export regenerates with `Geometry.ExactCurvedGeometry` ON when the project has it off (`--no-exact` / `exactCurved: false` = the model as set; the setting is restored, nothing saved) |
| Anything left (faceted curved surfaces over 400 faces, open / pinched meshes, concrete diaphragm stacks) | Mesh (double-precision vertices, creases split at 30 deg); the reasons are printed ("still meshes: ...", `Rhino3dmResult.MeshReasons` / `MeshItems`) |
| Bolts, rivets, studs, anchor bolts | one block (instance definition) per type and size + an instance reference per fastener |
| Bars, strands, PT tendons | curves: lines + true arcs (ArcFit, 0.5 mm) or polylines; `--bar-solids` adds an EXACT closed pipe B-rep per bar on `<layer>-SOLID` (4 quarter faces along the whole bar - quarter cylinders on lines, revolved torus patches on arcs, degree 2 with double knots at the piece joints - + 2 planar caps; `RhinoBarPipe`); mesh tube only for a real kink (> 10 deg) or a self-overlapping centerline (lap tail on a leg), reasons in MeshReasons |
| File version | Rhino 8 archive by default; `--3dm-version 7` writes a Rhino 7 archive (Rhino 7 cannot open version 8 files; Rhino 8 reads both). `SbmDocument.Export3dm(..., fileVersion: 7)` |
| Alignments | `Alignments::<name>`: plan curve (lines / arcs exact, spirals sampled <= 0.5 m) at the base elevation + 3D profile grade line (<= 1 m) |
| Terrain references (`--terrain`, visible ones) | `Terrain::<name>` mesh (the display mesh) |

**Deck as solids (2026-10-27).** With exact geometry on (the export default) every deck element is a closed B-rep or extrusion built from the
generator's exact functions: slab (also with the sloped overhang soffit: exact bottom with a vertical riser at the overhang root; each straight end
one planar face bounded by the exact top / soffit curves), haunches (clipped at skewed / kinked deck ends like the mesh), custom barriers / curbs
and deck widenings (NURBS sweeps; straight zone pieces stay extrusions), joint blockouts, shelves, device envelopes, end blocks / thickened slab ends
(exact blocks between insets from the deck end, split at vertical steps), integral steel cap haunches (split at the deck edge under a widening).
Non-planar end polygons of sweeps (kinked ends) are written as planar pieces of their triangulation. Read-back check: `sbm 3dm-info out.3dm
--objects "3D-DECK|3D-BARRIER|3D-JOINTS"` (one row per object: type, closed, valid, volume by quadrature, bounds, model `Volume_m3` (= the exact
volume), element key, naked edges of an open B-rep).

| Model (deck objects: slab, haunch, bump, cap haunch, barrier / curb, blockout, end block, joint device) | before: closed B-rep / extrusion / mesh | after |
| --- | --- | --- |
| Woodbury | 18 / 0 / 1 (custom barrier) | 23 / 4 / 0 |
| Nebraska St Ramp B | 5 / 3 / 6 (overhang slab, cap haunch, 2 barriers, 2 joint devices) | 25 / 14 / 0 |
| Northaven NHT (approach units) | 26 / 0 / 19 (10 curbs, 5 end blocks, 4 joint devices) | 45 / 0 / 0 |
| I-80 WB + Ramp G | 65 / 0 / 36 meshes (4 open slab meshes, 2 barriers, 12 blockouts, 12 end blocks, 6 devices) | 107 / 0 / 2 meshes + 4 open slab B-reps |

Volumes read back from the 3DM agree with the generator's exact volumes within 0.01 % (barriers; everything else < 0.001 %). File sizes change little
(Woodbury 50.3 -> 50.8 MB, Nebraska 110.2 -> 111.4 MB, NHT 116.1 -> 117.5 MB, I-80 + Ramp G 2398 -> 2430 MB); export times 4.6 -> 4.8 s, 43.6 -> 45.3 s,
9.7 -> 10.7 s, 756 -> 773 s (I-80: 650 s of it is the steel parts). End blocks with a step are two objects (one element, grouped).
Still not solids: the four I-80 / Ramp G U4 slabs whose kinked / skewed ends cross cross-slope kink lines are B-reps with naked edges (the exact-mode
slab panel layout leaves gaps there; the tessellated slab mesh of those decks is open too), the Ramp G U4 blockout and end block at Pier 19G (the
kinked end's break points change across the block width: meshes), the NHT arch-span deck (ArchGenerator form-deck slab: mesh), concrete diaphragms
(faceted slab lofts: meshes) and tub girder haunches (lofts).

A part whose recorded description Rhino rejects falls back to its mesh (counted and listed: "fallbacks to mesh", `Rhino3dmResult.FallbackItems`).
`--mesh-only` writes every solid as a mesh (one per element). `--no-detail` leaves out fasteners, welds and bars / strands / tendons.

**Organisation.** Layers `<bridge>::<3D layer>` with the viewport layer names and colours (`ModelLayers`, e.g. `Unit 2::3D-GIRDER-PLATES`,
`PTC Arch truss 18-20::3D-TRUSS-CHORDS`, `...::3D-RIVETS`), `Alignments::<name>`, `Terrain::<name>`. Object name = the mark (part mark, bar
"Bar 5a1", unit solid name ...). Attribute user text: `Kind`, `Mark`, `Assembly`, `Description`, `Material`, `Mass_kg`, `Length`, `Volume_m3`,
`Unit`, `Support`, `Fab_*` (fabrication attributes), `Status` / `Reason` / `Basis` (UNRESOLVED flags) and `GlobalId` = the IFC GlobalId
of the same element (`IfcKeys`), so Rhino objects match the IFC and the companion database. Bars carry only Kind, Mark, Size, Unit, Coating,
GlobalId (mark data are in the bar schedule / database); fasteners carry Assembly and GlobalId, and for flagged ones `StatusKey` = a key of
the document user text section `SBM_Reasons` (the shared reason text). Several Rhino objects of one element (a part made of several
recorded solids) are grouped.

**Units and base point.** The file is in the project's display length unit (feet for US projects; US survey feet are written as Rhino
feet with the survey-foot scale and noted). Coordinates are relative to the IFC export base point (rounded centre of the first bridge's
deck), so the 3DM and the IFC share one local frame and Rhino works near its origin. The base point (grid E / N / Z in file units and in
meters) is in the document user text (`SBM\BasePoint_E` ...), the notes and the earth anchor point description.

**Performance** (sandbox, 2026-10-26; export only, after regeneration): Woodbury 2.6 s / 35 MB (7.5 k objects); Nebraska St 2.6 s /
32 MB (19 k); I-80 + Ramp G 55 s / 703 MB (487 k objects: 124 k extrusions, 240 k fastener instances, 121 k bar curves); PTC Delaware River
50 s / 768 MB, 3.3 GB peak working set (600 k objects, 510 k rivet instances of 377 blocks); `--no-detail` PTC 293 MB. The file is
written to the local temp folder and copied (openNURBS writes in small pieces: ~20x slower straight onto a network / mapped folder).
Read back: `sbm 3dm-info out.3dm` or `SBM.IO.Rhino3dm.Rhino3dmInfo.Read`.


Civil items (2026-10-30, test `Rhino3dmTests.CivilItems_RoadUtilities_*`): a whole-model export (no `--bridges` subset) also writes the roads (courses,
curbs, subgrade layers, side slope / imported finished surfaces as meshes), retaining walls (stems, footings, MSE elements incl. pile sleeves), grading
(graded surfaces as meshes, protection solids) and utility networks (structures + cones / frames / covers, pipes, hangers, duct banks) on top-level layers
`Roads::<road>::3D-ROAD-*`, `Walls::<wall>::3D-WALL-*`, `Grading::<feature>::3D-GRADING-*`, `Utilities::<network>::3D-UTIL-<SYSTEM>` (APWA colours); closed
solids go through the planar B-rep path; user text GlobalId = the IFC / database GlobalId (`Rhino3dmOptions.Civil` = `Rhino3dmCivil`).

## Two ways in: project files vs. the running app

| | Works on | Use for |
|---|---|---|
| `SbmDocument`, `sbm <command> file.sbm`, `sbm run script.csx file.sbm` | a project **file**, in its own headless engine | batch runs, studies, CI — the app doesn't see the changes until the file is reopened |
| `SbmApp`, `sbm app <command>`, `App` in scripts | the **running app** (live model on screen) | driving what you're looking at: edits are undoable (Ctrl+Z) and regenerate immediately |

The running app listens on `\\.\pipe\SteelBridgeModeler.<process id>` (local, current Windows user only — the same idea
as COM's GetActiveObject, without registration). One JSON line per request, one back:

```
→ {"cmd":"set","args":["Bridges[0].Girders.Count","6"]}
← {"ok":true,"result":"Bridges[0].Girders.Count = 6"}
```

Commands: `status`, `get <path>`, `set <path> <value>`, `describe [path]`, `regen`, `open <file> [--force]`,
`save [file]`, `import-pgsuper <file> [alignment] [station] [name]`, `run <script.csx> [args]`, `snapshot <png>`,
`view plan|3d|zoom`, `section <station> [skew]` / `section off`, `measure <E,N[,Z]> <E,N[,Z]> [<E,N[,Z]>] [alignment=NAME]`,
`station-offset <E,N[,Z]> [alignment=NAME]`, `messages [n]`, `undo`, `close [--save|--discard]`
(close refuses when there are unsaved changes unless told what to do).

```
sbm app list
sbm app status
sbm app set "Bridges[0].Girders.Prestressed.Type" Tx62
sbm app import-pgsuper "C:\models\Option1.pgs" "CL Route 1" 30+50
sbm app snapshot C:\temp\view.png
sbm app close --save
```
```csharp
using var app = SbmApp.Connect();          // or Connect(processId) when several copies run
app.Set("Bridges[0].Deck.Haunch", "3.5");  // live, undoable
Console.WriteLine(app.Get("Bridges[0].Girders.Spacings"));
Console.WriteLine(app.Measure("100,200,50", "160,240,52"));      // points in project units (ft for US, m for SI)
Console.WriteLine(app.StationOffset("100,200", alignment: "CL Route 1"));
```
In a script run by `sbm run`, `App` is the running app and `Doc` is the file.

Python (e.g. from the PGSuper MCP tools): `samples/scripts/sbm_app_client.py` (standard library only) —
`send("set", "Bridges[0].Deck.Haunch", 3.5)`, or from a shell `python sbm_app_client.py status`.

### PGSuper import

```
sbm import-pgsuper Bridge.sbm "Option1.pgs" "CL Route 1" 25+00
```
```csharp
var res = Doc.ImportPgSuper(@"C:\models\Option1.pgs", "CL Route 1", Sta("25+00"));
foreach (var n in res.Notes) Print(n);
var ps = Doc.Prestressed(res.Bridge.Name);   // beams, strands, pads, bearing seats
```
Prestressed inputs live at `Bridges[i].Girders.Kind` (`SteelPlate` / `TxDotPrestressed`) and
`Bridges[i].Girders.Prestressed.*` (`Type` Tx28…Tx70 / Custom, `StrandCount`, `StrandDiameter`, `DepressedStrands`,
`ConcreteStrength` (ksi), `EndGapAtBent`, `BearingToEnd`, `InteriorEnds`, `PadLength` (0 = IGEB table), …).

Precast post-tensioned segmental box bridges (`Bridges[i].Segmental`, `Girders.Count = 0`; example `samples/plans/build-bonner-unit5.csx`):
```csharp
var sm = Doc.Segmental("Bonner Unit 5");     // Segments (type, stations, depths, chord, mesh), Tendons, Bearings, BentRows (WP / soffit / DIM A / B), Checks, Diagnostics
Print(sm.ConcreteVolume / Math.Pow(Ft(1), 3) / 27);   // box concrete, CY
// OPTIONAL details (SegmentalDefinition.BlockTypes / Blocks / Openings / Parapet / Bars; samples/plans/details-bonner-unit5.csx):
Print($"{sm.Blocks.Count} blocks, {sm.Openings.Count} openings, net {sm.NetConcreteVolume / Math.Pow(Ft(1), 3) / 27:0} CY");
Print($"{sm.Bars.Count} bars placed, {sm.Marks.Sum(m => m.Count)} scheduled, {sm.CoverViolations.Count} cover violations");
// joint keys (SegmentalDefinition.Keys) are blocks of Kind ShearKey, recorded only (not in NetConcreteVolume); parapet joints from Parapet.JointRuns
Print($"{sm.Blocks.Count(b => b.Kind == SegmentalBlockKind.ShearKey)} key sets, {sm.KeyVolume / Math.Pow(Ft(1), 3) / 27:0.00} CY; {sm.ParapetJoints.Count} parapet joints");
```
Blocks are the box void clipped per station (anchor blocks, erection PT bar blocks), openings are void solids with the concrete they remove,
the parapet is a barrier of the bridge's concrete model, and segment bill bars go to `Concrete.Marks / Bars` (IFC IfcReinforcingBar).

## C# scripts

These globals are available:

| Global | What it is |
|---|---|
| `Doc` | The open `SbmDocument` |
| `Print(x)` | Writes output |
| `Ft(x)`, `In(x)`, `M(x)` | Convert to meters |
| `Sta("12+34.56")`, `Len("12'-6\"")` | Parse text with units |
| `Args` | Script arguments |

Common namespaces are imported for you.

```csharp
var b = Doc.Bridge();                       // first bridge (or Doc.Bridge("name"))
b.Supports[0].SkewDegrees = 25;
b.Bearings.Type = BearingType.Disc;
b.LateralBracing.Enabled = true;
b.LateralBracing.Bays = new() { 1, 4 };
var r = Doc.Regenerate();                   // incremental: unchanged bridges are reused
Print($"{r.CrossFrames} frames, {r.Bolts} bolts, {r.SteelMassKg / 907.18:F1} tons");
foreach (var s in Doc.Steel().BearingSeats) Print($"{s.Support} G{s.Girder + 1}: seat {s.SeatElevation / 0.3048:F3} ft");
Doc.ExportIfc(@"C:\Jobs\Br12\Br12.ifc");
```

Vendor-designed bearings (plans give the assumed bearing height and the anchor bolts):

```csharp
b.Bearings.Type = BearingType.Generic;
b.Bearings.GenericHeight = In(9);                 // bottom of flange to bottom of the masonry pad, at CL bearing
b.Bearings.MasonryPadThickness = In(0.125);
b.Bearings.AnchorEdgeDistance = In(3); b.Bearings.AnchorEdgeDistanceFixed = In(5);
var pier = b.Supports[2];
pier.Bearings = b.Bearings.Clone();               // per-support override (anchors differ by pier)
pier.Bearings.AnchorSpacingAcross = In(44);       // 2 x "X"
pier.Bearings.AnchorSpacingAlong = In(18);        // 2 x "Y"
pier.Bearings.AnchorBoltDiameter = In(2.5); pier.Bearings.AnchorBoltEmbedment = Ft(2.5);
pier.Fixity = BearingFixity.Fixed;                // FX ...
pier.GirderFixities = new() { BearingFixity.Expansion, BearingFixity.Auto, BearingFixity.Auto, BearingFixity.Expansion }; // ... MD exterior girders
```
See `samples/plans/bearings.csx` for a full plan bearing data table.

In the app, a script runs against the open project and the model regenerates afterward. A single **Undo** reverts
everything the script did.

## .NET library

Reference `SBM.Api.dll`, `SBM.Core.dll` and `SBM.IO.dll`.

```csharp
using SBM.Api;
var doc = SbmDocument.Open("Br12.sbm");             // or SbmDocument.New(), SbmDocument.Sample()
doc.Set("Bridges[0].Girders.Count", "6");
doc.Bridge().Girders.Spacings = Enumerable.Repeat(SBM.Core.Units.UnitSystem.Ft(8), 5).ToList();
var report = doc.Regenerate();
var framing = doc.Framing();                         // bearings, girder lines, cross frames, stiffeners …
var steel   = doc.Steel();                           // 3D parts (meshes), bolts, studs, bearing seats
var sub     = doc.Substructure();                    // bents/abutments: caps, pedestals, columns, foundations, rebar marks
var sheets  = doc.Sheets();                          // composed 11x17 sheets; doc.ExportPdf("set.pdf")
var views   = doc.Drawings();                        // 2D views (PlanDrawing per view); doc.ExportDrawings(folder or "x.zip") writes DXF
doc.ExportSheetsDxf("set.zip", includeModels: true); // sheets as paper-space DXFs (+ 2D models in models/) to a zip or a folder
doc.Set("Bridges[0].Supports[1].Substructure", "{\"TopShape\":\"Crowned\",\"ColumnCount\":2,\"ColumnSpacings\":[6.1]}");  // JSON = SI units
var chains  = doc.BearingElevations();              // step-by-step elevation chains, each checked against the model
doc.Project.Units.TryParseStation("22+00", out var sta);
var g3      = doc.GirderElevations(2, sta);          // girder G3 web CL at a station
doc.Save();                                          // or Save("other.sbm")
doc.ExportIfc("Br12.ifc");
var res = doc.ExportIfc("Br12-abv.ifc", IfcGeometryMode.ReferenceView);   // AbV geometry; optional 3rd arg chord tolerance (m)
```

## View engine: projected 2D views (hidden lines)

`SBM.Core.Drawing.Projection` projects the model SOLIDS (not generator 2D data) onto a view, with hidden-line removal:

```csharp
using SBM.Core.Drawing.Projection;
var view = new OrthographicView(origin, direction, up) { Near = -2, Far = 30 };   // or OrthographicView.Top(), DevelopedView.AlongGirder(g) / .Along(alignment, sta0, sta1)
var src  = new ProjectionSourceOptions { Layers = new[] { "3D-ARCH", "-3D-ARCH-PT" }, Transparent = new[] { "3D-DECK" }, Girder = 2, Bolts = false, Rebar = false };
var r = doc.ProjectView(view, new ProjectionOptions { Method = HlrMethod.Exact, Hidden = HiddenLines.Show }, src, bridgeFilter: "Arch");
// r.Drawing = PlanDrawing: layers "3D-..." (visible) + "3D-...-HIDDEN" (dashed); every Polyline3.Tag = element key
// r.Elements[key] = (Layer, Kind, Name, Bridge, Source object); r.Pick(point, radius); r.Stats (counts, timings)
```

- Edges: faces merged BY PLANE (no tessellation lines, T-junctions resolved), sharp edges (> 30 deg, `SharpAngleDegrees`), boundary
  edges, silhouettes of smooth (curved, tessellated) surfaces; exact `ExtrudedSolid` recipe edges when the recipes describe the whole mesh.
- Hidden lines: `Exact` = every edge split exactly against the occluding triangles (2D BVH, parallel); `DepthBuffer` = z-buffer, fast,
  approximate at pixel size. Coincident lines are drawn once (visible wins over hidden).
- Depth slab: `Near` cuts the solids (cut outline drawn, cut capped so what is behind stays hidden), `Far` drops what is beyond.
  A section view = `OrthographicView` with its origin on the cut plane and `Near = 0`; everything returned beyond the cut is the
  "beyond" projection (hook for `sbm section`).
- Developed views: x = station / distance along a path, y = elevation, depth = horizontal offset; edges are subdivided so curves unroll.
- Timings (2026-10-19, 12 cores): Northaven arch top view 1,161 elements / 0.7 M triangles 2.0-2.4 s exact; I-80 + Ramp G top view
  (all 7 bridges, 58 k elements, 26 M triangles) exact 74 s with the deck hiding the steel, 27 s framing plan (deck transparent, no bolt
  holes), depth buffer 22 s; one girder developed elevation (480 parts) 0.75 s.
- App: View > 2D projection > **Project View** (also right-click > "Project view to 2D") projects the camera direction (or the section
  cut) with the visible layers into a drawing window (click a line = its element; DXF / PDF export). **Projected Plan** toggles the
  projected top view of every bridge in the plan view (layers `PLAN-...`); bridges without framing linework (tied arch) always show it.
  Snapshot options `--projectedplan`, `--projectview` (writes `<out>_projected.png`).

## Plan-driven deck reinforcing

`Bridges[i].Deck.Reinforcement.Plan` (optional, default null = the generic layout) holds `Transverse` zones, `Longitudinal` zones and the plan `PlanBarList` of the unit; see `samples/plans/deck-rebar-i80.csx` and
`deck-rebar-ramp-g.csx` (C# scripts building it from the plan sheets) and `docs/plan-data/deck-rebar.md`. After `Regenerate`, `Doc.Concrete(bridge).Marks / Bars / Notes / Diagnostics` give the placed
marks (plan marks 7a1, 6b1, ...), the per-mark comparison with the plan bar list and the containment check ("Deck rebar containment check ..."). `samples/plans/report-deck-rebar.csx` prints the comparison,
`check-deck-rebar.csx` the containment check. Plain numbers in paths follow the field's display unit (`StartDistance`, `Length` in ft; `Spacing`, `LeftMargin`, `TransverseBeginClear` in inches).

## Stiffener fit conditions (mill to bear, tight fit, welded)

Fit conditions are recorded only from plan values. `Bridges[i].Stiffeners.EndFits` (optional, default null) is a list of `StiffenerEndFit { Kind, End, Condition, Source }`:
Kind = `Bearing` | `Jacking` (takes the Bearing entries when none is given) | `ConnectionPlate` | `Intermediate`, End = `Top` | `Bottom`, Condition = `MillToBear` |
`FinishToBear` | `TightFit` | `Welded`. A support can override its bearing / jacking stiffeners with `Supports[j].BearingStiffenerFits`; integral steel caps use
`IntegralSteelCap.StiffenerFits` and `IntegralSteelCap.Connection.DiaphragmPlateFits`. Entries given for one end replace nothing else; if none of them is `Welded`, that end
gets no flange weld. Ends with no plan value get no fit record; where AASHTO LRFD sets a requirement (bearing stiffener bottom 6.10.11.2, connection plates 6.6.1.3.1,
intermediate stiffener top 6.10.11.1) the part carries a "POTENTIAL NONCOMPLIANCE" flag instead (nothing is assumed). Results: `SteelPart.Fits` (pick properties, IFC Pset
`SBM_FitConditions`, database props `fit1..`). Example (C# script):

```csharp
Doc.Bridge().Stiffeners.EndFits = new()
{
    new(StiffenerFitKind.Bearing, StiffenerEnd.Bottom, FitCondition.MillToBear, "p.32 F55"),
    new(StiffenerFitKind.Bearing, StiffenerEnd.Bottom, FitCondition.Welded, "p.32 std. 1021W Detail A"),
};
```

Skewed stiffeners are bevelled automatically so the web edge and the welded ends sit flat on the web and flanges (no input).

## Standard cross frames and connection plate twist (`sbm crossframes`)

Design-build practice: every cross frame of a type is the SAME frame. All inputs are optional (null / false = output unchanged, nothing written to the .sbm).

`Bridges[i].CrossFrames.Standard` (or a style's own `CrossFrames.Styles[k].Standard`, which wins for frames of that style = one standard per frame type) is a
`StandardCrossFrameDefinition`:

| Input | Meaning |
|---|---|
| `Name`, `Source`, `Unresolved` | report name, plan / firm reference, open items (UNRESOLVED warnings) |
| `Scope` | `Intermediate` (default) / `Support` / `All`; `Styles` (style names, "" = no style) and `Supports` (support names) narrow it |
| `DefinedEnd` | `Taller` (default: the end whose top of web is higher along the frame up axis) / `Left` / `Right`; the other end's holes move |
| `Reference` | `TopOfWeb` (default) / `TopOfSteel` |
| `TopWorkPoint` | top WP below the reference at the defined end, along the frame up axis. NaN = UNRESOLVED, standard not applied |
| `Depth` | top WP to bottom WP. NaN = UNRESOLVED, standard not applied (the report still gives the deepest common depth) |
| `TopClearance`, `BottomClearance` | required clear distance from the top WP to the top flange underside / bottom WP to the bottom flange top. NaN = UNRESOLVED, 0 used |
| `SpacingRounding` | precision the WP spacing is detailed to, for grouping frames into families. NaN = exact (0.1 mm) |
| `Plane` | frame plane (null = `CrossFrames.FramePlane`, default `Midway`): `Midway` (frame orientation's up axis, plane midway between the two plates = through both plates when they are on the frame plane), `Averaged` (averaged up axis through the left top of web), `DefinedEnd` (on the defined end's plate), `Warped` (each end on its own plate, members rolled) |
| `TwistLimitDegrees`, `TwistGapLimit` | limits for "CHECK (cross frame twist)" warnings; NaN = report only |

A standard frame has level chords in its plane (top WPs of both ends at one height, one depth), so every frame of a FAMILY (same standard, style, type and WP
spacing) is the same shop frame. The slop goes into the hole positions on the connection plates: each hole is where the bolt axis meets the plate (no slotted
holes). Frames that do not fit the entered depth are reported and still built at it; nothing is moved, straightened or chosen by the program.

Plane orientation inputs (generic): `CrossFrames.IntermediatePlane` / `SupportPlane` (`NormalToFlanges` | `NormalToBottomFlange` | `Plumb`; null = normal to the
flanges / plumb as before) drive both the frame and its connection plates; `CrossFrames.BearingStiffenerPlane` (null = plumb) the bearing stiffeners.
### Connection plates on the frame plane (default since 2026-10-27)

A cross frame's location (automatic layout, `CrossFrames.Lines` distance along the reference girder, staggered frames) is defined at the TOP OF WEB of its
reference girder: the `Lines` girder; else the standard's `DefinedEnd` (Left / Right / Taller = the highest girder of the chain); else the left girder of the bay.
The frame plane passes through that point, contains the frame line's horizontal plan direction and the frame orientation's up axis (normal to the flanges:
mean of the two girders' flange normals). Every intermediate connection plate is placed where that plane meets its girder's top of web, so the gussets bear
flat on the plates; frames sharing a plate through the web (one line across several bays) are solved outward from the reference girder and keep one plate.
Stiffener bevels, fit records, welds and hole registration follow the moved plate. Support frames stay on the bearing stiffeners (plumb: already flat).
`CrossFrameResult.PlatesOnFramePlane / ReferenceRight / LeftShift / RightShift / PlaneDirection` record it; the framing diagnostics give the moves (max / median).

| Input | Meaning |
|---|---|
| `CrossFrames.PlatesAtEachTopOfWeb` | LEGACY switch (false = omitted): each plate at the plan line's crossing with its own girder's top of web, frames in the averaged plane = the pre-2026-10-27 output byte for byte |
| `CrossFrames.FramePlane` | null = `Midway` (members exact extrusions; the far gussets carry the remaining TRUE twist between the two plates as a reported gap); `Averaged`, `DefinedEnd` (reference end's plate for non-standard frames); `Warped` = each end's gussets in its own plate's plane (gap 0 at every bolt), members straight between the work points with the section rolled linearly from one plate's orientation to the other (twisted members, faceted solids when the roll exceeds 0.001 deg); plate / channel diaphragms are never warped |

`CrossFrames.ReportTwist = true` measures every frame (twist angle between the two plates about the frame line, plate offsets from the frame plane, far-gusset gap
per bolt for the three plane options) without changing geometry.

Results: `SteelModel.CrossFrameFit` / `SbmDocument.CrossFrameFit(bridge)` (`CrossFrameFitReport`: Rows, Families, Standards = depth solver summary with the governing
frame / end / girder / station and the smallest top WP that clears every frame), diagnostics, database props on the cross frame assembly (`std_family`, `std_depth`,
`headroom`, `shift_left`, `shift_right`, `twist_deg`, `twist_gap_max`, meters) and `hole_shift` on the connection plates, IFC Pset `SBM_StandardCrossFrame` (standard
frames only), sheets "Standard cross frames" (one prototype per family) + "Cross frame hole shifts" (paged table).

```
sbm crossframes model.sbm [--bridge NAME] [--csv out.csv] [--rows N] [--twist] [--legacy] [--frame-plane midway|averaged|defined|warped]
sbm crossframes model.sbm --top 6 --depth 64 [--plane midway|averaged|defined|warped] [--end taller|left|right] [--top-clear L] [--bottom-clear L] [--rounding 1/16"]
```

Read-only: `--twist`, `--legacy`, `--frame-plane` and the what-if options (`--top`, `--depth` ...) apply in memory only. The report also prints the plate moves
(`shift_L/R` in the csv, reference end) and, for warped frames, the member roll (deg) and the leg-tip deviation it implies. Script:

```csharp
var cf = Doc.Bridge().CrossFrames;
cf.Standard = new StandardCrossFrameDefinition { Name = "INT", TopWorkPoint = In(6), Depth = In(64), TopClearance = In(3), BottomClearance = In(3), Source = "firm std." };
Doc.Regenerate();
foreach (var r in Doc.CrossFrameFit()!.Rows) Print($"{r.Mark} {r.Family} headroom {r.Headroom / In(1):0.00} in, right shift {r.ShiftTopR / In(1):0.000} in, twist {r.TwistDegrees:0.000} deg");
```

## Custom barriers and surface treatments (form liner, thin brick)

`Bridges[i].Deck.Barrier.Type = BarrierType.Custom` takes the section point by point (`Profile`: `BarrierProfilePoint(R inward from the deck edge, H above the deck,
radius, chamfer, tag)`), optionally `PerpendicularToDeck`, `BeginExtension` / `EndExtension` (square ends beyond the deck ends, along the toe line), `Zones`
(`BarrierZone`: another section over a length centred where a support's CL crosses the deck edge, e.g. a bump-out, with an optional `DeckWidening`) and
`PlanLength` (checked). Example: `samples/plans/barrier-woodbury.csx`.

`Bridges[i].SurfaceTreatments` (list of `SurfaceTreatmentDefinition`) describes form liner texture / integral thin brick / rustication: kind, pattern, products,
relief depth, orientation, plan quantity (m2) and `Regions` (`SurfaceRegion`: host Barrier by section tag, Substructure solids of a support by element kind, or the
Diaphragm; facing direction, S range, lower / upper limit lines (`SurfaceLevel.Level(z)`, `Along((S, z), ...)`, `BelowDeck(d)`) and excluded bands). After
`Regenerate`, `Doc.Model(bridge).Substructure.SurfaceTreatments.Items` give each region's area and relief-layer mesh, `.Totals` the area per treatment, and
`.Diagnostics` the comparison with the plan quantity. IFC: one `IfcCovering` per region with Pset `SBM_SurfaceTreatment`; database: group "surface treatment" >
elements "thin brick" / "form liner". Example: `samples/plans/surface-treatments-woodbury.csx`.

## Exact curved geometry (NURBS)

Project setting `Geometry` (saved in the .sbm only when changed; app ribbon "Units & geometry" > "Exact curves"):

| Path | Default | Meaning |
|---|---|---|
| `Geometry.ExactCurvedGeometry` | `false` | Curved parts are built as exact NURBS solids and their meshes tessellated from them. Off = everything exactly as before. |
| `Geometry.FitTolerance` | `0` | Maximum deviation of the NURBS from the generator's exact geometry; 0 = 1/256" (US) / 0.1 mm (SI). Plain numbers in inches / mm. |
| `Geometry.ChordTolerance` | `0` | Chord tolerance of the derived meshes; 0 = today's sample density (same vertex layout, vertices on the NURBS). |

What carries NURBS: girder flange and web plates (incl. bolt holes as circular trims + half-cylinder walls), field splice
plates and fillers on curved / graded girders, the deck slab (bands between crown / gutter / break / kinked-end columns,
panels between grade and cross-slope kink stations), haunches and barriers. Straight parts are unchanged. Each such mesh has
`Mesh.Nurbs` (`NurbsSolid`: faces = `NurbsSurface` or planar polygon, `NurbsHole` trims) and `Mesh.ExactVolume()`;
`SteelPart.ExactVolume`, `SteelModel.Nurbs` / `ConcreteModel.Nurbs` (`NurbsReport`: counts, fallbacks with reasons, worst fit),
`Doc.ExactQuantities()` (exact vs mesh volume / area per element), `NurbsSection.Cut(solid, point, normal)` (exact plane
section: points on the NURBS surfaces). Quantities, drawings, checks, viewport and IFC keep using the meshes.

```csharp
Doc.Set("Geometry.ExactCurvedGeometry", "true");
Doc.Regenerate();
foreach (var r in Doc.ExactQuantities("Br12 Unit 2")) Print($"{r.Element} {r.Kind}: exact {r.ExactVolume:F4} m3, mesh {r.VolumeDiffPercent:+0.000;-0.000}%");
```

## QC measurements (`sbm measure`, `sbm section`)

Read-only tools for the independent checker (docs/QC-PLAN.md section 3). The model is opened and regenerated in memory and
**never saved**. Output is a text report, or JSON with `--json` (stdout) / `--out file.json`. Several queries can be given
in one call (`--part A --part B`), or one per line in `--batch file.txt`; they share one regeneration, so each query
after the regeneration takes milliseconds (an assembly with its adjoining girder plates ~0.3 s).

```
sbm measure model.sbm --part FS1-G1-SP1 [--ref longest-edge|horizontal|holes|girder-tangent|alignment|x,y,z] [--leg N] [--svg p.svg]
sbm measure model.sbm --assembly FS1-G1 [--ref ...] [--no-neighbours] [--svg a.svg]
sbm measure model.sbm --member G1-2 --at 12ft|301+50 [--width all|LEN] [--svg s.svg] [--dxf s.dxf] [--csv s.csv]
sbm measure model.sbm --distance G1-LAT1-L1 G1-1-TFL1
sbm measure model.sbm --point E,N[,Z] | --station 301+50 --offset 4ft
sbm measure model.sbm --bar 5d2[#n] | --cover 5d2 | --spacing A --along girder:G1|alignment|axis|x,y,z | --laps T --fc 4  [--unit "Pier 2"]
sbm section model.sbm --station 2476+50.56 [--offset O] [--normal-to alignment|girder:G1|member:MARK|plumb] [--skew DEG]
                      | --plane x,y,z,nx,ny,nz   [--width LEN] [--height LEN] [--bolts] [--no-bars] [--only text,..] [--dims]
                      [--svg s.svg] [--dxf s.dxf] [--csv s.csv] [--json] [--out s.json]
common: --bridge NAME (exact or substring), --tol 1/8" (hole row / column clustering)
```

**Frames come from the geometry, never from the world axes** (parts on grade, cross slope, skew, curvature, arch parts):
- *Plate normal and thickness:* the triangles are clustered into face planes; the pair of largest opposite parallel faces
  (thin compared to their size) is the plate (`--leg 2` = the next pair: the other angle leg, a WT stem). When the part's
  `ExtrudedSolid` recipes lie in that plane the outline and holes are the exact recipe polygon and circles (`source:
  recipe`, sweep vector -> edge bevel); otherwise the mesh is cut at the mid plane (`source: mesh section`; hole diameter =
  2 x apothem of the circumscribed hole polygon). Strips of long plates are merged by cancelling shared edges.
- *In-plane axes:* u = the reference projected into the plate plane: the longest straight edge (default for parts), the
  girder tangent incl. grade (`girder-tangent`, default for field splices / field sections; nearest girder to the part),
  the alignment tangent incl. profile grade, a level line in the plane (`horizontal`), the hole pattern (`holes`) or any
  vector. v is "up" in the plane (for level plates n points up and v = n x u); n = u x v. The JSON gives u, v, n and the
  world origin (lower-left of the outline on the mid plane), so every (u, v) can be mapped back to E/N/Z.
- *Hole patterns:* each hole group is read along its own pattern axes (dominant nearest-neighbour direction): a bolt
  column parallel to a sloped end edge is one column (`axisDeg` = its angle to u / v). Groups split at spacings clearly
  larger than the typical one (the two sides of a splice). Edge distances are measured from each hole center to the edge
  hit along the pattern axes, **perpendicular to that edge** (`perpendicular`; `alongAxis` = along the ray).
- *Assemblies:* plate components are grouped by parallel normals (1.5 deg) and stacks along the normal (gaps <= 4", so a flange between its outside and inside splice plates keeps them in one plane),
  one plane per group. Parts of other assemblies in the same plane near the assembly (girder webs / flanges at a splice,
  stiffeners a frame bolts to) are added as "adjoining" (cut, clipped). Per plane: layers with near / far face offsets
  (fillers, gaps, overlaps < 0), joint gaps between like adjoining plates of different assemblies, bolts (axis within 5 deg
  of the normal) with the plates they pass through and the bolts on each side of the joint, bolt rows / columns.
- *Sections:* the plane normal is the alignment tangent incl. grade (default), a girder tangent incl. grade, a member axis
  (girders: path tangent; parts: exact sweep or principal axis) or a given vector, optionally skewed about v. 2D frame:
  u = right looking ahead, v = world up made normal to the plane. Every solid is cut (`MeshSection`), closed loops listed
  per element with its GlobalId; bars crossing the plane are dots with mark, size, angle to the plane and clear cover to
  the concrete loop holding them; bars lying in the plane are polylines. `--dims` adds element extents (width / height),
  vertical / horizontal clear distances between neighbouring elements, and bar covers.
- *Rebar:* `--bar` = centerline, bends (sampled arcs merged: angle, centerline radius, pin = 2R - db vs AASHTO minimum;
  a sharp model corner is reported as such), straights, hooks (90/135/180 with tail in db), developed vs scheduled
  length. `--cover` = minimum clear cover per bar to each host face type (top / bottom / side by face normal; faces shared
  with another host solid are not free faces; mesh distances). `--spacing` = sorted positions along a girder path, the
  alignment (stations) or the principal axis, collapsed into "n spa @ s". `--laps` = parallel overlapping bars of the
  same size in the same host: lap length, clear distance, lap centers, closest lap stagger, Class B lap for `--fc`.
  A mark used in several places (deck and piers) picks the largest set and says so; use `--unit` / `--bridge`.

JSON lengths are `{in, text, mm}` (text = ft-in to 1/16"); 2D coordinates are in inches (US) / mm (SI), world points in
ft / m. API: `SbmDocument.MeasureModel()` (GlobalIds attached) and the classes in `SBM.Core.Checks.Measure`
(`PlateMeasure`, `AssemblyMeasure`, `ModelSection`, `SectionPlanes`, `MeasureQueries`, `MeasureSvg`).
Limits: meshes are faceted (curved faces to the chord tolerance); curved / bent plates are measured at the cut of their
largest flat faces (`flatSharePct` and a note say so); hole polygons from meshes are assumed circumscribed.

## Element centerlines (`sbm centerlines`)

```
sbm centerlines model.sbm [--bridge NAME] [--kind sub,part,psbeam,deck,...] [--category Column,Cap,Footing,...] [--match REGEX] [--welds]
                [--stations 24] [--csv cl.csv] [--json cl.json] [--dxf cl.dxf]
sbm export model.sbm --3dm m.3dm --centerlines        # the same lines as Rhino curves (layers <bridge>::CL-GEOMETRIC / CL-CENTROID)
```

Two 3D lines per element (columns, caps, footings, piles, girders' plates, beams, diaphragms, deck, barriers, misc items ...), end to end along
its axis: **GEOMETRIC** = centre of the section's extents, **CENTROID** = area centroid of the section (holes / voids subtracted). They coincide for
symmetric sections and differ for angles, tees, beams with a wide bottom flange, sloped / stepped caps ("max_offset" in the CSV).

| Element | Method |
|---|---|
| One straight extrusion along its long direction (piles, rolled shapes, plates, prismatic beams) | exact: profile centroid / extents centre swept along the extrusion (`extrusion`) |
| Anything else (caps drawn as elevation profiles, tapered / stepped members, lofts, curved members) | `sections`: N cuts normal to the long principal axis of the solid (snapped plumb / level within 2 deg), a second pass square to the path when the path is curved (leaves its chord > 25 mm / 0.5 %); partial end sections (skewed / tapered ends, < 90 % of the median area) are not used and the lines run straight on to where they leave the solid (or to its extreme plane when the line is outside the material) |

Output: CSV (bridge, kind, mark, assembly, unit, GlobalId, category, method, length, points `E,N,Z ...` in ft / m, max offset), JSON, DXF
(3D polylines, CL-GEOMETRIC / CL-CENTROID). API: `SbmDocument.Centerlines(bridge, filter, options)` -> `CenterlineRow` (element ref with the IFC GlobalId +
`SBM.Core.Solids.ElementCenterline`), or `SBM.Core.Solids.Centerlines.Of(mesh)` for any closed mesh. Limits: near-square plan elements (square
footings) have no single long axis - one horizontal axis is taken; combined meshes (all haunches of a bridge in one solid) give one line for the set.

## Level of detail (`--lod`, project setting `Lod`)

OPTIONAL project setting `Doc.Project.Lod` = `LodSettings` (null = the full model = LOD 400; omitted from the .sbm when null). Levels after
BIMForum, stopping at 400 (user decision 2026-10-28; 500 = as-built / asset management, not a modeling level):

| LOD | Built |
|---|---|
| 100 | one envelope block per steel assembly (long plan axis, plumb), carrying the assembly's steel weight |
| 200 | plates / members without connection parts (gussets, splice / filler / connection plates, connection angles, fin plates) |
| 300 | specific geometry without fasteners, welds, bolt holes, placed bars (bar marks / schedules kept), strands kept |
| 350 | 300 + bolt / rivet holes |
| 400 | everything (today's model) |

Groups with their own level (`Groups`): Steel, Connections, Fasteners, Welds, Reinforcing, Prestressing; `Overrides` = per bridge name
(`*` wildcard, last match wins, optional group). The level is part of the regeneration cache key; the reduction runs once per bridge after
generation (`SBM.Core/Project/LodApply.cs`) so viewport, IFC, Rhino, database and quantities all see the same model, plus one Info
diagnostic "Level of detail (...): removed / simplified ...". The truss generator skips its rivet passes below 400 (PTC regeneration
46.5 s -> 31.6 s at 300, 513,878 fasteners -> 0). IFC: Pset `SBM_LOD` on each IfcBridge (Level + one per group + Basis) only when set.
Concrete solids (deck, substructure) are not reduced yet; generator-level skipping for bolts / bars of the other generators is stage 2.

```
sbm export model.sbm --ifc out.ifc --lod 300                  # any command: in memory only, never saved
sbm run report.csx model.sbm --lod 300,Fasteners=400          # per group
Doc.Project.Lod = new LodSettings { Default = 300, Overrides = new() { new LodOverride { Bridge = "PTC Arch*", Level = 400 } } };
```
App: Home > Units & geometry > "LOD 400" button (model level + per-group levels; `*` in the label = groups / overrides set).


Stage 2 (2026-10-30, tests `LodTests.OutputCap_*`):
- Output caps: OPTIONAL `LodSettings.Outputs` (`LodOutput` Viewport | Ifc | Rhino | Database -> level, JSON omitted when null): each group's level on that output is
  the lower of its own level and the cap (`LodSettings.ForOutput`). `SbmDocument.ExportIfc / Export3dm / ExportDatabase` regenerate for their output (the document
  keeps its project-level models afterwards); the app's viewport regenerates at the Viewport cap, its IFC / Rhino exports regenerate in a separate cache when
  their cap differs from the viewport's. App: Home > Units & geometry > LOD has one combo per output. Keep the IFC and Database caps equal when the database
  links elements to sheets (a removed element has no row).
- Per-element property set: when a LOD is set, every steel part, fastener, placed bar and strand carries `SBM_LOD` {Level, Group} (one set per bridge and group,
  one relationship to all its elements), besides the bridge-level `SBM_LOD`. The levels are the ones the export was generated at (`IfcExportOptions.Lod`).
- Fix: shear studs are fasteners and are now removed below 400 like the bolts (they were kept).
- Kept as is: concrete solids are not reduced at 100 / 200 (each is already one solid per element, and their quantities come from the solid volume -
  an envelope would misstate the CY); the I-girder / rebar generators still generate everything and the reduction runs after generation (the bar
  schedules are built from the placed bars, and creating the bolt instances is a small part of the steel generation time; only the truss generator skips its
  rivet passes).

## Roads (`Doc.Project.Roads`, `sbm roads`)

OPTIONAL roadway corridors (stage 1, 2026-10-28; design note `docs/plan-data/roads-walls-utilities-lod-design.md` section 2; generic test inputs
until a plan set with roadway sheets is supplied). `RoadDefinition`: alignment + station range, `Left` / `Right` sides = `StartOffset` + components
outward (`RoadComponent`: Kind Lane / Shoulder / Median / Sidewalk / CurbAndGutter / Ditch, Width, OPTIONAL own Slope (rise / run, + rising outward;
null = the alignment's cross-slope table), `Courses` top down (name, thickness, material), OPTIONAL closed `Section` swept along the road (curb)),
OPTIONAL `SideSlope` (cut / fill H:1V ratios + MaxWidth, all required; daylight to the loaded terrain; no terrain = UNRESOLVED, not built),
`SampleSpacing` (sections every <= 2 m + the profile / cross-slope kink stations). `RoadGenerator.Generate` -> `RoadModel` (course solids, top area
per component, side slope ribbons + daylight lines, diagnostics). IFC: IfcRoad > IfcRoadPart (TRAFFICLANE / SHOULDER / SIDEWALK / CENTRALRESERVE /
ROADSIDE) > IfcCourse (top PAVEMENT, others USERDEFINED = course name) / IfcKerb, materials (validated 0 issues; GlobalId keys `IfcKeys.Road*`).

Stage 2 (2026-10-29):
- Viewport layers `3D-ROAD-SURFACE` (first course of each component), `3D-ROAD-COURSES`, `3D-ROAD-CURBS`, `3D-ROAD-SIDE-SLOPES` (see-through); plan
  layers `SBM-ROAD-EDGE` / `SBM-ROAD-DAYLIGHT`; picks show road / component / course / material / volume and open the database row.
- Database: road (level unit, IFCROAD) > component (part, IFCROADPART) > course / curb (element, IFCCOURSE / IFCKERB), same GlobalIds as the IFC; road
  props = volume, top area, earthwork.
- Earthwork: with terrain and side slopes on both sides, cut / fill between the design section (subgrade under the components + side slopes) and the
  ground, average end areas along the alignment (no curvature correction); `RoadModel.CutVolume / FillVolume / EarthworkSections`; sections that do not
  daylight on both sides are left out (reported).
- Clearances (OPTIONAL `RoadDefinition.Clearances` = `RoadClearanceDefinition`: Bridge name or prefix*, MinVertical, MinHorizontal, Source; NaN = not
  given -> UNRESOLVED, the measured minimum is still reported): vertical = lowest steel / deck / beam / substructure triangle straight above any point of
  the road's top edge lines; horizontal = plan distance from the outermost edge lines to substructure solids reaching above the road top. Below the
  required value = `CONFLICT (plan vs model)` warning; `RoadModel.Clearances`, `sbm roads` rows.
- Bridge ties (OPTIONAL `RoadDefinition.BeginAtBridge` / `EndAtBridge` = `RoadBridgeTie` {Bridge, End Begin | End}): the road's end section follows
  that bridge end's approach slab far end, or the deck end line without a slab, offset by offset (skewed / kinked ends); the road must be on the
  bridge's alignment (else reported, station used).
- Approach slabs (OPTIONAL `SupportDefinition.ApproachSlab` on the first / last support = `ApproachSlabDefinition`: Length, Thickness, Gap,
  `LengthMeasure` AlongAlignment | NormalToDeckEnd, EdgeInsetLeft / Right from the deck edges, `Owner` Bridge | Road (user answer 7a.3: per bridge end),
  OPTIONAL `Sleeper` (Width, Depth below the slab, BeyondEnd), Material, Source, Unresolved; missing length / thickness = not built, missing Owner /
  LengthMeasure = built + UNRESOLVED (kept with the bridge / along the alignment)): top = the deck's finished grade continued, rows every <= 1.5 m
  (`ConcreteModel.ApproachSlabs`). IFC `IfcSlab .APPROACH_SLAB.` + sleeper `IfcSlab` 'SLEEPER_SLAB' in the bridge deck part, or - Owner Road - in the
  `IfcRoad` on the same alignment whose station range overlaps it; database rows likewise; viewport / Rhino layer `3D-APPROACH-SLABS`; material rows.

Subgrade layers (2026-10-30; tests `RoadSubgradeTests`):
- OPTIONAL `RoadSide.Subgrade` = list of `RoadSubgradeLayer`, TOP DOWN (JSON omitted when null): Name, `Kind` Subbase (placed material) | TreatedSubgrade
  (lime / cement / fly ash, in place) | PreparedSubgrade (scarified / compacted in place) | Other, Thickness (vertical), Material, `Under` (adjacent component
  names of the side, inner to outer; empty = every component), `ExtendBeyond` (past the last component's outer edge, at the slope of its bottom), `EdgeRatio`
  (outer face H:1V, 0 = vertical), Source, Unresolved. Nothing is assumed: no thickness, an unknown or non-adjacent component, negative extension = reported, not built.
- Geometry: the top of the first layer = TOP OF SUBGRADE = the bottom of the pavement courses (curb sections: their lowest point) of the components it lies
  under, stepped where the course depths differ; each layer is a constant-thickness band below it, lowered by the layers above. Where the step is >= the
  layer thickness the band would pinch, so the layer is built as separate closed pieces (`RoadPart.Pieces`, Info line).
- Earthwork: measured to the bottom of the PLACED (Subbase) layers - lower envelope of the course bottoms / side slopes and the subbase bottoms; treated /
  prepared / other layers are in-place soil and do not move the earthwork line.
- Levels: `RoadModel.Levels` (`RoadLevel` per side: "finished grade", "top of subgrade", "bottom of <layer>"; per section station the line as signed offset /
  elevation); `sbm roads` prints each level at the first / middle / last section, inner and outer end, with the depth below the road's own finished surface.
- Outputs: `RoadPart.Subgrade` = kind, `Component` = "Subgrade"; viewport layer `3D-ROAD-SUBGRADE`; IFC per side IfcRoadPart USERDEFINED 'SUBGRADE' (.VERTICAL.)
  > IfcCourse USERDEFINED (subbase) / IfcEarthworksFill .SUBGRADEBED. (treated / prepared) / IfcEarthworksFill USERDEFINED (other), one closed face set per
  piece (key `IfcKeys.RoadSubgrade` + layer name; 0 schema / EXPRESS issues); database part "road subgrade" > elements "subgrade layer" (prop subgrade_kind).
  Note: `tools/validate_ifc.py` counts a split layer as "open" because its pieces share an edge line; each piece is closed.

### Civil 3D corridor import (feature lines, 2026-10-30; tests `CorridorImportTests`)
LandXML `PlanFeatures/PlanFeature` (Civil 3D corridor feature lines: Line / IrregularLine / Chain PntList3D / Curve sampled every <= 5 deg / Spiral as a chord,
reported; file linear unit -> m; `SBM.IO.LandXml.LandXmlFeatureLines`) -> OPTIONAL `RoadDefinition.FeatureLines` (`RoadFeatureLine`: Name, 3D `Points`, OPTIONAL
`Kind` of the strip outward of it, else from the name: shoulder / curb / sidewalk / median / ditch / lane). With 2 or more lines the road is built BETWEEN them
(strips between neighbouring lines by mean offset; sections at <= SampleSpacing + every vertex station; top = the lines, linear between vertices; crossing lines
reported, that strip not built there) instead of the Left / Right template; `FeatureLineCourses` (top down) hang below every strip with vertical sides. No
courses = top surface only (`RoadModel.SideSlopes["finished surface"]`, used by the finished ground; UNRESOLVED flag: the pavement structure is not in a
LandXML corridor export). Earthwork is not computed for an imported corridor. `RoadGenerator.ToTemplate(road, alignment)` converts to the parametric
template when every line keeps its offset within 1 cm: widths = offset differences, a strip with a constant cross slope (0.1 %) gets `Slope`, else the
alignment's cross-slope table is used and the largest elevation difference is reported; a varying offset keeps the feature lines (reason given). The
IFC / database / viewport / clearance / bridge-tie paths are the normal road ones. `Doc.ImportLandXml` reports feature lines found; import with:

```
sbm import-corridor model.sbm corridor.xml --alignment "CL Route 1" --name "Approach A" [--lines EOP*,CL,SHLD-R] [--courses "Surface:4in:HMA,Base:10in:Aggregate"] [--template]
```

Not yet: LandXML TIN surfaces as road elements (they load as terrain / reference models), approach slab reinforcing, IfcEarthworksCut / Fill solids,
curvature-corrected earthwork, subgrade layers in the Rhino export, subgrade under side slopes / ditches that daylight into the fill slope (the layer ends
at ExtendBeyond), subgrade layers under an imported corridor.

```
sbm roads model.sbm         # course volumes (CY / m3), top areas (SY / m2), earthwork, clearances, diagnostics
```

## Retaining walls (`Doc.Project.Walls`, `sbm walls`)

OPTIONAL retaining walls (stage 1 = cantilever cast-in-place, 2026-10-29; design note section 3.1; generic test inputs). `RetainingWallDefinition`:
alignment + `Offset` of the CONTROL LINE = the front face of the stem at the top of wall, station range, `FaceSide` Left | Right (side the exposed face
looks to; the soil is on the other side), `TopOfWall` (station / elevation rows, linear), top of footing as `FootingTopSteps` (each row = level from
its station to the next: vertical steps) or `FootingTopProfile` (linear), `StemTopThickness`, `FrontBatter` / `BackBatter` (horizontal per unit
height), `FootingThickness`, `Toe` / `Heel` (from the stem faces at the footing top), OPTIONAL `Key` (Width, Depth, FromToe), `JointStations`
(stem + footing are separate panels between them), Material, Source, Unresolved. Every dimension is required (NaN = not given -> the wall is not
built, UNRESOLVED warning naming the missing values); nothing is defaulted. `RetainingWallGenerator` -> `RetainingWallModel` (panels with stem /
footing solids - a footing step inside a panel splits its solids -, volumes, exposed face area above the footing top, plan lines, diagnostics).
Viewport layers `3D-WALL-STEMS` / `3D-WALL-FOOTINGS`, plan layer `SBM-WALL`; IFC `IfcElementAssembly` 'RETAINING_WALL' (contained in the site) >
per panel `IfcWall .RETAININGWALL.` (stem) + `IfcFooting .STRIP_FOOTING.` (footing + key), keys `IfcKeys.Wall*` (validated 0 issues; a panel split
by a step is a two-item body, each item closed); database wall > stem / footing rows with the same GlobalIds. Not yet: coping / barrier on top of a
cantilever wall, weep holes / drains, piles under the footing, reinforcing (planned through `PlanBarSet`), Rhino export.

MSE walls (2026-10-29; design note section 3.2, user answer 7a.4 = contract-plans envelope by default): set `RetainingWallDefinition.Mse` =
`MseWallDefinition` (omitted from the .sbm when null; the cantilever stem / footing inputs are then not read and `FootingTopSteps` / `FootingTopProfile`
give the TOP OF THE LEVELING PAD). Control line = front face of the facing at the top of wall. Inputs: `FacingThickness`, `FacingBatter` (0 = plumb),
OPTIONAL panel grid `PanelWidth` x `PanelHeight` (from the begin station and the pad top, top course cut to the top of wall; joint widths and the
supplier's stagger pattern are not modeled - Info; without it one facing envelope per joint panel), `LevelingPadWidth` / `Thickness` /
`FrontProjection` (front edge ahead of the facing front face), OPTIONAL `Coping` (closed (U, Z) polygon, Z above the top of wall), reinforcement
lengths `LengthsByStation` (station ranges, win) / `LengthsByHeight` (design height = top of wall - pad top, first row covering it), `LengthFrom`
BackOfFacing | FrontOfFacing (null = UNRESOLVED, built from the back of the facing), OPTIONAL `Reinforcement` = supplier drawings only (Kind Strip |
Grid, Levels above the pad top, strip Spacing / FirstFromBegin / Width, Thickness; built at LOD 400 of the Reinforcing group with the wall name as the
bridge name, else the zone envelope only). Every missing value leaves that component unbuilt with an UNRESOLVED flag (page + reason); reinforcement
lengths are never derived from a height ratio. Output `RetainingWallModel.Elements` (`WallElement`: Role Facing / FacingPanel / LevelingPad / Coping /
ReinforcedZone / Reinforcement, stations, joint panel, solids, length, level); the zone splits where the length changes (height-table changes are
bisected) and at pad steps. Viewport layers `3D-WALL-FACING` / `-LEVELING-PAD` / `-COPING` / `-REINFORCED-ZONE` (see-through) / `-SOIL-REINFORCEMENT`;
IFC assembly 'RETAINING_WALL' > per joint panel `IfcWall .RETAININGWALL.` (facing; precast panels = `IfcBuildingElementPart .PRECASTPANEL.` aggregated
into it), `IfcFooting` 'LEVELING_PAD', `IfcCovering .COPING.`, `IfcReinforcedSoil` 'MSE_REINFORCED_ZONE', `IfcMember` 'SOIL_REINFORCEMENT' (key
`IfcKeys.WallElement`; test model 0 schema / EXPRESS issues, every product closed); database rows with the same GlobalIds.

Abutment on an MSE wall (2026-10-30, tests `MseAbutmentTests`): OPTIONAL `MseWallDefinition.Abutments` (`MseAbutmentInterface`: `Bridge`, `Support`,
`MinFacingClearance`, pile sleeve `SleeveDiameter` / `SleeveWall` / `SleeveTop` (NaN = the foundation bottom, reported) / `SleeveBottom` (NaN = UNRESOLVED, no
sleeves) / `SleeveMaterial`, Source). After the bridges are generated (`SbmDocument.Walls()` regenerates them; the app runs it with the scene)
`MseAbutmentCheck.Run` measures the clear distance from the facing / coping to the abutment concrete (CONFLICT below the required value), reports a
foundation that bears on the reinforced zone, finds every pile whose axis passes through the zone, builds a sleeve ring along each such pile axis (wall
element role `PileSleeve`: IFC IfcBuildingElementPart 'PILE_SLEEVE', DB "pile sleeve", layer 3D-WALL-PILE-SLEEVE), checks a CIP pipe pile against the
sleeve size and, at LOD 400, the soil reinforcements a sleeve obstructs (CONFLICT, the supplier skews / cuts them). Results `RetainingWallModel.Interfaces`.
Not yet: a bearing seat on the wall itself, grid reinforcement connections, wire / block facings.

```
sbm walls model.sbm         # cantilever: stem / footing volumes per panel; MSE: elements and volumes per role, reinforcement lengths
```

## Grading (`Doc.Project.Gradings`, `sbm grading`)

OPTIONAL grading features (2026-10-29; design note section 3b; generic test inputs). `GradingDefinition`: `Points` (`GradingPoint.At(station, offset,
elevation)` on `AlignmentName`, or `GradingPoint.Xy(x, y, elevation)` without one), `Closed` (pad: the interior is triangulated flat between the points),
`Side` Left | Right (of the point order) | Outside (closed lines), OPTIONAL `Slope` (`RoadSideSlope`: CutRatio / FillRatio = horizontal per vertical,
MaxWidth = search limit), `SampleSpacing` (ray spacing, default 1 m; convex corners get a fan every 7.5 deg), `Protection` (`GradingProtectionDefinition`:
Name, Kind, Region Slope | Top, Thickness normal to the surface - its top is the finished grade -, Material, Source), Source, Unresolved. A missing
elevation / position, side, ratio, region or thickness is an UNRESOLVED flag (page + reason) and that part is not built; no terrain = the slope is not built.
`GradingGenerator` -> `GradingModel` (feature line, daylight lines, `SlopeSurface` / `TopSurface` (open, upward), protection solids, `CutVolume` /
`FillVolume` against the existing ground by plan-grid sampling, area). `Doc.Gradings()`, `Doc.FinishedGround()` (`FinishedGround.Query(xy)` = elevation +
layer: road surfaces, then grading, then "existing ground"; `Doc.GroundAt` now loads the terrain itself). Viewport layers `3D-GRADING-SURFACE` /
`3D-GRADING-PROTECTION`, plan layers `SBM-GRADING` / `SBM-GRADING-DAYLIGHT`; IFC `IfcElementAssembly` 'GRADING' (site) > `IfcGeographicElement .TERRAIN.`
'FINISHED_GRADE' (open face sets, cut / fill in the description) + `IfcCourse .PROTECTION.` (keys `IfcKeys.Grading*`; test model 0 issues); database grading >
graded surface / slope protection rows (cut / fill / area in the props). Not yet: a merged finished-ground TIN export, grading between two feature lines, ditches.

```
sbm grading model.sbm [--at x,y]   # surface area, cut / fill, protection; --at = finished ground at a plan point (project meters) and its source layer
```


Finished-ground TIN export (2026-10-30, test `CorridorImportTests.FinishedGround_Tin_*`): `Doc.ExportFinishedGround(path, spacing, margin[, extents])` /
`sbm finished-ground model.sbm out.xml [--spacing 1m] [--margin 15m]` writes the merged finished ground (`FinishedGround`: road tops, then grading, then the
existing ground) as ONE LandXML TIN surface: the ground sampled on a square grid over the road / grading / wall / utility extents grown by the margin, two
triangles per cell whose four corners are on a surface (no ground there = the cell is left out). Linear between grid points, so features narrower than the
spacing are smoothed. The message lists the grid points per source.

## Storm drainage / utility networks (`Doc.Project.Utilities`, `sbm utilities`)

OPTIONAL utility networks, storm drainage first (2026-10-30; design note section 4; generic test inputs). `UtilityNetworkDefinition`: Name, `System` Storm |
Sanitary | Water | Gas | Electrical | Telecom | Lighting | Other, Owner, Source, OPTIONAL `MinCover` (required cover over the crowns from the plans / spec;
NaN = cover is reported only), `Unresolved`, `Structures`, `Pipes`.
- `UtilityStructureDefinition`: Name, `Kind` Manhole | Inlet | CatchBasin | JunctionBox | Headwall | Cleanout | Outfall | Other, Description, location
  X / Y (project m) or `AlignmentName` + Station + Offset, `RimElevation` (NaN = the FINISHED GROUND at the point: roads, then grading, then the existing
  ground; reported with its source), `SumpElevation` (inside floor), `Shape` Round (`InnerDiameter`) | Box (`InnerLength` x `InnerWidth`, `RotationDegrees`
  from +X; null = along its outlet pipe, flagged UNRESOLVED), `WallThickness`, `FloorThickness`, Material, Source, Unresolved. A missing size / sump / wall /
  floor = UNRESOLVED and no solid (its pipes still end at the inside face when the inside size is known, else at the centre).
- `UtilityPipeDefinition`: Name, `From` / `To` structure names (= flow direction) or free ends `StartX/Y`, `EndX/Y`, `InvertStart` / `InvertEnd` (inside
  bottom at the pipe ends), `Shape` Circular (`InnerDiameter`) | Elliptical | Box (`InnerRise` x `InnerSpan`), `WallThickness`, Material, Description,
  Source, Unresolved. A missing invert / size / wall = UNRESOLVED, pipe not built.
- `UtilityGenerator` -> `UtilityModel` (`UtilityStructure`: plan, rim + source, ONE closed shell = outside wall from the floor bottom, inside wall from the
  sump, rim annulus, floor; `UtilityPipe`: straight between the INSIDE wall faces, section normal to the axis with its lowest inside point on the invert,
  32-sided circle / ellipse or box ring, `Length` (plan), `Slope`, `MinCover`). The structure walls are not cut for the pipes (they overlap the wall).
- Checks (reported, never applied): adverse / flat slope, outlet invert above an inlet invert at a structure, invert below the sump, pipe wider than the
  structure inside, pipe top above the rim, rim vs the finished ground (> 1/2 in), cover over the outside crown every <= 1 m (`CONFLICT (cover)` below
  `MinCover`, CHECK when the crown is above the ground; no ground = not checked).
- LandXML import: `Doc.ImportLandXml(file)` / `sbm import-landxml` now also reads `PipeNetworks` (`SBM.IO.LandXml.LandXmlPipeNetworks`): `Struct` (Center
  N E, elevRim, elevSump, CircStruct diameter / RectStruct length x width, thickness, floorThickness, InletStruct -> Inlet, OutletStruct -> Outfall, else
  the kind from the catalog description: "cylindrical" / "manhole" -> Manhole ...), `Pipe` (refStart / refEnd, CircPipe / ElliPipe / RectPipe sizes,
  thickness, material; inverts from the structures' `Invert` rows: flowDir out = start, in = end; free ends from `Center` points). Lengths in the file's
  linear unit, sizes in its `diameterUnit` (missing = read in the linear unit and flagged). pipeNetType storm / sanitary / water / gas, else Other. A network
  with the name of an existing one REPLACES it. RectStruct has no rotation in LandXML 1.2 (flagged by the generator).
- Outputs: viewport layer per system `3D-UTIL-STORM` ... (APWA colours; picks show size, rim / sump, inverts, slope, cover and the GlobalId), plan layer
  `SBM-UTIL-<SYSTEM>` (pipe lines, structure outlines); IFC `IfcDistributionSystem` (.STORMWATER. / .SEWAGE. / .WATERSUPPLY. / .GAS. / .ELECTRICAL. /
  .COMMUNICATION. / .LIGHTING.) grouping (IfcRelAssignsToGroup) `IfcDistributionChamberElement` (MANHOLE, INSPECTIONCHAMBER = junction box, INSPECTIONPIT =
  cleanout, USERDEFINED 'INLET' / 'CATCH_BASIN' / 'HEADWALL' / 'OUTFALL') and `IfcPipeSegment` (.RIGIDSEGMENT., .CULVERT. between headwalls / outfalls),
  contained in the site, IfcRelServicesBuildings to the site, IfcDistributionPort per pipe end (inlet SINK, outlet SOURCE) + the structure's matching port,
  IfcRelConnectsPorts (keys `IfcKeys.Utility*`; test network 0 schema / EXPRESS issues, every solid closed); database network (IFCDISTRIBUTIONSYSTEM) >
  utility structure / utility pipe rows (rim, sump, inverts, slope, cover in the props).
- Stage 2 (2026-10-30, tests `UtilityStage2Tests`; all OPTIONAL, omitted from the .sbm when unset):
  - Structure tops: `UtilityStructureDefinition.Cone` (`Height`, `TopInnerDiameter`, `Eccentric` + `EccentricDegrees`; null direction = toward the outlet pipe,
    flagged) and `Frame` (`Height`, outside / opening diameter or length x width, `CoverThickness`, `Grate`, `Designation`, Material). The walls stop at the
    cone / frame bottom; the frame top = the rim. Missing values = UNRESOLVED and that part is not built. Grate bars are not modeled.
  - Wall openings: every pipe / duct bank end at a structure gets its OUTSIDE section swept through the wall (`UtilityStructure.Openings`); IFC
    IfcOpeningElement + IfcRelVoidsElement; `UtilityStructure.Volume` deducts area x wall length (the viewport keeps the wall solid, the pipe fills it).
  - Other utilities: kinds Valve / Hydrant (in-line envelopes of the given OUTSIDE size, bottom = sump, top = rim; IFC IfcValve / IfcFireSuppressionTerminal
    FIREHYDRANT), Handhole, Vault (water: VALVECHAMBER). Electrical / telecom / lighting runs are IfcCableCarrierSegment CONDUITSEGMENT (ports CABLECARRIER).
    `UtilityPipeDefinition.Cover` = plan depth for runs given by cover (invert = finished ground - cover - inside rise - wall, only where an invert is not
    given). Slope and invert-drop checks run for storm / sanitary only.
  - Duct banks: `UtilityNetworkDefinition.DuctBanks` (`UtilityDuctBankDefinition`: From / To or free ends, `BottomStart/End`, `Width` x `Height`, `Rows` x
    `Columns`, `ConduitDiameter`, `ConduitWall`, `Horizontal/VerticalSpacing`, `FirstConduitFromLeft/Bottom` (NaN = centred, flagged)). Encasement = exact
    extrusion with the conduit holes; conduits = rings filling the holes. IFC IfcElementAssembly 'DUCT_BANK' > IfcBuildingElementPart 'ENCASEMENT' +
    IfcCableCarrierSegment CONDUITSEGMENT.
  - Bridge-hung runs: `UtilityPipeDefinition.Hangers` (`UtilityHangerDefinition`: `Bridge`, `Spacing`, `FirstFromStart` (NaN = equal spaces, end hangers half
    a space in, flagged), `RodDiameter` (NaN = positions only), Type, Material, Source; user answer 7a.5). A rod runs from the outside crown straight up to the
    lowest face of that bridge's superstructure. IFC IfcDiscreteAccessory 'PIPE_HANGER'. Expansion fittings at joints are not modeled.
  - Clash / clearance (`UtilityClashCheck`, run by `UtilityGenerator.Finish` in `SbmDocument.Utilities()` and the app; reported, nothing moved): the solid
    clash engine (1/16 in contact tolerance) between every structure / pipe / duct bank / hanger and the bridges (steel, deck, barriers, beams, substructure),
    the walls (MSE reinforced zone too), the road pavement courses (pipes only) and the other networks. Clearances only where the plans give a value:
    `MinClearance` (to structures / walls / roads) and `MinSeparation` (to other networks, the larger value governs) -> "CONFLICT (clearance)". A hung pipe is
    not clearance-checked against its own bridge. `UtilityModel.Clashes`.
  - Profiles: `Drawing/Views/UtilityProfileViews` (`Runs` = flow-order runs, each pipe once; `Profile` = ground, structures, pipes inside / outside, rim /
    invert / sump labels, size / slope / length, elevation grid, vertical exaggeration); `sbm utilities model.sbm --profile <folder> [--ve 10]` writes one DXF per run.
- Not yet: utility profile sheets in the plan set, structure tables from plan CSVs, grate bars, expansion fittings on hung runs, casing pipes,
  the agent review's starting-value scan.

```
sbm import-landxml model.sbm storm.xml   # alignments and pipe networks
sbm utilities model.sbm [--profile out] [--ve 10] [--no-clash]   # structures, pipes, duct banks, hangers, LF / EA, checks, clashes, DXF profiles
```

## Performance notes

- **Incremental regeneration:** each bridge is keyed by a hash of its definition, alignment and units. Unchanged
  bridges are reused, so a no-change regeneration of the sample takes about 18 ms.
- **Parallel work:** bridges, girders, cross frames, splices, stiffeners and bearings all generate in parallel.
- **Fast plate meshing:** plates with holes are cut into cells, so triangulation stays linear in the number of holes.
- **Instanced fasteners:** bolts, studs and anchor bolts are drawn with GPU instancing, one mesh per size.
- **Reference models:** these load in the background, are cached by file timestamp, and are never rebuilt when the
  bridge regenerates.
- **Terrain queries:** terrain uses a uniform-grid spatial index. A 335,000-triangle TIN indexes in about 30 ms and
  answers an elevation query in about 0.3 µs.

## Model pipeline (`sbm pipeline`, default after `sbm run`) - REQUIRED final step of every model

```
sbm pipeline <model.sbm> [--status] [--no-regen] [--no-db] [--no-index] [--no-terrain] [--offline] [--reindex] [--force-db]
             [--pdf <plan set.pdf>]... [--plans <folder>[,<folder>]] [--terrain-dir <folder>] [--margin LEN]
sbm build-all <model.sbm> ...        (same)
sbm run <build.csx> <model.sbm> [--no-pipeline] [--no-db|--no-terrain|--no-index|--offline] [-- args]
```

Runs, in order, and prints one line per step (`DONE`, `UP TO DATE`, `SKIPPED` + why, `UNRESOLVED` + `next:` = what is needed, `FAILED`):

1. regenerate;
2. georeference check (`Doc.Project.Georeference`);
3. terrain: kept when a visible terrain reference file exists; else, for a georeferenced project in a CRS of `tools/terrain/crs.py`, USGS 3DEP (1 m DEM,
   else 1/3 arc-second) for the bridges' plan extents + 300 m (`--margin`), resampled into project metres (`tools/terrain/auto_terrain.py`), attached as
   "3DEP DEM (auto, sbm pipeline)", model saved and regenerated. Tiles cached in the terrain folder (`--terrain-dir`, `%SBM_TERRAIN%`,
   `C:\SBMData\terrain` when writable, `<repo>\dist\terrain`, else `<model folder>\terrain`). No georeference = UNRESOLVED with the next step;
4. plan sheet index: plan sets without a curated `"sheets"` index get `sheet-index-<set>.auto.json` from `tools/plan_index_auto.py` (text layer, or OCR
   when Tesseract is installed; scanned pages without OCR are reported). No rules file for the model + plan PDFs (`--pdf`, or `Doc.Project.PlanPdfs`) =
   a skeleton `sheet-links-<model>.json` is written once (never rewritten). A curated index is authoritative (`--reindex`: only pages it lacks are added);
5. companion database (`CompanionDatabase.Build`) unless it is newer than the model and its link inputs (`--force-db`).
6. agent review (`sbm review`, below): open flags keep the pipeline open.

`--status` reports without changing anything; `sbm regen` prints "Model pipeline: N item(s) open" when something is missing. `sbm run` runs the pipeline
only when the script SAVED the model; `--no-pipeline` / `SBM_PIPELINE=0` skip it, `SBM_OFFLINE=1` = no downloads, `SBM_PYTHON` = the Python to use.
Library: `ModelPipeline.Run(doc, new PipelineOptions { ... })`, `ModelPipeline.Status(doc)`, pure decisions `DecideTerrain`, `AutoSheetIndex.Merge`.
Diagnosis and proof runs: docs/MODEL-PIPELINE.md.

## Agent review (`sbm review`)

```
sbm review <model.sbm> [--all] [--no-regen] [--no-db] [--no-record] [--accept-pdf FILE]... [--json flags.json]
sbm review <model.sbm> --confirm PATH [--confirm PATH]... [--note TEXT]
```

Flags what is wrong, or not reviewed yet, in a model built from plan PDFs: plan PDFs (attached, present, unchanged since their fingerprint was
recorded), sheet links (links file, unused sheets, elements without a sheet), every input still at the program's starting value (with the sheets to
read it on; repeated values once per input pattern) and the generators' UNRESOLVED / CONFLICT flags. `--all` adds the minor ones (zero / off / choice
starting values, other warnings). `--confirm` records starting values that are right (path, object or pattern as printed). Writes
`<model>.agent-review.json` and the database table `agent_review` (view `v_agent_review_open`); exit code 3 when flags are open.
Library: `AgentReview.Run(doc, new AgentReviewOptions { ... })`, `AgentReview.Confirm(doc, paths, note)`. Rules and limits: docs/AGENT-REVIEW.md.

## Companion database (`sbm db`)

`sbm db <model.sbm> [out.sqlite] [--pages] [--plans <folder>] [--links rules.json] [--unused report.md] [--no-previews] [--no-vacuum] [--relink]`
writes `<model>.sqlite` (SQLite): one row per model element with the IFC GlobalId, hierarchy (project > site > bridge > unit > superstructure /
deck / substructure > groups such as girder, deck reinforcing, pier column n, footing n, FS4 bolts > elements), quantities and bounding box.
Tables: `elements` (view over the compact `el`/`strings`/`props`), `sheets`, `element_sheet` (links, stored on the group / assembly that the plan
organizes the information by), `qc` (status unchecked/ok/issue, checked_by, checked_at, note). Views: `v_element_sheets` (links resolved to every
child), `v_element_path`, `v_unused_sheets`, `v_element_qc`. `--pages` splits the plan PDFs (PyMuPDF, `tools/plan_split.py`) into
`<model dir>/plans/<set>/p###.pdf` + JPEG previews (git-ignored) and fills `sheets`. A rebuild keeps sheets and QC. The size is always printed
(a note appears above 100 MB; informational only).

**Plan sheet links by default.** The link rules file of a model is the `sheet-links*.json` beside it (or in its `plans` subfolder) whose
`"models"` list names the model file (a file without `"models"` applies to any model in the folder); `--links` overrides it. Shipped:
`samples/plans/sheet-links-i80-rampg.json` (I-80 WB + Ramp G, Designs 116 / 316), `sheet-links-woodbury.json` (Iowa Design 817),
`sheet-links-br02069.json` (MnDOT Br. 02069); the format is documented in the I-80 file's header. Each plan set carries its **sheet index**
(`"sheets": [ { "p": <PDF page>, "s": "<design sheet>", "t": "<title>" } ]`, from `docs/plan-data/*/INDEX.md` or a `--pages` run's
`sheets.json`), so every build (`sbm db` without `--pages`, the app's Create / Rebuild database) fills `sheets` and `element_sheet` with no
PDF splitting and no Python; index titles also replace the split tool's title-block text (which reads Iowa DOT blocks only). Rules may limit
themselves to PDF pages (`"pages": [24]`) when sheets share a title. The plan PDF is looked up (for Open PDF at the sheet's page; column
`sheets.pdf_path`) in `--plans`, the rules file's `"planFolders"` (relative to the rules file), the database and model folders and their `plans`
subfolders, `%SBM_PLANS%` (;-separated) and the program folder; a missing PDF only disables Open PDF. `--relink` re-applies the sheet index and
rules to an existing database in about a second (no regeneration; QC kept). Without a rules file the build prints that no plan sheet links were made.

**Querying (read-only, for plan review / fix agents).** Instead of reading whole plan PDFs, ask the database which pages matter. These options query
an EXISTING database (default `<model>.sqlite`, or `--db <file>`); they never open or regenerate the model, so they return at once.
- `sbm db <model.sbm> --sheets-for <text>`: elements whose mark, name, category or group name matches (exact mark first, else case-insensitive
  substring, or a `%` wildcard pattern). Up to 20 are shown (the rest are counted); each prints its hierarchy path and its sheets (direct + inherited):
  plan set, PDF page, design sheet no., title, role, `pdf_path`; then a de-duplicated `Pages to read:` list per PDF (`C:\SBMData\x.pdf: 29,30,31`).
- `sbm db <model.sbm> --find-sheet <text>`: sheets whose title or sheet no. contains the text (case-insensitive; a text starting with `^` is a regex);
  prints plan set, page, sheet no., title, `pdf_path` and the number of elements linked directly to the sheet.
- `--json` prints the same as JSON (`pagesToRead` maps each PDF to its comma-separated pages). API: `ModelDb.SheetsFor`, `ModelDb.FindSheets`.

In the app the Properties side shows the record of the selected element: hierarchy links and every plan sheet it is based on, its own or
inherited from a group / assembly ("via ..."), as "Sheet <n> · <title>", plan set, design sheet, PDF page, role and basis, with **Open PDF**
(the split single page if present, else the whole plan PDF at that page through Acrobat / Reader `/A page=N`, SumatraPDF `-page N` or Edge
`#page=N`), Preview (after Import plan pages…) and QC. When a model's database has no links, or its rules file changed since the links were
made, the app relinks it in the background when the model loads (log line `DB: plan sheet links from ...`).

## Asset management (`sbm asset`, G9)

The companion database is also the long-term asset record of the bridge: photos, documents, inspections (history by date), findings and calc
packages, all keyed by element GlobalId. **A rebuild (`sbm db`, Create / Rebuild database) never touches these tables** (it rewrites only the
element registry and links); a record whose element no longer exists in the model is an *orphan*: kept and listed by `sbm asset <m> status` and
the view `v_asset_orphans`, never deleted. Back up the `.sqlite` together with the assets folder: they are user data, not regenerable output
(both are git-ignored in this repository).

**Files live beside the database, not in it.** `photo add`, `doc add` and `calc add` COPY the file into `<model>_assets\` (beside the database;
subfolders `photos`, `documents`, `calcs`, `thumbnails`) named by content hash (`photos/2baa1e6bb845a623.jpg` = first 16 hex digits of the
SHA-256 + extension), so renaming or moving the original never breaks a link and the same file added twice is stored once. Table `asset_files`
keeps `location` (path RELATIVE to the assets folder, forward slashes: the model, database and assets folder move together) + `location_kind`
(`file` now, `url` for permalinks later; one resolver, `AssetStore.Resolve` / `ModelDb.ResolveAsset`), the original `file_name`, `media_type`,
`size_bytes`, `sha256`, `thumb_location`, `taken_at` (EXIF date of a JPEG unless `--taken`), `added_at`, `added_by`, `author`, `caption`, `note`.
`status` reports missing or changed files (size; `--verify` also checks the SHA-256); nothing is ever deleted. `meta.assets_folder` (relative to the
database folder or absolute) redirects the folder. Limits: none in the database (it holds metadata only); file sizes are those of the file system.
Thumbnails: PNG, longest side 320 px, made by `tools/asset_tool.py` (PyMuPDF: images, and the first summary page of a calc PDF) or, for photos
added in the app, by WPF. `asset_links(file_id, target_kind, target)` links a file to any number of `element` (GlobalId), `inspection`, `finding`
and `calc` ids.

Commands (`sbm asset <model.sbm> [--db file.sqlite] <command>`; the database must exist; REF = GlobalId, mark or name of an element; `--by NAME`
records who, default the Windows user; `sbm asset <m> help` lists everything):
```
sbm asset "Br.sbm" status [--verify]
sbm asset "Br.sbm" photo add IMG_0412.jpg IMG_0413.jpg --element CAP2 --caption "Pack rust at handhole 3" --author "A. Smith" [--taken 2026-05-20] [--inspection 2] [--finding 1]
sbm asset "Br.sbm" photo list [--element CAP2]          # doc add / doc list: the same for documents
sbm asset "Br.sbm" link 4 --element G3 [--unlink]
sbm asset "Br.sbm" inspection import samples/assets/inspection-elements.csv     # element-level layout
sbm asset "Br.sbm" inspection import samples/assets/inspection-ratings.csv      # ratings layout
sbm asset "Br.sbm" inspection add --date 2026-05-20 --kind routine --structure 7731 --inspector "A. Smith"
sbm asset "Br.sbm" inspection rate 2 deck 7 [--element REF]
sbm asset "Br.sbm" inspection list
sbm asset "Br.sbm" inspection export history.csv        # history.csv + history_ratings.csv + history_findings.csv
sbm asset "Br.sbm" finding add --element CAP2-W1 --desc "Section loss at handhole 3" --defect 1000 --severity high --location "lower edge"
          [--point E,N,Z | --station 24+76.3 --offset -12ft] --action "Clean, measure, paint" --due 2026-09-30 --inspection 2 --photo IMG_0412.jpg
sbm asset "Br.sbm" finding list [--element CAP2] [--open]
sbm asset "Br.sbm" finding set 1 --status repaired --note "painted 2026-08"
sbm asset "Br.sbm" calc add CAP2-design.pdf --summary 1-2 --title "Pier 2 steel cap" --rev B --date 2017-03-01 --element CAP2
sbm asset "Br.sbm" calc values 1 values.json
sbm asset "Br.sbm" calc diff [1] [--json] [--csv diff.csv]
sbm asset "Br.sbm" calc apply 1 --ids 2,5 | --all
sbm asset "Br.sbm" calc log [1]
```

**Inspections.** `inspections` (one per date + kind + structure number: routine, in-depth, fracture critical, underwater, special, damage, initial),
`element_condition` (AASHTO MBEI / SNBI element-level: element number + name, parent element number for protective systems / wearing surfaces,
defect number + name (empty = the element total row), unit, total quantity, CS1-CS4 quantities, note, `global_id` + the `element_ref` text it was
mapped from), `component_ratings` (NBI / SNBI 0-9 or N per component: deck, superstructure, substructure, culvert, channel, ...; `global_id` ''
= whole structure), `findings` (description, defect type, severity low / medium / high / critical, location text + optional model point E,N,Z (m,
project coordinates) and station / offset (m), recommended action, follow-up status open / monitor / scheduled / repaired / closed, due date,
closed_at, created / updated by + at). Views `v_condition_history`, `v_open_findings`.

CSV import (UTF-8, header row, names case-insensitive, any order, extra columns ignored; the layout is detected from the header; rows are grouped
into inspections by date + kind + structure number, created when missing; importing the same file again replaces the same rows; `element_ref` =
GlobalId, mark or name of the model element - rows that match nothing are kept unmapped and reported; element totals whose CS1-CS4 do not add up
to the total quantity are reported; * = required):
- element-level: `inspection_date`*, `inspection_kind`, `structure_number`, `inspector`, `element_ref`, `element_number`*, `element_name`,
  `parent_element_number`, `defect_number`, `defect_name`, `unit`, `total_qty`, `cs1`, `cs2`, `cs3`, `cs4`, `notes`
- ratings: `inspection_date`*, `inspection_kind`, `structure_number`, `inspector`, `component`*, `rating`*, `element_ref`, `notes`

Examples: `samples/assets/inspection-elements.csv`, `samples/assets/inspection-ratings.csv` (for the sample project). `inspection export` writes
the history in the same layouts (+ `global_id`, `element_model_name`), plus the findings with their photo locations.

**Calcs.** `calc add` copies the calc PDF to `calcs/`, records its SUMMARY pages (the 1-2 pages with the final design conclusions) and their text
(PyMuPDF, `tools/asset_tool.py pdftext`; table `calcs`). The values on the summary pages are extracted into a JSON file (in this phase by a Claude
agent reading the summary text / pages, or by hand) and loaded with `calc values` (replacing the previous extraction of that calc). Schema
`sbm-calc-values/1` (example `samples/assets/calc-values.json`):
```json
{ "schema": "sbm-calc-values/1", "extractedBy": "agent or person",
  "values": [
    { "name": "Web depth", "propertyPath": "Bridges[0].Girders.Section.WebDepth", "value": 60, "unit": "in", "element": "G1", "page": 1, "quote": "Web plate: 60 in x 1/2 in" },
    { "name": "Steel weight", "resultPath": "Steel.SteelMass", "bridge": "Sample Bridge", "value": 312.4, "unit": "kip", "tol": "2%", "page": 1, "quote": "..." },
    { "name": "Cap concrete", "element": "Abut 1 cap", "field": "volume_m3", "value": 19.2, "unit": "cy", "page": 2, "quote": "..." } ] }
```
Each value has exactly ONE target: `propertyPath` (a model INPUT, read and set with the same unit rules as `sbm get / set`: compared and can be
applied), `resultPath` (a path on the bridge's generated `BridgeModel`, e.g. `Steel.SteelMass`; SI internally) or `field` of an element (`element` =
GlobalId / mark / name resolved on import, or `globalId`, or `mark`; `mass_kg`, `length_m`, `volume_m3`, `material` or a key of the element's
database properties). `value` is a number (+ `unit`) or text ("W36x135", "1/2\"" - compared as text when it is not a number for the target).
Result units: in ft mm cm m, lb kip ton (US) t kg kN, in2 ft2 sf m2, ft3 cf cy yd3 m3, psi ksi MPa, kip-ft kN-m, deg. `tol`: "1/16\"" (input),
"0.5 kip" (result) or "1%"; default 0.1 % (inputs) / 0.5 % (results). `page` = calc PDF page, `quote` = the source line. Table `calc_values`.

`calc diff` compares every value with the model (reads only): `match`, `differs`, `missing` (path / element not found), `error` (value not readable).
`calc apply <calc> --ids ...` (or `--all` = every differing input of the calc) sets ONLY selected, differing inputs (results are never applied: change
the inputs that produce them), records every change in `calc_applied` (calc id + title, value id, property path, old -> new as displayed, page,
quote, applied_by, applied_at, model file) and saves the .sbm. Nothing is applied by a diff or an import. API: `SBM.Api.CalcSync` (`ParseValues`,
`ImportValues`, `Diff`, `DiffOne`, `Apply`), `PropertyPath.Parse` (text -> the value a set would store, without setting it).

**API** (`SBM.IO.Database.ModelDb`): `AddFile`, `AddUrl`, `Link` / `Unlink`, `Files`, `FilesForElement`, `ResolveAsset`, `CheckFiles`, `Orphans`,
`AssetCounts`, `FindElement`; `AddInspection`, `UpsertCondition`, `Condition`, `ConditionFor` (an element's rows or its nearest ancestor's),
`SetRating`, `Ratings`, `RatingsFor`; `AddFinding`, `UpdateFinding`, `Findings`; `AddCalc`, `Calcs`, `CalcsForElement`, `ReplaceCalcValues`,
`CalcValues`, `RecordApplied`, `AppliedLog`, `ElementField`. `InspectionCsv.Import` / `ExportHistory`. Schema: `AssetSchema` (version in
`meta.asset_schema_version`, currently 1; `meta.schema_version` 3); existing databases get the tables when first opened.

**In the app** the database panel (Properties side) shows for the selected element: Photos and documents (thumbnails of the element's and its
parents' photos, click = full size; documents open with their program; Add photo… / Add document… copy files into the assets folder), Inspection
(latest NBI / SNBI ratings with a ▼ when lower than at the previous inspection, the latest element-level CS1-CS4 quantities with defects -
recorded on the element or its nearest parent, e.g. the girder line - older inspections in one line each, findings of the element and its parts
with follow-up status + note (Save), Add finding… with severity, location, the picked 3D point and photos) and Calcs (calcs linked to the element
or its parents and their values vs the live model). Review > Asset > **Calcs** opens the project's calc packages: the stored PDF at its summary
pages (embedded Edge viewer), Add calc…, Import values…, the values-vs-model table, Summary text, Applied changes, and **Apply selected…**: a
confirmation lists every change (path: model -> calc, page); then each is logged, the model regenerates and is saved (Undo restores the inputs;
the log keeps the record). Snapshot `--calcswindow` captures the window (`_calcs.png`; the PDF viewer `_calcs_pdf.png`).

## Checking export (`--for-check`): what an independent checker is NOT given

For QC (docs/QC-PLAN.md sections 2 and 3) the checker must measure the model, not read the originator's reasoning. `sbm export <model> --ifc out.ifc --for-check [--keep-designations]`
(`SbmDocument.ExportIfc(..., forCheck: true, keepDesignations: false)`, `IfcExportOptions.ForCheck`) and `sbm db <model> out.sqlite --for-check`
(`DatabaseOptions.ForCheck`) write the checking versions. Normal exports are unchanged (byte-identical). The IFC filter (`SBM.IO.Ifc.IfcCheckFilter`, applied in
`StepWriter.Add`) never touches geometry: entity ids, placements, solids, GlobalIds, classes, predefined types, materials, relationships are identical to the normal export.

**Kept (IFC):** geometry, placements, GlobalIds, IFC class / PredefinedType, element Name when it is a plain identifier (mark), Tag, ObjectType class refinements (`END_DIAPHRAGM`),
material NAME, Pset SBM_Fabrication `Mark` / `Length` / `Mass` (measured or nominal-weight x length), SBM_Tendon model length / weight / strands, SBM_Placeholder `IsPlaceholder` / `Location` / `DeviceType`,
Pset_Stationing, alignment geometry, SBM_Status `Status` as the neutral flag `FLAGGED`.

**Stripped (IFC):**
- `Description` of every IfcRoot entity (element, assembly, type, alignment, project), of IfcMaterial and IfcProjectedCRS.
- `Name` / `Tag` / `ObjectType` / `ElementType` / material category and name remarks when they cite a plan or state a size or judgement (matches `IfcCheckFilter.IsLeak`: page refs `p.`, PDF, sheet, note, detail ref,
  OT1-style refs, UNRESOLVED, scaled / vectors / inferred / derived, assumed, plan(s), "not given", dates / revisions of standard drawings `(08-2017 rev. 11-24)`, inch fractions and inch marks, section
  designations such as `WT9x38`). A Name becomes the GlobalId; a USERDEFINED ObjectType becomes `USERDEFINED`; material remarks in parentheses are dropped (`Steel (placeholder, A36 minimum per plans)` -> `Steel`).
- SBM_Status: `Reason` and `Basis` removed; `Status` is rewritten to `FLAGGED` (the checker knows the part is flagged, not why).
- Properties: `Size` (the size string / designation of each part), `Reason`, `Basis`, `Source`, `PlanNotes`, `QuantityItem`, `Products`, `Pattern`, `Treatment`, `Anchors`, every `Plan*` property
  (PlanLength, PlanWeight_lb, PlanQuantity: the plan value is the checker's to read), SBM_Placeholder `Basis`, and any property whose text matches the leak rules.
- SBM_FabricationAttributes content (Toughness, ContactFinish, NDT, CoatingExterior / Interior, ShopAssembly, Tolerance, Camber, Weld) unless `--keep-designations`; PlanNotes never. A property set left empty is not written (nor its relationship).
- `--keep-designations` keeps the `Size` property when it holds a designation without inch-fraction size strings or plan references, and the fabrication attributes above (checkable claims).

**Companion database `--for-check`:** the element registry only: GlobalId, parent, level, category, subtype, IFC class, mark, name, bridge group / bridge / unit / support / girder, station range, material, mass, volume,
length, bounding box. Removed: `sheets`, `element_sheet` (sheet links, basis, source), `qc`, all property JSON (descriptions, status reasons, assumed bases, plan-derived values), the model path / links file in `meta`,
names / marks / strings matching the leak rules (name falls back to category + mark). Elements the originator flagged carry `properties = {"flagged":true}` (view `v_flagged`); `meta.for_check = 1`. The normal database
(with the sheet links) is a separate file and can be given to the checker later to CHECK the links as a finding source.

**Verifier:** `python -I tools/check_ifc_leaks.py file.ifc [--expect-clean] [--examples N] [--json out]` scans every string attribute / property value (GlobalIds and enumerations excluded) for the leak kinds
(page_ref, note, UNRESOLVED, scaled, assumed, plan_text, inch_size, designation) and prints counts per kind and per entity attribute; with `--expect-clean` the exit code is 1 when anything is found.
Not detectable by rule (accepted, named here): descriptive element names without sizes ("Left curb", "Pier 3 pedestal G1"), material strengths in material names, rolled-section Mass (nominal weight x length), the
SBM_Status flag itself.

## Headless screenshots and turntable video (`--snapshot`)

The app can render itself without anyone touching it and exit. Run these on a PC with a display (a GPU is needed for the viewport):

```
:: full app window + the viewport only (nebraska.png and nebraska_view.png)
SteelBridgeModeler.exe "samples\plans\Nebraska St Ramp B.sbm" --snapshot "temp\media\nebraska.png" --3d --winsize 1920,1080

:: turntable frames of the viewport only: nebraska_frame0001.png ... nebraska_frame0180.png
SteelBridgeModeler.exe "samples\plans\Nebraska St Ramp B.sbm" --snapshot "temp\media\nebraska.png" --3d --winsize 1920,1080 --turntable 180

:: frames -> video (pip install imageio imageio-ffmpeg; without them an animated GIF is written)
python -I tools\frames_to_video.py temp\media\nebraska temp\media\nebraska.mp4 --fps 30
```

`--turntable N[,degrees[,pitch]]`: N frames (default 120), `degrees` of yaw over the run (default 360; frame i turns by degrees * i / N, so a
full turn loops), `pitch` = degrees the camera looks down (default: the current 3D view). The orthographic camera circles the scene centre
(yaw about world Z), the view width is set once from the bounding sphere so nothing clips. Detail layers (welds, rivets, bolts, near rebar)
are off in every frame unless `--detail` is given (then the set drawn at the start stays drawn, no flicker). Also accepts `--winsize`,
`--noterrain`, `--off PREFIX,...`, `--camera`. Log lines `TURNTABLE:` in `%LOCALAPPDATA%\SteelBridgeModeler\session.log`.
Large models can take ~0.2 s per frame plus load time.
