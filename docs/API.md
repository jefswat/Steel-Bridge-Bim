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
sbm export "C:\Jobs\Br12\Br12.sbm" --3dm Br12.3dm [--bridges "Unit 2,Unit 3"] [--no-detail] [--mesh-only] [--terrain] [--bar-solids] [--3dm-version 7]
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
| Everything else (deck, barriers, concrete, substructure, meshes the generators build directly) | Mesh (double-precision vertices, creases split at 30 deg) |
| Bolts, rivets, studs, anchor bolts | one block (instance definition) per type and size + an instance reference per fastener |
| Bars, strands, PT tendons | curves: lines + true arcs (ArcFit, 0.5 mm) or polylines; `--bar-solids` adds tube meshes on `<layer>-SOLID` |
| File version | Rhino 8 archive by default; `--3dm-version 7` writes a Rhino 7 archive (Rhino 7 cannot open version 8 files; Rhino 8 reads both). `SbmDocument.Export3dm(..., fileVersion: 7)` |
| Alignments | `Alignments::<name>`: plan curve (lines / arcs exact, spirals sampled <= 0.5 m) at the base elevation + 3D profile grade line (<= 1 m) |
| Terrain references (`--terrain`, visible ones) | `Terrain::<name>` mesh (the display mesh) |

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
```

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

`--status` reports without changing anything; `sbm regen` prints "Model pipeline: N item(s) open" when something is missing. `sbm run` runs the pipeline
only when the script SAVED the model; `--no-pipeline` / `SBM_PIPELINE=0` skip it, `SBM_OFFLINE=1` = no downloads, `SBM_PYTHON` = the Python to use.
Library: `ModelPipeline.Run(doc, new PipelineOptions { ... })`, `ModelPipeline.Status(doc)`, pure decisions `DecideTerrain`, `AutoSheetIndex.Merge`.
Diagnosis and proof runs: docs/MODEL-PIPELINE.md.

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
