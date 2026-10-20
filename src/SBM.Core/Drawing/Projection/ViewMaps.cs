using SBM.Core.Geometry;

namespace SBM.Core.Drawing.Projection;

/// <summary>
/// World -> view mapping of a projection: (x, y) on the drawing, depth along the line of sight (away from the viewer). Orthographic
/// views are linear; a developed view (x = station / distance along a path) is not, and the projector then subdivides edges.
/// </summary>
public interface IViewMap
{
    /// <summary>(x, y, depth) of a world point.</summary>
    Vec3 Map(Vec3 world);
    /// <summary>World direction of increasing depth at a point (silhouette test).</summary>
    Vec3 ViewDir(Vec3 world);
    bool IsLinear { get; }
    /// <summary>Depth slab: only what lies between these depths is drawn (null = unbounded). The near plane cuts solids like a section.</summary>
    double? Near { get; }
    double? Far { get; }
    string Describe { get; }
}

/// <summary>
/// Orthographic view: origin, view direction (from the eye into the scene) and up. Drawing x = <c>Direction x Up</c> (to the right),
/// y = up made normal to the direction, depth = distance from the origin along the direction.
/// </summary>
public sealed class OrthographicView : IViewMap
{
    public Vec3 Origin { get; }
    public Vec3 Direction { get; }
    public Vec3 XAxis { get; }
    public Vec3 YAxis { get; }
    public double? Near { get; init; }
    public double? Far { get; init; }
    public bool IsLinear => true;

    public OrthographicView(Vec3 origin, Vec3 direction, Vec3 up)
    {
        Origin = origin;
        Direction = direction.Normalized();
        var y = up - Direction * Vec3.Dot(up, Direction);
        if (y.Length < 1e-9)
        {
            // Up along the view direction: pick a world axis.
            var alt = Math.Abs(Direction.Z) < 0.9 ? new Vec3(0, 0, 1) : new Vec3(0, 1, 0);
            y = alt - Direction * Vec3.Dot(alt, Direction);
        }
        YAxis = y.Normalized();
        XAxis = Vec3.Cross(Direction, YAxis).Normalized();
    }

    public Vec3 Map(Vec3 p)
    {
        var d = p - Origin;
        return new Vec3(Vec3.Dot(d, XAxis), Vec3.Dot(d, YAxis), Vec3.Dot(d, Direction));
    }

    public Vec3 ViewDir(Vec3 world) => Direction;

    /// <summary>World point of a view point (x, y) at a depth.</summary>
    public Vec3 Unmap(Vec3 v) => Origin + XAxis * v.X + YAxis * v.Y + Direction * v.Z;

    public string Describe => $"orthographic, direction ({Direction.X:0.###}, {Direction.Y:0.###}, {Direction.Z:0.###}), up ({YAxis.X:0.###}, {YAxis.Y:0.###}, {YAxis.Z:0.###})" +
        (Near != null || Far != null ? $", depth {Near?.ToString("0.###") ?? "-inf"} .. {Far?.ToString("0.###") ?? "inf"} m" : "");

    /// <summary>Top view (plan): looking down, north up, x = east; depth 0 at <paramref name="z"/> (output Z with KeepDepth = elevation - z).</summary>
    public static OrthographicView Top(double z = 0) => new(new Vec3(0, 0, z), new Vec3(0, 0, -1), new Vec3(0, 1, 0));
    /// <summary>Front view: looking north (+Y), x = east, y = elevation.</summary>
    public static OrthographicView Front() => new(Vec3.Zero, new Vec3(0, 1, 0), new Vec3(0, 0, 1));
    /// <summary>Left view (from the west): looking east (+X), x = south, y = elevation.</summary>
    public static OrthographicView Left() => new(Vec3.Zero, new Vec3(1, 0, 0), new Vec3(0, 0, 1));

    /// <summary>Same view with a depth slab.</summary>
    public OrthographicView WithSlab(double? near, double? far) => new(Origin, Direction, YAxis) { Near = near, Far = far };
}

/// <summary>
/// Developed elevation: x = station (or distance) along a plan path (alignment / girder web line), y = elevation, depth = horizontal
/// distance from the path away from the viewer. Viewer on the RIGHT of the path (stations increase to the right) unless
/// <see cref="FromLeft"/> (then x = -station, a true view from the left).
/// </summary>
public sealed class DevelopedView : IViewMap
{
    private readonly Vec2[] _p;
    private readonly double[] _s;
    private readonly double _cell;
    private readonly Vec2 _o;
    private readonly Vec2[] _n;
    private readonly Dictionary<(int, int), List<int>> _grid = new();
    public bool FromLeft { get; init; }
    public double? Near { get; init; }
    public double? Far { get; init; }
    public bool IsLinear => false;
    public string Name { get; init; } = "path";

    /// <param name="path">Plan points of the path (Z ignored), in order.</param>
    /// <param name="stations">x value at each point (station or distance), increasing.</param>
    public DevelopedView(IReadOnlyList<Vec3> path, IReadOnlyList<double> stations)
    {
        if (path.Count < 2 || stations.Count != path.Count) throw new ArgumentException("A developed view needs a path of at least two points with one station each.");
        var pts = new List<Vec2>();
        var ss = new List<double>();
        for (int i = 0; i < path.Count; i++)
        {
            if (pts.Count > 0 && (path[i].XY - pts[^1]).Length < 1e-6) continue;
            pts.Add(path[i].XY);
            ss.Add(stations[i]);
        }
        if (pts.Count < 2) throw new ArgumentException("Degenerate developed-view path.");
        _p = pts.ToArray();
        _s = ss.ToArray();
        _o = _p[0];
        _n = new Vec2[_p.Length];
        for (int i = 0; i < _p.Length; i++)
        {
            var nb = i > 0 ? (_p[i] - _p[i - 1]).Normalized().Right : Vec2.Zero;
            var na = i + 1 < _p.Length ? (_p[i + 1] - _p[i]).Normalized().Right : Vec2.Zero;
            var m = nb + na;
            _n[i] = m.Length < 1e-9 ? (na.Length > 0 ? na : nb) : m.Normalized();
        }
        double len = 0;
        for (int i = 1; i < _p.Length; i++) len += (_p[i] - _p[i - 1]).Length;
        _cell = Math.Max(1.0, len / Math.Max(1, _p.Length - 1) * 4);
        for (int i = 0; i + 1 < _p.Length; i++)
        {
            var a = _p[i] - _o; var b = _p[i + 1] - _o;
            int x0 = (int)Math.Floor(Math.Min(a.X, b.X) / _cell), x1 = (int)Math.Floor(Math.Max(a.X, b.X) / _cell);
            int y0 = (int)Math.Floor(Math.Min(a.Y, b.Y) / _cell), y1 = (int)Math.Floor(Math.Max(a.Y, b.Y) / _cell);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    if (!_grid.TryGetValue((x, y), out var l)) _grid[(x, y)] = l = new List<int>();
                    l.Add(i);
                }
        }
    }

    private DevelopedView(DevelopedView o)
    {
        _p = o._p; _s = o._s; _cell = o._cell; _o = o._o; _n = o._n; _grid = o._grid;
        FromLeft = o.FromLeft; Near = o.Near; Far = o.Far; Name = o.Name;
    }

    /// <summary>Same path with a depth slab (and optionally the other side / another name).</summary>
    public DevelopedView WithSlab(double? near, double? far, bool? fromLeft = null, string? name = null) =>
        new(this) { Near = near, Far = far, FromLeft = fromLeft ?? FromLeft, Name = name ?? Name };

    /// <summary>Closest path segment and parameter (the end segments extend beyond the path ends).</summary>
    private (int seg, double t, double dist) Closest(Vec2 q)
    {
        var l = q - _o;
        int cx = (int)Math.Floor(l.X / _cell), cy = (int)Math.Floor(l.Y / _cell);
        int best = -1; double bt = 0, bd = double.MaxValue;
        void Test(int i)
        {
            var a = _p[i]; var ab = _p[i + 1] - a;
            double t = Vec2.Dot(q - a, ab) / ab.LengthSquared;
            double tc = Math.Clamp(t, i == 0 ? double.NegativeInfinity : 0, i == _p.Length - 2 ? double.PositiveInfinity : 1);
            double d = (q - (a + ab * tc)).Length;
            if (d < bd) { bd = d; best = i; bt = tc; }
        }
        for (int r = 0; r < 64; r++)
        {
            for (int x = cx - r; x <= cx + r; x++)
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)) != r) continue;
                    if (_grid.TryGetValue((x, y), out var segs)) foreach (int i in segs) Test(i);
                }
            if (best >= 0 && bd < r * _cell) break;
        }
        if (best < 0 || bd > 63 * _cell)
            for (int i = 0; i + 1 < _p.Length; i++) Test(i);
        return (best, bt, bd);
    }

    /// <summary>Station parameter on segment i of the line through q along the normal interpolated between the vertex (miter) normals,
    /// so points on the radial line through a vertex map exactly to its station (a chorded arc develops like the arc).</summary>
    private (double t, double off) OnSegment(int i, Vec2 q, double tGuess)
    {
        var a = _p[i]; var e = _p[i + 1] - a;
        var n0 = _n[i]; var dn = _n[i + 1] - n0;
        var w = q - a;
        // cross(w - t e, n0 + t dn) = 0  ->  c2 t^2 + c1 t + c0 = 0
        double c0 = Vec2.Cross(w, n0), c1 = Vec2.Cross(w, dn) - Vec2.Cross(e, n0), c2 = -Vec2.Cross(e, dn);
        double t = tGuess;
        if (Math.Abs(c2) < 1e-14) { if (Math.Abs(c1) > 1e-14) t = -c0 / c1; }
        else
        {
            double disc = c1 * c1 - 4 * c2 * c0;
            if (disc >= 0)
            {
                double sq = Math.Sqrt(disc);
                double r1 = (-c1 + sq) / (2 * c2), r2 = (-c1 - sq) / (2 * c2);
                t = Math.Abs(r1 - tGuess) < Math.Abs(r2 - tGuess) ? r1 : r2;
            }
        }
        if (i > 0) t = Math.Max(t, -0.5);
        if (i < _p.Length - 2) t = Math.Min(t, 1.5);
        var n = (n0 + dn * t).Normalized();
        return (t, Vec2.Dot(q - (a + e * t), n));
    }

    public Vec3 Map(Vec3 w)
    {
        var (i, t0, _) = Closest(w.XY);
        var (t, off) = OnSegment(i, w.XY, t0);
        double s = _s[i] + (_s[i + 1] - _s[i]) * t;
        return FromLeft ? new Vec3(-s, w.Z, off) : new Vec3(s, w.Z, -off);
    }

    public Vec3 ViewDir(Vec3 w)
    {
        var (i, _, _) = Closest(w.XY);
        var dir = (_p[i + 1] - _p[i]).Normalized();
        var n = FromLeft ? dir.Right : dir.Left;   // from the viewer into the scene
        return new Vec3(n.X, n.Y, 0);
    }

    public string Describe => $"developed along {Name} ({(FromLeft ? "from the left" : "from the right")})" +
        (Near != null || Far != null ? $", depth {Near?.ToString("0.###") ?? "-inf"} .. {Far?.ToString("0.###") ?? "inf"} m" : "");

    /// <summary>Developed view along an alignment: x = station, sampled every <paramref name="step"/> m between two stations.</summary>
    public static DevelopedView Along(Alignments.Alignment al, double station0, double station1, double step = 0.5, double margin = 30)
    {
        double s0 = Math.Max(al.StartStation, Math.Min(station0, station1) - margin), s1 = Math.Min(al.EndStation, Math.Max(station0, station1) + margin);
        int n = Math.Max(2, (int)Math.Ceiling((s1 - s0) / step) + 1);
        var pts = new List<Vec3>(); var st = new List<double>();
        for (int i = 0; i < n; i++)
        {
            double s = s0 + (s1 - s0) * i / (n - 1);
            pts.Add(al.PointAt(s).WithZ(0));
            st.Add(s);
        }
        return new DevelopedView(pts, st) { Name = al.Name };
    }

    /// <summary>Developed view along a girder web line: x = distance along the girder from its start end (as the girder elevation sheets).</summary>
    public static DevelopedView AlongGirder(Framing.GirderResult g, double step = 0.5)
    {
        var path = g.Path;
        int n = Math.Max(2, (int)Math.Ceiling(path.Length / step) + 1);
        var pts = new List<Vec3>(); var st = new List<double>();
        // Extend 3 m beyond the ends along the end tangents (girder end plates, bearings).
        for (int i = -6; i < n + 6; i++)
        {
            double s = path.Length * Math.Clamp(i, 0, n - 1) / (n - 1);
            var (p, tan, _) = path.At(s);
            double ext = i < 0 ? i * step : i > n - 1 ? (i - n + 1) * step : 0;
            pts.Add((p.XY + tan.Normalized() * ext).WithZ(0));
            st.Add(s + ext);
        }
        return new DevelopedView(pts, st) { Name = $"girder {g.Name}" };
    }
}
