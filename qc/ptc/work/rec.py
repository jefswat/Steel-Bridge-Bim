import csv,os
F=r'C:\SBM\qc\ptc\results\checks.csv'
H=['id','page','sheet','view_table','item','method','value_plan','value_model','diff','tolerance','result','evidence']
def add(rows):
    new=not os.path.exists(F)
    ex=0
    if not new: ex=sum(1 for _ in open(F))-1
    w=csv.writer(open(F,'a',newline=''))
    if new: w.writerow(H)
    for i,r in enumerate(rows):
        w.writerow(['C%04d'%(ex+i+1)]+list(r))
