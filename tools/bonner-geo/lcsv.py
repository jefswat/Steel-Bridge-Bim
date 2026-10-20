"""Writes docs/plan-data/bonner-b2500/L-coordinates.csv: -L- PC / PI / PT / PRC with localized (plan) and NC State Plane grid coordinates (US ft) + lat / lon.
python -I tools/bonner-geo/lcsv.py <out.csv>"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lgeom import build, point, dms
from rasterio.warp import transform

E_B, N_B, CGF = 3031964.117, 751499.622, 0.99991846
rows = build()
bx, by, _ = point(rows, 2647.55, 23.82)
dx, dy = E_B - bx, N_B - by


def sta(s): return "%d+%05.2f" % (s // 100, s % 100)


def bearing(az):
    az %= 360
    if az <= 90: return "N %s E" % dm(az)
    if az <= 180: return "S %s E" % dm(180 - az)
    if az <= 270: return "S %s W" % dm(az - 180)
    return "N %s W" % dm(360 - az)


def dm(a):
    d = int(a); m = int((a - d) * 60); s = ((a - d) * 60 - m) * 60
    return "%d-%02d-%04.1f" % (d, m, s)


out = [("point", "station", "local_E_usft", "local_N_usft", "grid_E_usft", "grid_N_usft", "lat_NAD83", "lon_NAD83", "tangent_bearing", "source")]


def add(name, s, x, y, az, src):
    E, N = x + dx, y + dy
    gE, gN = (E - E_B) * CGF + E_B, (N - N_B) * CGF + N_B
    lon, lat = transform("EPSG:2264", "EPSG:4269", [gE], [gN])
    out.append((name, sta(s) if s is not None else "", "%.3f" % E, "%.3f" % N, "%.3f" % gE, "%.3f" % gN, "%.7f" % lat[0], "%.7f" % lon[0], bearing(math.degrees(az)) if az is not None else "", src))


add("GPS monument B2500-2", 2647.55, bx, by, None, "p.35 / p.36: N 751499.622 E 3031964.117 (-L- STA 26+47.55, 23.82' RT, EL 5.59); given, the anchor")
curves = [("Curve 1", rows[0], "LT", 1742.62, 2347.00, "PI 27+27.13 T 913.67"),
          ("Curve 2", rows[2], "RT", 1739.85, 2815.00, "PI 62+28.30 T 898.72"),
          ("Curve 3", rows[4], "RT", 4838.13, 6050.00, "PI 128+03.63 T 2556.79"),
          ("Curve 4", rows[6], "RT", 667.58, 5000.00, "PI 173+87.60 T 334.29")]
for nm, r, d, L, R, pi in curves:
    name, s0, Ll, Rr, dd, x, y, az = r
    T = float(pi.split("T ")[1])
    pis = float(pi.split()[1].replace("+", ""))
    add(nm + " PC", s0, x, y, az, "p.21-34 / p.35-36 curve data (" + d + ", R %.2f, L %.2f)" % (R, L))
    add(nm + " PI", pis, x + T * math.sin(az), y + T * math.cos(az), az, "intersection of the tangents (T %.2f from the PC)" % T)
    xe, ye, ae = point(rows, s0 + L)
    add(nm + " PT" if nm != "Curve 4" else nm + " PRC", s0 + L, xe, ye, ae, "PC + L")
with open(sys.argv[1], "w", newline="", encoding="utf8") as f:
    import csv
    csv.writer(f).writerows(out)
print("\n".join(",".join(r) for r in out))
