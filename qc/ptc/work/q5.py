import csv,sqlite3,collections
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
A={m:(v/0.3048**3*144/(l/0.3048)) for m,v,l in c.execute("select mark,volume_m3,length_m from elements where bridge_group='PTC Arch truss 18-20' and category='stringer'")}
rows=[r for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\geom3.csv')) if r['bridge']=='PTC Arch truss 18-20' and r['cat']=='stringer']
# panel index by s from L0 s=67598 (31 ft panels)
tab=collections.defaultdict(dict)
for r in rows:
    o=(float(r['omin'])+float(r['omax']))/2; s=(float(r['smin'])+float(r['smax']))/2
    p=int((s-67598)//31)
    tab[round(o,2)][p]=round(A[r['mark']],1)
for o in sorted(tab):
    d=tab[o]; print(o,[d.get(p) for p in (0,1,2,3,10,11,12,21,22,30,31,32,33,43)])
print(sorted(set(a for d in tab.values() for a in d.values())))
