import csv
p=r'C:\SBM\qc\ptc\results\checks.csv'
rows=list(csv.reader(open(p,encoding='utf-8')))
for r in rows:
    if r[0]=='C1711':
        r[5]+=' ; NOTE: the plan height is in the drawing frame, the model bbox is axis-aligned in (s,z); the L12 chord is steeply inclined so the two differ - comparison not valid, area row (72.17 vs 72.16 sq ft) used instead'
        r[8]=''; r[10]='n.a.'
csv.writer(open(p,'w',newline='',encoding='utf-8')).writerows(rows)
