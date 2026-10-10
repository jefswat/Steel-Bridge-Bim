# Builds a per-page text cache and a sheet index for plan-set PDFs.
# usage: python -I plan_index.py <pdf> <outdir>
import sys, os, re, pymupdf

pdf, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
doc = pymupdf.open(pdf)
rows = []
for i, page in enumerate(doc, start=1):
    text = page.get_text("text", sort=True)
    with open(os.path.join(out, f"p{i:03d}.txt"), "w", encoding="utf-8") as f:
        f.write(text)
    # Title block: text in the bottom-right region of the sheet (largest words there first).
    # Iowa DOT title block (11x17, 1224x792 pt): drawing title = large text (h ~12) at y 690-718, x > 900,
    # excluding the county line; design sheet no. = the number just left of "OF <total>" at y ~741.
    W, H = page.rect.width / 1224, page.rect.height / 792
    ws = page.get_text("words")
    big = [w for w in ws if w[0] > 900 * W and 685 * H < w[1] < 728 * H and w[3] - w[1] > 9 * H]
    lines = {}
    for w in sorted(big, key=lambda w: (round(w[1]), w[0])):
        lines.setdefault(round(w[1]), []).append(w[4])
    title = " / ".join(" ".join(v) for v in lines.values() if "COUNTY" not in " ".join(v))
    sheet = ""
    for w in ws:
        if 735 * H < w[1] < 746 * H and 975 * W < w[0] < 998 * W and re.fullmatch(r"\d+[A-Z]?", w[4]):
            sheet = w[4]
    if not title:  # other layouts: fall back to the largest words in the bottom-right corner
        cand = [w for w in ws if w[0] > page.rect.width * 0.6 and w[1] > page.rect.height * 0.8]
        if cand:
            hmax = max(w[3] - w[1] for w in cand)
            title = " ".join(w[4] for w in sorted(cand, key=lambda w: (round(w[1]), w[0])) if w[3] - w[1] > hmax * 0.8)
    nimg = len(page.get_images())
    rows.append((i, sheet, len(text), nimg, title[:120]))
with open(os.path.join(out, "INDEX.md"), "w", encoding="utf-8") as f:
    f.write(f"# Sheet index: {os.path.basename(pdf)}\n\nPDF page | design sheet | text chars | images | drawing title\n---|---|---|---|---\n")
    for i, s, n, im, t in rows:
        f.write(f"{i} | {s} | {n} | {im} | {t}\n")
print(f"{len(rows)} pages -> {out}")
