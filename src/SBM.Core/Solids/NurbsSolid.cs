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
