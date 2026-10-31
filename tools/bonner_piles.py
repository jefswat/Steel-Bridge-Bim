# Bonner B-2500 Unit 5 (bents 18-29): pile positions + 2:12 batter directions read from the VECTOR footing plans.
# Each pile symbol carries a filled batter arrow ("INDICATES DIRECTION OF 2:12 BATTER") whose centroid lies ~2.9 ft beyond the pile centre in the
# batter direction; the pile centre = arrow - 2.9 ft x direction, snapped to the 3'-9" grid of the dimension strings (all plan pile coordinates are
# multiples of 3.75 ft from the CL footing). Scale 3/16" = 1'-0" (13.5 pt/ft); centre = midpoint of the arrow extents (patterns are symmetric).
# Output: docs/plan-data/bonner-b2500/segmental-unit5/piles.csv (one row per pile).  Run: python -I tools/bonner_piles.py <plan.pdf> <out.csv>
import sys, math, csv
import pymupdf as fitz

PLANS = [  # (pdf page, sheet, bents, clip of the footing plan in pt)
    (335, "S3-87", [18, 29], (290, 40, 1000, 600)),
    (338, "S3-90", [19], (230, 40, 1000, 620)),
    (341, "S3-93", [20, 21, 22], (200, 40, 1050, 620)),
    (344, "S3-96", [23, 24], (230, 280, 960, 1110)),
    (349, "S3-101", [25], (200, 40, 1050, 620)),
    (352, "S3-104", [26, 27, 28], (230, 40, 1000, 620)),
]
PT_PER_FT = 13.5
ARROW_OFFSET_FT = 2.9
GRID = 3.75


def arrows(page, clip):
    out = []
    for d in page.get_drawings():
        if not clip.contains(fitz.Rect(d['rect'])) or d['type'] != 'f':
            continue
        if not all(i[0] == 'l' for i in d['items']) or not 12 < max(d['rect'].width, d['rect'].height) < 30:
            continue
        pts = []
        for it in d['items']:
            pts += [it[1], it[2]]
        u = []
        for q in pts:
            if not any(abs(q.x - r.x) < 0.3 and abs(q.y - r.y) < 0.3 for r in u):
                u.append(q)
        if len(u) != 3:
            continue
        for i in range(3):
            o = [u[j] for j in range(3) if j != i]
            if abs(o[0].x - o[1].x) < 0.5 or abs(o[0].y - o[1].y) < 0.5:   # base axis-aligned -> u[i] is the apex
                mx, my = (o[0].x + o[1].x) / 2, (o[0].y + o[1].y) / 2
                dx, dy = u[i].x - mx, u[i].y - my
                L = math.hypot(dx, dy)
                out.append((sum(q.x for q in u) / 3, sum(q.y for q in u) / 3, dx / L, dy / L))
                break
    return out


def main(pdf, out):
    doc = fitz.open(pdf)
    rows = []
    for p, sheet, bents, clip in PLANS:
        a = arrows(doc[p - 1], fitz.Rect(*clip))
        xs = [v[0] for v in a]; ys = [v[1] for v in a]
        cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        piles = []
        for x, y, dx, dy in a:
            s = (x - cx) / PT_PER_FT - ARROW_OFFSET_FT * dx
            n = -(y - cy) / PT_PER_FT + ARROW_OFFSET_FT * dy
            ss, nn = round(s / GRID) * GRID, round(n / GRID) * GRID
            if abs(ss - s) > 0.15 or abs(nn - n) > 0.15:
                raise SystemExit(f"p{p}: pile at ({s:.2f}, {n:.2f}) is off the 3'-9\" grid")
            piles.append((nn, ss, round(dx), round(-dy)))
        piles.sort(key=lambda t: (-t[0], t[1]))
        for b in bents:
            for k, (nn, ss, bs, bn) in enumerate(piles, 1):
                rows.append([b, k, f"{ss:.2f}", f"{nn:.2f}", bs, bn, p, sheet])
        print(f"p{p} {sheet} bents {bents}: {len(piles)} piles")
    with open(out, "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["bent", "pile", "s_ft_from_cl_footing", "n_ft_from_bent_control_line", "batter_dir_s", "batter_dir_n", "pdf_page", "sheet"])
        w.writerows(rows)
    print(f"{len(rows)} rows -> {out}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
