namespace SBM.Core.Geometry.Nurbs;

/// <summary>Several curves fitted on one knot vector (so they can be paired into ruled / tensor surfaces).</summary>
public sealed record CurveFit(int Degree, double[] Knots, Vec3[][] Control, double MaxDeviation, bool Converged)
{
    public NurbsCurve Curve(int k) => new(Degree, Knots, Control[k]);
}

/// <summary>
/// Fits B-splines to EXACT generator geometry (a function of the curve parameter, never to triangles): least squares on a
/// common clamped knot vector with the end points (and the points at C0 breaks) interpolated exactly, then adaptive knot
/// insertion in every span whose deviation from the exact function exceeds the tolerance. Deviation is measured as the
/// distance between fitted and exact point at the SAME parameter (an upper bound of the geometric distance).
/// </summary>
public static class NurbsFit
{
    /// <summary>
    /// Fits <paramref name="count"/> curves C_k(u) = exact(u)[k] on [u0, u1]. <paramref name="breaks"/> are parameters where the
    /// exact geometry may have a kink or a jump in curvature: they become knots of multiplicity p (C0) and are interpolated.
    /// </summary>
    public static CurveFit FitCommon(int count, Func<double, Vec3[]> exact, double u0, double u1, IEnumerable<double> breaks, double tol,
        int degree = 3, double initialSpan = 10, int maxControl = 4000, int samplesPerSpan = 6)
    {
        int p = degree;
        double L = u1 - u0;
        double eps = Math.Max(1e-9, 1e-9 * Math.Abs(L));
        var bk = new List<double>();
        foreach (double b in breaks.OrderBy(x => x))
            if (b > u0 + 1e-6 * L && b < u1 - 1e-6 * L && (bk.Count == 0 || b - bk[^1] > 1e-6 * L)) bk.Add(b);
        var bounds = new List<double> { u0 };
        bounds.AddRange(bk);
        bounds.Add(u1);
        var interior = new List<List<double>>();
        for (int s = 0; s + 1 < bounds.Count; s++)
        {
            double a = bounds[s], b = bounds[s + 1];
            int ns = Math.Max(1, (int)Math.Ceiling((b - a) / initialSpan - 1e-9));
            interior.Add(Enumerable.Range(1, ns - 1).Select(i => a + (b - a) * i / ns).ToList());
        }

        var cache = new Dictionary<double, Vec3[]>();
        Vec3 origin = exact(u0)[0];
        Vec3[] Q(double u)
        {
            if (!cache.TryGetValue(u, out var q))
            {
                q = exact(u);
                for (int k = 0; k < q.Length; k++) q[k] -= origin;
                cache[u] = q;
            }
            return q;
        }

        CurveFit? last = null;
        for (int iter = 0; iter < 40; iter++)
        {
            var inner = new List<double>();
            for (int s = 0; s < interior.Count; s++)
            {
                inner.AddRange(interior[s]);
                if (s + 1 < interior.Count) for (int r = 0; r < p; r++) inner.Add(bounds[s + 1]);
            }
            var U = BSpline.Clamped(p, u0, u1, inner);
            int n = U.Length - p - 1;
            var spans = new List<(double a, double b)>();
            for (int i = p; i < n; i++) if (U[i + 1] > U[i]) spans.Add((U[i], U[i + 1]));
            int m = Math.Max(samplesPerSpan, p + 1);
            var prm = new List<double>();
            foreach (var (a, b) in spans) for (int j = 0; j < m; j++) prm.Add(a + (b - a) * j / m);
            prm.Add(u1);

            // fixed control points: ends and C0 breaks (their basis function is 1 there)
            var fixedIdx = new Dictionary<int, Vec3[]>();
            fixedIdx[0] = Q(u0);
            fixedIdx[n - 1] = Q(u1);
            Span<double> Nb = stackalloc double[p + 1];
            foreach (double b in bk)
            {
                int sp = BSpline.FindSpan(n - 1, p, b, U);
                BSpline.BasisFuns(sp, b, p, U, Nb);
                int best = 0;
                for (int j = 1; j <= p; j++) if (Nb[j] > Nb[best]) best = j;
                fixedIdx[sp - p + best] = Q(b);
            }
            var freeOf = new int[n];
            int nf = 0;
            for (int i = 0; i < n; i++) freeOf[i] = fixedIdx.ContainsKey(i) ? -1 : nf++;

            var ctrl = new Vec3[count][];
            for (int k = 0; k < count; k++)
            {
                ctrl[k] = new Vec3[n];
                foreach (var (i, v) in fixedIdx) ctrl[k][i] = v[k];
            }
            if (nf > 0)
            {
                var band = new double[nf, p + 1];         // band[i, d] = M[i, i - d]
                var rhs = new Vec3[nf, count];
                foreach (double u in prm)
                {
                    int sp = BSpline.FindSpan(n - 1, p, u, U);
                    BSpline.BasisFuns(sp, u, p, U, Nb);
                    var q = Q(u);
                    var r = new Vec3[count];
                    for (int k = 0; k < count; k++) r[k] = q[k];
                    for (int j = 0; j <= p; j++)
                    {
                        int i = sp - p + j;
                        if (freeOf[i] < 0) for (int k = 0; k < count; k++) r[k] -= ctrl[k][i] * Nb[j];
                    }
                    for (int j = 0; j <= p; j++)
                    {
                        int fi = freeOf[sp - p + j];
                        if (fi < 0) continue;
                        for (int k = 0; k < count; k++) rhs[fi, k] += r[k] * Nb[j];
                        for (int l = 0; l <= j; l++)
                        {
                            int fl = freeOf[sp - p + l];
                            if (fl < 0) continue;
                            band[fi, fi - fl] += Nb[j] * Nb[l];
                        }
                    }
                }
                if (!BandCholesky(band, p)) return last ?? new CurveFit(p, U, ctrl.Select(c => c.Select(x => x + origin).ToArray()).ToArray(), double.PositiveInfinity, false);
                var sol = BandSolve(band, p, rhs, count);
                for (int i = 0; i < n; i++)
                    if (freeOf[i] >= 0) for (int k = 0; k < count; k++) ctrl[k][i] = sol[freeOf[i], k];
            }

            // deviation per span at the sample midpoints and the samples
            double maxDev = 0;
            var bad = new List<(double a, double b)>();
            foreach (var (a, b) in spans)
            {
                double dev = 0;
                for (int j = 0; j < 2 * m; j++)
                {
                    double u = a + (b - a) * (j + 0.5) / (2 * m);
                    int sp = BSpline.FindSpan(n - 1, p, u, U);
                    BSpline.BasisFuns(sp, u, p, U, Nb);
                    var q = Q(u);
                    for (int k = 0; k < count; k++)
                    {
                        var c = Vec3.Zero;
                        for (int jj = 0; jj <= p; jj++) c += ctrl[k][sp - p + jj] * Nb[jj];
                        dev = Math.Max(dev, (c - q[k]).Length);
                    }
                }
                maxDev = Math.Max(maxDev, dev);
                if (dev > tol && b - a > eps * 16) bad.Add((a, b));
            }
            var world = ctrl.Select(c => c.Select(x => x + origin).ToArray()).ToArray();
            last = new CurveFit(p, U, world, maxDev, maxDev <= tol);
            if (maxDev <= tol || bad.Count == 0 || n > maxControl) return last;
            foreach (var (a, b) in bad)
            {
                int s = 0;
                while (s + 1 < bounds.Count - 1 && (a + b) / 2 > bounds[s + 1]) s++;
                interior[s].Add((a + b) / 2);
                interior[s].Sort();
            }
        }
        return last!;
    }

    /// <summary>In-place banded Cholesky of a symmetric positive definite matrix stored as band[i, d] = M[i, i - d], d = 0..w.</summary>
    private static bool BandCholesky(double[,] a, int w)
    {
        int n = a.GetLength(0);
        for (int i = 0; i < n; i++)
        {
            for (int d = Math.Min(w, i); d >= 0; d--)
            {
                int j = i - d;
                double s = a[i, d];
                for (int k = Math.Max(0, i - w); k < j; k++)
                {
                    if (i - k > w || j - k > w) continue;
                    s -= a[i, i - k] * a[j, j - k];
                }
                if (d == 0)
                {
                    if (!(s > 1e-300)) return false;
                    a[i, 0] = Math.Sqrt(s);
                }
                else a[i, d] = s / a[j, 0];
            }
        }
        return true;
    }

    private static Vec3[,] BandSolve(double[,] L, int w, Vec3[,] b, int count)
    {
        int n = L.GetLength(0);
        var y = new Vec3[n, count];
        for (int k = 0; k < count; k++)
        {
            for (int i = 0; i < n; i++)
            {
                var s = b[i, k];
                for (int j = Math.Max(0, i - w); j < i; j++) s -= y[j, k] * L[i, i - j];
                y[i, k] = s * (1 / L[i, 0]);
            }
            for (int i = n - 1; i >= 0; i--)
            {
                var s = y[i, k];
                for (int j = i + 1; j <= Math.Min(n - 1, i + w); j++) s -= y[j, k] * L[j, j - i];
                y[i, k] = s * (1 / L[i, 0]);
            }
        }
        return y;
    }

    /// <summary>Global interpolation (A9.1): a degree-q curve through the points at the given parameters, knots by averaging.</summary>
    public static (double[] knots, Vec3[] control) Interpolate(IReadOnlyList<double> t, IReadOnlyList<Vec3> pts, int q)
    {
        int n = pts.Count - 1;
        q = Math.Min(q, n);
        var inner = new List<double>();
        for (int j = 1; j <= n - q; j++)
        {
            double s = 0;
            for (int i = j; i < j + q; i++) s += t[i];
            inner.Add(s / q);
        }
        var U = BSpline.Clamped(q, t[0], t[n], inner);
        var A = new double[n + 1, n + 1];
        Span<double> N = stackalloc double[q + 1];
        for (int r = 0; r <= n; r++)
        {
            int sp = BSpline.FindSpan(n, q, t[r], U);
            BSpline.BasisFuns(sp, t[r], q, U, N);
            for (int j = 0; j <= q; j++) A[r, sp - q + j] = N[j];
        }
        var o = pts[0];
        var B = pts.Select(x => x - o).ToArray();
        // Gaussian elimination with partial pivoting (small systems)
        for (int c = 0; c <= n; c++)
        {
            int piv = c;
            for (int r = c + 1; r <= n; r++) if (Math.Abs(A[r, c]) > Math.Abs(A[piv, c])) piv = r;
            if (piv != c)
            {
                for (int k = 0; k <= n; k++) (A[c, k], A[piv, k]) = (A[piv, k], A[c, k]);
                (B[c], B[piv]) = (B[piv], B[c]);
            }
            for (int r = c + 1; r <= n; r++)
            {
                double f = A[r, c] / A[c, c];
                if (f == 0) continue;
                for (int k = c; k <= n; k++) A[r, k] -= f * A[c, k];
                B[r] -= B[c] * f;
            }
        }
        var X = new Vec3[n + 1];
        for (int r = n; r >= 0; r--)
        {
            var s = B[r];
            for (int k = r + 1; k <= n; k++) s -= X[k] * A[r, k];
            X[r] = s * (1 / A[r, r]);
        }
        return (U, X.Select(x => x + o).ToArray());
    }
}
