import re,sys
cur=None;ray=None;res=[]
hits=[]
def flush():
    if ray is None: return
    truss=[h for h in hits if 'SHOE' not in h[2] and not h[2].startswith('sub:')]
    shoe=[h for h in hits if 'SHOE' in h[2]]
    sub=[h for h in hits if h[2].startswith('sub:')]
    tb=min((h[0] for h in truss),default=None); st=max((h[1] for h in shoe),default=None)
    sb=min((h[0] for h in shoe),default=None); pt=max((h[1] for h in sub),default=None)
    tw=[h[2].split(':')[1].split(' ')[0] for h in truss if h[0]==tb]
    sw=[h[2].split(':')[1].split(' ')[0] for h in shoe if h[1]==st]
    res.append((cur,ray,None if tb is None or st is None else tb-st, None if sb is None or pt is None else sb-pt, tw, sw))
for line in open('out/shoes.txt',encoding='utf-8'):
    m=re.match(r'##### SHOE (\S+)',line)
    if m: flush(); cur=m.group(1); ray=None; hits=[]; continue
    if line.startswith('#####'): flush(); ray=None; break
    m=re.match(r'\s+-- ray (.*) in',line)
    if m: flush(); ray=m.group(1); hits=[]; continue
    m=re.match(r'\s+z \S+ ft\s+\(rel WP ([-\d.]+)\.\.([-\d.]+) in\)(?: gap below previous = \S+ in)?\s+(.*)',line)
    if m and ray: hits.append((float(m.group(1)),float(m.group(2)),m.group(3)))
flush()
for r in res:
    g='n/a' if r[2] is None else f'{r[2]:7.3f}'; g2='n/a' if r[3] is None else f'{r[3]:7.3f}'
    print(f'{r[0]:7s} {r[1]:22s} truss->shoe {g}  shoe->pier {g2}  {r[4][:2]} / {r[5][:2]}')
