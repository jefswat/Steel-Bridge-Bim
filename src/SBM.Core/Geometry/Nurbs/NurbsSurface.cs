namespace SBM.Core.Geometry.Nurbs;

/// <summary>
/// Tensor-product NURBS surface: Control[i, j] with i along u (degree p, KnotsU) and j along v (degree q, KnotsV),
/// optional weights (null = polynomial). Normal = Su x Sv.
/// </summary>
public sealed class NurbsSurface
{
    public int DegreeU { get; }
    public int DegreeV { get; }
    public double[] KnotsU { get; }
    public double[] KnotsV { get; }
    public Vec3[,] Control { get; }
    public double[,]? Weights { get; }

    public NurbsSurface(int degreeU, int degreeV, double[] knotsU, double[] knotsV, Vec3[,] control, double[,]? weights = null)
    {
        DegreeU = degreeU;
        DegreeV = degreeV;
        KnotsU = knotsU;
        KnotsV = knotsV;
        Control = control;
        Weights = weights;
        if (BSpline.Check(knotsU, degreeU, control.GetLength(0)) is { } e1) throw new ArgumentException("Invalid NURBS surface (u): " + e1);
        if (BSpline.Check(knotsV, degreeV, control.GetLength(1)) is { } e2) throw new ArgumentException("Invalid NURBS surface (v): " + e2);
        if (weights != null && (weights.GetLength(0) != control.GetLength(0) || weights.GetLength(1) != control.GetLength(1))) throw new ArgumentException("Invalid NURBS weights");
    }

    public int CountU => Control.GetLength(0);
    public int CountV => Control.GetLength(1);
    public bool IsRational => Weights != null;
    public double U0 => KnotsU[DegreeU];
    public double U1 => KnotsU[CountU];
    public double V0 => KnotsV[DegreeV];
    public double V1 => KnotsV[CountV];

    /// <summary>Ruled surface between two compatible curves (same degree, knots and weights): v = 0 on <paramref name="a"/>, v = 1 on <paramref name="b"/>.</summary>
    public static NurbsSurface Ruled(NurbsCurve a, NurbsCurve b)
    {
        if (a.Degree != b.Degree || a.Control.Length != b.Control.Length || !a.Knots.SequenceEqual(b.Knots))
            throw new ArgumentException("Ruled surface needs compatible curves");
        int n = a.Control.Length;
        var c = new Vec3[n, 2];
        double[,]? w = a.Weights != null || b.Weights != null ? new double[n, 2] : null;
        for (int i = 0; i < n; i++)
        {
            c[i, 0] = a.Control[i];
            c[i, 1] = b.Control[i];
            if (w != null)
            {
                double wa = a.Weights?[i] ?? 1, wb = b.Weights?[i] ?? 1;
                if (Math.Abs(wa - wb) > 1e-12) throw new ArgumentException("Ruled surface needs equal weights");
                w[i, 0] = wa;
                w[i, 1] = wb;
            }
        }
        return new NurbsSurface(a.Degree, 1, a.Knots, new double[] { 0, 0, 1, 1 }, c, w);
    }

    public Vec3 PointAt(double u, double v) => Eval(u, v).p;

    /// <summary>Point and first partial derivatives.</summary>
    public (Vec3 p, Vec3 su, Vec3 sv) Eval(double u, double v)
    {
        int p = DegreeU, q = DegreeV;
        u = Math.Clamp(u, U0, U1);
        v = Math.Clamp(v, V0, V1);
        int su = BSpline.FindSpan(CountU - 1, p, u, KnotsU), sv = BSpline.FindSpan(CountV - 1, q, v, KnotsV);
        var Nu = BSpline.DersBasisFuns(su, u, p, 1, KnotsU);
        var Nv = BSpline.DersBasisFuns(sv, v, q, 1, KnotsV);
        Vec3 A = Vec3.Zero, Au = Vec3.Zero, Av = Vec3.Zero;
        double W = 0, Wu = 0, Wv = 0;
        for (int k = 0; k <= p; k++)
        {
            int i = su - p + k;
            for (int l = 0; l <= q; l++)
            {
                int j = sv - q + l;
                double w = Weights?[i, j] ?? 1;
                var P = Control[i, j];
                double b = Nu[0, k] * Nv[0, l] * w, bu = Nu[1, k] * Nv[0, l] * w, bv = Nu[0, k] * Nv[1, l] * w;
                A += P * b; Au += P * bu; Av += P * bv;
                W += b; Wu += bu; Wv += bv;
            }
        }
        if (Weights == null) return (A, Au, Av);
        var S = A * (1 / W);
        return (S, (Au - S * Wu) * (1 / W), (Av - S * Wv) * (1 / W));
    }

    public Vec3 NormalAt(double u, double v)
    {
        var (_, a, b) = Eval(u, v);
        return Vec3.Cross(a, b).Normalized();
    }

    /// <summary>Same surface with the v direction reversed (flips the normal).</summary>
    public NurbsSurface ReversedV()
    {
        int nu = CountU, nv = CountV;
        var c = new Vec3[nu, nv];
        double[,]? w = Weights != null ? new double[nu, nv] : null;
        for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                c[i, j] = Control[i, nv - 1 - j];
                if (w != null) w[i, j] = Weights![i, nv - 1 - j];
            }
        double a = KnotsV[0], b = KnotsV[^1];
        var k = KnotsV.Reverse().Select(x => a + b - x).ToArray();
        return new NurbsSurface(DegreeU, DegreeV, KnotsU, k, c, w);
    }

    public NurbsSurface Translated(Vec3 d)
    {
        var c = new Vec3[CountU, CountV];
        for (int i = 0; i < CountU; i++) for (int j = 0; j < CountV; j++) c[i, j] = Control[i, j] + d;
        return new NurbsSurface(DegreeU, DegreeV, KnotsU, KnotsV, c, Weights);
    }

    /// <summary>Iso-curve at constant u = U0 or U1 (exact: the boundary control row) or v = V0 / V1.</summary>
    public NurbsCurve Boundary(bool alongU, bool atEnd)
    {
        if (alongU)
        {
            int j = atEnd ? CountV - 1 : 0;
            var c = Enumerable.Range(0, CountU).Select(i => Control[i, j]).ToArray();
            var w = Weights == null ? null : Enumerable.Range(0, CountU).Select(i => Weights[i, j]).ToArray();
            return new NurbsCurve(DegreeU, KnotsU, c, w);
        }
        else
        {
            int i = atEnd ? CountU - 1 : 0;
            var c = Enumerable.Range(0, CountV).Select(j => Control[i, j]).ToArray();
            var w = Weights == null ? null : Enumerable.Range(0, CountV).Select(j => Weights[i, j]).ToArray();
            return new NurbsCurve(DegreeV, KnotsV, c, w);
        }
    }

    /// <summary>Closest point by Gauss-Newton from a start parameter (local).</summary>
    public (double u, double v, Vec3 p) ClosestPoint(Vec3 q, double u, double v, int iterations = 30)
    {
        Vec3 p = default;
        for (int it = 0; it < iterations; it++)
        {
            var (s, a, b) = Eval(u, v);
            p = s;
            var r = q - s;
            double aa = Vec3.Dot(a, a), ab = Vec3.Dot(a, b), bb = Vec3.Dot(b, b);
            double ra = Vec3.Dot(r, a), rb = Vec3.Dot(r, b);
            double det = aa * bb - ab * ab;
            if (Math.Abs(det) < 1e-30) break;
            double du = (ra * bb - rb * ab) / det, dv = (rb * aa - ra * ab) / det;
            double nu = Math.Clamp(u + du, U0, U1), nv = Math.Clamp(v + dv, V0, V1);
            bool done = Math.Abs(nu - u) < 1e-13 * (1 + Math.Abs(u)) && Math.Abs(nv - v) < 1e-13 * (1 + Math.Abs(v));
            u = nu; v = nv;
            if (done) break;
        }
        return (u, v, PointAt(u, v));
    }
}
