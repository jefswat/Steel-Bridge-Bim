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
- Close-up of any point: `--camera E,N,Z,dx,dy,dz,width[,dist]` (re-applied after the 3D switch's animated zoom, so it now takes), `--winsize W,H`
  (bigger window / viewport), `--off REF-` (hide reference models). Flagged-layer parts are NOT hidden by default.

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
  (`v_element_sheets`). `tools/plan_split.py` splits plan PDFs. App: `MainWindow.Database.cs` + `SBM.App/Database/` (panel, preview window, `PdfOpener` = open at page in an external viewer,
  `PlanSheetWindow` = the element's linked sheets INSIDE the app: one modeless window (movable to a second monitor, closable), sheet list + the plan PDF in
  WebView2 (Edge PDF viewer, `#page=N`; package Microsoft.Web.WebView2, user data in %LOCALAPPDATA%\SteelBridgeModeler\WebView2), opened by a 3D pick when the
  database has links unless the user closed it, follows the selection; panel "Show sheet"; snapshot `--sheetwindow` (with `--pickafter`) logs SHEETWIN: and writes `_sheet.png`).
  Sheet links need no PDF split: rules files `samples/plans/sheet-links-{i80-rampg,woodbury,br02069,nht,odot-o,bonner-unit5,bonner-unit6}.json` carry each plan set's sheet index
  (`sheets: [{p,s,t}]`) and `models` (which .sbm they belong to; `CompanionDatabase.FindLinksFile` picks by it) + `planFolders` (PDF lookup ->
  `sheets.pdf_path`). A model outside that folder (copied / saved elsewhere) gets the SHIPPED rules: `CompanionDatabase.LibraryFolders` = `%SBM_LINKS%`, the program's `plans` folder
  (the rules are copied there by the App / Cli csproj) and the repo's `samples/plans`, matched by `models` (file name) then by plan set `bridgeGroup` = a bridge name
  (`DatabaseOptions.BridgeNames`); a generic rules file is never taken from the library. Every build applies them; `CompanionDatabase.Relink` / `sbm db --relink` re-applies to an existing db; the app relinks on
  load when links are missing or the rules file is newer. A new bridge needs its own `sheet-links-<name>.json` with an index (tests check every rule matches a sheet).
  Agents reviewing plans: query instead of reading PDFs: `sbm db <model.sbm> --sheets-for <mark|name> [--json]` (pages to read per PDF) and `--find-sheet <title|no.>` (read-only, existing db, no regeneration).
  Keep `ModelDatabase.Builder` numbering in step with `IfcExporter` (test `GlobalIds_AreTheIfcGlobalIds`). CLI `sbm db`, docs in docs/API.md.
- `src/SBM.Cli` — `sbm.exe` command line over SBM.Api (new/info/get/set/add/remove/import-landxml/regen/export/run/bench/
  elevations/drawings/sheets/nurbs).
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
  - Exact curved geometry (NURBS, project setting `BridgeProject.Geometry` = `GeometrySettings`: `ExactCurvedGeometry` default OFF, `FitTolerance`
    0 = 1/256" US / 0.1 mm SI, `ChordTolerance` 0 = today's sample density; saved in the .sbm only when non-default; app ribbon "Units & geometry";
    part of the RegenerationCache key). OFF must leave every output byte-identical (checked: IFC of Woodbury / Ramp G U4 vs the base build).
    Math in `Geometry/Nurbs/` (no kernel): `BSpline` (Cox-de Boor basis + derivatives, knot checks), `NurbsCurve` / `NurbsSurface` (rational,
    derivatives, closest point, ruled, ReversedV), `NurbsArc` (exact rational quadratic arcs), `NurbsFit.FitCommon` (several curves on ONE
    knot vector, LSQ with ends + C0 breaks interpolated, adaptive knot insertion until the same-parameter deviation <= tol) and `Interpolate`.
    B-rep light `Solids/NurbsSolid` (faces = NURBS surface whose whole domain is the face, Su x Sv OUTWARD, or planar polygon; `NurbsHole`
    circular trims; Volume (divergence theorem, Gauss per knot span) / Area / Bounds), `NurbsSweep` (polygon section swept: corner curves fitted
    together, ruled faces, planar ends; plates (s, w, t) with holes -> half-cylinder walls; `PlateMap` = bilinear blend used to TESSELLATE),
    `NurbsSlab` (deck: panels along x bands across, tensor surfaces, bottom = top translated by the depth; panels fit in parallel),
    `NurbsSection.Cut` (exact plane section points), `NurbsReport`. Fit to the generators' EXACT functions, never to triangles:
    `GirderPath.Exact` / `ExactAt` / `ExactPoint` / `ExactBreaks` (FramingGenerator: span curves + `WebTopAt`; chorded spans / extensions = the
    linear path; breaks at span ends, grade / cross-slope kink stations `ConcreteGenerator.KinkStations`), carried through terminations /
    beginnings. Generators: `SteelNurbs.LongPlate` (girder flanges / webs, field splice plates, fillers; only when the plate is curved; hole at
    an edge -> today's plate + fallback reason), `ConcreteNurbs` (deck: hard columns = edges, gutter lines, pivot, cross-slope break offsets,
    CUT (inset) kinked-end vertices; panels at kink stations, clamped to skewed ends + per-panel columns where a kink line meets an end;
    haunch per girder split at per-span-layout jogs; barriers). The mesh is tessellated FROM the NURBS (`Mesh.Nurbs`, `NurbsTriangles`,
    `HasExactNurbs`, `ExactVolume()`); every consumer (viewport, drawings, checks, IFC, quantities) still reads the mesh. Opt-ins: `SteelPart.ExactVolume`,
    `NurbsQuantities.Rows` / `SbmDocument.ExactQuantities`, `sbm nurbs` (timings off/on, fallbacks, exact vs mesh volumes, csv). Measure snaps refine onto the NURBS (see App). Not wired yet:
    drawings section cuts, view-dependent viewport tessellation, welds / stiffeners / deck end blocks / joint shelves (stay meshes).
    Cost (2026-10-11): I-80 + Ramp G full regen 27 s OFF -> 41 s ON; Woodbury 0.2 -> 1.1 s cold. Tests `NurbsTests`.
  - `Solids/PlateBuilder.cs` â€” plates WITH HOLES: polygon+holes triangulation (horizontal bridge to the +x ray hit â€”
    don't change to a vertex bridge; that crossed unmerged holes and caused the bearing-stiffener artifact), long girder
    plates built in strips/cells (â‰¤1 hole per cell keeps it linear), Sutherlandâ€“Hodgman half-plane clip.
  - `References/` â€” reference model definitions, `TerrainModel` (grid index elevation queries, parallel contours).
  - `Substructure/` â€” `SubstructureDefinition` lives on each `SupportDefinition.Substructure` (null = defaults by type via
    `ResolvedSubstructure`). `SubstructureGenerator` runs AFTER steel (needs `SteelModel.BearingSeats`), per support in
    parallel: cap coordinates S (along the skewed support line from the alignment point, right +), N (normal, up-station +).
    Cap top = shape g(S) + c with c set so min pedestal = MinPedestalHeight; pedestals take the rest (variable).
    Plan pedestal sizes (OPTIONAL): `PedestalSizeAlong/Across` (S x N, centred on the bearing footprint; 0 = footprint + `PedestalProjection`) and `PedestalShared` (ONE block per girder
    line carries the back + ahead seats, top = highest seat, differences reported). Set by `piers-i80.csx` / `piers-ramp-g.csx` (not 6A, 19G: sizes not settled). Two rows (11WB): `PedestalSizeAcrossAhead` (+N row; Across = back row) and `PedestalRowJoint` (rows abut at CL pier + joint instead of centring on the seats); pedestal bar groups pick blocks by `PlanBarGroup.PedestalRow` (csx `;R=BACK|AHEAD|LINE`, LINE = one set across the back + ahead block of a girder line, 9G 5n1) and `EdgeDistance` (`;X=`, end bars from the block edge, default 4").
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
    Woodbury additions (OPTIONAL): `PedestalSideAngleDeg` (plan pedestal blocks as parallelograms, sides at that angle from the cap normal),
    `ColumnProjectionS` / `ColumnProjectionWidth` (rectangular column with a centred reveal projection on both S faces; rebar uses the core rectangle),
    `CapBlocks` (`CapBlockDefinition`: plan prism on an as-designed cap, base = cap top or soffit, top = plane; Kind Cap | Maskwall; Woodbury maskwall
    + 4" end reveal). Woodbury substructure: `samples/plans/substructure-woodbury.csx` (piers + integral abutments, called by build-woodbury.csx).
    `piers-i80.csx` / `pier-6a.csx` with env `PIER_POSITION_ONLY=1` update only the position fields (CapProfile, CapProfileAhead,
    FirstColumnOffset) of existing piers, so later scripts' data (rebar, aesthetics, pedestals, footing bars) survive a position fix.
    Chamfered (octagonal) column rebar (`SubstructureRebar.ColumnBars`): verticals per face from the OUTSIDE-face
    dimensions (centred groups, explicit chamfer positions, or equal spaces with shared corners) set in normal to the
    face; ties follow the section (`PolygonTie`, hooks at the sharpest corner), optionally as two overlapping end hoops
    (`ColumnHoopOverlap`, legs mapped like the S-face corner) + crossties (`Crosstie`); flared base gets its own cage, its ties
    per level one closed tie or a lapped PAIR (`FlareTieStyle` HairpinPair = open U "PLINTH, HAIRPINS" 6e3, shape HAIRPIN scheduled
    out-to-out / HoopPair = closed end hoops "PLINTH, HOOPS" 6e4; `FlareTieOverlap` out-to-out along S; `FlareTieSpaces` = the plan's
    "n SPA. @ ABT." count, 3" above the footing to 3" below the plinth top). Terminating bars (18d9/18d7/18d5) = the circled perimeter
    bars the half-filled legend tags point at (splice pattern 'T').
    Bar sizes include #14/#18. Plan data: `samples/plans/column-rebar.csx`.
    Battered piles (`SubstructurePiles.cs`): `FootingPoint.BatterDirS/N` (unit plan direction of the tip movement, 0 = plumb) +
    `FootingDefinition.BatterRatio` (4 = 1H:4V; diagonal batter measured along the diagonal); `FootingDefinition.BatterPerimeter`
    sets the plan pattern (perimeter out, optional S edges, 45 deg corners). Plan positions are at the FOOTING BOTTOM plane; length
    along the axis from the head; head/tip cut horizontal; section = plumb orientation rotated minimally onto the axis (oblique
    extrusion = exact IFC solid). `PileInfo` has head (S,N,TopZ) + tip (TipS,TipN,TipZ). Axis-distance clearance warnings
    (`CheckPileClearances`). Snapshot `--closeup FTGn@Bridge` frames the foundation. Conventions are flagged for user confirmation.
    `PileType.CipPipe` = cast-in-place concrete-filled steel pipe pile (round 32-gon solid of `PileSize` = outside diameter, `PileShellThickness`;
    `SubSolid.ShellThickness`); IFC: ONE IfcPile DRIVEN / ConstructionType COMPOSITE + Pset `SBM_PipePile` (shell as a property). `sd.PileLabel(...)` for views.
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
    5a14/6a15 end bars in the longitudinal plane (leg + top run + slanted leg along the raked face; 6A 6a16 = A14 with 0 run), 6A 5a11 reverse curve `PlanReverseCurve` (csx `S=RC:a1,R1,a2,R2,riseL,riseR`,
    fitted rigidly to the face's relief line + 3 1/2"), 5c5/5c6 (5c3/5c4) hairpins = U across the width (3 1/2" clear at both faces) hanging from the top
    in the plan's zone order, 4c7/4c5 U, 11g/10g bottom mats U, 6c1/6c2/5n2 hoops with lap tails (`PlanHoop`), 6b2 cantilever flat + slant (`CS`), 5a11/5a12 short end bars. Data is appended to the
    csx lines as `;S=shape:dims:pin` (+ `;Z=..;M=..` hairpins, `;E=-1/1`) by `tools/patch_*.py` from `tools/extract_all.py`/`shapes.py` (plan text + fraction glyph decoder `plantext.py`);
    plan vs model for every mark: `docs/plan-data/bend-diagram-comparison.md` (`tools/compare.py`, `make_doc.py`; abutment diagrams hand-read in `tools/abut_hand.py`). Bars are never moved to
    satisfy cover - containment violations are raised. `LateralSetback` continues each relief line level beyond its ends (the plans' arcs start at the cap tips) and counts each
    face's own layer lines once (`SubstructureUnit.ReliefLineTags` = face + layer per line). Pedestal U bars 5m1 / 5n1: base in the TOP of the plan pedestal block (2" under it; 5m1 one
    5n1 bar lower), legs through the block into the cap; 5n2 hoops round the blocks of the note's girders (`PlanBarGroup.Girders`, csx `;G=C,D`), else UNRESOLVED at mid-cap;
    `PlanBarGroup.End` also picks the end of CapEnd groups (5WB 5a15 shape `A15` left, 5a14 right). Check: pedestal blocks are host concrete for pedestal bars, hairpins need 3 1/2" CL.
    to both full-width faces (`HairpinFaceClear`, sections A-A/B-B), per-bar records `SubstructureUnit.CoverViolations` (`samples/plans/check-sub-cover.csx`); textured-face cover for
    other bars UNRESOLVED (2" kept). State and remaining warnings: `docs/plan-data/cap-cover-containment.md`.
  - Explicit PLAN BAR SETS (OPTIONAL, generic; `Bridge/PlanBarSet.cs`): one plan mark per set = bars given as centerline polylines computed by a plan-data script
    from the sheet dimensions (`Bars` = one string per bar "x,y,z x,y,z ..." in meters, `PlanBarSet.Encode/Decode/Add`; `Spirals` = `PlanSpiral` generated helices with
    1 1/2 flat turns and an optional upper pitch zone), printed bend diagram (`Shape`, `Dims`), `ListedLength` (scheduled length; 0 = geometry, varying lengths),
    `Unplaced` (schedule-only bars, location "... not modeled - UNRESOLVED"), `Unresolved` / `Note`, plan check values `PlanCount` / `PlanWeightLb`, covers `Cover` /
    `CoverBottom` / `CoverTop`, `CheckCover`. On a substructure unit (`SubstructureReinforcementDefinition.PlanBarSets`, unit frame S / N / elevation) they REPLACE the
    generic cap / column / shaft / footing generators (`SubstructureGenerator.PlanSetBars`); on a deck (`DeckReinforcementDefinition.PlanBarSets`, project X / Y / Z)
    the generic layout does not run (a `DeckPlanReinforcement` still does; `ConcreteGenerator.BuildPlanBarSets`). `Concrete/PlanBarSetBuilder.cs` -> `BarMark`s (plan
    mark, unit) + `BarPlacement`s; clear cover by `Concrete/RebarCover.cs` (generic: ball of cover + bar radius must lie in the UNION of closed host meshes - shared faces
    cap / column / shaft / pedestal are not free faces; vertical-ray parity + XY grid index; face orientation picks bottom / top cover; `AddAnalytic` = exact signed-
    distance hosts, used for the deck slab because the chorded loft's edge sagitta on tight curves exceeds 1/8"; drilled shaft 32-gons moved to the mid-sagitta)
    -> `SubstructureUnit.CoverViolations` / `ConcreteModel.CoverViolations` (`PlanBarCoverViolation`) + one warning per mark; bars never moved. Database grouping
    follows the location text (" cap " -> cap group, "column n" -> column group, "footing n" -> footing group (drilled shafts), "backwall"). Tests `PlanBarSetTests`.
    Deck sets with `Element` "barrier" / "diaphragm" keep their bars in the groups "Rail" / "Diaphragm" (database: barrier reinforcing / diaphragm reinforcing; other
    elements stay "Deck"). Br02069 uses them for all its plan bars (`rebar-abutments/-diaphragms/-barrier-br02069.csx`: a common script block turns frame-based
    specs - support line W/U or alignment station/offset, z absolute or from the deck top, copies along W - into the explicit sets; `check-br02069.csx` adds
    bill rows and an exterior-face cover report). Deck plan options (OPTIONAL, JSON omitted when default): `DeckTransverseZone.SideClear` (per-mat end clear),
    `DeckLongZone.AlternateEnds` (staggered laps: odd lines laid from the end). Tests `DeckPlanOptionsTests`.
    NHT data: `samples/plans/rebar-nht-sub.csx`, `rebar-nht-approach.csx` (see the Northaven entry).
  - As-designed abutment (OPTIONAL `SubstructureDefinition.Abutment`, `AbutmentDefinition.cs`; null = parametric abutment): `SubstructureAbutment.cs`
    (`AddSlab` = prism between two vertical planes with per-vertex elevation functions; end lines are alignment offsets, so S depends on N),
    `SubstructureAbutmentRebar.cs` (only proven placements in 3D, rest of the plan bar list = schedule-only marks, mismatches = warnings),
    `AbutmentViews` (3 sheet panels; the plan draws `AbutmentGeometry.FootingOutline` = generator outline with notches), `SubElementKind.Wingwall/Counterfort/Maskwall/WingFooting`,
    `unit.Abutment` (`AbutmentGeometry`). `WingwallDefinition.BottomOnFootings` (OPTIONAL): over each counterfort wing footing the wall bottom drops to the footing top (plan wing elevations, vertical steps at the footing ends). `AbutmentDefinition.StandalonePedestals` = pedestals WITHOUT a bearing (Br02069 Type B / D for the future beam lines,
    MnDOT STEM (OPTIONAL `AbutmentDefinition.Stem` = `AbutmentStemDefinition`, JSON omitted when null; Br02069 p11 A-A, semi-integral): plain footing block, stem from the
    backwall back face to `FrontToBearing` with a BACK LEDGE (`LedgeTop`) and a SEAT in front of the riser on the backwall front-face line (`SeatBackTop` -> `SeatFrontTop`,
    straight), backwall = one plain wall on the ledge between the backwall offsets (no corbel / pours), pedestals on the seat (`AbutmentGeometry.SeatAt / LedgeTop /
    XStemFront`; `SeatBaseZ` = seat at the CL brg, mirrored by ElevationCheckSubstructure); missing input = stem not built (warning). `MaskwallDefinition.BottomElevation`
    (OPTIONAL) = level panel bottom (mask wall beside the footing, Setback = -thickness). Tests `AbutmentTests.MnDotStem_*`.
    built now): same section as the beam steps, `PedestalInfo` / `AbutmentStepInfo` with Girder = -1 and `Label` (views print `Tag`). Backwall runs behind the wings to the maskwalls; `CorbelLeft/RightOffset` limit the corbel to the wing inner faces. Standing rule:
    no invented defaults - unresolved plan items are generator warnings ("UNRESOLVED"). Docs `docs/plan-data/abutments-model.md`; scripts `samples/plans/abutments-*.csx`.
    `AbutmentDefinition.PileRowOffsets` (x of each pile row from CL brg; empty = the two legacy rows `PileRows`). `Kind` = Stub (default, all of the above) |
    Integral (Iowa integral pile cap, Woodbury p.15-17; `SubstructureAbutmentIntegral.cs`, concrete-bridge design feature A / T1): cap faces `CapBack/FrontToBearing`
    (required), stepped top = one level step per girder (`Steps`) with vertical risers at `StepBoundaryS` or `StepRisersAtMidBay` (only when confirmed - user question Q3;
    neither = UNRESOLVED, no cap solid; Woodbury: user decision 11 = 1'-9 past the lower beam's CL, set by substructure-woodbury.csx, which also gives the
    abutment / pier diaphragms their bottoms), bottom = `FootingBottom`; no backwall / corbel / step blocks (diaphragm = feature C); checks `PlanCapDepthLeft/Right`
    (p.17 step diagram), `PlanCapVolume`, `StepToSeat`; `WingExtensionDefinition` (wall outboard of the gutter line + level footing from the cap back face, every
    missing elevation / position UNRESOLVED), `PileCasingDefinition` (CMP from the concrete bottom to `BottomElevation`; NaN = UNRESOLVED, `AbutmentGeometry.Casings`
    schedule records only; solid = `SubElementKind.PileCasing`, IFC mapping pending T3), `PileSpiralDefinition` (schedule-only mark, one per pile). Integral rebar:
    no 3D placement, the plan bar list is schedule-only. `AbutmentGeometry.StepTop/StepRisersS/DepthAtLeftEnd/DepthAtRightEnd`. Views reuse the stub panels (T3 pending).
  - Cap section step / aesthetic relief (OPTIONAL, `SubstructureDefinition.Aesthetics` = `CapAestheticsDefinition`, as-designed caps only): the
    cap is W above the relief curve and W - 1'-0 below (StepSetback 6" per face, BOTH faces). The cap solid itself is the narrower core; `BuildCapStep`
    adds full-width strips above each layer line (`Layers` = BandChains, lowest line first; plans: fan arcs R5, R4, R3, 2" each, drafted 7 deg) as a
    separate Cap-kind SubSolid ("... cap section step"); lines go to `SubstructureUnit.ReliefLines` (bent elevation view). `CapBandBuilder` builds the
    tangent-arc S-curve from the plan tables (`FromTable`) or the explicit figures (`FromLongArc`). Cap bars are NOT adapted (warning). Data:
    `samples/plans/pier-aesthetics.csx`, reading in `docs/plan-data/pier-cap-aesthetics.md` (Confirmed section). Stepped top (6A): `BandFromLowerTop` measures both faces' lines from the lower half's top. Texture itself / Detail A are not modeled (see Surface treatments below for the generic texture / thin-brick regions).
  - Surface treatments (OPTIONAL `BridgeDefinition.SurfaceTreatments` = `Bridge/SurfaceTreatment.cs`, null = none; generic for form liner texture, integral thin
    brick / terra cotta, rustication): `SurfaceTreatmentDefinition` (Kind, Pattern, Products, Relief depth, Orientation, PlanQuantity m2 + QuantityItem, Source,
    Unresolved) with `SurfaceRegion`s: Host Barrier (custom-barrier faces with a surface `Tag`) | Substructure (unit solids by `Elements` kinds / `NameContains`) |
    Diaphragm; near-vertical, EXPOSED faces (centroid + 5 mm not inside another solid of the support, the beams or the haunches - ray parity), optional facing
    (`FacingS/N` in the unit frame or `FacingOffsetSide` = outboard right/left), S range, `Lower`/`Upper` limit lines (`SurfaceLevel`: level, (S, z) polyline
    along the support line, or `DeckTop` + offset = parallel to grade) and `Exclude` bands. `Surface/SurfaceTreatmentGenerator` (RegenerationCache phase 2,
    after the diaphragms; result on `SubstructureModel.SurfaceTreatments` so every consumer has it): per region the AREA (clipped faces) and a RELIEF LAYER
    (closed prism per face piece, offset outward by Relief); totals vs PlanQuantity as Info. Viewport layers 3D-THIN-BRICK / 3D-FORM-LINER; IFC one IfcCovering
    per region (thin brick CLADDING, form liner USERDEFINED 'FORM_LINER_TEXTURE', key `IfcKeys.SurfaceTreatment`) + Pset SBM_SurfaceTreatment (kind, pattern,
    products, orientation, host, Area, TreatmentTotalArea, PlanQuantity, ReliefDepth, source; area unit = m2) + SBM_Status when flagged; database group per
    treatment (category "surface treatment", plan vs model in props) > elements "thin brick" / "form liner". The texture pattern itself is not modeled.
    Data: `samples/plans/surface-treatments-woodbury.csx`. Tests `SurfaceTreatmentTests`.
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
  - Sheets (2026-10-12, branch sheet-fixes; status in `docs/plan-data/sheets-vs-plans.md` section 17): `LabelPlacer.Resolve(dwg, frame)` keeps movable labels inside
    the panel at the chosen scale (SheetComposer) or a bend-diagram cell (BarSheet); layers S-TITLE / S-GRID / S-NOTES (`SheetLayers.Notes`, placed note blocks) are fixed.
    `MeshSection` = plane cut of meshes (segments, x / y clip, closed loops for hatch) -> true sections of the model: `SupportSectionViews.AlongSupport` (abutments of
    prestressed / integral bridges: cap or footing with steps, diaphragm, beams, pads, deck, barriers, piles) and `PrestressedViews.SupportSection` (normal to a
    support at a beam / mid-bay; concrete diaphragm and abutment sheets), steel diaphragm detail (beams cut, parts / bolts projected). Prestressed set: beam layout,
    deck plan (`DeckViews`), one sheet per beam type (`Sections/Elevation(ctx, type)`, `DataTables`), schedule + notes, steel / concrete diaphragm sheets. Agency text
    in `PsText` (Iowa DOT / MnDOT / TxDOT from `TxGirderShape.State`; strengths per type; pads from the model). Beam bars read by reflection (`PsBars`: `BarMarks`,
    `Bars`). `TableViews.QuantityRows` = plan-style groups (pile fill under PILING, structural concrete per unit, rebar by coating, NOT MODELED rows). Pier Section A-A
    is its own view (`BentViews.CapSectionView`, legend = bars cut). Pile plans at the bottom of the footing (`PierViews.PileAt`), tips plumb / battered
    (`PierViews.TipText`). `MiscViews`: dap details, misc items plan + schedule. Optional `GirderLayoutDefinition.PlanNames` -> `ViewContext.GirderName` (sheets only).
    DXF zip: `SheetExport.WriteAll/WriteSheets` (folder or .zip), `SbmDocument.ExportSheetsDxf`, `sbm sheets ... --dxf <folder|zip> [--models]`, `sbm drawings ... --zip`, app button.
  - App 3D: `MainWindow.Viewport3D.cs` â€” camera depth range follows the scene (`KeepCameraInRange` on CameraChanged; do
    not hand-set Near/Far elsewhere); section cut = CPU clipping of all solids/lines/text (Helix
    `CrossSectionMeshGeometryModel3D` was tried: transparent models stop rendering when it is in the scene â€” don't use
    it); picking via `SceneBuilder.Picks` (triangle range â†’ source object) and `InstanceTags`; clipped meshes map
    triangles back via `_triMap`. Hide/unhide: `_hidden` (tags) filters merged meshes by pick-table ranges and
    instanced meshes by `_visibleInstances`, through the same ClipMesh/ApplySection path as the section cut; `RemapHidden`
    re-matches hidden tags after regeneration via `HideKey`. Right-click (no drag) = context menu. Sheets mode: `MainWindow.Sheets.cs`. Snapshot options: `--section STA[,skew]`,
    `--pick fx,fy` (logs PICK: properties), `--sheets`, `--orbit fx,fy,dx,dy` (simulated orbit drag; logs ORBIT: pivot, leaves the marker), DETAIL: log lines = detail tiles drawn.
  - Isolate (`MainWindow.Isolate.cs`, state + save/restore in `SBM.Core/Geometry/ViewIsolation.cs`, tests `IsolateAndRecentTests`): right-click
    "Isolate <element>" / "Isolate <db group> (N elements)" (the group highlighted from the database panel, `_groupSel`, incl. its instanced bolts / bars;
    a right-click on the group keeps it selected) / "Isolate layer <name>", "Exit isolate", "Zoom to isolated", option "Zoom to isolated when isolating".
    Keys: I = isolate the selection (nothing selected + isolated = exit), Shift+I = isolate the selected element's layer, Esc with nothing selected = exit
    (Ctrl+I stays the Station/Offset query). Banner `IsolateBanner` ("Isolated: <name>" + Zoom + Exit; hidden in Sheets mode). Mechanism: other layers are
    switched off through the normal `LayerItems` (batched with `_layerBatch`, so detail tiles / VRAM residency / far-bridge LOD follow the usual paths);
    element isolates also filter merged meshes + instances with `TagHidden` (= hidden OR outside the isolate; the same predicate drives `HiddenRanges`,
    instance lists, `Pick`, the snap env `IsHidden` and therefore the orbit pivot ray cast); meshes without a pick table are hidden whole. `LayerModeOk`:
    isolated 3D layers are ALSO drawn in the plan view (top view of the isolated set). The state before the FIRST isolate (all layer visibility + `_hiddenList`)
    is restored exactly on exit (nested isolates keep it); regeneration remaps isolated + saved hidden tags by `HideKey` (`RemapIsolation`, ends the isolate
    if none survive); new reference layers stay off (`IsolateNewLayers`); opening another project (`MainViewModel.ProjectOpened`) exits. Zoom extents while
    isolated = zoom on the isolated set (`ZoomToIsolated`). Snapshot: `--isolate fx,fy` / `--isolatelayer NAME` [`--isolatezoom`] [`--isolatepick fx,fy`]
    [`--isolateexit`: also writes `_isolated.png` / `_isolated_view.png` before exiting]; ISOLATE: log lines. Note: `RenderBitmap` lags one frame
    (the `_view.png` after an in-tick change can show the previous state).
  - Navigation (`MainWindow.Navigation.cs`, math in `SBM.Core/Geometry/ViewNav.cs`, tests `ViewNavTests`): Tekla / CAD style, all camera moves are ours
    (`UseDefaultGestures="False"`, Helix InputBindings cleared, Preview handlers mark events handled). Bindings = table `NavPresets.Cad`
    (another preset = another table): middle-drag pan (grabbed point stays under the cursor) - Ctrl/Shift + middle-drag orbit - right-drag orbit
    (right CLICK = context menu; the orbit starts after 4 px) - double-click middle = zoom extents - wheel = zoom 15 %/notch about the cursor (min width 2 cm).
    Orbit = turntable (yaw about Z, pitch clamped to +-89 deg, no roll, no inertia). Pivot (`ChoosePivot`): selection centroid when "Pivot: selection" is on,
    else the exact BVH ray cast `SnapIndex.RayCast` (front-to-back, no time budget; the snap `Find` is budgeted and can return a far surface) under the
    cursor -> under the screen center -> previous pivot if on screen -> view target; never a plane point. Marker = WPF overlay `PivotOverlay` (always on top).
    Help: "Mouse ?" button (text from `NavPresets.Describe`). The camera is orthographic only (`DollyToward` / `NearPlaneFor` are there for a perspective one).
  - Detail tiles (`_detailBounds`): culled by distance from the view-center LINE OF SIGHT (`_detailRadius`, 120 m), never from `Position + LookDirection`
    (that point is at the depth of the scene center because `KeepCameraInRange` parks the eye outside the scene: in oblique views it was hundreds of
    meters from what is on screen, so close-up rebar, e.g. Ramp G Pier 7G, vanished). `OnCameraMoved` ignores no-op camera events.
  - `Prestressed/` â€” `TxGirderCatalog` (Tx28â€“Tx70 as PGSuper/WBFL precast I-beam dims `PrecastIBeamDims`; `TxGirderShape`
    outline = WBFL PrecastBeam polygon; IGND grid; IGEB pad table), `PrestressedGenerator` (one straight beam per span Ã—
    girder between the framing's support-line points; IGD/IGEB ends; strands; pads; `BearingSeat`s with Location so the
    substructure gets two pedestals per girder at bents). `GirderLayoutDefinition.Kind` switches steel â†” prestressed:
    FramingGenerator treats the beam top as "top of web" (flange t = 0, web = D), forces chorded, skips cross frames,
    stiffeners and splices; `RegenerationCache` runs PrestressedGenerator instead of steel (`BridgeModel.Prestressed`).
    Views: `PrestressedViews` + branch in `SectionViews` / `SheetSet`. IFC: IfcBeam + IfcTendon (validated 0 issues).
    State standards (all inputs OPTIONAL, Tx path unchanged): `StandardBeamCatalog` (key "IA BTD", "MN 30MH"; Tx listed too)
    with `SectionProfile` = exact outline from the sharp corners + true radius fillets / chamfers (`TxGirderShape.Profile`,
    properties integrated from it, printed values in `PrintedArea/Yb/Ix`, `PropertyCheck`); `Beam("BTD55"|"BTD60"|"BTD105")`
    = standard beam definitions (Woodbury p.27-30). `PrestressedBeamDefinition`: `Standard`, `Designation`,
    `StandardBearingLength`/`StandardOverallLength` (beam built at the STANDARD length along the sloped beam, CHECK warning vs
    layout, never stretched; within 1/16" of the layout = fits, Info per span, no placement needed), `Placement` (NO default: null = UNRESOLVED
  only when the difference exceeds 1/16", drawn centred), CL bearings = the span layout's bearing points (`LayoutBegin/End`) when present,
  `PierPadOrientation` (SquareToBeam default | ParallelToSupport = pad width along the pier line, Woodbury p.20; abutments square), `SpanBeams` (per span / beam line,
    last match wins; `For(span, girder)`), `HarpedColumns` + `HarpedTopBelowTopAtEnd` (`StrandPatterns.Resolve`),
    `HoldDownFromMidspan`, `Unresolved` list, `StrandPoint.DebondStart/End/Symbol` (`StrandPatterns.Debond`). Beams with a
    profile carry `PrestressedBeam.Body` -> IFC IfcArbitraryClosedProfileDef over IfcIndexedPolyCurve (IfcArcIndex) extruded
    and clipped by the end planes; debond sheathing = IfcTendonConduit (key `IfcKeys.PsSheath`) + Pset SBM_Debonding.
    Demo: `samples/plans/prestressed-shapes-demo.csx`; tests `PrestressedStandardTests`.
    Florida I-Beam "FL FIB-96" (also "FIB-96"): outline as DIMENSIONED on NCDOT B-2500 p.438 (no FDOT index cited; sharp web / bottom-slope
    corner at 14.5" + R 15" fillet, 3/4" bottom chamfers drawn not dimensioned) -> A 1,226.6 in² (= the 49.9 CY quantity). OPTIONAL beam inputs
    (all omitted from the .sbm when unused, output unchanged): `StartBearing` / `EndBearing` = `PrestressedBearingDefinition` per beam END (plan mark,
    pad size along x across, total height, `SolePlateDefinition` = tapered steel "P" plate: length, width, thickness back / ahead station end,
    `BearingFromBack`; stack uses the thickness AT the CL bearing) -> `PrestressedPad.BearingMark` / `.SolePlate` (`PrestressedSolePlate`: mesh,
    taper vs beam slope Info; IFC IfcPlate 'SOLE_PLATE' key `IfcKeys.PsSolePlate`, DB element "sole plate", viewport 3D-BEARING-SOLE-PLATES,
    elevation-check row "− Sole plate"); `StartBearingToEnd` / `EndBearingToEnd` (span-layout bearing offsets, 0 = BearingToEnd); `CamberTable`
    (`PrestressedCamberPoint` fraction / camber / SDL / printed final; recorded + Info check, geometry stays uncambered) + `CamberSource`;
    `PlanLength` / `PlanConcreteVolume` (check data); `StrandPoint.Diameter` / `Label` (e.g. 1/2" slack strands among 0.6") -> `StrandInstance.Diameter`
    (`DiameterIn(beam)`), IFC tendon diameter / area / description, viewport tube.
    Beam mild reinforcing (OPTIONAL `PrestressedBeamDefinition.Reinforcement` = `Bridge/BeamReinforcementDefinition.cs`, JSON omitted when null = no
    beam bars, output unchanged): `BeamBarSet` = mark / size / coating, CENTERLINE path with sharp corners in beam coordinates (A along the beam from
    the station TOWARD MIDSPAN, X right of CL, Y above the bottom; centerline = out-to-out face - db/2) + `BendRadius`, `Mirror` (pairs), `Copies`
    (lines / levels), `Stations` from the end of beam (`Ends` Both = mirrored from each end, a station landing on the other end's station = once;
    StartOnly), `StationAtBar` (longitudinal bars: clear to the end face at the bar's own x), `AlongOffset` (bar tied against another at the same
    dimensioned station), `PrintedDims`, `ListedLength` (scheduled length; 0 = developed), `Unresolved` (set NOT placed, raised as the warning);
    definition-level `Unresolved` / `Notes` (Info) / `BarList` (counts per beam, checked) / `ListedWeightLb`. `Prestressed/PrestressedBeamRebar.Place`
    (called from `PrestressedGenerator.BuildBeam`): transverse bars square to the beam in plan, station measured along the CL from the end face at
    each point's height (bars parallel to that end face in elevation: plumb abutment ends vertical, square pier ends square); bars crossing a
    SKEWED end face are reported (UNRESOLVED: the standards draw square ends), never moved. Output `PrestressedBeam.Bars` (BarPlacement Group "Beam",
    Unit "{designation} beams") + `PrestressedModel.BarMarks` (`MarkOf`, `RebarMass`) + Info rows (bar list per beam, lb per beam vs printed).
    Consumers: IFC IfcReinforcingBar aggregated into the girder assembly (key `IfcKeys.PsBar`, STIRRUP / HAIRPIN = LIGATURE), database rows under
    the girder assembly, viewport layer `3D-REBAR-BEAM` (tiles, RebarPick -> near-view LOD), bar schedule window, material rows "Reinforcing (beams)".
    Standard data `Prestressed/StandardBeamRebar.cs`: `IowaBtd(designation, IowaBtdEdition)` BTD55 / BTD60 / BTD105, editions Issue2004Rev2009 (as
    printed in Woodbury p.27-30, used there) and Reissue2024 (Iowa Beams.pdf p.98 / 100 / 101 / 110; end-zone chain and 5a1 lengths differ);
    `MnDot30Mh(chain, g505, g508, ...)` (Fig. 5-397.501(B) shapes, the spacing chain is a designer input). Scripts `samples/plans/beam-rebar-*.csx`
    (called by the build scripts); checks in check-woodbury / check-br02069 ("Beam rebar" rows); tests `BeamRebarTests`.
  - `Diaphragms/PsSteelDiaphragmDetailer` (T4; hooked up by T6: `BridgeDefinition.PsSteelDiaphragms` -> `RegenerationCache.SteelDiaphragms`
    after the prestressed beams -> `PrestressedModel.SteelDiaphragms` (holes, bay fits, diagnostics into the prestressed diagnostics) AND the
    bridge's `SteelModel` (= diaphragm parts / bolts / assemblies only, so viewport, IFC IfcMember/IfcPlate/IfcMechanicalFastener, database,
    quantities need no special code; `PsSteelDiaphragmDefinition.BoltGrade` "ASTM ..." is written as is). SheetSet bearing-detail sheets
    stay steel-girder only (`ctx.Prestressed == null`). Iowa 1036-BTD
    steel intermediate diaphragms between prestressed beams. Pure function `Detail(PsSteelDiaphragmDefinition,
    IReadOnlyList<PsDiaphragmBeamLine>)` -> `PsSteelDiaphragmModel` (SteelParts, BoltInstances with Diameter, cast holes,
    per-bay fit/length/weight results, diagnostics). Input `Bridge/PsSteelDiaphragmDefinition.cs`: hole CHAINS per beam
    line as dimensioned on the plan (from the beam end at the lower station; 1-based span/girder), bays (hole index each
    side, member/lower member exact AISC names, plan lengths), `PsDiaphragmConnection.IowaBtd()` (standard values),
    `Elevation` (Q4: `Level` default as p.32 labels it, or `AlongHoleLine`; warns when the 1" x 1 1/2" member slots
    (+-1/4) or 1" x 2" leg slots (+-1/2) cannot take the offset), `MemberSide` (WD-3: `Unresolved` default = holes only,
    no steel). `PsDiaphragmBeamLine.FromPrestressed(beam, depth, web)` adapts generated beams. Tests: PsSteelDiaphragmTests.
  - Concrete END / PIER diaphragms (design feature C, T5/T6): input `SupportDefinition.Diaphragm` = `Bridge/ConcreteDiaphragmDefinition.cs` (OPTIONAL,
    omitted from the .sbm when null): `Parts` (section polygons in (x normal to the support - abutments + toward the span, piers + ahead; z = ref
    DeckTop / Soffit / Bottom (stepped `BottomSteps` per beam line, `Risers` Unresolved = mid-bay drawn + Assumption flag | MidBay | Explicit) /
    Elevation + dz), vertically convex, non-vertical edges keep one ref), ends `DiaphragmEnd` (Offset line | AlongLineFromOffset | FasciaOutside =
    top-flange edge line of the fascia beam), a part with `Unresolved` text is NOT built (warning), `OpenBelowBeams` (bearing zone under the beams is
    pads + filler, not concrete), `Fillers` (EndFace / Section / AroundBearings / Interface; thin solids, plan material names), `Keyways`, `BeamHoles`
    (cast web holes: positions + strand clearance, records only), `PlanBearingGap` check. Generator `Diaphragms/ConcreteDiaphragmGenerator.cs`, run in
    RegenerationCache phase 2 after the substructure (result `PrestressedModel.ConcreteDiaphragms`, `SbmDocument.Diaphragms`): superstructure element
    split from the deck at the soffit; exact, no booleans: x-SLABS at every section vertex / beam-end event, each slice a simple polygon in (S, z)
    with a NOTCH per beam (outline + haunch to the soffit; skewed ends at the top-CL end x, SQUARE ends = outline clipped where the face meets the
    slice, events at every outline vertex, slices inset 1e-5 m and extrapolated) or, open below, PANELS between the beams; every slab = closed loft
    (`Pieces`, one IFC face set each). Bottom = `Frame.Pieces` (z linear in S) with boundaries (risers) `Bounds` that may move with x (S + PerX x); rule
    `DiaphragmRiserRule.Blocks` + `StepBlocks` (block length centred on the beam line, side angle, `Between` = substructure top (S, z) between the blocks,
    Woodbury pier caps p.9) besides Explicit / MidBay / Unresolved. IFC: IfcBeam .DIAPHRAGM. ('END_DIAPHRAGM' / 'PIER_DIAPHRAGM'), keyways IfcBuildingElementPart 'SHEAR_KEY'
    aggregated into it, fillers IfcBuildingElementPart 'JOINT_FILLER'; Assumption -> SBM_Status. Viewport layers 3D-DIAPHRAGMS / -FILLERS /
    3D-UNRESOLVED-DIAPHRAGMS, material rows. Data: `samples/plans/diaphragms-woodbury.csx`, `diaphragms-br02069.csx` (run by the build scripts),
    report `check-diaphragms.csx`. Tests `ConcreteDiaphragmTests`. Not yet: 2D sheet views, database groups (diaphragm SOLIDS; plan diaphragm bars: deck PlanBarSets).
    Semi-integral (Br02069 p.21 A-A / B-B, p.11 Detail A): `DiaphragmRiserRule.BeamBottoms` = bottom = bottom of the beams (level under each bottom flange at the
    beam's bottom, straight between adjacent flange edges, level beyond the fascia; per-piece `Frame.PieceZPerX` follows the beam slope across the part; 0.001 mm
    under the beam so the open void crossing is stable); with `OpenBelowBeams`, extra slab cuts where a NON-Bottom lower edge (fillet) passes a beam-outline vertex
    (`OpenBelowEvents`, slices inset there); a wedge slab whose bottom meets the top at one end is evaluated a hair inside and extrapolated; a top edge on the
    Bottom reference breaks at the risers (fillers whose top follows the beam-bottom line). Outputs of other bridges unchanged (IFC byte-identical).
  - `SBM.IO/PgSuper/` â€” `PgSuperReader` (.pgs BridgeDescription schema v6+ or ReportExportAgent export XML; strand key
    bits per WBFL PsStrand; end distances normalized to CL-bearing-to-end; straight-strand `DebondInfo` Strand/Length1/Length2
    -> StrandPoint debond lengths) and `PgSuperImport.Apply` (onto an alignment).
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
  - Steel TUB girders (OPTIONAL, WIP proof of concept; `GirderLayoutDefinition.Tub` = `Bridge/TubGirderDefinition.cs`, null = I-girders, omitted from the .sbm):
    `Steel/TubSection.cs` (section math in the ROTATED section about the top working point = girder path; `TubFrame`, `AtInPlane` for skewed support planes),
    `Steel/TubGirderGenerator.cs` (partial SteelModelGenerator, `RunTub` replaces `Run` when `IsTub`: plates from `Section.Layouts` (top flange width = one
    flange), splices, panel points (`TubPanelSegment`), stiffeners, internal K frames + WT top struts, WT top laterals, external / support diaphragms,
    pad + beveled-plate bearings + seats, hatches, studs, tension zones), report `SteelModel.Tub` (`TubGirderReport`), haunches `Concrete/ConcreteTubHaunch.cs`.
    FramingGenerator skips cross frames / stiffeners for tubs. New part categories `RolledTee` (IfcMember; WT top struts = role TopChord -> .CHORD.,
    top laterals .BRACE.) / `AccessDoor` (IfcPlate 'ACCESS_DOOR') appended AFTER main's arch categories (enum order). Support diaphragm top flanges carry
    2 studs @ 1'-0 (p.283) - girder studs are the parts whose assembly is not XSD*. Path distances (`BearingS`, `SpliceS`, plate runs) are PLAN arc lengths
    at the top working point (the plans' girder dimensions match horizontal, not along-grade, lengths).
    Model: `samples/plans/ODOT Bridge O tub unit.sbm` = `sbm new` + `build-odot-o-tub.csx` (superstructure only, user scope). Alignment = R 881.47 arc through
    the pier 14 / 16 working points (p.259; pier 13's N/E on p.148 is a Ramp 44E point off the arc - UNRESOLVED), 44F stationing back from 301+53.72 (the
    44E = 44F equation is data only: a station-gap profile broke IfcGradientCurve continuity). Pier 13 bearing line from the two girder spans
    141'-4 / 137'-2 5/16 (reproduces 18'-11 13/16 along CL brg and CL pier 301+53.72; the p.273 angles 69 deg 02'38"/18'54" do not fit - UNRESOLVED).
    Check `samples/plans/check-odot-o-tub.csx` (74 OK / 38 DIFF, all reported: plan rounding 1/16-1/12" at pier 16 / G2 span 15, H +3/32" from the
    rotated tub (plan H = vertical stack sum), bearing offset 3 1/16-3 1/8 vs 3", pedestal seats pier 14 +0.02 ft / pier 16 +0.17 ft, G2 top tension zone
    UNRESOLVED). Tests `TubGirderTests`; links `sheet-links-odot-o.json` (49-page index). IFC: 0 schema / EXPRESS issues; validator "open" connection
    angles = multi-item SweptSolid bodies (each item closed), top strut volume ratio 0.84 = nominal weight x length of a coped WT. I-girder IFCs
    byte-identical to main (Sample, I-80 + Ramp G, Br02069, Woodbury, Northaven; FILE_NAME timestamp only).
  - `Concrete/` â€” `ConcreteGenerator` (deck slab grid solid between skewed end lines following finished grade,
    haunch sweeps, barrier sweeps, deck mats, 1% negative-moment bars, TxDOT SSTR Bars S/U/R), `BarrierProfiles`
    (SSTR per RL-SSTR Sept 2019 â€” 3/4" back-face step is an assumption; `Expand` = custom section with fillets / chamfers / edge tags),
    custom barriers `BarrierType.Custom` (`Bridge/BarrierCustom.cs`, `Concrete/ConcreteBarrierCustom.cs`; all inputs OPTIONAL and omitted from the .sbm when unused):
    `Profile` (`BarrierProfilePoint` R inward from the deck edge / H, Radius, Chamfer, surface Tag of the next edge), `PerpendicularToDeck`, `Begin/EndExtension`
    (square ends beyond the deck ends, along the toe line), `Zones` (`BarrierZone`: own section centred where a support CL crosses the deck edge, shortest zone wins,
    optional `DeckWidening` -> `ConcreteModel.DeckBumps`, part of the deck slab), `PlanLength` check. Output: one mesh per barrier + `BarrierPieces` (closed solid per
    section length, IFC items) + `TaggedSurfaces` (tagged faces incl. zone "returns") for the surface treatments. Woodbury: `samples/plans/barrier-woodbury.csx`. `Rebar.cs` (A615 sizes, AASHTO 5.10.8 Class B
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
  reusable: `python -I tools/validate_ifc.py model.ifc [--express-sample N] [--geom N] [--geom-all REGEX] [--json out]`.
  Last full-model result (2026-10-11, 347 MB, 3.76 M entities): 0 schema + 0 EXPRESS issues; see docs/plan-data/ifc-validation.md.
  Exports must be reproducible (test `Ifc_RegeneratedModel_ExportsIdentically`): anything numbered from a parallel
  generator's ConcurrentBag (stiffener marks, bar order, hole order) must be put in a fixed order first. Bar role:
  TIE/HOOP/CROSSTIE/SPIRAL/SSTR-S = LIGATURE.
  Alignments (`Ifc/IfcAlignmentWriter.cs`, same in every IfcGeometryMode): IFC 4.3 bSI alignment concepts = IfcAlignment
  (aggregated by IfcProject, IfcRelReferencedInSpatialStructure from the site; GlobalId `IfcKeys.Alignment`) nesting
  IfcAlignmentHorizontal (IfcAlignmentSegment + IfcAlignmentHorizontalSegment LINE/CIRCULARARC/CLOTHOID; radius + = left, 0 =
  straight) and, with a profile, IfcAlignmentVertical (CONSTANTGRADIENT/PARABOLICARC by distance along, cut at PVC/PVT/grade
  breaks/unsym. VPI/station equations; RadiusOfCurvature = L/(g2-g1)), each ending with a zero-length segment; geometry
  IfcCompositeCurve of IfcCurveSegment (IfcLine/IfcCircle/IfcClothoid, mapping as ifcopenshell.api.alignment) + IfcGradientCurve
  ('FootPrint'/'Curve2D' + 'Axis'/'Curve3D'; no profile: only 'Axis'/'Curve2D'); IfcReferent STATION + Pset_Stationing at the
  start and every station equation (IfcLinearPlacement on the composite curve). Local coords = minus the export base point,
  project length units. Heights = the PROFILE (PGL) elevation at the alignment's plan point, even with a PGL/pivot offset.
  NO IfcAlignmentCant / IfcSegmentedReferenceCurve: cross slopes are road superelevation about the PGL, not rail cant (the US
  bridge IDS forbids cant). API for code that places along an alignment (inside `IfcExporter.Export`): `alignmentWriter.For(name)`
  / `.Written[alignment]` -> `IfcAlignmentEntities` {Alignment, Horizontal, Vertical, CompositeCurve, GradientCurve, Referents,
  AxisCurve = gradient curve or the 2D composite without a profile} (STEP ids), and `alignmentWriter.LinearPlacement(curve, al,
  distance m from the alignment START (not a station: `al.StationToDistance`), offsetLateral (IFC: + = LEFT), offsetVertical)`.
  Check: `sbm run tools/ifc-alignment-points.csx model.sbm -- a.ifc pts.json [abv] [--alignments-only|--spiral]` then
  `python -I tools/check_ifc_alignment.py a.ifc pts.json` (ifcopenshell.geom vs SBM, 1 mm); IDS: `python -I tools/run_ids.py a.ifc x.ids`.
  Geometry modes (`IfcExportOptions(Geometry, ChordTolerance)`, `SbmDocument.ExportIfc(path, mode, chordTol)`, CLI
  `sbm export --geometry parametric|abv [--chord-tol mm]`): **Parametric** (default, byte-identical to the pre-mode output)
  = exact IfcExtrudedAreaSolid / IfcBooleanClippingResult. **ReferenceView** (`abv`, header `ViewDefinition
  [AlignmentBasedView]`) writes only geometry the bSI AbV allows (checked against usBrSpec_AbV Exclusions.ids, 261
  entities; `python -I tools/run_ids.py model.ifc <ids>`): `Ifc/IfcReferenceViewGeometry.cs` turns every recorded
  `ExtrudedSolid` into an IfcPolygonalFaceSet (end caps = IfcIndexedPolygonalFaceWithVoids, holes = void loops of an
  EQUAL-AREA polygon (volume = Parametric), as many sides (>= 8) as an inscribed polygon with sagitta <= chord tolerance, default 1/32 in = 0.79 mm -> 9 sides for a 15/16 in hole;
  sides / hole walls = planar quads) and the arc-profile prestressed beams (Parametric: extrusion clipped by two half
  spaces + IfcPlane) into ONE face set cut exactly by the skewed end planes (arcs polygonized to the tolerance).
  Triangle meshes, swept-disk bars/strands, mapped bolt types (IfcTriangulatedFaceSet sources) are already allowed and
  unchanged; element GlobalIds, materials and psets are the same (the SBM_Fabrication pset GlobalIds are keyed by STEP id,
  so they differ between modes). Prestressed girders are IfcBeam `.GIRDER_SEGMENT.` in all modes (`.GIRDER.` is not in
  IFC4X3_ADD2's IfcBeamTypeEnum; it is valid only for IfcElementAssembly / IfcBridge).
  Volumes vs Parametric: `python -I tools/compare_ifc_volumes.py par.ifc abv.ifc --changed-only --exact-ref [--max N]`
  (exact-ref = analytic extrusion volumes; OpenCascade meshes hole circles coarsely; AbV meshing is ~2x slower, ~0.15 s/plate). Cost: I-80 WB and
  Ramp G: Parametric 346 MB / 3.76 M entities / 3.7 s, ReferenceView 765 MB / 6.03 M / 6.6 s (ifczip 72 vs 177 MB);
  the growth is the ~324 k bolt holes (9 wall quads + 18 points each). Full-model AbV result: 0 schema + 0 EXPRESS issues,
  AbV Exclusions IDS 261/261 pass (docs/plan-data/ifc-validation.md). NOT done: IfcSectionedSolidHorizontal along the
  alignment. How it would plug in (alignment entities come from `alignmentWriter.For(name)`, written before the bridges):
  deck slab, barriers and girder flange/web plates would each become IfcSectionedSolidHorizontal(Directrix =
  `AxisCurve` (the IfcGradientCurve), CrossSections = IfcArbitraryClosedProfileDef over IfcIndexedPolyCurve polylines in the
  section plane (x = lateral, + = LEFT as IFC; y = up from the directrix), CrossSectionPositions = IfcAxis2PlacementLinear(
  IfcPointByDistanceExpression(distance from alignment start (`al.StationToDistance`), lateral, vertical, AxisCurve))) at
  the stations where the section changes (deck: the loft rings of `ConcreteGenerator` = deck + haunch outline per station;
  barriers: `BarrierProfiles.For` offset by the deck edge; girders: plate sections at field-section ends / thickness
  transitions, offset = girder line). Hook = a ReferenceView branch next to `Body` / the deck `FaceSet` items in
  `ExportConcrete` (same style, GlobalId, psets). Caveat: the solid ends are sections NORMAL to the directrix, so skewed
  deck/barrier/girder ends cannot be represented exactly without a clip (AbV forbids booleans) - either keep the skewed
  end zones as face sets (two items) or accept a squared end; holes / clips / bolts stay face sets.
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
  - Recent models: list logic `SBM.Core/Project/RecentModels.cs` (`RecentModelList`: newest first, max 10, one entry per full path case-insensitive,
    missing files kept until removed; tests `IsolateAndRecentTests`), stored in `%LOCALAPPDATA%\SteelBridgeModeler\recent-models.json`
    (`MainViewModel.Recent` / `NoteRecent` on every SetProject with a path = Open / command line / remote open, and on Save / Save As / remote save;
    `--snapshot` runs never write it). Open (ribbon, Ctrl+O) = unsaved-changes prompt, then `Editors/RecentModelsWindow` (rows: name, folder, last opened;
    missing greyed with Remove; Clear list; Browse... = file dialog in the newest existing folder; empty list = file dialog directly). Home > Project > Recent
    (`MainWindow.Recent.cs`) = the File > Recent menu (open, missing greyed with a Remove submenu, Remove missing files, Clear list). There is no start
    screen (the app opens the sample / the command-line model). Snapshot `--recentdialog` renders the dialog from a copy (`_recent.png`).
  - `ViewModels/MainViewModel` â€” project, undo/redo (JSON snapshots), async regeneration (Task.Run + version counter).

  - Measure / query / snaps (App): `MainWindow.Measure.cs` (tools Measure, Measure Angle, Station/Offset query; overlay Canvas
    `MeasureOverlay`, panel `MeasurePanel`, snap bar; remote `measure` / `station-offset`; snapshot `--measure fx,fy,fx,fy`,
    `--query fx,fy`, `--snapbench N`), `Viewport/SnapEngine.cs` (`SnapIndex` = BVH over triangle/segment/instance chunks, built lazily
    after each scene rebuild; ranked candidates + occlusion test), pure helpers `SBM.Core/Geometry/SnapMath.cs`, `PointQuery.cs`.
    Query cost control: best-first BVH traversal, search radius shrinks to (K-th best score + max bonus) px once K=10 candidates are known
    (exact), 4 ms traversal budget, feature-edge checks deferred to ranking. Index is debounced (one build per scene burst; prebuilt once at startup).
  - Viewport GPU memory (`MainWindow.Vram.cs`, inventory `Viewport/SceneInventory.cs`, pure planner `SBM.Core/Geometry/VramBudget.cs`, tests `VramAndExactSnapTests`):
    every model's buffers are estimated with HelixToolkit layouts (mesh vertex 52 B, line vertex 32 B, index 4 B, instance 64 B) and compared
    with the budget (Memory menu / `%LOCALAPPDATA%\SteelBridgeModeler\viewport-memory.json`; 0 = 80 % of the DXGI budget beyond the startup
    usage, `QueryVideoMemoryInfo` via `EffectsManager.Device`). `VramPlanner.Ladder` order (user's): terrain clip 400 m + image 2048 px ->
    terrain stride 2 / 3 -> detail tiles within 120 m RESIDENT (far tiles removed from `View.Items` = buffers freed; Helix creates buffers on
    first draw, so hidden-but-attached tiles still filled the GPU as the user orbited) -> steel plates without bolt holes (`MeshLod.WithoutHoles`,
    from the ExtrudedSolid recipes; scene rebuilt from the cache) -> 60 m -> far bridges (units, > 150 m from the line of sight) primary
    layers only (`VramPlanner.PrimaryLayers`) -> terrain stride 8 + 1024 px -> 30 m -> terrain off -> detail off. Terrain is planned BEFORE
    its buffers are built (`MainViewModel.PlanTerrainDisplay`, `TerrainDisplayOptions`, `ElevationGrid.ToMesh(stride, clip)`). Render OOM
    (`View.RenderExceptionOccurred`) and DXGI usage > 95 % of the budget (checked on camera settle) step one level; managed OOM while building
    the scene rebuilds without detail (`SceneBuilder.SkipDetail`). Solid layers are built per bridge and merged (`SolidLayer.PartBridges`).
    Flat-shaded meshes share vertices of coplanar neighbours (`SceneBuilder.ShareVertices`); bars are one mitred loft each (`TubeBuilder`).
    Log lines `VRAM plan` (breakdown by group / bridge / ladder), `VRAM usage` (DXGI). Snapshot options `--vram MB`, `--vramlevel N`,
    `--novramreduce`, `--noshare`; snapshots now wait for the reference load. I-80 + Ramp G: 8.1 GB before -> 5.5 GB full detail, 2.0 GB
    without holes (numbers in docs/HANDOFF.md section 0-vram).
  - Exact measure snaps: with `Geometry.ExactCurvedGeometry` ON, a Surface snap on a part whose mesh has NURBS (SteelPart, deck / haunch /
    barrier `NamedPick.Source`, SubPick) is refined onto the exact face (`NurbsSnap.TryRefine`: coarse sample + closest point, then Newton
    ray / surface; planar faces ray / plane; holes rejected); `SnapHit.ExactWorld` is used by the measure and the readout says
    "(exact, x off the mesh)". OFF = today's mesh snap unchanged.
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
  point (W.P.), optional `Spacings`, prestressed `Haunches` per girder = plan T - slab; `ShiftMeasure` AlongReference (default) | AlongGirder =
  LineShift along the span's first girder (iterated), `OffsetOrigin` LineCrossing (default) | WorkPoint = along-line offsets from the foot
  of the W.P. (support line x reference curve) on the shifted line - an aligned dimension from the W.P.; Woodbury uses AlongGirder + WorkPoint). `PerpendicularSpacings` = girders parallel to
  the first one's chord at spacings measured perpendicular to it. Same girder count in every span (else an error). It overrides the
  offset curves and `PlanGeometry` in that span (warning). `FramingGenerator.SetupSpanLayouts` -> `_chord` (support-line points S0/S1,
  layout points L0/L1 with the prestressed top of beam); `GirderSpanCurve` gets `LayoutBegin/LayoutEnd`. Back and ahead points at an
  interior support may differ (prestressed): `SupportLineResult.Bearings` = behind, `AheadBearings` = ahead (null if equal). Use
  `BridgeFraming.SpanBearing(girder, span, atEnd)` for anything that belongs to one span. For steel a jog is an error (continuous girders).
  The prestressed elevation check names the layout. Data: `samples/plans/build-woodbury.csx` / `check-woodbury.csx` (Iowa Design 817).
  `PrestressedGenerator.BuildBeam` uses `SpanBearing` and the layout bearing points. Haunch solids sample the girder path at <= 1.5 m
  (`ConcreteGenerator.HaunchSamples`, jogs kept) so the haunch follows the soffit along straight chords in every geometry mode.
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
- Dapped girder ends (OPTIONAL `SupportDefinition.GirderEndDap` = `Bridge/GirderEndDapDefinition.cs`, null = square ends, JSON omitted when null): every girder END on that support line (start on the first / its auxiliary beginning support, end on the last support; girders terminated on a cross girder are skipped) gets a bent-down top flange (`FlangeDrop` 7 1/2 over `DropRun` 7 1/2, `BendRadius` 6, lowered flat to `LowFlatFromBearing` 1'-2 past the CL bearing; CL bearing taken from the nearest bearing stiffener) and an optional web end cope (`CopeInset` 6, `CopeEdge` 10 tabs, `CopeRun` 6, `CopeRadius` 6). `Steel/GirderEndDap.cs` (profile / cope functions, `For`) used by `SteelModelGenerator` (flange underside on the profile, vertical thickness t/cos, web top follows, cope = main web + two end-tab LongPlates, top flange-web welds, bearing / other stiffeners shortened, studs lowered). Plan data `samples/plans/girder-end-daps.csx` (I-80 PDF 106 Details A/B, Ramp G PDF 93 Detail A; prints the clearance to the joint blockout).
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
  each an "UNRESOLVED (deck reinforcing): ..." warning. Steel clashes of end dam bars are reported per mark: `DeckEndDamBar.FieldCutNote` (plan note
  allowing field cutting, Ramp G 5d4) -> Info quoting it; any other mark -> "PLAN CONFLICT" warning with `ConflictNote` (plain reason); never moved. Placed: Ramp G 5d2-5d5 at Piers 5G/9G/14G; I-80 and the rest of the end dams are schedule only (reasons + pages in `docs/plan-data/deck-rebar.md` 5-6).
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
- Clash check (`SBM.Core/Checks/SteelClashCheck.cs`, `SbmDocument.Clashes`, `sbm clash`): steel parts (+ optional bolts / welds), misc items, simple drains and
  substructure concrete (concrete only vs drains / misc); depth > 1/16" = clash. Walkways as designed (`InspectionAccessDefinition.SupportOnFrames`, built AFTER the
  connections phase from the generated frames, `Steel/InspectionWalkwayDetailer.cs`), cross girder `Level` / discontinuous girder cope inputs, drain pipes one mitred
  piece (`MiscMesh.Pipe`), pier drains `NoTrough` / `ColumnNumber` / jog / fittings. Status: docs/plan-data/cutoff-diaphragms-and-inspection.md (end).
- Misc items (OPTIONAL, `BridgeDefinition.Misc` = `MiscItemsDefinition` + `DeckDrainDefinition.Trench`; null = none; `Bridge/MiscItemsDefinition.cs`, generator `SBM.Core/Misc/`
  `MiscItemsGenerator` (+ `MiscPierDrains`, `MiscOthers`, `MiscMesh`), result `MiscModel` hung on `SubstructureModel.Misc` and built in `RegenerationCache` after the substructure):
  Ramp G deck drainage (trench inlets positioned FROM THE GUTTER LINE = deck edge - EdgeStrip width, tube, grate, splash basin, 12" span collector at the printed
  deck-top-to-CL heights, pier joint collection systems at 5G/9G/14G/19G: pier collection box HUNG ON THE BACK CAP FACE (`PierDrainDefinition.CapBoxFace`, inlet 9" / outlet 1 1/2" clear
  of the face), pier pipe in the vertical plane of the box outlet along the face to CL column then down - nothing passes through the cap; trough box top 1'-8 below the deck underside; a span collector
  feeding a pier box ends over its inlet), I-80 sign truss brackets (steel + anchor bolts; post base plate = placeholder) and exit sign bases,
  crash cushion end posts, lighting (pole bases, junction boxes, conduit in the barrier). Pole base `PoleBaseDefinition.BottomAtSlabSoffit` (plan Section D-D: block bottom flush with the slab soffit at the deck edge, 2'-7 1/2 high, outer face 1'-8 1/2, R 3'-0 soffit arc 11" over 1'-11 3/4); sign truss brackets `SignBracketDefinition.PostOutboard / AngleToGirderDegrees / StubHeight` (plan Section D-D / View J-J: plate-girder cantilever outboard of the exterior girder, post outside the deck edge; null = legacy stub on the web). Rules: nothing is assumed - unknown plan values are nullable inputs / `Unresolved`
  strings raised as WARNINGS ("UNRESOLVED ..."); things by others are placeholders (`MiscElement.Placeholder`, IFC Pset `SBM_Placeholder` with the plan basis); plan bar lists are
  schedule-only `BarMark`s (`MiscBarRow`, `BarMark.Coating`). DRAIN CLEARANCE: every tube / drop pipe / collector / pier pipe is checked against the steel parts of its bridge
  (`MiscMesh.Clearance` = exact segment-triangle distance, convex ternary search) -> `MiscModel.Clearances` + CONFLICT (overlap) / TIGHT (< 1") warnings; drains are never moved.
  IFC (`ExportMisc`): IfcElementAssembly per drain / bracket / pole base, IfcWasteTerminal, IfcPipeSegment, IfcPipeFitting, IfcTank, IfcJunctionBox, IfcCableCarrierSegment,
  IfcFooting, IfcMechanicalFastener ANCHORBOLT, proxies for placeholders; deck-piercing inlets void the slab. Database: groups drainage / sign supports / crash cushion / lighting >
  assemblies > elements (`MiscItems` in ModelDatabase; keys `IfcKeys.MiscAssembly/MiscElement`), sheet rules in `sheet-links-i80-rampg.json`. Viewport layers `3D-DRAIN-*`,
  `3D-SIGN-SUPPORT`, `3D-CRASH-CUSHION`, `3D-LIGHTING`, `3D-PLACEHOLDER`. Plan data scripts (re-runnable): `samples/plans/drains-ramp-g.csx`, `signs-i80.csx`, `end-posts.csx`,
  `lighting.csx`; data/results/unresolved list: `docs/plan-data/misc-items-data.md` (section "Model").
  UNRESOLVED WARNING TEXT (user rule): every flag names the plan page AND a short plain reason why it is open - build new ones with
  `UnresolvedFlag.Text(pages, item, why, then)` (SBM.Core/Assumption.cs) -> "UNRESOLVED (p.17): CMP casing bottom - it is the bottom of the EPS fill, which is in the
  road plans, not this set. ..."; generic generators use the definition's `Source` for the page when they have one. Diagnostics have no structured reason field.
  UNRESOLVED DIMENSION vs EXISTENCE (user rule 2026-10-12): `Assumption.DimensionOnly` (`Assumption.UnresolvedDimension(...)`) = the plans clearly show the
  part, only a dimension / position is open -> the steel part stays on its NORMAL viewport layer with the status flag (pick props, IFC SBM_Status); plain
  `Assumption.Unresolved` = the part itself may not exist -> 3D-UNRESOLVED-STEEL. Applied to all tied arch parts; other generators unchanged.
UNRESOLVED FLAG (general, reusable): `SBM.Core/Assumption.cs` `Assumption{Status="UNRESOLVED", Reason (what the plans lack + PDF page), Basis (what was assumed)}` on the element
  (`MiscElement.Assumption`; set with `Flag(name, reason, basis)`). Consumers: viewport layer `3D-UNRESOLVED` (all flagged elements, orange, removed from their normal layers; reason in the pick
  properties; isolate with the layer toggles), IFC Pset `SBM_Status` {Status, Reason, Basis}, database props `status/reason/assumed` + view `v_unresolved` (with QC status), one warning per reason.
  Plan dimension that governs over a derived one (piped drains 22-25: tube 1'-8 from CL girder A) is modeled and the disagreement raised as `CONFLICT (plan vs model)`.
- Plan-set test models: `samples/plans/*.csx` (Iowa DOT Designs 116/316); data in C:\SBMData (not in OneDrive).
- Northaven Trail (TxDOT CSJ 0047-07-228, `samples/plans/build-nht.csx` -> `Northaven NHT.sbm`, check `check-nht.csx`, data
  `docs/plan-data/nht-0047-07-228/`): alignment "CL NHT (TRAILCLNEW)" built element by element from PT 102 = 20+49.30 (the PI table's touching
  reverse / compound curves overlap after coordinate rounding; curve 102's back tangent passes 5 ft from span 8, so it is left out), plan X/Y as a
  local grid in feet. Five Tx28 approach units = five bridges ("NHT Unit 1" .. "NHT Unit 5", Structure "US 75 Ped.
  Underpass at Northaven Trail ..."), Piers 4 / 7 / 13 shared (`SharedPier`). Span layouts from the PIER REPORTs: offsets along the CL pier line
  + `GirderSpanEnd.BearingOffset` (1'-0 along the girder at piers, normal at abutments, 1'-9 1/4 normal at Piers 10 / 11).
  **Arch interface (span 10 = GAP between Pier 10 and Pier 11, no girders):** Pier 10 = LAST support of "NHT Unit 3", Pier 11 = FIRST
  support of "NHT Unit 4"; support line = CL pier (station 24+17.92 / 26+19.60, skew +19.578 deg from the radial). Their
  `SubstructureDefinition.CapBlocks` hold the arch pedestals as `CapBlockKind.Pedestal` blocks ("arch bearing south" / "arch bearing north",
  3'-4 x 1'-7 at 63 deg, plan tops EL. A / E p.102, `SeatS/SeatN` = CL bearing); after regeneration the substructure unit publishes them as
  `SubstructureUnit.FixedSeats` (Name, S, N, Z): world point = `unit.World(S, N, Z)` with
  `unit = Doc.Model("NHT Unit 3").Substructure.Units.First(u => u.Name == "Pier 10")` (Pier 11 likewise in Unit 4). Pier frame: S along
  the pier line from the CL NHT point (right +), N normal (up-station +). CL arch bearing line = N +6 7/8" at Pier 10, -6 7/8" at Pier 11; the deck
  ends 9 1/4" from CL pier on the approach side (`DeckEndOffset` = -9 1/4"). B1-B3 (pedestal tops vs tie WP chain) are raised as generator warnings
  from `CapBlockDefinition.Reported`.
- Fixed-elevation pedestals (OPTIONAL, general): `CapBlockKind.Pedestal` on an as-designed cap = a pedestal whose top is a plan elevation, not a
  seat of this bridge (e.g. an arch / other-structure bearing); `CapBlockDefinition.SeatS/SeatN` (null = outline average), `Reported` (plan
  differences -> warnings). Results: `SubstructureUnit.FixedSeats`. Solid kind Pedestal (IFC IfcColumn 'PEDESTAL').
- Bearing offset from the layout line (OPTIONAL, `GirderSpanEnd.BearingOffset` + `BearingMeasure` AlongGirder | NormalToSupport, default 0 =
  old behaviour): girder positions are given on the support line (PGSuper / TxDOT "girder spacing at CL pier"), the CL bearing / layout point is
  moved this far into the span along each girder's chord (`FramingGenerator.SetupSpanLayouts`). With an offset the prestressed beam end is
  `BearingToEnd` beyond the CL bearing (along the CL, or normal to the support for NormalToSupport) = TxDOT IGND / IGEB length C-C brg + 2 x 9"
  (`PrestressedGenerator.BuildBeam`). Note `PrestressedBeam.Length` / `BearingSpan` are PLAN (horizontal) lengths.
- Per-row plan pedestals (OPTIONAL `SubstructureDefinition.PedestalSizeAlongAhead` / `PedestalSideAngleAheadDeg`, null = the common values):
  seats up-station of the CL pier (N > CapMidN) take their own along size / parallelogram angle (TxDOT "level 3'-0 seat" square to girders
  meeting the pier at different angles each side). Foreign seats of a shared pier keep their own girder numbering (`Girder` >= 1000).
- Steel TIED ARCH (OPTIONAL `BridgeDefinition.Arch` = `Bridge/ArchDefinition.cs`, omitted from the .sbm when null; design note
  `docs/plan-data/nht-0047-07-228/ARCH-DESIGN.md`): an arch is its OWN bridge with `Girders.Count = 0` (allowed only with `Arch`; FramingGenerator then
  builds support lines only, `RunArchSupportsOnly`) whose supports are the arch piers WITHOUT SharedPier and with substructure disabled (the piers and the
  fixed-elevation arch pedestals belong to the approach units; phase 2 skips arch bridges). `RegenerationCache` runs `SBM.Core.Arch.ArchGenerator`
  (pure function framing -> `ArchModel`): bridge `Steel` = `ArchModel.Steel` (`SteelModel.Arch` back-reference; ALWAYS fully qualify `SBM.Core.Arch.ArchModel`
  inside types that have an `Arch` member), `Concrete` = slab (`Deck`) + knuckle-zone slabs and curbs (`Haunches` = second deck body). Inputs in the LOCAL
  structure CS (meters): `ArchFrameDefinition` (two plan WPs station/offset + their local X, origin elevation, tilt about the plan direction of a support line,
  descending to +X; `ArchGenerator.BuildFrame`), named `Points`, ribs (circle through the chord ends, radius, inclination about the chord, AISC W bent about
  the weak axis = web normal to the arch plane; pieces between splice points, end stubs below the first / above the last splice = KnucklePlate + Assumption when
  `StubBottomZ` is set and >= 2 splices), ties (line / arc chain from a start point, start direction SOLVED so it closes on the end point, panel points by
  distance; T section swept flat in local), hangers (straight strand between pin centres TC / HC, sockets as plan-letter envelopes A/B/G/L/W/dL/Db, fin plane =
  cable x member tangent), floorbeams (between tie panel points, flange coplanar with the tie flanges, crowned web with the crown where the line crosses the
  alignment), bracing (W shapes point to point), knuckles (end flange / end web / deck plate), deck (between the tie web faces, top = finished grade, corrugated
  form deck profile -> slab bottom + sheet solid offset normal to the profile), bearings (stack built UP from the plan pedestal top, beveled upper sole, pin,
  anchor bolts, `BearingSeat` with Location). Every plan-vs-plan difference = `ArchModel.Checks` + warning, never adjusted; `ArchDefinition.Unresolved` texts
  and `SteelPart.Assumption` (new, OPTIONAL: IFC Pset SBM_Status) carry the UNRESOLVED items. New `PartCategory` (appended): ArchRib, TiePlate, FloorbeamPlate,
  Hanger, HangerSocket, ArchBracing, KnucklePlate, FormDeck; `AssemblyKind`: ArchRib, ArchTie, ArchHanger, ArchFloorbeam, ArchBracing, ArchKnuckle, ArchDeckForm.
  IFC: IfcMember ARCH_SEGMENT / SUSPENDER (IFC 4.3 has no CABLE) / BRACE, IfcPlate FLANGE/WEB_PLATE / SHEET / 'KNUCKLE_PLATE', IfcMechanicalFastener 'CABLE_SOCKET',
  assemblies ARCH / GIRDER / SUSPENSIONASSEMBLY / CROSS_BRACING / 'KNUCKLE' / 'DECK_FORM' (IfcOpenShell 0 issues). Database: group "tied arch" with assembly
  categories arch rib / tie girder / hanger / floorbeam / upper lateral bracing / knuckle / deck form / bearing (support from " at <pier>"); rules `arch-*`
  in `sheet-links-nht.json` (selectors need `"bridge_group": "Arch span 10"` - the arch bridge name has no "Unit n"). Viewport layers 3D-ARCH-*.
  NHT data: `samples/plans/arch-nht.csx` (run after build-nht.csx; finds Pier 10 / 11 in the approach bridges, reads the pier units' `FixedSeats` for the
  seat tops; without an approach model builds a test alignment + 2 supports), `check-arch-nht.csx` (hangers / ribs / ties / floorbeams / bearings / deck /
  weights vs plan tables / WireCo catalog). v2 (design note section 7; all OPTIONAL inputs): field splices (`ArchSpliceDefinition` on ribs / ties, bolt rows
  with staggered columns, `ArchModel.SpliceBolts`, AssemblyKind.ArchSplice, tie cut at `SpliceAt` + gap), fin plates (`ArchFinSettings`, `ArchRibFin` /
  `ArchTieFin`; tie fin = 2 solids), `RibWorkPoint` (p.147 HC on the rib working line; `RibAnchor` = the pin), cable connectors, floorbeam connections /
  studs / PT slots (`ArchFloorbeamConnection`, `PtSlotLength`, `StudsPerFace`), bracing crossings (`Via`, `TrimStartAt/EndAt`, `ArchBraceCrossing`),
  deck bar bill options (`ArchDeckRebar.TransCount` / `CurbStirrupCount` = bill row counts, `CurbStirrupHook`, `TopTransPerRow = 2` = two top bars per row lapped at
  CL NHT and hooked down at the ties with a return on the bottom bar (`TopTransHookOffset` / `TopTransHookReturn`, top run = listed - drop - return, centre lap = a
  CHECK row), `EdgeBarMark` D3 = one hooked edge bar per tie per floorbeam panel at the bottom bar level),
  knuckle v2 (`ArchKnuckleDetail` + `Corners`, partial `ArchGeneratorKnuckle.cs`; tie field pieces trimmed at the joint = end flange edge; the W stub is not
  built), bearings along a plan axis (`LongAxisAngleDeg`), PT solids (`ArchPtSystem`, partial `ArchGeneratorDeck.cs`, PartCategory PtStrand / PtAnchorage
  -> IfcTendon / IfcTendonAnchor), deck bars (`ArchDeckRebar` -> `ConcreteModel.Marks/Bars`, `FlutesOnBarGrid`), 2D `Drawing/Views/ArchViews.cs`
  (SheetSet arch branch), flagged steel parts on viewport layer `3D-UNRESOLVED-STEEL`. New PartCategory (appended): FinPlate, CableConnector (UHMW, 940
  kg/m3, not in SteelMass), ArchConnectionPlate, ArchStiffener, PtStrand, PtAnchorage. Remaining GAPs: bracing U-point gussets (p.129-140), railing details (the RAIL (PED)(HIGH) run on the curbs is an envelope, rail-joints-nht.csx),
  duct, D3 bars, camber. Tests `ArchTests`, `ArchV2Tests`. Knuckle visual check without a snapshot: `temp`-style dump of part triangles + a small
  kg/m3, not in SteelMass), ArchConnectionPlate, ArchStiffener, PtStrand, PtAnchorage. v3 (design note section 8): bracing gusset connections at the
  22 rib panel points (`ArchBracingConnection` / `ArchGussetPlate` / `ArchPlateDrawing`, partial `ArchGeneratorBracing.cs`): gusset flat patterns with
  TRUE arcs split at the bend line, each leg placed by its brace (welded edge on the connection plate face), braces ROLLED from the gusset geometry
  (`_braceWeb`, mean of both ends) with skewed ends at the drawn flange ends, A-A connection plates / rib stiffeners, bolts; data from the VECTOR
  linework (`tools/nht_bracing_plates.py` + generic `tools/pdfvec.py`: plan PDFs without text layer still have vector strokes - prefer them to raster
  scaling) -> `upper-lateral-bracing-plates.csv`; sheet 'U(n)' fits points U(12-n) (CONFLICT). Weight checks group by the plan quantity notes + pay
  items. Remaining GAPs: railing, duct, D3 bars, camber, tie web studs (no spacing).
  v4 (design note section 9, user review): gussets are ONE bent plate (`OneBentPlate`: brace rolls + leg slides solved together by a small
  Nelder-Mead, legs closed on the common bend line; CHECKS for the leg tilt / welded edge gap); knuckle splice + fill plates from the p.124 plan outline
  (`ArchKnuckleDetail.Splice*`, acute end corner cut, end flange bolt rows), end flange ends cut parallel to the chords; bearings hung from the steel
  (`ArchBearingDefinition.HangFromSteel`, upper sole top in the steel plane, pedestal kept = CONFLICT per p.107 note 8); cable connectors on their own
  layer 3D-ARCH-CABLE-CONNECTORS.
- True-arc plate outlines (generic): `Solids/ArcPolygon` (points + signed minor-arc radii per edge, exact area, tessellation, Sub / Map / Reversed),
  `PlateBuilder.AddPlanarArc` (mesh from the tessellation, `ExtrudedSolid.ExactOutline` keeps the arcs) -> IFC Parametric IfcIndexedPolyCurve with
  IfcArcIndex, ReferenceView tessellated to its chord tolerance. Tests `ArchBracingConnectionTests`. Tests `ArchTests`, `ArchV2Tests`. Knuckle visual check without a snapshot: `temp`-style dump of part triangles + a small
  kg/m3, not in SteelMass), ArchConnectionPlate, ArchStiffener, PtStrand, PtAnchorage. Remaining GAPs: bracing U-point gussets (p.129-140), railing,
  duct, camber (D3 bars done 2026-10-12). Tests `ArchTests`, `ArchV2Tests`. Knuckle visual check without a snapshot: `temp`-style dump of part triangles + a small
  pymupdf painter's renderer (no matplotlib in the sandbox).
- Precast PT SEGMENTAL box girder (OPTIONAL `BridgeDefinition.Segmental` = `Bridge/SegmentalDefinition.cs`, omitted from the .sbm when null; design note
  `docs/plan-data/bonner-b2500/segmental-unit5/SEGMENTAL-DESIGN.md`): own bridge with `Girders.Count = 0` (support lines only, like the arch; supports =
  bents with `Substructure.Enabled = false`). `SBM.Core.Segmental.SegmentalGenerator` (RegenerationCache phase 1 -> `ConcreteModel { Segmental = sm }`, no
  deck; phase 2 takes `sm.Substructure` as the bridge's substructure): joints at their -L- stations, WP on the profile grade, joint plane normal to the graded
  tangent and rotated by the cross slope (box square to the sloped top; soffit CL uphill of the WP vertical), segments = straight 3D chords, depth = printed
  table at the joints + `ParabolaVertexX/Depth/Coefficient` reconstruction between (flagged), bent halves (soffit step `BentSoffitTransition`, solid
  diaphragm `BentDiaphragm`, `SegmentalBent.DepthAtCl`), closures / end segments `ConstantDepth`. `SegmentalSection` (outline / void with R fillet, y DOWN
  normal to the top), `SegmentalLoft` (ONE closed shell per segment, void on the hollow rings + void cap at a diaphragm). Tendons straight joint to joint in
  their duct (`TendonLayout` Cantilever = tip faces of Dk / Uk, CentredOnClosure, FromEndSegment), transverse per segment type; bearings (generic block =
  plan max plate x assumed height, top = pedestal top + height); bents as `SubstructureUnit`s (footing outline, column envelope, cap with taper, pedestals).
  Results: `SegmentalModel` (Segments, Tendons, Bearings, BentRows = WP / soffit / DIM A / B per bent, Checks, Diagnostics), `SbmDocument.Segmental(name)`.
  IFC: IfcElementAssembly GIRDER per cantilever > IfcBeam `.GIRDER_SEGMENT.` (ObjectType TYPICAL_SEGMENT / BENT_SEGMENT / CLOSURE_JOINT / END_SEGMENT; there is no
  SEGMENT in IfcBeamTypeEnum) + IfcTendon STRAND (Pset SBM_Tendon), IfcBearing DISK; keys `IfcKeys.Seg*`. Database group "segmental box" > "segmental
  cantilever" assemblies (`ModelDatabase.Segmental`). Viewport layers 3D-SEGMENTS / 3D-UNRESOLVED-SEGMENTS / 3D-PT-TENDONS / 3D-SEGMENTAL-BEARINGS. Bonner
  Unit 5: `samples/plans/build-bonner-unit5.csx` (reads the segmental-unit5 CSVs; local grid alignment - coordinates p21-34 not extracted) ->
  `Bonner Unit 5 segmental.sbm`, `check-bonner-unit5.csx` (169 PASS / 23 DIFF, all reported), links `sheet-links-bonner-unit5.json`. Tests `SegmentalTests`.
  Not yet: 2D views, keys, rebar, temporary / bar PT, future PT pipes, blocks, openings, parapets, joints, piles, camber.
- Bonner Unit 6 (spans 30-35, 4 x FIB-96, `samples/plans/build-bonner-unit6.csx` -> `Bonner Unit 6 FIB.sbm`, reads docs/plan-data/bonner-b2500/fib-unit6
  CSVs; same -L- local grid as Unit 5): 24 SpanBeams, per-span layouts (G1 LEFT, CL bearing 1'-2" / 3'-2" bent 29 / 1'-4" bent 35), p439 strand pattern
  read from the VECTOR drawing (72 + 4 slack, 8 draped, 20 debonded), E1/E2/E3 + P sole plates, camber tables, bents 30-35 (level precast cap profile,
  two pedestal rows, 2 square columns = precast segmental flagged, one combined footing via `FootingDefinition.OnFootingOf`, battered 36" piles per the
  footing plans). Check `check-bonner-unit6.csx` (143 PASS / 12 DIFF, all reported: station rounding, plan "A" per span pair); links
  `sheet-links-bonner-unit6.json`; tests `BonnerFibTests`. Bent 29 substructure = Unit 5 model. GAPs: beam / deck bars, parapets, diaphragms, joints.
  Substructure OPTIONAL `SubstructureDefinition.Unresolved` (raised as warnings) and `FootingDefinition.OnFootingOf` (combined footing: no footing / piles of its own).
- Project georeference (OPTIONAL `BridgeProject.Georeference` = `Project/GeoreferenceDefinition.cs`, null = local grid, omitted from the .sbm): CRS name,
  datum / projection / zone, map unit, `GridScale` (map / project distance, e.g. 1 / TxDOT SAF) -> IFC IfcProjectedCRS + IfcMapConversion Scale with the
  base point converted to grid units; the model is never moved. NHT: EPSG:2276 x Dallas County SAF 1.000136 (U12 resolved from a user GPS check).
- NHT model check: `check-nht.csx` 414 PASS / 4 FAIL (2026-10-11; the 4 = arch pier cap / column CY vs the p.102 quantity table, +-0.3 CY).
  2026-10-12 (branch nht-rail-joints): + section 6 (railing LF by type / side, curb volume, SEJ LF, mow strip CY): 422 PASS / 8 FAIL after build-nht + arch-nht;
  the 4 new FAILs are reported plan differences (p.91 low rail 579.0 vs the p.84-88 layout 578.88 LF; SEJ LF 111.57 at the full deck width vs 75 + 32 = 107).
- Pedestrian railing (OPTIONAL `BridgeDefinition.Railings` = list of `Bridge/PedestrianRailingDefinition.cs`, null = none, omitted from the .sbm): generic run
  along a REFERENCE LINE at a constant alignment offset (outward = side of the offset), limits = radial lines at Begin/EndStation, else the deck end lines,
  else the first / last support line + Begin/EndLimitOffset (arch: no deck result); base = highest top of the bridge's concrete under the line at `BaseProbe`
  (`Railing/TopSurface`: deck, haunches incl. arch curbs, barriers / curbs, deck bumps; ends searched a few cm inward on skewed ends; no concrete = warning,
  run not built). Components: `Envelope` (section swept continuously = placeholder of a railing whose post / fence details are missing, mass from
  `EnvelopeMassPerLength`), `Post` (side outline polygon extruded `Thickness` along the rail, plumb, n = ceil((L - 2e)/s) + 1 equally spaced) + `BasePlate`
  + anchor bolts (`BoltInstance` Anchor), `Members` (pipe / bar handrail and rails, `MassPerLength` override), `Unresolved` / per-component `Unresolved` +
  `Basis` (-> `SteelPart.Assumption`, layer 3D-UNRESOLVED-STEEL, IFC SBM_Status, DB status props). Generator `Railing/PedestrianRailingGenerator.cs`
  (`RailingModel`: runs with length along the reference line, posts, mass), run in `RegenerationCache` phase 1 after steel / prestressed / arch for every
  bridge kind; parts merged into the bridge's `SteelModel` (created for prestressed bridges without one) + `SteelModel.Railings`. New `PartCategory`
  (appended) RailPost, RailHandrail, RailMember, RailBasePlate, RailEnvelope (excluded from `SteelMass`: `SteelModel.IsRailing`), `AssemblyKind.Railing`,
  `SteelAssembly.PredefinedType/ObjectType`, `SteelPart.Density`. IFC: one IfcRailing per run (GUARDRAIL / HANDRAIL / FENCE / BALUSTRADE, ObjectType = type
  name) aggregating IfcMember POST / MEMBER, IfcRailing HANDRAIL, IfcPlate BASE_PLATE, IfcRailing 'RAILING_ENVELOPE', anchor IfcMechanicalFastener.
  Database: group "Pedestrian railings" > assemblies "pedestrian railing". Viewport layer 3D-PED-RAILING; quantity table rows by type (LF). Tests `PedestrianRailingTests`.
- Curbs: custom barrier with `BarrierDefinition.IsCurb` (OPTIONAL) -> solids "Left/Right curb", IFC IfcKerb (USERDEFINED, ObjectType = ProfileName), DB category "curb".
- Sealed joints without blockout (OPTIONAL `ExpansionJointDefinition` fields): `ExpansionJointType.Sealed` (appended), `BlockoutWidth` 0 = no blockout (deck not
  cut back, no blockout element in IFC / DB), `BodyCentered` (device envelope +-GapAt50F/2 about the deck-end line, for plans that dimension the slab to the
  CL joint), `Designation`, `PlanLength` (check), `Unresolved` / `Basis` (-> `ExpansionJointResult.Assumption`, IFC SBM_Status, DB status).
- Mow strips (OPTIONAL `SubstructureDefinition.MowStrip` = `Bridge/MowStripDefinition.cs`): ring Width beyond each face of a rectangular column, Thickness,
  top = TopElevation or foundation top + TopAboveFoundation; `SubElementKind.MowStrip` (appended) -> IFC IfcSlab 'MOW_STRIP', DB "mow strip", layer
  3D-SUB-MOW-STRIP (flagged: 3D-UNRESOLVED-SUB); `SubSolid.Assumption` (OPTIONAL, IFC SBM_Status / DB status).
- Joint hardware (OPTIONAL, generic; `ExpansionJointDefinition.Armor` = `JointArmorDefinition`, `.CoverPlate` = `JointCoverPlateDefinition`, both omitted from the
  .sbm when null; built only on the joint with `Body`): armor plates on BOTH faces of the opening (near = this deck-end face, far = `GapAt50F` beyond; `FarPlate`)
  = vertical PL `PlateThickness` x `PlateDepth` flush with the top of slab + seal retainer bar (`BarProjection` / `BarHeight` / `BarAboveBottom`) between the deck
  edge offsets moved in by `EdgeInsetLeft/Right` (normal to the alignment, e.g. the curb toes); headed stud anchors behind each plate (`StudDrop`, horizontal leg
  `StudLength` perpendicular to the joint in plan, optional bent leg `StudBentLength` at `StudBendDegrees`, head) at `StudSpacing` max, `StudEndDistance` from the
  plate ends (n = ceil((L - 2e)/s) + 1); cover plate = `Thickness` x `Width` centred on the opening, bent `Crown` at its CL, lying on the top of slab, ends
  `EndClearance` inside the insets (along the joint), anchors in staggered `AnchorRows` from the deck-side edge at `AnchorSpacing` max (`AnchorEndDistance`).
  Geometry in the joint frame (x = normal distance from the deck-end face via `EndD`, drop below finished grade, columns = offsets), `Concrete/ConcreteJointHardware.cs`
  -> `ExpansionJointResult.Hardware` / `HardwareAssembly`, merged into the bridge's steel model by `RegenerationCache` (created for prestressed bridges).
  New `PartCategory` (appended) JointArmor, JointStud, JointCoverPlate, JointAnchor (`SteelModel.IsJointHardware`, excluded from `SteelMass`), `AssemblyKind.ExpansionJoint`
  (appended). IFC: IfcElementAssembly 'EXPANSION_JOINT' > IfcPlate 'JOINT_ARMOR_PLATE' / 'JOINT_COVER_PLATE', IfcMechanicalFastener STUDSHEARCONNECTOR / ANCHORBOLT;
  DB group "Expansion joint hardware" > assemblies "expansion joint hardware" (parts: joint armor plate / joint stud anchor / joint cover plate / cover plate anchor);
  viewport layer 3D-JOINT-HARDWARE. `DeviceName` says "seal envelope; armor modeled" when armored. Tests `JointHardwareTests`.
- Deck end block lateral limits (OPTIONAL `DeckEndBlockDefinition.LeftOffset / RightOffset`, alignment offsets; null = deck edge), e.g. a TxDOT IGTS thickened slab end
  between the girder top flanges; `Source` (name "Thickened slab end (source), support", IFC description, DB element "thickened slab end" under the joint group with the
  IFC GlobalId key `jk + "/endblock"`) and `Unresolved` / `Basis` (IFC SBM_Status). A joint definition with `Body = false` can carry just an end block (the other
  unit's side of a pier joint).
- Ground traffic barriers on a pier (OPTIONAL `SubstructureDefinition.GroundBarriers` = list of `Bridge/GroundBarrierDefinition.cs`, omitted when null): section `Profile`
  (`RailPoint` R from the BACK face toward traffic, H above the base; `FullHeight`) swept along `Path` (`GroundBarrierVertex` S / N in the pier frame + Height, linear;
  back face at the base; `TrafficSide` +1 = right of the path direction), lower vertices: points above `StretchAt` move down (the top shape is kept); base at
  `BaseElevation` or foundation top + `BaseAboveFoundation`; optional replacement pavement strip (`PavementR0/Width/Thickness`) along the whole path; `PlanLength`
  (note). `SubElementKind.TrafficBarrier` / `PavementStrip` (appended) -> IFC IfcRailing GUARDRAIL / IfcPavement RIGID, DB "traffic barrier" / "replacement pavement",
  layer 3D-SUB-TRAFFIC-BARRIER (flagged: 3D-UNRESOLVED-SUB). Built with the first column (`SubstructureGenerator.BuildGroundBarrier`). Test in `JointHardwareTests`.
- NHT TxDOT standards (2026-10-12, branch nht-txdot-std; standards read-only in `C:\SBM\dist\standards`, NOT in the plan set; SUMMARY.md section 6b):
  RAIL (PED)(LOW / HIGH) is NOT a TxDOT standard type (RL-PR3 / PR11 / PR22 / PR3-HD / CLF-RO 2019 checked: 3'-6 / 4'-0 galvanized plate-post / pilaster rails,
  8 ft chain link vs the p.89 4'-3 / 9'-10 tapered stainless posts, Item 5110 special pay item; PR11(MOD) p.65-66 is the retaining-wall rail) -> railing envelopes
  unchanged, U14 open. Joints (rail-joints-nht.csx section 3): SEJ-B (10-2024) armor PL 1/2 x 4 + Bar 1/2 x 1/4 + 5/8" bent studs at 6" between the curb toes,
  opening 2 1/2" (R.J. Watson; manufacturer UNRESOLVED), slabs end at the armor faces (`DeckEndOffset` = plan joint line - 1 1/4", abutments - 2 1/2"); BS-EJCP
  (10-2024) 1/4" x 1'-4 plates 1" inside the curb toes (14 plf x L reproduces p.91 194 / 206 lb); IGTS (rev. 11-24) thickened ends at all 10 unit ends between the
  Tx28 top flanges, 10 1/2" deep, 4'-0 from the CL joint (bars G/H/J/M not placed). Joint LF 107 = (14'-0 or 14.857 + 2 x 3 1/2" curb upturns) rounded up per
  joint; the SEJ-B 2'-0 skew termination is not the explanation. Arch slab ends 3/8" / 1/8" past the CL SEJ = inside the opening (CONFLICT, arch unchanged).
  T80SS(MOD) at Pier 10 (section 5): RL-T80SS bridge-slab section + 1'-0 lower back face (= the p.105 D-D vectors), 9'-1 / 4'-6 / 7'-11 + 15'-0 1:10 taper +
  1'-8, base shaft top + 2'-9 (flagged), pavement 2'-4 x 1'-1; Pier 11 not drawn (76.33 vs 78 LF); bars / UHMW wedge UNRESOLVED.
  Check: the approach slab lengths / SF and rail LF are compared to the CL SEJ (cut-back strips added back in check-nht.csx).
  check-nht.csx after build-nht + arch-nht: 827 PASS / 6 FAIL (4 arch pier CY +-0.3, p.91 low rail 579.0 vs 578.88 layout, T80SS 76.33 vs 78 LF);
  IFC: validate_ifc 0 schema / EXPRESS issues, new products mesh closed (BS-EJCP sleeve anchors 1.03 volume ratio = tiny Pset mass rounding).
- NHT railing / curbs / joints / mow strips: `samples/plans/rail-joints-nht.csx` (hooked at the end of build-nht.csx before its regeneration and in arch-nht.csx
  before its final regeneration; works on whatever NHT bridges exist). Curbs 10" x 4" with 3/4" chamfers (p.89, 113, 83). RAIL (PED) LOW / HIGH on the curb
  line (7'-0 from CL NHT = the p.84-88 "face of rail", LF reproduced to 0.006 ft per side), sections from the p.89 page VECTORS (1.5 pt/in) as flagged
  envelopes (posts / spacing / plates / anchors UNRESOLVED: railing detail sheets not in the set, p.84 note 2 "diagrammatic"), mass = p.83 design dead load
  50 / 100 PLF, handrail top 3'-0 (dimensioned, pipe scaled). SEJ-B(MOD) / BS-EJCP / IGTS and T80SS(MOD): see "NHT TxDOT standards" above (the 4 in
  placeholder of 2026-10-12 morning is replaced). Mow strips 2'-0 x 4" at the 12 approach piers (6.12 CY vs 6.1, top EL. D + 1'-0 flagged).
  The older GAP lines in build-nht.csx about rail / curb / joints / mow strips are superseded by this script's output.
- NHT model check: `check-nht.csx` 796 PASS / 4 FAIL (2026-10-12; the 4 = arch pier cap / column CY vs the p.102 quantity table, +-0.3 CY; section 6 = reinforcing:
  every tabulated mark count / weight, weights by element model vs plan, cover violations).
- NHT REINFORCING (2026-10-12; `build-nht.csx` one-line hook runs `rebar-nht-sub.csx` + `rebar-nht-approach.csx`, both re-runnable on a built model with an output
  path argument; data `docs/plan-data/nht-0047-07-228/bar-lists.csv` = every plan bar table + the 2.3 lb/SF deck estimates; SUMMARY.md section 5): explicit
  `PlanBarSet`s placed from the sheet dimensions - abutments 1 / 16 (A, H, S, U / U1 / U2, V, p.95/96; 36" shaft cages UNRESOLVED: FD standard not in the set),
  approach piers (cap A / B / S1 / S2 / T / U / D dowels, columns V / V1 / V2 / Z, 48" shafts FD / FV / #4 spiral, p.97-100), arch piers 10 / 11 (cap A1 / M /
  S5-S36 / T1-T4, pedestals E1-E3, column V2 / V3 + S1-S4, shafts 28 #11 + #7 hoops + V1 dowels, p.101-104; H1 and 12 T1 schedule-only UNRESOLVED), approach
  decks (A / T / OA / D / C / CS; P (PCP), G/H/J/M (IGTS), UP UNRESOLVED; CS / C need the curbs). Arch deck: 606 A1 (two hooked per row), 303 B1, 68 D3,
  606 S1 per the p.154 bill (`arch-nht.csx`). Positions that are only drawn are SCALED and say so in the set notes.

- Integral steel pier cap (OPTIONAL `SupportDefinition.IntegralSteelCap` = `Bridge/IntegralSteelCapDefinition.cs`, null = none, omitted from the .sbm; detailer
  `Steel/IntegralCapDetailer.cs`, partial SteelModelGenerator): twin (or single) I-girder cap transverse to the bridge on its own bearings at `BearingOffsets` (S along the
  support line) with `BearingTopElevations` (= cap underside; missing = UNRESOLVED, no cap, girders not cut). FINAL shape: straight chord of the TOBs, webs plumb, flanges square
  to the webs (cap frame `CapGeom`: x along the sloped axis, y = N, z normal); `Camber` (+ tolerances) recorded and compared with the girders' dead load deflection at the cap point
  (plan camber tables; key Support = cap support) -> Info / "CHECK (camber, deflecting support ...)" warning (the girder camber chord runs through the deflecting support).
  Girders are CUT in phase 1 (`SetupIntegralCaps` -> `_capCuts`, `FieldSections` treats the cap as a joint of width 2 x (web offset + tw/2 + end plate)); `BuildGirder`
  stops the top flange `TopFlangeEndBeyondCapFlange` outside the cap flange edge and copes the web under the cap flange (`CopeDepth`); no girder bearings, bearing stiffeners
  (FramingGenerator) or support cross frames on that line; ElevationCheck skips girder chains there. `CapGirderConnection`: end plate (girder assembly, part EndPlate) on the
  outer web face + ConnectorPlate inside, bolts through all three (holes in the cap web), DiaphragmPlate in the girder web plane, ContinuityPlate (mill to bear) at the bottom
  flange level, top splice over the cap (outer + 2 inner plates, flange-level filler, TAPERED fills = two-direction prisms between the inside plates and the cap flange, bolts
  over the cap through the cap flange; girder-side holes registered), studs on the splice plate; `Handholes` (elliptical web openings, bolted cover + backing plates),
  `CoverPlates` (from / to the CL bearing, `FromSoleEdge` = modeled sole plate edge + offset), bearing / jacking stiffeners (plumb, clips, top no-weld length), studs,
  `EndScreens` (L angles + mesh panel with wire-mass density, stainless bolts via `BoltInstance.Material`). Cap bearings: `BuildCapBearing` (sole tapered to the cap slope,
  Generic / Disc stack) -> `BearingSeat` Girder 2000+k, `Label`, Location / Axis (substructure takes them as seats). Report `SteelModel.IntegralCaps` (`IntegralCapReport`).
  New `PartCategory` (appended): EndPlate, ConnectorPlate, DiaphragmPlate, ContinuityPlate, CoverPlate, HandholeCover, HandholeBacking, MeshScreen; `AssemblyKind` PierCap,
  PierCapConnection. IFC: assemblies GIRDER 'INTEGRAL_STEEL_PIER_CAP' / USERDEFINED 'PIER_CAP_GIRDER_CONNECTION', IfcPlate USERDEFINED / COVER_PLATE; database group
  "integral steel cap group"; viewport layers 3D-PIER-CAP-PLATES / -HANDHOLES / -SCREENS. Tests `IntegralSteelCapTests`.
- Capless pier (OPTIONAL `SubstructureDefinition.NoCap`, `CaplessPedestalHeight`, `PlanColumnHeights`; generic `ColumnRotationDeg` turns rectangular columns, + = right end
  up-station): `SubstructureGenerator.BuildCapless` - per column the nearest seat, pedestal (plan size, turned) from column top = seat - pedestal height, column, foundation;
  `SubstructureUnit.NoCap`, CapTop / CapBottom = column tops; rebar not placed; BentViews draw the integral steel cap outline and a plan Section A-A of the turned column.
- Per-support girder stiffeners (OPTIONAL): `SupportDefinition.BearingStiffener` (plate) and `JackingStiffenerOffsets` (plumb pairs at distances from the CL bearing).
- Fabrication attributes (OPTIONAL `BridgeDefinition.Fabrication` = `FabricationRule` list, Bridge/FabricationDefinition.cs): selectors (category / assembly kind / mark /
  assembly description / description regex, girders) -> `SteelPart.Fabrication` (`FabricationAttributes`: Toughness, ContactFinish, NDT, coating exterior / interior,
  ShopAssembly, Tolerance, Camber, Weld, plan note ids) and optional `Material` override; applied at the end of steel generation. App pick properties, IFC Pset
  `SBM_FabricationAttributes` (one per distinct set), database props `fab_*`. Absent = no output change (IFC of the other sample bridges byte-identical, 2026-10-12).
- Nebraska St. Ramp B (Iowa DOT Design 1617, "911"; PDF "B00167 - Nebraska Ramp B - Design 116.pdf"): `samples/plans/build-nebraska-st.csx` (sbm new + run) ->
  `Nebraska St Ramp B.sbm`, check `check-nebraska-st.csx`, links `sheet-links-nebraska-st.json`, data `docs/plan-data/nebraska-ramp-b-b00167/`. Placement APPROXIMATE in UTM 14N
  (GPS = CL at STA 2475+52.56, p.27/p.29 north arrows true az 296.262 minus convergence), 3DEP terrain. Pier 2 = integral cap + capless turned columns.

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
