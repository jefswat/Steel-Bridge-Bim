using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
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
        ad.Rebar.PlanBarList.Add(new PlanBarLine("8j3", 8, Ft(5), 107));
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
    public void CounterfortBody_IsNarrowerThanItsFooting_AndUpliftAnchorsSitOnTheInnerPile()
    {
        var (doc, ad) = Setup();
        ad.Wings[0].Counterforts[0].BodyWidth = In(22);
        ad.Wings[0].Counterforts[0].VerticalMark = "6h2";
        ad.Wings[0].UpliftPiles.Add(new UpliftPileDefinition { Footing = 1, Pile = 2 });
        ad.Rebar.PlanBarList.Add(new PlanBarLine("6h2", 10, Ft(11.4), 0));
        ad.Rebar.PlanBarList.Add(new PlanBarLine("8j3", 2, Ft(5), 0).Shaped("V", In(6), In(30), In(32), In(25.5)));
        var sd = doc.Bridge().Supports[0].ResolvedSubstructure.Clone();
        sd.Abutment = ad; sd.PileSection = "HP14x73"; sd.PileType = PileType.SteelHP;
        doc.Bridge().Supports[0].Substructure = sd;
        doc.Regenerate();
        var m = doc.Substructure();
        var u = m.Units.First(x => x.SupportIndex == 0);
        var wf = u.Solids.Single(s => s.Kind == SubElementKind.WingFooting);
        var cf = u.Solids.Single(s => s.Kind == SubElementKind.Counterfort);
        // footing 3'-0 along the wall x 7'-0 across (default) x 3'-0 deep; counterfort 1'-10 x 3'-6
        Assert.InRange(Math.Abs(wf.Mesh.Volume()), Ft(3) * Ft(7) * Ft(3) * 0.99, Ft(3) * Ft(7) * Ft(3) * 1.01);
        Assert.InRange(Math.Abs(cf.Mesh.Volume()) / (In(22) * Ft(3.5) * Ft(6)), 0.99, 1.01);
        // two V bars on the inner (second) pile of footing 1, 6" above the footing bottom
        var anchors = m.Bars.Where(b => b.Unit == u.Name && b.Mark == "8j3").ToList();
        Assert.Equal(2, anchors.Count);
        var f1 = u.Foundations[1];
        foreach (var a in anchors) Assert.Equal(f1.BottomZ + In(6), a.Points.Min(p => p.Z), 3);
        Assert.Equal(10, m.Bars.Count(b => b.Unit == u.Name && b.Mark == "6h2"));
        // the circled pile's web runs across the wall: both piles of the footing share it
        Assert.All(f1.Piles, p => Assert.Equal(1.0, Math.Sqrt(p.WebS * p.WebS + p.WebN * p.WebN), 6));
    }

    [Fact]
    public void FootingNotch_RemovesTheAcuteCorner_AndMaskwallIsFlushWithTheEndLine()
    {
        var (doc, ad) = Setup();
        var u0 = doc.Substructure().Units.First(u => u.SupportIndex == 0);
        var sl = doc.Framing().SupportLine(0);
        var al = doc.Alignment();
        var baseline = doc.Substructure().Units.First(u => u.SupportIndex == 0);
        ad.Masks.Add(new MaskwallDefinition { End = AbutmentEnd.Left, Height = Ft(10), Thickness = In(10) });
        var sd = doc.Bridge().Supports[0].ResolvedSubstructure.Clone();
        sd.Abutment = ad;
        doc.Bridge().Supports[0].Substructure = sd;
        doc.Regenerate();
        double v0 = Math.Abs(doc.Substructure().Units.First(x => x.SupportIndex == 0).Solids.Single(s => s.Kind == SubElementKind.Footing).Mesh.Volume());
        var mk = doc.Substructure().Units.First(x => x.SupportIndex == 0).Abutment!.Masks.Single();
        Assert.Equal(ad.LeftEndOffset, mk.EOuter, 9);                       // outer face flush with the footing end line
        Assert.Equal(ad.LeftEndOffset + In(10), mk.EInner, 9);
        // a notch on the span-side face at the left end: a line of constant station through the end line, 2 ft rearward of the face corner
        var g = doc.Substructure().Units.First(x => x.SupportIndex == 0).Abutment!;
        double nF = g.N(g.XFootFront);
        var pFace = al.Inverse(doc.Substructure().Units.First(x => x.SupportIndex == 0).Plan(g.LeftEndS(nF), nF));
        ad.FootingNotches.Add(new FootingNotchDefinition { End = AbutmentEnd.Left, FrontStation = pFace.Station - Ft(2) });
        sd.Abutment = ad;
        doc.Regenerate();
        double v1 = Math.Abs(doc.Substructure().Units.First(x => x.SupportIndex == 0).Solids.Single(s => s.Kind == SubElementKind.Footing).Mesh.Volume());
        Assert.NotEqual(v0, v1, 3);
        Assert.True(Math.Abs(v1 - v0) > 0.1 && Math.Abs(v1 - v0) < 5, $"notch changes the footing volume by {v1 - v0:F3} m3");
        _ = u0; _ = sl; _ = baseline;
        // the plan outline (generator) and the piling-layout view follow the notched footing: same area as the footing solid's bottom face
        var un = doc.Substructure().Units.First(x => x.SupportIndex == 0);
        var outline = un.Abutment!.FootingOutline;
        Assert.True(outline.Count > 4, "notched outline has more than the 4 parallelogram corners");
        double aOut = Math.Abs(MeshBuilder.SignedArea(outline));
        Assert.InRange(aOut / BottomArea(un.Solids.Single(s => s.Kind == SubElementKind.Footing).Mesh), 0.999, 1.001);
        var view = doc.Drawings().Single(v => v.Name == un.Name + " piling and wing layout");
        var concrete = view.Drawing.Layers.Where(l => l.Style == SBM.Core.Drawing.Views.SheetLayers.Concrete).SelectMany(l => l.Polylines).Where(p => p.Closed).ToList();
        var drawn = concrete.Single(p => p.Points.Length == outline.Count);
        Assert.Equal(aOut, Math.Abs(MeshBuilder.SignedArea(drawn.Points.Select(q => new Vec2(q.X, q.Y)).ToList())), 4);
    }

    [Fact]
    public void CorbelOffsets_LeaveAPlainWallBehindTheWing_AndTheRearElevationStopsTheCorbelLines()
    {
        var (doc, ad) = Setup();
        var sd = doc.Bridge().Supports[0].ResolvedSubstructure.Clone();
        sd.Abutment = ad;
        doc.Bridge().Supports[0].Substructure = sd;
        doc.Regenerate();
        double Upper() => Math.Abs(doc.Substructure().Units.First(x => x.SupportIndex == 0).Solids.Single(s => s.Name.EndsWith("backwall (upper, corbel)")).Mesh.Volume());
        double v0 = Upper();
        ad.CorbelLeftOffset = ad.LeftEndOffset + Ft(3);
        sd.Abutment = ad;
        doc.Regenerate();
        double v1 = Upper();
        var g = doc.Substructure().Units.First(x => x.SupportIndex == 0).Abutment!;
        double nB = g.N(g.XBwBack);
        double plainS = g.CorbelLeftS!(nB) - g.BwLeftS(nB);
        Assert.True(plainS > Ft(2.9), $"plain reach {plainS:F3} m");
        // removed = corbel section (projection x vertical face + 45 deg taper triangle) over the plain reach
        double area = ad.CorbelProjection * ad.CorbelVertical + ad.CorbelProjection * ad.CorbelTaperRise / 2;
        Assert.InRange((v0 - v1) / (area * plainS), 0.9, 1.1);
        // backwall extent unchanged (the plain wall continues to the end), corbel offsets outside the backwall are rejected
        Assert.Equal(g.BwLeftS(nB), g.LeftEndS(nB), 6);
        ad.CorbelLeftOffset = ad.LeftEndOffset - Ft(1);
        sd.Abutment = ad;
        var res = doc.Regenerate();
        Assert.True(res.Errors.Count > 0 || doc.Substructure().Diagnostics.Any(d => d.Message.Contains("corbel offsets")), "corbel outside the backwall must be reported");
    }

    /// <summary>Plan area of the downward-facing triangles of a solid.</summary>
    private static double BottomArea(Mesh m)
    {
        double a = 0, zMin = m.Positions.Min(p => p.Z);
        for (int t = 0; t < m.TriangleCount; t++)
        {
            var p0 = m.Positions[m.Indices[3 * t]]; var p1 = m.Positions[m.Indices[3 * t + 1]]; var p2 = m.Positions[m.Indices[3 * t + 2]];
            if (Math.Max(p0.Z, Math.Max(p1.Z, p2.Z)) > zMin + 1e-6) continue;          // level footing bottom only
            a += Math.Abs(Vec3.Cross(p1 - p0, p2 - p0).Z) / 2;
        }
        return a;
    }

    [Fact]
    public void DefaultAbutment_IsUnchanged_WhenNoAbutmentDefinition()
    {
        var doc = SbmDocument.Sample();
        var u = doc.Substructure().Units.First(x => x.Type == SupportType.Abutment);
        Assert.Null(u.Abutment);
        Assert.Contains(u.Solids, s => s.Kind == SubElementKind.Backwall);
    }

    [Fact]
    public void StandalonePedestal_AndCipPipePiles_AreBuiltAndExported()
    {
        var (doc, ad) = Setup();
        var u0 = doc.Substructure().Units.First(u => u.SupportIndex == 0);
        double sFree = u0.Pedestals.Max(p => p.S) + Ft(2);           // beyond the last bearing, still on the footing (ends at max + 3 ft)
        double top = u0.Pedestals.Min(p => p.SeatZ) - In(2);
        ad.StandalonePedestals.Add(new AbutmentPedestalDefinition(sFree, top, "FUTURE BEAM") { Length = Ft(1.5) });
        var sd = doc.Bridge().Supports[0].ResolvedSubstructure.Clone();
        sd.Abutment = ad; sd.PileType = PileType.CipPipe; sd.PileSize = In(12); sd.PileShellThickness = In(0.25);
        doc.Bridge().Supports[0].Substructure = sd;
        doc.Regenerate();
        var u = doc.Substructure().Units.First(x => x.SupportIndex == 0);
        var fp = Assert.Single(u.Pedestals, p => p.Girder < 0);
        Assert.Equal("FUTURE BEAM", fp.Label);
        Assert.Equal(top, fp.SeatZ, 6);
        Assert.Equal(Ft(1.5), fp.LengthS, 6);
        Assert.Contains(u.Abutment!.Steps, st => st.Girder < 0 && st.Tag == "FUTURE BEAM");
        var pile = u.Solids.First(s => s.Kind == SubElementKind.Pile);
        Assert.Equal(In(0.25), pile.ShellThickness, 9);
        double len = ad.PileLength;
        Assert.InRange(Math.Abs(pile.Mesh.Volume()) / (Math.PI * In(6) * In(6) * len), 0.99, 1.0);   // round (inscribed 32-gon)
        string path = Path.Combine(Path.GetTempPath(), "abut-cip-" + Guid.NewGuid().ToString("N") + ".ifc");
        try
        {
            doc.ExportIfc(path);
            string text = File.ReadAllText(path);
            Assert.Contains(".DRIVEN.,.COMPOSITE.)", text);
            Assert.Contains("'SBM_PipePile'", text);
            Assert.Contains("'ShellThickness'", text);
            Assert.Contains("FUTURE BEAM", text);
        }
        finally { File.Delete(path); }
    }

    /// <summary>MnDOT stem (AbutmentDefinition.Stem, Br. 02069 p11 A-A): plain footing, stem with back ledge + sloped seat, infill wall only between the
    /// backwall offsets on the ledge, pedestals on the seat, mask wall beside the footing down to its bottom; volumes are exact prisms.</summary>
    [Fact]
    public void MnDotStem_PlainFootingStemLedgeSeat_InfillOnLedge_MaskBesideFooting()
    {
        var (doc, ad) = Setup();
        ad.Wings.Clear();
        double zf = ad.FootingBottom[0].Elevation, D = Ft(3);
        ad.FootingDepth = D;
        ad.BackwallFrontToBearing = Ft(1); ad.BackwallThickness = Ft(1.5); ad.FootingBackToBackwallBack = In(9); ad.BearingToFootingFront = Ft(2.75);
        ad.PlanFootingWidth = Ft(6); ad.BearingToStepFront = Ft(1) + In(4);
        double ledge = zf + D + In(19.25), seatBack = ledge + Ft(1), seatFront = seatBack - In(1);
        ad.Stem = new AbutmentStemDefinition { FrontToBearing = Ft(1.5) };
        ad.Stem.LedgeTop.Add(new CapProfilePoint(0, ledge)); ad.Stem.SeatBackTop.Add(new CapProfilePoint(0, seatBack)); ad.Stem.SeatFrontTop.Add(new CapProfilePoint(0, seatFront));
        ad.BackwallLeftOffset = ad.RightEndOffset - Ft(6);            // infill only in the last 6 ft
        ad.Steps.Clear();
        var u0 = doc.Substructure().Units.First(u => u.SupportIndex == 0);
        foreach (var p in u0.Pedestals) ad.Steps.Add(new AbutmentStepDefinition(p.Girder + 1, seatBack + In(6)));
        ad.Masks.Add(new MaskwallDefinition { End = AbutmentEnd.Left, Thickness = In(17.5), Setback = -In(17.5), BottomElevation = zf, Height = Ft(12) });
        var sd = doc.Bridge().Supports[0].ResolvedSubstructure.Clone();
        sd.Abutment = ad; sd.PileSection = "HP14x73";
        doc.Bridge().Supports[0].Substructure = sd;
        doc.Regenerate();
        var u = doc.Substructure().Units.First(x => x.SupportIndex == 0);
        var g = u.Abutment!;
        double len = u.CapS1 - u.CapS0;                                   // footing ends are alignment-offset lines; the sample line is square enough
        var foot = u.Solids.Single(s => s.Kind == SubElementKind.Footing);
        Assert.InRange(Math.Abs(foot.Mesh.Volume()) / (len * Ft(6) * D), 0.995, 1.005);   // plain block: no wash / keyed zone
        var stem = u.Solids.Single(s => s.Name.EndsWith(" stem"));
        double area = Ft(1.5) * (ledge - zf - D) + Ft(2.5) * ((seatBack + seatFront) / 2 - zf - D);
        Assert.InRange(Math.Abs(stem.Mesh.Volume()) / (len * area), 0.995, 1.005);
        var infill = u.Solids.Single(s => s.Name.Contains("infill"));
        var ib = infill.Mesh.Bounds();
        Assert.Equal(ledge, ib.Min.Z, 6);
        Assert.InRange(ib.Max.Z, ad.BackwallTop.Max(p => p.Elevation) - In(0.25), ad.BackwallTop.Max(p => p.Elevation) + 1e-6);
        Assert.Equal(ledge, g.BackwallBaseZ(0), 9);
        Assert.Equal(seatBack - In(1) * 12 / 30, g.SeatBaseZ(0), 9);           // seat at the CL bearing (12" ahead of the riser on a 30" seat)
        foreach (var p in u.Pedestals) Assert.Equal(g.SeatBaseZ(p.S), p.CapTopZ, 9);
        var mask = u.Solids.Single(s => s.Kind == SubElementKind.Maskwall);
        Assert.Equal(zf, mask.Mesh.Bounds().Min.Z, 6);
        Assert.Equal(zf + Ft(12), mask.Mesh.Bounds().Max.Z, 6);
        Assert.InRange(Math.Abs(mask.Mesh.Volume()) / (In(17.5) * Ft(6) * Ft(12)), 0.99, 1.06);   // skewed sample support: offset-line sides
        // the stem definition round-trips and is omitted from the file when null
        var jo = new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        var json = System.Text.Json.JsonSerializer.Serialize(ad, jo);
        Assert.Contains("\"Stem\"", json);
        Assert.DoesNotContain("\"Stem\"", System.Text.Json.JsonSerializer.Serialize(new AbutmentDefinition(), jo));
        Assert.DoesNotContain("BottomElevation", System.Text.Json.JsonSerializer.Serialize(new MaskwallDefinition(), jo));
        Assert.Equal(ad.Stem.FrontToBearing, ad.Clone().Stem!.FrontToBearing, 12);
    }

    [Fact]
    public void MnDotStem_MissingInput_IsReportedNotGuessed()
    {
        var (doc, ad) = Setup();
        ad.Stem = new AbutmentStemDefinition { FrontToBearing = Ft(1.5) };   // no ledge / seat elevations
        var sd = doc.Bridge().Supports[0].ResolvedSubstructure.Clone();
        sd.Abutment = ad; sd.PileSection = "HP14x73";
        doc.Bridge().Supports[0].Substructure = sd;
        doc.Regenerate();
        Assert.Contains(doc.Substructure().Diagnostics, d => d.Message.Contains("stem needs") && d.Message.Contains("ledge top"));
        Assert.Null(doc.Substructure().Units.FirstOrDefault(x => x.SupportIndex == 0)?.Abutment);
    }
}
