using SBM.Core.Bridge;
using SBM.Core.Project;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>PTC main span fix batch 5 (2026-10-28): lateral / strut bracing plates on the members they connect (member plane, rolled members, beveled
/// fills, rivet lines across each member, probed faces), explicit section pieces (built-up portal strut).</summary>
public class TrussBatch5Tests
{
    private const double Half = 4.572;
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    /// <summary>K of two laterals N:L2 / S:L2 -> CL point of strut 3 (as TrussTests.BracingCornerClearance_*), lateral section <paramref name="latSec"/>,
    /// apex point <paramref name="dz"/> below the chord level.</summary>
    private static (BridgeProject p, TrussDefinition t) K(string latSec, double dz)
    {
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        t.Sections.Add(new TrussSectionDefinition { Name = "PL8", Kind = TrussSectionKind.Plate, Width = In(12), Depth = In(8) });
        var l3 = t.Points.First(x => x.Name == "L3");
        t.Points.Add(new TrussPointDefinition("LM3", l3.Station, l3.Elevation - dz) { Offset = Half, Planes = new() { "N" } });
        t.Members.First(m => m.Name == "STRUT3").Section = "PL";
        t.Members.Add(new TrussMemberDefinition { Name = "KA", From = "N:L2", To = "N:LM3", Section = latSec, Role = TrussMemberRole.LowerLateral });
        t.Members.Add(new TrussMemberDefinition { Name = "KB", From = "S:L2", To = "N:LM3", Section = latSec, Role = TrussMemberRole.LowerLateral });
        t.JointDefaults.BracingCornerClearance = In(0.5);
        t.JointDefaults.BracingStackClearance = In(4);
        t.BracingGussets = new()
        {
            new TrussBracingGussetDefinition { Point = "N:LM3", Thickness = In(0.5), Side = TrussBracingPlateSide.Below, Orientation = TrussBracingPlateOrientation.Fitted,
                RivetsPerMember = 6, RivetPitch = In(3), RivetGage = In(10), RivetDiameter = In(0.875) },
        };
        return (p, t);
    }

    [Fact]
    public void BevelFills_RivetLinesAcrossTheMember_ProbedOnePartRivetsNotPlaced()
    {
        // laterals 8 in. deep under a 12 in. strut: the plate lies on the strut bottom, the laterals stand 2 in. off it
        var (p0, _) = K("PL8", 0);
        var s0 = Regen(p0).Steel!;
        Assert.DoesNotContain(s0.Parts, x => x.Category == PartCategory.FillerPlate);
        var (p1, t1) = K("PL8", 0);
        var bg = t1.BracingGussets![0];
        Assert.DoesNotContain("MemberFills", p1.ToJson());
        bg.MemberFills = true; bg.RivetGageEdge = In(2); bg.ProbeFaces = true; bg.RivetGapTolerance = In(1.0 / 8);
        var json = p1.ToJson();
        foreach (var k in new[] { "MemberFills", "RivetGageEdge", "ProbeFaces", "RivetGapTolerance" }) Assert.Contains(k, json);
        var s1 = Regen(p1).Steel!;
        var fills = s1.Parts.Where(x => x.Category == PartCategory.FillerPlate && x.Mark.StartsWith("N-BGLM3")).ToList();
        Assert.Equal(2, fills.Count);                                                     // one per lateral
        foreach (var f in fills) Assert.InRange(f.Mesh.Bounds().Max.Z - f.Mesh.Bounds().Min.Z, In(2) - In(1.0 / 32), In(2) + In(1.0 / 32));
        // every placed bracing rivet grips plate + fill + member (or plate + strut): more than the plate alone
        var plate = s1.Parts.Single(x => x.Mark == "N-BGLM3");
        var pb = plate.Mesh.Bounds();
        var riv = s1.Bolts.Where(r => r.Kind == BoltKind.Rivet && r.Frame.Origin.X > pb.Min.X - 0.05 && r.Frame.Origin.X < pb.Max.X + 0.05 && r.Frame.Origin.Y > pb.Min.Y - 0.05 && r.Frame.Origin.Y < pb.Max.Y + 0.05).ToList();
        Assert.NotEmpty(riv);
        Assert.All(riv, r => Assert.True(r.Grip > In(0.5) + In(1.0 / 32), $"grip {r.Grip / 0.0254:0.000}"));
        Assert.Contains(riv, r => r.Grip >= In(0.5 + 2 + 0.25));                          // through plate + 2 in. fill + into the lateral
        Assert.True(BridgeProject.FromJson(json).Bridges[0].Truss!.BracingGussets![0].MemberFills);
    }

    [Fact]
    public void RollToLateralPlane_MemberPlane_PlateFlatOnTheMembers()
    {
        // apex 2 ft below the chord: the laterals slope; rolled into their plane + the plate in the member plane = no wedge left, no fill needed
        int Fills(bool roll)
        {
            var (p, t) = K("PL", In(24));
            var bg = t.BracingGussets![0];
            bg.MemberFills = true; bg.MemberPlane = true;
            if (roll) foreach (var m in t.Members.Where(m => m.Name is "KA" or "KB" or "STRUT3")) m.RollToLateralPlane = true;
            Assert.Equal(roll, p.ToJson().Contains("RollToLateralPlane"));
            return Regen(p).Steel!.Parts.Count(x => x.Category == PartCategory.FillerPlate && x.Mark.StartsWith("N-BGLM3"));
        }
        Assert.True(Fills(false) > 0);
        Assert.Equal(0, Fills(true));
    }

    [Fact]
    public void CustomPieces_SectionFromExplicitPolygons()
    {
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        Assert.DoesNotContain("CustomPieces", p.ToJson());
        // skewed box 30 x 24 in. of four 1/2 in. plates (top sloped), flagged
        TrussUV U(double x, double y) => new(In(x), In(y));
        var sd = new TrussSectionDefinition { Name = "SKEWBOX", Kind = TrussSectionKind.Nominal, Width = In(30), Depth = In(24), Unresolved = "UNRESOLVED (p.0): test box - corners scaled.",
            CustomPieces = new()
            {
                new() { Label = "PL left", Polygon = { U(-15, -12), U(-14.5, -12), U(-14.5, 10), U(-15, 10) } },
                new() { Label = "PL right", Polygon = { U(14.5, -12), U(15, -12), U(15, 14), U(14.5, 14) } },
                new() { Label = "PL bottom", Polygon = { U(-14.5, -12), U(14.5, -12), U(14.5, -11.5), U(-14.5, -11.5) } },
                new() { Label = "PL top", Kind = "plate", Polygon = { U(-14.5, 9.5), U(14.5, 13.5), U(14.5, 14), U(-14.5, 10) } },
            } };
        t.Sections.Add(sd);
        t.Members.First(m => m.Name == "STRUT3").Section = "SKEWBOX";
        Assert.Contains("CustomPieces", p.ToJson());
        var s = Regen(p).Steel!;
        var parts = s.Parts.Where(x => x.Mark == "STRUT3" || x.Member == "STRUT3").ToList();
        Assert.NotEmpty(parts);
        double vol = parts.Sum(x => x.Mesh.Volume());
        double area = In(0.5) * In(22) + In(0.5) * In(26) + In(29) * In(0.5) + In(29) * In(0.5);
        var cut = s.Truss!.Member("STRUT3")!.CutLength;
        Assert.InRange(vol / (area * cut), 0.97, 1.03);
        Assert.All(parts, x => Assert.NotNull(x.Assumption));
        Assert.Equal(4, BridgeProject.FromJson(p.ToJson()).Bridges[0].Truss!.Section("SKEWBOX")!.CustomPieces!.Count);
    }
}
