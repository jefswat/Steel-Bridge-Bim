import csv,collections,numpy as np
R=[r for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\rivets2.csv'))]
M={(r['bridge'],r['mark']):r for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\g_model.csv'))}
for br,mk in (('PTC 3-span truss 11-13','N-GU7-L'),('PTC 3-span truss 11-13','N-GU1-L')):
    g=M[(br,mk)]
    smin,smax,zmin,zmax=[float(g[k]) for k in ('smin','smax','zmin','zmax')]
    sel=[r for r in R if r['bridge']==br and smin<=float(r['s'])<=smax and zmin<=float(r['z'])<=zmax and float(r['o'])>0]
    print(mk,len(sel),sorted(collections.Counter(round(float(r['o']),2) for r in sel).items()))
