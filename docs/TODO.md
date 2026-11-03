# To-do list

Requested features not built yet (newest last). Each entry: what the user asked, then notes for whoever picks it up.

## Viewer

- **Street view on an alignment** (user 2026-11-03): an option to drop to a "street view" on an alignment - an eye-level camera standing on the
  alignment at a station, looking along it, with keys / mouse to move up and down station (and look around), like driving or walking the road.
  Notes: the camera is orthographic only today (`MainWindow.Navigation.cs`, `ViewNav`); a perspective camera is needed (`DollyToward` / `NearPlaneFor`
  were written for one). Eye height above the finished grade at the picked offset (`DeckDefinition.FinishedElevation` on a bridge, else the profile /
  road surface); stations from `Alignment`. Near-view detail tiles and rebar LOD follow the view-center line of sight, so they should work as is.
- **Drop a person on the ground surface** (user 2026-11-03): place a figure ("drop a guy") on the terrain / finished ground anywhere and look from his
  eye, so the structure can be seen from the ground. Notes: ground height from the terrain references (`TerrainModel` grid queries) or
  `FinishedGround` (roads > grading > existing ground); a simple human-scale figure (about 6 ft) as a viewport object for scale; same perspective camera
  and walk keys as the street view.

## Terrain and imagery

- **TIF support** (user 2026-11-03): read GeoTIFF files directly - elevation (USGS 3DEP 1 m / 1/3 arc-second tiles, other DEMs) as terrain references
  and orthoimages (NAIP, state imagery) as draped images - instead of converting them first. Notes: today the app imports LandXML TIN / DXF / OBJ / STL
  terrain and `.flt` grids, and images as JPG / PNG + world file; the Python pipeline already reads GeoTIFF (`tools/terrain/geotiff.py`: tags,
  tiling, compression) and resamples into project metres. Needed: a .NET GeoTIFF reader (strips / tiles, deflate / LZW, the GeoKeys or a .tfw world
  file, the CRS through `SBM.Core/Geometry/MapProjection.cs` and the project's GridScale), References > Import Terrain accepting *.tif / *.tiff, and
  `ReferenceModelDefinition.ImagePath` accepting a GeoTIFF image. Large tiles (400-500 MB) need clipping to the model extents.
