using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Deck plan reinforcing options: DeckTransverseZone.SideClear (per-mat end clear) and DeckLongZone.AlternateEnds (staggered laps).</summary>
public class DeckPlanOptionsTests
{
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
