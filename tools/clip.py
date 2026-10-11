import sys, pymupdf as fitz
ROOT = r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\dist\SteelBridgeModeler' + '\\'
NAMES = {'116': 'B00168 - CBIS-ESI-Westbound I-80 - Design 116.pdf', '316': 'B00169 - CBIS-ESI-Ramp G - Design 316.pdf'}
# clip.py design page x0 y0 x1 y1 scale out
des, pg, x0, y0, x1, y1, sc, out = sys.argv[1:9]
d = fitz.open(ROOT + NAMES[des])
pix = d[int(pg) - 1].get_pixmap(matrix=fitz.Matrix(float(sc), float(sc)), clip=fitz.Rect(float(x0), float(y0), float(x1), float(y1)), colorspace=fitz.csGRAY)
pix.save(out)
print(pix.width, pix.height)
d.close()
