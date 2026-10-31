using System.Text.RegularExpressions;
using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Road;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Approach slabs (<see cref="ApproachSlabDefinition"/>, 2026-10-29; ownership per bridge end, user answer 7a.3; generic test inputs).</summary>
public class ApproachSlabTests
{
    private static SbmDocument WithSlab(ApproachSlabOwner? owner, ApproachSlabLengthMeasure? measure = ApproachSlabLengthMeasure.NormalToDeckEnd)
    {
        var d = SbmDocument.Sample();
        d.Project.Bridges[0].Supports[0].ApproachSlab = new ApproachSlabDefinition
        {
            Length = Ft(20), Thickness = In(12), Owner = owner, LengthMeasure = measure, Source = "test",
            Sleeper = new SleeperSlabDefinition { Width = Ft(2), Depth = In(18), BeyondEnd = In(6) },
        };
        d.Regenerate();
        return d;
    }

    [Fact]
    public void Volume_TopFollowsTheDeckGrade_Sleeper()
    {
        var d = WithSlab(ApproachSlabOwner.Bridge);
        var b = d.Project.Bridges[0];
        var c = d.Concrete();
        var a = Assert.Single(c.ApproachSlabs);
        var f = d.Model(b.Name)!.Framing;
        var al = d.Alignment();
        // slab width along the deck end line between the deck edges
        double s0 = al.Inverse(f.Deck!.BeginLinePoint).Station;
        var p0 = al.PointAt(s0, 0); var t = al.PointAt(s0 + 0.5, 0) - p0; t = t / t.Length;
        var dir = f.Deck.BeginLineDirection;
        double sin = Math.Abs(t.X * dir.Y - t.Y * dir.X);
        double width = (b.Deck.RightAt(s0) - b.Deck.LeftAt(s0)) / sin;
        Assert.InRange(a.Volume / (Ft(20) * In(12) * width), 0.98, 1.02);
        // every top vertex lies on the finished grade (continued beyond the deck end)
        var tops = a.Mesh.Positions.GroupBy(p => (Math.Round(p.X, 4), Math.Round(p.Y, 4))).Select(g => g.MaxBy(p => p.Z)).ToList();
        foreach (var p in tops)
        {
            var iv = al.Inverse(p.XY);
            Assert.Equal(b.Deck.FinishedElevation(al, iv.Station, iv.Offset), p.Z, 3);
        }
        Assert.NotNull(a.Sleeper);
        Assert.InRange(a.SleeperVolume / (Ft(2) * In(18) * width), 0.98, 1.02);
        Assert.Null(a.Assumption);
        // the slab lies beyond the begin deck end (signed distance from the end line: + into the deck), its near face on the end line
        Assert.All(a.Mesh.Positions, p => Assert.True(f.Deck.FromBegin(p.XY) <= 1e-4, $"{f.Deck.FromBegin(p.XY)}"));
        Assert.Equal(0, a.Mesh.Positions.Max(p => f.Deck.FromBegin(p.XY)), 4);
        Assert.Equal(-Ft(20), a.Mesh.Positions.Min(p => f.Deck.FromBegin(p.XY)), 4);
    }

    [Fact]
    public void OwnerAndMeasureNotGiven_Unresolved_OmittedWhenNull()
    {
        Assert.DoesNotContain("ApproachSlab", SbmDocument.Sample().Project.ToJson());
        var d = WithSlab(null, null);
        var a = Assert.Single(d.Concrete().ApproachSlabs);
        Assert.NotNull(a.Assumption);
        Assert.Contains(d.Concrete().Diagnostics, x => x.Message.Contains("UNRESOLVED") && x.Message.Contains("ownership"));
        Assert.Contains(d.Concrete().Diagnostics, x => x.Message.Contains("UNRESOLVED") && x.Message.Contains("length measure"));
        var back = BridgeProject.FromJson(d.Project.ToJson());
        Assert.Null(back.Bridges[0].Supports[0].ApproachSlab!.Owner);
        Assert.Equal(In(18), back.Bridges[0].Supports[0].ApproachSlab!.Sleeper!.Depth, 9);
    }

    [Fact]
    public void Ifc_OwnerRoad_ContainedInTheRoad_OwnerBridge_InTheDeckPart()
    {
        foreach (var owner in new[] { ApproachSlabOwner.Bridge, ApproachSlabOwner.Road })
        {
            var d = WithSlab(owner);
            var al = d.Alignment();
            var a = d.Concrete().ApproachSlabs[0];
            d.Project.Roads.Add(new RoadDefinition
            {
                Name = "Approach road", AlignmentName = al.Name, BeginStation = a.StationMin - 30, EndStation = a.StationMax,
                Left = new RoadSide { Components = { new RoadComponent { Name = "Lane", Width = Ft(12), Courses = { new RoadCourse { Name = "Surface", Thickness = In(4), Material = "HMA" } } } } },
                Right = new RoadSide { Components = { new RoadComponent { Name = "Lane", Width = Ft(12), Courses = { new RoadCourse { Name = "Surface", Thickness = In(4), Material = "HMA" } } } } },
            });
            string path = Path.Combine(Path.GetTempPath(), "sbm-apslab-" + Guid.NewGuid().ToString("N")[..8] + ".ifc");
            try
            {
                d.ExportIfc(path);
                var lines = File.ReadAllLines(path);
                string slab = lines.Single(l => l.Contains("IFCSLAB(") && l.Contains(".APPROACH_SLAB.")).Split('=')[0];
                string road = lines.Single(l => l.Contains("IFCROAD(")).Split('=')[0];
                var rel = lines.Single(l => l.Contains("IFCRELCONTAINEDINSPATIALSTRUCTURE(") && Regex.IsMatch(l, Regex.Escape(slab) + @"[,)]"));
                Assert.Equal(owner == ApproachSlabOwner.Road, rel.TrimEnd().EndsWith(road + ");"));
                Assert.Contains(lines, l => l.Contains("'SLEEPER_SLAB'"));
            }
            finally { File.Delete(path); }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RoadTiedToTheBridgeBegin_EndsOnTheSlabFarEnd_OrTheDeckEnd(bool withSlab)
    {
        var d = withSlab ? WithSlab(ApproachSlabOwner.Road) : SbmDocument.Sample();
        d.Regenerate();
        var b = d.Project.Bridges[0];
        var al = d.Alignment();
        var deck = d.Model(b.Name)!.Framing.Deck!;
        double sEnd = al.Inverse(deck.BeginLinePoint).Station;
        RoadSide Side() => new() { Components = { new RoadComponent { Name = "Lane", Width = Ft(14), Courses = { new RoadCourse { Name = "Surface", Thickness = In(4), Material = "HMA" } } } } };
        d.Project.Roads.Add(new RoadDefinition { Name = "Tied", AlignmentName = al.Name, BeginStation = sEnd - 60, EndStation = sEnd,
            EndAtBridge = new RoadBridgeTie { Bridge = b.Name, End = BridgeEndKind.Begin }, Left = Side(), Right = Side() });
        var rm = d.Roads().Single();
        Assert.DoesNotContain(rm.Diagnostics, x => x.Message.Contains("tie"));
        double target = withSlab ? -(Ft(20)) : 0;   // NormalToDeckEnd, no gap
        // the end section of every course solid: the points nearest the bridge lie on the far end line of the slab (or the deck end)
        foreach (var p in rm.Parts)
        {
            double near = p.Mesh.Positions.Max(q => deck.FromBegin(q.XY));
            Assert.Equal(target, near, 3);
        }
        // every road edge line ends on that line
        foreach (var e in rm.EdgeLines) Assert.Equal(target, deck.FromBegin(e[^1].XY), 3);
    }
}
