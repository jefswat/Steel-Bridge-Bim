# Vector-font decoder for the Iowa DOT plan sheets: every character is drawn as a stroked path block
# ("q 1 0 0 1 x y cm ... Q"); the text layer only names SOME of them (identical cells are drawn as path copies
# without a text item). Glyph shapes are learned from text items whose following paths spell their characters,
# then every path block on a page is classified by its shape key.
import re, collections, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import plantext

PT = re.compile(r'(-?[\d.]+) (-?[\d.]+) ([ml])')


def shape(body):
    pts = [(float(a), float(b), op) for a, b, op in PT.findall(body)]
    if not pts:
        return None, None
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    nm = sum(1 for p in pts if p[2] == 'm')
    key = (nm, len(pts), round(max(xs) - min(xs), 0), round(max(ys) - min(ys), 0))
    return key, (min(xs), max(xs), min(ys), max(ys))


def chars_of(s):
    s = s.replace('\x01', '\\201')
    out = []
    i = 0
    while i < len(s):
        if s[i] == '\\' and i + 3 < len(s) + 0 and re.match(r'2[01][0-7]', s[i + 1:i + 4]):
            out.append('F' + str(int(s[i + 1:i + 4], 8) - 0o200)); i += 4
        elif s[i] == '\\' and i + 1 < len(s) and s[i + 1] in '()\\':
            out.append(s[i + 1]); i += 2
        elif s[i] in ' `':
            i += 1
        else:
            out.append(s[i]); i += 1
    return out


def learn(pages):
    """pages: iterable of pymupdf pages -> {shape key: Counter(char)}"""
    lex = collections.defaultdict(collections.Counter)
    for page in pages:
        its = plantext.parse_page(page)
        for i, it in enumerate(its):
            if it['t'] != 'text' or it['b'] != 0:
                continue
            ch = chars_of(it['s'])
            paths = []
            j = i + 1
            while j < len(its) and its[j]['t'] == 'path':
                paths.append(its[j]); j += 1
            if len(paths) < len(ch):
                continue
            for c, p in zip(ch, paths):
                k, _ = shape(p['body'])
                if k: lex[k][c] += 1
    return lex


def glyphs(page, lex):
    """every path block on the page classified: list of dict(ch, x0, x1, yc) in page coords (y down)"""
    out = []
    for it in plantext.parse_page(page):
        if it['t'] != 'path':
            continue
        k, bb = shape(it['body'])
        if not k:
            continue
        x0, x1 = it['x'] + bb[0], it['x'] + bb[1]
        yc = it['y'] - (bb[2] + bb[3]) / 2
        h = bb[3] - bb[2]
        ch = lex[k].most_common(1)[0][0] if k in lex else None
        out.append(dict(ch=ch, key=k, x0=x0, x1=x1, xc=(x0 + x1) / 2, yc=yc, w=bb[1] - bb[0], h=h, ox=it['x'], oy=it['y']))
    return out


CELLCH = set('0123456789-') | {f'F{i}' for i in range(1, 16)}


def pts_of(body):
    return [(float(a), float(b), op) for a, b, op in PT.findall(body)]


def learn_protos(pages):
    """per (nm, npts): list of (char, points) prototypes for table-cell characters, from text items whose paths spell them."""
    protos = collections.defaultdict(list)
    for page in pages:
        its = plantext.parse_page(page)
        for i, it in enumerate(its):
            if it['t'] != 'text' or it['b'] != 0:
                continue
            ch = chars_of(it['s'])
            if not ch or not all(c in CELLCH for c in ch):
                continue
            paths = []
            j = i + 1
            while j < len(its) and its[j]['t'] == 'path':
                paths.append(its[j]); j += 1
            if len(paths) < len(ch):
                continue
            for c, p in zip(ch, paths):
                P = pts_of(p['body'])
                if not P: continue
                k = (sum(1 for q in P if q[2] == 'm'), len(P))
                if any(dist(P, Q) <= 0.3 and cc == c for cc, Q in protos[k]):
                    continue
                protos[k].append((c, P))
    return protos


def dist(P, Q):
    if len(P) != len(Q): return 1e9
    return max(max(abs(a[0] - b[0]), abs(a[1] - b[1])) for a, b in zip(P, Q))


def classify(body, protos, tol=0.45):
    P = pts_of(body)
    if not P: return None, None
    k = (sum(1 for q in P if q[2] == 'm'), len(P))
    best = None
    for c, Q in protos.get(k, []):
        d = dist(P, Q)
        if d <= tol and (best is None or d < best[1]): best = (c, d)
    # conflicting prototypes (two chars within tol) -> ambiguous
    if best:
        others = [c for c, Q in protos.get(k, []) if c != best[0] and dist(P, Q) <= tol]
        if others: return '?', P
        return best[0], P
    return None, P


def cell_glyphs(page, protos):
    out = []
    for it in plantext.parse_page(page):
        if it['t'] != 'path':
            continue
        c, P = classify(it['body'], protos)
        if P is None: continue
        xs = [q[0] for q in P]; ys = [q[1] for q in P]
        x0, x1 = it['x'] + min(xs), it['x'] + max(xs)
        yc = it['y'] - (min(ys) + max(ys)) / 2
        out.append(dict(ch=c, x0=x0, x1=x1, xc=(x0 + x1) / 2, yc=yc, w=max(xs) - min(xs), h=max(ys) - min(ys)))
    return out
