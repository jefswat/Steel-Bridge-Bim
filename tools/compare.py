# Plan bend diagrams vs model bar shapes -> docs/plan-data/bend-diagram-comparison.md (tables) + temp/bend/compare.json
import json, re, csv, sys, collections, math
sys.path.insert(0, r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\.worktrees\bends\tools')
import abut_hand
W = r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\.worktrees\bends' + '\\'
ROOT = r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler' + '\\'
DUMP = sys.argv[1] if len(sys.argv) > 1 else W + r'temp\bend\dump1.txt'
OUT = sys.argv[2] if len(sys.argv) > 2 else W + r'temp\bend\compare.md'
SIZES = {'#3': .375, '#4': .5, '#5': .625, '#6': .75, '#7': .875, '#8': 1.0, '#9': 1.128, '#10': 1.27, '#11': 1.41, '#14': 1.693, '#18': 2.257}

plan = json.load(open(W + r'temp\bend\plan_shapes.json'))
byrec = {(r['design'], r['page'], r['mark'].lower()): r for r in plan}


def ftin(v, frac=True):
    if v is None: return '-'
    neg = v < 0
    v = abs(v)
    q = round(v * 16) / 16
    ft = int(q // 12)
    i = q - ft * 12
    whole = int(i)
    f = i - whole
    fs = ''
    if f > 1e-9:
        n = int(round(f * 16)); d = 16
        while n % 2 == 0 and d > 1: n //= 2; d //= 2
        fs = f' {n}/{d}'
    s = (f"{ft}'-" if ft else '') + f'{whole}{fs}"' if True else ''
    if not ft: s = f'{whole}{fs}"'
    else: s = f"{ft}'-{whole}{fs}\""
    return ('-' if neg else '') + s


def tol_for(v):
    f = abs(v * 16 - round(v * 16)) < 1e-6 and abs(v - round(v)) > 1e-6
    # printed precision: fractions with denominator >= 4 -> 1/4"; otherwise 1/2"
    fr = v - math.floor(v)
    if fr > 1e-6 and abs(fr * 2 - round(fr * 2)) > 1e-6: return 0.25
    return 0.5


# ---- model marks
models = collections.defaultdict(list)
for line in open(DUMP, encoding='utf-8', errors='replace'):
    if not line.startswith('M|'): continue
    f = line.rstrip('\n').split('|')
    pts = [tuple(float(x) for x in p.split(',')) for p in f[14].split()] if len(f) > 14 and f[14] else []
    m = dict(bridge=f[1], unit=f[2], mark=f[3], size=f[4], count=int(f[5]), length=float(f[6]), minlen=float(f[7]), code=f[8], pin=float(f[9] or 0),
             fixed=float(f[10]) if f[10] else None, dev=float(f[11]), loc=f[12], dims=[(d.split('=')[0], float(d.split('=')[1])) for d in f[13].split(';') if d], pts=pts)
    models[m['unit']].append(m)


def oto_dims(m):
    """model out-to-out dimension list (inches) of the bar shape."""
    db = SIZES.get(m['size'], .625)
    if m['code'] in ('U', 'L', 'A14', 'V'):                # plan-shaped: dims are the printed out-to-out values
        return [v for k, v in m['dims'] if k != 'W'], 'OTO'
    if m['code'] == 'STR': return [m['length']], 'OTO'
    if m['code'] == 'BENT' and len(m['pts']) >= 3:
        pts = m['pts']
        # merge nearly collinear neighbours (profile-following bars have 1-5 degree kinks that are not bends of the plan's diagram)
        keep = [pts[0]]
        for i in range(1, len(pts) - 1):
            a = (pts[i][0] - keep[-1][0], pts[i][1] - keep[-1][1]); b = (pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1])
            ang = abs(math.degrees(math.atan2(a[0] * b[1] - a[1] * b[0], a[0] * b[0] + a[1] * b[1])))
            if ang > 12: keep.append(pts[i])
        keep.append(pts[-1])
        pts = keep
        if len(pts) == 2: return [math.hypot(pts[1][0] - pts[0][0], pts[1][1] - pts[0][1])], 'straight within 12 deg (follows the cap profile)'
        segs = [math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]) for i in range(len(pts) - 1)]
        out = []
        n = len(segs)
        for i, s in enumerate(segs):
            add = (db / 2 if i in (0, n - 1) else 0) + (db / 2 if 0 < i < n - 1 else 0) + (db / 2 if i not in (0, n - 1) else 0)
            if n == 1: add = 0
            out.append(s + (db if 0 < i < n - 1 else db / 2))
        return out, 'OTO from centerline'
    return [v for k, v in m['dims']], 'as scheduled (A,B...)'


def unit_name(sheet):
    m = re.match(r'PIER NO\. (\w+) DETAILS', sheet)
    if m: return 'Pier ' + m.group(1)
    if sheet.startswith('WEST'): return 'West Abut'
    if sheet.startswith('EAST'): return 'East Abut'
    if sheet.startswith('SOUTH'): return 'South Abut'
    return None


ALIAS = [('COLUMN, VERTICAL', 'verticals'), ('FOOTING TO COLUMN DOWEL', 'dowels'), ('COLUMN, HOOPS', 'hoops'), ('COLUMN, TIES', 'crossties'),
         ('PLINTH, HOOPS', 'flare ties'), ('PLINTH, VERTICAL', 'flare bars')]


def find_model(unit, mark, desc=''):
    cands = []
    for k, kw in ALIAS:
        if desc.upper().startswith(k):
            res = [m for m in models.get(unit, []) if kw in m['loc'].lower() and not (kw == 'hoops' and 'flare' in m['loc'].lower())]
            return res
    for m in models.get(unit, []):
        locs = m['loc'].lower()
        if re.search(r'(?<![0-9a-z])' + re.escape(mark.lower()) + r'(?![0-9a-z])', locs) or m['mark'].lower() == mark.lower():
            cands.append(m)
    return cands


rows = []          # per plan mark
stat = collections.Counter()
cls_stat = collections.defaultdict(collections.Counter)
by_unit = collections.OrderedDict()

blist = collections.OrderedDict()
for r in csv.DictReader(open(ROOT + r'temp\ftg-rebar\planbars2.csv')):
    blist.setdefault((r['set'], int(r['page'])), []).append(r)

unitrows = collections.OrderedDict()
for (design, page), rr in blist.items():
    sheet = rr[0]['title']
    unit = unit_name(sheet)
    if not unit: continue
    out = unitrows.setdefault((design, page, sheet, unit), [])
    for b in rr:
        mark = b['mark']
        if not re.match(r'^\d{1,2}[a-z]\d{1,2}$', mark): continue
        rec = byrec.get((design, page, mark.lower()))
        hh = abut_hand.H.get((design, page), {}).get(mark.lower())
        if hh:
            segs = hh['segs']
            tot_h = abut_hand.total(hh)
            m0 = re.match(r"^(\d+)'-(\d+)", b['len'])
            lst = (int(m0.group(1)) * 12 + int(m0.group(2))) if m0 else None
            sumok = tot_h is not None and lst is not None and abs(tot_h - lst) <= 0.51
            vals = list(segs) if hh['kind'] != 'hoop' else [segs[0], segs[1], segs[2]]
            if hh['kind'] == 'hoopv': vals = [segs[0], segs[1]]
            rec = dict(solved=(sumok or hh['kind'] == 'hoopv'), used=[dict(val=v, vert=False, mult=1) for v in vals], unused=[], radii=[], minmax=[], pins=[hh['pin']], pin=hh['pin'],
                       shape=hh['kind'], dims=[], sumok=sumok, hand=hh['note'])
            if not rec['solved']: print('SUM MISMATCH', design, page, mark, tot_h, lst, file=sys.stderr)
        desc = b['desc']
        m = re.match(r"^(\d+)'-(\d+)", b['len'])
        listed = (int(m.group(1)) * 12 + int(m.group(2))) if m else None
        mm = re.search(r"(\d+)/(\d+)", b['len'])
        cands = find_model(unit, mark, desc)
        row = dict(design=design, page=page, unit=unit, mark=mark, desc=desc, size=b['raw'] and re.search(r'(#\d+)', b['raw']) and '', count=b['count'], len=b['len'], listed=listed)
        # model candidate: closest length
        best = None
        if cands and listed:
            best = min(cands, key=lambda c: abs(c['length'] - listed))
        elif cands: best = cands[0]
        row['model'] = best
        row['rec'] = rec
        out.append(row)

lines = []
P = lines.append
tot = collections.Counter()
detail = []
for key, rowlist in unitrows.items():
    design, page, sheet, unit = key
    tbl = []
    for row in rowlist:
        rec = row['rec']; best = row['model']
        listed = row['listed']
        if rec is None or not (rec.get('used') or rec.get('radii') or rec['pins']):
            # straight bar: length only
            if best is None:
                tbl.append((row['mark'], row['desc'], 'straight', ftin(listed) if listed else row['len'], '-', 'no model mark', 'not modeled'))
                tot['straight_nomodel'] += 1
            else:
                dl = (best['length'] - listed) if listed else None
                ok = dl is not None and abs(dl) <= 0.5 + 1e-9
                ml = f"{best['code']} {ftin(best['length'])}" + (f" (min {ftin(best['minlen'])})" if best['minlen'] != best['length'] else '')
                if best['code'] == 'BENT' and len(oto_dims(best)[0]) == 1:
                    verdict = 'match'
                    ml += ' (kinks < 12 deg: follows the cap profile)'
                elif best['code'] != 'STR':
                    verdict = 'MISMATCH (model bent)'
                    tot['straight_shape'] += 1
                else:
                    verdict = 'match' if ok else (f'length {dl:+.2f}"' if dl is not None else '?')
                tot['straight_ok' if ok and verdict == 'match' else 'straight_off'] += 1
                tbl.append((row['mark'], row['desc'], 'straight', ftin(listed) if listed else row['len'], ml, f'{dl:+.2f}"' if dl is not None else '', verdict))
            continue
        # bend diagram
        solved = rec.get('solved')
        used = rec.get('used', [])
        pdims = []
        for u in used:
            pdims += [(u['val'], 'v' if u['vert'] else 'h')] * u['mult']
        ptxt = ' + '.join(ftin(v) for v, o in sorted(pdims, key=lambda t: -t[0])) if pdims else '(not reconciled)'
        if pdims and rec.get('hand'): ptxt += ' (read on clip; sum = list length)' if rec.get('sumok') else ' (read on clip)'
        elif pdims: ptxt += ' (sum = list length)'
        if rec['radii']: ptxt += ' ; R ' + ', '.join(ftin(x) for x in rec['radii'])
        if rec['minmax']: ptxt += ' ; ' + ', '.join(f"{ftin(a)} {b}" for a, b, c in rec['minmax'])
        pin = rec['pin']
        ptxt += f' ; D {ftin(pin)}' if pin else ''
        pshape = rec.get('shape') or ('arcs' if rec['radii'] else 'bent')
        if best is None:
            tbl.append((row['mark'], row['desc'], f'{pshape}: {ptxt}', ftin(listed) if listed else row['len'], '-', '', 'not modeled'))
            tot['bend_nomodel_dims'] += len(pdims) + (1 if pin else 0)
            tot['bend_nomodel'] += 1
            continue
        odims, conv = oto_dims(best)
        if not solved or not pdims:
            tbl.append((row['mark'], row['desc'], f'{pshape}: {ptxt}', ftin(listed) if listed else row['len'],
                        f"{best['code']} " + ' + '.join(ftin(v) for v in odims) + (f' ; D {ftin(best["pin"])}' if best['pin'] else ''), '', 'not decoded'))
            tot['dims_notdecoded'] += len(rec.get('raw', [])) + len(rec['radii']) + len(rec['minmax']) + (1 if rec['pin'] else 0)
            tot['marks_notdecoded'] += 1
            continue
        # pair largest-with-largest
        ps = sorted([v for v, o in pdims], reverse=True)
        if best['code'] == 'V': odims = [v for k, v in best['dims'] if k == 'A'] * 2
        if best['code'] in ('TIE', 'CROSSTIE', 'HOOP'):                  # closed shapes: compare distinct values (the model schedules the rectangle / the laps once)
            ps = sorted({v for v, o in pdims}, reverse=True)
            if best['code'] == 'HOOP': odims = sorted(set(odims), reverse=True)
        ms = sorted(odims, reverse=True)
        diffs = []
        ok = True
        for i, pv in enumerate(ps):
            mv = ms[i] if i < len(ms) else None
            if mv is None:
                diffs.append((pv, None, None)); ok = False; tot['dim_beyond'] += 1; tot['dim_total'] += 1
                continue
            d = mv - pv
            good = abs(d) <= tol_for(pv) + 1e-9
            if not good: ok = False
            tot['dim_total'] += 1
            tot['dim_within' if good else 'dim_beyond'] += 1
            diffs.append((pv, mv, d))
        extra = ms[len(ps):]
        if extra:
            ok = False
        # pin
        pinnote = ''
        if pin:
            tot['dim_total'] += 1
            if best['pin'] and abs(best['pin'] - pin) <= 0.25 + 1e-9:
                tot['dim_within'] += 1
            else:
                tot['dim_beyond'] += 1; ok = False
                pinnote = f' pin: plan {ftin(pin)} / model {ftin(best["pin"]) if best["pin"] else "none (straight)"}'
        dtxt = '; '.join(f"{ftin(pv)} vs {ftin(mv) if mv is not None else 'missing'}" + ('' if d is None or abs(d) <= tol_for(pv) else f' ({d:+.2f}")') for pv, mv, d in diffs)
        if extra: dtxt += '; model extra ' + ', '.join(ftin(e) for e in extra)
        dtxt += pinnote
        # reconciliation of the bar-list length (plan)
        tot['marks_solved'] += 1
        verdict = 'match' if ok else 'MISMATCH'
        if best['code'] == 'STR' and len(ps) > 1: verdict = 'MISMATCH (model straight)'
        tot['marks_match' if ok else 'marks_mismatch'] += 1
        cls = re.sub(r'^\d+([a-z])\d+$', r'\1', row['mark'].lower())
        cls_stat[(cls, desc_key := (row['desc'] or '').split(',')[0] + ' ' + ', '.join((row['desc'] or '').split(',')[1:3]).strip())]['match' if ok else 'mismatch'] += 1
        mtxt = f"{best['code']} " + ' + '.join(ftin(v) for v in odims) + (f' ; D {ftin(best["pin"])}' if best['pin'] else '') + ('' if conv == 'OTO' else f' ({conv})')
        tbl.append((row['mark'], row['desc'], f'{pshape}: {ptxt}', ftin(listed) if listed else row['len'], mtxt, dtxt, verdict))
    unitrows[key] = tbl

json.dump(dict(tot=tot), open(W + r'temp\bend\compare.json', 'w'))
P('| statistic | count |')
P('|---|---:|')
for k, v in sorted(tot.items()): P(f'| {k} | {v} |')
P('')
for key, tbl in unitrows.items():
    design, page, sheet, unit = key
    P(f'### {unit} (Design {design}, sheet PDF p.{page})')
    P('')
    P('| mark | plan list name | plan diagram (as printed, out to out) | list length | model shape | differences (plan vs model) | verdict |')
    P('|---|---|---|---|---|---|---|')
    for t in tbl: P('| ' + ' | '.join(str(x).replace('|', '/') for x in t) + ' |')
    P('')
open(OUT, 'w', encoding='utf-8').write('\n'.join(lines))
print('written', OUT, len(lines))
for k, v in sorted(tot.items()): print(k, v)
