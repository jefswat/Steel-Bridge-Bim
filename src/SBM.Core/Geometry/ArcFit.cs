namespace SBM.Core.Geometry;

/// <summary>One piece of a fitted centerline: a straight run through point indices A..B, or a circular arc A → M → B.</summary>
public readonly record struct CurvePiece(bool IsArc, int A, int M, int B);

/// <summary>
/// Rebuilds a sampled 3D centerline (rebar, strand) as straight runs and circular arcs through its own sample points,
/// within a tolerance. Bends sampled on a true radius come back exact; helices (spirals) come back as short arcs
/// (each arc ≤ <see cref="MaxSweep"/>), far closer to the helix than the chords between samples.
/// </summary>
public static class ArcFit
{
    /// <summary>Largest arc sweep (radians) emitted; keeps helices accurate and arcs well-conditioned.</summary>
    public const double MaxSweep = Math.PI / 2;
    /// <summary>Largest arc radius (m) emitted.</summary>
    public const double MaxRadius = 5;

    public static List<CurvePiece> Fit(IReadOnlyList<Vec3> p, double tol)
    {
        var res = new List<CurvePiece>();
        int n = p.Count, i = 0;
        // Arcs only for planar bars (bends, hooks, ties, stirrups). Non-planar centerlines (spirals) stay straight
        // pieces through every sample: swept-disk kernels mis-build disks along chains of tilted arcs.
        if (!IsPlanar(p, tol))
        {
            for (int q = 0; q + 1 < n; q++) res.Add(new CurvePiece(false, q, q, q + 1));
            return res;
        }
        while (i < n - 1)
        {
            int jl = i + 1;
            while (jl + 1 < n && LineOk(p, i, jl + 1, tol)) jl++;
            int ja = -1;
            // At least 4 points: three always define a circle, the fourth (and more) must confirm it.
            for (int j = i + 3; j < n && ArcOk(p, i, j, tol); j++) ja = j;
            // A trailing 3-point arc (e.g. the last samples of a spiral) only when evenly spaced like arc samples.
            if (ja < 0 && jl == i + 1 && i + 2 < n && ArcOk(p, i, i + 2, tol))
            {
                double d1 = (p[i + 1] - p[i]).Length, d2 = (p[i + 2] - p[i + 1]).Length;
                if (Math.Abs(d1 - d2) <= 0.01 * Math.Max(d1, d2)) ja = i + 2;
            }
            if (ja > jl) { res.Add(new CurvePiece(true, i, (i + ja) / 2, ja)); i = ja; }
            else
            {
                // Merge consecutive straight runs into one piece only when they are collinear (handled by LineOk).
                res.Add(new CurvePiece(false, i, i, jl));
                i = jl;
            }
        }
        return KeepSmoothArcs(p, res);
    }

    /// <summary>All points within <paramref name="tol"/> of one plane (collinear counts as planar).</summary>
    public static bool IsPlanar(IReadOnlyList<Vec3> p, double tol)
    {
        if (p.Count < 4) return true;
        var a = p[0];
        int ib = 0;
        double best = 0;
        for (int k = 1; k < p.Count; k++) { double d = (p[k] - a).Length; if (d > best) { best = d; ib = k; } }
        if (best < 1e-9) return true;
        var u = (p[ib] - a) * (1 / best);
        Vec3 nrm = default;
        double off = 0;
        foreach (var q in p)
        {
            var w = Vec3.Cross(u, q - a);
            if (w.Length > off) { off = w.Length; nrm = w; }
        }
        if (off <= tol) return true;
        nrm = nrm.Normalized();
        foreach (var q in p)
            if (Math.Abs(Vec3.Dot(q - a, nrm)) > tol) return false;
        return true;
    }

    /// <summary>Largest tangent kink (radians) allowed where an arc meets its neighbors.</summary>
    public const double MaxKink = 0.05 * Math.PI / 180;

    /// <summary>
    /// Keeps only arcs that join their neighbors tangentially (true bends between straight legs). Arcs fitted to a helix
    /// are each planar and meet with small kinks; swept-disk kernels (OpenCascade in IfcOpenShell) build badly wrong
    /// solids along such chains, so those runs go back to straight pieces through every sample.
    /// </summary>
    private static List<CurvePiece> KeepSmoothArcs(IReadOnlyList<Vec3> p, List<CurvePiece> pieces)
    {
        Vec3 StartDir(CurvePiece c) => c.IsArc ? ArcTangent(p, c, atStart: true) : (p[c.A + 1] - p[c.A]).Normalized();
        Vec3 EndDir(CurvePiece c) => c.IsArc ? ArcTangent(p, c, atStart: false) : (p[c.B] - p[c.B - 1]).Normalized();
        bool Smooth(Vec3 a, Vec3 b) => Math.Atan2(Vec3.Cross(a, b).Length, Vec3.Dot(a, b)) <= MaxKink;
        var keep = new bool[pieces.Count];
        for (int k = 0; k < pieces.Count; k++)
        {
            var c = pieces[k];
            if (!c.IsArc) continue;
            bool ok = (k == 0 || Smooth(EndDir(pieces[k - 1]), StartDir(c)))
                && (k + 1 == pieces.Count || Smooth(EndDir(c), StartDir(pieces[k + 1])));
            keep[k] = ok;
        }
        var res = new List<CurvePiece>(pieces.Count);
        for (int k = 0; k < pieces.Count; k++)
        {
            var c = pieces[k];
            if (!c.IsArc || keep[k]) { res.Add(c); continue; }
            for (int q = c.A; q < c.B; q++) res.Add(new CurvePiece(false, q, q, q + 1));
        }
        return res;
    }

    private static Vec3 ArcTangent(IReadOnlyList<Vec3> p, CurvePiece c, bool atStart)
    {
        var (ctr, _, nrm) = Circle(p[c.A], p[c.M], p[c.B]);
        var at = atStart ? p[c.A] : p[c.B];
        var t = Vec3.Cross(nrm, at - ctr).Normalized();
        // Orient along the travel direction A → M → B.
        var travel = atStart ? p[c.M] - p[c.A] : p[c.B] - p[c.M];
        return Vec3.Dot(t, travel) < 0 ? -t : t;
    }

    /// <summary>Length of the fitted curve (arcs measured as arcs).</summary>
    public static double Length(IReadOnlyList<Vec3> p, IEnumerable<CurvePiece> pieces)
    {
        double len = 0;
        foreach (var c in pieces)
        {
            if (!c.IsArc) { len += (p[c.B] - p[c.A]).Length; continue; }
            var (ctr, r, _) = Circle(p[c.A], p[c.M], p[c.B]);
            double t1 = Angle(p[c.A] - ctr, p[c.M] - ctr), t2 = Angle(p[c.M] - ctr, p[c.B] - ctr);
            len += r * (t1 + t2);
        }
        return len;
    }

    private static double Angle(Vec3 a, Vec3 b) => Math.Atan2(Vec3.Cross(a, b).Length, Vec3.Dot(a, b));

    private static bool LineOk(IReadOnlyList<Vec3> p, int i, int j, double tol)
    {
        var a = p[i];
        var d = p[j] - a;
        double l = d.Length;
        if (l < 1e-9) return false;
        var u = d * (1 / l);
        for (int k = i + 1; k < j; k++)
        {
            var q = p[k] - a;
            double t = Vec3.Dot(q, u);
            if (t < -tol || t > l + tol || (q - u * t).Length > tol) return false;
        }
        return true;
    }

    /// <summary>Circumcircle of three points: center, radius, unit normal (radius 0 if collinear).</summary>
    private static (Vec3 c, double r, Vec3 n) Circle(Vec3 a, Vec3 b, Vec3 c)
    {
        var ab = b - a;
        var ac = c - a;
        var w = Vec3.Cross(ab, ac);
        double w2 = Vec3.Dot(w, w);
        if (w2 < 1e-18) return (a, 0, default);
        var ctr = a + (Vec3.Cross(w, ab) * Vec3.Dot(ac, ac) + Vec3.Cross(ac, w) * Vec3.Dot(ab, ab)) * (1 / (2 * w2));
        return (ctr, (a - ctr).Length, w * (1 / Math.Sqrt(w2)));
    }

    private static bool ArcOk(IReadOnlyList<Vec3> p, int i, int j, double tol)
    {
        int m = (i + j) / 2;
        var (ctr, r, nrm) = Circle(p[i], p[m], p[j]);
        // Only real bends, hoops and spirals; long gently curved bars (curved decks) stay polylines.
        if (r <= 0 || r > MaxRadius) return false;
        // Points must advance monotonically around the circle, within the sweep limit.
        var x = p[i] - ctr;
        x -= nrm * Vec3.Dot(x, nrm);
        var y = Vec3.Cross(nrm, x);
        double prev = 0;
        for (int k = i + 1; k <= j; k++)
        {
            var q = p[k] - ctr;
            double h = Vec3.Dot(q, nrm);
            var qp = q - nrm * h;
            if (Math.Sqrt(h * h + Math.Pow(qp.Length - r, 2)) > tol) return false;
            double ang = Math.Atan2(Vec3.Dot(qp, y), Vec3.Dot(qp, x));
            if (ang < 0) ang += 2 * Math.PI;
            if (ang <= prev - 1e-9 || ang > MaxSweep + 1e-9) return false;
            prev = ang;
        }
        return true;
    }
}
