# Asset helper for the companion database (G9), PyMuPDF only.
# usage:
#   python -I asset_tool.py thumb <image|pdf> <out.png> [--size 320] [--page 1]
#       PNG thumbnail, longest side <= size px (images: jpg/png/gif/bmp/tiff/webp...; PDFs: the given page)
#   python -I asset_tool.py pdftext <pdf> --pages 1,2
#       JSON {"page_count": n, "pages": [..], "text": "..."} = the text of the given pages (calc summary pages), page breaks marked
import sys, json, pymupdf


def opt(name, default):
    if name in sys.argv:
        return sys.argv[sys.argv.index(name) + 1]
    return default


def thumb(src, out, size, page):
    doc = pymupdf.open(src)
    p = doc[min(max(page, 1), doc.page_count) - 1]
    r = p.rect
    z = size / max(r.width, r.height)
    pix = p.get_pixmap(matrix=pymupdf.Matrix(z, z), alpha=False)
    pix.save(out)
    print(json.dumps({"out": out, "width": pix.width, "height": pix.height}))


def pdftext(src, pages):
    doc = pymupdf.open(src)
    parts = []
    for n in pages:
        if 1 <= n <= doc.page_count:
            parts.append(f"--- page {n} ---\n" + doc[n - 1].get_text("text").strip())
    print(json.dumps({"page_count": doc.page_count, "pages": pages, "text": "\n".join(parts)}, ensure_ascii=False))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    cmd = sys.argv[1]
    if cmd == "thumb":
        thumb(sys.argv[2], sys.argv[3], int(opt("--size", "320")), int(opt("--page", "1")))
    elif cmd == "pdftext":
        pdftext(sys.argv[2], [int(x) for x in opt("--pages", "1").split(",") if x])
    else:
        sys.exit("unknown command " + cmd)
