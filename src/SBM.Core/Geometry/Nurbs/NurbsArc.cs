namespace SBM.Core.Geometry.Nurbs;

/// <summary>Exact circular arcs as rational quadratic NURBS (Piegl &amp; Tiller A7.1): at most 90 degrees per Bezier segment,
/// middle weight cos(half segment angle). The curve parameter runs over [0, 1].</summary>
public static class NurbsArc
{
    /// <summary>Arc of radius r around <paramref name="center"/> in the plane of the unit axes x, y, from angle a0 to a1 (radians,
    /// counter-clockwise about x × y; a1 &gt; a0).</summary>
    public static NurbsCurve Arc(Vec3 center, Vec3 x, Vec3 y, double r, double a0, double a1)
    {
        double theta = a1 - a0;
        if (!(theta > 0) || theta > 2 * Math.PI + 1e-12) throw new ArgumentException("Arc sweep must be in (0, 2 pi]");
        int narcs = Math.Max(1, (int)Math.Ceiling(theta / (Math.PI / 2) - 1e-9));
        double dt = theta / narcs, w1 = Math.Cos(dt / 2);
        var ctrl = new Vec3[2 * narcs + 1];
        var wts = new double[2 * narcs + 1];
        Vec3 At(double a, double rr) => center + x * (rr * Math.Cos(a)) + y * (rr * Math.Sin(a));
        ctrl[0] = At(a0, r);
        wts[0] = 1;
        for (int i = 1; i <= narcs; i++)
        {
            double a = a0 + i * dt;
            ctrl[2 * i] = At(a, r);
            wts[2 * i] = 1;
            // middle control point: where the end tangents meet = on the bisector at r / cos(dt/2)
            ctrl[2 * i - 1] = At(a - dt / 2, r / w1);
            wts[2 * i - 1] = w1;
        }
        var knots = new List<double> { 0, 0, 0 };
        for (int i = 1; i < narcs; i++) { knots.Add((double)i / narcs); knots.Add((double)i / narcs); }
        knots.AddRange(new double[] { 1, 1, 1 });
        return new NurbsCurve(2, knots.ToArray(), ctrl, wts);
    }

    /// <summary>Full circle (four quarter arcs).</summary>
    public static NurbsCurve Circle(Vec3 center, Vec3 x, Vec3 y, double r) => Arc(center, x, y, r, 0, 2 * Math.PI);

    /// <summary>Unit axes of the plane with normal n (x = any perpendicular, y = n × x).</summary>
    public static (Vec3 x, Vec3 y) Axes(Vec3 n)
    {
        n = n.Normalized();
        var a = Math.Abs(n.Z) < 0.9 ? new Vec3(0, 0, 1) : new Vec3(1, 0, 0);
        var x = Vec3.Cross(a, n).Normalized();
        return (x, Vec3.Cross(n, x));
    }
}
