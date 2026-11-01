"""Render plan pages (or a crop) for the segment agent review.
usage: python -I render.py PAGE [dpi] [x0 y0 x1 y1 in page points] [tag]"""
import fitz, sys
doc = fitz.open(r"C:/SBMData/B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf")
p = int(sys.argv[1]); dpi = int(sys.argv[2]) if len(sys.argv) > 2 else 110
pg = doc[p - 1]
clip = fitz.Rect(*map(float, sys.argv[3:7])) if len(sys.argv) >= 7 else None
tag = sys.argv[7] if len(sys.argv) > 7 else ""
out = f"C:/SBM/qc/bonner-unit5/segments/img/p{p}{tag}.png"
pg.get_pixmap(dpi=dpi, clip=clip).save(out)
print(out, pg.rect)
