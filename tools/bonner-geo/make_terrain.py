"""Clip + resample the USGS CoNED Carolinas topobathy 1 m DEM (NAVD88, UTM 17N, metres) to the Bonner model CRS.

Model CRS = NCDOT "localized" NC State Plane (EPSG:2264, US ft) x 1200/3937 -> metres; localized = ground; grid = B + (P - B) * CGF about the control
point B2500-2 (plan S0-34 / S0-35). Output: ESRI float32 .flt + .hdr (cell 1.000 m ground, LSBFIRST), elevation metres NAVD88.
Reads only a window of the COG (HTTP range requests) - the 133 GB file is never downloaded.
Usage: python -I make_terrain.py <out_dir> [margin_m]
"""
import math, os, sys
import numpy as np
import rasterio
from rasterio.windows import from_bounds
from rasterio.warp import transform

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lgeom import build, point

out_dir = sys.argv[1]
margin = float(sys.argv[2]) if len(sys.argv) > 2 else 300.0
source = sys.argv[3] if len(sys.argv) > 3 else "ncei"       # ncei = NCEI 1/9 arc-second topobathy 2019v2 (~3 m); coned = USGS CoNED Carolinas 1 m
S3 = "/vsicurl/https://noaa-nos-coastal-lidar-pds.s3.amazonaws.com/dem/"
URL = {"ncei": S3 + "NCEI_ninth_Topobathy_2014_8483/NC/ncei19_n36x00_w075x75_2019v2.tif",
       "coned": S3 + "CoNED_Carolinas_DEM_2022_9484/North_Carolina_CoNED_Topobathy_DEM_1m_version_20.tif"}[source]
NAME = {"ncei": "bonner-oregon-inlet-ncei-ninth-3m-ncsp", "coned": "bonner-oregon-inlet-coned-1m-ncsp"}[source]
USFT = 1200.0 / 3937.0
CGF = 0.99991846
E_B, N_B = 3031964.117, 751499.622          # B2500-2 = -L- STA 26+47.55, 23.82' RT (p.35 / p.36)
CELL = 3.0 if source == "ncei" else 1.0
S0, S1 = 5200.0, 10300.0                      # Units 5 + 6: 52+00 - 103+00

rows = build()
bx, by, _ = point(rows, 2647.55, 23.82)
dx, dy = E_B - bx, N_B - by
pts = []
for s in np.arange(S0, S1 + 1, 25.0):
    for off in (-1, 0, 1):
        x, y, a = point(rows, s, 0.0)
        pts.append(((x + dx) * USFT, (y + dy) * USFT))
pts = np.array(pts)
x0, y0 = pts.min(0) - margin
x1, y1 = pts.max(0) + margin
x0 = math.floor(x0); y0 = math.floor(y0)
ncols = int(math.ceil((x1 - x0) / CELL)); nrows = int(math.ceil((y1 - y0) / CELL))
print("extent m", x0, y0, x0 + ncols * CELL, y0 + nrows * CELL, "cells", ncols, nrows)

# cell centres (model metres) -> usft -> grid (scale about B) -> UTM 17N (NAD83(2011))
xs = x0 + (np.arange(ncols) + 0.5) * CELL
ys = y0 + (np.arange(nrows) + 0.5) * CELL            # south -> north
X, Y = np.meshgrid(xs, ys)
Eg = (X / USFT - E_B) * CGF + E_B
Ng = (Y / USFT - N_B) * CGF + N_B
with rasterio.open(URL) as ds:
    u, v = transform("EPSG:2264", ds.crs, Eg.ravel().tolist(), Ng.ravel().tolist())
    u = np.array(u).reshape(X.shape); v = np.array(v).reshape(X.shape)
    print("u", u.min(), u.max(), "v", v.min(), v.max())
    pad = 3 if source == "coned" else 1e-4
    win = from_bounds(u.min() - pad, v.min() - pad, u.max() + pad, v.max() + pad, ds.transform).round_offsets().round_lengths()
    print("window", win, "cells", win.width * win.height)
    src = ds.read(1, window=win).astype(np.float64)
    wt = ds.window_transform(win)
src[(src < -1e30) | (src == -9999.0) | ~np.isfinite(src)] = np.nan
# bilinear (pixel centres at +0.5)
fx = (u - wt.c) / wt.a - 0.5
fy = (v - wt.f) / wt.e - 0.5
i0 = np.floor(fx).astype(int); j0 = np.floor(fy).astype(int)
tx = fx - i0; ty = fy - j0
h, w = src.shape
ok = (i0 >= 0) & (j0 >= 0) & (i0 + 1 < w) & (j0 + 1 < h)
i0c = np.clip(i0, 0, w - 2); j0c = np.clip(j0, 0, h - 2)
z = ((1 - tx) * (1 - ty) * src[j0c, i0c] + tx * (1 - ty) * src[j0c, i0c + 1] +
     (1 - tx) * ty * src[j0c + 1, i0c] + tx * ty * src[j0c + 1, i0c + 1])
z[~ok] = np.nan
print("valid %.2f %%  min %.2f max %.2f m" % (100 * np.isfinite(z).mean(), np.nanmin(z), np.nanmax(z)))
os.makedirs(out_dir, exist_ok=True)
name = NAME
arr = np.where(np.isfinite(z), z, -9999.0).astype("<f4")[::-1]          # north row first
arr.tofile(os.path.join(out_dir, name + ".flt"))
with open(os.path.join(out_dir, name + ".hdr"), "w") as f:
    f.write("ncols %d\nnrows %d\nxllcorner %.3f\nyllcorner %.3f\ncellsize %.6f\nNODATA_value -9999\nbyteorder LSBFIRST\n" % (ncols, nrows, x0, y0, CELL))
print("written", name)
