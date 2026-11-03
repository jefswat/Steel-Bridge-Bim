using SBM.Core.Bridge;
using SBM.Core.Project;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>PTC main span agent review 2026-11-02 (gusset outlines): <see cref="TrussJointDefinition.ChordFaceEdge"/> puts the chord-side edge of a traced
/// outline on each chord's own face line (kink at the face crossing), the side edges carried along their own direction.</summary>
public class TrussChordFaceEdgeTests
{
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    [Fact]
    public void TopEdge_OnEachChordsFace_KinkedChord_OmittedWhenNone()
    {
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        // kink the top chord at U3: U4 two feet higher
        var u4 = t.Points.First(x => x.Name == "U4");
        t.Points[t.Points.IndexOf(u4)] = new TrussPointDefinition("U4", u4.Station, u4.Elevation + Ft(2));
        var j = t.Joints.First(x => x.Point == "U3");
        j.Outline = new() { new TrussUV(-1, -1), new TrussUV(1, -1), new TrussUV(1, 0.05), new TrussUV(-1, 0.05) };   // top edge traced low and level
        Assert.DoesNotContain("ChordFaceEdge", p.ToJson());
        var g0 = Regen(p).Steel!.Parts.Single(x => x.Mark == "N-GU3-L").Mesh.Bounds();
        j.ChordFaceEdge = TrussChordFaceEdge.Top;
        Assert.Contains("ChordFaceEdge", p.ToJson());
        Assert.Equal(TrussChordFaceEdge.Top, BridgeProject.FromJson(p.ToJson()).Bridges[0].Truss!.Joints.First(x => x.Point == "U3").ChordFaceEdge);
        var s = Regen(p).Steel!;
        var g = s.Parts.Single(x => x.Mark == "N-GU3-L").Mesh;
        double zU3 = Ft(60) + TrussTests.Depth, tan = Ft(2) / TrussTests.Panel, cos = 1 / Math.Sqrt(1 + tan * tan);
        // face = the web plies / angles (12 in. above the working line), not the 1/2 in. cover plate
        Assert.Equal(zU3 + In(12) / cos + 1 * tan, g.Bounds().Max.Z, 4);
        // left of the point (level chord U2-U3) the top is the level face; the bottom edge and the side edges keep their u
        double xU3 = Ft(100) + 3 * TrussTests.Panel;
        var left = g.Positions.Where(q => q.X < xU3 - 0.9).ToList();
        Assert.Equal(zU3 + In(12), left.Max(q => q.Z), 4);
        Assert.Equal(g0.Min.Z, g.Bounds().Min.Z, 6);
        Assert.Equal(g0.Min.X, g.Bounds().Min.X, 6); Assert.Equal(g0.Max.X, g.Bounds().Max.X, 6);
        // the kink vertex: the two face lines meet above the point
        Assert.Contains(g.Positions, q => Math.Abs(q.X - xU3) < 0.02 && q.Z > zU3 + In(11.9));
        Assert.Contains(s.Diagnostics, d => d.Message.Contains("top edge on the chord face"));
    }

    [Fact]
    public void BottomEdge_OnTheLowerChordFace()
    {
        var (p, b) = TrussTests.Model();
        var j = new TrussJointDefinition { Point = "L4", Outline = { new TrussUV(-1, -0.1), new TrussUV(1, -0.1), new TrussUV(1, 1), new TrussUV(-1, 1) }, ChordFaceEdge = TrussChordFaceEdge.Bottom };
        b.Truss!.Joints.Add(j);
        var g = Regen(p).Steel!.Parts.Single(x => x.Mark == "N-GL4-L").Mesh.Bounds();
        Assert.Equal(Ft(60) - In(12), g.Min.Z, 4);
        Assert.Equal(Ft(60) + 1, g.Max.Z, 4);
    }
}
