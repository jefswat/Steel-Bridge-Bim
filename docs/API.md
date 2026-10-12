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
sbm drawings   "C:\Jobs\Br12\Br12.sbm" "C:\Jobs\Br12\dxf"   # 2D DXF models: layout, plan, typical section, bents, bar bending
sbm drawings   "C:\Jobs\Br12\Br12.sbm" "C:\Jobs\Br12\dxf" --zip   # same views as one zip (dxf.zip); a target ending in .zip does the same
sbm elevations "C:\Jobs\Br12\Br12.sbm"          # grade → deck → haunch → web → flange → sole plate → seat, every bearing
sbm blocking   "C:\Jobs\Br12\Br12.sbm"          # camber, deflections, girder-line haunch elevation and blocking at each key point (--all: every plan point)
```

**Exit codes:** `0` means OK, `1` means a usage or input error, and `2` means the model has errors.
`set`, `add`, `remove` and `import-landxml` save the file.

**Python:** see `samples/scripts/skew_study.py`. It calls `sbm` through `subprocess`. Project files are plain JSON
(SI units), so you can also read them directly.

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

In the app the Properties side shows the record of the selected element: hierarchy links and every plan sheet it is based on, its own or
inherited from a group / assembly ("via ..."), as "Sheet <n> · <title>", plan set, design sheet, PDF page, role and basis, with **Open PDF**
(the split single page if present, else the whole plan PDF at that page through Acrobat / Reader `/A page=N`, SumatraPDF `-page N` or Edge
`#page=N`), Preview (after Import plan pages…) and QC. When a model's database has no links, or its rules file changed since the links were
made, the app relinks it in the background when the model loads (log line `DB: plan sheet links from ...`).
