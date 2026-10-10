# Lists words on a page matching a regex, with coordinates (to locate a detail before clipping a raster).
import sys, re, pymupdf
doc = pymupdf.open(sys.argv[1]); p = doc[int(sys.argv[2]) - 1]
rx = re.compile(sys.argv[3])
for w in p.get_text("words"):
    if rx.search(w[4]):
        print(f"{w[0]:7.1f} {w[1]:7.1f}  {w[4]}")
