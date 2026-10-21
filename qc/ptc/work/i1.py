import csv,collections
r=list(csv.DictReader(open(r'C:\SBM\qc\ptc\results\checks.csv',encoding='utf-8')))
g=collections.defaultdict(list)
for x in r:
    if 'recheck' in x['view_table'].lower() or 'recheck' in x['item'].lower() or 'recheck' in x['method'].lower():
        g[(x['page'],x['view_table'][:50])].append(x)
for k,v in g.items():
    c=collections.Counter(y['result'] for y in v)
    print(k,len(v),dict(c),v[0]['id'],v[-1]['id'])
    for y in v:
        if y['result']!='pass': print('   ',y['id'],y['result'],y['item'][:80],'|',y['value_plan'],y['value_model'])
