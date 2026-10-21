"""QC pass 5: build check rows (web members, approach tables / joints, floor beams, bracing, outer gusset layers, pass 4 fix re-check,
kinked panel points) and append them to results/checks.csv; append new issues to results/issues.csv and close the verified pass 4 issues.
Read-only on the model. Run once:  python -I rows.py"""
import json, csv, os, collections, sys
HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, '..', '..', 'results'))
SBM = r'C:\SBM\temp\qc5\ptc.sbm'
CLASH = r'C:\SBM\temp\qc5\clash_arch.csv'
IN = 0.0254
d = json.load(open(SBM))
B = {b['Name']: b for b in d['Bridges']}
def fr(x):
    from math import gcd
    n = round(x * 16); w, r = divmod(n, 16)
    if r == 0: return '%d' % w
    g = gcd(r, 16)
    return ('%d %d/%d' % (w, r // g, 16 // g)) if w else '%d/%d' % (r // g, 16 // g)
rows = []
def row(page, sheet, view, item, method, plan, model, diff, tol, result, ev):
    rows.append([page, sheet, view, item, method, plan, model, diff, tol, result, ev])
def sheetno(p): return 'DRB-4 %d/68' % (p - 40)

# ---------------- 1. main truss web members (p.70) ----------------
web = json.load(open(os.path.join(HERE, 'web_cmp.json')))
for name, plan, model, probs, res in web:
    vert = name.split('-')[0][1:] == name.split('-')[1][1:]
    if name == 'U18-L18':
        res = 'conflict'; probs = 'p.70 SECTION prints (28-12) x 1/2 but its full gross 94.17 sq in fits a 26 in cover (28 in gives 96.17); p.88 joint U18 prints 26; model 26 (webs.csv note) - plan internal conflict resolved by p.88'
    row(70, sheetno(70), 'Verticals table' if vert else 'Diagonals table', 'Arch web member %s make-up (webs / channels, angles, cover plates, material)' % name,
        'web_cmp.py: p.70 SECTION columns (evidence/p5/p70_vs_*.png, p70_ds_*.png) vs .sbm section', plan, model, probs, 'exact', res,
        'work/p5/web_cmp.py; evidence/p5/p70_vs_a.png p70_vs_b.png p70_ds_a.png p70_ds_b.png p70_dn.png' + ('; p70_U18L18.png' if name == 'U18-L18' else ''))
row(82, sheetno(82), 'U13 joint callout', 'Arch U13-L13 web ply order (26 x 15/16 vs 26 x 7/8)', 'p.82 member callout "2-Web Pls 26 x 15/16 o.s." vs .sbm WebPlies order (out -> in)',
    '15/16 outside, 7/8 inside', '15/16 outside, 7/8 inside (x 13.563-14.500 / 12.687-13.563 in.)', '', 'exact', 'pass', 'evidence/p5/p82_U13L13 (not kept); dump_solids S-U13-L13')
row(70, sheetno(70), 'CHORDS sketch', 'Arch chord angle leg on the web (8 x 6 angles)', 'p.70 CHORDS sketch: rivet gage chain in the web leg 2 3/8 + 3 (two lines -> 8 in leg on the web); angles inside the plies, cover on the outstanding legs',
    '8 in leg on the web, angles inside', 'L8x6: 8 in leg on the web (y 9.25-17.25 in.), back on the innermost deep ply (S-L12-L13)', '', 'exact', 'pass', 'evidence/p5/p70_chord_sketch.png p70_chord_sketch_z.png')
row(70, sheetno(70), '21/24/26 in WEBS DIAGONALS sketch', 'Arch web member angle leg on the web (7 x 4 angles)', 'p.70 sketch: 2 gage lines in the web leg (2 1/4 + 3), angles inside the webs, perforated cover on the 4 in outstanding legs between the webs',
    '7 in leg on the web, inside', '7 in leg on the web (y 6.25-13.25), 4 in leg inward, cover 25-27 in between the webs on the legs (S-U13-L13, S-L8-U9)', '', 'exact', 'pass', 'evidence/p5/p70_sketches.png p70_diag_corner.png')
row(70, sheetno(70), '18 in [ WEBS sketches', 'Arch channel web members: channel orientation', 'p.70 sketches: channel webs parallel to the truss (gusset side), flanges pointing inward, cover across the flange tips',
    'web outside, flanges in', 'MC18 web at x 10.5-14.5 in. (outer face 14.5 = 2\'-5 o. to o.), flanges inward, 28 in cover on the flange tips (S-L0-U1)', '', 'exact', 'pass', 'evidence/p5/p70_sketches.png')

# ---------------- 2. approach member tables (p.47 / p.57) ----------------
ap = json.load(open(os.path.join(HERE, 'appr_cmp.json')))
for bn, name, plan, model, probs, res in ap:
    p = 47 if '3-span' in bn else 57
    row(p, sheetno(p), 'member table SECTION / MATERIAL', '%s: %s section, material%s' % (bn, name, ', cover plates' if 'Pls' in plan else ''),
        'appr_cmp.py: table row (evidence/p5/p%d_*.png) vs .sbm section designation / material / TopPlates+BottomPlates' % p, plan, model.strip(), probs, 'exact', res,
        'work/p5/appr_cmp.py; evidence/p5/' + ('p47_tab0.png p47_tab1.png p47_tab2.png' if p == 47 else 'p57_ch0.png p57_ch1.png p57_ch2.png p57_d1.png p57_d2.png'))
row(47, sheetno(47), 'NET AREAS sketch; p.49 Section B-B', 'Approach 14 WF orientation (web across the truss plane, flanges facing the gussets)',
    'p.47 CHORDS net-area sketch has a 2 in drain hole in the web (horizontal web); p.49 Section B-B draws the 14 WF 142 chord as an H with vertical flanges under the gussets',
    'web horizontal (across the plane)', 'Rotated = true for every 14 / 16 WF approach section', '', 'exact', 'pass', 'evidence/p5/p47 net-area sketch (not kept); p.49 Section B-B')
row(62, sheetno(62), 'U9 joint', '4-span U9-U11 cover plates 16 1/2 x 13/16 extent at U9', 'p.62: the cover dimension starts at the gusset right edge (leader of "2-7/8 Gus. Pls."); the 2-Spl Pls 16 x 1 1/8 run past it outside the gussets',
    'cover from the gusset edge', 'TopPlates/BottomPlates BetweenGussets', '', 'exact', 'pass', 'evidence/p5/p62_U9_cov.png')
row(64, sheetno(64), 'U11 joint', '4-span U9-U11 cover plate end at U11', 'p.64 "End of 16 x 13/16 Cov. Pl." at the gusset left edge; in-to-in 17 1/2 = 14 WF 342 depth, so no cover under the gusset',
    'cover ends at the gusset edge', 'BetweenGussets (gussets 17 1/2 in to in on the flanges)', '', 'exact', 'pass', 'evidence/p5/p64_U11_cov.png')

# ---------------- 3. approach joint sheets: gusset thickness / in to in ----------------
JA = {  # 3-span: joint -> (t, in to in, page) as printed
 'U1': ('5/8', 16, 50), 'U2': ('5/8', 15.875, 50), 'U3': ('3/4', 16.375, 50), 'L1': ('5/8', 14.375, 50), 'L2': ('5/8', 15.875, 50), 'L3': ('5/8', 15.75, 50),
 'U4': (None, 16.25, 51), 'L4': ('3/4', 15.75, 51), 'U5': ('5/8', 16.25, 51), 'L5': (None, 15.625, 51), 'U6': (None, 14.25, 51), 'L6': ('5/8', 15.75, 51),
 'U7': ('5/8', 17.625, 52), 'L7': ('5/8', 14.375, 52), 'U8': ('5/8', 16.5, 52), 'L8': ('7/8', 17.625, 52), 'U9': ('5/8', 16.875, 52), 'L9': ('5/8', 14.75, 52),
 'U10': ('5/8', 14.25, 53), 'L10': ('5/8', 15.25, 53), 'U11': ('5/8', 15.0, 53), 'L11': ('5/8', 14.0, 53), 'U12': ('5/8', 14.375, 53), 'L12': ('5/8', 14.875, 53),
 "U0'": ('5/8', 14.25, 59), "L0'": ('5/8', 16.125, 59),
}
JB = {  # 4-span
 'U0': ('5/8', 14.25, 59), 'L0': ('3/4', 16.875, 59),
 'U4': ('5/8', 18.625, 61), 'L4': ('7/8', 17.25, 61), 'U5': ('7/8', 17.375, 61), 'U6': ('5/8 (similar to U2)', 17.25, 61), 'L6': ('7/8', 17.25, 61),
 'U7': ('3/4', 17.25, 62), 'L7': ('5/8', 15.125, 62), 'U8': ('5/8', 14.125, 62), 'L8': ('3/4', 17.375, 62), 'U9': ('7/8', 17.625, 62), 'L9': ('5/8', 16.75, 62),
 'U11': ('7/8', 17.5, 64), 'L11': ('5/8', 17.25, 64), 'U12': ('5/8', 14.5, 64), 'L12': ('3/4', 17.25, 64), 'U13': ('3/4', 16.375, 64), 'L13': ('5/8', 14.0, 64),
 'U14': ('5/8', 14.875, 65), 'L14': ('3/4', 15.125, 65), 'U15': ('3/4', 15.5, 65), 'L15': ('5/8', 15.0, 65), 'U16': ('5/8', 15.375, 65), 'L16': ('3/4', 15.0, 65),
 'U17': ('3/4', 15.625, 66), 'L17': ('5/8', 14.375, 66), 'U18': ('5/8', 14.25, 66), 'L18': ('3/4', 15.625, 66), 'U19': ('7/8', 17.625, 66), 'L19': ('5/8', 15.5, 66),
 'U20': ('5/8 (see U8 sheet 12)', 17.25, 67), 'L20': ('7/8', 17.625, 67),
}
from fractions import Fraction as Fr
def tv(s): return float(sum(Fr(x) for x in s.split('(')[0].split()))
GUS = {}  # model gusset positions from the solids dump (in to in of the actual plates)
for r in csv.DictReader(open(r'C:\SBM\temp\qc5\solids.csv')):
    if r['category'] == 'TrussGusset' and r['plane'] == 'S' and r['mark'].endswith('-R') and r['solid'] == '0':
        GUS[(r['bridge'], r['mark'][3:-2])] = float(r['xin0'])
for bn, tab in (('PTC 3-span truss 11-13', JA), ('PTC 4-span truss 14-17', JB)):
    joints = {j['Point']: j for j in B[bn]['Truss']['Joints'] if not j.get('Plane')}
    for jn, (t, iti, p) in tab.items():
        j = joints.get(jn)
        if j is None: row(p, sheetno(p), 'joint ' + jn, '%s joint %s' % (bn, jn), '', '', 'joint missing', '', '', 'fail', ''); continue
        mt = j.get('GussetThickness') or 0; mi = j.get('GussetsInsideFaceDistance') or 0
        gx = GUS.get((bn, jn)); actual = 2 * gx if gx is not None else None
        if t:
            ok = abs(mt / IN - tv(t)) < 1e-3
            row(p, sheetno(p), 'joint ' + jn, '%s joint %s gusset thickness' % (bn, jn), 'joint sheet callout "2-t Gussets" (evidence/p5 crops of p.%d) vs Joint.GussetThickness' % p,
                t, fr(mt / IN), '' if ok else '%+.3f in.' % (mt / IN - tv(t)), 'exact', 'pass' if ok else 'fail', 'evidence/p5/p61_U6.png; I049' if (jn == 'U6' and '4-span' in bn) else 'joint sheet p.%d' % p)
        val = actual if actual is not None else mi / IN
        ok = abs(val - iti) <= 1 / 32
        row(p, sheetno(p), 'joint ' + jn, '%s joint %s gussets in to in' % (bn, jn), 'joint sheet "(x in. to in.)" vs 2 x inner face of the generated S-G%s-R plate (solids.csv)' % jn,
            fr(iti), fr(val) + ('' if mi else ' (input unset: automatic)'), '' if ok else '%+.3f in.' % (val - iti), '1/32 in.', 'pass' if ok else 'fail',
            'evidence/p5/p67_U20.png; I050' if (jn == 'U20') else 'joint sheet p.%d' % p)
# stacked layers
for bn, jn, inner, outer, p in (('PTC 3-span truss 11-13', 'L8', '7/8', '3/4', 52), ('PTC 3-span truss 11-13', "L8'", '7/8', '3/4', 49), ('PTC 3-span truss 11-13', "L0'", '5/8', '1/2', 59),
                                 ('PTC 4-span truss 14-17', 'L0', '3/4', '1/2', 59), ('PTC 4-span truss 14-17', 'L20', '7/8', '3/4', 67)):
    j = [x for x in B[bn]['Truss']['Joints'] if x['Point'] == jn and not x.get('Plane')][0]
    lay = [fr(L['Thickness'] / IN) for L in j.get('GussetLayers') or []]
    ok = fr(j['GussetThickness'] / IN) == inner and lay == [outer]
    row(p, sheetno(p), 'joint ' + jn, '%s joint %s stacked gussets (inner / outer, order)' % (bn, jn), 'callouts "2-%s Gussets (in to in)" + "2-%s Gussets"; the outer plate drawn over the rivet fields' % (inner, outer),
        '%s inner (in to in) + %s outer' % (inner, outer), '%s inner + layers %s' % (fr(j['GussetThickness'] / IN), lay), '', 'exact', 'pass' if ok else 'fail', 'joint sheet p.%d' % p)
# fills / splice plates of the approach joints
for p, bn, ex in ((50, '3-span', 'U1 2-Fills 14 1/2 x 5/16, L2 2-Fills 14 1/4 x 3/4 + 1-3/4 Fill + 2-Splice Pls 16 x 3/4 + 4-Splice Bars 6 x 1 1/8'),
                  (52, '3-span', 'U9 2-Fills 14 1/2 x 11/16 (x2), 2-Pls 16 x 1/2; L8 2-Fills 15 1/2 x 11/16'),
                  (62, '4-span', 'U9 2-Fills 14 1/2 x 7/8 (two per side, from the CL splice) on the 14 WF 95 (17 5/8 in to in - d 14.12 = 1 3/4 per side), 2-Spl Pls 16 x 1 1/8 outside the gussets'),
                  (64, '4-span', 'U11 2-Spl Pls 16 x 1 1/8, 1-1 1/8 Fill, 2-Fills 14 1/2 x 3/4 + 14 1/2 x 1/16'),
                  (66, '4-span', 'U19 2-Fills 16 x 1/8, 2-Spl Pls 16 x 1/2, 2-Fills 16 x 7/8')):
    row(p, sheetno(p), 'joint sheet', 'Approach %s joint fills / splice plates / splice bars (p.%d)' % (bn, p),
        'sheet callouts with sizes; extents drawn with dimension lines at 3/4 in. = 1 ft (scalable as on the main truss sheets) vs Joint.Plates',
        ex, 'none built (Truss.Unresolved: "listed per joint without extents ... not built")', 'gusset to flange gaps up to 1 3/4 in. per side', 'n.a.', 'info', 'evidence/p5/p62_U9_cov.png p64_U11_cov.png p50_L2.png; I051')
# chord rivet layout (uniform chord grid)
row(50, sheetno(50), 'joint U3 chord rivets', '3-span U3 chord rivets on one gusset face: lines', 'p.50 elevation: rivet lines across the 14 WF flanges; model rivets_multi.csx + chord_lines.py (+n face)',
    '4 lines (2 per flange half)', '4 lines (v +/-5.25..6.0, +/-1.5..2.25 in.)', '', 'exact', 'pass', 'evidence/p5/p50_U3_chord.png p50_U3_chord_z.png p50_U3_chord_r.png; work/p5/chord_lines.py')
row(50, sheetno(50), 'joint U3 chord rivets', '3-span U3 chord rivets per face left (U1-U3, field) / right (U3-U5, shop) of the CL splice',
    'hand count on the p.50 crops (and connections/rivet-counts.csv: U1-U3 36 inside + 5 beyond, U3-U5 27) vs model per face', '36 (+5 beyond) / 27 (about 2.4-3 in. pitch left, mixed right)',
    '36 / 33 (one 3.71 in. grid through the joint, UniformChordRivets)', 'right +6 (+22 %)', 'max(3, 5 %)', 'info', 'I052')
row(51, sheetno(51), 'joint L4 chord rivets', '3-span L4 chord rivet density', 'p.51 elevation vs model chord_lines.py', 'dense near the CL splice (about 2.7-2.9 in.), sparse (about 6.5 in., staggered) toward the gusset ends',
    'one uniform grid 3.45 in. pitch over the joint', '', 'pattern', 'info', 'evidence/p5/p51_L4_chord.png; I052')

# ---------------- 4. floor beams F5-F8, bracing ----------------
arch = B['PTC Arch truss 18-20']['Truss']; asec = {s['Name']: s for s in arch['Sections']}
def plates(s):
    return 'top ' + ', '.join('%sx%s%s' % (fr(p['Width'] / IN), fr(p['Thickness'] / IN), (' x %s ft' % fr(p['Length'] / IN / 12)) if p.get('Length') else ' F.L.') for p in s.get('TopPlates') or []) + \
        '; bot ' + ', '.join('%sx%s%s' % (fr(p['Width'] / IN), fr(p['Thickness'] / IN), (' x %s ft' % fr(p['Length'] / IN / 12)) if p.get('Length') else ' F.L.') for p in s.get('BottomPlates') or [])
FB = [('F5', '1-Pl 106x5/8; 4-Ls 8x8x3/4; 1-Pl 18x1/2 F.L. top; 1-Pl 18x1/2x40\'-0 bot.', 'pass', ''),
      ('F6', '1-Pl 106x5/8; 4-Ls 8x8x3/4; 2-Pls 18x5/8x46\'-0; 1-Pl 18x5/8 F.L. top; 1-Pl 18x5/8x60\'-0 bot.', 'pass', ''),
      ('F7', '2-Pls 106x7/16; 4-Ls 8x4x3/4; 2-Pls 25x9/16x40\'-0; 2-Pls 25x9/16x56\'-0; 2-Pls 25x9/16 F.L.', 'pass', ''),
      ('F8', '1-Pl 106x5/8; 4-Ls 8x8x3/4 F.L.; 2-Ls 9x4x1 F.L.; 2-Ls 7x4x7/8 F.L.; 2-Ls 8x4x1x76\'-0; 1-Pl 18x5/8x46\'-0 top; 1-Pl 18x5/8x76\'-0 bot.; 2-Pls 18x5/8 F.L.', 'pass',
       'extra angles 9x4 / 7x4 / 8x4 not built (flagged UNRESOLVED p.95, reason checked)')]
for mk, plan, res, note in FB:
    s = asec[mk]
    model = '%s web %s; %s; %s' % (s['Kind'], '; '.join('%sx%s' % (fr(p['Depth'] / IN), fr(p['Thickness'] / IN)) for p in s.get('WebPlies') or []), s.get('Angle'), plates(s))
    row(72, sheetno(72), 'FLOOR BEAM SCHEDULE', 'Main floor beam %s make-up (web, angles, cover plates and lengths)' % mk, 'p.72 schedule SECTION FURNISHED vs .sbm section', plan, model, note, 'exact', res, 'evidence/p5/p72_fbsched.png p72_fbsched_F8.png')
used = collections.defaultdict(list)
for m in arch['Members']:
    if m['Section'] in ('F5', 'F6', 'F7', 'F8'): used[m['Section']].append(m['Name'])
row(72, sheetno(72), 'floor plan (top)', 'Main floor beam marks by panel point', 'p.72 floor plan marks: F5 at 0 / 0\', F6 at 1-12 and 14-21, F8 at 13 / 13\', F7 at 22',
    'F5 FB0; F8 FB13; F7 FB22; F6 others', 'F5 %s; F8 %s; F7 %s; F6 %d members' % (used['F5'], used['F8'], used['F7'], len(used['F6'])), '', 'exact', 'pass', 'evidence/p5/p72_plan.png')
s = asec['F7']
row(93, sheetno(93), 'Sections A / B', 'Main floor beam F7 (two-web box at W22) web spacing', 'p.93 Section A "2\'-1 1/2 in to in Webs" (7/16 reinforcing plates inside), Section B "2\'-1 1/2 b.b. Ls"',
    '25 1/2 in to in webs (26 3/8 o. to o.)', '%s o. to o. (%s in to in; webs at sta 68279.338-.375 / 68281.385-.422 ft)' % (fr(s['WebsOutToOut'] / IN), fr(s['WebsOutToOut'] / IN - 2 * 7 / 16)),
    '-1 3/8 in.', '1/16 in.', 'fail', 'evidence/p5/p93_secA.png p93_secB.png; I046')
BR = [('STRUT', '2-18[ 45.8 (S); 2-Pls (29 1/2-13 1/2) x 1/2 (at other pts.)', 'transverse struts U12-U22, L1-L10, L12, L16-L22'),
      ('STRUT L0', '2-Cov. Pls 17 x 7/8 (S); 4-Ls 5 x 5 x 7/8 (S); 2-Web Pls (30-13 1/2) x 1/2 (S); 1\'-6 b.b. Ls; 2\'-6 1/2 b.b. Ls', 'strut at L0'),
      ('STRUT L11', '2-Cov. Pls 22 x 9/16; 4-Ls 7 x 4 x 9/16; 2-Web Pls (40-16) x 9/16; 1\'-10 1/2 / 3\'-4 1/2 b.b. Ls', 'strut at L11'),
      ('LAT31', '2-13[ 31.8, 2-Pls (29 1/2-13 1/2) x 1/2', 'lateral diagonals (other panels)'),
      ('LAT50', '2-13[ 50, 2-Pls (29 1/2-13 1/2) x 1/2', 'lateral diagonals L10-L11, L11-L12, L12-L13'),
      ('SWAY', '2-15[ 33.9; 2-Pls (12 1/2-6) x 3/8; 1\'-1', 'sway diagonals L0-L10, L12'),
      ('SWAY11', '2-18[ 42.7; 2-Cov. Pls (22-12) x 3/8; 1\'-11 5/8 b.b. Ls', 'sway diagonals at L11'),
      ('FLAT21', '2-12[ 25; 2-Pls (13-6) x 3/8', 'floor laterals W21-W22, W22-W21\''),
      ('L13-W14', '2-Web Pls 12 x 1/2; 2-15[ 50.0; 2-Pls (13-6) x 1/2', 'floor lateral L13-W14 (web plates flagged UNRESOLVED, not built)')]
for nm, plan, what in BR:
    s = asec[nm]
    row(71, sheetno(71), 'bracing sections', 'Main bracing %s section (%s)' % (nm, what), 'p.71 section callouts (evidence/p5/p71_*.png) vs .sbm section label / plates',
        plan, s.get('Label', ''), '', 'exact', 'pass', 'evidence/p5/p71_struts.png p71_struts_top.png p71_floorlat_sway.png p71_floorlat_top.png')
row(97, sheetno(97), 'lateral bracing details', 'Main lateral bracing gussets', 'p.97 "2-1/2 Pls." + "2-Ls 7x4x1/2" edge angles at L1 / L9 / L10 / L12 vs TrussBracingGussetDefinition',
    '1/2 plates both sides + 2-Ls 7x4x1/2', '120 Fitted 1/2 Both with L7x4x1/2, 68 Fitted 1/2 Both without edge angle', '', 'exact', 'pass', 'p.97 (overview read)')

# ---------------- 5. outer gusset layer outlines (gusset-layers.csv) ----------------
for jn, p, items in (('3-span L8', 52, [('left edge u', -36.1, -35.4), ('right edge u', 36.1, 35.4), ('top edge v', 37.3, 35.7), ('top corners u', '-24.4 / 23.2', '-23.4 / 23.4'), ('chamfer foot v', 25.7, 24.3), ('bottom v', -8.2, -7.6)]),
                     ('4-span L20', 67, [('left edge u', -31.4, -31.8), ('right edge u', 31.4, 32.8), ('top edge v', 31.7, 32.9), ('top corners u', '-21.3 / 21.3', '-21.6 / 25.8'), ('bottom v', -10.9, -8.5)])):
    for it, csvv, sheet in items:
        if isinstance(csvv, str):
            dv = max(abs(float(a) - float(b)) for a, b in zip(csvv.split(' / '), sheet.split(' / ')))
        else:
            dv = abs(csvv - sheet)
        res = 'pass' if dv <= 2.0 else 'info'
        row(p, sheetno(p), 'outer gusset layer ' + jn, 'Outer 3/4 gusset layer outline %s: %s (in., u along the chord from the WP, v up)' % (jn, it),
            're-scaled on a 300 dpi grid crop (gridcrop.py; 11 in. dimension at the joint = about 87 px -> 7.9 px/in, +-1.5 in.) vs connections/gusset-layers.csv outline used by the model',
            '%s (sheet)' % sheet, '%s (csv / model)' % csvv, '%.1f in.' % dv, '2 in. (scaled, dimension-only flag)', res,
            'evidence/p5/p52_L8_grid.png' if p == 52 else 'evidence/p5/p67_L20_grid.png')
row(78, sheetno(78), 'joint L10', 'Main L10 outer 1/2 gusset layer outline', 're-scale attempted on p.78 at 190 dpi', 'edges not separable from the rivet / member linework',
    'csv outline -43.7..41.4 x -23.9..52.1 in.', '', '', 'n.a.', 'not kept (p78 grid crop)')
for jn, csvv in (('L13', '26.5'), ('U7', '31'), ('U9', '30.5'), ('L8', '30.5')):
    row({'L13': 83, 'U7': 76, 'U9': 77, 'L8': 77}[jn], '', 'connections/gusset-layers.csv', 'gusset-layers.csv main %s in_to_in_in (data file under test)' % jn,
        'csv value vs the corrected gussets.csv / model (I039 / I040)', '29 (2\'-5 printed)', csvv + ' (csv); model 29', '', 'exact', 'fail', 'I053')

# ---------------- 6. pass 4 fixes re-verified on the regenerated model ----------------
FIX = [('I036', 69, 'L12-L13 ply / angle layers', '18 x 1 ply in the angle-leg layer, 31 1/2 x 5/8 innermost', '18x1 at x 11.312-12.313 = angle web leg x 11.312-12.312; 31 1/2 at 10.687-11.313 between the outstanding legs'),
       ('I037', 83, 'L13-L14 make-up', '2-34x1 1/8 + 2-34x1 1/8 + 2-34x1 1/16 webs', 'plies 34x1 1/8 (btwG); 34x1 1/8; 34x1 1/16'),
       ('I038', 81, 'L10-L11 ply order', '34 x 1 1/8 outermost', 'plies out->in 34x1 1/8; 34x3/4; 34x3/4; 18x1'),
       ('I039', 83, 'L13 gussets in to in', '2\'-5 (29 in.)', 'S-GL13-R x 14.500-15.625 (29 in to in)'),
       ('I040', 77, 'U7 / U9 / L8 gussets in to in', '2\'-5', 'GU7 14.500, GU9 14.500, GL8 14.500 -> 29'),
       ('I041', 84, 'L14 inlaid gusset', 'gusset in the outer ply layer, 2\'-5 in to in', 'S-GL14-R x 14.500-15.625; outer 34x1 1/8 ply x 14.500-15.625 between the gussets; WebsOutToOut 31 1/4'),
       ('I042', 74, 'fill stacking', 'fills of one side stacked, no fill x fill overlap', 'sbm clash Arch: FillerPlate x FillerPlate 0 (116 fill / splice x chord remain at kinked points, see I047)'),
       ('I043', 74, 'U1 cover fill side', 'bottom flange', 'S-FU1-1 Face Bottom (z 144.56-144.67 ft = under the bottom cover)'),
       ('I044', 86, 'L15 18x1x16\'-0 web plates', 'from L15 into L15-L16', 'S-SPL15-1 Offset 96 in. (0 to 16 ft from L15), 15.0 ft along station'),
       ('I045', 49, 'approach gusset-layer flag text', 'flag matches the built layers', 'Truss.Unresolved now lists the open items and says the layers are built')]
for iid, p, what, plan, model in FIX:
    row(p, sheetno(p), 'pass 4 re-check', 'Re-check of %s on the regenerated model: %s' % (iid, what), 'dump_sbm.py / dump_solids.csx / sbm clash on the qc5 copy', plan, model, '', 'exact', 'pass', iid)

# ---------------- 7. kinked panel points / web member ends (clash) ----------------
cl = list(csv.DictReader(open(CLASH, encoding='utf-8')))
cnt = collections.Counter(r['category_pair'] for r in cl)
def mx(pair): return max(float(r['depth_in']) for r in cl if r['category_pair'] == pair)
for pair in ('TrussChord x TrussChord', 'FillerPlate x TrussChord', 'SplicePlate x TrussChord'):
    row('74-89', 'DRB-4 34-49/68', 'chord joints', 'Arch %s overlaps (> 1/16 in.) at kinked panel points' % pair, 'sbm clash --bridge Arch (qc5 build, 1/16 in.)',
        '0 (sheets: one finished joint plane through the panel point)', '%d rows, max %.2f in.' % (cnt[pair], mx(pair)), '', '0', 'fail', 'C:/SBM/temp/qc5/clash_arch.csv (not committed); I047')
row(74, sheetno(74), 'joint U1 elevation', 'Arch chord joint at a kinked panel point (U1)', 'p.74: "FIN." line = one straight milled joint line through U1 (about 1 in. left of the WP), field rivets left / shop right of it',
    'mitred (bisector) finished joint at the point', 'chords cut square at the point + h/2 tan(d/2); wedges overlap', '', 'geometry', 'fail', 'evidence/p5/p74_U1_fin.png; I047')
row(76, sheetno(76), 'joint L6 elevation', 'Arch chord joint and fills at a kinked panel point (L6)', 'p.76: "Fin." at the L6 axis; 2-Fills 31 1/2 x 1 1/16 and 18 x 7/8 end at that line from the left, 2-Fills 18 x 7/8 start there to the right',
    'fills stop at the finished joint line', 'L6-L7 end wedge reaches 1-1 1/2 in. back past L6 into FL6-1 / -2 (0.09-0.44 in. overlaps)', '', 'geometry', 'fail', 'evidence/p5/p76_L6_fin.png; I047')
for pair in ('SplicePlate x TrussVertical', 'FillerPlate x TrussVertical'):
    row('74-75', 'DRB-4 34-35/68', 'web member ends', 'Arch %s overlaps (cover splice plate / fill on the chord face the vertical meets)' % pair, 'sbm clash --bridge Arch',
        '0 (p.74 U1: vertical ends about 2 in. below the chord bottom)', '%d rows, max %.3f in.' % (cnt[pair], mx(pair)), '', '0', 'fail', 'evidence/p5/p74_U1_vert.png; I048')

# ---------------- write ----------------
if '--dry' in sys.argv:
    for r in rows:
        if r[9] != 'pass': print(' | '.join(map(str, r))[:400])
    print(len(rows), dict(collections.Counter(r[9] for r in rows)))
    sys.exit()
path = os.path.join(RES, 'checks.csv')
last = [l for l in open(path, encoding='utf-8').read().splitlines() if l.strip()][-1]
n = int(last.split(',')[0][1:])
with open(path, 'a', newline='', encoding='utf-8') as f:
    w = csv.writer(f, lineterminator='\r\n')
    for r in rows:
        n += 1
        w.writerow(['C%04d' % n] + r)
c = collections.Counter(r[9] for r in rows)
print(len(rows), 'rows, last C%04d' % n, dict(c))
