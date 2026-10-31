using SBM.Core.Alignments;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Grading;

/// <summary>A protection layer built on the graded surface (closed solid below the finished grade).</summary>
public sealed class GradingProtectionResult
{
    public required GradingProtectionDefinition Definition { get; init; }
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public required Mesh Solid { get; init; }
    /// <summary>Area of the covered surface (m2, sloped).</summary>
    public double Area { get; init; }
    public double Volume => Math.Abs(Solid.Volume());
}

/// <summary>Result of <see cref="GradingGenerator"/>.</summary>
public sealed class GradingModel
{
    public required GradingDefinition Definition { get; init; }
    /// <summary>The feature line (densified, closed lines repeat the first point at the end).</summary>
    public List<Vec3> FeatureLine { get; } = new();
    /// <summary>Daylight lines on the existing ground (one per run of rays that daylighted).</summary>
    public List<List<Vec3>> Daylight { get; } = new();
    /// <summary>Graded surfaces: the slope from the feature line to the daylight line, and the top of a closed line (upward normals, open surfaces).</summary>
    public Mesh? SlopeSurface { get; set; }
    public Mesh? TopSurface { get; set; }
    public List<GradingProtectionResult> Protection { get; } = new();
    /// <summary>Cut / fill between the graded surface and the existing ground (m3, plan grid sampling); null without terrain.</summary>
    public double? CutVolume { get; set; }
    public double? FillVolume { get; set; }
    public double SurfaceArea { get; set; }
    public List<Diagnostic> Diagnostics { get; } = new();
    public IEnumerable<Mesh> Surfaces => new[] { TopSurface, SlopeSurface }.OfType<Mesh>().Where(m => !m.IsEmpty);
    public bool IsBuilt => Surfaces.Any();
}

/// <summary>
/// Grading generator (pure). The feature line is densified to <see cref="GradingDefinition.SampleSpacing"/>; at every point a ray leaves on the slope side
/// (corners on that side get a fan every 7.5 deg, re-entrant corners one bisector ray) and runs down (fill) or up (cut) at the plan ratio until it meets the
/// existing ground (bisection, like the road side slopes). Closed lines: the interior is triangulated flat between the feature line points (a pad).
/// </summary>
public static class GradingGenerator
{
    public static List<GradingModel> All(Project.BridgeProject project, Func<Vec2, double?>? ground)
    {
        var list = new List<GradingModel>();
        foreach (var g in project.Gradings)
        {
            Alignment? al = null;
            if (g.AlignmentName.Length > 0)
            {
                al = project.Alignments.FirstOrDefault(a => string.Equals(a.Name, g.AlignmentName, StringComparison.OrdinalIgnoreCase));
                if (al == null)
                {
                    var gm = new GradingModel { Definition = g };
                    gm.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, $"Grading {g.Name}: alignment '{g.AlignmentName}' not found - not built."));
                    list.Add(gm);
                    continue;
                }
            }
            list.Add(Generate(g, al, ground));
        }
        return list;
    }

    public static GradingModel Generate(GradingDefinition d, Alignment? a, Func<Vec2, double?>? ground)
    {
        var m = new GradingModel { Definition = d };
        void Warn(string s) => m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Grading {d.Name}: {s}"));
        void Info(string s) => m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Grading {d.Name}: {s}"));
        foreach (var u in d.Unresolved) Warn(u);

        // ---- feature line ----
        var bad = new List<int>();
        var pts = new List<Vec3>();
        for (int i = 0; i < d.Points.Count; i++)
        {
            var p = d.Points[i];
            bool ok = !double.IsNaN(p.Elevation) && (a != null ? !double.IsNaN(p.Station) && !double.IsNaN(p.Offset) : !double.IsNaN(p.X) && !double.IsNaN(p.Y));
            if (!ok) { bad.Add(i + 1); continue; }
            pts.Add((a != null ? a.PointAt(p.Station, p.Offset) : new Vec2(p.X, p.Y)).WithZ(p.Elevation));
        }
        if (bad.Count > 0) { Warn(UnresolvedFlag.Text(d.Source, $"grading {d.Name}", $"point(s) {string.Join(", ", bad)} without position or elevation", "not built.")); return m; }
        // drop repeated points (a closed line may list its first point again)
        pts = pts.Where((p, i) => i == 0 || (p.XY - pts[i - 1].XY).Length > 1e-6).ToList();
        if (d.Closed && pts.Count > 1 && (pts[0].XY - pts[^1].XY).Length <= 1e-6) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < (d.Closed ? 3 : 2)) { Warn($"needs at least {(d.Closed ? 3 : 2)} distinct points - not built."); return m; }
        double sp = d.SampleSpacing > 0.05 ? d.SampleSpacing : 1.0;
        var dense = new List<Vec3>(); var corner = new List<bool>();
        int segs = d.Closed ? pts.Count : pts.Count - 1;
        for (int i = 0; i < segs; i++)
        {
            Vec3 p0 = pts[i], p1 = pts[(i + 1) % pts.Count];
            int n = Math.Max(1, (int)Math.Ceiling((p1.XY - p0.XY).Length / sp));
            for (int k = 0; k < n; k++) { dense.Add(p0 + (p1 - p0) * ((double)k / n)); corner.Add(k == 0); }
        }
        if (!d.Closed) { dense.Add(pts[^1]); corner.Add(true); }
        m.FeatureLine.AddRange(dense);
        if (d.Closed) m.FeatureLine.Add(dense[0]);
        double area2 = 0;
        if (d.Closed) for (int i = 0; i < dense.Count; i++) area2 += Vec2.Cross(dense[i].XY, dense[(i + 1) % dense.Count].XY);

        // ---- top of a closed line ----
        if (d.Closed)
        {
            var poly = dense.Select(p => p.XY).ToList();
            var tris = MeshBuilder.Triangulate(poly);
            var top = new Mesh();
            foreach (var p in dense) top.Add(p);
            foreach (var (i0, i1, i2) in tris) AddUp(top, i0, i1, i2);
            m.TopSurface = top;
        }

        // ---- slope ----
        if (d.Slope is { } ss)
        {
            bool right;
            if (d.Side == null) { Warn(UnresolvedFlag.Text(d.Source, $"grading {d.Name} slope", "the side the slope runs to is not given", "slope not built.")); goto Volumes; }
            if (d.Side == GradingSide.Outside)
            {
                if (!d.Closed) { Warn("slope side Outside needs a closed line - slope not built."); goto Volumes; }
                right = area2 > 0;   // counter-clockwise: the outside is on the right
            }
            else right = d.Side == GradingSide.Right;
            if (!(ss.CutRatio > 0 && ss.FillRatio > 0 && ss.MaxWidth > 0))
            { Warn(UnresolvedFlag.Text(d.Source, $"grading {d.Name} slope", "cut / fill ratio and the maximum width are not all given", "slope not built.")); goto Volumes; }
            if (ground == null) { Warn(UnresolvedFlag.Text(d.Source, $"grading {d.Name} slope", "no terrain is loaded to daylight it on", "slope not built.")); goto Volumes; }

            Vec2 SideN(Vec2 dir) { var u = dir.Normalized(); return right ? u.Right : u.Left; }
            // rays: (top point index, direction)
            var rays = new List<(int top, Vec2 dir)>();
            int nd = dense.Count;
            for (int i = 0; i < nd; i++)
            {
                bool hasIn = d.Closed || i > 0, hasOut = d.Closed || i < nd - 1;
                Vec2 dIn = hasIn ? dense[i].XY - dense[(i - 1 + nd) % nd].XY : Vec2.Zero, dOut = hasOut ? dense[(i + 1) % nd].XY - dense[i].XY : Vec2.Zero;
                if (!hasIn) { rays.Add((i, SideN(dOut))); continue; }
                if (!hasOut) { rays.Add((i, SideN(dIn))); continue; }
                Vec2 nIn = SideN(dIn), nOut = SideN(dOut);
                double turn = Math.Atan2(Vec2.Cross(nIn, nOut), Vec2.Dot(nIn, nOut));
                double cross = Vec2.Cross(dIn, dOut);
                bool convex = right ? cross > 0 : cross < 0;
                if (!corner[i] || Math.Abs(turn) < 5 * Math.PI / 180 || !convex) { rays.Add((i, (nIn + nOut).Normalized())); continue; }
                int k = Math.Max(1, (int)Math.Ceiling(Math.Abs(turn) / (7.5 * Math.PI / 180)));
                for (int j = 0; j <= k; j++) rays.Add((i, nIn.Rotate(turn * j / k)));
            }
            var day = new Vec3?[rays.Count];
            int missing = 0;
            for (int r = 0; r < rays.Count; r++)
            {
                day[r] = Daylight(dense[rays[r].top], rays[r].dir, ss, ground);
                if (day[r] == null) missing++;
            }
            if (missing > 0) Warn($"slope: no daylight within {ss.MaxWidth:0.###} m (or no ground) on {missing} of {rays.Count} rays - not built there.");
            var slope = new Mesh();
            int last = d.Closed ? rays.Count : rays.Count - 1;
            List<Vec3>? run = null;
            for (int r = 0; r < last; r++)
            {
                int r1 = (r + 1) % rays.Count;
                if (day[r] is not Vec3 d0 || day[r1] is not Vec3 d1) { if (run != null) { m.Daylight.Add(run); run = null; } continue; }
                Vec3 t0 = dense[rays[r].top], t1 = dense[rays[r1].top];
                if (run == null) { run = new() { d0 }; }
                run.Add(d1);
                int a0 = slope.Add(t0), b0 = slope.Add(d0), b1 = slope.Add(d1);
                AddUp(slope, a0, b0, b1);
                if (rays[r].top != rays[r1].top) { int a1 = slope.Add(t1); AddUp(slope, a0, b1, a1); }
            }
            if (run != null) m.Daylight.Add(run);
            if (!slope.IsEmpty) m.SlopeSurface = slope;
        }

    Volumes:
        m.SurfaceArea = m.Surfaces.Sum(Area);
        if (!m.IsBuilt) return m;
        if (ground != null)
        {
            var ix = new SurfaceIndex(m.Surfaces);
            var b = ix.Bounds;
            double w = b.Max.X - b.Min.X, h = b.Max.Y - b.Min.Y;
            double step = Math.Max(0.05, Math.Sqrt(Math.Max(w * h, 1e-6) / 400000));
            double cut = 0, fill = 0;
            for (double x = b.Min.X + step / 2; x < b.Max.X; x += step)
                for (double y = b.Min.Y + step / 2; y < b.Max.Y; y += step)
                {
                    var p = new Vec2(x, y);
                    if (ix.ElevationAt(p) is not double z || ground(p) is not double g) continue;
                    if (z > g) fill += (z - g) * step * step; else cut += (g - z) * step * step;
                }
            m.CutVolume = cut; m.FillVolume = fill;
            Info($"earthwork against the existing ground (plan grid {step:0.###} m): cut {cut / 0.764554858:0.0} CY, fill {fill / 0.764554858:0.0} CY.");
        }

        // ---- protection ----
        int pi = 0;
        foreach (var pr in d.Protection)
        {
            string pages = pr.Source.Length > 0 ? pr.Source : d.Source;
            string label = pr.Name.Length > 0 ? pr.Name : pr.Kind.Length > 0 ? pr.Kind : "protection";
            var miss = new List<string>();
            if (pr.Region == null) miss.Add("region (slope / top)");
            if (!(pr.Thickness > 0)) miss.Add("thickness");
            if (miss.Count > 0) { Warn(UnresolvedFlag.Text(pages, $"grading {d.Name} {label}", $"{string.Join(", ", miss)} not given", "not built.")); continue; }
            var surf = pr.Region == GradingProtectionRegion.Top ? m.TopSurface : m.SlopeSurface;
            if (surf == null || surf.IsEmpty) { Warn($"{label}: the grading has no {pr.Region!.Value.ToString().ToLowerInvariant()} surface - not built."); continue; }
            var solid = Thicken(surf, pr.Thickness);
            pi++;
            m.Protection.Add(new GradingProtectionResult { Definition = pr, Index = pi, Name = $"{d.Name} {label}", Solid = solid, Area = Area(surf) });
        }
        return m;
    }

    /// <summary>Adds the triangle with its normal pointing up.</summary>
    private static void AddUp(Mesh m, int a, int b, int c)
    {
        var n = Vec3.Cross(m.Positions[b] - m.Positions[a], m.Positions[c] - m.Positions[a]);
        if (n.Length < 1e-14) return;
        if (n.Z >= 0) m.Tri(a, b, c); else m.Tri(a, c, b);
    }

    private static double Area(Mesh m)
    {
        double s = 0;
        for (int t = 0; t < m.TriangleCount; t++)
        {
            var a = m.Positions[m.Indices[3 * t]];
            s += 0.5 * Vec3.Cross(m.Positions[m.Indices[3 * t + 1]] - a, m.Positions[m.Indices[3 * t + 2]] - a).Length;
        }
        return s;
    }

    /// <summary>Daylight point of a ray from the top point: first sign change of (slope line - ground) within MaxWidth, then bisection.</summary>
    private static Vec3? Daylight(Vec3 e, Vec2 dir, Road.RoadSideSlope ss, Func<Vec2, double?> ground)
    {
        double? g0 = ground(e.XY);
        if (g0 == null) return null;
        if (Math.Abs(g0.Value - e.Z) < 1e-6) return e;
        bool cut = g0.Value > e.Z;
        double ratio = cut ? ss.CutRatio : ss.FillRatio;
        double F(double h) { double? g = ground(e.XY + dir * h); return g == null ? double.NaN : (e.Z + (cut ? h : -h) / ratio) - g.Value; }
        double lo = 0, flo = F(0), hi = double.NaN;
        int steps = Math.Max(4, (int)Math.Ceiling(ss.MaxWidth / 0.25));
        for (int k = 1; k <= steps; k++)
        {
            double h = ss.MaxWidth * k / steps, fh = F(h);
            if (double.IsNaN(fh)) return null;
            if (Math.Sign(fh) != Math.Sign(flo)) { hi = h; break; }
            lo = h; flo = fh;
        }
        if (double.IsNaN(hi)) return null;
        for (int it = 0; it < 50; it++) { double mid = (lo + hi) / 2, fm = F(mid); if (Math.Sign(fm) == Math.Sign(flo)) { lo = mid; flo = fm; } else hi = mid; }
        double hd = (lo + hi) / 2;
        return (e.XY + dir * hd).WithZ(e.Z + (cut ? hd : -hd) / ratio);
    }

    /// <summary>Closed solid from an open upward surface: the surface on top, a copy offset by <paramref name="t"/> along the (area weighted) vertex normals below, side walls along the boundary.</summary>
    internal static Mesh Thicken(Mesh surf, double t)
    {
        var id = new Dictionary<Vec3, int>(); var pos = new List<Vec3>(); var tris = new List<(int, int, int)>();
        int V(Vec3 p) { if (!id.TryGetValue(p, out int i)) { i = pos.Count; id[p] = i; pos.Add(p); } return i; }
        for (int k = 0; k < surf.TriangleCount; k++)
        {
            int a = V(surf.Positions[surf.Indices[3 * k]]), b = V(surf.Positions[surf.Indices[3 * k + 1]]), c = V(surf.Positions[surf.Indices[3 * k + 2]]);
            if (a != b && b != c && a != c) tris.Add((a, b, c));
        }
        var nrm = new Vec3[pos.Count];
        foreach (var (a, b, c) in tris) { var n = Vec3.Cross(pos[b] - pos[a], pos[c] - pos[a]); nrm[a] += n; nrm[b] += n; nrm[c] += n; }
        var mesh = new Mesh();
        foreach (var p in pos) mesh.Add(p);
        int off = pos.Count;
        for (int i = 0; i < pos.Count; i++) mesh.Add(pos[i] - (nrm[i].Length > 0 ? nrm[i].Normalized() : new Vec3(0, 0, 1)) * t);
        var edges = new Dictionary<(int, int), int>();
        foreach (var (a, b, c) in tris)
        {
            mesh.Tri(a, b, c); mesh.Tri(off + a, off + c, off + b);
            foreach (var e in new[] { (a, b), (b, c), (c, a) }) edges[e] = edges.GetValueOrDefault(e) + 1;
        }
        foreach (var ((a, b), _) in edges)
            if (!edges.ContainsKey((b, a))) { mesh.Tri(b, a, off + a); mesh.Tri(b, off + a, off + b); }
        if (mesh.Volume() < 0) for (int i = 0; i < mesh.Indices.Count; i += 3) (mesh.Indices[i + 1], mesh.Indices[i + 2]) = (mesh.Indices[i + 2], mesh.Indices[i + 1]);
        return mesh;
    }
}
