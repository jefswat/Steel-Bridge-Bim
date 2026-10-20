"""Relief picture of a .flt (local relief = z - 25 m box mean, stretched +-1.5 m) for eyeballing roads / embankments. python -I relief_png.py grid.flt out.png"""
import sys, numpy as np
from PIL import Image

flt = sys.argv[1]
h = {}
for line in open(flt[:-4] + ".hdr"):
    p = line.split()
    if len(p) == 2:
        h[p[0].lower()] = p[1]
nx, ny = int(h["ncols"]), int(h["nrows"])
z = np.fromfile(flt, dtype="<f4").reshape(ny, nx)
z = np.where(z == -9999, np.nan, z)


def box(a, r):
    c = np.cumsum(np.pad(a, ((r + 1, r), (0, 0)), mode="edge"), axis=0)
    a = (c[2 * r + 1:] - c[:-2 * r - 1]) / (2 * r + 1)
    c = np.cumsum(np.pad(a, ((0, 0), (r + 1, r)), mode="edge"), axis=1)
    return (c[:, 2 * r + 1:] - c[:, :-2 * r - 1]) / (2 * r + 1)


rel = z - box(z, 12)
img = np.clip((rel / 1.5 * 0.5 + 0.5) * 255, 0, 255).astype(np.uint8)
Image.fromarray(img).resize((nx * 2 if nx < 1000 else nx, ny * 2 if nx < 1000 else ny)).save(sys.argv[2])
print(nx, ny)
