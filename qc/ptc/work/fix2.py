import re
p=r'C:\SBM\qc\ptc\work\r_rows.py'
t=open(p).read()
t=t.replace("SH={50:'DRB-4 10/68',52:'DRB-4 12/68',60:'DRB-4 20/68',61:'DRB-4 21/68',65:'DRB-4 25/68',74:'DRB-4 34/68',75:'DRB-4 35/68',76:'DRB-4 36/68',65:'DRB-4 25/68'}",
"SH={}\nfor r in csv.DictReader(open(r'C:\\SBM\\qc\\ptc\\extract\\sheet_index.csv',encoding='utf-8')): SH[int(r['page'])]=r['dwg']+' '+r['sheet']")
t=t.replace("add(0,'DRB-4 10-67/68'","add('50-89','DRB-4 10/68 .. 49/68'")
open(p,'w').write(t)
