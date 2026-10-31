using SBM.Core.Alignments;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Wall;

/// <summary>One wall panel between joints (or the wall ends): stem and footing solids (several when a footing step falls inside the panel).</summary>
public sealed class WallPanel
{
    public int Index { get; init; }
    public double FromStation { get; init; }
    public double ToStation { get; init; }
    public List<Mesh> Stems { get; } = new();
    public List<Mesh> Footings { get; } = new();
    public double StemVolume => Stems.Sum(m => Math.Abs(m.Volume()));
    public double FootingVolume => Footings.Sum(m => Math.Abs(m.Volume()));
}

/// <summary>Result of <see cref="RetainingWallGenerator"/>.</summary>
public sealed class RetainingWallModel
{
    public required RetainingWallDefinition Definition { get; init; }
    /// <summary>Cantilever walls: stem / footing per joint panel. MSE walls: the joint panels (stations only; the solids are in <see cref="Elements"/>).</summary>
    public List<WallPanel> Panels { get; } = new();
    /// <summary>MSE walls: facing, facing panels, leveling pad, coping, reinforced zone and reinforcement elements.</summary>
    public List<WallElement> Elements { get; } = new();
    public bool IsMse => Definition.Mse != null;
    /// <summary>MSE elements written to the IFC / database: those with solids, and a facing without its own solid that holds facing panels.</summary>
    public IEnumerable<WallElement> ExportElements() => Elements.Where(e => e.Solids.Count > 0
        || (e.Role == WallElementRole.Facing && Elements.Any(x => x.Role == WallElementRole.FacingPanel && x.Panel == e.Panel)));
    public bool IsBuilt => IsMse ? Elements.Any(e => e.Solids.Count > 0) : Panels.Count > 0;
    /// <summary>Exposed (front) face area of the stem above the footing top, along the control line (m2).</summary>
    public double FaceArea { get; set; }
    /// <summary>Plan lines: the control line at the top of wall and the footing toe / heel lines (for plan drawings and checks).</summary>
    public List<Vec3[]> PlanLines { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    /// <summary>Abutment-on-MSE interfaces (filled by <see cref="MseAbutmentCheck.Run"/> after the bridges are generated).</summary>
    public List<MseAbutmentResult> Interfaces { get; } = new();
    public double StemVolume => Panels.Sum(p => p.StemVolume);
    public double FootingVolume => Panels.Sum(p => p.FootingVolume);
}

/// <summary>
/// Cantilever cast-in-place retaining wall generator (pure). Section coordinates: u from the control line toward the BACK (retained side), z = elevation.
/// Stem: front face through (0, top of wall) with the front batter, back face from the top thickness with the back batter, down to the footing top.
/// Footing: from the toe to the heel (measured from the stem faces at the footing top), its thickness below the footing top, optional key below it.
/// Pieces are swept between the joint / step stations; each is one closed loft.
/// </summary>
public static partial class RetainingWallGenerator
{
    public static List<RetainingWallModel> All(Project.BridgeProject project)
    {
        var list = new List<RetainingWallModel>();
        foreach (var w in project.Walls)
        {
            var al = project.Alignments.FirstOrDefault(a => string.Equals(a.Name, w.AlignmentName, StringComparison.OrdinalIgnoreCase));
            if (al == null)
            {
                var wm = new RetainingWallModel { Definition = w };
                wm.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, $"Wall {w.Name}: alignment '{w.AlignmentName}' not found - not built."));
                list.Add(wm);
                continue;
            }
            int lod = project.Lod?.LevelFor(w.Name, Project.LodGroup.Reinforcing) ?? Project.LodSettings.Full;
            list.Add(Generate(w, al, lod >= Project.LodSettings.Full));
        }
        return list;
    }

    private static double Interp(List<WallElevationPoint> t, double s)
    {
        var r = t.OrderBy(x => x.Station).ToList();
        if (s <= r[0].Station) return r[0].Elevation;
        if (s >= r[^1].Station) return r[^1].Elevation;
        for (int i = 0; i + 1 < r.Count; i++)
            if (s <= r[i + 1].Station)
            {
                double f = (s - r[i].Station) / Math.Max(1e-12, r[i + 1].Station - r[i].Station);
                return r[i].Elevation + f * (r[i + 1].Elevation - r[i].Elevation);
            }
        return r[^1].Elevation;
    }

    /// <param name="reinforcement">build individual MSE reinforcements (LOD 400 of the Reinforcing group); the zone envelope is always built.</param>
    public static RetainingWallModel Generate(RetainingWallDefinition d, Alignment a, bool reinforcement = true)
    {
        if (d.Mse != null) return GenerateMse(d, a, reinforcement);
        var m = new RetainingWallModel { Definition = d };
        void Warn(string s) => m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Wall {d.Name}: {s}"));
        foreach (var u in d.Unresolved) Warn(u);
        var missing = new List<string>();
        if (d.FaceSide == null) missing.Add("face side");
        if (d.TopOfWall.Count == 0) missing.Add("top of wall elevations");
        if (d.FootingTopSteps is not { Count: > 0 } && d.FootingTopProfile is not { Count: > 0 }) missing.Add("top of footing elevations");
        foreach (var (v, n) in new[] { (d.StemTopThickness, "stem top thickness"), (d.FrontBatter, "front batter"), (d.BackBatter, "back batter"),
                     (d.FootingThickness, "footing thickness"), (d.Toe, "toe"), (d.Heel, "heel") })
            if (double.IsNaN(v)) missing.Add(n);
        if (d.Key is { } kd && (double.IsNaN(kd.Width) || double.IsNaN(kd.Depth) || double.IsNaN(kd.FromToe))) missing.Add("key width / depth / position");
        if (missing.Count > 0)
        {
            Warn(UnresolvedFlag.Text(d.Source, $"wall {d.Name}", $"{string.Join(", ", missing)} not given", "wall not built."));
            return m;
        }
        double s0 = Math.Min(d.BeginStation, d.EndStation), s1 = Math.Max(d.BeginStation, d.EndStation);
        if (!(s1 - s0 > 1e-3)) { Warn("begin and end station are equal - not built."); return m; }
        if (!(d.StemTopThickness > 0) || !(d.FootingThickness > 0) || d.Toe < 0 || d.Heel < 0 || d.FrontBatter < 0 || d.BackBatter < 0)
        { Warn("stem / footing thickness must be > 0 and toe, heel, batters >= 0 - not built."); return m; }
        double face = d.FaceSide == WallFaceSide.Right ? 1 : -1;
        double Top(double s) => Interp(d.TopOfWall, s);
        bool steps = d.FootingTopSteps is { Count: > 0 };
        var stepRows = steps ? d.FootingTopSteps!.OrderBy(x => x.Station).ToList() : new();
        double Ftg(double s, bool atSegmentEnd)
        {
            if (!steps) return Interp(d.FootingTopProfile!, s);
            // the level of the step that covers s; exactly on a step station: the step ahead, except for the last section of the segment ending there (the step behind)
            var row = stepRows[0];
            foreach (var r in stepRows) if (r.Station < s - 1e-9 || (!atSegmentEnd && Math.Abs(r.Station - s) <= 1e-9)) row = r;
            return row.Elevation;
        }
        Vec3 W(double s, double u, double z) => a.PointAt(s, d.Offset - face * u).WithZ(z);

        // panel boundaries (joints) and solid breaks (joints + footing steps)
        var joints = d.JointStations.Where(j => j > s0 + 1e-6 && j < s1 - 1e-6).OrderBy(x => x).ToList();
        var breaks = joints.Concat(steps ? stepRows.Select(r => r.Station).Where(x => x > s0 + 1e-6 && x < s1 - 1e-6) : Enumerable.Empty<double>())
            .Concat(new[] { s0, s1 }).Distinct().OrderBy(x => x).ToList();
        var extra = d.TopOfWall.Select(r => r.Station).Concat(d.FootingTopProfile?.Select(r => r.Station) ?? Enumerable.Empty<double>()).ToList();
        var bounds = new List<double> { s0 }; bounds.AddRange(joints); bounds.Add(s1);
        for (int p = 0; p + 1 < bounds.Count; p++) m.Panels.Add(new WallPanel { Index = p + 1, FromStation = bounds[p], ToStation = bounds[p + 1] });
        double sp = d.SampleSpacing > 0.05 ? d.SampleSpacing : 2.0;
        var key = d.Key;
        var front = new List<Vec3>(); var toe = new List<Vec3>(); var heel = new List<Vec3>();
        for (int b = 0; b + 1 < breaks.Count; b++)
        {
            double a0 = breaks[b], a1 = breaks[b + 1];
            int n = Math.Max(1, (int)Math.Ceiling((a1 - a0) / sp));
            var st = Enumerable.Range(0, n + 1).Select(i => a0 + (a1 - a0) * i / n).Concat(extra.Where(x => x > a0 + 1e-6 && x < a1 - 1e-6)).Distinct().OrderBy(x => x).ToList();
            var stemRings = new List<Vec3[]>(); var ftgRings = new List<Vec3[]>();
            List<Vec2>? stemSec = null, ftgSec = null;
            for (int i = 0; i < st.Count; i++)
            {
                double s = st[i], t = Top(s), zf = Ftg(s, i == st.Count - 1);
                if (t - zf < 1e-3) { Warn($"top of wall is not above the top of footing at station {s:0.###} - not built."); m.Panels.Clear(); return m; }
                double h = t - zf, uf = -d.FrontBatter * h, ub = d.StemTopThickness + d.BackBatter * h, ut = uf - d.Toe, uh = ub + d.Heel, zb = zf - d.FootingThickness;
                var stem = new List<Vec2> { new(uf, zf), new(ub, zf), new(d.StemTopThickness, t), new(0, t) };
                var ftg = new List<Vec2> { new(ut, zf), new(uh, zf), new(uh, zb) };
                if (key != null)
                {
                    double k0 = ut + key.FromToe, k1 = k0 + key.Width;
                    ftg.AddRange(new Vec2[] { new(k1, zb), new(k1, zb - key.Depth), new(k0, zb - key.Depth), new(k0, zb) });
                }
                ftg.Add(new(ut, zb));
                stemSec ??= stem; ftgSec ??= ftg;
                stemRings.Add(stem.Select(q => W(s, q.X, q.Y)).ToArray());
                ftgRings.Add(ftg.Select(q => W(s, q.X, q.Y)).ToArray());
                if (i > 0 || b == 0) { front.Add(W(s, 0, t)); toe.Add(W(s, ut, zf)); heel.Add(W(s, uh, zf)); }
                if (i > 0)
                {
                    double ds = s - st[i - 1], hp = Top(st[i - 1]) - Ftg(st[i - 1], false);
                    m.FaceArea += ds * 0.5 * (h + hp) * Math.Sqrt(1 + d.FrontBatter * d.FrontBatter);
                }
            }
            var stemMesh = new Mesh(); MeshBuilder.AddLoft(stemMesh, stemSec!, stemRings, false);
            var ftgMesh = new Mesh(); MeshBuilder.AddLoft(ftgMesh, ftgSec!, ftgRings, false);
            var panel = m.Panels.Last(p => p.FromStation <= a0 + 1e-9);
            panel.Stems.Add(stemMesh); panel.Footings.Add(ftgMesh);
        }
        m.PlanLines.Add(front.ToArray()); m.PlanLines.Add(toe.ToArray()); m.PlanLines.Add(heel.ToArray());
        const double cy = 0.764554858;
        m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Wall {d.Name}: {m.Panels.Count} panels, stem {m.StemVolume / cy:0.0} CY, footing {m.FootingVolume / cy:0.0} CY, " +
            $"exposed face above the footing top {m.FaceArea / 0.09290304:0} SF."));
        return m;
    }
}
