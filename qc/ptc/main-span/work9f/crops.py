"""Crop each listed main-span joint around its work point (gusset-outlines.csv wp_px at 150 dpi) at 150 dpi -> pg/j_<joint>.png"""
import csv, sys, fitz
rows = {r['joint']: r for r in csv.DictReader(open('docs/plan-data/ptc-delaware-river/connections/gusset-outlines.csv', encoding='utf-8')) if r['structure'] == 'main'}
d = fitz.open(r'C:\SBMData\Original Existing Bridge Plans from PTC.pdf')
R = float(sys.argv[2]) if len(sys.argv) > 2 else 330
for j in sys.argv[1].split(','):
    r = rows[j]
    x, y = [float(v) for v in r['wp_px'].split(',')]
    s = 72 / 150
    clip = fitz.Rect((x - R) * s, (y - R) * s, (x + R) * s, (y + R) * s)
    d[int(r['page']) - 1].get_pixmap(dpi=150, clip=clip).save(f'qc/ptc/main-span/work9f/pg/j_{j}.png')
    print(j, r['page'])
