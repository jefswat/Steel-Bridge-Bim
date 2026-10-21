import json
d=json.load(open(r'C:\SBM\qc\ptc\work\fb.json'))
for q in d:
    print(q['plane'] if 'plane' in q else q.get('query'))
    tot=0; rows=[]
    for e in q['elements']:
        a=0
        for lp in e['loops']:
            pts=lp.get('points') or lp.get('pts') or lp
            if isinstance(pts,dict): pts=pts.get('points')
            n=len(pts); s=0
            for i in range(n):
                x0,y0=pts[i][:2]; x1,y1=pts[(i+1)%n][:2]; s+=x0*y1-x1*y0
            a+=s/2 if not lp.get('hole') else s/2
        rows.append((e['category'],round(abs(a),2),e['width'].get('in') if isinstance(e['width'],dict) else e['width'],e['height'].get('in') if isinstance(e['height'],dict) else e['height'], e['element'] if not isinstance(e['element'],dict) else e['element'].get('mark')))
        tot+=abs(a)
    for r in rows:
        if r[1]>0.5: print(r)
    print('total',round(tot,2))
    print(list(q['elements'][0]['loops'][0].keys()) if isinstance(q['elements'][0]['loops'][0],dict) else 'list loops')
