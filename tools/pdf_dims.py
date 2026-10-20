"""pdf_dims.py - generic dimension / fraction reader for bridge plan PDFs.

README
------
Reads every dimension-like string on one page of a plan PDF and decodes its value, INCLUDING the stacked / diagonal
fraction glyphs that plain PDF text extraction loses (1'-6 3/4" comes out as "1'-6" + a stray glyph).

    python -I tools/pdf_dims.py <pdf> --page N [--clip x0,y0,x1,y1] [--json out.json] [--png overlay.png]
                                [--near x,y [--radius R]] [--words REGEX] [--kind feet-inch,station,...]
                                [--leaders] [--text] [--dpi 150]
    python -I tools/pdf_dims.py --selftest [--truth tools/pdf_dims_truth.json] [--verbose]

Coordinates are PDF page points, origin top-left, y down (PyMuPDF convention), same as tools/words_near.py.
One record per dimension: kind, text (canonical, e.g. 12'-6 1/2"), value (inches for lengths, degrees for angles, number
otherwise), unit, bbox, rot (text direction in degrees, 0 = horizontal, 90 = reads bottom-to-top), size (text height pt),
conf (0..1), src (how the characters were read), parts (for counts "6 SPA. @ 22'-6 = 135'-0"" and plates).

Kinds: feet-inch, inch, decimal, station, elevation, angle, rebar (5c5, #6), plate (PL 1 x 12, 3/8" x 4'-3"), count,
fraction (bare 3/4), radius.

How the characters are read (src):
  glyph   CAD-plotted text (stroke fonts plotted as vector paths, hidden text layer, very common for MicroStation /
          AutoCAD plots).  The content stream is parsed (path blocks paired with text), the fraction glyph (one font code
          per fraction, drawn as numerator / denominator strokes with a slash or just stacked) is decoded from its SHAPE
          (digit templates learned from the same PDF) and cross-checked against the font code table.
  text    real PDF text layer: unicode fractions, "1/2" slash form, stacked small numerator / denominator characters.
  vector  text drawn as filled outlines with no text layer (e.g. the HNTB / TxDOT NHT plans).  Each glyph is classified by
          shape against tools/pdf_dims_glyphs.json (hand-labelled glyph shapes of that outline font; horizontal and +/-90
          degree text only; periods, hyphens and foot / inch ticks by geometry; small raised / lowered digits are stacked
          into fractions).  For any other font the library does not apply and nothing reliable comes out: add glyphs to the
          json (label clusters of one page, see the buildlib recipe in the git history of this script) or OCR the region.
          Tilted (not 0 / 90 / 270 degree) text is NOT read: such fragments are garbage and flagged with low confidence.
  Raster scans (page is one image, no text, no vector glyphs): not readable here; reported as "raster/vector text, OCR needed".

Fraction decoding detail (glyph mode): the plotted fraction glyph is one font code per fraction (0x81..0x8f here); its strokes
are split into numerator / denominator digit clusters (by the slash line or by the vertical gap), each cluster is matched
against fraction-digit shapes learned from the same PDF (bootstrapped from the code table, shapes of neighbouring pages are used
too), and the result must be a valid fraction (denominator 2..64, power of two, numerator < denominator); the code table value
is the fallback and the cross-check (disagreement lowers conf to 0.6).  Counts are validated arithmetically
("6 SPA. @ 22'-6 = 135'-0": parts.check = ok / MISMATCH).

Other conventions: the plotted PL symbol (font code '{') reads as "PL "; the drawing scale ("SCALE: 3/4" = 1'-0"") is its own
kind (scale); bare "11/16" without an inch mark (title block dates!) gets conf 0.6; plots often draw every text twice, exact
copies are dropped.  Selftest ground truth: tools/pdf_dims_truth.json (see docs/QC-PLAN.md section 3).

--leaders links each dimension text to its dimension line (parallel segment next to the text) and the two measured end
points (nearest extension lines on either side of the text, else the ends of the dimension line) in page coordinates, plus the
view scale when "SCALE: ..." is printed and measured_over_printed (1.0 = drawn to the printed scale, 0.5 = half-size plot).
Limits: dimension text placed outside its arrows picks the wrong line; dimensions in a stacked row can pick a neighbour's line.
"""
import re, sys, os, math, json, argparse, collections, itertools
import pymupdf as fitz

HERE = os.path.dirname(os.path.abspath(__file__))

# ----------------------------------------------------------------------------------------------------------------
# content stream parser
# ----------------------------------------------------------------------------------------------------------------
TOK = re.compile(rb'\((?:\\.|[^\\()]|\((?:\\.|[^\\()])*\))*\)|<<|>>|\[|\]|<[0-9A-Fa-f\s]*>|/[^\s/\[\]()<>]*|[-+]?(?:\d+\.?\d*|\.\d+)|[A-Za-z\'"*]+', re.S)


def mmul(a, b):
    return (a[0]*b[0] + a[1]*b[2], a[0]*b[1] + a[1]*b[3],
            a[2]*b[0] + a[3]*b[2], a[2]*b[1] + a[3]*b[3],
            a[4]*b[0] + a[5]*b[2] + b[4], a[4]*b[1] + a[5]*b[3] + b[5])


def unstring(b):
    s = b[1:-1]
    out = bytearray()
    i = 0
    while i < len(s):
        c = s[i]
        if c == 0x5c and i + 1 < len(s):
            n = s[i+1]
            if 0x30 <= n <= 0x37:
                j = i + 1; v = 0; k = 0
                while j < len(s) and k < 3 and 0x30 <= s[j] <= 0x37:
                    v = v*8 + s[j] - 0x30; j += 1; k += 1
                out.append(v & 255); i = j; continue
            out.append({0x6e: 10, 0x72: 13, 0x74: 9, 0x62: 8, 0x66: 12}.get(n, n)); i += 2; continue
        out.append(c); i += 1
    return bytes(out)


def bez(p0, p1, p2, p3, n=6):
    pts = []
    for i in range(1, n + 1):
        t = i / n; u = 1 - t
        pts.append((u**3*p0[0] + 3*u*u*t*p1[0] + 3*u*t*t*p2[0] + t**3*p3[0],
                    u**3*p0[1] + 3*u*u*t*p1[1] + 3*u*t*t*p2[1] + t**3*p3[1]))
    return pts


def parse_stream(page):
    """Ordered events: ('T', dict(raw, tm, size)) text shows; ('P', dict(subs, kind, w)) painted paths.
    Coordinates are page space (top-left origin, y down, points)."""
    c = page.read_contents()
    M = page.transformation_matrix
    ctm = (M.a, M.b, M.c, M.d, M.e, M.f)
    stack = []
    tm = (1, 0, 0, 1, 0, 0); tlm = tm
    fs = 1.0; tl = 0.0; lw = 1.0
    ops = []; ev = []
    subs = []; cur = None; clipflag = False
    for m in TOK.finditer(c):
        t = m.group(0)
        ch = t[:1]
        if ch in b'([/<' or ch in b'+-.0123456789':
            if t == b'[':
                ops.append('[')
            elif t == b']':
                j = len(ops) - 1
                while j >= 0 and ops[j] != '[': j -= 1
                arr = ops[j+1:]; del ops[j:]; ops.append(arr)
            elif t in (b'<<', b'>>'):
                pass
            elif ch == b'(':
                ops.append(unstring(t))
            elif ch == b'/':
                ops.append(t)
            elif ch == b'<':
                ops.append(b'')
            else:
                ops.append(float(t))
            continue
        op = t.decode('latin-1')
        a = ops
        try:
            if op == 'q':
                stack.append((ctm, fs, tl, lw))
            elif op == 'Q':
                if stack: ctm, fs, tl, lw = stack.pop()
            elif op == 'cm':
                ctm = mmul(tuple(a[-6:]), ctm)
            elif op == 'w':
                lw = a[-1]
            elif op == 'BT':
                tm = (1, 0, 0, 1, 0, 0); tlm = tm
            elif op == 'Tf':
                fs = a[-1]
            elif op == 'TL':
                tl = a[-1]
            elif op == 'Tm':
                tm = tuple(a[-6:]); tlm = tm
            elif op == 'Td':
                tlm = mmul((1, 0, 0, 1, a[-2], a[-1]), tlm); tm = tlm
            elif op == 'TD':
                tl = -a[-1]; tlm = mmul((1, 0, 0, 1, a[-2], a[-1]), tlm); tm = tlm
            elif op == 'T*':
                tlm = mmul((1, 0, 0, 1, 0, -tl), tlm); tm = tlm
            elif op in ('Tj', "'", '"', 'TJ'):
                raw = b''.join(x for x in a[-1] if isinstance(x, bytes)) if op == 'TJ' else a[-1]
                ev.append(('T', {'raw': raw, 'tm': mmul(tm, ctm), 'size': fs}))
            elif op == 'm':
                if cur: subs.append(cur)
                cur = [(a[-2], a[-1])]
            elif op == 'l':
                if cur is None: cur = [(a[-2], a[-1])]
                else: cur.append((a[-2], a[-1]))
            elif op == 'c':
                p0 = cur[-1]
                cur.extend(bez(p0, (a[-6], a[-5]), (a[-4], a[-3]), (a[-2], a[-1])))
            elif op == 'v':
                p0 = cur[-1]; cur.extend(bez(p0, p0, (a[-4], a[-3]), (a[-2], a[-1])))
            elif op == 'y':
                p0 = cur[-1]; cur.extend(bez(p0, (a[-4], a[-3]), (a[-2], a[-2 + 1]), (a[-2], a[-1])))
            elif op == 're':
                if cur: subs.append(cur)
                x, y, w, h = a[-4:]
                cur = [(x, y), (x + w, y), (x + w, y + h), (x, y + h), (x, y)]
            elif op == 'h':
                if cur: cur.append(cur[0])
            elif op in ('W', 'W*'):
                clipflag = True
            elif op in ('S', 's', 'f', 'F', 'f*', 'B', 'B*', 'b', 'b*', 'n'):
                if cur: subs.append(cur)
                if subs and not clipflag and op != 'n':
                    kind = 'S' if op in ('S', 's') else 'f'
                    sp = [[(ctm[0]*x + ctm[2]*y + ctm[4], ctm[1]*x + ctm[3]*y + ctm[5]) for x, y in s] for s in subs]
                    sc = math.sqrt(abs(ctm[0]*ctm[3] - ctm[1]*ctm[2]))
                    ev.append(('P', {'subs': sp, 'kind': kind, 'w': lw * sc, 'org': (ctm[4], ctm[5])}))
                subs = []; cur = None; clipflag = False
        except (IndexError, TypeError, ValueError):
            pass
        ops = []
    return ev


def glyph_pairs(ev):
    """Pair every drawn character of text runs with the path block drawn for it.
    Returns list of dict(byte, t(text event), p(path event), sp(explicit space before), idx, obj)."""
    out = []
    i = 0
    obj = 0
    while i < len(ev):
        if ev[i][0] != 'T':
            i += 1; continue
        ts = []
        while i < len(ev) and ev[i][0] == 'T':
            ts.append(ev[i][1]); i += 1
        ps = []
        while i < len(ev) and ev[i][0] == 'P':
            ps.append(ev[i][1]); i += 1
        chars = []
        sp = False
        for t in ts:
            for b in t['raw']:
                if b in (32, 96):
                    sp = True; continue
                chars.append((b, t, sp)); sp = False
        if not chars or len(chars) > len(ps):
            continue
        for k, ((b, t, sp), p) in enumerate(zip(chars, ps)):
            out.append({'byte': b, 't': t, 'p': p, 'sp': sp, 'obj': obj})
        obj += 1
    return out


# ----------------------------------------------------------------------------------------------------------------
# geometry helpers / glyph shapes
# ----------------------------------------------------------------------------------------------------------------
def frame_of(t):
    a, b, c, d = t['tm'][:4]
    la = math.hypot(a, b) or 1.0; lc = math.hypot(c, d) or 1.0
    return (a / la, b / la), (c / lc, d / lc), lc * t['size']   # direction, up, glyph height (pt)


def to_upright(subs, du, up):
    return [[(x*du[0] + y*du[1], x*up[0] + y*up[1]) for x, y in s] for s in subs]


def bbox_of(subs):
    xs = [p[0] for s in subs for p in s]; ys = [p[1] for s in subs for p in s]
    return min(xs), min(ys), max(xs), max(ys)


def sample(subs, step):
    """equally spaced points along all polylines (also vertices)."""
    pts = []
    for s in subs:
        if len(s) == 1:
            pts.append(s[0]); continue
        for (x0, y0), (x1, y1) in zip(s, s[1:]):
            L = math.hypot(x1 - x0, y1 - y0)
            n = max(1, int(L / step))
            for k in range(n):
                pts.append((x0 + (x1 - x0) * k / n, y0 + (y1 - y0) * k / n))
        pts.append(s[-1])
    return pts


def norm_shape(subs, h=None):
    """Normalise to unit height, centred horizontally.  Returns (points, aspect)."""
    x0, y0, x1, y1 = bbox_of(subs)
    H = (y1 - y0) if h is None else h
    if H <= 1e-9:
        H = max(x1 - x0, 1e-6)
    ns = [[((x - (x0 + x1) / 2) / H, (y - y0) / H) for x, y in s] for s in subs]
    return sample(ns, 0.06), (x1 - x0) / H


try:
    import numpy as np
except Exception:          # numpy is optional (speed only)
    np = None


def chamfer(A, B):
    if len(A) == 0 or len(B) == 0:
        return 9.0
    if np is not None:
        a = np.asarray(A); b = np.asarray(B)
        d2 = ((a[:, None, :] - b[None, :, :]) ** 2).sum(-1)
        return float(np.sqrt(d2.min(1).mean()) + np.sqrt(d2.min(0).mean()))
    sa = 0.0
    for p in A:
        sa += min((p[0]-q[0])**2 + (p[1]-q[1])**2 for q in B)
    sb = 0.0
    for q in B:
        sb += min((p[0]-q[0])**2 + (p[1]-q[1])**2 for p in A)
    return math.sqrt(sa / len(A)) + math.sqrt(sb / len(B))


class Templates:
    """Shape library: label -> list of (points, aspect, hash)."""
    def __init__(self):
        self.t = collections.defaultdict(list)

    def add(self, label, subs, tag=None):
        pts, asp = norm_shape(subs)
        for (q, a, tg) in self.t[label]:
            if tg == tag and tag is not None:
                return
            if abs(a - asp) < 0.04 and chamfer(pts, q) < 0.04:
                return
        self.t[label].append((np.asarray(pts) if np is not None else pts, asp, tag))

    def classify(self, subs, labels=None, exclude_tag=None):
        pts, asp = norm_shape(subs)
        mk = (tuple((round(x, 2), round(y, 2)) for x, y in pts), None if labels is None else tuple(sorted(labels)), exclude_tag)
        memo = self.__dict__.setdefault('_memo', {})
        if mk in memo and memo[mk][1] == len(self.t) + sum(len(v) for v in self.t.values()):
            return memo[mk][0]
        best = []
        pa = np.asarray(pts) if np is not None else pts
        for lab, lst in self.t.items():
            if labels is not None and lab not in labels:
                continue
            d = 9.0
            for q, a, tg in lst:
                if exclude_tag is not None and tg == exclude_tag:
                    continue
                if abs(a - asp) > 0.3:
                    continue
                dd = chamfer(pa, q) + 0.5 * abs(a - asp)
                if dd < d: d = dd
            best.append((d, lab))
        best.sort()
        memo[mk] = (best, len(self.t) + sum(len(v) for v in self.t.values()))
        return best


# ----------------------------------------------------------------------------------------------------------------
# fraction glyph decoding (stroke glyph blocks)
# ----------------------------------------------------------------------------------------------------------------
# font code table (MicroStation / AutoCAD fraction font as used by the sampled plan sets); verified against shapes
STD_FRAC = {0x81: (1, 2), 0x82: (1, 4), 0x83: (3, 4), 0x84: (1, 8), 0x85: (3, 8), 0x86: (5, 8), 0x87: (7, 8),
            0x88: (1, 16), 0x89: (3, 16), 0x8a: (5, 16), 0x8b: (7, 16), 0x8c: (9, 16), 0x8d: (11, 16),
            0x8e: (13, 16), 0x8f: (15, 16)}
VALID_DEN = (2, 4, 8, 16, 32, 64)


def seg_clusters(subs, tol):
    """merge subpaths whose bounding boxes touch (within tol) into glyph clusters."""
    boxes = [bbox_of([s]) for s in subs]
    parent = list(range(len(subs)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]; i = parent[i]
        return i
    for i in range(len(subs)):
        for j in range(i + 1, len(subs)):
            a, b = boxes[i], boxes[j]
            if a[0] - tol <= b[2] and b[0] - tol <= a[2] and a[1] - tol <= b[3] and b[1] - tol <= a[3]:
                parent[find(i)] = find(j)
    groups = collections.defaultdict(list)
    for i in range(len(subs)):
        groups[find(i)].append(subs[i])
    return list(groups.values())


def split_fraction(subs):
    """Split an upright fraction glyph block into (numerator clusters, denominator clusters, kind).
    kind: 'slash' | 'stack' | None (could not split)."""
    x0, y0, x1, y1 = bbox_of(subs)
    H = y1 - y0
    if H <= 0:
        return None, None, None
    slash = None; bar = None
    rest = []
    for s in subs:
        if len(s) == 2:
            dx = s[1][0] - s[0][0]; dy = s[1][1] - s[0][1]
            if abs(dy) >= 0.8 * H and abs(dx) >= 0.2 * abs(dy) and dx * dy > 0 and slash is None:
                slash = s; continue
            if abs(dy) < 0.06 * H and abs(dx) > 0.35 * H and bar is None and (len(subs) >= 4):
                bar = s; continue
        rest.append(s)
    cl = seg_clusters(rest, 0.05 * H)
    if slash is not None:
        (sx0, sy0), (sx1, sy1) = slash
        if sy1 < sy0:
            sx0, sy0, sx1, sy1 = sx1, sy1, sx0, sy0
        num = []; den = []
        for c in cl:
            b = bbox_of(c)
            cx = (b[0] + b[2]) / 2; cy = (b[1] + b[3]) / 2
            cross = (sx1 - sx0) * (cy - sy0) - (sy1 - sy0) * (cx - sx0)
            (num if cross > 0 else den).append(c)
        kind = 'slash'
    else:
        if len(cl) < 2:
            return None, None, None
        cl.sort(key=lambda c: (bbox_of(c)[1] + bbox_of(c)[3]) / 2)
        cy = [(bbox_of(c)[1] + bbox_of(c)[3]) / 2 for c in cl]
        # largest gap in vertical centres splits rows
        gaps = [(cy[i+1] - cy[i], i) for i in range(len(cy) - 1)]
        g, k = max(gaps)
        if g < 0.25 * H:
            return None, None, None
        den = cl[:k+1]; num = cl[k+1:]
        kind = 'stack'
    key = lambda c: bbox_of(c)[0]
    num.sort(key=key); den.sort(key=key)
    return num, den, kind


def valid_frac(n, d):
    return d in VALID_DEN and 0 < n < d


# ----------------------------------------------------------------------------------------------------------------
# atoms
# ----------------------------------------------------------------------------------------------------------------
class Atom:
    __slots__ = ('ch', 'x0', 'y0', 'x1', 'y1', 'du', 'up', 'size', 'sp', 'src', 'conf', 'obj', 'base', 'info')

    def __init__(self, ch, bbox, du, up, size, sp=False, src='glyph', conf=1.0, obj=0, base=None, info=None):
        self.ch = ch; self.x0, self.y0, self.x1, self.y1 = bbox
        self.du = du; self.up = up; self.size = size; self.sp = sp; self.src = src; self.conf = conf; self.obj = obj
        self.base = base; self.info = info


def build_templates(pages_pairs):
    """Fraction digit templates bootstrapped from the font code table (each fraction block with a known code and a
    matching count of digit clusters labels its clusters)."""
    T = Templates()
    stats = collections.Counter()
    for gp in pages_pairs:
        for g in gp:
            b = g['byte']
            if b not in STD_FRAC:
                continue
            du, up, h = frame_of(g['t'])
            subs = to_upright(g['p']['subs'], du, up)
            num, den, kind = split_fraction(subs)
            if num is None:
                stats['nosplit'] += 1; continue
            n, d = STD_FRAC[b]
            sn, sd = str(n), str(d)
            if len(num) == len(sn) and len(den) == len(sd):
                for cl, lab in zip(num, sn):
                    T.add(lab, cl, tag=('n', hash(repr([[(round(x, 2), round(y, 2)) for x, y in s] for s in cl]))))
                for cl, lab in zip(den, sd):
                    T.add(lab, cl, tag=('d', hash(repr([[(round(x, 2), round(y, 2)) for x, y in s] for s in cl]))))
                stats['ok'] += 1
            else:
                stats['countmismatch'] += 1
    return T, stats


def decode_fraction(g, T):
    """Return (n, d, conf, how) for a fraction glyph pair."""
    b = g['byte']
    du, up, h = frame_of(g['t'])
    subs = to_upright(g['p']['subs'], du, up)
    num, den, kind = split_fraction(subs)
    shape_val = None
    if num is not None and num and den:
        def read(clusters):
            s = ''; worst = 0.0
            for cl in clusters:
                bl = T.classify(cl, labels=set('0123456789'))
                if not bl:
                    return None, 9
                s += bl[0][1]; worst = max(worst, bl[0][0])
            return s, worst
        ns, w1 = read(num); ds, w2 = read(den)
        if ns and ds:
            try:
                shape_val = (int(ns), int(ds), max(w1, w2))
            except ValueError:
                pass
    code_val = STD_FRAC.get(b)
    if shape_val and valid_frac(shape_val[0], shape_val[1]):
        if code_val and code_val == shape_val[:2]:
            return shape_val[0], shape_val[1], 1.0, 'shape+code'
        if code_val:
            return shape_val[0], shape_val[1], 0.6, 'shape(code disagrees %d/%d)' % code_val
        return shape_val[0], shape_val[1], 0.85 if shape_val[2] < 0.12 else 0.6, 'shape'
    if code_val:
        return code_val[0], code_val[1], 0.8, 'code'
    return None


def atoms_from_stream(page, extra_pages=None, need_templates=True):
    ev = parse_stream(page)
    pairs = glyph_pairs(ev)
    if not pairs:
        return [], ev
    allp = [pairs]
    for pg in (extra_pages or []):
        try:
            allp.append(glyph_pairs(parse_stream(pg)))
        except Exception:
            pass
    T, stats = build_templates(allp) if any(g['byte'] in STD_FRAC for gp in allp for g in gp) else (Templates(), {})
    atoms = []
    for g in pairs:
        b = g['byte']; t = g['t']
        du, up, h = frame_of(t)
        bb = bbox_of(g['p']['subs'])
        base = (t['tm'][4], t['tm'][5])
        if b in STD_FRAC or (b >= 0x80 and b not in (0xb0, 0xb1)):
            r = decode_fraction(g, T) if b in STD_FRAC else None
            if r:
                n, d, conf, how = r
                a = Atom(' %d/%d' % (n, d), bb, du, up, h, g['sp'], 'glyph', conf, g['obj'], base, {'frac': (n, d), 'how': how, 'code': b})
            else:
                a = Atom('?', bb, du, up, h, g['sp'], 'glyph', 0.2, g['obj'], base, {'unknown_code': b})
        elif b == 0xb0:
            a = Atom('°', bb, du, up, h, g['sp'], 'glyph', 1.0, g['obj'], base)
        elif b == 0xb1:
            a = Atom('±', bb, du, up, h, g['sp'], 'glyph', 1.0, g['obj'], base)
        else:
            c = 'PL ' if b == 0x7b else chr(b)       # the plotted PL (plate) symbol is font code '{'
            a = Atom(c, bb, du, up, h, g['sp'], 'glyph', 1.0, g['obj'], base)
        a.info = dict(a.info or {})
        a.info['byte'] = b; a.info['evid'] = g['obj']
        atoms.append(a)
    return atoms, ev


# ---------------------------------------------------------------- generic text-layer atoms (real fonts) -------------
UNI_FRAC = {'½': (1, 2), '¼': (1, 4), '¾': (3, 4), '⅛': (1, 8), '⅜': (3, 8), '⅝': (5, 8),
            '⅞': (7, 8)}


def atoms_from_textlayer(page):
    out = []
    raw = page.get_text('rawdict')
    for b in raw['blocks']:
        for l in b.get('lines', []):
            dx, dy = l['dir']
            du = (dx, dy); up = (dy, -dx)
            for s in l['spans']:
                size = s['size']
                for c in s['chars']:
                    ch = c['c']
                    if ch == ' ':
                        continue
                    bb = c['bbox']
                    if ch in UNI_FRAC:
                        n, d = UNI_FRAC[ch]
                        ch = ' %d/%d' % (n, d)
                        info = {'frac': (n, d), 'how': 'unicode'}
                    else:
                        info = None
                    base = tuple(c['origin'])
                    out.append(Atom(ch, bb, du, up, size, False, 'text', 1.0, id(l), base, info))
    return out


# ----------------------------------------------------------------------------------------------------------------
# line assembly
# ----------------------------------------------------------------------------------------------------------------
def angle_of(du):
    return math.degrees(math.atan2(-du[1], du[0])) % 360    # math convention (y up): 0 horizontal, 90 reads upward


def assemble(atoms, gap_space=0.5, gap_break=2.6):
    """Group atoms into strings (one per text line / annotation).  Returns list of dict(text, atoms, bbox, rot, size)."""
    if not atoms:
        return []
    groups = collections.defaultdict(list)
    for a in atoms:
        groups[int(round(angle_of(a.du) / 3.0)) % 120].append(a)
    out = []
    for key, al in groups.items():
        ang = (key * 3.0) % 360
        angs = sorted(angle_of(a.du) for a in al)
        ang = angs[len(angs) // 2]
        r = math.radians(ang)
        du = (math.cos(r), -math.sin(r)); up = (du[1], -du[0])
        for a in al:
            cs = [(a.x0, a.y0), (a.x1, a.y0), (a.x0, a.y1), (a.x1, a.y1)]
            us = [x*du[0] + y*du[1] for x, y in cs]
            vs = [x*up[0] + y*up[1] for x, y in cs]
            a.info = dict(a.info or {})
            a.info['_u0'] = min(us); a.info['_u1'] = max(us); a.info['_v0'] = min(vs); a.info['_v1'] = max(vs)
            o = a.base
            a.info['_bl'] = (o[0]*up[0] + o[1]*up[1]) if o is not None else a.info['_v0']
        al.sort(key=lambda a: a.info['_bl'])
        rows = []
        for a in al:
            for r_ in rows:
                if abs(a.info['_bl'] - r_['v']) < 0.35 * max(a.size, 1):
                    r_['atoms'].append(a)
                    r_['v'] = sum(x.info['_bl'] for x in r_['atoms']) / len(r_['atoms'])
                    break
            else:
                rows.append({'v': a.info['_bl'], 'atoms': [a]})
        for r_ in rows:
            at = sorted(r_['atoms'], key=lambda a: (a.info['_u0'] + a.info['_u1']))
            cur = [at[0]]; segs = []
            for a in at[1:]:
                gapmax = a.info['_u0'] - max(x.info['_u1'] for x in cur)
                if gapmax > gap_break * max(a.size, 1):
                    segs.append(cur); cur = [a]
                else:
                    cur.append(a)
            segs.append(cur)
            for cur in segs:
                s = ''
                cmap = []
                prev = None
                for a in cur:
                    if prev is not None:
                        gap = a.info['_u0'] - prev.info['_u1']
                        if a.info.get('evid') is not None and a.info.get('evid') == prev.info.get('evid'):
                            sp = False                      # same text run: spaces are explicit characters
                        else:
                            sp = gap > gap_space * max(a.size, 1)
                        if a.sp or sp:
                            s += ' '; cmap.append(None)
                    s += a.ch
                    cmap.extend([a] * len(a.ch))
                    prev = a
                out.append({'text': s, 'cmap': cmap, 'atoms': cur,
                            'bbox': (min(a.x0 for a in cur), min(a.y0 for a in cur), max(a.x1 for a in cur), max(a.y1 for a in cur)),
                            'rot': ang, 'size': sum(a.size for a in cur) / len(cur), 'src': cur[0].src,
                            'conf': min(a.conf for a in cur)})
    return out


# ----------------------------------------------------------------------------------------------------------------
# dimension string parsing
# ----------------------------------------------------------------------------------------------------------------
from fractions import Fraction

QUOTE = r"(?:\"|'')"
FEET_IN = (r"(?<![\d.A-Za-z])(?P<ft>\d{1,5})\s*'(?:\s*-\s*(?P<in>\d{1,3}(?:\.\d+)?)?(?:\s*(?P<n>\d{1,2})\s*/\s*(?P<d>\d{1,2}))?)?(?:\s*" + QUOTE + ")?")
LEN = (r"(?:\d{1,5}\s*'(?:\s*-\s*\d{0,3}(?:\.\d+)?(?:\s*\d{1,2}\s*/\s*\d{1,2})?)?(?:\s*" + QUOTE + r")?"
       r"|\d{1,3}(?:\s+\d{1,2}\s*/\s*\d{1,2})?\s*" + QUOTE + r"|\d+\.\d+\s*(?:'|\"|FT|IN)?)")


def exact_str(v):
    v = Fraction(v)
    return str(v.numerator) if v.denominator == 1 else '%d/%d' % (v.numerator, v.denominator)


def frac_ok(n, d):
    return d in VALID_DEN and 0 < n < d


def _fi(m):
    ft = int(m.group('ft')); ins = m.group('in')
    n = m.group('n'); d = m.group('d')
    v = Fraction(ft * 12)
    txt = "%d'-" % ft
    if ins is not None:
        v += Fraction(ins)
        txt += ins
    else:
        txt += '0'
    if n is not None and d is not None:
        v += Fraction(int(n), int(d))
        txt += ' %d/%d' % (int(n), int(d))
    return v, txt + '"'


def parse_len(s):
    """Parse a standalone length string -> (inches Fraction, canonical text) or None."""
    s = s.strip()
    m = re.fullmatch(FEET_IN, s)
    if m:
        return _fi(m)
    m = re.fullmatch(r"(?:(\d{1,3})\s+)?(\d{1,2})\s*/\s*(\d{1,2})\s*" + QUOTE, s)
    if m:
        w = int(m.group(1)) if m.group(1) else 0
        n, d = int(m.group(2)), int(m.group(3))
        return Fraction(w) + Fraction(n, d), ('%s%d/%d"' % ((str(w) + ' ') if w else '', n, d))
    m = re.fullmatch(r"(\d{1,3}(?:\.\d+)?)\s*" + QUOTE, s)
    if m:
        return Fraction(m.group(1)), '%s"' % m.group(1)
    m = re.fullmatch(r"(\d+\.\d+)\s*(FT|IN|'|\")?", s, re.I)
    if m:
        u = (m.group(2) or '').upper()
        v = Fraction(m.group(1))
        if u in ('FT', "'"):
            return v * 12, m.group(1) + "'"
        return v, m.group(1) + '"'
    return None


def _rec(kind, span, text, value, unit, conf=1.0, parts=None):
    r = {'kind': kind, 'span': span, 'text': text, 'value': value, 'unit': unit, 'conf': conf}
    if parts:
        r['parts'] = parts
    return r


def parse_dims(s):
    """Find every dimension-like item in string s.  Returns list of dict(kind, span, text, value, unit, conf, ...)."""
    out = []
    taken = []

    def free(a, b):
        return all(b <= x or a >= y for x, y in taken)

    def add(r):
        a, b = r['span']
        if free(a, b):
            taken.append((a, b)); out.append(r)
            return True
        return False

    for m in re.finditer(r'\b\d{1,2}/\d{1,2}/\d{2,4}\b', s):      # dates
        taken.append(m.span())

    # ---- count / spacing: N SPA. @ each = total   (also N EQ. SPA. = total)
    cnt = re.compile(r"(?P<cnt>\d{1,3})\s*(?:EQ\.?\s*)?SP(?:A|ACES|ACE|S)?\.?\s*(?:@\s*(?P<each>%s|\d{1,3}(?:\.\d+)?(?![\d'\"/.])))?\s*(?:=\s*(?P<tot>%s))?" % (LEN, LEN), re.I)
    for m in cnt.finditer(s):
        if m.group('each') is None and m.group('tot') is None:
            continue
        n = int(m.group('cnt'))
        parts = {'n': n}
        each = parse_len(m.group('each')) if m.group('each') else None
        if m.group('each') and each is None and re.fullmatch(r'\d{1,3}(?:\.\d+)?', m.group('each')):
            each = (Fraction(m.group('each')), m.group('each') + '"')       # bare number after @ = inches
        tot = parse_len(m.group('tot')) if m.group('tot') else None
        if each:
            parts['each'] = {'text': each[1], 'value': float(each[0]), 'exact': exact_str(each[0])}
        if tot:
            parts['total'] = {'text': tot[1], 'value': float(tot[0]), 'exact': exact_str(tot[0])}
        conf = 1.0
        if each and tot:
            parts['check'] = 'ok' if n * each[0] == tot[0] else 'MISMATCH (n x each = %s in)' % exact_str(n * each[0])
            if n * each[0] != tot[0]:
                conf = 0.6
        txt = '%d%s SPA.' % (n, ' EQ.' if re.search(r'EQ', m.group(0), re.I) else '') + (' @ ' + each[1] if each else '') + (' = ' + tot[1] if tot else '')
        a, b = m.span()
        while b > a and s[b - 1] == ' ':
            b -= 1
        add(_rec('count', (a, b), txt, float(tot[0]) if tot else (float(n * each[0]) if each else None), 'in', conf, parts))

    # ---- drawing scale  SCALE: 3/4" = 1'-0"   (paper length = real length)
    for m in re.finditer(r"SCALE\s*[:=]?\s*(?P<a>(?:\d{1,3}\s+)?\d{1,2}\s*/\s*\d{1,2}\s*" + QUOTE + r"|\d{1,3}(?:\.\d+)?\s*" + QUOTE + r")\s*=\s*(?P<b>\d{1,3}\s*'\s*-?\s*\d{0,2}\s*(?:" + QUOTE + r")?)", s, re.I):
        pa = parse_len(m.group('a')); pb = parse_len(m.group('b'))
        if pa and pb and pb[0] > 0:
            add(_rec('scale', m.span(), 'SCALE ' + pa[1] + ' = ' + pb[1], float(pa[0] / pb[0]), 'ratio', 1.0,
                     {'paper_in': float(pa[0]), 'real_in': float(pb[0]), 'pt_per_real_in': float(pa[0] / pb[0]) * 72}))
    for m in re.finditer(r"SCALE\s*[:=]?\s*(?:N\.?T\.?S\.?|NONE)", s, re.I):
        add(_rec('scale', m.span(), 'SCALE N.T.S.', None, 'ratio', 1.0))

    # ---- angle d m' s"
    for m in re.finditer(r"(\d{1,3})\s*°\s*(?:(\d{1,2})\s*'\s*(?:(\d{1,2}(?:\.\d+)?)\s*" + QUOTE + r")?)?", s):
        d_ = int(m.group(1)); mi = int(m.group(2)) if m.group(2) else 0; se = float(m.group(3)) if m.group(3) else 0.0
        txt = "%d°" % d_ + (("%02d'" % mi + (('%s"' % m.group(3)) if m.group(3) else '')) if m.group(2) else '')
        a, b = m.span()
        while b > a and s[b - 1] == ' ':
            b -= 1
        add(_rec('angle', (a, b), txt, d_ + mi / 60 + se / 3600, 'deg', 1.0, {'d': d_, 'm': mi, 's': se}))

    # ---- station 123+45.67
    for m in re.finditer(r"(?<![\d.])(\d{1,4})\+(\d{2})(\.\d+)?(?!\d)", s):
        v = int(m.group(1)) * 100 + int(m.group(2)) + (float(m.group(3)) if m.group(3) else 0.0)
        add(_rec('station', m.span(), m.group(0), v, 'ft', 1.0))

    # ---- elevation EL. 1125.49
    for m in re.finditer(r"\bEL(?:EV)?\.?\s*[:=]?\s*(-?\d{1,5}\.\d{1,3})", s, re.I):
        add(_rec('elevation', m.span(), 'EL. ' + m.group(1), float(m.group(1)), 'ft', 1.0))

    # ---- plates / shapes: lengths joined by x  (PL 1 x 12, 3/8" x 4'-3", L5x5x3/8)
    lenp = (r"(?:\d{1,5}\s*'(?:\s*-\s*\d{0,3}(?:\.\d+)?(?:\s*\d{1,2}\s*/\s*\d{1,2})?)?(?:\s*" + QUOTE + r")?"
            r"|\d{1,3}\s+\d{1,2}\s*/\s*\d{1,2}\s*(?:" + QUOTE + r")?|\d{1,2}\s*/\s*\d{1,2}\s*(?:" + QUOTE + r")?"
            r"|\d{1,3}(?:\.\d+)?\s*(?:" + QUOTE + r")?)")
    plate = re.compile(r"(?:\b(?P<pre>PL|L|PLATE|FB|MC|HSS|TS|WT|WF|W|C)\s*\.?\s*)?(?P<chain>%s(?:\s*[xX×]\s*%s)+)" % (lenp, lenp))
    for m in plate.finditer(s):
        pieces = [p.strip() for p in re.split(r'[xX×]', m.group('chain'))]
        vals = []
        okk = True
        for p in pieces:
            r = parse_len(p)
            if r is None:
                mm = re.fullmatch(r"(\d{1,3})(?:\s+(\d{1,2})\s*/\s*(\d{1,2}))?|(\d{1,2})\s*/\s*(\d{1,2})|(\d+\.\d+)", p)
                if not mm:
                    okk = False; break
                if mm.group(6):
                    r = (Fraction(mm.group(6)), mm.group(6))
                elif mm.group(4):
                    if not frac_ok(int(mm.group(4)), int(mm.group(5))):
                        okk = False; break
                    r = (Fraction(int(mm.group(4)), int(mm.group(5))), '%s/%s' % (mm.group(4), mm.group(5)))
                else:
                    vv = Fraction(int(mm.group(1))); tx = mm.group(1)
                    if mm.group(2):
                        if not frac_ok(int(mm.group(2)), int(mm.group(3))):
                            okk = False; break
                        vv += Fraction(int(mm.group(2)), int(mm.group(3))); tx += ' %s/%s' % (mm.group(2), mm.group(3))
                    r = (vv, tx)
            vals.append(r)
        if not okk or len(vals) < 2:
            continue
        pre = m.group('pre')
        txt = ((pre + ' ') if pre else '') + ' x '.join(v[1] for v in vals)
        a, b = m.span()
        while b > a and s[b - 1] == ' ':
            b -= 1
        add(_rec('plate', (a, b), txt, None, 'in', 1.0 if pre else 0.8,
                 {'dims_in': [float(v[0]) for v in vals], 'dims': [v[1] for v in vals], 'exact': [exact_str(v[0]) for v in vals]}))
    for m in re.finditer(r"\bPL\.?\s*(\d{1,3}\s+\d{1,2}\s*/\s*\d{1,2}|\d{1,2}\s*/\s*\d{1,2}|\d{1,3}(?:\.\d+)?)\s*(" + QUOTE + r")?(?!\s*[xX\d/])", s):
        thk = re.sub(r'\s+', ' ', re.sub(r'\s*/\s*', '/', m.group(1)))
        v = None
        mm = re.fullmatch(r"(?:(\d+) )?(\d+)/(\d+)", thk)
        if mm:
            if not frac_ok(int(mm.group(2)), int(mm.group(3))):
                continue
            v = Fraction(int(mm.group(1) or 0)) + Fraction(int(mm.group(2)), int(mm.group(3)))
        else:
            v = Fraction(thk)
        add(_rec('plate', m.span(), 'PL ' + thk + ('"' if m.group(2) else ''), None, 'in', 0.9 if m.group(2) else 0.8,
                 {'thickness_in': float(v), 'exact': exact_str(v)}))

    # ---- rebar
    for m in re.finditer(r"#\s*(\d{1,2})\b(?:\s*@\s*(%s))?" % LEN, s):
        parts = {'size': int(m.group(1))}
        txt = '#%s' % m.group(1)
        if m.group(2):
            r = parse_len(m.group(2))
            if r:
                parts['spacing'] = {'text': r[1], 'value': float(r[0])}
                txt += ' @ ' + r[1]
        add(_rec('rebar', m.span(), txt, int(m.group(1)), 'bar', 1.0, parts))
    for m in re.finditer(r"(?<![\w.])(\d{1,2})\s*[cC]\s*(\d{1,3})(?![\w.])", s):
        add(_rec('rebar', m.span(), m.group(0), None, 'bar', 0.9, {'size': int(m.group(1)), 'mark': m.group(2)}))

    # ---- feet-inch
    for m in re.finditer(FEET_IN, s):
        a, b = m.span()
        while b > a and s[b - 1] == ' ':
            b -= 1
        ft = int(m.group('ft'))
        if not m.group('in') and m.group('n') is None and '-' not in m.group(0):
            add(_rec('feet-inch', (a, b), "%d'-0\"" % ft, float(ft * 12), 'in', 0.9, {'exact': str(ft * 12), 'feet_only': True}))
            continue
        v, txt = _fi(m)
        conf = 1.0
        if m.group('n') is not None and not frac_ok(int(m.group('n')), int(m.group('d'))):
            conf = 0.5
        if v - 12 * ft >= 12:
            conf = min(conf, 0.5)
        add(_rec('feet-inch', (a, b), txt, float(v), 'in', conf, {'exact': exact_str(v)}))

    # ---- inches: 2 7/16"  3/4"  1.5"
    for m in re.finditer(r"(?<![\w.'/])(?:(\d{1,3})\s+)?(\d{1,2})\s*/\s*(\d{1,2})\s*(" + QUOTE + r")?(?![\w/])|(?<![\w.'/+])(\d{1,3}(?:\.\d+)?)\s*" + QUOTE, s):
        a, b = m.span()
        while b > a and s[b - 1] == ' ':
            b -= 1
        if m.group(5):
            add(_rec('inch', (a, b), '%s"' % m.group(5), float(m.group(5)), 'in', 1.0, {'exact': exact_str(Fraction(m.group(5)))}))
            continue
        w = int(m.group(1)) if m.group(1) else 0
        n, d = int(m.group(2)), int(m.group(3))
        if not frac_ok(n, d):
            continue
        v = Fraction(w) + Fraction(n, d)
        quote = bool(m.group(4))
        if w:
            add(_rec('inch', (a, b), '%d %d/%d"' % (w, n, d), float(v), 'in', 1.0 if quote else 0.75, {'exact': exact_str(v)}))
        else:
            add(_rec('fraction', (a, b), '%d/%d' % (n, d) + ('"' if quote else ''), float(v), 'in', 1.0 if quote else 0.6,
                     {'exact': exact_str(v), 'num': n, 'den': d}))

    # ---- decimals with / without units
    for m in re.finditer(r"(?:(?<=^)|(?<=[\s(=,]))(?P<sg>[-\u2212])?(?P<num>\d{1,6}\.\d{1,4})\s*(?P<u>FT\.?|'|IN\.?|\"|%)?(?![\w.])", s, re.I):
        u = m.group('u')
        unit = 'ft' if u and (u.upper().startswith('FT') or u == "'") else ('in' if u and (u.upper().startswith('IN') or u == '"') else ('pct' if u == '%' else None))
        v = float(m.group('num')) * (-1 if m.group('sg') else 1)
        add(_rec('decimal', m.span(), ('-' if m.group('sg') else '') + m.group('num') + (u or ''), v * 12 if unit == 'ft' else v, 'in' if unit in ('ft', 'in') else unit,
                 1.0 if unit else 0.7, {'as_printed': ('-' if m.group('sg') else '') + m.group('num'), 'unit_printed': u}))
    # ---- radius: a length directly after "R" / "R=" is a radius
    for r in out:
        if r['kind'] in ('feet-inch', 'decimal'):
            m = re.search(r"(?<![A-Za-z])R\s*=?\s*$", s[:r['span'][0]])
            if m:
                r['span'] = (m.start(), r['span'][1]); r['text'] = 'R=' + r['text']; r['kind'] = 'radius'
    out.sort(key=lambda r: r['span'][0])
    return out


# ----------------------------------------------------------------------------------------------------------------
# page extraction
# ----------------------------------------------------------------------------------------------------------------
def merge_continuations(lines):
    """'3 SPA. @' on one line and '7'-8 = 23'-0' on the next (rotated or not) -> one line."""
    used = set()
    merged = {}
    for i, L in enumerate(lines):
        if i in used or not re.search(r"\bSPA?\.?\s*(@\s*[\d'\"\- /]*)?\s*$", L['text'].rstrip(), re.I) or '=' in L['text']:
            continue
        r = math.radians(L['rot'])
        du = (math.cos(r), -math.sin(r)); up = (du[1], -du[0])

        def frame(X):
            bl = sum((a.base[0]*up[0] + a.base[1]*up[1]) if a.base else (a.x0*up[0] + a.y1*up[1]) for a in X['atoms']) / len(X['atoms'])
            us = [v for a in X['atoms'] for v in (a.x0*du[0] + a.y0*du[1], a.x1*du[0] + a.y1*du[1], a.x0*du[0] + a.y1*du[1], a.x1*du[0] + a.y0*du[1])]
            return bl, min(us), max(us)
        lbl, lu0, lu1 = frame(L)
        best = None
        for j, M in enumerate(lines):
            if j == i or j in used or j in merged:
                continue
            if abs((M['rot'] - L['rot'] + 180) % 360 - 180) > 4:
                continue
            mbl, mu0, mu1 = frame(M)
            dv_ = mbl - lbl
            if 0.8 * L['size'] < abs(dv_) < 2.8 * L['size'] and mu0 < lu1 + 4 * L['size'] and mu1 > lu0 - 4 * L['size']                     and re.match(r"\s*[\d#=@]", M['text']):
                sc = abs(dv_) + abs(mu0 - lu0) * 0.2
                if best is None or sc < best[0]:
                    best = (sc, j)
        if best:
            used.add(best[1]); merged[i] = best[1]
    out = []
    for i, L in enumerate(lines):
        if i in used:
            continue
        if i in merged:
            M = lines[merged[i]]
            bb = (min(L['bbox'][0], M['bbox'][0]), min(L['bbox'][1], M['bbox'][1]), max(L['bbox'][2], M['bbox'][2]), max(L['bbox'][3], M['bbox'][3]))
            out.append({'text': L['text'] + ' ' + M['text'], 'cmap': L['cmap'] + [None] + M['cmap'], 'atoms': L['atoms'] + M['atoms'],
                        'bbox': bb, 'rot': L['rot'], 'size': L['size'], 'src': L['src'], 'conf': min(L['conf'], M['conf'])})
        else:
            out.append(L)
    return out


def classify_page(page):
    """'glyph' | 'text' | 'vector' | 'raster' | 'empty' heuristics."""
    ntext = len(page.get_text('words'))
    imgs = page.get_images()
    return ntext, len(imgs)


def line_records(lines, page_no):
    recs = []
    for L in lines:
        text = L['text']
        for r in parse_dims(text):
            a, b = r['span']
            ats = [x for x in L['cmap'][a:b] if x is not None]
            if not ats:
                continue
            seen = []
            for x in ats:
                if not any(x is y for y in seen):
                    seen.append(x)
            bb = (min(x.x0 for x in seen), min(x.y0 for x in seen), max(x.x1 for x in seen), max(x.y1 for x in seen))
            conf = r['conf'] * min(x.conf for x in seen)
            if L['src'] == 'vector' and len(L['atoms']) <= 3:
                conf *= 0.6          # isolated 1-3 glyph strings in outline text are often tilted-text fragments
            rec = {'kind': r['kind'], 'text': r['text'], 'value': r['value'], 'unit': r['unit'],
                   'bbox': [round(v, 2) for v in bb], 'rot': round(L['rot'], 1),
                   'vertical': 45 < L['rot'] % 180 < 135, 'size': round(L['size'], 2), 'conf': round(conf, 2),
                   'src': L['src'], 'line': text.strip(), 'page': page_no}
            if 'parts' in r:
                rec['parts'] = r['parts']
            fr = [x.info for x in seen if x.info and x.info.get('frac')]
            if fr:
                rec['fraction_read'] = [{'frac': '%d/%d' % x['frac'], 'how': x.get('how')} for x in fr]
            recs.append(rec)
    return recs


def extract(doc, pno, learn_pages=4, vlib=None):
    """Extract dimension records for page number pno (1-based).  Returns dict(dims, lines, mode, notes)."""
    page = doc[pno - 1]
    notes = []
    ntext, nimg = classify_page(page)
    mode = None
    atoms = []
    lines = []
    ev = None
    # glyph (stroke font) pipeline
    lo = max(0, pno - 1 - learn_pages); hi = min(len(doc), pno + learn_pages)
    extra = [doc[i] for i in range(lo, hi) if i != pno - 1]
    try:
        atoms, ev = atoms_from_stream(page, extra)
    except Exception as e:      # pragma: no cover
        notes.append('stream parse failed: %s' % e); atoms = []
    if len(atoms) >= 20 and len(atoms) >= 0.5 * max(1, sum(len(w[4]) for w in page.get_text('words'))):
        mode = 'glyph'
    else:
        atoms = []
    if mode is None and ntext >= 10:
        atoms = atoms_from_textlayer(page)
        atoms = stack_fractions(atoms)
        mode = 'text'
    if mode is None:
        va = vector_atoms(page, vlib)
        if va is not None:
            atoms = va; mode = 'vector'
    if mode is None:
        mode = 'raster' if nimg and ntext < 5 else 'empty'
        notes.append('raster/vector text, OCR needed' if mode == 'raster' else 'no text found')
    if atoms:      # plots often draw the same text twice (exact copies): keep one
        seen = set(); uniq = []
        for a in atoms:
            k = (a.ch, round(a.x0, 1), round(a.y0, 1), round(a.x1, 1), round(a.y1, 1))
            if k not in seen:
                seen.add(k); uniq.append(a)
        atoms = uniq
    lines = assemble(atoms) if atoms else []
    if mode == 'vector':       # outline fonts draw letter O and digit 0 alike: a 0 between letters is an O
        for L in lines:
            L['text'] = re.sub(r"(?<=[A-Z])0(?=[A-Z])|^0(?=[A-Z]{2})|(?<=[A-Z]{2})0$", 'O', L['text'])
    lines = merge_continuations(merge_continuations(lines))     # twice: '3 SPA.' / '@ 3"' / '= 10 1/2"'
    dims = line_records(lines, pno)
    return {'dims': dims, 'lines': lines, 'mode': mode, 'notes': notes, 'page': page, 'atoms': atoms, 'ev': ev}


def _frame_pts(subs, du, up):
    """page-space polygons -> upright, y-down glyph coordinates for the text direction (du, up)."""
    return [[(x * du[0] + y * du[1], -(x * up[0] + y * up[1])) for x, y in s] for s in subs]


ANGLES = {0: ((1.0, 0.0), (0.0, -1.0)), 90: ((0.0, -1.0), (-1.0, 0.0)), 270: ((0.0, 1.0), (1.0, 0.0))}


def load_glyph_templates(path=None):
    path = path or os.path.join(HERE, 'pdf_dims_glyphs.json')
    if not os.path.exists(path):
        return None
    with open(path, encoding='utf-8') as f:
        lib = json.load(f)
    T = Templates()
    for g in lib['glyphs']:
        subs = [[(x, y) for x, y in s] for s in g['subs']]
        T.add(g['ch'], subs)
    return T


VEC_MAXD = 0.10         # accept a glyph when its chamfer distance to the template is below this (unit = glyph height)


def vector_atoms(page, vlib=None):
    """Text drawn as filled outline glyphs (no text layer).  Recognises glyphs with the template library
    (horizontal and +/-90 degree text only); returns list of Atom or None when the page has no such glyphs / library."""
    T = vlib or load_glyph_templates()
    if T is None:
        return None
    ev = parse_stream(page)
    glyphs = []
    for e in ev:
        if e[0] != 'P' or e[1]['kind'] != 'f':
            continue
        subs = e[1]['subs']
        x0, y0, x1, y1 = bbox_of(subs)
        w = x1 - x0; h = y1 - y0
        if max(w, h) > 14 or max(w, h) < 0.25:
            continue
        glyphs.append({'subs': subs, 'bb': (x0, y0, x1, y1), 'w': w, 'h': h, 'cx': (x0 + x1) / 2, 'cy': (y0 + y1) / 2})
    if len(glyphs) < 40:
        return None
    # exact duplicates
    seen = set(); uniq = []
    for g in glyphs:
        k = tuple(round(v, 1) for v in g['bb']) + (len(g['subs']),)
        if k not in seen:
            seen.add(k); uniq.append(g)
    glyphs = uniq
    quads = []; shapes = []
    for g in glyphs:
        if len(g['subs']) == 1 and len(g['subs'][0]) == 5 and max(g['w'], g['h']) < 3.4:
            quads.append(g)
        else:
            shapes.append(g)

    memo = {}

    def best(g, angs):
        res = None
        for a in angs:
            du, up = ANGLES[a]
            fp = _frame_pts(g['subs'], du, up)
            mk = tuple(tuple((round((x - fp[0][0][0]) / 0.15), round((y - fp[0][0][1]) / 0.15)) for x, y in sub) for sub in fp)
            if mk not in memo:
                memo[mk] = T.classify(fp)
            bl = memo[mk]
            if bl and (res is None or bl[0][0] < res[0]):
                res = (bl[0][0], bl[0][1], a)
        return res
    # pass 1: free angle
    for g in shapes:
        g['r1'] = best(g, (0, 90, 270))
    good = [g for g in shapes if g['r1'] and g['r1'][0] < 0.07]
    # pass 2: angle by neighbourhood vote, re-classify with that angle
    for g in shapes:
        votes = collections.Counter()
        R = max(10.0, 3.0 * max(g['w'], g['h']))
        for o in good:
            if o is g:
                continue
            if abs(o['cx'] - g['cx']) < R and abs(o['cy'] - g['cy']) < R:
                votes[o['r1'][2]] += 1
        if votes:
            a = votes.most_common(1)[0][0]
            r = best(g, (a,))
            g['r'] = r
        else:
            g['r'] = g['r1']
    atoms = []
    cls = []
    for g in shapes:
        r = g['r']
        if r is None or r[0] > VEC_MAXD:
            continue
        d, lab, a = r
        du, up = ANGLES[a]
        cs = [(g['bb'][0], g['bb'][1]), (g['bb'][2], g['bb'][1]), (g['bb'][0], g['bb'][3]), (g['bb'][2], g['bb'][3])]
        vs = [x * up[0] + y * up[1] for x, y in cs]
        us = [x * du[0] + y * du[1] for x, y in cs]
        g['ang'] = a; g['du'] = du; g['up'] = up; g['v0'] = min(vs); g['v1'] = max(vs); g['u0'] = min(us); g['u1'] = max(us)
        g['lab'] = lab; g['dist'] = d
        cls.append(g)
    if len(cls) < 20:
        return None

    def nearest_cls(g, maxr=14.0):
        b = None
        for o in cls:
            dd = math.hypot(o['cx'] - g['cx'], o['cy'] - g['cy'])
            if dd < maxr and (b is None or dd < b[0]):
                b = (dd, o)
        return b[1] if b else None

    def mk(g, ch, ref_size, src_conf):
        du, up = g['du'], g['up']
        bb = g['bb']
        vc = (g['v0'] + g['v1']) / 2
        return g, ch

    out = []
    for g in cls:
        size = g['v1'] - g['v0']
        # base point: baseline at the bottom of the glyph
        bv = g['v0']
        cx, cy = g['cx'], g['cy']
        vc = cx * g['up'][0] + cy * g['up'][1]
        base = (cx + g['up'][0] * (bv - vc), cy + g['up'][1] * (bv - vc))
        conf = 1.0 if g['dist'] < 0.06 else 0.8
        a = Atom(g['lab'], g['bb'], g['du'], g['up'], size, False, 'vector', conf, 0, base, {'dist': round(g['dist'], 3)})
        out.append(a)
    # quads: period, hyphen, tick (foot mark)
    for g in quads:
        nb = nearest_cls(g)
        if nb is None:
            continue
        du, up = nb['du'], nb['up']
        cs = [(g['bb'][0], g['bb'][1]), (g['bb'][2], g['bb'][1]), (g['bb'][0], g['bb'][3]), (g['bb'][2], g['bb'][3])]
        us = [x * du[0] + y * du[1] for x, y in cs]; vs = [x * up[0] + y * up[1] for x, y in cs]
        wu = max(us) - min(us); hv = max(vs) - min(vs)
        ref = max([(o['v1'] - o['v0']) for o in cls if abs(o['cx'] - g['cx']) < 10 and abs(o['cy'] - g['cy']) < 10 and o['du'] == du] or [nb['v1'] - nb['v0']])
        if hv > 1.2 and hv > 1.35 * wu and hv < 0.6 * ref:
            ch = "'"; bv = max(vs) - ref          # tick hangs from cap height
        elif wu > 1.2 and wu > 1.35 * hv and wu < 0.95 * ref:
            ch = '-'; bv = (min(vs) + max(vs)) / 2 - 0.45 * ref
        else:
            ch = '.'; bv = min(vs)
        cx, cy = g['cx'], g['cy']
        vc = cx * up[0] + cy * up[1]
        base = (cx + up[0] * (bv - vc), cy + up[1] * (bv - vc))
        out.append(Atom(ch, g['bb'], du, up, ref, False, 'vector', 0.9, 0, base, {}))
    # two ticks side by side = inch mark
    ticks = [a for a in out if a.ch == "'"]
    used = set()
    for a in ticks:
        if id(a) in used:
            continue
        for b in ticks:
            if b is a or id(b) in used or a.du != b.du:
                continue
            ua = (a.x0 + a.x1) / 2 * a.du[0] + (a.y0 + a.y1) / 2 * a.du[1]
            ub = (b.x0 + b.x1) / 2 * a.du[0] + (b.y0 + b.y1) / 2 * a.du[1]
            va = (a.x0 + a.x1) / 2 * a.up[0] + (a.y0 + a.y1) / 2 * a.up[1]
            vb = (b.x0 + b.x1) / 2 * a.up[0] + (b.y0 + b.y1) / 2 * a.up[1]
            if 0.3 < ub - ua < 1.9 and abs(va - vb) < 0.5:
                a.ch = '"'; a.x0 = min(a.x0, b.x0); a.y0 = min(a.y0, b.y0); a.x1 = max(a.x1, b.x1); a.y1 = max(a.y1, b.y1)
                used.add(id(a)); used.add(id(b)); break
    out = [a for a in out if id(a) not in used or a.ch == '"']
    # glyphs removed because they were merged: the second tick of each pair
    pair_second = set()
    return stack_fractions(out)


def stack_fractions(atoms):
    """Merge small raised numerator / lowered denominator digits (with an optional slash glyph) into fraction atoms."""
    groups = collections.defaultdict(list)
    for a in atoms:
        groups[int(round(angle_of(a.du) / 3.0)) % 120].append(a)
    out = []
    for key, al in groups.items():
        du = al[0].du; up = al[0].up
        info = {}
        for a in al:
            cs = [(a.x0, a.y0), (a.x1, a.y0), (a.x0, a.y1), (a.x1, a.y1)]
            us = [x * du[0] + y * du[1] for x, y in cs]; vs = [x * up[0] + y * up[1] for x, y in cs]
            info[id(a)] = (min(us), max(us), min(vs), max(vs))
        digs = [a for a in al if a.ch.isdigit() and len(a.ch) == 1]
        if len(digs) < 3:
            out.extend(al); continue
        small = []
        for a in digs:
            u0, u1, v0, v1 = info[id(a)]
            h = v1 - v0
            R = max((info[id(o)][3] - info[id(o)][2]) for o in digs
                    if abs(info[id(o)][0] - u0) < 4 * h + 6 and abs(info[id(o)][2] - v0) < 2 * h + 4)
            if h < 0.8 * R and R > 2.5:
                small.append((a, R))
        smallset = {id(a): R for a, R in small}
        slashes = [a for a in al if a.ch == '/']
        cand = [a for a, R in small] + slashes
        parent = {id(a): id(a) for a in cand}

        def find(x):
            while parent[x] != x:
                parent[x] = parent[parent[x]]; x = parent[x]
            return x
        for i, a in enumerate(cand):
            for b in cand[i + 1:]:
                ia, ib = info[id(a)], info[id(b)]
                h = max(ia[3] - ia[2], ib[3] - ib[2])
                gapu = max(ib[0] - ia[1], ia[0] - ib[1])
                gapv = max(ib[2] - ia[3], ia[2] - ib[3])
                if gapu < 0.7 * h and gapv < 0.9 * h:
                    parent[find(id(a))] = find(id(b))
        clusters = collections.defaultdict(list)
        for a in cand:
            clusters[find(id(a))].append(a)
        consumed = set()
        newatoms = []
        for cl in clusters.values():
            ds = [a for a in cl if a.ch.isdigit()]
            if len(ds) < 2:
                continue
            vc = sorted(((info[id(a)][2] + info[id(a)][3]) / 2, i) for i, a in enumerate(ds))
            hs = sum(info[id(a)][3] - info[id(a)][2] for a in ds) / len(ds)
            gaps = [(vc[i + 1][0] - vc[i][0], i) for i in range(len(vc) - 1)]
            g, k = max(gaps)
            if g < 0.35 * hs:
                continue
            lower = [ds[i] for _, i in vc[:k + 1]]; upper = [ds[i] for _, i in vc[k + 1:]]
            lower.sort(key=lambda a: info[id(a)][0]); upper.sort(key=lambda a: info[id(a)][0])
            try:
                n = int(''.join(a.ch for a in upper)); d = int(''.join(a.ch for a in lower))
            except ValueError:
                continue
            if not valid_frac(n, d):
                continue
            allat = upper + lower + [a for a in cl if a.ch == '/']
            R = max(smallset.get(id(a), 0) for a in ds)
            # base: bottom of the nearest normal-size digit directly to the left
            left = None
            ul = min(info[id(a)][0] for a in allat)
            vlow = min(info[id(a)][2] for a in allat); vhigh = max(info[id(a)][3] for a in allat)
            for o in digs:
                if id(o) in smallset:
                    continue
                io = info[id(o)]
                if 0 <= ul - io[1] < 0.9 * R and io[2] < vhigh and io[3] > vlow:
                    if left is None or io[1] > info[id(left)][1]:
                        left = o
            base = left.base if left is not None else None
            bb = (min(a.x0 for a in allat), min(a.y0 for a in allat), max(a.x1 for a in allat), max(a.y1 for a in allat))
            fa = Atom(' %d/%d' % (n, d), bb, du, up, R, False, allat[0].src, min(a.conf for a in allat), 0, base,
                      {'frac': (n, d), 'how': 'stacked'})
            newatoms.append(fa)
            for a in allat:
                consumed.add(id(a))
        out.extend(a for a in al if id(a) not in consumed)
        out.extend(newatoms)
    return out


# ----------------------------------------------------------------------------------------------------------------
# filters, output, overlay
# ----------------------------------------------------------------------------------------------------------------
def in_clip(d, clip):
    x0, y0, x1, y1 = d['bbox']
    cx = (x0 + x1) / 2; cy = (y0 + y1) / 2
    return clip[0] <= cx <= clip[2] and clip[1] <= cy <= clip[3]


def filter_dims(dims, clip=None, near=None, radius=30.0, words=None, kinds=None, lines=None):
    out = []
    rx = re.compile(words, re.I) if words else None
    for d in dims:
        if clip and not in_clip(d, clip):
            continue
        if kinds and d['kind'] not in kinds:
            continue
        if rx and not (rx.search(d['text']) or rx.search(d.get('line', ''))):
            continue
        if near:
            x0, y0, x1, y1 = d['bbox']
            # distance from point to bbox
            dx = max(x0 - near[0], 0, near[0] - x1); dy = max(y0 - near[1], 0, near[1] - y1)
            dist = math.hypot(dx, dy)
            if dist > radius:
                continue
            d = dict(d); d['dist'] = round(dist, 1)
        out.append(d)
    if near:
        out.sort(key=lambda d: d['dist'])
    else:
        out.sort(key=lambda d: (round(d['bbox'][1] / 8), d['bbox'][0]))
    return out


KCOL = {'feet-inch': (0.8, 0, 0), 'inch': (0.9, 0.3, 0), 'fraction': (0.9, 0.3, 0), 'decimal': (0, 0.5, 0), 'station': (0, 0, 0.8),
        'elevation': (0, 0.5, 0.5), 'angle': (0.6, 0, 0.6), 'count': (0.8, 0, 0.5), 'plate': (0.4, 0.4, 0), 'rebar': (0, 0.4, 0.4),
        'radius': (0.5, 0.2, 0.2)}


def write_overlay(pdf, pno, dims, out_png, clip=None, dpi=150, leaders=None):
    doc = fitz.open(pdf)
    pg = doc[pno - 1]
    for d in dims:
        c = KCOL.get(d['kind'], (0.3, 0.3, 0.3))
        r = fitz.Rect(*d['bbox'])
        pg.draw_rect(r, color=c, width=0.5)
        lab = d['text']
        pg.insert_text((r.x0, max(6, r.y0 - 1.0)), lab, fontsize=4.0, color=c)
    for l in (leaders or []):
        if l.get('p0') and l.get('p1'):
            pg.draw_line(fitz.Point(*l['p0']), fitz.Point(*l['p1']), color=(0, 0.6, 0), width=0.4)
            for p in (l['p0'], l['p1']):
                pg.draw_circle(fitz.Point(*p), 0.8, color=(0, 0.6, 0), fill=(0, 0.6, 0))
    r = fitz.Rect(*clip) if clip else pg.rect
    pg.get_pixmap(dpi=dpi, clip=r).save(out_png)
    doc.close()


def fmt_line(d):
    s = '%8.1f %7.1f  %-9s %-34s' % (d['bbox'][0], d['bbox'][1], d['kind'], d['text'])
    v = d['value']
    if v is not None and d['kind'] in ('feet-inch', 'inch', 'fraction', 'count', 'radius', 'decimal'):
        s += ' = %.4f in' % v if d['unit'] == 'in' else ' = %s %s' % (v, d['unit'] or '')
    elif v is not None:
        s += ' = %s %s' % (v, d['unit'] or '')
    s += '  rot=%g sz=%g conf=%.2f %s' % (d['rot'], d['size'], d['conf'], d['src'])
    if 'dist' in d:
        s += ' dist=%.1f' % d['dist']
    return s


# ----------------------------------------------------------------------------------------------------------------
# self test
# ----------------------------------------------------------------------------------------------------------------
MIN_CONF_EVAL = 0.5      # below this (e.g. nothing today) a record is not scored; bare fractions without an inch mark are 0.6


def canon_truth(s):
    """canonical (kind, text) of a hand-written truth string (parsed with the same dimension grammar)."""
    r = parse_dims(s)
    if len(r) == 1:
        return (r[0]['kind'], r[0]['text'])
    return ('raw', s)


def selftest(truth_path, verbose=False, only=None, data_dir=None):
    with open(truth_path, encoding='utf-8') as f:
        truth = json.load(f)
    data_dir = data_dir or truth.get('data_dir') or os.environ.get('SBMDATA') or r'C:\SBMData'
    docs = {}
    cache = {}
    rows = []
    per = collections.OrderedDict()
    for case in truth['cases']:
        pdf = os.path.join(data_dir, case['pdf'])
        if only and only not in case['pdf']:
            continue
        if pdf not in docs:
            docs[pdf] = fitz.open(pdf)
        doc = docs[pdf]
        if (pdf, case['page']) not in cache:
            cache[(pdf, case['page'])] = extract(doc, case['page'])
        res = cache[(pdf, case['page'])]
        clip = case['clip']
        got = [d for d in res['dims'] if in_clip(d, clip) and d['conf'] >= MIN_CONF_EVAL]
        tr = collections.Counter(canon_truth(t) for t in case['dims'])
        gt = collections.Counter((d['kind'], d['text']) for d in got)
        # fractions-aware matching: compare on text only when kinds differ for the same text (kind is informational)
        tr_t = collections.Counter(); gt_t = collections.Counter()
        for (k, t), n in tr.items(): tr_t[t] += n
        for (k, t), n in gt.items(): gt_t[t] += n
        for t in case.get('ignore', []):            # known non-dimensions (title block revision dates such as 11/16)
            if gt_t[t] > 0:
                gt_t[t] -= 1
        tp = sum(min(n, gt_t[t]) for t, n in tr_t.items())
        ntr = sum(tr_t.values()); ngt = sum(gt_t.values())
        frt = {t: n for t, n in tr_t.items() if '/' in t}
        frtp = sum(min(n, gt_t[t]) for t, n in frt.items())
        nfr = sum(frt.values())
        miss = [(t, n - gt_t[t]) for t, n in tr_t.items() if gt_t[t] < n]
        extra = [(t, n - tr_t[t]) for t, n in gt_t.items() if tr_t[t] < n]
        key = '%s p.%d' % (case['pdf'][:28], case['page'])
        st = per.setdefault(key, {'tp': 0, 'truth': 0, 'got': 0, 'frtp': 0, 'frn': 0, 'miss': [], 'extra': []})
        st['tp'] += tp; st['truth'] += ntr; st['got'] += ngt; st['frtp'] += frtp; st['frn'] += nfr
        st['miss'] += miss; st['extra'] += extra
        if verbose:
            print('case', key, case.get('name', ''), 'mode=%s' % res['mode'], 'truth', ntr, 'got', ngt, 'tp', tp)
            for t, n in miss: print('   MISSED', t, n)
            for t, n in extra: print('   EXTRA ', t, n)
    print('%-40s %6s %6s %6s %7s %7s | %6s %6s' % ('pdf / page', 'truth', 'found', 'tp', 'recall', 'prec', 'frac', 'fracok'))
    T = collections.Counter()
    for k, st in per.items():
        rec = st['tp'] / st['truth'] if st['truth'] else float('nan')
        pre = st['tp'] / st['got'] if st['got'] else float('nan')
        print('%-40s %6d %6d %6d %6.1f%% %6.1f%% | %6d %6d' % (k, st['truth'], st['got'], st['tp'], 100 * rec, 100 * pre, st['frn'], st['frtp']))
        for a in ('tp', 'truth', 'got', 'frtp', 'frn'):
            T[a] += st[a]
    print('%-40s %6d %6d %6d %6.1f%% %6.1f%% | %6d %6d  (fraction exact %.1f%%)' % (
        'TOTAL', T['truth'], T['got'], T['tp'], 100 * T['tp'] / max(1, T['truth']), 100 * T['tp'] / max(1, T['got']), T['frn'], T['frtp'],
        100 * T['frtp'] / max(1, T['frn'])))
    return per


# ----------------------------------------------------------------------------------------------------------------
# CLI
# ----------------------------------------------------------------------------------------------------------------
def _pair(s, n=2):
    v = [float(x) for x in s.split(',')]
    if len(v) != n:
        raise argparse.ArgumentTypeError('expected %d comma-separated numbers' % n)
    return v


def main(argv=None):
    ap = argparse.ArgumentParser(description='Generic dimension / fraction reader for plan PDFs (see the README block at the top of the script).')
    ap.add_argument('pdf', nargs='?')
    ap.add_argument('--page', type=int, help='1-based page number')
    ap.add_argument('--clip', type=lambda s: _pair(s, 4), help='x0,y0,x1,y1 in page points (top-left origin)')
    ap.add_argument('--json', help='write the records to this json file')
    ap.add_argument('--png', help='write an overlay png (boxes + decoded text)')
    ap.add_argument('--dpi', type=int, default=150)
    ap.add_argument('--near', type=_pair, help='x,y: dimensions near a point (nearest first)')
    ap.add_argument('--radius', type=float, default=30.0, help='search radius for --near (pt, default 30)')
    ap.add_argument('--words', help='regex: only dimensions whose text or line matches')
    ap.add_argument('--kind', help='comma list of kinds to keep')
    ap.add_argument('--leaders', action='store_true', help='link each dimension to its dimension line / measured end points')
    ap.add_argument('--text', action='store_true', help='also print every assembled text line (not only dimensions)')
    ap.add_argument('--learn-pages', type=int, default=4, help='neighbour pages used to learn the fraction digit shapes (default 4)')
    ap.add_argument('--selftest', action='store_true')
    ap.add_argument('--truth', default=os.path.join(HERE, 'pdf_dims_truth.json'))
    ap.add_argument('--verbose', action='store_true')
    ap.add_argument('--only', help='selftest: only cases whose pdf name contains this')
    a = ap.parse_args(argv)
    if a.selftest:
        selftest(a.truth, a.verbose, a.only)
        return 0
    if not a.pdf or not a.page:
        ap.error('pdf and --page are required')
    doc = fitz.open(a.pdf)
    res = extract(doc, a.page, learn_pages=a.learn_pages)
    kinds = set(a.kind.split(',')) if a.kind else None
    dims = filter_dims(res['dims'], a.clip, a.near, a.radius, a.words, kinds)
    leaders = None
    if a.leaders:
        leaders = link_leaders(res, dims)
        for d, l in zip(dims, leaders):
            d['leader'] = l
    if a.text:
        for L in res['lines']:
            if a.clip and not in_clip({'bbox': L['bbox']}, a.clip):
                continue
            print('LINE %7.1f %7.1f rot=%g  %s' % (L['bbox'][0], L['bbox'][1], round(L['rot'], 1), L['text']))
    for d in dims:
        print(fmt_line(d))
        if a.leaders and d.get('leader'):
            l = d['leader']
            if l.get('p0'):
                print('         leader: p0=(%.1f,%.1f) p1=(%.1f,%.1f) len=%.1f pt%s' % (
                    l['p0'][0], l['p0'][1], l['p1'][0], l['p1'][1], l['length_pt'],
                    (' scale=%s -> measured %s' % (l.get('scale'), l.get('measured'))) if l.get('scale') else ''))
    print('# %d dimension(s), mode=%s%s' % (len(dims), res['mode'], ('; ' + '; '.join(res['notes'])) if res['notes'] else ''), file=sys.stderr)
    if a.json:
        with open(a.json, 'w', encoding='utf-8') as f:
            json.dump({'pdf': a.pdf, 'page': a.page, 'mode': res['mode'], 'notes': res['notes'], 'dims': dims}, f, indent=1, ensure_ascii=False)
    if a.png:
        write_overlay(a.pdf, a.page, dims, a.png, a.clip, a.dpi, leaders)
    return 0


def link_leaders(res, dims):
    """For each dimension record find the dimension line it annotates: the longest run of collinear stroke segments parallel
    to the text, within ~2.5 text heights of it and spanning the text position.  Returns one dict per dimension:
    p0, p1 (measured end points, page pt), length_pt, offset_pt, ext (extension lines found at the ends),
    scale / measured (inches) when a drawing scale is printed on the page."""
    ev = res.get('ev')
    if ev is None:
        ev = parse_stream(res['page'])
    segs = []
    for e in ev:
        if e[0] != 'P' or e[1]['kind'] != 'S':
            continue
        for s in e[1]['subs']:
            for (x0, y0), (x1, y1) in zip(s, s[1:]):
                L = math.hypot(x1 - x0, y1 - y0)
                if L > 6.0:
                    segs.append((x0, y0, x1, y1, L))
    scales = [d for d in res['dims'] if d['kind'] == 'scale' and d['value']]
    out = []
    for d in dims:
        r = math.radians(d['rot'])
        du = (math.cos(r), -math.sin(r)); up = (du[1], -du[0])
        x0, y0, x1, y1 = d['bbox']
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        cu = cx * du[0] + cy * du[1]; cv = cx * up[0] + cy * up[1]
        size = d['size']
        cand = []
        for sx0, sy0, sx1, sy1, L in segs:
            dx, dy = (sx1 - sx0) / L, (sy1 - sy0) / L
            if abs(dx * du[1] - dy * du[0]) > 0.06:         # not parallel (about 3.5 deg)
                continue
            su0 = sx0 * du[0] + sy0 * du[1]; su1 = sx1 * du[0] + sy1 * du[1]
            sv = ((sx0 + sx1) / 2) * up[0] + ((sy0 + sy1) / 2) * up[1]
            off = sv - cv
            if abs(off) > 2.6 * size:
                continue
            cand.append((sv, min(su0, su1), max(su0, su1)))
        best = None
        # group collinear candidates by perpendicular position
        cand.sort()
        groups = []
        for sv, a, b in cand:
            if groups and abs(groups[-1]['v'] - sv) < 0.5:
                groups[-1]['segs'].append((a, b))
            else:
                groups.append({'v': sv, 'segs': [(a, b)]})
        for g in groups:
            g['segs'].sort()
            chains = []
            for a, b in g['segs']:
                if chains and a <= chains[-1][1] + 1.2 * size + 1.0:     # allow the gap left for the text
                    chains[-1][1] = max(chains[-1][1], b)
                else:
                    chains.append([a, b])
            for a, b in chains:
                if a - 0.6 * size <= cu <= b + 0.6 * size and (b - a) > 2.0 * size:
                    score = abs(g['v'] - cv) + 0.002 * (b - a)
                    if best is None or score < best[0]:
                        best = (score, g['v'], a, b)
        info = {}
        if best:
            _, v, a, b = best
            p0 = (a * du[0] + v * up[0], a * du[1] + v * up[1])      # (u,v) -> page: columns of the orthonormal frame
            p1 = (b * du[0] + v * up[0], b * du[1] + v * up[1])
            info = {'p0': [round(p0[0], 2), round(p0[1], 2)], 'p1': [round(p1[0], 2), round(p1[1], 2)], 'length_pt': round(b - a, 2),
                    'offset_pt': round(v - cv, 2)}
            # extension lines: perpendicular strokes crossing the dimension line; the measured end points are the nearest ones
            # on either side of the text (falls back to the ends of the dimension line)
            us_t = [x * du[0] + y * du[1] for x, y in ((x0, y0), (x1, y0), (x0, y1), (x1, y1))]
            tu0, tu1 = min(us_t), max(us_t)
            xs = []
            for sx0, sy0, sx1, sy1, L in segs:
                dx, dy = (sx1 - sx0) / L, (sy1 - sy0) / L
                if abs(dx * du[0] + dy * du[1]) > 0.1:           # not perpendicular
                    continue
                mu = ((sx0 + sx1) / 2) * du[0] + ((sy0 + sy1) / 2) * du[1]
                va = sx0 * up[0] + sy0 * up[1]; vb = sx1 * up[0] + sy1 * up[1]
                if min(va, vb) - 1.0 <= v <= max(va, vb) + 1.0 and a - 1.5 <= mu <= b + 1.5:
                    xs.append(mu)
            left = [m_ for m_ in xs if m_ < tu0 + 0.3 * (tu1 - tu0)]
            right = [m_ for m_ in xs if m_ > tu1 - 0.3 * (tu1 - tu0)]
            ext = 0
            if left and right:
                a2 = max(left); b2 = min(right)
                if b2 - a2 > 2 * size:
                    a, b = a2, b2; ext = 2
            p0 = (a * du[0] + v * up[0], a * du[1] + v * up[1])
            p1 = (b * du[0] + v * up[0], b * du[1] + v * up[1])
            info['p0'] = [round(p0[0], 2), round(p0[1], 2)]; info['p1'] = [round(p1[0], 2), round(p1[1], 2)]
            info['length_pt'] = round(b - a, 2)
            info['ext_lines'] = ext
            if scales:
                sc = min(scales, key=lambda s: math.hypot((s['bbox'][0] + s['bbox'][2]) / 2 - cx, (s['bbox'][1] + s['bbox'][3]) / 2 - cy))
                pr = sc['parts']['pt_per_real_in']
                info['scale'] = sc['text']
                info['measured'] = '%.2f in' % ((b - a) / pr)
                if d.get('value') and d.get('unit') == 'in' and d['kind'] in ('feet-inch', 'inch'):
                    info['measured_over_printed'] = round(((b - a) / pr) / d['value'], 3)    # 1.0 = drawn to the printed scale; 0.5 = half-size plot
        out.append(info)
    return out


if __name__ == '__main__':
    sys.exit(main())
