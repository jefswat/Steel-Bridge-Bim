# Hairpin zones of a pier (elevation sheet): "N SPA. @ 1'-4 = ..; k-5c5 BARS" labels, left to right.
import sys, re, json
sys.path.insert(0, r'C:\SBM\.worktrees\bends\tools')
import bendx, plantext
import pymupdf as fitz

W = r'C:\SBM\.worktrees\bends' + '\\'
# A backtick (the sheets' wide-space glyph) may separate "BARS": "8-5c5`BARS" was missed by "\s*" and its right-end zone was put FIRST
# (2WB, 3WB, 7WB, 9WB, 10WB, 13WB; fixed by hand in pier-rebar-i80.csx, cap-cover work 2026-10-11).
LAB = re.compile(r"(\d+)-(\d+[a-z]\d+)(?:[\s`]*BARS)?[\s`]*$", re.I)


def zones(design, barpage, marks, counts):
    """marks: hairpin marks (lower) ; counts: total bars per mark from the bar list. Returns dict or None."""
    d = fitz.open(bendx.PDFS[design])
    best = None
    for pg in (barpage - 1, barpage - 2, barpage - 3):
        items = plantext.texts(d[pg - 1])
        labs = []
        for it in items:
            s = bendx.decode(it['s']).strip()
            m = LAB.search(s)
            if m and m.group(2).lower() in marks and it['b'] == 0:
                labs.append((it['x'], it['y'], m.group(2).lower(), int(m.group(1)), s))
        # multi-line end zone: "k-5c5" and a separate "BARS": handled by the regex (BARS optional)
        if len(labs) >= 2 and (best is None or len(labs) > len(best[1])): best = (pg, labs, items)
    d.close()
    if best is None: return None
    pg, labs, items = best
    labs.sort()
    # left-end zone written with leader notes only: remainder of the bar list counts
    seq = [(m, k) for x, y, m, k, s in labs]
    ys = [y for x, y, m, k, s in labs]
    tot = {}
    for m, k in seq: tot[m] = tot.get(m, 0) + k
    lead = []
    for m in marks:
        rem = counts.get(m, 0) - tot.get(m, 0)
        if rem > 0: lead.append((m, rem))
    if len(lead) > 1 or any(r % 2 for m, r in lead): return dict(page=pg, ok=False, seq=seq, lead=lead)
    # does the remainder zone sit at the left or right end? (labelled leaders on the left in the I-80 sheets): put it first unless a right-end label exists
    # where the remainder zone sits: x of its plain leader labels (text == mark) on the label row
    seq2 = list(seq)
    for m, r in lead:
        xs = [it['x'] for it in items if bendx.decode(it['s']).strip().lower() == m and it['b'] == 0 and min(ys) - 5 <= it['y'] <= max(ys) + 70]
        lx = sum(xs) / len(xs) if xs else -1
        pos = sum(1 for x, y, mm, k, s in labs if x < lx) if xs else 0
        seq2.insert(pos, (m, r))
    ok = all(counts.get(m, 0) == sum(k for mm, k in seq2 if mm == m) for m in marks) and all(k % 2 == 0 for m, k in seq2)
    # margins: single/double digit integers on the label row
    ys = [y for x, y, m, k, s in labs]
    row = [it for it in items if min(ys) - 22 <= it['y'] <= max(ys) + 3 and bendx.decode(it['s']).strip().isdigit() and len(bendx.decode(it['s']).strip()) <= 2]
    return dict(page=pg, ok=ok, seq=seq2, lead=lead, row=[(round(it['x']), round(it['y']), bendx.decode(it['s']).strip()) for it in sorted(row, key=lambda t: t['x'])])


if __name__ == '__main__':
    R = json.load(open(W + r'temp\bend\plan_shapes.json'))
    out = {}
    piers = {}
    for r in R:
        if 'PIER' not in r['sheet'] or r['desc'] and 'HAIRPIN' not in r['desc'].upper() and 'HAIRPINS' not in r['desc'].upper(): continue
        if not r['desc'] or 'HAIRPIN' not in r['desc'].upper() or 'CAP' not in r['desc'].upper(): continue
        key = (r['design'], r['page'], r['sheet'])
        piers.setdefault(key, {})[r['mark'].lower()] = int(r['count'] or 0)
    for (design, page, sheet), cnt in piers.items():
        z = zones(design, page, set(cnt), cnt)
        out[f'{design}:{page}'] = dict(sheet=sheet, counts=cnt, z=z)
        print(sheet[:14], cnt, 'ok' if z and z['ok'] else 'FAIL', z and z['seq'], z and z.get('row'))
    json.dump(out, open(W + r'temp\bend\zones.json', 'w'), indent=0)
