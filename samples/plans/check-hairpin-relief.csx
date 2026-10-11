// Hairpins (5c U bars hanging from the cap top with 3 1/2" clear to both faces) vs the cap section step: the legs are outside the narrower lower section
// wherever they reach below the lowest relief line. Lists per pier the hairpins whose leg bottom is below the relief line (by how much).
//   sbm run samples\plans\check-hairpin-relief.csx "temp\model.sbm"
double In_(double m) => m / 0.0254;
foreach (var b in Doc.Project.Bridges)
{
    var sm = Doc.Model(b.Name).Substructure;
    if (sm == null) continue;
    foreach (var u in sm.Units)
    {
        if (u.ReliefLines.Count == 0) continue;
        var hairMarks = sm.Marks.Where(m => m.Unit == u.Name && m.Location.Contains("hairpins")).ToDictionary(m => m.Mark, m => m.Location);
        var bars = sm.Bars.Where(x => x.Unit == u.Name && hairMarks.ContainsKey(x.Mark) && x.Points.Length >= 4).ToList();
        if (bars.Count == 0) continue;
        double Low(double s) { double z = double.MaxValue; foreach (var l in u.ReliefLines) if (l.Count > 1) z = Math.Min(z, SBM.Core.Substructure.SubstructureUnit.Interp(l, Math.Clamp(s, l[0].X, l[^1].X))); return z; }
        var rows = new List<string>();
        foreach (var bar in bars)
        {
            var lo = bar.Points.OrderBy(p => p.Z).First();
            double s = SBM.Core.Geometry.Vec2.Dot(lo.XY - u.Origin, u.D);
            double width = bar.Points.Max(p => SBM.Core.Geometry.Vec2.Dot(p.XY - u.Origin, u.N)) - bar.Points.Min(p => SBM.Core.Geometry.Vec2.Dot(p.XY - u.Origin, u.N));
            if (width < 1.0) continue;                          // only the wide U hairpins
            double over = Low(s) - lo.Z;                        // > 0: leg bottom (bar centre) below the lowest relief line
            if (over > 0) rows.Add($"   {hairMarks[bar.Mark].Replace(u.Name + " cap ", "")} S {s:0.000} leg bottom {lo.Z:0.000} relief {Low(s):0.000} below by {In_(over):0.0}\"  cap top {u.CapTopAt(s):0.000}");
        }
        Print($"== {b.Name} / {u.Name}: {bars.Count} hairpins, {rows.Count} with legs below the relief line");
        foreach (var r in rows) Print(r);
    }
}
