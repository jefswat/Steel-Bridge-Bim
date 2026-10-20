"""Plan 'approximate existing ground line' labels (General Drawing sheets S0-06..S0-10 = pdf p.7-11, section along -L-) vs the CoNED DEM
(file in C:/SBM/dist/terrain/bonner) at the bent stations. Label -> bent: nearest bent along the page's station axis."""
import re, sys, os, csv
import numpy as np
import fitz
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lgeom import build, point

USFT = 1200.0 / 3937.0
E_B, N_B = 3031964.117, 751499.622
rows = build()
bx, by, _ = point(rows, 2647.55, 23.82)
dx, dy = E_B - bx, N_B - by
d = r"C:/SBM/dist/terrain/bonner/bonner-oregon-inlet-ncei-ninth-3m-ncsp"
h = dict(l.split() for l in open(d + ".hdr"))
nc, nr = int(h["ncols"]), int(h["nrows"]); x0, y0, cs = float(h["xllcorner"]), float(h["yllcorner"]), float(h["cellsize"])
g = np.fromfile(d + ".flt", "<f4").reshape(nr, nc)[::-1]


def zat(x, y):
    fx = (x - x0) / cs - 0.5; fy = (y - y0) / cs - 0.5
    i, j = int(np.floor(fx)), int(np.floor(fy)); tx, ty = fx - i, fy - j
    return (g[j, i] * (1 - tx) * (1 - ty) + g[j, i + 1] * tx * (1 - ty) + g[j + 1, i] * (1 - tx) * ty + g[j + 1, i + 1] * tx * ty)


def dem(sta, off=0.0):
    x, y, a = point(rows, sta, off)
    return zat((x + dx) * USFT, (y + dy) * USFT) / 0.3048


def sta_of(s):
    a, b = s.split("+"); return float(a) * 100 + float(b)


bents = {}
here = os.path.dirname(os.path.abspath(__file__))
for r in csv.DictReader(open(os.path.join(here, "..", "..", "docs", "plan-data", "bonner-b2500", "bents.csv"), encoding="utf8")):
    m = re.match(r"(\d+\+\d+\.\d+)", r["station_L"])
    if m: bents[r["bent"]] = sta_of(m.group(1))
pdf = fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
out = []
for pno in range(7, 12):
    pg = pdf[pno - 1]
    words = pg.get_text("words")
    ticks = []
    for w in words:
        if re.fullmatch(r"\d+\+00", w[4]) and w[1] < 40: ticks.append(((w[0] + w[2]) / 2, sta_of(w[4])))
    ticks.sort()
    xs = np.array([t[0] for t in ticks]); ss = np.array([t[1] for t in ticks])
    a, b = np.polyfit(xs, ss, 1)
    # EL labels: words 'EL.' followed by "-NN'±" on the same line, above the ground line (y < 330 pt region of the section)
    labs = []
    for w in words:
        m = re.fullmatch(r"-(\d+)'[|±]*", w[4])
        if m and w[1] < 520:
            labs.append(((w[0] + w[2]) / 2, -float(m.group(1)), w[1]))
    for lx, el, ly in sorted(labs):
        sta = a * lx + b
        best = min(bents.items(), key=lambda kv: abs(kv[1] - sta))
        out.append((pno, best[0], best[1], sta, el, ly))
# keep one label per bent (nearest in station)
seen = {}
for pno, bent, bs, sta, el, ly in out:
    if bent not in seen or abs(sta - bs) < abs(seen[bent][3] - bs): seen[bent] = (pno, bent, bs, sta, el)
print("bent  STA(bent)     plan_ft  dem_CL_ft  dem_min/max(+-30ft along, +-15 across)  diff(dem-plan)")
tot = []
for bent, (pno, _, bs, sta, el) in sorted(seen.items(), key=lambda kv: kv[1][2]):
    if not (5200 <= bs <= 10300): continue
    vals = [dem(bs + ds, off) for ds in (-30, -15, 0, 15, 30) for off in (-15, 0, 15)]
    c = dem(bs)
    tot.append(c - el)
    print("%-4s %9.2f  p%-2d  %7.1f  %8.1f   %6.1f / %6.1f   %+6.1f   (label at STA %.0f)" % (bent, bs, pno, el, c, min(vals), max(vals), c - el, sta))
print("mean diff %.2f ft, rms %.2f ft, n=%d" % (np.mean(tot), np.sqrt(np.mean(np.square(tot))), len(tot)))
