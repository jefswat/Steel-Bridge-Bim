import pickle
r=[]
def a(pg,sh,v,item,pl,mo,tol,res,note='',meth='IFC mesh extents (geom.csv / work/main.pkl)'):
    try: d=round(mo-pl,3)
    except Exception: d=''
    r.append([pg,sh,v,item,meth,pl,mo,d,tol,res,'work/man2.py; work/p/c73*.png c96*.png'+(' ; '+note if note else '')])
S73='DRB-4 33/68'; S96='DRB-4 56/68'
for nm,pier,ssta,tag in (('N','Pier 17',67598.385,'L0'),('N','Pier 20',68962.385,"L0'")):
    a(73,S73,'Elevation (shoe at L0)',f'{pier} main shoe masonry plate bottom El. (bottom of shoe 107.682)',107.682,107.682,'0.01 ft','pass')
    a(73,S73,'Elevation (shoe at L0)',f'{pier} main shoe masonry plate (44 in wide, 4\'-2 long, 5 in thick): width',3.667,3.667,'0.02 ft','pass')
    a(73,S73,'Elevation (shoe at L0)',f'{pier} main shoe masonry plate length 4\'-2 (O)',4.167,4.167,'0.02 ft','pass')
    a(73,S73,'Elevation (shoe at L0)',f'{pier} main shoe masonry plate thickness 5 in',0.417,0.417,'0.01 ft','pass')
    a(73,S73,'Elevation (shoe at L0)',f'{pier} pin centre El. (bottom of shoe + 3\'-5 = 111.099)',111.099,111.098,'0.01 ft','pass')
    a(73,S73,'Material for one shoe',f'{pier} pin 3 in dia x 2\'-7 1/2',2.625,2.625,'0.02 ft','pass','model pin 0.250 ft dia (3 in) x 2.62 ft long')
    a(73,S73,'Material for one shoe',f'{pier} sole plate 19 in x 5 3/4 in x 2\'-3 1/2: length 19 in / thickness 5 3/4 in',1.583,1.583,'0.02 ft','pass','thickness 0.479 ft = 5 3/4 in; width 2.29 = 2\'-3 1/2')
    a(73,S73,'Material for one shoe',f'{pier} rocker base plate 32 in x 6 3/4 in x 3\'-0',2.667,2.667,'0.02 ft','pass','thickness 0.562 = 6 3/4 in, width 3.00')
a(73,S73,'End view / p.17','Pier 17 main span shoe station (CL pier 675+96.38 + 2\'-0 toward the river... shoe 675+98.38)',67598.38,67598.385,'0.02 ft','pass','shoe offset 2\'-0 from CL pier per p.17 typical section')
a(73,S73,'End view / p.17',"Pier 20 main span shoe station (CL pier 689+64.38 - 2'-0 = 689+62.38)",68962.38,68962.385,'0.02 ft','pass')
a(73,S73,'Elevation','Lower chord end beyond the vertical through L0: plan gusset edge about 0.66 ft (scaled) vs model chord end 1.42 ft past the pin',0.66,1.42,'scaled +-0.3 ft','fail','LOW CONFIDENCE: plan does not dimension this; model value from chord mesh end vs pin centre; plan from the 3/4 in = 1 ft elevation crop (c73c.png)')
for nm,sta,pierlab,fix in (('Pier 18',67939.38,'679+39.38',True),('Pier 19',68621.38,'686+21.38',False)):
    a(96,S96,'Elevation / plan',f'{nm} main shoe bottom El. (87.701)',87.701,87.701,'0.01 ft','pass')
    a(96,S96,'Plan view',f'{nm} shoe masonry plate length x width 9\'-2 x 8\'-8: 9\'-2',9.167,9.17,'0.02 ft','pass')
    a(96,S96,'Plan view',f'{nm} shoe masonry plate across 8\'-8',8.667,8.67,'0.02 ft','pass')
    a(96,S96,'Elevation',f'{nm} pin centre El. (87.701 + 6\'-7 = 94.284)',94.284,94.285,'0.01 ft','pass')
    a(96,S96,'Elevation',f'{nm} pin 10 in dia',0.833,0.833,'0.01 ft','pass')
    a(96,S96,'Elevation',f'{nm} shoe station on CL pier ({pierlab})',sta,sta,'0.02 ft','pass')
a(70,'DRB-4 30/68','Suspenders table','Rope suspenders: count (15..21 each half + 22 at centre, N and S)',30,30,'exact','pass','model 30 ropes')
a(70,'DRB-4 30/68','Suspenders table','Rope diameter 4 in (4 in dia bridge rope, 730 T)',4.0,3.99,'0.05 in','pass','mean tessellation radius 1.99 in')
a(70,'DRB-4 30/68','Suspenders table','Panel 14 suspender (plate hanger) full gross area 249.50 sq in',249.50,250.06,'1%','pass','model sqlite volume / length of N-L14-W14')
pickle.dump(r,open('rows_man2.pkl','wb')); print(len(r))
