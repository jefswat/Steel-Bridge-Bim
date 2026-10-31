using SBM.Core.Bridge;
using SBM.Core.Project;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>PTC main span rivet agent review (2026-10-30): high-strength bolts on end connection outstanding legs (R08), joint rivets of different groups
/// sharing one hole (R13), inscribed rivet heads (touching heads do not overlap).</summary>
public class TrussRivetReviewTests
{
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    [Fact]
    public void EndConnection_OutstandingLegBolts_AreStructuralBolts()
    {
        var (p, b) = TrussTests.Model();
        var fb = b.Truss!.Members.First(m => m.Name == "FB2");
        fb.EndConnections = new() { new TrussEndConnection { Label = "2-Ls 4x4x3/4", Angle = "L4x4x3/4", Top = In(14), Length = In(24), RivetDiameter = In(0.875), OutRivetDiameter = In(1),
            WebRivets = new() { new TrussRivetColumn(In(2.5), 4, In(3), In(1.5)) }, OutRivets = new() { new TrussRivetColumn(In(2.5), 4, In(3), In(1.5)) } } };
        var s0 = Regen(p).Steel!;
        int r0 = s0.Bolts.Count(x => x.Kind == BoltKind.Rivet && Math.Abs(x.Diameter - In(1)) < 1e-9);
        Assert.True(r0 > 0);
        Assert.DoesNotContain("OutBoltMaterial", p.ToJson());
        fb.EndConnections[0].OutBoltMaterial = "ASTM A325";
        Assert.Contains("OutBoltMaterial", p.ToJson());
        var s1 = Regen(p).Steel!;
        Assert.Equal(0, s1.Bolts.Count(x => x.Kind == BoltKind.Rivet && Math.Abs(x.Diameter - In(1)) < 1e-9));
        var bolts = s1.Bolts.Where(x => x.Kind == BoltKind.Structural && x.Material == "ASTM A325").ToList();
        Assert.Equal(r0, bolts.Count);
        Assert.All(bolts, x => Assert.True(x.Length > x.Grip));
    }

    [Fact]
    public void JointRivetsShareHoles_MergesOverlappingShanksOnly()
    {
        var (p, b) = TrussTests.Model();
        Assert.DoesNotContain("JointRivetsShareHoles", p.ToJson());
        b.Truss!.JointRivetsShareHoles = true;
        Assert.Contains("JointRivetsShareHoles", p.ToJson());
        Assert.True(BridgeProject.FromJson(p.ToJson()).Bridges[0].Truss!.JointRivetsShareHoles);
        BoltInstance R(double x, double y, double z0, double grip, double d = 0.0254) =>
            new() { Frame = new SBM.Core.Solids.Frame3(new SBM.Core.Geometry.Vec3(x, y, z0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)), Assembly = "T", Grip = grip, Length = grip, Kind = BoltKind.Rivet, Diameter = d };
        var bolts = new List<BoltInstance>
        {
            R(0, 0, 0, 0.05),                 // placed before (index < from)
            R(0.02, 0, 0.03, 0.05),           // same hole (0.02 m < 0.0254), shanks overlap -> merged into #0, grip 0 .. 0.08
            R(1, 0, 0, 0.05),
            R(1, 0, 0.05, 0.05),              // coaxial but only touching end to end -> kept
            R(2, 0, 0, 0.05), R(2.03, 0, 0, 0.05),   // 0.03 m apart > 0.0254 -> kept (heads may touch, holes do not)
        };
        int merged = SBM.Core.Truss.RivetHoles.MergeShared(bolts, 1);
        Assert.Equal(1, merged);
        Assert.Equal(5, bolts.Count);
        Assert.Equal(0.08, bolts[0].Grip, 2);
    }

    [Fact]
    public void RivetHeads_Inscribed_InTheButtonHeadCircle()
    {
        var m = SteelModel.RivetMesh(In(1), In(2));
        double r = m.Positions.Where(q => q.Z < -1e-9).Max(q => Math.Sqrt(q.X * q.X + q.Y * q.Y));
        Assert.Equal(0.8 * In(1), r, 9);
    }

    [Fact]
    public void RivetLineZones_PitchPerZone()
    {
        var (p, b) = TrussTests.Model(pratt: true, extras: false);
        var m = b.Truss!.Members.First(x => x.Name == "L1-L2");
        m.RivetLines = new() { new TrussRivetLine { Label = "zones", Along = "y", X = 0, Y = 0, Pitch = In(6), FirstFromEnd = In(2), Diameter = In(1) } };
        int n0 = Regen(p).Steel!.Bolts.Count(x => x.Kind == BoltKind.Rivet);
        // first metre at 3 in. pitch, the rest 6 in.: more rivets than one pitch everywhere
        m.RivetLines[0].Zones = new() { new TrussRivetZone(0, 1.0, In(3)) };
        var bm = Regen(p);
        int n1 = bm.Steel!.Bolts.Count(x => x.Kind == BoltKind.Rivet);
        Assert.True(n1 > n0);
        Assert.Single(BridgeProject.FromJson(p.ToJson()).Bridges[0].Truss!.Members.First(x => x.Name == "L1-L2").RivetLines![0].Zones!);
    }
}
