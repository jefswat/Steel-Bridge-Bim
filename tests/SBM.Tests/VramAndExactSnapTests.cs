using SBM.Core.Geometry;
using SBM.Core.Geometry.Nurbs;
using SBM.Core.References;
using SBM.Core.Solids;
using Xunit;

namespace SBM.Tests;

public class VramAndExactSnapTests
{
    // ------------------------------------------------------------------ viewport memory planner

    private static VramInventory Inventory()
    {
        var inv = new VramInventory { Bridges = new Box2 { MinX = 1000, MinY = 1000, MaxX = 1400, MaxY = 1200 } };
        inv.BridgeNames.AddRange(new[] { "I-80", "Ramp G" });
        // bridge 0 big, bridge 1 smaller; primary + secondary + detail tiles
        inv.Items.Add(new VramItem("3D-DECK", VramGroup.Deck, 1_000_000, 1_000_000, Bridge: 0));
        inv.Items.Add(new VramItem("3D-STIFFENERS", VramGroup.Steel, 2_000_000, 2_000_000, Bridge: 0));
        inv.Items.Add(new VramItem("3D-DECK", VramGroup.Deck, 500_000, 500_000, Bridge: 1));
        inv.Items.Add(new VramItem("3D-STIFFENERS", VramGroup.Steel, 1_000_000, 1_000_000, Bridge: 1));
        for (int i = 0; i < 40; i++)
            inv.Items.Add(new VramItem("3D-REBAR-DECK", VramGroup.Rebar, 200_000, 200_000, Bridge: i % 2, Detail: true, Center: new Vec2(1000 + 10 * i, 1100)));
        inv.Terrains.Add(new VramTerrain("REF-DEM", 2200, 1600, 0, 0, 1, 1, 4096, 2976));
        return inv;
    }

    [Fact]
    public void Ladder_DegradesTerrainFirst_ThenDetail_ThenFarBridges()
    {
        var inv = Inventory();
        var s = new VramSettings();
        var ladder = VramPlanner.Ladder(inv, s);
        Assert.True(ladder.Count >= 8);
        Assert.True(ladder[0].IsFull);
        for (int i = 1; i < ladder.Count; i++) Assert.True(ladder[i].Estimated <= ladder[i - 1].Estimated, $"level {i} grows");
        // order: terrain clip, terrain strides, detail residency, far bridges
        Assert.True(ladder[1].TerrainClip > 0 && ladder[1].ImageMaxPixels == 2048 && !ladder[1].DetailResidency);
        Assert.Equal(2, ladder[2].TerrainStride);
        int detail = ladder.FindIndex(l => l.DetailResidency);
        int far = ladder.FindIndex(l => l.FarBridgeLod);
        Assert.True(detail > 2 && far > detail);
        Assert.Equal(0, ladder[^2].TerrainStride);
        Assert.True(ladder[^1].DropDetail);
        // the full estimate is the inventory total (terrain modelled from the grid)
        Assert.Equal(inv.Total + inv.Terrains[0].MeshBytes(1, Box2.Empty) + inv.Terrains[0].ImageBytes(0), ladder[0].Estimated);
        // choosing
        Assert.Equal(0, VramPlanner.Choose(ladder, long.MaxValue).Index);
        Assert.Equal(ladder.Count - 1, VramPlanner.Choose(ladder, 1).Index);
        var mid = VramPlanner.Choose(ladder, ladder[3].Estimated);
        Assert.True(mid.Index <= 3 && mid.Estimated <= ladder[3].Estimated);
        Assert.Contains("terrain", VramPlanner.Summary(ladder, ladder[3]));
        // a later minimum (after a GPU out-of-memory error) is honoured
        Assert.Equal(5, VramPlanner.Choose(ladder, long.MaxValue, 5).Index);
    }

    [Fact]
    public void TerrainCost_FollowsStrideAndClip()
    {
        var t = new VramTerrain("REF", 2001, 1001, 0, 0, 1, 1, 0, 0);
        long full = t.MeshBytes(1, Box2.Empty);
        Assert.Equal(2001L * 1001 * (52 + 24), full);
        Assert.InRange(t.MeshBytes(2, Box2.Empty) / (double)full, 0.24, 0.26);
        var half = new Box2 { MinX = -100, MinY = -100, MaxX = 1000, MaxY = 2000 };
        Assert.InRange(t.MeshBytes(1, half) / (double)full, 0.49, 0.51);
        Assert.Equal(0, t.ImageBytes(0));
        var img = new VramTerrain("REF", 10, 10, 0, 0, 1, 1, 4096, 2048);
        Assert.Equal(4096L * 2048 * 4 * 4 / 3, img.ImageBytes(0));
        Assert.Equal(2048L * 1024 * 4 * 4 / 3, img.ImageBytes(2048));
    }

    [Fact]
    public void ResidentDetail_KeepsTheLargestTilesWithinTheRadius()
    {
        var tiles = Enumerable.Range(0, 100).Select(i => new VramItem("3D-BOLTS", VramGroup.Bolts, 100 * (i + 1), 0, Detail: true, Center: new Vec2(i * 40 + 5, 5))).ToList();
        // radius 0 -> pi tiles -> 4 largest
        Assert.Equal((100 + 99 + 98 + 97) * 100L * 52, VramPlanner.ResidentDetail(tiles, 0));
        Assert.Equal(tiles.Sum(t => t.Bytes), VramPlanner.ResidentDetail(tiles, 1000));
        // two buffers in one tile count together
        var two = new[] { new VramItem("L", VramGroup.Bolts, 10, 0, Detail: true, Center: new Vec2(1, 1)), new VramItem("L", VramGroup.Bolts, 10, 0, Detail: true, Center: new Vec2(2, 2)) };
        Assert.Equal(20 * 52, VramPlanner.ResidentDetail(two, 0));
    }

    [Fact]
    public void ElevationGrid_ClippedDisplayMesh()
    {
        int nx = 101, ny = 81;
        var z = new float[nx * ny];
        for (int k = 0; k < z.Length; k++) z[k] = k % 7;
        var g = new ElevationGrid { Nx = nx, Ny = ny, Cell = 1, X0 = 500, Y0 = 200, Z = z };
        var full = g.ToMesh(1);
        Assert.Equal(nx * ny, full.Positions.Count);
        var box = new Box2 { MinX = 520, MinY = 210, MaxX = 540.5, MaxY = 230 };
        var clipped = g.ToMesh(1, box);
        Assert.Equal(22 * 21, clipped.Positions.Count);      // 520..541 x 210..230
        Assert.All(clipped.Positions, p => Assert.True(box.Inflate(1).Contains(p.XY)));
        // elevations unchanged
        foreach (var p in clipped.Positions) Assert.Equal(g.At((int)(p.X - 500), (int)(p.Y - 200)), p.Z, 6);
        var coarse = g.ToMesh(4, box);
        Assert.True(coarse.Positions.Count < clipped.Positions.Count / 5);
        Assert.Empty(g.ToMesh(1, new Box2 { MinX = 0, MinY = 0, MaxX = 10, MaxY = 10 }).Positions);
        // TIN clip keeps triangles touching the box
        var tin = TerrainDisplay.Clip(full, box);
        Assert.True(tin.TriangleCount > 0 && tin.TriangleCount < full.TriangleCount / 10);
    }

    // ------------------------------------------------------------------ exact measure snaps

    private const double R = 200;
    private static readonly Vec3 C = new(3000000, 1000000, 0);

    private static Vec3 Map(double s, double w, double t)
    {
        double a = s / R;
        var radial = new Vec3(Math.Cos(a), Math.Sin(a), 0);
        return C + radial * (R + w) + new Vec3(0, 0, 300 + 0.03 * s - 0.0004 * s * s + t);
    }

    /// <summary>Curved flange 0.5 x 0.05 on R 200 m with a vertical curve: exact NURBS + today's chorded mesh (1.5 m samples).</summary>
    private static (NurbsSolid solid, Mesh mesh) Plate()
    {
        var sw = NurbsSweep.Plate(0, 30, _ => -0.25, _ => 0.25, _ => 0, _ => 0.05, Map, Array.Empty<double>(), 1e-5, out _)!;
        var solid = sw.ToSolid("girder flange");
        var mesh = new Mesh();
        var ss = Enumerable.Range(0, 21).Select(i => i * 1.5).ToList();
        PlateBuilder.AddLongPlate(mesh, 0, 30, ss, _ => -0.25, _ => 0.25, _ => 0, _ => 0.05, Array.Empty<Hole>(), (s, w, t) =>
        {
            int i = Math.Min((int)(s / 1.5), 19);
            double f = (s - ss[i]) / 1.5;
            return Vec3.Lerp(Map(ss[i], w, t), Map(ss[i + 1], w, t), f);
        });
        return (solid, mesh);
    }

    private static Vec3? RayMesh(Mesh m, Vec3 o, Vec3 d)
    {
        double best = double.MaxValue;
        for (int i = 0; i < m.Indices.Count; i += 3)
        {
            var a = m.Positions[m.Indices[i]]; var b = m.Positions[m.Indices[i + 1]]; var c = m.Positions[m.Indices[i + 2]];
            var e1 = b - a; var e2 = c - a;
            var p = Vec3.Cross(d, e2);
            double det = Vec3.Dot(e1, p);
            if (Math.Abs(det) < 1e-14) continue;
            var tv = o - a;
            double u = Vec3.Dot(tv, p) / det;
            if (u < 0 || u > 1) continue;
            var q = Vec3.Cross(tv, e1);
            double v = Vec3.Dot(d, q) / det;
            if (v < 0 || u + v > 1) continue;
            double t = Vec3.Dot(e2, q) / det;
            if (t > 0 && t < best) best = t;
        }
        return best < double.MaxValue ? o + d * best : null;
    }

    [Fact]
    public void ExactSnap_SideFace_RefinedOntoTheCylinder()
    {
        var (solid, mesh) = Plate();
        // horizontal ray toward the arc center at mid-chord (s = 0.75 m), hitting the outer side face w = +0.25
        double a = 0.75 / R;
        var radial = new Vec3(Math.Cos(a), Math.Sin(a), 0);
        var target = Map(0.75, 0.25, 0.025);
        var o = target + radial * 5;
        var d = -radial;
        var hit = RayMesh(mesh, o, d)!.Value;
        double sag = (hit - target).Length;
        Assert.InRange(sag, 1e-3, 2e-3);                     // the chorded mesh lies ~1.4 mm inside the arc
        var r = NurbsSnap.Refine(new[] { solid }, o, d, hit)!;
        Assert.NotNull(r);
        Assert.True(r.OnRay && r.Curved);
        Assert.True((r.Point - target).Length < 2e-5, $"refined {(r.Point - target).Length * 1000:0.000} mm from the exact surface");
        double radius = (r.Point - C).XY.Length;
        Assert.InRange(radius, R + 0.25 - 2e-5, R + 0.25 + 2e-5);
        // on the ray
        var v = r.Point - o;
        Assert.True((v - d * Vec3.Dot(v, d)).Length < 1e-8);
        Assert.InRange(r.Shift, 1e-3, 2e-3);
        Assert.True(Vec3.Dot(r.Normal, radial) > 0.999);       // outward
    }

    [Fact]
    public void ExactSnap_TopFace_FollowsTheVerticalCurve()
    {
        var (solid, mesh) = Plate();
        // vertical ray at a plan point between two chord samples
        var plan = Map(9.75, 0.1, 0);
        var o = new Vec3(plan.X, plan.Y, 400);
        var d = new Vec3(0, 0, -1);
        var hit = RayMesh(mesh, o, d)!.Value;
        var r = NurbsSnap.Refine(new[] { solid }, o, d, hit)!;
        Assert.NotNull(r);
        // exact top at that plan point: invert the plan map
        var rel = (r.Point - C).XY;
        double s = Math.Atan2(rel.Y, rel.X) * R;
        double zExact = 300 + 0.03 * s - 0.0004 * s * s + 0.05;
        Assert.True(Math.Abs(r.Point.Z - zExact) < 2e-5, $"dz {(r.Point.Z - zExact) * 1000:0.000} mm");
        Assert.Equal(plan.X, r.Point.X, 6);
        Assert.Equal(plan.Y, r.Point.Y, 6);
        Assert.True(Math.Abs(hit.Z - zExact) > Math.Abs(r.Point.Z - zExact));
    }

    [Fact]
    public void ExactSnap_PlanarEnd_AndOff()
    {
        var (solid, mesh) = Plate();
        // ray along the girder into the start end face (s = 0, planar)
        var p = Map(0, 0.05, 0.02);
        var o = p + new Vec3(0, -3, 0);
        var d = new Vec3(0, 1, 0);
        var hit = RayMesh(mesh, o, d)!.Value;
        var r = NurbsSnap.Refine(new[] { solid }, o, d, hit)!;
        Assert.NotNull(r);
        Assert.False(r.Curved);
        Assert.True((r.Point - hit).Length < 1e-9);
        // setting OFF (or a part without NURBS): no refinement, the mesh snap stays as it is
        Assert.Null(NurbsSnap.TryRefine(false, new[] { solid }, o, d, hit));
        Assert.Null(NurbsSnap.TryRefine(true, null, o, d, hit));
        Assert.NotNull(NurbsSnap.TryRefine(true, new[] { solid }, o, d, hit));
        // far from the solid: nothing
        Assert.Null(NurbsSnap.Refine(new[] { solid }, o, d, hit + new Vec3(0, 0, 1)));
    }
}
