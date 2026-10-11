using System.Numerics;
using SBM.Core.Alignments;
using SBM.Core.Geometry;
using Xunit;

namespace SBM.Tests;

public class SnapMathTests
{
    [Fact]
    public void NearestOnSegment_ClampsToEnds()
    {
        var a = new Vector3(0, 0, 0);
        var b = new Vector3(10, 0, 0);
        var p = SnapMath.NearestOnSegment(new Vector3(4, 3, 0), a, b, out float t);
        Assert.Equal(4, p.X, 4);
        Assert.Equal(0.4f, t, 4);
        var q = SnapMath.NearestOnSegment(new Vector3(-5, 1, 0), a, b, out t);
        Assert.Equal(0, q.X, 4);
        Assert.Equal(0, t, 4);
    }

    [Fact]
    public void PerpendicularFoot_OnlyInsideSegment()
    {
        var a = new Vector3(0, 0, 0);
        var b = new Vector3(10, 0, 0);
        Assert.True(SnapMath.PerpendicularFoot(new Vector3(3, 5, 0), a, b, out var f));
        Assert.Equal(3, f.X, 4);
        Assert.False(SnapMath.PerpendicularFoot(new Vector3(-2, 5, 0), a, b, out _));
        Assert.False(SnapMath.PerpendicularFoot(new Vector3(0, 5, 0), a, b, out _)); // at the end point -> endpoint snap instead
    }

    [Fact]
    public void SegmentIntersect2D_CrossingAndParallel()
    {
        Assert.True(SnapMath.SegmentIntersect2D(new Vector2(0, 0), new Vector2(10, 10), new Vector2(0, 10), new Vector2(10, 0), out float s, out float t));
        Assert.Equal(0.5f, s, 4);
        Assert.Equal(0.5f, t, 4);
        Assert.False(SnapMath.SegmentIntersect2D(new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 1), new Vector2(10, 1), out _, out _));
        Assert.False(SnapMath.SegmentIntersect2D(new Vector2(0, 0), new Vector2(1, 1), new Vector2(5, 0), new Vector2(0, 5), out _, out _)); // lines cross, segments do not
    }

    [Fact]
    public void RayTriangle_HitsFrontAndBack()
    {
        var a = new Vector3(0, 0, 0);
        var b = new Vector3(4, 0, 0);
        var c = new Vector3(0, 4, 0);
        var d = new Vector3(0, 0, -1);
        Assert.True(SnapMath.RayTriangle(new Vector3(1, 1, 10), d, a, b, c, out float t));
        Assert.Equal(10, t, 4);
        Assert.True(SnapMath.RayTriangle(new Vector3(1, 1, -10), -d, a, b, c, out t)); // from below, two-sided
        Assert.False(SnapMath.RayTriangle(new Vector3(3, 3, 10), d, a, b, c, out _));
    }

    [Fact]
    public void DistanceToRay_AndClosestOnSegment()
    {
        var o = new Vector3(0, 0, 100);
        var d = new Vector3(0, 0, -1);
        Assert.Equal(5, SnapMath.DistanceToRay(new Vector3(3, 4, 7), o, d), 4);
        // Segment passing under the ray at x=0 (crossing in plan): closest approach is the crossing, 2 m below the ray point.
        var q = SnapMath.ClosestOnSegmentToRay(o, d, new Vector3(-5, 0, 0), new Vector3(5, 0, 0), out float dist, out float rayT);
        Assert.Equal(0, q.X, 3);
        Assert.Equal(0, dist, 3);
        Assert.Equal(100, rayT, 3);
        // Skew segment offset 2 m in Y from the ray.
        SnapMath.ClosestOnSegmentToRay(o, d, new Vector3(-5, 2, 0), new Vector3(5, 2, 0), out dist, out _);
        Assert.Equal(2, dist, 3);
    }

    [Fact]
    public void LineHitsBox_RespectsPadding()
    {
        var o = new Vector3(0, 0, 100);
        var inv = SnapMath.Reciprocal(new Vector3(0, 0, -1));
        Assert.True(SnapMath.LineHitsBox(o, inv, new Vector3(-1, -1, 0), new Vector3(1, 1, 1), 0));
        Assert.False(SnapMath.LineHitsBox(o, inv, new Vector3(2, -1, 0), new Vector3(3, 1, 1), 0));
        Assert.True(SnapMath.LineHitsBox(o, inv, new Vector3(2, -1, 0), new Vector3(3, 1, 1), 2.5f));
    }

    [Fact]
    public void TransformBox_Translation()
    {
        var (lo, hi) = SnapMath.TransformBox(new Vector3(0, 0, 0), new Vector3(1, 2, 3), Matrix4x4.CreateTranslation(10, 20, 30));
        Assert.Equal(new Vector3(10, 20, 30), lo);
        Assert.Equal(new Vector3(11, 22, 33), hi);
    }

    [Fact]
    public void MeasureResult_DistanceSlopeAzimuth()
    {
        var m = new MeasureResult(new Vec3(100, 200, 10), new Vec3(103, 204, 15));
        Assert.Equal(5, m.PlanDistance, 9);
        Assert.Equal(Math.Sqrt(50), m.Distance3D, 9);
        Assert.Equal(1.0, m.Slope, 9);
        Assert.Equal(3, m.DX, 9);
        Assert.Equal(5, m.DZ, 9);
        // direction (3,4): azimuth from north = atan2(3, 4) = 36.87 deg
        Assert.Equal(36.8699, m.AzimuthDegrees, 3);
        // pure east
        Assert.Equal(90, new MeasureResult(new Vec3(0, 0, 0), new Vec3(1, 0, 0)).AzimuthDegrees, 9);
        // plumb: no slope/azimuth
        var plumb = new MeasureResult(new Vec3(0, 0, 0), new Vec3(0, 0, 4));
        Assert.True(double.IsNaN(plumb.Slope));
        Assert.True(double.IsNaN(plumb.AzimuthDegrees));
        Assert.Equal(90, MeasureResult.AngleAt(new Vec3(1, 0, 0), Vec3.Zero, new Vec3(0, 1, 0)), 9);
    }

    [Fact]
    public void PointQuery_StationOffsetAndGrades()
    {
        // 1000 m tangent along +X starting at station 100, 2% crown, profile 50 m rising 1%.
        var a = new Alignment(new[] { HorizontalElement.Line(Vec2.Zero, new Vec2(1000, 0)) }) { Name = "Test", StartStation = 100 };
        a.Profile.Vpis.Add(new Vpi(100, 50));
        a.Profile.Vpis.Add(new Vpi(1100, 60));
        // Point 20 m up-station... at X=300 (station 400), 8 m to the right (negative Y for a +X alignment).
        var r = PointQuery.Query(a, new Vec3(300, -8, 60.123))!;
        Assert.Equal(400, r.Station, 6);
        Assert.Equal(8, r.Offset, 6); // right of travel is positive
        Assert.True(r.OnAlignment);
        Assert.Equal(50 + 300 * 0.01, r.ProfileElevation, 6);
        Assert.Equal(a.ElevationAt(400, 8), r.FinishedGrade, 9);
        Assert.Equal(60.123 - r.FinishedGrade, r.DeltaToFinished, 9);
        // Off the end: extended tangent, flagged.
        var beyond = PointQuery.Query(a, new Vec3(1500, 3, 0))!;
        Assert.False(beyond.OnAlignment);
    }

    [Fact]
    public void PointQuery_StationEquationHonored()
    {
        var a = new Alignment(new[] { HorizontalElement.Line(Vec2.Zero, new Vec2(1000, 0)) })
        {
            StartStation = 100,
            StationEquations = { new StationEquation { Distance = 400, StationBack = 500, StationAhead = 600 } },
        };
        var r = PointQuery.Query(a, new Vec3(500, 0, 0))!;
        Assert.Equal(700, r.Station, 6);
    }

    [Fact]
    public void QueryNearest_PicksTheClosestAlignment()
    {
        var a1 = new Alignment(new[] { HorizontalElement.Line(Vec2.Zero, new Vec2(1000, 0)) }) { Name = "A" };
        var a2 = new Alignment(new[] { HorizontalElement.Line(new Vec2(0, 100), new Vec2(1000, 100)) }) { Name = "B" };
        Assert.Equal("A", PointQuery.QueryNearest(new[] { a1, a2 }, new Vec3(500, 10, 0))!.Alignment);
        Assert.Equal("B", PointQuery.QueryNearest(new[] { a1, a2 }, new Vec3(500, 90, 0))!.Alignment);
    }

    [Fact]
    public void PointQuery_Delta_StationOffsetAndTangentComponents()
    {
        var line = new Alignment(new[] { HorizontalElement.Line(Vec2.Zero, new Vec2(1000, 0)) }) { Name = "L" };
        var d = PointQuery.Delta(line, new Vec3(100, -5, 0), new Vec3(130, 3, 2))!;
        Assert.Equal(30, d.DStation, 9);
        Assert.Equal(-8, d.DOffset, 9);          // 5 RT -> 3 LT
        Assert.Equal(30, d.AlongTangent, 9);
        Assert.Equal(-8, d.AcrossTangent, 9);
        Assert.Equal(2, d.DZ, 9);

        // Left curve R 100: two points on the CL 50 apart along the curve.
        var arc = new Alignment(new[] { new HorizontalElement(Vec2.Zero, 0, 200, 0.01, 0.01) }) { Name = "C" };
        var p0 = arc.PointAtDistance(0); var p1 = arc.PointAtDistance(50);
        var c = PointQuery.Delta(arc, p0.WithZ(0), p1.WithZ(0))!;
        Assert.Equal(50, c.DStation, 6);
        Assert.Equal(0, c.DOffset, 6);
        Assert.Equal(100 * Math.Sin(0.5), c.AlongTangent, 6);
        Assert.Equal(-100 * (1 - Math.Cos(0.5)), c.AcrossTangent, 6); // chord falls LEFT of the tangent
    }
}
