using SBM.Core.Checks;
using SBM.Core.Geometry;
using SBM.Core.Misc;
using SBM.Core.Solids;
using Xunit;

namespace SBM.Tests;

public class SteelClashCheckTests
{
    private static double In(double x) => x * 0.0254;

    private static Mesh Box(double x0, double x1, double y0, double y1, double z0, double z1)
    {
        var m = new Mesh();
        // far from the origin, like a georeferenced model
        var f = new Frame3(new Vec3(262000, 4568000, 300), new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1));
        MeshBuilder.AddBox(m, f, x0, x1, y0, y1, z0, z1);
        return m;
    }

    private static ClashReport Run(params (string kind, Mesh mesh)[] items) =>
        SteelClashCheck.Run("test", items.Select((t, i) => new ClashItem(t.kind, $"P{i}", "A", "", t.mesh)).ToList(), new ClashOptions());

    [Fact]
    public void CrossingPlates_AreAClash_WithDepth()
    {
        // 1" plate through a 1/2" plate: depth = half the thinner plate
        var r = Run(("FlangePlate", Box(0, 1, 0, 0.3, 0, In(0.5))), ("Stiffener", Box(0.4, 0.4 + In(1), -0.1, 0.4, -0.2, 0.2)));
        Assert.Single(r.Clashes);
        Assert.InRange(r.Clashes[0].Depth, In(0.24), In(0.51));
        Assert.Equal("FlangePlate x Stiffener", r.Clashes[0].CategoryPair);
    }

    [Fact]
    public void FayingSurfaces_AndSmallOverlap_AreNotClashes()
    {
        // plate on a flange (touching) and a 1/32" overlap (below the 1/16" tolerance)
        var r = Run(("FlangePlate", Box(0, 1, 0, 0.3, 0, In(1))), ("SplicePlate", Box(0.2, 0.6, 0, 0.3, In(1), In(1.5))),
            ("Gusset", Box(0.7, 0.9, 0, 0.3, In(1) - In(1.0 / 32), In(2))));
        Assert.Empty(r.Clashes);
    }

    [Fact]
    public void BoltShankInLargerHole_IsNotAClash_ButInSolidPlateIs()
    {
        var plate = Box(-0.1, 0.1, -0.1, 0.1, 0, In(0.75));
        var o = new Vec3(262000, 4568000, 300);
        var shank = MiscMesh.Cylinder(o + new Vec3(0, 0, -0.05), o + new Vec3(0, 0, 0.08), In(0.5));
        Assert.Single(Run(("Plate", plate), ("Bolt", shank)).Clashes);
        var holed = new Mesh();
        PlateBuilder.AddPlanar(holed, new[] { new Vec2(-0.1, -0.1), new Vec2(0.1, -0.1), new Vec2(0.1, 0.1), new Vec2(-0.1, 0.1) },
            new[] { new Hole(Vec2.Zero, In(1.0625)) }, new Frame3(o, new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1)), 0, In(0.75));
        Assert.Empty(Run(("Plate", holed), ("Bolt", shank)).Clashes);
    }

    [Fact]
    public void MitredPipe_IsClosed_AndHasNoKinkOverlap()
    {
        var pts = new[] { new Vec3(0, 0, 10), new Vec3(0, 0, 5), new Vec3(3, 0, 4.76), new Vec3(3, 0, 0) };
        var pipe = MiscMesh.Pipe(pts, 0.16, 0.15);
        double v1 = 0, v2 = 0;
        var a = new Vec3(-5, -5, -5); var b = new Vec3(5, 5, 20);
        for (int i = 0; i < pipe.Indices.Count; i += 3)
        {
            Vec3 P(int k) => pipe.Positions[pipe.Indices[i + k]];
            v1 += Vec3.Dot(P(0) - a, Vec3.Cross(P(1) - a, P(2) - a));
            v2 += Vec3.Dot(P(0) - b, Vec3.Cross(P(1) - b, P(2) - b));
        }
        Assert.True(Math.Abs(v1 - v2) < 1e-6 * Math.Abs(v1));     // closed
        double len = 5 + Math.Sqrt(9 + 0.24 * 0.24) + 4.76;
        double expect = Math.PI * (0.16 * 0.16 - 0.15 * 0.15) * len;
        Assert.InRange(Math.Abs(v1) / 6, expect * 0.97, expect * 1.03);   // mitred joints: no overlap / gap at the bends
    }
}
