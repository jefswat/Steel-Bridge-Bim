using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>PTC main span fix batch 6 (2026-10-28): truss plane items in an explicit frame (station / offset / elevation, vertical or horizontal plane),
/// with their own part category, assembly kind and density (wire-rope handlines, ladder cage hoops).</summary>
public class TrussBatch6Tests
{
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    [Fact]
    public void PlaneItemFrame_VerticalAndHorizontal_CategoryAssemblyDensity()
    {
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        var l3 = t.Points.First(x => x.Name == "L3");
        double z0 = l3.Elevation + Ft(30), off = -Ft(2);
        var sq = new List<TrussUV> { new() { U = -In(0.25), V = -In(0.25) }, new() { U = In(0.25), V = -In(0.25) }, new() { U = In(0.25), V = In(0.25) }, new() { U = -In(0.25), V = In(0.25) } };
        t.PlaneItems = new()
        {
            // rope 1/2 in. square along the alignment, 3 ft up over 20 ft
            new TrussPlaneItemDefinition { Name = "ROPE", Kind = TrussPlaneItemKind.Sweep, Frame = new TrussItemFrame { Station = l3.Station, Offset = off, Elevation = z0 },
                Path = { new(0, 0), new(Ft(20), Ft(3)) }, Section = sq, Category = PartCategory.InspectionCable, AssemblyKind = AssemblyKind.InspectionWalkway,
                Assembly = "WALKWAYS", Density = 5000 },
            // hoop in a horizontal plane: U = across (90 deg toward the right), V = to the left of U = up-station, W = up; legs 1 ft + semicircle R 1 ft
            new TrussPlaneItemDefinition { Name = "HOOP", Kind = TrussPlaneItemKind.Sweep, Frame = new TrussItemFrame { Station = l3.Station, Offset = off, Elevation = z0, AngleDegrees = 90, Horizontal = true },
                Path = { new(-Ft(1), 0), new(-Ft(1), Ft(2), Ft(1)), new(Ft(1), Ft(2), Ft(1)), new(Ft(1), 0) }, Section = sq, ArcStepDegrees = 10,
                Category = PartCategory.WalkwayRail, AssemblyKind = AssemblyKind.InspectionWalkway, Assembly = "LADDERS" },
        };
        var json = p.ToJson();
        foreach (var k in new[] { "\"Frame\"", "\"Category\"", "\"AssemblyKind\"", "\"Density\"" }) Assert.Contains(k, json);
        var bm = Regen(p);
        var st = bm.Steel!;
        var rope = st.Parts.Single(x => x.Mark == "ROPE"); var hoop = st.Parts.Single(x => x.Mark == "HOOP");
        Assert.Equal(PartCategory.InspectionCable, rope.Category);
        Assert.Equal(PartCategory.WalkwayRail, hoop.Category);
        Assert.Equal(AssemblyKind.InspectionWalkway, st.Assemblies.Single(a => a.Mark == rope.Assembly).Kind);
        Assert.Equal(rope.Volume * 5000, rope.Mass, 6);
        var al = p.Alignments.First(a => a.Name == b.AlignmentName);
        var rb = rope.Mesh.Bounds();
        Assert.Equal(z0 - In(0.25), rb.Min.Z, 3); Assert.Equal(z0 + Ft(3) + In(0.25), rb.Max.Z, 2);
        var c0 = al.Inverse(new Vec2((rb.Min.X + rb.Max.X) / 2, (rb.Min.Y + rb.Max.Y) / 2));
        Assert.Equal(off, c0.Offset, 2);
        Assert.Equal(l3.Station + Ft(10), c0.Station, 2);
        // hoop: flat (1/2 in. high), centred on the offset across, reaching 2 ft + R... up-station, 2 ft wide across
        var hb = hoop.Mesh.Bounds();
        Assert.Equal(In(0.5), hb.Max.Z - hb.Min.Z, 6);
        var hc = al.Inverse(new Vec2((hb.Min.X + hb.Max.X) / 2, (hb.Min.Y + hb.Max.Y) / 2));
        Assert.Equal(off, hc.Offset, 2);
        Assert.True(hc.Station > l3.Station, "hoop on the up-station side (V = left of U = across)");
        // unchanged default: no frame keys when unused
        Assert.DoesNotContain("\"Frame\"", System.Text.Json.JsonSerializer.Serialize(new TrussPlaneItemDefinition()));
        Assert.DoesNotContain("\"Density\"", System.Text.Json.JsonSerializer.Serialize(new TrussPlaneItemDefinition()));
    }
}
