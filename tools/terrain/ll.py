"""State-plane -> lon/lat helper for TNM bbox queries. Usage: python -I ll.py zone xmin ymin xmax ymax   (zone: ok-north | tx-nc ; ftUS coords)"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from geotiff import Lcc2
import numpy as np

USFT = 1200 / 3937
Z = {
    "ok-north": Lcc2(35 + 34 / 60, 36 + 46 / 60, 35.0, -98.0, 600000.0, 0.0),
    # NAD83(HARN) Adj MN Anoka (ESRI:103708): LCC 2SP on the county-adjusted ellipsoid, FE 500000 / FN 100000 ftUS
    "mn-anoka": Lcc2(45 + 4 / 60, 45 + 22 / 60, 45 + 2 / 60 + 7 / 3600, -93 - 16 / 60, 152400.30480061, 30480.0609601219, a=6378418.941, rf=298.257222100883),
    "tx-nc": Lcc2(32 + 8 / 60, 33 + 58 / 60, 31 + 40 / 60, -98.5, 600000.0, 2000000.0),
}
if __name__ == "__main__":
    z = Z[sys.argv[1]]
    x0, y0, x1, y1 = map(float, sys.argv[2:6])
    for x, y in [(x0, y0), (x1, y0), (x0, y1), (x1, y1)]:
        lon, lat = z.inverse(np.array([x * USFT]), np.array([y * USFT]))
        print(f"{x:.0f},{y:.0f} -> lon {lon[0]:.6f} lat {lat[0]:.6f}")
