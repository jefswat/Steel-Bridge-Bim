p = 'src/SBM.Core/Bridge/TrussDefinition.cs'; s = open(p, encoding='utf-8').read()
old = '''    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public TrussEndCut EndCut { get; set; }
'''
assert s.count(old) == 1
s = s.replace(old, old + '''    /// <summary>OPTIONAL (null = none, omitted from the .sbm): panel points at which the OTHER web members of this plane meeting this member are cut on
    /// this member's side face (they abut it; this member keeps its own end): e.g. a post running up to the chord with the diagonals cut along its sides
    /// (<see cref="TrussJointDefaults.WebToWebClearance"/> must be set; without this list overlapping web ends are set back / corner cut).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? WebsAbutAt { get; set; }
''')
open(p, 'w', encoding='utf-8').write(s)

p = 'src/SBM.Core/Truss/TrussGenerator.cs'; s = open(p, encoding='utf-8').read()
old = '''            int changed = 0, corner = 0; double maxDt = 0;
            var big = new List<string>();'''
assert s.count(old) == 1
s = s.replace(old, '''            int changed = 0, corner = 0, abut = 0; double maxDt = 0;
            var big = new List<string>();
            var cornerPairs = new List<string>();
            bool AbutsAt(Inst k, string pt) => k.Def.WebsAbutAt is { Count: > 0 } wl && wl.Any(x => x.Equals(pt, StringComparison.OrdinalIgnoreCase));''')
old = '''                            bool os = StartsAt(o, pl, pt);
                            bool fixM = (start ? m.Def.StartSetback : m.Def.EndSetback) == null, fixO = (os ? o.Def.StartSetback : o.Def.EndSetback) == null;'''
assert s.count(old) == 1
s = s.replace(old, '''                            bool os = StartsAt(o, pl, pt);
                            // WebsAbutAt: the other member is cut on this member's side face (pass 0 only; overrides a corner cut)
                            if (AbutsAt(m, pt) != AbutsAt(o, pt))
                            {
                                if (pass > 0) continue;
                                var (keep, ks, cut, cs) = AbutsAt(m, pt) ? (m, start, o, os) : (o, os, m, start);
                                double dk = Need(cut, cs, keep, ks, pl, pt);
                                if (dk <= 1e-9) continue;
                                if (AbutCut(keep, ks, cut, cs, pl, pt, clr) is { } ab0)
                                {
                                    if (cs) cut.MitreA = (ab0.X, ab0.N); else cut.MitreB = (ab0.X, -ab0.N);
                                    abut++;
                                }
                                else big.Add($"{cut.Mark} against {keep.Mark} at {pt} (no abut plane)");
                                continue;
                            }
                            bool fixM = (start ? m.Def.StartSetback : m.Def.EndSetback) == null, fixO = (os ? o.Def.StartSetback : o.Def.EndSetback) == null;''')
old = '''                                    if (os) o.MitreA = (cc.X, -cc.N); else o.MitreB = (cc.X, cc.N);
                                    corner++;'''
assert s.count(old) == 1
s = s.replace(old, '''                                    if (os) o.MitreA = (cc.X, -cc.N); else o.MitreB = (cc.X, cc.N);
                                    corner++;
                                    cornerPairs.Add($"{m.Mark} / {o.Mark} at {pt}");''')
old = '''            if (corner > 0) Info($"Truss {_d.Name}: {corner} pairs of web member ends cut on the bisector plane where their facing sides meet (corner cut, clearance {L(clr)}).");'''
assert s.count(old) == 1
s = s.replace(old, '''            if (corner > 0) Info($"Truss {_d.Name}: {corner} pairs of web member ends cut on the bisector plane where their facing sides meet (corner cut, clearance {L(clr)}): {string.Join(", ", cornerPairs)}.");
            if (abut > 0) Info($"Truss {_d.Name}: {abut} web member ends cut on the side face of the member they abut (WebsAbutAt, clearance {L(clr)}).");''')
open(p, 'w', encoding='utf-8').write(s)
print('ok')
