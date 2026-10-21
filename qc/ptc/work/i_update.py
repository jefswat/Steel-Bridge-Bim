import csv
p=r'C:\SBM\qc\ptc\results\issues.csv'
rows=list(csv.DictReader(open(p,encoding='utf-8')))
st={ # id -> (status, evidence)
'I001':('open','Re-measured 2026-10-20 (pass 3b, C1791): carbon + silicon + cast + forged = 33.89 M lb = 86.3 % of the 39.28 M lb plan (was 83.8 %); still below the 1 % tolerance; new contributors found in I027 (floor beams) and I030/I029'),
'I002':('closed','Pass 3 re-check C1097-C1200 (3-span member table p.47, material + area, 104 rows pass)'),
'I003':('closed','Pass 3 re-check C1097-C1200'),
'I004':('accepted plan conflict','Plan table p.69 itself prints 140.782 (lower chord) and 140.762 (wind chord) at panel 13; model follows the wind-chord value; needs user decision'),
'I005':('accepted plan conflict','PLAN CONFLICT p.43 / p.1; model follows p.43 (later revision); user decision'),
'I006':('open','Not re-measured in pass 3 / 3b (girder spans depth 9.946 vs 9.906 ft, low confidence)'),
'I007':('closed','Re-read 2026-10-20 (C1792): drawn gusset / chord end on p.73 lies 18.5 in +-1 in past the L0 pin (overlay evidence/gusset-main-L0.png), model 17.0 in; originator answer accepted. Plan table 62.0254 = working-point length'),
'I008':('closed (definition, accepted)','Originator answer: solid length = working-point length + half depth x tan(deflection / 2) per end. Checker accepts as a definition difference; not independently re-measured (pass 3 checked areas only, C1208-C1248)'),
'I009':('closed','Pass 3 re-check C1081-C1096, C1201-C1204: mid-member section 114.37 / 127.40 / 129.47 / 114.30 sq in'),
'I010':('closed','Pass 3 re-check C1201-C1204 (62 table rows each unit, C/S material 0 mismatches)'),
'I011':('closed','Pass 3 re-check C1205-C1207: 293.67 / 217.17 zones agree; third zone 332.2 info'),
'I012':('closed','Pass 3 re-check C1249-C1292: 44 vertical / diagonal rows within 1 %'),
'I013':('accepted plan conflict','C1284 / C1285 conflict rows; model follows the section text; user decision'),
'I014':('closed','C1293 / C1294: 12.4975 / 12.8725 ft vs 12.50 / 12.875'),
'I015':('closed','C1299: 196 piles'),
'I016':('closed','C1295-C1298 floor beam mid-length areas and plate lengths (approach floor beams)'),
'I017':('closed','Pass 3 hanger rows inside C1201-C1204 (62 rows within 1 %)'),
}
new=[
('I018','low','all bolts / rivets x chords / diagonals','Rivet clashes with chords and diagonals (K002, K004, 131,680 rows): 63,864 of 87,408 chord rows are within a button-head height (head on the member face, intended); 23,544 (and the diagonal equivalents) are deeper than a head, max 1.21 in: rivet grip / position does not follow the member pieces (partial cover plates, angle roots)','depth <= head 0.6 d','max 1.21 in','open','clash-summary.csv K002 K004'),
('I019','low','rivets','Rivets on top of each other (K005, 21,492 rows, 64 deeper than a head, max 1.12 in at 660+81.76; 248 rows > 0.5 in)','touching rivets only','max 1.125 in','open','clash-summary.csv K005'),
('I020','low','girder-span sole plates','Girder-span sole plate 11 x 4 3/4 x 1 ft 6: 184 of 400 rivet clashes deeper than a head (max 0.76 in): rivets of the girder angle / cover set run through the sole plate zone','depth <= 0.6 in','0.757 in','open','clash-summary.csv K021'),
('I021','low','girder-span diaphragm channels','Diaphragm 15[33.9 overlaps the stringer webs by about 0.2 in in every panel (2,508 rows)','no overlap','0.20-0.26 in','open','clash-summary.csv K012'),
('I022','medium','main span portal U11 and bracing','PORTAL strut U11 / U11 prime (p.79 Section C) overlaps the upper lateral UL-SU11 by up to 10.17 in (6 rows); other bracing clashes with gussets, chords, floor beams, stringers 0.2-1.23 in (K014-K049, about 5,400 rows, mostly crossing laps = intended)','no overlap','max 10.17 in','open','clash-summary.csv K014 K016 K017 K018 K020 K034 K037 K040 K049'),
('I023','low','walkways, platforms, ladders','Walkway rails, posts and stringers on floor beam flanges (0.2-0.3 in) and walkway posts / rails / stringers through shoes, masonry and sole plates, pins (1.0-6.5 in; K063 K068-K086); 24 groups','no overlap','up to 6.5 in','open','clash-summary.csv K019 K028 K031 K041 K043 K050 K060 K063 K067-K074 K078 K080-K086'),
('I024','medium','main span suspender sockets','Suspender socket envelope (13 3/4 x 42 1/4) runs through the floor beam web, 30 rows, 6.82 in; the socket should sit outside the web plate at the pin','no overlap','6.82 in','open','clash-summary.csv K054'),
('I025','low','bearings','Stainless bearing plate sunk exactly 1.00 in into the masonry plate (28 rows); shoe bottom elevations match the plan, so the masonry plate top or the bearing plate bottom is 1 in off','no overlap','1.00 in','open','clash-summary.csv K055'),
('I026','medium','p.71 / p.99 main span floor laterals','Floor lateral X bracing is shifted one panel: plan X centred on floor beams W14, W16, W18, W20 (spans L13-L15 ... L19-L21, 4 two-panel X); model X centred on L15, L17, L19 (L14-L16 ... L18-L20) plus two single-panel X; first X starts at L14, not L13','first X centre 1.0 panel from L13; 4 two-panel X','2.0 panels; 3 two-panel X','open','C1740 C1741'),
('I027','medium','p.94 / p.95 / p.72 main span suspended floor beams','Floor beams carry a reduced section: typical interior (F6) mid-span 123.6 sq in (web 106 x 5/8, 4-L 8x8x3/4, one top cover plate) vs 157.0 sq in plan (2 + 1 top and 1 bottom plate 18 x 5/8); FB13 (F8, p.95) 134.85 vs 220.7 sq in (extra angles 9x4x1, 7x4x7/8, 8x4x1 x 76 ft and the 46 ft / 76 ft plates). Helps explain I001. Which F mark belongs to which floor beam was read from p.94 / p.95 only (the p.72 panel grid is not legible)','157.0 / 220.7 sq in','123.6 / 134.85 sq in','open','C1751 C1752'),
('I028','medium','p.77 U9, p.86 L15, p.87 L17','Main span gusset outlines (low-confidence joints) extend beyond the drawn plate edges: U9 right edge about 20 in (width 143.5 vs 165.6 in), L15 left edge about 17 in (104 vs 121.5 in), L17 right edge 11-16 in (113 vs 125.7 in); L12 / L18 / L20 / U12 / U11 / L10 follow the drawn edges. Overlays in evidence/gusset-main-*.png','drawn edge','outline polygon outboard','open','C1700 C1701 C1717-C1720'),
('I029','medium','p.50-89 gussets / rivets','Rivets: 88 of 224 N-side gusset plates carry no rivet in the model (3-span 29 of 50, 4-span 33 of 82, main 26 of 92; e.g. 3-span U1 U2 L1 L2) while the plan draws rivets on every gusset; at 9 of the 13 sampled joints the per-face count differs by more than max(3, 5 %) (3-span U2 27 vs 0, L1 24 vs 0, L9 26 vs 9, U8 47 vs 59; 4-span U2 28 vs 33, U15 98 vs 116; main U2 89 vs 84, U6 76 vs 171, L7 153 vs 204, L5 138 vs 110)','plan counts (image detection)','model per-face counts','open','C1775-C1790'),
('I030','low','p.60-66 gusset stacks, p.74-89','Gusset stack dimensions: 4-span in-to-in between gusset stacks 0.25-0.5 in off at 12 joints in both units (L7 L8 L9 L12 L14 L17 U4 U9 U13 U16 U19: e.g. 15.125 vs 15.624); 4-span L5 layer 0.625 vs 0.75; main L1 0.5 vs 0.75, L6 0.875 vs 0.625 and in-to-in 29.0 vs 31.0, U10 31.25 vs 30.0; L21 / U21 31.25 vs 29.0 (plan conflict row)','printed','see check rows','open','C1301 C1305 C1306 C1331 C1353 C1372 C1477-C1626 (pass 3)'),
('I031','low','p.52 / p.67 shoes at piers 11 / 15 / 22 / 26','Shouldered pin overall length 42.63 in vs 42 1/8 in printed (0.5 in) at piers 11 and 15','42.125 in','42.63 in','open','C1673 C1679 (pass 3)'),
('I032','low','p.71 / p.56 bracing sections','Bracing section areas 1.0-1.4 % off the printed shapes: 14 BP 73 21.1 vs 21.4 sq in (wind chords, floor lateral diagonals, approach bottom laterals; same sharp-root cause as I017), 2-13[31.8 + 2 Pls 29 1/2 x 1/2 48.7 vs 48.2, 2-18[42.7 + 2 Cov Pls 42.0 vs 41.6','21.4 / 48.2 / 41.6 sq in','21.1 / 48.7 / 42.0 sq in','open','C1729 C1734 C1738 C1743 C1754'),
('I033','low','p.108 parapet railing elevations','Railing post spacing 5.71 ft (5\'-8 1/2") uniform along the run vs the first panel at each abutment 5 @ 5\'-11 1/2" = 29\'-9 1/2" (28.55 ft in the model); the spacing of the other panels is not dimensioned on the sheet','5.958 ft','5.706 ft','open','C1755 C1756'),
('I034','low','p.91 Pier 20 expansion dam (LOW CONFIDENCE)','Tooth length 1\'-8 3/4" printed vs 31.5 in modelled (2.625 ft) and plate thickness 3 1/2 in vs 8.28 in vertical extent; the plan reading of what the 1\'-8 3/4" measures is uncertain','20.75 in / 3.5 in','31.5 in / 8.28 in','open','C1770 C1771'),
('I035','low','p.103 key plan (LOW CONFIDENCE)','Walkway at floor beam U11-L11 runs the full width (80 ft) in the model; the key plan draws it from the north ladder to about the bridge CL (about 41 ft by scale, not dimensioned)','about 41 ft (scaled)','80 ft','open','C1765'),
]
fields=['id','severity','page','element GlobalIds','finding','plan value','model value','status','evidence']
out=[]
for r in rows:
    s,e=st[r['id']]; r['status']=s; r['evidence']=e; out.append(r)
for (i,sev,pg,el,fi,pv,mv,s,e) in new:
    out.append({'id':i,'severity':sev,'page':pg,'element GlobalIds':el,'finding':fi,'plan value':pv,'model value':mv,'status':s,'evidence':e})
w=csv.DictWriter(open(p,'w',newline='',encoding='utf-8'),fieldnames=fields); w.writeheader(); w.writerows(out)
print(len(out))
