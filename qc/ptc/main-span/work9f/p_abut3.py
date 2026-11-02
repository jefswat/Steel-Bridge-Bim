p = 'src/SBM.Core/Truss/TrussGenerator.cs'; s = open(p, encoding='utf-8').read()
old = '''            for (int pass = 0; pass < 3; pass++)
            {
                var webs = _inst.Where(i => IsWeb(i.Def.Role) && i.Plane.Length > 0).ToList();'''
assert s.count(old) == 1
s = s.replace(old, '''            // pass -1 = the WebsAbutAt cuts only (before any setback / corner cut); an abutting end is then left alone by the other rules
            var abutted = new HashSet<(Inst, bool)>();
            for (int pass = -1; pass < 3; pass++)
            {
                var webs = _inst.Where(i => IsWeb(i.Def.Role) && i.Plane.Length > 0).ToList();''')
old = '''                            if (AbutsAt(m, pt) != AbutsAt(o, pt))
                            {
                                if (pass > 0) continue;
                                var (keep, ks, cut, cs) = AbutsAt(m, pt) ? (m, start, o, os) : (o, os, m, start);
                                if (AbutCut(keep, ks, cut, cs, pl, pt, clr) is { } ab0)
                                {
                                    if (cs) cut.MitreA = (ab0.X, ab0.N); else cut.MitreB = (ab0.X, -ab0.N);
                                    abut++; abutPairs.Add($"{cut.Mark} on {keep.Mark} at {pt}");
                                }
                                else big.Add($"{cut.Mark} against {keep.Mark} at {pt} (no abut plane)");
                                continue;
                            }'''
assert s.count(old) == 1
s = s.replace(old, '''                            if (AbutsAt(m, pt) != AbutsAt(o, pt))
                            {
                                if (pass != -1) continue;
                                var (keep, ks, cut, cs) = AbutsAt(m, pt) ? (m, start, o, os) : (o, os, m, start);
                                if (AbutCut(keep, ks, cut, cs, pl, pt, clr) is { } ab0)
                                {
                                    if (cs) cut.MitreA = (ab0.X, ab0.N); else cut.MitreB = (ab0.X, -ab0.N);
                                    abutted.Add((cut, cs));
                                    abut++; abutPairs.Add($"{cut.Mark} on {keep.Mark} at {pt}");
                                }
                                else big.Add($"{cut.Mark} against {keep.Mark} at {pt} (no abut plane)");
                                continue;
                            }
                            if (pass == -1 || abutted.Contains((m, start)) || abutted.Contains((o, os))) continue;''')
open(p, 'w', encoding='utf-8').write(s)
print('ok')
