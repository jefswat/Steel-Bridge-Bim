"""Generic: USGS 3DEP GeoTIFF (UTM, NAVD88 m) -> ESRI .flt/.hdr in the MODEL's coordinate numbers.
Usage: python -I terrain_clip.py <tif> <outbase> <system> <xmin> <ymin> <xmax> <ymax> [saf] [units ft|m]
  system: tx-nc | ok-north (NAD83 state plane, ftUS numbers) | utm14 | utm15 | utm16 ... (metres) | tm:<lon0>,<k0>,<x0>,<y0> (metres, NAD83/GRS80)
  bbox / output cell centres are in the model's plan numbers (ft or m). saf = model number / grid number (surface factor, default 1).
  units ft: cell = 1 m expressed in ft, z in ft (Reference UnitScale 0.3048).  units m: metres, z metres (UnitScale 1)."""
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from geotiff import read_geotiff, utm, Tm, bilinear, write_flt
from ll import Z, USFT

tif, outbase, system = sys.argv[1:4]
XMIN, YMIN, XMAX, YMAX = map(float, sys.argv[4:8])
SAF = float(sys.argv[8]) if len(sys.argv) > 8 else 1.0
units = sys.argv[9] if len(sys.argv) > 9 else "ft"
FT = 0.3048

if system in Z:
    proj, to_m = Z[system], USFT
elif system.startswith("utm"):
    proj, to_m = None, 1.0
elif system.startswith("tm:"):
    lon0, k0, x0, y0 = map(float, system[3:].split(","))
    proj, to_m = Tm(lon0, k0, x0, y0), 1.0
else:
    raise SystemExit("unknown system")

a, (tx0, ty0, dx, dy), tags = read_geotiff(tif)
from geotiff import geokeys
gk = geokeys(tags)
print("geokeys projected CS", gk.get(3072))
zone = gk.get(3072) - 26900 if gk.get(3072) and 26900 < gk.get(3072) < 26999 else None
tile = utm(zone)

cell = (1.0 / FT) if units == "ft" else 1.0
nx = int(np.ceil((XMAX - XMIN) / cell)); ny = int(np.ceil((YMAX - YMIN) / cell))
xs = XMIN + (np.arange(nx) + 0.5) * cell
ys = YMIN + ny * cell - (np.arange(ny) + 0.5) * cell
X, Y = np.meshgrid(xs, ys)
gx = X / SAF * to_m; gy = Y / SAF * to_m           # grid metres (map CRS)
if system.startswith("utm"):
    ux, uy = gx, gy
    if int(system[3:]) != zone:
        raise SystemExit("tile zone differs")
else:
    lon, lat = proj.inverse(gx, gy)
    ux, uy = tile.forward(lon, lat)
    print("lon/lat", lon.min(), lon.max(), lat.min(), lat.max())
z = bilinear(a, tx0, ty0, dx, dy, ux, uy, nodata=-999999.0)
z = np.where(z < -1000, np.nan, z)
print("valid", np.isfinite(z).mean(), "min/max m", np.nanmin(z), np.nanmax(z))
out = (z / FT) if units == "ft" else z
write_flt(outbase, out.astype(np.float32), XMIN, YMIN, cell)
print("wrote", outbase, nx, "x", ny, "cell", cell, units)
