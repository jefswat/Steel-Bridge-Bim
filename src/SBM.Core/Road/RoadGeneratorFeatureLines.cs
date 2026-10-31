using SBM.Core.Alignments;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Road;

public static partial class RoadGenerator
{
    /// <summary>A feature line located on the alignment: per vertex (station, signed offset, z), stations increasing.</summary>
    internal sealed record LocatedLine(RoadFeatureLine Line, double[] Sta, double[] Off, double[] Z)
    {
        public double From => Sta[0];
        public double To => Sta[^1];
        public double MeanOffset => Off.Average();
        public (double off, double z) At(double s)
        {
            int k = Array.BinarySearch(Sta, s);
            if (k >= 0) return (Off[k], Z[k]);
            k = ~k;
            if (k <= 0) return (Off[0], Z[0]);
            if (k >= Sta.Length) return (Off[^1], Z[^1]);
            double t = (s - Sta[k - 1]) / (Sta[k] - Sta[k - 1]);
            return (Off[k - 1] + (Off[k] - Off[k - 1]) * t, Z[k - 1] + (Z[k] - Z[k - 1]) * t);
        }
    }

    /// <summary>Locates the feature lines on the alignment (vertices off the alignment's station range are dropped, a line that turns back on itself is cut at the turn).</summary>
    internal static List<LocatedLine> Locate(IEnumerable<RoadFeatureLine> lines, Alignment a, Action<string> warn)
    {
        var res = new List<LocatedLine>();
        foreach (var fl in lines)
        {
            var pts = fl.Points.Select(p => (so: a.Inverse(new Vec2(p.X, p.Y)), p.Z)).Where(q => q.so.OnAlignment).ToList();
            if (pts.Count >= 2 && pts[^1].so.Station < pts[0].so.Station) pts.Reverse();
            var keep = new List<(double s, double o, double z)>();
            foreach (var (so, z) in pts)
            {
                if (keep.Count > 0 && so.Station <= keep[^1].s + 1e-6)
                {
                    if (so.Station < keep[^1].s - 0.01) { warn($"feature line {fl.Name} turns back along the alignment at station {so.Station:0.###} - cut there."); break; }
                    continue;
                }
                keep.Add((so.Station, so.Offset, z));
            }
            if (keep.Count < 2) { warn($"feature line {fl.Name} has fewer than 2 points along the alignment - ignored."); continue; }
            res.Add(new LocatedLine(fl, keep.Select(k => k.s).ToArray(), keep.Select(k => k.o).ToArray(), keep.Select(k => k.z).ToArray()));
        }
        return res;
    }

    internal static RoadComponentKind KindFromName(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("shoulder") || n.Contains("shld")) return RoadComponentKind.Shoulder;
        if (n.Contains("curb") || n.Contains("gutter") || n.Contains("flange")) return RoadComponentKind.CurbAndGutter;
        if (n.Contains("sidewalk") || n.Contains("walk")) return RoadComponentKind.Sidewalk;
        if (n.Contains("median")) return RoadComponentKind.Median;
        if (n.Contains("ditch") || n.Contains("daylight")) return RoadComponentKind.Ditch;
        return RoadComponentKind.Lane;
    }

    /// <summary>
    /// Road built between feature lines (<see cref="RoadDefinition.FeatureLines"/>): sections at <= SampleSpacing plus every feature-line vertex station inside the
    /// common station range; strips between neighbouring lines (ordered by their mean offset), top = the lines' 3D points (linear between vertices),
    /// courses (<see cref="RoadDefinition.FeatureLineCourses"/>) hang below with vertical sides; no courses = top surface only (flagged). Earthwork is not computed
    /// (the corridor's own daylight lines are feature lines).
    /// </summary>
    private static RoadModel FromFeatureLines(RoadDefinition d, Alignment a, RoadModel m, Action<string> warn)
    {
        void Info(string s) => m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Road {d.Name}: {s}"));
        if (d.Left.Components.Count > 0 || d.Right.Components.Count > 0) warn("feature lines are given: the Left / Right template is ignored.");
        var lines = Locate(d.FeatureLines!, a, warn).OrderBy(l => l.MeanOffset).ToList();
        if (lines.Count < 2) { warn("fewer than 2 feature lines lie along the alignment - not built."); return m; }
        double s0 = lines.Max(l => l.From), s1 = lines.Min(l => l.To);
        if (d.EndStation > d.BeginStation) { s0 = Math.Max(s0, Math.Min(d.BeginStation, d.EndStation)); s1 = Math.Min(s1, Math.Max(d.BeginStation, d.EndStation)); }
        if (!(s1 - s0 > 1e-3)) { warn("the feature lines have no common station range - not built."); return m; }
        double sp = d.SampleSpacing > 0.05 ? d.SampleSpacing : 2.0;
        int n = Math.Max(1, (int)Math.Ceiling((s1 - s0) / sp));
        var st = Enumerable.Range(0, n + 1).Select(i => s0 + (s1 - s0) * i / n).Concat(lines.SelectMany(l => l.Sta).Where(x => x > s0 + 1e-6 && x < s1 - 1e-6)).OrderBy(x => x).ToList();
        var clean = new List<double>();
        foreach (double x in st) if (clean.Count == 0 || x - clean[^1] > 1e-4) clean.Add(x);
        st = clean;
        var at = lines.Select(l => st.Select(s => l.At(s)).ToArray()).ToList();
        Vec3 P(int line, int i, double drop = 0) { var (o, z) = at[line][i]; return a.PointAt(st[i], o).WithZ(z - drop); }
        // lines must not cross: report crossings, build the strips where they are in order
        for (int k = 0; k + 1 < lines.Count; k++)
        {
            int bad = Enumerable.Range(0, st.Count).Count(i => at[k + 1][i].off < at[k][i].off - 1e-4);
            if (bad > 0) warn($"feature lines {lines[k].Line.Name} and {lines[k + 1].Line.Name} cross at {bad} of {st.Count} sections - the strip between them is not built there (reported).");
        }
        var courses = d.FeatureLineCourses?.Where(c => c.Thickness > 1e-6).ToList() ?? new();
        if (courses.Count == 0)
            warn(UnresolvedFlag.Text(d.Source, "imported corridor pavement structure", "the courses are not in a LandXML corridor export (feature lines carry the top only)",
                "top surface only; add FeatureLineCourses from the typical section."));
        var surface = new Mesh();
        for (int k = 0; k + 1 < lines.Count; k++)
        {
            var li = lines[k]; var lo = lines[k + 1];
            double mid = (li.MeanOffset + lo.MeanOffset) / 2;
            string side = mid < 0 ? "left" : "right";
            // the outward line names the strip: left of the alignment the inner line is the higher index
            var outer = mid < 0 ? li : lo;
            var kind = outer.Line.Kind ?? KindFromName(outer.Line.Name);
            string comp = $"{li.Line.Name} - {lo.Line.Name}";
            var ok = Enumerable.Range(0, st.Count).Select(i => at[k + 1][i].off - at[k][i].off > 1e-4).ToArray();
            // runs of sections where the strip has width
            var runs = new List<(int a, int b)>();
            for (int i = 0; i < st.Count;)
            {
                if (!ok[i]) { i++; continue; }
                int j = i; while (j + 1 < st.Count && ok[j + 1]) j++;
                if (j > i) runs.Add((i, j));
                i = j + 1;
            }
            double area = 0;
            foreach (var (ia, ib) in runs)
            {
                for (int i = ia; i < ib; i++)
                {
                    var p00 = P(k, i); var p01 = P(k + 1, i); var p10 = P(k, i + 1); var p11 = P(k + 1, i + 1);
                    area += 0.5 * (Vec3.Cross(p01 - p00, p10 - p00).Length + Vec3.Cross(p10 - p11, p01 - p11).Length);
                    if (courses.Count == 0)
                    {
                        int b = surface.Positions.Count;
                        surface.Add(p00); surface.Add(p01); surface.Add(p10); surface.Add(p11);
                        surface.Tri(b, b + 1, b + 3); surface.Tri(b, b + 3, b + 2);
                    }
                }
                double depth = 0;
                foreach (var co in courses)
                {
                    double t0 = depth, t1 = depth + co.Thickness;
                    double w0 = at[k + 1][ia].off - at[k][ia].off;
                    var sec2 = new List<Vec2> { new(0, -t0), new(w0, -t0), new(w0, -t1), new(0, -t1) };
                    var rings = Enumerable.Range(ia, ib - ia + 1).Select(i => new[] { P(k, i, t0), P(k + 1, i, t0), P(k + 1, i, t1), P(k, i, t1) }).ToList();
                    var mesh = new Mesh();
                    MeshBuilder.AddLoft(mesh, sec2, rings, false);
                    m.Parts.Add(new RoadPart($"{d.Name} {comp} {co.Name}", comp, kind, co.Name, co.Material, mesh, side));
                    depth = t1;
                }
            }
            m.TopArea[$"{side} {comp}"] = m.TopArea.GetValueOrDefault($"{side} {comp}") + area;
        }
        if (!surface.IsEmpty) m.SideSlopes["finished surface"] = surface;
        for (int k = 0; k < lines.Count; k++) { int kk = k; m.EdgeLines.Add(st.Select((_, i) => P(kk, i)).ToArray()); }
        m.OuterLeft = 0; m.OuterRight = lines.Count - 1;
        foreach (var (label, pick) in new[] { ("left", (Func<int, bool>)(k => lines[k].MeanOffset <= 0)), ("right", k => lines[k].MeanOffset > 0) })
        {
            var idx = Enumerable.Range(0, lines.Count).Where(pick).OrderBy(k => Math.Abs(lines[k].MeanOffset)).ToList();
            if (idx.Count > 0) m.Levels.Add(new RoadLevel(label, "finished grade", null, st.Select((s, i) => (s, idx.Select(k => at[k][i]).Select(q => (q.off, q.z)).ToList())).ToList()));
        }
        Info($"built between {lines.Count} feature lines ({string.Join(", ", lines.Select(l => l.Line.Name))}) from station {s0:0.###} to {s1:0.###}, {st.Count} sections; earthwork not computed for an imported corridor.");
        return m;
    }

    /// <summary>
    /// Converts a feature-line road to the parametric Left / Right template when every line keeps its offset within <paramref name="offsetTolerance"/> over the
    /// common range: component widths = the offset differences; a strip whose cross slope stays within <paramref name="slopeTolerance"/> gets that Slope, else the
    /// alignment's cross-slope table is used (the largest elevation difference to the feature lines is reported). Returns null + the reason when it cannot.
    /// </summary>
    public static (RoadDefinition? Road, List<string> Notes) ToTemplate(RoadDefinition d, Alignment a, double offsetTolerance = 0.01, double slopeTolerance = 0.001)
    {
        var notes = new List<string>();
        if (d.FeatureLines is not { Count: >= 2 }) { notes.Add("no feature lines."); return (null, notes); }
        var lines = Locate(d.FeatureLines, a, notes.Add).OrderBy(l => l.MeanOffset).ToList();
        if (lines.Count < 2) { notes.Add("fewer than 2 feature lines along the alignment."); return (null, notes); }
        double s0 = lines.Max(l => l.From), s1 = lines.Min(l => l.To);
        int n = Math.Max(2, (int)Math.Ceiling((s1 - s0) / 1.0));
        var st = Enumerable.Range(0, n + 1).Select(i => s0 + (s1 - s0) * i / n).ToList();
        var offs = new double[lines.Count];
        for (int k = 0; k < lines.Count; k++)
        {
            var o = st.Select(s => lines[k].At(s).off).ToList();
            if (o.Max() - o.Min() > offsetTolerance) { notes.Add($"feature line {lines[k].Line.Name} varies in offset by {o.Max() - o.Min():0.###} m - the template needs constant widths; kept as feature lines."); return (null, notes); }
            offs[k] = o.Average();
        }
        var r = new RoadDefinition
        {
            Name = d.Name, AlignmentName = d.AlignmentName, BeginStation = s0, EndStation = s1, SampleSpacing = d.SampleSpacing, Source = d.Source,
            Unresolved = d.Unresolved.ToList(), Clearances = d.Clearances, BeginAtBridge = d.BeginAtBridge, EndAtBridge = d.EndAtBridge,
        };
        // inner edge per side = the line nearest the alignment on that side (0 when a line runs on the alignment)
        foreach (var (side, sign) in new[] { (r.Left, -1), (r.Right, 1) })
        {
            var idx = Enumerable.Range(0, lines.Count).Where(k => sign < 0 ? offs[k] <= 1e-6 : offs[k] >= -1e-6).OrderBy(k => Math.Abs(offs[k])).ToList();
            if (idx.Count < 2) continue;
            side.StartOffset = Math.Abs(offs[idx[0]]);
            for (int q = 1; q < idx.Count; q++)
            {
                var li = lines[idx[q - 1]]; var lo = lines[idx[q]];
                double w = Math.Abs(offs[idx[q]] - offs[idx[q - 1]]);
                var slopes = st.Select(s => (lo.At(s).z - li.At(s).z) / w).ToList();
                double? slope = slopes.Max() - slopes.Min() <= slopeTolerance ? slopes.Average() : null;
                var c = new RoadComponent { Name = lo.Line.Name, Kind = lo.Line.Kind ?? KindFromName(lo.Line.Name), Width = w, Slope = slope, Source = d.Source };
                foreach (var co in d.FeatureLineCourses ?? new()) c.Courses.Add(new RoadCourse { Name = co.Name, Thickness = co.Thickness, Material = co.Material });
                side.Components.Add(c);
                if (slope == null)
                {
                    double dev = st.Max(s => Math.Abs(a.ElevationAt(s, offs[idx[q]]) - lo.At(s).z));
                    notes.Add($"{(sign < 0 ? "left" : "right")} {c.Name}: the cross slope varies ({slopes.Min():0.####} to {slopes.Max():0.####}) - the alignment's cross-slope table is used; "
                        + $"largest difference to the feature line {dev:0.###} m (reported).");
                }
            }
            double dev0 = st.Max(s => Math.Abs(a.ElevationAt(s, offs[idx[0]]) - lines[idx[0]].At(s).z));
            if (dev0 > 0.003) notes.Add($"{(sign < 0 ? "left" : "right")} inner line {lines[idx[0]].Line.Name}: the alignment's finished surface differs from it by up to {dev0:0.###} m (reported).");
        }
        return (r, notes);
    }
}
