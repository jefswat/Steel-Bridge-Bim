"""Map CRS table for the automatic terrain step (tools/terrain/auto_terrain.py, `sbm pipeline`).

Every entry maps a CRS name as written in `BridgeProject.Georeference.CrsName` to a projection object working in METRES (forward(lon, lat) ->
x, y; inverse(x, y) -> lon, lat; NAD83 / GRS80). The map UNIT (ftUS or m) does not matter here: the pipeline passes map METRES
((project - centre) x GridScale + centre, the IfcMapConversion rule). Keep `SUPPORTED` in step with SBM.Api/ModelPipeline.cs `TerrainCrs.Supported`
(a test reads this file). A CRS that is not listed is reported by the pipeline as an UNRESOLVED decision (add its parameters here).
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from geotiff import Lcc2, Tm, utm
from ll import Z


def _lcc(lat1, lat2, lat0, lon0, fe, fn):
    return lambda: Lcc2(lat1, lat2, lat0, lon0, fe, fn)


def _dm(d, m=0.0):
    return d + m / 60.0 if d >= 0 else d - m / 60.0


# State plane (NAD83) zones met by the sample models + neighbours; ftUS and metre EPSG codes share the projection (metres internally).
_STATE_PLANE = {
    # Texas North Central (EPSG:2276 ftUS, 32138 m) - Northaven NHT
    ("EPSG:2276", "EPSG:32138"): _lcc(_dm(32, 8), _dm(33, 58), _dm(31, 40), -98.5, 600000.0, 2000000.0),
    # North Carolina (EPSG:2264 ftUS, 32119 m) - Bonner B-2500
    ("EPSG:2264", "EPSG:32119"): _lcc(_dm(34, 20), _dm(36, 10), _dm(33, 45), -79.0, 609601.22, 0.0),
    # Pennsylvania South (EPSG:2272 ftUS, 32129 m) - PTC Delaware River
    ("EPSG:2272", "EPSG:32129"): _lcc(_dm(40, 58), _dm(39, 56), _dm(39, 20), _dm(-77, 45), 600000.0, 0.0),
    # Pennsylvania North (EPSG:2271 ftUS, 32128 m)
    ("EPSG:2271", "EPSG:32128"): _lcc(_dm(41, 57), _dm(40, 53), _dm(40, 10), _dm(-77, 45), 600000.0, 0.0),
    # Oklahoma North (EPSG:2267 ftUS, 32124 m) - ODOT Bridge O
    ("EPSG:2267", "EPSG:32124"): _lcc(_dm(35, 34), _dm(36, 46), 35.0, -98.0, 600000.0, 0.0),
    # Iowa North (EPSG:3417 ftUS, 26975 m) / Iowa South (EPSG:3418 ftUS, 26976 m)
    ("EPSG:3417", "EPSG:26975"): _lcc(_dm(43, 16), _dm(42, 4), 41.5, -93.5, 1500000.0, 1000000.0),
    ("EPSG:3418", "EPSG:26976"): _lcc(_dm(41, 47), _dm(40, 37), 40.0, -93.5, 500000.0, 0.0),
    # Nebraska (EPSG:32104 m; the ftUS variant is not listed)
    ("EPSG:32104",): _lcc(40.0, 43.0, _dm(39, 50), -100.0, 500000.0, 0.0),
}

SUPPORTED = sorted({k for keys in _STATE_PLANE for k in keys} | {"ESRI:103708"}
                   | {f"EPSG:{26900 + z}" for z in range(1, 24)} | {f"EPSG:{32600 + z}" for z in range(1, 61)})


def projection(name):
    """Projection object for a CRS name (case-insensitive), or None when it is not in the table."""
    n = (name or "").strip().upper()
    for keys, make in _STATE_PLANE.items():
        if n in keys:
            return make()
    if n == "ESRI:103708":
        return Z["mn-anoka"]
    if n.startswith("EPSG:"):
        try:
            code = int(n[5:])
        except ValueError:
            return None
        if 26901 <= code <= 26923:
            return utm(code - 26900)
        if 32601 <= code <= 32660:
            return utm(code - 32600)
    return None


if __name__ == "__main__":
    print("\n".join(SUPPORTED))
