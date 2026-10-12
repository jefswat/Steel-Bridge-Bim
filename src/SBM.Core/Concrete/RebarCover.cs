using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Concrete;

/// <summary>
/// Generic clear-cover test of reinforcing against the concrete it is cast in, given as closed triangle meshes (world coordinates). A point of a bar
/// centerline passes when the ball of radius (required clear cover + bar radius) around it lies inside the UNION of the host solids, so faces shared
/// between solids (cap on column, column on shaft, pedestal on cap) are not free faces. Inside / outside by vertical ray parity, distances to the
/// triangles within reach (XY grid index per mesh); where the nearest solid alone does not give the clearance the ball is sampled (26 directions) and
/// bisected. Faces looking down / up can carry their own requirement (bottom / top cover).
/// </summary>
public sealed class RebarCover
{
    private sealed class Host
    {
        public required Vec3[] A, B, C;
        public required Vec3[] Nrm;
        public required double MinX, MinY, MinZ, MaxX, MaxY, MaxZ, Cell;
        public required int Nx, Ny;
        public required List<int>[] Grid;
    }

    private readonly List<Host> _hosts = new();
    private readonly double _reach;
    private static readonly Vec3[] Dirs = BuildDirs();

    /// <param name="reach">Largest clearance requirement that will be asked (cover + bar radius), meters; distances beyond it are not resolved.</param>
    public RebarCover(IEnumerable<Mesh> meshes, double reach = 0.15)
    {
        _reach = reach;
        foreach (var m in meshes)
        {
            if (m == null || m.IsEmpty) continue;
            int n = m.TriangleCount;
            var a = new Vec3[n]; var b = new Vec3[n]; var c = new Vec3[n]; var nr = new Vec3[n];
            double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                a[i] = m.Positions[m.Indices[3 * i]]; b[i] = m.Positions[m.Indices[3 * i + 1]]; c[i] = m.Positions[m.Indices[3 * i + 2]];
                var cr = Vec3.Cross(b[i] - a[i], c[i] - a[i]);
                nr[i] = cr.Length > 1e-15 ? cr * (1 / cr.Length) : new Vec3(0, 0, 0);
                foreach (var p in new[] { a[i], b[i], c[i] })
                {
                    x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z);
                    x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z);
                }
            }
            double cell = Math.Max(0.25, Math.Sqrt((x1 - x0) * (y1 - y0) / Math.Max(1, n / 4.0)));
            int nx = Math.Max(1, (int)Math.Ceiling((x1 - x0) / cell) + 1), ny = Math.Max(1, (int)Math.Ceiling((y1 - y0) / cell) + 1);
            if ((long)nx * ny > 4_000_000) { cell *= Math.Sqrt((double)nx * ny / 4_000_000); nx = (int)Math.Ceiling((x1 - x0) / cell) + 1; ny = (int)Math.Ceiling((y1 - y0) / cell) + 1; }
            var grid = new List<int>[nx * ny];
            for (int i = 0; i < n; i++)
            {
                double tx0 = Math.Min(a[i].X, Math.Min(b[i].X, c[i].X)), tx1 = Math.Max(a[i].X, Math.Max(b[i].X, c[i].X));
                double ty0 = Math.Min(a[i].Y, Math.Min(b[i].Y, c[i].Y)), ty1 = Math.Max(a[i].Y, Math.Max(b[i].Y, c[i].Y));
                int i0 = (int)((tx0 - x0) / cell), i1 = (int)((tx1 - x0) / cell), j0 = (int)((ty0 - y0) / cell), j1 = (int)((ty1 - y0) / cell);
                for (int ii = i0; ii <= i1; ii++)
                    for (int jj = j0; jj <= j1; jj++)
                        (grid[jj * nx + ii] ??= new List<int>()).Add(i);
            }
            _hosts.Add(new Host { A = a, B = b, C = c, Nrm = nr, MinX = x0, MinY = y0, MinZ = z0, MaxX = x1, MaxY = y1, MaxZ = z1, Cell = cell, Nx = nx, Ny = ny, Grid = grid });
        }
    }

    private readonly List<Func<Vec3, (double sd, Vec3 n)>> _analytic = new();

    /// <summary>Adds an exact host given by its signed distance (positive inside = distance to the nearest face, negative outside) and the outward normal of
    /// that face - for solids whose tessellation would distort the test (a deck slab on a tight curve: chord sagitta at the edges).</summary>
    public void AddAnalytic(Func<Vec3, (double sd, Vec3 n)> sdf) => _analytic.Add(sdf);

    public bool HasHosts => _hosts.Count > 0 || _analytic.Count > 0;

    private static Vec3[] BuildDirs()
    {
        var l = new List<Vec3>();
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                    if (x != 0 || y != 0 || z != 0) { var v = new Vec3(x, y, z); l.Add(v * (1 / v.Length)); }
        return l.ToArray();
    }

    private static bool InBox(Host h, Vec3 p, double m) =>
        p.X >= h.MinX - m && p.X <= h.MaxX + m && p.Y >= h.MinY - m && p.Y <= h.MaxY + m && p.Z >= h.MinZ - m && p.Z <= h.MaxZ + m;

    /// <summary>Inside test by a vertical ray (+Z) with parity; the ray is nudged off edges.</summary>
    private static bool Inside(Host h, Vec3 p)
    {
        if (!InBox(h, p, 0)) return false;
        double px = p.X + 1.234567e-7, py = p.Y + 2.345678e-7;
        int ci = (int)((px - h.MinX) / h.Cell), cj = (int)((py - h.MinY) / h.Cell);
        if (ci < 0 || cj < 0 || ci >= h.Nx || cj >= h.Ny) return false;
        var list = h.Grid[cj * h.Nx + ci];
        if (list == null) return false;
        int cnt = 0;
        foreach (int i in list)
        {
            var a = h.A[i]; var b = h.B[i]; var c = h.C[i];
            double d = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (Math.Abs(d) < 1e-14) continue;                         // vertical face: the ray grazes it
            double w0 = ((b.X - px) * (c.Y - py) - (b.Y - py) * (c.X - px)) / d;
            double w1 = ((c.X - px) * (a.Y - py) - (c.Y - py) * (a.X - px)) / d;
            double w2 = 1 - w0 - w1;
            if (w0 < 0 || w1 < 0 || w2 < 0) continue;
            double z = w0 * a.Z + w1 * b.Z + w2 * c.Z;
            if (z > p.Z) cnt++;
        }
        return (cnt & 1) == 1;
    }

    private bool InUnion(Vec3 p)
    {
        foreach (var h in _hosts) if (Inside(h, p)) return true;
        foreach (var a in _analytic) if (a(p).sd > 0) return true;
        return false;
    }

    /// <summary>Distance from p to the surface of one host within <paramref name="max"/> (else max) and the normal of the nearest triangle.</summary>
    private static (double d, Vec3 n) Distance(Host h, Vec3 p, double max)
    {
        double best = max; Vec3 bn = default;
        int i0 = Math.Max(0, (int)((p.X - max - h.MinX) / h.Cell)), i1 = Math.Min(h.Nx - 1, (int)((p.X + max - h.MinX) / h.Cell));
        int j0 = Math.Max(0, (int)((p.Y - max - h.MinY) / h.Cell)), j1 = Math.Min(h.Ny - 1, (int)((p.Y + max - h.MinY) / h.Cell));
        HashSet<int>? seen = (i1 > i0 || j1 > j0) ? new HashSet<int>() : null;
        for (int ii = i0; ii <= i1; ii++)
            for (int jj = j0; jj <= j1; jj++)
            {
                var list = h.Grid[jj * h.Nx + ii];
                if (list == null) continue;
                foreach (int i in list)
                {
                    if (seen != null && !seen.Add(i)) continue;
                    var q = ClosestOnTriangle(p, h.A[i], h.B[i], h.C[i]);
                    double d = (q - p).Length;
                    if (d < best) { best = d; bn = h.Nrm[i]; }
                }
            }
        return (best, bn);
    }

    /// <summary>Slack (m, >= 0 passes) of one centerline point: (clear distance in the union of the hosts) - (cover + bar radius); cover per face
    /// orientation (bottom = nearest face looking down, top = looking up). Very negative = outside the concrete.</summary>
    public (double slack, string face) Slack(Vec3 p, double radius, double cover, double coverBottom, double coverTop)
    {
        double bestIn = -1; Vec3 nIn = default; double nearOut = double.MaxValue;
        foreach (var h in _hosts)
        {
            if (!InBox(h, p, _reach)) continue;
            bool ins = Inside(h, p);
            var (d, n) = Distance(h, p, _reach);
            if (ins) { if (d > bestIn) { bestIn = d; nIn = n; } }
            else nearOut = Math.Min(nearOut, d);
        }
        foreach (var a in _analytic)
        {
            var (sd, n) = a(p);
            if (sd > 0) { if (sd > bestIn) { bestIn = sd; nIn = n; } }
            else nearOut = Math.Min(nearOut, -sd);
        }
        string Face(Vec3 n) => n.Z < -0.7 ? "bottom face" : n.Z > 0.7 ? "top face" : "side face";
        double Req(Vec3 n) => radius + (n.Z < -0.7 && !double.IsNaN(coverBottom) ? coverBottom : n.Z > 0.7 && !double.IsNaN(coverTop) ? coverTop : cover);
        if (bestIn < 0) return (-(nearOut >= _reach ? _reach : nearOut) - radius - cover, "outside the concrete");
        double R = Req(nIn);
        if (bestIn >= R - 1e-9) return (bestIn - R, Face(nIn));
        // the nearest single solid is too close: the face may be shared with a neighbouring solid -> ball test in the union
        bool Ball(double r) { foreach (var d in Dirs) if (!InUnion(p + d * r)) return false; return true; }
        if (Ball(R)) return (0, Face(nIn));
        double lo = Math.Max(0, bestIn), hi = R;
        for (int k = 0; k < 7; k++) { double md = (lo + hi) / 2; if (Ball(md)) lo = md; else hi = md; }
        return (lo - R, Face(nIn));
    }

    /// <summary>Worst slack along a bar centerline sampled every <paramref name="step"/>; the end points count without the bar radius (flat bar ends).</summary>
    public (double slack, Vec3 at, string face) Check(IReadOnlyList<Vec3> pts, double radius, double cover, double coverBottom, double coverTop, double step = 0.1524)
    {
        double worst = double.MaxValue; Vec3 at = default; string face = "";
        int last = pts.Count - 1;
        for (int i = 0; i <= last; i++)
        {
            void Eval(Vec3 q, double r)
            {
                var (s, f) = Slack(q, r, cover, coverBottom, coverTop);
                if (s < worst) { worst = s; at = q; face = f; }
            }
            Eval(pts[i], i == 0 || i == last ? 0 : radius);
            if (i < last)
            {
                double len = (pts[i + 1] - pts[i]).Length; int n = (int)(len / step);
                for (int k = 1; k <= n; k++) Eval(Vec3.Lerp(pts[i], pts[i + 1], k / (n + 1.0)), radius);
            }
        }
        return (worst, at, face);
    }

    private static Vec3 ClosestOnTriangle(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
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
}
