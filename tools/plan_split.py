# Splits a plan-set PDF into single-page PDFs (+ JPEG previews) and writes sheets.json (PDF page -> design sheet -> title).
# usage: python -I plan_split.py <pdf> <outdir> [--no-preview] [--zoom 1.3]
# Title extraction is the same as plan_index.py (Iowa DOT 11x17 title block). Existing page files are kept (idempotent).
import sys, os, re, json, pymupdf

args = [a for a in sys.argv[1:] if not a.startswith("--")]
pdf, out = args[0], args[1]
preview = "--no-preview" not in sys.argv
zoom = 1.15
if "--zoom" in sys.argv:
    zoom = float(sys.argv[sys.argv.index("--zoom") + 1])
os.makedirs(out, exist_ok=True)
doc = pymupdf.open(pdf)
rows = []
for i, page in enumerate(doc, start=1):
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
    if not title:
        cand = [w for w in ws if w[0] > page.rect.width * 0.6 and w[1] > page.rect.height * 0.8]
        if cand:
            hmax = max(w[3] - w[1] for w in cand)
            title = " ".join(w[4] for w in sorted(cand, key=lambda w: (round(w[1]), w[0])) if w[3] - w[1] > hmax * 0.8)
    title = re.sub(r"\s+", " ", title.replace("`", " ")).strip()
    base = f"p{i:03d}"
    pf = os.path.join(out, base + ".pdf")
    if not os.path.exists(pf):
        one = pymupdf.open()
        one.insert_pdf(doc, from_page=i - 1, to_page=i - 1)
        one.save(pf, garbage=3, deflate=True)
        one.close()
    jf = ""
    if preview:
        jf = base + ".jpg"
        if not os.path.exists(os.path.join(out, jf)):
            page.get_pixmap(matrix=pymupdf.Matrix(zoom, zoom), colorspace=pymupdf.csGRAY).save(os.path.join(out, jf), jpg_quality=55)
    rows.append({"pdf_page": i, "design_sheet": sheet, "title": title, "page_file": base + ".pdf", "preview_file": jf})
with open(os.path.join(out, "sheets.json"), "w", encoding="utf-8") as f:
    json.dump({"pdf_file": os.path.basename(pdf), "sheets": rows}, f, indent=1)
print(f"{len(rows)} pages -> {out}")
