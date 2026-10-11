using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Drawing.Sheets;
using SBM.Core.Drawing.Views;
using SBM.Core.Substructure;
using Xunit;

namespace SBM.Tests;

/// <summary>Integral abutment (AbutmentKind.Integral, concrete-bridge design feature A / T1) on the sample bridge with the Woodbury p.17 step-diagram values.</summary>
public class IntegralAbutmentTests
{
    private static double Ft(double x) => x * 0.3048;
    private static double In(double x) => x * 0.0254;

    // Woodbury S abutment (p.17): step D 1117.50, H 1118.31; bottom 1113.71 (west) -> 1114.60, level for the last 3'-0 at the east end.
    private const double SD = 1117.50, SBotW = 1113.71, SH = 1118.31, SBotE = 1114.60;

    private sealed record Ctx(SbmDocument Doc, AbutmentDefinition Ad, double SMin, double SMax, double Dz, List<PedestalInfo> Peds, Func<double, double> Off);

    private static Ctx Setup(double dTop = SD, double dBot = SBotW, double hTop = SH, double hBot = SBotE)
    {
        var doc = SbmDocument.Sample();
        var u0 = doc.Substructure().Units.First(u => u.SupportIndex == 0);
        var al = doc.Alignment();
        double Off(double s) => al.Inverse(u0.Plan(s, 0)).Offset;
        var peds = u0.Pedestals.OrderBy(p => p.S).ToList();
        double sMin = peds[0].S - Ft(3), sMax = peds[^1].S + Ft(3);
        double dz = Ft(Math.Round((peds[0].SeatZ / 0.3048 - dTop) * 100) / 100);   // plan elevations shifted onto the sample (differences kept)
        var ad = new AbutmentDefinition
        {
            Kind = AbutmentKind.Integral, Source = "test (Woodbury p.17 values)", LeftEndOffset = Off(sMin), RightEndOffset = Off(sMax),
            CapBackToBearing = Ft(1.5), CapFrontToBearing = Ft(1.5), PileRowOffsets = { 0 }, StepRisersAtMidBay = true, PileTopAboveFootingBottom = Ft(2),
        };
        int n = peds.Count;
        for (int i = 0; i < n; i++)
        {
            double top = i == 0 ? dTop : i == n - 1 ? hTop : dTop + (hTop - dTop) * i / (n - 1);
            ad.Steps.Add(new AbutmentStepDefinition(peds[i].Girder + 1, Ft(top) + dz));
        }
        ad.FootingBottom.Add(new CapProfilePoint(sMin, Ft(dBot) + dz));
        ad.FootingBottom.Add(new CapProfilePoint(sMax - Ft(3), Ft(hBot) + dz));
        ad.FootingBottom.Add(new CapProfilePoint(sMax, Ft(hBot) + dz));
        for (int i = 0; i < 4; i++) ad.PileStations.Add(sMin + Ft(2.5) + (sMax - sMin - Ft(5)) * i / 3);
        return new Ctx(doc, ad, sMin, sMax, dz, peds, Off);
    }

    private static (SubstructureModel m, SubstructureUnit u) Run(Ctx c)
    {
        var sd = c.Doc.Bridge().Supports[0].ResolvedSubstructure.Clone();
        sd.Abutment = c.Ad; sd.PileSection = "HP12x84"; sd.PileType = PileType.SteelHP;
        c.Doc.Bridge().Supports[0].Substructure = sd;
        c.Doc.Regenerate();
        var m = c.Doc.Substructure();
        return (m, m.Units.First(x => x.SupportIndex == 0));
    }

    [Theory]
    // p.17 step diagram: S abutment 3'-9 1/2 (step D) / 3'-8 1/2 (step H); N abutment 3'-11 3/4 / 3'-10 1/2 (N: D 1117.89, bottom 1113.91; H 1119.51, bottom 1115.64)
    [InlineData(1117.50, 1113.71, 1118.31, 1114.60, 45.5, 44.5)]
    [InlineData(1117.89, 1113.91, 1119.51, 1115.64, 47.75, 46.5)]
    public void StepDiagramDepths_AreReproducedFromTheStepAndBottomElevations(double dTop, double dBot, double hTop, double hBot, double depthLIn, double depthRIn)
    {
        var c = Setup(dTop, dBot, hTop, hBot);
        c.Ad.PlanCapDepthLeft = In(depthLIn); c.Ad.PlanCapDepthRight = In(depthRIn);
        var (m, u) = Run(c);
        var g = u.Abutment!;
        Assert.Equal(In(depthLIn), g.DepthAtLeftEnd, In(0.125));
        Assert.Equal(In(depthRIn), g.DepthAtRightEnd, In(0.125));
        Assert.DoesNotContain(m.Diagnostics, d => d.Message.Contains("step diagram prints"));
        // the solid: lowest point = the west bottom, highest = step H; the end faces carry the end step tops
        var cap = u.Solids.Single(s => s.Kind == SubElementKind.Footing);
        var zs = cap.Mesh.Positions.Select(p => p.Z).ToList();
        Assert.Equal(Ft(dBot) + c.Dz, zs.Min(), 4);
        Assert.Equal(Ft(hTop) + c.Dz, zs.Max(), 4);
        Assert.Contains(m.Notes, n => n.Contains("integral cap depth at the left end"));
    }

    [Fact]
    public void WrongPrintedDepth_IsReported_NotAdjusted()
    {
        var c = Setup();
        c.Ad.PlanCapDepthLeft = In(48);
        var (m, u) = Run(c);
        Assert.Contains(m.Diagnostics, d => d.Message.Contains("step diagram prints"));
        Assert.Equal(Ft(SD - SBotW), u.Abutment!.DepthAtLeftEnd, 6);
    }

    [Fact]
    public void IntegralCap_IsSteppedAtMidBay_OnePileRowOnClBearing_NoBackwallOrStepBlocks()
    {
        var c = Setup();
        var (m, u) = Run(c);
        var g = u.Abutment!;
        var kinds = u.Solids.Select(s => s.Kind).ToList();
        Assert.Single(kinds, k => k == SubElementKind.Footing);
        Assert.DoesNotContain(SubElementKind.Backwall, kinds);
        Assert.DoesNotContain(SubElementKind.Pedestal, kinds);
        Assert.Equal(4, kinds.Count(k => k == SubElementKind.Pile));
        Assert.All(u.Foundations[0].Piles, p => Assert.Equal(0, p.N, 6));
        Assert.Equal(c.Peds.Count - 1, g.StepRisersS.Count);
        for (int k = 0; k < g.StepRisersS.Count; k++) Assert.Equal((c.Peds[k].S + c.Peds[k + 1].S) / 2, g.StepRisersS[k], 6);
        // level steps: the top just left / right of a riser is the step of that girder
        Assert.Equal(c.Ad.Steps[0].TopElevation, g.StepTop!(g.StepRisersS[0] - 0.01), 9);
        Assert.Equal(c.Ad.Steps[1].TopElevation, g.StepTop!(g.StepRisersS[0] + 0.01), 9);
        // volume = 3'-0 x integral of (step top - bottom) along the line (ends parallel to the alignment are symmetric about CL brg)
        double vol = 0, ds = 0.001;
        for (double s = c.SMin + ds / 2; s < c.SMax; s += ds) vol += (g.StepTop!(s) - g.FootingBottom(s)) * ds;
        vol *= Ft(3);
        var cap = u.Solids.Single(s => s.Kind == SubElementKind.Footing);
        Assert.InRange(Math.Abs(cap.Mesh.Volume()) / vol, 0.995, 1.005);
        // pile heads 2'-0 above the cap bottom
        foreach (var p in u.Foundations[0].Piles) Assert.Equal(g.FootingBottom(p.S) + Ft(2), p.TopZ, 6);
        Assert.Contains(m.Notes, n => n.Contains("integral abutment"));
    }

    [Fact]
    public void RisersNotGiven_AreUnresolved_CapNotBuilt_EndDepthsStillReported()
    {
        var c = Setup();
        c.Ad.StepRisersAtMidBay = false;
        var (m, u) = Run(c);
        Assert.Contains(m.Diagnostics, d => d.Message.Contains("UNRESOLVED") && d.Message.Contains("Q3") && d.Message.Contains("p.17"));
        Assert.DoesNotContain(u.Solids, s => s.Kind == SubElementKind.Footing);
        Assert.Null(u.Abutment!.StepTop);
        Assert.Equal(Ft(SD - SBotW), u.Abutment.DepthAtLeftEnd, 6);
        Assert.Equal(4, u.Solids.Count(s => s.Kind == SubElementKind.Pile));
    }

    [Fact]
    public void ExplicitRisers_AreUsed_AndAWrongCountIsUnresolved()
    {
        var c = Setup();
        c.Ad.StepRisersAtMidBay = false;
        for (int k = 0; k + 1 < c.Peds.Count; k++) c.Ad.StepBoundaryS.Add(c.Peds[k].S + Ft(1));
        var (_, u) = Run(c);
        Assert.Equal(c.Peds[0].S + Ft(1), u.Abutment!.StepRisersS[0], 6);
        Assert.Single(u.Solids, s => s.Kind == SubElementKind.Footing);

        var c2 = Setup();
        c2.Ad.StepRisersAtMidBay = false;
        c2.Ad.StepBoundaryS.Add(c2.Peds[0].S + Ft(1));
        var (m2, u2) = Run(c2);
        if (c2.Peds.Count > 2)
        {
            Assert.Contains(m2.Diagnostics, d => d.Message.Contains("UNRESOLVED") && d.Message.Contains("step riser positions given"));
            Assert.DoesNotContain(u2.Solids, s => s.Kind == SubElementKind.Footing);
        }
    }

    [Fact]
    public void CmpCasing_WithoutBottom_IsScheduledUnresolved_WithBottom_IsModeled()
    {
        var c = Setup();
        c.Ad.PileCasing = new PileCasingDefinition { Diameter = In(24), Fill = "flowable fill", PlanTotalLength = Ft(281) };
        var (m, u) = Run(c);
        Assert.Contains(m.Diagnostics, d => d.Message.Contains("UNRESOLVED") && d.Message.Contains("CMP pile casing bottom") && d.Message.Contains("p.17"));
        Assert.Equal(4, u.Abutment!.Casings.Count);
        Assert.All(u.Abutment.Casings, k => Assert.True(double.IsNaN(k.Length)));
        Assert.DoesNotContain(u.Solids, s => s.Kind == SubElementKind.PileCasing);

        var c2 = Setup();
        double bottom = c2.Ad.FootingBottom.Min(p => p.Elevation) - Ft(17.6);
        c2.Ad.PileCasing = new PileCasingDefinition { Diameter = In(24), BottomElevation = bottom };
        var (_, u2) = Run(c2);
        var casings = u2.Solids.Where(s => s.Kind == SubElementKind.PileCasing).ToList();
        Assert.Equal(4, casings.Count);
        Assert.All(casings, s => Assert.False(s.IsConcrete));
        double area = 24 * 0.5 * Math.Pow(In(12), 2) * Math.Sin(2 * Math.PI / 24);   // 24-gon inscribed in the 24" circle
        double total = u2.Abutment!.Casings.Sum(k => k.Length);
        Assert.All(u2.Abutment.Casings, k => Assert.Equal(k.Top - bottom, k.Length, 9));
        Assert.Equal(area * total, casings.Sum(s => Math.Abs(s.Mesh.Volume())), 1e-4);
    }

    [Fact]
    public void WingExtension_BuildsWallFootingAndPile_AndMissingInputsAreUnresolved()
    {
        var c = Setup();
        double gutter = c.Off(c.SMax);
        double fb = c.Ad.FootingBottom[^1].Elevation;
        c.Ad.WingExtension = new WingExtensionDefinition
        {
            End = AbutmentEnd.Right, GutterOffset = gutter, Length = Ft(6.5), WallThickness = In(19), FootingInside = In(17),
            FootingBottomElevation = fb, FootingTopElevation = fb + Ft(3), WallTop = { new WallTopPoint(0, fb + Ft(9)), new WallTopPoint(Ft(6.5), fb + Ft(8.8)) },
            Piles = { new WingExtensionPileDefinition { Along = Ft(3.25), Across = 0 } }, PlanPileCount = 1, PileTopAboveFootingBottom = Ft(2),
        };
        c.Ad.PileCasing = new PileCasingDefinition { Diameter = In(24) };
        var (m, u) = Run(c);
        var foot = u.Solids.Single(s => s.Kind == SubElementKind.WingFooting);
        var wall = u.Solids.Single(s => s.Kind == SubElementKind.Wingwall);
        double nominal = (In(17) + In(19)) * Ft(6.5) * Ft(3);
        Assert.InRange(Math.Abs(foot.Mesh.Volume()) / nominal, 0.7, 1.3);
        Assert.InRange(wall.Mesh.Positions.Max(p => p.Z), fb + Ft(8.8) - 1e-6, fb + Ft(9) + 1e-6);
        Assert.Equal(5, u.Solids.Count(s => s.Kind == SubElementKind.Pile));
        Assert.Equal(5, u.Abutment!.Casings.Count);
        Assert.Contains(u.Abutment.Casings, k => Math.Abs(k.Top - fb) < 1e-9 && k.N != 0);

        var c2 = Setup();
        c2.Ad.WingExtension = new WingExtensionDefinition { End = AbutmentEnd.Right, GutterOffset = c2.Off(c2.SMax), Length = Ft(6.5), WallThickness = In(19), FootingInside = In(17), FootingBottomElevation = fb, PlanPileCount = 1 };
        var (m2, u2) = Run(c2);
        Assert.Contains(m2.Diagnostics, d => d.Message.Contains("UNRESOLVED") && d.Message.Contains("footing top"));
        Assert.Contains(m2.Diagnostics, d => d.Message.Contains("UNRESOLVED") && d.Message.Contains("WA-1"));
        Assert.DoesNotContain(u2.Solids, s => s.Kind is SubElementKind.Wingwall or SubElementKind.WingFooting);
    }

    [Fact]
    public void PileSpiral_IsScheduleOnly_OnePerPile_AndPlanBarListIsScheduleOnly()
    {
        var c = Setup();
        c.Ad.PileSpiral = new PileSpiralDefinition { BarSize = "#2", Diameter = In(21), Pitch = In(3), Turns = 7 };
        c.Ad.Rebar.PlanBarList.Add(new PlanBarLine("5b1", 40, Ft(10), 400));
        var (m, u) = Run(c);
        var sp = m.Marks.Single(k => k.Unit == u.Name && k.Mark == "pile spiral");
        Assert.Equal(4, sp.Count);
        Assert.Equal(7 * Math.Sqrt(Math.Pow(Math.PI * In(21), 2) + In(3) * In(3)), sp.Shape.DevelopedLength, 6);
        Assert.Equal("#2", sp.Size.Name);
        Assert.DoesNotContain(m.Bars, b => b.Unit == u.Name);
        Assert.Contains(m.Marks, k => k.Unit == u.Name && k.Mark == "5b1" && k.Location.Contains("schedule only"));
    }

    [Fact]
    public void LegacyStub_PileRowOffsets_OverrideTheTwoRows()
    {
        var c = Setup();
        // reuse the integral setup as a stub abutment: stub needs a backwall top
        c.Ad.Kind = AbutmentKind.Stub;
        c.Ad.BackwallTop.Add(new CapProfilePoint(0, c.Ad.Steps.Max(s => s.TopElevation) + Ft(4)));
        c.Ad.PileRowOffsets.Clear();
        var (_, u) = Run(c);
        Assert.Equal(8, u.Solids.Count(s => s.Kind == SubElementKind.Pile));
        var c2 = Setup();
        c2.Ad.Kind = AbutmentKind.Stub;
        c2.Ad.BackwallTop.Add(new CapProfilePoint(0, c2.Ad.Steps.Max(s => s.TopElevation) + Ft(4)));
        c2.Ad.PileRowOffsets.Clear(); c2.Ad.PileRowOffsets.Add(-Ft(1));
        var (_, u2) = Run(c2);
        Assert.Equal(4, u2.Solids.Count(s => s.Kind == SubElementKind.Pile));
    }

    [Fact]
    public void IntegralDefinition_RoundTripsThroughJson_AndSheetsBuild()
    {
        var c = Setup();
        c.Ad.WingExtension = new WingExtensionDefinition { End = AbutmentEnd.Right, GutterOffset = 5, Length = Ft(6.5), WallTop = { new WallTopPoint(1, 2) }, Piles = { new WingExtensionPileDefinition() } };
        c.Ad.PileCasing = new PileCasingDefinition { Diameter = In(24) };
        c.Ad.PileSpiral = new PileSpiralDefinition { BarSize = "#2", Diameter = In(21), Pitch = In(3), Turns = 7 };
        var (_, _) = Run(c);
        var back = SBM.Core.Project.BridgeProject.FromJson(c.Doc.Project.ToJson());
        var ad = back.Bridges[0].Supports[0].Substructure!.Abutment!;
        Assert.Equal(AbutmentKind.Integral, ad.Kind);
        Assert.Equal(new List<double> { 0 }, ad.PileRowOffsets);
        Assert.True(ad.StepRisersAtMidBay);
        Assert.Equal(2, ad.WingExtension!.WallTop[0].Elevation);
        Assert.True(double.IsNaN(ad.WingExtension.Piles[0].Along));
        Assert.True(double.IsNaN(ad.PileCasing!.BottomElevation));
        Assert.Equal(7, ad.PileSpiral!.Turns);
        // an old file without the new members reads as a stub abutment
        Assert.Equal(AbutmentKind.Stub, new AbutmentDefinition().Kind);
        var mm = c.Doc.Model();
        var ctx = new ViewContext(mm.Framing, mm.Steel, mm.Concrete, mm.Substructure, null, c.Doc.Units, DrawingOptions.For(c.Doc.Units));
        var sheets = SheetSet.Build(ctx, new SheetFormat(17, 11, "P", "B", "D"));
        Assert.NotEmpty(sheets);
    }
}
