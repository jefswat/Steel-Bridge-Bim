import csv,sys,re
pat=re.compile(sys.argv[1]); br=sys.argv[2]
for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\geom3.csv')):
    if r['bridge']==br and (pat.search(r['name']) or pat.search(r['ifcname'])):
        print((r['ifcname'] or r['name'])[:55],r['cat'],'s %.2f..%.2f o %.2f..%.2f z %.2f..%.2f'%tuple(float(r[k]) for k in('smin','smax','omin','omax','zmin','zmax')))
