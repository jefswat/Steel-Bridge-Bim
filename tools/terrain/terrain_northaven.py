"""Northaven NHT terrain: USGS 3DEP 1 m (UTM 14N, NAVD88 m) -> grid in the MODEL's coordinates (TxDOT surface ft, Dallas SAF 1.000136) and feet.
Usage: python -I terrain_northaven.py <tif> <outdir>
Output cell centres are the plan X / Y numbers (ft); the Reference uses UnitScale 0.3048 (x, y, z ft -> model metres)."""
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from geotiff import read_geotiff, Lcc2, utm, bilinear, write_flt

tif, outdir = sys.argv[1], sys.argv[2]
FT = 0.3048
USFT = 1200 / 3937
SAF = 1.000136
# EPSG:2276 NAD83 / Texas North Central (ftUS): 2SP 32.1333333 / 33.9666667, origin 31.6666667 N, 98.5 W, FE 600000 m, FN 2000000 m
lcc = Lcc2(32 + 8 / 60, 33 + 58 / 60, 31 + 40 / 60, -98.5, 600000.0, 2000000.0)
tm = utm(14)
# bridge X 2499806..2500475 Y 7016100..7016300 (plan ft) + 984 ft (300 m)
XMIN, XMAX, YMIN, YMAX = 2498780.0, 2501500.0, 7015050.0, 7017300.0
cell = 1.0 / FT  # 1 m expressed in plan ft
nx = int(np.ceil((XMAX - XMIN) / cell)); ny = int(np.ceil((YMAX - YMIN) / cell))
xs = XMIN + (np.arange(nx) + 0.5) * cell
ys = YMAX - (np.arange(ny) + 0.5) * cell
X, Y = np.meshgrid(xs, ys)
# plan (surface) number -> grid ftUS -> metres -> lon/lat -> UTM 14N
gx = X / SAF * USFT; gy = Y / SAF * USFT
lon, lat = lcc.inverse(gx, gy)
ux, uy = tm.forward(lon, lat)
print("lon/lat range", lon.min(), lon.max(), lat.min(), lat.max())
# crop the tile to the needed window first
a_full, (x0, y0, dx, dy), tags = read_geotiff(tif)
nod = -999999.0
z = bilinear(a_full, x0, y0, dx, dy, ux, uy, nodata=nod)
z = np.where(z < -1000, np.nan, z)
print("valid", np.isfinite(z).mean(), "min/max m", np.nanmin(z), np.nanmax(z))
zft = (z / FT).astype(np.float32)
os.makedirs(outdir, exist_ok=True)
base = os.path.join(outdir, "northaven-nht-3dep-1m-txnc-sft")
write_flt(base, zft, XMIN, YMAX - ny * cell, cell)
print("wrote", base, nx, "x", ny, "cell ft", cell)
