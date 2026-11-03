"""Before / after PDF: S truss joints (work10 png -> png_final) and bracing points (work11 png -> png_final). python -I make_pdf2.py <out.pdf>"""
import csv, io, sys, os, re
import fitz
from PIL import Image

H10 = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'work10')
H11 = os.path.dirname(os.path.abspath(__file__))
out = sys.argv[1]
W, H = 1224, 792
PX = int(os.environ.get('PDF_PX', '900')); Q = int(os.environ.get('PDF_Q', '60')); PART = os.environ.get('PDF_PART', 'all')
def jpg(path, px=None):
    px = px or PX
    im = Image.open(path).convert('RGB'); im.thumbnail((px, px))
    b = io.BytesIO(); im.save(b, 'JPEG', quality=Q); return b.getvalue()
conn = {}
for r in csv.DictReader(open(os.path.join(H11, 'conn_after.csv'))):
    pt = r['where'].split(':')[-1]
    pt = re.sub(r'-[a-z]$|-SW$|-T$|-B$', '', pt)
    conn.setdefault(pt, []).append(r)
doc = fitz.open()
p = doc.new_page(width=W, height=H)
p.insert_text((60, 90), "PTC Delaware River Bridge - main span, agent reviews 1 + 2 (2026-11-02 / 03): BEFORE (left) vs AFTER (right)", fontsize=20)
lines = [
    "Part 1: the 90 truss joints of the S truss (the N truss and the second half are exact mirrors, checked numerically).",
    "Part 2: the 233 lateral / strut / sway / floor lateral end points (S truss copy for panel points, CL / work points as they are).",
    "Views: A outside elevation, B inside elevation (u to the right = mirrored), C plan from above, D plan from below; inches from the point; u = up-station.",
    "Colours: gussets red, bracing plates / connection angles purple, splice plates orange, fills yellow, chords blue, diagonals green, verticals teal,",
    "bracing khaki, floor beams grey; black circles = rivets along the view. Walkways / ladders / rails are left out.",
    "",
    "Bracing connections after the fixes (generator audit, placed rivets whose shank crosses both parts):",
    "  member ends riveted to their plates 504 of 508; plates fastened to the truss 504 of 544; edge angles riveted to the stack 468 of 514.",
    "  L11 / L11' laterals 36 / 40 rivets per end (p.71: 36 / 40); the '-b' bottom plates at L11 / L11' are still not fastened (angle past the gusset edge).",
    "Still open (flagged in the model with page + reason): LM7-LM10 sway apex plates, LM12 sway diagonals, LM13 bottom plate to the F8 extension, LQ13 double K,",
    "W21 K at the tongue beam, L11 gusset outline, L13 / L13' splice plate fit, 514 one-ply rivet symbols beyond the gussets (owner plate not identified).",
]
for i, t in enumerate(lines): p.insert_text((60, 140 + 22 * i), t, fontsize=12)
def pages(title, before_dir, after_dir, names, note=lambda n: ""):
    for name, label in names:
        b, a = os.path.join(before_dir, name + '.png'), os.path.join(after_dir, name + '.png')
        if not os.path.exists(a): continue
        pg = doc.new_page(width=W, height=H)
        pg.insert_text((30, 34), f"{title} {label}", fontsize=18)
        n = note(label)
        if n: pg.insert_text((330, 34), n, fontsize=10)
        pg.insert_text((30, 70), "BEFORE", fontsize=12); pg.insert_text((W / 2 + 10, 70), "AFTER", fontsize=12)
        s = (W - 60) / 2
        if os.path.exists(b): pg.insert_image(fitz.Rect(20, 80, 20 + s, 80 + s), stream=jpg(b))
        pg.insert_image(fitz.Rect(W / 2 + 10, 80, W / 2 + 10 + s, 80 + s), stream=jpg(a))
J = sorted([j for j in csv.DictReader(open(os.path.join(H10, 'out_final', 'joints.csv'))) if j['plane'] == 'S'], key=lambda j: float(j['station']))
if PART in ("all", "joints"): pages("Truss joint", os.path.join(H10, 'png'), os.path.join(H10, 'png_final'), [(f"S_{j['name'].replace(chr(39), 'p')}", j['name']) for j in J])
B = sorted(csv.DictReader(open(os.path.join(H11, 'out_final', 'joints.csv'))), key=lambda j: float(j['station']))
def note(pt):
    rs = conn.get(pt, [])
    if not rs: return ""
    ends = [r for r in rs if r['kind'] == 'end']; pl = [r for r in rs if r['kind'] == 'plate']
    return (f"member ends riveted {sum(r['ok'] == '1' for r in ends)}/{len(ends)} (rivets {', '.join(r['n1'] for r in ends[:6])}); "
            f"plates fastened {sum(r['ok'] == '1' for r in pl)}/{len(pl)}")
if PART in ("all", "bracing"): pages("Bracing at", os.path.join(H11, 'png'), os.path.join(H11, 'png_final'), [(f"{j['plane']}_{j['name'].replace(chr(39), 'p')}", j['name']) for j in B], note)
doc.save(out, garbage=3, deflate=True)
print(out, len(doc), os.path.getsize(out) / 1e6, 'MB')
