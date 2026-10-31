import sys, re, json
sys.path.insert(0, r'C:\SBM\.worktrees\bends\tools')
import bendx, plantext
import pymupdf as fitz
R = json.load(open(r'C:\SBM\.worktrees\bends\temp\bend\plan_shapes.json'))
pages = sorted({(r['design'], r['page'], r['sheet']) for r in R if 'PIER' in r['sheet']})
pat = re.compile(sys.argv[1])
for design, page, sheet in pages:
    d = fitz.open(bendx.PDFS[design])
    for pg in (page - 1, page - 2, page - 3):
        res = []
        for it in plantext.texts(d[pg - 1]):
            s = bendx.decode(it['s']).strip()
            if pat.search(s): res.append((s, round(it['x']), round(it['y'])))
        if res: print(design, sheet[9:14], pg, res); break
    d.close()
