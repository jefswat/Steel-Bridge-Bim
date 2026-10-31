using SBM.Core.Checks;
using SBM.Core.Checks.Measure;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;

namespace SBM.Core.Wall;

/// <summary>
/// An abutment on (behind) an MSE wall (OPTIONAL <see cref="MseWallDefinition.Abutments"/>; design note section 3.2): the abutment of support <see cref="Support"/> of
/// bridge <see cref="Bridge"/> sits on the reinforced fill. Every value is a plan value; NaN = not given (the check is then reported only / the sleeves are not built).
/// </summary>
public sealed class MseAbutmentInterface
{
    public string Bridge { get; set; } = "";
    public string Support { get; set; } = "";
    /// <summary>Required clear distance from the back of the facing (panels / coping) to the abutment concrete, m; NaN = the distance is reported only.</summary>
    public double MinFacingClearance { get; set; } = double.NaN;
    /// <summary>Pile sleeves through the reinforced zone: inside diameter and wall, m; NaN = no sleeves (the piles in the zone are still reported).</summary>
    public double SleeveDiameter { get; set; } = double.NaN;
    public double SleeveWall { get; set; } = double.NaN;
    /// <summary>Sleeve top elevation, m; NaN = the bottom of the abutment foundation the pile leaves (reported).</summary>
    public double SleeveTop { get; set; } = double.NaN;
    /// <summary>Sleeve bottom elevation, m (plans: usually the bottom of the reinforced zone); NaN = not given -> UNRESOLVED, sleeves not built.</summary>
    public double SleeveBottom { get; set; } = double.NaN;
    public string SleeveMaterial { get; set; } = "";
    public string Source { get; set; } = "";
    public string Unresolved { get; set; } = "";
}

/// <summary>Result of one interface: the measured facing clearance, the piles in the reinforced zone, the sleeves built and the reinforcements they obstruct.</summary>
public sealed record MseAbutmentResult(string Wall, string Bridge, string Support, double FacingClearance, double Required, int PilesInZone, int Sleeves, int ObstructedReinforcements)
{
    /// <summary>Null when no required clearance is given.</summary>
    public bool? ClearanceOk => double.IsNaN(Required) || double.IsNaN(FacingClearance) ? null : FacingClearance >= Required - 1e-6;
}

/// <summary>
/// Abutment-on-MSE interface (after the bridges are generated; reported, nothing is moved): clear distance from the wall facing to the abutment's concrete,
/// the piles whose axis passes through the reinforced zone, pile sleeves along each such pile axis (closed rings, wall element role
/// <see cref="WallElementRole.PileSleeve"/>; IFC IfcBuildingElementPart 'PILE_SLEEVE'), the sleeve size vs the pile, and the soil reinforcements (LOD 400) a sleeve
/// obstructs (the supplier skews / cuts them; reported).
/// </summary>
public static class MseAbutmentCheck
{
    public static void Run(IReadOnlyList<RetainingWallModel> walls, IReadOnlyList<BridgeModel?> bridges)
    {
        foreach (var w in walls.Where(w => w.Definition.Mse?.Abutments is { Count: > 0 }))
        {
            w.Elements.RemoveAll(e => e.Role == WallElementRole.PileSleeve);
            w.Interfaces.Clear();
            var d = w.Definition;
            void Warn(string t) => w.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Wall {d.Name}: {t}"));
            void Info(string t) => w.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Wall {d.Name}: {t}"));
            int sleeveNo = 0;
            foreach (var ai in d.Mse!.Abutments!)
            {
                string src = ai.Source.Length > 0 ? ai.Source : d.Source;
                if (ai.Unresolved.Length > 0) Warn(ai.Unresolved);
                var bm = bridges.FirstOrDefault(b => b != null && string.Equals(b.Definition.Name, ai.Bridge, StringComparison.OrdinalIgnoreCase));
                var unit = bm?.Substructure?.Units.FirstOrDefault(u => string.Equals(u.Name, ai.Support, StringComparison.OrdinalIgnoreCase));
                if (unit == null) { Warn($"abutment interface: support '{ai.Support}' of bridge '{ai.Bridge}' has no generated substructure - not checked."); continue; }
                string at = $"{ai.Bridge} {ai.Support}";
                // facing clearance: abutment concrete vs the facing / coping solids
                var facing = w.Elements.Where(e => e.Role is WallElementRole.Facing or WallElementRole.FacingPanel or WallElementRole.Coping).SelectMany(e => e.Solids).Where(m => !m.IsEmpty).ToList();
                var conc = unit.Solids.Where(s => s.IsConcrete && !s.Mesh.IsEmpty && s.Kind != Substructure.SubElementKind.Pile).ToList();
                double best = double.PositiveInfinity; string near = ""; Vec3 bp = default;
                foreach (var c in conc)
                {
                    var cb = c.Mesh.Bounds();
                    foreach (var f in facing)
                    {
                        if (Geo3.BoxDistance(cb, f.Bounds()) > Math.Min(best, 10)) continue;
                        var (pa, pb, dist) = MeasureQueries.MinDistance(c.Mesh, f);
                        if (dist < best) { best = dist; near = c.Name; bp = (pa + pb) * 0.5; }
                    }
                }
                if (double.IsPositiveInfinity(best)) best = double.NaN;
                if (double.IsNaN(best)) Info($"{at}: no facing within 10 m of the abutment concrete - facing clearance not measured.");
                else if (!double.IsNaN(ai.MinFacingClearance) && best < ai.MinFacingClearance - 1e-6)
                    Warn($"CONFLICT (facing clearance) {at}: {near} is {best:0.###} m from the wall facing at {bp.X:0.##}, {bp.Y:0.##}, el. {bp.Z:0.###}; required {ai.MinFacingClearance:0.###} m ({src}; reported, not moved).");
                else Info($"{at}: {near} is {best:0.###} m clear of the wall facing{(double.IsNaN(ai.MinFacingClearance) ? " (no required clearance given)" : $" (required {ai.MinFacingClearance:0.###} m)")}.");
                // footings bearing on the reinforced fill
                var zone = w.Elements.Where(e => e.Role == WallElementRole.ReinforcedZone).SelectMany(e => e.Solids).Where(m => !m.IsEmpty).ToList();
                foreach (var f in unit.Foundations)
                {
                    var c = unit.World(f.S, f.N, f.BottomZ - 0.01);
                    if (zone.Any(z => Inside(z, c))) Info($"{at}: the foundation bottom (el. {f.BottomZ:0.###}) bears on the reinforced zone.");
                }
                // piles through the reinforced zone, sleeves along their axes
                int inZone = 0, sleeves = 0;
                var sleeveMeshes = new List<Mesh>();
                bool sleeveInputs = ai.SleeveDiameter > 0 && ai.SleeveWall > 0;
                if (!double.IsNaN(ai.SleeveDiameter) && double.IsNaN(ai.SleeveBottom))
                    Warn(UnresolvedFlag.Text(src, $"{at} pile sleeve bottom", "not given", "sleeves not built."));
                foreach (var f in unit.Foundations)
                    foreach (var p in f.Piles)
                    {
                        var head = unit.World(p.S, p.N, p.TopZ); var tip = unit.World(p.TipS, p.TipN, p.TipZ);
                        int n = Math.Max(2, (int)Math.Ceiling((head - tip).Length / 0.25));
                        bool hits = Enumerable.Range(0, n + 1).Any(k => { var q = head + (tip - head) * ((double)k / n); return zone.Any(z => Inside(z, q)); });
                        if (!hits) continue;
                        inZone++;
                        if (!sleeveInputs || double.IsNaN(ai.SleeveBottom)) continue;
                        double zt = double.IsNaN(ai.SleeveTop) ? f.BottomZ : ai.SleeveTop, zb = ai.SleeveBottom;
                        if (!(zt > zb)) { Warn($"{at}: sleeve top {zt:0.###} is not above its bottom {zb:0.###} - not built."); continue; }
                        Vec3 OnAxis(double z) => head + (tip - head) * ((head.Z - z) / (head.Z - tip.Z));
                        var a0 = OnAxis(zt); var a1 = OnAxis(zb);
                        var mesh = Sleeve(a0, a1, ai.SleeveDiameter / 2, ai.SleeveDiameter / 2 + ai.SleeveWall);
                        sleeveNo++; sleeves++;
                        var el = new WallElement { Role = WallElementRole.PileSleeve, Index = sleeveNo, Name = $"{d.Name} pile sleeve {ai.Support} {sleeveNo}", FromStation = unit.Station, ToStation = unit.Station, Length = (a0 - a1).Length };
                        el.Solids.Add(mesh);
                        w.Elements.Add(el);
                        sleeveMeshes.Add(mesh);
                    }
                if (sleeveInputs && sleeves > 0 && double.IsNaN(ai.SleeveTop)) Info($"{at}: sleeve tops at the foundation bottom (the sleeve top is not given).");
                if (inZone > 0 && !sleeveInputs) Info($"{at}: {inZone} piles pass through the reinforced zone; no sleeve size given - sleeves not built.");
                double pileSize = unit.Definition.PileSize;
                if (sleeveInputs && pileSize > 0 && unit.Definition.PileType is SBM.Core.Bridge.PileType.CipPipe && ai.SleeveDiameter < pileSize + 1e-6)
                    Warn($"CONFLICT (sleeve) {at}: sleeve inside diameter {ai.SleeveDiameter:0.###} m is not larger than the {pileSize:0.###} m pile ({src}).");
                // soil reinforcements obstructed by the sleeves (LOD 400 reinforcements only)
                int obstructed = 0;
                var reinf = w.Elements.Where(e => e.Role == WallElementRole.Reinforcement).SelectMany(e => e.Solids).Where(m => !m.IsEmpty).ToList();
                if (sleeveMeshes.Count > 0 && reinf.Count > 0)
                {
                    var items = sleeveMeshes.Select((m, i) => new ClashItem("sleeve", "S" + i, "", "", m))
                        .Concat(reinf.Select((m, i) => new ClashItem("reinforcement", "R" + i, "", "", m))).ToList();
                    var rep = SteelClashCheck.Run(d.Name, items, new ClashOptions(), (x, y) => x.Kind != y.Kind);
                    obstructed = rep.Clashes.Select(c => c.A.Kind == "reinforcement" ? c.A.Mark : c.B.Mark).Distinct().Count();
                    if (obstructed > 0) Warn($"CONFLICT (soil reinforcement) {at}: {obstructed} reinforcements pass through pile sleeves - skewed / cut around them per the wall supplier (reported, not changed).");
                }
                else if (sleeveMeshes.Count > 0) Info($"{at}: soil reinforcements not modeled (LOD below 400 or no supplier data) - the sleeve obstruction is not checked.");
                if (inZone > 0) Info($"{at}: {inZone} piles through the reinforced zone, {sleeves} sleeves built.");
                w.Interfaces.Add(new MseAbutmentResult(d.Name, ai.Bridge, ai.Support, best, ai.MinFacingClearance, inZone, sleeves, obstructed));
            }
        }
    }

    private static Mesh Sleeve(Vec3 a, Vec3 b, double ri, double ro)
    {
        var ax = (b - a).Normalized();
        var u = Math.Abs(ax.Z) < 0.99 ? Vec3.Cross(ax, new Vec3(0, 0, 1)).Normalized() : new Vec3(1, 0, 0);
        var v = Vec3.Cross(u, ax);
        const int n = 24;
        Vec3[] L(Vec3 c, double r) => Enumerable.Range(0, n).Select(i => c + u * (r * Math.Cos(2 * Math.PI * i / n)) + v * (r * Math.Sin(2 * Math.PI * i / n))).ToArray();
        var m = new Mesh();
        int b0 = m.Positions.Count;
        var loops = new[] { L(a, ro), L(a, ri), L(b, ro), L(b, ri) };
        foreach (var lp in loops) foreach (var p in lp) m.Add(p);
        int o0 = b0, i0 = b0 + n, o1 = b0 + 2 * n, i1 = b0 + 3 * n;
        for (int k = 0; k < n; k++)
        {
            int k1 = (k + 1) % n;
            m.Tri(o0 + k, o0 + k1, o1 + k1); m.Tri(o0 + k, o1 + k1, o1 + k);
            m.Tri(i0 + k, i1 + k1, i0 + k1); m.Tri(i0 + k, i1 + k, i1 + k1);
            m.Tri(o0 + k, i0 + k1, o0 + k1); m.Tri(o0 + k, i0 + k, i0 + k1);
            m.Tri(o1 + k, o1 + k1, i1 + k1); m.Tri(o1 + k, i1 + k1, i1 + k);
        }
        // outward by the signed volume
        double vol = 0; var c0 = m.Positions[0];
        for (int t = 0; t < m.TriangleCount; t++)
        {
            var p0 = m.Positions[m.Indices[3 * t]] - c0; var p1 = m.Positions[m.Indices[3 * t + 1]] - c0; var p2 = m.Positions[m.Indices[3 * t + 2]] - c0;
            vol += Vec3.Dot(p0, Vec3.Cross(p1, p2));
        }
        if (vol < 0) for (int t = 0; t < m.TriangleCount; t++) (m.Indices[3 * t + 1], m.Indices[3 * t + 2]) = (m.Indices[3 * t + 2], m.Indices[3 * t + 1]);
        return m;
    }

    /// <summary>Point inside a closed mesh: parity of a vertical ray up (triangles above the point whose plan contains it).</summary>
    internal static bool Inside(Mesh m, Vec3 p)
    {
        var b = m.Bounds();
        if (p.X < b.Min.X || p.X > b.Max.X || p.Y < b.Min.Y || p.Y > b.Max.Y || p.Z < b.Min.Z || p.Z > b.Max.Z) return false;
        int hits = 0;
        // a tiny plan jitter keeps the ray off shared edges
        double px = p.X + 1.234e-7, py = p.Y + 2.345e-7;
        for (int t = 0; t < m.TriangleCount; t++)
        {
            var a = m.Positions[m.Indices[3 * t]]; var q = m.Positions[m.Indices[3 * t + 1]]; var c = m.Positions[m.Indices[3 * t + 2]];
            double d = (q.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (q.Y - a.Y);
            if (Math.Abs(d) < 1e-14) continue;
            double s1 = ((px - a.X) * (c.Y - a.Y) - (c.X - a.X) * (py - a.Y)) / d;
            double s2 = ((q.X - a.X) * (py - a.Y) - (px - a.X) * (q.Y - a.Y)) / d;
            if (s1 < 0 || s2 < 0 || s1 + s2 > 1) continue;
            if (a.Z + s1 * (q.Z - a.Z) + s2 * (c.Z - a.Z) > p.Z) hits++;
        }
        return hits % 2 == 1;
    }
}
