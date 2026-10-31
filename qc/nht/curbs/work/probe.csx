// Agent review: probe the curb / deck meshes vertically at the worst C / CS cover-violation points (read-only).
using SBM.Core;
using SBM.Core.Geometry;
using SBM.Core.Solids;

double inch = 0.0254;
List<double> Hits(Mesh m, double x, double y)
{
    var r = new List<double>();
    for (int t = 0; t < m.Indices.Count; t += 3)
    {
        var a = m.Positions[m.Indices[t]]; var b = m.Positions[m.Indices[t + 1]]; var c = m.Positions[m.Indices[t + 2]];
        double d = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
        if (Math.Abs(d) < 1e-14) continue;
        double u = ((x - a.X) * (c.Y - a.Y) - (c.X - a.X) * (y - a.Y)) / d;
        double v = ((b.X - a.X) * (y - a.Y) - (x - a.X) * (b.Y - a.Y)) / d;
        if (u < -1e-9 || v < -1e-9 || u + v > 1 + 1e-9) continue;
        r.Add(a.Z + u * (b.Z - a.Z) + v * (c.Z - a.Z));
    }
    r.Sort(); return r;
}
Doc.Regenerate();
foreach (var un in new[] { "NHT Unit 1", "NHT Unit 2", "NHT Unit 5" })
{
    var br = Doc.Project.Bridges.First(b => b.Name == un);
    var m = Doc.Model(un); var al = Doc.Project.FindAlignment(br.AlignmentName)!; var deck = br.Deck; var c = m.Concrete!;
    var worst = c.CoverViolations.Where(v => v.Mark == "C" || v.Mark == "CS").OrderByDescending(v => v.Excess).Take(4).ToList();
    foreach (var v in worst)
    {
        var iv = al.Inverse(new Vec2(v.At.X, v.At.Y));
        double fin = deck.FinishedElevation(al, iv.Station, iv.Offset);
        Print($"{un} {v.Mark} {v.Face} excess {v.Excess / inch:0.000}\" sta {iv.Station / 0.3048:0.00} off {iv.Offset / 0.3048:0.0000} ft bar z-fin {(v.At.Z - fin) / inch:0.000}\"");
        foreach (var (n, mesh) in c.Barriers.Where(b => (b.Name.StartsWith("Left") ? -1 : 1) == Math.Sign(iv.Offset)))
            Print($"   {n} hits z-fin: {string.Join(", ", Hits(mesh, v.At.X, v.At.Y).Select(z => ((z - fin) / inch).ToString("0.000")))}");
        if (c.Deck != null) Print($"   deck hits z-fin: {string.Join(", ", Hits(c.Deck, v.At.X, v.At.Y).Select(z => ((z - fin) / inch).ToString("0.000")))}");
        // lateral scan across the curb at this station: deck top and curb top/bottom every 1"
        int side = iv.Offset < 0 ? -1 : 1; double edge = side < 0 ? deck.LeftEdgeOffset : deck.RightEdgeOffset;
        var sb = new System.Text.StringBuilder();
        for (int k = -2; k <= 12; k += 1)
        {
            double off = edge - side * k * inch; var p = al.PointAt(iv.Station, off);
            double f2 = deck.FinishedElevation(al, iv.Station, off);
            var ch = c.Barriers.Where(b => (b.Name.StartsWith("Left") ? -1 : 1) == side).SelectMany(b => Hits(b.Mesh, p.X, p.Y)).ToList();
            var dh = c.Deck != null ? Hits(c.Deck, p.X, p.Y) : new List<double>();
            sb.Append($" [R{k}: curb {string.Join("/", ch.Select(z => ((z - f2) / inch).ToString("0.00")))} deck {string.Join("/", dh.Select(z => ((z - f2) / inch).ToString("0.00")))}]");
        }
        Print("   scan (z - finished at that offset):" + sb);
    }
}
