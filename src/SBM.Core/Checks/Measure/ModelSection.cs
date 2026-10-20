using System.Text.Json.Serialization;
using SBM.Core.Drawing;
using SBM.Core.Drawing.Views;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Units;

namespace SBM.Core.Checks.Measure;

/// <summary>A cut plane with its own 2D frame: <see cref="Ahead"/> = the viewing direction (the plane normal, e.g. the girder tangent incl. grade),
/// v = world up made normal to it (so v is "up in the plane", tilted by the grade), u = right when looking ahead.</summary>
public sealed class SectionPlane
{
    public Vec3 Origin { get; }
    public Vec3 Ahead { get; }
    public Vec3 U { get; }
    public Vec3 V { get; }
    public string Description { get; init; } = "";
    private readonly MeshSection _ms;

    public SectionPlane(Vec3 origin, Vec3 ahead, Vec3? up = null)
    {
        Origin = origin;
        Ahead = ahead.Normalized();
        var upv = up ?? new Vec3(0, 0, 1);
        if (Math.Abs(Vec3.Dot(upv.Normalized(), Ahead)) > 0.999) upv = new Vec3(0, 1, 0);
        // MeshSection: X = Y x N; with N = -ahead, X = ahead x Y = right when looking ahead
        _ms = new MeshSection(origin, -Ahead, upv);
        U = _ms.XAxis; V = _ms.YAxis;
    }

    public Vec2 To2(Vec3 p) => _ms.To2D(p);
    public Vec3 To3(Vec2 q) => Origin + U * q.X + V * q.Y;
    /// <summary>Signed distance along the viewing direction (+ = beyond the plane).</summary>
    public double Dist(Vec3 p) => Vec3.Dot(p - Origin, Ahead);
    public List<(Vec2 A, Vec2 B)> Cut(Mesh m) => _ms.Cut(m);

    /// <summary>Plane rotated about v by a skew angle (degrees, + = clockwise in plan seen from above for a level plane).</summary>
    public SectionPlane Skewed(double deg)
    {
        if (Math.Abs(deg) < 1e-12) return this;
        double a = -deg * Math.PI / 180;
        // rotate Ahead about V (Rodrigues)
        var k = V; var v = Ahead;
        var r = v * Math.Cos(a) + Vec3.Cross(k, v) * Math.Sin(a) + k * (Vec3.Dot(k, v) * (1 - Math.Cos(a)));
        return new SectionPlane(Origin, r, V) { Description = Description + $", skewed {deg:0.###}°" };
    }
}

public sealed class SectionLoop
{
    public double AreaIn2 { get; init; }
    public double[] Min { get; init; } = Array.Empty<double>();
    public double[] Max { get; init; } = Array.Empty<double>();
    public List<double[]> Points { get; init; } = new();
    [JsonIgnore] public List<Vec2> P { get; init; } = new();
}

public sealed class SectionElement
{
    public required ElementRef Element { get; init; }
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Concrete { get; init; }
    public double[] Min { get; init; } = Array.Empty<double>();
    public double[] Max { get; init; } = Array.Empty<double>();
    public Len Width { get; init; }
    public Len Height { get; init; }
    public List<SectionLoop> Loops { get; init; } = new();
    /// <summary>Cut segments that do not close into loops (open meshes, window clips).</summary>
    public int OpenSegments { get; init; }
    [JsonIgnore] public List<(Vec2 A, Vec2 B)> Segs { get; init; } = new();
}

public sealed class SectionBar
{
    public required ElementRef Bar { get; init; }
    public string Size { get; init; } = "";
    public double[] At { get; init; } = Array.Empty<double>();
    /// <summary>Angle between the bar and the cut plane (90 = square through it).</summary>
    public double AngleToPlaneDeg { get; init; }
    /// <summary>Clear cover to the nearest face of the concrete loop that contains the dot (face direction: top / bottom / side in the plane's v).</summary>
    public Len? Cover { get; set; }
    public string? CoverFace { get; set; }
    public string? CoverElement { get; set; }
    [JsonIgnore] public Vec2 P { get; init; }
    [JsonIgnore] public double Db { get; init; }
}

public sealed class SectionBarLine
{
    public required ElementRef Bar { get; init; }
    public string Size { get; init; } = "";
    public List<double[]> Points { get; init; } = new();
    [JsonIgnore] public List<Vec2> P { get; init; } = new();
}

public sealed class SectionDim
{
    public required string Kind { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public required Len Distance { get; init; }
    public double[]? At { get; init; }
}

public sealed class SectionResult
{
    public string Query { get; init; } = "section";
    public string Units { get; init; } = "in";
    public string Plane { get; init; } = "";
    public double[] Origin { get; init; } = Array.Empty<double>();
    public double[] Normal { get; init; } = Array.Empty<double>();
    public double[] U { get; init; } = Array.Empty<double>();
    public double[] V { get; init; } = Array.Empty<double>();
    public double? Station { get; init; }
    public string? StationText { get; init; }
    public double? Offset { get; init; }
    public List<SectionElement> Elements { get; init; } = new();
    public List<SectionBar> Bars { get; init; } = new();
    public List<SectionBarLine> BarsInPlane { get; init; } = new();
    public List<SectionDim>? Dims { get; set; }
    public List<string> Notes { get; init; } = new();
    public double? WindowU0 { get; init; }
    public double? WindowU1 { get; init; }
}

public sealed class SectionOptions
{
    /// <summary>Clip window in u / v (meters, relative to the plane origin); null = everything the plane cuts.</summary>
    public double? U0 { get; set; }
    public double? U1 { get; set; }
    public double? V0 { get; set; }
    public double? V1 { get; set; }
    public bool Bolts { get; set; }
    public bool Bars { get; set; } = true;
    public bool Dims { get; set; }
    public string? Bridge { get; set; }
    /// <summary>Only elements whose mark / name / category contains one of these (case-insensitive); empty = all.</summary>
    public List<string> Only { get; } = new();
}

/// <summary>
/// True section through every solid of the model (<see cref="MeshSection"/> per element, closed loops for hatching), drawn in the plane's own 2D frame;
/// bars crossing the plane are dots with their mark and size (and clear cover to the loop that holds them), bars lying in the plane are polylines.
/// </summary>
public static class ModelSection
{
    public static SectionResult Cut(MeasureModel mm, SectionPlane pl, SectionOptions o, double? station = null, double? offset = null)
    {
        var u = mm.Units;
        var notes = new List<string>();
        var els = new List<SectionElement>();
        double S(double v) => Fmt.Short(u, v);
        double[] P2(Vec2 q) => new[] { S(q.X), S(q.Y) };
        bool InWin(Vec2 q) => (o.U0 is not { } a || q.X >= a - 1e-9) && (o.U1 is not { } b || q.X <= b + 1e-9) && (o.V0 is not { } c || q.Y >= c - 1e-9) && (o.V1 is not { } d || q.Y <= d + 1e-9);
        bool Wanted(string mark, string cat, string desc) => o.Only.Count == 0 || o.Only.Any(x => mark.Contains(x, StringComparison.OrdinalIgnoreCase) || cat.Contains(x, StringComparison.OrdinalIgnoreCase) || desc.Contains(x, StringComparison.OrdinalIgnoreCase));
        var cands = mm.Elements(o.Bridge).Where(e => Crosses(pl, e.Bounds) && Wanted(e.Ref.Mark, e.Category, e.Description)).ToList();
        var cuts = new (MeasureElement E, List<(Vec2 A, Vec2 B)> Segs)[cands.Count];
        Parallel.For(0, cands.Count, i => cuts[i] = (cands[i], pl.Cut(cands[i].Mesh)));
        foreach (var (e, segsAll) in cuts)
        {
            if (segsAll.Count == 0) continue;
            var loops = MeshSection.Loops(segsAll, 1e-5).Select(l => Geo2.Simplify(l, 1e-6)).Where(l => l.Count >= 3).ToList();
            var inWin = loops.Where(l => l.All(InWin)).ToList();
            var segs = segsAll.Where(sg => InWin(sg.A) || InWin(sg.B)).ToList();
            if (segs.Count == 0) continue;
            var b = Box2.Empty;
            foreach (var (a, bb) in segs) { b.Add(a); b.Add(bb); }
            int inLoops = inWin.Sum(l => l.Count);
            els.Add(new SectionElement
            {
                Element = e.Ref, Category = e.Category, Description = e.Description, Concrete = e.IsConcrete, Min = P2(new Vec2(b.MinX, b.MinY)), Max = P2(new Vec2(b.MaxX, b.MaxY)),
                Width = Len.Of(b.Width), Height = Len.Of(b.Height), Segs = segs, OpenSegments = inWin.Count == 0 ? segs.Count : 0,
                Loops = inWin.Select(l => new SectionLoop
                {
                    AreaIn2 = Fmt.R(Math.Abs(Geo2.Area(l)) / (0.0254 * 0.0254), 3), P = l, Points = l.Select(P2).ToList(),
                    Min = P2(new Vec2(l.Min(q => q.X), l.Min(q => q.Y))), Max = P2(new Vec2(l.Max(q => q.X), l.Max(q => q.Y))),
                }).ToList(),
            });
        }
        // bolts
        if (o.Bolts)
            foreach (var m in mm.Filter(o.Bridge))
                if (m.Steel is { } s)
                    for (int i = 0; i < s.Bolts.Count; i++)
                    {
                        var bt = s.Bolts[i];
                        if (Math.Abs(pl.Dist(bt.Frame.Origin)) > bt.Length + 0.1) continue;
                        var mesh = MeasureModel.BoltWorldMesh(s, bt);
                        var segs = pl.Cut(mesh).Where(sg => InWin(sg.A) || InWin(sg.B)).ToList();
                        if (segs.Count == 0) continue;
                        var loops = MeshSection.Loops(segs, 1e-5).Where(l => l.Count >= 3).ToList();
                        var b = Box2.Empty; foreach (var (a, bb) in segs) { b.Add(a); b.Add(bb); }
                        els.Add(new SectionElement
                        {
                            Element = mm.Ref(m.Definition.Name, "bolt", $"bolt {i + 1}", bt.Assembly, i), Category = "Bolt", Description = $"{Fmt.FtIn(MeasureModel.BoltDiameter(s, bt))} bolt",
                            Min = P2(new Vec2(b.MinX, b.MinY)), Max = P2(new Vec2(b.MaxX, b.MaxY)), Width = Len.Of(b.Width), Height = Len.Of(b.Height), Segs = segs,
                            Loops = loops.Select(l => new SectionLoop { AreaIn2 = Fmt.R(Math.Abs(Geo2.Area(l)) / (0.0254 * 0.0254), 3), P = l, Points = l.Select(P2).ToList(), Min = P2(new Vec2(l.Min(q => q.X), l.Min(q => q.Y))), Max = P2(new Vec2(l.Max(q => q.X), l.Max(q => q.Y))) }).ToList(),
                        });
                    }
        // bars
        var dots = new List<SectionBar>();
        var lines = new List<SectionBarLine>();
        if (o.Bars)
            foreach (var bi in mm.Bars(o.Bridge))
            {
                if (!Crosses(pl, bi.Bounds, bi.Mark.Size.Diameter)) continue;
                if (o.Only.Count > 0 && !Wanted(bi.Mark.Mark, "bar", bi.Mark.Location)) continue;
                var pts = bi.Bar.Points;
                double r = bi.Mark.Size.Diameter / 2;
                var inPlane = new List<Vec2>();
                void FlushLine()
                {
                    if (inPlane.Count >= 2 && inPlane.Any(InWin)) lines.Add(new SectionBarLine { Bar = bi.Ref, Size = bi.Mark.Size.Name, P = inPlane.ToList(), Points = inPlane.Select(P2).ToList() });
                    inPlane.Clear();
                }
                for (int k = 0; k + 1 < pts.Length; k++)
                {
                    double d0 = pl.Dist(pts[k]), d1 = pl.Dist(pts[k + 1]);
                    bool lies = Math.Abs(d0) <= r && Math.Abs(d1) <= r && (pts[k + 1] - pts[k]).Length > 2 * r;
                    if (lies)
                    {
                        if (inPlane.Count == 0) inPlane.Add(pl.To2(pts[k]));
                        inPlane.Add(pl.To2(pts[k + 1]));
                        continue;
                    }
                    FlushLine();
                    if ((d0 > 0) == (d1 > 0) && d0 != 0 && d1 != 0) continue;
                    if (Math.Abs(d0 - d1) < 1e-12) continue;
                    double t = d0 / (d0 - d1);
                    if (t < 0 || t > 1 || (t == 1 && k + 2 < pts.Length)) continue;
                    var p = Vec3.Lerp(pts[k], pts[k + 1], t);
                    var q = pl.To2(p);
                    if (!InWin(q)) continue;
                    var dir = (pts[k + 1] - pts[k]).Normalized();
                    double ang = Math.Asin(Math.Min(1, Math.Abs(Vec3.Dot(dir, pl.Ahead)))) * 180 / Math.PI;
                    dots.Add(new SectionBar { Bar = bi.Ref, Size = bi.Mark.Size.Name, P = q, At = P2(q), AngleToPlaneDeg = Fmt.R(ang, 2), Db = bi.Mark.Size.Diameter });
                }
                FlushLine();
            }
        // cover of each dot to the concrete loop holding it
        var concLoops = els.Where(e => e.Concrete).SelectMany(e => e.Loops.Select(l => (e, l))).ToList();
        foreach (var d in dots)
        {
            foreach (var (e, l) in concLoops)
            {
                if (!Geo2.Inside(l.P, d.P)) continue;
                double best = double.MaxValue; Vec2 ea = default, eb = default;
                for (int i = 0; i < l.P.Count; i++)
                {
                    double dd = Geo2.SegDist(d.P, l.P[i], l.P[(i + 1) % l.P.Count]);
                    if (dd < best) { best = dd; ea = l.P[i]; eb = l.P[(i + 1) % l.P.Count]; }
                }
                // a loop of the union of touching solids can share an internal face: skip faces that another concrete loop continues across
                var mid = Geo2.Closest(d.P, ea, eb);
                var nrm = (mid - d.P).Length > 1e-9 ? (mid - d.P).Normalized() : new Vec2(0, 1);
                bool shared = concLoops.Any(x => !ReferenceEquals(x.l, l) && Geo2.Inside(x.l.P, mid + nrm * 0.002));
                if (shared) continue;
                double cover = best - d.Db / 2;
                if (d.Cover == null || cover < d.Cover.Value.In * 0.0254)
                {
                    d.Cover = Len.Of(cover);
                    d.CoverFace = nrm.Y > 0.7 ? "top" : nrm.Y < -0.7 ? "bottom" : nrm.X > 0 ? "side (+u)" : "side (-u)";
                    d.CoverElement = e.Element.Mark;
                }
            }
        }
        if (cands.Count == 0) notes.Add("no solid crosses the plane");
        var res = new SectionResult
        {
            Units = u.ShortUnitName, Plane = pl.Description, Origin = PlateMeasure.World(u, pl.Origin), Normal = Geo3.Arr(pl.Ahead), U = Geo3.Arr(pl.U), V = Geo3.Arr(pl.V),
            Station = station is { } st ? Fmt.Long(u, st) : null, StationText = station is { } st2 ? u.FormatStation(st2, 3) : null, Offset = offset is { } of ? Fmt.Long(u, of) : null,
            Elements = els.OrderBy(e => e.Element.Kind).ThenBy(e => e.Element.Mark, StringComparer.Ordinal).ToList(), Bars = dots.OrderBy(d => d.P.X).ToList(), BarsInPlane = lines, Notes = notes,
            WindowU0 = o.U0 is { } w0 ? S(w0) : null, WindowU1 = o.U1 is { } w1 ? S(w1) : null,
        };
        if (o.Dims) res.Dims = Dims(res, u);
        return res;
    }

    private static bool Crosses(SectionPlane pl, Box3 b, double grow = 0)
    {
        if (b.IsEmpty) return false;
        double lo = double.MaxValue, hi = double.MinValue;
        for (int i = 0; i < 8; i++)
        {
            var c = new Vec3((i & 1) == 0 ? b.Min.X : b.Max.X, (i & 2) == 0 ? b.Min.Y : b.Max.Y, (i & 4) == 0 ? b.Min.Z : b.Max.Z);
            double d = pl.Dist(c);
            lo = Math.Min(lo, d); hi = Math.Max(hi, d);
        }
        return lo <= grow && hi >= -grow;
    }

    /// <summary>Named features (element extents) and distances: clear gaps between elements stacked in v (overlapping in u), and side by side in u.</summary>
    public static List<SectionDim> Dims(SectionResult r, UnitSystem u)
    {
        var dims = new List<SectionDim>();
        double M(double s) => u.FromDisplay(s, LengthKind.Short);
        var items = r.Elements.Where(e => e.Loops.Count > 0 || e.Segs.Count > 0).Select(e =>
        {
            var b = Box2.Empty;
            foreach (var (a, bb) in e.Segs) { b.Add(a); b.Add(bb); }
            return (e, b);
        }).ToList();
        foreach (var (e, b) in items)
        {
            dims.Add(new SectionDim { Kind = "width", From = $"{e.Element.Mark}.left", To = $"{e.Element.Mark}.right", Distance = Len.Of(b.Width) });
            dims.Add(new SectionDim { Kind = "height", From = $"{e.Element.Mark}.bottom", To = $"{e.Element.Mark}.top", Distance = Len.Of(b.Height) });
        }
        // vertical: the nearest element above each element, among those overlapping it in u
        foreach (var (e, b) in items)
        {
            (SectionElement E, Box2 B)? best = null; double bd = double.MaxValue;
            foreach (var (f, c) in items)
            {
                if (ReferenceEquals(e, f)) continue;
                double ov = Math.Min(b.MaxX, c.MaxX) - Math.Max(b.MinX, c.MinX);
                if (ov <= 1e-4) continue;
                double gap = c.MinY - b.MaxY;
                if (gap < -1e-4 || c.MinY < b.MinY + 1e-4) continue;
                if (gap < bd) { bd = gap; best = (f, c); }
            }
            if (best is { } bb)
            {
                double x = (Math.Max(b.MinX, bb.B.MinX) + Math.Min(b.MaxX, bb.B.MaxX)) / 2;
                dims.Add(new SectionDim { Kind = "vertical clear", From = $"{e.Element.Mark}.top", To = $"{bb.E.Element.Mark}.bottom", Distance = Len.Of(Math.Max(0, bd)), At = new[] { Fmt.Short(u, x), Fmt.Short(u, b.MaxY) } });
            }
        }
        // horizontal: nearest element to the right overlapping in v
        foreach (var (e, b) in items)
        {
            (SectionElement E, Box2 B)? best = null; double bd = double.MaxValue;
            foreach (var (f, c) in items)
            {
                if (ReferenceEquals(e, f)) continue;
                double ov = Math.Min(b.MaxY, c.MaxY) - Math.Max(b.MinY, c.MinY);
                if (ov <= 1e-4) continue;
                double gap = c.MinX - b.MaxX;
                if (gap < -1e-4 || c.MinX < b.MinX + 1e-4) continue;
                if (gap < bd) { bd = gap; best = (f, c); }
            }
            if (best is { } bb && bd < 3)
                dims.Add(new SectionDim { Kind = "horizontal clear", From = $"{e.Element.Mark}.right", To = $"{bb.E.Element.Mark}.left", Distance = Len.Of(Math.Max(0, bd)) });
        }
        // bars: positions in the section and covers
        foreach (var bar in r.Bars.Where(x => x.Cover != null))
            dims.Add(new SectionDim { Kind = "bar cover", From = $"{bar.Bar.Mark} {bar.Size}", To = $"{bar.CoverElement} {bar.CoverFace}", Distance = bar.Cover!.Value, At = bar.At });
        return dims;
    }

    public static string ToText(SectionResult r)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Section {r.Plane}");
        sb.AppendLine($"  origin {r.Origin[0]:0.####}, {r.Origin[1]:0.####}, {r.Origin[2]:0.####}; looking along ({r.Normal[0]:0.#####}, {r.Normal[1]:0.#####}, {r.Normal[2]:0.#####}); u right ({r.U[0]:0.####}, {r.U[1]:0.####}, {r.U[2]:0.####}), v up ({r.V[0]:0.####}, {r.V[1]:0.####}, {r.V[2]:0.####})");
        sb.AppendLine($"  {r.Elements.Count} elements cut, {r.Bars.Count} bars crossing, {r.BarsInPlane.Count} bars in the plane (coordinates in {r.Units})");
        foreach (var e in r.Elements)
            sb.AppendLine($"    {e.Element.Kind,-8} {e.Element.Mark,-28} {e.Width.Text,-12} x {e.Height.Text,-12} u {e.Min[0]}..{e.Max[0]}  v {e.Min[1]}..{e.Max[1]}  loops {e.Loops.Count}{(e.OpenSegments > 0 ? $" (+{e.OpenSegments} open segments)" : "")}");
        foreach (var g in r.Bars.GroupBy(b => (b.Bar.Mark, b.Size)))
            sb.AppendLine($"    bar {g.Key.Mark} {g.Key.Size}: {g.Count()} dots" + (g.Any(x => x.Cover != null) ? $", min clear cover {Fmt.FtIn(g.Where(x => x.Cover != null).Min(x => x.Cover!.Value.In) * 0.0254)}" : ""));
        if (r.Dims != null) foreach (var d in r.Dims) sb.AppendLine($"    dim {d.Kind,-17} {d.From} -> {d.To}: {d.Distance.Text} ({d.Distance.Mm:0.0} mm)");
        foreach (var n in r.Notes) sb.AppendLine($"  note: {n}");
        return sb.ToString();
    }

    public static string Svg(SectionResult r)
    {
        var svg = new MeasureSvg();
        svg.NewPanel($"Section {r.Plane}");
        svg.Note($"{r.Elements.Count} elements, {r.Bars.Count} bar dots; looking along n = ({r.Normal[0]:0.####}, {r.Normal[1]:0.####}, {r.Normal[2]:0.####}); u right, v up in the plane");
        int ci = 0;
        foreach (var e in r.Elements)
        {
            string col = e.Concrete ? "#6b6355" : e.Element.Kind == "bolt" ? "#000" : MeasureSvg.Palette[ci++ % MeasureSvg.Palette.Length];
            foreach (var (a, b) in e.Segs) svg.Line(a, b, col, 1);
            foreach (var l in e.Loops) svg.Region(l.P, Array.Empty<IReadOnlyList<Vec2>>(), col, e.Concrete ? "#cfc8b8" : col, e.Concrete ? 0.55 : 0.25, 0.8);
            if (e.Loops.Count > 0 && e.Element.Kind != "bolt")
            {
                var big = e.Loops.OrderByDescending(l => l.AreaIn2).First();
                var c = Geo2.Centroid(big.P);
                if (big.AreaIn2 > 0.5) svg.Text(c, e.Element.Mark, 8, e.Concrete ? "#333" : col);
            }
        }
        foreach (var l in r.BarsInPlane) svg.Polyline(l.P, "#c60", 1.5);
        foreach (var d in r.Bars)
        {
            svg.Circle(d.P, d.Db / 2, "#a00", "#d22", 1.8, 0.5);
            svg.Text(d.P, $"{d.Bar.Mark}", 7, "#a00", "start", 4, -5);
        }
        return svg.Render();
    }

    /// <summary>DXF-ready drawing (meters, u/v as x/y): element cuts per layer, bar dots as circles with labels.</summary>
    public static PlanDrawing Drawing(SectionResult r)
    {
        var dwg = new PlanDrawing();
        var steel = new LayerStyle("SBM-SECTION-STEEL", 0, 140, 255, 5);
        var conc = new LayerStyle("SBM-SECTION-CONCRETE", 180, 170, 150, 8);
        var bars = new LayerStyle("SBM-SECTION-BARS", 255, 60, 60, 1);
        var text = new LayerStyle("SBM-SECTION-TEXT", 230, 230, 230, 7);
        foreach (var e in r.Elements)
        {
            var layer = dwg.Layer(e.Concrete ? conc : steel);
            if (e.Loops.Count > 0) foreach (var l in e.Loops) layer.Poly(l.P.Select(p => p.WithZ(0)), true);
            else foreach (var (a, b) in e.Segs) layer.Line(a.WithZ(0), b.WithZ(0));
        }
        foreach (var d in r.Bars)
        {
            dwg.Layer(bars).Circles.Add(new CircleItem(d.P.WithZ(0), d.Db / 2, true));
            dwg.Layer(text).Texts.Add(new TextItem(new Vec3(d.P.X + d.Db, d.P.Y + d.Db, 0), d.Bar.Mark, 0.02, 0, TextAlign.Left));
        }
        foreach (var l in r.BarsInPlane) dwg.Layer(bars).Poly(l.P.Select(p => p.WithZ(0)));
        return dwg;
    }

    public static void WriteCsv(string path, SectionResult r)
    {
        using var w = new StreamWriter(path);
        w.WriteLine($"kind,element,mark,assembly,global_id,loop,u_{r.Units},v_{r.Units}");
        foreach (var e in r.Elements)
            for (int k = 0; k < e.Loops.Count; k++)
                foreach (var p in e.Loops[k].Points)
                    w.WriteLine(string.Join(",", e.Element.Kind, Q(e.Category), Q(e.Element.Mark), Q(e.Element.Assembly ?? ""), e.Element.GlobalId ?? "", k + 1, p[0].ToString(Fmt.Inv), p[1].ToString(Fmt.Inv)));
        foreach (var b in r.Bars)
            w.WriteLine(string.Join(",", "bar", Q(b.Size), Q(b.Bar.Mark), Q(b.Bar.Unit ?? b.Bar.Assembly ?? ""), b.Bar.GlobalId ?? "", 0, b.At[0].ToString(Fmt.Inv), b.At[1].ToString(Fmt.Inv)));
        static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
