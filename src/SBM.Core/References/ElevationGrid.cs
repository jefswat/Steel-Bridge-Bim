using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.References;

/// <summary>
/// Regular raster DEM (e.g. lidar 0.5 m): full-resolution float elevations for exact, O(1) bilinear queries; a
/// decimated triangle mesh is made only for display and contours. Row 0 is the TOP (north) row, as in ESRI grids.
/// Missing cells are NaN. Memory: 4 bytes per cell (36 M cells = 144 MB).
/// </summary>
public sealed class ElevationGrid
{
    public required int Nx { get; init; }
    public required int Ny { get; init; }
    /// <summary>Cell size (m).</summary>
    public required double Cell { get; init; }
    /// <summary>Center of the lower-left cell (m, project coordinates).</summary>
    public required double X0 { get; init; }
    public required double Y0 { get; init; }
    /// <summary>Row-major from the top row; NaN = no data.</summary>
    public required float[] Z { get; init; }
    /// <summary>Added to every elevation (offsets / unit conversion done at load).</summary>
    public double ZOffset { get; init; }

    public double XMax => X0 + (Nx - 1) * Cell;
    public double YMax => Y0 + (Ny - 1) * Cell;
    public long Cells => (long)Nx * Ny;

    /// <summary>Elevation at grid node (column i from the west, row j from the SOUTH).</summary>
    public float At(int i, int j) => Z[(long)(Ny - 1 - j) * Nx + i];

    public bool TryElevation(Vec2 p, out double z)
    {
        z = double.NaN;
        double fx = (p.X - X0) / Cell, fy = (p.Y - Y0) / Cell;
        if (fx < 0 || fy < 0 || fx > Nx - 1 || fy > Ny - 1) return false;
        int i = Math.Min((int)fx, Nx - 2), j = Math.Min((int)fy, Ny - 2);
        double u = fx - i, v = fy - j;
        float a = At(i, j), b = At(i + 1, j), c = At(i, j + 1), d = At(i + 1, j + 1);
        if (float.IsNaN(a) || float.IsNaN(b) || float.IsNaN(c) || float.IsNaN(d))
        {
            // Nearest valid node of the cell.
            float n = At(u < 0.5 ? i : i + 1, v < 0.5 ? j : j + 1);
            if (float.IsNaN(n)) return false;
            z = n + ZOffset;
            return true;
        }
        z = (a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v + ZOffset;
        return true;
    }

    /// <summary>Display stride so the mesh has at most <paramref name="maxVertices"/> vertices.</summary>
    public int StrideFor(long maxVertices) => Math.Max(1, (int)Math.Ceiling(Math.Sqrt((double)Cells / Math.Max(1, maxVertices))));

    /// <summary>Triangle mesh of every <paramref name="stride"/>-th node (cells with missing corners are skipped), optionally only the
    /// nodes inside the plan box <paramref name="clip"/> (plus one node around it so the clipped edge is covered).</summary>
    public Mesh ToMesh(int stride, Box2? clip = null)
    {
        int nx = (Nx - 1) / stride + 1, ny = (Ny - 1) / stride + 1;
        int i0 = 0, i1 = nx - 1, j0 = 0, j1 = ny - 1;
        if (clip is { IsEmpty: false } cb)
        {
            double step = Cell * stride;
            i0 = Math.Clamp((int)Math.Floor((cb.MinX - X0) / step), 0, nx - 1);
            i1 = Math.Clamp((int)Math.Ceiling((cb.MaxX - X0) / step), 0, nx - 1);
            j0 = Math.Clamp((int)Math.Floor((cb.MinY - Y0) / step), 0, ny - 1);
            j1 = Math.Clamp((int)Math.Ceiling((cb.MaxY - Y0) / step), 0, ny - 1);
            if (cb.MaxX < X0 || cb.MinX > XMax || cb.MaxY < Y0 || cb.MinY > YMax) return new Mesh();
        }
        int mx = i1 - i0 + 1, my = j1 - j0 + 1;
        var mesh = new Mesh();
        mesh.Positions.Capacity = mx * my;
        var id = new int[mx * my];
        for (int jj = 0; jj < my; jj++)
            for (int ii = 0; ii < mx; ii++)
            {
                int i = (ii + i0) * stride, j = (jj + j0) * stride;
                float z = At(i, j);
                id[jj * mx + ii] = float.IsNaN(z) ? -1 : mesh.Add(new Vec3(X0 + i * Cell, Y0 + j * Cell, z + ZOffset));
            }
        mesh.Indices.Capacity = (mx - 1) * (my - 1) * 6;
        nx = mx; ny = my;
        for (int jj = 0; jj + 1 < ny; jj++)
            for (int ii = 0; ii + 1 < nx; ii++)
            {
                int a = id[jj * nx + ii], b = id[jj * nx + ii + 1], c = id[(jj + 1) * nx + ii + 1], d = id[(jj + 1) * nx + ii];
                if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                mesh.Tri(a, b, c);
                mesh.Tri(a, c, d);
            }
        return mesh;
    }
}
