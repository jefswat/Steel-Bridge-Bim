"""Before / after joint images -> one PDF (S truss; the N truss is its exact mirror). python -I make_pdf.py <out.pdf>"""
import csv, io, sys, os
import fitz
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
out = sys.argv[1]
joints = [j for j in csv.DictReader(open(os.path.join(HERE, 'out_after', 'joints.csv'))) if j['plane'] == 'S']
joints.sort(key=lambda j: float(j['station']))
doc = fitz.open()
W, H = 1224, 792            # 17 x 11 in. landscape
def jpg(path, px=1100):
    im = Image.open(path).convert('RGB')
    im.thumbnail((px, px))
    b = io.BytesIO(); im.save(b, 'JPEG', quality=72); return b.getvalue()
# cover page
p = doc.new_page(width=W, height=H)
p.insert_text((60, 90), "PTC Delaware River Bridge - main span truss joints, agent review 2026-11-02", fontsize=22)
lines = [
    "Each page: one joint of the S truss, BEFORE the fixes (left) and AFTER (right). The N truss is the exact mirror (checked numerically).",
    "Four views per image: A outside elevation, B inside elevation (u to the right = mirrored), C plan from above, D plan from below.",
    "Inches from the joint working point; u = along the bridge, + = up-station. Gussets red, bracing plates purple, splice plates orange,",
    "fills yellow, chords blue, diagonals green, verticals / hangers teal, bracing khaki, floor beams grey; black circles = rivets along the view.",
    "Walkways / ladders / rails are left out of the images.",
    "",
    "Fixes between the two images: chord splice / inlay / fill material (41 joints), gusset outlines on the chord faces at kinked chords,",
    "re-traced outlines (L18, U16, U12, U14, own L19 / L21 / U21), edge angles, U0 end post, lug angles, suspender support diaphragms",
    "(L15-L22), U21 / L21 diagonal fills, strut connection angles (L1-L10), lateral plates (LM1-LM8, L13 1/2, L11) and lateral ends.",
    "Still open (flagged in the model): L13 / L13' splice plate fit, L11 gusset outline, U11 bent plate, L13 floor lateral plate, L12 fills,",
    "L22 chord diaphragms, L14 internal diaphragm, remaining envelope apex / end plates.",
]
for i, t in enumerate(lines): p.insert_text((60, 140 + 22 * i), t, fontsize=12)
for j in joints:
    tag = f"S_{j['name'].replace(chr(39), 'p')}"
    b, a = os.path.join(HERE, 'png', tag + '.png'), os.path.join(HERE, 'png_after', tag + '.png')
    p = doc.new_page(width=W, height=H)
    p.insert_text((30, 34), f"Joint {j['name']} (S truss)", fontsize=18)
    p.insert_text((30, 70), "BEFORE", fontsize=12); p.insert_text((W / 2 + 10, 70), "AFTER", fontsize=12)
    s = (W - 60) / 2
    if os.path.exists(b): p.insert_image(fitz.Rect(20, 80, 20 + s, 80 + s), stream=jpg(b))
    if os.path.exists(a): p.insert_image(fitz.Rect(W / 2 + 10, 80, W / 2 + 10 + s, 80 + s), stream=jpg(a))
doc.save(out, garbage=3, deflate=True)
print(out, len(doc), os.path.getsize(out) / 1e6, 'MB')
