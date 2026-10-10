using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Checks;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class ElevationCheckTests
{
    [Fact]
    public void DiscBearings_HandChainMatchesModel()
    {
        var doc = SbmDocument.Sample();
        var chains = doc.BearingElevations();
        Assert.Equal(15, chains.Count);
        Assert.All(chains, c => Assert.False(c.Mismatch, string.Join("; ", c.Checks)));
        Assert.All(chains, c => Assert.Contains(c.Checks, t => t.StartsWith("Bearing seat matches")));
    }

    [Fact]
    public void ElastomericBearings_AndPlateChanges_Match()
    {
        var doc = SbmDocument.Sample();
        doc.Set("Bridges[0].Bearings.Type", "Elastomeric");
        doc.Set("Bridges[0].Deck.Haunch", "4");
        var chains = doc.BearingElevations();
        Assert.All(chains, c => Assert.False(c.Mismatch, string.Join("; ", c.Checks)));
        Assert.All(chains, c => Assert.Contains(c.Steps, s => s.Item == "− Elastomeric pad"));
    }

    [Fact]
    public void GenericBearings_TotalTheAssumedHeight_WithPerSupportAnchors()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Bearings.Type = BearingType.Generic;
        b.Bearings.GenericHeight = In(9);
        b.Bearings.MasonryPadThickness = In(0.125);
        b.Bearings.AnchorEdgeDistance = In(3); b.Bearings.AnchorEdgeDistanceFixed = In(5);
        var pier = b.Supports[1];
        pier.Bearings = b.Bearings.Clone();
        pier.Bearings.AnchorSpacingAcross = In(44); pier.Bearings.AnchorSpacingAlong = In(18);
        pier.Bearings.AnchorBoltDiameter = In(2.5);
        pier.Fixity = BearingFixity.Fixed;
        pier.GirderFixities = new() { BearingFixity.Expansion };   // first girder MD, rest fixed
        doc.Regenerate();
        var chains = doc.BearingElevations();
        Assert.All(chains, c => Assert.False(c.Mismatch, string.Join("; ", c.Checks)));
        Assert.All(chains, c => Assert.Contains(c.Steps, s => s.Item == "− Bearing (by manufacturer)"));
        // Bottom of flange to seat = the assumed height everywhere.
        var f = doc.Framing();
        var steel = doc.Model().Steel!;
        foreach (var sl in f.Supports)
            foreach (var br in sl.Bearings.OfType<SBM.Core.Framing.BearingResult>())
            {
                var seat = steel.BearingSeats.First(x => x.Girder == br.Girder && Math.Abs(x.Station - br.Station) < 1e-6);
                Assert.Equal(In(9), br.BottomOfSteelElevation - seat.SeatElevation, 6);
            }
        // Pier anchors: 2 1/2" at 44" x 18"; the masonry plate edge is 5" at fixed girders and 3" at the MD girder.
        var anchors = steel.Bolts.Where(x => x.Kind == SBM.Core.Steel.BoltKind.Anchor && x.Assembly.Contains(f.Supports[1].Name.Replace(" ", ""))).ToList();
        Assert.All(anchors, a => Assert.Equal(In(2.5), a.Diameter, 9));
        Assert.Equal(b.Girders.Count * 4, anchors.Count);
        var seats = steel.BearingSeats.Where(x => x.Support == f.Supports[1].Name).OrderBy(x => x.Girder).ToList();
        Assert.Equal(In(44 + 6), seats[0].FootprintWidth, 9);
        Assert.Equal(In(44 + 10), seats[1].FootprintWidth, 9);
    }

    [Fact]
    public void Steps_AreARunningSum()
    {
        var c = SbmDocument.Sample().BearingElevations()[0];
        for (int i = 1; i < c.Steps.Count; i++)
        {
            var s = c.Steps[i];
            if (double.IsNaN(s.Change)) continue;
            // Previous running elevation = last line that changed it (info lines are skipped).
            var prev = c.Steps.Take(i).Last(p => !p.Item.StartsWith("(info)"));
            Assert.Equal(prev.Elevation + s.Change, s.Elevation, 9);
        }
        var b = SbmDocument.Sample().Bridge();
        Assert.Contains(c.Steps, s => s.Item == "− Haunch" && Math.Abs(s.Change + b.Deck.Haunch) < 1e-12);
        Assert.Contains(c.Steps, s => s.Item == "− Web depth" && Math.Abs(s.Change + b.Girders.Section.WebDepth) < 1e-12);
    }

    [Fact]
    public void GirderAtStation_MatchesGirderPath()
    {
        var doc = SbmDocument.Sample();
        var u = doc.Project.Units;
        Assert.True(u.TryParseStation("22+00", out double sta));
        for (int g = 0; g < 5; g++)
        {
            var c = doc.GirderElevations(g, sta);
            Assert.False(c.Mismatch, string.Join("; ", c.Checks));
            Assert.Contains("Top of web matches", c.Checks[0]);
        }
    }

    [Fact]
    public void ProfileExplain_EqualsElevationAt()
    {
        var a = SbmDocument.Sample().Project.Alignments[0];
        for (double sta = a.StartStation; sta < a.EndStation; sta += Ft(37))
        {
            var (z, how) = a.Profile.Explain(sta, x => x.ToString(), x => x.ToString(), x => x.ToString());
            Assert.Equal(a.Profile.ElevationAt(sta), z, 9);
            Assert.False(string.IsNullOrEmpty(how));
        }
    }
}
