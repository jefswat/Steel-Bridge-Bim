import csv,collections
R=[(r['bridge'],float(r['s']),float(r['o']),float(r['z'])) for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\rivets2.csv'))]
M={(r['bridge'],r['mark']):r for r in csv.DictReader(open(r'C:\SBM\qc\ptc\work\g_model.csv'))}
import sys
br=sys.argv[1]; s0,s1,z0,z1=map(float,sys.argv[2:6])
sel=[r for r in R if r[0]==br and s0<=r[1]<=s1 and z0<=r[3]<=z1]
print(len(sel),sorted(collections.Counter(round(r[2],2) for r in sel).items()))
