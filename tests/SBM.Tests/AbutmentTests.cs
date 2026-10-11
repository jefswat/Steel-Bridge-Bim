using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Substructure;
using Xunit;

namespace SBM.Tests;

public class AbutmentTests
{
    private static double Ft(double x) => x * 0.3048;
    private static double In(double x) => x * 0.0254;

    private static (SbmDocument doc, AbutmentDefinition ad) Setup()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        var sup = b.Supports[0];
        var u0 = doc.Substructure().Units.First(u => u.SupportIndex == 0);
        var al = doc.Alignment();
        double Off(double s) => al.Inverse(u0.Plan(s, 0)).Offset;
        double sMin = u0.Pedestals.Min(p => p.S) - Ft(3), sMax = u0.Pedestals.Max(p => p.S) + Ft(3);
        double zSeat = u0.Pedestals.Min(p => p.SeatZ);
        var ad = new AbutmentDefinition
        {
            LeftEndOffset = Off(sMin), RightEndOffset = Off(sMax), PlanFootingWidth = In(80.5), BackwallFrontToBearing = In(22),
        };
        double zf = zSeat - Ft(5);
        ad.FootingBottom.Add(new CapProfilePoint(0, zf));
        ad.BackwallTop.Add(new CapProfilePoint(sMin, zf + Ft(11))); ad.BackwallTop.Add(new CapProfilePoint(sMax, zf + Ft(11.5)));
        foreach (var p in u0.Pedestals) ad.Steps.Add(new AbutmentStepDefinition(p.Girder + 1, p.SeatZ));
        double nc = (-(In(22) + Ft(1.5) + In(1.5)) + ad.BearingToFootingFront) / 2;
        for (int i = 0; i < 5; i++) ad.PileStations.Add(sMin + Ft(4) + Ft(6.8) * i);
        var wd = new WingwallDefinition { Name = "L", End = AbutmentEnd.Left, GutterOffset = Off(sMin) + Ft(3), Length = Ft(20), TopAtBackwall = zf + Ft(11), TopAtEnd = zf + Ft(10), BottomAtBackwall = zf + Ft(1), LevelReach = Ft(1) };
        wd.Counterforts.Add(new CounterfortDefinition { Start = Ft(8), Height = Ft(6), FootingTop = zf + Ft(3) });
        ad.Wings.Add(wd);
        _ = nc; _ = sup;
        return (doc, ad);
    }

    [Fact]
    public void AsDesignedAbutment_BuildsFootingBackwallStepsPilesAndWings()
    {
        var (doc, ad) = Setup();
        var sd = doc.Bridge().Supports[0].ResolvedSubstructure.Clone();
        sd.Abutment = ad; sd.PileSection = "HP14x73";
        doc.Bridge().Supports[0].Substructure = sd;
        doc.Regenerate();
        var u = doc.Substructure().Units.First(x => x.SupportIndex == 0);
        Assert.NotNull(u.Abutment);
        var kinds = u.Solids.Select(s => s.Kind).ToHashSet();
        foreach (var k in new[] { SubElementKind.Footing, SubElementKind.Backwall, SubElementKind.Pedestal, SubElementKind.Pile, SubElementKind.Wingwall, SubElementKind.Counterfort, SubElementKind.WingFooting })
            Assert.Contains(k, kinds);
        Assert.Equal(10 + 2, u.Solids.Count(s => s.Kind == SubElementKind.Pile));
        // footing volume = 6'-8 1/2 wide section x length x 4' (+ wash in front of the backwall) within a few percent
        var foot = u.Solids.Single(s => s.Kind == SubElementKind.Footing);
        double len = u.CapS1 - u.CapS0;
        double expect = len * (In(80.5) * Ft(4) + (In(80.5) - In(41.5 + 0) - In(22 + 18 + 1.5 - 41.5)) * 0 + (Ft(3.25) + In(22)) * In(1));
        Assert.InRange(Math.Abs(foot.Mesh.Volume()) / expect, 0.95, 1.06);
        Assert.Equal(u.Pedestals.Count, u.Solids.Count(s => s.Kind == SubElementKind.Pedestal));
    }

    [Fact]
    public void AsDesignedAbutment_RebarMarksCarryPlanNames_AndMismatchesAreReported()
    {
        var (doc, ad) = Setup();
        ad.Rebar.FootingLongMark = "9a1"; ad.Rebar.BackFaceBarLength = Ft(13) + In(1); ad.Rebar.FrontFaceBarLength = Ft(12.75);
        ad.Rebar.PlanBarList.Add(new PlanBarLine("5g3", 4, Ft(42.6), 178));
        ad.Rebar.PlanBarList.Add(new PlanBarLine("6d1", 999, Ft(13), 0));
        var sd = doc.Bridge().Supports[0].ResolvedSubstructure.Clone();
        sd.Abutment = ad;
        doc.Bridge().Supports[0].Substructure = sd;
        doc.Regenerate();
        var m = doc.Substructure();
        var u = m.Units.First(x => x.SupportIndex == 0);
        Assert.Contains(m.Marks, k => k.Unit == u.Name && k.Mark == "9a1" && k.Count == 40);
        Assert.Contains(m.Marks, k => k.Unit == u.Name && k.Mark == "5b1");
        Assert.Contains(m.Marks, k => k.Unit == u.Name && k.Mark == "5g3" && k.Count == 4 && k.Location.Contains("schedule only"));
        Assert.Contains(m.Diagnostics, d => d.Message.Contains("6d1") && d.Message.Contains("999"));
        Assert.Contains(m.Diagnostics, d => d.Message.Contains("UNRESOLVED") && d.Message.Contains("8j3"));
        Assert.DoesNotContain(m.Bars, b => b.Unit == u.Name && b.Mark == "5g3");
    }

    [Fact]
    public void AsDesignedAbutment_RoundTripsThroughJson_AndExportsIfc()
    {
        var (doc, ad) = Setup();
        var sd = doc.Bridge().Supports[0].ResolvedSubstructure.Clone();
        sd.Abutment = ad;
        doc.Bridge().Supports[0].Substructure = sd;
        var copy = sd.Clone();
        Assert.Equal(ad.Steps.Count, copy.Abutment!.Steps.Count);
        Assert.Equal(ad.Wings[0].Counterforts[0].FootingTop, copy.Abutment.Wings[0].Counterforts[0].FootingTop);
        var json = doc.Project.ToJson();
        var back = SBM.Core.Project.BridgeProject.FromJson(json);
        Assert.NotNull(back.Bridges[0].Supports[0].Substructure!.Abutment);
        doc.Regenerate();
        string path = Path.Combine(Path.GetTempPath(), "abut-test-" + Guid.NewGuid().ToString("N") + ".ifc");
        try
        {
            doc.ExportIfc(path);
            string text = File.ReadAllText(path);
            Assert.Contains("'WINGWALL'", text);
            Assert.Contains("'COUNTERFORT'", text);
            Assert.Contains("IFCFOOTING", text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void DefaultAbutment_IsUnchanged_WhenNoAbutmentDefinition()
    {
        var doc = SbmDocument.Sample();
        var u = doc.Substructure().Units.First(x => x.Type == SupportType.Abutment);
        Assert.Null(u.Abutment);
        Assert.Contains(u.Solids, s => s.Kind == SubElementKind.Backwall);
    }
}
