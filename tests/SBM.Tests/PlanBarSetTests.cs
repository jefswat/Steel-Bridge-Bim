using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Generic plan bar sets (BridgeDefinition.PlanBars, Concrete/PlanBarPlacer) and the deck plan options SideClear / AlternateEnds.</summary>
public class PlanBarSetTests
{
    private static PlanBarSet Line(bool super, int support, PlanBarFrame frame, string mark, params PlanBarPoint[] pts) => new()
    {
        Mark = mark, Size = "#5", Coating = "epoxy", Location = "TEST", Frame = frame, Support = support, Superstructure = super, Path = pts.ToList(),
    };

    [Fact]
    public void SupportAndAlignmentFrames_PlaceBars_AndBillIsCompared()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        var al = doc.Alignment();
        doc.Regenerate();
        double z0 = 100;
        var pb = new PlanBarsDefinition();
        // substructure: a U in the support-0 section copied at 3 positions along the line
        var u = Line(false, 0, PlanBarFrame.Support, "T501", new PlanBarPoint(0, In(-6), PlanBarZRef.Elevation, z0 + 1), new PlanBarPoint(0, In(-6), PlanBarZRef.Elevation, z0),
            new PlanBarPoint(0, In(6), PlanBarZRef.Elevation, z0), new PlanBarPoint(0, In(6), PlanBarZRef.Elevation, z0 + 1));
        u.Positions.AddRange(new[] { 0.0, Ft(1), Ft(2) });
        u.ListedLength = Ft(1) + In(12) + Ft(1);
        pb.Sets.Add(u);
        pb.BarList.Add(new PlanBarListLine { Support = 0, Mark = "T501", Size = "#5", Count = 3, Length = u.ListedLength, WeightLb = 10, Location = "TEST" });
        pb.BarList.Add(new PlanBarListLine { Support = 0, Mark = "T502", Size = "#5", Count = 7, Length = Ft(5), WeightLb = 36.5, Location = "SCHEDULE ONLY" });
        // superstructure: a bar along the alignment 3" under the deck top
        double s0 = b.Supports[0].Station + Ft(10);
        pb.Sets.Add(Line(true, 0, PlanBarFrame.Alignment, "T401", new PlanBarPoint(s0, 0, PlanBarZRef.DeckTop, -In(3)), new PlanBarPoint(s0 + Ft(20), 0, PlanBarZRef.DeckTop, -In(3))));
        // unresolved set: scheduled, not placed
        var un = Line(true, 0, PlanBarFrame.Alignment, "T402");
        un.Unresolved = "not dimensioned (test)"; un.UnplacedCount = 4; un.ListedLength = Ft(3);
        pb.Sets.Add(un);
        b.PlanBars = pb;
        doc.Regenerate();

        var sub = doc.Substructure();
        var unit = sub.Units.First(x => x.SupportIndex == 0);
        var bars = sub.Bars.Where(x => x.Mark == "T501").ToList();
        Assert.Equal(3, bars.Count);
        Assert.All(bars, x => Assert.Equal(unit.Name, x.Unit));
        Assert.Equal(3, sub.Marks.Single(m => m.Mark == "T501").Count);
        Assert.Equal(7, sub.Marks.Single(m => m.Mark == "T502").Count);                      // schedule only
        Assert.Contains("schedule only", sub.Marks.Single(m => m.Mark == "T502").Location);
        Assert.Contains(sub.Diagnostics, d => d.Message.Contains("T502") && d.Message.Contains("schedule only"));
        // frame: W along the support line, U normal toward the span; copies 1 ft apart along the line
        var sl = doc.Framing().Supports[0];
        var p0 = bars.OrderBy(x => Vec2.Dot(x.Points[0].XY - sl.AlignmentPoint, sl.Direction)).ToList();
        Assert.Equal(Ft(1), Vec2.Dot(p0[1].Points[1].XY - p0[0].Points[1].XY, sl.Direction), 9);
        Assert.Equal(In(12), (p0[0].Points[2] - p0[0].Points[1]).Length, 9);
        Assert.Equal(z0, p0[0].Points[1].Z, 9);
        Assert.True(Vec2.Dot(p0[0].Points[2].XY - p0[0].Points[1].XY, Vec2.FromAngle(al.AngleAt(sl.Station))) > 0);   // + toward the span (up-station at the first support)
        // superstructure bar follows the deck top
        var c = doc.Concrete();
        var t401 = c.Bars.Single(x => x.Mark == "T401");
        var so = al.Inverse(t401.Points[0].XY);
        Assert.Equal(b.Deck.FinishedElevation(al, so.Station, so.Offset) - In(3), t401.Points[0].Z, 9);
        var t402 = c.Marks.Single(m => m.Mark == "T402");
        Assert.Equal(4, t402.Count);
        Assert.DoesNotContain(c.Bars, x => x.Mark == "T402");
        Assert.Contains(c.Diagnostics, d => d.Message.Contains("UNRESOLVED") && d.Message.Contains("T402"));
        // JSON: omitted when null, round-trips when set
        Assert.Contains("\"PlanBars\"", doc.Project.ToJson());
        var back = BridgeProject.FromJson(doc.Project.ToJson());
        Assert.Equal(3, back.Bridges[0].PlanBars!.Sets.Count);
        Assert.DoesNotContain("\"PlanBars\"", SbmDocument.Sample().Project.ToJson());
    }

    private static (BridgeFraming f, ConcreteModel c) Deck(Action<DeckPlanReinforcement> setup)
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        b.Deck.Reinforcement.Plan = new DeckPlanReinforcement();
        setup(b.Deck.Reinforcement.Plan);
        var f = FramingGenerator.Generate(b, p.Alignments[0]);
        return (f, ConcreteGenerator.Generate(f, p.Units));
    }

    [Fact]
    public void DeckTransverseSideClear_AndStaggeredLongitudinalLaps()
    {
        var (f, c) = Deck(pl =>
        {
            pl.Transverse.Add(new DeckTransverseZone { Name = "top", Layer = DeckBarLayer.Top, Size = "#4", Reference = DeckBarSide.Left, StartDistance = Ft(50), Spacing = In(9), Count = 3, Pieces = { new DeckLinePiece { Mark = "X1" } } });
            pl.Transverse.Add(new DeckTransverseZone { Name = "bot", Layer = DeckBarLayer.Bottom, Size = "#4", Reference = DeckBarSide.Left, StartDistance = Ft(50), Spacing = In(9), Count = 3, SideClear = In(4.5), Pieces = { new DeckLinePiece { Mark = "X2" } } });
            pl.Longitudinal.Add(new DeckLongZone
            {
                Name = "long", Positions = new() { Ft(5), Ft(6) }, PositionLayers = new() { DeckBarLayer.Top, DeckBarLayer.Top }, Size = "#4", Mark = "L1", ClosureMark = "L2",
                StockLength = Ft(60), Lap = Ft(2.5), AlternateEnds = true,
            });
        });
        double top = c.Marks.Single(m => m.Mark == "X1").Length, bot = c.Marks.Single(m => m.Mark == "X2").Length;
        Assert.Equal(2 * (In(4.5) - f.Definition.Deck.Reinforcement.SideCover), top - bot, 3);
        // line 0: stock bar at the begin; line 1 (odd): stock bar at the end
        var a = f.Alignment;
        double D(BarPlacement x) => a.Inverse(x.Points[0].XY).Distance;
        double Off(BarPlacement x) => a.Inverse(x.Points[0].XY).Offset;
        var l1 = c.Bars.Where(x => x.Mark == "L1").ToList();
        var l2 = c.Bars.Where(x => x.Mark == "L2").ToList();
        Assert.True(l1.Count >= 2 && l2.Count >= 2);
        double FromLeft(BarPlacement x) { var so = a.Inverse(x.Points[0].XY); return so.Offset - f.Definition.Deck.LeftAt(so.Station); }
        var line0 = c.Bars.Where(x => x.Mark is "L1" or "L2").GroupBy(x => Math.Round(FromLeft(x) / Ft(1))).OrderBy(g => g.Key).ToList();
        Assert.Equal(2, line0.Count);
        Assert.Equal("L1", line0[0].OrderBy(D).First().Mark);
        Assert.Equal("L2", line0[1].OrderBy(D).First().Mark);
        // the json keeps the new options only when set
        var jo = new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        Assert.DoesNotContain("SideClear", System.Text.Json.JsonSerializer.Serialize(new DeckTransverseZone(), jo));
        Assert.DoesNotContain("AlternateEnds", System.Text.Json.JsonSerializer.Serialize(new DeckLongZone(), jo));
    }
}
