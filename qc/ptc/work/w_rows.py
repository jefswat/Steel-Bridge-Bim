import sys
sys.path.insert(0,r'C:\SBM\qc\ptc\work')
from rec4 import add
# railing p.108
add(108,'DRB-4 68/68','Elevation - parapet railing at W / E abutment','First rail panel at the abutment on the girder span: 5 @ 5\'-11 1/2" = 29\'-9 1/2" (ft)','IFC railing posts RAIL-LEFT/RIGHT-PARAPET-RAILING-P1..P6 (centres along the alignment): 5 equal spaces of 5.706 ft (uniform 5.71 along the whole run)',29.7917,28.53,0.05,evidence='work/q8.py; p.108 crop (W and E abutment elevations)')
add(108,'DRB-4 68/68','Elevation - parapet railing','Post spacing at the abutment panels: 5\'-11 1/2" (ft)','IFC post centre spacing (229 of 229 spacings in spans 1-10 are 5.71; 93 of 93 in spans 28-31 are 5.67)',5.9583,5.71,0.02,evidence='work/q8.py')
# walkways
S101='DRB-4 61/68'
add(101,S101,'Material for hand railing','Walkway handrail top rail L 3x3x5/16: area (sq in) (AISC 1.78)','registry mean section area of the walkway top rails (all three bridges)',1.78,1.78,0.02,evidence='work/q12.py')
add(101,S101,'Material for hand railing','Walkway handrail mid rail L 3x2 1/2x5/16: area (sq in) (AISC 1.62)','registry mean section area of the walkway mid rails',1.62,1.62,0.02,evidence='work/q12.py')
add(101,S101,'Material for walkways','Walkway stringers 10" channel @ 15.3 lb/ft: area (sq in) (AISC C10x15.3 = 4.48)','registry mean area of walkway channels',4.48,4.46,0.045,evidence='work/q12.py')
add(101,S101,'Material for walkways','Bearing bar grating depth 3/4" (in)','registry grating section (width 23.52 in x 0.75 in)',0.75,0.75,0.01,evidence='work/q12.py')
add(101,S101,'Material for hand railing','Handrail post spacing 6\'-0 (approach 3-span CL walkway) (ft)','IFC connected posts: 108 posts, 107 spacings',6.0,6.0,0.05,evidence='work/q13.py')
add(101,S101,'Material for hand railing','Handrail post spacing 6\'-0 (approach 4-span CL walkway) (ft)','IFC connected posts: 180 posts, 179 spacings',6.0,6.0,0.05,evidence='work/q13.py')
add(103,'DRB-4 63/68','Material for hand railing (main span)','Handrail post spacing 6\'-0 (main span CL walkway W15-W15\') (ft)','IFC connected posts: 73 posts, 72 spacings',6.0,6.0,0.05,evidence='work/q13.py')
add(103,'DRB-4 63/68','Key plan walkway at bottom strut','22\'-0 walkway at top of bottom strut (L11) (ft)','IFC walkway on strut L11 grating o -11..+11',22.0,22.0,0.02,evidence='work/q4.py')
add(103,'DRB-4 63/68','Key plan walkway at floor beam','Walkway at floor beam U11-L11: length across the bridge (ft). LOW CONFIDENCE: the key plan draws it from the north ladder to about the bridge CL, not dimensioned (scaled 7.53 px/ft from the 87\'-0 c.c. trusses: 40.9 ft); the model walkway runs the full width o -40..+40','IFC walkway at FB11 grating (80.0 ft)',40.9,80.0,3.0,evidence='p.103 key plan crop; work/q4.py')
add(103,'DRB-4 63/68','Key plan','Trusses c.c. 87\'-0 (ft)','IFC floor-lateral wind-chord and chord offsets (chords at o = +-43.5)',87.0,87.0,0.02,evidence='work/q4.py')
# dams p.91 (Pier 20)
S91='DRB-4 51/68'
add(91,S91,'Plan of dam at Pier 20','Dam width curb to curb 2 x 38\'-3 = 76\'-6 (ft)','IFC dam plates o -38.25..+38.25',76.5,76.5,0.02,evidence='work/q10.py')
add(91,S91,'Plan of dam at Pier 20','Teeth pitch 4 1/4" (in): 108 @ 4 1/4" = 38\'-3"','IFC tooth + gap spacing along the dam (2.0 + 2.25 in)',4.25,4.25,0.02,evidence='work/q11.py')
add(91,S91,'Plan of dam at Pier 20','Teeth per half (108 @ 4 1/4") x 2 halves = 216 per comb','IFC unique tooth tip vertices (432 / 2) of "teeth back" and "teeth ahead"',216,216,0,evidence='work/q11.py')
add(91,S91,'Enlarged detail of tooth','Tooth length 1\'-8 3/4" (in). LOW CONFIDENCE: the printed length may exclude the part over the plate','IFC "dam Pier 20 teeth back" extent along the alignment = 2.625 ft',20.75,31.5,0.25,evidence='work/q10.py; p.91 crop')
add(91,S91,'Typical flame cutting dimensions thru teeth','Tooth plate thickness 3 1/2" (in). LOW CONFIDENCE: section shows 3 1/2 in depth; model vertical extent includes the taper / support','IFC "dam Pier 20 teeth back" vertical extent 8.28 in',3.5,8.28,0.25,evidence='work/q11.py; p.91 crop')
# abutment p.12
S12='DRB-2 9/11'
add(12,S12,'Section E-E','W abutment footing thickness 4\'-6" (ft)','IFC footing z 16.00..20.50',4.5,4.5,0.01,evidence='work/q14.py')
add(12,S12,'Section E-E','W abutment footing top EL 20.5 (ft)','IFC footing zmax',20.5,20.5,0.01,evidence='work/q14.py')
add(12,S12,'Plan of pedestal / Section H-H','W abutment pedestal final top EL 41.974 (ft)','IFC pedestal zmax 41.97 (41.974 to 3 decimals in the mesh)',41.974,41.97,0.01,evidence='work/q14.py')
print('ok')
