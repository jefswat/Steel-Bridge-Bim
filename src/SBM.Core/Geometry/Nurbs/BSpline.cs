namespace SBM.Core.Geometry.Nurbs;

/// <summary>
/// B-spline basis functions (Cox-de Boor recursion, Piegl &amp; Tiller "The NURBS Book" A2.1-A2.3) and knot-vector helpers.
/// Knot vectors are full (every knot repeated by its multiplicity): m + 1 = n + p + 2 values for n + 1 control points.
/// </summary>
public static class BSpline
{
    /// <summary>Knot span index i with U[i] &lt;= u &lt; U[i+1] (the last non-empty span at the end of the domain).</summary>
    public static int FindSpan(int n, int p, double u, IReadOnlyList<double> U)
    {
        if (u >= U[n + 1]) return n;
        if (u <= U[p]) return p;
        int lo = p, hi = n + 1;
        int mid = (lo + hi) / 2;
        while (u < U[mid] || u >= U[mid + 1])
        {
            if (u < U[mid]) hi = mid; else lo = mid;
            mid = (lo + hi) / 2;
        }
        return mid;
    }

    /// <summary>The p + 1 non-zero basis functions N[i-p..i] at u (Cox-de Boor, triangular scheme).</summary>
    public static void BasisFuns(int i, double u, int p, IReadOnlyList<double> U, Span<double> N)
    {
        Span<double> left = stackalloc double[p + 1], right = stackalloc double[p + 1];
        N[0] = 1;
        for (int j = 1; j <= p; j++)
        {
            left[j] = u - U[i + 1 - j];
            right[j] = U[i + j] - u;
            double saved = 0;
            for (int r = 0; r < j; r++)
            {
                double temp = N[r] / (right[r + 1] + left[j - r]);
                N[r] = saved + right[r + 1] * temp;
                saved = left[j - r] * temp;
            }
            N[j] = saved;
        }
    }

    /// <summary>Basis functions and their derivatives up to order <paramref name="nd"/>: ders[k, j] = k-th derivative of N[i-p+j] (A2.3).</summary>
    public static double[,] DersBasisFuns(int i, double u, int p, int nd, IReadOnlyList<double> U)
    {
        var ndu = new double[p + 1, p + 1];
        var left = new double[p + 1];
        var right = new double[p + 1];
        ndu[0, 0] = 1;
        for (int j = 1; j <= p; j++)
        {
            left[j] = u - U[i + 1 - j];
            right[j] = U[i + j] - u;
            double saved = 0;
            for (int r = 0; r < j; r++)
            {
                ndu[j, r] = right[r + 1] + left[j - r];
                double temp = ndu[r, j - 1] / ndu[j, r];
                ndu[r, j] = saved + right[r + 1] * temp;
                saved = left[j - r] * temp;
            }
            ndu[j, j] = saved;
        }
        var ders = new double[nd + 1, p + 1];
        for (int j = 0; j <= p; j++) ders[0, j] = ndu[j, p];
        var a = new double[2, p + 1];
        for (int r = 0; r <= p; r++)
        {
            int s1 = 0, s2 = 1;
            a[0, 0] = 1;
            for (int k = 1; k <= nd; k++)
            {
                double d = 0;
                int rk = r - k, pk = p - k;
                if (r >= k)
                {
                    a[s2, 0] = a[s1, 0] / ndu[pk + 1, rk];
                    d = a[s2, 0] * ndu[rk, pk];
                }
                int j1 = rk >= -1 ? 1 : -rk;
                int j2 = r - 1 <= pk ? k - 1 : p - r;
                for (int j = j1; j <= j2; j++)
                {
                    a[s2, j] = (a[s1, j] - a[s1, j - 1]) / ndu[pk + 1, rk + j];
                    d += a[s2, j] * ndu[rk + j, pk];
                }
                if (r <= pk)
                {
                    a[s2, k] = -a[s1, k - 1] / ndu[pk + 1, r];
                    d += a[s2, k] * ndu[r, pk];
                }
                ders[k, r] = d;
                (s1, s2) = (s2, s1);
            }
        }
        int f = p;
        for (int k = 1; k <= nd; k++)
        {
            for (int j = 0; j <= p; j++) ders[k, j] *= f;
            f *= p - k;
        }
        return ders;
    }

    /// <summary>Clamped knot vector: <paramref name="interior"/> knots (ascending, each listed once per multiplicity) between
    /// <paramref name="a"/> and <paramref name="b"/>, which are repeated p + 1 times.</summary>
    public static double[] Clamped(int p, double a, double b, IEnumerable<double> interior)
    {
        var list = new List<double>();
        for (int i = 0; i <= p; i++) list.Add(a);
        list.AddRange(interior);
        for (int i = 0; i <= p; i++) list.Add(b);
        return list.ToArray();
    }

    /// <summary>Distinct knot values and their multiplicities (the compact form STEP / IFC use).</summary>
    public static (double[] knots, int[] mults) Compact(IReadOnlyList<double> U)
    {
        var k = new List<double>();
        var m = new List<int>();
        foreach (double u in U)
        {
            if (k.Count > 0 && u == k[^1]) m[^1]++;
            else { k.Add(u); m.Add(1); }
        }
        return (k.ToArray(), m.ToArray());
    }

    /// <summary>Validity of a knot vector for <paramref name="nCtrl"/> control points of degree p: count, non-decreasing,
    /// end multiplicity at most p + 1, interior multiplicity at most p, non-empty domain. Returns null when valid.</summary>
    public static string? Check(IReadOnlyList<double> U, int p, int nCtrl)
    {
        if (p < 1) return "degree < 1";
        if (nCtrl < p + 1) return $"{nCtrl} control points for degree {p}";
        if (U.Count != nCtrl + p + 1) return $"knot count {U.Count} != control points {nCtrl} + degree {p} + 1";
        for (int i = 1; i < U.Count; i++) if (U[i] < U[i - 1]) return $"knots decrease at {i}";
        var (k, m) = Compact(U);
        if (m[0] > p + 1 || m[^1] > p + 1) return "end multiplicity > degree + 1";
        for (int i = 1; i < m.Length - 1; i++) if (m[i] > p) return $"interior multiplicity {m[i]} > degree at {k[i]}";
        if (!(U[nCtrl] > U[p])) return "empty parameter domain";
        return null;
    }
}
