import sys,csv,collections,json
sys.path.insert(0,r"C:\SBM\qc\nebraska-st\tools")
from qclib import *
# category -> (coverage, reason). coverage: V = verified by checks (size and position), P = partly, U = unverified
R={
'alignment':('V','curve/profile/support stations checked (P06-*, P07-*)'),
'unit':('V','spans, skews, support stations'),
'deck slab':('P','thickness, width, top elevations (603 points), extents checked; haunch shape and 9 in edge thickness not checked; volume -4.7% vs plan quantity'),
'deck':('P','see deck slab'),
'barrier':('P','section area, height, roadway width checked; profile outline (indentations, bump-outs) and barrier plates not checked'),
'girder':('V','plate sizes, plate breaks, span lengths, splice stations, camber tables'),
'girder field section':('V','span / splice distances (P27-*)'),
'flange plate':('P','size and breaks checked for all 4 girders; plate length of 5 transition tapers not checked; cap flanges via section'),
'web plate':('P','size 76x9/16 and depth checked; web lengths between splices / cap checked only through splice locations'),
'splice plate':('V','all 4 splices x 4 girders sized; bolt counts verified'),
'filler plate':('U','fill plates are "as required" on the sheets (p34); no printed size'),
'bolt':('P','splice bolts counted (12 groups x 4 girders); cross frame, cap and bearing-stiffener bolts (about 5,700 of 7,132) not counted'),
'bolt group':('P','as bolt'),
'shear stud':('P','girder stud spacing zones checked (FAIL, spacing 1.0 ft vs plan 1.13-1.22 ft); stud diameter/length (7/8 x 5, 7/8 x 9) not checked; cap stud rows checked (shifted)'),
'stud group':('P','as shear stud'),
'intermediate stiffener':('U','size checked (PL 6 1/2 x 7/16); count 101 and station of each (equal spaces between cross frames, p27 note) not checked'),
'connection stiffener':('U','size checked (PL 7 1/2 x 1/2); positions follow cross frame lines (fail on span 2) not individually checked'),
'bearing stiffener':('P','sizes checked; locations at supports (3 ft 3 in jacking offsets, p27) not checked; Pier 2 bearing/jacking stiffeners (16) only via section'),
'cross frame':('P','counts, member sizes, line positions checked (span 2 FAIL); node geometry (WP offsets, 1 in clearances) not checked'),
'cross frame group':('P','as cross frame'),
'angle':('P','L5x5 sizes checked; L2x2x1/4 end screen angles (4) not checked'),
'channel':('P','W24x68 size checked; classification as channel noted'),
'gusset':('U','gusset PL sizes are printed only as thickness (1/2, 5/8); plate outlines (e.g. 15 5/8 x 17 7/16) not checked'),
'weld':('U','weld sizes (5/16 typ, p32 details) and the 352 modelled weld solids not compared; unsized connection welds flagged UNRESOLVED in the model'),
'weld group':('U','as weld'),
'bearing':('V','14 assemblies: count, top elevation, orientation, anchor bolts'),
'bearing disc':('P','elevation checked; disc diameter (28 in min at Pier 2) not checked'),
'bearing pad':('P','present, 1/8 in not checked'),
'sole plate':('P','top elevation at CL via sole plate checked; plate size (PL 28x1 1/4x16 etc.) and taper not checked against bearing details'),
'masonry plate':('U','plate size (40x1 3/4x22/26) not printed on sheets read; manufacturer designed'),
'anchor bolt':('V','count, diameter, embedment'),
'anchor bolt group':('V','as anchor bolt'),
'footing':('V','Pier 1/2 footings and abutment footings (rectangle only; wing footing missing: FAIL)'),
'footing group':('V','as footing'),
'column':('P','Pier 1 verified; Pier 2 overall length -4 in (FAIL)'),
'column group':('P','as column'),
'pier cap':('P','Pier 1 cap checked; end notches not modelled (FAIL)'),
'cap group':('P','as pier cap'),
'pedestal':('V','tops and plan size at Pier 1/2 and abutment beam steps'),
'maskwall':('P','Pier 1 heights checked; thickness / extent (p10 section D-D) not checked'),
'maskwall group':('P','as maskwall'),
'backwall':('P','bottoms and thickness checked; corbel / paving notch / keyway dims (p18 section) not checked'),
'backwall group':('P','as backwall'),
'abutment':('P','see footing/backwall; wings/maskwalls missing (FAIL)'),
'pile':('P','Pier 1/2 count, length, top elevation, grid positions, batter; abutment piles: count FAIL (12 vs 13/14), batter FAIL, positions not checked'),
'integral steel pier cap':('P','section, web spacing, plate sizes, slope, length; handholes (16 per web?) and cap-level bolts (1,780) not counted'),
'integral steel cap group':('P','as cap'),
'pier cap connection':('U','end plates, connector plates, diaphragm plates: thicknesses only (p30); sizes 27 x 84 1/2 and 18 1/2 x 70 not checked'),
'end plate':('U','as pier cap connection'),
'connector plate':('U','as pier cap connection'),
'connection diaphragm plate':('U','as pier cap connection'),
'mill-to-bear plate':('P','thickness 2 1/2 x 24 checked; length 18 1/2 not checked'),
'cover plate':('U','cap top/bottom cover plates (PL 3/4 x 40 3/8): lengths 137 1/16 .. 173 5/16 not checked; 2 clashes with splice/filler plates'),
'handhole backing plate':('U','count and size not confirmed'),
'handhole cover':('U','count and positions not confirmed'),
'wire mesh screen':('U','mesh screen details p31 not compared'),
'rebar':('P','5d2 / 5e5 / 5e6: count, size, length, shape, placement checked; bend pins FAIL'),
'deck reinforcing':('P','record holds only 3 marks; all other marks not placed (FAIL)'),
'expansion joint device':('U','placeholder (status UNRESOLVED), plan p48-49 details not modelled'),
'joint':('U','placeholder'),
'thin brick':('P','start elevations and areas (quantities FAIL for Pier 1); brick coursing / bands not checked'),
'form liner':('U','texture limits not compared to p65/p66'),
'surface treatment':('U','not geometry'),
'superstructure':('V','container'),'field splice':('V','4 splices x 4 girders: plates, bolts, stations'),'barrier group':('P','as barrier'),'pier cap haunch':('U','haunch part (CAP2) carries only the 53 haunch bars; haunch concrete shape not on any sheet read'),
'substructure':('V','container'),
'pier':('V','container'),
'project':('V','container'),'site':('V','container'),
}
rows=q("select global_id,category,name,mark,support,girder from elements")
cnt=collections.Counter(); per=[]
for r in rows:
    cov,why=R.get(r['category'],('U','no rule'))
    cnt[(r['category'],cov)]+=1
    if cov!='V': per.append((r['global_id'],r['category'],r['name'],r['support'] or '',r['girder'] or '',cov,why))
cats=collections.defaultdict(dict)
for (c,cov),n in cnt.items(): cats[c][cov]=n
with open(BASE+"/results/unverified.csv","w",newline="",encoding="utf8") as f:
    w=csv.writer(f); w.writerow(["category","elements","coverage(V verified / P partly / U unverified)","what is not verified (reason)"])
    for c,d in sorted(cats.items(),key=lambda x:x[0]):
        cov=R.get(c,('U','no rule'))
        w.writerow([c,sum(d.values()),cov[0],cov[1]])
with open(BASE+"/results/unverified_elements.csv","w",newline="",encoding="utf8") as f:
    w=csv.writer(f); w.writerow(["global_id","category","name","support","girder","coverage","reason"]); w.writerows(per)
tot=sum(cnt.values()); u=sum(n for (c,cov),n in cnt.items() if cov=='U'); p=sum(n for (c,cov),n in cnt.items() if cov=='P'); v=sum(n for (c,cov),n in cnt.items() if cov=='V')
print(tot,"V",v,"P",p,"U",u)
print({c:d for c,d in cats.items() if 'U' in d or 'P' in d and sum(d.values())>500})
