using SBM.Core.Alignments;
using SBM.Core.Geometry;

namespace SBM.Core.Framing;

/// <summary>
/// Plan geometry of one girder within one span, from CL bearing to CL bearing, parameterized by u ∈ [0, 1].
/// Curved girders follow the alignment at a (possibly varying) offset; chorded girders are straight.
/// An arc-length table maps between u and distance along the girder.
/// </summary>
public sealed class GirderSpanCurve
{
    private const int TableSize = 256;

    public int Girder { get; }
    public int Span { get; }
    public bool Chorded { get; }

    /// <summary>Alignment distances at the start/end bearing of this girder.</summary>
    public double D0 { get; }
    public double D1 { get; }

    public Vec2 Start { get; }
    public Vec2 End { get; }

    public double Length { get; }

    private readonly Alignment _align;
    private readonly Func<double, double> _offsetAt;
    private readonly double[] _s = new double[TableSize + 1];

    public GirderSpanCurve(Alignment align, Func<double, double> offsetAt, int girder, int span, double d0, double d1, bool chorded)
    {
        _align = align;
        _offsetAt = offsetAt;
        Girder = girder;
        Span = span;
        D0 = d0;
        D1 = d1;
        Chorded = chorded;
        Start = align.PointAtDistance(d0, offsetAt(d0));
        End = align.PointAtDistance(d1, offsetAt(d1));

        var prev = PointAt(0);
        _s[0] = 0;
        for (int i = 1; i <= TableSize; i++)
        {
            var p = PointAt((double)i / TableSize);
            _s[i] = _s[i - 1] + Vec2.Distance(prev, p);
            prev = p;
        }
        Length = _s[TableSize];
    }

    /// <summary>Alignment distance associated with u (exact for curved girders, approximate for chorded).</summary>
    public double DistanceAt(double u) => D0 + (D1 - D0) * u;

    public double OffsetAtU(double u) => _offsetAt(DistanceAt(u));

    public Vec2 PointAt(double u)
    {
        if (Chorded) return Vec2.Lerp(Start, End, u);
        double d = DistanceAt(u);
        return _align.PointAtDistance(d, _offsetAt(d));
    }

    public Vec2 TangentAt(double u)
    {
        if (Chorded) return (End - Start).Normalized();
        const double h = 1e-5;
        double a = Math.Max(u - h, -0.05), b = Math.Min(u + h, 1.05);
        return (PointAt(b) - PointAt(a)).Normalized();
    }

    /// <summary>Distance along the girder from the start bearing at parameter u.</summary>
    public double SAt(double u)
    {
        if (u <= 0) return u * (_s[1] * TableSize);
        if (u >= 1) return Length + (u - 1) * ((_s[TableSize] - _s[TableSize - 1]) * TableSize);
        double x = u * TableSize;
        int i = Math.Min((int)x, TableSize - 1);
        return _s[i] + (_s[i + 1] - _s[i]) * (x - i);
    }

    /// <summary>Parameter u at distance s along the girder from the start bearing.</summary>
    public double UAt(double s)
    {
        if (Length <= 0) return 0;
        if (s <= 0) return s / Length;
        if (s >= Length) return 1 + (s - Length) / Length;
        int lo = 0, hi = TableSize;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (_s[mid] <= s) lo = mid; else hi = mid;
        }
        double seg = _s[hi] - _s[lo];
        double f = seg > 0 ? (s - _s[lo]) / seg : 0;
        return (lo + f) / TableSize;
    }

    /// <summary>Intersects an infinite line with this girder; returns the u nearest to <paramref name="uGuess"/>.</summary>
    public bool Intersect(Vec2 linePoint, Vec2 lineDir, out double u, double uGuess = 0.5, double margin = 0.02)
    {
        var dir = lineDir.Normalized();
        if (Chorded)
        {
            var c = End - Start;
            if (Numerics.IntersectLines(Start, c, linePoint, dir, out double t, out _) && t >= -margin && t <= 1 + margin)
            {
                u = t;
                return true;
            }
            u = double.NaN;
            return false;
        }
        double F(double x) => Vec2.Cross(dir, PointAt(x) - linePoint);
        var roots = Numerics.FindRoots(F, -margin, 1 + margin, 64, 1e-12);
        if (roots.Count == 0) { u = double.NaN; return false; }
        u = roots.OrderBy(r => Math.Abs(r - uGuess)).First();
        return true;
    }

    public List<Vec2> Sample(int minSegments = 8, double maxAngle = 0.01, double maxSegment = 3.0)
    {
        int n = minSegments;
        if (!Chorded)
        {
            double k = 0;
            for (int i = 0; i <= 8; i++) k = Math.Max(k, Math.Abs(_align.CurvatureAtDistance(DistanceAt(i / 8.0))));
            n = Math.Max(n, (int)Math.Ceiling(Math.Max(k * Length / maxAngle, Length / maxSegment)));
        }
        else n = 1;
        n = Math.Min(n, 4000);
        var pts = new List<Vec2>(n + 1);
        for (int i = 0; i <= n; i++) pts.Add(PointAt((double)i / n));
        return pts;
    }
}
