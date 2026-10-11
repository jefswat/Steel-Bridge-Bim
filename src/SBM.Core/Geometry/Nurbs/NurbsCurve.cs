namespace SBM.Core.Geometry.Nurbs;

/// <summary>
/// NURBS curve in 3D (world meters): degree p, full knot vector, control points and optional weights (null = polynomial
/// B-spline, all weights 1). Evaluation by Cox-de Boor; rational curves through homogeneous coordinates.
/// </summary>
public sealed class NurbsCurve
{
    public int Degree { get; }
    public double[] Knots { get; }
    public Vec3[] Control { get; }
    public double[]? Weights { get; }

    public NurbsCurve(int degree, double[] knots, Vec3[] control, double[]? weights = null)
    {
        Degree = degree;
        Knots = knots;
        Control = control;
        Weights = weights;
        if (BSpline.Check(knots, degree, control.Length) is { } err) throw new ArgumentException("Invalid NURBS curve: " + err);
        if (weights != null && (weights.Length != control.Length || weights.Any(w => !(w > 0)))) throw new ArgumentException("Invalid NURBS weights");
    }

    public bool IsRational => Weights != null;
    public double Start => Knots[Degree];
    public double End => Knots[Control.Length];

    public Vec3 PointAt(double u) => Eval(u, 0).p;

    /// <summary>Point and first derivative.</summary>
    public (Vec3 p, Vec3 d1) Derivative(double u) { var e = Eval(u, 1); return (e.p, e.d1); }

    /// <summary>Point, first and second derivatives.</summary>
    public (Vec3 p, Vec3 d1, Vec3 d2) Eval(double u, int nd = 2)
    {
        int p = Degree, n = Control.Length - 1;
        u = Math.Clamp(u, Start, End);
        int span = BSpline.FindSpan(n, p, u, Knots);
        nd = Math.Min(nd, p);
        var N = BSpline.DersBasisFuns(span, u, p, Math.Max(nd, 0), Knots);
        Span<Vec3> A = stackalloc Vec3[3];
        Span<double> W = stackalloc double[3];
        for (int k = 0; k <= nd; k++)
        {
            Vec3 a = Vec3.Zero;
            double w = 0;
            for (int j = 0; j <= p; j++)
            {
                int i = span - p + j;
                double wi = Weights?[i] ?? 1;
                a += Control[i] * (N[k, j] * wi);
                w += N[k, j] * wi;
            }
            A[k] = a;
            W[k] = w;
        }
        if (Weights == null) return (A[0], nd >= 1 ? A[1] : Vec3.Zero, nd >= 2 ? A[2] : Vec3.Zero);
        // C = A / W; C' = (A' - W' C) / W; C'' = (A'' - 2 W' C' - W'' C) / W
        var c0 = A[0] * (1 / W[0]);
        var c1 = nd >= 1 ? (A[1] - c0 * W[1]) * (1 / W[0]) : Vec3.Zero;
        var c2 = nd >= 2 ? (A[2] - c1 * (2 * W[1]) - c0 * W[2]) * (1 / W[0]) : Vec3.Zero;
        return (c0, c1, c2);
    }

    /// <summary>Same curve translated by <paramref name="d"/>.</summary>
    public NurbsCurve Translated(Vec3 d) => new(Degree, Knots, Control.Select(c => c + d).ToArray(), Weights);

    /// <summary>Arc length by Gauss-Legendre quadrature per knot span.</summary>
    public double Length()
    {
        double len = 0;
        for (int i = Degree; i < Control.Length; i++)
        {
            double a = Knots[i], b = Knots[i + 1];
            if (b <= a) continue;
            foreach (var (x, w) in Gauss.Points(8, a, b)) len += Derivative(x).d1.Length * w;
        }
        return len;
    }
}

/// <summary>Gauss-Legendre quadrature nodes and weights.</summary>
public static class Gauss
{
    private static readonly Dictionary<int, (double[] x, double[] w)> Cache = new();

    public static IEnumerable<(double x, double w)> Points(int n, double a, double b)
    {
        var (x, w) = Rule(n);
        double h = (b - a) / 2, m = (a + b) / 2;
        for (int i = 0; i < n; i++) yield return (m + h * x[i], h * w[i]);
    }

    public static (double[] x, double[] w) Rule(int n)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(n, out var r)) return r;
            var x = new double[n];
            var w = new double[n];
            for (int i = 0; i < n; i++)
            {
                double z = Math.Cos(Math.PI * (i + 0.75) / (n + 0.5)), pp = 0;
                for (int it = 0; it < 100; it++)
                {
                    double p1 = 1, p2 = 0;
                    for (int j = 1; j <= n; j++) { double p3 = p2; p2 = p1; p1 = ((2 * j - 1) * z * p2 - (j - 1) * p3) / j; }
                    pp = n * (z * p1 - p2) / (z * z - 1);
                    double z1 = z;
                    z = z1 - p1 / pp;
                    if (Math.Abs(z - z1) < 1e-15) break;
                }
                x[i] = z;
                w[i] = 2 / ((1 - z * z) * pp * pp);
            }
            return Cache[n] = (x, w);
        }
    }
}
