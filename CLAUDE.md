# Steel Bridge Modeler — notes for Claude

Desktop BIM modeler for steel plate girder bridges and substructures (Windows, .NET 10, WPF). The user is a bridge
engineer who directs and tests; Claude writes and maintains all code. Keep this file current when architecture changes.

## Build / test / run
- SDK: `C:\Program Files\dotnet\dotnet.exe` (may not be on PATH in older shells).
- `dotnet build SteelBridgeModeler.sln` · `dotnet test tests/SBM.Tests`
- App: `src/SBM.App/bin/Debug/net10.0-windows/SteelBridgeModeler.exe` (loads the sample project on start).
- Release: `dotnet publish src/SBM.App -c Release -o dist/SteelBridgeModeler`
- Close-up of a cross frame: add `--closeup CF7 [--below]` (implies 3D).
- Visual check without a screen: `SteelBridgeModeler.exe --snapshot out.png [--3d] [--editors]` writes the window,
  the GPU viewport (`out_view.png`) and every editor tab, then exits. Normal screen capture of WPF shows blank in this
  environment — use `--snapshot`.
- Session log: `%LOCALAPPDATA%\SteelBridgeModeler\session.log` (messages + exceptions).

- Release: `dotnet publish src/SBM.App -c Release -r win-x64 --self-contained false -p:PublishReadyToRun=true -o dist/SteelBridgeModeler`
  and the same for `src/SBM.Cli` (same output folder → `sbm.exe` beside the app).
- Automation docs for the user: `docs/API.md`; example scripts in `samples/scripts`.

## Projects
- `src/SBM.Api` — `SbmDocument` (open/new/save, typed + text property-path edits, incremental `Regenerate`, export),
  `PropertyPath` (paths like `Bridges[0].Supports[1].SkewDegrees`; unit-aware parsing; plain numbers use the field's
  display unit inferred from the property name via `KindOf` — keep that list in sync when adding inputs),
  `ScriptHost` (Roslyn C# scripting; globals Doc/Print/Ft/In/Sta/Len).
- Remote control of the running app: `SBM.App/MainWindow.Remote.cs` (named pipe `SteelBridgeModeler.<pid>`,
  CurrentUserOnly, one JSON line in/out, handled on the UI thread via the same VM methods as the ribbon) ↔
  `SBM.Api/SbmApp.cs` client, `sbm app …`, script global `App`. Not started in `--snapshot` runs. Add new remote commands
  in `HandleRemote` + `SbmApp` wrappers + docs/API.md.
- `src/SBM.Cli` — `sbm.exe` command line over SBM.Api (new/info/get/set/add/remove/import-landxml/regen/export/run/bench/
  elevations/drawings/sheets).
- `src/SBM.Core` — no UI. All geometry and the parametric model.
  - `Geometry/` Vec2/Vec3 (double), Box2, Numerics (Gauss-Legendre, Brent, FindRoots, line intersection).
  - `Alignments/` `HorizontalElement` = one class for line/arc/clothoid (linear curvature K0→K1, partial spirals OK);
    `Alignment` (distance-based internally, station APIs public, station equations, Inverse = point→sta/off);
    `PiDefinition` (PI table → exact elements incl. asymmetric spirals; `TryExtract` recovers PIs from elements);
    `VerticalProfile` (sym/unsym parabolic VCs); `CrossSlopeTable` (station rows, pivot offset).
  - `Bridge/BridgeDefinition.cs` — the parametric input (supports, girders, deck, barrier, cross frames, stiffeners, splices).
  - `Framing/FramingGenerator.cs` — pure function definition+alignment → `BridgeFraming` (bearings, girder curves,
    cross frames, stiffeners, splices, deck outline, diagnostics). Parallel per girder / per bay.
    `GirderSpanCurve` is the per-span girder geometry (curved-parallel or chorded) with an arc-length table.
  - `Drawing/` renderer-neutral layered linework (`PlanDrawing`) built by `PlanDrawingBuilder`; shared by viewport and DXF.
  - `Solids/Mesh.cs` — Frame3, indexed triangle Mesh (volume relative to a mesh vertex — origin-based sums lose all
    digits at UTM coordinates and made masses/tonnage garbage; bounds), MeshBuilder (AddLoft = sweep of a 2D section
    through 3D rings with auto-orientation, extrusions, prisms, fillet welds, ear-clipping, convex hull).
  - `Solids/ExtrudedSolid.cs` — exact recipe a builder records on the `Mesh` (`Mesh.Solids`/`HasExactSolids`): planar
    polygon + round holes swept along a vector. `AddLoft` (straight sweeps → boxes, prisms, angles, welds), `AddPrism`/
    `AddPlanar` (affine maps) and `AddLongPlate` (greedy merge of flat, constant-thickness strips) record them; IFC writes
    IfcExtrudedAreaSolid + IfcArbitraryProfileDefWithVoids (IfcCircle holes) when they cover every triangle and are
    smaller, else a merged-vertex IfcTriangulatedFaceSet. New solid builders should record recipes too.
  - `Geometry/ArcFit.cs` — sampled bar/strand centerlines → lines + arcs (≥4 points per arc, radius ≤ 5 m, sweep ≤ 90°,
    0.5 mm) for IFC IfcIndexedPolyCurve (IfcArcIndex). Spirals stay true helices in the model (32 samples/turn, exact
    length; close end turns climb 1 db/turn); IFC has no helix, arcs stay within 0.5 mm. Long curved deck bars stay
    polylines (IfcOpenShell mis-sweeps flat 3D arcs).
  - `Solids/PlateBuilder.cs` — plates WITH HOLES: polygon+holes triangulation (horizontal bridge to the +x ray hit —
    don't change to a vertex bridge; that crossed unmerged holes and caused the bearing-stiffener artifact), long girder
    plates built in strips/cells (≤1 hole per cell keeps it linear), Sutherland–Hodgman half-plane clip.
  - `References/` — reference model definitions, `TerrainModel` (grid index elevation queries, parallel contours).
  - `Substructure/` — `SubstructureDefinition` lives on each `SupportDefinition.Substructure` (null = defaults by type via
    `ResolvedSubstructure`). `SubstructureGenerator` runs AFTER steel (needs `SteelModel.BearingSeats`), per support in
    parallel: cap coordinates S (along the skewed support line from the alignment point, right +), N (normal, up-station +).
    Cap top = shape g(S) + c with c set so min pedestal = MinPedestalHeight; pedestals take the rest (variable).
    `SubstructureRebar.cs` = bars + marks (per unit; `BarMark.Unit`/`BarPlacement.Unit`; straight bars within 2" share a
    mark cut to the shortest). Ground for automatic foundation tops: `RegenerationCache.Ground` (+ `GroundVersion` in key);
    app sets it from loaded terrains, `SbmDocument` loads terrain references itself.
  - As-designed substructure (OPTIONAL, layered on the standard parametric bent which stays the default):
    `SubstructureDefinition.CapProfile` (+ `CapProfileAhead` for stepped caps) = closed (S, elevation) polygon from the
    plan pier elevation (hammerheads, tapered/raked ends); octagonal columns (`ColumnChamferS/N`) with optional flared
    base (`ColumnFlare*`); `Footings` per column (outline polygon, explicit pile positions, pile length). Shared lines with
    side-by-side as-designed piers ("Pier 11WB & 19G"): every support with a CapProfile owns its cap and takes only the
    seats on it (RegenerationCache owners). Conflicts (seats off the cap, seat below cap top, column not under cap) are
    WARNINGS, never auto-fixed. Plan-set data: `samples/plans/piers-i80.csx`, `piers-ramp-g.csx`, `deck-edges-i80.csx`.
    Chamfered (octagonal) column rebar (`SubstructureRebar.ColumnBars`): verticals per face from the OUTSIDE-face
    dimensions (centred groups, explicit chamfer positions, or equal spaces with shared corners) set in normal to the
    face; ties follow the section (`PolygonTie`, hooks at the sharpest corner), optionally as two overlapping end hoops
    (`ColumnHoopOverlap`, legs mapped like the S-face corner) + crossties (`Crosstie`); flared base gets its own cage.
    Bar sizes include #14/#18. Plan data: `samples/plans/column-rebar.csx`.
  - `Drawing/Views/` — 2D views as `PlanDrawing`s (Z = 0) via `Drafter` (paper-inch sizes × scale; dims with ticks,
    leaders, hatch, elevation tags, AWS weld symbols; ALL text is vertically middle-aligned — DxfWriter writes 72/73
    accordingly; "\n" in text = multi-line). Keep annotation text short/multi-line: paper-sized text does not shrink
    with scale, so long single-line notes make a view unfittable. Shell heredocs mangle backslashes — write patch
    scripts with the Write tool.
    `LayoutViews` (developed plan over elevation: X = station; plus true plan), `SectionViews` (typical section),
    `BentViews` (per unit; abutment at begin is mirrored to look from the span), `BarSheet` (paper inches, PaperSpace=true),
    `DrawingSet.Build`. DXF via `SBM.IO.Dxf.SheetExport` (2D polylines, circles, LTSCALE from scale). App:
    `Editors/DrawingsWindow` + `Viewport/DrawingCanvas` (also `RenderToPng` for `--snapshot ... --editors [--dwgzoom x0,y0,x1,y1]`).
    `--closeup SUBn` = 3D close-up of substructure unit n.
  - `Drawing/Sheets/` — `SheetComposer` (fits each `Panel`'s `ViewSpec` at the largest standard scale that fits — views are
    REBUILT per scale so paper-sized text stays right — then places it on a sheet with border + title block; sheet
    drawings are paper meters, 1" = 0.0254), `SheetSet.Specs/Build` (the plan set). New views: `GirderViews` (developed,
    depth schematic), `SteelDetailViews` (cross frames from `SteelModel.CrossFrameDetails` = 2D geometry recorded by the
    detailer — keep it in sync when changing CrossFrameDetailer), `BearingViews` (anchor bolts from `BoltKind.Anchor`
    instances; 3D clearance checks vs cap bars). `SBM.IO.Pdf.PdfWriter` = minimal vector PDF (Helvetica, print colors via
    `PrintStyle`, also used by the app's paper preview).
  - App 3D: `MainWindow.Viewport3D.cs` — camera depth range follows the scene (`KeepCameraInRange` on CameraChanged; do
    not hand-set Near/Far elsewhere); section cut = CPU clipping of all solids/lines/text (Helix
    `CrossSectionMeshGeometryModel3D` was tried: transparent models stop rendering when it is in the scene — don't use
    it); picking via `SceneBuilder.Picks` (triangle range → source object) and `InstanceTags`; clipped meshes map
    triangles back via `_triMap`. Hide/unhide: `_hidden` (tags) filters merged meshes by pick-table ranges and
    instanced meshes by `_visibleInstances`, through the same ClipMesh/ApplySection path as the section cut; `RemapHidden`
    re-matches hidden tags after regeneration via `HideKey`. Right-click (no drag) = context menu. Sheets mode: `MainWindow.Sheets.cs`. Snapshot options: `--section STA[,skew]`,
    `--pick fx,fy` (logs PICK: properties), `--sheets`.
  - `Prestressed/` — `TxGirderCatalog` (Tx28–Tx70 as PGSuper/WBFL precast I-beam dims `PrecastIBeamDims`; `TxGirderShape`
    outline = WBFL PrecastBeam polygon; IGND grid; IGEB pad table), `PrestressedGenerator` (one straight beam per span ×
    girder between the framing's support-line points; IGD/IGEB ends; strands; pads; `BearingSeat`s with Location so the
    substructure gets two pedestals per girder at bents). `GirderLayoutDefinition.Kind` switches steel ↔ prestressed:
    FramingGenerator treats the beam top as "top of web" (flange t = 0, web = D), forces chorded, skips cross frames,
    stiffeners and splices; `RegenerationCache` runs PrestressedGenerator instead of steel (`BridgeModel.Prestressed`).
    Views: `PrestressedViews` + branch in `SectionViews` / `SheetSet`. IFC: IfcBeam + IfcTendon (validated 0 issues).
  - `SBM.IO/PgSuper/` — `PgSuperReader` (.pgs BridgeDescription schema v6+ or ReportExportAgent export XML; strand key
    bits per WBFL PsStrand; end distances normalized to CL-bearing-to-end) and `PgSuperImport.Apply` (onto an alignment).
    The user's PGSuper/BridgeLink tooling lives in `Documents\PGSuper-master\copilot` (agent, MCP) — reuse its export.
  - `Checks/ElevationCheck.cs` — transparent elevation chains (`CalcStep` rows: profile via `VerticalProfile.Explain`,
    cross slope, slab, haunch, web, flange, sole plate, bearing stack) reconciled against framing + `BearingSeats`.
    MUST mirror `BearingDetailer` math; tests fail if they drift.
  - `Project/RegenerationCache.cs` — per-bridge hash cache used by app and API.
  - Steel generator phases: layouts → connections (cross frames, field splices, lateral bracing register bolt holes
    in `_holes` / `_stiffHoles` / `_studExclusions`) → members (girder plates, stiffeners, bearings, studs). Files:
    `SteelModelGenerator.cs`, `CrossFrameDetailer.cs`, `FieldSpliceDetailer.cs`, `LateralBracingDetailer.cs`,
    `BearingDetailer.cs` (elastomeric / disc / GENERIC = vendor-designed: sole plate + one body sized so the assembly is
    `GenericHeight` at the CL bearing + masonry plate sized from the anchor spacing + optional preformed pad; anchors as
    given). Per-support bearing override `SupportDefinition.Bearings` (`BridgeDefinition.BearingsAt(j)`) and per-girder
    fixity `GirderFixities` (`FixityOf(g)`). Shared helpers on `BearingDefinition` (SoleAtCl, GenericBodyHeight, Footprint)
    keep BearingDetailer, ElevationCheck and BearingViews in step; steel `BearingSeat`s carry the footprint.
  - `Concrete/` — `ConcreteGenerator` (deck slab grid solid between skewed end lines following finished grade,
    haunch sweeps, barrier sweeps, deck mats, 1% negative-moment bars, TxDOT SSTR Bars S/U/R), `BarrierProfiles`
    (SSTR per RL-SSTR Sept 2019 — 3/4" back-face step is an assumption), `Rebar.cs` (A615 sizes, AASHTO 5.10.8 Class B
    laps, `BarShape` centerlines + bend-diagram dims, `BarMark` incl. "varies" marks). Mark rules: ≥4 identical =
    own mark; straight bars within `MarkLengthTolerance` snap to the cluster's SHORTEST length; odd closures grouped
    into "varies" marks (≤3 ft range). Negative-moment bars are one length centered on the pier. Equal-piece splits
    except staggered longitudinals (stock 60 ft + closure).
  - `Steel/` — `Sections.cs` (AISC angle/channel catalog, bolt dims + length rounding), `SteelModel` (parts,
    assemblies, bolt instances, bearing seats), `SteelModelGenerator` (girder plates swept along `GirderPath`, plate
    changes with 1:2.5 width tapers, field-section split at splices, flange-web welds, stiffeners in their skewed
    planes, bearings) and `CrossFrameDetailer.cs` (frame-plane coords x/z/v; single angles on gusset faces, gussets =
    convex hull of bolt group + member laps, bolts through connection stiffener + gusset, X filler + crossing bolt,
    K/inverted-K center gussets, channel diaphragms). Trim() computes member end cuts from half-plane clearances.
  - `Project/` `BridgeProject` (JSON *.sbm via DTOs — keep the file format stable, bump `CurrentVersion` on breaking
    changes), `SampleProject`.
  - `Units/UnitSystem.cs` — the ONLY place display units live. Internal units: meters, radians (skews stored in
    degrees), slopes rise/run. Parses ft-in (12'-6 1/2"), stations (12+34.56), DMS, %.
- `src/SBM.IO` — `LandXml/` reader (namespace-agnostic; Line/Curve/Spiral, StaEquation, ProfAlign PVI/ParaCurve/
  UnsymParaCurve/CircCurve, Superelevation critical stations) and writer; `Dxf/DxfWriter` (ASCII R12, 3D polylines);
  `Ifc/` StepWriter + IfcExporter (IFC4X3_ADD2, IfcBridge/IfcBridgePart/IfcElementAssembly/IfcPlate/
  IfcMember/IfcMechanicalFastener (typed by IfcMechanicalFastenerType per size holding the representation map; occurrences = own
  IfcLocalPlacement + the type's SHARED IfcProductDefinitionShape — a shape representation may not be shared, a product
  shape may)/IfcFastener WELD/IfcBearing, IfcMapConversion base point,
  deterministic GlobalIds; a *.ifczip path writes a buildingSMART IFC-ZIP = one DEFLATE entry <name>.ifc, streamed). NOTE: in ADD2 IfcFacilityPart is abstract — use IfcBridgePart(..., UsageType, PredefinedType).
  Validate with IfcOpenShell (`pip install ifcopenshell pytest`; `ifcopenshell.validate.validate(f, log, express_rules=True)`);
  last result: 0 issues.
- `src/SBM.App` — WPF (Fluent dark theme), MVVM via CommunityToolkit.Mvvm, GPU viewport = HelixToolkit.Wpf.SharpDX 3.x
  (DirectX 11, System.Numerics types; namespaces `HelixToolkit`, `HelixToolkit.SharpDX`, `HelixToolkit.Wpf.SharpDX`).
  - `Viewport/SceneBuilder` batches each layer into one LineGeometry3D + one BillboardText3D; vertices are rebased to a
    local origin (state-plane coords exceed float precision). Model-sized text (FixedSize=false).
  - `Editors/` input windows built from `FieldBase` rows (TextField/ChoiceField/BoolField/SpacingField) and DataGrids of
    `CellValue` (unit-aware text cells). Apply = regenerate without closing.
  - Header readability: App.xaml defines explicit TabItem (`SbmTabItem`) and DataGridColumnHeader templates — do NOT
    use `BasedOn="{StaticResource {x:Type TabItem}}"` (resolves to the classic light template). TabItem Foreground is
    inherited by tab content, so it must stay light. All windows get dark title bars (DWM) and a light default
    Foreground in App.xaml.cs. Any custom-templated style without BasedOn (RibbonButton, ToolIconButton) MUST set its
    own Foreground — the user saw black ribbon labels on screen even though `--snapshot` rendered them light.
  - Command search (Ctrl+K) in `MainWindow.Commands.cs` auto-collects every ribbon button (label, tab, group, tooltip).
  - `ViewModels/MainViewModel` — project, undo/redo (JSON snapshots), async regeneration (Task.Run + version counter).

  - Measure / query / snaps (App): `MainWindow.Measure.cs` (tools Measure, Measure Angle, Station/Offset query; overlay Canvas
    `MeasureOverlay`, panel `MeasurePanel`, snap bar; remote `measure` / `station-offset`; snapshot `--measure fx,fy,fx,fy`,
    `--query fx,fy`, `--snapbench N`), `Viewport/SnapEngine.cs` (`SnapIndex` = BVH over triangle/segment/instance chunks, built lazily
    after each scene rebuild; ranked candidates + occlusion test), pure helpers `SBM.Core/Geometry/SnapMath.cs`, `PointQuery.cs`.
    TODO: prune the search radius by the K-th best candidate (zoomed-out queries on big models are slow).
- Units/shared piers: `SupportDefinition.SharedPier` + `PierCenterOffset`; `RegenerationCache` is two-phase (superstructure
  per bridge, then substructure per bridge with `ForeignSeats` of other bridges on the owner's shared piers).
- Discontinuous girders: `GirderLayoutDefinition.Terminations`; `FramingGenerator.ApplyTerminations` truncates the girder,
  nulls its bearings beyond, adds `CrossGirderResult`; cross-frame bays are (Bay, RightGirder) pairs — never assume
  RightGirder = Bay + 1. Steel `BuildCrossGirder`.
- Girders beginning on their own support line: `BridgeDefinition.AuxiliarySupports` (SupportDefinitions whose CL bearing
  line does NOT split spans; framing index = Supports.Count + k) + `GirderLayoutDefinition.Beginnings` {Girder, Support name}.
  `FramingGenerator.ApplyBeginnings` cuts the girder at (bearing − GirderEndExtension) and RE-BASES its path (s from 0;
  `_startExt`/SpanStart shrink, span curves unchanged), nulls bearings before it, puts the start bearing on
  `BridgeFraming.AuxSupports` (`BearingResult.PathDistance` set). Use `BridgeFraming.SupportLine(i)` /
  `BridgeDefinition.SupportAt(i)` for any index from a bearing, frame or substructure unit — never index `Supports` directly.
  Support cross frames and bearing stiffeners are built on aux lines; substructure units, elevation check, bent/bearing/
  girder views include them. Data example: `samples/plans/pier-6a.csx` (I-80 Pier 6A).
- As-designed cross frames (OPTIONAL; without them the automatic layout + definition angles are used):
  `CrossFrameDefinition.Styles` (`CrossFrameStyle`: InvertedK/KFrame/XFrame/Strut/PlateDiaphragm; top chord angle or W
  bolted through its web; bottom chord angle, channel, "2-" back-to-back channels straddling the gussets, or WT (stem on
  the gussets); per-style gusset, bolt dia/columns/gage/rows/pitch/edge, work points, bottom bolt offset, tie plates),
  `IntermediateStyle`, `SupportStyles[support name]` ("none" = no frames on that line), and `Lines` (`CrossFrameLine`:
  cumulative distance along a reference girder from its first bearing, radial to the alignment or normal to the girder,
  optional `Bays` list and `BayStyles`). `FramingGenerator.BuildFrameLines` crosses every girder in every span (6"
  bearing snap), frames only in bays crossed within one span; spans touched by lines skip the automatic layout. Detailer:
  `Member` = angle/channel(pair)/W/WT, `BuildPlateDiaphragm`. Catalog lookups are exact (`FindAngle/Channel/WideFlange/
  Tee`); unknown sections are reported. Plan data: `samples/plans/cross-frames-i80.csx`, `cross-frames-ramp-g.csx`.
  Rolled sections come from the embedded AISC Shapes Database v16.0 extract `Steel/AiscShapes.csv` (all W, WT, C, MC, L:
  dims, A, W lb/ft, kdes/kdet/k1, x/y, workable gages WGi/WGo; angle gages g/g1/g2 = AISC Manual Table 1-7A in code).
  Profiles (`AnglePolygon`, `ChannelPolygon`, `WideFlangePolygon`, `TeePolygon` via `RoundCorners`) have root fillets
  from kdes; C flanges sloped 16-2/3% (tf at mid free width); MC flanges parallel (slope not in the database); toes
  square (toe radius not published). Rolled parts weigh AISC lb/ft x length (`SteelPart.NominalWeightLbPerFt`).
- Cross-slope breaks: `CrossSlopeRow.LeftBreak/LeftOuterSlope` (and Right*) — slope beyond a distance from the pivot
  (gores) — plus any number of further breaks `LeftMoreBreaks`/`RightMoreBreaks` (`CrossSlopeBreak` distance+slope);
  `CrossSlopeTable.SidesAt` interpolates break by break (a missing break = neighbour's distance, unchanged slope) /
  `DeltaElevation`; the elevation check lists the segments. Plan checks: `samples/plans/check-top-of-slab.csx`,
  `check-top-of-bearing.csx`.
- Expansion joints (OPTIONAL, default off): `SupportDefinition.ExpansionJoint` (`ExpansionJointDefinition`) on the FIRST/LAST support of a
  bridge. `ConcreteGenerator` cuts the deck back by the blockout width D (separate `Ends(o, insetB, insetE)`; shelf if the blockout is shallower than the
  slab), builds the blockout concrete and a generic device envelope (`ConcreteModel.Joints`, viewport layer `3D-JOINTS`, material rows). IFC: blockout =
  IfcBuildingElementPart (aggregated into the deck IfcSlab), device = IfcDiscreteAccessory EXPANSION_JOINT_DEVICE + pset `SBM_Placeholder`. The support's
  `DeckEndOffset` stays the authority (the joint's own value is only a check). Plan data/confirmations: `docs/plan-data/expansion-joints.md`,
  `samples/plans/joints-i80.csx`, `joints-ramp-g.csx` (run after the build scripts).
- Variable deck width: `DeckDefinition.LeftAt/RightAt(station)`; constant decks keep the old code path.
- Drainage: `BridgeDefinition.Drains` → `SubstructureModel.Drainage` (built in SubstructureGenerator, needs terrain for basins).
- Raster terrain: `ElevationGrid` (full-res bilinear) + decimated display mesh; `TerrainModel(grid, mesh)`.
- Plan-set test models: `samples/plans/*.csx` (Iowa DOT Designs 116/316); data in C:\SBMData (not in OneDrive).

## Conventions (bridge engineering)
- Offsets right-positive (Civil 3D). Skew from the radial line; positive = right end of support line up-station.
- Girder offsets are measured normal to the alignment; flared layouts via `OffsetsAtSupports` (linear between supports).
- HAUNCH = top of web to bottom of deck (user's convention). Top of web = finished grade − slab − haunch; top flange
  thickness only affects top-of-steel and stud length (= haunch − tf + penetration, rounded). Webs plumb, flanges level.
- Intermediate cross frames, connection plates and intermediate stiffeners are NORMAL TO THE FLANGES (tilt with
  grade); support frames and bearing stiffeners are PLUMB. Connection/bearing stiffeners lie in the frame/support plane.
- Shop splices: 1:2.5 width and thickness tapers on the larger plate. Field splices: wider flange tapers (1:2.5) to the
  narrower at the joint; thickness differences → fillers on the outside. Splices are NSBA-style (outer full-width +
  2 inner flange plates, 2 web plates); bolt quantities are inputs.
- Cross frames: Auto orientation = parallel to skew if skew ≤ 20° else normal (AASHTO 6.7.4.2); Auto arrangement
  staggers by bay when skew > limit; frames slide toward mid-bay to keep `MinDistanceFromBearing`.
- Default foot = international (0.3048); LandXML USSurveyFoot is honored on import.

## Roadmap (agreed with user)
1. ✅ Milestone 1: alignment import/create with cross slope; 2D framing plan with deck, stiffeners, cross frames.
2. ✅ Milestone 2: 3D steel (plates, skewed stiffeners, single-angle cross frames welded to gussets bolted to
   stiffeners with A325 7/8" bolts, welds, elastomeric bearings), material list, IFC 4.3 export.
   User's detailing preferences are the defaults (see memory). Known simplifications: holes not cut in solids,
   no camber, flange thickness transitions are square butt joints (no thickness taper), girder ends cut square.
3. ✅ Milestone 3: holes in all bolted plates, NSBA field splices, shop/field tapers, shear studs, disc bearings w/
   anchor bolts + fixity, top/bottom lateral bracing, frames normal to flanges, API/CLI/C# scripting, terrain
   (LandXML TIN/DXF/OBJ/STL) + reference models, incremental regeneration, ReadyToRun.
   Not yet: camber, holes in webs for lateral connections, web fillers at splices, IFC/DGN reference import.
5. ✅ Substructure (multi-column bents, abutment caps/backwalls, pedestals, sloped/crowned/level/seat-following caps,
   drilled shafts / spread footings / pile caps + piles, rebar) and 2D views → DXF 2D models (layout plan+elevation,
   true plan, typical section, bent elevations + cap sections, bar bending sheet). IFC substructure validated 0 issues.
   Superstructure leftovers (not requested yet): camber, web fillers at splices, wingwalls, approach slabs.
4. ✅ Deck/barrier solids + rebar (TxDOT SSTR default rail), bar schedule window with bend diagrams, concrete/rebar
   in IFC (IfcSlab, IfcRailing GUARDRAIL, IfcReinforcingBar with IfcSweptDiskSolid — validated 0 issues).
   Next: substructure (multi-column piers first) with rebar layouts. Camber deferred by the user.
   Snapshot targets: `--closeup RAIL [--below]` (rail oblique / end-on section), `--editors` also renders the bar schedule.
5. IFC 4.3 export (IfcAlignment, IfcBridge), DXF 3D, later DWG/DGN via ODA if licensed.
