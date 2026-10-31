using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Grading;

/// <summary>Plan grid index over the upward-facing triangles of some meshes: elevation of the highest triangle over a plan point.</summary>
public sealed class SurfaceIndex
{
    private readonly List<(Vec3 a, Vec3 b, Vec3 c)> _tris = new();
    private readonly Dictionary<(int, int), List<int>> _cells = new();
    private readonly double _cell;
    public Box3 Bounds { get; } = Box3.Empty;
    public int TriangleCount => _tris.Count;

    /// <param name="upwardOnly">skip triangles whose normal points down or is (near) vertical: the TOP of solids.</param>
    public SurfaceIndex(IEnumerable<Mesh> meshes, bool upwardOnly = true, double cell = 2.0)
    {
        _cell = cell;
        var b = Box3.Empty;
        foreach (var m in meshes)
            for (int t = 0; t < m.TriangleCount; t++)
            {
                var a = m.Positions[m.Indices[3 * t]]; var p = m.Positions[m.Indices[3 * t + 1]]; var c = m.Positions[m.Indices[3 * t + 2]];
                var n = Vec3.Cross(p - a, c - a);
                double len = n.Length;
                if (len < 1e-12) continue;
                if (upwardOnly && n.Z / len < 0.02) continue;
                int id = _tris.Count;
                _tris.Add((a, p, c));
                b.Add(a); b.Add(p); b.Add(c);
                int x0 = Cell(Math.Min(a.X, Math.Min(p.X, c.X))), x1 = Cell(Math.Max(a.X, Math.Max(p.X, c.X)));
                int y0 = Cell(Math.Min(a.Y, Math.Min(p.Y, c.Y))), y1 = Cell(Math.Max(a.Y, Math.Max(p.Y, c.Y)));
                for (int i = x0; i <= x1; i++)
                    for (int j = y0; j <= y1; j++)
                        (_cells.TryGetValue((i, j), out var l) ? l : _cells[(i, j)] = new()).Add(id);
            }
        Bounds = b;
    }

    private int Cell(double v) => (int)Math.Floor(v / _cell);

    /// <summary>Highest surface elevation over the plan point, null when no triangle covers it.</summary>
    public double? ElevationAt(Vec2 p)
    {
        if (!_cells.TryGetValue((Cell(p.X), Cell(p.Y)), out var list)) return null;
        double? best = null;
        foreach (int id in list)
        {
            var (a, b, c) = _tris[id];
            double d = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(d) < 1e-14) continue;
            double l1 = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / d;
            double l2 = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / d;
            double l3 = 1 - l1 - l2;
            const double e = -1e-9;
            if (l1 < e || l2 < e || l3 < e) continue;
            double z = l1 * a.Z + l2 * b.Z + l3 * c.Z;
            if (best == null || z > best) best = z;
        }
        return best;
    }
}

/// <summary>
/// Finished ground = the first surface layer that covers a plan point, in priority order (the caller's order: roads, then grading), else the existing ground.
/// Used for structure rims / cover (storm drainage) and anything else that needs the proposed ground.
/// </summary>
public sealed class FinishedGround
{
    private readonly List<(string source, SurfaceIndex index)> _layers = new();
    private readonly Func<Vec2, double?>? _existing;

    public FinishedGround(Func<Vec2, double?>? existing, IEnumerable<(string source, IEnumerable<Mesh> meshes)> layers)
    {
        _existing = existing;
        foreach (var (s, m) in layers)
        {
            var ix = new SurfaceIndex(m);
            if (ix.TriangleCount > 0) _layers.Add((s, ix));
        }
    }

    /// <summary>Elevation and the layer it came from ("existing ground" for the terrain); (null, "") when nothing covers the point.</summary>
    public (double? z, string source) Query(Vec2 p)
    {
        foreach (var (s, ix) in _layers)
            if (ix.ElevationAt(p) is double z) return (z, s);
        return _existing?.Invoke(p) is double g ? (g, "existing ground") : (null, "");
    }

    public double? ElevationAt(Vec2 p) => Query(p).z;

    /// <summary>Plan extents of the road / grading surfaces (empty when there are none; the existing ground has no extents here).</summary>
    public Box2 SurfaceExtents
    {
        get
        {
            var b = Box2.Empty;
            foreach (var (_, ix) in _layers)
                if (ix.TriangleCount > 0) { b.Add(new Vec2(ix.Bounds.Min.X, ix.Bounds.Min.Y)); b.Add(new Vec2(ix.Bounds.Max.X, ix.Bounds.Max.Y)); }
            return b;
        }
    }

    /// <summary>
    /// The merged finished ground as one TIN: the surface sampled on a square grid of <paramref name="spacing"/> over <paramref name="extents"/> (each cell two triangles
    /// where all four corners have a surface; cells with a corner on no surface are left out). The surface between grid points is linear, so features narrower than the
    /// spacing (curb lines, ditch inverts) are smoothed: pick the spacing for the use. Returns the mesh and the number of grid points per source.
    /// </summary>
    public (Mesh Tin, Dictionary<string, int> Sources) ToTin(Box2 extents, double spacing)
    {
        if (!(spacing > 0)) throw new ArgumentException("spacing must be > 0");
        int nx = (int)Math.Ceiling(extents.Width / spacing), ny = (int)Math.Ceiling((extents.MaxY - extents.MinY) / spacing);
        if ((long)(nx + 1) * (ny + 1) > 20_000_000) throw new ArgumentException($"{(nx + 1) * (long)(ny + 1):N0} grid points - use a larger spacing or smaller extents.");
        var z = new double?[nx + 1, ny + 1];
        var src = new string[nx + 1, ny + 1];
        System.Threading.Tasks.Parallel.For(0, nx + 1, i =>
        {
            for (int j = 0; j <= ny; j++)
            {
                var q = Query(new Vec2(extents.MinX + i * spacing, extents.MinY + j * spacing));
                z[i, j] = q.z; src[i, j] = q.source;
            }
        });
        var m = new Mesh();
        var id = new int[nx + 1, ny + 1];
        var counts = new Dictionary<string, int>();
        for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= ny; j++)
            {
                id[i, j] = -1;
                if (z[i, j] is not double zz) continue;
                id[i, j] = m.Add(new Vec3(extents.MinX + i * spacing, extents.MinY + j * spacing, zz));
                counts[src[i, j]] = counts.GetValueOrDefault(src[i, j]) + 1;
            }
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                int a = id[i, j], b = id[i + 1, j], c = id[i + 1, j + 1], d = id[i, j + 1];
                if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                m.Tri(a, b, c); m.Tri(a, c, d);
            }
        return (m, counts);
    }
}
