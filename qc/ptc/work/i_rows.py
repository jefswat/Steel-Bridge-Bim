import sys
sys.path.insert(0,r'C:\SBM\qc\ptc\work')
from rec4 import add
add(42,'DRB-4 2/68','Quantities (p.42)','Total structural steel items 1-6 (lb): plan 39,280,000; model = carbon + silicon + cast + forged steel in the registry (excludes HP piling, aluminum rail, grating)','sum of registry mass_kg x 2.20462 for Structural carbon (12,680,531) + silicon (20,815,504) + cast 255,424 + forged 139,960 = 33,891,419 lb',39280000,33891419,392800,evidence='work/i2.py')
add(73,'DRB-4 33/68','Main span joint L0 (p.73)','Bottom chord L0-L2 end beyond the L0 pin (in): drawn gusset / chord end past the pin line, scaled from the overlay (58 px at 3.14 px/in = 18.5 in, +-1 in)','model chord end past the L0 working point = 63.4441 - 62.0254 ft = 17.0 in; overlay evidence/gusset-main-L0.png',18.5,17.0,2.0,evidence='evidence/gusset-main-L0.png (re-reading of C0810)')
print('ok')
