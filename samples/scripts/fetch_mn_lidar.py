"""Download a window of Minnesota's statewide 0.5 m lidar DEM and an Esri World Imagery mosaic for terrain tests.

    python fetch_mn_lidar.py <out folder> <easting> <northing> [half size m = 1500] [image px = 8192]

Coordinates are NAD83(2011) UTM zone 15N meters (EPSG 6344 / 26915). Writes:
  dem.flt + dem.hdr   ESRI float grid (little-endian float32, NAVD88 meters, nodata -9999), read by Steel Bridge Modeler
  ortho.jpg + .jgw    imagery with a world file (same coordinates), draped on the terrain

Sources:
  DEM      MnGeo / MN DNR "Statewide Seamless 0.5m Lidar DEM, Minnesota, 2025" (cloud-optimized GeoTIFF, read by window)
  Imagery  Esri World Imagery MapServer export (for visualization tests; see Esri's terms of use)
Requires: pip install rasterio pillow numpy
"""
import io
import os
import sys
import urllib.request

import numpy as np
import rasterio
from rasterio.windows import from_bounds
from PIL import Image

DEM_VRT = "/vsicurl/https://g46prsaddst01.blob.core.windows.net/mntopo/dem_100k/2nd_Generation_Seamless_Lidar_DEM.vrt"
ESRI = ("https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/export"
        "?bbox={x0},{y0},{x1},{y1}&bboxSR=26915&imageSR=26915&size={w},{h}&format=jpg&f=image")


def fetch_dem(out, x0, y0, x1, y1):
    with rasterio.open(DEM_VRT) as ds:
        win = from_bounds(x0, y0, x1, y1, ds.transform).round_offsets().round_lengths()
        a = ds.read(1, window=win).astype("<f4")
        t = ds.window_transform(win)
        nod = ds.nodata
    a[(a == nod) | ~np.isfinite(a)] = -9999.0
    a.tofile(os.path.join(out, "dem.flt"))
    nrows, ncols = a.shape
    # ESRI .hdr: lower-left corner of the lower-left cell.
    with open(os.path.join(out, "dem.hdr"), "w") as f:
        f.write(f"ncols {ncols}\nnrows {nrows}\nxllcorner {t.c:.3f}\nyllcorner {t.f - nrows * abs(t.e):.3f}\n"
                f"cellsize {t.a:.6f}\nNODATA_value -9999\nbyteorder LSBFIRST\n")
    print(f"DEM {ncols} x {nrows} = {ncols * nrows / 1e6:.1f} M cells, {a[a > -9999].min():.2f} to {a.max():.2f} m")


def fetch_image(out, x0, y0, x1, y1, px):
    tile = 4096 if px > 4096 else px
    n = max(1, px // tile)
    mosaic = Image.new("RGB", (tile * n, tile * n))
    dx, dy = (x1 - x0) / n, (y1 - y0) / n
    for j in range(n):          # rows from the top
        for i in range(n):
            bx0, by1 = x0 + i * dx, y1 - j * dy
            url = ESRI.format(x0=bx0, y0=by1 - dy, x1=bx0 + dx, y1=by1, w=tile, h=tile)
            with urllib.request.urlopen(url, timeout=120) as r:
                mosaic.paste(Image.open(io.BytesIO(r.read())).convert("RGB"), (i * tile, j * tile))
    path = os.path.join(out, "ortho.jpg")
    mosaic.save(path, quality=88)
    W = mosaic.size[0]
    cs = (x1 - x0) / W
    with open(os.path.join(out, "ortho.jgw"), "w") as f:  # world file: pixel size, rotation, -pixel size, center of UL pixel
        f.write(f"{cs:.6f}\n0\n0\n{-cs:.6f}\n{x0 + cs / 2:.3f}\n{y1 - cs / 2:.3f}\n")
    print(f"Imagery {W} x {W} px, {cs:.3f} m/px, {os.path.getsize(path) / 1e6:.1f} MB")


if __name__ == "__main__":
    out, e, n = sys.argv[1], float(sys.argv[2]), float(sys.argv[3])
    half = float(sys.argv[4]) if len(sys.argv) > 4 else 1500
    px = int(sys.argv[5]) if len(sys.argv) > 5 else 8192
    os.makedirs(out, exist_ok=True)
    box = (e - half, n - half, e + half, n + half)
    fetch_dem(out, *box)
    fetch_image(out, *box, px)
