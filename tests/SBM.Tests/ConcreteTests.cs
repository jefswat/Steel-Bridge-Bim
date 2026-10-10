using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Project;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class ConcreteTests
{
    private static (BridgeFraming f, ConcreteModel c) Build(Action<BridgeDefinition>? tweak = null)
    {
        var p = SampleProject.Create();
        tweak?.Invoke(p.Bridges[0]);
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        Assert.False(f.HasErrors);
        return (f, ConcreteGenerator.Generate(f, p.Units));
    }

    [Fact]
    public void DeckVolume_EqualsAreaTimesThickness()
    {
        var (f, c) = Build();
        Assert.Equal(f.Deck!.Area * f.Definition.Deck.Thickness, c.DeckVolume, 1);
        Assert.True(c.HaunchVolume > 0);
        Assert.Equal(2, c.Barriers.Count);
    }

    [Fact]
    public void SstrBarrier_SectionAreaMatchesStandard()
    {
        // RL-SSTR: 36" high, 14 1/2" base, 7 1/2" top → about 396 in² (less chamfers and the 3/4" back step).
        var prof = BarrierProfiles.TxDotSstr();
        double area = Math.Abs(SBM.Core.Solids.MeshBuilder.SignedArea(prof)) / (0.0254 * 0.0254);
        Assert.InRange(area, 370, 396);
        var (toe, top, h) = BarrierProfiles.PlanLines(new BarrierDefinition());
        Assert.Equal(In(14.5), toe, 6);
        Assert.Equal(In(36), h, 6);
    }

    [Fact]
    public void SstrBars_MatchStandardShapes()
    {
        var (_, c) = Build();
        var s = c.Marks.Single(m => m.Mark == "S");
        var u = c.Marks.Single(m => m.Mark == "U");
        Assert.Equal("#4", s.Size.Name);
        Assert.Equal(In(34.75), s.Shape.Dimensions.Single(d => d.Letter == "A").Value, 6);
        Assert.InRange(s.Shape.Dimensions.Single(d => d.Letter == "C").Value, In(9.5), In(10));
        Assert.Equal(In(16.5), u.Shape.Dimensions.Single(d => d.Letter == "A").Value, 6);
        Assert.Equal(In(4.75), u.Shape.Dimensions.Single(d => d.Letter == "B").Value, 6);
        Assert.Equal(s.Count, u.Count);
        // 8 Bars R per rail run continuously (lapped) along both rails.
        Assert.Contains(c.Marks, m => m.Mark.StartsWith("R"));
    }

    [Fact]
    public void Bars_NeverExceedMaxLength()
    {
        var (f, c) = Build();
        double max = f.Definition.Deck.Reinforcement.MaxBarLength;
        Assert.All(c.Marks, m => Assert.True(m.Length <= max + In(1), $"{m.Mark} {m.Length}"));
        Assert.All(c.Bars, b => Assert.True(b.Points.Zip(b.Points.Skip(1)).Sum(p => (p.Second - p.First).Length) <= max + In(1)));
    }

    [Fact]
    public void NegativeMomentSteel_ReachesOnePercent_TwoThirdsTop()
    {
        var (f, c) = Build();
        var r = f.Definition.Deck.Reinforcement;
        double width = f.Definition.Deck.RightEdgeOffset - f.Definition.Deck.LeftEdgeOffset;
        double req = 0.01 * width * f.Definition.Deck.Thickness;
        // Count longitudinal bars crossing the pier at mid-depth top and bottom.
        var pier = f.Supports[1];
        var align = f.Alignment;
        double dPier = align.StationToDistance(pier.Station);
        double top = 0, bottom = 0;
        double zDeck = align.ElevationAt(pier.Station, 0);
        foreach (var b in c.Bars)
        {
            var mk = c.Marks.First(m => m.Mark == b.Mark);
            if (!(mk.Location.Contains("longitudinal") || mk.Location.Contains("negative"))) continue;
            double d0 = align.Inverse(b.Points[0].XY).Distance, d1 = align.Inverse(b.Points[^1].XY).Distance;
            if (Math.Min(d0, d1) > dPier || Math.Max(d0, d1) < dPier) continue;
            var mid = b.Points[b.Points.Length / 2];
            var so = align.Inverse(mid.XY);
            bool isTop = mid.Z > align.ElevationAt(so.Station, so.Offset) - f.Definition.Deck.Thickness / 2;
            if (isTop) top += mk.Size.Area; else bottom += mk.Size.Area;
        }
        // Laps put two bars at a section; count each line once by halving overlaps conservatively is not needed:
        // provided area at the pier must meet the requirement.
        Assert.True(top + bottom >= req * 0.999, $"total {top + bottom} < {req}");
        Assert.True(top >= 2.0 / 3 * req * 0.999, $"top {top} < {2.0 / 3 * req}");
        Assert.Contains(c.Notes, n => n.Contains("1% of"));
    }

    [Fact]
    public void LapLength_AashtoClassB_IsReasonable()
    {
        var b5 = RebarCatalog.Size("#5");
        double lap = RebarCatalog.ClassBLap(b5, 4, 60, true, In(2.5), In(9));
        Assert.InRange(lap / In(1), 18, 60);
        double plain = RebarCatalog.ClassBLap(b5, 4, 60, false, In(2.5), In(9));
        Assert.True(plain <= lap);
    }

    [Fact]
    public void NormalTransverseBars_OnHighSkew_ClipAtSkewedEnds()
    {
        var (f, c) = Build(b => { foreach (var s in b.Supports) s.SkewDegrees = 30; });
        Assert.Contains(c.Notes, n => n.Contains("normal to the alignment"));
        // Clipped bars at the skewed ends produce shorter (varying) transverse bars.
        Assert.Contains(c.Marks, m => m.Mark.StartsWith("T") && m.MinLength < c.Marks.Where(x => x.Mark.StartsWith("T")).Max(x => x.Length) - Ft(2));
    }

    [Fact]
    public void Api_SetsDeckReinforcementWithUnits()
    {
        var doc = SbmDocument.Sample();
        doc.Set("Bridges[0].Deck.Reinforcement.TopCover", "2");
        doc.Set("Bridges[0].Deck.Reinforcement.MaxBarLength", "40");
        doc.Set("Bridges[0].Deck.Reinforcement.NegativeRegionFraction", "0.3");
        var r = doc.Bridge().Deck.Reinforcement;
        Assert.Equal(In(2), r.TopCover, 9);
        Assert.Equal(Ft(40), r.MaxBarLength, 9);
        Assert.Equal(0.3, r.NegativeRegionFraction, 9);
        Assert.All(doc.Concrete().Marks, m => Assert.True(m.Length <= Ft(40) + In(1)));
    }
}
