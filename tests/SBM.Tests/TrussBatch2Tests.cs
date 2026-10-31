using SBM.Core.Bridge;
using SBM.Core.Project;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>PTC main span fix batch 2 (2026-10-28, bearings): gussets carried down to a finished bottom edge per layer, shoe connection angles and shoe
/// fasteners (rivets with probed grips, tap bolts).</summary>
public class TrussBatch2Tests
{
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    private static (BridgeProject p, BridgeDefinition b) Model(bool extend, bool fasteners)
    {
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        var jd = new TrussJointDefinition
        {
            Point = "L6", GussetThickness = In(0.75),
            GussetLayers = new()
            {
                new TrussGussetLayer { Thickness = In(0.5) },                                                                     // = the joint outline
                new TrussGussetLayer { Thickness = In(0.5), Outline = { new(-5, -5), new(5, -5), new(5, 0), new(-5, 0) } },        // own outline, clipped to the inner gusset
            },
        };
        t.Joints.Add(jd);
        if (extend)
        {
            jd.GussetExtension = new TrussGussetExtension { BottomV = -In(36), FromU = -In(12), ToU = In(12), Source = "test", Unresolved = "UNRESOLVED (p.0): gusset bottom - test." };
            jd.GussetLayers[1].Extension = new TrussGussetExtension { BottomV = -In(34), FromU = -In(12), ToU = In(12) };   // this layer stops 2 in. higher
        }
        if (fasteners)
        {
            var sh = t.Shoes.First(s => s.Name == "N-L6");
            sh.MasonryThickness = 0;      // the field masonry plate (3 ft below the pin) would sit where the test angle is
            var L = SteelCatalog.FindAngle("L4x4x1/2")!;
            double nOut = 10 + 0.75 + 0.5 + 0.5;      // box 20 in. o. to o. + gusset + 2 layers (both reach below the rivets at -33 in.)
            sh.Pieces = new()
            {
                new TrussShoePiece { Name = "block", Kind = TrussShoePieceKind.Body, Axis = TrussShoePieceAxis.Across, From = -In(16), To = In(16),
                    Profile = { new(-In(12), -In(40)), new(In(12), -In(40)), new(In(12), -In(36)), new(-In(12), -In(36)) } },
                new TrussShoePiece { Name = "shoe angle", Kind = TrussShoePieceKind.Angle, Axis = TrussShoePieceAxis.Along, From = -In(12), To = In(12), MirrorN = true,
                    Profile = SteelCatalog.AnglePolygon(L, In(nOut), 1, -In(36), 1).Select(q => new TrussUV(q.X, q.Y)).ToList() },
            };
            sh.Fasteners = new()
            {
                new TrussShoeFastener { Name = "rivets", Kind = TrussShoeFastenerKind.Rivet, Axis = TrussShoePieceAxis.Across, Diameter = In(0.875),
                    Us = { -In(6), In(6) }, Ns = { In(nOut + 0.25) }, Zs = { -In(33) }, MirrorN = true },
                new TrussShoeFastener { Name = "tap bolts", Kind = TrussShoeFastenerKind.TapBolt, Axis = TrussShoePieceAxis.Vertical, Diameter = In(1),
                    Us = { -In(8), 0, In(8) }, Ns = { In(nOut + 2.5) }, Zs = { -In(35.5) }, MirrorN = true, Grip = In(0.5), TapDepth = In(2), Material = "Tap bolt (test)" },
            };
        }
        return (p, b);
    }

    [Fact]
    public void GussetExtension_PerLayer_DownToTheBottomEdge()
    {
        var (p0, _) = Model(false, false);
        Assert.DoesNotContain("GussetExtension", p0.ToJson());
        Assert.DoesNotContain("\"Extension\"", p0.ToJson());
        var (p1, b1) = Model(true, false);
        var s0 = Regen(p0).Steel!; var s1 = Regen(p1).Steel!;
        double zP = Regen(p1).Steel!.Truss!.Points.First(q => q.Plane == "N" && q.Name == "L6").World.Z;
        double Bottom(SteelModel s, string mark) => s.Parts.Single(x => x.Mark == mark).Mesh.Bounds().Min.Z;
        // inner gusset and the layer that takes the joint outline: down to -36 in.; the layer with its own extension: -34 in.
        Assert.Equal(zP - In(36), Bottom(s1, "N-GL6-L"), 6);
        Assert.Equal(zP - In(36), Bottom(s1, "N-GL6-R2"), 6);
        Assert.Equal(zP - In(34), Bottom(s1, "N-GL6-L3"), 6);
        Assert.True(Bottom(s0, "N-GL6-L") > zP - In(20));                      // the envelope alone ends at the chord bottom
        // the bottom edge spans -12 .. +12 in. along the truss
        var g = s1.Parts.Single(x => x.Mark == "N-GL6-L").Mesh;
        var low = g.Positions.Where(v => v.Z < zP - In(36) + 1e-6).ToList();
        Assert.Equal(In(24), low.Max(v => v.X) - low.Min(v => v.X), 4);
        Assert.NotNull(s1.Parts.Single(x => x.Mark == "N-GL6-L").Assumption);
        Assert.Contains(s1.Diagnostics, d => d.Message.Contains("gusset bottom - test"));
        // other joints unchanged
        Assert.Equal(s0.Parts.Single(x => x.Mark == "N-GL2-L").Volume, s1.Parts.Single(x => x.Mark == "N-GL2-L").Volume, 12);
        // round trip
        var p2 = BridgeProject.FromJson(p1.ToJson());
        var j2 = p2.Bridges[0].Truss!.Joints.Last(j => j.Point == "L6");
        Assert.Equal(-In(36), j2.GussetExtension!.BottomV, 9);
        Assert.Equal(-In(34), j2.GussetLayers![1].Extension!.BottomV, 9);
        Assert.Equal(s1.Parts.Count, Regen(p2).Steel!.Parts.Count);
    }

    [Fact]
    public void ShoeAngles_Rivets_TapBolts()
    {
        var (p, _) = Model(true, true);
        Assert.Contains("\"Fasteners\"", p.ToJson());
        var s = Regen(p).Steel!;
        var parts = s.Parts.Where(x => x.Assembly == "SHOE-N-L6").ToList();
        var ang = parts.Single(x => x.Description.StartsWith("shoe angle"));
        Assert.Equal(PartCategory.TrussConnectionAngle, ang.Category);
        var bolts = s.Bolts.Where(x => x.Assembly == "SHOE-N-L6").ToList();
        // rivets: 2 x 2 sides, grip = angle 1/2 + layer 1/2 + layer 1/2 + gusset 3/4 = 2 1/4 (the steel along the shank)
        var riv = bolts.Where(x => x.Kind == BoltKind.Rivet).ToList();
        Assert.Equal(4, riv.Count);
        Assert.All(riv, r => Assert.Equal(In(2.25), r.Grip, 4));
        // heads on the outer (angle) face, shank toward the truss plane
        var pin = parts.Single(x => x.Mark.EndsWith("-PIN")).Mesh.Bounds();
        double yc = (pin.Min.Y + pin.Max.Y) / 2;
        Assert.All(riv, r => Assert.True(Math.Abs(r.Frame.Origin.Y - yc) > Math.Abs(r.Frame.Origin.Y + r.Frame.Z.Y * r.Grip - yc)));
        // tap bolts: 3 x 2 sides, bolts of their own material, frame at the bottom of the tapped hole, z up, length = plate + tap depth
        var tap = bolts.Where(x => x.Kind == BoltKind.Structural).ToList();
        Assert.Equal(6, tap.Count);
        double zP = s.Truss!.Points.First(q => q.Plane == "N" && q.Name == "L6").World.Z;
        Assert.All(tap, x =>
        {
            Assert.Equal("Tap bolt (test)", x.Material);
            Assert.Equal(In(2.5), x.Grip, 6);
            Assert.Equal(zP - In(35.5) - In(2.5), x.Frame.Origin.Z, 6);
            Assert.True(x.Frame.Z.Z > 0.999);
        });
        // other models: no fasteners -> no change in the bolt list of another shoe
        Assert.DoesNotContain(s.Bolts, x => x.Assembly == "SHOE-N-L0");
        var p2 = BridgeProject.FromJson(p.ToJson());
        Assert.Equal(2, p2.Bridges[0].Truss!.Shoes.First(x => x.Name == "N-L6").Fasteners!.Count);
    }
    [Fact]
    public void InlaidPly_StopsAtTheListedJointOnly_UnderGussetPlate()
    {
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        t.PartPerPiece = true;
        var j3 = new TrussJointDefinition { Point = "L3", Plane = "N" };
        t.Joints.Add(j3);
        Assert.DoesNotContain("InlaidAt", p.ToJson());
        Assert.DoesNotContain("UnderGusset", p.ToJson());
        double Gap(BridgeProject pp)
        {
            var s = Regen(pp).Steel!;
            // web ply pieces of the two chords meeting at L3 (plane N): gap between them along x
            var a = s.Parts.Where(x => x.Member == "N-L2-L3" && x.Piece == "web ply").Select(x => x.Mesh.Bounds()).ToList();
            var c = s.Parts.Where(x => x.Member == "N-L3-L4" && x.Piece == "web ply").Select(x => x.Mesh.Bounds()).ToList();
            return c.Min(q => q.Min.X) - a.Max(q => q.Max.X);
        }
        double g0 = Gap(p);
        t.Sections.First(x => x.Name == "BOX").WebPlies[0].InlaidAt = new() { "L3" };
        Assert.Contains("InlaidAt", p.ToJson());
        double g1 = Gap(p);
        Assert.True(g1 > g0 + 0.3, $"gap {g0} -> {g1}");
        // the far ends run on: L2-L3 still starts where it did at L2
        var s1 = Regen(p).Steel!;
        Assert.Contains(s1.Parts, x => x.Member == "N-L4-L5" && x.Piece == "web ply");
        // plate under the gusset at L3 (WebOutside, both webs): its length = the gusset extent over the band
        j3.Plates = new() { new TrussJointPlate { Face = TrussPlateFace.WebOutside, Width = In(24), Thickness = In(0.5), UnderGusset = true, Label = "o.s. splice" } };
        Assert.Contains("\"UnderGusset\": true", p.ToJson());
        var s2 = Regen(p).Steel!;
        var plate = s2.Parts.Where(x => x.Description.Contains("o.s. splice")).ToList();
        Assert.NotEmpty(plate);
        var gb = s2.Parts.First(x => x.Mark == "N-GL3-L").Mesh.Bounds();
        var pb = plate[0].Mesh.Bounds();
        Assert.True(pb.Max.X - pb.Min.X > 0.3 && pb.Min.X >= gb.Min.X - 1e-3 && pb.Max.X <= gb.Max.X + 1e-3);
    }
}
