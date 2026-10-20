# Bonner B-2500 (Marc Basnight Bridge): location, georeference, terrain (2026-10-19)

## CRS / datum (plan sheets S0-34 / S0-35 = PDF p.35 / p.36, general notes p.17)
- "LOCALIZED PROJECT COORDINATES ARE BASED ON THE NAD83/NSRS 2007 (CORS96) STATE PLANE COORDINATES FOR B2500-2 - N 751499.622 E 3031964.117 (US FT) - USING AN
  AVERAGE COMBINED GRID FACTOR (LOCALIZED TO GRID) OF 0.99991846. ALL LINEAR DIMENSIONS ARE LOCALIZED HORIZONTAL DISTANCES, UNITS ARE US SURVEY FOOT. VERTICAL DATUM IS NAVD88."
- B2500-2 = GPS monument at -L- STA 26+47.55, 23.82 ft RIGHT, EL 5.59.
- CRS = NAD83 / North Carolina (ftUS) = **EPSG:2264** (N 751,499 / E 3,031,964 = 35.7652 N, 75.5207 W, Oregon Inlet: checked). The plan names NAD83(NSRS2007); the
  difference to NAD83(2011) (the DEMs) is centimetres. Horizontal: the plan coordinates are GROUND (localized) = grid / 0.99991846, scaled about B2500-2 (the usual NCDOT
  localization keeps the control point's published values; the plan does not say the scale centre in words - ASSUMED, effect <= 1.2 ft over the bridge).
  `BridgeProject.Georeference`: EPSG:2264, GridScale 0.99991846, NEW optional `ScaleCenterX/Y` (= B2500-2 in metres; default null = scale about the origin, all other models unchanged).
  Project X / Y = plan US ft x 1200 / 3937 (metres).
- Vertical NAVD88 (plan elevations in ft; MHW +0.4, MLW -0.5 on the general drawing; p.35 gives "+6.2 ft (NGVD)" design high water - the one NGVD29 value in the set).

## -L- (what the plans give, what had to be derived)
- The structure plans print NO N / E for PI / PC / PT. Printed: curve data (p.3 / p.21-34 / p.35-36), the bearings N 80-29-29.1 W (tangent after curve 1, 35+56.07 to 53+29.58),
  N 45-04-44.5 W (curve 2 PT to curve 3 PC), N 0-44-23.4 E (curve 3 PT to curve 4 PC), station of every PC / PT, chord lengths (long chord 794'-6 1/4" etc.), W.P. stations.
- Placement: curve 1 (PC 18+13.45, PI 27+27.13, R 2,347.00 LT, delta 42-32-29.0) with the end tangent N 80-29-29.1 W (stations increase towards Nags Head / north,
  p.35 / p.36 "TO RODANTHE" at the begin) and the monument at STA 26+47.55 / 23.82' RT fixes the whole line. `docs/plan-data/bonner-b2500/L-coordinates.csv` (tool `tools/bonner-geo/lcsv.py`).
- Checks: long chord of p.21, WP#1 28+68 to WP#7 36+66 = 794.522 ft (plan 794'-6 1/4" = 794.521); unit 1 chord 169.96 (plan 169'-11 5/16" = 169.93);
  the chain through all four curves closes to the printed PT stations within 0.01 ft and the printed bearing within 0.4" (rounding). The direction of the stationing was
  also validated against the terrain: the alignment runs over water between the two shores, bents land on the plan's ground line (below).
- The local grid used earlier (PC 2 at 10,000 / 10,000 ft) was a pure translation (same bearings), so the models were rebuilt with PC 2 at its true localized coordinates
  (E 3,029,357.322 / N 752,089.964 US ft): both build scripts, checks unchanged (Unit 5 169 PASS / 23 DIFF, Unit 6 143 PASS / 12 DIFF).

## Terrain
| source | what | size | use |
|---|---|---|---|
| NOAA NCEI "NCEI_ninth_Topobathy_2014_8483" `ncei19_n36x00_w075x75_2019v2.tif` (1/9 arc-second, ~3 m, NAD83, NAVD88, topography + bathymetry) | https://noaa-nos-coastal-lidar-pds.s3.amazonaws.com/dem/NCEI_ninth_Topobathy_2014_8483/NC/ncei19_n36x00_w075x75_2019v2.tif | 185 MB (only a 670 x 474 cell window read over HTTP range) | **used** |
| USGS CoNED Carolinas topobathy 1 m (NAVD88, UTM 17N) `North_Carolina_CoNED_Topobathy_DEM_1m_version_20.tif` | https://noaa-nos-coastal-lidar-pds.s3.amazonaws.com/dem/CoNED_Carolinas_DEM_2022_9484/ | 133 GB COG (window read only, 11 MB output) | kept as `bonner-oregon-inlet-coned-1m-ncsp.flt`, NOT referenced |
| USGS TNM 3DEP 1 m `NC_HurricaneFlorence_2020_D20` x44-45 y396-397, NED ned19 topobathy 2003 | tnmaccess.nationalmap.gov | listed, not downloaded | - |

- Output (`C:\SBM\dist\terrain\bonner\`, not in git): `bonner-oregon-inlet-ncei-ninth-3m-ncsp.flt/.hdr` (601 x 514 cells, 3.000 m ground cells, 1.2 MB, float32 LSBFIRST, NODATA -9999),
  window = Units 5 + 6 (STA 52+00 - 103+00) + 300 m, resampled bilinearly into the MODEL CRS (ground metres: cell centre -> US ft -> grid (scale about B2500-2) -> NAD83 lon / lat).
  Elevations metres NAVD88. Script: `tools/bonner-geo/make_terrain.py <out_dir> 300 ncei|coned` (python -I, rasterio); Reference + Georeference: `samples/plans/terrain-bonner.csx` (run on each model).
- Why NCEI and not CoNED 1 m: the CoNED water bottom over the inlet is smoothed / interpolated (flat -2 m over STA 66-91, no channel; Unit 5 bents 22-28 read -5 to -7 ft against
  the plan's -12 to -24 ft); NCEI follows the plan's ground line along Units 5 and 6.

## Ground check: plan "approximate existing ground line" (General Drawing sheets 5-9, pdf p.7-11, labels at the bents) vs the DEM at the bent CL (`tools/bonner-geo/groundcheck.py`)
| bent | STA | plan ft | DEM ft | diff |
|---|---|---|---|---|
| 22 | 69+95 | -24 | -28.0 | -4.0 |
| 23 | 73+45 | -32 | -14.5 | +17.5 (navigation channel: the plans say the inlet is dynamic and the ground elevations are subject to change; the DEM predates the 2019 channel) |
| 25 | 80+45 | -17 | -11.5 | +5.5 |
| 26 | 83+95 | -12 | -12.6 | -0.6 |
| 27 | 87+45 | -14 | -12.5 | +1.5 |
| 28 | 90+95 | -14 | -13.7 | +0.3 |
| 29 | 92+95 | -15 | -14.4 | +0.6 |
| 30 | 94+55.83 | -14 | -15.6 | -1.6 |
| 31 | 96+16.67 | -14 | -17.0 | -3.0 |
| 33 | 99+38.33 | -17 | -18.2 | -1.2 |
| 34 | 100+99.17 | -18 | -17.9 | +0.1 |
| 35 | 102+60 | -15 | -18.2 | -3.2 |
Bents 29-35 (Unit 6): within 3.2 ft (rms ~1.8); bents 26-29 within 1.5 ft. Datum: both NAVD88, no shift. (Bents 19-21, 24, 32 have no label that maps unambiguously.)
The program's own `GroundAt` at the bent stations equals the python sampling (e.g. bent 22 -28.0, bent 29 -14.4 ft), i.e. the model placement and the raster registration agree.

## UNRESOLVED
- (p.35 / p.36) which point the grid factor is applied about: assumed B2500-2 (see above).
- Bathymetry changes: the plans state the inlet is dynamic; the DEM (survey years inside the NCEI 2014-2019 compilation) misses the navigation channel at bents 22-25 by up to 17 ft.
- The north extension (-L- beyond PRC 177+20.90) is in the roadway plans, not in this set.
