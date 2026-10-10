using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Prestressed;
using SBM.IO.PgSuper;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class PgSuperImportTests
{
    private static string Data(string f) => Path.Combine(AppContext.BaseDirectory, "Data", f);

    [Fact]
    public void Reader_ReadsWf58gBridge()
    {
        var pg = PgSuperReader.Read(Data("PGSuper_WF58G.pgs"));
        Assert.Equal("WF58G", pg.GirderName);
        Assert.Equal(2, pg.Piers.Count);
        Assert.Equal(Ft(120), pg.Piers[1].Station - pg.Piers[0].Station, 4);
        Assert.Equal(4, pg.GirderCount);
        Assert.NotNull(pg.Dims);
        Assert.Equal(In(58), pg.Dims!.H, 6);
        var g = pg.Girders[1];
        Assert.Equal(28, g.NumStraight);
        Assert.Equal(13, g.NumHarped);
        Assert.Equal(In(0.6), g.StrandDiameter, 6);
    }

    [Fact]
    public void Wf58gSection_MatchesPgSuperGeometry()
    {
        var pg = PgSuperReader.Read(Data("PGSuper_WF58G.pgs"));
        var s = TxGirderShape.FromDims(pg.Dims!);
        Assert.Equal(49, s.TopWidth / In(1), 3);
        Assert.Equal(38.375, s.BottomWidth / In(1), 3);
        // Hand sum of the WBFL precast I-beam polygon (flange, taper, fillets, web, bulb, chamfers).
        Assert.InRange(s.Area, 825.0, 826.0);
    }

    [Fact]
    public void Import_FillsStrandsLikePgSuper_AndRegenerates()
    {
        var doc = SbmDocument.Sample();
        doc.Project.Bridges.Clear();
        var res = doc.ImportPgSuper(Data("PGSuper_WF58G.pgs"), "CL Route 1", doc.Station("25+00"));
        var ps = res.Bridge.Girders.Prestressed;
        Assert.Equal(TxGirderType.Custom, ps.Type);
        Assert.Equal(41, ps.StrandLayout!.Count);
        Assert.Equal(13, ps.StrandLayout.Count(s => s.Harped));
        // Harped offsets: top strand 4" below the top at the ends, bottom harped strand 4" above the bottom at the hold-downs.
        var h = ps.StrandLayout.Where(s => s.Harped).ToList();
        Assert.Equal(In(58 - 4), h.Max(s => s.YEnd), 4);
        Assert.Equal(In(4), h.Min(s => s.Y), 4);
        // "A" 11 1/2" − slab 7 1/2" = 4" haunch.
        Assert.Equal(In(4), res.Bridge.Deck.Haunch, 4);
        Assert.Equal(4, res.Bridge.Girders.Count);

        var r = doc.Regenerate();
        Assert.Empty(r.Errors);
        var m = doc.Model(res.Bridge.Name).Prestressed!;
        Assert.Equal(4, m.Beams.Count);
        Assert.All(m.Beams, b => Assert.Equal(41, b.Strands.Count));
        // Span: 120 ft pier to pier; CL bearing to CL bearing = 120 ft − two bearing offsets.
        var chains = doc.BearingElevations(res.Bridge.Name);
        Assert.Equal(8, chains.Count);
        Assert.All(chains, c => Assert.False(c.Mismatch, c.Title + ": " + string.Join("; ", c.Checks)));
        var pier = PgSuperReader.Read(Data("PGSuper_WF58G.pgs")).Piers;
        // The sample alignment is curved here: girders straddle the CL symmetrically, so their mean span ≈ the CL span.
        Assert.InRange(m.Beams.Average(b => b.BearingSpan), Ft(120) - pier[0].AheadBearingOffset - pier[1].BackBearingOffset - 0.03,
            Ft(120) - pier[0].AheadBearingOffset - pier[1].BackBearingOffset + 0.03);
    }

    [Fact]
    public void Import_TxDotGirderMapsToTxShape_WithEndDistancesFromThePier()
    {
        var doc = SbmDocument.Sample();
        doc.Project.Bridges.Clear();
        var res = doc.ImportPgSuper(Data("PGSuper_Tx54.pgs"), "CL Route 1", doc.Station("25+00"));
        var ps = res.Bridge.Girders.Prestressed;
        Assert.Equal(TxGirderType.Tx54, ps.Type);
        // PGSuper measures 3" from the pier line to the girder end, CL bearing 12" from the pier → 9" bearing to end.
        Assert.Equal(In(9), ps.BearingToEnd, 4);
        Assert.Empty(ps.StrandLayout!); // undesigned model
        Assert.Empty(doc.Regenerate().Errors);
    }

    [Fact]
    public void SkewStrings_Parse()
    {
        Assert.Equal(0, PgSuperReader.ParseSkew("NORMAL"));
        Assert.Equal(15.5, PgSuperReader.ParseSkew("15 30 00.0 L"), 6);
        Assert.Equal(-20, PgSuperReader.ParseSkew("20 0 0 R"), 6);
        Assert.True(double.IsNaN(PgSuperReader.ParseSkew("N 45 00 00 E")));
    }

    [Fact]
    public void IgebPads_FollowTheTable()
    {
        Assert.Equal(In(8), TxGirderCatalog.IgebPad(TxGirderType.Tx54, false, 40).Length, 6);
        Assert.Equal(In(9), TxGirderCatalog.IgebPad(TxGirderType.Tx70, false, 10).Length, 6);
        var p = TxGirderCatalog.IgebPad(TxGirderType.Tx46, true, 35);
        Assert.Equal((In(9), In(4.5), In(4.5)), (p.Length, p.ClipA, p.ClipB));
        Assert.True(TxGirderCatalog.IgebPad(TxGirderType.Tx40, true, 50).Round);
    }
}
