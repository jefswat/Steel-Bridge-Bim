using SBM.Core.Geometry;

namespace SBM.Core.Alignments;

/// <summary>Station equation: at <see cref="Distance"/> along the alignment the station jumps from back to ahead.</summary>
public sealed class StationEquation
{
    public double Distance { get; set; }
    public double StationBack { get; set; }
    public double StationAhead { get; set; }
}

/// <summary>Result of projecting a plan point onto an alignment.</summary>
public readonly record struct StationOffset(double Station, double Offset, double Distance, bool OnAlignment);

/// <summary>
/// Horizontal + vertical + cross-slope alignment. Internal geometry is parameterized by distance along the
/// alignment (meters); public APIs that take "station" use display stations which include the start
/// station and station equations. Offsets are right-positive (Civil 3D convention).
/// </summary>
public sealed class Alignment
{
    public string Name { get; set; } = "Alignment";
    public string Description { get; set; } = "";

    /// <summary>Station at distance 0.</summary>
    public double StartStation { get; set; }

    public List<StationEquation> StationEquations { get; set; } = new();

    public List<HorizontalElement> Elements { get; private set; } = new();

    /// <summary>Optional editable PI definition the elements were built from (null for imported alignments that can't be expressed by PIs).</summary>
    public PiDefinition? PiDefinition { get; set; }

    public VerticalProfile Profile { get; set; } = new();

    public CrossSlopeTable CrossSlope { get; set; } = CrossSlopeTable.NormalCrown();

    public double Length => Elements.Count == 0 ? 0 : Elements[^1].EndDistance;

    public double EndStation => DistanceToStation(Length);

    public Alignment() { }

    public Alignment(IEnumerable<HorizontalElement> elements) => SetElements(elements);

    /// <summary>Replaces the horizontal geometry and recomputes cumulative distances.</summary>
    public void SetElements(IEnumerable<HorizontalElement> elements)
    {
        Elements = elements.Where(e => e.Length > 1e-9).ToList();
        double d = 0;
        foreach (var e in Elements)
        {
            e.StartDistance = d;
            d += e.Length;
        }
    }

    /// <summary>Gaps or kinks between consecutive elements larger than tolerance.</summary>
    public List<string> CheckContinuity(double posTol = 0.001, double angTol = 1e-4)
    {
        var msgs = new List<string>();
        for (int i = 1; i < Elements.Count; i++)
        {
            var a = Elements[i - 1];
            var b = Elements[i];
            double gap = Vec2.Distance(a.EndPoint, b.Start);
            double kink = Math.Abs(Numerics.NormalizeAngle(b.StartAngle - a.EndAngle));
            if (gap > posTol) msgs.Add($"Gap of {gap:F4} m between elements {i} and {i + 1}.");
            if (kink > angTol) msgs.Add($"Direction change of {Numerics.RadToDeg(kink):F4}° between elements {i} and {i + 1}.");
        }
        return msgs;
    }

    // ---------- Stationing ----------

    public double DistanceToStation(double distance)
    {
        double sta = StartStation + distance;
        foreach (var eq in StationEquations.OrderBy(e => e.Distance))
        {
            if (distance >= eq.Distance) sta = eq.StationAhead + (distance - eq.Distance);
            else break;
        }
        return sta;
    }

    public double StationToDistance(double station)
    {
        if (StationEquations.Count == 0) return station - StartStation;
        // Walk the regions; return the first region containing the station.
        var eqs = StationEquations.OrderBy(e => e.Distance).ToList();
        double regionStartDist = 0, regionStartSta = StartStation;
        for (int i = 0; i <= eqs.Count; i++)
        {
            double regionEndDist = i < eqs.Count ? eqs[i].Distance : double.PositiveInfinity;
            double d = regionStartDist + (station - regionStartSta);
            if (d >= regionStartDist - 1e-9 && d <= regionEndDist + 1e-9) return d;
            if (i < eqs.Count)
            {
                regionStartDist = eqs[i].Distance;
                regionStartSta = eqs[i].StationAhead;
            }
        }
        return station - StartStation;
    }

    // ---------- Horizontal evaluation (by distance) ----------

    private int ElementIndexAt(double distance)
    {
        if (Elements.Count == 0) throw new InvalidOperationException("Alignment has no horizontal geometry.");
        int lo = 0, hi = Elements.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (Elements[mid].StartDistance <= distance) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>Point and tangent angle at a distance. Beyond the ends the end tangents are extended.</summary>
    public (Vec2 point, double angle) EvaluateAtDistance(double distance)
    {
        if (distance <= 0)
        {
            var e0 = Elements[0];
            return (e0.Start + Vec2.FromAngle(e0.StartAngle) * distance, e0.StartAngle);
        }
        if (distance >= Length)
        {
            var en = Elements[^1];
            double a = en.EndAngle;
            return (en.EndPoint + Vec2.FromAngle(a) * (distance - Length), a);
        }
        var e = Elements[ElementIndexAt(distance)];
        double s = distance - e.StartDistance;
        return (e.PointAt(s), e.AngleAt(s));
    }

    public double CurvatureAtDistance(double distance)
    {
        if (distance <= 0 || distance >= Length) return 0;
        var e = Elements[ElementIndexAt(distance)];
        return e.CurvatureAt(distance - e.StartDistance);
    }

    /// <summary>Plan point at distance and offset (right positive).</summary>
    public Vec2 PointAtDistance(double distance, double offset = 0)
    {
        var (p, a) = EvaluateAtDistance(distance);
        return offset == 0 ? p : p + Vec2.FromAngle(a).Right * offset;
    }

    // ---------- Station-based convenience ----------

    public Vec2 PointAt(double station, double offset = 0) => PointAtDistance(StationToDistance(station), offset);

    public double AngleAt(double station) => EvaluateAtDistance(StationToDistance(station)).angle;

    /// <summary>Unit vector pointing right, perpendicular (radial) to the alignment at the station.</summary>
    public Vec2 RightNormalAt(double station) => Vec2.FromAngle(AngleAt(station)).Right;

    /// <summary>
    /// Finished-grade elevation at station/offset: the profile applies at the cross-slope pivot point,
    /// and the cross slope is applied from the pivot outward.
    /// </summary>
    public double ElevationAt(double station, double offset = 0) =>
        Profile.ElevationAt(station) + CrossSlope.DeltaElevation(station, offset);

    public Vec3 Point3At(double station, double offset = 0) => PointAt(station, offset).WithZ(ElevationAt(station, offset));

    // ---------- Inverse (point → station/offset) ----------

    /// <summary>
    /// Projects a plan point onto the alignment. Returns the closest perpendicular foot; points beyond the
    /// ends are projected onto the extended end tangents (OnAlignment = false).
    /// </summary>
    public StationOffset Inverse(Vec2 p)
    {
        double bestDist = double.NaN, bestOffset = 0, bestAbs = double.MaxValue;
        bool onAlign = false;

        // Search elements nearest-first by bounding box distance; prune once boxes are farther than best.
        var order = new List<(double d, HorizontalElement e)>(Elements.Count);
        foreach (var e in Elements)
        {
            var b = e.Bounds;
            double dx = Math.Max(0, Math.Max(b.MinX - p.X, p.X - b.MaxX));
            double dy = Math.Max(0, Math.Max(b.MinY - p.Y, p.Y - b.MaxY));
            order.Add((Math.Sqrt(dx * dx + dy * dy), e));
        }
        order.Sort((a, b) => a.d.CompareTo(b.d));

        foreach (var (boxDist, e) in order)
        {
            if (boxDist > bestAbs) break;
            // The element ends are candidates too: a foot exactly at an element junction (point square to the alignment at a PC / PT) can be
            // missed by the root search of BOTH neighbours, which fell through to the "start of alignment" fallback (Nebraska St barrier
            // lines: one point 8 m high at the PT). The closest point of an element is a perpendicular foot or an end.
            foreach (double s in e.PerpendicularFeet(p).Append(0).Append(e.Length))
            {
                var foot = e.PointAt(s);
                double off = Vec2.Dot(p - foot, e.TangentAt(s).Right);
                double dist = Vec2.Distance(p, foot);
                if (dist < bestAbs)
                {
                    bestAbs = dist;
                    bestDist = e.StartDistance + s;
                    bestOffset = off;
                    onAlign = true;
                }
            }
        }

        // Extended tangents before the start / after the end.
        if (Elements.Count > 0)
        {
            var e0 = Elements[0];
            var t0 = Vec2.FromAngle(e0.StartAngle);
            double s0 = Vec2.Dot(p - e0.Start, t0);
            if (s0 < 0)
            {
                var foot = e0.Start + t0 * s0;
                double d = Vec2.Distance(p, foot);
                if (d < bestAbs) { bestAbs = d; bestDist = s0; bestOffset = Vec2.Dot(p - foot, t0.Right); onAlign = false; }
            }
            var en = Elements[^1];
            var tn = Vec2.FromAngle(en.EndAngle);
            double sn = Vec2.Dot(p - en.EndPoint, tn);
            if (sn > 0)
            {
                var foot = en.EndPoint + tn * sn;
                double d = Vec2.Distance(p, foot);
                if (d < bestAbs) { bestAbs = d; bestDist = Length + sn; bestOffset = Vec2.Dot(p - foot, tn.Right); onAlign = false; }
            }
        }

        if (double.IsNaN(bestDist))
        {
            // Point exactly at a vertex with no perpendicular foot (outside of a kink); fall back to nearest end.
            var d0 = Vec2.Distance(p, Elements[0].Start);
            bestDist = 0;
            bestOffset = d0;
        }
        return new StationOffset(DistanceToStation(bestDist), bestOffset, bestDist, onAlign);
    }

    /// <summary>
    /// Finds the distance along the alignment where the offset curve at <paramref name="offsetAt"/>(distance)
    /// crosses the infinite line through <paramref name="linePoint"/> with direction <paramref name="lineDir"/>.
    /// Searches within [d0, d1] and returns the root closest to <paramref name="nearDistance"/>.
    /// </summary>
    public bool IntersectOffsetCurveWithLine(Func<double, double> offsetAt, Vec2 linePoint, Vec2 lineDir,
        double d0, double d1, double nearDistance, out double distance)
    {
        var dir = lineDir.Normalized();
        double F(double d) => Vec2.Cross(dir, PointAtDistance(d, offsetAt(d)) - linePoint);
        int n = Math.Max(16, (int)Math.Ceiling((d1 - d0) / 0.5));
        n = Math.Min(n, 20000);
        var roots = Numerics.FindRoots(F, d0, d1, n);
        distance = double.NaN;
        if (roots.Count == 0) return false;
        distance = roots.OrderBy(r => Math.Abs(r - nearDistance)).First();
        return true;
    }

    /// <summary>Dense polyline of the alignment (optionally offset) between two distances.</summary>
    public List<Vec2> SamplePolyline(double d0, double d1, double offset = 0, double maxAngle = 0.01, double maxSegment = 5.0)
    {
        var pts = new List<Vec2>();
        if (Elements.Count == 0 || d1 <= d0) return pts;
        var breaks = new List<double> { d0 };
        foreach (var e in Elements)
        {
            if (e.StartDistance > d0 && e.StartDistance < d1) breaks.Add(e.StartDistance);
        }
        breaks.Add(d1);
        for (int b = 0; b < breaks.Count - 1; b++)
        {
            double a0 = breaks[b], a1 = breaks[b + 1];
            double mid = (a0 + a1) / 2;
            double k = Math.Max(Math.Abs(CurvatureAtDistance(a0 + 1e-9)), Math.Abs(CurvatureAtDistance(a1 - 1e-9)));
            k = Math.Max(k, Math.Abs(CurvatureAtDistance(mid)));
            int n = (int)Math.Ceiling(Math.Max(k * (a1 - a0) / maxAngle, (a1 - a0) / maxSegment));
            n = Math.Clamp(n, 1, 20000);
            for (int i = (b == 0 ? 0 : 1); i <= n; i++)
                pts.Add(PointAtDistance(a0 + (a1 - a0) * i / n, offset));
        }
        return pts;
    }
}
