using SBM.Core.Alignments;
using SBM.Core.Geometry;

namespace SBM.Core.Framing;

/// <summary>
/// A kinked (polyline) end-of-slab line in plan, from the plans' break points (<see cref="Bridge.SupportDefinition.DeckEndPoints"/>).
/// Points run left to right and must be monotone along the overall direction (first to last point); the first and the last segment
/// extend without limit (to the deck edges). The deck lies up-station of a BEGIN line and down-station of an END line.
/// "Inside" tests treat the line as a graph over its overall direction, so a point's side is exact; <see cref="SignedDistance"/> is the
/// distance normal to the segment the point projects onto (exact away from the kinks, a close measure within a few inches of them).
/// </summary>
public sealed class DeckEndPolyline
{
    public IReadOnlyList<Vec2> Points { get; }
    public bool AtBegin { get; }
    /// <summary>Unit direction first → last point (left → right).</summary>
    public Vec2 Direction { get; }
    private readonly double[] _s;

    public DeckEndPolyline(IReadOnlyList<Vec2> points, bool atBegin)
    {
        if (points.Count < 2) throw new ArgumentException("A deck end polyline needs at least two points.");
        Points = points;
        AtBegin = atBegin;
        Direction = (points[^1] - points[0]).Normalized();
        _s = points.Select(p => Vec2.Dot(p - points[0], Direction)).ToArray();
    }

    /// <summary>True when the points advance along the overall direction (required; checked by the generator).</summary>
    public bool IsMonotone
    {
        get
        {
            for (int i = 1; i < _s.Length; i++) if (_s[i] <= _s[i - 1] + 1e-9) return false;
            return true;
        }
    }

    public int SegmentCount => Points.Count - 1;

    /// <summary>Unit normal of segment i pointing into the deck.</summary>
    public Vec2 Normal(int i)
    {
        var t = (Points[i + 1] - Points[i]).Normalized();
        return AtBegin ? t.Left : -t.Left;
    }

    /// <summary>Unit normal into the deck of the overall direction (for the straight-line approximations: block coordinates, bar directions).</summary>
    public Vec2 OverallNormal => AtBegin ? Direction.Left : -Direction.Left;

    /// <summary>Segment whose extent along the overall direction contains the projection of p (first / last extend).</summary>
    public int SegmentAt(Vec2 p)
    {
        double s = Vec2.Dot(p - Points[0], Direction);
        for (int i = 1; i < _s.Length - 1; i++) if (s < _s[i]) return i - 1;
        return _s.Length - 2;
    }

    /// <summary>Distance of a plan point from the line (shortest, to any segment; the first and last extend without limit), + toward the deck.</summary>
    public double SignedDistance(Vec2 p)
    {
        int k = SegmentAt(p);
        double side = Vec2.Dot(p - Points[k], Normal(k));
        double best = double.MaxValue;
        for (int i = 0; i + 1 < Points.Count; i++)
        {
            var a = Points[i]; var ab = Points[i + 1] - a;
            double t = Vec2.Dot(p - a, ab) / Math.Max(ab.LengthSquared, 1e-24);
            if (i > 0) t = Math.Max(t, 0);
            if (i + 2 < Points.Count) t = Math.Min(t, 1);
            best = Math.Min(best, Vec2.Distance(p, a + ab * t));
        }
        return side >= 0 ? best : -best;
    }

    /// <summary>The line moved by <paramref name="inset"/> toward the deck (each segment parallel; vertices = intersections of neighbouring
    /// offset segments; middle segments that vanish are dropped).</summary>
    public DeckEndPolyline Inset(double inset)
    {
        if (Math.Abs(inset) < 1e-12) return this;
        var pts = Points.ToList();
        while (true)
        {
            int n = pts.Count;
            Vec2 Nrm(int i) { var t = (pts[i + 1] - pts[i]).Normalized(); return AtBegin ? t.Left : -t.Left; }
            var res = new List<Vec2>(n) { pts[0] + Nrm(0) * inset };
            for (int i = 1; i + 1 < n; i++)
            {
                var a0 = pts[i - 1] + Nrm(i - 1) * inset; var a1 = pts[i] + Nrm(i - 1) * inset;
                var b0 = pts[i] + Nrm(i) * inset; var b1 = pts[i + 1] + Nrm(i) * inset;
                res.Add(Intersect(a0, a1, b0, b1) ?? b0);
            }
            res.Add(pts[^1] + Nrm(n - 2) * inset);
            if (n > 2)
            {
                // the first / last segments extend without limit: keep their outer points beyond the new vertices (a short end segment must not turn around)
                var d0 = (pts[1] - pts[0]).Normalized(); var d1 = (pts[^1] - pts[^2]).Normalized();
                res[0] = res[1] - d0 * Math.Max(Vec2.Distance(pts[0], pts[1]), 1.0);
                res[^1] = res[^2] + d1 * Math.Max(Vec2.Distance(pts[^1], pts[^2]), 1.0);
            }
            // a middle segment that turned around has vanished at this inset: drop it (its neighbours' lines meet directly) and redo
            int bad = -1;
            for (int i = 1; i + 2 < n; i++)
                if (Vec2.Dot(res[i + 1] - res[i], pts[i + 1] - pts[i]) <= 0) { bad = i; break; }
            var cand = new DeckEndPolyline(res, AtBegin);
            if (bad < 0 && cand.IsMonotone) return cand;
            if (bad < 0)
            {
                if (n <= 3) return new DeckEndPolyline(new List<Vec2> { res[0], res[^1] }, AtBegin);
                // not monotone without a reversed segment (a spike): drop the shortest middle segment
                bad = Enumerable.Range(1, n - 3).OrderBy(i => Vec2.Distance(res[i], res[i + 1])).First();
            }
            var x = Intersect(pts[bad - 1], pts[bad], pts[bad + 1], pts[bad + 2]) ?? Vec2.Lerp(pts[bad], pts[bad + 1], 0.5);
            pts.RemoveAt(bad + 1);
            pts[bad] = x;
            for (int i = pts.Count - 1; i > 0; i--) if (Vec2.Distance(pts[i], pts[i - 1]) < 1e-9) pts.RemoveAt(i == pts.Count - 1 ? i - 1 : i);
            if (pts.Count < 2) return new DeckEndPolyline(new List<Vec2> { res[0], res[^1] }, AtBegin);
        }
    }

    private static Vec2? Intersect(Vec2 a0, Vec2 a1, Vec2 b0, Vec2 b1)
    {
        var da = a1 - a0; var db = b1 - b0;
        double den = Vec2.Cross(da, db);
        if (Math.Abs(den) < 1e-15) return null;
        return a0 + da * (Vec2.Cross(b0 - a0, db) / den);
    }

    /// <summary>Keeps the part of the plan segment p0→p1 that lies in the deck (signed distance ≥ 0). Returns false when nothing is left.
    /// When the segment enters and leaves more than once, the longest inside piece is kept.</summary>
    public bool Clip(ref Vec2 p0, ref Vec2 p1)
    {
        var ts = new List<double> { 0, 1 };
        double s0 = Vec2.Dot(p0 - Points[0], Direction), s1 = Vec2.Dot(p1 - Points[0], Direction);
        if (Math.Abs(s1 - s0) > 1e-12)
            for (int i = 1; i < _s.Length - 1; i++) { double t = (_s[i] - s0) / (s1 - s0); if (t > 0 && t < 1) ts.Add(t); }
        ts.Sort();
        var a = p0; var b = p1;
        // inside intervals: between two breakpoints the point stays over one segment, so its side is linear there
        var pieces = new List<(double t0, double t1)>();
        double? start = null;
        for (int k = 0; k + 1 < ts.Count; k++)
        {
            if (ts[k + 1] - ts[k] < 1e-12) continue;
            int seg = SegmentAt(Vec2.Lerp(a, b, (ts[k] + ts[k + 1]) / 2));
            var nrm = Normal(seg);
            double fa = Vec2.Dot(Vec2.Lerp(a, b, ts[k]) - Points[seg], nrm), fb = Vec2.Dot(Vec2.Lerp(a, b, ts[k + 1]) - Points[seg], nrm);
            if (fa >= 0 && start == null) start = ts[k];
            if ((fa >= 0) != (fb >= 0))
            {
                double tc = ts[k] + (ts[k + 1] - ts[k]) * (fa / (fa - fb));
                if (fa >= 0) { pieces.Add((start ?? ts[k], tc)); start = null; }
                else start = tc;
            }
        }
        if (start is double st) pieces.Add((st, 1));
        if (pieces.Count == 0) return false;
        var best = pieces.OrderByDescending(q => q.t1 - q.t0).First();
        p0 = Vec2.Lerp(a, b, best.t0);
        p1 = Vec2.Lerp(a, b, best.t1);
        return true;
    }

    /// <summary>Alignment distance where an offset curve crosses this line (root of the signed distance along the curve nearest the straight-line guess).</summary>
    public bool CrossOffsetCurve(Alignment a, Func<double, double> offsetAtD, out double distance)
    {
        // guess: the crossing with the infinite line of the segment nearest the curve
        var mid = Vec2.Lerp(Points[0], Points[^1], 0.5);
        var inv = a.Inverse(mid);
        double o = offsetAtD(inv.Distance);
        double guess = inv.Distance;
        int seg = SegmentCount / 2;
        for (int it = 0; it < 3; it++)
        {
            var q = a.PointAtDistance(guess, offsetAtD(guess));
            seg = SegmentAt(q);
            var p = Points[seg]; var dir = (Points[seg + 1] - Points[seg]).Normalized();
            var ip = a.Inverse(p);
            double w0 = Math.Abs(offsetAtD(ip.Distance) - ip.Offset) * 2 + 15;
            if (a.IntersectOffsetCurveWithLine(offsetAtD, p, dir, ip.Distance - w0, ip.Distance + w0, ip.Distance, out double dl)) guess = dl;
            else break;
        }
        double span = Vec2.Distance(Points[0], Points[^1]);
        double w = Math.Abs(o - inv.Offset) + span + 15;
        double F(double d) => SignedDistance(a.PointAtDistance(d, offsetAtD(d)));
        var roots = Numerics.FindRoots(F, guess - w, guess + w, Math.Clamp((int)Math.Ceiling(2 * w / 0.25), 16, 20000));
        if (roots.Count == 0) { distance = guess; return false; }
        distance = roots.OrderBy(r => Math.Abs(r - guess)).First();
        return true;
    }

    /// <summary>Interior vertices (not the extended ends) that lie between two offset curves (the deck edges), left to right.</summary>
    public IEnumerable<Vec2> VerticesBetween(Alignment a, Func<double, double> leftAtD, Func<double, double> rightAtD)
    {
        for (int i = 1; i + 1 < Points.Count; i++)
        {
            var so = a.Inverse(Points[i]);
            if (so.Offset > leftAtD(so.Distance) + 1e-6 && so.Offset < rightAtD(so.Distance) - 1e-6) yield return Points[i];
        }
    }
}
