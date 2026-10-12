using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Substructure;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>OPTIONAL integral steel twin I-girder pier cap, capless pier (turned columns), jacking stiffeners, fabrication attributes
/// (Iowa Design 1617 Nebraska St. Ramp B features; generic inputs on the sample bridge).</summary>
public class IntegralSteelCapTests
{
    /// <summary>Sample bridge, square supports; pier 1 carries an integral twin cap whose top is about 4" under the girders' top of web.</summary>
    private static SbmDocument Doc(bool cap = true, bool capless = true, bool fabrication = false, bool camber = false)
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        foreach (var s in b.Supports) s.SkewDegrees = 0;
        doc.Regenerate(includeSteel: false);
        var f = doc.Framing();
        var sl = f.SupportLine(1);
        double tow = Enumerable.Range(0, b.Girders.Count).Min(g => f.Girders[g].Path.At(sl.Bearings[g]!.PathDistance is double pd && !double.IsNaN(pd) ? pd : f.Girders[g].DistanceAt(1, 0)).Item1.Z);
        if (!cap) return doc;
        double depth = In(1.75) + In(60) + In(1.75);
        double tob = tow - In(6) - depth;
        var cd = new IntegralSteelCapDefinition
        {
            Mark = "CAP1", Source = "test",
            BearingOffsets = { -Ft(24), Ft(24) }, BearingTopElevations = { tob - 0.08, tob + 0.08 }, BearingNames = { "Col 1", "Col 2" },
            WebDepth = In(60), PlanSlope = 0.16 / Ft(48), PlanFlangeGap = In(3.0 / 16),
            BearingStiffener = new PlateSize(In(9), In(1.5)), JackingStiffenerOffset = Ft(4), StiffenerTopNoWeld = In(4),
            StudLines = { Ft(10), Ft(10.5) }, StudRows = { -In(10), In(10) }, CoverPlateStudRows = { 0 },
            EndScreens = new CapEndScreen(),
        };
        for (int k = 1; k <= 5; k++) cd.Camber.Add(new CapCamberPoint(Ft(8) * k, In(k == 3 ? 0.4375 : 0.25), 0, In(0.125)));
        cd.CoverPlates.Add(new CapCoverPlate { Top = true, Bearing = 0, From = -Ft(1.5), To = Ft(6), BoltColumns = { In(3), In(6) }, BoltRows = { -In(5), In(5) } });
        cd.CoverPlates.Add(new CapCoverPlate { Top = false, Bearing = 1, From = In(1.5), FromSoleEdge = true, To = Ft(6), BoltColumns = { In(24), In(27) }, BoltRows = { -In(5), In(5) }, Unresolved = "UNRESOLVED (test): start" });
        var conn = new CapGirderConnection { BottomPlate = new PlateSize(In(24), In(2.5)) };
        foreach (var (x, z) in new[] { (-9.0, -1.75), (9.0, -1.75), (9.0, -50.0), (-9.0, -50.0) }) conn.EndPlateOutline.Add(new CapPoint(In(x), In(z)));
        foreach (double z in new[] { -4.0, -10, -16, -22, -28, -34, -40, -46 }) { conn.EndPlateBolts.Add(new CapPoint(-In(7.5), In(z))); conn.EndPlateBolts.Add(new CapPoint(In(7.5), In(z))); }
        foreach (double c in new[] { 2.1875, 7.1875, 13.1875, 18.1875 }) conn.SpliceBoltColumns.Add(In(c));
        for (int i = 0; i < 6; i++) conn.SpliceBoltColumns.Add(In(22.6875 + 3 * i));
        conn.SpliceBoltRows = new() { -In(5), In(5) };
        conn.SpliceStudColumns = new() { In(30) }; conn.SpliceStudRows = new() { 0 };
        cd.Connection = conn;
        cd.Handholes.Add(new CapHandhole(CapWebSide.Back, In(18), In(12)));
        cd.Handholes.Add(new CapHandhole(CapWebSide.Ahead, -In(18), In(30)));
        var hd = new CapHandholeDetail();
        foreach (var (x, z) in new[] { (-5.0, -6.5), (5.0, -6.5), (5.0, 6.5), (-5.0, 6.5) }) hd.CoverOutline.Add(new CapPoint(In(x), In(z)));
        foreach (var (x, z) in new[] { (-3.75, 3.0), (3.75, 3.0), (-3.75, -3.0), (3.75, -3.0) }) hd.Bolts.Add(new CapPoint(In(x), In(z)));
        hd.BackingOutlines.Add(new() { new(In(-5), In(4.5)), new(In(5), In(4.5)), new(In(5), In(6.5)), new(In(-5), In(6.5)) });
        cd.HandholeDetail = hd;
        b.Supports[1].IntegralSteelCap = cd;
        var bd = b.Bearings.Clone(); bd.Type = BearingType.Generic; bd.GenericHeight = In(14); bd.GenericBodyLength = In(28); bd.GenericBodyWidth = In(28);
        b.Supports[1].Bearings = bd;
        if (capless)
        {
            var sd = SubstructureDefinition.Default(SupportType.Pier);
            sd.NoCap = true; sd.CaplessPedestalHeight = In(4); sd.ColumnRotationDeg = 15;
            sd.ColumnCount = 2; sd.FirstColumnOffset = -Ft(24); sd.ColumnSpacings = new() { Ft(48) };
            sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = Ft(6); sd.ColumnDepth = Ft(7);
            sd.PedestalSizeAlong = Ft(5); sd.PedestalSizeAcross = Ft(6);
            sd.Foundation = FoundationType.SpreadFooting; sd.FoundationTopElevation = tob - Ft(20); sd.FootingLength = Ft(12); sd.FootingWidth = Ft(12); sd.FootingThickness = Ft(4);
            sd.Reinforcement.Enabled = false;
            sd.PlanColumnHeights = new() { Ft(20), Ft(20) };
            b.Supports[1].Substructure = sd;
        }
        if (camber)
        {
            for (int g = 0; g < b.Girders.Count; g++)
            {
                var gc = new GirderCamberDefinition { Girder = g, Keys = { new CamberKeyPoint { Number = 1, Support = 0 }, new CamberKeyPoint { Number = 2, Support = 1 }, new CamberKeyPoint { Number = 3, Support = 2 } } };
                gc.Points.Add(new CamberPoint { Number = 1 });
                gc.Points.Add(new CamberPoint { Number = 2, SlabDeflection = In(g == 0 ? 0.20 : 0.05), SteelDeflection = In(0.08) });
                gc.Points.Add(new CamberPoint { Number = 3 });
                b.Girders.Camber.Add(gc);
            }
        }
        if (fabrication)
            b.Fabrication = new()
            {
                new FabricationRule { Note = "F28", Source = "p.29", AssemblyKinds = new() { "PierCap" }, Categories = new() { "FlangePlate", "WebPlate" }, Set = { Toughness = "CVN zone 2, main tension member" } },
                new FabricationRule { Note = "F43", Source = "p.30", Categories = new() { "ContinuityPlate" }, Set = { ContactFinish = "Mill to bear" } },
                new FabricationRule { Note = "F01", Source = "p.4", Categories = new() { "FlangePlate" }, AssemblyKinds = new() { "GirderFieldSection" }, Material = "ASTM A709 Gr. 50W" },
                new FabricationRule { Note = "F19", Source = "p.24", Categories = new() { "Weld" }, Set = { Ndt = "MT" } },
            };
        return doc;
    }

    [Fact]
    public void Cap_IsBuilt_GirdersFrameIntoIt_NoGirderBearingsOrSupportFramesThere()
    {
        var plain = Doc(cap: false);
        Assert.Empty(plain.Regenerate().Errors);
        var s0 = plain.Steel();
        var doc = Doc();
        Assert.Empty(doc.Regenerate().Errors);
        var st = doc.Steel();
        var f = doc.Framing();
        int ng = doc.Bridge().Girders.Count;
        Assert.Contains(st.Assemblies, a => a.Kind == AssemblyKind.PierCap && a.Mark == "CAP1");
        Assert.Equal(ng, st.Assemblies.Count(a => a.Kind == AssemblyKind.PierCapConnection));
        // twin members: 2 top + 2 bottom flanges + 2 webs
        Assert.Equal(4, st.Parts.Count(p => p.Assembly == "CAP1" && p.Category == PartCategory.FlangePlate));
        Assert.Equal(2, st.Parts.Count(p => p.Assembly == "CAP1" && p.Category == PartCategory.WebPlate));
        // one more field section per girder (cut at the cap), two end plates per girder
        Assert.Equal(s0.Assemblies.Count(a => a.Kind == AssemblyKind.GirderFieldSection) + ng, st.Assemblies.Count(a => a.Kind == AssemblyKind.GirderFieldSection));
        Assert.Equal(2 * ng, st.Parts.Count(p => p.Category == PartCategory.EndPlate));
        // no girder bearings at the pier; two cap bearings with labels and plan locations
        string pier = f.SupportLine(1).Name;
        Assert.DoesNotContain(st.BearingSeats, x => x.Support == pier && x.Girder < 1000);
        var capSeats = st.BearingSeats.Where(x => x.Support == pier && x.Girder >= 2000).ToList();
        Assert.Equal(new[] { "Col 1", "Col 2" }, capSeats.Select(x => x.Label).OrderBy(x => x));
        Assert.All(capSeats, x => Assert.NotNull(x.Location));
        // no support cross frames and no girder bearing stiffeners on the cap line
        Assert.DoesNotContain(f.CrossFrames, c => c.AtSupport && c.SupportIndex == 1);
        Assert.Equal(s0.Parts.Count(p => p.Category == PartCategory.BearingStiffener && p.Mark.StartsWith("G")) - ng,
            st.Parts.Count(p => p.Category == PartCategory.BearingStiffener && p.Mark.StartsWith("G")));
        // cap stiffeners: 2 bearings + 2 jacking lines x 2 members x 2 faces, with welds
        Assert.Equal(16, st.Parts.Count(p => p.Assembly == "CAP1" && p.Category == PartCategory.BearingStiffener));
        Assert.Equal(16, st.Parts.Count(p => p.Assembly == "CAP1" && p.Category == PartCategory.Weld));
        // connection plates, cover plates, screens, studs, report
        Assert.Equal(2 * ng, st.Parts.Count(p => p.Category == PartCategory.ConnectorPlate));
        Assert.Equal(ng, st.Parts.Count(p => p.Category == PartCategory.ContinuityPlate));
        Assert.Equal(2, st.Parts.Count(p => p.Category == PartCategory.CoverPlate));
        Assert.Equal(2, st.Parts.Count(p => p.Category == PartCategory.MeshScreen));
        Assert.Contains(st.Bolts, x => x.Material != null && x.Material.Contains("Stainless"));
        Assert.True(st.Studs.Count(x => x.Assembly == "CAP1") >= 4);
        var rep = Assert.Single(st.IntegralCaps);
        Assert.Equal(ng, rep.Girders.Count);
        Assert.Equal(2, rep.Bearings.Count);
        Assert.Equal(0.16 / Ft(48), rep.Slope, 9);
        Assert.Contains(rep.Checks, c => c.StartsWith("Slope"));
        // the flagged bottom cover plate carries its assumption
        Assert.NotNull(st.Parts.Single(p => p.Category == PartCategory.CoverPlate && p.Description.StartsWith("Bottom")).Assumption);
    }

    [Fact]
    public void TaperedFills_TaperInTwoDirections_AndTopFlangeHolesAreRegistered()
    {
        var doc = Doc();
        doc.Regenerate();
        var st = doc.Steel();
        var rep = st.IntegralCaps.Single();
        foreach (var g in rep.Girders)
        {
            Assert.True(g.FillMax > g.FillMin + In(0.05), $"{g.Name}: fill should be tapered ({g.FillMin} .. {g.FillMax})");
            Assert.True(g.FillMin > 0);
        }
        var fills = st.Parts.Where(p => p.Category == PartCategory.FillerPlate && p.Description.StartsWith("Tapered fill")).ToList();
        Assert.Equal(2 * doc.Bridge().Girders.Count, fills.Count);
        // the top splice plates exist and the girder flanges next to the cap carry the splice bolt holes
        Assert.Equal(3 * doc.Bridge().Girders.Count, st.Parts.Count(p => p.Category == PartCategory.SplicePlate && p.Assembly.StartsWith("CAP1-C")));
        var tf = st.Parts.Where(p => p.Category == PartCategory.FlangePlate && p.Mark.StartsWith("G1-") && p.Mark.Contains("-TF")).ToList();
        Assert.Contains(tf, p => p.Mesh.Solids is { Count: > 0 } sol && sol.Any(x => x.Holes.Length > 0));
    }

    [Fact]
    public void Handholes_OpenTheWeb_WithBoltedCoverAndBackingPlates()
    {
        var doc = Doc();
        doc.Regenerate();
        var st = doc.Steel();
        int ng = doc.Bridge().Girders.Count;
        Assert.Equal(2 * ng, st.Parts.Count(p => p.Category == PartCategory.HandholeCover));
        Assert.Equal(2 * ng, st.Parts.Count(p => p.Category == PartCategory.HandholeBacking));
        Assert.Contains($"{ng} handholes", st.Parts.Single(p => p.Mark == "CAP1-W1").Description);
        Assert.Equal(2 * ng * 4, st.Bolts.Count(b => b.Assembly == "CAP1" && Math.Abs(b.Diameter - In(0.75)) < 1e-9));
    }

    [Fact]
    public void Camber_DeflectingSupport_IsComparedWithTheCapCamber()
    {
        var doc = Doc(camber: true);
        var r = doc.Regenerate();
        var rep = doc.Steel().IntegralCaps.Single();
        Assert.All(rep.Girders, g => Assert.False(double.IsNaN(g.GirderDeflection)));
        Assert.All(rep.Girders, g => Assert.False(double.IsNaN(g.CapCamber)));
        // girder 1 deflects 0.28" (inside the cap camber window at its position); the others 0.13" (outside) -> CHECK warnings
        Assert.Contains(r.Warnings, w => w.Contains("CHECK (camber, deflecting support"));
        // the girder camber chord runs through the deflecting support (it is not a chord end)
        var cam = doc.Framing().Camber!.Girder(0)!;
        Assert.False(cam.Point(2)!.ChordEnd);
        Assert.False(double.IsNaN(cam.Point(2)!.CamberFromNoLoad));
    }

    [Fact]
    public void CaplessPier_TurnedColumns_PedestalsUnderTheCapBearings_NoCapSolid()
    {
        var doc = Doc();
        Assert.Empty(doc.Regenerate().Errors);
        var u = doc.Substructure().Units.Single(x => x.Name == doc.Framing().SupportLine(1).Name);
        Assert.True(u.NoCap);
        Assert.DoesNotContain(u.Solids, s => s.Kind == SubElementKind.Cap);
        Assert.Equal(2, u.Columns.Count);
        Assert.Equal(2, u.Solids.Count(s => s.Kind == SubElementKind.Pedestal));
        // pedestal 4" high from the column top to the seat
        Assert.All(u.Pedestals, p => Assert.Equal(In(4), p.Height, 6));
        // the column is turned 15 deg: its corners reach beyond the unturned half sizes in S
        var col = u.Solids.First(s => s.Kind == SubElementKind.Column).Mesh;
        double sMax = col.Positions.Max(q => Vec2.Dot(q.XY - u.Plan(u.Columns[0].S, u.Columns[0].N), u.D));
        double expect = Ft(3) * Math.Cos(15 * Math.PI / 180) + Ft(3.5) * Math.Sin(15 * Math.PI / 180);
        Assert.Equal(expect, sMax, 4);
        Assert.Contains("turned 15", u.Solids.First(s => s.Kind == SubElementKind.Column).Description);
    }

    [Fact]
    public void JackingStiffeners_AndPerSupportBearingStiffenerPlate()
    {
        var doc = Doc(cap: false);
        var b = doc.Bridge();
        b.Supports[0].JackingStiffenerOffsets = new() { Ft(1.5) };
        b.Supports[0].BearingStiffener = new PlateSize(In(10.5), In(1.125));
        doc.Regenerate();
        var f = doc.Framing();
        var at0 = f.Stiffeners.Where(s => s.Kind == SBM.Core.Framing.StiffenerKind.Bearing && s.GirderDistance < Ft(3)).ToList();
        Assert.Equal(2 * b.Girders.Count, at0.Count);
        Assert.All(at0, s => Assert.Equal(In(10.5), s.Plate.Width, 9));
    }

    [Fact]
    public void FabricationRules_AreAppliedToParts_AndExportedToIfc()
    {
        var doc = Doc(fabrication: true);
        doc.Regenerate();
        var st = doc.Steel();
        var capFlange = st.Parts.First(p => p.Assembly == "CAP1" && p.Category == PartCategory.FlangePlate);
        Assert.Equal("CVN zone 2, main tension member", capFlange.Fabrication!.Toughness);
        Assert.Contains("F28 p.29", capFlange.Fabrication.Notes);
        Assert.Equal("Mill to bear", st.Parts.First(p => p.Category == PartCategory.ContinuityPlate).Fabrication!.ContactFinish);
        Assert.Equal("ASTM A709 Gr. 50W", st.Parts.First(p => p.Category == PartCategory.FlangePlate && p.Mark.StartsWith("G1")).Material);
        Assert.Equal("MT", st.Parts.First(p => p.Category == PartCategory.Weld).Fabrication!.Ndt);
        var path = Path.Combine(Path.GetTempPath(), $"sbm-fab-{Guid.NewGuid():N}.ifc");
        try
        {
            doc.ExportIfc(path);
            var text = File.ReadAllText(path);
            Assert.Contains("'SBM_FabricationAttributes'", text);
            Assert.Contains("'END_PLATE'", text);
            Assert.Contains("'INTEGRAL_STEEL_PIER_CAP'", text);
            Assert.Contains("'Toughness'", text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void WithoutTheNewInputs_NoCapPartsAndNoFabricationPset()
    {
        var doc = Doc(cap: false);
        doc.Regenerate();
        var st = doc.Steel();
        Assert.Empty(st.IntegralCaps);
        Assert.DoesNotContain(st.Parts, p => p.Fabrication != null);
        var path = Path.Combine(Path.GetTempPath(), $"sbm-nofab-{Guid.NewGuid():N}.ifc");
        try
        {
            doc.ExportIfc(path);
            Assert.DoesNotContain("SBM_FabricationAttributes", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NewInputs_SurviveSaveAndLoad_AndAreOmittedWhenUnused()
    {
        var doc = Doc(fabrication: true);
        var path = Path.Combine(Path.GetTempPath(), $"sbm-cap-{Guid.NewGuid():N}.sbm");
        try
        {
            doc.Save(path);
            var back = SbmDocument.Open(path).Bridge();
            var cd = back.Supports[1].IntegralSteelCap!;
            Assert.Equal(2, cd.BearingOffsets.Count);
            Assert.Equal(In(18), cd.Handholes[0].Offset, 9);
            Assert.True(back.Supports[1].Substructure!.NoCap);
            Assert.Equal(15, back.Supports[1].Substructure!.ColumnRotationDeg);
            Assert.Equal(4, back.Fabrication!.Count);
            var plain = Doc(cap: false);
            plain.Save(path);
            string json = File.ReadAllText(path);
            foreach (var k in new[] { "IntegralSteelCap", "JackingStiffenerOffsets", "\"Fabrication\"", "NoCap", "ColumnRotationDeg", "CaplessPedestalHeight" })
                Assert.DoesNotContain(k, json);
        }
        finally { File.Delete(path); }
    }
}
