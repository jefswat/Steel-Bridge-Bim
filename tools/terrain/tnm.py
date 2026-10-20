"""USGS TNM Access API query (public 3DEP products). Usage: python -I tnm.py west south east north"""
import sys, json, urllib.request, urllib.parse


def query(bbox, dataset):
    q = urllib.parse.urlencode({"datasets": dataset, "bbox": ",".join(map(str, bbox)), "prodFormats": "GeoTIFF", "max": 50, "outputFormat": "JSON"})
    with urllib.request.urlopen("https://tnmaccess.nationalmap.gov/api/v1/products?" + q, timeout=60) as r:
        return json.load(r)


if __name__ == "__main__":
    bbox = list(map(float, sys.argv[1:5]))
    for ds in ["Digital Elevation Model (DEM) 1 meter", "National Elevation Dataset (NED) 1/3 arc-second"]:
        d = query(bbox, ds)
        print("##", ds, d.get("total"))
        for it in d.get("items", []):
            print(it["title"], "|", it["downloadURL"], "|", round(it["sizeInBytes"] / 1e6, 1), "MB |", it.get("publicationDate"), "|", it.get("boundingBox"))
