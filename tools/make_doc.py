# Assemble docs/plan-data/bend-diagram-comparison.md from compare.md + compare.json + the containment counts (before / after).
import re, json, collections, subprocess, sys
W = r'C:\Users\jefsw\OneDrive\Desktop\Steel Bridge Modeler\.worktrees\bends' + '\\'


def cont(path):
    bars = collections.Counter(); units = collections.Counter()
    for line in open(path, encoding='utf-8', errors='replace'):
        if not line.startswith('D|') or 'clear-cover' not in line: continue
        m = re.search(r'envelope.*?: (.*?) - kept', line)
        for part in m.group(1).split('; '):
            mm = re.match(r'(.*?) x(\d+) worst', part)
            k = mm.group(1)
            k = k.replace('5a14 end vertical', '5a14 end bars').replace('6a15 end vertical', '6a15 end bars')
            bars[k] += int(mm.group(2)); units[k] += 1
    return bars, units


b0, u0 = cont(W + r'temp\bend\dump0.txt')
b1, u1 = cont(W + r'temp\bend\dump1.txt')
bm, um = cont(W + r'temp\bend\dump_base_newrule.txt')
keys = sorted(set(b0) | set(b1) | set(bm), key=lambda k: -(b1.get(k, 0) + b0.get(k, 0) + bm.get(k, 0)))
tot = json.load(open(W + r'temp\bend\compare.json'))['tot']
T = tot
header = open(W + r'temp\bend\header.md', encoding='utf-8').read()
cl = ['| bar kind (plan mark, what it is) | A: before (old shapes, old check) | B: old shapes, check with the relief fix | C: after (plan shapes + relief fix) |', '|---|---:|---:|---:|']
for k in keys:
    cl.append(f'| {k} | {b0.get(k, 0)} ({u0.get(k, 0)}) | {bm.get(k, 0)} ({um.get(k, 0)}) | {b1.get(k, 0)} ({u1.get(k, 0)}) |')
cl.append(f'| **total** | **{sum(b0.values())}** | **{sum(bm.values())}** | **{sum(b1.values())}** |')
cmp_md = open(W + r'temp\bend\compare.md', encoding='utf-8').read()
# drop the raw statistics table at the top of compare.md (the header has its own summary)
cmp_md = cmp_md[cmp_md.index('### '):]
doc = header.format(
    dim_total=T['dim_total'], dim_within=T['dim_within'], dim_beyond=T['dim_beyond'], dim_notdec=T['dims_notdecoded'], dim_nomodel=T['bend_nomodel_dims'],
    marks_solved=T['marks_solved'], marks_match=T['marks_match'], marks_mismatch=T['marks_mismatch'], marks_notdec=T['marks_notdecoded'], marks_nomodel=T['bend_nomodel'],
    str_ok=T['straight_ok'], str_off=T['straight_off'], str_nomodel=T['straight_nomodel'],
    cont_table='\n'.join(cl), before=sum(b0.values()), after=sum(b1.values()), mid=sum(bm.values()), tables=cmp_md)
open(W + r'docs\plan-data\bend-diagram-comparison.md', 'w', encoding='utf-8', newline='\n').write(doc)
print(len(doc), sum(b0.values()), sum(b1.values()))
