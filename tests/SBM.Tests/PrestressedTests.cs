using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using SBM.Core.Solids;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class PrestressedTests
{
    private static SbmDocument Sample(Action<PrestressedBeamDefinition>? edit = null)
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.Kind = GirderKind.TxDotPrestressed;
        edit?.Invoke(b.Girders.Prestressed);
        foreach (var s in b.Supports) s.Substructure = SubstructureDefinition.Default(s.Type);
        return doc;
    }

    [Theory]
    [InlineData(TxGirderType.Tx28)]
    [InlineData(TxGirderType.Tx34)]
    [InlineData(TxGirderType.Tx40)]
    [InlineData(TxGirderType.Tx46)]
    [InlineData(TxGirderType.Tx54)]
    [InlineData(TxGirderType.Tx62)]
    [InlineData(TxGirderType.Tx70)]
    public void SectionOutline_MatchesIgdAreaAndCentroid(TxGirderType t)
    {
        var s = TxGirderCatalog.Get(t);
        var o = s.Outline().Select(p => new Vec2(p.X / In(1), p.Y / In(1))).ToList();
        double a = MeshBuilder.SignedArea(o);
        Assert.True(a > 0, "outline must be counter-clockwise");
        Assert.InRange(a, s.Area - 0.5, s.Area + 0.5);
        Assert.Equal(s.DepthIn, o.Max(p => p.Y), 6);
        // Centroid height vs. the tabulated Yb.
        double cy = 0;
        for (int i = 0; i < o.Count; i++)
        {
            var p = o[i];
            var q = o[(i + 1) % o.Count];
            cy += (p.Y + q.Y) * (p.X * q.Y - q.X * p.Y);
        }
        cy /= 6 * a;
        Assert.InRange(cy, s.Yb - 0.05, s.Yb + 0.05);
        Assert.Equal(s.WebStraightIn, s.DepthIn - (s.TopWidthIn > 40 ? 24.5 : s.BottomEdgeIn > 8 ? 24 : 22), 6);
    }

    [Fact]
    public void StrandGrid_FollowsIgnd()
    {
        var tx28 = TxGirderCatalog.Get(TxGirderType.Tx28);
        Assert.Equal(new[] { 14, 14, 12, 8, 4, 2 }, tx28.RowCapacity().Take(6));
        Assert.Equal(In(24.5), tx28.HighestAPosition, 6);
        var tx54 = TxGirderCatalog.Get(TxGirderType.Tx54);
        Assert.Equal(In(50.5), tx54.HighestAPosition, 6); // IGND: 24 spaces at 2" above 2½"
        var pos = tx54.StrandPositions();
        // Fill order: row 2½" from A outward.
        Assert.Equal(new Vec2(-In(1), In(2.5)), pos[0]);
        Assert.Equal(new Vec2(In(1), In(2.5)), pos[1]);
        Assert.Equal(new Vec2(In(13), In(2.5)), pos[13]);
        Assert.Equal(In(4.5), pos[14].Y, 9);
        Assert.Equal(In(70 - 3.5), TxGirderCatalog.Get(TxGirderType.Tx70).HighestAPosition, 6);
    }

    [Fact]
    public void Regenerate_BuildsOneBeamPerSpanAndGirder_WithStrandsPadsAndSeats()
    {
        var doc = Sample(p => { p.Type = TxGirderType.Tx54; p.StrandCount = 40; p.DepressedStrands = 6; });
        var r = doc.Regenerate();
        Assert.Empty(r.Errors);
        var m = doc.Model();
        Assert.Null(m.Steel);
        var ps = m.Prestressed!;
        int nG = doc.Bridge().Girders.Count, nSpan = doc.Bridge().Supports.Count - 1;
        Assert.Equal(nG * nSpan, ps.Beams.Count);
        Assert.Equal(2 * nG * nSpan, ps.Pads.Count);
        Assert.Equal(2 * nG * nSpan, ps.BearingSeats.Count);
        Assert.Empty(m.Framing.CrossFrames);
        Assert.Empty(m.Framing.Stiffeners);
        foreach (var b in ps.Beams)
        {
            Assert.Equal(40, b.Strands.Count);
            Assert.Equal(6, b.Strands.Count(s => s.Depressed));
            // Volume ≈ area × length (skewed ends only shift volume between the ends).
            double expected = b.Shape.Area * In(1) * In(1) * b.Length;
            Assert.InRange(b.Volume, expected * 0.98, expected * 1.02);
            // Depressed strands rise to the top A position at the ends, keeping 2" spacing.
            var dep = b.Strands.Where(s => s.Depressed).OrderBy(s => s.Section.Y).ToList();
            Assert.Equal(b.Shape.HighestAPosition, dep[^1].EndHeight, 6);
            Assert.Equal(b.Shape.HighestAPosition - In(4), dep[0].EndHeight, 6);
        }
        // Interior bent: CL bearings 12" each side of the CL bent along the girder (IGD 3" + 9").
        var pierBeams = ps.Beams.Where(b => b.Girder == 2).ToList();
        var pierPoint = m.Framing.Supports[1].Bearings[2]!.TopOfSteel;
        double d0 = (pierBeams[0].BearingEnd.XY - pierPoint.XY).Length, d1 = (pierBeams[1].BearingStart.XY - pierPoint.XY).Length;
        Assert.Equal(In(12), d0, 3);
        Assert.Equal(In(12), d1, 3);
        // Substructure gets one pedestal per beam end at the pier.
        var pier = m.Substructure!.Units.Single(u => u.Type == SupportType.Pier);
        Assert.Equal(2 * nG, pier.Pedestals.Count);
    }

    [Fact]
    public void Haunch_IsMeasuredFromTopOfBeam()
    {
        var doc = Sample();
        doc.Regenerate();
        var m = doc.Model();
        var def = doc.Bridge();
        var a = m.Framing.Alignment;
        foreach (var b in m.Prestressed!.Beams)
        {
            var so = a.Inverse(m.Framing.Supports[b.Span].Bearings[b.Girder]!.TopOfSteel.XY);
            double deckBottom = a.ElevationAt(so.Station, so.Offset) - def.Deck.Thickness;
            Assert.Equal(def.Deck.Haunch, deckBottom - m.Framing.Supports[b.Span].Bearings[b.Girder]!.TopOfSteel.Z, 4);
            Assert.True(b.MaxHaunch >= b.MinHaunch);
        }
    }

    [Fact]
    public void Ifc_ExportsBeamsTendonsAndPads()
    {
        var doc = Sample(p => p.StrandCount = 20);
        string path = Path.Combine(Path.GetTempPath(), $"ps_{Guid.NewGuid():N}.ifc");
        try
        {
            doc.ExportIfc(path);
            string text = File.ReadAllText(path);
            int nBeams = doc.Model().Prestressed!.Beams.Count;
            Assert.Equal(nBeams, CountOf(text, "=IFCBEAM(") - CountOf(text, ".PIERCAP."));
            Assert.Equal(nBeams * 20, CountOf(text, "=IFCTENDON("));
            Assert.Contains(".STRAND.", text);
            Assert.Equal(nBeams * 2, CountOf(text, ".ELASTOMERIC."));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ElevationCheck_ReconcilesEveryBeamEnd()
    {
        var doc = Sample(p => p.DepressedStrands = 4);
        doc.Regenerate();
        var chains = doc.BearingElevations();
        var m = doc.Model();
        Assert.Equal(m.Prestressed!.Pads.Count, chains.Count);
        Assert.All(chains, c => Assert.False(c.Mismatch, c.Title + ": " + string.Join("; ", c.Checks)));
        Assert.All(chains, c => Assert.Contains(c.Steps, s => s.Result == "Top of beam"));
        Assert.All(chains, c => Assert.Contains(c.Steps, s => s.Result.StartsWith("Top of cap")));
        // Girder line at mid-span.
        var g = doc.GirderElevations(2, (m.Framing.Supports[0].Station + m.Framing.Supports[1].Station) / 2);
        Assert.False(g.Mismatch, string.Join("; ", g.Checks));
    }

    [Fact]
    public void Validate_RejectsTooManyStrands()
    {
        var p = new PrestressedBeamDefinition { Type = TxGirderType.Tx28, StrandCount = 200 };
        Assert.Contains(p.Validate(), e => e.Contains("at most"));
    }

    private static int CountOf(string text, string what)
    {
        int n = 0, i = 0;
        while ((i = text.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i += what.Length; }
        return n;
    }
}
