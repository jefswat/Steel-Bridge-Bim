"""Sample an ESRI .flt/.hdr at X,Y points (bilinear, same units as the grid). Usage: python -I sample_flt.py grid.flt points.csv  (cols: name,x,y[,plan_value])"""
import sys, csv, numpy as np

flt = sys.argv[1]
h = {}
for line in open(flt[:-4] + ".hdr"):
    p = line.split()
    if len(p) == 2:
        h[p[0].lower()] = p[1]
nx, ny, cell = int(h["ncols"]), int(h["nrows"]), float(h["cellsize"])
x0 = float(h["xllcorner"]); y0 = float(h["yllcorner"]) + ny * cell
z = np.fromfile(flt, dtype="<f4").reshape(ny, nx)
z = np.where(z == -9999, np.nan, z)
print("grid", nx, ny, cell, "x", x0, x0 + nx * cell, "y", y0 - ny * cell, y0)
for row in csv.reader(open(sys.argv[2])):
    n, x, y = row[0], float(row[1]), float(row[2])
    c = (x - x0) / cell - 0.5; r = (y0 - y) / cell - 0.5
    ci, ri = int(np.floor(c)), int(np.floor(r))
    if not (0 <= ci < nx - 1 and 0 <= ri < ny - 1):
        print(n, "outside"); continue
    fc, fr = c - ci, r - ri
    v = (z[ri, ci] * (1 - fc) + z[ri, ci + 1] * fc) * (1 - fr) + (z[ri + 1, ci] * (1 - fc) + z[ri + 1, ci + 1] * fc) * fr
    extra = ""
    if len(row) > 3 and row[3]:
        extra = f"  plan {float(row[3]):.2f}  diff (ground - plan) {v - float(row[3]):+.2f}"
    print(f"{n}: ground {v:.2f}{extra}")
