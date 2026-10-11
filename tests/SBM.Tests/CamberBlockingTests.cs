using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Project;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class CamberBlockingTests
{
    // Sample bridge: 3 supports, 2 radial splices per girder (numbers 1 and 2). Girder 0: points 1..17 equally spaced between
    // bearing(0) - FS1 - bearing(1) - FS2 - bearing(2) at points 1, 5, 9, 13, 17.
    private static BridgeDefinition Def(Action<GirderCamberDefinition>? tweak = null)
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        var gc = new GirderCamberDefinition { Girder = 0, DatumPoint = 1 };
        gc.Keys.Add(new CamberKeyPoint { Number = 1, Support = 0 });
        gc.Keys.Add(new CamberKeyPoint { Number = 5, Splice = 1 });
        gc.Keys.Add(new CamberKeyPoint { Number = 9, Support = 1 });
        gc.Keys.Add(new CamberKeyPoint { Number = 13, Splice = 2 });
        gc.Keys.Add(new CamberKeyPoint { Number = 17, Support = 2 });
        for (int i = 1; i <= 17; i++)
            gc.Points.Add(new CamberPoint { Number = i, SteelDeflection = In(0.1 * i), SlabDeflection = In(0.25 * i), Camber = In(0.05 * (i % 4)) });
        tweak?.Invoke(gc);
        b.Girders.Camber.Add(gc);
        return b;
    }

    private static (BridgeFraming f, GirderCamberResult g) Run(BridgeDefinition b)
    {
        var p = SampleProject.Create();
        var f = FramingGenerator.Generate(b, p.Alignments[0]);
        Assert.False(f.HasErrors, string.Join("\n", f.Diagnostics));
        Assert.NotNull(f.Camber);
        return (f, f.Camber!.Girders.Single());
    }

    [Fact]
    public void Points_AreEquallySpacedBetweenKeyPoints_AndHitBearingsAndSplices()
    {
        var (f, g) = Run(Def());
        Assert.Equal(17, g.Points.Count);
        Assert.Empty(g.UnlocatedPoints);
        var gr = f.Girders[0];
        double s1 = f.Splices.Single(s => s.Girder == 0 && s.Number == 1).GirderDistance;
        Assert.Equal(s1, g.Point(5)!.PathDistance, 6);
        Assert.Equal(gr.DistanceAt(0, 0), g.Point(1)!.PathDistance, 6);
        Assert.Equal(gr.DistanceAt(1, 0), g.Point(9)!.PathDistance, 6);
        Assert.Equal(gr.DistanceAt(1, 1), g.Point(17)!.PathDistance, 6);
        double s0 = g.Point(1)!.PathDistance;
        for (int i = 2; i <= 4; i++)
            Assert.Equal(s0 + (s1 - s0) * (i - 1) / 4, g.Point(i)!.PathDistance, 6);
        Assert.Equal("splice", g.Point(5)!.Kind);
        Assert.Equal("bearing", g.Point(9)!.Kind);
        Assert.Equal("", g.Point(6)!.Kind);
    }

    [Fact]
    public void Blocking_IsNoLoadTopOfWebAboveDatum_AndHaunchElevationFollowsTheGrade()
    {
        var b = Def();
        var (f, g) = Run(b);
        double slab = b.Deck.Thickness, haunch = b.Deck.Haunch;
        var d = g.Point(1)!;
        Assert.Equal(0, d.Blocking, 9);
        foreach (var p in g.Points)
        {
            var (pt, _, _) = f.Girders[0].Path.At(p.PathDistance);
            Assert.Equal(pt.Z, p.TopOfWeb, 9);
            Assert.Equal(pt.Z + slab + haunch, p.FinishedGrade, 9);
            // girder line haunch elevation = bottom of slab + slab/barrier deflection
            Assert.Equal(p.FinishedGrade - slab + p.SlabDeflection, p.HaunchElevation, 9);
            // no-load top of web = final + all deflections; blocking relative to the datum point
            Assert.Equal(p.TopOfWeb + p.SteelDeflection + p.SlabDeflection, p.NoLoadTopOfWeb, 9);
            Assert.Equal(p.NoLoadTopOfWeb - d.NoLoadTopOfWeb, p.Blocking, 9);
            // the plans' observed relation: blocking = H - H(datum) + (steel deflection - steel deflection at the datum)
            Assert.Equal(p.HaunchElevation - d.HaunchElevation + p.SteelDeflection - d.SteelDeflection, p.Blocking, 9);
        }
    }

    [Fact]
    public void Camber_IsMeasuredFromTheChordBetweenSplices_AndIsZeroAtChordEnds()
    {
        var (_, g) = Run(Def());
        foreach (var p in g.Points.Where(p => p.ChordEnd))
            Assert.Equal(0, p.CamberFromNoLoad, 9);
        Assert.Contains(g.Points, p => p.Kind == "splice" && p.ChordEnd);
        Assert.False(g.Point(9)!.ChordEnd);      // a pier is not a chord end
        // pier point: no-load top of web minus the straight line between the splices, weighted by distance
        var a = g.Point(5)!; var c = g.Point(13)!; var m = g.Point(9)!;
        double t = (m.PathDistance - a.PathDistance) / (c.PathDistance - a.PathDistance);
        double chordN = a.NoLoadTopOfWeb + (c.NoLoadTopOfWeb - a.NoLoadTopOfWeb) * t;
        Assert.Equal(m.NoLoadTopOfWeb - chordN, m.CamberFromNoLoad, 9);
        double chordD = a.TotalDeflection + (c.TotalDeflection - a.TotalDeflection) * t;
        Assert.Equal(m.TotalDeflection - chordD, m.CamberFromDeflection, 9);
        // blocking built the plan's way (chord of N + camber) differs from the blocking by the camber misfit only
        Assert.Equal(m.Camber - m.CamberFromNoLoad, m.BlockingFromCamber - m.Blocking, 9);
    }

    [Fact]
    public void UnlocatedPointsAndMissingKeys_AreReported_NotGuessed()
    {
        var b = Def(gc =>
        {
            gc.Keys.Add(new CamberKeyPoint { Number = 21, Splice = 9 });                 // splice that does not exist
            gc.Points.Add(new CamberPoint { Number = 19, Camber = In(1) });               // between key 17 and the missing key 21
            gc.Points.Add(new CamberPoint { Number = 40, Camber = In(1) });               // beyond every key
        });
        var (_, g) = Run(b);
        Assert.Equal(new[] { 19, 40 }, g.UnlocatedPoints.OrderBy(x => x));
        Assert.Contains(g.Warnings, w => w.Contains("no field splice 9"));
        Assert.Contains(g.Warnings, w => w.Contains("could not be located") && w.Contains("19") && w.Contains("40"));
        Assert.Equal(17, g.Points.Count);
    }

    [Fact]
    public void NoDatum_NoBlocking_AndWarning()
    {
        var (_, g) = Run(Def(gc => gc.DatumPoint = 0));
        Assert.All(g.Points, p => Assert.True(double.IsNaN(p.Blocking)));
        Assert.Contains(g.Warnings, w => w.Contains("no datum"));
        var (_, g2) = Run(Def(gc => gc.DatumPoint = 99));
        Assert.Contains(g2.Warnings, w => w.Contains("datum point 99"));
    }

    [Fact]
    public void GirderEnd_KeyUsesTheTerminationDistance()
    {
        var b = Def(gc =>
        {
            gc.Girder = 1;
            gc.Keys.Clear(); gc.Points.Clear();
            gc.Keys.Add(new CamberKeyPoint { Number = 1, Support = 1 });
            gc.Keys.Add(new CamberKeyPoint { Number = 5, GirderEnd = true });
            for (int i = 1; i <= 5; i++) gc.Points.Add(new CamberPoint { Number = i });
        });
        b.Girders.Terminations.Add(new GirderTermination { Girder = 1, Support = 1, Distance = Ft(40) });
        var p = SampleProject.Create();
        var f = FramingGenerator.Generate(b, p.Alignments[0]);
        var g = f.Camber!.Girders.Single();
        Assert.Equal(5, g.Points.Count);
        Assert.Equal(Ft(40), g.Point(5)!.PathDistance - g.Point(1)!.PathDistance, 6);
        Assert.Equal("end", g.Point(5)!.Kind);
        Assert.Equal(Ft(10), g.Point(2)!.PathDistance - g.Point(1)!.PathDistance, 6);
    }

    [Fact]
    public void CamberData_SurvivesProjectJson_AndChangesInvalidateNothingElse()
    {
        var b = Def();
        var p = SampleProject.Create();
        p.Bridges[0] = b;
        var back = BridgeProject.FromJson(p.ToJson()).Bridges[0].Girders.Camber;
        Assert.Single(back);
        Assert.Equal(17, back[0].Points.Count);
        Assert.Equal(5, back[0].Keys.Count);
        Assert.Equal(1, back[0].DatumPoint);
        Assert.Equal(In(0.25 * 7), back[0].Points[6].SlabDeflection, 12);
        Assert.Equal(1, back[0].Keys[1].Splice);
        // clone is deep
        var c = b.Clone().Girders.Camber[0];
        c.Points[0].Camber = In(9);
        Assert.NotEqual(c.Points[0].Camber, b.Girders.Camber[0].Points[0].Camber);
    }

    [Fact]
    public void Report_ListsKeyPoints()
    {
        var b = Def();
        var p = SampleProject.Create();
        var f = FramingGenerator.Generate(b, p.Alignments[0]);
        string txt = CamberBlocking.ToText("Sample", f.Camber!, p.Units);
        Assert.Contains("G1: datum point 1", txt);
        Assert.Contains("FS 1", txt);
    }
}
