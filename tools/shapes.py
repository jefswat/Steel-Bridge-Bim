# Reduce the extracted diagram dimensions of every mark to the subset that reproduces the bar-list length (bar list length = plain sum of the
# out-to-out dimensions) and classify simple shapes. Output: temp/bend/plan_shapes.json
import json, itertools, re, sys
W = r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\.worktrees\bends\temp\bend' + '\\'
D = json.load(open(W + 'plan_dims.json'))


def solve(dims, L):
    cand = [d for d in dims if not d['pre'] and not d['suf']]
    if L is None or not cand: return None, cand
    best = None
    rng = [(0, 1, 2)] * len(cand)
    if len(cand) > 10:
        cand = sorted(cand, key=lambda d: (d['own'] is None))[:10]
        rng = [(0, 1, 2)] * 10
    for mult in itertools.product(*rng):
        t = sum(m * d['val'] for m, d in zip(mult, cand))
        if abs(t - L) > 0.51: continue
        used = [i for i, m in enumerate(mult) if m]
        owned_unused = sum(1 for i, d in enumerate(cand) if d['own'] and not mult[i])
        score = (owned_unused, len(used), sum(mult))
        if best is None or score < best[0]: best = (score, mult)
    return (best[1] if best else None), cand


out = []
for o in D:
    mult, cand = solve(o['dims'], o['listed'])
    pins = [d['val'] for d in o['dims'] if d['pre'] == 'D']
    rad = [d['val'] for d in o['dims'] if d['pre'] == 'R']
    mm = [(d['val'], d['suf'], d['vert']) for d in o['dims'] if d['suf']]
    rec = dict(design=o['design'], page=o['page'], sheet=o['sheet'], mark=o['mark'], listed=o['listed'], listed_txt=o['listed_txt'], desc=o['desc'], count=o['count'],
               pin=pins[0] if pins else None, pins=pins, radii=rad, minmax=mm, solved=mult is not None)
    if mult is not None:
        used = [dict(val=d['val'], vert=d['vert'], mult=m) for m, d in zip(mult, cand) if m]
        unused = [dict(val=d['val'], vert=d['vert']) for m, d in zip(mult, cand) if not m]
        rec['used'] = used; rec['unused'] = unused
        H = [u for u in used if not u['vert']]; V = [u for u in used if u['vert']]
        shape, dims = '', []
        mark = o['mark'].lower()
        letter = re.sub(r'^\d+([a-z])\d+$', r'\1', mark)
        if len(H) == 1 and H[0]['mult'] == 1 and len(V) == 1 and V[0]['mult'] == 2:
            shape, dims = 'U', [V[0]['val'], H[0]['val'], V[0]['val']]
        elif len(H) == 1 and H[0]['mult'] == 1 and len(V) == 2 and all(v['mult'] == 1 for v in V):
            shape, dims = 'U', [V[0]['val'], H[0]['val'], V[1]['val']]
        elif len(H) == 1 and H[0]['mult'] == 1 and len(V) == 1 and V[0]['mult'] == 1:
            shape, dims = 'L', [V[0]['val'], H[0]['val']]
        rec['shape'] = shape; rec['dims'] = dims
    out.append(rec)
json.dump(out, open(W + 'plan_shapes.json', 'w'), indent=0)
import collections
print(len(out), collections.Counter((r['solved'], r.get('shape', '')) for r in out))
