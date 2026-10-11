using SBM.Api;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Checks;
using SBM.Core.Units;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class DeckEdgeStripTests
{
    private static Alignment Align(CrossSlopeTable cs)
    {
        var doc = SbmDocument.Sample();
        var a = doc.Alignment(doc.Bridge().AlignmentName);
        a.CrossSlope = cs;
        return a;
    }

    [Fact]
    public void LowSideIsLevelAtTheGutterElevation_HighSideFollowsTheSlope_SwitchingAtTheSignChange()
    {
        // Right slope runs from +2% (rises toward the right edge: high side) to -2% (low side); left is always -2% (low side).
        var cs = new CrossSlopeTable { Rows = { new CrossSlopeRow(0, -0.02, 0.02), new CrossSlopeRow(100, -0.02, -0.02) } };
        var a = Align(cs);
        var deck = new DeckDefinition { LeftEdgeOffset = -10, RightEdgeOffset = 10, EdgeStrip = new DeckEdgeStrip { Width = 2 } };
        double sta = 25;                                  // right slope +1% here
        double Z(double o, double st = 25) => deck.FinishedElevation(a, st, o);
        // High (right) side continues the plane to the edge.
        Assert.Equal(a.ElevationAt(sta, 10), Z(10), 9);
        // Low (left) side: level beyond the gutter line (-8).
        Assert.Equal(a.ElevationAt(sta, -8), Z(-10), 9);
        Assert.Equal(a.ElevationAt(sta, -8), Z(-8.5), 9);
        Assert.Equal(a.ElevationAt(sta, -7), Z(-7), 9);   // inboard of the strip: plane
        // Past the sign change (75: right slope -1%) the right side is level too.
        Assert.Equal(a.ElevationAt(75, 8), Z(10, 75), 9);
        Assert.NotEqual(a.ElevationAt(75, 10), Z(10, 75));
        // Continuous through the zero crossing (station 50).
        Assert.Equal(Z(10, 49.999), Z(10, 50.001), 3);
        // Off: the plane everywhere.
        deck.EdgeStrip = null;
        Assert.Equal(a.ElevationAt(sta, -10), Z(-10), 12);
    }

    [Fact]
    public void StripWidthFollowsAVariableDeckEdge()
    {
        var cs = new CrossSlopeTable { Rows = { new CrossSlopeRow(0, -0.03, -0.03) } };
        var a = Align(cs);
        var deck = new DeckDefinition
        {
            LeftEdgeOffset = -10, RightEdgeOffset = 10, EdgeStrip = new DeckEdgeStrip { Width = 2 },
            LeftEdgeTable = new() { new DeckEdgePoint { Station = 0, Offset = -10 }, new DeckEdgePoint { Station = 100, Offset = -14 } },
        };
        Assert.Equal(a.ElevationAt(50, -10), deck.FinishedElevation(a, 50, -12), 9);   // edge -12 at 50: gutter -10
        Assert.Equal(a.ElevationAt(50, -9), deck.FinishedElevation(a, 50, -9), 9);
    }

    [Fact]
    public void ElevationChainExplainsTheLevelStrip_AndMatchesTheModel()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        var a = doc.Alignment(b.AlignmentName);
        a.CrossSlope = new CrossSlopeTable { Rows = { new CrossSlopeRow(0, -0.02, -0.02) } };
        b.Deck.EdgeStrip = new DeckEdgeStrip { Width = Ft(1 + 7 / 12.0) };
        var u = new UnitSystem();
        double sta = (b.Supports[0].Station + b.Supports[1].Station) / 2;
        var ch = ElevationCheck.Grade(a, sta, b.Deck.RightEdgeOffset, u, "edge", b.Deck);
        Assert.False(ch.Mismatch, string.Join("; ", ch.Checks));
        Assert.Contains(ch.Steps, s => s.Item == "Edge strip level");
        Assert.Equal(b.Deck.FinishedElevation(a, sta, b.Deck.RightEdgeOffset), ch.Steps[^1].Elevation, 9);
        // Inboard of the gutter line: no extra row.
        var ch2 = ElevationCheck.Grade(a, sta, 0, u, "cl", b.Deck);
        Assert.DoesNotContain(ch2.Steps, s => s.Item == "Edge strip level");
    }

    [Fact]
    public void DeckSolidTopFollowsTheLevelStrip()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        var a = doc.Alignment(b.AlignmentName);
        a.CrossSlope = new CrossSlopeTable { Rows = { new CrossSlopeRow(0, -0.02, -0.02) } };
        doc.Regenerate();
        var before = doc.Concrete(b.Name).Deck!;
        double minBefore = before.Bounds().Min.Z, maxBefore = before.Bounds().Max.Z;
        int trisBefore = before.TriangleCount;
        b.Deck.EdgeStrip = new DeckEdgeStrip { Width = Ft(1 + 7 / 12.0) };
        doc.Regenerate();
        var after = doc.Concrete(b.Name).Deck!;
        Assert.True(after.TriangleCount > trisBefore);                       // extra gutter columns
        Assert.True(after.Bounds().Min.Z > minBefore + 0.005);                // both edges (low sides) are raised to the gutter level
        Assert.Equal(maxBefore, after.Bounds().Max.Z, 6);
    }
}
