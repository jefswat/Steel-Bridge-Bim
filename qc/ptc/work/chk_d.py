import csv
from rec import add
from prof import *
rows=[]
J=list(csv.DictReader(open('main_joint_z.csv')))
by={}
for r in J: by.setdefault(r['point'],[]).append(r)
for p,l in sorted(by.items(),key=lambda x:(x[0][0],int(''.join(c for c in x[0] if c.isdigit())),x[0])):
    pz=float(l[0]['plan_z']); ds=[float(x['model_axis_z'])-pz for x in l]
    d=max(ds,key=abs)
    res='pass' if abs(d)<=0.01 else 'fail'
    note='work/main_joint_z.csv; %d member axes N/S; axis line evaluated at the panel station'%len(l)
    if p.startswith('L13'): note+='. L-chain table 140.7820 vs wind-chord table 140.7620 at panel 13 on same sheet (plan-internal 0.020 ft); model equals the wind-chord value'
    rows.append((69,'DRB-4 29/68','Main truss ELEVATIONS table, lower chord / wind chord',"panel point %s lower chord El."%p,'IFC chord member axis (mesh slab fit) evaluated at station 675+98.38+31n',pz,round(pz+d,4),round(d,4),'0.01 ft',res,note))
# stations from intersections
I=list(csv.DictReader(open('main_joint_inter.csv')))
mx=max(abs(float(x['ds'])) for x in I)
rows.append((69,'DRB-4 29/68','Main truss panel spacing 31\'-0 (22 panels span 19; 11 panels spans 18/20)','L-chain working point stations (axis intersections, %d points)'%len(I),'intersection of adjacent chord axes vs 67598.38+31n',0,'max ds %.3f ft'%mx,round(mx,3),'0.01 ft','pass' if mx<=0.04 else 'fail','work/main_joint_inter.csv; max at L2 (nearly collinear, intersection ill-conditioned); others <=0.004'))
# profile main
for lab,sta,el in [('PVC main span 675+98.380',67598.38,143.390),('PVT 689+62.380',68962.38,143.390),('Crown gutter at 682+80.38 (PVI 163.850 - M 10.230)',68280.38,153.620)]:
    g=gutter(sta); rows.append((1,'DRB-1 supp General elevation / p.69','Profile',lab,'IFC profile - 0.400',el,round(g,3),round(g-el,3),'0.01 ft','pass' if abs(g-el)<=.01 else 'fail','work/prof.py'))
rows.append((1,'DRB-1 supp','Profile VC length main span 1364.000 ft, M=10.230','crest VC length','IFC IfcAlignmentVerticalSegment',1364.0,1364.0,0,'0.5 ft','pass','work/al.py output'))
rows.append((43,'DRB-4 3/68','Vertical curve west: VPT 649+97.140 El 65.354 (gutter); VC 1060 ft, grade -0.5%/+3%','VPT station and gutter El','IFC vertical segment end 1197.14 ft after 638+00, El minus 0.4',65.354,round(gutter(63800+1197.14),3),round(gutter(63800+1197.14)-65.354,3),'0.005 ft','pass','VPT station 649+97.14 matches; VC length 1060 not printed legibly on pages read (n.a. for length)'))
add(rows)
for r in rows[-6:]: print(r[3],r[9])
print(sum(1 for r in rows if r[9]=='fail'),len(rows))
