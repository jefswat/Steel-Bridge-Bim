import pickle
M='IFC pile head/tip vertices (work/piles.pkl)'
r=[]
def a(pg,sh,v,item,pl,mo,tol,res,note='',meth=M):
    r.append([pg,sh,v,item,meth,pl,mo,round(mo-pl,3),tol,res,note])
for nm in ('Pier 17 W brg','Pier 20 main brg'):
    a(27,'DRB-1 10/11','Pile location half plan',f'{nm} pile cut-off El. (-6.25)',-6.25,-6.25,'0.01 ft','pass')
    a(27,'DRB-1 10/11','Pile location half plan',f'{nm} pile rows a-k (11 rows @ 3\'-0)',11,11,'exact','pass')
    a(27,'DRB-1 10/11','Pile location half plan',f'{nm} piles per side in column 8 (4\'-0 beyond col 7; rows at +-1.5,4.5,7.5,10.5)',8,7,'exact','fail','plan: 8 piles on rows offset 1\'-6 from the main rows (dim string 3-0,3-0,3-0,1-6,1-6,3-0,3-0,3-0); model: 7 piles on the main 3\'-0 rows (-9..+9)')
    a(27,'DRB-1 10/11','Pile location half plan',f'{nm} piles per side in column 9 (5, rows d-h)',5,5,'exact','pass')
    a(27,'DRB-1 10/11','Pile location half plan',f'{nm} piles per side in columns 10 / 12 / 13 (2 each) and column 11 (1)',7,7,'exact','pass')
    a(27,'DRB-1 10/11','Pile location half plan',f'{nm} columns 1-7 at 1\'-6,3x3\'-6,3x4\'-0 from the cap end: stations of columns 3-7 from CL (52.06..36.56)',52.06,52.06,'0.03 ft','pass','columns 1-2 (anchor piles, battered 1:4) head at El -6.25 is 0.18 ft inside the plan position at the footing bottom: consistent with batter')
    a(27,'DRB-1 10/11','Pile location half plan',f'{nm} total piles (154 + 16 + 10 + 4 + 2 + 4 + 4 + 2 from the plan columns)',196,194,'exact','fail','difference = column 8 (above); 2 per side')
a(38,'DRB-3 8/10','Pile plan','E abutment pile cut-off El. (51.654)',51.654,51.65,'0.01 ft','pass')
a(38,'DRB-3 8/10','Pile plan','E abutment pile columns (17 columns, spacing chain 6\'-4,6\'-4,5\'-0,5\'-0,3\'-6,5\'-6,5\'-6,5\'-8 from each end, 1\'-6 edge)',17,17,'exact','pass','column stations -42.8 .. 0 .. +42.8 match the chain to 0.1 ft')
a(38,'DRB-3 8/10','Pile plan','E abutment pile count (visual count: 6 piles x 3 outer columns per side + 5 x 11 inner columns)',91,91,'exact','pass','LOW CONFIDENCE: counted by eye on a raster pile plan; rows per column S positions not individually verified')
a(17,'DRB-1 3/11','Elevation','Pier 18/19 pile cut-off El. (-18.25)',-18.25,-18.25,'0.01 ft','pass')
pickle.dump(r,open('rows_man1.pkl','wb')); print(len(r))
