"""Minimal GeoTIFF float32 reader (Pillow) + projection math (no pyproj/GDAL here). Read downloaded files with python -I only."""
import math
import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None


def read_geotiff(path, window=None):
    """Returns (array float32 (rows, cols), (x0, y0, dx, dy) of the NW pixel CORNER, tags). window=(c0, r0, c1, r1) crops."""
    im = Image.open(path)
    tags = {k: v for k, v in im.tag_v2.items()}
    scale = tags[33550]
    tie = tags[33922]
    x0 = tie[3] - tie[0] * scale[0]
    y0 = tie[4] + tie[1] * scale[1]
    if window:
        c0, r0, c1, r1 = window
        im = im.crop((c0, r0, c1, r1))
        x0 += c0 * scale[0]
        y0 -= r0 * scale[1]
    a = np.array(im, dtype=np.float32)
    return a, (x0, y0, scale[0], scale[1]), tags


def geokeys(tags):
    g = tags.get(34735, ())
    out = {}
    for i in range(4, len(g), 4):
        out[g[i]] = g[i + 3] if g[i + 1] == 0 else (g[i + 1], g[i + 3])
    return out


# ---------------------------------------------------------------- NAD83 / GRS80 projections
A = 6378137.0
F = 1 / 298.257222101
E2 = F * (2 - F)
E = math.sqrt(E2)


def _m(phi, e2=None):
    e2 = E2 if e2 is None else e2
    return math.cos(phi) / math.sqrt(1 - e2 * math.sin(phi) ** 2)


def _t(phi, e=None):
    e = E if e is None else e
    s = math.sin(phi)
    return math.tan(math.pi / 4 - phi / 2) / ((1 - e * s) / (1 + e * s)) ** (e / 2)


class Lcc2:
    """Lambert Conformal Conic 2SP (degrees, metres)."""

    def __init__(self, lat1, lat2, lat0, lon0, x0, y0, a=None, rf=None):
        r = math.radians
        p1, p2, p0 = r(lat1), r(lat2), r(lat0)
        self.lon0 = r(lon0)
        self.x0, self.y0 = x0, y0
        self.a = A if a is None else a
        f = F if rf is None else 1 / rf
        self.e2 = f * (2 - f)
        self.e = math.sqrt(self.e2)
        e2, e = self.e2, self.e
        self.n = (math.log(_m(p1, e2)) - math.log(_m(p2, e2))) / (math.log(_t(p1, e)) - math.log(_t(p2, e)))
        self.F = _m(p1, e2) / (self.n * _t(p1, e) ** self.n)
        self.r0 = self.a * self.F * _t(p0, e) ** self.n

    def forward(self, lon, lat):
        lon = np.radians(lon); lat = np.radians(lat)
        s = np.sin(lat)
        t = np.tan(np.pi / 4 - lat / 2) / ((1 - self.e * s) / (1 + self.e * s)) ** (self.e / 2)
        r = self.a * self.F * t ** self.n
        th = self.n * (lon - self.lon0)
        return self.x0 + r * np.sin(th), self.y0 + self.r0 - r * np.cos(th)

    def inverse(self, x, y):
        x = np.asarray(x, float) - self.x0
        y = self.r0 - (np.asarray(y, float) - self.y0)
        sg = np.sign(self.n)
        r = sg * np.hypot(x, y)
        t = (r / (self.a * self.F)) ** (1 / self.n)
        th = np.arctan2(sg * x, sg * y)
        phi = np.pi / 2 - 2 * np.arctan(t)
        for _ in range(10):
            s = np.sin(phi)
            phi = np.pi / 2 - 2 * np.arctan(t * ((1 - self.e * s) / (1 + self.e * s)) ** (self.e / 2))
        return np.degrees(th / self.n + self.lon0), np.degrees(phi)


class Tm:
    """Transverse Mercator (UTM-style), Krueger series; degrees, metres."""

    def __init__(self, lon0, k0=0.9996, x0=500000.0, y0=0.0):
        self.lon0 = math.radians(lon0); self.k0 = k0; self.x0 = x0; self.y0 = y0
        n = F / (2 - F)
        self.n = n
        self.A = A / (1 + n) * (1 + n ** 2 / 4 + n ** 4 / 64)
        self.alpha = [n / 2 - 2 * n ** 2 / 3 + 5 * n ** 3 / 16, 13 * n ** 2 / 48 - 3 * n ** 3 / 5, 61 * n ** 3 / 240]
        self.beta = [n / 2 - 2 * n ** 2 / 3 + 37 * n ** 3 / 96, n ** 2 / 48 + n ** 3 / 15, 17 * n ** 3 / 480]

    def forward(self, lon, lat):
        lon = np.radians(lon) - self.lon0; lat = np.radians(lat)
        n = self.n
        c = 2 * math.sqrt(n) / (1 + n)
        t = np.sinh(np.arctanh(np.sin(lat)) - c * np.arctanh(c * np.sin(lat)))
        xi = np.arctan2(t, np.cos(lon)); eta = np.arctanh(np.sin(lon) / np.sqrt(1 + t * t))
        x = eta.copy(); y = xi.copy()
        for j, a in enumerate(self.alpha, 1):
            y = y + a * np.sin(2 * j * xi) * np.cosh(2 * j * eta)
            x = x + a * np.cos(2 * j * xi) * np.sinh(2 * j * eta)
        return self.x0 + self.k0 * self.A * x, self.y0 + self.k0 * self.A * y

    def inverse(self, x, y):
        eta = (np.asarray(x, float) - self.x0) / (self.k0 * self.A)
        xi = (np.asarray(y, float) - self.y0) / (self.k0 * self.A)
        xp = xi.copy(); ep = eta.copy()
        for j, b in enumerate(self.beta, 1):
            xp = xp - b * np.sin(2 * j * xi) * np.cosh(2 * j * eta)
            ep = ep - b * np.cos(2 * j * xi) * np.sinh(2 * j * eta)
        chi = np.arcsin(np.sin(xp) / np.cosh(ep))
        n = self.n
        d = [2 * n - 2 * n ** 2 / 3 - 2 * n ** 3, 7 * n ** 2 / 3 - 8 * n ** 3 / 5, 56 * n ** 3 / 15]
        phi = chi.copy()
        for j, dj in enumerate(d, 1):
            phi = phi + dj * np.sin(2 * j * chi)
        lon = self.lon0 + np.arctan2(np.sinh(ep), np.cos(xp))
        return np.degrees(lon), np.degrees(phi)


def utm(zone):
    return Tm(-183 + 6 * zone)


def bilinear(a, x0, y0, dx, dy, X, Y, nodata=None):
    """Sample array a (pixel corner origin x0,y0 NW, centres at +0.5) at map X, Y (same CRS). NaN outside / nodata."""
    c = (X - x0) / dx - 0.5
    r = (y0 - Y) / dy - 0.5
    c0 = np.floor(c).astype(np.int64); r0 = np.floor(r).astype(np.int64)
    fc = c - c0; fr = r - r0
    ok = (c0 >= 0) & (r0 >= 0) & (c0 + 1 < a.shape[1]) & (r0 + 1 < a.shape[0])
    c0 = np.clip(c0, 0, a.shape[1] - 2); r0 = np.clip(r0, 0, a.shape[0] - 2)
    v00 = a[r0, c0]; v01 = a[r0, c0 + 1]; v10 = a[r0 + 1, c0]; v11 = a[r0 + 1, c0 + 1]
    v = (v00 * (1 - fc) + v01 * fc) * (1 - fr) + (v10 * (1 - fc) + v11 * fc) * fr
    if nodata is not None:
        bad = (v00 == nodata) | (v01 == nodata) | (v10 == nodata) | (v11 == nodata)
        ok &= ~bad
    v = np.where(ok, v, np.nan)
    return v


def write_flt(path_noext, z, xll_corner, yll_corner, cell):
    """z: rows north->south float32 (NaN = nodata). Writes ESRI .flt + .hdr (LSBFIRST, NODATA -9999)."""
    z = np.where(np.isnan(z), -9999.0, z).astype("<f4")
    with open(path_noext + ".flt", "wb") as f:
        f.write(z.tobytes())
    with open(path_noext + ".hdr", "w") as f:
        f.write(f"ncols {z.shape[1]}\nnrows {z.shape[0]}\nxllcorner {xll_corner:.6f}\nyllcorner {yll_corner:.6f}\ncellsize {cell:.6f}\nNODATA_value -9999\nbyteorder LSBFIRST\n")
