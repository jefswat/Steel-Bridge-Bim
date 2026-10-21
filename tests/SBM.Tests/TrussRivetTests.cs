using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Rivet placement (PTC d39): distributed joint groups (count exact per gusset face, inside the gusset / member lap, grip through the
/// gusset + fills + web plies), whole-joint counts split by lap area, member rivet lines (grip = connected piece stack), rivet head mass in SteelMass.</summary>
public class TrussRivetTests
{
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    [Fact]
    public void DistributedGroups_CountInsideLapAndGrip()
    {
        var (p, b) = TrussTests.Model(pratt: true, extras: false);
        var t = b.Truss!;
        t.Joints.Add(new TrussJointDefinition { Point = "L2", Plane = "S", Rivets = { new TrussRivetGroup { Member = "L2-L3", Count = 20, Diameter = In(0.875), Distributed = true, Unresolved = "UNRESOLVED (p.0): rivet positions - test." } } });
        t.Joints.Add(new TrussJointDefinition { Point = "L4", Plane = "S", Rivets = { new TrussRivetGroup { Member = "*", Count = 31, Diameter = In(0.875), Distributed = true } } });
        var bm = Regen(p);
        var steel = bm.Steel!;
        var rv = steel.Bolts.Where(x => x.Kind == BoltKind.Rivet).ToList();
        Assert.Equal(2 * 20 + 2 * 31, rv.Count);
        Assert.All(rv, r => Assert.Equal(In(0.875), r.Diameter, 9));

        // L2 group: on chord L2-L3 (toward +X from L2), inside the gusset plates of S-L2, grip = gusset 3/4 + (gusset inside face 10" - web inner face 9 1/2")
        var tm = steel.Truss!;
        var l2 = tm.Point("S", "L2")!.World;
        var gus = steel.Parts.Where(x => x.Mark is "S-GL2-L" or "S-GL2-R").ToList();
        Assert.Equal(2, gus.Count);
        var g20 = rv.Where(r => Math.Abs(r.Frame.Origin.X - l2.X) < 3 && Math.Abs(r.Frame.Origin.Y - l2.Y) < 1).ToList();
        Assert.Equal(40, g20.Count);
        foreach (var r in g20)
        {
            Assert.True(r.Frame.Origin.X > l2.X, "rivet lies on the L2-L3 side");
            Assert.True(gus.Any(gp => { var bb = gp.Mesh.Bounds(); var o = r.Frame.Origin; return o.X >= bb.Min.X - 1e-3 && o.X <= bb.Max.X + 1e-3 && o.Z >= bb.Min.Z - 1e-3 && o.Z <= bb.Max.Z + 1e-3 && o.Y >= bb.Min.Y - 1e-3 && o.Y <= bb.Max.Y + 1e-3; }),
                "rivet head face on a gusset plate");
            // + the inside angle's leg on the ply where the rivet lies on it (clean plate stack, 2026-10-21)
            Assert.True(Math.Abs(r.Grip - (In(0.75) + In(0.5))) < 1e-6 || Math.Abs(r.Grip - (In(0.75) + In(0.5) + In(0.5))) < 1e-6, $"grip {r.Grip / 0.0254}");
            // within the web depth of the chord (24") about the working line
            Assert.True(Math.Abs(r.Frame.Origin.Z - l2.Z) <= In(12) - 1.5 * In(0.875) + 1e-6);
            Assert.NotNull(r.Assumption);
            Assert.True(r.Assumption!.DimensionOnly);
        }
        // no two rivets of a face closer than the grid
        var face = g20.Where(r => r.Frame.Z.Y > 0).ToList();
        Assert.Equal(20, face.Count);
        double minD = face.SelectMany((a, i) => face.Skip(i + 1).Select(c => (a.Frame.Origin - c.Frame.Origin).Length)).Min();
        Assert.True(minD > 1.6 * In(0.875), $"rivet heads overlap ({minD})");

        // whole-joint count at L4 split over the members framing in (chords, vertical, diagonal); total exact
        var l4 = tm.Point("S", "L4")!.World;
        Assert.Equal(62, rv.Count(r => (r.Frame.Origin - l4).Length < 3));

        // head mass is part of the steel weight
        Assert.Equal(rv.Count * SteelModel.RivetHeadMassOf(In(0.875)), steel.RivetHeadMass, 6);
    }

    [Fact]
    public void RivetLines_GripFollowsThePieces()
    {
        var (p, b) = TrussTests.Model(pratt: true, extras: false);
        var t = b.Truss!;
        var m = t.Members.First(x => x.Name == "L1-L2");     // PL 16 x 12 section
        m.RivetLines = new() { new TrussRivetLine { Label = "test", Along = "y", X = 0, Y = 0, Pitch = In(6), FirstFromEnd = In(2), Diameter = In(1), Unresolved = "UNRESOLVED (p.0): rule-based - test." } };
        var bm = Regen(p);
        var rv = bm.Steel!.Bolts.Where(x => x.Kind == BoltKind.Rivet).ToList();
        var res = bm.Steel.Truss!.Member("N-L1-L2")!;
        int expect = (int)Math.Floor((res.CutLength - 2 * In(2)) / In(6) + 1e-9) + 1;
        Assert.Equal(2 * expect, rv.Count);                    // both planes
        Assert.All(rv, r => Assert.Equal(In(12), r.Grip, 6));
        Assert.All(rv, r => Assert.True(Math.Abs(r.Frame.Z.Z) > 0.99));   // shank vertical (section y)
    }
}
