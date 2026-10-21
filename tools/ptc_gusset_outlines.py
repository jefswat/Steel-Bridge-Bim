"""PTC Delaware River Bridge - gusset plate outlines read from the raster joint sheets (2026-10-20).

Reading method (docs/plan-data/ptc-delaware-river/connections/README.md, "Gusset outlines"): the gusset is read as a polygon in ABSOLUTE page pixels
at a chosen dpi from grid-overlaid renders, together with the work point (intersection of the member centre lines), two points on the chord
centre line (direction only) and the sheet scale (px per inch from a printed dimension). `convert` turns the pixel readings into the joint frame
in inches (u = drawing x = toward the higher panel number, v up, origin = work point) and adds the drawn chord angle, size and area.

  python -I tools/ptc_gusset_outlines.py grid PAGE DPI X0 Y0 X1 Y1 out.png [grid=20] [poly="x,y;x,y;..."] [marks="x,y;..."]
      render a crop (absolute page pixels at DPI) with a labelled grid (labels = absolute coordinates), optional polygon (green) / marks (blue)
  python -I tools/ptc_gusset_outlines.py prof PAGE DPI Y X0 X1          dark runs along row Y   (centre[width])
  python -I tools/ptc_gusset_outlines.py prof PAGE DPI x=X Y0 Y1        dark runs along column X
  python -I tools/ptc_gusset_outlines.py convert raw.csv out.csv        derive outline_uv_in etc. (rows keep their pixel data)
  python -I tools/ptc_gusset_outlines.py overlay out.csv ROWINDEX out.png [margin_px]   the row's polygon + work point over the page

One page is opened per call (the 108-page raster PDF is large).
"""
import csv, math, sys

PDF = r"C:\SBMData\Original Existing Bridge Plans from PTC.pdf"


def _page(pg):
    import pymupdf
    return pymupdf.open(PDF)[pg - 1]


def _render(pg, dpi, x0, y0, x1, y1):
    import pymupdf
    from PIL import Image
    p = _page(pg)
    k = 72.0 / dpi
    pix = p.get_pixmap(matrix=pymupdf.Matrix(dpi / 72.0, dpi / 72.0), clip=pymupdf.Rect(x0 * k, y0 * k, x1 * k, y1 * k), colorspace=pymupdf.csGRAY)
    return Image.frombytes("L", (pix.width, pix.height), pix.samples)


def pts(s):
    return [tuple(float(c) for c in q.split(',')) for q in s.split(';') if q.strip()]


def grid(argv):
    from PIL import ImageDraw
    pg, dpi = int(argv[0]), int(argv[1])
    x0, y0, x1, y1 = [float(v) for v in argv[2:6]]
    out = argv[6]
    opts = dict(a.split('=', 1) for a in argv[7:])
    g = int(opts.get('grid', 20))
    z = float(opts.get('zoom', 1))  # render at dpi * zoom, grid labels stay in page pixels at DPI
    src = _render(pg, dpi * z, x0 * z, y0 * z, x1 * z, y1 * z)
    im = src.convert("RGB")
    d = ImageDraw.Draw(im)
    if g > 0:
        for x in range(int(math.ceil(x0 / g)) * g, int(x1) + 1, g):
            major = x % (2 * g) == 0
            d.line([((x - x0) * z, 0), ((x - x0) * z, im.height)], fill=(255, 110, 110) if major else (255, 185, 185), width=1)
            if major:
                d.text(((x - x0) * z + 2, 2), str(x), fill=(220, 0, 0))
                d.text(((x - x0) * z + 2, im.height - 12), str(x), fill=(220, 0, 0))
        for y in range(int(math.ceil(y0 / g)) * g, int(y1) + 1, g):
            major = y % (2 * g) == 0
            d.line([(0, (y - y0) * z), (im.width, (y - y0) * z)], fill=(110, 110, 255) if major else (185, 185, 255), width=1)
            if major:
                d.text((2, (y - y0) * z + 2), str(y), fill=(0, 0, 220))
                d.text((im.width - 30, (y - y0) * z + 2), str(y), fill=(0, 0, 220))
        px, sp = im.load(), src.load()
        for yy in range(im.height):
            for xx in range(im.width):
                if sp[xx, yy] < 128:
                    px[xx, yy] = (sp[xx, yy],) * 3
    if 'poly' in opts:
        P = pts(opts['poly'])
        d.line([((x - x0) * z, (y - y0) * z) for x, y in P + [P[0]]], fill=(0, 170, 0), width=2)
    if 'marks' in opts:
        for x, y in pts(opts['marks']):
            d.ellipse([(x - x0) * z - 5, (y - y0) * z - 5, (x - x0) * z + 5, (y - y0) * z + 5], outline=(0, 140, 255), width=2)
    im.save(out)
    print(im.size)


def prof(argv):
    import numpy as np, pymupdf
    pg, dpi = int(argv[0]), int(argv[1])
    k = 72.0 / dpi
    p = _page(pg)
    if argv[2].startswith('x='):
        x = float(argv[2][2:]); a0, a1 = float(argv[3]), float(argv[4])
        pix = p.get_pixmap(dpi=dpi, clip=pymupdf.Rect((x - 1) * k, a0 * k, (x + 2) * k, a1 * k), colorspace=pymupdf.csGRAY)
        arr = np.frombuffer(pix.samples, np.uint8).reshape(pix.height, pix.width).min(axis=1)
    else:
        y = float(argv[2]); a0, a1 = float(argv[3]), float(argv[4])
        pix = p.get_pixmap(dpi=dpi, clip=pymupdf.Rect(a0 * k, (y - 1) * k, a1 * k, (y + 2) * k), colorspace=pymupdf.csGRAY)
        arr = np.frombuffer(pix.samples, np.uint8).reshape(pix.height, pix.width).min(axis=0)
    dark = arr < 140
    runs, i = [], 0
    while i < len(dark):
        if dark[i]:
            j = i
            while j < len(dark) and dark[j]:
                j += 1
            runs.append((a0 + i, a0 + j - 1)); i = j
        else:
            i += 1
    print(' '.join(f"{(a + b) / 2:.1f}" + (f"[{int(b - a + 1)}]" if b - a > 2 else '') for a, b in runs))


def area(P):
    return 0.5 * sum(P[i][0] * P[(i + 1) % len(P)][1] - P[(i + 1) % len(P)][0] * P[i][1] for i in range(len(P)))


def derive(r):
    s = float(r['px_per_in'])
    wx, wy = pts(r['wp_px'])[0]
    (ax, ay), (bx, by) = pts(r['chord_px'])[:2]
    dx, dy = bx - ax, -(by - ay)
    if dx < 0 or (dx == 0 and dy < 0):
        dx, dy = -dx, -dy
    r['drawn_chord_deg'] = f"{math.degrees(math.atan2(dy, dx)):.2f}"
    uv = [((x - wx) / s, -(y - wy) / s) for x, y in pts(r['outline_px'])]
    if area(uv) < 0:
        uv.reverse()
    r['outline_uv_in'] = '; '.join(f"{u:.1f},{v:.1f}" for u, v in uv)
    r['width_in'] = f"{max(u for u, _ in uv) - min(u for u, _ in uv):.1f}"
    r['height_in'] = f"{max(v for _, v in uv) - min(v for _, v in uv):.1f}"
    r['area_sf'] = f"{area(uv) / 144:.2f}"
    return r


COLS = ['structure', 'joint', 'also_applies_to', 'page', 'dpi', 'px_per_in', 'scale_basis', 'wp_px', 'chord_px', 'outline_px',
        'drawn_chord_deg', 'outline_uv_in', 'width_in', 'height_in', 'area_sf', 'evidence', 'confidence', 'note']


def convert(argv):
    rows = list(csv.DictReader(open(argv[0], newline='', encoding='utf-8')))
    out = [derive(dict(r)) for r in rows]
    with open(argv[1], 'w', newline='', encoding='utf-8') as f:
        w = csv.DictWriter(f, fieldnames=COLS, extrasaction='ignore')
        w.writeheader()
        for r in out:
            w.writerow({c: r.get(c, '') for c in COLS})
    for r in out:
        print(f"{r['structure']:22s} {r['joint']:6s} p.{r['page']:3s} {r['width_in']:>6s} x {r['height_in']:>6s} in  {r['area_sf']:>6s} SF  chord {r['drawn_chord_deg']} deg")


def overlay(argv):
    rows = list(csv.DictReader(open(argv[0], newline='', encoding='utf-8')))
    r = rows[int(argv[1])]
    m = float(argv[3]) if len(argv) > 3 else 40
    P = pts(r['outline_px'])
    x0, y0 = min(x for x, _ in P) - m, min(y for _, y in P) - m
    x1, y1 = max(x for x, _ in P) + m, max(y for _, y in P) + m
    grid([r['page'], r['dpi'], str(x0), str(y0), str(x1), str(y1), argv[2], 'grid=0', 'poly=' + r['outline_px'], 'marks=' + r['wp_px']])


if __name__ == '__main__':
    cmd = sys.argv[1]
    {'grid': grid, 'prof': prof, 'convert': convert, 'overlay': overlay}[cmd](sys.argv[2:])
