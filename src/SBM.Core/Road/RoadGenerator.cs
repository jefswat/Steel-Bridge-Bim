using SBM.Core.Alignments;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Road;

/// <summary>A generated road solid: one course of one component (or a swept curb / section).</summary>
public sealed record RoadPart(string Name, string Component, RoadComponentKind Kind, string Course, string Material, Mesh Mesh, string Side)
{
    public double Volume => Math.Abs(Mesh.Volume());
    /// <summary>Set for a subgrade layer (<see cref="RoadSide.Subgrade"/>; <see cref="Component"/> = <see cref="RoadGenerator.SubgradeComponent"/>,
    /// <see cref="Course"/> = the layer name); null for pavement courses and sections.</summary>
    public RoadSubgradeKind? Subgrade { get; init; }
    /// <summary>Closed pieces of <see cref="Mesh"/> when it has more than one (a subgrade layer split at a course-depth step); null = one piece.</summary>
    public List<Mesh>? Pieces { get; init; }
}

/// <summary>A level line of the road section per side ("finished grade" = the component tops, "top of subgrade" = bottom of the pavement courses, then "bottom of &lt;layer&gt;" for every
/// subgrade layer): per section station the line across the side as (signed offset from the alignment, elevation), inner to outer.</summary>
public sealed record RoadLevel(string Side, string Name, RoadSubgradeKind? Kind, List<(double Station, List<(double Offset, double Z)> Line)> Sections);

/// <summary>Result of <see cref="RoadGenerator"/>.</summary>
public sealed class RoadModel
{
    public required RoadDefinition Definition { get; init; }
    public List<RoadPart> Parts { get; } = new();
    /// <summary>Top surface area per component name (m2, plan: the sloped area of the top).</summary>
    public Dictionary<string, double> TopArea { get; } = new();
    /// <summary>Side slope surfaces (not solids: earthwork volumes are stage 2) per side, and the daylight lines on the ground.</summary>
    public Dictionary<string, Mesh> SideSlopes { get; } = new();
    public Dictionary<string, List<Vec3>> Daylight { get; } = new();
    /// <summary>Top edge lines of the template (the side's first inner edge and every component's outer edge), for plan drawings.</summary>
    public List<Vec3[]> EdgeLines { get; } = new();
    /// <summary>Index in <see cref="EdgeLines"/> of the outermost top edge of the left / right side (-1 = none).</summary>
    public int OuterLeft { get; set; } = -1;
    public int OuterRight { get; set; } = -1;
    /// <summary>Clearances to the bridges (<see cref="RoadClearanceCheck"/>; filled by the caller that has the bridge models).</summary>
    public List<RoadClearanceResult> Clearances { get; } = new();
    /// <summary>Earthwork between the design section (subgrade under the components + side slopes) and the ground, by average end areas along the
    /// alignment (no curvature correction): cut and fill in m3; null without terrain. Stations whose section does not daylight on both sides are left out.</summary>
    public double? CutVolume { get; set; }
    public double? FillVolume { get; set; }
    /// <summary>Per section station: cut and fill area (m2); only sections with both side slopes daylighted.</summary>
    public List<(double Station, double Cut, double Fill)> EarthworkSections { get; } = new();
    /// <summary>Level lines below the finished surface per side: "top of subgrade" (bottom of the pavement courses / sections) and the bottom of every
    /// subgrade layer (<see cref="RoadLevel"/>).</summary>
    public List<RoadLevel> Levels { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
}

/// <summary>
/// Road corridor generator (pure): the template is evaluated at the sample stations (<see cref="RoadDefinition.SampleSpacing"/> + every kink station
/// of the profile / cross-slope table inside the range). Top of each component: the alignment's finished surface (profile + cross-slope table at that
/// offset) or, with its own <see cref="RoadComponent.Slope"/>, straight from its inner edge. Courses hang below the top at their thicknesses with
/// vertical sides at the component edges. Section components are swept closed solids. Side slopes from the outer top edge to the ground at the
/// given cut / fill ratios (no ground = UNRESOLVED, not built). Nothing is assumed: a component without width or a course without thickness is
/// reported and skipped.
/// </summary>
public static partial class RoadGenerator
{
    /// <summary>Component name of the subgrade layer parts of a side (one IfcRoadPart per side holds them).</summary>
    public const string SubgradeComponent = "Subgrade";

    /// <summary>Every road of the project (<see cref="Project.BridgeProject.Roads"/>) on the given ground (null = no terrain: side slopes UNRESOLVED);
    /// a road whose alignment is not in the project gets an error diagnostic and no parts.</summary>
    public static List<RoadModel> All(Project.BridgeProject project, Func<Vec2, double?>? ground, IReadOnlyList<Project.BridgeModel?>? bridges = null)
    {
        var list = new List<RoadModel>();
        foreach (var r in project.Roads)
        {
            var al = project.Alignments.FirstOrDefault(a => string.Equals(a.Name, r.AlignmentName, StringComparison.OrdinalIgnoreCase));
            if (al == null)
            {
                var rm = new RoadModel { Definition = r };
                rm.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, $"Road {r.Name}: alignment '{r.AlignmentName}' not found - not built."));
                list.Add(rm);
                continue;
            }
            var tieDiag = new List<Diagnostic>();
            Func<double, double>? beginAt = null, endAt = null;
            if (r.BeginAtBridge is { } bt) beginAt = bridges == null ? null : RoadBridgeTies.StationAt(r, bt, al, bridges, tieDiag);
            if (r.EndAtBridge is { } et) endAt = bridges == null ? null : RoadBridgeTies.StationAt(r, et, al, bridges, tieDiag);
            if (bridges == null && (r.BeginAtBridge != null || r.EndAtBridge != null))
                tieDiag.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Road {r.Name}: bridge ties need the generated bridges - the road end stations are used."));
            var model = Generate(r, al, ground, beginAt, endAt);
            model.Diagnostics.InsertRange(0, tieDiag);
            list.Add(model);
        }
        return list;
    }

    /// <param name="beginAt">OPTIONAL station of the first section as a function of the signed offset (a road end that follows a skewed / kinked bridge
    /// or approach slab end, <see cref="RoadDefinition.BeginAtBridge"/>); null = the radial line at the begin station. <paramref name="endAt"/> likewise.</param>
    public static RoadModel Generate(RoadDefinition d, Alignment a, Func<Vec2, double?>? ground = null, Func<double, double>? beginAt = null, Func<double, double>? endAt = null)
    {
        var m = new RoadModel { Definition = d };
        void Warn(string s) => m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Road {d.Name}: {s}"));
        foreach (var u in d.Unresolved) Warn(u);
        if (d.FeatureLines is { Count: > 0 }) return FromFeatureLines(d, a, m, Warn);
        double s0 = Math.Min(d.BeginStation, d.EndStation), s1 = Math.Max(d.BeginStation, d.EndStation);
        if (beginAt != null) s0 = Math.Min(beginAt(0), s1);
        if (endAt != null) s1 = Math.Max(endAt(0), s0);
        if (!(s1 - s0 > 1e-3)) { Warn("begin and end station are equal - not built."); return m; }
        // sample stations: uniform at <= SampleSpacing + every kink station inside the range
        double sp = d.SampleSpacing > 0.05 ? d.SampleSpacing : 2.0;
        int n = Math.Max(1, (int)Math.Ceiling((s1 - s0) / sp));
        var st = Enumerable.Range(0, n + 1).Select(i => s0 + (s1 - s0) * i / n).ToList();
        st.AddRange(Concrete.ConcreteGenerator.KinkStations(a).Where(k => k > s0 + 1e-6 && k < s1 - 1e-6));
        st = st.Distinct().OrderBy(x => x).ToList();
        var clean = new List<double>();
        foreach (double x in st) if (clean.Count == 0 || x - clean[^1] > 1e-4) clean.Add(x);
        st = clean;
        int last = st.Count - 1;
        // station of section i at a signed offset: the end sections follow the tied bridge end; interior sections stay radial but never cross a tied end
        double S(int i, double off)
        {
            if (i == 0 && beginAt != null) return beginAt(off);
            if (i == last && endAt != null) return endAt(off);
            double s = st[i];
            if (beginAt != null) s = Math.Max(s, beginAt(off) + 1e-3 * i);
            if (endAt != null) s = Math.Min(s, endAt(off) - 1e-3 * (last - i));
            return s;
        }

        // design section per side and station, from the alignment outward: (offset from the alignment, outward +; z) = subgrade, then the daylight point
        var design = new Dictionary<int, List<(double o, double z)>[]>();
        var daylit = new Dictionary<int, bool[]>();
        // bottoms of PLACED subgrade layers per side and station (earthwork is measured to them)
        var placed = new Dictionary<int, List<List<(double o, double z)>>[]>();
        foreach (var (side, sign, label) in new[] { (d.Left, -1, "left"), (d.Right, 1, "right") })
        {
            var des = design[sign] = st.Select(_ => new List<(double o, double z)>()).ToArray();
            var lit = daylit[sign] = new bool[st.Count];
            var plc = placed[sign] = st.Select(_ => new List<List<(double o, double z)>>()).ToArray();
            // bottom of the pavement structure per built component: offsets and elevations at the inner / outer edge per station
            var bots = new List<(string name, double o0, double o1, double[] z0, double[] z1)>();
            // finished surface per station across the side (component tops at their edges; a section = its inner and outer top)
            var fin = st.Select(_ => new List<(double o, double z)>()).ToArray();
            double inner = side.StartOffset;
            Vec3 At(int i, double off, double z) => a.PointAt(S(i, sign * off), sign * off).WithZ(z);
            // top of the outer edge of the previous component as a function of the station (a component with its own slope starts there)
            Func<double, double> edgeAt = s => a.ElevationAt(s, sign * side.StartOffset);
            if (side.Components.Count > 0 && inner > 1e-6) { double o = inner; var e0 = edgeAt; m.EdgeLines.Add(st.Select((_, i) => At(i, o, e0(S(i, sign * o)))).ToArray()); }
            foreach (var c in side.Components)
            {
                foreach (var u in new[] { c.Unresolved }.Where(x => x.Length > 0)) Warn(u);
                double w = c.Section is { Count: >= 3 } sec ? sec.Max(q => q.U) : c.Width;
                if (!(w > 1e-6)) { Warn($"{label} component '{c.Name}' has no width - not built."); continue; }
                double o0 = inner, o1 = inner + w;
                var edge = edgeAt;
                double TopS(double s, double off) => c.Slope is double sl ? edge(s) + sl * (off - o0) : a.ElevationAt(s, sign * off);
                double Top(int i, double off) => TopS(S(i, sign * off), off);
                {
                    // subgrade: below the courses, or the lowest point of a swept section
                    double drop = c.Section is { Count: >= 3 } ss0 ? -ss0.Min(q => q.V) : c.Courses.Where(co => co.Thickness > 1e-6).Sum(co => co.Thickness);
                    var bz0 = new double[st.Count]; var bz1 = new double[st.Count];
                    for (int i = 0; i < st.Count; i++)
                    {
                        double z0 = c.Section is { Count: >= 3 } ? edge(S(i, sign * o0)) - drop : Top(i, o0) - drop, z1 = c.Section is { Count: >= 3 } ? edge(S(i, sign * o1)) - drop : Top(i, o1) - drop;
                        des[i].Add((o0, z0)); des[i].Add((o1, z1));
                        bz0[i] = z0; bz1[i] = z1;
                    }
                    bots.Add((c.Name, o0, o1, bz0, bz1));
                }
                if (c.Section is { Count: >= 3 } secPts)
                {
                    // swept closed section (curb and gutter): U outward, V up from the inner edge top
                    var sec2 = secPts.Select(q => new Vec2(q.U, q.V)).ToList();
                    var rings = st.Select((_, i) => secPts.Select(q => { double off = o0 + q.U, s = S(i, sign * off); return a.PointAt(s, sign * off).WithZ(edge(s) + q.V); }).ToArray()).ToList();
                    var mesh = new Mesh();
                    MeshBuilder.AddLoft(mesh, sec2, rings, false);
                    m.Parts.Add(new RoadPart($"{d.Name} {label} {c.Name}", c.Name, c.Kind, "", c.Courses.FirstOrDefault()?.Material ?? "Concrete", mesh, label));
                }
                else
                {
                    if (c.Courses.Count == 0) Warn($"{label} component '{c.Name}' has no courses - top surface only (area), no solid.");
                    double depth = 0;
                    foreach (var co in c.Courses)
                    {
                        if (!(co.Thickness > 1e-6)) { Warn($"{label} {c.Name} course '{co.Name}' has no thickness - not built."); continue; }
                        double t0 = depth, t1 = depth + co.Thickness;
                        // section corners (offset outward, drop below the top): inner top, outer top, outer bottom, inner bottom
                        var sec2 = new List<Vec2> { new(o0, -t0), new(o1, -t0), new(o1, -t1), new(o0, -t1) };
                        var rings = st.Select((_, i) => new[]
                        {
                            At(i, o0, Top(i, o0) - t0), At(i, o1, Top(i, o1) - t0), At(i, o1, Top(i, o1) - t1), At(i, o0, Top(i, o0) - t1),
                        }).ToList();
                        var mesh = new Mesh();
                        MeshBuilder.AddLoft(mesh, sec2, rings, false);
                        m.Parts.Add(new RoadPart($"{d.Name} {label} {c.Name} {co.Name}", c.Name, c.Kind, co.Name, co.Material, mesh, label));
                        depth = t1;
                    }
                }
                for (int i = 0; i < st.Count; i++)
                    if (c.Section is { Count: >= 3 } fs)
                    {
                        fin[i].Add((o0, edge(S(i, sign * o0)) + fs.Where(q => q.U <= fs.Min(r => r.U) + 1e-9).Max(q => q.V))); fin[i].Add((o1, edge(S(i, sign * o1)) + fs[^1].V));
                    }
                    else { fin[i].Add((o0, Top(i, o0))); fin[i].Add((o1, Top(i, o1))); }
                // top area (sloped) of the component
                double area = 0;
                for (int i = 0; i + 1 < st.Count; i++)
                {
                    Vec3 P(int k, double off) => At(k, off, Top(k, off));
                    var p00 = P(i, o0); var p01 = P(i, o1); var p10 = P(i + 1, o0); var p11 = P(i + 1, o1);
                    area += 0.5 * (Vec3.Cross(p01 - p00, p10 - p00).Length + Vec3.Cross(p10 - p11, p01 - p11).Length);
                }
                m.TopArea[$"{label} {c.Name}"] = m.TopArea.GetValueOrDefault($"{label} {c.Name}") + area;
                edgeAt = c.Section is { Count: >= 3 } secE ? (s => edge(s) + secE.Last().V) : (s => TopS(s, o1));
                var eNext = edgeAt;
                m.EdgeLines.Add(st.Select((_, i) => At(i, o1, eNext(S(i, sign * o1)))).ToArray());
                if (sign < 0) m.OuterLeft = m.EdgeLines.Count - 1; else m.OuterRight = m.EdgeLines.Count - 1;
                inner = o1;
            }
            if (fin[0].Count > 0) m.Levels.Add(new RoadLevel(label, "finished grade", null, st.Select((s, i) => (s, fin[i].Select(p => (sign * p.o, p.z)).ToList())).ToList()));
            Subgrade(m, d, side, sign, label, st, bots, At, plc, Warn);
            // side slope to the ground
            if (side.SideSlope is { } ss)
            {
                if (ground == null) { Warn(UnresolvedFlag.Text(d.Source, $"{label} side slope", "no terrain is loaded to daylight it on", "side slope not built.")); continue; }
                if (!(ss.CutRatio > 0 && ss.FillRatio > 0 && ss.MaxWidth > 0)) { Warn($"{label} side slope: cut / fill ratio and the maximum width must be given - not built."); continue; }
                var top = new List<Vec3>(); var day = new List<Vec3>();
                int missing = 0;
                var outerEdge = edgeAt;
                for (int i = 0; i < st.Count; i++)
                {
                    double se = S(i, sign * inner);
                    var e = a.PointAt(se, sign * inner).WithZ(outerEdge(se));
                    double? g0 = ground(new Vec2(e.X, e.Y));
                    if (g0 == null) { missing++; continue; }
                    bool cut = g0.Value > e.Z;
                    double ratio = cut ? ss.CutRatio : ss.FillRatio;
                    double F(double h) { var p = a.PointAt(se, sign * (inner + h)); double? g = ground(p); return g == null ? double.NaN : (e.Z + (cut ? h : -h) / ratio) - g.Value; }
                    // bisection on the first sign change out to MaxWidth
                    double lo = 0, flo = F(0), hi = double.NaN;
                    int steps = Math.Max(4, (int)Math.Ceiling(ss.MaxWidth / 0.25));
                    for (int k = 1; k <= steps; k++)
                    {
                        double h = ss.MaxWidth * k / steps, fh = F(h);
                        if (double.IsNaN(fh)) break;
                        if (Math.Sign(fh) != Math.Sign(flo)) { hi = h; break; }
                        lo = h; flo = fh;
                    }
                    if (double.IsNaN(hi)) { missing++; continue; }
                    for (int it = 0; it < 50; it++) { double mid = (lo + hi) / 2, fm = F(mid); if (Math.Sign(fm) == Math.Sign(flo)) { lo = mid; flo = fm; } else hi = mid; }
                    double hd = (lo + hi) / 2;
                    var dp = a.PointAt(se, sign * (inner + hd));
                    top.Add(e); day.Add(dp.WithZ(e.Z + (cut ? hd : -hd) / ratio));
                    des[i].Add((inner, e.Z)); des[i].Add((inner + hd, e.Z + (cut ? hd : -hd) / ratio));
                    lit[i] = true;
                }
                if (missing > 0) Warn($"{label} side slope: no daylight within {ss.MaxWidth:0.###} m (or no ground) at {missing} of {st.Count} stations - not built there.");
                if (top.Count >= 2)
                {
                    var ms = new Mesh();
                    for (int i = 0; i + 1 < top.Count; i++)
                    {
                        // ribbon between the edge line and the daylight line (surface, outward-up normals)
                        int a0 = ms.Add(top[i]), a1 = ms.Add(top[i + 1]), b0 = ms.Add(day[i]), b1 = ms.Add(day[i + 1]);
                        if (sign > 0) { ms.Tri(a0, b0, b1); ms.Tri(a0, b1, a1); }
                        else { ms.Tri(a0, b1, b0); ms.Tri(a0, a1, b1); }
                    }
                    m.SideSlopes[label] = ms;
                    m.Daylight[label] = day;
                }
            }
        }
        if (ground != null && d.Left.SideSlope != null && d.Right.SideSlope != null) Earthwork(m, a, st, design, daylit, placed, ground);
        m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Road {d.Name}: {st.Count} sections, {m.Parts.Count} solids, {m.Parts.Sum(p => p.Volume):0.##} m3, top area {m.TopArea.Values.Sum():0.##} m2."));
        return m;
    }

    /// <summary>
    /// Subgrade layers of one side (<see cref="RoadSide.Subgrade"/>, top down): layer k lies under the bottom of the pavement structure of its components
    /// (the "top of subgrade" line, stepped where course depths differ) lowered by the thicknesses of the layers above it; it continues
    /// <see cref="RoadSubgradeLayer.ExtendBeyond"/> past the last component at the slope of that component's bottom and ends in a vertical or
    /// <see cref="RoadSubgradeLayer.EdgeRatio"/> face. Also records the level lines (<see cref="RoadModel.Levels"/>) and the bottoms of placed layers.
    /// </summary>
    private static void Subgrade(RoadModel m, RoadDefinition d, RoadSide side, int sign, string label, List<double> st,
        List<(string name, double o0, double o1, double[] z0, double[] z1)> bots, Func<int, double, double, Vec3> At,
        List<List<(double o, double z)>>[] placed, Action<string> warn)
    {
        if (bots.Count == 0) return;
        List<(double, double)> Signed(IEnumerable<(double o, double z)> pts) => pts.Select(p => (sign * p.o, p.z)).ToList();
        // top of subgrade = the bottom of every component of the side
        m.Levels.Add(new RoadLevel(label, "top of subgrade", null, st.Select((s, i) => (s, Signed(bots.SelectMany(b => new[] { (b.o0, b.z0[i]), (b.o1, b.z1[i]) })))).ToList()));
        if (side.Subgrade is not { Count: > 0 } layers) return;
        double above = 0;
        foreach (var L in layers)
        {
            if (L.Unresolved.Length > 0) warn(L.Unresolved);
            string what = $"{label} subgrade layer '{L.Name}'";
            if (!(L.Thickness > 1e-6)) { warn($"{what} has no thickness - not built."); continue; }
            if (L.ExtendBeyond < 0 || L.EdgeRatio < 0) { warn($"{what}: ExtendBeyond and EdgeRatio must not be negative - not built."); continue; }
            List<int> idx;
            if (L.Under.Count == 0) idx = Enumerable.Range(0, bots.Count).ToList();
            else
            {
                var missing = L.Under.Where(n => !bots.Any(b => string.Equals(b.name, n, StringComparison.OrdinalIgnoreCase))).ToList();
                if (missing.Count > 0) { warn($"{what}: component(s) {string.Join(", ", missing)} not found among the built components of the {label} side - not built."); continue; }
                idx = L.Under.Select(n => bots.FindIndex(b => string.Equals(b.name, n, StringComparison.OrdinalIgnoreCase))).Distinct().OrderBy(x => x).ToList();
                if (idx[^1] - idx[0] != idx.Count - 1) { warn($"{what}: the components it lies under must be adjacent - not built."); continue; }
            }
            double t = L.Thickness, top = above;
            // pieces: where the bottom of the courses steps by the layer thickness or more between two components (at any section), the band would
            // pinch to a point there - the layer is built as separate closed pieces either side of that edge
            var groups = new List<List<int>> { new() { idx[0] } };
            var splits = new List<string>();
            for (int j = 1; j < idx.Count; j++)
            {
                var pa = bots[idx[j - 1]]; var pb = bots[idx[j]];
                bool step = Enumerable.Range(0, st.Count).Any(i => Math.Abs(pa.z1[i] - pb.z0[i]) >= t - 1e-9);
                if (step) { groups.Add(new List<int>()); splits.Add($"{pa.name} / {pb.name}"); }
                groups[^1].Add(idx[j]);
            }
            // section line per station: top points inner to outer (bottoms lowered by the layers above), then the extension on the last piece
            List<(double o, double z)> TopLine(int i, List<int> g, bool last)
            {
                var pts = new List<(double o, double z)>();
                foreach (int k in g) { var b = bots[k]; pts.Add((b.o0, b.z0[i] - top)); pts.Add((b.o1, b.z1[i] - top)); }
                var lb = bots[g[^1]];
                double slope = (lb.z1[i] - lb.z0[i]) / Math.Max(lb.o1 - lb.o0, 1e-9);
                if (last && L.ExtendBeyond > 1e-6) pts.Add((lb.o1 + L.ExtendBeyond, lb.z1[i] - top + slope * L.ExtendBeyond));
                // drop repeated points (adjacent components with the same bottom)
                var clean = new List<(double o, double z)>();
                foreach (var p in pts) if (clean.Count == 0 || Math.Abs(p.o - clean[^1].o) > 1e-9 || Math.Abs(p.z - clean[^1].z) > 1e-9) clean.Add(p);
                return clean;
            }
            List<(double o, double z)> BottomLine(List<(double o, double z)> tl, bool last)
            {
                var bl = tl.Select(p => (p.o, p.z - t)).ToList();
                if (last && L.EdgeRatio > 1e-9)
                {
                    // the bottom continues the last top segment's slope out to the edge face
                    var (oa, za) = tl[^2]; var (ob, zb) = tl[^1];
                    double slope = Math.Abs(ob - oa) > 1e-9 ? (zb - za) / (ob - oa) : 0, dx = L.EdgeRatio * t;
                    bl[^1] = (ob + dx, zb - t + slope * dx);
                }
                return bl;
            }
            var mesh = new Mesh();
            var botsL = st.Select(_ => new List<(double o, double z)>()).ToList();
            var pieces = new List<Mesh>();
            bool ok = true;
            for (int gi = 0; gi < groups.Count && ok; gi++)
            {
                bool last = gi == groups.Count - 1;
                var tops = st.Select((_, i) => TopLine(i, groups[gi], last)).ToList();
                if (tops.Any(x => x.Count != tops[0].Count) || tops[0].Count < 2) { ok = false; break; }
                var bl = tops.Select(x => BottomLine(x, last)).ToList();
                for (int i = 0; i < st.Count; i++) botsL[i].AddRange(bl[i]);
                // closed polygon: top inner -> outer, bottom outer -> inner
                List<(double o, double z)> Poly(int i) => tops[i].Concat(Enumerable.Reverse(bl[i])).ToList();
                var sec2 = Poly(0).Select(p => new Vec2(p.o, p.z)).ToList();
                var rings = st.Select((_, i) => Poly(i).Select(p => At(i, p.o, p.z)).ToArray()).ToList();
                MeshBuilder.AddLoft(mesh, sec2, rings, false);
                var piece = new Mesh();
                MeshBuilder.AddLoft(piece, sec2, rings, false);
                pieces.Add(piece);
            }
            if (!ok) { warn($"{what}: the section changes its point count along the road - not built."); continue; }
            if (splits.Count > 0)
                m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Road {d.Name}: {what} is built in {groups.Count} pieces - the bottom of the courses steps by its thickness or more at {string.Join(", ", splits)}."));
            m.Parts.Add(new RoadPart($"{d.Name} {label} {L.Name}", SubgradeComponent, RoadComponentKind.Other, L.Name, L.Material, mesh, label) { Subgrade = L.Kind, Pieces = pieces.Count > 1 ? pieces : null });
            m.Levels.Add(new RoadLevel(label, "bottom of " + L.Name, L.Kind, st.Select((s, i) => (s, Signed(botsL[i]))).ToList()));
            if (L.Kind == RoadSubgradeKind.Subbase) for (int i = 0; i < st.Count; i++) placed[i].Add(botsL[i]);
            above += t;
        }
    }

    /// <summary>Lower envelope of section lines (offset non-decreasing, vertical steps allowed): the design line for earthwork when placed subgrade layers
    /// lie below the bottom of the courses / side slopes. Exact for piecewise linear lines (breaks at every vertex and crossing).</summary>
    internal static List<(double o, double z)> LowerEnvelope(List<List<(double o, double z)>> lines)
    {
        // the segment of a line over (xa, xb) as its values at xa and xb; null = not covered
        static (double a, double b)? Seg(List<(double o, double z)> l, double xa, double xb)
        {
            double xm = (xa + xb) / 2;
            for (int k = 0; k + 1 < l.Count; k++)
            {
                var (x0, z0) = l[k]; var (x1, z1) = l[k + 1];
                if (x1 - x0 < 1e-12 || xm < x0 || xm > x1) continue;
                double sl = (z1 - z0) / (x1 - x0);
                return (z0 + sl * (xa - x0), z0 + sl * (xb - x0));
            }
            return null;
        }
        var xs = lines.SelectMany(l => l.Select(p => p.o)).Distinct().OrderBy(x => x).ToList();
        var outp = new List<(double o, double z)>();
        void Add(double x, double z) { if (outp.Count == 0 || Math.Abs(outp[^1].o - x) > 1e-12 || Math.Abs(outp[^1].z - z) > 1e-12) outp.Add((x, z)); }
        for (int k = 0; k + 1 < xs.Count; k++)
        {
            double xa = xs[k], xb = xs[k + 1];
            if (xb - xa < 1e-12) continue;
            var segs = lines.Select(l => Seg(l, xa, xb)).OfType<(double a, double b)>().ToList();
            if (segs.Count == 0) continue;
            // break points where two segments cross inside the interval
            var ts = new List<double> { 0, 1 };
            for (int p = 0; p < segs.Count; p++)
                for (int q = p + 1; q < segs.Count; q++)
                {
                    double d0 = segs[p].a - segs[q].a, d1 = segs[p].b - segs[q].b;
                    if (d0 * d1 < 0) ts.Add(d0 / (d0 - d1));
                }
            foreach (double tt in ts.Distinct().OrderBy(x => x))
                Add(xa + (xb - xa) * tt, segs.Min(sg => sg.a + (sg.b - sg.a) * tt));
        }
        return outp;
    }

    /// <summary>Cut / fill areas between the design line and the ground across each section (sampled every 0.1 m, exact at the vertices), average end areas.</summary>
    private static void Earthwork(RoadModel m, Alignment a, List<double> st, Dictionary<int, List<(double o, double z)>[]> design,
        Dictionary<int, bool[]> daylit, Dictionary<int, List<List<(double o, double z)>>[]> placed, Func<Vec2, double?> ground)
    {
        double cutV = 0, fillV = 0;
        (double s, double c, double f)? prev = null;
        int skipped = 0;
        for (int i = 0; i < st.Count; i++)
        {
            if (!daylit[-1][i] || !daylit[1][i]) { skipped++; prev = null; continue; }
            // signed offsets left to right
            List<(double o, double z)> SideLine(int sg) => placed[sg][i].Count == 0 ? design[sg][i] : LowerEnvelope(placed[sg][i].Prepend(design[sg][i]).ToList());
            var line = SideLine(-1).Select(p => (x: -p.o, p.z)).Reverse().Concat(SideLine(1).Select(p => (x: p.o, p.z))).ToList();
            double cut = 0, fill = 0;
            for (int k = 0; k + 1 < line.Count; k++)
            {
                var (x0, z0) = line[k]; var (x1, z1) = line[k + 1];
                if (x1 - x0 < 1e-9) continue;   // vertical steps (curb faces, side slope start) carry no area
                int n = Math.Max(1, (int)Math.Ceiling((x1 - x0) / 0.1));
                double? gPrev = null; double xPrev = x0, dPrev = z0;
                for (int j = 0; j <= n; j++)
                {
                    double x = x0 + (x1 - x0) * j / n, dz = z0 + (z1 - z0) * j / n;
                    double? g = ground(a.PointAt(st[i], x));
                    if (g == null) { gPrev = null; continue; }
                    if (gPrev != null)
                    {
                        double h0 = gPrev.Value - dPrev, h1 = g.Value - dz, w = x - xPrev;   // + = ground above the design line = cut
                        if (h0 >= 0 && h1 >= 0) cut += w * (h0 + h1) / 2;
                        else if (h0 <= 0 && h1 <= 0) fill -= w * (h0 + h1) / 2;
                        else { double t = h0 / (h0 - h1); double a0 = w * t * h0 / 2, a1 = w * (1 - t) * h1 / 2; if (h0 > 0) { cut += a0; fill -= a1; } else { fill -= a0; cut += a1; } }
                    }
                    gPrev = g; xPrev = x; dPrev = dz;
                }
            }
            m.EarthworkSections.Add((st[i], cut, fill));
            if (prev is { } p)
            {
                double L = st[i] - p.s;
                cutV += L * (p.c + cut) / 2; fillV += L * (p.f + fill) / 2;
            }
            prev = (st[i], cut, fill);
        }
        m.CutVolume = cutV; m.FillVolume = fillV;
        if (skipped > 0)
            m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Road {m.Definition.Name}: earthwork left out at {skipped} of {st.Count} sections (a side slope does not daylight there)."));
        m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Road {m.Definition.Name}: earthwork cut {cutV:0.#} m3 ({cutV / 0.764554858:0.#} CY), fill {fillV:0.#} m3 ({fillV / 0.764554858:0.#} CY) to the subgrade (bottom of the courses, or of the placed subbase layers), average end areas, no curvature correction."));
    }
}
