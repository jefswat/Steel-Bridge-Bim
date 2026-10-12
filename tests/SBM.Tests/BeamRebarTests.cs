using System.Text.RegularExpressions;
using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Prestressed beam mild reinforcing: spacing chains, end zones, standard counts / weights, skewed ends, IFC bars, Tx path unchanged.</summary>
public class BeamRebarTests
{
    /// <summary>Straight level test beam: s along X, x right = +Y... (any orthonormal map works), length L, optional skew of the start face.</summary>
    private static PrestressedBeamRebar.BeamFrame Frame(double lengthIn, double depthIn, double startSkewDeg = 0, double endSkewDeg = 0) =>
        new((s, x, y) => new Vec3(s, -x, y),
            (x, y) => x * Math.Tan(startSkewDeg * Math.PI / 180),
            (x, y) => In(lengthIn) + x * Math.Tan(endSkewDeg * Math.PI / 180), 1, In(depthIn));

    private static double Inch(double m) => m / In(1);

    [Fact]
    public void Chain_GivesTheStationsOfAPlanSpacingChain()
    {
        // Br02069 p16: 2" + 4 sps @ 2 1/2" + 9 sps @ 4" + 19 sps @ 9" + 27 sps @ 14" (+ 6" to CL span)
        var c = PrestressedBeamRebar.Chain(2, (4, 2.5), (9, 4), (19, 9), (27, 14));
        Assert.Equal(60, c.Count);
        Assert.Equal(new[] { 2, 4.5, 7, 9.5, 12, 16 }, c.Take(6));
        Assert.Equal(48, c[13], 9);
        Assert.Equal(57, c[14], 9);
        Assert.Equal(219, c[32], 9);
        Assert.Equal(597, c[^1], 9);
        Assert.Equal(603, c[^1] + 6, 9);
    }

    [Theory]
    [InlineData("BTD55", IowaBtdEdition.Issue2004Rev2009, 676)]
    [InlineData("BTD60", IowaBtdEdition.Issue2004Rev2009, 736)]
    [InlineData("BTD105", IowaBtdEdition.Issue2004Rev2009, 1276)]
    [InlineData("BTD55", IowaBtdEdition.Reissue2024, 676)]
    [InlineData("BTD60", IowaBtdEdition.Reissue2024, 736)]
    [InlineData("BTD105", IowaBtdEdition.Reissue2024, 1276)]
    public void IowaBtd_PlacedCounts_AreTheBarList_AndWeightIsThePrintedWeight(string designation, IowaBtdEdition ed, double overallIn)
    {
        var rd = StandardBeamRebar.IowaBtd(designation, ed)!;
        var placed = PrestressedBeamRebar.Place(rd, designation + " beams", Frame(overallIn, 54));
        Assert.Empty(placed.OutsideEnd);
        foreach (var row in rd.BarList!)
            Assert.True(row.Count == placed.Bars.Count(b => b.Mark == row.Mark), $"{designation} {ed} {row.Mark}: {placed.Bars.Count(b => b.Mark == row.Mark)} placed, {row.Count} listed");
        Assert.Equal(rd.BarList!.Sum(r => r.Count), placed.Bars.Count);
        // weight per beam from the listed lengths = the BTD Beam Data reinforcing weight (lb, rounded)
        double lb = rd.BarList!.Sum(r => r.Count * r.Length * SBM.Core.Concrete.RebarCatalog.Size("#" + r.Mark[0]).MassPerLength) * 2.2046226218;
        // (std 4730 re-issue 2024 BTD105: the data table's 2,795 lb is the bar list with 5a1 = 35'-6; the bar list prints 36'-6, which the
        //  3'-5 lap to 5a2 needs - the bar list governs, the 12 lb difference is reported as a note)
        double tol = designation == "BTD105" && ed == IowaBtdEdition.Reissue2024 ? 13 : 1;
        Assert.InRange(lb, rd.ListedWeightLb - tol, rd.ListedWeightLb + tol);
        // every placed bar lies within the beam length; projections only above the top (5b1 / 6b3 into the deck)
        foreach (var b in placed.Bars)
            Assert.All(b.Points, p => Assert.InRange(Inch(p.X), -1e-6, overallIn + 1e-6));
    }

    [Fact]
    public void IowaBtd55_StationsFollowTheChains_FromBothEnds_MidspanBarOnce()
    {
        var rd = StandardBeamRebar.IowaBtd("BTD55", IowaBtdEdition.Issue2004Rev2009)!;
        var placed = PrestressedBeamRebar.Place(rd, "BTD55 beams", Frame(676, 54));
        // 5b1 chain (p.28): 59 + 3 @ 9 + 4 @ 1'-0 + 4 @ 1'-3 + 8 @ 1'-6 to CL (338) - legs at the station; CL bar placed once
        var b1 = PrestressedBeamRebar.Chain(59, (3, 9), (4, 12), (4, 15), (8, 18));
        var exp = b1.Concat(b1.Where(v => Math.Abs(v - 338) > 1e-6).Select(v => 676 - v)).OrderBy(v => v).ToList();
        var mod = placed.Bars.Where(b => b.Mark == "5b1").Select(b => Inch(b.Points.Where(p => p.Z > In(20)).Average(p => p.X))).OrderBy(v => v).ToList();
        Assert.Equal(39, mod.Count);
        for (int i = 0; i < exp.Count; i++) Assert.Equal(exp[i], mod[i], 6);
        // feet point toward midspan at both ends
        foreach (var b in placed.Bars.Where(b => b.Mark == "5b1"))
        {
            double station = Inch(b.Points.Where(p => p.Z > In(20)).Average(p => p.X));
            double foot = Inch(b.Points[0].X);
            if (station < 337) Assert.True(foot > station); else if (station > 339) Assert.True(foot < station);
        }
        // end zone: 6b3 pairs at 6, 15, 21 ... 51 from each end; 6b4 pairs at 1 1/2 and 10 1/2; 4h1 three levels 1'-2 + 2 @ 6 at each end
        var b3 = placed.Bars.Where(b => b.Mark == "6b3").Select(b => Inch(b.Points[0].X)).Where(v => v < 338).Distinct().OrderBy(v => v).ToList();
        Assert.Equal(new[] { 6.0, 15, 21, 27, 33, 39, 45, 51 }, b3.Select(v => Math.Round(v, 6)));
        Assert.Equal(16, placed.Bars.Count(b => b.Mark == "6b3" && Inch(b.Points[0].X) < 338));
        var b4 = placed.Bars.Where(b => b.Mark == "6b4").Select(b => Inch(b.Points[0].X)).Where(v => v < 338).Distinct().OrderBy(v => v).ToList();
        Assert.Equal(new[] { 1.5, 10.5 }, b4.Select(v => Math.Round(v, 6)));
        Assert.Equal(new[] { 14.0, 20, 26 }, placed.Bars.Where(b => b.Mark == "4h1" && b.Points[0].X < In(338)).Select(b => Math.Round(Inch(b.Points[0].Z), 6)).OrderBy(v => v));
        // 5a1: 1 1/2" clear to the ends, lapped across midspan (6 lines x 2)
        var a1 = placed.Bars.Where(b => b.Mark == "5a1").ToList();
        Assert.Equal(12, a1.Count);
        Assert.Equal(1.5, a1.Min(b => Inch(b.Points.Min(p => p.X))), 6);
        Assert.Equal(676 - 1.5, a1.Max(b => Inch(b.Points.Max(p => p.X))), 6);
    }

    [Fact]
    public void SkewedEnd_TransverseBarsMeasuredAlongTheCl_AreReported_LongitudinalBarsKeepTheirClear()
    {
        var rd = StandardBeamRebar.IowaBtd("BTD55", IowaBtdEdition.Issue2004Rev2009)!;
        var square = PrestressedBeamRebar.Place(rd, "u", Frame(676, 54));
        var skew = PrestressedBeamRebar.Place(rd, "u", Frame(676, 54, 15, 15));
        Assert.Empty(square.OutsideEnd);
        Assert.Contains("4c1", skew.OutsideEnd.Keys);
        Assert.Contains("4e1", skew.OutsideEnd.Keys);
        Assert.DoesNotContain("5a1", skew.OutsideEnd.Keys);
        // 4c1 at 1 1/2" (+9/16") from the end at the CL; the flange tip at 15.5" is 15.5 tan 15 = 4.15" further on the acute side
        Assert.InRange(Inch(skew.OutsideEnd["4c1"].Max), 15.5 * Math.Tan(Math.PI / 12) - 2.0625 - 0.01, 15.5 * Math.Tan(Math.PI / 12) - 2.0625 + 0.01);
        // same number of bars; transverse bars stay square to the beam (both ends of a 4c1 at the same s)
        Assert.Equal(square.Bars.Count, skew.Bars.Count);
        Assert.All(skew.Bars.Where(b => b.Mark == "4c1"), b => Assert.Equal(b.Points[0].X, b.Points[^1].X, 9));
        // 5a1 keeps 1 1/2" clear to the skewed face at its own line
        foreach (var b in skew.Bars.Where(b => b.Mark == "5a1" && b.Points[0].X < In(338)))
        {
            double x = -b.Points[0].Y;
            Assert.Equal(1.5, Inch(b.Points[0].X - x * Math.Tan(Math.PI / 12)), 6);
        }
    }

    [Fact]
    public void MnDot30Mh_Br02069Chain_SplittingBars_Hairpins_AndUnresolvedSets()
    {
        var chain = PrestressedBeamRebar.Chain(2, (4, 2.5), (9, 4), (19, 9), (27, 14));
        var rd = StandardBeamRebar.MnDot30Mh(chain, 1, 4, "test", "p16");
        var placed = PrestressedBeamRebar.Place(rd, "30MH beams", Frame(1206, 30));
        Assert.Equal(110, placed.Bars.Count(b => b.Mark == "G402E"));
        Assert.Equal(4, placed.Bars.Count(b => b.Mark == "G505E"));
        Assert.Equal(16, placed.Bars.Count(b => b.Mark == "G508E"));
        Assert.Equal(120, placed.Bars.Count(b => b.Mark == "G301E"));
        Assert.Equal(20, placed.Bars.Count(b => b.Mark == "G303E"));
        Assert.Equal(0, placed.Bars.Count(b => b.Mark == "G806E"));
        Assert.Equal(2, rd.Bars.Count(b => b.Unresolved.StartsWith("UNRESOLVED (p16)")));
        // G505E legs run into the beam from both ends (top leg 3'-6 o-o), vertical 2" from the end
        foreach (var b in placed.Bars.Where(b => b.Mark == "G505E"))
        {
            double v = Inch(b.Points[1].X), tip = Inch(b.Points[0].X);
            Assert.True(Math.Abs(v - 2) < 1e-6 || Math.Abs(v - 1204) < 1e-6);
            Assert.Equal(41.6875, Math.Abs(tip - v), 6);
            Assert.True(v < 600 ? tip > v : tip < v);
        }
        // G402E hairpin: 4" o-o (legs 3 1/2" apart on centers), 7" above the 30" beam
        var h = placed.Bars.First(b => b.Mark == "G402E");
        Assert.Equal(37 - 0.25, Inch(h.Points.Max(p => p.Z)), 6);
        Assert.Equal(3.5, Inch(h.Points.Max(p => p.Y) - h.Points.Min(p => p.Y)), 6);
        // G301E 1/2" against the stirrup: stations = chain + 1/2" from each end
        var g301 = placed.Bars.Where(b => b.Mark == "G301E").Select(b => Inch(b.Points[0].X)).OrderBy(v => v).ToList();
        var exp = chain.Select(v => v + 0.5).Concat(chain.Select(v => 1206 - v - 0.5)).OrderBy(v => v).ToList();
        for (int i = 0; i < exp.Count; i++) Assert.Equal(exp[i], g301[i], 6);
    }

    private static SbmDocument PsSample(BeamReinforcementDefinition? rd)
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.Kind = GirderKind.TxDotPrestressed;
        b.Girders.Prestressed.Reinforcement = rd;
        foreach (var s in b.Supports) s.Substructure = SubstructureDefinition.Default(s.Type);
        return doc;
    }

    private static BeamReinforcementDefinition MhBars() =>
        StandardBeamRebar.MnDot30Mh(PrestressedBeamRebar.Chain(2, (4, 2.5), (9, 4), (19, 9), (27, 14)), 1, 4, "test", "p16");

    [Fact]
    public void TxPath_Unchanged_WithoutReinforcement()
    {
        var doc = PsSample(null);
        Assert.Empty(doc.Regenerate().Errors);
        var ps = doc.Model().Prestressed!;
        Assert.Empty(ps.BarMarks);
        Assert.All(ps.Beams, b => Assert.Empty(b.Bars));
        Assert.DoesNotContain("\"Reinforcement\"", System.Text.Json.JsonSerializer.Serialize(doc.Bridge().Girders.Prestressed));
        Assert.DoesNotContain(ps.Diagnostics, d => d.Message.Contains("Beam reinforcing"));
        // IFC identical to a document where reinforcing was set and removed again
        var doc2 = PsSample(MhBars());
        doc2.Regenerate();
        doc2.Bridge().Girders.Prestressed.Reinforcement = null;
        doc2.Regenerate();
        string p1 = Path.Combine(Path.GetTempPath(), $"sbm-br1-{Guid.NewGuid():N}.ifc"), p2 = Path.Combine(Path.GetTempPath(), $"sbm-br2-{Guid.NewGuid():N}.ifc");
        try
        {
            doc.ExportIfc(p1);
            doc2.ExportIfc(p2);
            string Body(string p) => string.Join("\n", File.ReadAllLines(p).Where(l => !l.StartsWith("FILE_NAME")));
            Assert.Equal(Body(p1), Body(p2));
            Assert.DoesNotContain("'Bar G402E'", File.ReadAllText(p1));
        }
        finally { File.Delete(p1); File.Delete(p2); }
    }

    [Fact]
    public void Pipeline_BarsReachSchedule_Ifc_Database_AndRoundTripJson()
    {
        var doc = PsSample(MhBars());
        Assert.Empty(doc.Regenerate().Errors);
        var ps = doc.Model().Prestressed!;
        int perBeam = 270, beams = ps.Beams.Count;
        Assert.All(ps.Beams, b => Assert.Equal(perBeam, b.Bars.Count));
        Assert.Equal(perBeam * beams, ps.BarMarks.Sum(m => m.Count));
        Assert.All(ps.BarMarks, m => Assert.EndsWith(" beams", m.Unit));
        Assert.Contains(ps.Diagnostics, d => d.Message.StartsWith("UNRESOLVED (p16): 4-G806E"));
        Assert.True(ps.RebarMass > 0);
        // JSON round trip keeps the definition
        var back = SBM.Core.Project.BridgeProject.FromJson(doc.Project.ToJson());
        var rd = back.Bridges[0].Girders.Prestressed.Reinforcement!;
        Assert.Equal(MhBars().Bars.Count, rd.Bars.Count);
        Assert.Equal(MhBars().Bars[0].Path.Count, rd.Bars[0].Path.Count);
        Assert.Equal(MhBars().Bars[0].Stations, rd.Bars[0].Stations);
        // IFC: one IfcReinforcingBar per placed bar, hairpins / stirrups are LIGATURE, aggregated into the girder assemblies
        string dir = Path.Combine(Path.GetTempPath(), "sbm-beambars-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string ifc = Path.Combine(dir, "m.ifc");
            doc.ExportIfc(ifc);
            string text = File.ReadAllText(ifc);
            var bars = Regex.Matches(text, @"IFCREINFORCINGBAR\('[^']+',\$,'Bar (G\d{3}E)',.*?,(\.[A-Z]+\.),\.TEXTURED\.\);").Cast<Match>().ToList();
            Assert.Equal(perBeam * beams, bars.Count);
            Assert.All(bars.Where(m => m.Groups[1].Value == "G402E"), m => Assert.Equal(".LIGATURE.", m.Groups[2].Value));
            Assert.All(bars.Where(m => m.Groups[1].Value == "G301E"), m => Assert.Equal(".MAIN.", m.Groups[2].Value));
            // database rows carry the same GlobalIds as the IFC bars
            string db = Path.Combine(dir, "m.sqlite");
            doc.ExportDatabase(db);
            var gids = Regex.Matches(text, @"IFCREINFORCINGBAR\('([^']{22})'").Select(m => m.Groups[1].Value).ToHashSet();
            using var cn = SBM.IO.Database.ModelDatabase.Open(db);
            using var c = cn.CreateCommand();
            c.CommandText = "SELECT global_id FROM elements WHERE category = 'rebar'";
            var inDb = new HashSet<string>();
            using (var r = c.ExecuteReader()) while (r.Read()) inDb.Add(r.GetString(0));
            Assert.True(gids.IsSubsetOf(inDb));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
