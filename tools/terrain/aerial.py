"""Download a USGS NAIP Plus orthoimage (public, imagery.nationalmap.gov ImageServer exportImage) for a box and write one JPEG + world file (.jgw)
that the program drapes on a terrain reference (`ReferenceModelDefinition.ImagePath`, world file beside the image).

    python -I tools/terrain/aerial.py --epsg 32119 --bbox x0,y0,x1,y1 --out C:\\SBMData\\terrain\\site\\ortho.jpg [--px 0.5]
                                      [--scale GRIDSCALE --center CX,CY] [--max-tile 4000]

--epsg   a METRE-based EPSG code the image service understands (UTM 269xx / 326xx, or the metre variant of a state plane zone, e.g. 32119 NC,
         32129 PA South, 32138 TX North Central). Use the metre code even when the plans are in ftUS.
--bbox   the box in that CRS (metres): x0,y0,x1,y1.
--px     pixel size in metres (NAIP is 0.3 - 1 m; 0.5 m default). The image is fetched in tiles of at most --max-tile pixels and mosaicked.
--scale / --center   the project's Georeference GridScale / ScaleCenterX,Y (metres) when the model is in LOCALIZED coordinates (project = (map - centre)
         / scale + centre, the inverse of the IfcMapConversion rule): the world file is then written in project metres, so the image lines up with the model.
NAIP is an ortho made on the ground model: anything high above the ground (bridge decks, towers) is displaced radially - use it for context, not as
a survey. Needs network access; refuses images larger than 25,000 px on a side.
"""
import argparse, io, json, math, os, ssl, sys, time, urllib.parse, urllib.request
from PIL import Image

try:
    import certifi
    CTX = ssl.create_default_context(cafile=certifi.where())
except Exception:
    CTX = None
BASE = "https://imagery.nationalmap.gov/arcgis/rest/services/USGSNAIPPlus/ImageServer/exportImage"


def fetch(bb, epsg, w, h):
    q = urllib.parse.urlencode(dict(bbox=",".join(f"{v:.3f}" for v in bb), bboxSR=epsg, imageSR=epsg, size=f"{w},{h}", format="jpg",
                                    bandIds="0,1,2", f="json"))
    last = None
    for att in range(6):
        try:
            r = json.load(urllib.request.urlopen(BASE + "?" + q, timeout=120, context=CTX))
            if "href" not in r:
                raise RuntimeError(f"service answer without an image: {json.dumps(r)[:300]}")
            data = urllib.request.urlopen(r["href"], timeout=120, context=CTX).read()
            return Image.open(io.BytesIO(data)).convert("RGB")
        except Exception as e:
            last = e
            print(f"  retry {att + 1}: {e}", flush=True)
            time.sleep(5)
    raise SystemExit(f"download failed: {last}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--epsg", type=int, required=True)
    ap.add_argument("--bbox", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--px", type=float, default=0.5)
    ap.add_argument("--scale", type=float, default=1.0)
    ap.add_argument("--center", default="0,0")
    ap.add_argument("--max-tile", type=int, default=4000)
    a = ap.parse_args()
    x0, y0, x1, y1 = map(float, a.bbox.split(","))
    if x1 <= x0 or y1 <= y0:
        sys.exit("bbox must be x0,y0,x1,y1 with x1 > x0 and y1 > y0")
    W, H = int(math.ceil((x1 - x0) / a.px)), int(math.ceil((y1 - y0) / a.px))
    if max(W, H) > 25000:
        sys.exit(f"image would be {W} x {H} px: use a larger --px or a smaller box")
    nx, ny = int(math.ceil(W / a.max_tile)), int(math.ceil(H / a.max_tile))
    tw, th = int(math.ceil(W / nx)), int(math.ceil(H / ny))
    mosaic = Image.new("RGB", (tw * nx, th * ny))
    for j in range(ny):
        for i in range(nx):
            bb = (x0 + i * tw * a.px, y1 - (j + 1) * th * a.px, x0 + (i + 1) * tw * a.px, y1 - j * th * a.px)
            print(f"tile r{j}c{i} {tw} x {th} px", flush=True)
            mosaic.paste(fetch(bb, a.epsg, tw, th), (i * tw, j * th))
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    mosaic.save(a.out, "JPEG", quality=88)
    # world file: pixel size, rotation terms, centre of the upper-left pixel - in PROJECT metres
    cx, cy = map(float, a.center.split(","))
    s = a.scale if a.scale > 0 else 1.0
    P = lambda x, y: ((x - cx) / s + cx, (y - cy) / s + cy)
    ux, uy = P(x0 + a.px / 2, y1 - a.px / 2)
    with open(os.path.splitext(a.out)[0] + ".jgw", "w") as f:
        f.write(f"{a.px / s:.6f}\n0\n0\n{-a.px / s:.6f}\n{ux:.4f}\n{uy:.4f}\n")
    json.dump(dict(source=BASE, epsg=a.epsg, bbox=[x0, y0, x1, y1], px=a.px, size=[mosaic.width, mosaic.height], scale=s, center=[cx, cy]),
              open(a.out + ".json", "w"), indent=1)
    print(f"saved {a.out} ({mosaic.width} x {mosaic.height} px, {os.path.getsize(a.out) / 1e6:.1f} MB) + world file")


if __name__ == "__main__":
    main()
