using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Drawing.Views;

/// <summary>
/// Section of triangle meshes by a plane, in 2D view coordinates (x = <c>XAxis</c>, y = <c>YAxis</c> through <c>Origin</c>):
/// the cut segments, optionally clipped to an x window, and the closed loops they form (for hatching). Used to draw true
/// sections of generated solids (diaphragms, beams, caps, deck) on the sheets without re-deriving the geometry.
/// </summary>
public sealed class MeshSection
{
    public Vec3 Origin { get; }
    public Vec3 Normal { get; }
    public Vec3 XAxis { get; }
    public Vec3 YAxis { get; }

    public MeshSection(Vec3 origin, Vec3 normal, Vec3 yAxis)
    {
        Origin = origin;
        Normal = normal.Normalized();
        // y = the up direction made normal to the plane normal; x completes a right-handed (x, y, n) frame.
        YAxis = (yAxis - Normal * Vec3.Dot(yAxis, Normal)).Normalized();
        XAxis = Vec3.Cross(YAxis, Normal).Normalized();
    }

    public Vec2 To2D(Vec3 p) { var d = p - Origin; return new Vec2(Vec3.Dot(d, XAxis), Vec3.Dot(d, YAxis)); }

    /// <summary>Cut segments of a mesh (each triangle crossing the plane gives one segment).</summary>
    public List<(Vec2 A, Vec2 B)> Cut(Mesh m)
    {
        var res = new List<(Vec2, Vec2)>();
        var pos = m.Positions;
        var dist = new double[pos.Count];
        for (int i = 0; i < pos.Count; i++) dist[i] = Vec3.Dot(pos[i] - Origin, Normal);
        var pts = new List<Vec3>(3);
        for (int t = 0; t + 2 < m.Indices.Count; t += 3)
        {
            int a = m.Indices[t], b = m.Indices[t + 1], c = m.Indices[t + 2];
            double da = dist[a], db = dist[b], dc = dist[c];
            if ((da > 0 && db > 0 && dc > 0) || (da < 0 && db < 0 && dc < 0)) continue;
            pts.Clear();
            void Edge(int i, int j, double di, double dj)
            {
                if ((di > 0) == (dj > 0) && di != 0 && dj != 0) return;
                if (Math.Abs(di - dj) < 1e-15) return;
                double s = di / (di - dj);
                if (s < 0 || s > 1) return;
                pts.Add(pos[i] + (pos[j] - pos[i]) * s);
            }
            Edge(a, b, da, db); Edge(b, c, db, dc); Edge(c, a, dc, da);
            if (pts.Count < 2) continue;
            var p0 = To2D(pts[0]);
            var p1 = To2D(pts[^1]);
            for (int k = 1; k < pts.Count; k++) { var q = To2D(pts[k]); if ((q - p0).Length > (p1 - p0).Length) p1 = q; }
            if ((p1 - p0).Length > 1e-7) res.Add((p0, p1));
        }
        return res;
    }

    /// <summary>Segments clipped to x0 &lt;= x &lt;= x1.</summary>
    public static List<(Vec2 A, Vec2 B)> ClipX(IEnumerable<(Vec2 A, Vec2 B)> segs, double x0, double x1)
    {
        var res = new List<(Vec2, Vec2)>();
        foreach (var (a, b) in segs)
        {
            double lo = Math.Min(a.X, b.X), hi = Math.Max(a.X, b.X);
            if (hi < x0 || lo > x1) continue;
            Vec2 pa = a, pb = b;
            double dx = b.X - a.X;
            if (Math.Abs(dx) > 1e-12)
            {
                double t0 = Math.Clamp((x0 - a.X) / dx, 0, 1), t1 = Math.Clamp((x1 - a.X) / dx, 0, 1);
                double ta = Math.Min(t0, t1), tb = Math.Max(t0, t1);
                pa = Vec2.Lerp(a, b, ta); pb = Vec2.Lerp(a, b, tb);
            }
            if ((pb - pa).Length > 1e-7) res.Add((pa, pb));
        }
        return res;
    }

    /// <summary>Segments clipped to y0 &lt;= y &lt;= y1.</summary>
    public static List<(Vec2 A, Vec2 B)> ClipY(IEnumerable<(Vec2 A, Vec2 B)> segs, double y0, double y1) =>
        ClipX(segs.Select(q => (new Vec2(q.A.Y, q.A.X), new Vec2(q.B.Y, q.B.X))), y0, y1).Select(q => (new Vec2(q.A.Y, q.A.X), new Vec2(q.B.Y, q.B.X))).ToList();

    /// <summary>Closed loops chained from segments (endpoint match within <paramref name="tol"/>); open chains are dropped.</summary>
    public static List<List<Vec2>> Loops(List<(Vec2 A, Vec2 B)> segs, double tol = 1e-5)
    {
        var loops = new List<List<Vec2>>();
        var used = new bool[segs.Count];
        long Key(Vec2 p) => ((long)Math.Round(p.X / tol) * 73856093L) ^ ((long)Math.Round(p.Y / tol) * 19349663L);
        var at = new Dictionary<long, List<int>>();
        for (int i = 0; i < segs.Count; i++)
            foreach (var p in new[] { segs[i].A, segs[i].B })
            {
                long k = Key(p);
                if (!at.TryGetValue(k, out var l)) at[k] = l = new List<int>();
                l.Add(i);
            }
        int Next(Vec2 p)
        {
            if (!at.TryGetValue(Key(p), out var l)) return -1;
            foreach (int j in l)
                if (!used[j] && ((segs[j].A - p).Length < tol * 2 || (segs[j].B - p).Length < tol * 2)) return j;
            return -1;
        }
        for (int i = 0; i < segs.Count; i++)
        {
            if (used[i]) continue;
            used[i] = true;
            var loop = new List<Vec2> { segs[i].A, segs[i].B };
            while (true)
            {
                int j = Next(loop[^1]);
                if (j < 0) break;
                used[j] = true;
                loop.Add((segs[j].A - loop[^1]).Length < tol * 2 ? segs[j].B : segs[j].A);
                if ((loop[^1] - loop[0]).Length < tol * 2) break;
            }
            if (loop.Count >= 4 && (loop[^1] - loop[0]).Length < tol * 2)
            {
                loop.RemoveAt(loop.Count - 1);
                if (Math.Abs(MeshBuilder.SignedArea(loop)) > tol * tol * 10) loops.Add(loop);
            }
        }
        return loops;
    }

    /// <summary>Draws the cut of a mesh: outline segments on <paramref name="layer"/>, and hatches the closed loops inside the window.</summary>
    public void Draw(Drafter d, Mesh m, LayerStyle layer, double x0 = double.NegativeInfinity, double x1 = double.PositiveInfinity, bool hatch = true, double hatchSpacing = 0.08)
    {
        var all = Cut(m);
        var segs = double.IsInfinity(x0) && double.IsInfinity(x1) ? all : ClipX(all, x0, x1);
        foreach (var (a, b) in segs) d.Line(layer, a, b);
        if (!hatch) return;
        foreach (var loop in Loops(all))
            if (loop.All(p => p.X >= x0 - 1e-9 && p.X <= x1 + 1e-9)) d.Hatch(loop, hatchSpacing);
    }
}
