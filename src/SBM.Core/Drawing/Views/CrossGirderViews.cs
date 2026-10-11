using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;

namespace SBM.Core.Drawing.Views;

/// <summary>Cross girder detail: elevation of the generated cross girder (outlines of the front faces of every part, incl. web openings, bolts, stiffeners).</summary>
public static class CrossGirderViews
{
    /// <summary>Cross girders grouped by identical part lists (one detail per group).</summary>
    public static List<List<CrossGirderResult>> Groups(ViewContext ctx)
    {
        var res = new List<List<CrossGirderResult>>();
        if (ctx.Steel == null) return res;
        var by = new Dictionary<string, List<CrossGirderResult>>();
        foreach (var xg in ctx.Framing.CrossGirders)
        {
            var sig = string.Join("|", ctx.Steel.Parts.Where(p => p.Assembly == xg.Mark && p.Category != PartCategory.Weld).Select(p => p.Description).OrderBy(x => x));
            if (sig.Length == 0) continue;
            if (!by.TryGetValue(sig, out var l)) { by[sig] = l = new(); res.Add(l); }
            l.Add(xg);
        }
        return res;
    }

    /// <summary>Boundary edges of the triangles that face the viewer, projected: the outline of a plate with its openings.</summary>
    public static List<(Vec2 a, Vec2 b)> FrontOutline(Mesh mesh, Func<Vec3, Vec2> proj, Vec3 view)
    {
        var counts = new Dictionary<(long, long, long, long), (int n, Vec2 a, Vec2 b)>();
        long Q(double v) => (long)Math.Round(v * 10000);
        for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
        {
            var p0 = mesh.Positions[mesh.Indices[i]]; var p1 = mesh.Positions[mesh.Indices[i + 1]]; var p2 = mesh.Positions[mesh.Indices[i + 2]];
            var nrm = Vec3.Cross(p1 - p0, p2 - p0);
            if (Vec3.Dot(nrm, view) <= 1e-12) continue;
            var q = new[] { proj(p0), proj(p1), proj(p2) };
            for (int e = 0; e < 3; e++)
            {
                var a = q[e]; var b = q[(e + 1) % 3];
                var ka = (Q(a.X), Q(a.Y)); var kb = (Q(b.X), Q(b.Y));
                if (ka == kb) continue;
                var key = ka.CompareTo(kb) < 0 ? (ka.Item1, ka.Item2, kb.Item1, kb.Item2) : (kb.Item1, kb.Item2, ka.Item1, ka.Item2);
                counts[key] = counts.TryGetValue(key, out var c) ? (c.n + 1, c.a, c.b) : (1, a, b);
            }
        }
        return counts.Values.Where(c => c.n == 1).Select(c => (c.a, c.b)).ToList();
    }

    public static SheetView Detail(ViewContext ctx, List<CrossGirderResult> group)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.SectionScale);
        var steel = ctx.Steel!;
        var xg = group[0];
        var o = xg.Left;
        var t = new Vec3(xg.Direction.X, xg.Direction.Y, 0).Normalized();
        var lat = new Vec3(-t.Y, t.X, 0);                 // viewer side: the left of the cross girder direction (looking at the near face)
        Vec2 Proj(Vec3 p) => new(Vec3.Dot(p - o, t), p.Z - o.Z);
        var parts = steel.Parts.Where(p => p.Assembly == xg.Mark && p.Category != PartCategory.Weld).ToList();
        foreach (var p in parts)
            foreach (var (a, b) in FrontOutline(p.Mesh, Proj, -lat))
                d.Line(p.Category == PartCategory.Angle ? SheetLayers.SteelHidden : SheetLayers.Steel, a, b);
        foreach (var b in steel.Bolts.Where(b => b.Assembly == xg.Mark && Math.Abs(Vec3.Dot(b.Frame.Z, lat)) > 0.7))
            d.Circle(SheetLayers.Steel, Proj(b.Frame.Origin), (b.Diameter > 0 ? b.Diameter : steel.Bolt.Diameter) / 2);
        // Girder web lines at both ends.
        var all = parts.SelectMany(p => p.Mesh.Positions.Select(Proj)).ToList();
        double x0 = all.Min(q => q.X), x1 = all.Max(q => q.X), z0 = all.Min(q => q.Y), z1 = all.Max(q => q.Y);
        d.Dim(new Vec2(x0, z0), new Vec2(x1, z0), -0.5, d.Len(x1 - x0));
        d.Dim(new Vec2(x1, z0), new Vec2(x1, z1), -0.5, d.Short(z1 - z0));
        int k = 0;
        foreach (var gp in parts.GroupBy(p => p.Description).OrderByDescending(g => { var q = g.First().Mesh.Positions.Select(Proj).ToList(); return (q.Min(v => v.Y) + q.Max(v => v.Y)) / 2; }))
        {
            var p0 = gp.First();
            var pts = p0.Mesh.Positions.Select(Proj).ToList();
            var c = new Vec2((pts.Min(q => q.X) + pts.Max(q => q.X)) / 2, (pts.Min(q => q.Y) + pts.Max(q => q.Y)) / 2);
            var elbow = new Vec2(x1 + d.Paper(0.7), z1 - d.Paper(0.3) * (k++));
            d.Leader(new Vec2(pts.Max(q => q.X) - (pts.Max(q => q.X) - pts.Min(q => q.X)) * 0.1, c.Y), elbow, $"{(gp.Count() > 1 ? gp.Count() + " - " : "")}{p0.Description.ToUpperInvariant()}");
        }
        var box = dwg.Extents();
        string others = string.Join(", ", group.Skip(1).Select(x => x.Mark));
        d.Title($"CROSS GIRDER {xg.Mark}", $"G{xg.LeftGirder + 1}–G{xg.RightGirder + 1}, carrying discontinuous G{xg.Girder + 1}{(others.Length > 0 ? " · also " + others : "")} · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((x0 + x1) / 2, box.MinY - d.Paper(0.4)));
        return new SheetView($"Cross girder {xg.Mark}", xg.Mark, dwg, d.Scale, "Cross girder elevation as generated.");
    }
}
