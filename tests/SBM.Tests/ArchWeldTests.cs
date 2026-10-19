using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Generic T-joint weld finder (<see cref="WeldFinder"/>) and the data-driven arch weld rules (<see cref="ArchWeldRule"/>).</summary>
public class ArchWeldTests
{
    /// <summary>Plate B (thickness 1, width 10 along x, height 5) standing on the top of plate A (20 x 20 x 2) at x0..x0+10.</summary>
    private static (Mesh A, Mesh B) Tee(double x0 = 5, double bWidth = 10)
    {
        var a = new Mesh(); var b = new Mesh();
        var fa = new Frame3(Vec3.Zero, new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1));
        PlateBuilder.AddPrism(a, new List<Vec2> { new(0, 0), new(20, 0), new(20, 20), new(0, 20) }, Array.Empty<Hole>(), (x, y, z) => fa.ToWorld(x, y, z), _ => 0, _ => 2);
        PlateBuilder.AddPrism(b, new List<Vec2> { new(x0, 9.5), new(x0 + bWidth, 9.5), new(x0 + bWidth, 10.5), new(x0, 10.5) }, Array.Empty<Hole>(), (x, y, z) => fa.ToWorld(x, y, z), _ => 2, _ => 7);
        return (a, b);
    }

    [Fact]
    public void TJoint_BothSidesAlongTheContact_OneSideByPredicate()
    {
        var (a, b) = Tee();
        var beads = WeldFinder.TJoint(a, b, 1e-3, out double over);
        // Plate B stands on A: the two long edges (10 long) of its base, one per side of the plate; the plate ends are not welded.
        Assert.Equal(2, beads.Count);
        Assert.All(beads, x => Assert.Equal(10, (x.P1 - x.P0).Length, 6));
        Assert.Equal(0, over, 9);
        Assert.Equal(20, WeldFinder.AddBeads(new Mesh(), beads, 0.3), 6);
        // One side only: the side facing +y.
        var one = WeldFinder.TJoint(a, b, 1e-3, out _, 3, false, (_, n) => n.Y > 0);
        Assert.Single(one);
        Assert.True(one[0].P0.Y > 10 - 1e-9 && one[0].P1.Y > 10 - 1e-9);
    }

    [Fact]
    public void TJoint_PlateOverhangingTheBase_WeldsOnlyTheContactAndReportsTheOverhang()
    {
        // B starts 5 before the end of A (x0 = 15, width 10): 5 on A, 5 beyond.
        var (a, b) = Tee(15, 10);
        var beads = WeldFinder.TJoint(a, b, 1e-3, out double over);
        Assert.Equal(2, beads.Count);
        Assert.All(beads, x => Assert.Equal(5, (x.P1 - x.P0).Length, 2));
        Assert.Equal(10, over, 2);                                   // 5 per side hangs past A
        // Gap larger than the tolerance: no weld.
        var (a2, b2) = Tee();
        Assert.Empty(WeldFinder.TJoint(a2, Shift(b2, 0, 0, 0.01), 1e-3));
    }

    private static Mesh Shift(Mesh m, double dx, double dy, double dz)
    {
        var r = new Mesh();
        r.Positions.AddRange(m.Positions.Select(p => new Vec3(p.X + dx, p.Y + dy, p.Z + dz)));
        r.Indices.AddRange(m.Indices);
        return r;
    }

    [Fact]
    public void BracingGusset_MitredWeldedEdge_IsAFacetedSolid_UnmitredKeepsTheExactArcs()
    {
        foreach (bool mitre in new[] { false, true })
        {
            var (p, b) = ArchTests.Model();
            var c = ArchBracingConnectionTests.Connection();
            c.MitreWeldedEdge = mitre;
            b.Arch!.BracingConnections.Add(c);
            var (_, m) = ArchTests.Generate(p);
            var gus = m.Steel.Parts.Where(q => q.Category == PartCategory.Gusset).ToList();
            Assert.Equal(2, gus.Count);
            Assert.All(gus, g => Assert.Equal(!mitre, g.Mesh.HasExactSolids));
            Assert.All(gus, g => Assert.True(g.Mesh.Volume() > 0));
            Assert.Equal(mitre, m.Steel.Diagnostics.Any(d => d.Message.Contains("welded edge cut to the connection plate face")));
        }
    }

    [Fact]
    public void WeldRule_CaptureGroupsPairTheAttachedPlateWithItsOwnBasePlate()
    {
        // Two stacked tees: each B welds to its own A only when Onto uses "$1".
        var (a, b) = Tee();
        var rule = new ArchWeldRule { Name = "t", Parts = @"^(P\d)-B$", Onto = @"^$1-A$" };
        var m = System.Text.RegularExpressions.Regex.Match("P2-B", rule.Parts);
        Assert.Equal("^P2-A$", m.Result(rule.Onto));
        Assert.NotEmpty(WeldFinder.TJoint(a, b, 1e-3));
    }
}
