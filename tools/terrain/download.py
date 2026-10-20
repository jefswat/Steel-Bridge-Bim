"""Download one public USGS product. Usage: python -I download.py URL OUTDIR (prints name / size first; refuses > 400 MB)."""
import sys, os, ssl, certifi, urllib.request
CTX = ssl.create_default_context(cafile=certifi.where())

url, outdir = sys.argv[1], sys.argv[2]
os.makedirs(outdir, exist_ok=True)
name = url.rsplit("/", 1)[1]
dest = os.path.join(outdir, name)
req = urllib.request.Request(url, method="HEAD")
with urllib.request.urlopen(req, timeout=60, context=CTX) as r:
    size = int(r.headers["Content-Length"])
print(f"{name} | {url} | {size/1e6:.1f} MB")
if size > 400e6:
    sys.exit("refused: > 400 MB")
if os.path.exists(dest) and os.path.getsize(dest) == size:
    print("already downloaded")
    sys.exit(0)
with urllib.request.urlopen(url, timeout=120, context=CTX) as r, open(dest, "wb") as f:
    got = 0
    while True:
        b = r.read(1 << 20)
        if not b:
            break
        f.write(b)
        got += len(b)
print("saved", dest, os.path.getsize(dest))
