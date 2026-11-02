p = 'src/SBM.Core/Bridge/TrussDefinition.cs'; s = open(p, encoding='utf-8').read()
old = '''    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? WebsAbutAt { get; set; }
'''
assert s.count(old) == 1
s = s.replace(old, old + '''    /// <summary>OPTIONAL (null = none, omitted from the .sbm): panel points at which the CHORD members of this plane ending there are cut on this member's side
    /// faces (the chords abut a post that runs through the joint; e.g. a lower chord kinked at a pier post). Needs <see cref="TrussJointDefaults.WebToWebClearance"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ChordsAbutAt { get; set; }
    /// <summary>OPTIONAL (null = none, omitted from the .sbm): panel points at which this web member's end is set back SQUARE until it clears the other web
    /// members there (any distance; no corner cut), e.g. a short vertical stopping below the diagonals that run to the chord.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? SetBackClearAt { get; set; }
''')
open(p, 'w', encoding='utf-8').write(s)

p = 'src/SBM.Core/Truss/TrussGenerator.cs'; s = open(p, encoding='utf-8').read()
old = '''            bool AbutsAt(Inst k, string pt) => k.Def.WebsAbutAt is { Count: > 0 } wl && wl.Any(x => x.Equals(pt, StringComparison.OrdinalIgnoreCase));'''
assert s.count(old) == 1
s = s.replace(old, old + '''
            bool SetsBackAt(Inst k, string pt) => k.Def.SetBackClearAt is { Count: > 0 } wl && wl.Any(x => x.Equals(pt, StringComparison.OrdinalIgnoreCase));
            // ChordsAbutAt: the chords ending at the point are cut on the post's side faces
            int chordAbut = 0;
            foreach (var k in _inst.Where(i => i.Def.ChordsAbutAt is { Count: > 0 } && i.Plane.Length > 0))
                for (int end = 0; end < 2; end++)
                {
                    bool ks = end == 0;
                    string pl = ks ? k.PlaneA : k.PlaneB, pt = ks ? k.PointA : k.PointB;
                    if (!k.Def.ChordsAbutAt!.Any(x => x.Equals(pt, StringComparison.OrdinalIgnoreCase))) continue;
                    foreach (var c in At(pl, pt).Where(c => c != k && IsChord(c.Def.Role) && !IsCrossPlane(c.Def.Role) && c.Plane == k.Plane))
                    {
                        bool cs = StartsAt(c, pl, pt);
                        if (AbutCut(k, ks, c, cs, pl, pt, clr) is { } abc) { if (cs) c.MitreA = (abc.X, abc.N); else c.MitreB = (abc.X, -abc.N); chordAbut++; }
                        else big.Add($"{c.Mark} against {k.Mark} at {pt} (no abut plane)");
                    }
                }
            if (chordAbut > 0) Info($"Truss {_d.Name}: {chordAbut} chord ends cut on the side faces of the post they abut (ChordsAbutAt, clearance {L(clr)}).");''')
old = '''                            bool fixM = (start ? m.Def.StartSetback : m.Def.EndSetback) == null, fixO = (os ? o.Def.StartSetback : o.Def.EndSetback) == null;
                            double dm = fixM ? Need(m, start, o, os, pl, pt) : double.PositiveInfinity;
                            double dO = fixO ? Need(o, os, m, start, pl, pt) : double.PositiveInfinity;
                            if (dm <= 1e-9 && dO <= 1e-9) continue;'''
assert s.count(old) == 1
s = s.replace(old, '''                            bool fixM = (start ? m.Def.StartSetback : m.Def.EndSetback) == null, fixO = (os ? o.Def.StartSetback : o.Def.EndSetback) == null;
                            double dm = fixM ? Need(m, start, o, os, pl, pt) : double.PositiveInfinity;
                            double dO = fixO ? Need(o, os, m, start, pl, pt) : double.PositiveInfinity;
                            if (dm <= 1e-9 && dO <= 1e-9) continue;
                            // SetBackClearAt: that member is set back square (any distance), the other one keeps its end
                            if (SetsBackAt(m, pt) != SetsBackAt(o, pt))
                            {
                                var (sbm, sbs, dd) = SetsBackAt(m, pt) ? (m, start, dm) : (o, os, dO);
                                if (dd > 1e-9 && !double.IsInfinity(dd))
                                {
                                    if (sbs) { sbm.SA += dd; sbm.MitreA = null; } else { sbm.SB += dd; sbm.MitreB = null; }
                                    changed++; maxDt = Math.Max(maxDt, dd);
                                }
                                else if (double.IsInfinity(dd)) big.Add($"{sbm.Mark} at {pt} (SetBackClearAt: no square setback clears it)");
                                continue;
                            }''')
open(p, 'w', encoding='utf-8').write(s)
print('ok')
