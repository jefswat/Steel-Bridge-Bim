p = 'docs/plan-data/ptc-delaware-river/connections/gusset-layers.csv'; s = open(p, encoding='utf-8').read()
old = '"-43.6,-17.8; -7.6,-47.7; 14.4,-47.7; 43.8,-17.8; 43.6,30.6; 31.7,42.5; -31.7,42.5; -44.0,30.1"'
assert s.count(old) == 1
s = s.replace(old, '"-43.6,-17.8; -7.2,-47.7; 7.1,-47.7; 43.8,-17.8; 43.6,30.6; 31.7,42.5; -31.7,42.5; -44.0,30.1"')
old2 = 'the left lower edge is drawn at about 40 deg, the right one at 45 deg (as drawn);'
assert s.count(old2) == 1
s = s.replace(old2, 'the left lower edge is drawn at about 40 deg, the right one at 45 deg (as drawn); RE-READ 2026-11-02 (agent review, 300 dpi): the bottom edge is centred on the CL, -7.2 / +7.1 in. (both lower edges at 45 deg; the 586 / 680 px read put it at -7.6 / +14.4);')
old3 = 'The 1 in. pair is modeled as one plate per face (the 1/8 in. gap at the CL is not modeled).'
assert s.count(old3) == 1
s = s.replace(old3, 'Since 2026-11-02 the 1 in. pair is TWO plates per face, 1/8 in. apart at the CL (TrussJointDefinition.GussetSplitGap, connections-ptc.csx section 5).')
open(p, 'w', encoding='utf-8').write(s)
p = 'samples/plans/connections-ptc.csx'; s = open(p, encoding='utf-8').read()
old = '''        jd.GussetLayers = new();
        foreach (var r in rows.Where(r => !r["position"].StartsWith("inner")).OrderByDescending(r => int.Parse(r["layer_from_outside"])))'''
assert s.count(old) == 1
s = s.replace(old, '''        // agent review 2026-11-02: main L22 = '2-1" Gus. Pls. | 1/8 | 2-1" Gus. Pls.' (p.90): each half truss brings its own 1 in. gusset, 1/8 in. apart at the CL
        jd.GussetSplitGap = st == "main" && dj == "L22" ? In(1.0 / 8) : 0;
        if (jd.GussetSplitGap > 0) report.Add($"gusset layers {bn} {mj}: inner 1 in. gussets split at the CL, 1/8 in. gap (p.90)");
''' + old)
open(p, 'w', encoding='utf-8').write(s)
print('ok')
