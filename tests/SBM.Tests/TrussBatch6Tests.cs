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

    /// <summary>Agent review 2 (2026-11-03, floor laterals): OPTIONAL pitched / rolled frames and a rivet gap tolerance on plane items.</summary>
    [Fact]
    public void PlaneItemFrame_Pitch_RivetGapTolerance()
    {
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        var l3 = t.Points.First(x => x.Name == "L3");
        double z0 = l3.Elevation + Ft(40), off = -Ft(3), pitch = 2.0;
        List<TrussUV> Sq(double h) => new() { new(-h, -h), new(h, -h), new(h, h), new(-h, h) };
        // a level 1/2 in. plate and, 1/16 in. above it, a 3/8 plate pitched 2 deg (its far edge rises): rivets at the plate centre with / without a gap tolerance
        var lo = new TrussPlaneItemDefinition { Name = "LO", Kind = TrussPlaneItemKind.Plate, Frame = new TrussItemFrame { Station = l3.Station, Offset = off, Elevation = z0, Horizontal = true },
            Outline = Sq(In(6)), W0 = -In(0.5), W1 = 0, Category = PartCategory.TrussConnectionPlate };
        TrussPlaneItemDefinition Up(string n, double tol) => new() { Name = n, Kind = TrussPlaneItemKind.Plate,
            Frame = new TrussItemFrame { Station = l3.Station + (n == "UPT" ? Ft(4) : 0), Offset = off, Elevation = z0, Horizontal = true, PitchDegrees = pitch },
            Outline = Sq(In(6)), W0 = In(1.0 / 16), W1 = In(1.0 / 16 + 0.375), Category = PartCategory.TrussConnectionPlate, RivetGapTolerance = tol,
            Rivets = new() { new TrussPlaneItemRivet { U = 0, V = 0, W = In(0.25), Diameter = In(0.875) } } };
        var loT = System.Text.Json.JsonSerializer.Deserialize<TrussPlaneItemDefinition>(System.Text.Json.JsonSerializer.Serialize(lo))!; loT.Name = "LOT"; loT.Frame!.Station += Ft(4);
        t.PlaneItems = new() { lo, Up("UP", 0), loT, Up("UPT", In(1.0 / 8)) };
        var json = p.ToJson();
        Assert.Contains("\"PitchDegrees\"", json); Assert.Contains("\"RivetGapTolerance\"", json);
        Assert.DoesNotContain("\"RollDegrees\"", json);
        var bm = Regen(p);
        var st = bm.Steel!;
        // the pitched plate rises along U: its bounds' height = thickness / cos + width x sin
        var up = st.Parts.Single(x => x.Mark == "UP").Mesh.Bounds();
        Assert.Equal(In(0.375) / Math.Cos(pitch * Math.PI / 180) + In(12) * Math.Sin(pitch * Math.PI / 180), up.Max.Z - up.Min.Z, 4);
        // rivet without tolerance: the upper plate only (3/8 / cos); with 1/8 in. tolerance: both plates and the 1/16 gap
        var rv = st.Bolts.Where(x => x.Kind == BoltKind.Rivet && Math.Abs(x.Frame.Origin.Z - z0) < 0.1).OrderBy(x => x.Grip).ToList();
        Assert.Equal(2, rv.Count);
        Assert.Equal(In(0.375), rv[0].Grip, 2);
        Assert.Equal(In(0.5 + 1.0 / 16 + 0.375), rv[1].Grip, 2);
        Assert.DoesNotContain("\"PitchDegrees\"", System.Text.Json.JsonSerializer.Serialize(new TrussItemFrame()));
        Assert.DoesNotContain("\"RivetGapTolerance\"", System.Text.Json.JsonSerializer.Serialize(new TrussPlaneItemDefinition()));
    }
}
