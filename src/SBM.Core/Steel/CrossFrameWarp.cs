using SBM.Core.Framing;
using SBM.Core.Geometry;

namespace SBM.Core.Steel;

/// <summary>
/// Cross frames on their connection plates (default since 2026-10-27, not with the legacy <c>CrossFrameDefinition.PlatesAtEachTopOfWeb</c>): the plate
/// each frame end really bolts to (the stiffener the framing built there, incl. the plan direction a through-web pair took from the other bay) and the
/// OPTIONAL WARPED frame (<c>StandardFramePlane.Warped</c>): each end's gussets lie in their own plate's plane and every member runs straight between its
/// work points with its section rolled linearly from the first end's plate orientation to the second's (ruled frame surface, as the shop beats it to fit).
/// </summary>
public sealed partial class SteelModelGenerator
{
    /// <summary>Members whose roll between the two ends is below this (degrees) are built untwisted (exact extrusions).</summary>
    public const double WarpRollTolerance = 0.001;

    private Dictionary<int, List<StiffenerResult>>? _stiffByGirder;
    private readonly object _stiffByGirderLock = new();

    /// <summary>Connection plate (intermediate frames) or bearing stiffener (support frames) at girder distance s, as BuildStiffener builds it: origin on the
    /// mid-plane at the web centre line, top of web (lowered at a dapped end), up axis, and the plate's plan direction oriented like <paramref name="u3"/>.
    /// Null when the framing built no such plate within 2 cm.</summary>
    private (PlateEndGeometry e, Vec3 ux)? PlateEnd(GirderResult g, double s, bool atSupport, Vec3 u3)
    {
        if (_stiffByGirder == null)
            lock (_stiffByGirderLock)
                _stiffByGirder ??= _f.Stiffeners.GroupBy(x => x.Girder).ToDictionary(x => x.Key, x => x.ToList());
        if (!_stiffByGirder.TryGetValue(g.Index, out var list)) return null;
        var kind = atSupport ? StiffenerKind.Bearing : StiffenerKind.ConnectionPlate;
        StiffenerResult? best = null;
        double bd = 0.02;
        foreach (var x in list)
        {
            if (x.Kind != kind || x.Jacking) continue;
            double d = Math.Abs(x.GirderDistance - s);
            if (d < bd) { bd = d; best = x; }
        }
        if (best == null) return null;
        var u = new Vec3(best.Direction.X, best.Direction.Y, 0);
        var y = PlateUp(best.Plane, best.Kind == StiffenerKind.Bearing, g.Path, best.GirderDistance, u);
        var p = g.Path.At(best.GirderDistance).point;
        var o = p - Up * DapDrop(g.Index, best.GirderDistance);
        var ux = Vec3.Dot(u, u3) >= 0 ? u : u * -1;
        return (new PlateEndGeometry(o, y, TrueFlangeNormal(g.Path, best.GirderDistance), p - Up * Sec.WebDepth, _top[g.Index].Thickness(best.GirderDistance)), ux);
    }

    /// <summary>The plate's mid-plane as a frame plane that keeps the frame's 2D coordinates at the plate origin (x along the plate's plan direction,
    /// z along the plate's up axis).</summary>
    private static FramePlane PlatePlane(FramePlane frame, PlateEndGeometry e, Vec3 ux)
    {
        var wt = (e.Up - ux * Vec3.Dot(e.Up, ux)).Normalized();
        var (xz, _) = frame.Local(e.Origin);
        return new FramePlane { Origin = e.Origin - ux * xz.X, U = ux, Wt = wt, Z0 = xz.Y };
    }

    /// <summary>Plane between the two end planes at frame point <paramref name="pc"/> (centre gusset of a K frame, X crossing): mean axes, through the mean
    /// of the two ends' images of pc.</summary>
    private static FramePlane CenterPlane(FramePlane l, FramePlane r, Vec2 pc)
    {
        var u = (l.U + r.U).Normalized();
        var wt = l.Wt + r.Wt;
        wt = (wt - u * Vec3.Dot(wt, u)).Normalized();
        var m = (l.W(pc, 0) + r.W(pc, 0)) * 0.5;
        return new FramePlane { Origin = m - u * pc.X, U = u, Wt = wt, Z0 = pc.Y };
    }

    private static WarpMember WarpedMember(Member m, FramePlane p1, FramePlane p2) => new(m, p1, p2);

    /// <summary>A member of a warped frame: straight between its work points (each mapped by its own end's plane), section rolled linearly about the
    /// work line from end 1's plate orientation to end 2's.</summary>
    private sealed class WarpMember
    {
        private readonly Vec3 _w1, _d, _a, _m1, _n1;
        private readonly double _len, _roll;
        public double RollDegrees => _roll * 180 / Math.PI;
        public bool Twisted { get; }
        /// <summary>Largest distance of a built section point from the work line (for the leg-tip deviation).</summary>
        public double RMax { get; private set; }

        public WarpMember(Member m, FramePlane p1, FramePlane p2)
        {
            _w1 = p1.W(m.Wp1, 0);
            var w2 = p2.W(m.Wp2, 0);
            _d = w2 - _w1;
            _len = (m.Wp2 - m.Wp1).Length;
            _a = _d.Normalized();
            (Vec3 mm, Vec3 nn) Ortho(Vec3 mv, Vec3 nv)
            {
                var mo = (mv - _a * Vec3.Dot(mv, _a)).Normalized();
                var no = (nv - _a * Vec3.Dot(nv, _a) - mo * Vec3.Dot(nv, mo)).Normalized();
                return (mo, no);
            }
            (_m1, _n1) = Ortho(p1.Dir(m.M), p1.N);
            var (_, n2) = Ortho(p2.Dir(m.M), p2.N);
            _roll = Math.Atan2(Vec3.Dot(Vec3.Cross(_n1, n2), _a), Vec3.Dot(_n1, n2));
            Twisted = Math.Abs(RollDegrees) > WarpRollTolerance;
        }

        private double Tau(double t) => _len > 0 ? t / _len : 0;
        private (Vec3 m, Vec3 n) Axes(double t)
        {
            if (!Twisted) return (_m1, _n1);
            double th = _roll * Tau(t), c = Math.Cos(th), s = Math.Sin(th);
            return (_m1 * c + Vec3.Cross(_a, _m1) * s, _n1 * c + Vec3.Cross(_a, _n1) * s);
        }
        public Vec3 MDir(double t) => Axes(t).m;
        public Vec3 NDir(double t) => Axes(t).n;
        public Vec3 Map(double t, double q, double v)
        {
            RMax = Math.Max(RMax, Math.Sqrt(q * q + v * v));
            var (mv, nv) = Axes(t);
            return _w1 + _d * Tau(t) + mv * q + nv * v;
        }
        /// <summary>Rings for a loft from t0 to t1 (one ring per 0.25 deg of roll, at least the two ends).</summary>
        public List<Vec3[]> Rings(IReadOnlyList<Vec2> sec, double t0, double t1)
        {
            double part = Math.Abs(RollDegrees) * Math.Abs(t1 - t0) / Math.Max(_len, 1e-9);
            int n = Twisted ? Math.Max(1, (int)Math.Ceiling(part / 0.25)) : 1;
            var rings = new List<Vec3[]>(n + 1);
            for (int k = 0; k <= n; k++)
            {
                double t = t0 + (t1 - t0) * k / n;
                rings.Add(sec.Select(q => Map(t, q.X, q.Y)).ToArray());
            }
            return rings;
        }
        /// <summary>Deviation of the farthest section point at the far end from an untwisted member (2 r sin(roll / 2)).</summary>
        public double TipDeviation => 2 * RMax * Math.Abs(Math.Sin(_roll / 2));
    }
}
