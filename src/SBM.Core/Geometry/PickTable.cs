namespace SBM.Core.Geometry;

/// <summary>Maps triangles of a merged (batched) mesh back to the objects they came from, for picking.</summary>
public sealed class PickTable
{
    public List<int> TriStart { get; } = new();
    public List<object> Tags { get; } = new();

    public object? Find(int triangle, out int start, out int end)
    {
        int i = TriStart.BinarySearch(triangle);
        if (i < 0) i = ~i - 1;
        start = end = 0;
        if (i < 0 || i >= Tags.Count) return null;
        start = TriStart[i];
        end = i + 1 < TriStart.Count ? TriStart[i + 1] : int.MaxValue;
        return Tags[i];
    }

    /// <summary>
    /// Index of the triangle whose corners are the VERTEX indices <paramref name="a"/>, <paramref name="b"/>, <paramref name="c"/>
    /// (a ray hit reports the triangle by its three vertex indices, not by its position in the index buffer). The vertex index / 3
    /// is the triangle only when every triangle has its own three vertices; merged meshes share vertices between coplanar
    /// neighbours (SceneBuilder.ShareVertices), where vertex / 3 lands on an EARLIER triangle - often of another part - so the
    /// wrong element was selected and highlighted. Fast path for the unshared layout, else a scan from the earliest possible
    /// triangle (a triangle adds at most 3 vertices, so the one using vertex v is at index >= v / 3). -1 = not found.
    /// </summary>
    public static int TriangleOf(IList<int> indices, int a, int b, int c)
    {
        int n = indices.Count / 3;
        if (n == 0) return -1;
        int guess = a / 3;
        if (guess < n && Same(indices, guess, a, b, c)) return guess;
        int from = Math.Min(n - 1, Math.Max(0, Math.Min(a, Math.Min(b, c)) / 3));
        for (int t = from; t < n; t++)
            if (Same(indices, t, a, b, c)) return t;
        for (int t = 0; t < from; t++)
            if (Same(indices, t, a, b, c)) return t;
        return -1;
    }

    /// <summary>Same three corners in the same cyclic order (the hit reports them in buffer order).</summary>
    private static bool Same(IList<int> ind, int t, int a, int b, int c)
    {
        int i0 = ind[3 * t], i1 = ind[3 * t + 1], i2 = ind[3 * t + 2];
        return (i0 == a && i1 == b && i2 == c) || (i0 == b && i1 == c && i2 == a) || (i0 == c && i1 == a && i2 == b);
    }
}
