# Extract the FIELD HAUNCH DATA "MISCELLANEOUS DATA TABLE" (fractions) and "TABLE OF GIRDER LINE HAUNCH ELEVATIONS" (decimals)
# from the I-80 WB (116) / Ramp G (316) plan PDFs into long CSVs with the source page.
# Values are read from the drawn vector glyphs (vecfont.py): the text layer omits repeated cells. Fraction octal codes
# (201..217 octal = table FR below) were confirmed against the drawn glyphs on raster clips.
# Usage: python -I tools/haunch_extract.py <Design 116 PDF> <Design 316 PDF> <output dir> [<glyph examples .txt>]
#   (output dir: docs/plan-data/haunch; PDFs: dist/SteelBridgeModeler/B00168 ... Design 116.pdf, B00169 ... Design 316.pdf)
import sys, re, csv, collections, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pymupdf as fitz
import plantext
import vecfont

PDF = {'I80': '', 'RG': ''}      # set from the command line
MISC = {('I80', 1): [137, 138], ('I80', 2): [147, 148, 149, 150], ('I80', 3): [156, 157],
        ('RG', 1): [114], ('RG', 2): [118], ('RG', 3): [123], ('RG', 4): [127, 128]}
HELEV = {('I80', 1): [136], ('I80', 2): [145, 146], ('I80', 3): [155],
         ('RG', 1): [113], ('RG', 2): [117], ('RG', 3): [122], ('RG', 4): [126]}
FR = {1: 1/2, 2: 1/4, 3: 3/4, 4: 1/8, 5: 3/8, 6: 5/8, 7: 7/8, 8: 1/16, 9: 3/16, 10: 5/16, 11: 7/16, 12: 9/16, 13: 11/16, 14: 13/16, 15: 15/16}
FRS = {1: '1/2', 2: '1/4', 3: '3/4', 4: '1/8', 5: '3/8', 6: '5/8', 7: '7/8', 8: '1/16', 9: '3/16', 10: '5/16', 11: '7/16', 12: '9/16', 13: '11/16', 14: '13/16', 15: '15/16'}
OUT = ''


def items_with_glyphs(page):
    its = plantext.parse_page(page)
    res = []
    for i, it in enumerate(its):
        if it['t'] != 'text':
            continue
        paths = []
        j = i + 1
        while j < len(its) and its[j]['t'] == 'path':
            paths.append(its[j]); j += 1
        it = dict(it); it['paths'] = paths
        res.append(it)
    return res


def fingerprint(path):
    body = path['body']
    nums = [float(v) for v in re.findall(r'(-?[\d.]+) (-?[\d.]+) [ml]', body) for v in v]
    xs, ys = nums[0::2], nums[1::2]
    nm = len(re.findall(r' m', ' ' + body))
    return (nm, len(xs), round(max(xs) - min(xs)), round(max(ys) - min(ys)))


def cell_value(s):
    """'1\\211' -> (1 + 3/16, code 9); '\\x01' -> 1/2; '-\\210' -> -1/16; '0' -> 0; returns (value, code or 0, text)"""
    t = s.replace('\x01', '\\201').strip()
    m = re.fullmatch(r'(-?)(\d*)(?:\\(2[01][0-7]))?', t)
    if not m or (m.group(2) == '' and m.group(3) is None):
        return None
    sign = -1 if m.group(1) else 1
    ip = int(m.group(2)) if m.group(2) else 0
    code = int(m.group(3), 8) - 0o200 if m.group(3) else 0
    if code and code not in FR:
        return None
    v = ip + (FR[code] if code else 0)
    txt = (m.group(1) + (m.group(2) + ' ' if m.group(2) else '') + (FRS[code] if code else '')).strip() if code else (m.group(1) + m.group(2))
    return sign * v, code, txt


def header_rows(texts):
    """rows of consecutive point numbers (>= 4 on one y) -> list of (y, {x: number})"""
    by_y = collections.defaultdict(list)
    for t in texts:
        if re.fullmatch(r'\d{1,3}', t['s'].strip()) and t['b'] == 0:
            by_y[round(t['y'])].append(t)
    rows = []
    for y, ts in by_y.items():
        ts.sort(key=lambda t: t['x'])
        nums = [int(t['s']) for t in ts]
        run = sum(1 for a, b in zip(nums, nums[1:]) if b == a + 1)
        if len(ts) >= 5 and run >= len(ts) - 2 and all(b > a for a, b in zip(nums, nums[1:])):
            rows.append((y, ts))
    return sorted(rows, key=lambda r: r[0])



PROTOS = None


def protos():
    global PROTOS
    if PROTOS is None:
        docs = {k: fitz.open(v) for k, v in PDF.items()}
        PROTOS = vecfont.learn_protos([docs[br][p - 1] for (br, u), pgs in MISC.items() for p in pgs])
    return PROTOS


def glyph_cells(page, x_lo, x_hi, y_lo, y_hi):
    """cells built from the drawn glyphs (the text layer omits repeated values): pseudo text items {x, y, s, sym}"""
    gl = [g for g in vecfont.cell_glyphs(page, protos()) if x_lo <= g['xc'] <= x_hi and y_lo <= g['yc'] + 2.25 <= y_hi]
    known = [g for g in gl if g['ch'] is not None]
    unk = [g for g in gl if g['ch'] is None and 2 < g['h'] < 8 and g['w'] < 8]
    rows = []
    for g in sorted(known, key=lambda g: g['yc']):
        if rows and g['yc'] - rows[-1][-1]['yc'] <= 2.5: rows[-1].append(g)
        else: rows.append([g])
    out = []
    for r in rows:
        r.sort(key=lambda g: g['x0'])
        cell = []
        for g in r:
            if cell and g['x0'] - cell[-1]['x1'] > 4.0:
                out.append(cell); cell = []
            cell.append(g)
        if cell: out.append(cell)
    items = []
    for c in out:
        s = ''
        for g in c:
            s += ('\\' + oct(0o200 + int(g['ch'][1:]))[2:]) if g['ch'].startswith('F') else g['ch']
        yc = sum(g['yc'] for g in c) / len(c)
        items.append(dict(x=c[0]['x0'] - 0.8, xc=(c[0]['x0'] + c[-1]['x1']) / 2, y=yc + 2.25, s=s, b=0, paths=[], glyphs=len(c)))
    syms = [dict(x=g['x0'], y=g['yc'] + 2.25) for g in unk]
    return items, syms


glyph_fp = collections.defaultdict(collections.Counter)   # code -> fingerprint counts
glyph_ex = {}
SYMS = []
SKIPPED = []
DBG = []                                              # (code, fp) -> (br, page, x, y) example for raster confirmation


def unesc(s):
    return s.replace('\\(', '(').replace('\\)', ')').replace('`', ' ').strip()


def parse_rows(br, unit, pg, cells, mm, grp, colof, table_suffix, rows_out, issues):
    ys = sorted(set(round(t['y']) for t in cells))
    rowy = []
    for y in ys:
        if rowy and y - rowy[-1][-1] <= 2: rowy[-1].append(y)
        else: rowy.append([y])
    section = 'deflection'
    prev_letter = ''
    for ry in rowy:
        yc = sum(ry) / len(ry)
        m_ = [t for t in mm if abs(t['y'] - yc) <= 2.5]
        if m_:
            kind = 'allowable_' + m_[0]['s'].strip().rstrip('.').lower()
            cand = sorted([t for t in grp if abs(t['y'] - yc) <= 7.5], key=lambda t: abs(t['y'] - yc))
            # group label is half a row below MIN / above MAX (or level with it)
            cand = [t for t in cand if (t['y'] > yc - 2.5 if kind.endswith('min') else t['y'] < yc + 2.5)] or cand
            g = unesc(cand[0]['s']) if cand else '?'
            section = 'allowable'
        else:
            g_ = [t for t in grp if abs(t['y'] - yc) <= 2.5]
            if not g_:
                SKIPPED.append((br, unit, pg, round(yc), ' '.join(c['s'] for c in cells if round(c['y']) in ry)[:60]))
                continue                      # no row label: not a table row (title block / notes)
            g = unesc(g_[0]['s'])
            single = re.fullmatch(r'[A-K]', g) is not None
            if section == 'deflection' and (not single or (prev_letter and g <= prev_letter)):
                section = 'cross_slope'
            prev_letter = g if single else prev_letter
            kind = 'slab_deflection' if section == 'deflection' else 'cross_slope_adjustment'
        for t in [c for c in cells if round(c['y']) in ry]:
            col = colof(t)
            if col is None:
                issues.append(f'{br} U{unit} p{pg}: cell {t["s"]!r} at x={t["x"]:.0f} y={t["y"]:.0f} has no column')
                continue
            cv = cell_value(t['s'])
            if cv is None:
                continue                      # '-' = not printed
            v, code, txt = cv
            rows_out.append(dict(bridge=br, unit=unit, page=pg, table=kind + table_suffix, group=g, point=col, value_in=round(v, 4), printed=txt,
                                 tag='VECTOR'))


def parse_misc(br, unit, pg):
    d = fitz.open(PDF[br])
    page = d[pg - 1]
    texts = items_with_glyphs(page)
    heads = header_rows(texts)
    rows_out, issues = [], []
    nsym = [0]
    disc = [t for t in texts if 'FOR DISCONTINUOUS GIRDERS' in t['s'] and 'UNIT' in t['s']]
    dy = disc[0]['y'] if disc else 1e9
    for hi, (hy, hts) in enumerate(heads):
        ylim = heads[hi + 1][0] - 25 if hi + 1 < len(heads) else 700
        if dy > hy: ylim = min(ylim, dy - 5)
        cols = [(t['x'] + 1.5 * len(t['s']), int(t['s'])) for t in hts]   # header centre
        x0 = min(c[0] for c in cols) - 15
        lab = [t for t in texts if hy + 3 < t['y'] < ylim and 100 < t['x'] < x0 and t['b'] == 0]
        mm = [t for t in lab if t['s'].strip() in ('MIN.', 'MAX.')]
        grp = [t for t in lab if t['x'] >= 140]
        cells, syms = glyph_cells(page, x0, 1224, hy + 4, ylim)
        nsym[0] += len(syms)
        def colof(t, cols=cols):
            cx = t['x'] + 3
            c = min(cols, key=lambda c: abs(c[0] - cx))
            return c[1] if abs(c[0] - cx) <= 14 else None
        parse_rows(br, unit, pg, cells, mm, grp, colof, '', rows_out, issues)
    for dt in disc:
        # discontinuous-girder table: no point numbers; columns = CL pier, then the girder's own points (order index 1..n)
        y0, y1 = dt['y'] + 8, dt['y'] + 190
        reg = [t for t in texts if y0 < t['y'] < y1 and dt['x'] - 60 < t['x'] < dt['x'] + 700 and t['b'] == 0]
        letters = [t for t in reg if re.fullmatch(r'[A-K]', t['s'].strip())]
        lx = collections.Counter(round(t['x']) for t in letters).most_common(1)[0][0]
        grp = [t for t in letters if abs(t['x'] - lx) <= 3]
        mm = [t for t in reg if t['s'].strip() in ('MIN.', 'MAX.')]
        cells, syms = glyph_cells(page, lx + 20, dt['x'] + 700, y0, y1)
        nsym[0] += len(syms)
        xs = sorted(t['xc'] for t in cells)
        cl = []
        for x in xs:
            if cl and x - cl[-1][-1] <= 3: cl[-1].append(x)
            else: cl.append([x])
        cl = [c for c in cl if len(c) >= 3]           # a real column has a value (or '-') in most rows
        centres = [sum(c) / len(c) for c in cl]
        if len(centres) > 2:                          # stop at the first gap wider than 1.5 pitches (title block glyphs beyond the table)
            pitch = sorted(b - a for a, b in zip(centres, centres[1:]))[len(centres) // 2 - 1]
            for i in range(1, len(centres)):
                if centres[i] - centres[i - 1] > 1.5 * pitch: centres = centres[:i]; break
        DBG.append((pg, [round(c) for c in centres]))
        def colof(t, centres=centres):
            i = min(range(len(centres)), key=lambda i: abs(centres[i] - t['xc']))
            return i + 1 if abs(centres[i] - t['xc']) <= 10 else None
        parse_rows(br, unit, pg, cells, mm, grp, colof, '_discontinuous', rows_out, issues)
    SYMS.append((br, unit, pg, nsym[0]))
    d.close()
    return rows_out, issues


def parse_helev(br, unit, pg):
    d = fitz.open(PDF[br])
    page = d[pg - 1]
    texts = items_with_glyphs(page)
    d.close()
    heads = [h for h in header_rows(texts)]
    # a block with one point only (e.g. Ramp G U3 point 113): the number on the 'POINT' label line
    for pl in [t for t in texts if t['s'].strip() == 'POINT']:
        if any(abs(hy - pl['y']) <= 6 for hy, _ in heads): continue
        lone = [t for t in texts if abs(t['y'] - pl['y']) <= 4 and re.fullmatch(r'\d{1,3}', t['s'].strip()) and t['x'] > pl['x']]
        if lone: heads.append((round(lone[0]['y']), lone))
    heads.sort(key=lambda h: h[0])
    out, issues = [], []
    for hi, (hy, hts) in enumerate(heads):
        ylim = heads[hi + 1][0] - 10 if hi + 1 < len(heads) else 700
        cols = [(t['x'] + 1.5 * len(t['s']), int(t['s'])) for t in hts]
        labs = [t for t in texts if hy + 3 < t['y'] < ylim and t['x'] < 150 and re.search(r'(^|LINE )[A-K]$', unesc(t['s']))
                and any(abs(u['y'] - t['y']) <= 2 and 'GIRDER' in u['s'] for u in texts)]
        for lb in labs:
            g = unesc(lb['s'])[-1]
            for t in texts:
                if abs(t['y'] - lb['y']) <= 2.5 and re.fullmatch(r'\d{3,4}\.\d\d', t['s'].strip()):
                    cx = t['x'] + 10
                    col = min(cols, key=lambda c: abs(c[0] - cx))
                    if abs(col[0] - cx) > 16:
                        continue   # tail table of a discontinuous girder (no point numbers; see camber/*-unnumbered.csv)
                    out.append(dict(bridge=br, unit=unit, page=pg, girder=g, point=col[1], haunch_elev_ft=t['s'].strip(), tag='TEXT'))
    return out, issues


if __name__ == '__main__':
    if len(sys.argv) < 4:
        sys.exit('usage: haunch_extract.py <Design 116 PDF> <Design 316 PDF> <output dir> [<glyph examples .txt>]')
    PDF['I80'], PDF['RG'], OUT = sys.argv[1], sys.argv[2], sys.argv[3]
    GLYPHS = sys.argv[4] if len(sys.argv) > 4 else None
    os.makedirs(OUT, exist_ok=True)
    allissues = []
    for key, pages in MISC.items():
        rows = []
        for pg in pages:
            r, iss = parse_misc(*key, pg); rows += r; allissues += iss
        fn = os.path.join(OUT, f'{key[0]}-unit{key[1]}-misc_data.csv')
        with open(fn, 'w', newline='') as f:
            w = csv.DictWriter(f, fieldnames=list(rows[0].keys())); w.writeheader(); w.writerows(rows)
        c = collections.Counter((r['table']) for r in rows)
        print(fn, len(rows), dict(c))
    for key, pages in HELEV.items():
        rows = []
        for pg in pages:
            r, iss = parse_helev(*key, pg); rows += r; allissues += iss
        fn = os.path.join(OUT, f'{key[0]}-unit{key[1]}-girder_line_haunch_elev.csv')
        with open(fn, 'w', newline='') as f:
            w = csv.DictWriter(f, fieldnames=list(rows[0].keys())); w.writeheader(); w.writerows(rows)
        print(fn, len(rows))
    print('GLYPH fingerprints per code:')
    for code in sorted(glyph_fp):
        print(' ', code, FRS[code], dict(glyph_fp[code]))
    if GLYPHS:                                        # one example location per (code, shape) for raster confirmation
        with open(GLYPHS, 'w') as f:
            for (code, fp), ex in sorted(glyph_ex.items()):
                f.write('\t'.join(map(str, (code, FRS[code], fp, ex))) + '\n')
    print('symbol glyphs (see-other-table marks) per page:', SYMS)
    print('rows without a label (skipped):', len(SKIPPED))
    for s in SKIPPED: print('   skip', s)
    print('ISSUES', len(allissues))
    for i in allissues[:60]: print(' ', i)
