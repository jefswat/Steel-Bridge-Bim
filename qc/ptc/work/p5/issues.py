"""QC pass 5: append new issues I046-I053 to results/issues.csv and mark the pass 4 issues I036-I045 closed (re-verified on the regenerated model,
checks C2339-C2348). Run once:  python -I issues.py"""
import csv, os
RES = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'results'))
path = os.path.join(RES, 'issues.csv')
NEW = [
 ('I046', 'medium', '93 (72)', 'PTC Arch truss 18-20: FB22 (section F7, two-web floor beam at W22)',
  'F7 web spacing: p.93 dimensions the box "2\'-1 1/2 in to in Webs" (Section A, with 7/16 reinforcing plates inside) and "2\'-1 1/2 b.b. Ls" (Section B), '
  'i.e. 25 1/2 in. between the 106 x 7/16 webs (26 3/8 out to out). The model builds the webs 25 in. out to out (24 1/8 in to in, the cover width), 1 3/8 in. too narrow; '
  'the 25 in. covers then fit between the webs on the 8 x 4 angle legs, as on the truss members. The section flag says "the detail sheet p.93 (Sheet 53) was not read" - the flag reason is wrong; '
  'the tongue beam / W22 lateral flags give the same reason although p.93 Section E draws the tongue beam (16 WF 88 + 2-Cov. Pls 11 1/2 x 1/2 x 5\'-6) and its connection. High confidence (two dimensions).',
  '25 1/2 in. in to in webs', '24 1/8 in. in to in (25 o. to o.)', 'open', 'C2312; evidence/p5/p93_secA.png p93_secB.png'),
 ('I047', 'medium', '74-89 (74, 76)', 'PTC Arch truss 18-20: chord members at every kinked panel point (U1-U10, L2-L22 and primes)',
  'Kinked chord joints. The sheets draw ONE finished (milled, "Fin.") joint line through the panel point: p.74 U1 (straight line about 1 in. left of the WP, field rivets on one side, shop on the other), '
  'p.76 L6 ("Fin." on the L6 axis; the left fills 31 1/2 x 1 1/16 and 18 x 7/8 end at that line, the right fills 18 x 7/8 start at it). The model cuts each chord square at the point + h/2 tan(d/2) '
  '(TRUSS-DESIGN "automatic ends"), so the end wedges of the two chords overlap each other and reach 1-1 1/2 in. back past the point into the neighbour\'s fills / splice plates: '
  'sbm clash (qc5 build) TrussChord x TrussChord 168 rows up to 0.63 in., FillerPlate x TrussChord 64 up to 0.44, SplicePlate x TrussChord 52 up to 0.50 (= the 116 left by pass 4). '
  'Suggested for the user: cut both chord ends on the bisector plane through the panel point (mitre); the fills / splice fills then end on it as drawn. Medium confidence on the line being the exact bisector (raster), high on the single joint line and the fills ending at it.',
  'mitred finished joint through the panel point; fills end at it', 'square cut + h/2 tan(d/2) extension; 284 overlaps', 'open', 'C2349-C2353; evidence/p5/p74_U1_fin.png p76_L6_fin.png'),
 ('I048', 'low', '74-75', 'PTC Arch truss 18-20: verticals U1-L1..U5-L5 (cover splice plates SPU1-6, SPU2-4, SPU3-5, SPU4-4, SPU5-6), U2-L2 / U4-L4 (SPL2-3, SPL4-6, fill FL4-1), + primes, both trusses',
  'Web member ends vs chord cover splice plates on the face they meet: the vertical (channel) members run to the chord envelope and overlap the 24 x 1/2 - 5/8 cover splice plates on that chord face by 5/16 in. '
  '(28 clash rows) and the 24 x 1/8 cover fill FL4-1 by 0.15 in. (4). p.74 draws the U1-L1 channels ending about 2 in. below the chord bottom, clear of the splice plate. '
  'The automatic web member end (TRUSS-DESIGN: web members stop at the chord envelope) does not count the joint plates on the chord face. Medium confidence on the 2 in. (scaled); high on the clearance.',
  'vertical ends clear of the cover splice plate (about 2 in. below the chord)', 'overlap 5/16 in. (0.15 at FL4-1)', 'open', 'C2354-C2355; evidence/p5/p74_U1_vert.png'),
 ('I049', 'low', '61 (60)', 'PTC 4-span truss 14-17 / 21-24: gussets at U6 (both trusses, both units)',
  'U6 gusset thickness: p.61 prints only "(17 1/4 in to in gussets)" and "Details not shown similar to U2 (see sht 20)"; U2 has 5/8 gussets (p.60), and every other "similar to U2" joint is 5/8 '
  '(U4 even prints "2-5/8 Gussets (18 5/8 in to in)"). The model has 7/8; truss-approach/joints.csv says "2 - 7/8 gussets (17 1/4 in. to in.)", a callout that is not on the sheet. Medium confidence (no thickness printed at U6).',
  '5/8 (similar to U2)', '7/8', 'open', 'C2238; evidence/p5/p61_U6.png'),
 ('I050', 'low', '67 (52)', 'PTC 4-span truss 14-17 / 21-24: gussets at U20',
  'U20 gussets in to in: p.67 prints "17 1/4 in. to in." (details as U8 sheet 12 = 5/8 gussets). joints.csv leaves the thickness empty and the model has no in-to-in input (automatic), giving 17 1/8 in. (S-GU20 inner face 8.569 in. from the plane). '
  'Thickness 5/8 agrees.', '17 1/4 in.', '17.14 in. (automatic)', 'open', 'C2291; evidence/p5/p67_U20.png'),
 ('I051', 'low', '49-53, 59-67', 'PTC 3-span / 4-span approach trusses: Truss.Unresolved (fills, splice plates, splice bars of the joints)',
  'Approach joint fills / splice plates: the flag says they are "listed per joint without extents" and builds none. The joint sheets give every fill / splice plate / splice bar with its size and draw its extent with dimension lines '
  'at 3/4 in. = 1 ft (e.g. p.62 U9: two 14 1/2 x 7/8 fills per side from the CL splice on the 14 WF 95 - 17 5/8 in to in less d 14.12 = 1 3/4 in. per side; 2-Spl Pls 16 x 1 1/8 OUTSIDE the gussets; '
  'p.64 U11: 2-Spl Pls 16 x 1 1/8, 1-1 1/8 fill; p.50 L2: 2-Fills 14 1/4 x 3/4, 1-3/4 fill, 2-Splice Pls 16 x 3/4, 4-Splice Bars 6 x 1 1/8). That is the same information the main truss sheets give, '
  'where the plates were built from scaled lengths. As modeled, the approach gussets stand up to 1 3/4 in. off the member flanges with nothing between. Flag reason inaccurate; for the user to decide whether to build them like the main truss.',
  'fills / splice plates sized and drawn to scale', 'not built; gaps up to 1 3/4 in. per side', 'open', 'C2299-C2303; evidence/p5/p62_U9_cov.png p64_U11_cov.png p50_L2.png'),
 ('I052', 'low', '50, 51 (67)', 'PTC approach trusses: chord rivets at the joints (UniformChordRivets)',
  'Uniform chord rivet grid vs the drawn layout at other joints: p.50 3-span U3 draws 36 field rivets (+5 beyond the gusset) on U1-U3 and 27 shop rivets on U3-U5 per face (connections/rivet-counts.csv agrees); '
  'the model lays one 3.71 in. grid through the joint: 36 / 33 per face (right side +6, +22 %). p.51 L4: dense rows near the CL splice (about 2.7-2.9 in.), sparse staggered rivets (about 6.5 in.) toward the gusset ends; model one 3.45 in. grid. '
  'The uniform field was decided from 4-span L20 (p.67, user review); at these joints the sheet draws per-member densities. Information for the user; totals are kept. Low-medium confidence (hand count on 300 dpi crops).',
  'per member: 36 (+5) / 27 at U3', '36 / 33 uniform', 'open', 'C2304-C2306; evidence/p5/p50_U3_chord*.png p51_L4_chord.png'),
 ('I053', 'low', '76, 77, 83', 'docs/plan-data/ptc-delaware-river/connections/gusset-layers.csv (main U7, U9, L8, L13 rows)',
  'Data hygiene: gusset-layers.csv still has in_to_in_in 31 (U7), 30.5 (U9, L8) and 26.5 (L13) after the I039 / I040 fixes; gussets.csv and the model use 29 (2\'-5 printed). The model is right; the csv rows are stale.',
  '29', '31 / 30.5 / 30.5 / 26.5 (csv only)', 'open', 'C2335-C2338'),
]
rows = list(csv.reader(open(path, encoding='utf-8')))
hdr = rows[0]
for r in rows[1:]:
    if r and 'I036' <= r[0] <= 'I045' and r[7] == 'fixed':
        r[7] = 'closed'
        r[8] = r[8] + '; re-verified pass 5 (C2339-C2348)'
with open(path, 'w', newline='', encoding='utf-8') as f:
    w = csv.writer(f, lineterminator='\r\n')
    w.writerow(hdr)
    for r in rows[1:]:
        if r: w.writerow(r)
    for i in NEW:
        w.writerow([i[0], i[1], i[2], i[3], i[4], i[5], i[6], i[7], i[8]])
print('issues now', len(rows) - 1 + len(NEW))
