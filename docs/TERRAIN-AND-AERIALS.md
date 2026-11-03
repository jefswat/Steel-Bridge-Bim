# Terrain and aerial images

Existing ground for a model: a USGS 3DEP elevation model (automatic) and, optionally, an orthoimage draped on it. Both need the model to be
**georeferenced**; per-project notes are in `docs/plan-data/<project>/TERRAIN.md` (and `ptc-delaware-river/GEOREFERENCE.md`).

## 1. Georeference first

`Doc.Project.Georeference` (in the build script, with the plan page as `Basis`):

```csharp
Doc.Project.Georeference = new SBM.Core.Project.GeoreferenceDefinition
{
    CrsName = "EPSG:2264", MapUnitUsSurveyFoot = true, GridScale = 0.99991846,     // NC State Plane ftUS, localized (plan p.35)
    ScaleCenterX = 0, ScaleCenterY = 0,                                             // the localization point (m) when the plans scale about one
    VerticalDatum = "NAVD88", Basis = "p.35 monument B2500-2, combined factor",
};
```

- The CRS must be in the projection table (`tools/terrain/crs.py` = `SBM.Core/Geometry/MapProjection.cs`): NAD83 UTM zones 1-23 N, WGS84 UTM,
  the state plane zones of the sample sets (TX North Central, NC, PA South / North, OK North, IA North / South, NE) and ESRI:103708 (Anoka). Add a zone
  to BOTH tables when a project needs another one.
- A model on a local grid (I-80, Woodbury, the PTC set as built) gets no terrain until it is moved into the map CRS: one plan / GPS point + the north
  arrow (`samples/plans/geolocate-cbis.csx` pattern). `sbm pipeline` reports this as UNRESOLVED with the next step.
- The model is never moved: map = (project - centre) x GridScale + centre (the IFC map conversion rule).

## 2. Terrain (automatic)

`sbm pipeline <model.sbm>` (also the last step of `sbm run <build script> <model.sbm>`):

- takes the model's plan extents (alignment between the first and last support of every bridge) + 300 m (`--margin`),
- queries USGS TNM for 3DEP (1 m lidar DEM, else 1/3 arc-second), downloads the tiles once into `<terrain folder>\tiles`,
- resamples into project metres -> `<terrain folder>\<model>\<model>-3dep.flt`, attaches it as reference "3DEP DEM (auto, sbm pipeline)", saves and
  regenerates (automatic foundation tops follow the ground).
- Terrain folder: `--terrain-dir`, `%SBM_TERRAIN%`, `C:\SBMData\terrain` when writable, `<repo>\dist\terrain` for a model in the repository, else
  `<model folder>\terrain`. Offline: `--offline` / `SBM_OFFLINE=1`; skip: `--no-terrain`. An existing visible terrain reference is kept.
- Heights are NAVD88 metres (3DEP). A model in another vertical datum (NGVD29 on the PTC set) is reported, not shifted.
- Single files by hand: `python -I tools/terrain/download.py <URL> <outdir>` (public USGS product, refuses > 400 MB).

Other terrain sources: References > Import Terrain in the app (LandXML TIN, DXF 3DFACE, OBJ, STL), or a reference in a script
(`ReferenceModelDefinition` with `Kind = Terrain`, `Format = Grid` for .flt grids as the pipeline writes them).

## 3. Aerial image (orthoimage)

The pipeline does NOT fetch imagery. `tools/terrain/aerial.py` downloads USGS NAIP Plus (public, 0.3-1 m) for a box and writes a JPEG + world file:

```
python -I tools/terrain/aerial.py --epsg 32119 --bbox 922600,229450,923000,229750 --px 0.5 --out C:\SBMData\terrain\bonner\ortho.jpg
                                  [--scale 0.99991846 --center CX,CY]
```

- `--epsg` must be a METRE-based code the image service knows (UTM 269xx / 326xx, or the metre variant of a state plane zone: 32119 NC, 32129 PA
  South, 32138 TX North Central, ...). `--bbox` is in that CRS (metres). Get it from the model extents or from `sbm export --kmz` coordinates.
- `--scale` / `--center` = the georeference `GridScale` / `ScaleCenterX,Y` when the model uses localized coordinates: the world file is then in
  project metres and the image lines up with the model.
- Attach it to the terrain reference so it drapes in 3D (and shows flat under the plan view):

```csharp
var t = Doc.Project.References.First(r => r.Name == "3DEP DEM (auto, sbm pipeline)");
t.ImagePath = @"C:\SBMData\terrain\bonner\ortho.jpg";    // world file (.jgw / .pgw / .wld) beside it
Doc.Save();
```

- NAIP is orthorectified on the GROUND model: anything high above the ground (bridge decks, towers) is displaced radially from the flight line;
  flight-line seams can shift tall objects by several metres. Use it for context and for ground features, never as survey control.
- Keep downloads out of git (images and DEMs are ignored); record what was fetched (source, date, box, size) in the project's TERRAIN.md.

## 4. Google Earth check

`sbm export <model.sbm> --kmz out.kmz` writes a low-resolution KMZ (see API.md "Google Earth export"): a quick way to see the georeferenced model
on the world imagery and catch a wrong CRS, grid scale or north arrow.
