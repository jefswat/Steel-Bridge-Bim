# Parses a plan sheet content stream: text items (x, y_top, string) with the drawn fraction glyphs decoded.
# Fractions are vector glyphs: the text holds \201 where a fraction follows; the LAST path block of that text object is the fraction glyph.
import re, sys, hashlib
import pymupdf as fitz

TJ = re.compile(r'\((.*?)(?<!\\)\)\s*Tj', re.S)
TM = re.compile(r'([\-\d.]+) ([\-\d.]+) ([\-\d.]+) ([\-\d.]+) ([\-\d.]+) ([\-\d.]+) Tm')
BLK = re.compile(r'BT\s*(.*?)\s*ET|q 1 0 0 1 ([\-\d.]+) ([\-\d.]+) cm\s*(.*?)\s*\nQ', re.S)


def parse_page(page):
    c = page.read_contents().decode('latin-1')
    H = page.rect.height
    out = []
    for m in BLK.finditer(c):
        if m.group(1) is not None:
            body = m.group(1)
            tm = TM.search(body)
            tj = TJ.findall(body)
            fs = re.search(r'/\S+ ([\d.]+) Tf', body)
            if not tm or not tj:
                continue
            s = ''.join(tj).replace('\\201', '\x01').replace("\\'", "'")
            a, b, cc, dd, x, y = map(float, tm.groups())
            out.append({'t': 'text', 's': s, 'x': x, 'y': H - y, 'a': a, 'b': b, 'c': cc, 'd': dd,
                        'fs': float(fs.group(1)) if fs else 0})
        else:
            out.append({'t': 'path', 'x': float(m.group(2)), 'y': H - float(m.group(3)), 'body': m.group(4)})
    return out


def frac_key(body):
    return hashlib.md5(re.sub(r'\s+', ' ', body).encode()).hexdigest()[:8]


def texts(page):
    items = parse_page(page)
    res = []
    for i, it in enumerate(items):
        if it['t'] != 'text':
            continue
        if '\x01' in it['s']:
            blks = []
            j = i + 1
            while j < len(items) and items[j]['t'] == 'path':
                blks.append(items[j])
                j += 1
            it = dict(it)
            idx = len(it['s'][:it['s'].index('')].replace(' ', ''))
            b = blks[idx] if idx < len(blks) else None
            it['frac'] = frac_key(b['body']) if b else None
            it['fracbody'] = b['body'] if b else ''
        res.append(it)
    return res


if __name__ == '__main__':
    d = fitz.open(sys.argv[1])
    p = d[int(sys.argv[2]) - 1]
    for t in texts(p):
        print(round(t['x']), round(t['y']), repr(t['s']), t.get('frac', ''), 'rot' if t['b'] != 0 else '')
    d.close()
