namespace SBM.Core.Geometry;

public static class Numerics
{
    public const double Eps = 1e-12;

    /// <summary>Linear geometric tolerance (meters) used for coincidence checks.</summary>
    public const double LinearTol = 1e-6;

    public static double DegToRad(double d) => d * Math.PI / 180.0;
    public static double RadToDeg(double r) => r * 180.0 / Math.PI;

    /// <summary>Wraps an angle into (-π, π].</summary>
    public static double NormalizeAngle(double a)
    {
        a = Math.IEEERemainder(a, 2 * Math.PI);
        if (a <= -Math.PI) a += 2 * Math.PI;
        return a;
    }

    // 10-point Gauss-Legendre nodes/weights on [-1, 1].
    private static readonly double[] GlX =
    {
        -0.9739065285171717, -0.8650633666889845, -0.6794095682990244, -0.4333953941292472, -0.1488743389816312,
         0.1488743389816312,  0.4333953941292472,  0.6794095682990244,  0.8650633666889845,  0.9739065285171717,
    };
    private static readonly double[] GlW =
    {
        0.0666713443086881, 0.1494513491505806, 0.2190863625159820, 0.2692667193099963, 0.2955242247147529,
        0.2955242247147529, 0.2692667193099963, 0.2190863625159820, 0.1494513491505806, 0.0666713443086881,
    };

    /// <summary>Composite 10-point Gauss-Legendre integration of a vector-valued function.</summary>
    public static Vec2 Integrate(Func<double, Vec2> f, double a, double b, int segments)
    {
        if (segments < 1) segments = 1;
        double h = (b - a) / segments;
        double sx = 0, sy = 0;
        for (int s = 0; s < segments; s++)
        {
            double a0 = a + s * h;
            double mid = a0 + h / 2, half = h / 2;
            for (int i = 0; i < GlX.Length; i++)
            {
                var v = f(mid + half * GlX[i]);
                sx += GlW[i] * v.X * half;
                sy += GlW[i] * v.Y * half;
            }
        }
        return new Vec2(sx, sy);
    }

    /// <summary>Brent's method root finder on a bracket [a, b] where f(a), f(b) have opposite signs.</summary>
    public static bool Brent(Func<double, double> f, double a, double b, out double root, double tol = 1e-12, int maxIter = 200)
    {
        double fa = f(a), fb = f(b);
        root = double.NaN;
        if (double.IsNaN(fa) || double.IsNaN(fb)) return false;
        if (fa == 0) { root = a; return true; }
        if (fb == 0) { root = b; return true; }
        if (fa * fb > 0) return false;

        double c = a, fc = fa, d = b - a, e = d;
        for (int iter = 0; iter < maxIter; iter++)
        {
            if (fb * fc > 0) { c = a; fc = fa; d = b - a; e = d; }
            if (Math.Abs(fc) < Math.Abs(fb))
            {
                a = b; b = c; c = a;
                fa = fb; fb = fc; fc = fa;
            }
            double tol1 = 2 * Eps * Math.Abs(b) + 0.5 * tol;
            double xm = 0.5 * (c - b);
            if (Math.Abs(xm) <= tol1 || fb == 0) { root = b; return true; }
            if (Math.Abs(e) >= tol1 && Math.Abs(fa) > Math.Abs(fb))
            {
                double s = fb / fa, p, q;
                if (a == c) { p = 2 * xm * s; q = 1 - s; }
                else
                {
                    double qq = fa / fc, r = fb / fc;
                    p = s * (2 * xm * qq * (qq - r) - (b - a) * (r - 1));
                    q = (qq - 1) * (r - 1) * (s - 1);
                }
                if (p > 0) q = -q;
                p = Math.Abs(p);
                double min1 = 3 * xm * q - Math.Abs(tol1 * q), min2 = Math.Abs(e * q);
                if (2 * p < Math.Min(min1, min2)) { e = d; d = p / q; }
                else { d = xm; e = d; }
            }
            else { d = xm; e = d; }
            a = b; fa = fb;
            b += Math.Abs(d) > tol1 ? d : (xm > 0 ? tol1 : -tol1);
            fb = f(b);
        }
        root = b;
        return true;
    }

    /// <summary>
    /// Finds all roots of f on [a, b] by sampling n intervals for sign changes, then refining with Brent.
    /// Robust for smooth functions with roots separated by more than (b - a) / n.
    /// </summary>
    public static List<double> FindRoots(Func<double, double> f, double a, double b, int n, double tol = 1e-10)
    {
        var roots = new List<double>();
        if (n < 1) n = 1;
        double h = (b - a) / n;
        double x0 = a, f0 = f(a);
        if (f0 == 0) roots.Add(a);
        for (int i = 1; i <= n; i++)
        {
            double x1 = i == n ? b : a + i * h;
            double f1 = f(x1);
            if (f1 == 0) roots.Add(x1);
            else if (f0 * f1 < 0 && Brent(f, x0, x1, out double r, tol)) roots.Add(r);
            x0 = x1; f0 = f1;
        }
        return roots;
    }

    /// <summary>Intersection of two infinite 2D lines p + t·u and q + s·v. Returns false if parallel.</summary>
    public static bool IntersectLines(Vec2 p, Vec2 u, Vec2 q, Vec2 v, out double t, out double s)
    {
        double den = Vec2.Cross(u, v);
        t = s = double.NaN;
        if (Math.Abs(den) < 1e-14 * u.Length * v.Length) return false;
        var w = q - p;
        t = Vec2.Cross(w, v) / den;
        s = Vec2.Cross(w, u) / den;
        return true;
    }
}
