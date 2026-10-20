using SBM.Core.Drawing;
using SBM.Core.Drawing.Projection;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using Xunit;

namespace SBM.Tests;

/// <summary>3D -> 2D projection with hidden-line removal (ModelProjector): simple solids with known visible / hidden edges.</summary>
public class ProjectionTests
{
    private static readonly Vec3 Far = new(262302.0, 4568371.0, 300.0);   // georeferenced coordinates (precision check)

    /// <summary>Unit cube [0,1]^3 + Far with a square through-hole along Z (x, y in [a, b]), as a plain triangle mesh (no recipe).</summary>
    private static Mesh CubeWithSquareHole(double a = 0.3, double b = 0.7)
    {
        var m = new Mesh();
        Vec3 P(double x, double y, double z) => Far + new Vec3(x, y, z);
        void Quad(Vec3 p0, Vec3 p1, Vec3 p2, Vec3 p3) { int i = m.Add(p0), j = m.Add(p1), k = m.Add(p2), l = m.Add(p3); m.Tri(i, j, k); m.Tri(i, k, l); }
        // Outer walls (outward CCW).
        Quad(P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), P(0, 0, 1));
        Quad(P(1, 0, 0), P(1, 1, 0), P(1, 1, 1), P(1, 0, 1));
        Quad(P(1, 1, 0), P(0, 1, 0), P(0, 1, 1), P(1, 1, 1));
        Quad(P(0, 1, 0), P(0, 0, 0), P(0, 0, 1), P(0, 1, 1));
        // Hole walls (normals into the hole).
        Quad(P(a, a, 0), P(a, a, 1), P(b, a, 1), P(b, a, 0));
        Quad(P(b, a, 0), P(b, a, 1), P(b, b, 1), P(b, b, 0));
        Quad(P(b, b, 0), P(b, b, 1), P(a, b, 1), P(a, b, 0));
        Quad(P(a, b, 0), P(a, b, 1), P(a, a, 1), P(a, a, 0));
        // Top and bottom rings: 4 trapezoids each.
        foreach (double z in new[] { 0.0, 1.0 })
        {
            var o = new[] { P(0, 0, z), P(1, 0, z), P(1, 1, z), P(0, 1, z) };
            var h = new[] { P(a, a, z), P(b, a, z), P(b, b, z), P(a, b, z) };
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                if (z > 0.5) Quad(o[i], o[j], h[j], h[i]); else Quad(o[j], o[i], h[i], h[j]);
            }
        }
        return m;
    }

    private static ProjectionItem Item(Mesh m, string key = "A", string layer = "3D-TEST") => new() { Mesh = m, Layer = layer, Key = key };

    private static (double vis, double hid, int nVis, int nHid) Lengths(ProjectionResult r)
    {
        double vis = 0, hid = 0; int nv = 0, nh = 0;
        foreach (var l in r.Drawing.Layers)
            foreach (var pl in l.Polylines)
            {
                double len = 0;
                for (int i = 0; i + 1 < pl.Points.Length; i++) len += (pl.Points[i + 1].XY - pl.Points[i].XY).Length;
                if (pl.Closed) len += (pl.Points[0].XY - pl.Points[^1].XY).Length;
                if (l.Name.EndsWith(ModelProjector.HiddenSuffix)) { hid += len; nh++; } else { vis += len; nv++; }
            }
        return (vis, hid, nv, nh);
    }

    [Fact]
    public void CubeWithHole_FrontView_VisibleOutline_HiddenHoleWalls()
    {
        var r = ModelProjector.Project(new[] { Item(CubeWithSquareHole()) }, new OrthographicView(Far, new Vec3(0, 1, 0), new Vec3(0, 0, 1)));
        var (vis, hid, nVis, nHid) = Lengths(r);
        Assert.Equal(4.0, vis, 6);          // the square outline (hole edges on the top / bottom faces coincide with it)
        Assert.Equal(2.0, hid, 6);          // two hidden hole lines (front and back hole edges coincide)
        Assert.Equal(1, nVis);              // chained into one closed polyline
        Assert.Equal(2, nHid);
        Assert.All(r.Drawing.Layers.SelectMany(l => l.Polylines), p => Assert.Equal("A", p.Tag));
        Assert.Contains(r.Drawing.Layers, l => l.Name == "3D-TEST-HIDDEN" && l.Style.LineStyle == LineStyle.Hidden);
    }

    [Fact]
    public void CubeWithHole_TopView_OutlineAndHole_NoHidden()
    {
        var r = ModelProjector.Project(new[] { Item(CubeWithSquareHole()) }, OrthographicView.Top());
        var (vis, hid, nVis, _) = Lengths(r);
        Assert.Equal(4 + 4 * 0.4, vis, 6);
        Assert.Equal(0, hid, 6);            // the bottom edges lie exactly under the top edges: drawn once, visible
        Assert.Equal(2, nVis);
        // Oblique view: some hidden lines appear, nothing visible is lost.
        var r2 = ModelProjector.Project(new[] { Item(CubeWithSquareHole()) }, new OrthographicView(Far, new Vec3(1, 2, -3), new Vec3(0, 0, 1)));
        var (v2, h2, _, _) = Lengths(r2);
        Assert.True(h2 > 0.1 && v2 > 4, $"vis {v2}, hidden {h2}");
        // Depth buffer agrees with the exact method within a few pixels.
        var r3 = ModelProjector.Project(new[] { Item(CubeWithSquareHole()) }, new OrthographicView(Far, new Vec3(1, 2, -3), new Vec3(0, 0, 1)),
            new ProjectionOptions { Method = HlrMethod.DepthBuffer, DepthBufferPixels = 2048 });
        var (v3, h3, _, _) = Lengths(r3);
        Assert.Equal(v2, v3, 1);
        Assert.Equal(h2, h3, 1);
    }

    [Fact]
    public void RecipePlateWithRoundHole_MatchesMesh_NoTessellationLines()
    {
        var m = new Mesh();
        var f = new Frame3(Far, new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1));
        PlateBuilder.AddPlanar(m, new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 0.5), new Vec2(0, 0.5) }, new[] { new Hole(new Vec2(0.5, 0.25), 0.1) }, f, 0, 0.02);
        Assert.True(m.HasExactSolids);
        var top = OrthographicView.Top();
        var exact = ModelProjector.Project(new[] { Item(m) }, top);
        var mesh = ModelProjector.Project(new[] { Item(m) }, top, new ProjectionOptions { UseRecipes = false });
        var (ve, he, _, _) = Lengths(exact);
        var (vm, hm, _, _) = Lengths(mesh);
        double circle = 16 * 2 * (0.05 / Math.Cos(Math.PI / 16)) * Math.Sin(Math.PI / 16);
        Assert.Equal(3 + circle, ve, 6);
        Assert.Equal(ve, vm, 6);
        Assert.Equal(0, he, 6);
        Assert.Equal(0, hm, 6);
        // Front view: the hole is seen edge-on -> two hidden silhouette lines through the thickness.
        var front = new OrthographicView(Far, new Vec3(0, 1, 0), new Vec3(0, 0, 1));
        var (vf, hf, _, _) = Lengths(ModelProjector.Project(new[] { Item(m) }, front));
        Assert.Equal(2 * 1 + 2 * 0.02, vf, 6);
        Assert.Equal(2 * 0.02, hf, 6);
    }

    [Fact]
    public void CoplanarTriangles_WithTJunction_AreMerged()
    {
        // Box whose top face is split into two quads, one of them split again at a mid-edge vertex (T-junction).
        var m = new Mesh();
        Vec3 P(double x, double y, double z) => Far + new Vec3(x, y, z);
        void Tri(Vec3 a, Vec3 b, Vec3 c) => m.Tri(m.Add(a), m.Add(b), m.Add(c));
        void Quad(Vec3 a, Vec3 b, Vec3 c, Vec3 d) { Tri(a, b, c); Tri(a, c, d); }
        Quad(P(0, 0, 0), P(0, 1, 0), P(2, 1, 0), P(2, 0, 0));          // bottom (down)
        Quad(P(0, 0, 0), P(2, 0, 0), P(2, 0, 1), P(0, 0, 1));
        Quad(P(2, 0, 0), P(2, 1, 0), P(2, 1, 1), P(2, 0, 1));
        Quad(P(2, 1, 0), P(0, 1, 0), P(0, 1, 1), P(2, 1, 1));
        Quad(P(0, 1, 0), P(0, 0, 0), P(0, 0, 1), P(0, 1, 1));
        Quad(P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1));          // top left
        Tri(P(1, 0, 1), P(2, 0, 1), P(2, 1, 1));                       // top right: vertex (1, 0.5) on the left quad's edge
        Tri(P(1, 0, 1), P(2, 1, 1), P(1, 0.5, 1));
        Tri(P(1, 0.5, 1), P(2, 1, 1), P(1, 1, 1));
        var e = FeatureEdges.FromMesh(m, 20);
        Assert.True(e.Closed);
        double sharp = e.Edges.Where(x => x.Kind == EdgeKind.Sharp).Sum(x => (x.B - x.A).Length);
        Assert.Equal(2 * (2 + 1) * 2 + 4 * 1, sharp, 6);                // 12 box edges, no line across the top face
        var (vis, hid, _, _) = Lengths(ModelProjector.Project(new[] { Item(m) }, OrthographicView.Top()));
        Assert.Equal(6, vis, 6);
        Assert.Equal(0, hid, 6);
    }

    [Fact]
    public void DepthSlab_CutsAndCaps_AndDropsWhatIsBeyond()
    {
        var big = new Mesh();
        MeshBuilder.AddBox(big, new Frame3(Far, new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1)), 0, 1, 0, 1, 0, 1);
        var small = new Mesh();
        MeshBuilder.AddBox(small, new Frame3(Far, new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1)), 0.4, 0.6, 0.4, 0.6, -1, -0.5);
        var items = new[] { Item(big, "big"), Item(small, "small", "3D-SMALL") };
        var top = new OrthographicView(Far + new Vec3(0, 0, 1), new Vec3(0, 0, -1), new Vec3(0, 1, 0));   // depth = 1 - z
        // No slab: the small box is hidden under the big one.
        var r0 = ModelProjector.Project(items, top);
        Assert.Equal(0.8, r0.Drawing.Layers.Where(l => l.Name == "3D-SMALL-HIDDEN").Sum(l => l.Polylines.Sum(Len)), 6);
        // Near plane through the big box (z = 0.5): its cut outline is drawn, the cap still hides the small box.
        var r1 = ModelProjector.Project(items, top.WithSlab(0.5, null));
        Assert.Equal(4, r1.Drawing.Layers.Where(l => l.Name == "3D-TEST").Sum(l => l.Polylines.Sum(Len)), 6);
        Assert.Equal(0.8, r1.Drawing.Layers.Where(l => l.Name == "3D-SMALL-HIDDEN").Sum(l => l.Polylines.Sum(Len)), 6);
        Assert.DoesNotContain(r1.Drawing.Layers, l => l.Name == "3D-SMALL");
        // Far plane at z = 0.2: the small box is beyond and not drawn at all.
        var r2 = ModelProjector.Project(items, top.WithSlab(null, 0.8));
        Assert.DoesNotContain(r2.Drawing.Layers, l => l.Name.StartsWith("3D-SMALL") && l.Polylines.Count > 0);
        // Slab entirely past the big box: the small box is visible.
        var r3 = ModelProjector.Project(items, top.WithSlab(1.2, null));
        Assert.Equal(0.8, r3.Drawing.Layers.Where(l => l.Name == "3D-SMALL").Sum(l => l.Polylines.Sum(Len)), 6);
        Assert.Equal("small", r3.Pick(new Vec2(r3.Drawing.Layers.First(l => l.Name == "3D-SMALL").Polylines[0].Points[0].X, r3.Drawing.Layers.First(l => l.Name == "3D-SMALL").Polylines[0].Points[0].Y), 0.01)?.Key);
    }

    private static double Len(Polyline3 pl)
    {
        double len = 0;
        for (int i = 0; i + 1 < pl.Points.Length; i++) len += (pl.Points[i + 1].XY - pl.Points[i].XY).Length;
        if (pl.Closed) len += (pl.Points[0].XY - pl.Points[^1].XY).Length;
        return len;
    }

    [Fact]
    public void DevelopedView_CurvedWall_UnrollsToStraightLines()
    {
        // Wall 0.1 thick, 1 high, following a R = 50 arc (center at Far, CCW) over 60 degrees; path = the arc itself.
        const double R = 50, sweep = Math.PI / 3;
        int n = 61;
        var path = new List<Vec3>(); var st = new List<double>();
        var rings = new List<Vec3[]>();
        for (int i = -1; i <= n; i++)
        {
            double th = sweep * i / (n - 1);
            var rad = new Vec3(Math.Cos(th), Math.Sin(th), 0);
            path.Add(Far + rad * R); st.Add(R * th);   // path one sample beyond each end (end normals = radial)
            if (i >= 0 && i < n) rings.Add(new[] { Far + rad * (R + 0.2), Far + rad * (R + 0.3), Far + rad * (R + 0.3) + new Vec3(0, 0, 1), Far + rad * (R + 0.2) + new Vec3(0, 0, 1) });
        }
        var wall = new Mesh();
        MeshBuilder.AddLoft(wall, new[] { new Vec2(0, 0), new Vec2(0.1, 0), new Vec2(0.1, 1), new Vec2(0, 1) }, rings);
        var view = new DevelopedView(path, st);
        // A point outside the arc (right of the CCW path) maps to x = R theta, y = z, depth = -offset.
        var q = view.Map(Far + new Vec3(Math.Cos(0.5), Math.Sin(0.5), 0) * (R + 2) + new Vec3(0, 0, 7));
        Assert.Equal(R * 0.5, q.X, 3);
        Assert.Equal(Far.Z + 7, q.Y, 9);
        Assert.Equal(-2, q.Z, 2);   // chord sagitta 2 mm
        var r = ModelProjector.Project(new[] { Item(wall) }, view);
        var (vis, hid, _, _) = Lengths(r);
        // Seen from outside: top + bottom edges (outer face, developed at the path's x scale R/R... = arc length at the path) and the two end lines.
        double arc = R * sweep;
        double outer = arc * (R + 0.3) / R;   // edges at R + 0.3 develop to the path's station: x spans R * sweep
        Assert.Equal(2 * arc + 2 * 1, vis, 2);
        Assert.True(outer > arc);
        // The inner face's top / bottom edges coincide in the developed view; the ring (tessellation) lines are not drawn.
        Assert.Equal(0, hid, 2);
        foreach (var pl in r.Drawing.Layers.SelectMany(l => l.Polylines))
            foreach (var p in pl.Points) Assert.True(Math.Abs(p.Y - Far.Z) < 1e-6 || Math.Abs(p.Y - Far.Z - 1) < 1e-6 || Math.Abs(p.X) < 1e-6 || Math.Abs(p.X - arc) < 1e-3);
    }
}
