using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

namespace SBM.Core.Framing;

/// <summary>
/// The as-designed plan curve of one girder (<see cref="GirderPlanGeometry"/>): tangents and circular arcs in world
/// coordinates, parameterized by length t along the girder from the start point. Extended backwards along the start
/// tangent and forwards along the last element. <see cref="OffsetAt"/> expresses it as the offset normal to the
/// alignment at an alignment distance, which is what the framing generator builds girders from.
/// </summary>
public sealed class GirderPlanCurve
{
    private readonly Alignment _align;
    private readonly Vec2[] _p;     // start point of each element
    private readonly Vec2[] _t;     // unit tangent at the start of each element
    private readonly double[] _s;   // start length of each element
    private readonly GirderPlanElement[] _e;
    private readonly double _dStart;

    public Vec2 Start => _p[0];
    public double TotalLength { get; }

    public GirderPlanCurve(Alignment align, GirderPlanGeometry g)
    {
        _align = align;
        var (a0, ang0) = align.EvaluateAtDistance(align.StationToDistance(g.StartStation));
        var right = Vec2.FromAngle(ang0).Right;
        var start = a0 + right * g.StartOffset;
        double heading = ang0 - Numerics.DegToRad(g.StartDirectionDegrees);
        _dStart = align.StationToDistance(g.StartStation);

        int n = g.Elements.Count;
        _e = g.Elements.ToArray();
        _p = new Vec2[n]; _t = new Vec2[n]; _s = new double[n];
        var p = start; var t = Vec2.FromAngle(heading); double s = 0;
        for (int i = 0; i < n; i++)
        {
            _p[i] = p; _t[i] = t; _s[i] = s;
            (p, t) = Advance(_e[i], p, t, _e[i].Length);
            s += _e[i].Length;
        }
        TotalLength = s;
    }

    private static (Vec2 p, Vec2 t) Advance(GirderPlanElement e, Vec2 p, Vec2 t, double len)
    {
        if (e.Kind == GirderPlanElementKind.Line) return (p + t * len, t);
        double sgn = e.Kind == GirderPlanElementKind.ArcLeft ? 1 : -1;
        var center = p + (sgn > 0 ? t.Left : t.Right) * e.Radius;
        double th = sgn * len / e.Radius;
        return (center + (p - center).Rotate(th), t.Rotate(th));
    }

    /// <summary>Point and unit tangent at length t along the girder (extended beyond both ends).</summary>
    public (Vec2 point, Vec2 tangent) At(double t)
    {
        if (t <= 0) return (_p[0] + _t[0] * t, _t[0]);
        int k = _s.Length - 1;
        while (k > 0 && _s[k] > t) k--;
        return Advance(_e[k], _p[k], _t[k], t - _s[k]);
    }

    /// <summary>
    /// Offset (right positive) of the girder from the alignment, measured along the alignment normal at distance d:
    /// the point of the plan curve whose alignment foot is at d.
    /// </summary>
    public double OffsetAt(double d)
    {
        var (c, ang) = _align.EvaluateAtDistance(d);
        var tan = Vec2.FromAngle(ang);
        var rt = tan.Right;
        double t = d - _dStart;
        for (int it = 0; it < 60; it++)
        {
            var (pt, tg) = At(t);
            double f = Vec2.Dot(pt - c, tan);
            double fp = Vec2.Dot(tg, tan);
            if (fp < 0.2) fp = 0.2;
            double dt = f / fp;
            t -= dt;
            if (Math.Abs(dt) < 1e-10) break;
        }
        return Vec2.Dot(At(t).point - c, rt);
    }
}
