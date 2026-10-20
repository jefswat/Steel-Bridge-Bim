# ODOT Bridge O tub unit - terrain (2026-10-19)

- CRS of the model X / Y: plan E / N as printed (p.148, p.254-259), used as ft. **UNRESOLVED (U-GEO): the plans do not print the datum / zone** (no "NAD", "state plane", "zone" text in the 300 pages; OK County, I-235 / I-44 Oklahoma City).
  Set as NAD83 Oklahoma North (EPSG:2267, ftUS, grid scale 1, no surface factor stated). Evidence: Pier 14 working point E 2113643.09 N 193275.14 -> lat 35.5304 N, lon 97.5124 W (the I-44 / I-235 / US-77 interchange); DEM ground within 0.4-2.5 ft of the boring ground elevations (below). NAD27 not excluded by the plans themselves, but the DEM / boring agreement favours NAD83.
- Source (public USGS 3DEP): `USGS_1M_14_x63y394_OK_Panhandle_2018_D18.tif`, 300.2 MB, NAD83 UTM 14N, NAVD88 m. Saved `C:\SBM\dist\terrain\odot-o\`.
  2018 lidar = AFTER the Phase 2B construction (ramps / fill present; existing ground of the plans' time is not recoverable).
- Placement: `tools/terrain/terrain_clip.py ... ok-north 2112400 192000 2114800 194800 1.0 ft`: plan number -> EPSG:2267 -> lon / lat -> UTM 14N -> bilinear. Output `odot-o-ramp44f-3dep-1m-okn-sft.flt/.hdr`, 732 x 854 cells of 1 m (3.2808 ft), 2.5 MB, z NAVD88 ft, UnitScale 0.3048.
- `samples/plans/terrain-odot-o-tub.csx` sets Georeference + Reference, regenerates, saves.
- Ground check vs the p.254 boring top elevations (O-2..O-5 assigned to Pier 14, 15, 16, Abut 1 by order - the plan sheet gives no boring coordinates, so the pairing is assumed): Pier 14 DEM 1104.65 vs 1103.83 (+0.8), Pier 15 1105.65 vs 1105.26 (+0.4), Pier 16 1107.63 vs 1105.12 (+2.5), Abut 1 1106.61 vs 1105.08 (+1.5). Ground along the ramp 1104-1108.6 ft; the plan clearances put I-44 at ~1105. NAVD88 assumed = plan datum (not stated).
- Snapshot: `temp/odot-terrain_view.png` (worktree).
