using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.References;

public enum ReferenceKind { Terrain, Reference }

/// <summary>Grid = raster DEM (ESRI float grid .flt/.hdr or ASCII grid .asc), meters unless UnitScale says otherwise.</summary>
public enum ReferenceFormat { Auto, LandXmlSurface, Dxf, Obj, Stl, Grid }

/// <summary>
/// An external model referenced by the project (not embedded): existing ground TINs, adjacent structures, utilities,
/// roadway models. Paths are stored relative to the project file when possible.
/// </summary>
public sealed class ReferenceModelDefinition
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public ReferenceKind Kind { get; set; } = ReferenceKind.Reference;
    public ReferenceFormat Format { get; set; } = ReferenceFormat.Auto;
    public bool Visible { get; set; } = true;
    public byte R { get; set; } = 170;
    public byte G { get; set; } = 170;
    public byte B { get; set; } = 175;
    public double Opacity { get; set; } = 1.0;
    /// <summary>File length unit in meters (0 = from the file, or the project's long unit if the file has none).</summary>
    public double UnitScale { get; set; }
    /// <summary>Translation applied after scaling (meters).</summary>
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double OffsetZ { get; set; }
    /// <summary>Terrain only: contour interval (meters; 0 = automatic).</summary>
    public double ContourInterval { get; set; }
    /// <summary>Georeferenced image draped on the surface in 3D (JPG/PNG with a world file .jgw/.pgw/.wld beside it).</summary>
    public string ImagePath { get; set; } = "";
    /// <summary>Raster DEMs: most vertices drawn (the full grid is still used for elevations). 0 = 4 million.</summary>
    public int DisplayMaxVertices { get; set; }
    public ReferenceModelDefinition Clone() => (ReferenceModelDefinition)MemberwiseClone();
}

/// <summary>Geometry loaded from a reference file, in project coordinates (meters).</summary>
public sealed class ReferenceGeometry
{
    public Mesh Mesh { get; set; } = new();
    /// <summary>Raster DEM (full resolution) when the source is a grid; <see cref="Mesh"/> is then its decimated display mesh.</summary>
    public ElevationGrid? Grid { get; set; }
    public List<Vec3[]> Polylines { get; } = new();
    public List<string> Messages { get; } = new();
    public string SourceUnit { get; set; } = "";
    public Box3 Bounds()
    {
        var b = Mesh.Bounds();
        foreach (var pl in Polylines) foreach (var p in pl) b.Add(p);
        return b;
    }
}

/// <summary>
/// Triangulated terrain with a uniform-grid spatial index for fast elevation queries (O(1) average), plus contour
/// generation. Built once per load; queries are thread-safe.
/// </summary>
public sealed class TerrainModel
{
    private readonly Mesh _mesh;
    private readonly double _x0, _y0, _cell;
    private readonly int _nx, _ny;
    private readonly int[][] _cells = Array.Empty<int[]>();
    private readonly ElevationGrid? _grid;

    /// <summary>Full-resolution raster terrain: exact bilinear elevations; <paramref name="displayMesh"/> for contours/drawing.</summary>
    public TerrainModel(ElevationGrid grid, Mesh displayMesh)
    {
        _grid = grid;
        _mesh = displayMesh;
        Bounds = displayMesh.Bounds();
    }

    public ElevationGrid? Grid => _grid;

    public Mesh Mesh => _mesh;
    public Box3 Bounds { get; }
    public int TriangleCount => _mesh.TriangleCount;

    public TerrainModel(Mesh mesh)
    {
        _mesh = mesh;
        Bounds = mesh.Bounds();
        int nt = Math.Max(1, mesh.TriangleCount);
        double w = Math.Max(Bounds.Max.X - Bounds.Min.X, 1e-6), h = Math.Max(Bounds.Max.Y - Bounds.Min.Y, 1e-6);
        // ~2 triangles per cell on average.
        _cell = Math.Max(Math.Sqrt(w * h / nt) * 1.5, 1e-3);
        _nx = Math.Clamp((int)Math.Ceiling(w / _cell), 1, 4096);
        _ny = Math.Clamp((int)Math.Ceiling(h / _cell), 1, 4096);
        _cell = Math.Max(w / _nx, h / _ny);
        _x0 = Bounds.Min.X;
        _y0 = Bounds.Min.Y;
        var lists = new List<int>[_nx * _ny];
        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            var a = mesh.Positions[mesh.Indices[3 * t]];
            var b = mesh.Positions[mesh.Indices[3 * t + 1]];
            var c = mesh.Positions[mesh.Indices[3 * t + 2]];
            int i0 = CellX(Math.Min(a.X, Math.Min(b.X, c.X))), i1 = CellX(Math.Max(a.X, Math.Max(b.X, c.X)));
            int j0 = CellY(Math.Min(a.Y, Math.Min(b.Y, c.Y))), j1 = CellY(Math.Max(a.Y, Math.Max(b.Y, c.Y)));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    (lists[j * _nx + i] ??= new List<int>(4)).Add(t);
        }
        _cells = lists.Select(l => l?.ToArray() ?? Array.Empty<int>()).ToArray();
    }

    private int CellX(double x) => Math.Clamp((int)((x - _x0) / _cell), 0, _nx - 1);
    private int CellY(double y) => Math.Clamp((int)((y - _y0) / _cell), 0, _ny - 1);

    /// <summary>Ground elevation at a plan point (false outside the surface).</summary>
    public bool TryElevation(Vec2 p, out double z)
    {
        if (_grid != null) return _grid.TryElevation(p, out z);
        z = double.NaN;
        if (p.X < Bounds.Min.X || p.X > Bounds.Max.X || p.Y < Bounds.Min.Y || p.Y > Bounds.Max.Y) return false;
        foreach (int t in _cells[CellY(p.Y) * _nx + CellX(p.X)])
        {
            var a = _mesh.Positions[_mesh.Indices[3 * t]];
            var b = _mesh.Positions[_mesh.Indices[3 * t + 1]];
            var c = _mesh.Positions[_mesh.Indices[3 * t + 2]];
            double d = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(d) < 1e-14) continue;
            double l1 = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / d;
            double l2 = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / d;
            double l3 = 1 - l1 - l2;
            if (l1 < -1e-9 || l2 < -1e-9 || l3 < -1e-9) continue;
            z = l1 * a.Z + l2 * b.Z + l3 * c.Z;
            return true;
        }
        return false;
    }

    /// <summary>Contour segments at the given interval (marching triangles, parallel). Every <paramref name="majorEvery"/>th is major.</summary>
    public (List<(Vec3 a, Vec3 b)> minor, List<(Vec3 a, Vec3 b)> major) Contours(double interval, int majorEvery = 5)
    {
        var minor = new System.Collections.Concurrent.ConcurrentBag<List<(Vec3, Vec3)>>();
        var major = new System.Collections.Concurrent.ConcurrentBag<List<(Vec3, Vec3)>>();
        int n = _mesh.TriangleCount;
        if (interval <= 0 || n == 0) return (new(), new());
        Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, n, 4096), range =>
        {
            var mi = new List<(Vec3, Vec3)>();
            var ma = new List<(Vec3, Vec3)>();
            for (int t = range.Item1; t < range.Item2; t++)
            {
                var a = _mesh.Positions[_mesh.Indices[3 * t]];
                var b = _mesh.Positions[_mesh.Indices[3 * t + 1]];
                var c = _mesh.Positions[_mesh.Indices[3 * t + 2]];
                double lo = Math.Min(a.Z, Math.Min(b.Z, c.Z)), hi = Math.Max(a.Z, Math.Max(b.Z, c.Z));
                for (long k = (long)Math.Ceiling(lo / interval); k * interval <= hi; k++)
                {
                    double z = k * interval;
                    var pts = new List<Vec3>(3);
                    void Edge(Vec3 p, Vec3 q)
                    {
                        if ((p.Z < z) == (q.Z < z)) return;
                        double s = (z - p.Z) / (q.Z - p.Z);
                        pts.Add(Vec3.Lerp(p, q, s));
                    }
                    Edge(a, b); Edge(b, c); Edge(c, a);
                    if (pts.Count == 2) (k % majorEvery == 0 ? ma : mi).Add((pts[0], pts[1]));
                }
            }
            minor.Add(mi);
            major.Add(ma);
        });
        return (minor.SelectMany(x => x).ToList(), major.SelectMany(x => x).ToList());
    }
}

/// <summary>Reduced display meshes for terrain (the viewport memory manager): elevations keep using the full surface.</summary>
public static class TerrainDisplay
{
    /// <summary>Display mesh of a terrain at a stride (raster DEMs; TINs keep their triangles) clipped to a plan box (empty = no clip).</summary>
    public static Mesh Build(ReferenceGeometry geo, int stride, Box2 clip)
    {
        if (geo.Grid is { } g) return g.ToMesh(Math.Max(1, stride), clip.IsEmpty ? null : clip);
        return clip.IsEmpty ? geo.Mesh : Clip(geo.Mesh, clip);
    }

    /// <summary>Triangles with at least one vertex inside the plan box (vertices renumbered).</summary>
    public static Mesh Clip(Mesh m, Box2 box)
    {
        var r = new Mesh();
        var map = new int[m.Positions.Count];
        Array.Fill(map, -1);
        for (int t = 0; t + 2 < m.Indices.Count; t += 3)
        {
            int a = m.Indices[t], b = m.Indices[t + 1], c = m.Indices[t + 2];
            if (!box.Contains(m.Positions[a].XY) && !box.Contains(m.Positions[b].XY) && !box.Contains(m.Positions[c].XY)) continue;
            int V(int i) => map[i] >= 0 ? map[i] : map[i] = r.Add(m.Positions[i]);
            r.Tri(V(a), V(b), V(c));
        }
        return r;
    }
}
