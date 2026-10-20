using SBM.Core.Geometry;
using Xunit;

namespace SBM.Tests;

/// <summary>
/// Viewport picking of merged meshes: a ray hit reports the triangle by its three VERTEX indices. With shared flat-shaded
/// vertices (SceneBuilder.ShareVertices: the two triangles of a quad share 2 vertices) vertex / 3 is an earlier triangle, so
/// the old pick selected and highlighted another element (user report 2026-10-20: a suspender socket picked, the rope / a
/// socket elsewhere highlighted; truss verticals / diagonals "mislabeled").
/// </summary>
public class PickTableTests
{
    private sealed record Part(string Mark, int Bridge);

    /// <summary>Merged layer as SceneBuilder lays it out with shared vertices: each part = 2 quads = 4 triangles on 8 vertices.</summary>
    private static (List<int> indices, PickTable table, List<Part> parts) SharedLayer()
    {
        // Same marks in two bridges (repeated truss units), one more part.
        var parts = new List<Part> { new("N-U3-L3", 0), new("N-U3-L4", 0), new("N-U3-L3", 1), new("SUSP-N15-R1-LS", 1) };
        var idx = new List<int>();
        var table = new PickTable();
        int v = 0, t = 0;
        foreach (var p in parts)
        {
            table.TriStart.Add(t);
            table.Tags.Add(p);
            for (int q = 0; q < 2; q++)
            {
                idx.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 }); // quad = 2 triangles on 4 shared vertices
                v += 4; t += 2;
            }
        }
        return (idx, table, parts);
    }

    [Fact]
    public void SharedVertices_HitTriangle_PicksThePartUnderTheCursor()
    {
        var (idx, table, parts) = SharedLayer();
        for (int tri = 0; tri < idx.Count / 3; tri++)
        {
            int a = idx[3 * tri], b = idx[3 * tri + 1], c = idx[3 * tri + 2];
            int found = PickTable.TriangleOf(idx, a, b, c);
            Assert.Equal(tri, found);
            var tag = table.Find(found, out int t0, out int t1);
            Assert.Same(parts[tri / 4], tag);
            // The highlight range is exactly this part's triangles (and nothing of the part with the same mark in the other bridge).
            Assert.Equal(4 * (tri / 4), t0);
            Assert.Equal(tri / 4 == parts.Count - 1 ? int.MaxValue : t0 + 4, t1);
        }
        // The old lookup (first vertex / 3) lands on another part for the later parts - the reported bug.
        int last = idx.Count / 3 - 1;
        Assert.NotSame(parts[^1], table.Find(idx[3 * last] / 3, out _, out _));
    }

    [Fact]
    public void UnsharedLayout_FastPath_AndRotatedCorners()
    {
        var idx = Enumerable.Range(0, 30).ToList(); // 10 triangles, own vertices (clipped meshes)
        Assert.Equal(7, PickTable.TriangleOf(idx, 21, 22, 23));
        Assert.Equal(7, PickTable.TriangleOf(idx, 22, 23, 21));
        Assert.Equal(-1, PickTable.TriangleOf(idx, 21, 23, 22)); // other winding = not this triangle
        Assert.Equal(-1, PickTable.TriangleOf(new List<int>(), 0, 1, 2));
    }
}
