using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Utility;

/// <summary>A generated structure: plan point, rim (given or from the finished ground), sump, the walls + floor solid (null = not built).</summary>
public sealed class UtilityStructure
{
    public required UtilityStructureDefinition Definition { get; init; }
    public Vec2 Plan { get; init; }
    public double Rim { get; set; } = double.NaN;
    /// <summary>"plans" when the rim was given, else the finished ground layer it came from ("road", "grading", "existing ground").</summary>
    public string RimSource { get; set; } = "";
    public double Sump => Definition.SumpElevation;
    /// <summary>Plan direction of a box structure's length (radians from +X).</summary>
    public double Rotation { get; set; }
    public Mesh? Mesh { get; set; }
    public bool IsBuilt => Mesh is { IsEmpty: false };
    /// <summary>Volume of the walls + floor (and cone) less the wall openings (m3); frames / covers not included.</summary>
    public double Volume => Mesh == null ? 0 : Math.Abs(Mesh.Volume()) + Parts.Where(p => p.Kind == "cone").Sum(p => p.Volume) - Openings.Sum(o => o.Deduction);
    /// <summary>Plan outline of the structure's outside (for plan drawings); empty when not built.</summary>
    public List<Vec2> Outline { get; } = new();
    /// <summary>Cone, frame, cover / grate solids (OPTIONAL inputs <see cref="UtilityStructureDefinition.Cone"/> / <see cref="UtilityStructureDefinition.Frame"/>).</summary>
    public List<UtilityPart> Parts { get; } = new();
    /// <summary>Openings in the walls for the pipes / duct banks (IFC voids; the viewport keeps the wall solid, the pipe fills the opening).</summary>
    public List<UtilityOpening> Openings { get; } = new();

    /// <summary>Distance from the centre to the inside wall face along a plan direction (0 when the inside size is not known); <paramref name="grow"/> = the wall
    /// thickness gives the outside face. Envelope items (valves, hydrants): their given (outside) size.</summary>
    public double InsideDistance(Vec2 dir, double grow = 0)
    {
        var d = Definition;
        if (d.Shape == UtilityStructureShape.Round) return double.IsNaN(d.InnerDiameter) ? 0 : d.InnerDiameter / 2 + grow;
        if (double.IsNaN(d.InnerLength) || double.IsNaN(d.InnerWidth)) return 0;
        double c = Math.Cos(Rotation), s = Math.Sin(Rotation);
        double ux = Math.Abs(dir.X * c + dir.Y * s), uy = Math.Abs(-dir.X * s + dir.Y * c);
        double a = ux > 1e-12 ? (d.InnerLength / 2 + grow) / ux : double.MaxValue, b = uy > 1e-12 ? (d.InnerWidth / 2 + grow) / uy : double.MaxValue;
        return Math.Min(a, b);
    }
}

/// <summary>A generated pipe: end points at the inverts (inside bottom), the pipe solid (null = not built), length, slope and the cover found.</summary>
public sealed class UtilityPipe
{
    public required UtilityPipeDefinition Definition { get; init; }
    public UtilityStructure? FromStructure { get; init; }
    public UtilityStructure? ToStructure { get; init; }
    /// <summary>Invert points at the pipe ends (inside bottom of the pipe).</summary>
    public Vec3 Start { get; set; }
    public Vec3 End { get; set; }
    public Mesh? Mesh { get; set; }
    public bool IsBuilt => Mesh is { IsEmpty: false };
    public double Volume => Mesh == null ? 0 : Math.Abs(Mesh.Volume());
    /// <summary>Plan (horizontal) length between the pipe ends, m.</summary>
    public double Length => Vec2.Distance(Start.XY, End.XY);
    /// <summary>Invert slope, + = falling from start to end (rise / run).</summary>
    public double Slope => Length > 1e-9 ? (Start.Z - End.Z) / Length : 0;
    /// <summary>Smallest cover found over the outside crown (finished ground - crown), m; null = no ground over the pipe.</summary>
    public double? MinCover { get; set; }
    public double MinCoverStation { get; set; }
    /// <summary>Outside height (rise + 2 walls) and span.</summary>
    public double OutsideRise { get; set; }
}

/// <summary>Result of <see cref="UtilityGenerator"/> for one network.</summary>
public sealed class UtilityModel
{
    public required UtilityNetworkDefinition Definition { get; init; }
    public List<UtilityStructure> Structures { get; } = new();
    public List<UtilityPipe> Pipes { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    /// <summary>Hangers of the bridge-hung pipes (filled by <see cref="UtilityGenerator.Finish"/>).</summary>
    public List<UtilityHanger> Hangers { get; } = new();
    public List<UtilityDuctBank> DuctBanks { get; } = new();
    /// <summary>Clashes / clearance conflicts (filled by <see cref="UtilityGenerator.Finish"/>; reported, nothing moved).</summary>
    public List<UtilityClash> Clashes { get; } = new();
    public bool IsBuilt => Structures.Any(s => s.IsBuilt) || Pipes.Any(p => p.IsBuilt) || DuctBanks.Count > 0;
}

/// <summary>
/// Utility network generator (pure). Structures: walls (ring between the inside and outside outline) from the sump to the rim + a floor slab below the
/// sump; rims not given are read from the finished ground. Pipes: straight between the inside wall faces of their structures (or free ends), invert
/// at each end, section normal to the axis; the structure walls are not cut for the pipes (they overlap the wall thickness). Checks reported, never
/// applied: invert slope against the flow direction, outlet invert above an inlet invert, invert below the sump, pipe wider than the structure, cover
/// over the crowns (against <see cref="UtilityNetworkDefinition.MinCover"/> when given), rim vs the finished ground.
/// </summary>
public static partial class UtilityGenerator
{
    private const int Sides = 32;

    /// <param name="ground">finished ground (roads, grading, existing ground): elevation and its source layer; null = none (rims not given are UNRESOLVED).</param>
    public static List<UtilityModel> All(Project.BridgeProject project, Func<Vec2, (double? z, string source)>? ground) =>
        project.Utilities.Select(n => Generate(n, project.Alignments, ground)).ToList();

    public static UtilityModel Generate(UtilityNetworkDefinition net, IReadOnlyList<Alignments.Alignment> alignments, Func<Vec2, (double? z, string source)>? ground)
    {
        var m = new UtilityModel { Definition = net };
        void Diag(DiagnosticSeverity s, string t) => m.Diagnostics.Add(new Diagnostic(s, $"Network {net.Name}: {t}"));
        void Warn(string t) => Diag(DiagnosticSeverity.Warning, t);
        void Info(string t) => Diag(DiagnosticSeverity.Info, t);
        foreach (var u in net.Unresolved) Warn(u);
        string Src(string s) => s.Length > 0 ? s : net.Source;

        // ---- structures
        var byName = new Dictionary<string, UtilityStructure>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in net.Structures)
        {
            if (d.Unresolved.Length > 0) Warn(d.Unresolved);
            Vec2? p = null;
            if (!double.IsNaN(d.X) && !double.IsNaN(d.Y)) p = new Vec2(d.X, d.Y);
            else if (d.AlignmentName.Length > 0)
            {
                var al = alignments.FirstOrDefault(a => string.Equals(a.Name, d.AlignmentName, StringComparison.OrdinalIgnoreCase));
                if (al == null) Warn($"structure {d.Name}: alignment '{d.AlignmentName}' not found - not built.");
                else if (double.IsNaN(d.Station) || double.IsNaN(d.Offset)) Warn(UnresolvedFlag.Text(Src(d.Source), $"structure {d.Name} location", "station or offset not given", "not built."));
                else p = al.PointAt(d.Station, d.Offset);
            }
            else Warn(UnresolvedFlag.Text(Src(d.Source), $"structure {d.Name} location", "no X / Y and no alignment station / offset", "not built."));
            if (p == null) continue;
            if (byName.ContainsKey(d.Name)) { Warn($"structure name {d.Name} is used twice - the second is not built."); continue; }
            var s = new UtilityStructure { Definition = d, Plan = p.Value };
            byName[d.Name] = s;
            m.Structures.Add(s);
        }
        // box rotation: given, else along the outlet pipe (first pipe leaving it, else the first pipe reaching it)
        foreach (var s in m.Structures.Where(s => s.Definition.Shape == UtilityStructureShape.Box))
        {
            if (s.Definition.RotationDegrees is double rd) { s.Rotation = rd * Math.PI / 180; continue; }
            var pd = net.Pipes.FirstOrDefault(x => Eq(x.From, s.Definition.Name)) ?? net.Pipes.FirstOrDefault(x => Eq(x.To, s.Definition.Name));
            var other = pd == null ? null : PlanEnd(pd, Eq(pd.From, s.Definition.Name) ? false : true, byName);
            if (other is { } o && Vec2.Distance(o, s.Plan) > 1e-6) s.Rotation = Math.Atan2(o.Y - s.Plan.Y, o.X - s.Plan.X);
            Warn(UnresolvedFlag.Text(Src(s.Definition.Source), $"structure {s.Definition.Name} plan orientation", "the box rotation is not given",
                pd != null ? $"length laid along pipe {pd.Name}." : "laid along project +X."));
        }
        foreach (var s in m.Structures)
        {
            var d = s.Definition;
            var fg = ground?.Invoke(s.Plan) ?? (null, "");
            if (!double.IsNaN(d.RimElevation))
            {
                s.Rim = d.RimElevation; s.RimSource = "plans";
                if (fg.z is double g && Math.Abs(g - s.Rim) > 0.0127)
                    Info($"CHECK structure {d.Name}: rim {s.Rim:0.###} m vs the finished ground {g:0.###} m ({fg.source}), difference {s.Rim - g:+0.###;-0.###} m (reported, not changed).");
            }
            else if (fg.z is double g)
            {
                s.Rim = g; s.RimSource = fg.source;
                Info($"structure {d.Name}: rim not given - taken from the finished ground ({fg.source}) {g:0.###} m.");
            }
            else { Warn(UnresolvedFlag.Text(Src(d.Source), $"structure {d.Name} rim", "not given and no finished ground covers the point", "structure not built.")); continue; }
            var missing = new List<string>();
            string size = d.IsEnvelope ? "outside" : "inside";
            if (double.IsNaN(d.SumpElevation)) missing.Add(d.IsEnvelope ? "bottom (sump) elevation" : "sump elevation");
            if (d.Shape == UtilityStructureShape.Round && !(d.InnerDiameter > 0)) missing.Add(size + " diameter");
            if (d.Shape == UtilityStructureShape.Box && !(d.InnerLength > 0 && d.InnerWidth > 0)) missing.Add(size + " length / width");
            if (!d.IsEnvelope && !(d.WallThickness > 0)) missing.Add("wall thickness");
            if (!d.IsEnvelope && !(d.FloorThickness > 0)) missing.Add("floor thickness");
            if (missing.Count > 0) { Warn(UnresolvedFlag.Text(Src(d.Source), $"structure {d.Name} {string.Join(", ", missing)}", "not given", "structure solid not built; its pipes end at the structure centre where the inside size is unknown.")); continue; }
            if (!(s.Rim > d.SumpElevation)) { Warn($"structure {d.Name}: rim {s.Rim:0.###} m is not above the sump {d.SumpElevation:0.###} m - not built."); continue; }
            if (d.IsEnvelope)
            {
                // in-line item (valve, hydrant): envelope of the given outside size from the bottom to the top, no inside
                var env = Outline(d, s.Rotation, 0);
                var em = new Mesh();
                Prism(em, env.Select(q => (s.Plan + q).WithZ(d.SumpElevation)).ToArray(), env.Select(q => (s.Plan + q).WithZ(s.Rim)).ToArray());
                s.Mesh = em;
                s.Outline.AddRange(env.Select(q => s.Plan + q));
                Info($"structure {d.Name}: {d.Kind} built as an envelope of its outside size (the item itself is by the supplier).");
                continue;
            }
            var outPipe = net.Pipes.FirstOrDefault(x => Eq(x.From, d.Name));
            Vec2? outDir = outPipe != null && PlanEnd(outPipe, false, byName) is { } oe && Vec2.Distance(oe, s.Plan) > 1e-6 ? (oe - s.Plan) / Vec2.Distance(oe, s.Plan) : null;
            double wallTop = TopParts(s, d, Src(d.Source), Warn, Info, outDir);
            if (!(wallTop > d.SumpElevation)) { Warn($"structure {d.Name}: the frame / cone leave no wall height above the sump - walls not built (reported)."); s.Parts.Clear(); continue; }
            var inner = Outline(d, s.Rotation, 0);
            var outer = Outline(d, s.Rotation, d.WallThickness);
            var mesh = new Mesh();
            Cup(mesh, outer.Select(q => s.Plan + q).ToList(), inner.Select(q => s.Plan + q).ToList(), d.SumpElevation - d.FloorThickness, d.SumpElevation, wallTop);
            s.Mesh = mesh;
            s.Outline.AddRange(outer.Select(q => s.Plan + q));
        }

        // ---- pipes
        foreach (var d in net.Pipes)
        {
            if (d.Unresolved.Length > 0) Warn(d.Unresolved);
            UtilityStructure? fs = null, ts = null;
            if (d.From.Length > 0 && !byName.TryGetValue(d.From, out fs)) { Warn($"pipe {d.Name}: start structure '{d.From}' not found - not built."); continue; }
            if (d.To.Length > 0 && !byName.TryGetValue(d.To, out ts)) { Warn($"pipe {d.Name}: end structure '{d.To}' not found - not built."); continue; }
            Vec2? a = fs?.Plan ?? (double.IsNaN(d.StartX) || double.IsNaN(d.StartY) ? null : new Vec2(d.StartX, d.StartY));
            Vec2? b = ts?.Plan ?? (double.IsNaN(d.EndX) || double.IsNaN(d.EndY) ? null : new Vec2(d.EndX, d.EndY));
            if (a == null || b == null) { Warn(UnresolvedFlag.Text(Src(d.Source), $"pipe {d.Name} end point", "no structure and no free end coordinates", "not built.")); continue; }
            var dir = b.Value - a.Value;
            if (dir.Length < 1e-6) { Warn($"pipe {d.Name}: both ends at the same plan point - not built."); continue; }
            dir = dir / dir.Length;
            var missing = new List<string>();
            double rise = d.Shape == UtilityPipeShape.Circular ? d.InnerDiameter : d.InnerRise, span = d.Shape == UtilityPipeShape.Circular ? d.InnerDiameter : d.InnerSpan;
            double invS = d.InvertStart, invE = d.InvertEnd;
            // runs given by depth (pressure mains, conduits): invert = finished ground - plan cover - outside rise + wall at an end without an invert
            if (!double.IsNaN(d.Cover) && rise > 0 && d.WallThickness > 0)
                foreach (var (isStart, pt) in new[] { (true, a.Value), (false, b.Value) })
                {
                    if (!double.IsNaN(isStart ? invS : invE)) continue;
                    if (ground?.Invoke(pt).z is not double g) { Warn(UnresolvedFlag.Text(Src(d.Source), $"pipe {d.Name} {(isStart ? "start" : "end")} invert", "given by cover, but no finished ground at the end", "not built.")); continue; }
                    double inv = g - d.Cover - rise - d.WallThickness;
                    if (isStart) invS = inv; else invE = inv;
                    Info($"pipe {d.Name}: {(isStart ? "start" : "end")} invert {inv:0.###} m from the plan cover {d.Cover:0.###} m below the finished ground {g:0.###} m.");
                }
            if (double.IsNaN(invS)) missing.Add("start invert");
            if (double.IsNaN(invE)) missing.Add("end invert");
            if (!(rise > 0 && span > 0)) missing.Add(d.Shape == UtilityPipeShape.Circular ? "inside diameter" : "inside rise / span");
            if (!(d.WallThickness > 0)) missing.Add("wall thickness");
            if (missing.Count > 0) { Warn(UnresolvedFlag.Text(Src(d.Source), $"pipe {d.Name} {string.Join(", ", missing)}", "not given", "pipe not built.")); continue; }
            // ends at the inside wall faces of the structures
            var pa = a.Value + dir * (fs?.InsideDistance(dir) ?? 0);
            var pb = b.Value - dir * (ts?.InsideDistance(-dir) ?? 0);
            if (Vec2.Dot(pb - pa, dir) <= 1e-6) { Warn($"pipe {d.Name}: the structures overlap in plan - no length left between their inside faces, not built."); continue; }
            var pipe = new UtilityPipe { Definition = d, FromStructure = fs, ToStructure = ts, Start = pa.WithZ(invS), End = pb.WithZ(invE), OutsideRise = rise + 2 * d.WallThickness };
            pipe.Mesh = PipeMesh(d, pipe.Start, pipe.End, rise, span);
            m.Pipes.Add(pipe);
            {
                // wall openings at the structures it enters: the outside section through the wall
                var ax = (pipe.End - pipe.Start); double al = ax.Length; var wv = ax.Normalized();
                var uu = Vec3.Cross(wv, new Vec3(0, 0, 1)).Normalized(); var vv = Vec3.Cross(uu, wv);
                double cosS = Math.Max(1e-6, pipe.Length / al), hr = rise / 2 + d.WallThickness, hs = span / 2 + d.WallThickness;
                bool box = d.Shape == UtilityPipeShape.Box;
                if (fs != null && Opening(fs, d.Name, dir, x => pipe.Start + vv * (rise / 2) + wv * x, uu, vv, hr, hs, box, cosS) is { } o1) fs.Openings.Add(o1);
                if (ts != null && Opening(ts, d.Name, -dir, x => pipe.End + vv * (rise / 2) - wv * x, uu, vv, hr, hs, box, cosS) is { } o2) ts.Openings.Add(o2);
            }
            bool gravity = net.System is UtilitySystem.Storm or UtilitySystem.Sanitary;
            if (!gravity) { }
            else if (pipe.Slope < 0) Info($"CHECK pipe {d.Name}: adverse slope {pipe.Slope * 100:0.###} % (the invert rises from {d.From} to {d.To}; reported, not changed).");
            else if (pipe.Slope == 0) Info($"CHECK pipe {d.Name}: flat (0 %) invert.");
            foreach (var (st, inv, end) in new[] { (fs, invS, "start"), (ts, invE, "end") })
            {
                if (st == null) continue;
                if (st.Definition.IsEnvelope) continue;
                if (!double.IsNaN(st.Sump) && inv < st.Sump - 1e-4) Info($"CHECK pipe {d.Name}: {end} invert {inv:0.###} m is below the sump of {st.Definition.Name} ({st.Sump:0.###} m).");
                double inside = st.Definition.Shape == UtilityStructureShape.Round ? st.Definition.InnerDiameter : Math.Min(st.Definition.InnerLength, st.Definition.InnerWidth);
                if (!double.IsNaN(inside) && span + 2 * d.WallThickness > inside + 1e-6)
                    Info($"CHECK pipe {d.Name}: outside width {span + 2 * d.WallThickness:0.###} m is larger than the inside of {st.Definition.Name} ({inside:0.###} m).");
                if (!double.IsNaN(st.Rim) && inv + pipe.OutsideRise > st.Rim) Info($"CHECK pipe {d.Name}: the pipe top at {st.Definition.Name} is above its rim.");
            }
            // cover over the outside crown, every <= 1 m along the pipe
            if (ground != null)
            {
                int n = Math.Max(2, (int)Math.Ceiling(pipe.Length / 1.0));
                double? best = null; double at = 0;
                for (int k = 0; k <= n; k++)
                {
                    double t = (double)k / n;
                    var q = pipe.Start + (pipe.End - pipe.Start) * t;
                    if (ground(q.XY).z is not double g) continue;
                    double c = g - (q.Z - d.WallThickness + pipe.OutsideRise);   // outside crown = invert - wall + outside rise
                    if (best == null || c < best) { best = c; at = t * pipe.Length; }
                }
                pipe.MinCover = best; pipe.MinCoverStation = at;
                if (best is double bc)
                {
                    if (!double.IsNaN(net.MinCover) && bc < net.MinCover)
                        Warn($"CONFLICT (cover) pipe {d.Name}: {bc:0.###} m over the crown at {at:0.##} m from its start is less than the required {net.MinCover:0.###} m (reported, not changed).");
                    else if (bc < 0) Warn($"CHECK pipe {d.Name}: the crown is {-bc:0.###} m ABOVE the finished ground at {at:0.##} m from its start.");
                }
                else Info($"pipe {d.Name}: no finished ground over the pipe - cover not checked.");
            }
            else Info($"pipe {d.Name}: no finished ground loaded - cover not checked.");
        }
        DuctBanks(m, byName, Warn, Info);
        // invert drops at structures: an outlet invert above the lowest inlet invert
        foreach (var s in m.Structures)
        {
            var ins = m.Pipes.Where(p => p.ToStructure == s).ToList();
            var outs = m.Pipes.Where(p => p.FromStructure == s).ToList();
            if (ins.Count == 0 || outs.Count == 0 || s.Definition.IsEnvelope || net.System is not (UtilitySystem.Storm or UtilitySystem.Sanitary)) continue;
            double lowIn = ins.Min(p => p.End.Z);
            foreach (var o in outs.Where(o => o.Start.Z > lowIn + 1e-4))
                Info($"CHECK structure {s.Definition.Name}: outlet {o.Definition.Name} invert {o.Start.Z:0.###} m is above the inlet invert {lowIn:0.###} m (reported, not changed).");
        }
        int built = m.Structures.Count(s => s.IsBuilt), pb2 = m.Pipes.Count(p => p.IsBuilt);
        Info($"{built} of {net.Structures.Count} structures and {pb2} of {net.Pipes.Count} pipes built, {m.Pipes.Sum(p => p.Length):0.##} m of pipe"
            + (m.DuctBanks.Count > 0 ? $", {m.DuctBanks.Count} duct banks ({m.DuctBanks.Sum(x => x.Length):0.##} m)" : "") + $", {m.Structures.Sum(x => x.Openings.Count)} wall openings.");
        return m;
    }

    private static bool Eq(string a, string b) => a.Length > 0 && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static Vec2? PlanEnd(UtilityPipeDefinition p, bool start, Dictionary<string, UtilityStructure> byName)
    {
        string n = start ? p.From : p.To;
        if (n.Length > 0) return byName.TryGetValue(n, out var s) ? s.Plan : null;
        double x = start ? p.StartX : p.EndX, y = start ? p.StartY : p.EndY;
        return double.IsNaN(x) || double.IsNaN(y) ? null : new Vec2(x, y);
    }

    /// <summary>Structure outline relative to its centre (counter-clockwise), grown by <paramref name="grow"/> (the wall thickness for the outside).</summary>
    private static List<Vec2> Outline(UtilityStructureDefinition d, double rot, double grow)
    {
        if (d.Shape == UtilityStructureShape.Round)
        {
            double r = d.InnerDiameter / 2 + grow;
            return Enumerable.Range(0, Sides).Select(i => new Vec2(r * Math.Cos(2 * Math.PI * i / Sides), r * Math.Sin(2 * Math.PI * i / Sides))).ToList();
        }
        double a = d.InnerLength / 2 + grow, b = d.InnerWidth / 2 + grow, c = Math.Cos(rot), s = Math.Sin(rot);
        return new[] { (-a, -b), (a, -b), (a, b), (-a, b) }.Select(q => new Vec2(q.Item1 * c - q.Item2 * s, q.Item1 * s + q.Item2 * c)).ToList();
    }

    /// <summary>
    /// A structure as ONE closed shell (an open-top box / cylinder): outside wall from the floor bottom <paramref name="zb"/> to the rim, inside wall from the
    /// sump to the rim, rim annulus, inside floor at the sump, bottom face. Outlines convex, counter-clockwise, equal vertex counts.
    /// </summary>
    private static void Cup(Mesh mesh, List<Vec2> outer, List<Vec2> inner, double zb, double zs, double zr)
    {
        int n = outer.Count, t0 = mesh.TriangleCount;
        int ob = mesh.Positions.Count; foreach (var q in outer) mesh.Add(q.WithZ(zb));
        int ot = mesh.Positions.Count; foreach (var q in outer) mesh.Add(q.WithZ(zr));
        int it = mesh.Positions.Count; foreach (var q in inner) mesh.Add(q.WithZ(zr));
        int ib = mesh.Positions.Count; foreach (var q in inner) mesh.Add(q.WithZ(zs));
        var co = new Vec2(outer.Average(q => q.X), outer.Average(q => q.Y));
        int cb = mesh.Add(co.WithZ(zb)), cs = mesh.Add(co.WithZ(zs));
        for (int k = 0; k < n; k++)
        {
            int k1 = (k + 1) % n;
            mesh.Tri(ob + k, ob + k1, ot + k1); mesh.Tri(ob + k, ot + k1, ot + k);      // outside wall
            mesh.Tri(ot + k, ot + k1, it + k1); mesh.Tri(ot + k, it + k1, it + k);      // rim
            mesh.Tri(it + k, it + k1, ib + k1); mesh.Tri(it + k, ib + k1, ib + k);      // inside wall
            mesh.Tri(ib + k, ib + k1, cs);                                              // inside floor
            mesh.Tri(ob + k1, ob + k, cb);                                              // bottom
        }
        Orient(mesh, t0);
    }

    /// <summary>Flips the triangles from <paramref name="triStart"/> when their signed volume is negative (shell inside out).</summary>
    private static void Orient(Mesh mesh, int triStart)
    {
        double v = 0;
        var c = mesh.Positions[mesh.Indices[3 * triStart]];
        for (int t = triStart; t < mesh.TriangleCount; t++)
        {
            var a = mesh.Positions[mesh.Indices[3 * t]] - c; var b = mesh.Positions[mesh.Indices[3 * t + 1]] - c; var e = mesh.Positions[mesh.Indices[3 * t + 2]] - c;
            v += Vec3.Dot(a, Vec3.Cross(b, e));
        }
        if (v < 0)
            for (int t = triStart; t < mesh.TriangleCount; t++) (mesh.Indices[3 * t + 1], mesh.Indices[3 * t + 2]) = (mesh.Indices[3 * t + 2], mesh.Indices[3 * t + 1]);
    }

    /// <summary>
    /// Closed ring solid between two cross sections (outer / inner loops of equal length at the start and the end): outside wall, inside wall, two
    /// end annuli. Faces are oriented outward from the signed volume.
    /// </summary>
    private static void Ring(Mesh mesh, Vec3[] o0, Vec3[] i0, Vec3[] o1, Vec3[] i1)
    {
        int n = o0.Length, triStart = mesh.TriangleCount;
        int bo0 = mesh.Positions.Count; foreach (var p in o0) mesh.Add(p);
        int bi0 = mesh.Positions.Count; foreach (var p in i0) mesh.Add(p);
        int bo1 = mesh.Positions.Count; foreach (var p in o1) mesh.Add(p);
        int bi1 = mesh.Positions.Count; foreach (var p in i1) mesh.Add(p);
        for (int k = 0; k < n; k++)
        {
            int k1 = (k + 1) % n;
            // outside wall (start -> end), inside wall reversed, start annulus, end annulus
            mesh.Tri(bo0 + k, bo0 + k1, bo1 + k1); mesh.Tri(bo0 + k, bo1 + k1, bo1 + k);
            mesh.Tri(bi0 + k, bi1 + k1, bi0 + k1); mesh.Tri(bi0 + k, bi1 + k, bi1 + k1);
            mesh.Tri(bo0 + k, bi0 + k1, bo0 + k1); mesh.Tri(bo0 + k, bi0 + k, bi0 + k1);
            mesh.Tri(bo1 + k, bo1 + k1, bi1 + k1); mesh.Tri(bo1 + k, bi1 + k1, bi1 + k);
        }
        Orient(mesh, triStart);   // the loops' winding decides
    }

    /// <summary>Straight pipe between two invert points: section normal to the axis (circle / ellipse 32 sides, box 4), inside bottom on the inverts.</summary>
    private static Mesh PipeMesh(UtilityPipeDefinition d, Vec3 inv0, Vec3 inv1, double rise, double span)
    {
        var axis = inv1 - inv0;
        var dn = axis.Normalized();
        var u = Vec3.Cross(dn, new Vec3(0, 0, 1)); u = u.Normalized();   // horizontal, to the right of the flow
        var v = Vec3.Cross(u, dn);                                      // up, normal to the axis
        double t = d.WallThickness;
        // centre so that the lowest inside point of the end section lies on the invert
        Vec3 Centre(Vec3 inv) => inv + v * (rise / 2);
        Vec3[] Loop(Vec3 centre, double hr, double hs)
        {
            if (d.Shape == UtilityPipeShape.Box)
                return new[] { (-hs, -hr), (hs, -hr), (hs, hr), (-hs, hr) }.Select(q => centre + u * q.Item1 + v * q.Item2).ToArray();
            return Enumerable.Range(0, Sides).Select(i => { double a = 2 * Math.PI * i / Sides; return centre + u * (hs * Math.Cos(a)) + v * (hr * Math.Sin(a)); }).ToArray();
        }
        var m = new Mesh();
        Vec3 s0 = Centre(inv0), s1 = Centre(inv1);
        Ring(m, Loop(s0, rise / 2 + t, span / 2 + t), Loop(s0, rise / 2, span / 2), Loop(s1, rise / 2 + t, span / 2 + t), Loop(s1, rise / 2, span / 2));
        return m;
    }
}
