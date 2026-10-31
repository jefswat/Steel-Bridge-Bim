// Agent review: where are the C / CS cover violations; railing base jumps (read-only).
using SBM.Core;
using SBM.Core.Geometry;

double inch = 0.0254;
Doc.Regenerate();
foreach (var un in new[] { "NHT Unit 1", "NHT Unit 2", "NHT Unit 3", "NHT Unit 4", "NHT Unit 5" })
{
    var br = Doc.Project.Bridges.First(b => b.Name == un);
    var m = Doc.Model(un);
    var al = Doc.Project.FindAlignment(br.AlignmentName)!;
    var deck = br.Deck; var dr = m.Framing.Deck!; var c = m.Concrete!;
    var rows = c.CoverViolations.Where(v => v.Mark == "C" || v.Mark == "CS").Select(v =>
    {
        var iv = al.Inverse(new Vec2(v.At.X, v.At.Y));
        int side = iv.Offset < 0 ? -1 : 1; double edge = side < 0 ? deck.LeftEdgeOffset : deck.RightEdgeOffset;
        double rin = -side * (iv.Offset - edge) / inch;
        double z = (v.At.Z - deck.FinishedElevation(al, iv.Station, iv.Offset)) / inch;
        double fb = dr.FromBegin(new Vec2(v.At.X, v.At.Y)) / inch, fe = dr.FromEnd(new Vec2(v.At.X, v.At.Y)) / inch;
        return (v, sta: iv.Station / 0.3048, side, rin, z, fb, fe);
    }).ToList();
    Print($"== {un}: {rows.Count} C/CS violations");
    foreach (var g in rows.GroupBy(r => (r.v.Mark, r.v.Face, r.side, near: Math.Min(r.fb, r.fe) < 24 ? "end" : "mid")))
    {
        var w = g.OrderByDescending(r => r.v.Excess).First();
        Print($"   {g.Key.Mark} {g.Key.Face} side {g.Key.side} {g.Key.near}: n {g.Count()} worst {w.v.Excess / inch:0.000}\" at sta {w.sta:0.00} R-in {w.rin:0.00}\" z-rel-deck {w.z:0.00}\" fromBegin {w.fb:0.0}\" fromEnd {w.fe:0.0}\"; sta range {g.Min(r => r.sta):0.00}..{g.Max(r => r.sta):0.00}");
    }
    // stations of mid-length violations
    var mids = rows.Where(r => Math.Min(r.fb, r.fe) >= 24).Select(r => r.sta).Distinct().OrderBy(x => x).ToList();
    if (mids.Count > 0) Print($"   mid violation stations: {string.Join(" ", mids.Take(60).Select(x => x.ToString("0.00")))}");
    // CS placement near the ends: first / last CS distance from deck ends
    var cs = c.Bars.Where(b => b.Mark == "CS").ToList();
    double minB = cs.Min(b => b.Points.Min(p => dr.FromBegin(new Vec2(p.X, p.Y)))) / inch, minE = cs.Min(b => b.Points.Min(p => dr.FromEnd(new Vec2(p.X, p.Y)))) / inch;
    Print($"   CS min centerline distance from begin end {minB:0.00}\", from end end {minE:0.00}\"");
    var cb = c.Bars.Where(b => b.Mark == "C").ToList();
    Print($"   C ends: min from begin {cb.Min(b => b.Points.Min(p => dr.FromBegin(new Vec2(p.X, p.Y)))) / inch:0.00}\", from end {cb.Min(b => b.Points.Min(p => dr.FromEnd(new Vec2(p.X, p.Y)))) / inch:0.00}\"");
}
// railing base jumps (as check-rail-base.csx), approach units only
double tol = 0.0254 / 4; int jumps = 0, runs = 0;
foreach (var n in Doc.Project.Bridges.Select(b => b.Name).Where(n => n.StartsWith("NHT Unit")))
{
    var rr = Doc.Model(n).Steel?.Railings; if (rr == null) continue;
    foreach (var r in rr.Runs)
    {
        runs++;
        for (int i = 1; i < r.Line.Count; i++)
        {
            var a = r.Line[i - 1]; var b = r.Line[i];
            double dxy = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)), dz = b.Z - a.Z;
            if (Math.Abs(dz) > tol + 0.2 * dxy) { jumps++; Print($"JUMP {n} | {r.Name} | pt {i} dz {dz * 1000:F1} mm over {dxy:F2} m"); }
        }
    }
    foreach (var d in rr.Diagnostics) if (d.Message.Contains("dips") || d.Message.Contains("curb")) Print($"RAILDIAG {n}: {d.Severity} {d.Message}");
}
Print($"{runs} approach railing runs, {jumps} base jumps");
