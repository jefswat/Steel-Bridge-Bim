using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;

namespace SBM.Core.Road;

/// <summary>
/// Required clearance of a road under / beside a bridge (OPTIONAL <see cref="RoadDefinition.Clearances"/>; design note section 2.2). Values come from
/// the plans: NaN = not given -> UNRESOLVED (the measured minimum is still reported). No default is ever used.
/// </summary>
public sealed class RoadClearanceDefinition
{
    /// <summary>Bridge name, or a prefix ending in '*'; "" = every bridge.</summary>
    public string Bridge { get; set; } = "";
    /// <summary>Minimum vertical clearance from the road top (at its edge lines) to the lowest structure face above it (meters).</summary>
    public double MinVertical { get; set; } = double.NaN;
    /// <summary>Minimum horizontal clearance in plan from the road's outer edge lines to the substructure above the road surface (meters).</summary>
    public double MinHorizontal { get; set; } = double.NaN;
    public string Source { get; set; } = "";

    internal bool Matches(string bridge) => Bridge.Length == 0 || (Bridge.EndsWith('*')
        ? bridge.StartsWith(Bridge[..^1], StringComparison.OrdinalIgnoreCase) : string.Equals(bridge, Bridge, StringComparison.OrdinalIgnoreCase));
}

/// <summary>One measured clearance (the minimum over the road for one bridge and one direction).</summary>
public sealed record RoadClearanceResult(string Road, string Bridge, string Kind, double Required, double Measured, Vec3 At, string Element)
{
    /// <summary>Null when the required value is not given (UNRESOLVED).</summary>
    public bool? Ok => double.IsNaN(Required) ? null : Measured >= Required - 1e-6;
}

/// <summary>
/// Clearance check of the generated roads against the generated bridges. Vertical: at every point of every road top edge line (the section stations), the
/// lowest triangle of the bridge's steel / concrete / prestressed / substructure solids straight above it. Horizontal: plan distance from the outermost
/// edge line of each side to the substructure solids that reach above the road top at the nearest edge point. Differences are reported, never adjusted.
/// </summary>
public static class RoadClearanceCheck
{
    private sealed record Tri(Vec3 A, Vec3 B, Vec3 C, string Element);

    /// <summary>Named solids of a bridge model that can obstruct a road.</summary>
    public static IEnumerable<(string Element, Mesh Mesh, bool Substructure)> Solids(BridgeModel b)
    {
        if (b.Steel != null) foreach (var p in b.Steel.Parts) yield return (p.Mark, p.Mesh, false);
        if (b.Concrete is { } c)
        {
            if (c.Deck != null) yield return ("deck slab", c.Deck, false);
            if (c.Haunches != null) yield return ("haunches", c.Haunches, false);
            foreach (var (n, m) in c.Barriers) yield return (n, m, false);
        }
        if (b.Prestressed != null) foreach (var be in b.Prestressed.Beams) yield return (be.Mark, be.Mesh, false);
        if (b.Substructure != null)
            foreach (var u in b.Substructure.Units)
                foreach (var s in u.Solids) yield return ($"{u.Name} {s.Name}", s.Mesh, true);
    }

    public static List<RoadClearanceResult> Check(RoadModel road, IReadOnlyList<BridgeModel?> bridges, List<Diagnostic>? diagnostics = null)
    {
        var res = new List<RoadClearanceResult>();
        var d = road.Definition;
        if (d.Clearances is not { Count: > 0 } reqs) return res;
        var pts = road.EdgeLines.SelectMany(e => e).ToList();
        if (pts.Count == 0) return res;
        double x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y);
        // outermost edge line per side = the lines whose points lie farthest from the alignment on each side (first and last added per side)
        var outer = OuterEdges(road);
        foreach (var b in bridges.OfType<BridgeModel>())
        {
            var req = reqs.LastOrDefault(r => r.Matches(b.Definition.Name));
            if (req == null) continue;
            string src = req.Source.Length > 0 ? $" ({req.Source})" : "";
            // vertical: triangles over the road's plan box, XY grid
            const double cell = 2.0;
            var grid = new Dictionary<(int, int), List<Tri>>();
            var subTris = new List<Tri>();
            foreach (var (el, mesh, sub) in Solids(b))
            {
                if (mesh.IsEmpty) continue;
                var bb = mesh.Bounds();
                bool overRoad = !(bb.Max.X < x0 || bb.Min.X > x1 || bb.Max.Y < y0 || bb.Min.Y > y1);
                if (!overRoad && !sub) continue;
                for (int t = 0; t < mesh.TriangleCount; t++)
                {
                    var a = mesh.Positions[mesh.Indices[3 * t]]; var bv = mesh.Positions[mesh.Indices[3 * t + 1]]; var cv = mesh.Positions[mesh.Indices[3 * t + 2]];
                    var tri = new Tri(a, bv, cv, el);
                    if (sub) subTris.Add(tri);
                    if (!overRoad) continue;
                    double tx0 = Math.Min(a.X, Math.Min(bv.X, cv.X)), tx1 = Math.Max(a.X, Math.Max(bv.X, cv.X));
                    double ty0 = Math.Min(a.Y, Math.Min(bv.Y, cv.Y)), ty1 = Math.Max(a.Y, Math.Max(bv.Y, cv.Y));
                    if (tx1 < x0 || tx0 > x1 || ty1 < y0 || ty0 > y1) continue;
                    for (int i = (int)Math.Floor(tx0 / cell); i <= (int)Math.Floor(tx1 / cell); i++)
                        for (int j = (int)Math.Floor(ty0 / cell); j <= (int)Math.Floor(ty1 / cell); j++)
                        {
                            if (!grid.TryGetValue((i, j), out var l)) grid[(i, j)] = l = new();
                            l.Add(tri);
                        }
                }
            }
            double vMin = double.PositiveInfinity; Vec3 vAt = default; string vEl = "";
            foreach (var p in pts)
            {
                if (!grid.TryGetValue(((int)Math.Floor(p.X / cell), (int)Math.Floor(p.Y / cell)), out var l)) continue;
                foreach (var t in l)
                    if (ZAt(t, p.X, p.Y) is double z && z > p.Z && z - p.Z < vMin) { vMin = z - p.Z; vAt = p; vEl = t.Element; }
            }
            if (!double.IsPositiveInfinity(vMin)) res.Add(new(d.Name, b.Definition.Name, "vertical", req.MinVertical, vMin, vAt, vEl));
            // horizontal: plan distance from the outer edge lines to substructure triangles reaching above the road top there
            double hMin = double.PositiveInfinity; Vec3 hAt = default; string hEl = "";
            foreach (var line in outer)
                for (int k = 0; k + 1 < line.Length; k++)
                {
                    var p = line[k]; var q = line[k + 1];
                    foreach (var t in subTris)
                    {
                        if (Math.Max(t.A.Z, Math.Max(t.B.Z, t.C.Z)) <= Math.Min(p.Z, q.Z)) continue;
                        double dist = PlanDistance(p, q, t, out var near);
                        if (dist < hMin) { hMin = dist; hAt = near; hEl = t.Element; }
                    }
                }
            if (!double.IsPositiveInfinity(hMin)) res.Add(new(d.Name, b.Definition.Name, "horizontal", req.MinHorizontal, hMin, hAt, hEl));
            if (diagnostics != null)
                foreach (var r in res.Where(r => r.Bridge == b.Definition.Name))
                {
                    string m = $"{r.Measured / 0.3048:0.00} ft ({r.Measured:0.000} m) to {r.Element} at E {r.At.X:0.00} N {r.At.Y:0.00}";
                    string who = $"Road {d.Name} under {b.Definition.Name}: {r.Kind} clearance";
                    diagnostics.Add(r.Ok switch
                    {
                        null => new Diagnostic(DiagnosticSeverity.Warning, UnresolvedFlag.Text("", $"{d.Name} {r.Kind} clearance under {b.Definition.Name}",
                            "the required value is not given in the inputs", $"measured minimum {m}.")),
                        true => new Diagnostic(DiagnosticSeverity.Info, $"{who} {m} >= required {r.Required / 0.3048:0.00} ft{src}."),
                        false => new Diagnostic(DiagnosticSeverity.Warning, $"CONFLICT (plan vs model): {who} {m} < required {r.Required / 0.3048:0.00} ft{src}."),
                    });
                }
        }
        return res;
    }

    private static List<Vec3[]> OuterEdges(RoadModel road)
    {
        var list = new List<Vec3[]>();
        if (road.OuterLeft >= 0) list.Add(road.EdgeLines[road.OuterLeft]);
        if (road.OuterRight >= 0) list.Add(road.EdgeLines[road.OuterRight]);
        return list;
    }

    private static double? ZAt(Tri t, double x, double y)
    {
        double d = (t.B.Y - t.C.Y) * (t.A.X - t.C.X) + (t.C.X - t.B.X) * (t.A.Y - t.C.Y);
        if (Math.Abs(d) < 1e-14) return null;
        double l1 = ((t.B.Y - t.C.Y) * (x - t.C.X) + (t.C.X - t.B.X) * (y - t.C.Y)) / d;
        double l2 = ((t.C.Y - t.A.Y) * (x - t.C.X) + (t.A.X - t.C.X) * (y - t.C.Y)) / d;
        double l3 = 1 - l1 - l2;
        if (l1 < -1e-9 || l2 < -1e-9 || l3 < -1e-9) return null;
        return l1 * t.A.Z + l2 * t.B.Z + l3 * t.C.Z;
    }

    /// <summary>Plan distance between segment pq and triangle t (0 when they overlap in plan); <paramref name="near"/> = the point on pq.</summary>
    private static double PlanDistance(Vec3 p, Vec3 q, Tri t, out Vec3 near)
    {
        var a = new Vec2(p.X, p.Y); var b = new Vec2(q.X, q.Y);
        Vec2[] tv = { new(t.A.X, t.A.Y), new(t.B.X, t.B.Y), new(t.C.X, t.C.Y) };
        if (ZAt(t, a.X, a.Y) != null) { near = p; return 0; }
        if (ZAt(t, b.X, b.Y) != null) { near = q; return 0; }
        double best = double.PositiveInfinity; double bestS = 0;
        for (int k = 0; k < 3; k++)
        {
            var c = tv[k]; var e = tv[(k + 1) % 3];
            double dd = SegSeg(a, b, c, e, out double s);
            if (dd < best) { best = dd; bestS = s; }
        }
        near = p + (q - p) * bestS;
        return best;
    }

    private static double SegSeg(Vec2 p1, Vec2 q1, Vec2 p2, Vec2 q2, out double s)
    {
        // proper crossing = 0
        double Cross(Vec2 o, Vec2 u, Vec2 v) => (u.X - o.X) * (v.Y - o.Y) - (u.Y - o.Y) * (v.X - o.X);
        double d1 = Cross(p2, q2, p1), d2 = Cross(p2, q2, q1), d3 = Cross(p1, q1, p2), d4 = Cross(p1, q1, q2);
        if (d1 * d2 < 0 && d3 * d4 < 0) { s = d1 / (d1 - d2); return 0; }
        double best = double.PositiveInfinity, sBest = 0;
        void PtSeg(Vec2 x, Vec2 a, Vec2 b, bool onFirst, double sx)
        {
            var ab = b - a; double L2 = ab.X * ab.X + ab.Y * ab.Y;
            double u = L2 < 1e-18 ? 0 : Math.Clamp(((x.X - a.X) * ab.X + (x.Y - a.Y) * ab.Y) / L2, 0, 1);
            var c = new Vec2(a.X + ab.X * u, a.Y + ab.Y * u);
            double dist = Vec2.Distance(x, c);
            if (dist < best) { best = dist; sBest = onFirst ? u : sx; }
        }
        PtSeg(p2, p1, q1, true, 0); PtSeg(q2, p1, q1, true, 0);
        PtSeg(p1, p2, q2, false, 0); PtSeg(q1, p2, q2, false, 1);
        s = sBest;
        return best;
    }
}
