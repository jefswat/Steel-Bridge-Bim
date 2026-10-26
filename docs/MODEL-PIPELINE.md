# Model pipeline: terrain, companion database and plan sheet index by default

2026-10-26. User request: "correct why agents typically never build terrain, and why the database and PDF parsing aren't happening by default".

## 1. Diagnosis (why new plan-set models ended up without terrain, database or sheet index)

State found on main (5aa61b0), 13 sample models (`sbm pipeline <model> --status`):

| model | georeference | terrain | sheet index / rules | companion db |
|---|---|---|---|---|
| Bonner Unit 5 / Unit 6 | EPSG:2264 | yes (hand-made, later task) | curated | **none** |
| Br02069 TH65 | ESRI:103708 (inferred) | yes (later task) | curated | **none** |
| I-80 WB and Ramp G | **none** (moved to UTM 15N by geolocate-cbis.csx, CRS never declared) | yes | curated | **none** |
| Nebraska St Ramp B | **none** (UTM 14N, not declared) | yes (copied from Woodbury) | curated | **none** in the repo checkout |
| Northaven NHT | EPSG:2276 | yes (later task) | curated | **none** |
| ODOT Bridge O tub unit | EPSG:2267 (inferred) | yes (later task) | curated | **none** |
| PTC Delaware River / approach trusses | EPSG:2272 (after a user decision) | yes | curated (108 raster pages, typed by hand) | **none** |
| PTC main truss 18-20 | **none** | **none** | **no rules named it** | **none** |
| Woodbury I-29 NB | **none** (UTM 14N, not declared) | yes | curated | **none** |
| Sample / Savage test | none | yes | none | none |

Every terrain was added AFTER the model by a dedicated task, never by the agent that built the model (git: `c6a901b` / `c1b54e0` "Every sample
model now has terrain" for NHT / ODOT / Br02069, `9d769f2` for Bonner; Woodbury's terrain is in its build script because that task was also a
terrain task). No model had a database. Every `sheet-links-*.json` index was typed by hand (`63272ca`, `dfbf97e`, `2c07e65`).

Root causes, each checked against the code / docs:

1. **The steps were separate, opt-in commands that nothing ran or asked for - CONFIRMED.** `sbm run <build.csx>` ran the script and returned;
   `SbmDocument.Save` writes only the .sbm; `sbm regen` printed counts. The database was only `sbm db` (src/SBM.Cli/Program.cs `case "db"`), the
   terrain only a per-bridge hand script (`samples/plans/terrain-*.csx`), the PDF parsing only `sbm db --pages`.
2. **CLAUDE.md never named them as steps of a new model - CONFIRMED.** The companion database is described as architecture inside the project list;
   "A new bridge needs its own sheet-links-<name>.json with an index" is one passive clause in the middle of a 30-line paragraph; terrain appears only
   as the `References/` / `TerrainModel` classes and per-bridge script names. There was no checklist; build scripts end with `Doc.Save()`.
3. **The app does not build the database on open either - candidate REJECTED in its strong form.** `MainWindow.Database.cs`: the database is built
   only by the panel's Create / Rebuild button (`BuildDatabaseAsync`); on open the app only RELINKS an EXISTING database (`RelinkIfNeededAsync`
   returns when `_db == null`). So a model nobody built a database for never gets one, in the app or the CLI.
4. **The sheet index had to be written by hand - CONFIRMED.** `PlanSetSpec.Sheets` (`{p,s,t}`) is the only index source besides `--pages`;
   `tools/plan_split.py` (the `--pages` path) reads ONLY the Iowa DOT 11x17 title block, splits every page into PDF + JPEG (slow, large) and is
   opt-in. Raster sets (NHT 134 of 155 pages, PTC all 108) have no text layer and there is no OCR in the toolchain (no Tesseract in the sandbox).
5. **Terrain needs a georeference that new models lack, plus network - CONFIRMED.** 5 of 13 models have no `Georeference` (three of them are in UTM
   but never declared it). Each terrain task wrote its own projection code (`terrain_northaven.py` hard-codes the bridge box and EPSG:2276,
   `ll.py` knows 3 zones, `terrain_clip.py` takes a system argument); the downloaded rasters live in `C:\SBM\dist\terrain` (git-ignored, machine
   local); `C:\SBMData` is READ-ONLY in the sandbox (found by this work: the first automatic run failed there).
6. **Failures were silent - CONFIRMED for terrain.** `SbmDocument.EnsureTerrain` skipped a missing terrain file with `continue` and swallowed read
   errors in an empty `catch`: a model copied to another machine silently had no ground. Missing link rules were only written to the build log
   (app: session.log), never as a status.
7. Small finding: "PTC main truss 18-20.sbm" (a copy of the PTC model) was not in the PTC rules' `models`, so it had no sheet links at all.

## 2. Fix: one default pipeline

`SBM.Api/ModelPipeline.cs` (pure decisions + runner), CLI `sbm pipeline <model.sbm>` (alias `sbm build-all`, src/SBM.Cli/PipelineCommand.cs):

1. **regenerate** (`--no-regen`).
2. **georeference** check: the CRS, or UNRESOLVED with what is needed (CRS from the plan's control / PI coordinates; local grid -> move it into the
   map CRS with one GPS / plan point + the north arrow, `samples/plans/geolocate-cbis.csx`; inferred CRS pattern `terrain-br02069.csx`).
3. **terrain** (`--no-terrain`, `--offline` / `SBM_OFFLINE=1`): a visible terrain reference whose file exists is kept. Else, for a georeferenced
   project in a CRS of `tools/terrain/crs.py` (NAD83 UTM 1-23N, WGS84 UTM, the state-plane zones of the sample sets + Anoka county): the model's
   plan extents (alignment between the first and last support of every bridge) + 300 m margin (`--margin`) -> `tools/terrain/auto_terrain.py`
   = USGS TNM query (3DEP 1 m DEM, else 1/3 arc-second; newest tile wins where projects overlap), tiles downloaded once into `<terrain folder>\tiles`,
   resampled into PROJECT METRES (GridScale / ScaleCenter applied like IfcMapConversion) -> `<terrain folder>\<model>\<model>-3dep.flt`, attached as
   reference "3DEP DEM (auto, sbm pipeline)" (UnitScale 1), model saved and regenerated (foundation tops follow the ground). Terrain folder:
   `--terrain-dir`, `%SBM_TERRAIN%`, `C:\SBMData\terrain` when writable, `<repo>\dist\terrain` for a model in the repository, else `<model folder>\terrain`.
   No georeference / unsupported CRS = UNRESOLVED with the next step; network failure = SKIPPED with the reason. Heights are NAVD88 (reported).
4. **plan sheet index** (`--no-index`): the rules file is found as before (`CompanionDatabase.FindLinksFile`).
   - No rules file names the model: with plan PDFs (`--pdf <file>`, repeatable, or the new optional project field `Doc.Project.PlanPdfs`) the
     pipeline writes a SKELETON `sheet-links-<model>.json` beside the model (models, plan folders, plan sets, no rules) once - it is then the user's
     file and is never rewritten; without a PDF it is UNRESOLVED ("pass --pdf").
   - Each plan set WITHOUT a curated index: `tools/plan_index_auto.py` reads the PDF -> `sheet-index-<set id>.auto.json` beside the rules file
     (machine-owned, rebuilt when the PDF or the tool is newer). Vector pages from the text layer (Iowa DOT title block, else a generic bottom-right
     title-block reader), scanned pages by PyMuPDF + Tesseract OCR when Tesseract is installed, else listed with an empty title and reported
     ("N scanned pages without a text layer: install Tesseract or write their titles"). Each page carries `src` (text / ocr / raster) and `q` (ok / low).
   - A CURATED index (`"sheets"` in the rules file) is authoritative: the pipeline does not touch it; `--reindex` builds the automatic index too and
     only pages the curated index LACKS are added (`AutoSheetIndex.Merge`, at database build time in `CompanionDatabase.ApplySheets`).
   - A rules file with no link rules = UNRESOLVED (the database gets the sheets, no element -> sheet links).
5. **companion database** (`--no-db`, `--force-db`): `CompanionDatabase.Build` (vacuumed) unless the database is newer than the model, the rules file
   and the automatic index files (UP TO DATE). Asset tables are untouched as always.

Every step prints `DONE` / `UP TO DATE` / `SKIPPED` (why) / `UNRESOLVED` (what is needed, `next:`) / `FAILED`; the last line counts the open items.
`sbm pipeline <model> --status` reports without changing anything.

**By default:** `sbm run <script.csx> <model.sbm>` runs the pipeline after the script when the script SAVED the model (check / report scripts that
do not save do not trigger it); opt out with `--no-pipeline` or `SBM_PIPELINE=0`; `--no-db`, `--no-terrain`, `--no-index`, `--offline` are passed on.
`sbm regen` prints "Model pipeline: N item(s) open" when something is missing, and terrain references that do not load are now warnings
(`SbmDocument.TerrainProblems`). `sbm new` prints the checklist pointer.

## 3. Proof runs (2026-10-26, sandbox with network)

- Nebraska St Ramp B: `sbm pipeline` -> regenerate 4.8 s, curated index 71 pages (kept), database 15,688 elements, 71 sheets, 1,781 links (1.7 s);
  georeference UNRESOLVED (model is in UTM 14N, CRS not declared); second run: database UP TO DATE. Total 7.7 s.
- Woodbury I-29 NB: regenerate 16.6 s, database 6,630 elements, 48 sheets, 684 links; `sbm db ... --sheets-for "Pier 1"` -> p.9-11 PIER DETAILS.
- ODOT Bridge O tub unit (georeferenced EPSG:2267, terrain present, curated index 49 pages): database 15,766 elements, 317 links -> "Pipeline complete:
  nothing open" (10 s).
- Terrain: a copy of Br02069 with its terrain removed, `sbm run strip.csx copy.sbm --no-db --no-index` -> the pipeline ran after the save, found 3DEP
  1 m tile x48y501 (298 MB, downloaded in ~15 s), wrote a 601 x 631 m grid (ESRI:103708 county ftUS system, 100 % valid), attached and regenerated;
  vs the hand-made terrain at the 6 support points (offsets -20 / 0 / +20 m): max 18 mm, mean 2.9 mm.
- New plan set: a copy of the Sample model + `--pdf` Nebraska (vector) + `--pdf` PTC original (raster) -> skeleton rules file, two automatic indexes
  (Nebraska 71 pages, 68 text, 3 scanned; PTC 108 scanned = no titles, "install Tesseract"), database with 179 sheets, link rules UNRESOLVED.
- Automatic index quality vs the curated indexes: Nebraska sheet no. 69 / 71, titles 62 / 71 (the rest are hand-added parentheticals); Woodbury
  43 / 48 and 43 / 48; NHT (TxDOT, 134 scanned pages with a stamped sheet number) sheet no. 153 / 155, titles need OCR.

Tests: `ModelPipelineTests` (terrain decisions incl. offline / unsupported CRS / hidden or missing references, CRS table mirrors crs.py, index merge,
skeleton = valid rules file, database up-to-date logic, writable terrain folder, PlanPdfs round trip, an offline end-to-end run: skeleton + automatic
index -> database with the sheets, second run keeps the hand-edited skeleton and finds the database up to date). Full suite 602 / 602.

## 4. Open

- OCR: no Tesseract in the sandbox; raster sets get sheet numbers only where a stamped text layer exists. Install Tesseract (and `TESSDATA_PREFIX`) for titles.
- Georeference for Woodbury, Nebraska St, I-80 + Ramp G (coordinates are UTM 14N / 14N / 15N, but no `Georeference` is set): adding it changes the
  IFC map conversion of these models (Woodbury IFC is the byte-identical regression baseline) - a user decision, not done.
- Orthoimagery (NAIP) is not fetched (DEM only).
- The app still builds the database only on request (it relinks an existing one on open); the CLI pipeline is the default path for agents.
- The databases of PTC / I-80 / NHT / Bonner / ODOT / Br02069 were not built here (big models; run `sbm pipeline <model>` when the models are next touched).
