# Northaven NHT - terrain (2026-10-19)

- CRS of the model X / Y: TxDOT surface coordinates = NAD83 Texas North Central (EPSG:2276, ftUS) x Dallas County SAF 1.000136 (U12, resolved 2026-10-11). Georeference already set; the model is not moved.
- Source (user-authorized public USGS 3DEP download): `USGS_1M_14_x70y365_TX_Pecos_Dallas_2018_D19.tif`, 317.2 MB, NAD83 UTM 14N, NAVD88 m, 2018 lidar.
  Saved `C:\SBM\dist\terrain\northaven\` (gitignored). Read with `python -I` (Pillow + own projection math, `tools/terrain/`).
- Placement: `tools/terrain/terrain_northaven.py` resamples the DEM INTO THE MODEL'S NUMBERS: cell centre (plan X, Y) -> / 1.000136 -> EPSG:2276 (ftUS) -> lon / lat -> UTM 14N -> bilinear. Output `northaven-nht-3dep-1m-txnc-sft.flt/.hdr`
  = 830 x 686 cells of 1 m (3.2808 plan ft), 2.3 MB, plan X 2498780..2501503, Y 7015050..7017300 (bridge + ~300 m), z NAVD88 ft; Reference UnitScale 0.3048.
  Projection math validated against the 2026-10-11 GPS check (32.901380, -96.769012 -> 2499728.33).
- `samples/plans/terrain-northaven.csx` sets Georeference (if null) + the Reference, regenerates, saves.
- Ground check (DEM bilinear at the CL pier, minus plan EL D = top of drilled shaft, approach-piers.csv; plans: mow strip top = existing ground, shaft top >= 1'-0 below): Pier 2 +1.2, 3 +0.7, 4 +1.3, 5 +1.8, 6 +1.2, 7 +0.8, 8 +1.9, 9 +1.7, 12 +1.7, 13 +1.1, 14 +1.0, 15 +1.2 ft (ground above the shaft top, consistent with the >= 1 ft cover; vertical datum NAVD88 assumed = plan).
  Ground at Abut 1 515.0, Pier 10/11 511.8 / 509.8, Abut 16 508.3 ft. The 2018 lidar post-dates nothing in the plans (bridge built later), so it is existing ground.
- Snapshot: `temp/nht-terrain_view.png` (worktree). UNRESOLVED: none new.
