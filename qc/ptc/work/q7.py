import json,sys
d=json.load(open(r'C:\SBM\qc\ptc\work\fb.json'))
for qi,name in ((0,'FB13'),(1,'FB16'),(2,'FB5')):
    q=d[qi]
    for e in q['elements']:
        el=e['element']; nm=el if isinstance(el,str) else el.get('mark')
        if nm==name:
            print(name,e['category'])
            for lp in e['loops']:
                print('  loop area %.2f'%lp['areaIn2'],'min',[round(x,2) for x in lp['min']],'max',[round(x,2) for x in lp['max']])
