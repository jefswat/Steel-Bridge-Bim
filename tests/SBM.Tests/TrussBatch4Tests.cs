using SBM.Core.Bridge;
using SBM.Core.Project;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>PTC main span fix batch 4 (2026-10-28): web member ends (parallel to the chord, clear of each other), gusset edge angles, joint plate rivets,
/// dropped drawn rivet records.</summary>
public class TrussBatch4Tests
{
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    [Fact]
    public void WebEnds_ParallelToChord_AndClearOfEachOther()
    {
        var (p0, _) = TrussTests.Model();
        var (p1, b1) = TrussTests.Model();
        var t = b1.Truss!;
        Assert.DoesNotContain("WebToWebClearance", p1.ToJson());
        t.JointDefaults.WebToWebClearance = 0;
        var vert = t.Members.First(m => m.Name == "U2-L2");
        vert.EndCut = TrussEndCut.ParallelToChord;
        Assert.Contains("WebToWebClearance", p1.ToJson());
        Assert.Contains("ParallelToChord", p1.ToJson());
        var m0 = Regen(p0).Steel!; var m1 = Regen(p1).Steel!;
        // web members meeting at a lower chord point: no pair of web member solids overlaps more than 1/64 in. (clash check)
        var items = m1.Parts.Where(x => x.Category is PartCategory.TrussDiagonal or PartCategory.TrussVertical).Select(x => new SBM.Core.Checks.ClashItem(x.Category.ToString(), x.Mark, x.Assembly, x.Description, x.Mesh)).ToList();
        var rep = SBM.Core.Checks.SteelClashCheck.Run("t", items, new SBM.Core.Checks.ClashOptions { Tolerance = In(1.0 / 64), Bolts = false, Concrete = false, Misc = false });
        Assert.Empty(rep.Clashes);
        // the level chord: a vertical cut parallel to it ends at the chord face + clearance, like the square rule
        double z0 = m0.Parts.Single(x => x.Mark == "N-U2-L2").Mesh.Bounds().Min.Z, z1 = m1.Parts.Single(x => x.Mark == "N-U2-L2").Mesh.Bounds().Min.Z;
        Assert.True(z1 <= z0 + 1e-6);
        Assert.True(BridgeProject.FromJson(p1.ToJson()).Bridges[0].Truss!.JointDefaults.WebToWebClearance == 0);
    }

    [Fact]
    public void GussetEdgeAngles_AndPlateRivets()
    {
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        Assert.DoesNotContain("EdgeAngles", p.ToJson());
        // edge angle along the top edge of the U3 gusset (explicit outline +-1 m x +-0.5 m, 5/8 in.), cover splice plate with rivets at L3
        var u3 = t.Joints.First(x => x.Point == "U3");
        u3.EdgeAngles = new() { new TrussGussetEdgeAngle { Angle = "L4x3-1/2x7/16", U0 = -0.6, V0 = 0.5, U1 = -0.1, V1 = 0.5, Rivets = 4, RivetDiameter = In(0.875), Unresolved = "UNRESOLVED (p.0): edge angle - test." } };
        var j = new TrussJointDefinition
        {
            Point = "L3", Plane = "N",
            Plates = new() { new TrussJointPlate { Face = TrussPlateFace.Top, Width = In(18), Thickness = In(0.5), Length = In(24), RivetCount = 10, RivetRows = 2, RivetDiameter = In(0.875), Label = "cover splice" } },
        };
        t.Joints.Add(j);
        Assert.Contains("EdgeAngles", p.ToJson());
        Assert.Contains("RivetCount", p.ToJson());
        var s = Regen(p).Steel!;
        var ea = s.Parts.Where(x => x.Mark.StartsWith("N-EAU3-")).ToList();
        Assert.Equal(2, ea.Count);                                     // one per gusset face
        Assert.All(ea, x => Assert.Equal(PartCategory.TrussConnectionAngle, x.Category));
        Assert.All(ea, x => Assert.NotNull(x.Assumption));
        double len = 0.5;
        Assert.Equal(len, ea[0].Length, 6);
        // rivets: 4 per angle through the angle + gusset (probed grip >= angle + gusset), 10 through the cover splice plate (grip >= plate + cover)
        var riv = s.Bolts.Where(x => x.Kind == BoltKind.Rivet).ToList();
        var eb = ea.Select(x => x.Mesh.Bounds()).ToList();
        var onAngles = riv.Where(r => eb.Any(q => r.Frame.Origin.X > q.Min.X - In(1) && r.Frame.Origin.X < q.Max.X + In(1) && r.Frame.Origin.Z > q.Min.Z - In(1) && r.Frame.Origin.Z < q.Max.Z + In(1)) && Math.Abs(r.Frame.Z.Z) < 0.5).ToList();
        Assert.True(onAngles.Count >= 8, $"{onAngles.Count} edge angle rivets");
        Assert.All(onAngles, r => Assert.True(r.Grip >= In(7.0 / 16) + In(0.625) - 1e-6, $"grip {r.Grip / 0.0254}"));
        var plate = s.Parts.Single(x => x.Description.StartsWith("cover splice"));
        var pb = plate.Mesh.Bounds();
        var onPlate = riv.Where(r => r.Frame.Origin.X > pb.Min.X - 1e-3 && r.Frame.Origin.X < pb.Max.X + 1e-3 && r.Frame.Origin.Y > pb.Min.Y - 1e-3 && r.Frame.Origin.Y < pb.Max.Y + 1e-3 && Math.Abs(r.Frame.Z.Z) > 0.99).ToList();
        Assert.Equal(10, onPlate.Count);
        Assert.All(onPlate, r => Assert.True(r.Grip >= In(1.0) - 1e-6));      // 1/2 plate + 1/2 cover at least
        Assert.Single(BridgeProject.FromJson(p.ToJson()).Bridges[0].Truss!.Joints.First(x => x.Point == "U3").EdgeAngles!);
    }

    [Fact]
    public void DroppedDrawnRivets_AreRecordedWithAReason()
    {
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        var j = t.Joints.First(x => x.Point == "L2" && x.Plane == "N");
        j.Rivets.Clear();
        // two positions: one on the diagonal U1-L2 near the point, one far outside any member
        j.Rivets.Add(new TrussRivetGroup { Member = "U1-L2", Count = 2, Diameter = In(0.875), Positions = new() { new TrussUV(-In(20), In(20)), new TrussUV(In(200), -In(200)) } });
        var tm = Regen(p).Steel!.Truss!;
        Assert.Contains(tm.DroppedRivets, d => d.Point == "L2" && d.Member == "N-U1-L2" && d.Reason.Length > 0);
    }
}
