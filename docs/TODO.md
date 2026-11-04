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

## Plan sets

- **Plans stored as TIFF** (user 2026-11-03): read plan sets that come as TIFF images (scanned sheets; often one multi-page .tif per set, or one .tif per
  sheet) wherever a plan PDF works today. Notes: `Doc.Project.PlanPdfs` + the sheet-links `planFolders` / `pdf` patterns, the automatic sheet index
  (`tools/plan_index_auto.py`: scanned pages go to Tesseract OCR - TIFF pages are scans by nature), `sbm db --sheets-for` page numbers, the agent review
  PDF fingerprints (SHA-256), and the reading tools (PyMuPDF opens TIFF / multi-page TIFF as a document, so most Python tools need only the file filter).
  The app's plan sheet window shows PDFs in WebView2 (Edge's PDF viewer), which cannot show TIFF: either convert each TIFF set to a PDF once (lossless
  image-only PDF beside it, fingerprinted against the TIFF) or add an image viewer with page navigation. A folder of single-page TIFFs needs a page order
  (file name sort or a list).
