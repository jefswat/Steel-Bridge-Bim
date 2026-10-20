using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;

namespace SBM.Core.Railing;

/// <summary>One built railing run (quantities by length along its reference line).</summary>
public sealed class RailingRun
{
    public required string Name { get; init; }
    public required string Assembly { get; init; }
    public string TypeName { get; init; } = "";
    public RailingKind Kind { get; init; }
    /// <summary>Reference line offset from the alignment (right +).</summary>
    public double Offset { get; init; }
    public double BeginStation { get; init; }
    public double EndStation { get; init; }
    /// <summary>Length along the reference line between the run ends (m).</summary>
    public double Length { get; init; }
    public double? PlanLength { get; init; }
    public int Posts { get; init; }
    /// <summary>Fence fabric panels (one per bay) and their total area (m2, one face).</summary>
    public int FabricPanels { get; init; }
    public double FabricArea { get; init; }
    /// <summary>Mass of the fabric panels (kg), included in <see cref="Mass"/>.</summary>
    public double FabricMass { get; init; }
    /// <summary>Alignment stations of the posts (m, station values), begin to end.</summary>
    public List<double> PostStations { get; init; } = new();
    /// <summary>Mass of the run's parts (kg; envelope by its mass per length).</summary>
    public double Mass { get; init; }
    /// <summary>Reference-line points (plan, base elevation) from begin to end (sampled).</summary>
    public List<Vec3> Line { get; init; } = new();
}

/// <summary>Result of <see cref="PedestrianRailingGenerator"/>: steel parts / anchor bolts / assemblies (merged into the bridge's steel model) and the runs.</summary>
public sealed class RailingModel
{
    public List<RailingRun> Runs { get; } = new();
    public List<SteelAssembly> Assemblies { get; } = new();
    public List<SteelPart> Parts { get; } = new();
    public List<BoltInstance> Bolts { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    public double TotalLength => Runs.Sum(r => r.Length);
}

/// <summary>
/// Pedestrian railing generator (pure function framing + concrete -> <see cref="RailingModel"/>): for every <see cref="PedestrianRailingDefinition"/> of the
/// bridge, the run limits on the reference line, the base elevation from the bridge's concrete (highest concrete surface under the probe point: deck, curb,
/// barrier, arch curb), then the swept envelope / continuous members, posts with base plates and anchor bolts. Nothing is moved to fit: a run without
/// concrete under it is reported and not built.
/// </summary>
public static class PedestrianRailingGenerator
{
    private static readonly Vec3 Up = new(0, 0, 1);

    public static RailingModel Generate(BridgeFraming f, Concrete.ConcreteModel? concrete, UnitSystem u)
    {
        var res = new RailingModel();
        var defs = f.Definition.Railings;
        if (defs is not { Count: > 0 }) return res;
        var top = TopSurface.From(concrete);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int k = 0;
        foreach (var d in defs)
        {
            k++;
            string name = d.Name.Length > 0 ? d.Name : $"Railing {k}";
            string asm = "RAIL-" + new string(name.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '-').ToArray()).Trim('-');
            while (!used.Add(asm)) asm += "-" + k;
            string where = $"railing {name}";
            foreach (var t in d.Unresolved) res.Diagnostics.Add(new(DiagnosticSeverity.Warning, $"{where}: {t}"));
            try { BuildRun(f, d, name, asm, where, top, u, res); }
            catch (Exception e) { res.Diagnostics.Add(new(DiagnosticSeverity.Warning, $"{where}: not built - {e.Message}")); }
        }
        return res;
    }

    private static void BuildRun(BridgeFraming f, PedestrianRailingDefinition d, string name, string asm, string where, TopSurface top, UnitSystem u, RailingModel res)
    {
        var a = f.Alignment;
        double side = d.Offset < 0 ? -1 : 1;
        Vec2 P(double dist) => a.PointAtDistance(dist, d.Offset);
        Vec2 Tan(double dist) => Vec2.FromAngle(a.AngleAt(a.DistanceToStation(dist)));
        Vec2 Out(double dist) => Tan(dist).Right * side;

        // ---- run limits (alignment distances)
        double d0 = Limit(f, d, true, P), d1 = Limit(f, d, false, P);
        if (!(d1 > d0)) { res.Diagnostics.Add(new(DiagnosticSeverity.Warning, $"{where}: the run limits do not enclose a length; not built.")); return; }
        // arc length table along the reference line
        var ds = new List<double>(); var ls = new List<double>();
        int nFine = Math.Max(8, (int)Math.Ceiling((d1 - d0) / 0.05));
        double acc = 0; var prev = P(d0);
        for (int i = 0; i <= nFine; i++)
        {
            double di = d0 + (d1 - d0) * i / nFine;
            var p = P(di);
            if (i > 0) acc += (p - prev).Length;
            prev = p; ds.Add(di); ls.Add(acc);
        }
        double DAt(double s)
        {
            if (s <= 0) return ds[0];
            if (s >= ls[^1]) return ds[^1];
            int lo = 0, hi = ls.Count - 1;
            while (hi - lo > 1) { int m = (lo + hi) / 2; if (ls[m] <= s) lo = m; else hi = m; }
            double t = (s - ls[lo]) / Math.Max(1e-12, ls[hi] - ls[lo]);
            return ds[lo] + (ds[hi] - ds[lo]) * t;
        }
        double SAt(double dist)
        {
            if (dist <= ds[0]) return 0;
            if (dist >= ds[^1]) return ls[^1];
            int lo = 0, hi = ds.Count - 1;
            while (hi - lo > 1) { int m = (lo + hi) / 2; if (ds[m] <= dist) lo = m; else hi = m; }
            double t = (dist - ds[lo]) / Math.Max(1e-12, ds[hi] - ds[lo]);
            return ls[lo] + (ls[hi] - ls[lo]) * t;
        }
        double s0 = d.EndClearance, s1 = ls[^1] - d.EndClearance, len = s1 - s0;
        if (len <= 1e-3) { res.Diagnostics.Add(new(DiagnosticSeverity.Warning, $"{where}: run shorter than the end clearances; not built.")); return; }

        // base elevation: top of concrete under the probe point (searched a little toward the run interior at the ends, e.g. skewed deck ends)
        double? BaseRaw(double s)
        {
            foreach (double step in new[] { 0.0, 0.02, 0.05, 0.1, 0.2, 0.3 })
            {
                double ss = Math.Clamp(s + (s < (s0 + s1) / 2 ? step : -step), 0, ls[^1]);
                double dd = DAt(ss);
                if (top.At(P(dd) + Out(dd) * d.BaseProbe) is double z) return z;
            }
            return null;
        }
        // Short dips of the base (a curb notched around steel, a break in the concrete narrower than BaseBridgeGap) are bridged: grey-scale closing of the
        // sampled base over the window (the dip is filled up to the lower rim). Each bridged dip is reported.
        double bridge = d.BaseBridgeGap > 0 ? d.BaseBridgeGap : d.BaseBridgeGap < 0 ? 0 : (d.Post?.Spacing ?? 0) / 3;
        double[]? bsz = null; const double bstep = 0.05;
        if (bridge >= 2 * bstep)
        {
            int nb = (int)Math.Ceiling(ls[^1] / bstep) + 1;
            var raw = new double?[nb];
            for (int i = 0; i < nb; i++) raw[i] = BaseRaw(Math.Min(ls[^1], i * bstep));
            int w = (int)Math.Ceiling(bridge / bstep);
            bsz = new double[nb];
            var rep = new List<string>();
            double runLow = 0; int runStart = -1;
            for (int i = 0; i < nb; i++)
            {
                if (raw[i] is not double z) { bsz[i] = double.NaN; continue; }
                double mL = double.NegativeInfinity, mR = double.NegativeInfinity;
                for (int j = Math.Max(0, i - w); j <= i; j++) if (raw[j] is double zj) mL = Math.Max(mL, zj);
                for (int j = i; j <= Math.Min(nb - 1, i + w); j++) if (raw[j] is double zj) mR = Math.Max(mR, zj);
                double zb = Math.Max(z, Math.Min(mL, mR));
                bsz[i] = zb;
                if (zb - z > 0.0064) { if (runStart < 0) { runStart = i; runLow = z; } }
                else if (runStart >= 0)
                {
                    rep.Add($"{u.FormatStation(a.DistanceToStation(DAt(runStart * bstep)))} to {u.FormatStation(a.DistanceToStation(DAt(i * bstep)))} ({u.FormatLength((i - runStart) * bstep)} long, {u.FormatLength(bsz[runStart] - runLow, LengthKind.Short)} deep)");
                    runStart = -1;
                }
            }
            if (rep.Count > 0)
                res.Diagnostics.Add(new(DiagnosticSeverity.Warning,
                    $"{where}: the concrete under the reference line dips for less than {u.FormatLength(bridge)} at {rep.Count} place(s) ({string.Join("; ", rep.Take(6))}{(rep.Count > 6 ? "; ..." : "")}) - base bridged at the lower rim (UNRESOLVED: curb / slab notched around steel in the model, the plans show the curb continuous)."));
        }
        double? Base(double s)
        {
            if (bsz == null) return BaseRaw(s);
            double x = Math.Clamp(s, 0, ls[^1]) / bstep; int i0 = Math.Min(bsz.Length - 1, (int)Math.Floor(x)), i1 = Math.Min(bsz.Length - 1, i0 + 1);
            double a0 = bsz[i0], a1 = bsz[i1];
            if (double.IsNaN(a0) || double.IsNaN(a1)) return BaseRaw(s);
            return a0 + (a1 - a0) * (x - i0);
        }
        // ring stations (length along the line)
        int nr = Math.Max(1, (int)Math.Ceiling(len / Math.Max(0.05, d.SampleStep)));
        var st = Enumerable.Range(0, nr + 1).Select(i => s0 + len * i / nr).ToList();
        var frames = new List<(Vec2 p, Vec2 o, Vec2 t, double z)>();
        foreach (double s in st)
        {
            double dd = DAt(s);
            var z = Base(s);
            if (z == null)
            {
                res.Diagnostics.Add(new(DiagnosticSeverity.Warning,
                    $"{where}: no concrete under the reference line at station {u.FormatStation(a.DistanceToStation(dd))} (offset {u.FormatLength(d.Offset)}, probe {u.FormatLength(d.BaseProbe, LengthKind.Short)}); not built."));
                return;
            }
            frames.Add((P(dd), Out(dd), Tan(dd), z.Value));
        }
        Vec3 W((Vec2 p, Vec2 o, Vec2 t, double z) fr, double r, double h) => (fr.p + fr.o * r).WithZ(fr.z + h);
        (Vec2 p, Vec2 o, Vec2 t, double z) FrameAt(double s)
        {
            double dd = DAt(s);
            return (P(dd), Out(dd), Tan(dd), Base(s) ?? frames[0].z);
        }

        double mass = 0;
        var asmDesc = $"{(d.TypeName.Length > 0 ? d.TypeName : "Pedestrian railing")} {name}, {u.FormatLength(len)} along the reference line {u.FormatLength(Math.Abs(d.Offset))} {(side < 0 ? "left" : "right")} of the alignment";
        res.Assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.Railing, Description = asmDesc, PredefinedType = d.Kind.ToString().ToUpperInvariant(), ObjectType = d.TypeName.Length > 0 ? d.TypeName : null });
        // dimOnly: the plans show the part (posts, handrail, fence), only a size / type is open -> stays on its normal layer (Assumption.DimensionOnly)
        Assumption? Flag(string? why, string? basis, bool dimOnly = false) => why == null ? null : dimOnly ? Assumption.UnresolvedDimension(why, basis ?? "as modeled") : Assumption.Unresolved(why, basis ?? "as modeled");

        // ---- envelope (one swept solid)
        if (d.Envelope is { Count: >= 3 } env)
        {
            var sec = env.Select(q => new Vec2(q.R, q.H)).ToList();
            var rings = frames.Select(fr => env.Select(q => W(fr, q.R, q.H)).ToArray()).ToList();
            var m = new Mesh();
            MeshBuilder.AddLoft(m, sec, rings);
            var part = new SteelPart
            {
                Mark = $"{asm}-ENV", Category = PartCategory.RailEnvelope, Assembly = asm, Mesh = m, Length = len, Material = d.Material, Density = d.Density,
                Description = $"{(d.TypeName.Length > 0 ? d.TypeName : "railing")} envelope (placeholder section)",
                NominalWeightLbPerFt = d.EnvelopeMassPerLength > 0 ? d.EnvelopeMassPerLength / 1.48816394 : 0,
                Assumption = Flag(d.EnvelopeUnresolved, d.EnvelopeBasis),
            };
            res.Parts.Add(part); mass += part.Mass;
        }

        // ---- continuous members
        int mk = 0;
        foreach (var mem in d.Members)
        {
            mk++;
            List<Vec2> sec;
            if (mem.Diameter > 0)
                sec = Enumerable.Range(0, 16).Select(i => new Vec2(mem.Center.R + mem.Diameter / 2 * Math.Cos(2 * Math.PI * i / 16), mem.Center.H + mem.Diameter / 2 * Math.Sin(2 * Math.PI * i / 16))).ToList();
            else if (mem.Width > 0 && mem.Height > 0)
                sec = new() { new(mem.Center.R - mem.Width / 2, mem.Center.H - mem.Height / 2), new(mem.Center.R + mem.Width / 2, mem.Center.H - mem.Height / 2),
                              new(mem.Center.R + mem.Width / 2, mem.Center.H + mem.Height / 2), new(mem.Center.R - mem.Width / 2, mem.Center.H + mem.Height / 2) };
            else { res.Diagnostics.Add(new(DiagnosticSeverity.Warning, $"{where}: member {mem.Name} has no size; not built.")); continue; }
            double ms0 = s0 + mem.EndSetback, ms1 = s1 - mem.EndSetback;
            if (ms1 - ms0 < 1e-3) continue;
            var mst = st.Where(s => s > ms0 + 1e-6 && s < ms1 - 1e-6).Prepend(ms0).Append(ms1).ToList();
            var rings = mst.Select(s => { var fr = FrameAt(s); return sec.Select(q => W(fr, q.X, q.Y)).ToArray(); }).ToList();
            var m = new Mesh();
            MeshBuilder.AddLoft(m, sec, rings);
            var part = new SteelPart
            {
                Mark = $"{asm}-{(mem.Name.Length > 0 ? mem.Name : (mem.Kind == RailMemberKind.Handrail ? "HR" : "R") + mk)}",
                Category = mem.Kind == RailMemberKind.Handrail ? PartCategory.RailHandrail : PartCategory.RailMember, Assembly = asm, Mesh = m,
                Length = ms1 - ms0, Material = d.Material, Density = d.Density, Description = mem.Description.Length > 0 ? mem.Description : mem.Kind.ToString(),
                // explicit mass per length (0 = carried by the envelope: a vanishing nominal weight keeps Mass at zero)
                NominalWeightLbPerFt = mem.MassPerLength is double mpl ? Math.Max(mpl, 1e-12) / 1.48816394 : 0,
                Assumption = Flag(mem.Unresolved, mem.Basis, d.FlagsDimensionOnly),
            };
            res.Parts.Add(part); mass += part.Mass;
        }

        // ---- posts, base plates, anchor bolts
        int posts = 0;
        var postS = new List<double>();
        if (d.Post is { } pd && pd.Profile.Count >= 3 && pd.Thickness > 0)
        {
            if (pd.Spacing <= 0) res.Diagnostics.Add(new(DiagnosticSeverity.Warning, $"{where}: post spacing not given; posts not built."));
            else
            {
                double inner = len - 2 * pd.EndDistance;
                var ps = new List<double>();
                if (pd.GridStation is double g0)
                {
                    // layout along the alignment stations (plan elevations): radial lines at g0 + k x Spacing inside the run (ends included, 1 mm)
                    double dg = a.StationToDistance(g0), dLo = DAt(s0), dHi = DAt(s1);
                    int k0 = (int)Math.Ceiling((dLo - dg - 1e-3) / pd.Spacing), k1 = (int)Math.Floor((dHi - dg + 1e-3) / pd.Spacing);
                    for (int k = k0; k <= k1; k++) ps.Add(SAt(dg + k * pd.Spacing));
                }
                else if (inner < -1e-9) ps.Add(s0 + len / 2);
                else
                {
                    int n = Math.Max(2, (int)Math.Ceiling(inner / pd.Spacing - 1e-9) + 1);
                    if (inner < 1e-6) n = 1;
                    for (int i = 0; i < n; i++) ps.Add(s0 + pd.EndDistance + (n == 1 ? 0 : inner * i / (n - 1)));
                }
                postS = ps;
                var sec = pd.Profile.Select(q => new Vec2(q.R, q.H)).ToList();
                var flag = Flag(pd.Unresolved, pd.Basis, d.FlagsDimensionOnly);
                foreach (double s in ps)
                {
                    posts++;
                    var fr = FrameAt(s);
                    var t3 = new Vec3(fr.t.X, fr.t.Y, 0);
                    var ring = pd.Profile.Select(q => W(fr, q.R, q.H)).ToArray();
                    var m = new Mesh();
                    MeshBuilder.AddLoft(m, sec, new[] { ring.Select(v => v - t3 * (pd.Thickness / 2)).ToArray(), ring.Select(v => v + t3 * (pd.Thickness / 2)).ToArray() });
                    var post = new SteelPart
                    {
                        Mark = $"{asm}-P{posts}", Category = PartCategory.RailPost, Assembly = asm, Mesh = m, Length = sec.Max(q => q.Y) - sec.Min(q => q.Y),
                        Material = d.Material, Density = d.Density, Description = pd.Description.Length > 0 ? pd.Description : "post", Assumption = flag,
                    };
                    res.Parts.Add(post); mass += post.Mass;
                    if (pd.BasePlate is { Length: > 0, Width: > 0, Thickness: > 0 } bp)
                    {
                        var rect = new List<Vec2> { new(bp.CenterR - bp.Width / 2, 0), new(bp.CenterR + bp.Width / 2, 0), new(bp.CenterR + bp.Width / 2, bp.Thickness), new(bp.CenterR - bp.Width / 2, bp.Thickness) };
                        var pr = rect.Select(q => W(fr, q.X, q.Y)).ToArray();
                        var pm = new Mesh();
                        MeshBuilder.AddLoft(pm, rect, new[] { pr.Select(v => v - t3 * (bp.Length / 2)).ToArray(), pr.Select(v => v + t3 * (bp.Length / 2)).ToArray() });
                        var plate = new SteelPart
                        {
                            Mark = $"{asm}-BP{posts}", Category = PartCategory.RailBasePlate, Assembly = asm, Mesh = pm, Length = bp.Length, Material = d.Material, Density = d.Density,
                            Description = $"PL {u.FormatLength(bp.Thickness, LengthKind.Short)} x {u.FormatLength(bp.Width, LengthKind.Short)} x {u.FormatLength(bp.Length, LengthKind.Short)}", Assumption = flag,
                        };
                        res.Parts.Add(plate); mass += plate.Mass;
                        if (bp.AnchorDiameter > 0)
                            foreach (var an in bp.Anchors)
                            {
                                var o3 = (fr.p + fr.o * (bp.CenterR + an.H)).WithZ(fr.z + bp.Thickness) + t3 * an.R;
                                var y = Vec3.Cross(Up, t3).Normalized();
                                res.Bolts.Add(new BoltInstance
                                {
                                    Frame = new Frame3(o3, t3, y, Up), Grip = bp.Thickness, Length = bp.Thickness + bp.AnchorEmbedment, Assembly = asm,
                                    Kind = BoltKind.Anchor, Diameter = bp.AnchorDiameter, Embedment = bp.AnchorEmbedment,
                                });
                            }
                    }
                }
            }
        }

        // ---- fence fabric: one thin panel per bay between the posts (the end bays run to the run ends)
        int panels = 0; double fabArea = 0, fabMass = 0;
        if (d.Fabric is { } fab && fab.Path.Count >= 2 && fab.Thickness > 0)
        {
            var secPoly = FabricSection(fab);
            if (secPoly.Count < 4) res.Diagnostics.Add(new(DiagnosticSeverity.Warning, $"{where}: fence fabric path / height range gives no panel; not built."));
            else
            {
                double half = (d.Post?.Thickness ?? 0) / 2 + fab.PostClearance;
                var edges = new List<(double s, bool post)>();
                foreach (double pS in postS.OrderBy(x => x)) edges.Add((pS, true));
                if (edges.Count == 0 || edges[0].s > s0 + 1e-3) edges.Insert(0, (s0, false));
                if (edges[^1].s < s1 - 1e-3) edges.Add((s1, false));
                var fflag = Flag(fab.Unresolved, fab.Basis, d.FlagsDimensionOnly);
                double dens = fab.MassPerArea / fab.Thickness;
                for (int i = 0; i + 1 < edges.Count; i++)
                {
                    double b0 = edges[i].s + (edges[i].post ? half : 0), b1 = edges[i + 1].s - (edges[i + 1].post ? half : 0);
                    if (b1 - b0 < 0.02) continue;
                    var mst = st.Where(x => x > b0 + 1e-6 && x < b1 - 1e-6).Prepend(b0).Append(b1).ToList();
                    var rings = mst.Select(x => { var fr = FrameAt(x); return secPoly.Select(q => W(fr, q.X, q.Y)).ToArray(); }).ToList();
                    var fm = new Mesh();
                    MeshBuilder.AddLoft(fm, secPoly, rings);
                    panels++;
                    var part = new SteelPart
                    {
                        Mark = $"{asm}-FAB{panels}", Category = PartCategory.RailFabric, Assembly = asm, Mesh = fm, Length = b1 - b0,
                        Material = fab.Material.Length > 0 ? fab.Material : d.Material, Density = dens,
                        Description = fab.Description.Length > 0 ? fab.Description : "fence fabric panel", Assumption = fflag, MassNotCounted = fab.MassNotCounted,
                    };
                    res.Parts.Add(part); mass += part.Mass; fabMass += part.Mass;
                    fabArea += part.Volume / fab.Thickness;
                }
            }
        }

        var line = frames.Select(fr => fr.p.WithZ(fr.z)).ToList();
        res.Runs.Add(new RailingRun
        {
            Name = name, Assembly = asm, TypeName = d.TypeName, Kind = d.Kind, Offset = d.Offset, Length = len, PlanLength = d.PlanLength, Posts = posts, Mass = mass, Line = line, FabricPanels = panels, FabricArea = fabArea, FabricMass = fabMass,
            PostStations = postS.Select(x => a.DistanceToStation(DAt(x))).ToList(),
            BeginStation = a.DistanceToStation(DAt(s0)), EndStation = a.DistanceToStation(DAt(s1)),
        });
        if (d.PlanLength is double pl)
        {
            double diff = len - pl;
            bool ok = Math.Abs(diff) <= 0.003048;
            res.Diagnostics.Add(new(ok ? DiagnosticSeverity.Info : DiagnosticSeverity.Warning,
                $"{where}: length along the reference line {u.FormatLength(len)} vs plan {u.FormatLength(pl)} ({(diff >= 0 ? "+" : "")}{u.FormatLength(diff, LengthKind.Short)})."));
        }
    }

    /// <summary>Closed section polygon (R, H as X, Y) of the fabric panel: the clipped path offset by +- thickness / 2 about the offset line.</summary>
    private static List<Vec2> FabricSection(RailFabricDefinition f)
    {
        double lo = f.BottomH, hi = f.TopH ?? double.MaxValue;
        var pts = new List<Vec2>();
        var p = f.Path.Select(q => new Vec2(q.R, q.H)).ToList();
        for (int i = 0; i + 1 < p.Count; i++)
        {
            Vec2 a = p[i], b = p[i + 1];
            if (b.Y <= a.Y + 1e-12) continue;                                   // the path rises
            double t0 = Math.Clamp((lo - a.Y) / (b.Y - a.Y), 0, 1), t1 = Math.Clamp((hi - a.Y) / (b.Y - a.Y), 0, 1);
            if (t1 - t0 < 1e-9) continue;
            var q0 = a + (b - a) * t0; var q1 = a + (b - a) * t1;
            if (pts.Count == 0 || (pts[^1] - q0).Length > 1e-9) pts.Add(q0);
            pts.Add(q1);
        }
        if (pts.Count < 2) return new();
        var left = new List<Vec2>(); var right = new List<Vec2>();
        for (int i = 0; i < pts.Count; i++)
        {
            Vec2 dir = i == 0 ? pts[1] - pts[0] : i == pts.Count - 1 ? pts[i] - pts[i - 1] : (pts[i + 1] - pts[i]).Normalized() + (pts[i] - pts[i - 1]).Normalized();
            dir = dir.Normalized();
            var n = new Vec2(dir.Y, -dir.X);                                    // right-hand side of the upward path = outward
            left.Add(pts[i] + n * (f.OffsetFromPost - f.Thickness / 2));
            right.Add(pts[i] + n * (f.OffsetFromPost + f.Thickness / 2));
        }
        right.Reverse();
        left.AddRange(right);
        return left;
    }

    /// <summary>Alignment distance of a run end: radial line at the given station, else the deck end line, else the support line moved by the limit offset.</summary>
    private static double Limit(BridgeFraming f, PedestrianRailingDefinition d, bool begin, Func<double, Vec2> P)
    {
        var a = f.Alignment;
        double? sta = begin ? d.BeginStation : d.EndStation;
        if (sta is double s) return a.StationToDistance(s);
        Func<double, double> g;                    // signed distance of the line point from the limit, increasing with the distance along the alignment
        double guess;
        var sl = begin ? f.Supports[0] : f.Supports[^1];
        guess = a.StationToDistance(sl.Station);
        if (f.Deck is { } dr && f.Definition.Girders.Count > 0)
        {
            if (begin) g = x => dr.FromBegin(P(x));
            else g = x => -dr.FromEnd(P(x));
        }
        else
        {
            var n = sl.Direction.Left;                   // toward ahead stations
            double off = begin ? d.BeginLimitOffset : d.EndLimitOffset;
            var p0 = sl.AlignmentPoint + n * (begin ? -off : off);
            g = x => Vec2.Dot(P(x) - p0, n);
        }
        double lo = guess - 20, hi = guess + 20;
        for (int i = 0; i < 20 && g(lo) > 0; i++) lo -= 20;
        for (int i = 0; i < 20 && g(hi) < 0; i++) hi += 20;
        lo = Math.Max(lo, 0); hi = Math.Min(hi, a.Length);
        // a limit at the very start / end of the alignment (deck end = alignment begin): accept it within 0.1 mm
        if (g(lo) > 0 && g(lo) < 1e-4) return lo;
        if (g(hi) < 0 && g(hi) > -1e-4) return hi;
        if (g(lo) > 0 || g(hi) < 0) throw new InvalidOperationException($"the {(begin ? "begin" : "end")} limit line does not cross the reference line on the alignment");
        for (int i = 0; i < 80; i++)
        {
            double m = (lo + hi) / 2;
            if (g(m) < 0) lo = m; else hi = m;
        }
        return (lo + hi) / 2;
    }
}

/// <summary>Highest top surface of a set of meshes at a plan point (non-vertical triangles bucketed in a plan grid).</summary>
public sealed class TopSurface
{
    private const double Cell = 0.5;
    private readonly List<(Vec3 a, Vec3 b, Vec3 c)> _tris = new();
    private readonly Dictionary<(long, long), List<int>> _grid = new();

    public static TopSurface From(Concrete.ConcreteModel? c)
    {
        var meshes = new List<Mesh>();
        if (c != null)
        {
            if (c.Deck != null) meshes.Add(c.Deck);
            if (c.Haunches != null) meshes.Add(c.Haunches);
            meshes.AddRange(c.Barriers.Select(b => b.Mesh));
            meshes.AddRange(c.DeckBumps.Select(b => b.Mesh));
        }
        return new TopSurface(meshes);
    }

    public TopSurface(IEnumerable<Mesh> meshes)
    {
        foreach (var m in meshes)
            for (int i = 0; i + 2 < m.Indices.Count; i += 3)
            {
                var a = m.Positions[m.Indices[i]]; var b = m.Positions[m.Indices[i + 1]]; var c = m.Positions[m.Indices[i + 2]];
                var n = Vec3.Cross(b - a, c - a);
                if (Math.Abs(n.Z) < 1e-9 * Math.Max(1e-12, n.Length) || Math.Abs(n.Z) < 1e-14) continue;
                int id = _tris.Count;
                _tris.Add((a, b, c));
                long x0 = (long)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X)) / Cell), x1 = (long)Math.Floor(Math.Max(a.X, Math.Max(b.X, c.X)) / Cell);
                long y0 = (long)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y)) / Cell), y1 = (long)Math.Floor(Math.Max(a.Y, Math.Max(b.Y, c.Y)) / Cell);
                if ((x1 - x0 + 1) * (y1 - y0 + 1) > 400000) continue;
                for (long x = x0; x <= x1; x++)
                    for (long y = y0; y <= y1; y++)
                    {
                        if (!_grid.TryGetValue((x, y), out var l)) _grid[(x, y)] = l = new List<int>();
                        l.Add(id);
                    }
            }
    }

    /// <summary>Highest surface elevation at the plan point, null when no triangle covers it.</summary>
    public double? At(Vec2 p)
    {
        if (!_grid.TryGetValue(((long)Math.Floor(p.X / Cell), (long)Math.Floor(p.Y / Cell)), out var l)) return null;
        double? best = null;
        foreach (int id in l)
        {
            var (a, b, c) = _tris[id];
            double det = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(det) < 1e-14) continue;
            double l1 = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / det;
            double l2 = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / det;
            double l3 = 1 - l1 - l2;
            const double eps = -1e-9;
            if (l1 < eps || l2 < eps || l3 < eps) continue;
            double z = l1 * a.Z + l2 * b.Z + l3 * c.Z;
            if (best == null || z > best) best = z;
        }
        return best;
    }
}
