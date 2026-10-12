using SBM.Core.Arch;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Steel;
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
}
