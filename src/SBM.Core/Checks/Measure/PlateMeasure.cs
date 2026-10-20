using System.Text.Json.Serialization;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Units;

namespace SBM.Core.Checks.Measure;

/// <summary>How the in-plane u axis of a flattened plate is chosen.</summary>
public sealed class PlateFrameOptions
{
    /// <summary>"longest-edge" (default), "horizontal" (level line in the plate plane), "holes" (dominant hole-pattern direction) or "vector"
    /// (<see cref="Vector"/>, e.g. a girder tangent incl. grade, projected into the plane).</summary>
    public string Ref { get; set; } = "longest-edge";
    public Vec3? Vector { get; set; }
    /// <summary>Text naming the reference in the output (e.g. "girder tangent G1 incl. grade").</summary>
    public string? RefName { get; set; }
    /// <summary>Preferred sense of u (e.g. up-station / ahead direction) when the reference gives only a line.</summary>
    public Vec3? Ahead { get; set; }
    /// <summary>Which plate component (0 = largest faces; angle legs, WT flange / stem are 0, 1, ...).</summary>
    public int Component { get; set; }
    /// <summary>Hole row / column clustering tolerance (m), default 1/8".</summary>
    public double Tolerance { get; set; } = 0.0254 / 8;
}

/// <summary>The plate's own frame: u, v in the mid plane (v "up" for inclined plates, n toward the viewer), origin at the outline's lower-left.</summary>
public sealed class PlateFrame
{
    public required Vec3 Origin { get; init; }
    public required Vec3 U { get; init; }
    public required Vec3 V { get; init; }
    public required Vec3 N { get; init; }
    public Vec2 To2(Vec3 p) { var d = p - Origin; return new Vec2(Vec3.Dot(d, U), Vec3.Dot(d, V)); }
    public Vec3 To3(Vec2 q) => Origin + U * q.X + V * q.Y;
}

public sealed class OutlineEdge
{
    public int No { get; init; }
    public string Type { get; init; } = "line";
    public Len Length { get; init; }
    /// <summary>Direction from +u, degrees (counter-clockwise outline).</summary>
    public double AngleDeg { get; init; }
    public double[] From { get; init; } = Array.Empty<double>();
    public double[] To { get; init; } = Array.Empty<double>();
    public Len? Radius { get; init; }
    public double? SweepDeg { get; init; }
    public bool? Concave { get; init; }
}

public sealed class OutlineCorner
{
    public int No { get; init; }
    public double[] At { get; init; } = Array.Empty<double>();
    public double InteriorDeg { get; init; }
    public bool Concave { get; init; }
}

public sealed class OutlineFeature
{
    public required string Kind { get; init; }
    public required string Text { get; init; }
    public double[]? At { get; init; }
    public Len? LegA { get; init; }
    public Len? LegB { get; init; }
    public Len? Radius { get; init; }
}

public sealed class EdgeDistance
{
    /// <summary>Ray direction from the hole: -u, +u, -v, +v.</summary>
    public required string Dir { get; init; }
    /// <summary>Outline edge hit (number in <see cref="PlateResult.Edges"/>; openings: "opening n").</summary>
    public required string Edge { get; init; }
    /// <summary>Distance from the hole center to that edge measured perpendicular to the edge.</summary>
    public required Len Perpendicular { get; init; }
    /// <summary>Distance along the ray (= perpendicular when the edge is square to the axis).</summary>
    public required Len AlongAxis { get; init; }
}

public sealed class HoleResult
{
    public int No { get; init; }
    public double[] At { get; init; } = Array.Empty<double>();
    public string Shape { get; init; } = "round";
    public Len Diameter { get; init; }
    public Len? SlotLength { get; init; }
    public double? SlotAngleDeg { get; init; }
    public int Group { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
    public Len? NearestEdge { get; set; }
    public string? NearestEdgeNo { get; set; }
    public List<EdgeDistance> Edges { get; } = new();
    [JsonIgnore] public Vec2 C { get; init; }
    [JsonIgnore] public double D { get; init; }
}

public sealed class OpeningResult
{
    public int No { get; init; }
    public double[] Min { get; init; } = Array.Empty<double>();
    public double[] Max { get; init; } = Array.Empty<double>();
    public Len Width { get; init; }
    public Len Height { get; init; }
    public double AreaIn2 { get; init; }
    public List<double[]> Outline { get; init; } = new();
}

/// <summary>One bolt/hole group: a block of rows x columns between large gaps.</summary>
public sealed class HoleGroup
{
    public int Group { get; init; }
    public int Holes { get; init; }
    public int Rows { get; init; }
    public int Columns { get; init; }
    /// <summary>Angle of the group's pattern axes from the plate's u / v (0 = square to the frame). Columns, rows, spacings and edge distances
    /// (-u / +u / -v / +v) are read along these axes.</summary>
    public double AxisDeg { get; init; }
    /// <summary>True when every row x column position has a hole.</summary>
    public bool FullGrid { get; init; }
    public List<Len> Diameters { get; init; } = new();
    /// <summary>Column positions (u) and row positions (v), short display units.</summary>
    public List<double> ColumnsU { get; init; } = new();
    public List<double> RowsV { get; init; } = new();
    /// <summary>Spacing chain along u (between columns; "pitch" when u runs along the member) and along v (between rows; "gage").</summary>
    public string SpacingU { get; init; } = "";
    public string SpacingV { get; init; } = "";
    public List<SpaceRun> SpacingURuns { get; init; } = new();
    public List<SpaceRun> SpacingVRuns { get; init; } = new();
    /// <summary>Smallest perpendicular edge distance of the group's holes toward each side (-u, +u, -v, +v).</summary>
    public Dictionary<string, Len> EdgeDistance { get; init; } = new();
    public Len? MinEdgeDistance { get; init; }
    public double[] Min { get; init; } = Array.Empty<double>();
    public double[] Max { get; init; } = Array.Empty<double>();
}

public sealed class PlateResult
{
    public string Query { get; init; } = "part";
    public required string Bridge { get; init; }
    public required string Mark { get; init; }
    public string Assembly { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public string? GlobalId { get; init; }
    public string Units { get; init; } = "in";
    public required string Source { get; init; }
    public int Component { get; init; }
    public int Components { get; init; }
    public Len Thickness { get; init; }
    /// <summary>World (project long units, E N Z) origin of the (u, v) coordinates = the outline's lower-left on the mid plane.</summary>
    public double[] Origin { get; init; } = Array.Empty<double>();
    public double[] U { get; init; } = Array.Empty<double>();
    public double[] V { get; init; } = Array.Empty<double>();
    public double[] Normal { get; init; } = Array.Empty<double>();
    public string Reference { get; init; } = "";
    /// <summary>Angle of the plate normal from vertical (0 = horizontal plate) and of v from plumb (0 = a vertical plate with v plumb).</summary>
    public double NormalFromVerticalDeg { get; init; }
    public double VFromPlumbDeg { get; init; }
    public double? SweepToNormalDeg { get; init; }
    public double FlatSharePct { get; init; }
    public Len Width { get; init; }
    public Len Height { get; init; }
    public double AreaIn2 { get; init; }
    public List<double[]> Outline { get; init; } = new();
    public List<OutlineEdge> Edges { get; init; } = new();
    public List<OutlineCorner> Corners { get; init; } = new();
    public List<OutlineFeature> Features { get; init; } = new();
    public List<HoleResult> Holes { get; init; } = new();
    public List<OpeningResult> Openings { get; init; } = new();
    public List<HoleGroup> HoleGroups { get; init; } = new();
    /// <summary>Clear distance between adjacent hole groups along u / v (center to center of the facing columns / rows).</summary>
    public List<string> GroupGaps { get; init; } = new();
    public double? PatternAngleDeg { get; init; }
    public List<string> Notes { get; init; } = new();
    [JsonIgnore] public FlatShape? Shape { get; init; }
    [JsonIgnore] public PlateFrame? Frame { get; init; }
    [JsonIgnore] public double Shift => 0;
}

/// <summary>Flattens a plate-like part into its own plane and measures it (outline, corners, clips / copes, holes, bolt pattern, edge distances).</summary>
public static class PlateMeasure
{
    public static PlateResult? Measure(Mesh mesh, ElementRef id, string category, string description, PlateFrameOptions o, UnitSystem u)
    {
        var comps = PlateGeometry.Components(mesh);
        if (comps.Count == 0) return null;
        if (o.Component < 0 || o.Component >= comps.Count) throw new ArgumentException($"{id.Mark} has {comps.Count} plate component(s); --leg {o.Component} is out of range.");
        var c = comps[o.Component];
        var notes = new List<string>();
        var frame = FrameFor(mesh, c, o, notes, out string refText);
        var shape = PlateGeometry.Outline(mesh, c, frame.Origin, frame.U, frame.V);
        if (shape.Outer.Count < 3)
        {
            notes.Add("the mid-plane cut gives no closed outline (curved or non-planar part)");
            return new PlateResult { Bridge = id.Bridge, Mark = id.Mark, Assembly = id.Assembly ?? "", Category = category, Description = description, GlobalId = id.GlobalId, Source = shape.Source, Thickness = Len.Of(c.Thickness), Notes = notes, Units = u.ShortUnitName };
        }
        // origin at the lower-left of the outline
        double u0 = shape.Outer.Min(p => p.X), v0 = shape.Outer.Min(p => p.Y);
        var shift = new Vec2(u0, v0);
        var f = new PlateFrame { Origin = frame.Origin + frame.U * u0 + frame.V * v0, U = frame.U, V = frame.V, N = frame.N };
        var outer = shape.Outer.Select(p => p - shift).ToList();
        var fs = new FlatShape { Outer = outer, Source = shape.Source, Sweep = shape.Sweep };
        fs.Inner.AddRange(shape.Inner.Select(l => l.Select(p => p - shift).ToList()));
        fs.OtherPieces.AddRange(shape.OtherPieces.Select(l => l.Select(p => p - shift).ToList()));
        foreach (var (hc, d) in shape.ExactHoles) fs.ExactHoles.Add((hc - shift, d));
        if (fs.OtherPieces.Count > 0) notes.Add($"{fs.OtherPieces.Count + 1} separate pieces in the plane; the largest is measured");
        if (c.Coverage < 0.6) notes.Add($"the two faces are only {c.Coverage * 100:0}% of the surface: the part is not a flat plate (curved, bent or a rolled shape); dimensions are those of the cut at the mid plane");

        double S(double m) => Fmt.Short(u, m);
        double[] P2(Vec2 p) => new[] { S(p.X), S(p.Y) };
        var (edges, corners, features, rawToEdge) = AnalyzeOutline(outer, P2);
        var (holes, openings) = Holes(fs, P2, u);
        var openingLoops = fs.Inner.Where(l => !holes.Any(h => (Geo2.Centroid(l) - h.C).Length < 1e-4)).ToList();
        var (groups, gaps, pattern) = Pattern(holes, o.Tolerance, outer, openingLoops, rawToEdge, u, P2, notes);

        double w = outer.Max(p => p.X), h = outer.Max(p => p.Y);
        double area = Geo2.Area(outer) - fs.Inner.Sum(l => Math.Abs(Geo2.Area(l)));
        double? sweepAng = shape.Sweep is { } sw ? Math.Acos(Math.Min(1, Math.Abs(Vec3.Dot(sw.Normalized(), f.N)))) * 180 / Math.PI : null;
        if (sweepAng is > 0.05) notes.Add($"edges are bevelled: the sweep is {sweepAng:0.00}° from the plate normal (outline = mid-plane outline)");
        return new PlateResult
        {
            Bridge = id.Bridge, Mark = id.Mark, Assembly = id.Assembly ?? "", Category = category, Description = description, GlobalId = id.GlobalId,
            Units = u.ShortUnitName, Source = shape.Source, Component = o.Component, Components = comps.Count, Thickness = Len.Of(c.Thickness),
            Origin = World(u, f.Origin), U = Geo3.Arr(f.U), V = Geo3.Arr(f.V), Normal = Geo3.Arr(f.N), Reference = refText,
            NormalFromVerticalDeg = Fmt.R(Math.Acos(Math.Min(1, Math.Abs(f.N.Z))) * 180 / Math.PI, 3),
            VFromPlumbDeg = Fmt.R(Math.Acos(Math.Clamp(f.V.Z, -1, 1)) * 180 / Math.PI, 3),
            SweepToNormalDeg = sweepAng is { } sa ? Fmt.R(sa, 3) : null, FlatSharePct = Fmt.R(c.Coverage * 100, 1),
            Width = Len.Of(w), Height = Len.Of(h), AreaIn2 = Fmt.R(area / (0.0254 * 0.0254), 2),
            Outline = outer.Select(P2).ToList(), Edges = edges, Corners = corners, Features = features, Holes = holes, Openings = openings,
            HoleGroups = groups, GroupGaps = gaps, PatternAngleDeg = pattern, Notes = notes, Shape = fs, Frame = f,
        };
    }

    public static double[] World(UnitSystem u, Vec3 p) => new[] { Fmt.Long(u, p.X), Fmt.Long(u, p.Y), Fmt.Long(u, p.Z) };

    /// <summary>The plate frame: u from the reference projected into the plane; v = in-plane "up" (horizontal plates: n up, v = n x u).</summary>
    public static PlateFrame FrameFor(Mesh mesh, PlateComponent c, PlateFrameOptions o, List<string> notes, out string refText)
    {
        var N0 = c.Normal;
        var t0 = Math.Abs(N0.Z) < 0.9 ? Vec3.Cross(new Vec3(0, 0, 1), N0).Normalized() : Vec3.Cross(N0, new Vec3(1, 0, 0)).Normalized();
        var V0 = Vec3.Cross(N0, t0);
        Vec3? U = null;
        refText = o.RefName ?? o.Ref;
        Vec3 InPlane(Vec3 r) => r - N0 * Vec3.Dot(r, N0);
        switch (o.Ref)
        {
            case "vector" when o.Vector is { } vec:
            {
                var p = InPlane(vec);
                if (p.Length > 0.1 * vec.Length) U = p.Normalized();
                else notes.Add($"the reference ({refText}) is nearly normal to the plate; u = longest edge instead");
                break;
            }
            case "horizontal":
                if (Math.Abs(N0.Z) < 0.995) U = Vec3.Cross(new Vec3(0, 0, 1), N0).Normalized();
                else notes.Add("the plate is level: every in-plane line is horizontal; u = longest edge instead");
                break;
            case "holes":
            {
                var shape = PlateGeometry.Outline(mesh, c, c.MidPoint, t0, V0);
                var hs = Holes(shape, p => new[] { p.X, p.Y }, null).holes;
                if (hs.Count >= 3 && DominantAngle(hs.Select(h => h.C).ToList()) is { } ang)
                    U = (t0 * Math.Cos(ang) + V0 * Math.Sin(ang)).Normalized();
                else notes.Add("fewer than 3 holes: u = longest edge instead");
                break;
            }
        }
        if (U == null)
        {
            var shape = PlateGeometry.Outline(mesh, c, c.MidPoint, t0, V0);
            var ol = shape.Outer.Count > 3 ? Geo2.Simplify(shape.Outer, 0.0254 / 32) : shape.Outer;   // kinks of 1/32" do not split the reference edge
            double best = 0; Vec2 dir = new(1, 0);
            for (int i = 0; i < ol.Count; i++)
            {
                var e = ol[(i + 1) % ol.Count] - ol[i];
                if (e.Length > best + 1e-9) { best = e.Length; dir = e.Normalized(); }
            }
            U = (t0 * dir.X + V0 * dir.Y).Normalized();
            if (o.Ref != "longest-edge" && !refText.Contains("longest")) refText += " -> longest edge";
            else refText = $"longest edge ({Fmt.FtIn(best)})";
            // a line, not a direction: point it ahead / +X
            var ahead = o.Ahead ?? new Vec3(1, 0, 0);
            var ap = InPlane(ahead);
            if (ap.Length < 1e-6) ap = InPlane(new Vec3(0, 1, 0));
            if (Vec3.Dot(U.Value, ap) < 0) U = -U.Value;
        }
        var u = U.Value;
        Vec3 n, v;
        if (Math.Abs(N0.Z) > 0.95)
        {
            n = N0.Z > 0 ? N0 : -N0;
            v = Vec3.Cross(n, u).Normalized();
        }
        else
        {
            v = Vec3.Cross(N0, u).Normalized();
            if (v.Z < 0) v = -v;
            n = Vec3.Cross(u, v).Normalized();
        }
        return new PlateFrame { Origin = c.MidPoint, U = u, V = v, N = n };
    }

    /// <summary>Dominant direction (radians, modulo 90 deg, in (-45, 45]) of the nearest-neighbour vectors of points.</summary>
    public static double? DominantAngle(List<Vec2> pts)
    {
        double sx = 0, sy = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            double best = double.MaxValue; Vec2 d = default;
            for (int j = 0; j < pts.Count; j++)
            {
                if (i == j) continue;
                double l = (pts[j] - pts[i]).Length;
                if (l > 1e-6 && l < best) { best = l; d = pts[j] - pts[i]; }
            }
            if (best == double.MaxValue) continue;
            double a = 4 * Math.Atan2(d.Y, d.X);
            sx += Math.Cos(a); sy += Math.Sin(a);
        }
        if (Math.Abs(sx) + Math.Abs(sy) < 1e-9) return null;
        return Math.Atan2(sy, sx) / 4;
    }

    /// <summary>Edges (lines and sampled arcs merged), corners, clips and copes of a counter-clockwise outline.</summary>
    public static (List<OutlineEdge> Edges, List<OutlineCorner> Corners, List<OutlineFeature> Features, int[] RawToEdge) AnalyzeOutline(List<Vec2> p, Func<Vec2, double[]> P2)
    {
        // kinks: vertices between two long edges that turn by less than 0.5 deg (a plate following a kinked / chorded girder) are not corners;
        // the edge is reported as one line (its chord) and the kink as a feature
        int n0 = p.Count;
        var keep = Enumerable.Repeat(true, n0).ToArray();
        var kinks = new List<OutlineFeature>();
        for (int i = 0; i < n0 && n0 > 3; i++)
        {
            int ip = (i - 1 + n0) % n0; while (!keep[ip]) ip = (ip - 1 + n0) % n0;
            int inx = (i + 1) % n0;
            var e0 = p[i] - p[ip]; var e1 = p[inx] - p[i];
            double t = Math.Atan2(Vec2.Cross(e0, e1), Vec2.Dot(e0, e1)) * 180 / Math.PI;
            if (Math.Abs(t) < 0.5 && e0.Length > 0.0254 && e1.Length > 0.0254 && keep.Count(k => k) > 3)
            {
                keep[i] = false;
                kinks.Add(new OutlineFeature { Kind = "kink", At = P2(p[i]), Text = $"kink {t:0.###}° in a straight edge at ({P2(p[i])[0]}, {P2(p[i])[1]}): the plate follows a kinked or chorded line (edge reported as its chord)" });
            }
        }
        if (kinks.Count == 0) return AnalyzeCore(p, P2);
        var idx = Enumerable.Range(0, n0).Where(i => keep[i]).ToList();
        var q = idx.Select(i => p[i]).ToList();
        var (edges, corners, feats, rawQ) = AnalyzeCore(q, P2);
        feats.AddRange(kinks);
        var raw = new int[n0];
        for (int i = 0; i < n0; i++)
        {
            int k = idx.Count - 1;
            for (int j = 0; j < idx.Count; j++) if (idx[j] <= i) k = j;
            if (idx[0] > i) k = idx.Count - 1;
            raw[i] = rawQ[k];
        }
        return (edges, corners, feats, raw);
    }

    private static (List<OutlineEdge> Edges, List<OutlineCorner> Corners, List<OutlineFeature> Features, int[] RawToEdge) AnalyzeCore(List<Vec2> p, Func<Vec2, double[]> P2)
    {
        int n = p.Count;
        var turn = new double[n];   // signed turn at vertex i (from edge i-1 to edge i), degrees
        var len = new double[n];
        for (int i = 0; i < n; i++)
        {
            var e0 = p[i] - p[(i - 1 + n) % n]; var e1 = p[(i + 1) % n] - p[i];
            turn[i] = Math.Atan2(Vec2.Cross(e0, e1), Vec2.Dot(e0, e1)) * 180 / Math.PI;
            len[i] = e1.Length;
        }
        double w = p.Max(q => q.X) - p.Min(q => q.X), h = p.Max(q => q.Y) - p.Min(q => q.Y);
        // arc vertices: |turn| in (0.3, 31] with equal-ish neighbouring edges of the same sense
        var arcV = new bool[n];
        for (int i = 0; i < n; i++)
        {
            double a = Math.Abs(turn[i]);
            if (a < 0.3 || a > 31) continue;
            int ip = (i - 1 + n) % n, inx = (i + 1) % n;
            bool PrevSame = Math.Abs(turn[ip]) is >= 0.3 and <= 31 && Math.Sign(turn[ip]) == Math.Sign(turn[i]) && Ratio(len[ip], len[(ip - 1 + n) % n]) < 1.7;
            bool NextSame = Math.Abs(turn[inx]) is >= 0.3 and <= 31 && Math.Sign(turn[inx]) == Math.Sign(turn[i]) && Ratio(len[i], len[inx]) < 1.7;
            if (PrevSame || NextSame) arcV[i] = true;
        }
        static double Ratio(double a, double b) => Math.Max(a, b) / Math.Max(1e-12, Math.Min(a, b));
        // edge i is in an arc when both its vertices are arc vertices of the same sense
        var arcE = new bool[n];
        for (int i = 0; i < n; i++) arcE[i] = arcV[i] && arcV[(i + 1) % n] && Math.Sign(turn[i]) == Math.Sign(turn[(i + 1) % n]);
        // start at a vertex that begins a line edge (or 0)
        int start = Enumerable.Range(0, n).FirstOrDefault(i => !arcE[(i - 1 + n) % n], 0);
        var edges = new List<OutlineEdge>();
        var raw = new int[n];
        var corners = new List<OutlineCorner>();
        var feats = new List<OutlineFeature>();
        var edgeStart = new List<int>(); var edgeEnd = new List<int>(); var edgeIsArc = new List<bool>();
        int k = 0;
        while (k < n)
        {
            int i = (start + k) % n;
            if (!arcE[i])
            {
                edgeStart.Add(i); edgeEnd.Add((i + 1) % n); edgeIsArc.Add(false);
                raw[i] = edgeStart.Count;
                k++;
            }
            else
            {
                int j = k;
                while (j < n && arcE[(start + j) % n]) { raw[(start + j) % n] = edgeStart.Count + 1; j++; }
                edgeStart.Add(i); edgeEnd.Add((start + j) % n); edgeIsArc.Add(true);
                k = j;
            }
        }
        for (int e = 0; e < edgeStart.Count; e++)
        {
            var a = p[edgeStart[e]]; var b = p[edgeEnd[e]];
            if (!edgeIsArc[e])
            {
                var d = b - a;
                edges.Add(new OutlineEdge { No = e + 1, Type = "line", Length = Len.Of(d.Length), AngleDeg = Fmt.R(Norm360(Math.Atan2(d.Y, d.X) * 180 / Math.PI), 3), From = P2(a), To = P2(b) });
            }
            else
            {
                // sweep = sum of turns over the arc's vertices (tangent points carry half a step each)
                double sweep = 0; int cnt = 0;
                for (int vi = edgeStart[e]; ; vi = (vi + 1) % n) { sweep += turn[vi]; cnt++; if (vi == edgeEnd[e] || cnt > n) break; }
                double chord = (b - a).Length;
                double r = Math.Abs(sweep) > 1e-6 ? chord / (2 * Math.Sin(Math.Abs(sweep) * Math.PI / 360)) : double.NaN;
                double arcLen = r * Math.Abs(sweep) * Math.PI / 180;
                var d = b - a;
                edges.Add(new OutlineEdge { No = e + 1, Type = "arc", Length = Len.Of(arcLen), AngleDeg = Fmt.R(Norm360(Math.Atan2(d.Y, d.X) * 180 / Math.PI), 3), From = P2(a), To = P2(b), Radius = Len.Of(r), SweepDeg = Fmt.R(sweep, 2), Concave = sweep < 0 });
                feats.Add(new OutlineFeature { Kind = sweep < 0 ? "concave arc" : "rounded corner", Text = $"{(sweep < 0 ? "concave arc (cope radius)" : "rounded corner")} R {Fmt.FtIn(r)}, {Math.Abs(sweep):0.#}° between {Fmt.FtIn(a.X)},{Fmt.FtIn(a.Y)} and {Fmt.FtIn(b.X)},{Fmt.FtIn(b.Y)}", At = P2((a + b) * 0.5), Radius = Len.Of(r) });
            }
        }
        // corners between consecutive merged edges
        int m = edges.Count;
        for (int e = 0; e < m; e++)
        {
            int vi = edgeStart[e];
            int prev = (e - 1 + m) % m;
            if (edgeIsArc[e] || edgeIsArc[prev])
            {
                // tangent point of an arc: interior angle from the adjoining chords is not a corner unless the turn is large
                if (Math.Abs(turn[vi]) < 31) continue;
            }
            double interior = 180 - turn[vi];
            corners.Add(new OutlineCorner { No = corners.Count + 1, At = P2(p[vi]), InteriorDeg = Fmt.R(interior, 3), Concave = interior > 180.01 });
        }
        // clips: a short line between two convex corners whose interior angles sum to 270 (a cut corner); copes: re-entrant corners
        double small = 0.35 * Math.Min(w, h);
        for (int e = 0; e < m; e++)
        {
            if (edgeIsArc[e]) continue;
            int prev = (e - 1 + m) % m, next = (e + 1) % m;
            if (edgeIsArc[prev] || edgeIsArc[next]) continue;
            double ia = 180 - turn[edgeStart[e]], ib = 180 - turn[edgeEnd[e]];
            var a = p[edgeStart[e]]; var b = p[edgeEnd[e]];
            if ((b - a).Length > small || ia > 180 || ib > 180 || Math.Abs(ia + ib - 270) > 2) continue;
            var pa = p[edgeStart[prev]]; var nb = p[edgeEnd[next]];
            var x = Intersect(pa, a, b, nb);
            if (x is not { } xc) continue;
            feats.Add(new OutlineFeature
            {
                Kind = "clip", At = P2(xc), LegA = Len.Of((xc - a).Length), LegB = Len.Of((xc - b).Length),
                Text = $"corner clip {Fmt.FtIn((xc - a).Length)} x {Fmt.FtIn((xc - b).Length)} (edge {e + 1}, corner at {Fmt.FtIn(xc.X)}, {Fmt.FtIn(xc.Y)})",
            });
        }
        foreach (var cn in corners.Where(cn => cn.Concave))
            feats.Add(new OutlineFeature { Kind = "re-entrant corner", At = cn.At, Text = $"re-entrant corner (cope / notch) {cn.InteriorDeg:0.##}° at {cn.At[0]}, {cn.At[1]}" });
        return (edges, corners, feats, raw);
    }

    private static double Norm360(double a) { a %= 360; return a < 0 ? a + 360 : a; }

    private static Vec2? Intersect(Vec2 a0, Vec2 a1, Vec2 b0, Vec2 b1)
    {
        var d1 = a1 - a0; var d2 = b1 - b0;
        double den = Vec2.Cross(d1, d2);
        if (Math.Abs(den) < 1e-15) return null;
        double t = Vec2.Cross(b0 - a0, d2) / den;
        return a0 + d1 * t;
    }

    /// <summary>Holes (round / slot) and openings from the inner loops (exact recipe holes when known).</summary>
    public static (List<HoleResult> holes, List<OpeningResult> openings) Holes(FlatShape fs, Func<Vec2, double[]> P2, UnitSystem? u)
    {
        var holes = new List<HoleResult>();
        var openings = new List<OpeningResult>();
        var inner = fs.Inner.ToList();
        if (fs.ExactHoles.Count > 0)
        {
            foreach (var (c, d) in fs.ExactHoles) holes.Add(new HoleResult { C = c, D = d, At = P2(c), Diameter = Len.Of(d) });
            // the 24-gon loops drawn for exact holes are not openings
            inner = inner.Where(l => !fs.ExactHoles.Any(h => (Geo2.Centroid(l) - h.Center).Length < 1e-5)).ToList();
        }
        foreach (var l in inner)
        {
            var c = Geo2.Centroid(l);
            var rs = l.Select(q => (q - c).Length).ToList();
            double mean = rs.Average();
            if (l.Count >= 6 && (rs.Max() - rs.Min()) / mean < 0.03)
            {
                // the solids' hole polygons are circumscribed about the circle (sides tangent): diameter = 2 x apothem (mean edge-midpoint radius)
                double apo = Enumerable.Range(0, l.Count).Average(i => ((l[i] + l[(i + 1) % l.Count]) * 0.5 - c).Length);
                holes.Add(new HoleResult { C = c, D = 2 * apo, At = P2(c), Diameter = Len.Of(2 * apo) });
                continue;
            }
            // principal axes
            double sxx = 0, syy = 0, sxy = 0;
            foreach (var q in l) { var d = q - c; sxx += d.X * d.X; syy += d.Y * d.Y; sxy += d.X * d.Y; }
            double ang = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
            var e1 = Vec2.FromAngle(ang); var e2 = e1.Left;
            double a0 = l.Min(q => Vec2.Dot(q - c, e1)), a1 = l.Max(q => Vec2.Dot(q - c, e1));
            double b0 = l.Min(q => Vec2.Dot(q - c, e2)), b1 = l.Max(q => Vec2.Dot(q - c, e2));
            double L = a1 - a0, W = b1 - b0;
            var mid = c + e1 * ((a0 + a1) / 2) + e2 * ((b0 + b1) / 2);
            double half = (L - W) / 2;
            bool slot = L > W * 1.05 && l.Count >= 8 && l.All(q =>
            {
                double t = Math.Clamp(Vec2.Dot(q - mid, e1), -half, half);
                double dd = (q - (mid + e1 * t)).Length;
                return Math.Abs(dd - W / 2) < 0.04 * W;
            });
            if (slot)
            {
                double deg = Norm360(ang * 180 / Math.PI) % 180;
                holes.Add(new HoleResult { C = mid, D = W, At = P2(mid), Shape = "slot", Diameter = Len.Of(W), SlotLength = Len.Of(L), SlotAngleDeg = Fmt.R(deg, 2) });
                continue;
            }
            var bmin = new Vec2(l.Min(q => q.X), l.Min(q => q.Y)); var bmax = new Vec2(l.Max(q => q.X), l.Max(q => q.Y));
            openings.Add(new OpeningResult
            {
                No = openings.Count + 1, Min = P2(bmin), Max = P2(bmax), Width = Len.Of(bmax.X - bmin.X), Height = Len.Of(bmax.Y - bmin.Y),
                AreaIn2 = Fmt.R(Math.Abs(Geo2.Area(l)) / (0.0254 * 0.0254), 2), Outline = l.Select(P2).ToList(),
            });
        }
        var sorted = holes.OrderBy(h => Math.Round(h.C.X / 0.003)).ThenBy(h => h.C.Y).ToList();
        var res = sorted.Select((h, i) => new HoleResult { No = i + 1, C = h.C, D = h.D, At = h.At, Shape = h.Shape, Diameter = h.Diameter, SlotLength = h.SlotLength, SlotAngleDeg = h.SlotAngleDeg }).ToList();
        return (res, openings);
    }

    /// <summary>Nearest outline edge of each hole and, along the given axes, the edge hit by a ray from the hole center with the distance measured
    /// perpendicular to that edge.</summary>
    private static void EdgeDistances(HoleResult h, Vec2 e1, Vec2 e2, List<Vec2> outer, IReadOnlyList<List<Vec2>> openings, int[]? rawToEdge)
    {
        if (outer.Count < 3) return;
        double best = double.MaxValue; int be = -1;
        for (int i = 0; i < outer.Count; i++)
        {
            double d = Geo2.SegDist(h.C, outer[i], outer[(i + 1) % outer.Count]);
            if (d < best) { best = d; be = i; }
        }
        h.NearestEdge = Len.Of(best);
        h.NearestEdgeNo = be >= 0 ? (rawToEdge != null ? rawToEdge[be].ToString() : $"raw {be + 1}") : null;
        h.Edges.Clear();
        foreach (var (name, dir) in new[] { ("-u", -e1), ("+u", e1), ("-v", -e2), ("+v", e2) })
        {
            double tb = double.MaxValue; string edge = ""; Vec2 ea = default, eb = default;
            for (int i = 0; i < outer.Count; i++)
            {
                var a = outer[i]; var b = outer[(i + 1) % outer.Count];
                double t = Geo2.Ray(h.C, dir, a, b);
                if (!double.IsNaN(t) && t < tb) { tb = t; edge = rawToEdge != null ? rawToEdge[i].ToString() : $"raw {i + 1}"; ea = a; eb = b; }
            }
            for (int k = 0; k < openings.Count; k++)
            {
                var l = openings[k];
                for (int i = 0; i < l.Count; i++)
                {
                    var a = l[i]; var b = l[(i + 1) % l.Count];
                    double t = Geo2.Ray(h.C, dir, a, b);
                    if (!double.IsNaN(t) && t < tb) { tb = t; edge = $"opening {k + 1}"; ea = a; eb = b; }
                }
            }
            if (tb == double.MaxValue) continue;
            h.Edges.Add(new EdgeDistance { Dir = name, Edge = edge, Perpendicular = Len.Of(Geo2.LineDist(h.C, ea, eb)), AlongAxis = Len.Of(tb) });
        }
    }

    /// <summary>
    /// Groups, rows / columns, spacing chains and edge distances of holes. Groups are blocks between spacings clearly larger than the typical one; each
    /// group is read along its own pattern axes (the dominant direction of its nearest-neighbour vectors; = u / v when the pattern is square to the
    /// plate frame, else rotated by <see cref="HoleGroup.AxisDeg"/>), so a column of bolts parallel to a sloped end edge is one column.
    /// </summary>
    public static (List<HoleGroup> Groups, List<string> Gaps, double? PatternDeg) Pattern(List<HoleResult> holes, double tol, List<Vec2> outer, IReadOnlyList<List<Vec2>> openings,
        int[]? rawToEdge, UnitSystem u, Func<Vec2, double[]> P2, List<string> notes)
    {
        var groups = new List<HoleGroup>();
        var gaps = new List<string>();
        if (holes.Count == 0) return (groups, gaps, null);
        double thG = holes.Count >= 3 && DominantAngle(holes.Select(h => h.C).ToList()) is { } da ? da : 0;
        if (Math.Abs(thG) < 0.25 * Math.PI / 180) thG = 0;
        var g1 = Vec2.FromAngle(thG); var g2 = g1.Left;
        var us = holes.Select(h => Vec2.Dot(h.C, g1)).ToList(); var vs = holes.Select(h => Vec2.Dot(h.C, g2)).ToList();
        var (cu, ofu) = Geo2.Cluster(us, tol);
        var (cv, ofv) = Geo2.Cluster(vs, tol);
        List<int> Breaks(List<double> centers)
        {
            var res = new List<int>();
            if (centers.Count < 3) return res;
            var sp = centers.Zip(centers.Skip(1), (a, b) => b - a).ToList();
            double med = sp.OrderBy(x => x).ElementAt(sp.Count / 2);
            for (int i = 0; i < sp.Count; i++) if (sp[i] > 1.3 * med && sp[i] > med + 0.0254) res.Add(i);
            return res;
        }
        var bu = Breaks(cu); var bv = Breaks(cv);
        int Band(int idx, List<int> br) => br.Count(b => idx > b);
        var key = holes.Select((h, i) => (Band(ofu[i], bu), Band(ofv[i], bv))).ToList();
        var keys = key.Distinct().OrderBy(k => k.Item2).ThenBy(k => k.Item1).ToList();
        int gno = 0;
        foreach (var k in keys)
        {
            gno++;
            var idx = Enumerable.Range(0, holes.Count).Where(i => key[i] == k).ToList();
            var pts = idx.Select(i => holes[i].C).ToList();
            double th = pts.Count >= 2 && DominantAngle(pts) is { } dl ? dl : thG;
            if (Math.Abs(th) < 0.25 * Math.PI / 180) th = 0;
            if (pts.Count >= 2 && Math.Abs(th - thG) < 0.25 * Math.PI / 180) th = thG;
            var e1 = Vec2.FromAngle(th); var e2 = e1.Left;
            var ga = pts.Select(p => Vec2.Dot(p, e1)).ToList(); var gb = pts.Select(p => Vec2.Dot(p, e2)).ToList();
            var (lcu, lofu) = Geo2.Cluster(ga, tol); var (lcv, lofv) = Geo2.Cluster(gb, tol);
            for (int j = 0; j < idx.Count; j++)
            {
                var h = holes[idx[j]];
                h.Group = gno; h.Column = lofu[j] + 1; h.Row = lofv[j] + 1;
                EdgeDistances(h, e1, e2, outer, openings, rawToEdge);
            }
            var spU = lcu.Zip(lcu.Skip(1), (a, b) => b - a).ToList(); var spV = lcv.Zip(lcv.Skip(1), (a, b) => b - a).ToList();
            var ed = new Dictionary<string, Len>();
            foreach (var dir in new[] { "-u", "+u", "-v", "+v" })
            {
                var ds = idx.SelectMany(i => holes[i].Edges.Where(e => e.Dir == dir)).Select(e => e.Perpendicular.In).ToList();
                if (ds.Count > 0) ed[dir] = Len.Of(ds.Min() * 0.0254);
            }
            double deg = th * 180 / Math.PI;
            groups.Add(new HoleGroup
            {
                Group = gno, Holes = idx.Count, Rows = lcv.Count, Columns = lcu.Count, FullGrid = lcv.Count * lcu.Count == idx.Count, AxisDeg = Fmt.R(deg, 3),
                Diameters = idx.Select(i => Math.Round(holes[i].D / 0.0254 * 16)).Distinct().Select(x => Len.Of(x / 16 * 0.0254)).ToList(),
                ColumnsU = lcu.Select(x => Fmt.Short(u, x)).ToList(), RowsV = lcv.Select(x => Fmt.Short(u, x)).ToList(),
                SpacingU = Fmt.Chain(spU, tol), SpacingV = Fmt.Chain(spV, tol), SpacingURuns = Fmt.Runs(spU, tol), SpacingVRuns = Fmt.Runs(spV, tol),
                EdgeDistance = ed, MinEdgeDistance = idx.All(i => holes[i].NearestEdge != null) ? Len.Of(idx.Min(i => holes[i].NearestEdge!.Value.In) * 0.0254) : null,
                Min = P2(new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y))), Max = P2(new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y))),
            });
            if (Math.Abs(deg) > 0.25) notes.Add($"hole group {gno} is read along its own pattern axes, rotated {deg:0.###}° from u / v (rows, columns, spacings and edge distances along those axes)");
        }
        for (int a = 0; a < keys.Count; a++)
            for (int b = 0; b < keys.Count; b++)
            {
                var ia = Enumerable.Range(0, holes.Count).Where(i => key[i] == keys[a]).ToList();
                var ib = Enumerable.Range(0, holes.Count).Where(i => key[i] == keys[b]).ToList();
                if (keys[b].Item2 == keys[a].Item2 && keys[b].Item1 == keys[a].Item1 + 1)
                    gaps.Add($"groups {a + 1} -> {b + 1} along u: {Fmt.FtIn(ib.Min(i => us[i]) - ia.Max(i => us[i]))} between the facing columns");
                if (keys[b].Item1 == keys[a].Item1 && keys[b].Item2 == keys[a].Item2 + 1)
                    gaps.Add($"groups {a + 1} -> {b + 1} along v: {Fmt.FtIn(ib.Min(i => vs[i]) - ia.Max(i => vs[i]))} between the facing rows");
            }
        return (groups, gaps, thG == 0 ? 0 : Fmt.R(thG * 180 / Math.PI, 3));
    }

    public static string ToText(PlateResult r, UnitSystem u)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{r.Mark} ({r.Assembly}) {r.Category}: {r.Description}");
        sb.AppendLine($"  {r.Bridge}{(r.GlobalId != null ? ", GlobalId " + r.GlobalId : "")}; outline from the {r.Source}; component {r.Component + 1} of {r.Components}");
        sb.AppendLine($"  thickness {r.Thickness.Text} ({r.Thickness.Mm:0.0} mm); overall {r.Width.Text} (u) x {r.Height.Text} (v); area {r.AreaIn2:0.##} in2");
        sb.AppendLine($"  frame: u = {r.Reference}; u {V(r.U)}, v {V(r.V)}, n {V(r.Normal)}; normal {r.NormalFromVerticalDeg:0.###}° from vertical, v {r.VFromPlumbDeg:0.###}° from plumb");
        sb.AppendLine($"  origin (lower-left of the outline, mid plane) E/N/Z {r.Origin.ElementAtOrDefault(0):0.####}, {r.Origin.ElementAtOrDefault(1):0.####}, {r.Origin.ElementAtOrDefault(2):0.####}");
        sb.AppendLine($"  edges ({r.Edges.Count}):");
        foreach (var e in r.Edges)
            sb.AppendLine($"    {e.No,3} {e.Type,-4} {e.Length.Text,-14} at {e.AngleDeg,8:0.000}°  from ({e.From[0]}, {e.From[1]}) to ({e.To[0]}, {e.To[1]}){(e.Radius is { } rr ? $"  R {rr.Text}, sweep {e.SweepDeg}°" : "")}");
        var odd = r.Corners.Where(c => Math.Abs(c.InteriorDeg - 90) > 0.05).ToList();
        sb.AppendLine($"  corners: {r.Corners.Count} ({r.Corners.Count - odd.Count} square){(odd.Count > 0 ? "; not square: " + string.Join(", ", odd.Select(c => $"#{c.No} {c.InteriorDeg:0.###}°")) : "")}");
        foreach (var f in r.Features) sb.AppendLine($"  {f.Text}");
        foreach (var o in r.Openings) sb.AppendLine($"  opening {o.No}: {o.Width.Text} x {o.Height.Text} from ({o.Min[0]}, {o.Min[1]}) to ({o.Max[0]}, {o.Max[1]})");
        sb.AppendLine($"  holes: {r.Holes.Count}{(r.PatternAngleDeg is { } pa ? $" (pattern at {pa:0.###}° to u)" : "")}");
        foreach (var g in r.HoleGroups)
        {
            sb.AppendLine($"    group {g.Group}: {g.Holes} holes {string.Join("/", g.Diameters.Select(d => d.Text))} = {g.Rows} row(s) x {g.Columns} column(s){(g.FullGrid ? "" : " (not a full grid)")}{(g.AxisDeg != 0 ? $", pattern axes at {g.AxisDeg:0.###}° to u / v" : "")}");
            if (g.Columns > 1) sb.AppendLine($"      along u (columns): {g.SpacingU}");
            if (g.Rows > 1) sb.AppendLine($"      along v (rows):    {g.SpacingV}");
            sb.AppendLine($"      first column u = {g.ColumnsU[0]}, first row v = {g.RowsV[0]} {u.ShortUnitName}; edge distances (perpendicular to the edge hit): " +
                string.Join(", ", g.EdgeDistance.Select(kv => $"{kv.Key} {kv.Value.Text}")) + $"; min to any edge {g.MinEdgeDistance?.Text}");
        }
        foreach (var gtxt in r.GroupGaps) sb.AppendLine($"    {gtxt}");
        foreach (var n in r.Notes) sb.AppendLine($"  note: {n}");
        return sb.ToString();
    }

    private static string V(double[] v) => $"({v[0]:0.####}, {v[1]:0.####}, {v[2]:0.####})";
}
