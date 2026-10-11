using SBM.Core.Geometry;

namespace SBM.Core.Solids;

/// <summary>Right-handed orthonormal frame: local (x, y, z) → world.</summary>
public readonly record struct Frame3(Vec3 Origin, Vec3 X, Vec3 Y, Vec3 Z)
{
    public static Frame3 World => new(Vec3.Zero, new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1));

    public Vec3 ToWorld(double x, double y, double z) => Origin + X * x + Y * y + Z * z;
    public Vec3 ToWorld(Vec3 p) => ToWorld(p.X, p.Y, p.Z);

    /// <summary>Frame from origin, x direction and an approximate up (z); y = z × x.</summary>
    public static Frame3 FromXZ(Vec3 origin, Vec3 x, Vec3 zApprox)
    {
        var xn = x.Normalized();
        var y = Vec3.Cross(zApprox, xn).Normalized();
        var z = Vec3.Cross(xn, y);
        return new Frame3(origin, xn, y, z);
    }
}

/// <summary>Indexed triangle mesh (shared vertices, CCW outward triangles) in world coordinates (meters).</summary>
public sealed class Mesh
{
    public List<Vec3> Positions { get; } = new();
    public List<int> Indices { get; } = new();

    public int TriangleCount => Indices.Count / 3;
    public bool IsEmpty => Indices.Count == 0;

    /// <summary>Exact extrusions recorded by the builders, and how many of the mesh's triangles they describe.</summary>
    public List<ExtrudedSolid>? Solids { get; private set; }
    public int SolidTriangles { get; private set; }
    /// <summary>True when <see cref="Solids"/> describe the whole mesh (exporters may write them instead of triangles).</summary>
    public bool HasExactSolids => Solids is { Count: > 0 } && SolidTriangles == TriangleCount;

    /// <summary>Records that the triangles added since <paramref name="triStart"/> are exactly <paramref name="solid"/>.</summary>
    public void Describe(ExtrudedSolid? solid, int triStart) => DescribeRange(solid, TriangleCount - triStart);

    /// <summary>Records that <paramref name="triangles"/> of the mesh's triangles are exactly <paramref name="solid"/>.</summary>
    public void DescribeRange(ExtrudedSolid? solid, int triangles)
    {
        if (solid == null) return;
        (Solids ??= new()).Add(solid);
        SolidTriangles += triangles;
    }

    /// <summary>Exact NURBS definitions of (curved) solids in this mesh, when the generator built the mesh by tessellating them
    /// (project setting <c>Geometry.ExactCurvedGeometry</c>). Null = the mesh is the only definition.</summary>
    public List<NurbsSolid>? Nurbs { get; private set; }

    /// <summary>Triangle ranges (start, count) tessellated from <see cref="Nurbs"/>.</summary>
    public List<(int start, int count)>? NurbsTriangles { get; private set; }

    /// <summary>Records a NURBS solid; the triangles added since <paramref name="triStart"/> are its tessellation.</summary>
    public void AddNurbs(NurbsSolid solid, int triStart)
    {
        (Nurbs ??= new()).Add(solid);
        (NurbsTriangles ??= new()).Add((triStart, TriangleCount - triStart));
    }

    /// <summary>True when NURBS solids describe every triangle.</summary>
    public bool HasExactNurbs => Nurbs is { Count: > 0 } && NurbsTriangles!.Sum(r => r.count) == TriangleCount;

    /// <summary>Exact volume: the NURBS solids plus any triangles they do not describe (closed sub-meshes); null without NURBS.</summary>
    public double? ExactVolume()
    {
        if (Nurbs is not { Count: > 0 }) return null;
        double v = Nurbs.Sum(s => Math.Abs(s.Volume()));
        var covered = new bool[TriangleCount];
        foreach (var (s, c) in NurbsTriangles!) for (int i = s; i < s + c; i++) covered[i] = true;
        if (covered.All(x => x)) return v;
        var o = Positions[0];
        double rest = 0;
        for (int i = 0; i < TriangleCount; i++)
        {
            if (covered[i]) continue;
            var a = Positions[Indices[3 * i]] - o;
            var b = Positions[Indices[3 * i + 1]] - o;
            var cc = Positions[Indices[3 * i + 2]] - o;
            rest += Vec3.Dot(a, Vec3.Cross(b, cc));
        }
        return v + Math.Abs(rest / 6);
    }

    public int Add(Vec3 p)
    {
        Positions.Add(p);
        return Positions.Count - 1;
    }

    public void Tri(int a, int b, int c)
    {
        Indices.Add(a);
        Indices.Add(b);
        Indices.Add(c);
    }

    public void Append(Mesh other)
    {
        int o = Positions.Count;
        Positions.AddRange(other.Positions);
        foreach (int i in other.Indices) Indices.Add(i + o);
        if (other.Solids != null) { (Solids ??= new()).AddRange(other.Solids); SolidTriangles += other.SolidTriangles; }
        if (other.Nurbs != null)
        {
            int to = TriangleCount - other.TriangleCount;
            (Nurbs ??= new()).AddRange(other.Nurbs);
            (NurbsTriangles ??= new()).AddRange(other.NurbsTriangles!.Select(r => (r.start + to, r.count)));
        }
    }

    /// <summary>Enclosed volume (divergence theorem). Valid for closed, consistently oriented meshes.</summary>
    public double Volume()
    {
        // Tetrahedra from a vertex of the mesh, not the world origin: georeferenced models sit millions of meters out,
        // where a·(b×c) loses every significant digit to cancellation.
        if (Positions.Count == 0) return 0;
        var o = Positions[0];
        double v = 0;
        for (int i = 0; i < Indices.Count; i += 3)
        {
            var a = Positions[Indices[i]] - o;
            var b = Positions[Indices[i + 1]] - o;
            var c = Positions[Indices[i + 2]] - o;
            v += Vec3.Dot(a, Vec3.Cross(b, c));
        }
        return v / 6;
    }

    public Box3 Bounds()
    {
        var b = Box3.Empty;
        foreach (var p in Positions) b.Add(p);
        return b;
    }
}

public struct Box3
{
    public Vec3 Min, Max;
    public static Box3 Empty => new() { Min = new Vec3(double.MaxValue, double.MaxValue, double.MaxValue), Max = new Vec3(double.MinValue, double.MinValue, double.MinValue) };
    public readonly bool IsEmpty => Min.X > Max.X;
    public void Add(Vec3 p)
    {
        Min = new Vec3(Math.Min(Min.X, p.X), Math.Min(Min.Y, p.Y), Math.Min(Min.Z, p.Z));
        Max = new Vec3(Math.Max(Max.X, p.X), Math.Max(Max.Y, p.Y), Math.Max(Max.Z, p.Z));
    }
}

/// <summary>Builders for the solids used in steel detailing: extrusions, sweeps, prisms, cylinders and fillet welds.</summary>
public static class MeshBuilder
{
    /// <summary>
    /// Builds a closed solid from a sequence of rings (each ring = the same section polygon placed in 3D).
    /// <paramref name="section"/> is the 2D section used to triangulate the end caps; rings must follow its vertex order.
    /// </summary>
    public static void AddLoft(Mesh mesh, IReadOnlyList<Vec2> section, IReadOnlyList<Vec3[]> rings)
    {
        int n = section.Count;
        if (rings.Count < 2 || n < 3) return;
        int triStart = mesh.TriangleCount;
        AddLoftCore(mesh, section, rings);
        mesh.Describe(StraightSweep(rings), triStart);
    }

    /// <summary>The loft as an exact extrusion when every ring is a uniform translation of the first along one line.</summary>
    private static ExtrudedSolid? StraightSweep(IReadOnlyList<Vec3[]> rings)
    {
        var r0 = rings[0];
        var rl = rings[^1];
        var d = rl[0] - r0[0];
        if (d.Length < 1e-6) return null;
        var dn = d.Normalized();
        for (int k = 1; k + 1 < rings.Count; k++)
        {
            var e = rings[k][0] - r0[0];
            if (Vec3.Cross(e, dn).Length > ExtrudedSolid.Tolerance) return null;
            for (int i = 1; i < r0.Length; i++)
                if ((rings[k][i] - r0[i] - e).Length > ExtrudedSolid.Tolerance) return null;
        }
        return ExtrudedSolid.TryCreate(r0, rl);
    }

    private static void AddLoftCore(Mesh mesh, IReadOnlyList<Vec2> section, IReadOnlyList<Vec3[]> rings)
    {
        int n = section.Count;
        bool ccw = SignedArea(section) > 0;
        var capTris = Triangulate(section);

        // Orientation: sections are traversed CCW in (u, v) with the sweep direction = u × v when the frame is right-handed.
        // Detect actual handedness from geometry so callers don't have to care.
        var r0 = rings[0];
        var r1 = rings[1];
        var c0 = Centroid(r0);
        var c1 = Centroid(r1);
        var along = c1 - c0;
        var nrm = PolygonNormal(r0);
        bool flip = Vec3.Dot(nrm, along) < 0; // ring normal should point along the sweep for the start cap to face backward

        int baseIdx = mesh.Positions.Count;
        foreach (var ring in rings)
            foreach (var p in ring) mesh.Add(p);

        for (int k = 0; k + 1 < rings.Count; k++)
        {
            int a0 = baseIdx + k * n, b0 = baseIdx + (k + 1) * n;
            for (int i = 0; i < n; i++)
            {
                int i1 = (i + 1) % n;
                int p00 = a0 + i, p01 = a0 + i1, p10 = b0 + i, p11 = b0 + i1;
                if (!flip) { mesh.Tri(p00, p01, p11); mesh.Tri(p00, p11, p10); }
                else { mesh.Tri(p00, p11, p01); mesh.Tri(p00, p10, p11); }
            }
        }

        int first = baseIdx, last = baseIdx + (rings.Count - 1) * n;
        foreach (var (a, b, c) in capTris)
        {
            // Cap triangles are CCW in section space; that normal points along the sweep when ccw ^ flip.
            // Start cap must face backward, end cap forward.
            bool alongSweep = ccw ^ flip;
            if (alongSweep) { mesh.Tri(first + a, first + c, first + b); mesh.Tri(last + a, last + b, last + c); }
            else { mesh.Tri(first + a, first + b, first + c); mesh.Tri(last + a, last + c, last + b); }
        }
    }

    /// <summary>Extrudes a planar polygon (in frame x-y) from z0 to z1 along the frame's z axis.</summary>
    public static void AddExtrusion(Mesh mesh, IReadOnlyList<Vec2> polygon, Frame3 f, double z0, double z1)
    {
        var a = polygon.Select(p => f.ToWorld(p.X, p.Y, z0)).ToArray();
        var b = polygon.Select(p => f.ToWorld(p.X, p.Y, z1)).ToArray();
        AddLoft(mesh, polygon, new[] { a, b });
    }

    public static void AddBox(Mesh mesh, Frame3 f, double x0, double x1, double y0, double y1, double z0, double z1) =>
        AddExtrusion(mesh, new[] { new Vec2(x0, y0), new Vec2(x1, y0), new Vec2(x1, y1), new Vec2(x0, y1) }, f, z0, z1);

    /// <summary>Regular n-gon prism (n = 6 → hex head/nut) about the frame z axis.</summary>
    public static void AddPrism(Mesh mesh, Frame3 f, double circumradius, int sides, double z0, double z1, double rotation = 0)
    {
        var poly = Enumerable.Range(0, sides).Select(i => Vec2.FromAngle(rotation + 2 * Math.PI * i / sides) * circumradius).ToArray();
        AddExtrusion(mesh, poly, f, z0, z1);
    }

    /// <summary>
    /// Fillet weld as a triangular prism along p0→p1. The weld root is on the line; its legs run along
    /// <paramref name="leg1"/> and <paramref name="leg2"/> (unit vectors) with length <paramref name="size"/>.
    /// </summary>
    public static void AddFilletWeld(Mesh mesh, Vec3 p0, Vec3 p1, Vec3 leg1, Vec3 leg2, double size)
    {
        if ((p1 - p0).Length < 1e-6) return;
        var sec = new[] { new Vec2(0, 0), new Vec2(size, 0), new Vec2(0, size) };
        Vec3[] Ring(Vec3 p) => new[] { p, p + leg1 * size, p + leg2 * size };
        AddLoft(mesh, sec, new[] { Ring(p0), Ring(p1) });
    }

    private static Vec3 Centroid(Vec3[] ring)
    {
        var c = Vec3.Zero;
        foreach (var p in ring) c += p;
        return c * (1.0 / ring.Length);
    }

    /// <summary>Newell's method polygon normal (not normalized).</summary>
    private static Vec3 PolygonNormal(Vec3[] r)
    {
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < r.Length; i++)
        {
            var a = r[i];
            var b = r[(i + 1) % r.Length];
            x += (a.Y - b.Y) * (a.Z + b.Z);
            y += (a.Z - b.Z) * (a.X + b.X);
            z += (a.X - b.X) * (a.Y + b.Y);
        }
        return new Vec3(x, y, z);
    }

    public static double SignedArea(IReadOnlyList<Vec2> p)
    {
        double a = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var u = p[i];
            var v = p[(i + 1) % p.Count];
            a += u.X * v.Y - v.X * u.Y;
        }
        return a / 2;
    }

    /// <summary>Ear-clipping triangulation of a simple polygon. Returns CCW triangles relative to the polygon's own winding.</summary>
    public static List<(int, int, int)> Triangulate(IReadOnlyList<Vec2> poly)
    {
        var tris = new List<(int, int, int)>();
        int n = poly.Count;
        if (n < 3) return tris;
        bool ccw = SignedArea(poly) > 0;
        var idx = Enumerable.Range(0, n).ToList();
        int guard = 0;
        while (idx.Count > 3 && guard++ < 10000)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                var a = poly[ia];
                var b = poly[ib];
                var c = poly[ic];
                double cross = Vec2.Cross(b - a, c - b);
                if (ccw ? cross <= 1e-14 : cross >= -1e-14) continue; // reflex
                bool inside = false;
                foreach (int j in idx)
                {
                    if (j == ia || j == ib || j == ic) continue;
                    if (PointInTri(poly[j], a, b, c)) { inside = true; break; }
                }
                if (inside) continue;
                tris.Add(ccw ? (ia, ib, ic) : (ia, ic, ib));
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break; // degenerate; fall back to fan below
        }
        if (idx.Count == 3) tris.Add(ccw ? (idx[0], idx[1], idx[2]) : (idx[0], idx[2], idx[1]));
        else for (int i = 1; i + 1 < idx.Count; i++) tris.Add(ccw ? (idx[0], idx[i], idx[i + 1]) : (idx[0], idx[i + 1], idx[i]));
        return tris;
    }

    private static bool PointInTri(Vec2 p, Vec2 a, Vec2 b, Vec2 c)
    {
        double d1 = Vec2.Cross(b - a, p - a), d2 = Vec2.Cross(c - b, p - b), d3 = Vec2.Cross(a - c, p - c);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    /// <summary>Convex hull (Andrew's monotone chain), CCW.</summary>
    public static List<Vec2> ConvexHull(IEnumerable<Vec2> points)
    {
        var p = points.Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
        if (p.Count < 3) return p;
        var h = new List<Vec2>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = h.Count;
            foreach (var q in p)
            {
                while (h.Count >= start + 2 && Vec2.Cross(h[^1] - h[^2], q - h[^2]) <= 1e-12) h.RemoveAt(h.Count - 1);
                h.Add(q);
            }
            h.RemoveAt(h.Count - 1);
            p.Reverse();
        }
        return h;
    }
}
