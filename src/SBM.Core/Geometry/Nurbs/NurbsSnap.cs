using SBM.Core.Solids;

namespace SBM.Core.Geometry.Nurbs;

/// <summary>
/// Exact measure snaps on parts that carry a NURBS definition (project setting Geometry.ExactCurvedGeometry): a hit on the
/// tessellated mesh is moved onto the exact face it approximates - the ray / surface intersection found by Newton's method from the
/// mesh hit (start parameters from a coarse sample + closest point), or the ray / plane intersection for planar faces.
/// </summary>
public static class NurbsSnap
{
    /// <summary>Refined point on the exact face, its outward normal, the shift from the mesh hit (m), the face role and whether
    /// the point is on the ray (false = closest point to the mesh hit, used when the ray grazes the surface).</summary>
    public sealed record Result(Vec3 Point, Vec3 Normal, double Shift, string Face, bool OnRay, bool Curved);

    /// <summary>The app's rule: refine only when the project's exact curved geometry is ON and the part carries NURBS; otherwise null
    /// (the mesh snap is used unchanged).</summary>
    public static Result? TryRefine(bool exactGeometryOn, IReadOnlyList<NurbsSolid>? solids, Vec3 origin, Vec3 dir, Vec3 meshHit, double maxShift = 0.02) =>
        exactGeometryOn && solids is { Count: > 0 } ? Refine(solids, origin, dir, meshHit, maxShift) : null;

    /// <summary>
    /// Refines <paramref name="meshHit"/> (on the ray <paramref name="origin"/> + t <paramref name="dir"/>, world coordinates) to the
    /// nearest exact face of <paramref name="solids"/>; null when no face lies within <paramref name="maxShift"/> of the hit
    /// (e.g. the hit is on a triangle that was not tessellated from the NURBS).
    /// </summary>
    public static Result? Refine(IReadOnlyList<NurbsSolid> solids, Vec3 origin, Vec3 dir, Vec3 meshHit, double maxShift = 0.02)
    {
        dir = dir.Normalized();
        Result? best = null;
        foreach (var solid in solids)
        {
            double reach = maxShift + solid.MaxDeviation;
            var b = solid.Bounds();
            if (meshHit.X < b.Min.X - reach || meshHit.Y < b.Min.Y - reach || meshHit.Z < b.Min.Z - reach
                || meshHit.X > b.Max.X + reach || meshHit.Y > b.Max.Y + reach || meshHit.Z > b.Max.Z + reach) continue;
            foreach (var f in solid.Faces)
            {
                var r = f.Surface != null ? OnSurface(f, origin, dir, meshHit, reach) : OnPlane(f, origin, dir, meshHit, reach);
                if (r != null && (best == null || r.Shift < best.Shift)) best = r;
            }
        }
        return best;
    }

    private static Result? OnPlane(NurbsFace f, Vec3 o, Vec3 d, Vec3 hit, double reach)
    {
        var poly = f.Polygon!;
        var n = f.PlaneNormal();
        double dn = Vec3.Dot(n, d);
        if (Math.Abs(dn) < 1e-9) return null;
        double t = Vec3.Dot(n, poly[0] - o) / dn;
        var q = o + d * t;
        double shift = (q - hit).Length;
        if (shift > reach || !InPolygon(poly, n, q, 1e-5) || InHole(f, q)) return null;
        return new Result(q, n, shift, f.Role, true, false);
    }

    private static Result? OnSurface(NurbsFace f, Vec3 o, Vec3 d, Vec3 hit, double reach)
    {
        var s = f.Surface!;
        // Convex hull property: the surface lies in its control points' box.
        var lo = new Vec3(double.MaxValue, double.MaxValue, double.MaxValue);
        var hi = new Vec3(double.MinValue, double.MinValue, double.MinValue);
        foreach (var c in s.Control)
        {
            lo = new Vec3(Math.Min(lo.X, c.X), Math.Min(lo.Y, c.Y), Math.Min(lo.Z, c.Z));
            hi = new Vec3(Math.Max(hi.X, c.X), Math.Max(hi.Y, c.Y), Math.Max(hi.Z, c.Z));
        }
        if (hit.X < lo.X - reach || hit.Y < lo.Y - reach || hit.Z < lo.Z - reach || hit.X > hi.X + reach || hit.Y > hi.Y + reach || hit.Z > hi.Z + reach) return null;

        // Start: the nearest of a coarse sample (4 per knot span), then the closest point to the mesh hit.
        var us = Samples(s.KnotsU, s.DegreeU, s.CountU);
        var vs = Samples(s.KnotsV, s.DegreeV, s.CountV);
        double bu = us[0], bv = vs[0], bd = double.MaxValue;
        foreach (double u in us)
            foreach (double v in vs)
            {
                double dd = (s.PointAt(u, v) - hit).Length;
                if (dd < bd) { bd = dd; bu = u; bv = v; }
            }
        var (cu, cv, cp) = s.ClosestPoint(hit, bu, bv);
        if ((cp - hit).Length > reach) return null;

        // Ray / surface intersection: S(u, v) - (o + t d) = 0, Newton in (u, v, t) from the closest point.
        double uu = cu, vv = cv, tt = Vec3.Dot(cp - o, d);
        bool onRay = false;
        for (int it = 0; it < 30; it++)
        {
            var (p, su, sv) = s.Eval(uu, vv);
            var F = p - (o + d * tt);
            if (F.Length < 1e-7) { onRay = true; break; } // 0.1 micron (world coordinates carry ~1e-9 m of rounding)
            // Solve [su sv -d] x = -F (Cramer).
            var c3 = -d;
            double det = Vec3.Dot(su, Vec3.Cross(sv, c3));
            if (Math.Abs(det) < 1e-30) break;
            var rhs = -F;
            double du = Vec3.Dot(rhs, Vec3.Cross(sv, c3)) / det;
            double dv = Vec3.Dot(su, Vec3.Cross(rhs, c3)) / det;
            double dt = Vec3.Dot(su, Vec3.Cross(sv, rhs)) / det;
            uu = Math.Clamp(uu + du, s.U0, s.U1);
            vv = Math.Clamp(vv + dv, s.V0, s.V1);
            tt += dt;
        }
        Vec3 q;
        if (onRay) q = s.PointAt(uu, vv);
        else { q = cp; uu = cu; vv = cv; } // grazing ray / off the face: the closest point on the surface
        double shift = (q - hit).Length;
        if (shift > reach || InHole(f, q)) return null;
        return new Result(q, s.NormalAt(uu, vv), shift, f.Role, onRay, true);
    }

    private static List<double> Samples(double[] knots, int degree, int count)
    {
        var r = new List<double>();
        double a = knots[degree], b = knots[count];
        var breaks = knots.Skip(degree).Take(count - degree + 1).Distinct().ToList();
        for (int i = 0; i + 1 < breaks.Count; i++)
            for (int k = 0; k < 4; k++) r.Add(breaks[i] + (breaks[i + 1] - breaks[i]) * k / 4);
        r.Add(b);
        if (r.Count == 1) r.Insert(0, a);
        return r;
    }

    private static bool InHole(NurbsFace f, Vec3 q)
    {
        foreach (var h in f.Holes)
        {
            var n = h.Normal.Normalized();
            var v = q - h.Center;
            var inPlane = v - n * Vec3.Dot(v, n);
            if (inPlane.Length < h.Radius - 1e-9) return true;
        }
        return false;
    }

    /// <summary>Point (on the polygon's plane) inside the polygon or within <paramref name="tol"/> of its boundary.</summary>
    private static bool InPolygon(Vec3[] poly, Vec3 n, Vec3 q, double tol)
    {
        // Drop the dominant normal axis.
        int ax = Math.Abs(n.X) > Math.Abs(n.Y) ? (Math.Abs(n.X) > Math.Abs(n.Z) ? 0 : 2) : (Math.Abs(n.Y) > Math.Abs(n.Z) ? 1 : 2);
        Vec2 P(Vec3 v) => ax == 0 ? new Vec2(v.Y, v.Z) : ax == 1 ? new Vec2(v.Z, v.X) : new Vec2(v.X, v.Y);
        var p = P(q);
        bool inside = false;
        double best = double.MaxValue;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var a = P(poly[i]);
            var b = P(poly[j]);
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            // distance to the 3D edge
            var e = poly[i] - poly[j];
            double t = Math.Clamp(Vec3.Dot(q - poly[j], e) / Math.Max(Vec3.Dot(e, e), 1e-30), 0, 1);
            best = Math.Min(best, (poly[j] + e * t - q).Length);
        }
        return inside || best <= tol;
    }
}
