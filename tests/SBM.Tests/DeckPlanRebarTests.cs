using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Project;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Plan-driven deck reinforcing (zones + plan bar list); the default generator is covered by <see cref="ConcreteTests"/>.</summary>
public class DeckPlanRebarTests
{
    private static (BridgeFraming f, ConcreteModel c) Build(Action<DeckReinforcementDefinition> setup)
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        b.Deck.Reinforcement.Plan = new DeckPlanReinforcement();
        setup(b.Deck.Reinforcement);
        var f = FramingGenerator.Generate(b, p.Alignments[0]);
        Assert.False(f.HasErrors);
        return (f, ConcreteGenerator.Generate(f, p.Units));
    }

    [Fact]
    public void TransverseZone_PlacesCountedLinesWithPlanMark()
    {
        var (f, c) = Build(r =>
        {
            r.Plan!.Transverse.Add(new DeckTransverseZone
            {
                Name = "mid", Layer = DeckBarLayer.Top, Size = "#7", Reference = DeckBarSide.Right, StartDistance = Ft(60), Spacing = In(7), Count = 25,
                Pieces = { new DeckLinePiece { Mark = "7a1" } },
            });
        });
        var m = c.Marks.Single(x => x.Mark == "7a1");
        Assert.Equal(25, m.Count);
        Assert.Equal("#7", m.Size.Name);
        Assert.Equal(25, c.Bars.Count(b => b.Mark == "7a1"));
        // one piece spans the deck less the side covers; far from the ends of the deck
        var d = f.Definition.Deck;
        double width = d.RightEdgeOffset - d.LeftEdgeOffset - 2 * d.Reinforcement.SideCover;
        Assert.InRange(m.Length, width - In(2), width + In(2));
        // top mat: below finished grade by the cover + half the bar
        var z = c.Bars[0].Points[0].Z;
        Assert.True(z < 1e9);
    }

    [Fact]
    public void TransverseZone_FixedPlusRemainderPieces_LapAndTotalLength()
    {
        var (f, c) = Build(r =>
        {
            r.Plan!.Transverse.Add(new DeckTransverseZone
            {
                Layer = DeckBarLayer.Bottom, Size = "#7", Reference = DeckBarSide.Left, StartDistance = Ft(80), Spacing = In(7), Count = 10, Lap = Ft(3.25),
                FirstFrom = DeckBarSide.Right,
                Pieces = { new DeckLinePiece { Mark = "7a12", Length = Ft(30) }, new DeckLinePiece { Mark = "7a13" } },
            });
        });
        var a = c.Marks.Single(x => x.Mark == "7a12");
        var b2 = c.Marks.Single(x => x.Mark == "7a13");
        Assert.Equal(10, a.Count);
        Assert.Equal(10, b2.Count);
        Assert.Equal(Ft(30), a.Length, 6);
        var d = f.Definition.Deck;
        double width = d.RightEdgeOffset - d.LeftEdgeOffset - 2 * d.Reinforcement.SideCover;
        // pieces overlap by the lap: fixed + remainder - lap = line
        Assert.InRange(a.Length + b2.MinLength - Ft(3.25), width - In(1), width + In(1));
    }

    [Fact]
    public void BetweenZone_PlacesOneLineBetweenEachPair()
    {
        var (_, c) = Build(r =>
        {
            r.Plan!.Transverse.Add(new DeckTransverseZone { Layer = DeckBarLayer.Top, Size = "#7", StartDistance = Ft(50), Spacing = In(10), Count = 11, Pieces = { new DeckLinePiece { Mark = "7a1" } } });
            r.Plan.Transverse.Add(new DeckTransverseZone { Layer = DeckBarLayer.Top, Size = "#5", BetweenZone = 0, Pieces = { new DeckLinePiece { Mark = "5j1", Length = Ft(6) } }, FirstFrom = DeckBarSide.Left });
        });
        Assert.Equal(11, c.Marks.Single(x => x.Mark == "7a1").Count);
        var j = c.Marks.Single(x => x.Mark == "5j1");
        Assert.Equal(10, j.Count);
        Assert.Equal(Ft(6), j.Length, 6);
    }

    [Fact]
    public void LongitudinalBay_EqualSpacesBetweenGirders_AlternateLayersAndLapChain()
    {
        var (f, c) = Build(r =>
        {
            r.Plan!.Longitudinal.Add(new DeckLongZone
            {
                Left = DeckLongRef.OfGirder(1), Right = DeckLongRef.OfGirder(2), Spaces = 8, LeftMargin = In(8.5), RightMargin = In(8.5), Size = "#6", Mark = "6b1",
                ClosureMark = "6b2", StockLength = Ft(40), Lap = Ft(3), FirstLayer = DeckBarLayer.Top,
            });
        });
        var stock = c.Marks.Single(x => x.Mark == "6b1");
        Assert.Equal(Ft(40), stock.Length, 6);
        // 9 lines, every line is longer than one stock bar on the sample bridge, so each line has at least one stock bar
        Assert.True(stock.Count >= 9);
        Assert.Contains(c.Marks, m => m.Mark == "6b2");
        // layers alternate: line 0 top, line 1 bottom -> two distinct elevations relative to the grade
        var firstPieces = c.Bars.Where(b => b.Mark == "6b1").ToList();
        Assert.True(firstPieces.Select(b => Math.Round(b.Points[0].Z - f.Definition.Deck.Thickness * 0, 3)).Distinct().Count() > 1);
    }

    [Fact]
    public void PlanBarList_UnplacedRowsAreScheduleOnly_AndDifferencesAreReported()
    {
        var (_, c) = Build(r =>
        {
            r.Plan!.Transverse.Add(new DeckTransverseZone { Layer = DeckBarLayer.Top, Size = "#7", StartDistance = Ft(50), Spacing = In(10), Count = 11, Pieces = { new DeckLinePiece { Mark = "7a1" } } });
            r.Plan.PlanBarList.Add(new DeckPlanBarLine { Mark = "7a1", Size = "#7", Count = 12, Length = Ft(34), WeightLb = 5000 });
            r.Plan.PlanBarList.Add(new DeckPlanBarLine { Mark = "5e1", Size = "#5", Count = 30, Length = Ft(9) + In(4), WeightLb = 292, Location = "END DAM" });
        });
        var sched = c.Marks.Single(m => m.Mark == "5e1");
        Assert.Equal(30, sched.Count);
        Assert.Contains("schedule only", sched.Location);
        Assert.DoesNotContain(c.Bars, b => b.Mark == "5e1");
        Assert.Contains(c.Diagnostics, d => d.Message.Contains("mark 7a1") && d.Message.Contains("plan 12 bars"));
        Assert.Contains(c.Diagnostics, d => d.Message.Contains("schedule only") && d.Message.Contains("5e1"));
    }

    [Fact]
    public void PlanReinforcement_RoundTripsThroughProjectJson()
    {
        var p = SampleProject.Create();
        var r = p.Bridges[0].Deck.Reinforcement;
        r.Plan = new DeckPlanReinforcement();
        r.Plan.Transverse.Add(new DeckTransverseZone { Name = "z", Count = 3, Spacing = In(7), Pieces = { new DeckLinePiece { Mark = "7a1", Length = Ft(10) } } });
        r.Plan.Longitudinal.Add(new DeckLongZone { Mark = "6b1", Positions = new() { In(2), In(6.5) }, Right = DeckLongRef.OfGirder(3) });
        var p2 = BridgeProject.FromJson(p.ToJson());
        var q = p2.Bridges[0].Deck.Reinforcement.Plan!;
        Assert.Equal(3, q.Transverse[0].Count);
        Assert.Equal(Ft(10), q.Transverse[0].Pieces[0].Length, 9);
        Assert.Equal(2, q.Longitudinal[0].Positions!.Count);
        Assert.Equal(3, q.Longitudinal[0].Right.Girder);
        Assert.NotSame(r.Plan, r.Clone().Plan);
    }

    [Fact]
    public void WithoutPlan_TheGenericLayoutIsUnchanged()
    {
        var p = SampleProject.Create();
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var c = ConcreteGenerator.Generate(f, p.Units);
        Assert.Null(p.Bridges[0].Deck.Reinforcement.Plan);
        Assert.Contains(c.Marks, m => m.Mark.StartsWith("T"));
        Assert.Contains(c.Marks, m => m.Mark.StartsWith("L"));
    }
}
