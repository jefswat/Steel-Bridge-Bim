using SBM.Core.Geometry;

namespace SBM.Core.Solids;

/// <summary>
/// Exact description of a solid that a builder put into a <see cref="Mesh"/>: a planar polygon with round holes (profile
/// in the frame's x-y plane at z = 0) swept along <see cref="Extrusion"/> (world vector, need not be normal to the profile).
/// Exporters use it to write compact parametric geometry (IFC IfcExtrudedAreaSolid) instead of triangles.
/// </summary>
public sealed class ExtrudedSolid
{
    /// <summary>Orthonormal frame of the profile plane (Z = profile normal).</summary>
    public required Frame3 Frame { get; init; }
    /// <summary>Counter-clockwise outer boundary in frame (x, y).</summary>
    public required Vec2[] Outline { get; init; }
    public required Hole[] Holes { get; init; }
    public required Vec3 Extrusion { get; init; }
    /// <summary>Exact outline with true arcs (OPTIONAL, same frame as <see cref="Outline"/>, counter-clockwise); <see cref="Outline"/> is then its
    /// tessellation. IFC writes it as an IfcIndexedPolyCurve with IfcArcIndex segments.</summary>
    public ArcPolygon? ExactOutline { get; init; }

    /// <summary>Fit tolerance (m): points may be off the profile plane / off the common sweep by this much.</summary>
    public const double Tolerance = 5e-4;

    /// <summary>
    /// Recognizes a straight extrusion from its bottom and top boundary rings (same vertex order) and hole centers on the
    /// bottom face. Returns null when the bottom is not planar or the top is not a uniform translation of it.
    /// </summary>
    public static ExtrudedSolid? TryCreate(IReadOnlyList<Vec3> bottom, IReadOnlyList<Vec3> top,
        IReadOnlyList<(Vec3 Center, double Diameter)>? holes = null, double tol = Tolerance)
    {
        int n = bottom.Count;
        if (n < 3 || top.Count != n) return null;
        var d = top[0] - bottom[0];
        for (int i = 1; i < n; i++)
            if ((top[i] - bottom[i] - d).Length > tol) return null;

        // Newell normal and centroid.
        double nx = 0, ny = 0, nz = 0;
        var c = Vec3.Zero;
        for (int i = 0; i < n; i++)
        {
            var a = bottom[i];
            var b = bottom[(i + 1) % n];
            nx += (a.Y - b.Y) * (a.Z + b.Z);
            ny += (a.Z - b.Z) * (a.X + b.X);
            nz += (a.X - b.X) * (a.Y + b.Y);
            c += a;
        }
        c *= 1.0 / n;
        var nrm = new Vec3(nx, ny, nz);
        if (nrm.Length < 1e-12) return null;
        nrm = nrm.Normalized();
        double dl = d.Length;
        if (dl < 1e-6 || Math.Abs(Vec3.Dot(d, nrm)) < 1e-3 * dl) return null;
        foreach (var p in bottom)
            if (Math.Abs(Vec3.Dot(p - c, nrm)) > tol) return null;

        // X along the longest edge (projected into the plane).
        Vec3 x = default;
        double best = 0;
        for (int i = 0; i < n; i++)
        {
            var e = bottom[(i + 1) % n] - bottom[i];
            e -= nrm * Vec3.Dot(e, nrm);
            if (e.Length > best) { best = e.Length; x = e; }
        }
        if (best < 1e-9) return null;
        x = x.Normalized();
        var y = Vec3.Cross(nrm, x);
        var f = new Frame3(c, x, y, nrm);
        Vec2 To2(Vec3 p) { var q = p - c; return new Vec2(Vec3.Dot(q, x), Vec3.Dot(q, y)); }

        // Outline: drop repeated and collinear points.
        var pts = new List<Vec2>(n);
        foreach (var p in bottom)
        {
            var q = To2(p);
            if (pts.Count == 0 || (q - pts[^1]).Length > 1e-7) pts.Add(q);
        }
        if (pts.Count > 1 && (pts[0] - pts[^1]).Length <= 1e-7) pts.RemoveAt(pts.Count - 1);
        for (int guard = 0; guard < 2; guard++)
            for (int i = pts.Count - 1; i >= 0 && pts.Count > 3; i--)
            {
                var a = pts[(i - 1 + pts.Count) % pts.Count];
                var b = pts[(i + 1) % pts.Count];
                var ab = b - a;
                double len = ab.Length;
                if (len < 1e-9) continue;
                var ap = pts[i] - a;
                if (Math.Abs(ab.X * ap.Y - ab.Y * ap.X) / len < 1e-7 && Vec2.Dot(ap, ab) > 0 && Vec2.Dot(ap, ab) < len * len) pts.RemoveAt(i);
            }
        if (pts.Count < 3) return null;
        if (MeshBuilder.SignedArea(pts) < 0) pts.Reverse();

        var hs = new List<Hole>();
        if (holes != null)
            foreach (var (hc, dia) in holes)
            {
                if (Math.Abs(Vec3.Dot(hc - c, nrm)) > tol) return null;
                hs.Add(new Hole(To2(hc), dia));
            }
        return new ExtrudedSolid { Frame = f, Outline = pts.ToArray(), Holes = hs.ToArray(), Extrusion = d };
    }
}
