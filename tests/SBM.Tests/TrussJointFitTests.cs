using SBM.Core.Bridge;
using SBM.Core.Project;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>PTC main span agent review 2026-11-02 (joint fit): split gussets at a closure joint, diagonals abutting a vertical that runs to the chord,
/// short verticals set back square, chords abutting a post.</summary>
public class TrussJointFitTests
{
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    private static List<SBM.Core.Checks.ClashResult> WebClashes(SteelModel s)
    {
        var items = s.Parts.Where(x => x.Category is PartCategory.TrussDiagonal or PartCategory.TrussVertical or PartCategory.TrussChord)
            .Select(x => new SBM.Core.Checks.ClashItem(x.Category.ToString(), x.Mark, x.Assembly, x.Description, x.Mesh)).ToList();
        return SBM.Core.Checks.SteelClashCheck.Run("t", items, new SBM.Core.Checks.ClashOptions { Tolerance = In(1.0 / 64), Bolts = false, Concrete = false, Misc = false })
            .Clashes.Where(c => c.A.Mark.StartsWith("N-") && c.B.Mark.StartsWith("N-") && (c.A.Mark.Contains("L2") || c.B.Mark.Contains("L2"))).ToList();
    }

    [Fact]
    public void SplitGusset_TwoPlatesPerFace_WithTheGap()
    {
        var (p, b) = TrussTests.Model();
        var j = b.Truss!.Joints.First(x => x.Point == "U3");
        Assert.DoesNotContain("GussetSplitGap", p.ToJson());
        j.GussetSplitGap = In(0.125);
        Assert.Contains("GussetSplitGap", p.ToJson());
        var s = Regen(p).Steel!;
        Assert.DoesNotContain(s.Parts, x => x.Mark == "N-GU3-L" || x.Mark == "N-GU3-R");
        var a = s.Parts.Single(x => x.Mark == "N-GU3-LA"); var c = s.Parts.Single(x => x.Mark == "N-GU3-LB");
        Assert.Single(s.Parts, x => x.Mark == "N-GU3-RA"); Assert.Single(s.Parts, x => x.Mark == "N-GU3-RB");
        // the alignment runs along +X: the lower station half ends gap / 2 before the point, the other starts gap / 2 after it
        double px = Ft(100) + 3 * TrussTests.Panel;
        Assert.Equal(px - In(0.0625), a.Mesh.Bounds().Max.X, 4);
        Assert.Equal(px + In(0.0625), c.Mesh.Bounds().Min.X, 4);
        Assert.Equal(px - 1, a.Mesh.Bounds().Min.X, 4);
        Assert.Equal(In(0.125), BridgeProject.FromJson(p.ToJson()).Bridges[0].Truss!.Joints.First(x => x.Point == "U3").GussetSplitGap, 9);
    }

    [Fact]
    public void WebsAbutAt_DiagonalCutOnTheVerticalSide_VerticalKeepsItsEnd()
    {
        var (p0, b0) = TrussTests.Model();
        b0.Truss!.JointDefaults.WebToWebClearance = 0;
        var (p1, b1) = TrussTests.Model();
        b1.Truss!.JointDefaults.WebToWebClearance = 0;
        var v = b1.Truss.Members.First(m => m.Name == "U2-L2");
        Assert.DoesNotContain("WebsAbutAt", p1.ToJson());
        v.WebsAbutAt = new() { "L2" };
        Assert.Contains("WebsAbutAt", p1.ToJson());
        var s0 = Regen(p0).Steel!; var s1 = Regen(p1).Steel!;
        Assert.Empty(WebClashes(s1));
        // the vertical keeps (at least) its end: never shorter than with the setback / corner cut rule
        Assert.True(s1.Parts.Single(x => x.Mark == "N-U2-L2").Mesh.Bounds().Min.Z <= s0.Parts.Single(x => x.Mark == "N-U2-L2").Mesh.Bounds().Min.Z + 1e-6);
        Assert.Single(BridgeProject.FromJson(p1.ToJson()).Bridges[0].Truss!.Members.First(m => m.Name == "U2-L2").WebsAbutAt!);
    }

    [Fact]
    public void SetBackClearAt_VerticalSetBackSquare_DiagonalUntouched()
    {
        var (p0, _) = TrussTests.Model();
        var (p1, b1) = TrussTests.Model();
        b1.Truss!.JointDefaults.WebToWebClearance = 0;
        var v = b1.Truss.Members.First(m => m.Name == "U2-L2");
        v.SetBackClearAt = new() { "L2" };
        Assert.Contains("SetBackClearAt", p1.ToJson());
        var s0 = Regen(p0).Steel!; var s1 = Regen(p1).Steel!;
        Assert.Empty(WebClashes(s1));
        var d0 = s0.Parts.Single(x => x.Mark == "N-U1-L2").Mesh.Bounds(); var d1 = s1.Parts.Single(x => x.Mark == "N-U1-L2").Mesh.Bounds();
        Assert.Equal(d0.Min.Z, d1.Min.Z, 6); Assert.Equal(d0.Max.X, d1.Max.X, 6);
        Assert.True(s1.Parts.Single(x => x.Mark == "N-U2-L2").Mesh.Bounds().Min.Z >= s0.Parts.Single(x => x.Mark == "N-U2-L2").Mesh.Bounds().Min.Z - 1e-6);
    }

    [Fact]
    public void ChordsAbutAt_ChordEndsOnThePostSides()
    {
        var (p, b) = TrussTests.Model();
        b.Truss!.JointDefaults.WebToWebClearance = 0;
        var v = b.Truss.Members.First(m => m.Name == "U2-L2");
        v.ChordsAbutAt = new() { "L2" };
        Assert.Contains("ChordsAbutAt", p.ToJson());
        var s = Regen(p).Steel!;
        var vb = s.Parts.Single(x => x.Mark == "N-U2-L2").Mesh.Bounds();
        Assert.True(s.Parts.Single(x => x.Mark == "N-L1-L2").Mesh.Bounds().Max.X <= vb.Min.X + 1e-6);
        Assert.True(s.Parts.Single(x => x.Mark == "N-L2-L3").Mesh.Bounds().Min.X >= vb.Max.X - 1e-6);
    }
}
