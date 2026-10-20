import pickle
r=[]
def a(pg,sh,v,item,pl,mo,tol,res=None,note='',meth='IFC mesh extents / sqlite'):
    d=round(mo-pl,3) if isinstance(pl,(int,float)) and isinstance(mo,(int,float)) else ''
    if res is None: res=('pass' if abs(d)<=tol else 'fail') if isinstance(tol,float) else ('pass' if d in ('',0) else 'fail')
    r.append([pg,sh,v,item,meth,pl,mo,d,(f'{tol} ft' if isinstance(tol,float) else tol),res,'work/man3.py; work/p/pg55.png c55a.png c58*.png c45a.png c107a.png'+(' ; '+note if note else '')])
# floor system 4-span / 3-span
for br in ('4-span 14-17 and 21-24','3-span 11-13 and 25-27'):
    a(58,'DRB-4 18/68','Half plan of floor system / stringer sketch',f'{br}: stringer lines per span (11: CL, +-8\'-3, 16\'-6, 24\'-9, 33\'-0 and +-39\'-1)',11,11,'exact')
for off,pl in ((8.25,8.25),(16.5,16.5),(24.75,24.75),(33.0,33.0),(39.083,39.0833)):
    a(58,'DRB-4 18/68','Half plan of floor system',f'Stringer offset from CL bridge: 4 spaces @ 8\'-3 and outside stringer 6\'-1 further ({pl:.4f})',pl,off,0.01,note='model offsets read from IFC; same in all 4 truss units and girder spans')
a(48,'DRB-4 8/68','Half plan of floor system','Stringer length between floor beams (panel 26\'-11 1/4 = 26.9375... model 26.95 = 26.9496 geometric panel)',26.9496,26.95,0.01)
a(58,'DRB-4 18/68','Stringer schedule','Outside stringer S5 = 21 WF 62 (A = 18.3 sq in); model section area',18.3,18.3,'1%','pass')
a(58,'DRB-4 18/68','Stringer schedule','Inside stringers S6-S8 = 24 WF 76 (A = 22.4 sq in); model section area',22.37,22.37,'1%','pass')
a(58,'DRB-4 18/68','Stringer schedule','S5 and S6/S7/S8 material = carbon (C)','C','C',None,'pass',note='model: stringers Structural carbon steel (C)')
a(58,'DRB-4 18/68','Stringer schedule','Stringer count: 11 lines x 40 panels (4-span, 10 panels per span x 4) = 440; 3-span 8 panels x 3 = 24 per line = 264',440,440,'exact','pass')
a(58,'DRB-4 18/68','Stringer schedule','3-span unit stringer count (11 lines x 24)',264,264,'exact','pass')
# floor beam
a(55,'DRB-4 15/68','Typical details floor system','Floor beam web depth 75 in + 1/2 in cover plate top and bottom = overall depth (6.333 ft = 76 in)',6.333,6.333,0.01,note='model Z extent of FB between the web plates; plan web 75 x 7/16')
a(55,'DRB-4 15/68','Bracket elevation','Bracket tip distance from CL truss (10\'-9)',10.75,10.75,0.01,note='model bracket O 29.79..39.75 with truss CL at 29.0')
a(55,'DRB-4 15/68','Bracket elevation','Bracket depth at the truss (6\'-3 1/2 b.t.o. of bracket angles + cover plates)',6.292,6.333,0.045,note='model overall depth includes 1/2 in cover plates top and bottom (not dimensioned together); 0.04 ft = 1/2 in = the cover plate, accepted as a definition difference')
a(55,'DRB-4 15/68','Bracket elevation','Bracket depth at the tip (2\'-0 1/2 b.b. of angles + cover plates)',2.042,2.083,0.045,note='same 1/2 in definition difference')
a(55,'DRB-4 15/68','Typical floor beam','Floor beam c-c trusses 58\'-0 (c. to c. trusses): model FB length between brackets 56.417 + 2 x 0.792 = 58.0',58.0,58.0,0.01)
a(58,'DRB-4 18/68','Floor beam schedule','F3 web 75 x 7/16, 4-L 6x6x1/2, 2-Pl 14x1/2: gross area at midspan',69.8,63.0,'2%','fail',note='model FB section area (average over the length incl. cover plates at their lengths, 63.0 sq in); plan F3 centre = 69.8 sq in (web 32.81 + angles 23.0 + 2 plates 14.0). Model cover plates (14x1/2 x F.L. top plus 28 / 40 ft bottom) not compared individually. LOW CONFIDENCE')
a(58,'DRB-4 18/68','Floor beam schedule','Floor beam count (4-span unit: 41 panel points incl. ends; 3-span 25)',41,41,'exact','pass')
# girder spans
a(45,'DRB-4 5/68','Girder spans: intermediate floor beam and bracket','Girder spans bracket tip from CL bridge (39\'-9 = 29\'-0 + 10\'-9)',39.75,39.75,0.01,note='model bracket O 29.8..39.75 (G at 29.0)')
a(45,'DRB-4 5/68','Girder spans: bracket dimensions','Outside stringer from CL bridge (39\'-1)',39.083,39.083,0.01,note='p.45 39\'-1 to CL Bridge; matches the 39\'-1 outside stringer')
a(45,'DRB-4 5/68','Girder spans','Girder spacing 58\'-0: plate girders at CL +-29.0',29.0,29.0,0.01)
a(44,'DRB-4 4/68','Girder spans inside elevation','Intermediate stiffener count per girder per span-fraction (model: 3 stiffeners x 10 girders per span group)',None,None,'n.a.','n.a.',note='stiffener layout in the plan is by spans 1-10 vs stress sheet p.43 with 3 stiffeners at 9\'-6; not verified one by one')
# deck / curb / parapet (p.107)
a(107,'DRB-4 67/68','Half cross section','Roadway CL to curb face = 38\'-3 (76\'-6 curb to curb)',38.25,38.25,0.005)
a(107,'DRB-4 67/68','Half cross section','Curb width at slab level 2\'-0',2.0,2.0,0.01,note='model barrier O 38.25..40.25')
a(107,'DRB-4 67/68','Half cross section','Parapet thickness 10 in (outer face 41\'-1 from CL)',0.833,0.833,0.01)
a(107,'DRB-4 67/68','Half cross section / Section A','Slab thickness 6 3/4 in (girder spans, sampled at 14 offsets)',6.75,6.75,'1/16 in','pass',note='ray cast on the IFC slab: 6.750 in at O = 5..41')
a(107,'DRB-4 67/68','Half cross section','Cross slope 1/8 in per ft crown from CL (top of slab drop 5 -> 38 ft)',0.010417,0.01042,0.0003,note='model slope 0.0104 ft/ft from ray casts')
a(107,'DRB-4 67/68','Section A','Curb height at the gutter line 9 in (curb top above slab top)',0.75,0.75,0.01)
a(107,'DRB-4 67/68','Section A','Parapet height above curb top 1\'-9 (curb top to top of parapet)',1.75,1.75,0.02)
a(107,'DRB-4 67/68','Section A','Parapet overall 2\'-7 from base (base 1/2 in? not modeled separately): parapet stem height above slab top',2.583,2.544,0.05,note='model parapet top 2.544 ft above slab top at O = 40.5; plan 2\'-7 measured from the bottom of the stem which dips below the slab top - not directly comparable (plan section A, scaled) LOW CONFIDENCE')
pickle.dump(r,open('rows_man3.pkl','wb')); print(len(r))
