"""Re-check analysis of out/ (recheck.csx). python -I ana.py <out dir>"""
import csv, collections, re, sys, os
D = sys.argv[1]
def rd(f): return list(csv.DictReader(open(os.path.join(D, f), encoding='utf-8', errors='replace')))
diag = open(os.path.join(D, 'diagnostics.txt'), encoding='utf-8', errors='replace').read().splitlines()
pr = lambda *a: print(*a, flush=True)

# ---- R01: placed vs drawn per member group (N plane, unprimed joints)
drawn = collections.defaultdict(int)
for r in csv.DictReader(open(r'C:\SBM\docs\plan-data\ptc-delaware-river\connections\rivet-counts.csv')):
    if r['structure'] != 'main' or r['member'] == 'unassigned': continue
    k = (r['joint'], frozenset(r['member'].split('-')), r['region'])
    drawn[k] += int(r['total'])
rx = re.compile(r"Truss: joint (\S+) \((\w)\) member (\S+): (\d+) rivets .*? as drawn \((\d+) per (member face beyond the gusset|gusset face)")
placed = collections.defaultdict(int)
for l in diag:
    m = rx.search(l)
    if not m: continue
    j, pl, mk, tot, per, reg = m.groups()
    if pl != 'N' or "'" in j: continue
    name = mk.split('-', 1)[1]
    k = (j, frozenset(name.split('-')), 'beyond' if reg.startswith('member') else 'inside')
    placed[k] += int(per)
td = sum(v for k, v in drawn.items() if k[2] == 'inside'); tp = sum(placed.get(k, 0) for k in drawn if k[2] == 'inside')
tdb = sum(v for k, v in drawn.items() if k[2] == 'beyond'); tpb = sum(placed.get(k, 0) for k in drawn if k[2] == 'beyond')
pr(f"R01 inside: placed {tp} / drawn {td} ({100*(td-tp)/td:.1f} % missing); beyond {tpb} / {tdb}")
diffs = []
for k, v in drawn.items():
    p = placed.get(k, 0)
    if v and abs(v - p) / v > 0.15 and abs(v - p) >= 4: diffs.append((k[0], '-'.join(sorted(k[1])), k[2], v, p))
diffs.sort(key=lambda x: -(x[3] - x[4]))
pr(f"  groups > 15 % off: {len(diffs)}")
for d in diffs[:25]: pr('   ', d)
with open(os.path.join(D, '..', 'rivet_count_diffs.csv'), 'w', newline='') as f:
    w = csv.writer(f); w.writerow(['joint', 'member', 'region', 'drawn_per_face', 'model_per_face']); w.writerows(diffs)
# hanger / wind chord
for k in drawn:
    if 'W14' in k[1]: pr('  W14 group', k[0], sorted(k[1]), k[2], 'drawn', drawn[k], 'placed', placed.get(k, 0))
# dropped
dr = rd('dropped.csv')
c = collections.Counter((r['plane'], r['reason']) for r in dr)
pr('dropped', len(dr), dict(c))
c = collections.Counter((r['point'], r['member']) for r in dr if r['plane'] == 'N' and "'" not in r['point'])
pr('  worst N groups:', c.most_common(12))

# ---- covers (R05)
cv = rd('covers.csv')
short = [r for r in cv if int(r['short']) > 0]
pr(f"R05 covers: faces with short {len(short)} of {len(cv)}; rivets short {sum(int(r['short']) for r in short)}")
neg = sorted(short, key=lambda r: float(r['minedge_in']))[:12]
pr('  tightest:', [(r['plane'], r['point'], r['short'], r['minedge_in']) for r in neg])

# ---- clash
cl = rd('clash.csv')
nb = [r for r in cl if r['kindA'] != 'Bolt' and r['kindB'] != 'Bolt']
bb = [r for r in cl if r['kindA'] == 'Bolt' and r['kindB'] == 'Bolt']
pr(f"clash rows {len(cl)}: non-bolt {len(nb)}, bolt x bolt {len(bb)} (>1/4 in: {sum(float(r['depth_in'])>0.25 for r in bb)})")
for k, v in collections.Counter(r['pair'] for r in nb).most_common(30):
    mx = max(float(r['depth_in']) for r in nb if r['pair'] == k); pr(f'   {v} {k} max {mx:.2f}')
pr('  web x web rows:')
for r in nb:
    if r['pair'] in ('TrussDiagonal x TrussVertical', 'TrussDiagonal x TrussDiagonal', 'TrussVertical x TrussVertical'): pr('    ', r['markA'], r['markB'], r['depth_in'])
pr('  W14 rows:', [(r['markA'], r['markB'], r['depth_in']) for r in nb if 'W14' in r['markA'] + r['markB']][:20])
pr('  gusset rows:', collections.Counter(r['pair'] for r in nb if 'Gusset' in r['pair']).most_common())
# bolt-bolt by location (mark-> part?) : bolt marks have no member; summarize by depth
# rivets through parts
bp = collections.defaultdict(int)
for r in cl:
    if r['kindA'] == 'Bolt' and r['kindB'] != 'Bolt': bp[r['markB']] += 1
    elif r['kindB'] == 'Bolt' and r['kindA'] != 'Bolt': bp[r['markA']] += 1
parts = rd('parts.csv')
def unriveted(pred, label):
    ps = [p for p in parts if pred(p)]
    no = [p['mark'] for p in ps if bp.get(p['mark'], 0) == 0]
    pr(f"{label}: {len(ps)} parts, without a rivet {len(no)}; e.g. {no[:12]}")
    return ps, no
unriveted(lambda p: p['category'] in ('SplicePlate', 'FillerPlate') and '-BG' not in p['mark'] and p['assembly'].startswith('TRUSS'), 'R04 joint splice / fill plates')
unriveted(lambda p: re.search(r'-EA\d*$', p['mark']) and '-BG' not in p['mark'], 'G02 gusset edge angles')
pr('G02 edge angle parts by joint:', collections.Counter(re.sub(r'-(L|R).*', '', p['mark']) for p in parts if re.search(r'-EA\d*$', p['mark']) and '-BG' not in p['mark']).most_common(50))
# categories
pr('categories:', collections.Counter(p['category'] for p in parts).most_common())
# W14 gussets
pr('W14 parts:', [(p['mark'], p['description'][:80]) for p in parts if 'GW14' in p['mark']][:8])
pr('stacks W14/L11/L10/L22:', [r for r in rd('stacks.csv') if r['point'] in ('W14', 'L11', 'L10', 'L22', 'U11') and r['plane'] == 'N'])
# bracing (brace-ends)
be = rd('brace-ends.csv')
no = [r for r in be if r['rivets_conn'] == '0']
pr(f"B: bracing member ends {len(be)}, no connecting rivet {len(no)}:", collections.Counter(r['role'] for r in no))
for r in no: pr('    ', r['member'], r['end'], r['plates_touching'])
br = rd('brace-rivets.csv')
pr('bracing rivets', len(br), collections.Counter(r['class'] for r in br))
# members present
mem = rd('members.csv')
pr('members with 13H/13 1/2:', [r['mark'] for r in mem if '13H' in r['mark'] or 'LQ13' in r['mark'] or 'LM13H' in r['mark']][:20])
pr('WM22 members:', [r['mark'] for r in mem if 'WM22' in r['from'] + r['to']])
pr('portal:', [(r['mark'], r['section']) for r in mem if r['role'] == 'Portal'])
pr('bracing plates at WM22 / portal / L13:', [p['mark'] for p in parts if re.search(r'BG(WM22|PORT|U11|LM13|L13)', p['mark'])][:40])
# diag bracing / rivet checks
for l in diag:
    if re.search(r'CHECK|CONFLICT', l) and re.search(r'rivet|bracing|edge angle|web member|overlap|gusset', l, re.I): pr('  D:', l[:330])
