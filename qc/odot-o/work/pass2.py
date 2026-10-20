# Pass 2 (QC-PLAN 4.1a): element-by-element reconciliation against the passed checks of results/checks.csv
import csv, re, sqlite3, collections
Q = 'C:/SBM/qc/odot-o/'
checks = list(csv.DictReader(open(Q + 'results/checks.csv', encoding='utf-8')))
res = {c['check_id']: c for c in checks}
db = sqlite3.connect('file:' + Q + 'inputs/odot-o.sqlite?mode=ro', uri=True)
els = list(db.execute("select global_id, name, mark, category, ifc_class from elements where in_ifc=1 and level in ('part','element','fastener') and ifc_class<>'IFCBRIDGEPART'"))
# coverage rules: (regex on name/mark, aspect, check ids found by item text)
def cid(substr, page=None):
    out = [c['check_id'] for c in checks if substr in c['item'] and (page is None or str(c['sheet_page']) == str(page))]
    assert out, substr
    return out
R = [  # regex, size checks, position checks
 (r'^G\d-\d-(TF[LR]|BF|W[LR])\d$', cid('plate schedule') + cid('web 2-PL'), cid('CL brg P13 - FS1') + cid('flange break')),
 (r'^FS\d-G\d-SP(7|8|9|10|11|12)$', cid('Web splice 2 PL') + cid('BF splice 2 PL'), cid('FS1 - FS2', 278)),
 (r'^FS\d-G\d-SP[1-6]$', cid('TF splice: 1 PL'), cid('FS1 - FS2', 278)),
 (r'^FS3-G\d-FP\d$', cid('Filler PL'), cid('FS2 - FS3', 278)),
 (r'^B\d-G\d-PAD$', cid('pad '), cid("'H' dimension", None) if False else cid('Top of slab to bottom of bearing') + cid('Horizontal offset 3')),
 (r'^B\d-G\d-BP$', cid('beveled plate'), cid('Top of slab to bottom of bearing')),
 (r'^G\d-\d-TS\d+\.\d+[LR]$', cid('Internal cross frame: top strut'), cid('Lateral / cross-frame spacing')),
 (r'^G\d-K\d+\.\d+-(D\d|TS|G|TAB\d)$', cid('Internal cross frame: top strut'), cid('Lateral / cross-frame spacing')),
 (r'^G\d-T\d+\.\d+-TS$', cid('Internal cross frame: top strut'), cid('Lateral / cross-frame spacing')),
 (r'^XD\d-(W|TF|BF|C[LR]\d)$', cid('External intermediate diaphragm: web'), cid('External intermediate diaphragms: 2 per span')),
 (r'^G\d-SD1-(W|TF|BS\d)$', cid('Interior support diaphragm web PL 3/4, flanges PL 1 x 12'), cid('P13 skew')),
 (r'^G\d-SD4-(W|TF|BS\d)$', cid('Interior web PL 3/4, flanges 1x12'), cid('Top of slab to bottom of bearing')),
 (r'^G\d-SD[23]-(W|TF|BS\d|ST\d)$', cid('Interior web PL 1 1/2'), cid('17\'-8" along CL brg')),
 (r'^XSD1-1-(W|TF|BF|C[LR]\d)$', cid('Exterior support diaphragm web PL 3/4, flanges PL 1 x 12, 2 bent'), cid('P13 skew')),
 (r'^XSD[23]-1-(W|TF|BF|C[LR]\d)$', cid('Interior web PL 1 1/2'), cid('17\'-8" along CL brg')),
 (r'^XSD4-1-(W|TF|BF|C[LR]\d)$', cid('Interior web PL 3/4, flanges 1x12'), cid('Top of slab to bottom of bearing')),
 (r'^G\d-LAT\d-L\d+$', cid('Laterals WT9x38'), cid('Lateral / cross-frame spacing')),
 (r'^G\d-\d-HATCH\d$', cid('Access door PL 3/8'), cid('Access hatch 4')),
 (r'^(DECK|Deck slab)$', cid('Slab thickness 8') + cid('Out-to-out deck width'), cid('Cross slope 6%') + cid('Deck end / expansion joint')),
] + [ (r'shear stud G%d-%d-SS'%(g,k), cid('3 - 7/8" dia x 7" studs'), cid('G%d field section %d shear studs'%(g,k)) + cid('First stud row')) for g in (1,2) for k in range(1,6) ] + [
 (r'bolt FS\d-G\d-B', cid('Bolts per splice'), []),
 (r'anchor bolt', cid('Detail of anchor bolt') if False else [], cid('Anchor bolts: G1 2')),
 (r'^G\d-\d-FW$', cid('Web-to-flange fillet'), []),
]
def status(ids):
    if not ids: return 'none'
    rs = [res[i]['result'] for i in ids]
    if all(r == 'pass' for r in rs): return 'pass'
    if any(r == 'pass' for r in rs): return 'partial(' + ','.join(sorted(set(rs))) + ')'
    return '/'.join(sorted(set(rs)))
rows = []; cnt = collections.Counter()
for gid, name, mark, cat, cls in els:
    key = mark or name or ''
    hit = None
    for rx, s, p in R:
        if re.search(rx, key) or re.search(rx, name or ''):
            hit = (s, p); break
    if hit:
        ss, ps = status(hit[0]), status(hit[1])
        verified = ss == 'pass' and ps == 'pass'
    else:
        ss = ps = 'none'; verified = False
    if cat == 'barrier':
        cnt['n.a. barrier (out of scope but present)'] += 1
        rows.append((gid, cls, cat, key, 'n.a.', 'n.a.', 'out of model scope (barrier) - present in IFC, not checked')); continue
    cnt['verified' if verified else 'unverified'] += 1
    if not verified:
        why = []
        if ss != 'pass': why.append('size: ' + ss)
        if ps != 'pass': why.append('position: ' + ps)
        rows.append((gid, cls, cat, key, ss, ps, '; '.join(why)))
with open(Q + 'results/unverified.csv', 'w', newline='', encoding='utf-8') as f:
    w = csv.writer(f); w.writerow(['global_id', 'ifc_class', 'category', 'mark_or_name', 'size_checks', 'position_checks', 'reason']); w.writerows(rows)
print(cnt)
agg = collections.Counter((r[2], r[6]) for r in rows)
for k, v in sorted(agg.items(), key=lambda x: -x[1]): print(v, k)
# ---- sheet links not used by any check
used = collections.defaultdict(set)
pages_by_cat = collections.defaultdict(set)
for gid, name, mark, cat, cls in els:
    key = mark or name or ''
    for rx, s, p in R:
        if re.search(rx, key) or re.search(rx, name or ''):
            for i in s + p: used[gid].add(str(res[i]['sheet_page']))
            break
link = collections.defaultdict(set)
for gid, page in db.execute("select v.global_id, s.pdf_page from v_element_sheets v join sheets s on s.sheet_id=v.sheet_id"):
    link[gid].add(str(page))
find = collections.Counter(); missing = collections.Counter()
for gid, name, mark, cat, cls in els:
    for p in link.get(gid, set()) - used.get(gid, set()):
        find[(cat, p)] += 1
    for p in used.get(gid, set()) - link.get(gid, set()):
        missing[(cat, p)] += 1
with open(Q + 'results/sheet_link_findings.csv', 'w', newline='', encoding='utf-8') as f:
    w = csv.writer(f); w.writerow(['kind', 'category', 'pdf_page', 'element_count', 'meaning'])
    for (c, p), n in sorted(find.items()): w.writerow(['linked_not_used', c, p, n, 'element claims this sheet but no check of the element used it'])
    for (c, p), n in sorted(missing.items()): w.writerow(['used_not_linked', c, p, n, 'a check of the element used this sheet but the DB link (nearest-ancestor rule) does not include it'])
print('linked_not_used', sum(find.values()), 'used_not_linked', sum(missing.values()))
for k, v in sorted(missing.items(), key=lambda x: -x[1])[:15]: print('  missing link', v, k)
for k, v in sorted(find.items(), key=lambda x: -x[1])[:15]: print('  unused link', v, k)
# checklist per sheet
with open(Q + 'extract/checklist_by_sheet.csv', 'w', newline='', encoding='utf-8') as f:
    w = csv.writer(f); w.writerow(['sheet_page', 'sheet_no', 'view', 'item', 'check_id', 'result'])
    for c in checks: w.writerow([c['sheet_page'], c['sheet_no'], c['view'], c['item'], c['check_id'], c['result']])
