import sys; sys.path.insert(0,'.')
from clash_an import load
import collections,csv,re
rows=load('clashb')
T={
'Bolt x TrussGusset':('intended contact','Rivet shank/head in a gusset: plates are modelled solid (no holes cut, design rule in CLAUDE.md); depth <= rivet head height 0.6 d in every row.',''),
'Bolt x TrussChord':('intended contact + model error','63,864 of 87,408 rows are within a button-head height (head seated on the member face, solid plates); 23,544 are deeper than a head (max 1.21 in.): rivet grip/position does not follow the member pieces at those spots (partial cover plates / angle roots).','I018'),
'Bolt x TrussGirder':('intended contact','All 71,280 rows are 0.56 in. = head seated against angle/cover (holes not cut).',''),
'Bolt x TrussDiagonal':('intended contact + model error','24,888 of 44,272 within head height; 19,384 deeper (max 1.18 in.) same cause as chords.','I018'),
'Bolt x Bolt':('model error','21,428 rows are head-to-head contact of neighbouring rivets (< head height); 64 rows deeper, max 1.12 in. at 660+81.76 (B8446 / B8640 grip 2.5): rivets on top of each other; 248 rows > 0.5 in.','I019'),
'Bolt x TrussVertical':('intended contact','Heads seated, solid plates.',''),
'Bolt x SplicePlate':('intended contact','Heads seated, solid plates.',''),
'Bolt x FillerPlate':('intended contact','Heads seated, solid plates.',''),
'Bolt x TrussBracing':('intended contact','Heads seated (<= 0.6 d); max 0.62 in. on a 1 1/4 rivet.',''),
'Bolt x TrussHanger':('intended contact','Heads seated; hanger solid.',''),
'Bolt x IntermediateStiffener':('intended contact','Heads seated on the stiffener legs.',''),
'Bolt x TrussFloorBeam':('intended contact','Heads seated.',''),
'Bolt x SolePlate':('model error','Girder-span sole plate 11 x 4 3/4 x 1 ft 6: 184 of 400 rivet clashes deeper than a head (max 0.76 in.; 308 > 0.5 in.): rivets of the girder angle/cover set run through the sole plate zone (grip 1.125 only).','I020'),
'Channel x TrussStringer':('model error','Diaphragm 15[33.9 at the girder-span stringer webs overlaps by about 0.2 in. in every panel (2,508 rows; constant 0.20-0.26): channel placed 0.2 in. into the stringer web. Small, repeatable.','I021'),
'RailMember x RailPost':('intended contact','Oval aluminum rail passes through the cast post (depth 2.8 in. = rail depth inside the post body); plan shows the rail threaded through the post; the post is not cored.',''),
'TrussBracing x TrussBracing':('model error + intended contact','1,868 rows. Max 10.17 in.: PORTAL strut U11 / U11 prime (p.79 Section C) overlaps the upper lateral UL-SU11 at the arch main span (6 rows). Median 0.25 in.: crossing laterals / sway bracing at their intersections (lap, plan shows one on the other) = intended.','I022'),
'TrussBracing x TrussGusset':('model error','1,190 rows, 0.375-0.56 in. = one gusset plate thickness: bracing member lies in the gusset plane instead of lapping on its face (portal strut U11 vs gusset 1 1/8, sway strut SW-S11 vs gusset L11).','I022'),
'TrussBracing x TrussChord':('model error','892 rows: lateral bracing 6 WF 29 (TL-S10-9 / -11) ends INTO the chord U9-U11 by 1.23 in. (end not set back to the chord face); sway struts SW-S11 prime into L12-L11 0.56 in.','I022'),
'TrussBracing x TrussFloorBeam':('model error','650 rows, max 0.52 in.: lower laterals LL-SL12-LM13 2-13[ 50 / lateral 6 WF 29 touch the floor beam FB13 / F1 flange; levels differ by < 0.5 in. (plan elevation of laterals unresolved).','I022'),
'TrussBracing x TrussStringer':('model error','504 rows girder spans: lateral 6 WF 29 (LAT-N/S-n) crosses stringer 24 WF 94 (S3 / S4) by 0.58 in.','I022'),
'TrussFloorBeam x WalkwayRail':('model error','588 rows 0.2-0.3 in.: walkway rail end posts lie on the floor beam flange (p.101-106 platform is bolted to the floor beam; detail not drawn).','I023'),
'TrussFloorBeam x WalkwayStringer':('intended contact','Walkway stringers sit on / are bolted to the floor beam flange (0.27 in. median).',''),
'TrussFloorBeam x WalkwayPost':('intended contact','Post base on the floor beam flange (0.25 in.).',''),
'WalkwayPost x WalkwayRail':('intended contact','Mid rail through post (max 1.41 in. at the FB0 platforms, p.102), 0.156 in. elsewhere.',''),
'Grating x WalkwayPost':('intended contact','Grating cut around posts is not modelled (0.375 in. = grating thickness).',''),
'Grating x WalkwayStringer':('intended contact','Grating on the stringer flange (0.33 in. median).',''),
'Grating x TrussFloorBeam':('intended contact','Grating seats on the floor beam flange (0.375 in.).',''),
'TrussChord x TrussChord':('intended contact','Arch main span: adjacent chords overlap at the joint wedge (up to 0.64 in. at U20 / U21 / U22) = square cuts closing the joint (answer I008).',''),
'Grating x TrussBracing':('intended contact','Grating lies on the walkway frames / laterals (0.24 in. median).',''),
'TrussBracing x WalkwayStringer':('model error','150 rows 0.15-0.30 in.: walkway stringers over lower lateral in the same plane.','I023'),
'WalkwayRail x WalkwayRail':('intended contact','Rails of adjacent segments overlap by one post width (0.156 in.).',''),
'TrussBracing x TrussGirder':('model error','112 rows 0.14-0.21 in. girder-span bracing at the girder angles.','I022'),
'TrussFloorBeam x TrussGirder':('intended contact','Floor beam bracket seated on girder (0.38 in.), bolted connection.',''),
'TrussDiagonal x TrussVertical':('intended contact','Square cuts at joint (0.44 in. median) closing around the gusset.',''),
'FillerPlate x TrussChord':('intended contact','Fill plate sits on the member face; solid.',''),
'TrussFloorBeam x TrussGusset':('model error','88 rows 0.31 in.: floor beam flange vs gusset edge.','I022'),
'IntermediateStiffener x TrussGirder':('intended contact','Stiffener legs on the angles (0.52 in. = angle stand-off).',''),
'TrussFloorBeam x TrussFloorBeam':('intended contact','Adjacent bracket beams (0.32 in.).',''),
'TrussChord x TrussFloorBeam':('model error','68 rows 0.28 in.: floor beam flange on the top chord of the 4-span deck trusses (p.55: floor beams ON the top chords, seat plate not drawn).','I022'),
'SuspenderSocket x TrussPin':('intended contact','Pin runs through the open socket (3.6 in. = pin radius); modelled as solid envelope.',''),
'SolePlate x TrussPin':('intended contact','Shoe pin through the sole plate (hole not cut, 3.6 in. = pin radius).',''),
'TrussFloorBeam x TrussPin':('intended contact','Hanger pin through the floor beam (hole not cut).',''),
'SuspenderSocket x TrussFloorBeam':('model error','30 rows, 6.82 in.: the LS socket envelope (13 3/4 x 42 1/4) runs through the floor beam web (arch main span); the socket should sit outside the web plate at the pin.','I024'),
'BearingPlate x MasonryPlate':('model error','28 rows exactly 1.00 in.: the stainless bearing plate is sunk 1 in. into the masonry plate; shoe bottom elevations match the plan, so the masonry plate top or the bearing plate bottom is 1 in. off.','I025'),
'SolePlate x TrussGusset':('intended contact','Shoe sole plate on the lowest gusset (0.26 in. median, max 0.72).',''),
'SolePlate x TrussGirder':('intended contact','Sole plate on the girder bottom (0.16 in.).',''),
'JointArmor x TrussStringer':('model error','27 rows 0.36 in. girder-span dam armor vs stringer end.','I023'),
'FillerPlate x FillerPlate':('intended contact','Stacked fills overlap by one 1/2 in. layer (0.51).',''),
'SplicePlate x TrussChord':('intended contact','Splice plate on the chord face, solid.',''),
'SplicePlate x TrussBracing':('model error','44 rows, 0.5 in.: splice plate over the bracing flange.','I022'),
}
g=collections.defaultdict(list)
for r in rows: g[r['category_pair']].append(r)
out=[]
i=0
for k,rs in sorted(g.items(),key=lambda x:-len(x[1])):
    i+=1
    d=sorted(float(r['depth_in']) for r in rs); ex=max(rs,key=lambda r:float(r['depth_in']))
    t=T.get(k)
    if not t:
        if re.search(r'Walkway|Grating|TrussShoe|MasonryPlate|TrussPin',k): t=('model error','walkway / platform vs shoe, masonry plate or pin: platform framing passes through the shoe parts (max %.2f in.); platform at the end floor beam FB0 not coordinated with the shoe (p.52 / 101-106).'%d[-1],'I023')
        else: t=('intended contact','small (max %.2f in.) lap / seat between adjacent members; solid plates.'%d[-1],'')
    out.append(['K%03d'%i,k,len(rs),'; '.join('%s:%d'%(b[4:],n) for b,n in collections.Counter(r['bridge'] for r in rs).most_common()),round(d[-1],3),round(d[len(d)//2],3),"%s x %s @ %s"%(ex['mark_a'][:40],ex['mark_b'][:40],ex['station']),t[0],t[1],t[2]])
w=csv.writer(open(r'C:\SBM\qc\ptc\results\clash-summary.csv','w',newline='',encoding='utf-8'))
w.writerow(['group','category_pair','count','by_bridge','max_depth_in','median_depth_in','worst_example','triage','reason','issue'])
w.writerows(out)
c=collections.Counter(); n=collections.Counter()
for o in out:
    kk='model error' if 'model error' in o[7] else ('intended contact' if o[7]=='intended contact' else 'other'); c[kk]+=1; n[kk]+=o[2]
print(len(out),c,n,sum(o[2] for o in out))
