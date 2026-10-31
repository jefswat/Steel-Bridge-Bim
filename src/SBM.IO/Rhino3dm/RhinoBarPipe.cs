using SBM.Core.Geometry;
using SBM.Core.Geometry.Nurbs;
using SCurve = SBM.Core.Geometry.Nurbs.NurbsCurve;
using SSurface = SBM.Core.Geometry.Nurbs.NurbsSurface;

namespace SBM.IO.Rhino3dm;

/// <summary>
/// Exact pipe of a bar / strand: the centerline (own fit <see cref="Fit"/>: collinear runs = lines, runs of >= 4 cocircular samples = arcs of any sweep) swept by the bar circle. Every piece is
/// four faces (one per quarter of the circle, so no face has a seam): a straight piece = ruled quarter cylinder, an arc piece = the quarter
/// circle REVOLVED about the arc axis (rational quadratic in both directions = exact torus patch); planar end caps bounded by the four quarter
/// arcs. The circle frame is carried along the bar (arcs rotate it exactly, straight pieces re-align it square to the piece), so consecutive
/// pieces share their junction circle and the faces form ONE closed polysurface in <see cref="RhinoBrepBuilder"/>.
/// </summary>
internal static class RhinoBarPipe
{
    /// <summary>Largest angle between the carried circle plane and a piece's start tangent for which the pipe is still built (small turns
    /// between pieces that are not exact circles); beyond it (a real kink, a helix sample) the caller keeps the mesh tube. A tilted junction
    /// circle makes the next ruled / revolved face slightly oblique (radius error r (1 / cos k - 1), 1.5 % at 10 deg).</summary>
    public const double MaxKinkDeg = 10.0;

    private static readonly double W45 = Math.Sqrt(0.5);

    /// <summary>Faces of the pipe; <paramref name="innerRadius"/> > 0 = HOLLOW pipe (duct / pipe wall): the outer quarter faces, the same faces at the
    /// inner radius with reversed normals (into the bore) and annular end caps, one closed polysurface.</summary>
    public static List<FaceSpec>? Faces(IReadOnlyList<Vec3> pts, double radius, out string? why, double innerRadius = 0)
    {
        why = null;
        if (innerRadius < 0 || innerRadius >= radius) { why = "inner radius not inside the pipe"; return null; }
        var p = new List<Vec3>(pts.Count);
        foreach (var q in pts) if (p.Count == 0 || (q - p[^1]).Length > 1e-7) p.Add(q);
        if (p.Count < 2 || !(radius > 0)) { why = "no centerline"; return null; }
        var pieces = Fit(p);
        if (pieces.Count == 0) { why = "no centerline"; return null; }
        // a centerline that comes back within one bar diameter of itself (lap tails drawn on top of a leg, a closed hoop) would weld its pipe
        // onto itself - not a solid; the mesh tube is kept
        if (SelfOverlap(p, radius)) { why = "self-overlapping centerline"; return null; }

        // start frame: T along the first piece, (T, N, B) right-handed (B = T x N, N x B = T)
        var t0 = StartTangent(p, pieces[0]);
        var (n0, _) = NurbsArc.Axes(t0);
        var c = p[pieces[0].A];
        var nn = (n0 - t0 * Vec3.Dot(n0, t0)).Normalized();
        var bb = Vec3.Cross(t0, nn);
        var (c0, n0s, b0s) = (c, nn, bb);
        // one face per circle quarter along the WHOLE bar: rows of quarter-arc control points, pieces joined at double knots (C0 rows); rows are
        // generated per radius (outer wall, and the bore of a hollow pipe)
        var rows = new List<Func<double, Vec3[][]>> { r => Circle(c0, n0s, b0s, r) };
        var rowW = new List<double> { 1 };
        double cosKink = Math.Cos(MaxKinkDeg * Math.PI / 180);
        foreach (var pc in pieces)
        {
            var tc = Vec3.Cross(nn, bb);
            if (Vec3.Dot(tc, StartTangent(p, pc)) < cosKink) { why = "kink in the centerline"; return null; }
            var start = Circle(c, nn, bb, radius);
            var (cs, ns, bs) = (c, nn, bb);
            if (!pc.IsArc)
            {
                var d = p[pc.B] - p[pc.A];
                if (d.Length < 1e-9) continue;
                var dn = d.Normalized();
                // re-align the frame square to this piece (minimal rotation), so arcs never accumulate a tilt
                (nn, bb) = Transport(nn, bb, tc, dn);
                c += d;
                var end = Circle(c, nn, bb, radius);
                // a tilted start circle on a piece shorter than its tilt would fold the ruled face
                for (int k = 0; k < 4; k++)
                    for (int i = 0; i < 3; i += 2)
                        if (Vec3.Dot(end[k][i] - start[k][i], dn) < 0.1 * d.Length) { why = "short piece at a kink"; return null; }
                var (ce, ne, be) = (c, nn, bb);
                rows.Add(r => { var s0 = Circle(cs, ns, bs, r); var e0 = Circle(ce, ne, be, r); return Enumerable.Range(0, 4).Select(k => s0[k].Zip(e0[k], (a, b) => (a + b) * 0.5).ToArray()).ToArray(); }); rowW.Add(1);
                rows.Add(r => Circle(ce, ne, be, r)); rowW.Add(1);
            }
            else
            {
                var (ctr, axis, theta) = ArcOf(p[pc.A], p[pc.M], p[pc.B]);
                if (!(theta > 1e-9) || theta >= 2 * Math.PI - 1e-6) { why = "arc piece sweep"; return null; }
                // revolve in steps of at most 90 deg (one rational quadratic span each)
                int nsub = Math.Max(1, (int)Math.Ceiling(theta / (Math.PI / 2) - 1e-9));
                double dt = theta / nsub, h = Math.Cos(dt / 2);
                for (int sub = 0; sub < nsub; sub++)
                {
                    var (cq, nq, bq) = (c, nn, bb);
                    double hh = h;
                    rows.Add(r => { var st = Circle(cq, nq, bq, r); var mid = new Vec3[4][]; for (int k = 0; k < 4; k++) mid[k] = st[k].Select(q => Rotate(q, ctr, axis, dt / 2, 1 / hh)).ToArray(); return mid; });
                    c = Rotate(c, ctr, axis, dt, 1);
                    nn = RotateDir(nn, axis, dt);
                    bb = RotateDir(bb, axis, dt);
                    rowW.Add(h);
                    var (cr, nr, br) = (c, nn, bb);
                    rows.Add(r => Circle(cr, nr, br, r)); rowW.Add(1);
                }
            }
        }
        if (rows.Count < 3) { why = "no centerline"; return null; }
        var faces = new List<FaceSpec> { Cap(c0, n0s, b0s, radius, innerRadius, start: true) };
        var outer = rows.Select(g => g(radius)).ToList();
        for (int k = 0; k < 4; k++) faces.Add(Quarter(outer, rowW, k, false));
        if (innerRadius > 0)
        {
            var inner = rows.Select(g => g(innerRadius)).ToList();
            for (int k = 0; k < 4; k++) faces.Add(Quarter(inner, rowW, k, true));
        }
        faces.Add(Cap(c, nn, bb, radius, innerRadius, start: false));
        return faces;
    }

    /// <summary>Pieces of a sampled bar centerline: maximal runs of collinear points = one line, maximal runs of >= 4 points on one circle
    /// (0.05 mm, coplanar; the true-radius bends RebarBends samples at <= 15 deg per segment, any sweep) = one arc (M = a middle sample);
    /// anything else = the single segment (a real kink, a helix sample).</summary>
    internal static List<CurvePiece> Fit(List<Vec3> p)
    {
        const double tol = 5e-5;
        var res = new List<CurvePiece>();
        int n = p.Count, s = 0;
        while (s < n - 1)
        {
            int eLine = s + 1;
            while (eLine + 1 < n && Collinear(p, s, eLine + 1, tol)) eLine++;
            int eArc = -1;
            if (s + 3 < n && Cocircular(p, s, s + 3, tol))
            {
                eArc = s + 3;
                while (eArc + 1 < n && Cocircular(p, s, eArc + 1, tol)) eArc++;
            }
            if (eArc > eLine) { res.Add(new CurvePiece(true, s, (s + eArc) / 2, eArc)); s = eArc; }
            else { res.Add(new CurvePiece(false, s, s, eLine)); s = eLine; }
        }
        return res;
    }

    private static bool Collinear(List<Vec3> p, int s, int e, double tol)
    {
        var d = p[e] - p[s]; double l = d.Length;
        if (l < 1e-9) return false;
        d = d * (1 / l);
        for (int i = s + 1; i < e; i++)
        {
            var q = p[i] - p[s]; double t = Vec3.Dot(q, d);
            if (t <= 0 || t >= l || (q - d * t).Length > tol) return false;
        }
        return true;
    }

    private static bool Cocircular(List<Vec3> p, int s, int e, double tol)
    {
        int m = (s + e) / 2;
        var ab = p[e] - p[s]; var am = p[m] - p[s];
        var nrm = Vec3.Cross(am, ab);
        double n2 = Vec3.Dot(nrm, nrm);
        if (n2 < 1e-18) return false;
        var c = p[s] + (Vec3.Cross(nrm, am) * Vec3.Dot(ab, ab) + Vec3.Cross(ab, nrm) * Vec3.Dot(am, am)) * (1 / (2 * n2));
        double r = (p[s] - c).Length;
        var un = nrm * (1 / Math.Sqrt(n2));
        double prev = 0;
        var axis = un; var r0 = p[s] - c;
        for (int i = s + 1; i <= e; i++)
        {
            var q = p[i] - c;
            if (Math.Abs(Vec3.Dot(q, un)) > tol || Math.Abs(q.Length - r) > tol) return false;
            double a = Math.Atan2(Vec3.Dot(Vec3.Cross(r0, q), axis), Vec3.Dot(r0, q));
            if (a < 0) a += 2 * Math.PI;
            if (a <= prev + 1e-9 || a > 2 * Math.PI - 1e-3) return false;    // samples advance one way, less than a full turn
            prev = a;
        }
        return true;
    }

    /// <summary>Two points of the centerline closer than 2 r whose distance ALONG the bar exceeds 4 r (the pipe would pass through itself).</summary>
    private static bool SelfOverlap(List<Vec3> p, double r)
    {
        // densify to r spacing, then compare every pair (bars have at most a few hundred such points)
        var q = new List<Vec3> { p[0] };
        var s = new List<double> { 0 };
        for (int i = 1; i < p.Count; i++)
        {
            var d = p[i] - p[i - 1];
            int n = Math.Max(1, (int)Math.Ceiling(d.Length / r));
            for (int k = 1; k <= n; k++) { q.Add(p[i - 1] + d * ((double)k / n)); s.Add(s[^1] + d.Length / n); }
        }
        // spatial hash, cell 2 r: only points in the 27 neighbouring cells are compared
        double cell = 2 * r, lim2 = 4 * r * r * 0.98;
        var grid = new Dictionary<(long, long, long), List<int>>();
        (long, long, long) Key(Vec3 v) => ((long)Math.Floor(v.X / cell), (long)Math.Floor(v.Y / cell), (long)Math.Floor(v.Z / cell));
        for (int j = 0; j < q.Count; j++)
        {
            var (x, y, z) = Key(q[j]);
            for (long i0 = x - 1; i0 <= x + 1; i0++)
                for (long j0 = y - 1; j0 <= y + 1; j0++)
                    for (long k0 = z - 1; k0 <= z + 1; k0++)
                        if (grid.TryGetValue((i0, j0, k0), out var list))
                            foreach (int i in list)
                                if (s[j] - s[i] > 4 * r && (q[j] - q[i]).Length * (q[j] - q[i]).Length < lim2) return true;
            var key = (x, y, z);
            if (!grid.TryGetValue(key, out var l2)) grid[key] = l2 = new List<int>();
            l2.Add(j);
        }
        return false;
    }

    private static Vec3 StartTangent(List<Vec3> p, CurvePiece pc)
    {
        if (!pc.IsArc) return (p[pc.B] - p[pc.A]).Normalized();
        var (ctr, axis, _) = ArcOf(p[pc.A], p[pc.M], p[pc.B]);
        return Vec3.Cross(axis, p[pc.A] - ctr).Normalized();
    }

    /// <summary>Centre, unit axis (right-handed: rotating A about it by +theta reaches B through M) and sweep of the arc through A, M, B.</summary>
    private static (Vec3 c, Vec3 axis, double theta) ArcOf(Vec3 a, Vec3 m, Vec3 b)
    {
        var ab = b - a; var am = m - a;
        var n = Vec3.Cross(am, ab);
        double n2 = Vec3.Dot(n, n);
        var c = a + (Vec3.Cross(n, am) * Vec3.Dot(ab, ab) + Vec3.Cross(ab, n) * Vec3.Dot(am, am)) * (1 / (2 * n2));
        var axis = Vec3.Cross(a - c, m - c).Normalized();
        var ra = a - c; var rb = b - c;
        var rm = m - c;
        double Ang(Vec3 u, Vec3 v) { double t = Math.Atan2(Vec3.Dot(Vec3.Cross(u, v), axis), Vec3.Dot(u, v)); return t < 0 ? t + 2 * Math.PI : t; }
        double theta = Ang(ra, rm) + Ang(rm, rb);                          // through M: sweeps beyond 180 deg too
        return (c, axis, theta);
    }

    private static Vec3 RotateDir(Vec3 v, Vec3 k, double a) =>
        v * Math.Cos(a) + Vec3.Cross(k, v) * Math.Sin(a) + k * (Vec3.Dot(k, v) * (1 - Math.Cos(a)));

    /// <summary>Point rotated about the line (o, k) by a; its distance from the line scaled by s (s = 1 / cos(a) for a revolve's middle control point).</summary>
    private static Vec3 Rotate(Vec3 q, Vec3 o, Vec3 k, double a, double s)
    {
        var f = o + k * Vec3.Dot(q - o, k);
        return f + RotateDir(q - f, k, a) * s;
    }

    /// <summary>Rotates (N, B) by the minimal rotation taking t onto d.</summary>
    private static (Vec3, Vec3) Transport(Vec3 n, Vec3 b, Vec3 t, Vec3 d)
    {
        var ax = Vec3.Cross(t, d);
        double s = ax.Length, cc = Vec3.Dot(t, d);
        if (s < 1e-12) return (n, b);
        double a = Math.Atan2(s, cc);
        ax = ax * (1 / s);
        var n2 = RotateDir(n, ax, a);
        n2 = (n2 - d * Vec3.Dot(n2, d)).Normalized();
        return (n2, Vec3.Cross(d, n2));
    }

    /// <summary>The four quarter arcs (3 control points each, weights 1, sqrt(1/2), 1) of the circle, counter-clockwise about N x B from N.</summary>
    private static Vec3[][] Circle(Vec3 c, Vec3 n, Vec3 b, double r)
    {
        var e = new[] { n, b, -n, -b };
        var q = new Vec3[4][];
        for (int k = 0; k < 4; k++)
        {
            var e0 = e[k]; var e1 = e[(k + 1) % 4];
            q[k] = new[] { c + e0 * r, c + (e0 + e1) * r, c + e1 * r };
        }
        return q;
    }

    /// <summary>Quarter face k over the whole bar: u = around the circle (3 control points, weights 1, sqrt(1/2), 1; Su x Sv outward), v = along
    /// the bar, degree 2: each piece = three rows (start, middle, end; middle weight 1 for a line = degree-elevated, cos(theta / 2) for a revolve),
    /// pieces share their end rows at double interior knots.</summary>
    private static FaceSpec Quarter(List<Vec3[][]> rows, List<double> rowW, int k, bool inward)
    {
        int nv = rows.Count, np = (nv - 1) / 2;
        var ctrl = new Vec3[3, nv];
        var w = new double[3, nv];
        double[] wu = { 1, W45, 1 };
        for (int j = 0; j < nv; j++)
            for (int i = 0; i < 3; i++) { ctrl[inward ? 2 - i : i, j] = rows[j][k][i]; w[inward ? 2 - i : i, j] = wu[i] * rowW[j]; }
        var kv = new List<double> { 0, 0, 0 };
        for (int i = 1; i < np; i++) { kv.Add(i); kv.Add(i); }
        kv.AddRange(new double[] { np, np, np });
        return new FaceSpec { Surface = new SSurface(2, 2, new double[] { 0, 0, 0, 1, 1, 1 }, kv.ToArray(), ctrl, w), Role = (inward ? "pipe bore quarter " : "pipe quarter ") + k };
    }

    private static FaceSpec Cap(Vec3 c, Vec3 n, Vec3 b, double r, double ri, bool start)
    {
        // outward normal X x Y: -T at the start (X = N, Y = -B), +T at the end (X = N, Y = B)
        var f = new FaceSpec { Origin = c, X = n, Y = start ? -b : b, Role = start ? "start cap" : "end cap" };
        foreach (var q in Circle(c, n, b, r))
            f.Outer.Add(new SCurve(2, new double[] { 0, 0, 0, 1, 1, 1 }, q, new[] { 1, W45, 1 }));
        if (ri > 0)
        {
            // annular cap of a hollow pipe: the bore circle is the hole
            var hole = new List<SCurve>();
            foreach (var q in Circle(c, n, b, ri)) hole.Add(new SCurve(2, new double[] { 0, 0, 0, 1, 1, 1 }, q, new[] { 1, W45, 1 }));
            f.Holes.Add(hole);
        }
        return f;
    }
}
