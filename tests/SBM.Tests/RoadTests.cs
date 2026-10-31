using SBM.Api;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Road;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Roadway corridor (<see cref="RoadGenerator"/>, 2026-10-28; generic test inputs per the user's answer 7a.1).</summary>
public class RoadTests
{
    private static RoadDefinition Road(SbmDocument d, double s0, double s1) => new()
    {
        Name = "Test road", AlignmentName = d.Alignment().Name, BeginStation = s0, EndStation = s1,
        Left = new RoadSide
        {
            Components =
            {
                new RoadComponent { Name = "Lane 1", Kind = RoadComponentKind.Lane, Width = Ft(12),
                    Courses = { new RoadCourse { Name = "Surface", Thickness = In(4), Material = "HMA" }, new RoadCourse { Name = "Base", Thickness = In(8), Material = "Aggregate base" } } },
                new RoadComponent { Name = "Curb", Kind = RoadComponentKind.CurbAndGutter,
                    Section = new() { new(0, -In(6)), new(In(6), -In(6)), new(In(6), In(6)), new(0, In(6)) } },
            },
            SideSlope = new RoadSideSlope { CutRatio = 2, FillRatio = 3, MaxWidth = 30 },
        },
        Right = new RoadSide
        {
            Components =
            {
                new RoadComponent { Name = "Lane 1", Kind = RoadComponentKind.Lane, Width = Ft(12),
                    Courses = { new RoadCourse { Name = "Surface", Thickness = In(4), Material = "HMA" } } },
                new RoadComponent { Name = "Shoulder", Kind = RoadComponentKind.Shoulder, Width = Ft(8), Slope = -0.04,
                    Courses = { new RoadCourse { Name = "Surface", Thickness = In(4), Material = "HMA" } } },
            },
            SideSlope = new RoadSideSlope { CutRatio = 2, FillRatio = 3, MaxWidth = 30 },
        },
    };

    [Fact]
    public void Courses_Curb_Areas_And_NoGround_SideSlopeUnresolved()
    {
        var d = SbmDocument.Sample();
        var al = d.Alignment();
        double s0 = al.StartStation + 10, s1 = s0 + 50;
        var m = RoadGenerator.Generate(Road(d, s0, s1), al);
        var len = 50.0;   // along the alignment; offsets of 12 ft on a gentle curve change the length by < 0.5 %
        var lane = m.Parts.Single(p => p.Side == "left" && p.Component == "Lane 1" && p.Course == "Base");
        Assert.InRange(lane.Volume / (Ft(12) * In(8) * len), 0.99, 1.01);
        var curb = m.Parts.Single(p => p.Component == "Curb");
        Assert.InRange(curb.Volume / (In(6) * In(12) * len), 0.99, 1.01);
        Assert.InRange(m.TopArea["right Shoulder"] / (Ft(8) * len), 0.99, 1.01);
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("UNRESOLVED") && x.Message.Contains("side slope"));
        Assert.Empty(m.SideSlopes);
    }

    [Fact]
    public void SideSlope_DaylightsOnFlatGround_AtTheGivenRatio()
    {
        var d = SbmDocument.Sample();
        var al = d.Alignment();
        double s0 = al.StartStation + 10, s1 = s0 + 20;
        double gz = al.ElevationAt(s0) - 3;   // ground 3 m below the road: fill at 3H:1V
        var m = RoadGenerator.Generate(Road(d, s0, s1), al, _ => gz);
        Assert.True(m.Daylight.ContainsKey("right"));
        var day = m.Daylight["right"][0];
        var edge = al.PointAt(s0, Ft(20)).WithZ(al.ElevationAt(s0, Ft(12)) - 0.04 * Ft(8));
        Assert.Equal(gz, day.Z, 4);
        double h = Vec2.Distance(new Vec2(day.X, day.Y), new Vec2(edge.X, edge.Y));
        Assert.Equal((edge.Z - gz) * 3, h, 2);
    }

    [Fact]
    public void Earthwork_FlatGround_MatchesTheSectionArea()
    {
        var d = SbmDocument.Sample();
        var al = d.Alignment();
        double s0 = al.StartStation + 10, s1 = s0 + 20, w = Ft(12), t = In(12);
        RoadSide Side() => new()
        {
            Components = { new RoadComponent { Name = "Lane", Width = w, Slope = 0, Courses = { new RoadCourse { Name = "Surface", Thickness = t, Material = "HMA" } } } },
            SideSlope = new RoadSideSlope { CutRatio = 2, FillRatio = 3, MaxWidth = 40 },
        };
        var def = new RoadDefinition { Name = "Fill", AlignmentName = al.Name, BeginStation = s0, EndStation = s1, Left = Side(), Right = Side() };
        double gz = al.ElevationAt(s0) - 3;
        var m = RoadGenerator.Generate(def, al, _ => gz);
        Assert.NotNull(m.FillVolume);
        Assert.True(m.EarthworkSections.Count >= 2);
        foreach (var (sta, cut, fill) in m.EarthworkSections)
        {
            double h = al.ElevationAt(sta, 0) - gz;   // the lanes are level from the alignment point (Slope = 0)
            Assert.Equal(0, cut, 6);
            Assert.Equal(2 * w * (h - t) + 3 * h * h, fill, 3);
        }
        Assert.InRange(m.FillVolume!.Value, m.EarthworkSections.Min(x => x.Fill) * 20 * 0.999, m.EarthworkSections.Max(x => x.Fill) * 20 * 1.001);
        Assert.Equal(0, m.CutVolume!.Value, 6);
    }

    [Fact]
    public void Clearance_RoadUnderTheBridge_VerticalConflict_HorizontalOk_UnresolvedWhenNotGiven()
    {
        var d = SbmDocument.Sample();
        d.Regenerate();
        var al = d.Alignment();
        var f = d.Model(d.Project.Bridges[0].Name)!.Framing;
        double mid = (f.Supports[0].Station + f.Supports[1].Station) / 2;
        var c = al.PointAt(mid, 0);
        var t = al.PointAt(mid + 1, 0) - c; t = t / t.Length;
        var n = new Vec2(-t.Y, t.X);   // the crossing road runs normal to the bridge
        double zb = d.Steel().Parts.SelectMany(p => p.Mesh.Positions).Where(q => Vec2.Distance(new Vec2(q.X, q.Y), c) < Ft(12)).Min(q => q.Z);
        var cross = new SBM.Core.Alignments.Alignment(new[] { SBM.Core.Alignments.HorizontalElement.Line(c - n * 40, c + n * 40) }) { Name = "Crossing road" };
        cross.Profile.Vpis.Add(new SBM.Core.Alignments.Vpi(0, zb - 5));
        cross.Profile.Vpis.Add(new SBM.Core.Alignments.Vpi(80, zb - 5));
        d.Project.Alignments.Add(cross);
        RoadSide Side() => new() { Components = { new RoadComponent { Name = "Lane", Width = Ft(12), Slope = 0, Courses = { new RoadCourse { Name = "Surface", Thickness = In(4), Material = "HMA" } } } } };
        var def = new RoadDefinition { Name = "Under road", AlignmentName = cross.Name, BeginStation = 0, EndStation = 80, Left = Side(), Right = Side(),
            Clearances = new() { new RoadClearanceDefinition { Bridge = "", MinVertical = 6.0, MinHorizontal = 1.0, Source = "test" } } };
        d.Project.Roads.Add(def);
        var rm = d.Roads().Single();
        var v = rm.Clearances.Single(x => x.Kind == "vertical");
        Assert.InRange(v.Measured, 4.9, 5.6);   // the lowest steel within 12 ft of the crossing is 5 m above it; the edge lines are 12 ft either side
        Assert.False(v.Ok);
        Assert.Contains(rm.Diagnostics, x => x.Message.StartsWith("CONFLICT (plan vs model)") && x.Message.Contains("vertical clearance"));
        var h = rm.Clearances.Single(x => x.Kind == "horizontal");
        Assert.True(h.Measured > 1.0 && h.Ok == true, $"horizontal {h.Measured}");
        // not given -> UNRESOLVED, measured value still reported
        def.Clearances[0].MinVertical = double.NaN;
        var rm2 = d.Roads().Single();
        Assert.Null(rm2.Clearances.Single(x => x.Kind == "vertical").Ok);
        Assert.Contains(rm2.Diagnostics, x => x.Message.Contains("UNRESOLVED") && x.Message.Contains("vertical clearance"));
        Assert.DoesNotContain("\"Clearances\"", BridgeProject.FromJson(SbmDocument.Sample().Project.ToJson()).ToJson());
    }

    [Fact]
    public void RoundTrip_OmittedWhenEmpty_IfcRoadTree()
    {
        var d = SbmDocument.Sample();
        Assert.DoesNotContain("\"Roads\"", d.Project.ToJson());
        var al = d.Alignment();
        d.Project.Roads.Add(Road(d, al.StartStation + 10, al.StartStation + 40));
        var back = BridgeProject.FromJson(d.Project.ToJson());
        Assert.Equal(2, back.Roads.Single().Left.Components.Count);
        Assert.Equal(-0.04, back.Roads[0].Right.Components[1].Slope);
        d.Regenerate();
        string path = Path.Combine(Path.GetTempPath(), "sbm-road-" + Guid.NewGuid().ToString("N")[..8] + ".ifc");
        try
        {
            d.ExportIfc(path);
            string ifc = File.ReadAllText(path);
            Assert.Contains("IFCROAD(", ifc);
            Assert.Contains("IFCROADPART(", ifc);
            Assert.Contains(".TRAFFICLANE.", ifc);
            Assert.Contains("IFCCOURSE(", ifc);
            Assert.Contains(".PAVEMENT.", ifc);
            Assert.Contains("IFCKERB(", ifc);
            // database rows carry the GlobalIds of the IFC entities (IfcKeys.Road*)
            var roads = d.Roads();
            var tags = SBM.IO.Database.ModelDatabase.BuildTagIndex(d.Project, Array.Empty<SBM.IO.Ifc.IfcBridgeData>(), roads);
            Assert.All(roads[0].Parts, p => Assert.Contains("'" + tags.Lookup(p) + "'", ifc));
        }
        finally { File.Delete(path); }
    }
}
