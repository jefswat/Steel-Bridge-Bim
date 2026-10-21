import csv,sys
for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\geom3.csv')):
    if r['bridge']=='PTC Arch truss 18-20' and r['cat']=='joint armor plate':
        print(r['mark'],r['name'],'s %.2f..%.2f o %.2f..%.2f z %.2f..%.2f'%tuple(float(r[k]) for k in('smin','smax','omin','omax','zmin','zmax')),'ext',r['e1'],r['e2'],r['e3'])
