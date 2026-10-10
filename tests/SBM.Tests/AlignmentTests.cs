using SBM.Core.Alignments;
using SBM.Core.Geometry;
using SBM.Core.Project;
using Xunit;

namespace SBM.Tests;

public class AlignmentTests
{
    [Fact]
    public void Arc_QuarterCircle_EndsAtExpectedPoint()
    {
        double r = 100;
        var arc = new HorizontalElement(Vec2.Zero, 0, Math.PI / 2 * r, 1 / r, 1 / r);
        var e = arc.EndPoint;
        Assert.Equal(100, e.X, 9);
        Assert.Equal(100, e.Y, 9);
        Assert.Equal(Math.PI / 2, arc.EndAngle, 12);
    }

    [Theory]
    [InlineData(60, 300)]
    [InlineData(200, 1500)]
    [InlineData(150, 200)]
    public void Spiral_MatchesClothoidSeries(double ls, double r)
    {
        var sp = new HorizontalElement(Vec2.Zero, 0, ls, 0, 1 / r);
        double th = ls / (2 * r);
        // Fresnel series for a full clothoid (sufficient terms for th < 0.5).
        double x = ls * (1 - Math.Pow(th, 2) / 10 + Math.Pow(th, 4) / 216 - Math.Pow(th, 6) / 9360 + Math.Pow(th, 8) / 685440);
        double y = ls * (th / 3 - Math.Pow(th, 3) / 42 + Math.Pow(th, 5) / 1320 - Math.Pow(th, 7) / 75600 + Math.Pow(th, 9) / 6894720);
        var e = sp.EndPoint;
        Assert.Equal(x, e.X, 7);
        Assert.Equal(y, e.Y, 7);
        Assert.Equal(th, sp.EndAngle, 12);
    }

    [Fact]
    public void PiDefinition_SymmetricSpiralCurve_MatchesTextbookTangentLength()
    {
        double r = 500, ls = 100, delta = Numerics.DegToRad(40);
        var pis = new PiDefinition
        {
            Pis =
            {
                new PiRow(0, 0),
                new PiRow(0, 1000, r, ls, ls),
                new PiRow(1000 * Math.Sin(delta), 1000 + 1000 * Math.Cos(delta)),
            },
        };
        var els = pis.Build(out var curves);
        var (k, p) = PiDefinition.SpiralShifts(ls, r);
        double ts = (r + p) * Math.Tan(delta / 2) + k;
        Assert.Equal(ts, curves[0].TangentIn, 6);
        Assert.Equal(ts, curves[0].TangentOut, 6);

        var a = new Alignment(els);
        Assert.Empty(a.CheckContinuity(1e-6, 1e-9));
        Assert.Equal(5, els.Count); // line, spiral, arc, spiral, line
        Assert.Equal(delta, a.Elements[^1].StartAngle, 9);
    }

    [Fact]
    public void PiDefinition_AsymmetricSpirals_AreContinuous()
    {
        var pis = new PiDefinition
        {
            Pis =
            {
                new PiRow(0, 0),
                new PiRow(200, 800, 400, 60, 120),
                new PiRow(-300, 1500, 600, 0, 80),
                new PiRow(-300, 2500),
            },
        };
        var a = new Alignment(pis.Build(out _));
        Assert.Empty(a.CheckContinuity(1e-6, 1e-9));
        var extracted = PiDefinition.TryExtract(a.Elements);
        Assert.NotNull(extracted);
        Assert.Equal(4, extracted!.Pis.Count);
        Assert.Equal(200, extracted.Pis[1].Northing, 6);
        Assert.Equal(800, extracted.Pis[1].Easting, 6);
        Assert.Equal(120, extracted.Pis[1].SpiralOut, 6);
    }

    [Fact]
    public void PiDefinition_OverlappingCurves_Throws()
    {
        var pis = new PiDefinition
        {
            Pis = { new PiRow(0, 0), new PiRow(0, 100, 2000), new PiRow(100, 200, 2000), new PiRow(100, 400) },
        };
        Assert.Throws<AlignmentException>(() => pis.Build(out _));
    }

    [Fact]
    public void Inverse_RoundTrips_StationOffset()
    {
        var a = SampleProject.CreateAlignment();
        var rnd = new Random(42);
        for (int i = 0; i < 300; i++)
        {
            double sta = a.StartStation + rnd.NextDouble() * a.Length;
            double off = (rnd.NextDouble() - 0.5) * 60;
            var p = a.PointAt(sta, off);
            var so = a.Inverse(p);
            Assert.Equal(sta, so.Station, 6);
            Assert.Equal(off, so.Offset, 6);
        }
    }

    [Fact]
    public void Inverse_BeyondEnds_UsesExtendedTangents()
    {
        var a = SampleProject.CreateAlignment();
        var p = a.PointAtDistance(-50, 5);
        var so = a.Inverse(p);
        Assert.False(so.OnAlignment);
        Assert.Equal(-50, so.Distance, 6);
        Assert.Equal(5, so.Offset, 6);
    }

    [Fact]
    public void Profile_SymmetricCurve_MidOrdinate()
    {
        var p = new VerticalProfile { Vpis = { new Vpi(0, 100), new Vpi(500, 110, 400), new Vpi(1000, 100) } };
        // g1 = +2%, g2 = -2%, A = -4%, L = 400 → e = A·L/8 = -2.0
        Assert.Equal(108, p.ElevationAt(500), 9);
        Assert.Equal(0, p.GradeAt(500), 9);
        Assert.Equal(100 + 0.02 * 300, p.ElevationAt(300), 9); // PVC
        Assert.Equal(0.02, p.GradeAt(299.999), 4);
    }

    [Fact]
    public void Profile_UnsymmetricCurve_IsContinuousAtVpi()
    {
        var p = new VerticalProfile { Vpis = { new Vpi(0, 100), new Vpi { Station = 500, Elevation = 110, LengthIn = 100, LengthOut = 300 }, new Vpi(1000, 100) } };
        double below = p.ElevationAt(500 - 1e-7), above = p.ElevationAt(500 + 1e-7);
        Assert.Equal(below, above, 5);
        Assert.Equal(p.GradeAt(500 - 1e-6), p.GradeAt(500 + 1e-6), 4);
        Assert.Equal(100 + 0.02 * 400, p.ElevationAt(400), 9); // PVC
        Assert.Equal(110 - 0.02 * 300, p.ElevationAt(800), 9); // PVT
    }

    [Fact]
    public void CrossSlope_InterpolatesAndAppliesAboutPivot()
    {
        var t = new CrossSlopeTable { Rows = { new CrossSlopeRow(0, -0.02, -0.02), new CrossSlopeRow(100, -0.06, 0.06) } };
        var (l, r) = t.SlopesAt(50);
        Assert.Equal(-0.04, l, 12);
        Assert.Equal(0.02, r, 12);
        Assert.Equal(0.2, t.DeltaElevation(50, 10), 12);
        Assert.Equal(-0.4, t.DeltaElevation(50, -10), 12);
    }

    [Fact]
    public void StationEquation_MapsBothWays()
    {
        var a = new Alignment(new[] { HorizontalElement.Line(Vec2.Zero, new Vec2(1000, 0)) })
        {
            StartStation = 100,
            StationEquations = { new StationEquation { Distance = 400, StationBack = 500, StationAhead = 600 } },
        };
        Assert.Equal(300, a.DistanceToStation(200), 9);
        Assert.Equal(700, a.DistanceToStation(500), 9);
        Assert.Equal(500, a.StationToDistance(700), 9);
        Assert.Equal(200, a.StationToDistance(300), 9);
    }
}
