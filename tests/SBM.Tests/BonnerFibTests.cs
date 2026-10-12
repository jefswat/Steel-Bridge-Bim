using System.Text.Json;
using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using SBM.Core.Substructure;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>FIB-96 catalog shape (NCDOT B-2500 p.438), per-end bearings with tapered sole plates, per-strand diameters, and the
/// Bonner Unit 6 sample model (build-bonner-unit6.csx).</summary>
public class BonnerFibTests
{
    private static string? RepoFile(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "SteelBridgeModeler.sln"))) return Path.Combine(d.FullName, rel);
        return null;
    }

    [Fact]
    public void Fib96_Outline_IsThePlanDimensions()
    {
        var s = StandardBeamCatalog.Find("FL FIB-96");
        Assert.NotNull(s);
        Assert.Same(s, StandardBeamCatalog.Find("FIB-96"));
        Assert.Same(s, StandardBeamCatalog.Find("Florida FIB-96"));
        Assert.True(s!.IsStateStandard);
        Assert.Equal(96, s.Depth / In(1), 6);
        Assert.Equal(48, s.TopWidth / In(1), 6);
        Assert.Equal(38, s.BottomWidth / In(1), 6);
        Assert.Equal(48, s.Profile!.Width / In(1), 6);
        Assert.Equal(96, s.Profile.Height / In(1), 6);
        // exact area (in²): 1,226.6 = 8.52 ft² vs 49.9 CY / 158'-3 1/2" = 8.51 ft² (p428 quantity)
        Assert.Equal(1226.6, s.Area, 0);
        Assert.True(Math.Abs(s.Area / 144 - 49.9 * 27 / (158 + 3.5 / 12)) < 0.01);
        // R = 1'-3" web-to-bulb fillets with the tangent points of the vector drawing (web y 23.90", slope (11.97", 10.40"))
        var arcs = s.Profile.Segments.Where(g => g.IsArc).ToList();
        Assert.Equal(2, arcs.Count);
        foreach (var a in arcs)
        {
            Assert.Equal(15, a.Radius / In(1), 6);
            var pts = new[] { a.Start / In(1), a.End / In(1) }.OrderBy(p => p.Y).ToArray();
            Assert.True(Math.Abs(Math.Abs(pts[0].X) - 11.97) < 0.01 && Math.Abs(pts[0].Y - 10.40) < 0.01, $"{pts[0]}");
            Assert.Equal(3.5, Math.Abs(pts[1].X), 6);
            Assert.True(Math.Abs(pts[1].Y - 23.90) < 0.01, $"{pts[1]}");
        }
        // the polygon used for meshes stays within the outline and closed
        var o = s.Outline();
        double polyArea = Math.Abs(MeshArea(o)) / (In(1) * In(1));
        Assert.True(Math.Abs(polyArea - s.Area) < 0.005 * s.Area, $"{polyArea}");
    }

    private static double MeshArea(Vec2[] p)
    {
        double a = 0;
        for (int i = 0; i < p.Length; i++) a += Vec2.Cross(p[i], p[(i + 1) % p.Length]);
        return a / 2;
    }

    [Fact]
    public void SolePlate_ThicknessAtBearing_IsTheLinearTaper()
    {
        var sp = new SolePlateDefinition { Length = In(19.75), ThicknessBack = In(2.125), ThicknessAhead = In(1.25), BearingFromBack = In(12.75) };
        Assert.Equal(2.125 - 0.875 * 12.75 / 19.75, sp.ThicknessAtBearing / In(1), 9);
        sp.BearingFromBack = double.NaN;
        Assert.Equal((2.125 + 1.25) / 2, sp.ThicknessAtBearing / In(1), 9);
    }

    [Fact]
    public void NewOptionalInputs_AreOmittedFromTheFile_WhenUnused()
    {
        var d = new PrestressedBeamDefinition { StrandLayout = new() { new StrandPoint { X = 0, Y = In(2), YEnd = In(2) } } };
        var o = new JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        string json = JsonSerializer.Serialize(d, o);
        foreach (var k in new[] { "StartBearing", "EndBearing", "StartBearingToEnd", "EndBearingToEnd", "CamberTable", "CamberSource", "PlanLength", "PlanConcreteVolume", "\"Diameter\"", "\"Label\"" })
            Assert.DoesNotContain(k, json);
        Assert.DoesNotContain("OnFootingOf", JsonSerializer.Serialize(new FootingDefinition(), o));
        Assert.DoesNotContain("Unresolved", JsonSerializer.Serialize(new SubstructureDefinition(), o));
    }

    [Fact]
    public void BonnerUnit6_Sample_BuildsThePlanCounts()
    {
        var path = RepoFile(Path.Combine("samples", "plans", "Bonner Unit 6 FIB.sbm"));
        if (path == null || !File.Exists(path)) return;
        var doc = SbmDocument.Open(path);
        doc.Regenerate();
        var ps = doc.Prestressed("Bonner Unit 6");
        Assert.Equal(24, ps.Beams.Count);
        Assert.All(ps.Beams, b =>
        {
            Assert.Equal(76, b.Strands.Count);                                            // 72 x 0.6" + 4 x 1/2" slack (p439)
            Assert.Equal(4, b.Strands.Count(s => Math.Abs(s.Diameter - In(0.5)) < 1e-9));
            Assert.Equal(8, b.Strands.Count(s => s.Depressed));
            Assert.Equal(20, b.Strands.Count(s => s.Debonded));
            Assert.Equal(21, b.Definition!.CamberTable!.Count);
            Assert.NotNull(b.Body);
        });
        Assert.Equal(48, ps.Pads.Count);
        Assert.All(ps.Pads, p => Assert.NotNull(p.SolePlate));
        Assert.Equal(new[] { "E1", "E2", "E3" }, ps.Pads.Select(p => p.BearingMark).Distinct().OrderBy(x => x));
        // girder concrete 49.9 CY (span 30) / 50.5 CY (spans 31-35) within 0.1 CY
        double cy = Math.Pow(Ft(1), 3) * 27;
        Assert.All(ps.Beams, b => Assert.True(Math.Abs(b.Volume / cy - (b.Span == 0 ? 49.9 : 50.5)) < 0.1, b.Mark));
        var sub = doc.Substructure("Bonner Unit 6")!;
        Assert.Equal(6, sub.Units.Count);                                                  // bents 30-35 (bent 29 is Unit 5's)
        Assert.Equal(14 + 14 + 12 + 6 + 8 + 6, sub.Units.Sum(u => u.Foundations.Sum(f => f.Piles.Count)));
        Assert.All(sub.Units, u =>
        {
            Assert.Equal(2, u.Columns.Count);
            Assert.Single(u.Solids, s => s.Kind == SubElementKind.Footing);              // one combined footing under both columns
            double cap = u.Solids.Where(s => s.Kind == SubElementKind.Cap).Sum(s => Math.Abs(s.Mesh.Volume())) / cy;
            Assert.Equal(55.6, cap, 1);
        });
        // bearing seats of bents 30-35 vs the printed pedestal tops (p483 / p486) within 0.01 ft
        var ped = new Dictionary<string, (double[] back, double[] ahead)>
        {
            ["Bent 30"] = (new[] { 65.35, 65.06, 64.78, 64.49 }, new[] { 65.29, 65.00, 64.71, 64.42 }),
            ["Bent 32"] = (new[] { 50.72, 50.44, 50.15, 49.86 }, new[] { 50.65, 50.37, 50.08, 49.79 }),
            ["Bent 35"] = (new[] { 33.76, 33.42, 33.08, 32.74 }, new double[0]),
        };
        var supports = doc.Bridge("Bonner Unit 6").Supports.Select(s => s.Name).ToList();
        foreach (var p in ps.Pads.Where(p => ped.ContainsKey(p.Support)))
        {
            bool back = p.Span < supports.IndexOf(p.Support);
            double plan = (back ? ped[p.Support].back : ped[p.Support].ahead)[p.Girder];
            Assert.True(Math.Abs((p.Center.Z - p.Thickness / 2) / Ft(1) - plan) <= 0.01, $"{p.Mark} {p.Support}");
        }
    }
}
