using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.Core.Truss;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Truss joint details of QC pass 5 (PTC I047 / I048 / I051 / I052): mitred kinked chord joints with the joint plates ending on the joint plane,
/// web member ends clear of the joint plates, plates on one member (fills between a rolled member's flanges and the gussets, bars inside the flanges),
/// rivets placed as drawn (joint and member frame). All inputs OPTIONAL: the defaults build exactly as before.</summary>
public class TrussJointDetailTests
{
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    /// <summary>Pratt test truss with the lower chord kinked at L3 (L3 raised 2 ft).</summary>
    private static (BridgeProject p, BridgeDefinition b) Kinked(bool mitre)
    {
        var (p, b) = TrussTests.Model(extras: false);
        var t = b.Truss!;
        var l3 = t.Points.First(x => x.Name == "L3");
        t.Points[t.Points.IndexOf(l3)] = new TrussPointDefinition("L3", l3.Station, l3.Elevation + Ft(2));
        t.JointDefaults.MitredChordJoints = mitre;
        return (p, b);
    }

    [Fact]
    public void MitredChordJoints_BothEndsOnTheBisectorPlane_PlatesEndOnIt_DefaultUnchanged()
    {
        static (BridgeModel bm, Vec3 P, Vec3 n) Run(bool mitre)
        {
            var (p, b) = Kinked(mitre);
            b.Truss!.Joints.Add(new TrussJointDefinition { Point = "L3", Plane = "N", Plates = new()
            {
                new TrussJointPlate { Face = TrussPlateFace.WebOutside, Width = In(14), Thickness = In(0.25), Length = In(20), Offset = -In(10), Fill = true, Label = "left fill" },
                new TrussJointPlate { Face = TrussPlateFace.Top, Width = In(16), Thickness = In(0.5), Length = In(40), Label = "cover spl" },
            } });
            var bm = Regen(p);
            var tm = bm.Steel!.Truss!;
            var P = tm.Point("N", "L3")!.World;
            var a = tm.Member("N-L2-L3")!; var c = tm.Member("N-L3-L4")!;
            var dL = (a.A - a.B).Normalized(); var dR = (c.B - c.A).Normalized();   // away from L3
            return (bm, P, (dR - dL).Normalized());                                // bisector plane normal (toward L4)
        }
        var (m1, P, n) = Run(true);
        var (m0, _, _) = Run(false);
        double Side(SteelPart part, Func<double, double, double> pick) => part.Mesh.Positions.Select(q => Vec3.Dot(q - P, n)).Aggregate(pick);
        SteelPart Part(BridgeModel bm, string mark) => bm.Steel!.Parts.Single(x => x.Mark == mark);
        // mitred: the two chord solids meet on the plane (no overlap), each a closed solid
        Assert.True(Side(Part(m1, "N-L2-L3"), Math.Max) <= 1e-6);
        Assert.True(Side(Part(m1, "N-L3-L4"), Math.Min) >= -1e-6);
        Assert.True(Part(m1, "N-L3-L4").Mesh.Volume() > 0);
        // default (square cut + half depth x tan(defl / 2)): both ends reach past the plane - unchanged
        Assert.True(Side(Part(m0, "N-L2-L3"), Math.Max) > In(0.25));
        Assert.True(Side(Part(m0, "N-L3-L4"), Math.Min) < -In(0.25));
        Assert.True(Part(m0, "N-L3-L4").Mesh.HasExactSolids);
        // the left fill ends on the joint plane and follows the left chord; the cover splice plate is two halves meeting on it (exact extrusions)
        var fill = m1.Steel!.Parts.Single(x => x.Description.StartsWith("left fill"));
        Assert.True(Math.Abs(Side(fill, Math.Max)) < 1e-6);
        Assert.True(fill.Mesh.HasExactSolids);
        var cov = m1.Steel.Parts.Single(x => x.Description.StartsWith("cover spl"));
        Assert.True(cov.Mesh.HasExactSolids);
        Assert.Equal(2, cov.Mesh.Solids!.Count);
        Assert.Contains(m1.Steel.Diagnostics, d => d.Message.Contains("kinked chord joints cut on the bisector plane"));
        // the option is omitted from the .sbm when off
        Assert.DoesNotContain("MitredChordJoints", Kinked(false).p.ToJson());
        Assert.Contains("MitredChordJoints", Kinked(true).p.ToJson());
    }

    [Fact]
    public void WebMemberClearOfJointPlates_VerticalStopsClearOfTheCoverSplicePlate()
    {
        static double Cut(double clr)
        {
            var (p, b) = TrussTests.Model(extras: false);
            b.Truss!.JointDefaults.WebMemberClearOfJointPlates = clr;
            b.Truss.Joints.Add(new TrussJointDefinition { Point = "L3", Plane = "N", Plates = new()
            {
                new TrussJointPlate { Face = TrussPlateFace.Top, Width = In(18), Thickness = In(0.5), Length = In(30), Label = "cover spl" },
            } });
            return Regen(p).Steel!.Truss!.Member("N-U3-L3")!.CutLength;
        }
        double c0 = Cut(0), c1 = Cut(In(1.5));
        // the vertical meets the chord top square: its lower end moves up by the plate (1/2) + 1 1/2 clear; the upper end is unchanged
        Assert.Equal(In(2), c0 - c1, 6);
    }

    [Fact]
    public void MemberPlates_FillBetweenFlangeAndGusset_BarsInsideTheFlanges_ClippedToTheMember()
    {
        var (p, b) = TrussTests.Model(extras: false);
        var t = b.Truss!;
        t.Sections.Add(new TrussSectionDefinition { Name = "WV", Kind = TrussSectionKind.Rolled, Designation = "W12x50", Rotated = true });
        foreach (var m in t.Members.Where(m => m.Role == TrussMemberRole.Vertical)) m.Section = "WV";
        var w = SteelCatalog.FindWideFlange("W12x50")!;
        double gap = In(10) - w.Depth / 2;                                         // gussets at the BOX chord's 20 in out to out
        t.Joints.Add(new TrussJointDefinition { Point = "L3", Plane = "N", Plates = new()
        {
            new TrussJointPlate { Member = "U3-L3", Face = TrussPlateFace.WebOutside, Fill = true, Width = In(8), Thickness = gap - In(0.5), Length = In(30), Offset = 0, Label = "fill 1" },
            new TrussJointPlate { Member = "U3-L3", Face = TrussPlateFace.WebOutside, Fill = true, Width = In(8), Thickness = In(0.5), Length = In(30), Offset = 0, Label = "fill 2" },
            new TrussJointPlate { Member = "U3-L3", Face = TrussPlateFace.WebInside, Width = In(2), Thickness = In(0.5), Length = In(12), Offset = In(20), CenterOffset = In(2.5), Label = "bar" },
        } });
        var bm = Regen(p);
        var tm = bm.Steel!.Truss!;
        var P = tm.Point("N", "L3")!.World;
        var mem = tm.Member("N-U3-L3")!;
        var ax = (mem.A - mem.B).Normalized();                                    // away from L3 (the member runs U3 -> L3)
        var xn = new Vec3(0, 1, 0);                                               // across the truss plane (the test alignment runs along +x)
        (double lo, double hi) Ext(string label, Vec3 d) { var q = bm.Steel.Parts.Single(x => x.Description.StartsWith(label)).Mesh.Positions.Select(v => Vec3.Dot(v - P, d)).ToList(); return (q.Min(), q.Max()); }
        // fill 1 on the flange face, fill 2 on fill 1, ending at the gusset inner face; both start at the member's cut end (the plate was given from the point)
        Assert.Equal(w.Depth / 2 + gap - In(0.5), Ext("fill 1", xn).hi, 6);
        Assert.Equal(In(10), Ext("fill 2", xn).hi, 6);
        double cutEnd = (mem.WorkLength - mem.CutLength) / 2;                     // equal setbacks at both chords (square vertical)
        Assert.Equal(cutEnd, Ext("fill 1", ax).lo, 6);
        // the bar lies on the inner face of the flange, above the web
        Assert.Equal(w.Depth / 2 - w.FlangeThickness, Ext("bar", xn).hi, 6);
        Assert.True(bm.Steel.Parts.Where(x => x.Description.StartsWith("fill")).All(x => x.Mesh.HasExactSolids));
        Assert.DoesNotContain("\"CenterOffset\"", TrussTests.Model(extras: false).p.ToJson());
    }

    [Fact]
    public void RivetPositions_AsDrawn_JointAndMemberFrame_OffTheMemberDropped()
    {
        var (p, b) = TrussTests.Model(extras: false);
        var t = b.Truss!;
        // joint frame: three rivets on the L2-L3 chord web field (y = 0 = the chord axis), one far below the chord (no member there: dropped)
        var g1 = new TrussRivetGroup { Member = "L2-L3", Count = 4, Diameter = In(0.875), Positions = new() { new(In(6), 0), new(In(9), 0), new(In(12), In(3)), new(In(12), -In(40)) } };
        // member frame of the same chord (along the member from L2 toward L3, section y)
        var g2 = new TrussRivetGroup { Member = "L2-L3", Count = 2, Diameter = In(0.875), PositionsInMemberFrame = true, Positions = new() { new(In(15), In(-3)), new(In(18), In(-3)) } };
        t.Joints.Add(new TrussJointDefinition { Point = "L2", Plane = "N", Rivets = { g1, g2 } });
        var bm = Regen(p);
        var rv = bm.Steel!.Bolts.Where(x => x.Kind == BoltKind.Rivet).ToList();
        Assert.Equal(2 * (3 + 2), rv.Count);
        var P = bm.Steel.Truss!.Point("N", "L2")!.World;
        // heads where drawn: u along the alignment (+x), v up
        var uv = rv.Select(x => (u: (x.Frame.Origin.X - P.X) / 0.0254, v: (x.Frame.Origin.Z - P.Z) / 0.0254)).ToList();
        foreach (var (u, v) in new[] { (6.0, 0.0), (9.0, 0.0), (12.0, 3.0), (15.0, -3.0), (18.0, -3.0) })
            Assert.Equal(2, uv.Count(q => Math.Abs(q.u - u) < 1e-6 && Math.Abs(q.v - v) < 1e-6));
        Assert.Contains(bm.Steel.Diagnostics, d => d.Message.Contains("1 of 4 drawn rivet positions"));
        Assert.Contains(bm.Steel.Diagnostics, d => d.Message.Contains("placed as drawn"));
        var json = p.ToJson();
        Assert.Contains("\"PositionsInMemberFrame\"", json);
        Assert.DoesNotContain("\"Positions\"", TrussTests.Model().p.ToJson());
    }
}
