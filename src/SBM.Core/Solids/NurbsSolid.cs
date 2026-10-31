using SBM.Core.Geometry;
using SBM.Core.Geometry.Nurbs;

namespace SBM.Core.Solids;

/// <summary>A circular trim (inner loop) on a face: circle in the face's tangent plane at the hole.</summary>
public readonly record struct NurbsHole(Vec3 Center, Vec3 Normal, double Radius);

/// <summary>
/// One bounded face of a <see cref="NurbsSolid"/>: either a NURBS surface whose whole parameter rectangle is the face (oriented so
/// Su x Sv points OUT of the solid), or a planar polygon (counter-clockwise about the outward normal). Optional circular holes are
/// inner trimming loops; the hole walls are faces of their own.
/// </summary>
public sealed class NurbsFace
{
    public NurbsSurface? Surface { get; init; }
    public Vec3[]? Polygon { get; init; }
    public List<NurbsHole> Holes { get; } = new();
    /// <summary>OPTIONAL exact boundary of a planar face bounded by curves (e.g. a slab end: top and soffit curves in the end plane, straight
    /// sides and risers), chained end to start; <see cref="Polygon"/> then holds a dense sampling of it (CCW about the outward normal) for the
    /// consumers that work on polygons.</summary>
    public List<NurbsCurve>? Loop { get; init; }
    /// <summary>What the face is ("t0", "t1", "side", "end", "top", "bottom", "hole wall" ...).</summary>
    public string Role { get; init; } = "";

    /// <summary>Outward unit normal of a planar face (Newell).</summary>
    public Vec3 PlaneNormal()
    {
        var p = Polygon!;
        double nx = 0, ny = 0, nz = 0;
        for (int i = 0; i < p.Length; i++)
        {
            var a = p[i] - p[0];
            var b = p[(i + 1) % p.Length] - p[0];
            nx += (a.Y - b.Y) * (a.Z + b.Z);
            ny += (a.Z - b.Z) * (a.X + b.X);
            nz += (a.X - b.X) * (a.Y + b.Y);
        }
        return new Vec3(nx, ny, nz).Normalized();
    }
}

/// <summary>
/// Exact boundary representation of a curved solid ("B-rep light"): a closed set of NURBS / planar faces with circular trims.
/// Generators that know a part's exact definition record one on the part's <see cref="Mesh"/> (<see cref="Mesh.Nurbs"/>); the
/// mesh is then DERIVED from it by tessellation, so every consumer that reads triangles keeps working. Consumers can opt in to
/// the exact geometry: <see cref="Volume"/>, <see cref="Area"/>, <see cref="NurbsSection"/>.
/// </summary>
public sealed class NurbsSolid
{
    public string Kind { get; init; } = "";
    public List<NurbsFace> Faces { get; } = new();
    /// <summary>Fit tolerance requested (m) and achieved maximum deviation from the generator's exact geometry (m).</summary>
    public double Tolerance { get; init; }
    public double MaxDeviation { get; init; }

    private Vec3 Origin => Faces.Select(f => f.Surface != null ? f.Surface.Control[0, 0] : f.Polygon![0]).First();

    /// <summary>Enclosed volume by the divergence theorem, Gauss quadrature over every knot span (holes: minus their disks).</summary>
    public double Volume()
    {
        var o = Origin;
        double v = 0;
        foreach (var f in Faces)
        {
            if (f.Surface is { } s)
                v += Integrate(s, (p, su, sv) => Vec3.Dot(p - o, Vec3.Cross(su, sv)));
            else if (f.Loop is { Count: > 0 } loop)
            {
                // exact: (1/3) (p0 - o) . A, area vector A = 1/2 closed integral of (p - p0) x dp, oriented like the sampled polygon
                var p0 = f.Polygon![0];
                var a = LoopAreaVector(loop, p0);
                var n = f.PlaneNormal();
                if (Vec3.Dot(a, n) < 0) a = -a;
                v += Vec3.Dot(p0 - o, a);
            }
            else
            {
                var n = f.PlaneNormal();
                v += Vec3.Dot(f.Polygon![0] - o, n) * PolygonArea(f.Polygon);
            }
            foreach (var h in f.Holes)
            {
                var n = OutwardAt(f, h.Center);
                v -= Vec3.Dot(h.Center - o, n) * Math.PI * h.Radius * h.Radius;
            }
        }
        return v / 3;
    }

    /// <summary>Surface area (holes: minus their disks, plus their walls).</summary>
    public double Area()
    {
        double a = 0;
        foreach (var f in Faces)
        {
            a += f.Surface is { } s ? Integrate(s, (_, su, sv) => Vec3.Cross(su, sv).Length) : PolygonArea(f.Polygon!);
            foreach (var h in f.Holes) a -= Math.PI * h.Radius * h.Radius;
        }
        return a;
    }

    private static Vec3 OutwardAt(NurbsFace f, Vec3 p)
    {
        if (f.Surface == null) return f.PlaneNormal();
        var s = f.Surface;
        var (u, v, _) = s.ClosestPoint(p, (s.U0 + s.U1) / 2, (s.V0 + s.V1) / 2);
        return s.NormalAt(u, v);
    }

    /// <summary>Area vector of a closed loop of curves (Gauss quadrature per knot span of 1/2 (p - p0) x p').</summary>
    public static Vec3 LoopAreaVector(IReadOnlyList<NurbsCurve> loop, Vec3 p0)
    {
        var a = Vec3.Zero;
        foreach (var c in loop)
        {
            var ks = c.Knots;
            for (int i = c.Degree; i < c.Control.Length; i++)
            {
                if (ks[i + 1] <= ks[i]) continue;
                foreach (var (u, w) in Gauss.Points(c.IsRational ? 12 : c.Degree + 2, ks[i], ks[i + 1]))
                {
                    var (p, d1) = c.Derivative(u);
                    a += Vec3.Cross(p - p0, d1) * (0.5 * w);
                }
            }
        }
        return a;
    }

    public static double PolygonArea(IReadOnlyList<Vec3> p)
    {
        var c = Vec3.Zero;
        for (int i = 0; i < p.Count; i++) c += Vec3.Cross(p[i] - p[0], p[(i + 1) % p.Count] - p[0]);
        return c.Length / 2;
    }

    /// <summary>Integral of f(S, Su, Sv) du dv over the full parameter domain.</summary>
    public static double Integrate(NurbsSurface s, Func<Vec3, Vec3, Vec3, double> f)
    {
        int nu = s.IsRational ? 10 : s.DegreeU + 3, nv = s.IsRational ? 10 : s.DegreeV + 3;
        double sum = 0;
        for (int i = s.DegreeU; i < s.CountU; i++)
        {
            double a = s.KnotsU[i], b = s.KnotsU[i + 1];
            if (b <= a) continue;
            for (int j = s.DegreeV; j < s.CountV; j++)
            {
                double c = s.KnotsV[j], d = s.KnotsV[j + 1];
                if (d <= c) continue;
                foreach (var (u, wu) in Gauss.Points(nu, a, b))
                    foreach (var (v, wv) in Gauss.Points(nv, c, d))
                    {
                        var (p, su, sv) = s.Eval(u, v);
                        sum += f(p, su, sv) * wu * wv;
                    }
            }
        }
        return sum;
    }

    /// <summary>
    /// Closed solid lofted STRAIGHT between two planar rings with the same vertex count (vertex q of one ring runs straight to vertex q of the
    /// other, as a two-ring loft defines it): one ruled (bilinear) face per ring edge, the rings as planar end faces. Exact for a generator whose
    /// section vertices move linearly between the two rings. Null when a ring is not planar (1 micron) or the rings do not match.
    /// </summary>
    public static NurbsSolid? RuledLoft(IReadOnlyList<Vec3> r0, IReadOnlyList<Vec3> r1, string kind)
    {
        int n = r0.Count;
        if (n < 3 || r1.Count != n) return null;
        Vec3 Newell(IReadOnlyList<Vec3> p)
        {
            var a = Vec3.Zero;
            for (int i = 0; i < p.Count; i++) a += Vec3.Cross(p[i] - p[0], p[(i + 1) % p.Count] - p[0]);
            return a;
        }
        Vec3 Centroid(IReadOnlyList<Vec3> p) { var c = Vec3.Zero; foreach (var q in p) c += q - p[0]; return p[0] + c * (1.0 / p.Count); }
        var n0 = Newell(r0); var n1 = Newell(r1);
        if (n0.Length < 1e-12 || n1.Length < 1e-12 || Vec3.Dot(n0, n1) <= 0) return null;   // degenerate or oppositely ordered rings
        n0 = n0.Normalized(); n1 = n1.Normalized();
        if (r0.Any(q => Math.Abs(Vec3.Dot(q - r0[0], n0)) > 1e-6) || r1.Any(q => Math.Abs(Vec3.Dot(q - r1[0], n1)) > 1e-6)) return null;
        var solid = new NurbsSolid { Kind = kind };
        var c0 = Centroid(r0); var c1 = Centroid(r1);
        static NurbsCurve Line(Vec3 a, Vec3 b) => new(1, new double[] { 0, 0, 1, 1 }, new[] { a, b });
        for (int q = 0; q < n; q++)
        {
            int q1 = (q + 1) % n;
            var e = (r0[q1] - r0[q]) + (r1[q1] - r1[q]);
            if (e.Length < 1e-12) continue;                                   // both rings repeat a vertex: no face
            var outward = Vec3.Cross(e, n0 + n1);                             // rings counter-clockwise about n: edge x n points out
            var s = NurbsSurface.Ruled(Line(r0[q], r1[q]), Line(r0[q1], r1[q1]));
            var av = NurbsSlab.AreaVector(s);
            if (av.Length < 1e-14) continue;
            if (Vec3.Dot(av, outward) < 0) s = s.ReversedV();
            solid.Faces.Add(new NurbsFace { Surface = s, Role = "side" });
        }
        // end faces point away from the other ring
        var p0 = r0.ToArray(); var p1 = r1.ToArray();
        if (Vec3.Dot(n0, c1 - c0) > 0) Array.Reverse(p0);
        if (Vec3.Dot(n1, c0 - c1) > 0) Array.Reverse(p1);
        solid.Faces.Add(new NurbsFace { Polygon = p0, Role = "end 0" });
        solid.Faces.Add(new NurbsFace { Polygon = p1, Role = "end 1" });
        return solid;
    }

    /// <summary>Axis-aligned bounds of the control polygons (contains the solid: convex hull property).</summary>
    public Box3 Bounds()
    {
        var b = Box3.Empty;
        foreach (var f in Faces)
        {
            if (f.Surface is { } s) foreach (var c in s.Control) b.Add(c);
            else foreach (var c in f.Polygon!) b.Add(c);
        }
        return b;
    }
}
