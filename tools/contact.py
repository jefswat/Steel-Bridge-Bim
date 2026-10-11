import sys; sys.path.insert(0, r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\.worktrees\bends\tools')
import plantext, pymupdf as fitz, re
from PIL import Image, ImageDraw
ROOT = r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\dist\SteelBridgeModeler' + '\\'
d = fitz.open(ROOT + 'B00168 - CBIS-ESI-Westbound I-80 - Design 116.pdf')
found = {}
for pg in range(18, 95):
    c = d[pg].read_contents().decode('latin-1')
    for m in re.finditer(r'\(([^()]*?\\(2[01][0-9])[^()]*?)\)Tj', c):
        code = m.group(2)
        if code in found: continue
        # locate Tm before
        tm = re.findall(r'([\-\d.]+) ([\-\d.]+) ([\-\d.]+) ([\-\d.]+) ([\-\d.]+) ([\-\d.]+) Tm', c[:m.start()])[-1]
        found[code] = (pg, float(tm[4]), 792.12 - float(tm[5]), m.group(1), float(tm[0]), float(tm[1]))
imgs = []
for code in sorted(found):
    pg, x, y, s, a, b = found[code]
    n = len(s.replace('\\', ''))
    # clip near the text (rotated text: use a square box)
    rect = fitz.Rect(x - 25, y - 25, x + 25, y + 25)
    pix = d[pg].get_pixmap(matrix=fitz.Matrix(5, 5), clip=rect, colorspace=fitz.csGRAY)
    im = Image.frombytes('L', (pix.width, pix.height), pix.samples)
    dr = ImageDraw.Draw(im); dr.text((2, 2), f'{code} p{pg+1} {s} rot={b!=0}', fill=0)
    imgs.append(im)
W = imgs[0].width
cols = 3
rows = (len(imgs) + cols - 1) // cols
sheet = Image.new('L', (W * cols, imgs[0].height * rows), 255)
for i, im in enumerate(imgs): sheet.paste(im, ((i % cols) * W, (i // cols) * imgs[0].height))
sheet.save(r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\.worktrees\bends\temp\bend\codes.png')
print(sheet.size)
for k, v in sorted(found.items()): print(k, v)
