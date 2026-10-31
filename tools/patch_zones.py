# Append ";Z=n1,n2,..;M=margin" (hairpin zones: stations per zone for this mark, left to right; margin in inches) to the CapHairpin lines.
import json, re
W = r'C:\SBM\.worktrees\bends' + '\\'
Z = json.load(open(W + r'temp\bend\zones.json'))
byp = {}
for k, v in Z.items():
    m = re.match(r'PIER NO\. (\w+) DETAILS', v['sheet'])
    if m and v['z'] and v['z']['ok']:
        z = v['z']
        row = [int(t[2]) for t in z['row']]
        margin = row[0] if row else None
        byp[('Pier ' + m.group(1))] = (z['seq'], margin)

total = 0
for f in ('pier-rebar-i80.csx', 'pier-rebar-ramp-g.csx'):
    path = W + 'samples\\plans\\' + f
    src = open(path, encoding='utf-8', newline='').read()
    out = []
    pier = None
    for line in src.split('\n'):
        m = re.match(r'\s*\["(Pier [^"]+)"\] = \(', line)
        if m: pier = m.group(1)
        if pier in byp and 'CapHairpin' in line:
            seq, margin = byp[pier]
            def rep(mm):
                global total
                s = mm.group(1)
                t = s.split(';')
                if t[1] != 'CapHairpin' or any(x.startswith('Z=') for x in t) or not any(x.startswith('S=U') for x in t): return mm.group(0)
                mk = t[0].lower()
                zs = [k // 2 if m2 == mk else 0 for m2, k in seq]
                if sum(zs) * 2 != int(t[3]): return mm.group(0)
                total += 1
                return '"' + s + ';Z=' + ','.join(str(x) for x in zs) + ';M=' + str(margin if margin else 4) + '"'
            line = re.sub(r'"([0-9]+[a-z][0-9]+;CapHairpin;[^"]*?)"', rep, line)
        out.append(line)
    open(path, 'w', encoding='utf-8', newline='').write('\n'.join(out))
print('zone fields', total)
