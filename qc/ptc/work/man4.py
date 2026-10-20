import pickle
r=[]
def a(pg,sh,v,item,pl,mo,tol,res=None,note=''):
    d=round(mo-pl,3) if isinstance(pl,(int,float)) and isinstance(mo,(int,float)) else ''
    if res is None: res='pass' if abs(d)<=tol else 'fail'
    r.append([pg,sh,v,item,'IFC mesh extents (work/dam.pkl, geom.csv)',pl,mo,d,(f'{tol} ft' if isinstance(tol,float) else tol),res,'work/man4.py; work/p/pg46.png'+(' ; '+note if note else '')])
a(46,'DRB-4 6/68','Dam plan (108 @ 4 1/2 in = 38\'-3)','Girder-span dam half length each side of CL bridge (38\'-3 to the curb face)',38.25,38.25,0.01)
a(46,'DRB-4 6/68','Section A','Girder-span dam: floor beams 2\'-6 c.c. across the joint (model back plate face to ahead plate face, Pier 1: 64743.38..64745.88)',2.5,2.5,0.01,note='dam plates +/- 1.25 ft about the CL joint')
a(46,'DRB-4 6/68','Section A','Dam plate thickness / width 0.91 ft in model (plate + toe) vs plan: not dimensioned on the crop read',None,0.91,'n.a.','n.a.',note='plan shows 12 in [35 channels + 3/4 in bulkhead plate; thickness-wise comparison not possible at this resolution')
a(46,'DRB-4 6/68','Plan','Teeth: 18 teeth ? (finger dam; plan shows ~4 1/4 in pitch (108 @ 4 1/4... printed 4 1/4"))',None,None,'n.a.','n.a.',note='pitch printed 4 1/4 in; model teeth 0.56 ft deep: not verified (raster, glyphs too small)')
a(46,'DRB-4 6/68','Dam count','Expansion dams on the girder-span piers 1-9, 28, 29, 30: 12 dams',12,12,'exact','pass',note='model has dams at piers 1..9, 28, 29, 30 (4 plates each)')
a(54,'DRB-4 14/68','Title / index','Dams at piers 10 and 27; model has 6 parts each (2 plates, 2 supports, 2 teeth)',2,2,'exact','pass')
a(68,'DRB-4 28/68','Title / index','Dams at piers 13, 17, 24 (modelled); plus main span dam at pier 20 (p.91) and W22 (p.92)',5,5,'exact','pass',note='model dam names: Pier 13, 17, 24, 20 and W22 (+10, 27 on p.54)')
pickle.dump(r,open('rows_man4.pkl','wb')); print(len(r))
