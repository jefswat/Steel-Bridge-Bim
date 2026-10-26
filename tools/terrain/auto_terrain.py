"""Automatic terrain for a georeferenced model (called by `sbm pipeline`; also usable by hand).

USGS 3DEP DEM (1 m lidar tiles, else 1/3 arc-second) for the model extents -> ESRI .flt/.hdr in PROJECT METRES (x, y = project X / Y in
metres, z = NAVD88 metres; Reference UnitScale 1). Tiles are downloaded once into <cache>/tiles (public USGS TNM products, size-limited).

Usage: python -I auto_terrain.py --crs EPSG:2276 --bbox XMIN YMIN XMAX YMAX --out <base without extension> --cache <folder>
                                 [--scale G] [--center CX CY] [--cell M] [--max-cells N] [--max-mb MB] [--dry-run]
  --bbox    project X / Y in METRES (the pipeline adds the margin)
  --scale   GridScale of the georeference (map = (project - centre) x scale + centre, map metres), default 1
  --center  ScaleCenterX / Y in project metres (default 0 0)
Last line of stdout: RESULT {json} (status ok | error, message, tiles, valid fraction, z range, cell, file).
Exit 0 = ok, 2 = no data / unsupported, 3 = network error.
"""
import sys, os, json, math, argparse, ssl, urllib.request, urllib.parse
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np


def result(status, message, code, **kw):
    print("RESULT " + json.dumps(dict(status=status, message=message, **kw)))
    sys.exit(code)


ap = argparse.ArgumentParser()
ap.add_argument("--crs", required=True)
ap.add_argument("--bbox", nargs=4, type=float, required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--cache", required=True)
ap.add_argument("--scale", type=float, default=1.0)
ap.add_argument("--center", nargs=2, type=float, default=[0.0, 0.0])
ap.add_argument("--cell", type=float, default=1.0)
ap.add_argument("--max-cells", type=float, default=8e6)
ap.add_argument("--max-mb", type=float, default=1500)
ap.add_argument("--dry-run", action="store_true")
a = ap.parse_args()

from crs import projection
proj = projection(a.crs)
if proj is None:
    result("unsupported", f"CRS {a.crs} is not in tools/terrain/crs.py: add its projection parameters", 2)

x0, y0, x1, y1 = a.bbox
cx, cy = a.center
g = a.scale if a.scale > 0 else 1.0


def to_map(x, y):
    return (np.asarray(x) - cx) * g + cx, (np.asarray(y) - cy) * g + cy


# lon/lat box from a densified outline of the project box
t = np.linspace(0, 1, 21)
ex = np.concatenate([x0 + (x1 - x0) * t, np.full(21, x1), x1 - (x1 - x0) * t, np.full(21, x0)])
ey = np.concatenate([np.full(21, y0), y0 + (y1 - y0) * t, np.full(21, y1), y1 - (y1 - y0) * t])
lon, lat = proj.inverse(*to_map(ex, ey))
west, east, south, north = float(lon.min()), float(lon.max()), float(lat.min()), float(lat.max())
if not (-180 < west < east < 180 and -90 < south < north < 90):
    result("error", f"projected box gives an invalid lon/lat range {west},{south},{east},{north}: check the georeference", 2)
print(f"lon/lat box {west:.6f} {south:.6f} {east:.6f} {north:.6f}")

CTX = None
try:
    import certifi
    CTX = ssl.create_default_context(cafile=certifi.where())
except ImportError:
    CTX = ssl.create_default_context()

DATASETS = ["Digital Elevation Model (DEM) 1 meter", "National Elevation Dataset (NED) 1/3 arc-second"]


def tnm(dataset):
    q = urllib.parse.urlencode({"datasets": dataset, "bbox": f"{west},{south},{east},{north}", "prodFormats": "GeoTIFF", "max": 100, "outputFormat": "JSON"})
    with urllib.request.urlopen("https://tnmaccess.nationalmap.gov/api/v1/products?" + q, timeout=90, context=CTX) as r:
        return json.load(r).get("items", [])


def overlaps(it):
    b = it.get("boundingBox") or {}
    try:
        return b["minX"] < east and b["maxX"] > west and b["minY"] < north and b["maxY"] > south
    except KeyError:
        return True


def covered(items):
    """True when the union of the tile boxes covers the lon/lat box (sampled)."""
    gx, gy = np.meshgrid(np.linspace(west, east, 15), np.linspace(south, north, 15))
    ok = np.zeros(gx.shape, bool)
    for it in items:
        b = it["boundingBox"]
        ok |= (gx >= b["minX"]) & (gx <= b["maxX"]) & (gy >= b["minY"]) & (gy <= b["maxY"])
    return ok.all()


chosen, used_ds = [], None
try:
    for ds in DATASETS:
        items = [i for i in tnm(ds) if overlaps(i) and i.get("downloadURL", "").lower().endswith((".tif", ".tiff"))]
        # newest first: where tiles of several lidar projects overlap, the newest wins (sampling takes the first valid value)
        items.sort(key=lambda i: i.get("publicationDate") or "", reverse=True)
        if items and covered(items):
            chosen, used_ds = items, ds
            break
        if items and not chosen:
            chosen, used_ds = items, ds   # partial cover: keep unless the next dataset covers fully
except Exception as ex:  # network / service error
    result("network", f"TNM query failed: {ex}", 3)
if not chosen:
    result("nodata", "no 3DEP GeoTIFF product covers the model extents (outside the US 3DEP coverage?)", 2)
total_mb = sum(i.get("sizeInBytes", 0) for i in chosen) / 1e6
print(f"dataset {used_ds}: {len(chosen)} tile(s), {total_mb:.0f} MB")
for i in chosen:
    print("  ", i.get("title"), "|", i.get("publicationDate"), "|", i.get("downloadURL"))
if total_mb > a.max_mb:
    result("error", f"{len(chosen)} tiles = {total_mb:.0f} MB > --max-mb {a.max_mb:.0f}: reduce the margin or raise the limit", 2)
if a.dry_run:
    result("dry-run", "query only", 0, dataset=used_ds, tiles=[i.get("downloadURL") for i in chosen], mb=total_mb)

tiles_dir = os.path.join(a.cache, "tiles")
os.makedirs(tiles_dir, exist_ok=True)


def fetch(url):
    name = url.rsplit("/", 1)[1]
    dest = os.path.join(tiles_dir, name)
    try:
        req = urllib.request.Request(url, method="HEAD")
        with urllib.request.urlopen(req, timeout=60, context=CTX) as r:
            size = int(r.headers.get("Content-Length") or 0)
    except Exception as ex:
        if os.path.exists(dest):
            return dest
        raise
    if os.path.exists(dest) and (size == 0 or os.path.getsize(dest) == size):
        print("   cached", name)
        return dest
    print(f"   downloading {name} ({size / 1e6:.0f} MB)")
    tmp = dest + ".part"
    with urllib.request.urlopen(url, timeout=300, context=CTX) as r, open(tmp, "wb") as f:
        while True:
            b = r.read(1 << 20)
            if not b:
                break
            f.write(b)
    os.replace(tmp, dest)
    return dest


try:
    files = [fetch(i["downloadURL"]) for i in chosen]
except Exception as ex:
    result("network", f"download failed: {ex}", 3)

# output grid in project metres
cell = max(a.cell, math.sqrt((x1 - x0) * (y1 - y0) / a.max_cells))
nx = int(math.ceil((x1 - x0) / cell)); ny = int(math.ceil((y1 - y0) / cell))
xs = x0 + (np.arange(nx) + 0.5) * cell
ys = y0 + ny * cell - (np.arange(ny) + 0.5) * cell
X, Y = np.meshgrid(xs, ys)
mx, my = to_map(X, Y)
del X, Y
glon, glat = proj.inverse(mx, my)
del mx, my
z = np.full(glon.shape, np.nan, np.float32)

from geotiff import read_geotiff, geokeys, utm, bilinear
from PIL import Image
for f in files:
    im = Image.open(f)
    tags = {k: v for k, v in im.tag_v2.items()}
    gk = geokeys(tags)
    pcs = gk.get(3072)
    if isinstance(pcs, int) and (26901 <= pcs <= 26923 or 32601 <= pcs <= 32660):
        tm = utm(pcs - 26900 if pcs < 32600 else pcs - 32600)
        tx, ty = tm.forward(glon, glat)
    elif gk.get(1024) == 2 or pcs is None:   # geographic tile (NED 1/3 arc-second, NAD83 lon / lat)
        tx, ty = glon, glat
    else:
        print(f"   skipped {os.path.basename(f)}: tile CRS {pcs} not handled")
        continue
    scale = tags[33550]; tie = tags[33922]
    tx0 = tie[3] - tie[0] * scale[0]; ty0 = tie[4] + tie[1] * scale[1]
    need = np.isnan(z)
    if not need.any():
        break
    c0 = int(np.floor((np.nanmin(tx[need]) - tx0) / scale[0])) - 2; c1 = int(np.ceil((np.nanmax(tx[need]) - tx0) / scale[0])) + 3
    r0 = int(np.floor((ty0 - np.nanmax(ty[need])) / scale[1])) - 2; r1 = int(np.ceil((ty0 - np.nanmin(ty[need])) / scale[1])) + 3
    W, H = im.size
    c0, r0, c1, r1 = max(c0, 0), max(r0, 0), min(c1, W), min(r1, H)
    if c1 - c0 < 2 or r1 - r0 < 2:
        continue
    arr, (wx0, wy0, dx, dy), _ = read_geotiff(f, (c0, r0, c1, r1))
    nod = tags.get(42113)
    try:
        nod = float(nod) if nod is not None else -999999.0
    except (TypeError, ValueError):
        nod = -999999.0
    v = bilinear(arr, wx0, wy0, dx, dy, tx[need], ty[need], nodata=nod)
    v = np.where(v < -1000, np.nan, v)
    z[need] = v.astype(np.float32)
    print(f"   {os.path.basename(f)}: filled {np.isfinite(v).mean() * 100:.1f} % of the remaining cells")
    del arr

valid = float(np.isfinite(z).mean())
if valid == 0:
    result("nodata", "the downloaded tiles do not cover the model extents", 2)
from geotiff import write_flt
os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
write_flt(a.out, z, x0, y0, cell)
result("ok", f"{nx} x {ny} cells of {cell:.2f} m, {valid * 100:.1f} % valid", 0, file=a.out + ".flt", dataset=used_ds,
       tiles=[os.path.basename(f) for f in files], valid=valid, zmin=float(np.nanmin(z)), zmax=float(np.nanmax(z)), cell=cell, nx=nx, ny=ny)
