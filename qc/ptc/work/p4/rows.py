"""QC pass 4: build check rows (member make-up and layered plates) and append them to results/checks.csv. Run once."""
import json, csv, os
from fractions import Fraction as F
HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, '..', '..', 'results')
SBM = r'C:\SBM\temp\qc4\ptc.sbm'
IN = 0.0254
d = json.load(open(SBM))
def truss(name):
    return [b for b in d['Bridges'] if b['Name'] == name][0]['Truss']
arch = truss('PTC Arch truss 18-20')
secs = {s['Name']: s for s in arch['Sections']}
def r16(x): return F(round(x / IN * 16), 16)
def fx(f):
    f = F(f); w = int(f); r = f - w
    return (('%d %s' % (w, r)) if w and r else ('%d' % w if not r else str(r)))
# ---- p.69 SECTION column as read in pass 4 (webs per side as (depth, thk) x count-per-side; angle; top cover; bottom cover (24-12) or (23 1/2-11 1/2))
def W(*l): return [(F(a), F(b), n) for a, b, n in l]
h = F(1, 2)
P69 = {
 'U0-U1': (W((34, '3/8', 1)), '6x6x3/8', ('24', '7/16'), ('24', '3/8')),
 'U1-U2': (W((34, '11/16', 1)), '8x6x1/2', ('24', '1/2'), ('24', '5/8')),
 'U2-U3': (W((34, '11/16', 1)), '8x6x1/2', ('24', '1/2'), ('24', '5/8')),
 'U3-U4': (W((34, '3/4', 1)), '8x6x5/8', ('24', '9/16'), ('24', '11/16')),
 'U4-U5': (W((34, '3/4', 1)), '8x6x5/8', ('24', '9/16'), ('24', '11/16')),
 'U5-U6': (W((34, '9/8', 1), (34, '3/4', 1)), '8x6x3/4', ('24', '5/8'), ('24', '3/4')),
 'U6-U7': (W((34, '9/8', 1), (34, '3/4', 1)), '8x6x3/4', ('24', '11/16'), ('24', '13/16')),
 'U7-U8': (W((34, '9/8', 1), (34, '13/16', 1), (18, '3/4', 1), ('63/2', '1/2', 1)), '8x6x3/4', ('24', '3/4'), ('24', '7/8')),
 'U8-U9': (W((34, '9/8', 1), (34, '13/16', 1), (18, '3/4', 1), ('63/2', '1/2', 1)), '8x6x3/4', ('24', '13/16'), ('24', '15/16')),
 'U9-U10': (W((34, '9/8', 1), (34, '13/16', 1), (18, '7/8', 1), ('63/2', '15/16', 1)), '8x6x7/8', ('24', '13/16'), ('24', '15/16')),
 'U10-U11': (W((34, '9/8', 1), (34, '13/16', 1), (18, '7/8', 1), ('63/2', '15/16', 1)), '8x6x7/8', ('24', '7/8'), ('24', '1')),
 'U11-U12': (W((34, '9/8', 1), (34, '13/16', 1), (18, '7/8', 1), ('63/2', '9/16', 1)), '8x6x7/8', ('24', '7/8'), ('24', '1')),
 'U12-U13': (W((34, '9/8', 1), (34, '13/16', 1), (18, '5/8', 1)), '8x6x7/8', ('24', '13/16'), ('24', '15/16')),
 'U13-U14': (W((34, '9/8', 1), (18, '13/16', 1)), '8x6x7/8', ('24', '3/4'), ('24', '7/8')),
 'U14-U15': (W((34, '15/16', 1)), '8x6x3/4', ('24', '1/2'), ('24', '5/8')),
 'U15-U16': (W((34, '5/8', 1)), '8x6x1/2', ('24', '1/2'), ('24', '5/8')),
 'U16-U17': (W((34, '13/16', 1)), '8x6x1/2', ('24', '1/2'), ('24', '5/8')),
 'U17-U18': (W((34, '13/16', 1)), '8x6x1/2', ('24', '9/16'), ('24', '11/16')),
 'U18-U19': (W((34, '7/8', 1), (18, '3/4', 1)), '8x6x3/4', ('24', '11/16'), ('24', '13/16')),
 'U19-U20': (W((34, '9/8', 1), (18, '3/4', 1), ('63/2', '9/16', 1)), '8x6x3/4', ('24', '3/4'), ('24', '7/8')),
 'U20-U21': (W((34, '9/8', 1), (18, '1', 1), ('63/2', '13/16', 1)), '8x6x1', ('24', '3/4'), ('24', '7/8')),
 'U21-U22': (W((34, '9/8', 1), (18, '1', 1), ('63/2', '17/16', 1)), '8x6x1', ('24', '3/4'), ('24', '7/8')),
 'L0-L2': (W((34, '11/16', 1)), '8x6x1/2', ('24', '1/2'), ('24', '5/8')),
 'L2-L4': (W((34, '11/16', 1)), '8x6x1/2', ('24', '1/2'), ('24', '5/8')),
 'L4-L5': (W((34, '9/8', 1)), '8x6x7/8', ('24', '5/8'), ('24', '3/4')),
 'L5-L6': (W((34, '9/8', 1)), '8x6x7/8', ('24', '11/16'), ('24', '13/16')),
 'L6-L7': (W((34, '9/8', 1), (34, '17/16', 1)), '8x6x7/8', ('24', '3/4'), ('24', '7/8')),
 'L7-L8': (W((34, '9/8', 1), (34, '17/16', 1)), '8x6x7/8', ('24', '3/4'), ('24', '7/8')),
 'L8-L9': (W((34, '9/8', 2), (18, '1', 1)), '8x6x1', ('24', '7/8'), ('24', '1')),
 'L9-L10': (W((34, '9/8', 2), (18, '1', 1)), '8x6x1', ('24', '15/16'), ('24', '17/16')),
 'L10-L11': (W((34, '3/4', 2), (34, '9/8', 1), (18, '1', 1)), '8x6x1', ('47/2', '15/16'), ('47/2', '17/16')),
 'L11-L12': (W((34, '3/4', 2), (34, '9/8', 1), (18, '1', 1), ('63/2', '1', 1)), '8x6x1', ('47/2', '1'), ('47/2', '9/8')),
 'L12-L13': (W((34, '9/8', 1), (34, '17/16', 1), (18, '1', 1), ('63/2', '5/8', 1)), '8x6x1', ('24', '1'), ('24', '9/8')),
 # L13-L17 "See Sketch Below": webs from the half longitudinal section + joint sheets p.83 / p.84 / p.86
 'L13-L14': (W((34, '9/8', 2), (34, '17/16', 1)), '8x6x1', ('24', '15/16'), ('24', '17/16')),
 'L14-L15': (W((34, '9/8', 2), (18, '1', 1)), '8x6x1', ('24', '7/8'), ('24', '1')),
 'L15-L16': (W((34, '9/8', 1)), '8x6x1', ('24', '13/16'), ('24', '15/16')),
 'L16-L17': (W((34, '13/16', 1)), '8x6x9/16', ('24', '9/16'), ('24', '11/16')),
 'L17-L18': (W((34, '13/16', 1)), '8x6x1/2', ('24', '9/16'), ('24', '11/16')),
 'L18-L19': (W((34, '13/16', 1)), '8x6x1/2', ('24', '9/16'), ('24', '11/16')),
 'L19-L20': (W((34, '1', 1), (18, '11/16', 1)), '8x6x3/4', ('24', '5/8'), ('24', '3/4')),
 'L20-L21': (W((34, '1', 1), (34, '11/16', 1), (18, '3/4', 1)), '8x6x3/4', ('24', '3/4'), ('24', '7/8')),
 'L21-L22': (W((34, '1', 1), (34, '17/16', 1), (18, '7/8', 1)), '8x6x7/8', ('24', '13/16'), ('24', '15/16')),
}
rows = []
def add(page, sheet, view, item, method, plan, model, diff, tol, result, ev):
    rows.append([page, sheet, view, item, method, plan, model, diff, tol, result, ev])
for k, (webs, ang, top, bot) in P69.items():
    s = secs['CH ' + k]
    mp = {}
    for p in s['WebPlies']:
        key = (r16(p['Depth'] or s['WebDepth']), r16(p['Thickness'])); mp[key] = mp.get(key, 0) + 1
    pp = {(a, b): n for a, b, n in webs}
    plan_t = '; '.join('%dx %s x %s' % (n, fx(a), fx(b)) for (a, b), n in pp.items())
    mod_t = '; '.join('%dx %s x %s' % (n, fx(a), fx(b)) for (a, b), n in mp.items())
    ok = pp == mp
    page = 69 if k not in ('L13-L14',) else '69/83/84'
    note = ''
    if k == 'L13-L14': note = ' (sketch + p.83 / p.84 callouts "4-Web Pls 34x1 1/8; 2-Web Pls 34x1 1/16")'
    add(page, 'DRB-4 29/68', 'p.69 chord SECTION column / L13-L17 sketch', 'Arch chord %s web plies per side%s' % (k, note), 'plan reading (pass 4, 280-320 dpi crops) vs .sbm section WebPlies', plan_t, mod_t, '' if ok else 'make-up differs', 'exact', 'pass' if ok else 'fail', 'work/p4/rows.py; evidence/p4/p69-half-section-L13-L15.png' if k == 'L13-L14' else 'work/p4/rows.py')
    ma = s['Angle'].replace('L', '').replace('-', ' ')
    okA = ma.replace(' ', '') == ang.replace(' ', '') or ma == ang
    add(69, 'DRB-4 29/68', 'p.69 chord SECTION column', 'Arch chord %s angles (4-Ls)' % k, 'plan reading vs section Angle (4 inside angles, AngleCount 2 per web)', '4-Ls ' + ang, '4-Ls ' + s['Angle'], '', 'exact', 'pass' if okA else 'fail', 'work/p4/rows.py')
    tp = s['TopPlates'][0]; bp = s['BottomPlates'][0]
    okC = (r16(tp['Width']), r16(tp['Thickness'])) == (F(top[0]), F(top[1])) and (r16(bp['Width']), r16(bp['Thickness'])) == (F(bot[0]), F(bot[1])) and bp.get('Perforated') and not tp.get('Perforated')
    add(69, 'DRB-4 29/68', 'p.69 chord SECTION column', 'Arch chord %s cover plates (top solid / bottom (w-12) perforated)' % k, 'plan reading vs TopPlates / BottomPlates', '%s x %s / (%s-%s) x %s' % (fx(F(top[0])), top[1], fx(F(bot[0])), fx(F(bot[0]) / 2), bot[1]), '%s x %s / %s x %s perf=%s' % (fx(r16(tp['Width'])), fx(r16(tp['Thickness'])), fx(r16(bp['Width'])), fx(r16(bp['Thickness'])), bp.get('Perforated')), '', 'exact', 'pass' if okC else 'fail', 'work/p4/rows.py')
# ---- ply order (angle layer) for sections with 18 in. and 31 1/2 in. plies
for k in ['U7-U8', 'U8-U9', 'U9-U10', 'U10-U11', 'U11-U12', 'U19-U20', 'U20-U21', 'U21-U22', 'L11-L12', 'L12-L13']:
    add('69/81', 'DRB-4 29/68, 41/68', 'p.69 half longitudinal section ("18x1 Web & 1in Ls" one layer, "31 1/2 x 5/8 Web" innermost); p.81 L12 plan view (31 1/2 x 1 web = inner line)',
        'Arch chord %s: position of the inside angles and the 18 in. ply relative to the 31 1/2 in. ply' % k,
        'dump_solids.csx (exact extrusion recipes, x from the truss plane): ply / angle x ranges at mid member',
        'out -> in: 34 plies | 18 ply between the angle legs (same layer) | 31 1/2 ply inside the angle legs, between the horizontal legs',
        'out -> in: 34 plies | 18 ply | 31 1/2 ply | angles standing on the inner face of the 31 1/2 ply (e.g. S-U9-U10: angles x 4.75-10.75 in., 31 1/2 at 10.75-11.69, 18 at 11.69-12.56)',
        'angles 9/16 - 1 1/16 in. too far inboard; void beside the 18 ply', 'layer order', 'fail', 'evidence/p4/p69-half-section-L13-L15.png; evidence/p4/p81-L12-oo-webs.png; I036')
for k in ['L10-L11', 'L11-L12']:
    add(81, 'DRB-4 41/68', 'p.81 L12 half flange plan "2\'-5 o. to o. 1 1/8 in. webs"', 'Arch chord %s: outermost web ply' % k, 'plan reading vs .sbm WebPlies order (out -> in)', '34 x 1 1/8 outermost (4-34x3/4 inside it)', '34 x 3/4, 34 x 3/4 outside, 34 x 1 1/8 third', '', 'order', 'fail', 'evidence/p4/p81-L12-oo-webs.png; I038')
# ---- main truss gussets: thickness + in-to-in as printed on the joint sheets
JT = {  # joint: (page, t, layers, in-to-in printed (in), note)
 'U1': (74, '3/4', None, 29, ''), 'U2': (74, '5/8', None, 29, ''), 'L2': (74, '3/4', None, 29, ''),
 'U3': (75, '3/4', None, 29, ''), 'L3': (75, '1/2', None, None, ''), 'U4': (75, '5/8', None, 29, ''), 'L4': (75, '3/4', None, 29, ''), 'U5': (75, '7/8', None, 29, ''), 'L5': (75, '3/4', None, None, ''),
 'U6': (76, '5/8', None, 31, '2\'-7 in to in gussets, 2\'-5 o. to o. webs (o.s. splice 34x1)'), 'L6': (76, '7/8', None, 29, ''),
 'U7': (76, '9/8', None, 29, '2\'-5 in. to in. Gussets (inlay plates, no o.s. splice plate)'), 'L7': (76, '5/8', None, 31, ''),
 'U8': (77, '5/8', None, F(61, 2), '(2\'-6 1/2 in to in)'), 'L8': (77, '1', None, 29, '2\'-5 in. to in. gussets (inlay plates 34x1 1/8 - o.s.)'),
 'U9': (77, '1', None, 29, '2\'-5 in. to in. gussets (inlay plates 34x1 1/8 - o.s.)'), 'L9': (77, '5/8', None, F(61, 2), '(2\'-6 1/2 in to in)'),
 'U10': (78, '3/4', None, F(125, 4), '2\'-7 1/4 in to in gussets'), 'L10': (78, '3/4', ['1/2'], 29, '2-3/4 and 2-1/2 Gussets'),
 'U12': (81, '9/8', None, 29, ''), 'L12': (81, '1', None, 29, ''),
 'U13': (82, '9/8', None, 29, ''), 'U14': (82, '9/8', None, 29, ''), 'U15': (82, '7/8', None, 29, ''),
 'L13': (83, '9/8', None, 29, '2\'-5 in to in Guss. Pls. (Sections A, B and half flange view)'), 'L14': (84, '9/8', None, 29, '1 1/8 Inlaid Gusset Pl.; 2\'-5 in to in Gussets'),
 'L15': (86, '9/8', None, 29, ''), 'L16': (86, '1', None, 29, ''),
 'U16': (87, '7/8', None, 29, ''), 'U17': (87, '7/8', None, 29, ''), 'L17': (87, '7/8', None, 29, ''),
 'U18': (88, '7/8', None, 29, ''), 'L18': (88, '7/8', None, 29, ''), 'U19': (88, '7/8', None, 29, ''), 'L19': (88, '7/8', None, 29, ''),
 'U20': (89, '7/8', None, 29, ''), 'L20': (89, '1', None, 29, ''), 'U21': (89, '3/4', None, F(125, 4), '(2\'-7 1/4 In. to In.)'), 'L21': (89, '1', None, F(125, 4), '(2\'-7 1/4 In. to In.)'),
 'L22': (90, '1', ['1 1/8'], 29, '2-1in Gus. Pls. + 2-1 1/8 Gus. Pls.'),
}
jd = {j['Point']: j for j in arch['Joints']}
for jn, (pg, t, lay, ii, note) in JT.items():
    j = jd[jn]
    mt = r16(j['GussetThickness'])
    ml = [fx(r16(L['Thickness'])) for L in (j.get('GussetLayers') or [])]
    okt = mt == F(t) and (ml == (lay or []))
    add(pg, 'DRB-4 %d/68' % (pg - 40), 'joint %s gusset callout' % jn, 'Arch joint %s gusset thickness (+ stacked layers)' % jn, 'plan reading vs .sbm joint GussetThickness / GussetLayers', fx(F(t)) + ((' + ' + ' + '.join(lay)) if lay else ''), fx(mt) + ((' + ' + ' + '.join(ml)) if ml else ''), '', 'exact', 'pass' if okt else 'fail', 'work/p4/rows.py')
    if ii is not None:
        mi = j.get('GussetsInsideFaceDistance') or 0
        mi = r16(mi) if mi else F(29)  # 0 = chord box width (o-o webs 29 in.)
        ok = mi == F(ii)
        if jn == 'L14': ok = False
        add(pg, 'DRB-4 %d/68' % (pg - 40), 'joint %s in to in gussets %s' % (jn, note), 'Arch joint %s gussets in to in (in.)' % jn, 'plan dimension vs .sbm GussetsInsideFaceDistance (0 = webs o-o 29) and the gusset solids (dump_solids.csx)', str(float(ii)), str(float(mi)) + (' (gusset modeled outside the chord, not inlaid)' if jn == 'L14' else ''), str(float(mi - F(ii))), '1/16', 'pass' if ok else 'fail', 'work/p4/rows.py' + ({'U7': '; evidence/p4/p76-U7-in-to-in.png; I040', 'U9': '; evidence/p4/p77-U9-in-to-in.png; I040', 'L8': '; evidence/p4/p77-L8-in-to-in.png; I040', 'L13': '; evidence/p4/p83-L13-sections-in-to-in.png; I039', 'L14': '; evidence/p4/p84-L14-inlaid-gusset.png; I041'}.get(jn, '')))
# ---- approach 4-span joints p.60 / p.63
a4 = {j['Point']: j for j in truss('PTC 4-span truss 14-17')['Joints']}
for jn, pg, t, lay, ii in [('U1', 60, '3/4', None, F(67, 4)), ('L1', 60, '5/8', None, F(59, 4)), ('U2', 60, '5/8', None, F(67, 4)), ('L2', 60, '7/8', None, F(139, 8)), ('U3', 60, '7/8', None, F(67, 4)), ('L3', 60, '5/8', None, F(69, 4)), ('U10', 63, '5/8', None, F(153, 8)), ('L10', 63, '7/8', ['7/8'], F(141, 8))]:
    j = a4[jn]
    mt = r16(j['GussetThickness']); ml = [fx(r16(L['Thickness'])) for L in (j.get('GussetLayers') or [])]; mi = r16(j['GussetsInsideFaceDistance'])
    ok = mt == F(t) and ml == (lay or []) and mi == ii
    add(pg, 'DRB-4 %d/68' % (pg - 40), 'joint %s gusset callout "(.. in. to in.)"' % jn, '4-span joint %s gusset thickness / layers / in to in' % jn, 'plan reading vs .sbm (PTC 4-span truss 14-17; 21-24 is the same data)', '%s%s / %s in.' % (fx(F(t)), (' + ' + ' + '.join(lay)) if lay else '', float(ii)), '%s%s / %s in.' % (fx(mt), (' + ' + ' + '.join(ml)) if ml else '', float(mi)), '', 'exact', 'pass' if ok else 'fail', 'work/p4/rows.py')
# ---- girder spans p.44
g = [s for s in truss('PTC Girder spans 1-10')['Sections'] if s['Name'] == 'G1'][0]
add(44, 'DRB-4 4/68', 'Inside elevation of main girders', 'Girder G1 web plate', 'plan reading vs .sbm section G1', 'Web 114 x 7/16', '%s x %s' % (fx(r16(g['WebDepth'])), fx(r16(g['WebPlies'][0]['Thickness']))), '', 'exact', 'pass', 'work/p4/dump_PTC_Girder_spans_1-10.txt')
add(44, 'DRB-4 4/68', 'Inside elevation of main girders', 'Girder G1 flange angles', 'plan reading vs .sbm', '4-Ls 8 x 8 x 1 1/8', g['Angle'], '', 'exact', 'pass', 'work/p4/dump_PTC_Girder_spans_1-10.txt')
add(44, 'DRB-4 4/68', 'Inside elevation of main girders', 'Girder G1 back to back of angles', 'web depth + 2 x angle projection', "9'-6 1/2 = 114.5 in.", str(float(r16(g['WebDepth'] + 2 * g['AngleProjection']))), '0', '1/16', 'pass', 'work/p4/rows.py')
add(44, 'DRB-4 4/68', 'Inside elevation of main girders', 'Girder G1 top cover plates', 'plan reading vs TopPlates', "22x13/16 F.L. + 22x13/16 x 86'-0 + 22x13/16 x 51'-0", '; '.join('%sx%s L=%s' % (fx(r16(p['Width'])), fx(r16(p['Thickness'])), fx(r16(p['Length'])) if p['Length'] else 'F.L.') for p in g['TopPlates']), '', 'exact', 'pass', 'work/p4/rows.py')
add(44, 'DRB-4 4/68', 'Inside elevation of main girders', 'Girder G1 bottom cover plates', 'plan reading vs BottomPlates', "22x13/16 x 90'-0 + x 74'-0 + x 51'-0", '; '.join('%sx%s L=%s' % (fx(r16(p['Width'])), fx(r16(p['Thickness'])), fx(r16(p['Length'])) if p['Length'] else 'F.L.') for p in g['BottomPlates']), '', 'exact', 'pass', 'work/p4/rows.py')
f1 = [s for s in truss('PTC Girder spans 1-10')['Sections'] if s['Name'] == 'F1'][0]
add(44, 'DRB-4 4/68', 'F1 / B1 elevation', "Floor beam F1 web + b.to b. angles 6'-3 1/2", 'plan reading vs .sbm F1', "Web 75 x 7/16, 4-Ls 6x6x1/2, 6'-3 1/2 b.b.", '%s x %s, %s, %s b.b.' % (fx(r16(f1['WebDepth'])), fx(r16(f1['WebPlies'][0]['Thickness'])), f1['Angle'], float(r16(f1['WebDepth'] + 2 * f1['AngleProjection']))), '', 'exact', 'pass', 'work/p4/rows.py')
# ---- layered plates at the chord splices (stacking)
add(74, 'DRB-4 34/68', 'U1 joint elevation: 2-Fills 22x3/8 (longer, against the web, between the 6x6 angle legs) and 2-Fills 31 1/2x7/16 (shorter, inside it) on the U0-U1 side',
    'Arch U1 web fills stacking (S-FU1-2, S-FU1-3)', 'dump_solids.csx x ranges + sbm clash (FillerPlate x FillerPlate 0.21 in.)',
    '22x3/8 at 14.125-13.75 in.; 31 1/2x7/16 at 13.75-13.3125 in.; i.s. splice plate 13.3125-12.8125', '22x3/8 at 14.125-13.75; 31 1/2x7/16 at 14.125-13.688 (both on the member face: overlap each other and the U0-U1 angle legs; 3/8 in. gap under the splice plate)', 'overlap 3/8 in.', 'no overlap', 'fail', 'evidence/p4/p74-U1-fills.png; I042')
add(74, 'DRB-4 34/68', 'U1 half flange plan: "Fill 24 x 5/16 (C)" drawn under the HALF BOTTOM FLANGE', 'Arch U1 cover splice fill face (S-FU1-1)', 'plan reading vs .sbm Plates Face', 'bottom flange', 'Top', '', 'face', 'fail', 'evidence/p4/p74-U1-fills.png; I043')
add('69/86', 'DRB-4 29/68, 46/68', 'p.86 L15-L16 callout "2-Webs 18x1x16\'-0 at L15" + p.69 sketch (16\'-0 from L15)', 'Arch L15 18 x 1 x 16\'-0 web plates (S-SPL15-1)', 'dump_solids.csx stations vs L15', 'from L15 into L15-L16 (0 to +16 ft)', 'centred on L15 (-8 to +8 ft); 8 ft doubles the 18x1 ply of L14-L15 (stacked inside it at x 10.25-11.25)', '8 ft', 'position', 'fail', 'evidence/p4/p86-L15-webs-16ft.png; evidence/p4/p69-half-section-L15-L17.png; I044')
import collections
cl = list(csv.DictReader(open(r'C:\SBM\temp\qc4\clash_arch.csv')))
n_fc = [r for r in cl if r['category_pair'] in ('FillerPlate x TrussChord', 'FillerPlate x FillerPlate', 'SplicePlate x TrussChord') and float(r['depth_in']) > 0.0625]
mx = max(float(r['depth_in']) for r in n_fc)
add('74-77', 'DRB-4 34-37/68', 'chord splice details U1-L7: fills fill the gap between members of different thickness; nothing overlaps', 'Arch chord splices: fill / splice plate overlaps with chord pieces or other fills (> 1/16 in.)', 'sbm clash --bridge Arch (pass 4 build, 1/16 in. tolerance)', '0', str(len(n_fc)), 'max depth %.2f in.' % mx, '0', 'fail', 'C:/SBM/temp/qc4/clash_arch.csv (not committed); I042')
add('49-67', 'DRB-4 9-27/68', 'approach joint sheets', 'Approach UNRESOLVED flag text "only one gusset pair per joint ... is built" (p.49-53, p.59-67)', '.sbm Truss.Unresolved vs the joints actually built', 'flag text should match the model', 'GussetLayers built at L0 / L0\' (1/2), L8 / L8\' (3/4), 4-span L10 / L10\' (7/8), L20 (3/4)', '', 'text', 'fail', 'work/p4/unresolved.txt; I045')
# write (only with --write; otherwise list the non-pass rows)
import sys
if '--write' not in sys.argv:
    for r in rows:
        if r[9] != 'pass': print(r[9], r[3][:70], '|', str(r[5])[:50], '|', str(r[6])[:60])
    print(len(rows), collections.Counter(r[9] for r in rows)); raise SystemExit
path = os.path.join(RES, 'checks.csv')
last = [l for l in open(path, encoding='utf-8').read().splitlines() if l.strip()][-1]
n = int(last.split(',')[0][1:])
with open(path, 'a', newline='', encoding='utf-8') as f:
    w = csv.writer(f, lineterminator='\r\n')
    for r in rows:
        n += 1
        w.writerow(['C%04d' % n] + r)
c = collections.Counter(r[9] for r in rows)
print(len(rows), c, 'last id C%04d' % n)
