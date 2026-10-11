import re, sys, json
sys.path.insert(0, r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\.worktrees\bends\tools')
import bendx, plantext
import pymupdf as fitz
W = r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\.worktrees\bends' + '\\'
R = json.load(open(W + r'temp\bend\plan_shapes.json'))
pages = sorted({(r['design'], r['page'], r['sheet']) for r in R if 'PIER' in r['sheet']})
ends = {}
for design, page, sheet in pages:
    pier = 'Pier ' + re.match(r'PIER NO\. (\w+) DETAILS', sheet).group(1)
    d = fitz.open(bendx.PDFS[design])
    for pg in (page - 1, page - 2, page - 3):
        found = {}
        for it in plantext.texts(d[pg - 1]):
            s = bendx.decode(it['s']).strip()
            m = re.match(r'^(\d+a1[12]) .{1,3}E\.F\..{1,3}$', s)
            if m: found[m.group(1)] = it['x']
        if found:
            # only labels near the ends of the elevation (x < 330 or > 880); the grouped labels of the ramp piers (x ~ 520-545) are not at the ends
            for mk, x in found.items():
                if x < 330 or x > 880: ends[(pier, mk)] = -1 if x < 612 else 1
            break
    d.close()
total = 0
for f in ('pier-rebar-i80.csx', 'pier-rebar-ramp-g.csx'):
    path = W + 'samples\\plans\\' + f
    src = open(path, encoding='utf-8', newline='').read()
    out = []
    pier = None
    for line in src.split('\n'):
        m = re.match(r'\s*\["(Pier [^"]+)"\] = \(', line)
        if m: pier = m.group(1)
        if pier and 'CapSide' in line:
            def rep(mm):
                global total
                s = mm.group(1)
                t = s.split(';')
                e = ends.get((pier, t[0].lower()))
                if e is None or any(x.startswith('E=') for x in t): return mm.group(0)
                total += 1
                return '"' + s + ';E=' + str(e) + '"'
            line = re.sub(r'"([0-9]+a1[12];CapSide;[^"]*?)"', rep, line)
        out.append(line)
    open(path, 'w', encoding='utf-8', newline='').write('\n'.join(out))
print('ends set', total, sorted(ends.items())[:6])
