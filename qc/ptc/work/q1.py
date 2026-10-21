import csv,collections,sys
R=list(csv.DictReader(open(r'C:\SBM\qc\ptc\work\geom3.csv')))
c=collections.Counter((r['bridge'],r['cat']) for r in R)
for k,v in sorted(c.items()): print(k,v)
