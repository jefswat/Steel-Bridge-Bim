p = 'samples/plans/connections-ptc.csx'; s = open(p, encoding='utf-8').read()
old = '''        tm4.JointDefaults.WebToWebClearance = 0;
        tm4.Unresolved.RemoveAll(x => x.Contains("web member ends at the panel points"));'''
assert s.count(old) == 1
new = '''        tm4.JointDefaults.WebToWebClearance = 0;
        // agent review 2026-11-02 (clash rows web member x web member, 0.3-0.55 in.; the bisector corner cut is not what the sheets draw):
        // (a) the verticals run to the chord and the diagonals are cut along their sides: U11 (p.79), U12 (p.81), U13 / U14 / U15 (p.82), U16 / U17 (p.87) and at the
        //     lower chord L6 (p.76), L8 (p.77), L15 / L16 (p.86), L17 (p.87), L18 (p.88; L19 similar) - WebsAbutAt on the vertical;
        // (b) the short verticals U7-L7 / U9-L9 (p.76 / p.77) stop square below the diagonals at U7 / U9 and U10-L10 above the diagonals at L10 (p.78) - SetBackClearAt;
        // (c) at L11 the chords L10-L11 / L11-L12 end against the post U11-L11 (p.80 elevation: both chords cut vertical at the post sides) - ChordsAbutAt.
        int nAb = 0, nSb = 0, nCa = 0;
        var abutTop = new[] { "U11", "U12", "U13", "U14", "U15", "U16", "U17" };
        var abutBot = new[] { "L6", "L8", "L15", "L16", "L17", "L18" };
        foreach (var m in tm4.Members.Where(m => m.Role == TrussMemberRole.Vertical && m.Plane.Length == 0))
        {
            string a = m.From.TrimEnd('\\''), b = m.To.TrimEnd('\\''), prime = m.From.EndsWith("'") ? "'" : "";
            m.WebsAbutAt = null; m.SetBackClearAt = null; m.ChordsAbutAt = null;
            var ab = new List<string>();
            if (abutTop.Contains(a)) ab.Add(m.From);
            if (abutBot.Contains(b)) ab.Add(m.To);
            if (ab.Count > 0) { m.WebsAbutAt = ab; nAb += ab.Count; }
            if (a is "U7" or "U9") { m.SetBackClearAt = new() { m.From }; nSb++; }
            if (b == "L10") { m.SetBackClearAt = new() { m.To }; nSb++; }
            if (a == "U11" && b == "L11") { m.ChordsAbutAt = new() { m.To }; nCa++; }
        }
        report.Add($"web member ends (agent review 2026-11-02): {nAb} vertical ends the diagonals abut (p.76-p.88), {nSb} short vertical ends set back square clear of the diagonals (U7 / U9 / L10), {nCa} posts the L11 chords abut (p.80)");
        tm4.Unresolved.RemoveAll(x => x.Contains("post U11-L11 below the L11 chords"));
        tm4.Unresolved.Add(U("p.80", "post U11-L11 below the L11 chords",
            "the elevation draws the post webs running down through the joint (round the bottom strut box) to the FIN. line over the shoe, with the chords cut against its sides; the post bottom and the cut of its cover plates round the strut are not dimensioned",
            "the chords end at the post sides; the post keeps its end above the chords (its part through the joint is not built)."));
        tm4.Unresolved.RemoveAll(x => x.Contains("web member ends at the panel points"));'''
s = s.replace(old, new)
old2 = '''            "web members set back just clear of each other (0 in.); the three verticals end on a plane parallel to the lower chord face (WebMemberClearance off it)."));'''
assert s.count(old2) == 1
s = s.replace(old2, '''            "web members set back just clear of each other (0 in.); the diagonals cut along the verticals that run to the chord, the short verticals at U7 / U9 / L10 set back square; the three verticals end on a plane parallel to the lower chord face (WebMemberClearance off it)."));''')
open(p, 'w', encoding='utf-8').write(s)
print('ok')
