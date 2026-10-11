# Extract bend-diagram dimensions per mark from a plan sheet; compare to the bar list length.
import sys, re, csv, itertools, json
sys.path.insert(0, r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\.worktrees\bends\tools')
import pymupdf as fitz
import plantext

ROOT = r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler' + '\\'
PDFS = {'116': ROOT + r'dist\SteelBridgeModeler\B00168 - CBIS-ESI-Westbound I-80 - Design 116.pdf',
        '316': ROOT + r'dist\SteelBridgeModeler\B00169 - CBIS-ESI-Ramp G - Design 316.pdf'}
FR = {1: 1/2, 2: 1/4, 3: 3/4, 4: 1/8, 5: 3/8, 6: 5/8, 7: 7/8, 8: 1/16, 9: 3/16, 10: 5/16, 11: 7/16, 12: 9/16, 13: 11/16, 14: 13/16, 15: 15/16}
FRS = {1: '1/2', 2: '1/4', 3: '3/4', 4: '1/8', 5: '3/8', 6: '5/8', 7: '7/8', 8: '1/16', 9: '3/16', 10: '5/16', 11: '7/16', 12: '9/16', 13: '11/16', 14: '13/16', 15: '15/16'}
MARK = re.compile(r'^(\d{1,2}[a-z]\d{1,2})(, ?\d{1,2}[a-z]\d{1,2})*$', re.I)


def decode(s):
    """raw text (octal escapes as backslash-ddd, \\x01 = 1/2) -> (clean text, inches or None)"""
    def rep(m):
        n = int(m.group(1), 8) - 0o200
        return '{' + str(n) + '}'
    s = s.replace('\x01', '{1}')
    s = re.sub(r'\\(2[01][0-7])', rep, s)
    s = re.sub(r'\\(26[0-7])', '*', s)
    return s


def inches(txt):
    t = txt.strip()
    pre = ''
    m = re.match(r'^(D|R)=(.*)$', t)
    if m: pre, t = m.group(1), m.group(2)
    suf = ''
    m = re.match(r'^(.*?)\s*(MAX\.|MIN\.)\s*$', t)
    if m: t, suf = m.group(1), m.group(2)
    m = re.match(r"^(?:(\d+)'-?)?\s*(\d+)?\s*(?:\{(\d+)\})?$", t)
    if not m or (m.group(1) is None and m.group(2) is None and m.group(3) is None): return None
    ft = int(m.group(1)) if m.group(1) else 0
    i = int(m.group(2)) if m.group(2) else 0
    f = FR[int(m.group(3))] if m.group(3) else 0
    return pre, ft * 12 + i + f, suf


def sheet_items(design, page):
    d = fitz.open(PDFS[design])
    p = d[page - 1]
    items = plantext.texts(p)
    d.close()
    out = []
    for it in items:
        s = decode(it['s'])
        ang = None
        out.append(dict(x=it['x'], y=it['y'], s=s, a=it['a'], b=it['b'], fs=it['fs']))
    return out


def barlist(design):
    rows = {}
    for r in csv.DictReader(open(ROOT + r'temp\ftg-rebar\planbars2.csv')):
        if r['set'] != design: continue
        rows.setdefault(int(r['page']), []).append(r)
    return rows


def plen(s):
    m = re.match(r"^(\d+)'-(\d+)$", s)
    return int(m.group(1)) * 12 + int(m.group(2)) if m else None


def analyse(design, page, rows, xmin=500, xmax=862):
    its = sheet_items(design, page)
    labels = []
    dims = []
    for it in its:
        if not (xmin <= it['x'] <= xmax): continue
        txt = it['s'].strip()
        if MARK.match(txt):
            labels.append(it)
            continue
        v = inches(txt)
        if v and it['y'] < 755: dims.append(dict(it, pre=v[0], val=v[1], suf=v[2]))
    # assign each dim to the nearest label below it (same column band)
    for dm in dims:
        best = None
        for lb in labels:
            dy = lb['y'] - dm['y']
            dx = abs(lb['x'] + 8 - dm['x'])
            if dy < -4 or dy > 140 or dx > 95: continue
            sc = max(dy, 0) + 0.6 * dx
            if best is None or sc < best[0]: best = (sc, lb)
        dm['label'] = best[1]['s'].strip() if best else None
    res = {}
    for dm in dims:
        res.setdefault(dm['label'], []).append(dm)
    return labels, dims, res


def recon(ds, L):
    if L is None: return '?'
    vals = [d['val'] for d in ds if d['pre'] == '' and d['suf'] == '']
    best = None
    for mult in itertools.product(*[(0, 1, 2) for _ in vals]) if len(vals) <= 9 else []:
        t = sum(m * v for m, v in zip(mult, vals))
        if abs(t - L) <= 1.0:
            n = sum(1 for m in mult if m)
            if best is None or n < best[0]: best = (n, mult)
    return ('ok' if best else 'NO')


def main(design, page):
    rows = barlist(design)[page]
    labels, dims, res = analyse(design, page, rows)
    lens = {}
    for r in rows:
        m = re.match(r"^(\d+)'-(\d+)", r['len'])
        lens[r['mark']] = (int(m.group(1)) * 12 + int(m.group(2))) if m else None
    for lb in sorted(labels, key=lambda l: (round(l['y'] / 40), l['x'])):
        key = lb['s'].strip()
        ds = res.get(key, [])
        marks = [x.strip() for x in key.split(',')]
        txt = ' '.join(('%s%s%s%s' % (d['pre'] + ('=' if d['pre'] else ''), round(d['val'], 3), d['suf'], 'v' if d['b'] != 0 else '')) for d in sorted(ds, key=lambda d: (d['pre'], d['y'], d['x'])))
        print(key, [lens.get(m) for m in marks], '|', txt, '|', ' '.join(recon(ds, lens.get(m)) for m in marks))


if __name__ == '__main__':
    main(sys.argv[1], int(sys.argv[2]))


def analyse2(design, page, xmin=500, xmax=862, ymax=745, abut=False):
    """groups: title label -> shared dims + per-mark dims (dim labels)."""
    its = sheet_items(design, page)
    titles, dlabels, dims = [], [], []
    for it in its:
        if not (xmin <= it['x'] <= xmax and it['y'] < ymax): continue
        txt = it['s'].strip()
        if MARK.match(txt):
            ((titles if it['b'] == 0 else dlabels) if abut else (titles if it['fs'] >= 4.502 else dlabels)).append(it)
            continue
        v = inches(txt)
        if v: dims.append(dict(it, pre=v[0], val=v[1], suf=v[2], label=None, own=None))
    for dm in dims:
        best = None
        for lb in titles:
            dy = lb['y'] - dm['y']
            dx = abs(lb['x'] + 8 - dm['x'])
            if dy < -4 or dy > 140 or dx > 95: continue
            sc = max(dy, 0) + 0.6 * dx
            if best is None or sc < best[0]: best = (sc, lb)
        dm['label'] = best[1]['s'].strip() if best else None
    for lb in dlabels:
        best = None
        for dm in dims:
            if dm['pre']: continue
            dx = dm['x'] - lb['x']; dy = dm['y'] - lb['y']
            rot = lb['b'] != 0
            if rot:
                # rotated label sits above its vertical dimension
                if dm['b'] == 0 or abs(dx) > 14 or dy < 0 or dy > 40: continue
                sc = dy + abs(dx)
            else:
                if dm['b'] != 0 or abs(dy) > 5 or dx > 5 or dx < -60: continue
                sc = abs(dy) * 3 + abs(dx)
            if best is None or sc < best[0]: best = (sc, dm)
        if best: best[1]['own'] = lb['s'].strip()
    return titles, dlabels, dims
