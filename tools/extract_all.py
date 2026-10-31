import sys, json, re
sys.path.insert(0, r'C:\SBM\.worktrees\bends\tools')
import bendx, csv
out = []
for design in ('116', '316'):
    rows = bendx.barlist(design)
    for page, rr in rows.items():
        title = rr[0]['title']
        lens = {}
        for r in rr:
            m = re.match(r"^(\d+)'-(\d+)", r['len'])
            lens[r['mark']] = ((int(m.group(1)) * 12 + int(m.group(2))) if m else None, r['len'], r['desc'], r['count'])
        titles, dl, dims = bendx.analyse2(design, page, *((370, 825) if 'ABUT' in title else (500, 862)), abut='ABUT' in title)
        for t in titles:
            key = t['s'].strip()
            marks = [x.strip() for x in key.split(',')]
            for mk in marks:
                mine = [d for d in dims if d['label'] == key and (d['own'] in (None, mk)) and not (d['own'] and d['own'] != mk)]
                import math as _m
                ds = [dict(val=d['val'], pre=d['pre'], suf=d['suf'], vert=d['b'] != 0, ang=round(_m.degrees(_m.atan2(d['b'], d['a'])), 1), x=round(d['x']), y=round(d['y']), own=d['own']) for d in mine]
                L = lens.get(mk.lower(), lens.get(mk, (None, '', '', '')))
                out.append(dict(design=design, page=page, sheet=title, title=key, mark=mk, listed=L[0], listed_txt=L[1], desc=L[2], count=L[3], dims=ds,
                                recon=bendx.recon(mine, L[0])))
json.dump(out, open(r'C:\SBM\.worktrees\bends\temp\bend\plan_dims.json', 'w'), indent=0)
import collections
c = collections.Counter(o['recon'] for o in out); print(len(out), c)
for o in out:
    if o['recon'] != 'ok': print(o['design'], o['page'], o['sheet'][:14], o['mark'], o['listed_txt'], [(d['val'], d['own'] or '') for d in o['dims'] if not d['pre']])
