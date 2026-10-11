# Steel Bridge Modeler â€” notes for Claude

Desktop BIM modeler for steel plate girder bridges and substructures (Windows, .NET 10, WPF). The user is a bridge
engineer who directs and tests; Claude writes and maintains all code. Keep this file current when architecture changes.

## Build / test / run
- SDK: `C:\Program Files\dotnet\dotnet.exe` (may not be on PATH in older shells).
- `dotnet build SteelBridgeModeler.sln` Â· `dotnet test tests/SBM.Tests`
- App: `src/SBM.App/bin/Debug/net10.0-windows/SteelBridgeModeler.exe` (loads the sample project on start).
- Release: `dotnet publish src/SBM.App -c Release -o dist/SteelBridgeModeler`
- Close-up of a cross frame: add `--closeup CF7 [--below]` (implies 3D).
- Visual check without a screen: `SteelBridgeModeler.exe --snapshot out.png [--3d] [--editors]` writes the window,
  the GPU viewport (`out_view.png`) and every editor tab, then exits. Normal screen capture of WPF shows blank in this
  environment â€” use `--snapshot`.
- Session log: `%LOCALAPPDATA%\SteelBridgeModeler\session.log` (messages + exceptions).

- Release: `dotnet publish src/SBM.App -c Release -r win-x64 --self-contained false -p:PublishReadyToRun=true -o dist/SteelBridgeModeler`
  and the same for `src/SBM.Cli` (same output folder â†’ `sbm.exe` beside the app).
- Automation docs for the user: `docs/API.md`; example scripts in `samples/scripts`.

## Projects
- `src/SBM.Api` â€” `SbmDocument` (open/new/save, typed + text property-path edits, incremental `Regenerate`, export),
  `PropertyPath` (paths like `Bridges[0].Supports[1].SkewDegrees`; unit-aware parsing; plain numbers use the field's
  display unit inferred from the property name via `KindOf` â€” keep that list in sync when adding inputs),
  `ScriptHost` (Roslyn C# scripting; globals Doc/Print/Ft/In/Sta/Len).
- Remote control of the running app: `SBM.App/MainWindow.Remote.cs` (named pipe `SteelBridgeModeler.<pid>`,
  CurrentUserOnly, one JSON line in/out, handled on the UI thread via the same VM methods as the ribbon) â†”
  `SBM.Api/SbmApp.cs` client, `sbm app â€¦`, script global `App`. Not started in `--snapshot` runs. Add new remote commands
  in `HandleRemote` + `SbmApp` wrappers + docs/API.md.
- `src/SBM.Cli` â€” `sbm.exe` command line over SBM.Api (new/info/get/set/add/remove/import-landxml/regen/export/run/bench/
- Companion database: `SBM.IO/Database` (`ModelDatabase` writer + schema, `PlanSheets` sheets/link rules/reports, `ModelDb` reader, `CompanionDatabase`
  orchestration; `IfcKeys` = the key patterns behind the IFC GlobalIds, shared with `IfcExporter` — never change a pattern). Compact layout `el` +
  `strings` + `props`, readable VIEW `elements`; groups (no IFC entity) at the plans' granularity; links on groups/assemblies, children inherit
  (`v_element_sheets`). `tools/plan_split.py` splits plan PDFs. App: `MainWindow.Database.cs` + `SBM.App/Database/` (panel, preview window).
  Keep `ModelDatabase.Builder` numbering in step with `IfcExporter` (test `GlobalIds_AreTheIfcGlobalIds`). CLI `sbm db`, docs in docs/API.md.
- `src/SBM.Cli` — `sbm.exe` command line over SBM.Api (new/info/get/set/add/remove/import-landxml/regen/export/run/bench/
  elevations/drawings/sheets).
- `src/SBM.Core` â€” no UI. All geometry and the parametric model.
  - `Geometry/` Vec2/Vec3 (double), Box2, Numerics (Gauss-Legendre, Brent, FindRoots, line intersection).
  - `Alignments/` `HorizontalElement` = one class for line/arc/clothoid (linear curvature K0â†’K1, partial spirals OK);
    `Alignment` (distance-based internally, station APIs public, station equations, Inverse = pointâ†’sta/off);
    `PiDefinition` (PI table â†’ exact elements incl. asymmetric spirals; `TryExtract` recovers PIs from elements);
    `VerticalProfile` (sym/unsym parabolic VCs); `CrossSlopeTable` (station rows, pivot offset = the
    profile grade line the profile is given on and the slopes pivot about, constant `PivotOffset` or OPTIONAL station table
    `PivotOffsets` (linear, held beyond the ends); always read it with `PivotOffsetAt(station)`; profile stations = alignment
    stations; the elevation check prints "on the PGL x RT of the alignment").
  - `Bridge/BridgeDefinition.cs` â€” the parametric input (supports, girders, deck, barrier, cross frames, stiffeners, splices).
  - `Framing/FramingGenerator.cs` â€” pure function definition+alignment â†’ `BridgeFraming` (bearings, girder curves,
    cross frames, stiffeners, splices, deck outline, diagnostics). Parallel per girder / per bay.
    `GirderSpanCurve` is the per-span girder geometry (curved-parallel or chorded) with an arc-length table.
  - `Drawing/` renderer-neutral layered linework (`PlanDrawing`) built by `PlanDrawingBuilder`; shared by viewport and DXF.
  - `Solids/Mesh.cs` â€” Frame3, indexed triangle Mesh (volume relative to a mesh vertex â€” origin-based sums lose all
    digits at UTM coordinates and made masses/tonnage garbage; bounds), MeshBuilder (AddLoft = sweep of a 2D section
    through 3D rings with auto-orientation, extrusions, prisms, fillet welds, ear-clipping, convex hull).
  - `Solids/ExtrudedSolid.cs` â€” exact recipe a builder records on the `Mesh` (`Mesh.Solids`/`HasExactSolids`): planar
    polygon + round holes swept along a vector. `AddLoft` (straight sweeps â†’ boxes, prisms, angles, welds), `AddPrism`/
    `AddPlanar` (affine maps) and `AddLongPlate` (greedy merge of flat, constant-thickness strips) record them; IFC writes
    IfcExtrudedAreaSolid + IfcArbitraryProfileDefWithVoids (IfcCircle holes) when they cover every triangle and are
    smaller, else a merged-vertex IfcTriangulatedFaceSet. New solid builders should record recipes too.
  - `Geometry/ArcFit.cs` â€” sampled bar/strand centerlines â†’ lines + arcs (â‰¥4 points per arc, radius â‰¤ 5 m, sweep â‰¤ 90Â°,
    0.5 mm) for IFC IfcIndexedPolyCurve (IfcArcIndex). Spirals stay true helices in the model (32 samples/turn, exact
    length; close end turns climb 1 db/turn); IFC has no helix, arcs stay within 0.5 mm. Long curved deck bars stay
    polylines (IfcOpenShell mis-sweeps flat 3D arcs).
  - `Solids/PlateBuilder.cs` â€” plates WITH HOLES: polygon+holes triangulation (horizontal bridge to the +x ray hit â€”
    don't change to a vertex bridge; that crossed unmerged holes and caused the bearing-stiffener artifact), long girder
    plates built in strips/cells (â‰¤1 hole per cell keeps it linear), Sutherlandâ€“Hodgman half-plane clip.
  - `References/` â€” reference model definitions, `TerrainModel` (grid index elevation queries, parallel contours).
  - `Substructure/` â€” `SubstructureDefinition` lives on each `SupportDefinition.Substructure` (null = defaults by type via
    `ResolvedSubstructure`). `SubstructureGenerator` runs AFTER steel (needs `SteelModel.BearingSeats`), per support in
    parallel: cap coordinates S (along the skewed support line from the alignment point, right +), N (normal, up-station +).
    Cap top = shape g(S) + c with c set so min pedestal = MinPedestalHeight; pedestals take the rest (variable).
    Plan pedestal sizes (OPTIONAL): `PedestalSizeAlong/Across` (S x N, centred on the bearing footprint; 0 = footprint + `PedestalProjection`) and `PedestalShared` (ONE block per girder
    line carries the back + ahead seats, top = highest seat, differences reported). Set by `piers-i80.csx` / `piers-ramp-g.csx` (not 6A, 11WB, 9G, 19G: sizes not settled).
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
    Battered piles (`SubstructurePiles.cs`): `FootingPoint.BatterDirS/N` (unit plan direction of the tip movement, 0 = plumb) +
    `FootingDefinition.BatterRatio` (4 = 1H:4V; diagonal batter measured along the diagonal); `FootingDefinition.BatterPerimeter`
    sets the plan pattern (perimeter out, optional S edges, 45 deg corners). Plan positions are at the FOOTING BOTTOM plane; length
    along the axis from the head; head/tip cut horizontal; section = plumb orientation rotated minimally onto the axis (oblique
    extrusion = exact IFC solid). `PileInfo` has head (S,N,TopZ) + tip (TipS,TipN,TipZ). Axis-distance clearance warnings
    (`CheckPileClearances`). Snapshot `--closeup FTGn@Bridge` frames the foundation. Conventions are flagged for user confirmation.
  - Footing dowels / column splices (`SubstructureReinforcementDefinition.ColumnSplice` = Continuous | Lap | Coupler; `SplicedVerticals` in SubstructureRebar.cs): L-shaped
    dowel per column bar (`DowelL`, mark series "L", scheduled length = A + B as the plans list, hook on the bottom mat `DowelBottomAboveFooting`), column bars start at the
    footing top (lap: dowel projects `ColumnLapLength`, in contact on the inside) or meet it at `CouplerLow/High` (generic coupler cylinder = `SubElementKind.Coupler`,
    IFC IfcMechanicalFastener COUPLER). `ColumnSplicePattern` = one letter per bar, counter-clockwise from +S (H upper / L lower level / T lower + stops short, length
    `ColumnTerminatingBarLength`), read from the plan sections' bar symbols; `ColumnBarEmbedInCap`, `ColumnMidLapStart` (9G). Data + comparison: `samples/plans/column-rebar.csx`.
  - Pile web orientation: `FootingPoint.WebDirS/N` (own) or `FootingDefinition.PileWeb` (Default = web along N; AlongBatter = battered piles web along the batter direction, as the
    plan pile symbols show); `PileInfo.WebS/WebN`. Plan data: `samples/plans/pile-web.csx` (and the PileWeb lines in the pier scripts).
  - Plan bar lists (OPTIONAL, `SubstructurePlanBars.cs`, inputs `PlanBarGroup` in Bridge/PlanBars.cs): `FootingDefinition.Bars` (mats with hooks, 1" clear above the pile heads, side bars, 8f6
    pile uplift V-bars through the web holes of `UpliftPiles`) and `SubstructureReinforcementDefinition.CapBars` (longitudinal rows, side bars, hoops with the height solved from the
    listed length, cantilever hoops, ties, hairpins, ends, c7, pedestal bars; all inside the lower, narrower cap section). Counts / sizes / lengths = the plan bar list so weights reproduce it;
    positions follow documented simple rules (zones and sketches not all decoded). Data generated from the plan bar lists: `samples/plans/pier-rebar-i80.csx`, `pier-rebar-ramp-g.csx`.
  - Plan BEND DIAGRAMS (`SubstructurePlanShapes.cs`; `PlanBarGroup.Shape/Dims/Pin`, hairpin `ZoneStations/ZoneMargin`, skin-bar `End`; abutment `PlanBarLine.Shaped`): plan convention is
    "ALL DIMENSIONS ARE OUT TO OUT. D = PIN DIAMETER" and the bar-list length = the plain SUM of the out-to-out segments (no bend deductions). `PlanBent/PlanU/PlanL/PlanA14/PlanHoop` build
    the `BarShape` (Dimensions = printed values, centerline = outline inset db/2, printed pin, FixedLength = listed) and the sharp 3D centerline; marks: 5m1/5n1 pedestal U, 5a13/6a14 end U,
    5a14/6a15 end bars in the longitudinal plane (leg + top run + slanted leg along the raked face), 5c5/5c6 (5c3/5c4) hairpins = U across the width (3 1/2" clear at both faces) hanging from the top
    in the plan's zone order, 4c7/4c5 U, 11g/10g bottom mats U, 6c1/6c2/5n2 hoops with lap tails (`PlanHoop`), 6b2 cantilever flat + slant (`CS`), 5a11/5a12 short end bars. Data is appended to the
    csx lines as `;S=shape:dims:pin` (+ `;Z=..;M=..` hairpins, `;E=-1/1`) by `tools/patch_*.py` from `tools/extract_all.py`/`shapes.py` (plan text + fraction glyph decoder `plantext.py`);
    plan vs model for every mark: `docs/plan-data/bend-diagram-comparison.md` (`tools/compare.py`, `make_doc.py`; abutment diagrams hand-read in `tools/abut_hand.py`). Bars are never moved to
    satisfy cover - containment violations are raised. `LateralSetback` continues each relief line level beyond its ends (the plans' arcs start at the cap tips) and counts each
    face's own layer lines once (`SubstructureUnit.ReliefLineTags` = face + layer per line). Pedestal U bars 5m1 / 5n1: base in the TOP of the plan pedestal block (2" under it; 5m1 one
    5n1 bar lower), legs through the block into the cap; 5n2 hoops round the blocks of the note's girders (`PlanBarGroup.Girders`, csx `;G=C,D`), else UNRESOLVED at mid-cap;
    `PlanBarGroup.End` also picks the end of CapEnd groups (5WB 5a15 shape `A15` left, 5a14 right). Check: pedestal blocks are host concrete for pedestal bars, hairpins need 3 1/2" CL.
    to both full-width faces (`HairpinFaceClear`, sections A-A/B-B), per-bar records `SubstructureUnit.CoverViolations` (`samples/plans/check-sub-cover.csx`); textured-face cover for
    other bars UNRESOLVED (2" kept). State and remaining warnings: `docs/plan-data/cap-cover-containment.md`.
  - As-designed abutment (OPTIONAL `SubstructureDefinition.Abutment`, `AbutmentDefinition.cs`; null = parametric abutment): `SubstructureAbutment.cs`
    (`AddSlab` = prism between two vertical planes with per-vertex elevation functions; end lines are alignment offsets, so S depends on N),
    `SubstructureAbutmentRebar.cs` (only proven placements in 3D, rest of the plan bar list = schedule-only marks, mismatches = warnings),
    `AbutmentViews` (3 sheet panels; the plan draws `AbutmentGeometry.FootingOutline` = generator outline with notches), `SubElementKind.Wingwall/Counterfort/Maskwall/WingFooting`,
    `unit.Abutment` (`AbutmentGeometry`). Backwall runs behind the wings to the maskwalls; `CorbelLeft/RightOffset` limit the corbel to the wing inner faces. Standing rule:
    no invented defaults - unresolved plan items are generator warnings ("UNRESOLVED"). Docs `docs/plan-data/abutments-model.md`; scripts `samples/plans/abutments-*.csx`.
  - Cap section step / aesthetic relief (OPTIONAL, `SubstructureDefinition.Aesthetics` = `CapAestheticsDefinition`, as-designed caps only): the
    cap is W above the relief curve and W - 1'-0 below (StepSetback 6" per face, BOTH faces). The cap solid itself is the narrower core; `BuildCapStep`
    adds full-width strips above each layer line (`Layers` = BandChains, lowest line first; plans: fan arcs R5, R4, R3, 2" each, drafted 7 deg) as a
    separate Cap-kind SubSolid ("... cap section step"); lines go to `SubstructureUnit.ReliefLines` (bent elevation view). `CapBandBuilder` builds the
    tangent-arc S-curve from the plan tables (`FromTable`) or the explicit figures (`FromLongArc`). Cap bars are NOT adapted (warning). Data:
    `samples/plans/pier-aesthetics.csx`, reading in `docs/plan-data/pier-cap-aesthetics.md` (Confirmed section). Texture itself / Detail A are not modeled.
  - `Drawing/Views/` â€” 2D views as `PlanDrawing`s (Z = 0) via `Drafter` (paper-inch sizes Ã— scale; dims with ticks,
    leaders, hatch, elevation tags, AWS weld symbols; ALL text is vertically middle-aligned â€” DxfWriter writes 72/73
    accordingly; "\n" in text = multi-line). Keep annotation text short/multi-line: paper-sized text does not shrink
    with scale, so long single-line notes make a view unfittable. Shell heredocs mangle backslashes â€” write patch
    scripts with the Write tool.
    `LayoutViews` (developed plan over elevation: X = station; plus true plan), `SectionViews` (typical section),
    `BentViews` (per unit; abutment at begin is mirrored to look from the span), `BarSheet` (paper inches, PaperSpace=true),
    `DrawingSet.Build`. DXF via `SBM.IO.Dxf.SheetExport` (2D polylines, circles, LTSCALE from scale). App:
    `Editors/DrawingsWindow` + `Viewport/DrawingCanvas` (also `RenderToPng` for `--snapshot ... --editors [--dwgzoom x0,y0,x1,y1]`).
    `--closeup SUBn` = 3D close-up of substructure unit n.
  - `Drawing/Sheets/` â€” `SheetComposer` (fits each `Panel`'s `ViewSpec` at the largest standard scale that fits â€” views are
    REBUILT per scale so paper-sized text stays right â€” then places it on a sheet with border + title block; sheet
    drawings are paper meters, 1" = 0.0254), `SheetSet.Specs/Build` (the plan set). New views: `GirderViews` (developed,
    depth schematic), `SteelDetailViews` (cross frames from `SteelModel.CrossFrameDetails` = 2D geometry recorded by the
    detailer â€” keep it in sync when changing CrossFrameDetailer), `BearingViews` (anchor bolts from `BoltKind.Anchor`
    instances; 3D clearance checks vs cap bars). `SBM.IO.Pdf.PdfWriter` = minimal vector PDF (Helvetica, print colors via
    `PrintStyle`, also used by the app's paper preview).
  - Sheets (2026-10 additions): `Views/LabelPlacer` = generic label collision avoidance run on every non-table view (SheetComposer per panel, DrawingSet);
    dimension texts are placed first, other texts move perpendicular to their baseline / along it to the nearest free spot with a leader; title and grid texts are fixed; paper-space
    table views (bar lists, notes, data tables) are skipped. `PlanDrawing.Extents` uses `LabelPlacer.WidthInHeights` (Helvetica widths). New views: `FramingViews` (steel framing plan,
    developed, spans grouped by `SpanGroups`), `SpliceViews` (distinct splice types from the generated FS assemblies: plate silhouettes + bolt positions in the girder frame, schedule),
    `CrossGirderViews` (front-face outlines of the generated XG parts), `PierViews` (cap plan with pedestals/columns/column heights, footing + pile layout in S/N), `TableViews` (paged
    tables in paper inches: bearing data, girder spacing, top of slab / top of web, cross slope, camber and blocking, estimated quantities; `Paged(..., blocks)`).
    Notes and callouts that quote numbers MUST come from the model: cross frame bolt notes from `CrossFrameDetail2D.Bolts` (`SteelDetailViews.BoltGroups`), steel notes from `Steel.Parts[].Material`.
    Plan comparison: `tools/detail_inventory.py` -> `docs/plan-data/detail-inventory.md`; `sbm compare-sheets` / `sbm qc-sheet` -> `tools/compare_sheets.py` (PyMuPDF; qc rows keyed `gen:<bridge>:<S-n>`).
  - App 3D: `MainWindow.Viewport3D.cs` â€” camera depth range follows the scene (`KeepCameraInRange` on CameraChanged; do
    not hand-set Near/Far elsewhere); section cut = CPU clipping of all solids/lines/text (Helix
    `CrossSectionMeshGeometryModel3D` was tried: transparent models stop rendering when it is in the scene â€” don't use
    it); picking via `SceneBuilder.Picks` (triangle range â†’ source object) and `InstanceTags`; clipped meshes map
    triangles back via `_triMap`. Hide/unhide: `_hidden` (tags) filters merged meshes by pick-table ranges and
    instanced meshes by `_visibleInstances`, through the same ClipMesh/ApplySection path as the section cut; `RemapHidden`
    re-matches hidden tags after regeneration via `HideKey`. Right-click (no drag) = context menu. Sheets mode: `MainWindow.Sheets.cs`. Snapshot options: `--section STA[,skew]`,
    `--pick fx,fy` (logs PICK: properties), `--sheets`.
  - `Prestressed/` â€” `TxGirderCatalog` (Tx28â€“Tx70 as PGSuper/WBFL precast I-beam dims `PrecastIBeamDims`; `TxGirderShape`
    outline = WBFL PrecastBeam polygon; IGND grid; IGEB pad table), `PrestressedGenerator` (one straight beam per span Ã—
    girder between the framing's support-line points; IGD/IGEB ends; strands; pads; `BearingSeat`s with Location so the
    substructure gets two pedestals per girder at bents). `GirderLayoutDefinition.Kind` switches steel â†” prestressed:
    FramingGenerator treats the beam top as "top of web" (flange t = 0, web = D), forces chorded, skips cross frames,
    stiffeners and splices; `RegenerationCache` runs PrestressedGenerator instead of steel (`BridgeModel.Prestressed`).
    Views: `PrestressedViews` + branch in `SectionViews` / `SheetSet`. IFC: IfcBeam + IfcTendon (validated 0 issues).
  - `SBM.IO/PgSuper/` â€” `PgSuperReader` (.pgs BridgeDescription schema v6+ or ReportExportAgent export XML; strand key
    bits per WBFL PsStrand; end distances normalized to CL-bearing-to-end) and `PgSuperImport.Apply` (onto an alignment).
    The user's PGSuper/BridgeLink tooling lives in `Documents\PGSuper-master\copilot` (agent, MCP) â€” reuse its export.
  - `Checks/ElevationCheck.cs` â€” transparent elevation chains (`CalcStep` rows: profile via `VerticalProfile.Explain`,
    cross slope, slab, haunch, web, flange, sole plate, bearing stack) reconciled against framing + `BearingSeats`.
    MUST mirror `BearingDetailer` math; tests fail if they drift. Below the seat, `Checks/ElevationCheckSubstructure.cs`
    (`AddSubstructure`) continues the chain: pedestal (plan size / shared block top = highest seat) or as-designed abutment
    beam step → cap top from the `CapProfile` polygon upper envelope (stepped `CapProfileAhead` on the ahead half, cap
    section step as an info row) → cap bottom → soffit at the nearest column (= column top) → flare → footing / shaft →
    pile head. Each row states its input; values are compared with the `SubstructureGenerator` unit (MUST mirror it;
    `ElevationCheckSubstructureTests`) and exposed as `ElevationChain.Substructure`. Input differences (seats that differ
    on a shared block, seats beyond the cap, step top vs seat) go to `ElevationChain.Reported` — never adjusted. Shared
    piers built by another bridge are found via `others` (`SbmDocument.BearingElevations` passes the other bridges' units).
    Plan reconciliation: `samples\plans\check-substructure-chains.csx`.
  - `Project/RegenerationCache.cs` â€” per-bridge hash cache used by app and API.
  - Steel generator phases: layouts â†’ connections (cross frames, field splices, lateral bracing register bolt holes
    in `_holes` / `_stiffHoles` / `_studExclusions`) â†’ members (girder plates, stiffeners, bearings, studs). Files:
    `SteelModelGenerator.cs`, `CrossFrameDetailer.cs`, `FieldSpliceDetailer.cs`, `LateralBracingDetailer.cs`,
    `BearingDetailer.cs` (elastomeric / disc / GENERIC = vendor-designed: sole plate + one body sized so the assembly is
    `GenericHeight` at the CL bearing + masonry plate sized from the anchor spacing + optional preformed pad; anchors as
    given). Per-support bearing override `SupportDefinition.Bearings` (`BridgeDefinition.BearingsAt(j)`) and per-girder
    fixity `GirderFixities` (`FixityOf(g)`). Shared helpers on `BearingDefinition` (SoleAtCl, GenericBodyHeight, Footprint)
    keep BearingDetailer, ElevationCheck and BearingViews in step; steel `BearingSeat`s carry the footprint.
  - `Concrete/` â€” `ConcreteGenerator` (deck slab grid solid between skewed end lines following finished grade,
    haunch sweeps, barrier sweeps, deck mats, 1% negative-moment bars, TxDOT SSTR Bars S/U/R), `BarrierProfiles`
    (SSTR per RL-SSTR Sept 2019 â€” 3/4" back-face step is an assumption), `Rebar.cs` (A615 sizes, AASHTO 5.10.8 Class B
    laps, `BarShape` centerlines + bend-diagram dims, `BarMark` incl. "varies" marks). Mark rules: â‰¥4 identical =
    own mark; straight bars within `MarkLengthTolerance` snap to the cluster's SHORTEST length; odd closures grouped
    into "varies" marks (â‰¤3 ft range). Negative-moment bars are one length centered on the pier. Equal-piece splits
    except staggered longitudinals (stock 60 ft + closure).
  - `Steel/` â€” `Sections.cs` (AISC angle/channel catalog, bolt dims + length rounding), `SteelModel` (parts,
    assemblies, bolt instances, bearing seats), `SteelModelGenerator` (girder plates swept along `GirderPath`, plate
    changes with 1:2.5 width tapers, field-section split at splices, flange-web welds, stiffeners in their skewed
    planes, bearings) and `CrossFrameDetailer.cs` (frame-plane coords x/z/v; single angles on gusset faces, gussets =
    convex hull of bolt group + member laps, bolts through connection stiffener + gusset, X filler + crossing bolt,
    K/inverted-K center gussets, channel diaphragms). Trim() computes member end cuts from half-plane clearances.
  - `Project/` `BridgeProject` (JSON *.sbm via DTOs â€” keep the file format stable, bump `CurrentVersion` on breaking
    changes), `SampleProject`.
  - `Units/UnitSystem.cs` â€” the ONLY place display units live. Internal units: meters, radians (skews stored in
    degrees), slopes rise/run. Parses ft-in (12'-6 1/2"), stations (12+34.56), DMS, %.
- `src/SBM.IO` â€” `LandXml/` reader (namespace-agnostic; Line/Curve/Spiral, StaEquation, ProfAlign PVI/ParaCurve/
  UnsymParaCurve/CircCurve, Superelevation critical stations) and writer; `Dxf/DxfWriter` (ASCII R12, 3D polylines);
  `Ifc/` StepWriter + IfcExporter (IFC4X3_ADD2, IfcBridge/IfcBridgePart/IfcElementAssembly/IfcPlate/
  IfcMember/IfcMechanicalFastener (typed by IfcMechanicalFastenerType per size holding the representation map; occurrences = own
  IfcLocalPlacement + the type's SHARED IfcProductDefinitionShape â€” a shape representation may not be shared, a product
  shape may)/IfcFastener WELD/IfcBearing, IfcMapConversion base point,
  deterministic GlobalIds; a *.ifczip path writes a buildingSMART IFC-ZIP = one DEFLATE entry <name>.ifc, streamed). NOTE: in ADD2 IfcFacilityPart is abstract â€” use IfcBridgePart(..., UsageType, PredefinedType).
  Validate with IfcOpenShell (`pip install ifcopenshell pytest`; `ifcopenshell.validate.validate(f, log, express_rules=True)`);
  last result: 0 issues.
- `src/SBM.App` â€” WPF (Fluent dark theme), MVVM via CommunityToolkit.Mvvm, GPU viewport = HelixToolkit.Wpf.SharpDX 3.x
  (DirectX 11, System.Numerics types; namespaces `HelixToolkit`, `HelixToolkit.SharpDX`, `HelixToolkit.Wpf.SharpDX`).
  - `Viewport/SceneBuilder` batches each layer into one LineGeometry3D + one BillboardText3D; vertices are rebased to a
    local origin (state-plane coords exceed float precision). Model-sized text (FixedSize=false).
  - `Editors/` input windows built from `FieldBase` rows (TextField/ChoiceField/BoolField/SpacingField/StringField with
    optional validator + AllowEmpty/LengthListField = comma list of lengths, blank = null) and DataGrids of
    `CellValue` (unit-aware text cells). Apply = regenerate without closing.
    Bridge editor tabs beyond the basics: Substructure (support combo = supports THEN auxiliary supports; pedestal plan
    sizes S/N + shared block, chamfer / flare column geometry, per-face column bars, chamfer positions, hoop overlap,
    crossties, flare cage, ColumnSplice Lap/Coupler + pattern), Aux. Supports (AuxiliarySupports grid + Girders.Beginnings
    grid; renaming an aux support carries its beginnings), Deck Ends & Joints (first/last support: ExpansionJoint +
    EndBlock; explicit Soffit polyline is kept, script only), Deck & Barrier (Deck.EdgeStrip; Width 0 = null).
    `SupportRowVm.Source` keeps the loaded SupportDefinition so inputs WITHOUT an editor (SharedPier, GirderFixities,
    DeckEndPoints, EndSupport/EndSplice/EndOffset, PierCenterOffset, Bearing...) survive Apply; build new support inputs
    from it, never `new SupportDefinition`. Snapshot: `--editors [--editbridge NAME] [--editsupport NAME]` picks the bridge
    and the support shown on Substructure / Deck Ends; long tabs also render scrolled pages `_bridgeN_p1..`.
    Still script/API only (no editor): abutment / wingwall / counterfort detail + abutment rebar, cap profiles + aesthetics,
    CapBars, cross-frame styles / bolting, field splice plate details, GirderBeginning cross-girder details,
    Girders.CrossGirder / PlanGeometry / Terminations, inspection access, drains, lighting, sign supports, misc items,
    DeckPlanReinforcement families / end dam, deck edge tables, prestressed strand layout, pile web / batter / uplift.
  - Header readability: App.xaml defines explicit TabItem (`SbmTabItem`) and DataGridColumnHeader templates â€” do NOT
    use `BasedOn="{StaticResource {x:Type TabItem}}"` (resolves to the classic light template). TabItem Foreground is
    inherited by tab content, so it must stay light. All windows get dark title bars (DWM) and a light default
    Foreground in App.xaml.cs. Any custom-templated style without BasedOn (RibbonButton, ToolIconButton) MUST set its
    own Foreground â€” the user saw black ribbon labels on screen even though `--snapshot` rendered them light.
  - Command search (Ctrl+K) in `MainWindow.Commands.cs` auto-collects every ribbon button (label, tab, group, tooltip).
  - `ViewModels/MainViewModel` â€” project, undo/redo (JSON snapshots), async regeneration (Task.Run + version counter).

  - Measure / query / snaps (App): `MainWindow.Measure.cs` (tools Measure, Measure Angle, Station/Offset query; overlay Canvas
    `MeasureOverlay`, panel `MeasurePanel`, snap bar; remote `measure` / `station-offset`; snapshot `--measure fx,fy,fx,fy`,
    `--query fx,fy`, `--snapbench N`), `Viewport/SnapEngine.cs` (`SnapIndex` = BVH over triangle/segment/instance chunks, built lazily
    after each scene rebuild; ranked candidates + occlusion test), pure helpers `SBM.Core/Geometry/SnapMath.cs`, `PointQuery.cs`.
    Query cost control: best-first BVH traversal, search radius shrinks to (K-th best score + max bonus) px once K=10 candidates are known
    (exact), 4 ms traversal budget, feature-edge checks deferred to ranking. Index is debounced (one build per scene burst; prebuilt once at startup).
  - Near-view rebar LOD (`MainWindow.RebarLod.cs`, `Viewport/SmoothRebar.cs`): zoomed in (width < 60 ft) and settled, bars within 20 ft of the view-center ray become smooth
    lofted tubes (parallel transport, bends <= 10 deg/step, 10 sides) in an overlay model per rebar layer; the same bars are skipped in the light tile/instanced models via
    `_lodSkip` (flows through ApplyToModel / HiddenRanges). Settings in static `RebarLod`; snapshot `--nolod --lodradius --lodwidth --width --noterrain --framebench --pickafter --hideafter`.
- Units/shared piers: `SupportDefinition.SharedPier` + `PierCenterOffset`; `RegenerationCache` is two-phase (superstructure
  per bridge, then substructure per bridge with `ForeignSeats` of other bridges on the owner's shared piers).
- Discontinuous girders: `GirderLayoutDefinition.Terminations`; `FramingGenerator.ApplyTerminations` truncates the girder,
  nulls its bearings beyond, adds `CrossGirderResult`; cross-frame bays are (Bay, RightGirder) pairs â€” never assume
  RightGirder = Bay + 1. Steel `BuildCrossGirder`.
- Girders beginning on their own support line: `BridgeDefinition.AuxiliarySupports` (SupportDefinitions whose CL bearing
  line does NOT split spans; framing index = Supports.Count + k) + `GirderLayoutDefinition.Beginnings` {Girder, Support name}.
  `FramingGenerator.ApplyBeginnings` cuts the girder at (bearing âˆ’ GirderEndExtension) and RE-BASES its path (s from 0;
  `_startExt`/SpanStart shrink, span curves unchanged), nulls bearings before it, puts the start bearing on
  `BridgeFraming.AuxSupports` (`BearingResult.PathDistance` set). Use `BridgeFraming.SupportLine(i)` /
  `BridgeDefinition.SupportAt(i)` for any index from a bearing, frame or substructure unit â€” never index `Supports` directly.
  Support cross frames and bearing stiffeners are built on aux lines; substructure units, elevation check, bent/bearing/
  girder views include them. Data example: `samples/plans/pier-6a.csx` (I-80 Pier 6A).
- Per-span girder layouts (OPTIONAL): `GirderLayoutDefinition.SpanLayouts` (`Bridge/GirderSpanLayout.cs`) = per listed span, straight chord
  girders between their points on a begin and an end line (`GirderSpanEnd`: the support line moved parallel by `LineShift` along the
  `ReferenceOffset` curve, e.g. "CL brg 1'-0 from CL pier along CL NB"; offsets `NormalToAlignment` or `AlongLine` from the reference
  point (W.P.), optional `Spacings`, prestressed `Haunches` per girder = plan T - slab). `PerpendicularSpacings` = girders parallel to
  the first one's chord at spacings measured perpendicular to it. Same girder count in every span (else an error). It overrides the
  offset curves and `PlanGeometry` in that span (warning). `FramingGenerator.SetupSpanLayouts` -> `_chord` (support-line points S0/S1,
  layout points L0/L1 with the prestressed top of beam); `GirderSpanCurve` gets `LayoutBegin/LayoutEnd`. Back and ahead points at an
  interior support may differ (prestressed): `SupportLineResult.Bearings` = behind, `AheadBearings` = ahead (null if equal). Use
  `BridgeFraming.SpanBearing(girder, span, atEnd)` for anything that belongs to one span. For steel a jog is an error (continuous girders).
  The prestressed elevation check names the layout. Data: `samples/plans/build-woodbury.csx` / `check-woodbury.csx` (Iowa Design 817).
  OPEN: `PrestressedGenerator.BuildBeam` still uses `Supports[j].Bearings` (it must switch to `SpanBearing`).
- As-designed girder plan geometry (OPTIONAL): `GirderLayoutDefinition.PlanGeometry` = per girder a start point (alignment station/offset),
  start direction (degrees from the alignment tangent, + toward the right) and a chain of tangent / left / right arc elements (radius, length;
  first extended backwards, last forwards) = the plan's "P.C. STA ..., R = ..." data. `FramingGenerator` builds a `GirderPlanCurve` and uses
  its normal offset `OffsetAt(d)` for that girder INSTEAD of the offset-at-supports curve (those table values are then ignored for it, except
  validation); bearings, spans, cross frames, splices, elevations follow. Data: `samples/plans/girder-geometry-i80.csx` (run after
  girders-i80.csx / pier-6a.csx, which it supersedes for I-80 U2 A-E and U3 A-C), `docs/plan-data/girder-geometry-i80.md`.
- As-designed cross frames (OPTIONAL; without them the automatic layout + definition angles are used):
  `CrossFrameDefinition.Styles` (`CrossFrameStyle`: InvertedK/KFrame/XFrame/Strut/PlateDiaphragm; top chord angle or W
  bolted through its web; bottom chord angle, channel, "2-" back-to-back channels straddling the gussets, or WT (stem on
  the gussets); per-style gusset, bolt dia/columns/gage/rows/pitch/edge, work points, bottom bolt offset, tie plates),
  `IntermediateStyle`, `SupportStyles[support name]` ("none" = no frames on that line), and `Lines` (`CrossFrameLine`:
  cumulative distance along a reference girder from its first bearing, radial to the alignment or normal to the girder,
  optional `Bays` list and `BayStyles`). `FramingGenerator.BuildFrameLines` crosses every girder in every span (6"
  bearing snap), frames only in bays crossed within one span; spans touched by lines skip the automatic layout. Detailer:
  `Member` = angle/channel(pair)/W/WT, `BuildPlateDiaphragm`. Catalog lookups are exact (`FindAngle/Channel/WideFlange/
  Tee`); unknown sections are reported. Gap inputs (docs/plan-data/cross-frame-gaps.md "Implementation"): `CrossFrameDefinition.Links` (`CrossFrameLink`: frame
  between girder g1 at support / aux support line s1 and g2 at s2 - plane through the two CL bearings, E-F at Pier 6A / 5WB), `SupportBayStyles[support][bay]`
  (Type 6 in bay A-D at 14WB/15WB), `CrossFrameStyle.BottomBoltPitch`, `CrossFrameStyle.Unresolved` (plan items not dimensioned: raised once per bridge as UNRESOLVED
  warnings, nothing invented), `CrossFrameLine.Unresolved` (per-line plan conflicts / undimensioned items, raised with the line location; frames still
  generated as listed), per-girder lines at skewed piers (Girder = X, Distance = X's bearing distance, explicit Bays) and a warning for every listed bay a
  line cannot generate (`BuildFrameLines`). Plan data: `samples/plans/cross-frames-i80.csx`, `cross-frames-ramp-g.csx`.
  Rolled sections come from the embedded AISC Shapes Database v16.0 extract `Steel/AiscShapes.csv` (all W, WT, C, MC, L:
  dims, A, W lb/ft, kdes/kdet/k1, x/y, workable gages WGi/WGo; angle gages g/g1/g2 = AISC Manual Table 1-7A in code).
  Profiles (`AnglePolygon`, `ChannelPolygon`, `WideFlangePolygon`, `TeePolygon` via `RoundCorners`) have root fillets
  from kdes; C flanges sloped 16-2/3% (tf at mid free width); MC flanges parallel (slope not in the database); toes
  square (toe radius not published). Rolled parts weigh AISC lb/ft x length (`SteelPart.NominalWeightLbPerFt`).
- Cross-slope breaks: `CrossSlopeRow.LeftBreak/LeftOuterSlope` (and Right*) â€” slope beyond a distance from the pivot
  (gores) â€” plus any number of further breaks `LeftMoreBreaks`/`RightMoreBreaks` (`CrossSlopeBreak` distance+slope);
  `CrossSlopeTable.SidesAt` interpolates break by break (a missing break = neighbour's distance, unchanged slope) /
  `DeltaElevation`; the elevation check lists the segments. Plan checks: `samples/plans/check-top-of-slab.csx`,
  `check-top-of-bearing.csx`.
- Expansion joints (OPTIONAL, default off): `SupportDefinition.ExpansionJoint` (`ExpansionJointDefinition`) on the FIRST/LAST support of a
  bridge. `ConcreteGenerator` cuts the deck back by the blockout width D (separate `Ends(o, insetB, insetE)`; shelf if the blockout is shallower than the
  slab), builds the blockout concrete and a generic device envelope (`ConcreteModel.Joints`, viewport layer `3D-JOINTS`, material rows). IFC: blockout =
  IfcBuildingElementPart (aggregated into the deck IfcSlab), device = IfcDiscreteAccessory EXPANSION_JOINT_DEVICE + pset `SBM_Placeholder`. The support's
  `DeckEndOffset` stays the authority (the joint's own value is only a check). Plan data/confirmations: `docs/plan-data/expansion-joints.md`,
  `samples/plans/joints-i80.csx`, `joints-ramp-g.csx` (run after the build scripts).
- Deck end blocks (OPTIONAL, default off): `ExpansionJointDefinition.EndBlock` (`DeckEndBlockDefinition`: Depth, FlatLength, ChamferRun or explicit `Soffit` polyline)
  = the plans' "end dam diaphragm" (2'-2 deep, flat soffit then 45 degrees to the slab soffit, full deck width, girder ends cast into it). `ConcreteGenerator.BuildEndBlock`
  -> `ExpansionJointResult.EndBlock` (`DeckEndBlockResult.Probe`); the blockout is then built to its full plan depth. `Checks/DeckEndBlockCheck` (run in
  `RegenerationCache`) reports steel inside a block: girder ends/studs/W strut flange flush = INTENDED (Info), anything else = CLASH warning; nothing is moved.
  Block rebar: see `DeckPlanReinforcement.EndDam` below. Data/evidence: `docs/plan-data/deck-end-blocks.md`; scripts `joints-i80.csx`, `joints-ramp-g.csx` (DECK_END_BLOCKS flag). Snapshot `--closeup EBLKn[@Bridge]`.
- Kinked deck ends (OPTIONAL `SupportDefinition.DeckEndPoints` on the first / last support = the plan's break points of the end of slab as station / offset, left to right, monotone; first and last
  segments extend; null / < 2 points = the straight `DeckEndOffset` line): `Framing/DeckEndPolyline.cs` (`SignedDistance` = shortest distance, + into the deck; `Inset` = parallel offset with miter vertices,
  vanished middle segments dropped, end segments kept; `Clip` of a plan segment; `CrossOffsetCurve`). `DeckResult.BeginPolyline/EndPolyline` + `FromBegin/FromEnd` (use these, not `BeginLinePoint`, for
  distances from a deck end). Followed by the deck outline (break points are vertices), slab solid (extra columns through the break points, `VarColumns`/`DeckOffsets`), blockouts / end blocks / joint
  bodies (`EndD`, `PlanX`), transverse bar clipping (`ClipToEnds`) and longitudinal bar ends (`EndsVar`), `DeckRebarCheck`. `BeginLinePoint/Direction` stay the straight lines (gap checks, drains, views).
  Plan data: `samples/plans/deck-end-kinks.csx` (I-80 U2 at Pier 5WB / 6A, Ramp G U4 at Pier 19G incl. its straight right-edge flare as a `RightEdgeTable`); run before the deck-rebar scripts.
- Camber / blocking (OPTIONAL `GirderLayoutDefinition.Camber` = `GirderCamberDefinition` per girder: DatumPoint, Keys (plan point number -> support index / FS number /
  `GirderEnd`), Points (camber, steel and slab+barrier deflection)): `Framing/CamberBlocking.cs` -> `BridgeFraming.Camber` (per point: final top of web, girder-line haunch
  elevation H = grade - slab + slab deflection, no-load top of web N = web + deflections, BLOCKING = N - N(datum), camber from the no-load shape = N - chord(N) between splices).
  The model geometry stays the final shape. Points between key points are equally spaced along each girder. Girder properties, material report, DB girder-group properties
  (camber_table), `sbm blocking`. Plan data/results: `docs/plan-data/camber-blocking.md` (section 9), `docs/plan-data/camber/*.csv`; scripts `samples/plans/camber-i80.csx`,
  `camber-ramp-g.csx` (run after the girder/splice/termination scripts) and `check-blocking.csx`.
- Plan-driven deck reinforcing (OPTIONAL `DeckReinforcementDefinition.Plan` = `DeckPlanReinforcement`, Bridge/DeckPlanReinforcement.cs; null = the generic generator below stays the default):
  `Concrete/ConcretePlanRebar.cs` (partial `ConcreteGenerator`). `DeckTransverseZone` = equally spaced RADIAL lines along a deck edge from the begin deck-end line (`StartAfterPrevious`, `AnchorToEnd`,
  `BetweenZone` for bars between another zone's lines; edge paths extend beyond the deck ends), each line = lapped `DeckLinePiece`s (fixed stock pieces + a remainder), clipped by the end lines with
  `TransverseBeginClear/EndClear`. `DeckLongZone` = equal spaces between two reference lines (girder web line / edge / offset, margins, alternating layers) or explicit `Positions` (+ `PositionLayers`,
  `PositionsFromRight`), stock chains with laps; zones that abut between the same references form a RUN and their lines continue across zones (nearest line of the same layer) so bar counts follow the length
  of the lines. `PlanBarList` (+ `Families`, e.g. "6b") = the plan's bar list per unit: every placed mark is compared (counts exactly, weight 3%) with warnings for differences, rows without a placed bar become
  schedule-only marks (no 3D bar). Marks are the plan's (7a1, 6b1, 5j1...), the generic AssignMarks is bypassed. End dam diaphragm bars (`DeckPlanReinforcement.EndDam` = `DeckEndDamBar`,
  `Concrete/ConcreteEndDam.cs`): bend-diagram shape (open bar by its OUTSIDE outline in section coordinates X from the deck-end face / Drop below grade, or a hoop w x h with lap tails, built with
  `SubstructureGenerator.PlanBent/PlanHoop`) in the plane normal to the deck-end line, repeated per girder bay (`FromGirder`, `Spacing`, `PerBay`, `Overhang` from the exterior girder lines, girders found where
  their web lines cross the deck end); `Unplaced` bars count toward the mark (location "... not modeled - UNRESOLVED") with `Unresolved` raised; `DeckPlanReinforcement.Unresolved` = plan items not dimensioned,
  each an "UNRESOLVED (deck reinforcing): ..." warning. Placed: Ramp G 5d2-5d5 at Piers 5G/9G/14G; I-80 and the rest of the end dams are schedule only (reasons + pages in `docs/plan-data/deck-rebar.md` 5-6).
  `Checks/DeckRebarCheck` (run in RegenerationCache for every deck
  with bars): cover top/bottom, edges, deck-end/blockout distance, bars vs the highest steel surface (top flanges, splice plates, fillers) -> `ConcreteModel.Notes` / Diagnostics; end dam marks are checked
  separately (`CheckEndDams`: clear inside the deck-end concrete incl. the end block soffit, exact test against the top flange / splice plate triangles near the ends). Plan data and results:
  `docs/plan-data/deck-rebar.md`, `deck-bar-lists.csv`; scripts `samples/plans/deck-rebar-i80.csx`, `deck-rebar-ramp-g.csx` (run LAST, after the joint/deck-edge/girder scripts and `deck-end-kinks.csx`), `report-deck-rebar.csx`,
  `check-deck-rebar.csx`. The typical-section sheet text and the database group "deck reinforcing" (unit bar schedule in its properties, incl. schedule-only marks) follow the plan.
- Variable deck width: `DeckDefinition.LeftAt/RightAt(station)`; constant decks keep the old code path.
- Deck edge strip (OPTIONAL, default off, PROJECT-SPECIFIC to the Iowa plans): `DeckDefinition.EdgeStrip` (`DeckEdgeStrip` Width inboard from the edge, follows
  `LeftAt/RightAt`). `DeckDefinition.FinishedElevation(alignment, sta, off)` = `Alignment.ElevationAt` except beyond the gutter line (edge -/+ Width) on a side whose
  slope at the gutter line falls toward the edge: level at the gutter elevation (switches exactly at the slope's sign change; `CrossSlopeTable.Side.SlopeAt`).
  Everything that reads the DECK surface must call it, not `Alignment.ElevationAt`: deck solid (extra gutter columns, `VarColumns`/`StripLines`), framing
  (web tops, deck lines), haunches, rebar/barrier Z, drains, prestressed, ElevationCheck (`Edge strip level` row; pass the deck to `AddGrade`/`Grade`),
  section/bent/layout views, the top-of-slab check scripts. Set by `samples/plans/deck-edge-strip.csx` (run after the build scripts); data in
  `docs/plan-data/superelevation-8wb.md` section 5.
- Drainage: `BridgeDefinition.Drains` â†’ `SubstructureModel.Drainage` (built in SubstructureGenerator, needs terrain for basins).
- Raster terrain: `ElevationGrid` (full-res bilinear) + decimated display mesh; `TerrainModel(grid, mesh)`.
- Cross girders as designed (OPTIONAL fields on `CrossGirderSection`; defaults = the simple legacy cross girder): `CrossGirderDetailer.cs`
  (`BuildCrossGirder`, runs in the CONNECTIONS phase so the angle bolts register holes in the continuing girders' webs and in the
  discontinuous girder's web). Frame: x along the XG, -y = near side (the discontinuous girder's side), z = 0 at the top of the top flange.
  `TopBelowDiscontinuousWebTop` = top of XG 11" (Unit 2) / 8" (Unit 3) under the discontinuous girder's top of web; angles L5x5x1/2 centred in
  the web with a single bolt line in both legs; 3'-0 x 2'-0 R6" web opening each side of the discontinuous girder
  (`PlateBuilder.AddPlanarWithOpenings`, tessellated in IFC), far-side edge plates, near-side stiffeners, fillet welds.
  `GirderTermination.CrossGirderAngleDegrees` = plan angle girder to XG (CW from the ahead tangent to the left-to-right XG direction; null = square).
  Plan data: `samples/plans/cross-girders-i80.csx`. Girder webs are treated as perpendicular to the XG when locating holes (plan angles 85-90 deg).
- Inspection access (OPTIONAL `BridgeDefinition.InspectionAccess`, null = none; `InspectionAccessDefinition.cs`, `InspectionAccessDetailer.cs`, CONNECTIONS
  phase): 1/2" wire-rope cables (`InspectionCableDefinition` = girder face + begin/end support and offset from the CL bearing; rope 4'-6 above the
  web bottom, 3" off the web) on WT5x44 anchorages / intermediate supports (<= 200') and WT5x15 brackets at the cross-frame connection plates, bolted
  THROUGH THE WEB (holes registered); walkways (Type A: C7 stringers, Type B: L8x4x1/2) in every bay on chords between consecutive cross frame lines
  (`InspectionWalkwayDefinition` per bay: type, first/last line), ending 1" clear of cross girder webs. New `PartCategory.InspectionBracket/
  InspectionCable/Grating/Walkway*`, `AssemblyKind.InspectionCable/InspectionWalkway`, viewport layer `3D-INSPECTION`; IFC: IfcMember (USERDEFINED
  'CABLE_BRACKET', STRUCTURALCABLE rope, STRINGER, POST), IfcRailing HANDRAIL, IfcPlate USERDEFINED (GRATING, WALKWAY_PLATE), assemblies
  ACCESSORY_ASSEMBLY (IfcOpenShell: 0 issues). Database: group "Inspection access" with assembly categories `inspection cable` / `inspection walkway`,
  linked to the INSPECTION ACCESS PLAN / CABLE / WALKWAY sheets in `sheet-links-i80-rampg.json`. Not modeled: rope hardware, chains, walkway end
  connections, bent-plate posts at skewed frames; grating = one solid slab (mass from nominal weight). Data: `samples/plans/inspection-i80.csx`,
  `inspection-ramp-g.csx`. BoltInstance.Diameter is honored per instance (viewport, IFC fastener types, material list).
  Snapshot: `--closeup MARK@Bridge name` also selects one bridge for steel assemblies (walkway marks repeat in every bridge).
- Misc items (OPTIONAL, `BridgeDefinition.Misc` = `MiscItemsDefinition` + `DeckDrainDefinition.Trench`; null = none; `Bridge/MiscItemsDefinition.cs`, generator `SBM.Core/Misc/`
  `MiscItemsGenerator` (+ `MiscPierDrains`, `MiscOthers`, `MiscMesh`), result `MiscModel` hung on `SubstructureModel.Misc` and built in `RegenerationCache` after the substructure):
  Ramp G deck drainage (trench inlets positioned FROM THE GUTTER LINE = deck edge - EdgeStrip width, tube, grate, splash basin, 12" span collector at the printed
  deck-top-to-CL heights, pier joint collection systems at 5G/9G/14G/19G), I-80 sign truss brackets (steel + anchor bolts; post base plate = placeholder) and exit sign bases,
  crash cushion end posts, lighting (pole bases, junction boxes, conduit in the barrier). Rules: nothing is assumed - unknown plan values are nullable inputs / `Unresolved`
  strings raised as WARNINGS ("UNRESOLVED ..."); things by others are placeholders (`MiscElement.Placeholder`, IFC Pset `SBM_Placeholder` with the plan basis); plan bar lists are
  schedule-only `BarMark`s (`MiscBarRow`, `BarMark.Coating`). DRAIN CLEARANCE: every tube / drop pipe / collector / pier pipe is checked against the steel parts of its bridge
  (`MiscMesh.Clearance` = exact segment-triangle distance, convex ternary search) -> `MiscModel.Clearances` + CONFLICT (overlap) / TIGHT (< 1") warnings; drains are never moved.
  IFC (`ExportMisc`): IfcElementAssembly per drain / bracket / pole base, IfcWasteTerminal, IfcPipeSegment, IfcPipeFitting, IfcTank, IfcJunctionBox, IfcCableCarrierSegment,
  IfcFooting, IfcMechanicalFastener ANCHORBOLT, proxies for placeholders; deck-piercing inlets void the slab. Database: groups drainage / sign supports / crash cushion / lighting >
  assemblies > elements (`MiscItems` in ModelDatabase; keys `IfcKeys.MiscAssembly/MiscElement`), sheet rules in `sheet-links-i80-rampg.json`. Viewport layers `3D-DRAIN-*`,
  `3D-SIGN-SUPPORT`, `3D-CRASH-CUSHION`, `3D-LIGHTING`, `3D-PLACEHOLDER`. Plan data scripts (re-runnable): `samples/plans/drains-ramp-g.csx`, `signs-i80.csx`, `end-posts.csx`,
  `lighting.csx`; data/results/unresolved list: `docs/plan-data/misc-items-data.md` (section "Model").
  UNRESOLVED FLAG (general, reusable): `SBM.Core/Assumption.cs` `Assumption{Status="UNRESOLVED", Reason (what the plans lack + PDF page), Basis (what was assumed)}` on the element
  (`MiscElement.Assumption`; set with `Flag(name, reason, basis)`). Consumers: viewport layer `3D-UNRESOLVED` (all flagged elements, orange, removed from their normal layers; reason in the pick
  properties; isolate with the layer toggles), IFC Pset `SBM_Status` {Status, Reason, Basis}, database props `status/reason/assumed` + view `v_unresolved` (with QC status), one warning per reason.
  Plan dimension that governs over a derived one (piped drains 22-25: tube 1'-8 from CL girder A) is modeled and the disagreement raised as `CONFLICT (plan vs model)`.
- Plan-set test models: `samples/plans/*.csx` (Iowa DOT Designs 116/316); data in C:\SBMData (not in OneDrive).

## Conventions (bridge engineering)
- Offsets right-positive (Civil 3D). Skew from the radial line; positive = right end of support line up-station.
- Girder offsets are measured normal to the alignment; flared layouts via `OffsetsAtSupports` (linear between supports).
- HAUNCH = top of web to bottom of deck (user's convention). Top of web = finished grade âˆ’ slab âˆ’ haunch; top flange
  thickness only affects top-of-steel and stud length (= haunch âˆ’ tf + penetration, rounded). Webs plumb, flanges level.
- Intermediate cross frames, connection plates and intermediate stiffeners are NORMAL TO THE FLANGES (tilt with
  grade); support frames and bearing stiffeners are PLUMB. Connection/bearing stiffeners lie in the frame/support plane.
- Shop splices: 1:2.5 width and thickness tapers on the larger plate. Field splices: wider flange tapers (1:2.5) to the
  narrower at the joint; thickness differences â†’ fillers on the outside. Splices are NSBA-style (outer full-width +
  2 inner flange plates, 2 web plates); bolt quantities are inputs.
- Cross frames: Auto orientation = parallel to skew if skew â‰¤ 20Â° else normal (AASHTO 6.7.4.2); Auto arrangement
  staggers by bay when skew > limit; frames slide toward mid-bay to keep `MinDistanceFromBearing`.
- Default foot = international (0.3048); LandXML USSurveyFoot is honored on import.

## Roadmap (agreed with user)
1. âœ… Milestone 1: alignment import/create with cross slope; 2D framing plan with deck, stiffeners, cross frames.
2. âœ… Milestone 2: 3D steel (plates, skewed stiffeners, single-angle cross frames welded to gussets bolted to
   stiffeners with A325 7/8" bolts, welds, elastomeric bearings), material list, IFC 4.3 export.
   User's detailing preferences are the defaults (see memory). Known simplifications: holes not cut in solids,
   no camber, flange thickness transitions are square butt joints (no thickness taper), girder ends cut square.
3. âœ… Milestone 3: holes in all bolted plates, NSBA field splices, shop/field tapers, shear studs, disc bearings w/
   anchor bolts + fixity, top/bottom lateral bracing, frames normal to flanges, API/CLI/C# scripting, terrain
   (LandXML TIN/DXF/OBJ/STL) + reference models, incremental regeneration, ReadyToRun.
   Not yet: camber, holes in webs for lateral connections, web fillers at splices, IFC/DGN reference import.
5. âœ… Substructure (multi-column bents, abutment caps/backwalls, pedestals, sloped/crowned/level/seat-following caps,
   drilled shafts / spread footings / pile caps + piles, rebar) and 2D views â†’ DXF 2D models (layout plan+elevation,
   true plan, typical section, bent elevations + cap sections, bar bending sheet). IFC substructure validated 0 issues.
   Superstructure leftovers (not requested yet): camber, web fillers at splices, wingwalls, approach slabs.
4. âœ… Deck/barrier solids + rebar (TxDOT SSTR default rail), bar schedule window with bend diagrams, concrete/rebar
   in IFC (IfcSlab, IfcRailing GUARDRAIL, IfcReinforcingBar with IfcSweptDiskSolid â€” validated 0 issues).
   Next: substructure (multi-column piers first) with rebar layouts. Camber deferred by the user.
   Snapshot targets: `--closeup RAIL [--below]` (rail oblique / end-on section), `--editors` also renders the bar schedule.
5. IFC 4.3 export (IfcAlignment, IfcBridge), DXF 3D, later DWG/DGN via ODA if licensed.
