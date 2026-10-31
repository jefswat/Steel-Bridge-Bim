// Agent review: plan chord deviation of the curb loft from the true curb faces (read-only).
using SBM.Core;
using SBM.Core.Geometry;

double inch = 0.0254;
Doc.Regenerate();
foreach (var un in new[] { "NHT Unit 1", "NHT Unit 2", "NHT Unit 3", "NHT Unit 4", "NHT Unit 5" })
{
    var br = Doc.Project.Bridges.First(b => b.Name == un);
    var m = Doc.Model(un); var al = Doc.Project.FindAlignment(br.AlignmentName)!; var deck = br.Deck; var c = m.Concrete!;
    foreach (var (name, mesh) in c.Barriers)
    {
        int side = name.StartsWith("Left") ? -1 : 1; double edge = side < 0 ? deck.LeftEdgeOffset : deck.RightEdgeOffset;
        foreach (double rin in new[] { 0.0, 10.0 })
        {
            // bottom vertices of that face (z ~ deck), ordered by station
            var pts = mesh.Positions.Select(p => (p, iv: al.Inverse(new Vec2(p.X, p.Y))))
                .Where(x => Math.Abs(-side * (x.iv.Offset - edge) / inch - rin) < 0.05 && Math.Abs(x.p.Z - deck.FinishedElevation(al, x.iv.Station, x.iv.Offset)) < 0.001)
                .GroupBy(x => Math.Round(x.iv.Station, 4)).Select(g => g.First()).OrderBy(x => x.iv.Station).ToList();
            double worst = 0, wsta = 0, maxL = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1].p; var b = pts[i].p;
                maxL = Math.Max(maxL, new Vec2(b.X - a.X, b.Y - a.Y).Length);
                for (int k = 1; k < 8; k++)
                {
                    var q = new Vec2(a.X + (b.X - a.X) * k / 8.0, a.Y + (b.Y - a.Y) * k / 8.0);
                    var iv = al.Inverse(q);
                    double dev = -side * (iv.Offset - edge) / inch - rin;   // + = inward of the true face
                    if (Math.Abs(dev) > Math.Abs(worst)) { worst = dev; wsta = iv.Station; }
                }
            }
            Print($"{un} {name} face R{rin}: {pts.Count} rings, max ring spacing {maxL / 0.3048:0.00} ft, max chord deviation {worst:0.000}\" (+ = inward) at sta {wsta / 0.3048:0.00}");
        }
    }
    // alignment curvature over the unit
    var s0 = al.Inverse(m.Framing.Deck!.BeginLinePoint).Station; var s1 = al.Inverse(m.Framing.Deck!.EndLinePoint).Station;
    double minR = double.MaxValue;
    for (int k = 0; k <= 200; k++)
    {
        double s = s0 + (s1 - s0) * k / 200, h = 0.5;
        var p0 = al.PointAt(s - h, 0); var p1 = al.PointAt(s, 0); var p2 = al.PointAt(s + h, 0);
        double a1 = Math.Atan2(p1.Y - p0.Y, p1.X - p0.X), a2 = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
        double d = Math.Abs(Math.IEEERemainder(a2 - a1, 2 * Math.PI)); if (d > 1e-9) minR = Math.Min(minR, h / d);
    }
    Print($"{un}: min CL radius over the deck {minR / 0.3048:0.0} ft");
}
