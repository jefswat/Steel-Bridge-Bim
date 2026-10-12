using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Prestressed;

/// <summary>
/// Places the mild reinforcing of a precast beam (<see cref="BeamReinforcementDefinition"/>) along the generated beam: every
/// <see cref="BeamBarSet"/> path at its stations (chains from the end of the beam, measured along the beam), at both ends for
/// symmetric standards. Transverse bars stay square to the beam (vertical section planes as the beam body); a station is measured
/// along the beam CL from the end face at the CL, so at a SKEWED end the bars near it may cross the end face - those are reported
/// (the standard sheets draw square ends only), never moved. Longitudinal bars with <see cref="BeamBarSet.StationAtBar"/> keep their
/// clear distance to the end face at their own x.
/// </summary>
public static class PrestressedBeamRebar
{
    /// <summary>Beam geometry from the generator: P(s, x, y) = world point at plan-chord parameter s, x right of the CL, y above the
    /// beam bottom; SA / SB = s of the start / end face at (x, y); kSlope = length along the beam per unit s.</summary>
    public sealed record BeamFrame(Func<double, double, double, Vec3> P, Func<double, double, double> SA, Func<double, double, double> SB,
        double KSlope, double Depth);

    /// <summary>Result for one beam: bars, and per mark the bars crossing an end face (count, largest distance outside, meters).</summary>
    public sealed record Placed(List<BarPlacement> Bars, Dictionary<string, (int Count, double Max)> OutsideEnd);

    /// <summary>Stations of a dimension chain: <paramref name="first"/> then each group "n spaces @ s" (meters). The first value is a bar.</summary>
    public static List<double> Chain(double first, params (int Count, double Spacing)[] groups)
    {
        var r = new List<double> { first };
        double s = first;
        foreach (var (n, sp) in groups)
            for (int k = 0; k < n; k++) r.Add(s += sp);
        return r;
    }

    /// <summary>Places every set of <paramref name="rd"/> on one beam; bars get <paramref name="unit"/> and Group "Beam".</summary>
    public static Placed Place(BeamReinforcementDefinition rd, string unit, BeamFrame f)
    {
        var bars = new List<BarPlacement>();
        var outside = new Dictionary<string, (int, double)>();
        double tol = In(1.0 / 16);
        // beam length along the CL at the top (= end to end of beam; the station of the far end)
        double lenS = f.SB(0, f.Depth) - f.SA(0, f.Depth);
        foreach (var set in rd.Bars)
        {
            if (set.Unresolved.Length > 0 || set.Path.Count == 0) continue;
            var copies = set.Copies is { Count: > 0 } c ? c : new List<BeamBarOffset> { new(0, 0) };
            var variants = new List<List<BeamBarPoint>>();
            foreach (var o in copies)
            {
                variants.Add(set.Path.Select(p => new BeamBarPoint(p.A, p.X + o.X, p.Y + o.Y)).ToList());
                if (set.Mirror) variants.Add(set.Path.Select(p => new BeamBarPoint(p.A, -(p.X + o.X), p.Y + o.Y)).ToList());
            }
            // (end, station) pairs: stations from the start; from the end, skip those that coincide with a start station (a bar at midspan)
            var places = new List<(int end, double st)>();
            foreach (double st in set.Stations) places.Add((0, st + set.AlongOffset));
            if (set.Ends == BeamBarEnds.Both)
                foreach (double st in set.Stations)
                {
                    double fromStart = lenS * f.KSlope - st;
                    if (set.Stations.Any(a => Math.Abs(a - fromStart) < tol)) continue;
                    places.Add((1, st + set.AlongOffset));
                }
            foreach (var (end, st) in places)
                foreach (var v in variants)
                {
                    var local = Round(v.Select(p => new Vec3(p.A, p.X, p.Y)).ToList(), set.BendRadius);
                    double dir = end == 0 ? 1 : -1;
                    // Station reference: transverse bars - the end face at the CL at each point's height (in elevation the bars are parallel
                    // to that end face: plumb ends -> vertical, ends square to a sloped beam -> square); longitudinal bars - the face at the bar.
                    double S(Vec3 q)
                    {
                        double face = set.StationAtBar
                            ? (end == 0 ? f.SA(v[0].X, v[0].Y) : f.SB(v[0].X, v[0].Y))
                            : (end == 0 ? f.SA(0, q.Z) : f.SB(0, q.Z));
                        return face + dir * (st + q.X) / f.KSlope;
                    }
                    var pts = local.Select(q => f.P(S(q), q.Y, q.Z)).ToArray();
                    bars.Add(new BarPlacement { Mark = set.Mark, Points = pts, Group = "Beam", Unit = unit });
                    // inside the beam length? (points within the beam depth only; projections into the deck are outside by design)
                    double worst = 0;
                    foreach (var q in local)
                    {
                        if (q.Z > f.Depth - 1e-6) continue;
                        double s = S(q);
                        double outA = f.SA(q.Y, q.Z) - s, outB = s - f.SB(q.Y, q.Z);
                        worst = Math.Max(worst, Math.Max(outA, outB) * f.KSlope);
                    }
                    if (worst > 1e-4)
                    {
                        outside.TryGetValue(set.Mark, out var w);
                        outside[set.Mark] = (w.Item1 + 1, Math.Max(w.Item2, worst));
                    }
                }
        }
        return new Placed(bars, outside);
    }

    /// <summary>Polyline with every interior corner replaced by a tangent arc of the centerline radius (in the plane of the two legs).</summary>
    public static List<Vec3> Round(IReadOnlyList<Vec3> pts, double radius)
    {
        if (radius <= 0 || pts.Count < 3) return pts.ToList();
        var res = new List<Vec3> { pts[0] };
        for (int i = 1; i < pts.Count - 1; i++)
        {
            var corner = pts[i];
            var u = (pts[i - 1] - corner).Normalized();
            var v = (pts[i + 1] - corner).Normalized();
            double ang = Math.Acos(Math.Clamp(Vec3.Dot(u, v), -1, 1));
            if (ang < 1e-6 || ang > Math.PI - 1e-6) { res.Add(corner); continue; }
            double t = radius / Math.Tan(ang / 2);
            var p = corner + u * t;
            var q = corner + v * t;
            var c = corner + (u + v).Normalized() * (radius / Math.Sin(ang / 2));
            var a = p - c;
            var b = q - c;
            double sweep = Math.PI - ang;
            int n = Math.Max(2, (int)Math.Ceiling(sweep / (Math.PI / 12)));
            // b' = component of b normal to a, for the rotation within the plane
            var bn = (b - a * (Vec3.Dot(a, b) / Vec3.Dot(a, a))).Normalized() * radius;
            for (int k = 0; k <= n; k++)
            {
                double th = sweep * k / n;
                res.Add(c + a * Math.Cos(th) + bn * Math.Sin(th));
            }
        }
        res.Add(pts[^1]);
        // drop consecutive duplicates (corners whose arcs meet, e.g. a 180-degree hairpin top)
        var clean = new List<Vec3> { res[0] };
        foreach (var p in res.Skip(1))
            if ((p - clean[^1]).Length > 1e-7) clean.Add(p);
        return clean;
    }

    /// <summary>Developed centerline length of a set (rounded corners), meters.</summary>
    public static double DevelopedLength(BeamBarSet set)
    {
        var pts = Round(set.Path.Select(p => new Vec3(p.A, p.X, p.Y)).ToList(), set.BendRadius);
        double l = 0;
        for (int i = 1; i < pts.Count; i++) l += (pts[i] - pts[i - 1]).Length;
        return l;
    }

    /// <summary>Schedule shape of a set: the rounded centerline projected onto the plane of its two largest extents (bend diagram),
    /// printed out-to-out dims as A, B, C, ... (drawn along the sharp path segments), FixedLength = the listed length (0 = developed).</summary>
    public static BarShape Shape(BeamBarSet set)
    {
        var sharp = set.Path.Select(p => new Vec3(p.A, p.X, p.Y)).ToList();
        var round = Round(sharp, set.BendRadius);
        double ext(Func<Vec3, double> g) => round.Max(g) - round.Min(g);
        var axes = new (Func<Vec3, double> g, double e)[] { (p => p.X, ext(p => p.X)), (p => p.Y, ext(p => p.Y)), (p => p.Z, ext(p => p.Z)) }
            .OrderByDescending(a => a.e).Take(2).ToArray();
        Vec2 Pr(Vec3 p) => new(axes[0].g(p), axes[1].g(p));
        var shape = new BarShape
        {
            Code = set.ShapeCode, Description = set.Description == "" ? set.ShapeCode : set.Description,
            Centerline = round.Select(Pr).ToArray(), PinDiameter = set.BendRadius > 0 ? Math.Max(0, 2 * set.BendRadius - RebarCatalog.Size(set.Size).Diameter) : 0,
            FixedLength = set.ListedLength > 0 ? set.ListedLength : null,
        };
        for (int k = 0; k < set.PrintedDims.Count; k++)
        {
            var a = Pr(sharp[Math.Min(k, sharp.Count - 1)]);
            var b = Pr(sharp[Math.Min(k + 1, sharp.Count - 1)]);
            var d = b - a;
            var n = d.Length > 1e-9 ? d.Normalized().Left : new Vec2(0, 1);
            shape.Dimensions.Add(new BarDimension(((char)('A' + k)).ToString(), set.PrintedDims[k], a, b, n));
        }
        return shape;
    }
}
