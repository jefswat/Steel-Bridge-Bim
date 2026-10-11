# Steel Bridge Modeler

3D BIM modeler for steel plate girder bridges and their substructures.

## Running it
The built program is not stored in this repository (`dist\` is git-ignored). Either:

- **Download a release** (when available) from the GitHub *Releases* page, unzip it anywhere and double-click
  `SteelBridgeModeler.exe` (it opens with a sample project). `sbm.exe` beside it is the command-line tool.
  - A *self-contained* release runs on any 64-bit Windows 10/11 with nothing to install.
  - A *framework-dependent* release needs the free **.NET 10 Desktop Runtime (x64)** from Microsoft installed once.
- **Build it yourself** (needs the .NET 10 SDK):
  ```
  dotnet publish src/SBM.App -c Release -r win-x64 --self-contained false -p:PublishReadyToRun=true -o dist/SteelBridgeModeler
  dotnet publish src/SBM.Cli -c Release -r win-x64 --self-contained false -p:PublishReadyToRun=true -o dist/SteelBridgeModeler
  ```
  then double-click `dist\SteelBridgeModeler\SteelBridgeModeler.exe`. Use `--self-contained true` instead to get a folder
  that runs without the runtime installed.

Plan-set PDFs used for the sample models are not part of the repository.

## Milestone 1: what works now
**Alignments**
- Import LandXML from Civil 3D, OpenRoads, etc.: lines, circular curves, clothoid spirals (including partial
  spirals), station equations, vertical profiles (symmetrical and unsymmetrical parabolic curves) and superelevation.
- Create or edit an alignment from a PI table (radius, spiral in, spiral out), with a curve data report.
- Vertical profile by VPI table. Cross slope / superelevation by station table, with a pivot (crown) offset.
- Export LandXML.

**Bridge layout** (Superstructure tab)
- Supports and spans: stations, skews, deck end offsets, girder end extensions. You can type a span list
  ("140', 150', 140'") to re-station all supports at once.
- Girders: count, spacing (`4@9'-0"` or a list), offset from the alignment, curved or chorded, plate sizes.
- Deck: edges, slab thickness, haunch, SIP forms. F-shape barrier by default.
- Cross frames: X / K / inverted K / diaphragm, or Auto by bay aspect ratio. Orientation and arrangement follow
  AASHTO 6.7.4.2 automatically: parallel to skew when the skew is 20° or less, otherwise normal to the girders and
  staggered by bay. You set the maximum spacing and the minimum clearance from bearings.
- Stiffeners: bearing stiffener pairs, connection plates at every cross frame, and intermediate stiffeners at a
  maximum spacing.
- Field splices by station.

**Output and review**
- Framing plan in a GPU viewport, with plan and 3D views and layers you can turn on and off.
- Bearings report: station, offset, N/E, top of steel, bottom of steel, top of deck.
- Cross-frame schedule.
- DXF export: layered 3D polylines at true elevations, ready to reference in MicroStation.
- The project is saved as one parametric `.sbm` file. Undo and redo are supported.

**Units**
- US (ft-in) or SI. Inputs accept `12'-6 1/2"`, `12.5'`, `150"`, `1.2 m`, stations as `12+34.56`, and angles
  in decimal degrees or as `15°30'00"`.

## Milestone 2: 3D steel and IFC
- **Girders:** flange and web plates are swept along the true girder path (curved, superelevated, on grade).
  - Optional plate changes are entered by station, and flange width changes get 1:2.5 tapers.
  - Girders are split into field sections at the splices, with continuous flange-to-web fillet welds.
- **Stiffeners:** bearing and connection stiffeners lie in the plane of the support line or cross frame, so they are
  skewed on skewed layouts. Plates have corner clips and fillet welds. Intermediate stiffeners stop short of the
  bottom flange.
- **Cross frames:** single angles welded to gussets (heel and toe welds), with gussets bolted to the stiffeners.
  - Default bolts are A325 7/8" with correct grips and lengths.
  - X-frames get a filler plate and a bolt where the diagonals cross. K and inverted-K frames get a center gusset.
  - The Diaphragm type uses a bolted channel.
  - Angle sizes, work points, gusset thickness, laps and weld sizes are all inputs.
- **Bearings:** elastomeric pad plus a sole plate tapered to the girder grade. Bearing seat elevations are reported.
- **Material list:** plates, angles, gussets, welds and bolts (by length), with weights and total steel.
- **IFC 4.3 export:**
  - The bridge, superstructure and assemblies (girder field sections, cross frames, bearings) contain plates,
    members, bolts, welds and bearings.
  - Every part carries its mark, size, length and mass, plus material and colors.
  - The file is georeferenced with a base point (IfcMapConversion), and the alignment is included.
  - It validates with 0 errors in IfcOpenShell.

## Milestone 3: details, automation and references
- **Haunch:** measured from the top of the web to the bottom of the deck. Top flange thickness affects only stud
  lengths and top of steel.
- **Cross frames:**
  - Intermediate cross frames, connection plates and intermediate stiffeners are normal to the flanges, so they tilt
    with the grade.
  - Support frames and bearing stiffeners are plumb.
- **Holes:** every bolted plate is modeled with real holes. This covers girder flanges and webs at splices and lateral
  connections, connection and bearing stiffeners, gussets, splice plates, fillers, angles at X crossings, and masonry
  plates.
- **Field splices (NSBA style):**
  - Each flange gets an outside plate the full flange width and two inside plates; the web gets two plates.
  - Fillers are added where flange thicknesses differ.
  - Flanges of different widths taper 1:2.5 down to the narrower width at the joint.
  - Plate thicknesses and bolt rows, lines and columns are inputs.
- **Shop splices:** 1:2.5 width and thickness transitions are modeled on the larger plate.
- **Shear studs:**
  - Rows across, spacing and pitch are inputs.
  - Length is haunch − top flange thickness + penetration, rounded up, with a cover check.
  - Studs are left out over splice plates and bolt heads.
- **Disc bearings:** sole plate, upper bearing plate (lengthened for expansion), PTFE, urethane disc, shear pin,
  masonry plate with oversized holes, and anchor bolts with plate washers and nuts.
  - Guided expansion bearings get guide bars.
  - Fixity is set per support (fixed / expansion / guided).
  - Elastomeric pads are still available.
- **Lateral bracing:**
  - Top or bottom flange, in selected bays and spans, as a Warren or single-diagonal pattern.
  - Single angles are welded to gussets that are bolted to the flange and coped around the stiffener.
- **Automation:** the `sbm` command line, C# scripts (Home ▸ Run Script), and a .NET library. See `docs/API.md` and
  the examples in `samples/scripts` (C# and Python).
- **Terrain and reference models:**
  - Terrain: LandXML TIN surfaces (Civil 3D / OpenRoads), DXF 3DFACE, OBJ and STL. It shows as a shaded surface in
    3D and contours in plan.
  - The status bar shows ground elevation and grade − ground under the cursor, and the bearings report gives ground
    at each bearing.
  - Reference models: DXF lines, polylines and faces, OBJ and STL.
- **Speed:**
  - Unchanged bridges are reused when regenerating.
  - All generation runs in parallel.
  - Bolts and studs are drawn with GPU instancing.
  - Reference models load in the background and are cached.
  - The release build is compiled ReadyToRun for faster startup.

## Milestone 4: deck, barrier and reinforcing
- **Concrete solids:**
  - The deck slab follows finished grade between the skewed end lines.
  - Haunches run from the top flange to the deck bottom.
  - Barriers default to the TxDOT Type SSTR per standard RL-SSTR; generic F-shape and single-slope barriers are also
    available.
- **Deck reinforcing:**
  - Top and bottom mats, with sizes, spacings and covers as inputs.
  - Transverse bars are normal to the alignment or parallel to the skew (Auto switches at a skew limit).
  - Bars are limited to 60 ft with AASHTO Class B lap splices, or a lap override you enter.
  - Longitudinal laps are staggered.
- **Over interior supports:** additional bars bring total longitudinal steel to 1% of the deck section, with
  two-thirds in the top layer (AASHTO 6.10.1.7). The calculation is reported in Messages.
- **SSTR reinforcing:** Bars S and U (#4 at 6" maximum) and 8 Bars R, with laps per the standard.
- **Bar schedule** (Superstructure ▸ Bar Schedule):
  - The bar mark list shows size, number, length (or the range for "varies" marks), shape dimensions, weight,
    coating and location.
  - Bending diagrams are drawn to scale with out-to-out dimensions and pin diameters.
  - The list exports to CSV.
- **Quantities:** concrete in CY and rebar by size are in the Material List.
- **IFC:** the deck, rails and every bar are included (`IfcSlab`, `IfcRailing`, `IfcReinforcingBar`).

## Milestone 5: substructure and 2D views
**Bents and abutments** (Superstructure ▸ Supports & Spans ▸ Substructure tab; pick a support, then "Copy to all piers")
- **Cap top shapes:**
  - Sloped (a slope you enter, or the best fit through the bearing seats).
  - Crowned (two slopes parallel to the deck, breaking at the cross-slope pivot or a position you enter).
  - Level.
  - Follows the seats (straight between bearings).
- **Cap bottom:** parallel to the top (constant depth) or level.
- **Pedestals:**
  - Each pedestal's height = bearing seat − cap top.
  - The cap is set so the shortest pedestal is the minimum height you enter (0 = the highest bearing sits on the cap).
  - The rest are variable pedestals. Pedestals taller than a limit are reported.
- **Columns:** any number, with spacings along the skewed support line. Round or rectangular. Centered under the bearings or positioned explicitly.
- **Foundations:**
  - Drilled shafts, spread footings, or pile caps on steel HP or precast piles.
  - Under abutment caps, shafts or piles frame directly into the cap.
  - The top of foundation is entered, or set from the attached terrain (ground − cover).
- **Abutments:** a cap at the bearings plus a backwall at the end of deck, its top following finished grade.
- **Reinforcing:**
  - Caps: top and bottom bars following the cap profile, side-face bars, closed stirrups with 135° hooks.
  - Columns: verticals lapped into the shafts, plus spirals or ties.
  - Shafts: verticals plus spirals.
  - Footings: top and bottom mats.
  - Backwalls: both faces, both ways.
  - Bars over 60 ft are lap spliced. Marks are per support and appear in the Bar Schedule (filter by unit).
- **Everywhere else:**
  - Shown in 3D (caps, pedestals, columns, foundations, piles, rebar).
  - Outlined (hidden) in the plan.
  - Exported to IFC as IfcBridgePart PIER / ABUTMENT containing IfcBeam PIERCAP, IfcColumn, IfcPile (BORED / DRIVEN), IfcFooting, IfcWall and IfcReinforcingBar. Validates with 0 issues.
  - Listed in the material list (concrete by element, piles, substructure rebar).
- The Elevation Check now continues below the seat: − pedestal = top of cap, − cap depth = bottom of cap. Both are checked against the model.

**Plan sheets in the app** (switch **Model / Sheets 11x17** at the top of the viewport, or Output ▸ Sheets, **Ctrl+D**)
- The sheets are built from the live model and rebuild after every change. Each view is drawn at the largest standard
  scale that fits its area (1"=20', 1/2"=1'-0", …). Every sheet has a border and a title block (project, bridge, sheet
  title, scales, sheet number).
- **Export PDF** writes the whole set as one vector 11x17 PDF. **Export Sheets DXF** writes each sheet in paper inches.
  **Export 2D Models DXF** writes the full-size views for referencing into MicroStation.
- Command line: `sbm sheets project.sbm set.pdf`.
- **The set:**
  - Bridge layout.
  - Typical section.
  - **Girder elevations**, two per sheet: flange and web plate sizes with segment lengths, field sections with lengths
    and weights, bearings, spans along the girder, stiffeners, and cross-frame marks.
  - **Steel details:**
    - The typical intermediate and support cross frames, drawn exactly as detailed in the 3D model: angles,
      gussets, bolts, work points, welds.
    - Bearing stiffener, connection plate and intermediate stiffener sections (clips, gaps, welds).
    - Structural steel notes.
  - **Bearing details for each pier**, for anchor bolt considerations:
    - Plan of the most critical bearing: masonry plate, anchor bolts, pedestal, cap faces, cap top bars and stirrups.
    - Section along the girder: bearing stack, anchor embedment, cap bars.
    - An anchor bolt check table for every bearing at the pier: edge distance to the pedestal and cap faces,
      embedment and bottom elevation versus the bottom of the cap, and the 3D clear distance to the nearest cap bar.
  - Each bent and abutment.
  - Bar lists with bending diagrams, one page per unit. Closed stirrups and ties are bent around a 4 db pin (#5 and
    smaller; 6 db for #6–#8) with 135° hooks and 6 db (3" min) extensions, and the diagrams show the inside bend radius.

**3D view**
- **No more clipping:** the camera depth range now follows the scene, so nothing is cut off when you zoom in or orbit.
- **Section Cut** (View ▸ Section Cut, **Ctrl+Shift+S**):
  - Cut the model with a vertical plane in alignment coordinates: a cross section at a station (along any skew) or a
    longitudinal cut at an offset.
  - Drag the slider to move it live. Everything on the kept side stays visible, and the cut runs through solids,
    rebar, terrain and linework.
  - "Snap to" places the cut beside a support, along its skew, to look at a bent or abutment in elevation.
- **Select elements:** click any solid in 3D (plates, angles, gussets, bolts, studs, bearings, rebar, caps, columns,
  shafts, piles, deck, rail). It highlights in yellow and the Properties panel shows its data: mark, size, assembly,
  material, length, weight, elevations, and for rebar the mark, shape dimensions and count. Esc clears the selection.
- **Right-click menu** (a right-click without dragging; right-drag still orbits):
  - **Hide** the element under the cursor, or **Hide layer** for its whole layer.
  - **Unhide** lists every hidden element so you can bring them back one at a time, or all at once.
  - **Show layer** brings back hidden layers.
  - Hidden elements stay hidden through regeneration (matched by mark), and they work together with section cuts.

**2D views as models** (command line `sbm drawings project.sbm folder`)
- Each view is written as its own 2D DXF model, ready to reference into MicroStation or AutoCAD sheets.
- Model views are full size in feet (meters for SI), with text and dimensions sized for the scale shown.
- The views:
  - **Bridge layout:** a developed plan (straightened along the CL) drawn directly over an elevation section cut along the CL alignment, so stations line up. The elevation shows:
    - the deck cut at the CL;
    - the nearest girder beyond, with stiffeners and splices;
    - the rail beyond;
    - bearings and pedestals;
    - caps cut at the CL, with columns and foundations beyond (hidden below ground);
    - existing ground;
    - CL bearing stations, seat and cap elevations, span and bridge-length dimensions, and an elevation grid.
  - **Plan:** the same plan in true state-plane coordinates, with a north arrow.
  - **Typical section:**
    - Cut items: slab, haunches, plate girders and rails.
    - The next cross frame beyond.
    - Deck mats (transverse bars and longitudinal dots).
    - Girder spacing and overhang dimensions.
    - Cross-slope arrows and the PGL elevation.
  - **Each bent / abutment:**
    - An elevation along the skewed support line: deck and girders cut above, seats and pedestal heights per girder, the cap with top and bottom elevations, columns, foundations, ground, reinforcing and dimensions.
    - Section A–A through the cap, with its bars.
    - Abutments are drawn looking from the span toward the backwall.
  - **Bar bending sheet:** the bar list for the superstructure and each support, with a bending diagram for every non-straight mark. Drawn 1:1 in paper inches.

## Prestressed concrete girders (TxDOT) and PGSuper import

Bridge editor → **Girders** tab → **Girders = TxDOT prestressed I-girders (Tx28–Tx70)**, or **Superstructure → Import PGSuper**.

- **Shapes:**
  - TxDOT Tx28–Tx70 per standard IGD. The outlines reproduce the tabulated A and Yb.
  - Any PGSuper "Precast I-Beam" (e.g. WSDOT WF58G) is built from its library dimensions D1–D6, H, T1, T2, W1–W5, C1 — the same polygon as WBFL.
- **Strands:**
  - The IGND 2" grid is filled row by row from the A positions outward.
  - Depressed strands come from the A positions and keep 2" spacing at the ends.
  - Hold-downs are at L/20 each side of midspan (5'-0" min).
  - A PGSuper import keeps PGSuper's own strand layout: the library grid in its fill order, harped end and hold-down offsets, and an odd harped strand at the CL.
- **Ends and bearings:** IGD/IGEB rules apply.
  - Abutments: the support station is the CL bearing; the girder end is 9" beyond it, plumb and parallel to the backwall.
  - Interior bents: the nominal CL bearing is 1'-0" from the CL bent along the girder. Ends are square to the girder with the nearest corner 3" from the bent (or parallel to the bent, an option).
  - The pad moves off the nominal bearing only when needed to stay 1" inside the end.
- **Pads:** IGEB table (Pad length = 0): 8"/9" x 21", clipped 8–10" x 21" or 15" round at skewed abutments, 2¾" thick.
- **Haunch:** measured from top of beam to bottom of slab and set at the supports. Beams are straight; camber is not modeled, and a warning appears when the haunch gets thin.
- **Substructure:** one pedestal per beam end, so two per girder at interior bents.
- **Viewing and output:**
  - 3D: see-through beams with strand tubes and pads; click a beam for e, weight and haunch range.
  - IFC: IfcElementAssembly with IfcBeam GIRDER + IfcTendon STRAND, and IfcBearing ELASTOMERIC pads.
  - Sheets: "Prestressed girder details" (sections with the strand grid, elevation with the strand profile, girder schedule) replaces the steel girder and detail sheets.
- **PGSuper import** (`.pgs` from PGSuper/BridgeLink 6+, or the ReportExportAgent `*_export.xml`) places the PGSuper bridge on one of this project's alignments at a station you choose.
  - Mapped from PGSuper: spans (pier station differences), skews, girder count, spacing and reference offset, deck edges and slab, haunch = "A" − slab, girder section, strands, f'c / f'ci, bearing and end distances, pad size.
  - From the project alignment: alignment, profile and cross slope.
  - Notes and warnings appear in Messages. For example: different strands per girder (the first interior girder's are used), multiple deck points or girder groups, or files older than PGSuper 6.
- **Command line:** `sbm import-pgsuper project.sbm model.pgs ["CL Route 1"] [25+00]`.

## Units, discontinuous girders, drainage, raster terrain

- **Units / shared piers:**
  - Each unit of a viaduct is its own bridge (with the same `Structure` name).
  - Supports with the same `SharedPier` id are one physical expansion pier. It is built once, from every unit's bearings; `PierCenterOffset` sets the CL bearing to the CL pier.
  - Girders run past the CL bearing at the unit ends.
- **Discontinuous girders:** `Girders.Terminations` makes a girder end on a built-up cross girder at a given distance past a support.
  - Bearings beyond the end are dropped.
  - Cross frames re-bay to the next continuing girder.
  - The cross girder is set by `Girders.CrossGirder` and connects with L5x5x1/2 angles.
- **Variable-width decks:** `Deck.LeftEdgeTable` / `RightEdgeTable` give station/offset points. The slab, barriers and rail bars follow them.
- **Deck drains:** `Drains` gives the station, side, type, outlet (free fall or piped to the nearest pier) and splash basin.
  - Drains generate inlets, downspouts or collectors, and 10' x 10' erosion-stone basins on the terrain.
  - In IFC they export as IfcWasteTerminal, IfcPipeSegment and IfcCourse ARMOUR.
- **Raster DEMs:** a terrain reference can be an ESRI float grid (`.flt` + `.hdr`) or an ASCII grid (`.asc`).
  - Elevations use the full grid; display is decimated (`DisplayMaxVertices`).
  - A georeferenced image (JPG/PNG + world file) can be draped on the terrain (`ImagePath`).
  - `samples/scripts/fetch_mn_lidar.py` downloads MN 0.5 m lidar + Esri imagery.
- **Plan-set test models:** `samples/plans/` has scripts that build Iowa DOT Design 116 (WB I-80, three units) and Design 316 (Ramp G, four units) from the PDFs, geolocated to UTM 15N on USGS 3DEP lidar.

## Viewport navigation (large models)

- **View controls** (top-left of the viewport):
  - **Plan / 3D**.
  - **Terrain** on/off.
  - **Views ▾**: from N, NE, E, SE, S, SW, W or NW at a look-down angle (default 45°), plus the detail drawing distance.
  - **Extents** and **Window** (drag a rectangle to zoom to it; Esc cancels).
- **Rotate and zoom points:** the wheel zooms toward the cursor and right-drag orbits about the point under the cursor. Both use a plane through the view target instead of hit-testing every triangle, so mouse presses stay instant on very large models.
- **Detail drawing:** bolts, studs, anchor bolts, welds and rebar are drawn in 40 m tiles. They are hidden while the view moves and reappear about ¼ s after it stops. Only tiles within the detail distance of the view center are drawn (Views ▾; 0 = all). On the I-80/Ramp G model (54 M triangles), navigation went from about 25 fps to 60 fps.
- **Terrain is reference-only:** it is linked by file path, never saved in the project and never exported to IFC or DXF. Sheets only show the existing ground line cut from it.

## Driving the running app

`sbm app …`, the `SbmApp` class and `App` in scripts control the copy that is open on screen (a local named pipe, current
user only): get/set any input, regenerate, open/save, import PGSuper, run a script, snapshot, switch views, section cuts,
read messages, undo, close. The other `sbm` commands and `SbmDocument` work on project files headlessly. See docs/API.md.

## Elevation Check and navigation
- **Elevation Check** (Review ▸ Elevation Check, or **Ctrl+E**) shows each bearing as a hand calculation:
  - Profile grade at the station, with the tangent or vertical curve data (PVC, g1, g2, L, x) and the formula.
  - Cross slope × offset, saying which superelevation rows it was interpolated between, then top of deck.
  - − slab, − haunch (top of web), − web, − bottom flange (bottom of steel / top of sole plate).
  - − sole plate at CL bearing (minimum + |grade| × length / 2), then the bearing plates, PTFE, disc or pad.
  - The result is bottom of bearing (bearing seat).
- Every chain is compared with the generated 3D model, and a ✓ or the difference is shown.
- Any station and offset gives finished grade there. Pick a girder to carry the chain down to bottom of steel at that
  station.
- **Copy Selected** puts the calculation on the clipboard. **Export All** writes a text report or a CSV.
- Command line: `sbm elevations project.sbm`.
- **Find a command** (top right, **Ctrl+K**): type part of a name, such as "elev", "bar" or "ifc", then press Enter.
  The ribbon switches to that command's tab, so you also learn where it lives.
- The **Review** tab collects the checks and reports: Elevation Check, Bearings, Cross Frames, Material List, Bar
  Schedule and Messages.

## Mouse
- **Plan view:** wheel to zoom, middle-drag to pan.
- **3D view:** right-drag to orbit.
- **Home** zooms to extents. **F5** regenerates.

The status bar shows the cursor's N/E coordinates and its station, offset and finished-grade elevation.

## Samples
`samples\` contains a LandXML alignment (spiral–curve–spiral with superelevation), the matching project file, and
an example framing plan DXF.
