import csv,sys,re
pat=re.compile(sys.argv[1]); br=sys.argv[2] if len(sys.argv)>2 else 'PTC Arch truss 18-20'
for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\geom3.csv')):
    if r['bridge']==br and pat.search(r['mark']):
        print(r['mark'],r['cat'],'s %.1f..%.1f'%(float(r['smin']),float(r['smax'])),'o %.1f..%.1f'%(float(r['omin']),float(r['omax'])),'z %.1f..%.1f'%(float(r['zmin']),float(r['zmax'])),'ext',r['e1'],r['e2'],r['e3'])
