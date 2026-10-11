using SBM.Core.Alignments;

namespace SBM.Core.Geometry;

/// <summary>Distance / direction between two picked points (world coordinates, meters).</summary>
public sealed record MeasureResult(Vec3 A, Vec3 B)
{
    public Vec3 Delta => B - A;
    public double Distance3D => Delta.Length;
    public double PlanDistance => Delta.XY.Length;
    public double DX => B.X - A.X;
    public double DY => B.Y - A.Y;
    public double DZ => B.Z - A.Z;
    /// <summary>Rise over plan run (rise/run; NaN when the points are plumb above one another).</summary>
    public double Slope => PlanDistance > 1e-9 ? DZ / PlanDistance : double.NaN;
    /// <summary>Math angle of the plan direction (radians CCW from +X); NaN when there is no plan run.</summary>
    public double PlanAngle => PlanDistance > 1e-9 ? Math.Atan2(DY, DX) : double.NaN;
    /// <summary>Azimuth clockwise from grid north, degrees 0..360 (NaN when there is no plan run).</summary>
    public double AzimuthDegrees => double.IsNaN(PlanAngle) ? double.NaN : ((90 - PlanAngle * 180 / Math.PI) % 360 + 360) % 360;
    /// <summary>Vertical angle of the line above horizontal, degrees.</summary>
    public double ElevationAngleDegrees => Math.Atan2(DZ, PlanDistance) * 180 / Math.PI;

    /// <summary>Interior angle at B between BA and BC, degrees (3-point angle measurement).</summary>
    public static double AngleAt(Vec3 a, Vec3 b, Vec3 c)
    {
        var u = (a - b).Normalized();
        var v = (c - b).Normalized();
        return Math.Acos(Math.Clamp(Vec3.Dot(u, v), -1, 1)) * 180 / Math.PI;
    }
}

/// <summary>Where a point lies relative to an alignment: station, offset (right +), profile grade and finished grade.</summary>
public sealed record PointReadout(Vec3 World, string Alignment, double Station, double Offset, bool OnAlignment,
    double ProfileElevation, double FinishedGrade)
{
    /// <summary>Picked elevation minus finished grade (deck top / cross-slope surface) at that station and offset.</summary>
    public double DeltaToFinished => World.Z - FinishedGrade;
    public double DeltaToProfile => World.Z - ProfileElevation;
}

/// <summary>A distance between two points expressed in an alignment's directions. <see cref="DStation"/> /
/// <see cref="DOffset"/> follow the (curved) alignment: change in distance along it and change in offset (right +).
/// <see cref="AlongTangent"/> / <see cref="AcrossTangent"/> split the straight plan vector A→B along and across the
/// alignment's tangent at A's station (across: right +).</summary>
public sealed record AlignmentDelta(string Alignment, double DStation, double DOffset, double AlongTangent, double AcrossTangent, double DZ,
    bool OnAlignment);

public static class PointQuery
{
    /// <summary>A→B in the station / offset directions of one alignment (both points projected onto the same one).</summary>
    public static AlignmentDelta? Delta(Alignment a, Vec3 from, Vec3 to)
    {
        if (a.Elements.Count == 0) return null;
        var sa = a.Inverse(from.XY);
        var sb = a.Inverse(to.XY);
        var (_, ang) = a.EvaluateAtDistance(Math.Clamp(sa.Distance, 0, a.Length));
        var t = Vec2.FromAngle(ang);
        var d = to.XY - from.XY;
        double along = d.X * t.X + d.Y * t.Y;
        double across = Vec2.Dot(d, t.Right);
        return new AlignmentDelta(a.Name, sb.Distance - sa.Distance, sb.Offset - sa.Offset, along, across, to.Z - from.Z,
            sa.OnAlignment && sb.OnAlignment);
    }

    /// <summary>Station / offset / grades of a world point on one alignment (stations honor station equations).</summary>
    public static PointReadout? Query(Alignment a, Vec3 world)
    {
        if (a.Elements.Count == 0) return null;
        var so = a.Inverse(world.XY);
        double prof = a.Profile.ElevationAt(so.Station);
        double finished = a.ElevationAt(so.Station, so.Offset);
        return new PointReadout(world, a.Name, so.Station, so.Offset, so.OnAlignment, prof, finished);
    }

    /// <summary>The alignment closest to the point (smallest |offset|, on-alignment ones first); null when there are none.</summary>
    public static PointReadout? QueryNearest(IEnumerable<Alignment> alignments, Vec3 world)
    {
        PointReadout? best = null;
        double bestKey = double.MaxValue;
        foreach (var a in alignments)
        {
            if (Query(a, world) is not { } r) continue;
            double key = Math.Abs(r.Offset) + (r.OnAlignment ? 0 : 1e6);
            if (key < bestKey) { bestKey = key; best = r; }
        }
        return best;
    }
}
