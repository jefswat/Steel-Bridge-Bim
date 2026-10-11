import re, sys, collections
tot = collections.Counter(); bars = collections.Counter(); units = collections.Counter(); worst = {}
nunits = 0
for line in open(sys.argv[1], encoding='utf-8', errors='replace'):
    if not line.startswith('D|') or 'clear-cover' not in line: continue
    nunits += 1
    m = re.search(r'envelope.*?: (.*?) - kept', line)
    for part in m.group(1).split('; '):
        mm = re.match(r'(.*?) x(\d+) worst (.*?) at', part)
        if not mm: print('??', part); continue
        k = re.sub(r'^\d+[a-z]\d+ ', '', mm.group(1)); k = re.sub(r'^(\d+[a-z]\d+) ', '', mm.group(1))
        tot[mm.group(1) if False else re.sub(r'^\d+\w\d+ ', '', mm.group(1))] += int(mm.group(2))
        bars[mm.group(1)] += int(mm.group(2)); units[mm.group(1)] += 1
print('units with violations', nunits, 'bars total', sum(bars.values()))
for k, v in bars.most_common(): print(f'{v:6d} bars  {units[k]:3d} units  {k}')
