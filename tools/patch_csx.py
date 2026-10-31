# Append the plan bend-diagram shape (";S=shape:d1,d2,..:pin", inches) to every plan bar line of pier-rebar-*.csx that has a decoded diagram.
import json, re, sys, csv
W = r'C:\SBM\.worktrees\bends' + '\\'
R = json.load(open(W + r'temp\bend\plan_shapes.json'))
by = {}
for r in R:
    m = re.match(r'PIER NO\. (\w+) DETAILS', r['sheet'])
    if not m: continue
    by[('Pier ' + m.group(1), r['mark'].lower())] = r


def fmt(v):
    return ('%g' % v)


def spec(r, kind, vertical=False):
    mark = r['mark'].lower()
    if not r.get('solved') and kind != 'CapCantileverHoop': return None
    pin = r['pin'] or 0
    if kind in ('CapEnd',) and vertical:
        used = {(u['val'], u['vert']): u['mult'] for u in r['used']}
        un = [u['val'] for u in r['unused']]
        V = sorted([u['val'] for u in r['used'] if u['vert']])
        H = [u['val'] for u in r['used'] if not u['vert']]
        if len(H) == 1 and len(V) == 2 and 2.5 in un:
            return 'A14:%s,%s,%s,%s:%s' % (fmt(V[1]), fmt(H[0]), fmt(V[0]), '2.5', fmt(pin))
        return None
    if kind == 'CapCantilever' and r.get('used') and len(r['used']) == 2:
        un = [u['val'] for u in r['unused']]
        import math
        slant = max(r['used'], key=lambda u: u['val'])['val']
        flat = min(r['used'], key=lambda u: u['val'])['val']
        for a in un:
            for b in un:
                if a != b and abs(math.hypot(a, b) - slant) <= 1.0:
                    return 'CS:%s,%s,%s,%s:%s' % (fmt(flat), fmt(slant), fmt(max(a, b)), fmt(min(a, b)), fmt(pin))
        return None
    if kind in ('CapHoop', 'PedestalHoop') and r.get('used'):
        ms = sorted([u['val'] for u in r['used'] for _ in range(u['mult'])], reverse=True)
        if len(ms) == 6 and ms[0] == ms[1] and ms[2] == ms[3] and ms[4] == ms[5] and ms[0] > ms[2] > ms[4]:
            return 'HOOP:%s,%s,%s,%s:%s' % (fmt(ms[2]), fmt(ms[0]), fmt(ms[4]), fmt(ms[4]), fmt(pin))
        return None
    if kind == 'CapCantileverHoop' and r.get('minmax') and len(r['minmax']) == 2:
        mm = sorted(v for v, suf, vert in r['minmax'])
        rem = [v for v in r.get('raw', []) if v not in mm]
        rem = sorted(set(rem))
        if len(rem) == 2 and mm[0] > rem[1]:
            return 'HOOPV:%s,%s,%s,%s:%s' % (fmt(rem[1]), fmt(mm[0]), fmt(mm[1]), fmt(rem[0]), fmt(pin))
        return None
    if r['shape'] == 'U' and kind in ('FootingMat', 'CapEnd', 'CapTransverse', 'CapHairpin', 'PedestalLongitudinal', 'PedestalTransverse'):
        if kind == 'CapEnd' and vertical: return None
        return 'U:%s:%s' % (','.join(fmt(d) for d in r['dims']), fmt(pin))
    return None


def patch(path, table):
    src = open(path, encoding='utf-8').read()
    # the file lists "Pier xx" = ( new string[] {...foot...}, new string[] {...cap...}, ...)
    out = []
    pier = None
    n = 0
    for line in src.split('\n'):
        m = re.match(r'\s*\["(Pier [^"]+)"\] = \(', line)
        if m: pier = m.group(1)
        if pier and 'new string[]' in line:
            def rep(mm):
                global n
                s = mm.group(1)
                t = s.split(';')
                r = by.get((pier, t[0].lower()))
                if r is None or any(x.startswith('S=') for x in t): return mm.group(0)
                sp = spec(r, t[1], len(t) > 5 and t[5] == '1' and t[1] == 'CapEnd')
                if sp is None: return mm.group(0)
                n += 1
                return '"' + s + ';S=' + sp + '"'
            line = re.sub(r'"([0-9]+[a-z][0-9]+;[A-Za-z]+;[^"]*?)"', rep, line)
        out.append(line)
    open(path, 'w', encoding='utf-8', newline='').write('\n'.join(out))
    return n


n = 0
for f in ('pier-rebar-i80.csx', 'pier-rebar-ramp-g.csx'):
    n += patch(W + 'samples\\plans\\' + f, by)
print('patched', n)
