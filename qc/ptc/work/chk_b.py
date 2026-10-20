import csv,sqlite3,re,collections
from prof import *
from rec import add
c=sqlite3.connect(r'C:\SBM\qc\ptc\inputs\ptc-forcheck.sqlite')
st={}
for g,n,s in c.execute("select bridge_group,name,station_begin from elements where category in ('pier','abutment')"): st[n]=s/0.3048
def stn(x): return '%d+%06.3f'%(int(x//100),x%100)
plan=[ # name, station ft, page, sheet, src
('W Abutment',64621.463,43,'DRB-4 3/68'),('Pier 1',64744.630,43,'DRB-4 3/68'),('Pier 2',64869.046,43,'DRB-4 3/68'),('Pier 3',64993.463,43,'DRB-4 3/68'),('Pier 4',65117.880,43,'DRB-4 3/68'),('Pier 5',65242.297,43,'DRB-4 3/68'),('Pier 6',65366.713,43,'DRB-4 3/68'),('Pier 7',65491.130,43,'DRB-4 3/68'),('Pier 8',65615.547,43,'DRB-4 3/68'),('Pier 9',65739.963,43,'DRB-4 3/68'),('Pier 10 girder brg',65861.651,43,'DRB-4 3/68'),
('Pier 28',70820.797,43,'DRB-4 3/68'),('Pier 29',70945.213,43,'DRB-4 3/68'),('Pier 30',71069.630,43,'DRB-4 3/68'),('Abutment E',71192.797,43,'DRB-4 3/68'),
('Pier 11',66081.880,47,'DRB-4 7/68'),('Pier 12',66297.380,47,'DRB-4 7/68'),('Pier 13 W brg',66512.880,47,'DRB-4 7/68'),('Pier 25',70263.380,47,'DRB-4 7/68'),('Pier 26',70478.880,47,'DRB-4 7/68'),('Pier 27 truss brg',70694.380,47,'DRB-4 7/68'),
('Pier 14',66786.255,57,'DRB-4 17/68'),('Pier 15',67055.630,57,'DRB-4 17/68'),('Pier 16',67325.005,57,'DRB-4 17/68'),('Pier 21',69235.755,57,'DRB-4 17/68'),('Pier 22',69505.130,57,'DRB-4 17/68'),('Pier 23',69774.505,57,'DRB-4 17/68'),
('Pier 18',67939.380,69,'DRB-4 29/68'),('Pier 19',68621.380,69,'DRB-4 29/68'),('Pier 20 main brg',68962.380,69,'DRB-4 29/68'),('Pier 17 W brg',67594.380,57,'DRB-4 17/68'),('Pier 24 W brg',70043.880,57,'DRB-4 17/68')]
rows=[];
keymap={'W Abutment':'W Abutment','Abutment E':'E Abutment'}
for n,p,pg,sh in plan:
    k=keymap.get(n,n)
    m=None
    for kk,v in st.items():
        if kk==k: m=v
    if m is None:
        for kk,v in st.items():
            if kk.startswith(k.split()[0]+' '+k.split()[1]) and kk.split()[1]==k.split()[1]: m=v
    if m is None: rows.append((pg,sh,'support station',n,'sqlite station','','' ,'','0.005 ft','n.a.','model support not found by name')); continue
    d=m-p
    rows.append((pg,sh,'stress sheet / elevation: C pier / brg stations',n+' station',"sqlite station_begin of pier/abutment element",stn(p),stn(m),round(d,3),'0.005 ft','pass' if abs(d)<=0.005 else 'fail','inputs/ptc-forcheck.sqlite'))
for r in rows: print(r[3],r[5],r[6],r[7],r[9])
add(rows)
