using SBM.Api;
using SBM.Core.Project;
using SBM.Core.Road;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Subgrade layers under the road template (<see cref="RoadSide.Subgrade"/>, 2026-10-30; generic test inputs).</summary>
public class RoadSubgradeTests
{
    private static RoadSide Side(double w, double t, List<RoadSubgradeLayer>? layers, bool shoulder = false)
    {
        var s = new RoadSide
        {
            Components = { new RoadComponent { Name = "Lane", Width = w, Slope = 0, Courses = { new RoadCourse { Name = "Surface", Thickness = t, Material = "HMA" } } } },
            SideSlope = new RoadSideSlope { CutRatio = 2, FillRatio = 3, MaxWidth = 40 },
            Subgrade = layers,
        };
        if (shoulder)
            s.Components.Add(new RoadComponent { Name = "Shoulder", Width = Ft(8), Slope = -0.04, Courses = { new RoadCourse { Name = "Surface", Thickness = In(6), Material = "HMA" } } });
        return s;
    }

    private static List<RoadSubgradeLayer> Layers(double extend) => new()
    {
        new RoadSubgradeLayer { Name = "Granular subbase", Kind = RoadSubgradeKind.Subbase, Thickness = In(6), Material = "Aggregate subbase", ExtendBeyond = extend },
        new RoadSubgradeLayer { Name = "Lime treated subgrade", Kind = RoadSubgradeKind.TreatedSubgrade, Thickness = In(12), Material = "Lime treated soil", Under = { "Lane" } },
    };

    [Fact]
    public void Layers_ConstantThicknessUnderTheCourses_StepAndExtension_Volumes()
    {
        var d = SbmDocument.Sample();
        var al = d.Alignment();
        double s0 = al.StartStation + 10, s1 = s0 + 30, w = Ft(12);
        var def = new RoadDefinition { Name = "Sub", AlignmentName = al.Name, BeginStation = s0, EndStation = s1,
            Left = Side(w, In(12), Layers(Ft(2)), shoulder: true), Right = Side(w, In(12), Layers(Ft(2)), shoulder: true) };
        var m = RoadGenerator.Generate(def, al);
        var sub = m.Parts.Where(p => p.Subgrade != null).ToList();
        Assert.Equal(4, sub.Count);
        Assert.All(sub, p => Assert.Equal(RoadGenerator.SubgradeComponent, p.Component));
        // a vertically shifted band: area = thickness x horizontal width (the shoulder's 6 in courses step the top up 6 in at the lane edge)
        var sb = sub.Single(p => p.Side == "right" && p.Course == "Granular subbase");
        Assert.InRange(sb.Volume / (In(6) * (w + Ft(8) + Ft(2)) * 30), 0.98, 1.02);
        var tr = sub.Single(p => p.Side == "right" && p.Course == "Lime treated subgrade");
        Assert.InRange(tr.Volume / (In(12) * w * 30), 0.98, 1.02);
        // level lines: top of subgrade 12 in below grade under the lane, the subbase bottom 18 in, the treated layer bottom 30 in
        double fg = al.ElevationAt(s0, 0);
        double Z(string name) => m.Levels.Single(l => l.Side == "right" && l.Name == name).Sections[0].Line[0].Z;
        Assert.Equal(fg - In(12), Z("top of subgrade"), 6);
        Assert.Equal(fg - In(18), Z("bottom of Granular subbase"), 6);
        Assert.Equal(fg - In(30), Z("bottom of Lime treated subgrade"), 6);
        // the subbase line steps up under the shoulder (6 in courses) and runs on 2 ft at the shoulder bottom's slope
        var line = m.Levels.Single(l => l.Side == "left" && l.Name == "bottom of Granular subbase").Sections[0].Line;
        Assert.Equal(-(w + Ft(10)), line[^1].Offset, 6);
        Assert.Equal(al.ElevationAt(s0, 0) - 0.04 * Ft(10) - In(12), line[^1].Z, 4);
    }

    [Fact]
    public void Earthwork_IsMeasuredToTheBottomOfThePlacedSubbase_NotTheTreatedLayer()
    {
        var d = SbmDocument.Sample();
        var al = d.Alignment();
        double s0 = al.StartStation + 10, s1 = s0 + 20, w = Ft(12), t = In(12), ts = In(6), e = Ft(2);
        RoadDefinition Def(List<RoadSubgradeLayer>? l) => new() { Name = "Fill", AlignmentName = al.Name, BeginStation = s0, EndStation = s1,
            Left = Side(w, t, l), Right = Side(w, t, l) };
        double gz = al.ElevationAt(s0) - 3;
        var m0 = RoadGenerator.Generate(Def(null), al, _ => gz);
        var m1 = RoadGenerator.Generate(Def(Layers(e)), al, _ => gz);
        Assert.Equal(m0.EarthworkSections.Count, m1.EarthworkSections.Count);
        // per side the fill loses the subbase under the lane and, over the extension, the band between the side slope (3H:1V) and the subbase bottom
        double less = 2 * (w * ts + e * (t + ts) - e * e / 6);
        for (int i = 0; i < m0.EarthworkSections.Count; i++)
        {
            Assert.Equal(0, m1.EarthworkSections[i].Cut, 6);
            Assert.Equal(m0.EarthworkSections[i].Fill - less, m1.EarthworkSections[i].Fill, 3);
        }
        // the treated layer (in-place soil) does not move the earthwork line: same result with the subbase alone
        var m2 = RoadGenerator.Generate(Def(new() { Layers(e)[0] }), al, _ => gz);
        Assert.Equal(m2.FillVolume!.Value, m1.FillVolume!.Value, 6);
    }

    [Fact]
    public void BadInputs_AreReported_NotBuilt()
    {
        var d = SbmDocument.Sample();
        var al = d.Alignment();
        var layers = new List<RoadSubgradeLayer>
        {
            new() { Name = "No thickness", Kind = RoadSubgradeKind.PreparedSubgrade },
            new() { Name = "Unknown", Thickness = In(6), Under = { "Median" } },
            new() { Name = "Gap", Thickness = In(6), Under = { "Lane", "Shoulder", "Ditch" }, Unresolved = "UNRESOLVED (p.3): subbase material - not named." },
        };
        var side = Side(Ft(12), In(12), layers, shoulder: true);
        side.Components.Add(new RoadComponent { Name = "Curb", Kind = RoadComponentKind.CurbAndGutter,
            Section = new() { new(0, -In(6)), new(In(6), -In(6)), new(In(6), In(6)), new(0, In(6)) } });
        side.Components.Add(new RoadComponent { Name = "Ditch", Kind = RoadComponentKind.Ditch, Width = Ft(4), Slope = -0.25, Courses = { new RoadCourse { Name = "Sod", Thickness = In(2) } } });
        var def = new RoadDefinition { Name = "Bad", AlignmentName = al.Name, BeginStation = al.StartStation + 10, EndStation = al.StartStation + 20, Right = side };
        var m = RoadGenerator.Generate(def, al);
        Assert.DoesNotContain(m.Parts, p => p.Subgrade != null);
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("'No thickness' has no thickness"));
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("Median not found"));
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("must be adjacent"));
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("UNRESOLVED (p.3)"));
        Assert.Equal(new[] { "finished grade", "top of subgrade" }, m.Levels.Select(l => l.Name));   // no layer built
    }

    [Fact]
    public void RoundTrip_OmittedWhenNull_IfcSubgradePart()
    {
        var d = SbmDocument.Sample();
        var al = d.Alignment();
        var def = new RoadDefinition { Name = "Ifc sub", AlignmentName = al.Name, BeginStation = al.StartStation + 10, EndStation = al.StartStation + 30,
            Left = Side(Ft(12), In(12), null), Right = Side(Ft(12), In(12), Layers(Ft(1))) };
        d.Project.Roads.Add(def);
        string json = d.Project.ToJson();
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"Subgrade\""));
        var back = BridgeProject.FromJson(json);
        Assert.Null(back.Roads[0].Left.Subgrade);
        Assert.Equal(RoadSubgradeKind.TreatedSubgrade, back.Roads[0].Right.Subgrade![1].Kind);
        d.Regenerate();
        string path = Path.Combine(Path.GetTempPath(), "sbm-roadsub-" + Guid.NewGuid().ToString("N")[..8] + ".ifc");
        try
        {
            d.ExportIfc(path);
            string ifc = File.ReadAllText(path);
            Assert.Contains("'SUBGRADE'", ifc);
            Assert.Contains("IFCEARTHWORKSFILL(", ifc);
            Assert.Contains(".SUBGRADEBED.", ifc);
            var roads = d.Roads();
            var tags = SBM.IO.Database.ModelDatabase.BuildTagIndex(d.Project, Array.Empty<SBM.IO.Ifc.IfcBridgeData>(), roads);
            Assert.All(roads[0].Parts, p => Assert.Contains("'" + tags.Lookup(p) + "'", ifc));
        }
        finally { File.Delete(path); }
    }
}
