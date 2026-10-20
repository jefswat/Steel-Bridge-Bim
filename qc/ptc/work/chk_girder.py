import csv,sqlite3
from prof import *
from rec import add
R=list(csv.DictReader(open('geom.csv')))
# plan p.43 (rows: label, station, gutter El, shoe-bottom El)
W=[('W Abut',64621.463,56.414,41.974),('Pier 1',64744.630,58.831,44.437),('Pier 2',64869.046,61.782,47.381),('Pier 3',64993.463,65.244,50.836),('Pier 4',65117.880,68.975,54.568),('Pier 5',65242.297,72.707,58.300),('Pier 6',65366.713,76.440,62.033),('Pier 7',65491.130,80.172,65.765),('Pier 8',65615.547,83.905,69.498),('Pier 9',65739.963,87.637,73.230)]
rows=[]
mp=[(float(r['smin'])+float(r['smax']))/2 for r in R]
for lab,sta,gut,shoe in W:
    # masonry plates of girder spans 1-10 within 3 ft of station
    c=[float(r['zmin']) for r in R if 'Girder spans 1-10' in r['bridge'] and r['cat']=='masonry plate' and abs((float(r['smin'])+float(r['smax']))/2-sta)<4.0]
    zm=min(c) if c else None
    rows.append((43,'DRB-4 3/68','Girder elevation G1 shoe bottom','%s bottom of shoe El.'%lab,'min z of masonry plates within 4 ft of support (IFC mesh)',shoe,round(zm,3) if zm else '',round(zm-shoe,3) if zm else '',0.01,'pass' if zm and abs(zm-shoe)<=0.01 else 'fail','work/geom.csv'))
    gm=gutter(sta)
    rows.append((43,'DRB-4 3/68','Girder elevation gutter El.','%s gutter grade El at %s'%(lab,sta),'IFC alignment vertical (CL) minus 0.400 (p.43 note)',gut,round(gm,3),round(gm-gut,3),0.01,'pass' if abs(gm-gut)<=0.01 else 'fail','work/prof.py'))
add(rows)
for r in rows: print(r[3],r[5],r[6],r[7],r[9])
