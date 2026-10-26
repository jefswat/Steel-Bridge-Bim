"""Automatic sheet index of a plan-set PDF (page -> sheet no. -> title), the skeleton of a sheet-links rules file (`sbm pipeline`).

usage: python -I plan_index_auto.py <pdf> <out.json> [--ocr auto|off] [--ocr-dpi 150]

Vector (text-layer) pages: the title block is read from the text layer - first the Iowa DOT 11x17 title block (same as plan_split.py), else a
generic reader (sheet number = the largest number-like token in the bottom-right corner, preferring one next to a SHEET / DWG / NO. label; title =
the largest text lines of the bottom-right title-block region, boilerplate removed). Raster pages (no text layer): OCR through PyMuPDF +
Tesseract when Tesseract is installed (--ocr auto), else the page is listed with src "raster" and an empty sheet / title = NEEDS OCR or hand work.
The result is a SKELETON: the pipeline never overwrites a hand-curated index with it (it only fills pages the curated index lacks).

Output JSON: {"pdf", "page_count", "text_pages", "raster_pages", "ocr_available", "ocr_pages", "empty", "low_confidence",
              "sheets": [{"p", "s", "t", "src" (text | ocr | raster), "q" (ok | low)}]}
Measured against the hand-curated indexes (2026-10-26): Nebraska St (Iowa, vector) sheet no. 69 / 71, title 62 / 71 (the rest = hand-added
parentheticals); Woodbury 43 / 48 and 43 / 48; NHT (TxDOT, 134 of 155 pages scanned) sheet no. 153 / 155 from the stamped text, titles need OCR.
"""
import sys, os, re, json, shutil

args = [x for x in sys.argv[1:] if not x.startswith("--")]
if len(args) < 2:
    sys.exit(__doc__)
pdf, out = args[0], args[1]
ocr_mode = sys.argv[sys.argv.index("--ocr") + 1] if "--ocr" in sys.argv else "auto"
ocr_dpi = int(sys.argv[sys.argv.index("--ocr-dpi") + 1]) if "--ocr-dpi" in sys.argv else 150
import pymupdf

NUM = re.compile(r"^(?:[A-Z]{1,3}\d?[-.]?)?\d{1,4}[A-Z]?$")
LABEL = re.compile(r"^(SHEET|SHT\.?|DWG\.?|DRAWING|NO\.?|NUMBER)$", re.I)
BOILER = re.compile(r"COUNTY|DESIGNED|DRAWN|CHECKED|APPROVED|DATE|SCALE|PROJECT\s*NO|REVISION|SHEET\s*NO|STATE\s+OF|DEPARTMENT|ENGINEER|"
                    r"FILE\s*NAME|PLOT|CONTRACT|SEAL|LICENSE|P\.E\.|DIVISION|BRIDGE\s*NO|^NO\.?$|^OF$|TOTAL\s+SHEETS", re.I)


def ocr_available():
    if ocr_mode == "off" or not hasattr(pymupdf.Page, "get_textpage_ocr"):
        return False
    return bool(shutil.which("tesseract") or os.environ.get("TESSDATA_PREFIX"))


def iowa(page, ws):
    """Iowa DOT 11x17 title block (plan_split.py)."""
    W, H = page.rect.width / 1224, page.rect.height / 792
    big = [w for w in ws if w[0] > 900 * W and 685 * H < w[1] < 728 * H and w[3] - w[1] > 9 * H]
    lines = {}
    for w in sorted(big, key=lambda w: (round(w[1]), w[0])):
        lines.setdefault(round(w[1]), []).append(w[4])
    title = " / ".join(" ".join(v) for v in lines.values() if "COUNTY" not in " ".join(v))
    sheet = ""
    for w in ws:
        if 735 * H < w[1] < 746 * H and 975 * W < w[0] < 998 * W and re.fullmatch(r"\d+[A-Z]?", w[4]):
            sheet = w[4]
    return sheet, title


def generic(page, ws):
    R = page.rect
    corner = [w for w in ws if w[0] > R.width * 0.72 and w[1] > R.height * 0.80]
    sheet, best = "", None
    for w in corner:
        tok = w[4].strip(".,:")
        if not NUM.match(tok):
            continue
        h = w[3] - w[1]
        near_label = any(LABEL.match(v[4].strip(":")) and abs(v[1] - w[1]) < 4 * h and -1 * h < w[0] - v[2] < 12 * h for v in corner) \
            or any(LABEL.match(v[4].strip(":")) and 0 < w[1] - v[3] < 3 * h and abs(v[0] - w[0]) < 10 * h for v in corner)
        key = (near_label, round(h, 1), w[0] + w[1])
        if best is None or key > best:
            best, sheet = key, tok
    region = [w for w in ws if w[0] > R.width * 0.55 and w[1] > R.height * 0.72 and w[4].strip(".,:") != sheet]
    if not region:
        return sheet, ""
    lines = {}
    for w in region:
        lines.setdefault((w[5], w[6]), []).append(w)
    cand = []
    for ln in lines.values():
        txt = " ".join(v[4] for v in sorted(ln, key=lambda v: v[0]))
        h = max(v[3] - v[1] for v in ln)
        if len(txt) < 4 or BOILER.search(txt) or not re.search(r"[A-Za-z]{3}", txt):
            continue
        cand.append((h, min(v[1] for v in ln), min(v[0] for v in ln), txt))
    if not cand:
        return sheet, ""
    hmax = max(c[0] for c in cand)
    top = sorted([c for c in cand if c[0] >= hmax * 0.8], key=lambda c: (round(c[1]), c[2]))[:3]
    return sheet, " / ".join(c[3] for c in top)


doc = pymupdf.open(pdf)
can_ocr = ocr_available()
rows, text_pages, raster_pages, ocr_pages = [], 0, 0, 0
for i, page in enumerate(doc, start=1):
    ws = page.get_text("words")
    src = "text"
    stamp = []
    # A scanned sheet: (almost) no text layer, or only a few stamped words (date / sheet number) over an image.
    if len(ws) < 8 or (len(ws) < 30 and page.get_images()):
        raster_pages += 1
        src, stamp = "raster", ws
        if can_ocr:
            try:
                tp = page.get_textpage_ocr(dpi=ocr_dpi, full=True)
                ws = page.get_text("words", textpage=tp)
                src, ocr_pages = "ocr", ocr_pages + 1
            except Exception as ex:  # tesseract missing / failed
                print(f"p{i}: OCR failed: {ex}")
                can_ocr = False
    else:
        text_pages += 1
    sheet, title = ("", "")
    if ws:
        sheet, title = iowa(page, ws)
        if not title or not sheet:
            s2, t2 = generic(page, ws)
            sheet, title = sheet or s2, title or t2
        if src == "raster":
            title = ""   # stamped words are not a title
    title = re.sub(r"\s+", " ", title.replace("`", " ")).strip()
    # Confidence: a title of at least two real words and a sheet number; route / county strings of a title block column are "low".
    low = not title or not sheet or len(re.findall(r"[A-Za-z]{3,}", title)) < 2 or re.search(r"\betc\b", title, re.I) is not None
    rows.append({"p": i, "s": sheet, "t": title, "src": src, "q": "low" if low else "ok"})
res = {
    "pdf": os.path.basename(pdf), "page_count": len(rows), "text_pages": text_pages, "raster_pages": raster_pages,
    "ocr_available": can_ocr, "ocr_pages": ocr_pages, "empty": sum(1 for r in rows if not r["t"]),
    "low_confidence": sum(1 for r in rows if r["q"] == "low"), "sheets": rows,
}
os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
with open(out, "w", encoding="utf-8") as f:
    json.dump(res, f, indent=1)
print(f"RESULT {json.dumps({k: v for k, v in res.items() if k != 'sheets'})}")
