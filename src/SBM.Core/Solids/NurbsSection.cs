using SBM.Core.Geometry;

namespace SBM.Core.Solids;

/// <summary>
/// Exact plane section of a <see cref="NurbsSolid"/>: per face, the curve where the face crosses the plane, as points ON the NURBS
/// surface (root of n·(S(u, v) - p0) along u on dense v lines, refined to 1e-12). Planar faces give straight segments. Points inside a
/// face's circular holes are dropped. One polyline per crossing branch of each face.
/// </summary>
public static class NurbsSection
{
    public static List<List<Vec3>> Cut(NurbsSolid solid, Vec3 planePoint, Vec3 normal, int samplesPerSpan = 8)
    {
        var n = normal.Normalized();
        var result = new List<List<Vec3>>();
        foreach (var f in solid.Faces)
        {
            if (f.Surface is { } s)
            {
                double F(double u, double v) => Vec3.Dot(s.PointAt(u, v) - planePoint, n);
                var us = Params(s.KnotsU, s.DegreeU, s.CountU, samplesPerSpan);
                var vs = Params(s.KnotsV, s.DegreeV, s.CountV, samplesPerSpan);
                var branches = new List<List<Vec3>>();
                foreach (double v in vs)
                {
                    var roots = new List<Vec3>();
                    double fa = F(us[0], v);
                    for (int i = 0; i + 1 < us.Count; i++)
                    {
                        double fb = F(us[i + 1], v);
                        if (fa == 0 || fa * fb < 0)
                        {
                            double vv = v;
                            if (Numerics.Brent(u => F(u, vv), us[i], us[i + 1], out double r, 1e-13)) roots.Add(s.PointAt(r, v));
                        }
                        fa = fb;
                    }
                    for (int k = 0; k < roots.Count; k++)
                    {
                        if (branches.Count <= k) branches.Add(new List<Vec3>());
                        branches[k].Add(roots[k]);
                    }
                }
                foreach (var b in branches)
                {
                    var kept = b.Where(p => !InHole(f, p)).ToList();
                    if (kept.Count >= 2) result.Add(kept);
                }
            }
            else
            {
                var poly = f.Polygon!;
                var pts = new List<Vec3>();
                for (int i = 0; i < poly.Length; i++)
                {
                    var a = poly[i]; var b = poly[(i + 1) % poly.Length];
                    double da = Vec3.Dot(a - planePoint, n), db = Vec3.Dot(b - planePoint, n);
                    if ((da < 0) != (db < 0)) pts.Add(Vec3.Lerp(a, b, da / (da - db)));
                }
                if (pts.Count >= 2) result.Add(pts);
            }
        }
        return result;
    }

    private static bool InHole(NurbsFace f, Vec3 p) => f.Holes.Any(h =>
    {
        var d = p - h.Center;
        d -= h.Normal * Vec3.Dot(d, h.Normal);
        return d.Length < h.Radius;
    });

    private static List<double> Params(double[] U, int p, int n, int per)
    {
        var list = new List<double>();
        for (int i = p; i < n; i++)
        {
            if (U[i + 1] <= U[i]) continue;
            for (int k = 0; k < per; k++) list.Add(U[i] + (U[i + 1] - U[i]) * k / per);
        }
        list.Add(U[n]);
        return list;
    }
}
