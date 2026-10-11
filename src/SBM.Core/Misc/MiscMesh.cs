using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Misc;

/// <summary>Small solid builders and distance queries for the misc items.</summary>
public static class MiscMesh
{
    /// <summary>Two unit vectors perpendicular to the axis (u x v = axis).</summary>
    public static (Vec3 u, Vec3 v) Perp(Vec3 axis)
    {
        var d = axis.Normalized();
        var seed = Math.Abs(d.Z) < 0.9 ? new Vec3(0, 0, 1) : new Vec3(1, 0, 0);
        var u = Vec3.Cross(seed, d).Normalized();
        var v = Vec3.Cross(d, u);
        return (u, v);
    }

    /// <summary>Hollow tube (outer / inner radius) between two axis points. Recorded as an exact extrusion with a round hole.</summary>
    public static Mesh Tube(Vec3 a, Vec3 b, double rOut, double rIn, int n = 24)
    {
        var m = new Mesh();
        var axis = b - a;
        if (axis.Length < 1e-6) return m;
        var (u, v) = Perp(axis);
        Vec3 P(Vec3 c, double r, int i) { double t = 2 * Math.PI * i / n; return c + u * (r * Math.Cos(t)) + v * (r * Math.Sin(t)); }
        int tri0 = m.TriangleCount;
        int[,] id = new int[4, n];
        for (int i = 0; i < n; i++)
        {
            id[0, i] = m.Add(P(a, rOut, i)); id[1, i] = m.Add(P(b, rOut, i));
            id[2, i] = m.Add(P(a, rIn, i)); id[3, i] = m.Add(P(b, rIn, i));
        }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            // outer side (outward), inner side (inward), caps
            m.Tri(id[0, i], id[0, j], id[1, j]); m.Tri(id[0, i], id[1, j], id[1, i]);
            m.Tri(id[2, i], id[3, j], id[2, j]); m.Tri(id[2, i], id[3, i], id[3, j]);
            m.Tri(id[1, i], id[1, j], id[3, j]); m.Tri(id[1, i], id[3, j], id[3, i]);
            m.Tri(id[0, i], id[2, j], id[0, j]); m.Tri(id[0, i], id[2, i], id[2, j]);
        }
        var bottom = Enumerable.Range(0, n).Select(i => P(a, rOut, i)).ToArray();
        var top = Enumerable.Range(0, n).Select(i => P(b, rOut, i)).ToArray();
        m.Describe(ExtrudedSolid.TryCreate(bottom, top, new[] { (a, 2 * rIn) }), tri0);
        return m;
    }

    /// <summary>Solid cylinder between two axis points.</summary>
    public static Mesh Cylinder(Vec3 a, Vec3 b, double r, int n = 24)
    {
        var m = new Mesh();
        var axis = b - a;
        if (axis.Length < 1e-6) return m;
        var (u, v) = Perp(axis);
        var sec = Enumerable.Range(0, n).Select(i => new Vec2(Math.Cos(2 * Math.PI * i / n), Math.Sin(2 * Math.PI * i / n)) * r).ToArray();
        Vec3[] Ring(Vec3 c) => sec.Select(q => c + u * q.X + v * q.Y).ToArray();
        MeshBuilder.AddLoft(m, sec, new[] { Ring(a), Ring(b) });
        return m;
    }

    /// <summary>Frame at a plan point with x along <paramref name="dir"/>, z up (y to the left of dir).</summary>
    public static Frame3 Plan(Vec2 p, Vec2 dir, double z = 0) =>
        new(p.WithZ(z), new Vec3(dir.X, dir.Y, 0), new Vec3(-dir.Y, dir.X, 0), new Vec3(0, 0, 1));

    /// <summary>Polygon (in the frame's x = along dir, y = world Z) extruded +-halfWidth across (frame y direction). Frame from <see cref="Plan"/> with z = 0.</summary>
    public static Mesh VerticalSection(Vec2 p, Vec2 dir, IReadOnlyList<Vec2> xz, double halfWidth)
    {
        var m = new Mesh();
        var d = new Vec3(dir.X, dir.Y, 0);
        var side = new Vec3(-dir.Y, dir.X, 0);
        var o = p.WithZ(0);
        Vec3[] Ring(double w) => xz.Select(q => o + d * q.X + new Vec3(0, 0, q.Y) + side * w).ToArray();
        MeshBuilder.AddLoft(m, xz, new[] { Ring(-halfWidth), Ring(halfWidth) });
        return m;
    }

    /// <summary>Quad-section strip (width across, thickness down) swept between two 3D points.</summary>
    public static Mesh Strip(Vec3 a, Vec3 b, Vec3 across, double width, double thickness)
    {
        var m = new Mesh();
        var sec = new[] { new Vec2(-width / 2, 0), new Vec2(width / 2, 0), new Vec2(width / 2, -thickness), new Vec2(-width / 2, -thickness) };
        Vec3[] Ring(Vec3 c) => sec.Select(q => c + across * q.X + new Vec3(0, 0, q.Y)).ToArray();
        MeshBuilder.AddLoft(m, sec, new[] { Ring(a), Ring(b) });
        return m;
    }

    /// <summary>ASTM A53 / ASME B36.10 Schedule 40 outside diameter and wall for a nominal size (meters).</summary>
    public static (double od, double wall) PipeSch40(double nominal)
    {
        double n = nominal / 0.0254;
        (double od, double wall) = n switch
        {
            <= 2.5 => (2.375, 0.154),
            <= 4.5 => (4.5, 0.237),
            <= 6.5 => (6.625, 0.280),
            <= 8.5 => (8.625, 0.322),
            <= 10.5 => (10.75, 0.365),
            <= 12.5 => (12.75, 0.406),
            _ => (n + 0.75, 0.5),
        };
        return (In(od), In(wall));
    }

    /// <summary>Steel parts with their bounds (bolts are not parts), for repeated clearance queries.</summary>
    public static List<(SteelPart part, Box3 box)> Index(IEnumerable<SteelPart> parts) =>
        parts.Where(p => !p.Mesh.IsEmpty && p.Category != PartCategory.Weld).Select(p => (p, p.Mesh.Bounds())).ToList();

    // ---------------------------------------------------------------- distances

    /// <summary>Closest point of triangle abc to p (Ericson, Real-Time Collision Detection).</summary>
    public static Vec3 ClosestOnTriangle(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
    {
        var ab = b - a; var ac = c - a; var ap = p - a;
        double d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return a;
        var bp = p - b; double d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return b;
        double vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
        var cp = p - c; double d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return c;
        double vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
        double va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        double den = 1 / (va + vb + vc);
        return a + ab * (vb * den) + ac * (vc * den);
    }

    /// <summary>Minimum distance from the segment p0-p1 to triangle abc (convex in the segment parameter: ternary search) and where on the segment.</summary>
    public static (double dist, Vec3 at) SegmentTriangle(Vec3 p0, Vec3 p1, Vec3 a, Vec3 b, Vec3 c)
    {
        double lo = 0, hi = 1;
        double F(double t) { var q = Vec3.Lerp(p0, p1, t); return (q - ClosestOnTriangle(q, a, b, c)).Length; }
        for (int i = 0; i < 40; i++)
        {
            double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
            if (F(m1) < F(m2)) hi = m2; else lo = m1;
        }
        double t0 = (lo + hi) / 2;
        // the ternary search can miss a minimum at an end of the segment
        double best = F(t0), bt = t0;
        foreach (double t in new[] { 0.0, 1.0 }) { double f = F(t); if (f < best) { best = f; bt = t; } }
        return (best, Vec3.Lerp(p0, p1, bt));
    }

    /// <summary>Clearance (surface to surface) of a straight pipe of radius <paramref name="r"/> along p0-p1 to the steel parts: the nearest part and its triangle distance minus r.</summary>
    public static (double clearance, string against, Vec3 at) Clearance(IReadOnlyList<(SteelPart part, Box3 box)> parts, Vec3 p0, Vec3 p1, double r, double search = 0.75)
    {
        double lox = Math.Min(p0.X, p1.X) - r - search, hix = Math.Max(p0.X, p1.X) + r + search;
        double loy = Math.Min(p0.Y, p1.Y) - r - search, hiy = Math.Max(p0.Y, p1.Y) + r + search;
        double loz = Math.Min(p0.Z, p1.Z) - r - search, hiz = Math.Max(p0.Z, p1.Z) + r + search;
        double best = double.MaxValue;
        string against = "";
        Vec3 at = p0;
        foreach (var (part, bb) in parts)
        {
            if (part.Mesh.IsEmpty) continue;
            if (bb.Max.X < lox || bb.Min.X > hix || bb.Max.Y < loy || bb.Min.Y > hiy || bb.Max.Z < loz || bb.Min.Z > hiz) continue;
            var pos = part.Mesh.Positions; var idx = part.Mesh.Indices;
            for (int i = 0; i < idx.Count; i += 3)
            {
                var a = pos[idx[i]]; var b = pos[idx[i + 1]]; var c = pos[idx[i + 2]];
                if (Math.Max(a.X, Math.Max(b.X, c.X)) < lox || Math.Min(a.X, Math.Min(b.X, c.X)) > hix) continue;
                if (Math.Max(a.Y, Math.Max(b.Y, c.Y)) < loy || Math.Min(a.Y, Math.Min(b.Y, c.Y)) > hiy) continue;
                if (Math.Max(a.Z, Math.Max(b.Z, c.Z)) < loz || Math.Min(a.Z, Math.Min(b.Z, c.Z)) > hiz) continue;
                var (d, q) = SegmentTriangle(p0, p1, a, b, c);
                if (d < best) { best = d; at = q; against = $"{part.Mark} ({part.Category})"; }
            }
        }
        return best == double.MaxValue ? (double.NaN, "", at) : (best - r, against, at);
    }
}
