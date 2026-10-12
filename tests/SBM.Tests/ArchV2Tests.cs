using SBM.Core.Arch;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Project;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Tied arch v2 details (Arch/ArchGenerator): field splices, fin plates, floorbeam connections, bracing gussets, knuckle details, PT,
/// deck rebar. Uses the self-contained <see cref="ArchTests.Model"/>.</summary>
public class ArchV2Tests
{
    internal static ArchSpliceDefinition RibSplice() => new()
    {
        Name = "rib", FlangeOuterWidth = In(14.5), FlangeOuterThickness = In(0.625), FlangeInnerWidth = In(5.625), FlangeInnerThickness = In(0.625),
        FlangeBoltFirst = In(2), FlangeBoltPitch = In(2), FlangeBoltColumns = 6, FlangeEndEdge = In(1.75), FlangeClip = In(1.75),
        FlangeRows = { new ArchBoltRow(In(1.75), 0, 2), new ArchBoltRow(In(4.25), 1, 2) },
        WebPlateWidth = In(10), WebPlateThickness = In(0.75), WebClip = In(1.75), WebBoltFirst = In(2), WebBoltPitch = In(2), WebBoltColumns = 4, WebEndEdge = In(1.75),
        WebRows = { new ArchBoltRow(In(1.5), 0, 2), new ArchBoltRow(In(3.25), 1, 2), new ArchBoltRow(In(5), 0, 2), new ArchBoltRow(In(6.75), 1, 2), new ArchBoltRow(In(8.5), 0, 2) },
    };

    internal static ArchSpliceDefinition TieSplice() => new()
    {
        Name = "tie", FlangeOuterWidth = In(14), FlangeOuterThickness = In(0.625), FlangeInnerWidth = In(6), FlangeInnerThickness = In(0.625),
        FlangeBoltFirst = In(1.75), FlangeBoltPitch = In(3), FlangeBoltColumns = 3, FlangeEndEdge = In(1.5), FlangeRows = { new ArchBoltRow(In(1.5)), new ArchBoltRow(In(4.5)) },
        WebPlateWidth = In(9), WebPlateThickness = In(0.75), WebPlateEdgeFromWebEnd = 0, WebBoltFirst = In(1.75), WebBoltPitch = In(3), WebBoltColumns = 3, WebEndEdge = In(1.5),
        WebRows = { new ArchBoltRow(In(1.5)), new ArchBoltRow(In(4.5)), new ArchBoltRow(In(7.5)) }, CheckFlangePlateLength = In(18.5),
    };

    [Fact]
    public void RibAndTieSplices_HavePlatesAndBoltsPerTheDetail()
    {
        var (p, b) = ArchTests.Model();
        b.Arch!.Ribs[0].SpliceDetail = RibSplice();
        var tie = b.Arch.Ties[0];
        tie.SpliceDetail = TieSplice();
        tie.SpliceAt.Add(new ArchPanelPoint("TS(1)", Ft(201) / 2 + Ft(1)));
        tie.SpliceUnresolved = "UNRESOLVED (p.x): test";
        var (_, m) = ArchTests.Generate(p);

        // Rib RS1: 68 bolts (flanges 2 x 24, web 20), 2 outside + 4 inside flange plates + 2 web plates.
        Assert.Equal(68, m.SpliceBolts["SOUTH-RIB-RS1"]);
        var rp = m.Steel.Parts.Where(x => x.Assembly == "SOUTH-RIB-RS1").ToList();
        Assert.Equal(8, rp.Count);
        Assert.All(rp, x => Assert.Equal(PartCategory.SplicePlate, x.Category));
        Assert.Equal(In(27.5), rp.First(x => x.Description.Contains("outside")).Length, 9);
        // Plate volume = outline - holes: outside plate 14.5 x 27.5 - 4 clips - 24 holes, x 5/8.
        double hole = Math.PI * Math.Pow(In(15.0 / 16) / 2, 2);
        double outside = (In(14.5) * In(27.5) - 4 * 0.5 * In(1.75) * In(1.75) - 24 * hole) * In(0.625);
        Assert.Equal(outside, rp.First(x => x.Description.Contains("outside")).Volume, 3);   // curved plate on R 126': within 0.1 %
        // Bolt grip through the flange: 5/8 + tf + 5/8.
        var w = SteelCatalog.FindWideFlange("W14x132")!;
        var rb = m.Steel.Bolts.Where(x => x.Assembly == "SOUTH-RIB-RS1").ToList();
        Assert.Equal(48, rb.Count(x => Math.Abs(x.Grip - (In(1.25) + w.FlangeThickness)) < 1e-9));
        Assert.Equal(20, rb.Count(x => Math.Abs(x.Grip - (In(1.5) + w.WebThickness)) < 1e-9));
        // Flange bolts are normal to the arch plane (binormal), web bolts lie in it (radial).
        var rib = m.Ribs[0];
        var bn = Vec3.Cross(rib.ChordDir, rib.Up).Normalized();
        var bnW = m.Frame.X * bn.X + m.Frame.Y * bn.Y + m.Frame.Z * bn.Z;
        Assert.Equal(48, rb.Count(x => Math.Abs(Math.Abs(Vec3.Dot(x.Frame.Z, bnW)) - 1) < 1e-6));

        // Tie: two segments (flange + web each), 42 bolts, plates flagged UNRESOLVED; bottom plate under the WP plane.
        Assert.Equal(4, m.Steel.Parts.Count(x => x.Category == PartCategory.TiePlate && x.Assembly == "SOUTH-TIE"));
        Assert.Equal(42, m.SpliceBolts["SOUTH-TIE-TS1"]);
        var tp = m.Steel.Parts.Where(x => x.Assembly == "SOUTH-TIE-TS1").ToList();
        Assert.Equal(5, tp.Count);      // bottom + 2 top flange plates + 2 web plates
        Assert.All(tp, x => Assert.NotNull(x.Assumption));
        var bottom = tp.First(x => x.Description.Contains("outside"));
        double zMin = bottom.Mesh.Positions.Min(q => m.Local(q).Z), zMax = bottom.Mesh.Positions.Max(q => m.Local(q).Z);
        Assert.Equal(-In(0.625), zMin, 6);
        Assert.Equal(0, zMax, 6);
        var web = tp.Where(x => x.Description.Contains("web")).ToList();
        Assert.All(web, x => Assert.Equal(In(11.875), x.Mesh.Positions.Max(q => m.Local(q).Z), 6));   // flush with the top of the web
        Assert.Contains(m.Checks, c => c.Item.Contains("splice flange plate length") && c.Ok);
        // Segment gap 1/2" at the splice.
        var segs = m.Steel.Parts.Where(x => x.Category == PartCategory.TiePlate && x.Assembly == "SOUTH-TIE" && x.Mark.EndsWith("-F")).ToList();
        Assert.Equal(Ft(201) - In(0.5), segs.Sum(x => x.Length), 6);
    }
    [Fact]
    public void FinPlates_FloorbeamConnections_Studs_Slots()
    {
        var (p, b) = ArchTests.Model();
        var a = b.Arch!;
        var h = a.Hangers[0];
        h.RibFin = new ArchRibFin { P3 = In(7), P4 = In(1) };
        h.TieFin = new ArchTieFin { Panel = "T2" };
        a.Fins = new ArchFinSettings();
        a.FloorbeamConnection = new ArchFloorbeamConnection
        {
            WebRows = { new ArchBoltRow(In(1.375), 0, 2), new ArchBoltRow(In(3.5), 1, 2), new ArchBoltRow(In(5.625), 0, 2) },
            FlangeRows = { new ArchBoltRow(In(1.375), 0, 2), new ArchBoltRow(In(3.5625), 1, 2) },
        };
        foreach (var fb in a.Floorbeams) { fb.PtDimA = Ft(6); fb.PtDimB = Ft(5); fb.PtSlotLength = In(5.5); fb.StudsPerFace = 32; fb.StudLength = In(6.875); }
        var (_, m) = ArchTests.Generate(p);
        // Fin plates: one rib fin, a tie fin as two legs; 4 bolts through the fin and the tie web.
        Assert.Single(m.Steel.Parts, x => x.Category == PartCategory.FinPlate && x.Assembly == "SOUTH-RIB");
        Assert.Equal(2, m.Steel.Parts.Count(x => x.Category == PartCategory.FinPlate && x.Assembly == "SOUTH-TIE"));
        Assert.Equal(4, m.Steel.Bolts.Count(x => x.Assembly == "SOUTH-TIE" && Math.Abs(x.Grip - In(1.75)) < 1e-9));
        // Floorbeam ends: stiffener + web / flange plates (top + bottom) per end, 22 bolts per end.
        var fb1 = m.Steel.Parts.Where(x => x.Assembly == "FB1").ToList();
        Assert.Equal(2, fb1.Count(x => x.Category == PartCategory.ArchStiffener));
        Assert.Equal(6, fb1.Count(x => x.Category == PartCategory.ArchConnectionPlate));
        Assert.Equal(44, m.Steel.Bolts.Count(x => x.Assembly == "FB1"));
        // 32 studs per web face; 4 PT slots in the web.
        Assert.Equal(64, m.Steel.Studs.Count(x => x.Assembly == "FB1"));
        Assert.Contains("4 PT slots", fb1.First(x => x.Mark == "FB1-W").Description);
    }

    [Fact]
    public void Bracing_ContinuousMember_CrossingSplice()
    {
        var (p, b) = ArchTests.Model();
        var a = b.Arch!;
        a.Points.Add(new ArchPoint("U2", Ft(-40), Ft(-5.5), Ft(42)));
        a.Points.Add(new ArchPoint("U2p", Ft(40), Ft(5.5), Ft(42)));
        a.Points.Add(new ArchPoint("C", 0, 0, Ft(38.5)));
        a.Bracing.Clear();
        a.Bracing.Add(new ArchBraceDefinition { Name = "X", From = "U1", To = "U1'" });
        a.Bracing.Add(new ArchBraceDefinition { Name = "C1", From = "U2", To = "C", TrimEndAt = "X" });
        a.Bracing.Add(new ArchBraceDefinition { Name = "C2", From = "C", To = "U2p", TrimStartAt = "X" });
        a.BracingCrossings.Add(new ArchBraceCrossing { Name = "UL", Point = "C", Continuous = "X", PieceA = "C1", PieceB = "C2",
            TopL1 = In(8), TopL2 = In(8), TopL3 = In(8), BottomL1 = In(8), BottomL2 = In(8), BottomL3 = In(8) });
        var (_, m) = ArchTests.Generate(p);
        var w = SteelCatalog.FindWideFlange("W8x28")!;
        var x = m.Steel.Parts.First(q => q.Mark == "X");
        Assert.Equal((m.World(a.Point("U1'")!.Vec) - m.World(a.Point("U1")!.Vec)).Length, x.Length, 9);
        var c1 = m.Steel.Parts.First(q => q.Mark == "C1");
        Assert.True(c1.Length < (m.World(a.Point("C")!.Vec) - m.World(a.Point("U2")!.Vec)).Length - w.FlangeWidth / 2);
        Assert.Equal(2, m.Steel.Parts.Count(q => q.Mark.StartsWith("BR-UL")));
        Assert.Equal(24, m.SpliceBolts["UPPER-BRACING-UL"]);
    }

    [Fact]
    public void KnuckleV2_PlatesStudsAndTieJoint()
    {
        var (p, b) = ArchTests.Model();
        var a = b.Arch!;
        var k = a.Knuckles.First(q => q.Name == "West");
        k.Detail = new ArchKnuckleDetail();
        k.Corners.Add(new ArchKnuckleCorner { Point = "T0", Rib = "South rib", Tie = "South tie", Splice = "RS1", OutsideLegHeight = In(10.5), InsideLegHeight = In(10.5) });
        k.EndWebOpeningsAt = new() { -In(49.5), In(49.5) };
        k.EndWebStudsAt = new() { 0, In(9.5) };
        k.DeckPlateEdgeFromTieCL = In(1); k.DeckPlateEdgeFromFloorbeamCL = In(1); k.DeckPlateOverEndFlange = In(3);
        k.DeckStudRows = new() { new ArchPoint("R1", In(23.875), 0, In(7)) }; k.DeckStudSpacing = In(9.5);
        var (_, m) = ArchTests.Generate(p);
        var parts = m.Steel.Parts.Where(q => q.Assembly == "KNUCKLE-WEST").ToList();
        Assert.Equal(2, parts.Count(q => q.Mark.Contains("-AF")));
        Assert.Contains(parts, q => q.Mark.EndsWith("-SW"));
        Assert.Contains(parts, q => q.Mark.EndsWith("-GU"));
        Assert.Equal(18, m.Steel.Bolts.Count(q => q.Assembly == "KNUCKLE-WEST"));
        Assert.DoesNotContain(m.Steel.Parts, q => q.Mark == "SOUTH-RIB-STUB1");
        // The end plate sits on the flange tops: bottom at the end flange top (7/8" above the WP).
        var dp = m.Steel.Parts.First(q => q.Mark == "KNUCKLE-WEST-EP-DP");
        Assert.Equal(In(0.875), dp.Mesh.Positions.Min(q => m.Local(q).Z), 6);
        Assert.Equal(2, m.Steel.Studs.Count(q => q.Assembly == "KNUCKLE-WEST-EP" && Math.Abs(q.Frame.Z.Z) < 0.5));
        // The south tie field piece starts at the joint (end flange edge), not at T0.
        var tieF = m.Steel.Parts.First(q => q.Assembly == "SOUTH-TIE" && q.Mark.EndsWith("-F"));
        Assert.True(tieF.Length < Ft(201) - In(18));
    }

    [Fact]
    public void Pt_DeckRebar_Ifc()
    {
        var (p, b) = ArchTests.Model();
        var dk = b.Arch!.Deck!;
        dk.Rebar = new ArchDeckRebar { TopTransLength = Ft(13) + In(10), BottomTransLength = Ft(18) + In(2) };
        dk.FlutesOnBarGrid = true;
        dk.PtSystem = new ArchPtSystem { ZoneBarsAt = { In(12), In(16) } };
        dk.Tendons.Add(new ArchTendon { Name = "PT1", Path = { new ArchPoint("a", Ft(-80), 0, In(7)), new ArchPoint("b", Ft(80), 0, In(7)) } });
        var bm = Assert.Single(new RegenerationCache().Regenerate(p))!;
        var m = bm.Steel!.Arch!;
        Assert.Equal(4, m.Steel.Parts.Count(q => q.Category == PartCategory.PtStrand));
        Assert.Equal(2, m.Steel.Parts.Count(q => q.Category == PartCategory.PtAnchorage));
        var marks = m.Concrete.Marks.ToDictionary(q => q.Mark);
        Assert.True(marks["D1"].Count >= 27 && marks["D1"].Count % 27 == 0);   // 27 lines split into lapped bars
        Assert.True(marks["D1"].Length <= Ft(40) + 1e-6);
        Assert.True(marks["B1"].Count > 200);
        Assert.Equal(4, marks["S2"].Count);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "arch.ifc", p, new[] { new IfcBridgeData(bm.Framing, bm.Steel, bm.Concrete) });
        string ifc = sw.ToString();
        foreach (var e in new[] { "IFCTENDON(", "IFCTENDONANCHOR(", "IFCREINFORCINGBAR(" }) Assert.Contains(e, ifc);
    }
}
