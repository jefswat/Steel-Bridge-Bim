using SBM.Core.Geometry;

namespace SBM.Core.Prestressed;

/// <summary>One piece of an exact section outline: a straight line or a circular arc (center, counter-clockwise flag).</summary>
public readonly record struct ProfileSegment(Vec2 Start, Vec2 End, Vec2? Center = null, bool Ccw = true)
{
    public bool IsArc => Center != null;
    public double Radius => Center is { } c ? (Start - c).Length : 0;

    /// <summary>Signed sweep (radians, + = counter-clockwise) of an arc; 0 for a line.</summary>
    public double Sweep
    {
        get
        {
            if (Center is not { } c) return 0;
            double a0 = (Start - c).Angle, a1 = (End - c).Angle;
            double d = a1 - a0;
            if (Ccw) { while (d <= 0) d += 2 * Math.PI; }
            else { while (d >= 0) d -= 2 * Math.PI; }
            return d;
        }
    }

    /// <summary>Point at parameter t in [0, 1] (by angle on arcs).</summary>
    public Vec2 PointAt(double t)
    {
        if (Center is not { } c) return Vec2.Lerp(Start, End, t);
        double a0 = (Start - c).Angle;
        return c + Vec2.FromAngle(a0 + Sweep * t) * Radius;
    }

    public Vec2 Mid => PointAt(0.5);
    public double Length => IsArc ? Math.Abs(Sweep) * Radius : (End - Start).Length;
    public ProfileSegment Reversed() => new(End, Start, Center, !Ccw);
}

/// <summary>Corner treatment of a sharp outline vertex: none, a tangent radius fillet (concave or convex), or a chamfer
/// (equal legs measured along both faces from the sharp intersection point).</summary>
public enum CornerKind { Sharp, Fillet, Chamfer }

/// <summary>A vertex of the THEORETICAL (sharp) outline, i.e. where the dimensioned faces intersect, and its treatment.</summary>
public readonly record struct ProfileCorner(Vec2 Point, CornerKind Kind = CornerKind.Sharp, double Size = 0)
{
    public static ProfileCorner Sharp(double x, double y) => new(new Vec2(x, y));
    public static ProfileCorner Fillet(double x, double y, double r) => new(new Vec2(x, y), CornerKind.Fillet, r);
    public static ProfileCorner Chamfer(double x, double y, double c) => new(new Vec2(x, y), CornerKind.Chamfer, c);
    public ProfileCorner Scaled(double s) => new(Point * s, Kind, Size * s);
}

/// <summary>
/// Exact closed section outline (counter-clockwise, x right of the CL, y up from the bottom) made of lines and TRUE circular
/// arcs. Built from the sharp outline of the standard drawing plus the corner radii / chamfers
/// (<see cref="FromCorners"/>: the printed dimensions of the state standards run to the intersection points of the faces,
/// the fillets are tangent to both faces). Section properties are integrated exactly over the arcs.
/// </summary>
public sealed class SectionProfile
{
    public IReadOnlyList<ProfileSegment> Segments { get; }
    public IReadOnlyList<ProfileCorner> Corners { get; }

    public SectionProfile(IReadOnlyList<ProfileSegment> segments, IReadOnlyList<ProfileCorner>? corners = null)
    {
        Segments = segments;
        Corners = corners ?? Array.Empty<ProfileCorner>();
    }

    /// <summary>
    /// Outline from the sharp polygon (counter-clockwise) and per-corner fillets / chamfers. Fillet: tangent distance
    /// R / tan(a/2) along both faces (a = angle between the faces), center on the bisector; convex corners turn
    /// counter-clockwise, re-entrant corners clockwise. Throws when two treatments overlap on one face.
    /// </summary>
    public static SectionProfile FromCorners(IReadOnlyList<ProfileCorner> corners)
    {
        int n = corners.Count;
        if (n < 3) throw new ArgumentException("A section outline needs at least three corners.");
        var tin = new Vec2[n];
        var tout = new Vec2[n];
        var arcs = new ProfileSegment?[n];
        var used = new double[n, 2]; // tangent length taken on the incoming / outgoing face
        for (int i = 0; i < n; i++)
        {
            var c = corners[i];
            var p = c.Point;
            var prev = corners[(i - 1 + n) % n].Point;
            var next = corners[(i + 1) % n].Point;
            var u1 = (prev - p).Normalized();
            var u2 = (next - p).Normalized();
            tin[i] = tout[i] = p;
            if (c.Kind == CornerKind.Sharp || c.Size <= 0) continue;
            double cosA = Math.Clamp(Vec2.Dot(u1, u2), -1, 1);
            double a = Math.Acos(cosA);
            if (a < 1e-6 || Math.PI - a < 1e-6) continue; // straight or folded: nothing to round
            double d = c.Kind == CornerKind.Chamfer ? c.Size : c.Size / Math.Tan(a / 2);
            tin[i] = p + u1 * d;
            tout[i] = p + u2 * d;
            used[i, 0] = used[i, 1] = d;
            if (c.Kind == CornerKind.Fillet)
            {
                var bis = (u1 + u2).Normalized();
                var center = p + bis * (c.Size / Math.Sin(a / 2));
                bool left = Vec2.Cross(p - prev, next - p) > 0; // convex corner of a CCW outline
                arcs[i] = new ProfileSegment(tin[i], tout[i], center, left);
            }
        }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            double face = (corners[j].Point - corners[i].Point).Length;
            if (used[i, 1] + used[j, 0] > face + 1e-9)
                throw new ArgumentException($"Corner treatments at {corners[i].Point} and {corners[j].Point} overlap on their common face.");
        }
        var segs = new List<ProfileSegment>();
        for (int i = 0; i < n; i++)
        {
            if (arcs[i] is { } arc) segs.Add(arc);
            else if ((tout[i] - tin[i]).Length > 1e-12) segs.Add(new ProfileSegment(tin[i], tout[i]));
            int j = (i + 1) % n;
            if ((tin[j] - tout[i]).Length > 1e-12) segs.Add(new ProfileSegment(tout[i], tin[j]));
        }
        return new SectionProfile(segs, corners.ToArray());
    }

    /// <summary>Scales every coordinate (e.g. inches → meters).</summary>
    public SectionProfile Scaled(double s) =>
        new(Segments.Select(g => new ProfileSegment(g.Start * s, g.End * s, g.Center is { } c ? c * s : null, g.Ccw)).ToArray(),
            Corners.Select(c => c.Scaled(s)).ToArray());

    /// <summary>Polygon of the outline: arcs split into pieces of at most <paramref name="maxAngleDeg"/> degrees (end points on the arc).</summary>
    public Vec2[] Tessellate(double maxAngleDeg = 7.5)
    {
        var pts = new List<Vec2>();
        foreach (var g in Segments)
        {
            pts.Add(g.Start);
            if (!g.IsArc) continue;
            int k = Math.Max(2, (int)Math.Ceiling(Math.Abs(g.Sweep) / (maxAngleDeg * Math.PI / 180)));
            for (int m = 1; m < k; m++) pts.Add(g.PointAt((double)m / k));
        }
        return pts.ToArray();
    }

    /// <summary>Exact section properties of the outline (units of the coordinates): area, centroid (x, y), Ix and Iy about the
    /// centroidal axes. Green's theorem with 12-point Gauss-Legendre on every segment (exact for lines and, to round-off, arcs).</summary>
    public (double Area, double Xc, double Yc, double Ix, double Iy) Properties()
    {
        double a = 0, sx = 0, sy = 0, ixx = 0, iyy = 0; // ∫dA, ∫x dA, ∫y dA, ∫y² dA, ∫x² dA
        foreach (var g in Segments)
        {
            int pieces = g.IsArc ? Math.Max(1, (int)Math.Ceiling(Math.Abs(g.Sweep) / (Math.PI / 8))) : 1;
            for (int k = 0; k < pieces; k++)
            {
                double t0 = (double)k / pieces, t1 = (double)(k + 1) / pieces;
                for (int q = 0; q < Gl.Length; q++)
                {
                    double t = t0 + (t1 - t0) * (Gl[q].x + 1) / 2;
                    double w = Gl[q].w * (t1 - t0) / 2;
                    var p = g.PointAt(t);
                    // derivative by central difference of the exact parametrization (smooth trig / linear): use analytic
                    Vec2 dp;
                    if (g.Center is { } c)
                    {
                        var r = p - c;
                        dp = new Vec2(-r.Y, r.X) * g.Sweep;
                    }
                    else dp = g.End - g.Start;
                    double x = p.X, y = p.Y, dy = dp.Y * w, dx = dp.X * w;
                    a += x * dy;               // ∮ x dy
                    sx += x * x / 2 * dy;      // ∮ x²/2 dy  = ∫∫ x dA
                    sy += -y * y / 2 * dx;     // −∮ y²/2 dx = ∫∫ y dA
                    ixx += -y * y * y / 3 * dx; // −∮ y³/3 dx = ∫∫ y² dA
                    iyy += x * x * x / 3 * dy;  // ∮ x³/3 dy  = ∫∫ x² dA
                }
            }
        }
        double xc = sx / a, yc = sy / a;
        return (a, xc, yc, ixx - a * yc * yc, iyy - a * xc * xc);
    }

    public double Width => Segments.Max(g => Math.Max(g.Start.X, g.End.X)) - Segments.Min(g => Math.Min(g.Start.X, g.End.X));
    public double Height => Segments.Max(g => Math.Max(g.Start.Y, g.End.Y)) - Segments.Min(g => Math.Min(g.Start.Y, g.End.Y));

    // 12-point Gauss-Legendre nodes / weights on [-1, 1].
    private static readonly (double x, double w)[] Gl =
    {
        (-0.9815606342467192, 0.0471753363865118), (-0.9041172563704749, 0.1069393259953184),
        (-0.7699026741943047, 0.1600783285433462), (-0.5873179542866175, 0.2031674267230659),
        (-0.3678314989981802, 0.2334925365383548), (-0.1252334085114689, 0.2491470458134028),
        (0.1252334085114689, 0.2491470458134028), (0.3678314989981802, 0.2334925365383548),
        (0.5873179542866175, 0.2031674267230659), (0.7699026741943047, 0.1600783285433462),
        (0.9041172563704749, 0.1069393259953184), (0.9815606342467192, 0.0471753363865118),
    };
}
