# Georeference fit inputs - Delaware River Bridge (PTC / NJTA, 1954)

Prepared 2026-10-19 from public USGS data (user-approved). Companion to `LOCATION.md` (plan stations / bearing). Scripts: `C:\SBM\temp\ptc-geo\` (`geo.py` projections, `fill.py`, `rough.py`, `fit.py`, `vcheck.py`, `reproj.py`, `naip_get.py`; run with `python -I`). No pyproj / rasterio / GDAL on this machine: UTM 18N and EPSG:2272 (LCC 2SP) are implemented in `geo.py` (checked against NOAA NCAT: UTM easting / northing 0.4 m for a 5-decimal input, scale and convergence to 7 digits; EPSG:2272 checked only for origin (1,968,500 / 0), inverse round trip, k = 1 on the standard parallels).

Accuracy in one line: PLACEMENT +-10 m along the bridge, +-5 m across, azimuth +-0.15 deg. Good for a draped model; NOT survey control. Ask the Turnpike for coordinates of the R/W monuments listed in LOCATION.md section 8 to remove this.

## 1. Files downloaded (`C:\SBM\dist\terrain\ptc\`)

| file | size | source | CRS / datum | note |
|---|---|---|---|---|
| `ptc-delaware-3dep-1m-utm18n.tif` | 18,746,761 B | USGS 3DEP `ImageServer/exportImage` (elevation.nationalmap.gov/arcgis/rest/services/3DEPElevation), 1 m, bbox 513039-515839 E, 4439951-4441551 N, 2800 x 1600 px, float32, nodata -9999, fetched 2026-10-19 | NAD83 / UTM 18N (EPSG:26918), m; NAVD88 m | bare-earth, project DelawareValley HD 2015 (the only 1 m project in TNM here) |
| `ptc-naip-utm18n-r{0,1}c{0,1,2}.jpg` (+ `.jpg.json` = tile bbox) | 6 JPEG, 3.09 MB total | USGS NAIP Plus `ImageServer/exportImage` (imagery.nationalmap.gov/arcgis/rest/services/USGSNAIPPlus), 0.5 m, RGB, same box as the DEM in 3 x 2 tiles | EPSG:26918, m | NAIP Plus catalog item year 2023, NJ (leaf-on spring; acquisition date not returned). Two flight lines: seam runs N-S through the river span (different sun / exposure left and right) |
| DERIVED `ptc-delaware-3dep-1m-utm18n.flt` + `.hdr` | 17,920,000 B | `reproj.py` from the tif (the loader reads .flt, not .tif) | EPSG:26918 m, **NAVD88 m** | xllcorner 513039, yllcorner 4439951, cell 1 m |
| DERIVED `ptc-delaware-3dep-1m-pa-south-ftus-ngvd29.flt` + `.hdr` | 19,249,952 B | `reproj.py`: bilinear resample of the DEM onto an axis-aligned EPSG:2272 grid | **EPSG:2272, US survey feet, z = NGVD29 US ft** (NAVD88 m + 0.311) / 0.3048006 | 2851 x 1688, xllcorner 2780178.167, yllcorner 296183.791, cell 3.280833 ft; use `UnitScale = 1200/3937 = 0.3048006096` (the loader scales x, y AND z) -> project metres of EPSG:2272, z in plan metres NGVD29 |
| DERIVED `ptc-naip-pa-south.jpg` + `.jgw` | 4.79 MB | mosaic of the six tiles resampled to EPSG:2272 | EPSG:2272, world file in METRES of EPSG:2272 (ftUS x 0.3048006), 0.5 m px | for `ReferenceModelDefinition.ImagePath`; unverified whether the draping code wants m or ft (world file is in m = the model's units) |

Total 47 MB (budget 600 MB). Deviation from the brief: the TNM product tile for this site is `USGS_one_meter_x51y445_DE_DelawareValley_HD_2015.tif` (464,665,969 B > the 400 MB stop limit; URL https://prd-tnm.s3.amazonaws.com/StagedProducts/Elevation/1m/Projects/DE_DelawareValley_HD_2015/TIFF/...; a second tile x51y444, 524,602,573 B, only touches the box at its edge). Neither was downloaded; the same 3DEP 1 m data was taken as a clip through the 3DEP image service. TNM has no NAIP download items here ("Imagery - NAIP" is the NAIP Plus service), hence the image-service clips. The clip is a mosaic of the service: pixel values are the 3DEP 1 m surface, but resampling / blending is not identical to the tile.

## 2. What was measured (and what was not)

IMPORTANT limits found:
- The DEM is bare earth: the 1954 deck, piers and trusses are removed (void). Piers cannot be measured in it.
- NAIP is an ortho made on a ground model: everything high above the ground is displaced radially from each flight line's nadir. The deck is 15-50 m up; the river towers are far higher. At the flight line seam the truss jumps about 17 m between the two images; the tops of the river piers / towers (what the imagery shows) are displaced by roughly 5-25 m. Pier positions from NAIP are therefore only checks, not fit points. Piers 17, 20 and all land piers are hidden under the deck and were not measured.
- Only ground-level LiDAR features are used for the fit: (a) the footprint of the bridge over the two land spans = the void the bridge classification left in the DEM (flat, low-roughness band: second-difference roughness < 0.035 m in a 7 x 7 window; 41 centre points, 23 east + 18 west, 10 m apart, bands ~18 m wide, rms of centres about the fitted line 2.8 m, max 8 m); (b) the end of the approach fills (fill-top corner) at both abutments.

Measured points (EPSG:2272, US ft; E, N):

| point | method | E | N |
|---|---|---|---|
| fitted axis, reference point (mean of the 41 band centres) | LiDAR void band | 2,784,800.378 | 299,024.902 |
| W abutment, fill-top corner (CL axis) | DEM: top reached 7 m before the interpolated slope ends | see below (along axis -3648.9 ft of the reference point) | |
| E abutment, fill-top corner | DEM | along axis +3003.3 ft | |
| Pier 18 (tower tips N + S, midpoint) | NAIP, displaced - check only | 2,784,515.4 | 299,107.7 |
| Pier 19 | NAIP, displaced - check only | 2,785,212.2 | 298,881.5 |

## 3. Transform PTC station -> EPSG:2272

Axis: orthogonal least-squares line through the 41 band centres.
- Grid azimuth of the axis in EPSG:2272 = **108.1620 deg** (east side alone 108.38, west side alone 108.36 - the two sides are offset ~9 m laterally, which is why the single line is 0.2 deg off; take +-0.15 deg).
- Same line: UTM 18N grid azimuth 109.947 deg; TRUE azimuth 110.055 deg (+-0.15) from convergence +1.8933 deg (EPSG:2272 at the site) / +0.1083 deg (UTM).
- Plan bearing S 69-54-15 E = azimuth 110.0958 deg. Difference measured - plan = -0.04 deg: **the plans' bearing is TRUE north (or an equivalent meridian within 0.15 deg)**. It is not EPSG:2272 grid: if 110.0958 were a PA South grid azimuth the fit would read 110.10 instead of 108.16 (rejected by 1.9 deg). NJ State Plane (convergence -0.213 deg) would predict a true azimuth 109.883: cannot be excluded at +-0.15 deg, but true north is the best fit. Pier long axes (tip to tip, NAIP) are at 17.3 / 18.3 deg vs 18.16 deg expected (axis - 90): independent agreement within 1 deg.
- Scale: not fitted. The two end points are only good to +-6 m (0.3 %), so scale is fixed at the theoretical EPSG:2272 grid scale at the site, **k = 0.999976** (grid / ground; elevation factor <= 3e-6 ignored). Measured corner-to-corner grid length 6652.2 ft = ground 6652.4 ft versus the plan 6609.25 ft (deck end to deck end, p.1): +43 ft = 21.6 ft beyond the plan deck end at each end, which is what a 20 ft approach slab would give (explains the fill-top corner); versus the 6572.6 ft bearing-to-bearing length the excess is 80 ft. Scale from the data would be 1.0065 (rejected: that is the end definition, not scale).
- Origin in station: the mid point of the two fill corners is set to the mid point of the plan deck ends, (646+17.796 + 712+27.047)/2 = 679+22.42. Residuals along the axis: W end corner -21.6 ft, E end corner +21.6 ft (+ = beyond the plan deck end, by construction equal and opposite).

Formula (station sta in ft, offset Y in ft left of travel, X = sta - 64621.463 from the west abutment CL bearing):
`E = Ow_E + k (X sin A - Y cos A)`, `N = Ow_N + k (X cos A + Y sin A)` with A = 108.1620 deg, `sin A = 0.950179`, `cos A = -0.311705`.

| quantity | EPSG:2272 (US ft) | EPSG:2272 (m) | UTM 18N (m) |
|---|---|---|---|
| CL bearing W abutment, STA 646+21.463 (X = 0, Y = 0) | E 2,781,357.274 / N 300,154.409 | 847,759.393 / 91,487.247 | 513,385.130 / 4,441,149.738 |
| azimuth of +X | 108.1620 deg | | 109.947 deg |
| grid scale (map / ground) | 0.999976 | | 0.99960 (UTM k0 at the site) |

Checks against the NAIP towers (displaced, NOT in the fit): Pier 18 comes out at station 679+48.6 (plan 679+39.38: +9 ft, 3 m); Pier 19 at 686+81.2 (plan 686+21.38: +60 ft); measured 18 -> 19 spacing 732.7 ft vs plan 682.0 (+51 ft = the relief displacement difference between the two flight lines at the seam: consistent with ~17 m). Lateral offset of the tower mid points from the fitted axis -10 / -8 ft (3 m). If the two tower errors are +-25 ft about a common value, the common shift is +34 ft, i.e. the whole mapping could be up to ~10 m too far east (the vertical profile below points the other way on the east side: see). UNRESOLVED.

## 4. Vertical

- NGVD29 -> NAVD88 at the bridge (40.11694 N, 74.83056 W): NOAA NCAT / VERTCON 3.0 (fetched 2026-10-19, geodesy.noaa.gov/api/ncat): NGVD29 0 m -> NAVD88 **-0.311 m (-1.020 ft)** +- 0.032 m. So NAVD88 = NGVD29 - 1.020 ft; the plans' elevations (NGVD29, "sea level datum 1929") are 1.02 ft HIGHER than the same point in NAVD88. The 3DEP DEM is NAVD88.
- NOTE: El 163.85 on the plans is the vertical point of intersection (PVI), not a point of the road. The profile's actual high point is PVI - M = 163.850 - 10.230 = **153.62 ft at 682+80.38** (curve PVC 675+98.38 El 143.39, PVT 689+62.38 El 143.39).
- Deck / ground check (`vcheck.py`; plan profile reproduced: PVT 143.39, W deck end 646+17.8 El 56.35, E deck end 712+27.05 El 75.45 NGVD29): the LiDAR ground on top of the east approach fill falls at exactly 3.0 % away from the bridge and sits +0.47 m (+1.55 ft, sd 0.02 m) above the plan profile in NAVD88, at stations measured over 71266-71857; the west fill top follows the plan vertical curve (sag min near 15.45 m) within -0.2 .. +0.07 m (mean -0.08 m). So the plan profile / datum shift are right to about 0.5 m; the east +1.55 ft = resurfacing / widening since 1954 (or, if it were pure position, a 51 ft longitudinal shift on the east side; the west sag minimum puts that side 39 ft the other way - neither is used).
- No LiDAR deck elevations (deck is removed from the bare-earth product). River water surface in the DEM is flat at about -0.3 .. -1 m NAVD88 (tidal, MSL ~0 NGVD29).

## 5. What the model needs

`BridgeProject.Georeference` (`GeoreferenceDefinition`) has only CRS name, datum / projection / zone, map unit, `GridScale`, basis: it carries NO origin and NO rotation (the model is never moved). The project X / Y must therefore already be map coordinates, as for Woodbury (UTM) and NHT (state plane). So:

1. Build the alignment in map coordinates: begin the tangent at the station of the west abutment CL bearing (646+21.463) at **E 847,759.393 m / N 91,487.247 m** (= 2,781,357.274 / 300,154.409 US ft), **grid azimuth 108.1620 deg** (math angle from +X = -18.1620 deg), stationing 64621.463 ft there, one straight tangent to 712+27.5 (6573 ft of bridge + approaches; no curves per the plans). Lengths in plan feet (ground). The existing alignment dimensions are ground; map distance = ground x 0.999976.
2. `Georeference`: `CrsName = "EPSG:2272"`, `GeodeticDatum = "NAD83"` (plans: pre-NAD83; measured against NAD83(2011) / NAD83 imagery), `MapProjection = "Lambert Conformal Conic (2SP)"`, `MapZone = "Pennsylvania South (FIPS 3702)"`, `MapUnitUsSurveyFoot = true`, `GridScale = 0.999976`, Basis = "LiDAR / NAIP fit 2026-10-19, +-10 m, azimuth +-0.15 deg; plan bearing is true north: see GEOREFERENCE.md". (Whether the NHT-style GridScale should be applied when the alignment is already built in grid distances is the model builder's call; the effect is 0.16 m over the bridge.)
3. Elevations: plan NGVD29 feet as printed; vertical datum goes in Basis (NAVD88 = NGVD29 - 1.020 ft, VERTCON 3.0). Terrain below.
4. Terrain references (`ReferenceModelDefinition`, Kind Terrain, Format Grid):
   - plan datum, EPSG:2272 option (matches step 1): `C:\SBM\dist\terrain\ptc\ptc-delaware-3dep-1m-pa-south-ftus-ngvd29.flt`, `UnitScale = 0.3048006096` (ftUS -> m, applied to x, y and z). Header values are US ft.
   - if the model is instead built in UTM 18N (zero resampling): `ptc-delaware-3dep-1m-utm18n.flt`, `UnitScale = 1`, `OffsetZ = +0.311` (NAVD88 m -> NGVD29 m), origin O = 513,385.130 / 4,441,149.738, grid azimuth 109.947, GridScale 0.9996 (UTM) - Georeference EPSG:26918, metres.
   - Image drape: `ptc-naip-pa-south.jpg` (+ `.jgw`, EPSG:2272 metres) for the 2272 option; the UTM tiles `ptc-naip-utm18n-r?c?.jpg` need their own world files (bbox in the `.json` sidecars).

## 6. UNRESOLVED

- Absolute position: +-10 m along, +-5 m across (no control; centred on the approach fill ends, the east fill-top elevation and the NAIP towers disagree by ~10 m). Ask PTC / NJTA for coordinates of the R/W monuments (STA 651+84.89, 656+84.59, 658+88.44, 671+89.44, 694+21.89, 710+38.82) or the as-built CL.
- Lateral: the footprint centre of the 2015 structure was used; the current deck is wider than the 1954 one (widened / added lanes: the void bands are ~18 m and the fill tops ~38 m across), so the 1954 CL may differ by several metres from the fitted line. The fitted axis passes 19 / 52 ft (W / E corner) from the centre of the fill tops.
- Azimuth: 108.16 +-0.15 (EPSG:2272 grid) ; the separate east / west fits give 108.38; the plan bearing is therefore treated as TRUE (grid conversion +1.893 deg at the site).
- Piers: none measured accurately (river piers 18 / 19 only from relief-displaced NAIP, piers 17 / 20 and land piers hidden). A point cloud (LAZ, class 17 bridge / piers) would fix them; the 3DEP LPC for DelawareValley 2015 was not fetched (size, no LAZ reader here).
- Whether the 1954 structure was changed in 2015 (widening visible in the NAIP; the truss and river piers appear to be the original).
- Approach slab / abutment: fill-top corner to the plan deck ends = 21.6 ft; the plans' E abutment CL (711+94.046 vs 711+92.797) not resolved.
- EPSG:2272 projection code only self-checked (no pyproj): confirm one point with an independent tool before delivering coordinates.
- Plan feet taken as US survey feet.
