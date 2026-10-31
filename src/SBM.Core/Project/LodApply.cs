using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.Core.Project;

/// <summary>
/// Reduces a freshly generated bridge model to its level of detail (<see cref="LodSettings"/>). Runs inside <see cref="RegenerationCache"/>
/// on the objects of one cache entry (the level is part of the cache key), never on a model generated for another level.
/// Fasteners / welds / placed bars below 400 and strands below 300 are removed; steel plates below 350 lose their bolt holes (the recorded
/// extrusion outlines, <see cref="MeshLod.WithoutHoles"/>); connection parts below 300 are removed; at 100 each assembly becomes one envelope
/// block carrying the assembly's steel weight. Bar MARKS (schedules) are always kept. Every change is reported as one Info diagnostic.
/// </summary>
public static class LodApply
{
    /// <summary>Parts that only connect other parts (removed below 300).</summary>
    public static bool IsConnectionPart(PartCategory c) => c is PartCategory.Gusset or PartCategory.FillerPlate or PartCategory.SplicePlate
        or PartCategory.ConnectionStiffener or PartCategory.TrussGusset or PartCategory.TrussConnectionAngle or PartCategory.TrussConnectionPlate
        or PartCategory.ArchConnectionPlate or PartCategory.FinPlate or PartCategory.ConnectorPlate;

    /// <summary>Phase 1 products (superstructure steel, concrete, prestressed beams).</summary>
    public static void Superstructure(LodSettings lod, string bridge, SteelModel? st, Concrete.ConcreteModel? cm, Prestressed.PrestressedModel? ps)
    {
        int steel = lod.LevelFor(bridge, LodGroup.Steel), conn = lod.LevelFor(bridge, LodGroup.Connections), fast = lod.LevelFor(bridge, LodGroup.Fasteners),
            weld = lod.LevelFor(bridge, LodGroup.Welds), bars = lod.LevelFor(bridge, LodGroup.Reinforcing), pre = lod.LevelFor(bridge, LodGroup.Prestressing);
        var done = new List<string>();
        if (st != null)
        {
            if (fast < LodSettings.Full && st.Bolts.Count > 0) { done.Add($"{st.Bolts.Count:N0} fasteners"); st.Bolts.Clear(); }
            if (fast < LodSettings.Full && st.Studs.Count > 0) { done.Add($"{st.Studs.Count:N0} shear studs"); st.Studs.Clear(); }
            if (weld < LodSettings.Full)
            {
                int n = st.Parts.RemoveAll(p => p.Category == PartCategory.Weld);
                if (n > 0) done.Add($"{n:N0} welds");
            }
            if (conn < 300)
            {
                int n = st.Parts.RemoveAll(p => IsConnectionPart(p.Category));
                if (n > 0) done.Add($"{n:N0} connection parts");
            }
            if (steel < 350)
            {
                int n = 0;
                for (int i = 0; i < st.Parts.Count; i++)
                {
                    var p = st.Parts[i];
                    var m = MeshLod.WithoutHoles(p.Mesh);
                    if (ReferenceEquals(m, p.Mesh)) continue;
                    st.Parts[i] = WithMesh(p, m, p.Length, p.NominalWeightLbPerFt);
                    n++;
                }
                if (n > 0) done.Add($"bolt holes filled in {n:N0} parts (their mesh-based mass includes the holes)");
            }
            if (steel < 200) done.Add(Envelopes(st));
            // assemblies left without parts (all removed) are dropped
            var used = st.Parts.Select(p => p.Assembly).ToHashSet();
            int na = st.Assemblies.RemoveAll(a => !used.Contains(a.Mark));
            if (na > 0) done.Add($"{na:N0} empty assemblies");
        }
        if (cm != null && bars < LodSettings.Full && cm.Bars.Count > 0) { done.Add($"{cm.Bars.Count:N0} deck bars (marks kept)"); cm.Bars.Clear(); }
        if (ps != null)
        {
            int nb = 0, ns = 0;
            foreach (var b in ps.Beams)
            {
                if (bars < LodSettings.Full) { nb += b.Bars.Count; b.Bars.Clear(); }
                if (pre < 300) { ns += b.Strands.Count; b.Strands.Clear(); }
            }
            if (nb > 0) done.Add($"{nb:N0} beam bars (marks kept)");
            if (ns > 0) done.Add($"{ns:N0} strands");
        }
        Report(lod, bridge, done, st?.Diagnostics ?? cm?.Diagnostics ?? ps?.Diagnostics);
    }

    /// <summary>Phase 2 products (substructure).</summary>
    public static void Substructure(LodSettings lod, string bridge, Substructure.SubstructureModel? sub)
    {
        if (sub == null || lod.LevelFor(bridge, LodGroup.Reinforcing) >= LodSettings.Full || sub.Bars.Count == 0) return;
        var done = new List<string> { $"{sub.Bars.Count:N0} substructure bars (marks kept)" };
        sub.Bars.Clear();
        Report(lod, bridge, done, sub.Diagnostics);
    }

    private static void Report(LodSettings lod, string bridge, List<string> done, List<Diagnostic>? to)
    {
        if (to == null || done.Count == 0) return;
        to.Add(new Diagnostic(DiagnosticSeverity.Info, $"Level of detail ({lod.Describe(bridge)}): removed / simplified {string.Join(", ", done)}."));
    }

    /// <summary>LOD 100: each assembly's parts become one box along the assembly's long plan direction (principal axis of its vertices), plumb, with the
    /// assembly's steel weight (as a nominal weight over the box length). Category / material of the heaviest part.</summary>
    private static string Envelopes(SteelModel st)
    {
        var groups = st.Parts.GroupBy(p => p.Assembly).ToList();
        var result = new List<SteelPart>();
        int n = 0;
        foreach (var g in groups)
        {
            var parts = g.Where(p => !p.Mesh.IsEmpty).ToList();
            if (parts.Count == 0) continue;
            var pts = parts.SelectMany(p => p.Mesh.Positions).ToList();
            double cx = pts.Average(q => q.X), cy = pts.Average(q => q.Y);
            double sxx = 0, syy = 0, sxy = 0;
            foreach (var q in pts) { double dx = q.X - cx, dy = q.Y - cy; sxx += dx * dx; syy += dy * dy; sxy += dx * dy; }
            double ang = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
            var U = new Vec3(Math.Cos(ang), Math.Sin(ang), 0); var V = new Vec3(-Math.Sin(ang), Math.Cos(ang), 0); var Z = new Vec3(0, 0, 1);
            var c = new Vec3(cx, cy, 0);
            double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue, z0 = double.MaxValue, z1 = double.MinValue;
            foreach (var q in pts)
            {
                var d = q - c;
                double u = Vec3.Dot(d, U), v = Vec3.Dot(d, V);
                u0 = Math.Min(u0, u); u1 = Math.Max(u1, u); v0 = Math.Min(v0, v); v1 = Math.Max(v1, v); z0 = Math.Min(z0, q.Z); z1 = Math.Max(z1, q.Z);
            }
            double len = Math.Max(u1 - u0, 1e-3);
            var mesh = new Mesh();
            var sec = new[] { new Vec2(v0, z0), new Vec2(v1, z0), new Vec2(v1, z1), new Vec2(v0, z1) };
            Vec3 W(double u, Vec2 s) => c + U * u + V * s.X + Z * s.Y;
            MeshBuilder.AddLoft(mesh, sec, new[] { sec.Select(s => W(u0, s)).ToArray(), sec.Select(s => W(u1, s)).ToArray() });
            double mass = parts.Sum(p => p.Mass);
            var main = parts.MaxBy(p => p.Mass)!;
            result.Add(new SteelPart
            {
                Mark = g.Key + "-ENV", Category = main.Category, Assembly = g.Key, Description = $"LOD 100 envelope of {parts.Count} parts", Mesh = mesh,
                Material = main.Material, Length = len, NominalWeightLbPerFt = mass / (1.48816394 * len),
            });
            n += parts.Count;
        }
        st.Parts.Clear();
        st.Parts.AddRange(result);
        return $"{n:N0} parts as {result.Count:N0} assembly envelopes";
    }

    private static SteelPart WithMesh(SteelPart p, Mesh m, double length, double nominal) => new()
    {
        Mark = p.Mark, Category = p.Category, Assembly = p.Assembly, Description = p.Description, Mesh = m, Material = p.Material, Role = p.Role,
        Length = length, NominalWeightLbPerFt = nominal, Assumption = p.Assumption, Density = p.Density, Fabrication = p.Fabrication, Fits = p.Fits,
        MassNotCounted = p.MassNotCounted, Member = p.Member, Piece = p.Piece,
    };
}
