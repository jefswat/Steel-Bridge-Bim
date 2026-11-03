# Steel Bridge Modeler: app user guide

How to work in the desktop app (`SteelBridgeModeler.exe`). Scripting, the command line and the .NET library are in [API.md](API.md); the
build-and-review pipeline is in [MODEL-PIPELINE.md](MODEL-PIPELINE.md) and [AGENT-REVIEW.md](AGENT-REVIEW.md).

## Starting

- Run `dist\SteelBridgeModeler\SteelBridgeModeler.exe`. With no file it opens the built-in sample; `SteelBridgeModeler.exe "model.sbm"` opens that model.
- **Home > Project**: New, Open (Ctrl+O: the recent-models list, with Browse... for the file dialog), Recent, Save, Save As, Sample.
  Open asks first when there are unsaved changes. The recent list keeps the last 10 models; missing files stay greyed until you remove them.
- Messages and errors go to the Messages tab (bottom) and to `%LOCALAPPDATA%\SteelBridgeModeler\session.log`.

## The ribbon

| Tab | Groups / buttons |
|---|---|
| **Home** | Project (New, Open, Recent, Save, Save As, Sample) - Edit (Undo, Redo, Regenerate) - Automation (Run Script: a C# `.csx` against the open model) - Units & geometry (display units, exact curved geometry on / off, level of detail) |
| **Alignment** | Import LandXML (alignments, profiles, pipe networks) - New by PIs - Edit Alignment - Export LandXML |
| **Superstructure** | New Bridge, Import PGSuper, Supports & Spans - Girders, Cross Frames, Stiffeners, Field Splices - Connections & Bearings - Studs & Laterals - Bar Schedule - Elevation Check - Deck, Barrier & Rebar |
| **References** | Import Terrain (LandXML TIN, DXF 3DFACE, OBJ, STL) - Attach Reference (DXF, OBJ, STL, LandXML surface) - Detach |
| **View** | Plan, 3D, Zoom Extents - Background light / dark - Section Cut - 2D projection (Project View, Projected Plan, Click Views) - Inquiry (Measure, Measure Angle, Station / Offset, Object Snaps, Clear Measurements) |
| **Review** | Elevation Check - Reports (Bearings, Cross Frames, Material List, Bar Schedule, Messages) - Asset (Calcs) |
| **Output** | Sheets (the generated plan set, DXF / PDF) - CAD (IFC 4.3 Model, Export Rhino (.3dm), Google Earth (.kmz), Framing Plan DXF, LandXML) |

Ctrl+K opens the command search: type any part of a button name, tab or tooltip.

Editors (Girders, Cross Frames, Supports & Spans ...) are windows of fields and tables in the display units; **Apply** regenerates without closing.
Many detailed inputs (as-designed abutments, plan bar lists, truss details, PT, misc items ...) have no editor: they are set by the plan-data
scripts (`samples/plans/*.csx`, Run Script) and survive editing in the app.

## Moving around (CAD style)

| Action | Mouse / key |
|---|---|
| Pan | middle-drag (the point under the cursor stays under it) |
| Orbit | right-drag, or Ctrl / Shift + middle-drag (turntable: yaw about vertical, no roll) |
| Zoom | wheel (about the cursor) |
| Zoom extents | double-click the middle button, or View > Zoom Extents |
| Context menu | right-CLICK (no drag) |

The orbit pivots about the surface under the cursor (or the selection with "Pivot: selection" on). The "Mouse ?" button shows the bindings.
The camera is orthographic. The north arrow (top right; View > North arrow) shows GRID north = project +Y ("grid N" with a georeference,
"local grid +Y" without).

## Selecting, properties, hiding, isolating

- Click an element: its properties appear in the Properties panel, the second row is its IFC GlobalId. Values are read-only text (select / copy).
- Right-click menu: Hide, Isolate <element>, Isolate <database group> (N elements), Isolate layer <name>, Exit isolate, Zoom to isolated.
- Keys: **I** isolate the selection (nothing selected while isolated = exit), **Shift+I** isolate the selected element's layer, **Esc** with nothing
  selected = exit isolate. The banner at the top shows what is isolated (Zoom / Exit). Exiting restores exactly the layers / hidden items you had.
- Layers: the layer list turns groups of elements on / off (e.g. 3D-REBAR-*, 3D-TRUSS-GUSSETS, 3D-UNRESOLVED-* for flagged parts).
  Flagged (UNRESOLVED) elements show their reason in the properties.

## Inquiry

- **Measure / Measure Angle**: click two (three) snapped points; snaps follow View > Object Snaps. With exact curved geometry on, surface snaps
  refine onto the exact NURBS face and the readout says so.
- **Station / Offset** (Ctrl+I): click a point -> station, offset and elevation on the nearest alignment.
- **Section Cut**: a vertical cut at a station (and skew); everything is clipped on the CPU.
- **Click Views**: while the window is open, every pick shows a plan, an elevation and a section through the picked point (hidden lines removed).
- **Project View**: a 2D hidden-line drawing of what is on screen (or of the section cut) -> DXF / PDF.

## Companion database and plan sheets

If the model has a database (`<model>.sqlite`, built by `sbm pipeline`), the database panel shows the picked element's group, properties and
linked plan sheets. The **plan sheet window** opens the element's sheets inside the app (the PDF at the linked page; movable to a second
monitor; it follows the selection until you close it). Asset items (photos, inspections, findings, calcs) are on the same panel; Review > Calcs
compares a calc package with the live model.

## Outputs

- **Sheets**: the generated plan set (layout, framing, girders, cross frames, bearings, piers, bar lists, tables) as DXF (folder / zip) or PDF.
- **IFC 4.3**: the full model (IfcBridge, US Bridge Dictionary classification, georeference when set).
- **Rhino 3DM**: exact extrusions / B-reps, fastener blocks, bars as curves, layers as the 3D view.
- **Google Earth (.kmz)**: low resolution (no bars, fasteners, welds); needs a georeference.
- **Framing Plan DXF**, **LandXML** (alignments).

## Performance

Big models (PTC, I-80 + Ramp G) load in stages: steel, concrete, details (rebar, bolts, welds) in tiles near the view. The Memory menu sets the
graphics budget; when it is exceeded the app drops detail in a fixed order (terrain resolution first, then far detail, bolt holes, far bridges).
`sbm profile <model.sbm>` times each regeneration step.
